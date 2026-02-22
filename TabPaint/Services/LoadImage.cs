using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using XamlAnimatedGif; // 添加这一行
using SkiaSharp;
using Svg.Skia;
using TabPaint.Services;
//
//图片加载机制
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private readonly object _queueLock = new object();
        private string _pendingFilePath = null;  // “待办事项”：只存放最新的一个图片加载请求
        private bool _isProcessingQueue = false;   // 标志位：表示图像加载“引擎”是否正在工作中
        private CancellationTokenSource _loadImageCts;
        private CancellationTokenSource _progressCts;
        private readonly object _lockObj = new object();
        private bool _lazyLoad = false;
        private static readonly HashSet<string> AllowedExtensions = new HashSet<string>(AppConsts.ImageExtensions, StringComparer.OrdinalIgnoreCase);

        public async Task OpenImageAndTabs(string filePath, bool refresh = false, bool lazyload = false, bool forceFolderScan = false, bool nobackup = false)
        {
           
            _isLoadingImage = true;
            OnPropertyChanged("IsLoadingImage");
            try
            {
                bool autoLoad = SettingsManager.Instance.Current.AutoLoadFolderImages || forceFolderScan;
                if (autoLoad && _currentImageIndex == -1 && _currentFileExists)
                {
                    await ScanFolderImagesAsync(filePath);
                }
                else if (!autoLoad && _currentImageIndex == -1 && !IsVirtualPath(filePath))
                {
                    _imageFiles = new List<string> { filePath };
                }

                if (!nobackup) TriggerBackgroundBackup();

                if (IsVirtualPath(filePath) && !_imageFiles.Contains(filePath))
                {
                    _imageFiles.Add(filePath);
                }
                if (File.Exists(filePath))
                {
                    SettingsManager.Instance.AddRecentFile(filePath);
                }
                int newIndex = _imageFiles.IndexOf(filePath);
                _currentImageIndex = newIndex;
                RefreshTabPageAsync(_currentImageIndex, refresh);

                var current = FileTabs.FirstOrDefault(t => t.FilePath == filePath);

                if (current != null)
                {
                    foreach (var tab in FileTabs) tab.IsSelected = false;
                    current.IsSelected = true;
                    _currentTabItem = current;
                }
                UpdateImageBarVisibilityState();
                // --- 智能加载逻辑 ---
                string fileToLoad = filePath;
                bool isFileLoadedFromCache = false;
                string actualSourcePath = null; // 新增变量
                                                // 检查是否有缓存
                if (current != null)                              // 对于虚拟文件，如果它有 BackupPath (例如从 Session 恢复的)，必须读 BackupPath
                    if (_activeSaveTasks.TryGetValue(current.Id, out Task? pendingSave))
                    {
                        try { await pendingSave;  }
                catch (Exception ex)
                {
                    Logger.Error("Wait save failed", ex);
                }
                    }

                // 等待结束后，优先检查 MemorySnapshot，其次是 BackupPath
                if (current != null && (current.IsDirty || current.IsNew))
                {
                    if (current.MemorySnapshot != null)
                    {
                        // 直接从内存快照应用到 UI，跳过文件加载
                        await ApplyFullImageToUI(current.MemorySnapshot, fileToLoad, current.BackupPath ?? fileToLoad, "Memory", CancellationToken.None);

                        // 释放内存快照引用，交由新窗口的 _bitmap 接管
                        current.MemorySnapshot = null;

                        // 强制重置备份追踪，确保在新窗口切换走之前能触发一次备份
                        _lastBackedUpVersion = -1;

                        ResetDirtyTracker();
                        _savedUndoPoint = -1;
                        CheckDirtyState();
                        return;
                    }
                    else if (!string.IsNullOrEmpty(current.BackupPath) && File.Exists(current.BackupPath))
                    {
                        actualSourcePath = current.BackupPath;
                        isFileLoadedFromCache = true; // 标记从备份加载，需维持 Dirty
                    }
                }
                await LoadImage(fileToLoad, actualSourcePath, lazyload);

                if (!isFileLoadedFromCache)
                {
                    ResetDirtyTracker();
                }

                if (isFileLoadedFromCache)
                {
                    // 仅当标签页原先就是脏状态时，加载备份后才维持脏状态
                    if (current != null && current.IsDirty)
                    {
                        _savedUndoPoint = -1;
                    }
                    else
                    {
                        ResetDirtyTracker();
                    }
                    CheckDirtyState();
                }


            }
            finally
            {
                _isLoadingImage = false;
                OnPropertyChanged("IsLoadingImage");
            }
        }
        public void RequestImageLoad(string filePath)
        {
            lock (_queueLock)
            {

                _pendingFilePath = filePath;
                if (!_isProcessingQueue)
                {
                    _isProcessingQueue = true;
                    _ = ProcessImageLoadQueueAsync();
                }
            }
        }
  
        private async Task ProcessImageLoadQueueAsync()
        {
            _isLoadingImage = true;
            OnPropertyChanged("IsLoadingImage"); // 如果你有绑定属性，请通知界面

            try
            {
                while (true)
                {
                    string filePathToLoad;

                    // 进入临界区，检查并获取下一个任务
                    lock (_queueLock)
                    {
                        // 1. 检查是否还有待办事项
                        if (_pendingFilePath == null)
                        {
                            _isProcessingQueue = false;
                            break; // 退出循环
                        }
                        filePathToLoad = _pendingFilePath;
                        _pendingFilePath = null;
                    }
                    await LoadAndDisplayImageInternalAsync(filePathToLoad);
                }
            }
            finally
            {
                _isLoadingImage = false;
                OnPropertyChanged("IsLoadingImage");
            }
        }
        private async Task LoadAndDisplayImageInternalAsync(string filePath)
        {
            try
            {
                OpenImageAndTabs(filePath, lazyload: true);

            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading image {filePath}", ex);
            }
        }

        private async Task ScanFolderImagesAsync(string filePath)
        {
            try
            {
                if (IsVirtualPath(filePath) || string.IsNullOrEmpty(filePath)) return;

                string folder = System.IO.Path.GetDirectoryName(filePath)!;
                if (!File.Exists(_currentFilePath) && !Directory.Exists(_currentFilePath)) { MainImageBar.IsSingleTabMode = true; }
                var sortedFiles = await Task.Run(() =>
                {
                    return Directory.EnumerateFiles(folder)
                        .Where(f =>
                        {
                            var ext = System.IO.Path.GetExtension(f);
                            return ext != null && AllowedExtensions.Contains(ext);
                        })
                        .OrderBy(f => f, NaturalStringComparer.Default)
                        .ToList();
                });

                var virtualPaths = FileTabs.Where(t => IsVirtualPath(t.FilePath))
                                            .Select(t => t.FilePath)
                                            .ToList();

                // 预分配内存
                var combinedFiles = new List<string>(virtualPaths.Count + sortedFiles.Count);
                combinedFiles.AddRange(virtualPaths);
                combinedFiles.AddRange(sortedFiles);

                _imageFiles = combinedFiles;
                _currentImageIndex = _imageFiles.IndexOf(filePath);

            }
            catch (Exception ex)
            {
                Logger.Error("Scan folder images failed", ex);
            }
        }
   

        private BitmapSource DecodePreviewBitmap(Stream stream, CancellationToken token)
        {
            if (token.IsCancellationRequested) return null;
            try
            {
                // 使用 BitmapCacheOption.OnLoad 确保流关闭后数据依然可用
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
                int frameIndex = GetLargestFrameIndex(decoder);
                var frame = decoder.Frames[frameIndex];

                if (decoder.Frames.Count > 1)
                {
                    // 对于多帧（如ICO），立即转换为 Bgra32 并手动拷贝像素，确保脱离流依赖并保留透明度
                    var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                    int w = converted.PixelWidth;
                    int h = converted.PixelHeight;
                    var stride = w * 4;
                    byte[] pixels = new byte[stride * h];
                    converted.CopyPixels(pixels, stride, 0);

                    var wb = new WriteableBitmap(w, h, converted.DpiX, converted.DpiY, PixelFormats.Bgra32, null);
                    wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
                    wb.Freeze();
                    return wb;
                }

                int originalWidth = frame.PixelWidth;

                // 重置流位置以供 BitmapImage 读取
                stream.Position = 0;

                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;

                if (originalWidth > AppConsts.PreviewDecodeWidth) img.DecodePixelWidth = AppConsts.PreviewDecodeWidth;
                img.StreamSource = stream;
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch { return null;}
        }
        private BitmapSource DecodeFullResBitmap(Stream stream, CancellationToken token)
        {
            if (token.IsCancellationRequested) return null;
            if (SettingsManager.Instance.Current.EnableIccColorCorrection)
            {
                try
                {
                    var skiaBitmap = DecodeWithSkiaAndIcc(stream);
                    if (skiaBitmap != null) return skiaBitmap;
                }
                catch (Exception ex){  Debug.WriteLine($"Skia ICC Decode failed, falling back to WPF: {ex.Message}");  }
            }
            try
            {
                stream.Position = 0;
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
                int frameIndex = GetLargestFrameIndex(decoder);
                var frame = decoder.Frames[frameIndex];

                if (decoder.Frames.Count > 1)
                {
                    var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                    int w = converted.PixelWidth;
                    int h = converted.PixelHeight;
                    var stride = w * 4;
                    byte[] pixels = new byte[stride * h];
                    converted.CopyPixels(pixels, stride, 0);

                    var wb = new WriteableBitmap(w, h, converted.DpiX, converted.DpiY, PixelFormats.Bgra32, null);
                    wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
                    wb.Freeze();
                    return wb;
                }

                int originalWidth = frame.PixelWidth;
                int originalHeight = frame.PixelHeight;

                stream.Position = 0; // 重置流位置以重新读取

                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                img.StreamSource = stream;

                const int maxSize = (int)AppConsts.MaxCanvasSize;
                if (originalWidth > maxSize || originalHeight > maxSize)
                {
                    if (originalWidth >= originalHeight) img.DecodePixelWidth = maxSize;
                    else img.DecodePixelHeight = maxSize;
                    Dispatcher.Invoke(() => ShowToast("L_Toast_ImageTooLarge"));
                }

                img.EndInit();
                img.Freeze();
                return img;
            }
            catch
            {
                return null;
            }
        }

        private Task<(int Width, int Height)?> GetImageDimensionsAsync(Stream stream, string filePath)
        {
            return Task.Run(() =>
            {
                try
                {
                    stream.Position = 0;
                    string ext = System.IO.Path.GetExtension(filePath)?.ToLower();
                    if (ext == ".svg")
                    {
                        using var svg = new SKSvg();
                        svg.Load(stream);
                        if (svg.Picture != null)  // 检查是否加载成功
                        {
                            int w = (int)svg.Picture.CullRect.Width;  // 获取画布尺寸
                            int h = (int)svg.Picture.CullRect.Height;
                            if (w <= 0) w = AppConsts.FallbackImageWidth;
                            if (h <= 0) h = AppConsts.FallbackImageHeight;

                            return ((int Width, int Height)?)(w, h);
                        }
                        return null;
                    }
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);

                    if (decoder.Frames != null && decoder.Frames.Count > 0)
                    {
                        int index = GetLargestFrameIndex(decoder);
                        return ((int Width, int Height)?)(decoder.Frames[index].PixelWidth, decoder.Frames[index].PixelHeight);
                    }
                    return null;
                }
            catch (Exception ex)
            {
                Logger.Error($"GetDimensions Error for {filePath}", ex);
                return null;
            }
            });
        }


        private async Task LoadImage(string filePath, string? sourcePath = null, bool lazyload = false)
        {
            // 1. 初始化与取消旧任务
            _isCurrentFileGif = false;
            GifPlayerImage.Visibility = Visibility.Collapsed;

            _loadImageCts?.Cancel();
            _loadImageCts = new CancellationTokenSource();
            var token = _loadImageCts.Token;

            StopProgressSimulation(); // 封装了取消 _progressCts 的逻辑

            // 2. 路径校验与文件准备
            var validationResult = await ValidateAndGetPath(filePath, sourcePath);
            if (!validationResult.IsValid) return; // 校验失败，内部已调用 LoadBlankCanvasAsync 或 Toast
            string fileToRead = validationResult.PathToRead;

            try
            {
                // 3. 使用 FileStream 替代 ReadAllBytesAsync
                using var fs = new FileStream(fileToRead, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                long byteLength = fs.Length;

                await UpdateFileSizeInfo(byteLength); // 更新底部文件大小显示

                // 4. 获取尺寸并处理进度条逻辑
                var dimensions = await GetImageDimensionsAsync(fs, filePath);
                if (token.IsCancellationRequested) return;

                if (dimensions == null)
                {
                    await LoadBlankCanvasAsync(filePath, LocalizationManager.GetString("L_Load_Reason_Header"));
                    return;
                }

                var (originalWidth, originalHeight) = dimensions.Value;
                if (_ctx != null)
                {
                    _ctx.FullImageWidth = originalWidth;
                    _ctx.FullImageHeight = originalHeight;
                }
                await HandleProgressDisplay(originalWidth, originalHeight, token); // 启动进度条逻辑

             
                bool hasThumbnail = await TryShowCachedThumbnail(filePath, token);   // 立即显示缓存的缩略图

                var decodingTasks = StartDecodingTasks(fs, filePath, token);

             
                var previewBitmap = await decodingTasks.PreviewTask;   //  [UI阶段 1] 显示预览图 
                if (!token.IsCancellationRequested && previewBitmap != null) await ApplyPreviewImageToUI(filePath, previewBitmap, token);
                var fullResBitmap = await decodingTasks.FullResTask;  //  [阶段 2] 等待完整大图并应用
                StopProgressSimulation(); // 停止进度条

                if (lazyload) await Task.Delay(AppConsts.ImageLoadDelayLazyMs, token).ConfigureAwait(false); // LazyLoad 延迟

                if (!token.IsCancellationRequested && fullResBitmap != null)
                {
                    // 获取元数据
                    string metadata = await GetImageMetadataInfoAsync(filePath, byteLength, fullResBitmap);

                    // 应用最终图像 (这是最重的 UI 操作)
                    await ApplyFullImageToUI(fullResBitmap, filePath, fileToRead, metadata, token);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"Image load for {filePath} was canceled.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading image {filePath}", ex);
                StopProgressSimulation();
                await LoadBlankCanvasAsync(filePath, LocalizationManager.GetString("L_Load_Reason_Corrupt"));
            }
        }

        private async Task<(bool IsValid, string PathToRead)> ValidateAndGetPath(string filePath, string? sourcePath)
        {
            string fileToRead = sourcePath ?? filePath;

            if (IsVirtualPath(filePath) && string.IsNullOrEmpty(sourcePath))
            {
                await LoadBlankCanvasAsync(filePath);
                return (false, null);
            }

            if (!File.Exists(fileToRead))
            {
                ShowToast("L_Toast_FileNotFound_Format");
                return (false, null);
            }

            try
            {
                if (new FileInfo(fileToRead).Length == 0)
                {
                    await LoadBlankCanvasAsync(filePath, LocalizationManager.GetString("L_Load_Reason_Empty"));
                    return (false, null);
                }
            }
            catch
            {
            }

            return (true, fileToRead);
        }
        private async Task HandleProgressDisplay(int width, int height, CancellationToken token)
        {
            long totalPixels = (long)width * height;
            bool showProgress = totalPixels > AppConsts.PerformanceScorePixelThreshold * PerformanceScore;

            if (showProgress)
            {
                _progressCts = new CancellationTokenSource();
                var pToken = _progressCts.Token;
                _ = SimulateProgressAsync(pToken, totalPixels, (msg) =>
                {
                    if (!pToken.IsCancellationRequested)
                        Dispatcher.Invoke(() => { if (_isLoadingImage) { _imageSize = msg; OnPropertyChanged(nameof(ImageSize)); } });
                });
            }
            else
            {
                Dispatcher.Invoke(() => {
                    _imageSize = $"{width}×{height}" + LocalizationManager.GetString("L_Main_Unit_Pixel");
                    OnPropertyChanged(nameof(ImageSize));
                });
            }
            if (totalPixels > AppConsts.HugeImagePixelThreshold * PerformanceScore)
            {
                await Task.Delay(AppConsts.ImageLoadDelayHugeMs, token);
            }
        }
        private void StopProgressSimulation()
        {
            if (_progressCts != null)
            {
                _progressCts.Cancel();
                _progressCts.Dispose();
                _progressCts = null;
            }
        }
        private (Task<BitmapSource> PreviewTask, Task<BitmapSource> FullResTask) StartDecodingTasks(FileStream fs, string filePath, CancellationToken token)
        {
            string extension = System.IO.Path.GetExtension(filePath)?.ToLower();
            string fileName = fs.Name;

            if (extension == ".svg")
            {
                var task = Task.Run<BitmapSource>(() =>
                {
                    try
                    {
                        using var svgFs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                        return DecodeSvg(svgFs, token);
                    }
                    catch { return null; }
                }, token);
                return (task, task);
            }
            else
            {
                var preview = Task.Run<BitmapSource>(() =>
                {
                    try
                    {
                        using var previewFs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                        return DecodePreviewBitmap(previewFs, token);
                    }
                    catch { return null; }
                }, token);

                var full = Task.Run<BitmapSource>(() =>
                {
                    try
                    {
                        using var fullFs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                        return DecodeFullResBitmap(fullFs, token);
                    }
                    catch { return null; }
                }, token);

                return (preview, full);
            }
        }
        private async Task<bool> TryShowCachedThumbnail(string filePath, CancellationToken token)
        {
            var tabItem = FileTabs.FirstOrDefault(t => t.FilePath == filePath);
            if (tabItem?.Thumbnail != null)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested) return;
                    UpdateUIForImage(tabItem.Thumbnail, filePath, isPreview: true);
                    _canvasResizer.UpdateUI();
                });
                return true;
            }
            return false;
        }
        private async Task ApplyPreviewImageToUI(string filePath, BitmapSource previewBitmap, CancellationToken token)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested) return;
                UpdateUIForImage(previewBitmap, filePath, isPreview: true);
            });
        }
        private async Task ApplyFullImageToUI(BitmapSource fullResBitmap, string filePath, string physicalPath, string metadataInfo, CancellationToken token)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested) return;
                _originalDpiX = fullResBitmap.DpiX;
                _originalDpiY = fullResBitmap.DpiY;
                _bitmap = CreateWriteableBitmap(fullResBitmap);
                if (_ctx != null)
                {
                    _ctx.FullImageWidth = _bitmap.PixelWidth;
                    _ctx.FullImageHeight = _bitmap.PixelHeight;
                }
                fullResBitmap = null;
                RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.NearestNeighbor);
                BackgroundImage.Source = _bitmap;
                this.CurrentImageFullInfo = metadataInfo;

                if (_surface == null) _surface = new CanvasSurface(_bitmap);
                else _surface.Attach(_bitmap);
                ResetEditorState(filePath);
                FitToWindow(needcanvasUpdateUI: true);
                HandleGifAnimation(physicalPath);

            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
        private void UpdateUIForImage(ImageSource source, string filePath, bool isPreview)
        {
            if (isPreview)
                RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.Linear);

            BackgroundImage.Source = source;
            _currentFileName = IsVirtualPath(filePath)
                ? (FileTabs.FirstOrDefault(t => t.FilePath == filePath)?.FileName ?? "未命名")
                : System.IO.Path.GetFileName(filePath);
            _currentFilePath = filePath;
            UpdateWindowTitle();

            if (isPreview)
            {
               if(_startupFinished) FitToWindow(needcanvasUpdateUI:true);
                CenterImage();
                BackgroundImage.InvalidateVisual();
            }
        }
        private WriteableBitmap CreateWriteableBitmap(BitmapSource source)
        {
            int width = source.PixelWidth;
            int height = source.PixelHeight;

            // 格式统一化
            if (source.Format != PixelFormats.Bgra32)
            {
                var formatted = new FormatConvertedBitmap();
                formatted.BeginInit();
                formatted.Source = source;
                formatted.DestinationFormat = PixelFormats.Bgra32;
                formatted.EndInit();
                source = formatted;
            }

            var wb = new WriteableBitmap(width, height, 96.0, 96.0, PixelFormats.Bgra32, null);
            wb.Lock();
            try
            {
                source.CopyPixels(new Int32Rect(0, 0, width, height), wb.BackBuffer, wb.BackBufferStride * height, wb.BackBufferStride);
                wb.AddDirtyRect(new Int32Rect(0, 0, width, height));
            }
            finally
            {
                wb.Unlock();
            }
            return wb;
        }
        private async Task UpdateFileSizeInfo(long byteLength)
        {
            string sizeString = FormatFileSize(byteLength);

            await Dispatcher.InvokeAsync(() =>  {   this.FileSize = sizeString; });
        }

        private void ResetEditorState(string filePath)
        {
            _undo?.ClearUndo();
            _undo?.ClearRedo();
            _isEdited = false;
            SetPreviewSlider();
            _imageSize = $"{_surface.Width}×{_surface.Height}" + LocalizationManager.GetString("L_Main_Unit_Pixel");
            OnPropertyChanged(nameof(ImageSize));
            _hasUserManuallyZoomed = false;
        }
        private void HandleGifAnimation(string physicalPath)
        {
            string ext = System.IO.Path.GetExtension(physicalPath)?.ToLower();
            _isCurrentFileGif = (ext == ".gif");

            if (_isCurrentFileGif && IsViewMode)
            {
                AnimationBehavior.SetSourceUri(GifPlayerImage, new Uri(physicalPath));
                GifPlayerImage.Visibility = Visibility.Visible;
                var controller = AnimationBehavior.GetAnimator(GifPlayerImage);
                controller?.Play();
            }
            else
            {
                AnimationBehavior.SetSourceUri(GifPlayerImage, null);
                GifPlayerImage.Visibility = Visibility.Collapsed;
                BackgroundImage.Visibility = Visibility.Visible;
            }
        }
        private async Task LoadBlankCanvasAsync(string filePath, string reason = null)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                int width = AppConsts.DefaultBlankCanvasWidth;
                int height = AppConsts.DefaultBlankCanvasHeight;
                _bitmap = new WriteableBitmap(width, height, 96.0, 96.0, PixelFormats.Bgra32, null);

                // 优化填充白色：直接在内存中操作，不分配 byte[]
                _bitmap.Lock();
                try
                {
                    unsafe
                    {
                        byte* ptr = (byte*)_bitmap.BackBuffer;
                        long totalSize = (long)width * height * 4;
                        for (long i = 0; i < totalSize; i++)
                        {
                            ptr[i] = 255;
                        }
                    }
                    _bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                }
                finally
                {
                    _bitmap.Unlock();
                }

                // 2. 设置状态
                _originalDpiX = 96.0;
                _originalDpiY = 96.0;

                if (_ctx != null)
                {
                    _ctx.FullImageWidth = width;
                    _ctx.FullImageHeight = height;
                }

                RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.NearestNeighbor);
                BackgroundImage.Source = _bitmap;
                if (IsVirtualPath(filePath))
                {
                    var tab = FileTabs.FirstOrDefault(t => t.FilePath == filePath);
                    _currentFileName = tab?.FileName ?? LocalizationManager.GetString("L_Common_Untitled");
                }
                else _currentFileName = System.IO.Path.GetFileName(filePath);
                _currentFilePath = filePath;

                // 设置底部栏信息
                this.CurrentImageFullInfo = reason ?? LocalizationManager.GetString("L_Load_Info_Memory");
                this.FileSize = LocalizationManager.GetString("L_Status_Unsaved");
                if (_surface == null) _surface = new CanvasSurface(_bitmap);
                else _surface.Attach(_bitmap);

                _undo?.ClearUndo();
                _undo?.ClearRedo();
                _isEdited = false; // 刚打开的空文件不算被编辑过，或者是空文件视为原状

                _imageSize = $"{width}×{height}{LocalizationManager.GetString("L_Main_Unit_Pixel")}";
                OnPropertyChanged(nameof(ImageSize));
                UpdateWindowTitle();

                // 隐藏 GIF 播放器
                AnimationBehavior.SetSourceUri(GifPlayerImage, null);
                GifPlayerImage.Visibility = Visibility.Collapsed;
                BackgroundImage.Visibility = Visibility.Visible;
                if (!string.IsNullOrEmpty(reason)) ShowToast(reason);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    FitToWindow(viewHeightoffset: _startupFinished ?0:- 72,needcanvasUpdateUI: true);
                    CenterImage();

                }), DispatcherPriority.Loaded);
             
            });
        }

    }
}
