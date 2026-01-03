
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

//
//图片加载队列机制
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private readonly object _queueLock = new object();

        // “待办事项”：只存放最新的一个图片加载请求
        private string _pendingFilePath = null;

        // 标志位：表示图像加载“引擎”是否正在工作中
        private bool _isProcessingQueue = false;
        private CancellationTokenSource _loadImageCts;
        public async Task OpenImageAndTabs(string filePath, bool refresh = false)
        {
            _isLoadingImage = true;
            OnPropertyChanged("IsLoadingImage");
            try
            {
                if (_currentImageIndex == -1 && !IsVirtualPath(filePath))
                {
                   await ScanFolderImagesAsync(filePath);
                }
              
                TriggerBackgroundBackup();

                // 2. [适配] 确保 _imageFiles 里有这个虚拟路径 (通常 CreateNewTab 已经加进去了，但为了保险)
                if (IsVirtualPath(filePath) && !_imageFiles.Contains(filePath))
                {
                    _imageFiles.Add(filePath);
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

                // --- 智能加载逻辑 ---
                string fileToLoad = filePath;
                bool isFileLoadedFromCache = false;
                string actualSourcePath = null; // 新增变量
                // 检查是否有缓存
                // 对于虚拟文件，如果它有 BackupPath (例如从 Session 恢复的)，必须读 BackupPath
                if (current != null && (current.IsDirty || current.IsNew) && !string.IsNullOrEmpty(current.BackupPath))
                {
                    if (_activeSaveTasks.TryGetValue(current.Id, out Task? pendingSave))
                    {
                        await pendingSave;
                    }

                    if (File.Exists(current.BackupPath))
                    {
                        // 不要修改 filePath，而是记录实际来源
                        actualSourcePath = current.BackupPath;
                    }
                }
                await LoadImage(fileToLoad,actualSourcePath);

                ResetDirtyTracker();

                if (isFileLoadedFromCache)
                {
                    _savedUndoPoint = -1;
                    CheckDirtyState();
                }

            }
            finally
            {
                // [新增] 无论上面发生什么错误，最终都必须关闭加载状态
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
                OpenImageAndTabs(filePath);
  
            }
            catch (Exception ex)
            {
                // 最好有异常处理
                Debug.WriteLine($"Error loading image {filePath}: {ex.Message}");
            }
        }
        // 1. 在类级别定义支持的扩展名（静态只读，利用HashSet的哈希查找，极快）
        private static readonly HashSet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    ".png", ".jpg", ".jpeg", ".tif", ".tiff",
    ".gif", ".webp", ".bmp", ".ico", ".heic"
};

        // 2. 改为异步方法
        private async Task ScanFolderImagesAsync(string filePath)
        {
            try
            {
                if (IsVirtualPath(filePath) || string.IsNullOrEmpty(filePath)) return;

                string folder = System.IO.Path.GetDirectoryName(filePath)!;

                // 放到后台线程处理，彻底解放 UI 线程
                var sortedFiles = await Task.Run(() =>
                {
                    // 使用 EnumerateFiles，虽然还是要遍历，但配合 LINQ 内存开销略小
                    // 关键优化：只提取扩展名进 HashSet 查找，比 10 次 EndsWith 快得多
                    return Directory.EnumerateFiles(folder)
                        .Where(f => {
                            var ext = System.IO.Path.GetExtension(f);
                            return ext != null && AllowedExtensions.Contains(ext);
                        })
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase) // 既然是看图，自然排序可能更好(WinAPI StrCmpLogicalW)，这里先保持 Ordinal
                        .ToList();
                });

                // --- 回到 UI 线程更新数据 ---

                // 获取虚拟路径 (内存操作，很快)
                var virtualPaths = FileTabs.Where(t => IsVirtualPath(t.FilePath))
                                           .Select(t => t.FilePath)
                                           .ToList();

                // 使用 Capacity 预分配内存，避免 List 扩容
                var combinedFiles = new List<string>(virtualPaths.Count + sortedFiles.Count);
                combinedFiles.AddRange(virtualPaths);
                combinedFiles.AddRange(sortedFiles);

                _imageFiles = combinedFiles;

                // 重新定位索引
                _currentImageIndex = _imageFiles.IndexOf(filePath);

                // 可以在这里触发一个更新 UI 的事件或方法
                // UpdateImageNavigationUI(); 
            }
            catch (Exception ex)
            {
                // 建议记录日志，防止静默失败
                System.Diagnostics.Debug.WriteLine($"Scan Error: {ex.Message}");
            }
        }



        private BitmapImage DecodePreviewBitmap(byte[] imageBytes, CancellationToken token)
        {
            if (token.IsCancellationRequested) return null;

            using var ms = new System.IO.MemoryStream(imageBytes);

            // 1. 先只读取元数据获取原始尺寸，不解码像素，速度极快
            var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
            int originalWidth = decoder.Frames[0].PixelWidth;

            // 重置流位置以供 BitmapImage 读取
            ms.Position = 0;

            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;

            // 2. 只有当原图宽度大于 480 时才进行降采样
            if (originalWidth > 480)
            {
                img.DecodePixelWidth = 480;
            }
            // 否则不设置 DecodePixelWidth（默认为0，即加载原图尺寸），避免小图报错或被拉伸

            img.StreamSource = ms;
            img.EndInit();
            img.Freeze();
            return img;
        }


        private BitmapImage DecodeFullResBitmap(byte[] imageBytes, CancellationToken token)
        {
            if (token.IsCancellationRequested) return null;

            using var ms = new System.IO.MemoryStream(imageBytes);

            // 先用解码器获取原始尺寸
            var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
            int originalWidth = decoder.Frames[0].PixelWidth;
            int originalHeight = decoder.Frames[0].PixelHeight;

            ms.Position = 0; // 重置流位置以重新读取

            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            img.StreamSource = ms;

            const int maxSize = 16384;
            if (originalWidth > maxSize || originalHeight > maxSize)
            {
                if (originalWidth >= originalHeight) img.DecodePixelWidth = maxSize;
                else img.DecodePixelHeight = maxSize;
                Dispatcher.Invoke(() => ShowToast("⚠️ 图片过大，已自动压缩显示"));
            }

            img.EndInit();
            img.Freeze();
            return img;
        }

        private Task<(int Width, int Height)> GetImageDimensionsAsync(byte[] imageBytes)
        {
            return Task.Run(() =>
            {
                using var ms = new System.IO.MemoryStream(imageBytes);
                // Create a decoder but only access the metadata, which is very fast.
                var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                return (decoder.Frames[0].PixelWidth, decoder.Frames[0].PixelHeight);
            });
        }


        private readonly object _lockObj = new object();
        private async Task LoadImage(string filePath, string? sourcePath = null)
        {
          
            _loadImageCts?.Cancel();
            _loadImageCts = new CancellationTokenSource();
            var token = _loadImageCts.Token;
            string fileToRead = sourcePath ?? filePath;
            if (IsVirtualPath(filePath) && string.IsNullOrEmpty(sourcePath))
            {// 新建空白图像逻辑，不加载

                await Dispatcher.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested) return;

                    // 1. 创建默认白色画布 (1200x900)
                    int width = 1200;
                    int height = 900;
                    _bitmap = new WriteableBitmap(width, height, 96.0, 96.0, PixelFormats.Bgra32, null);

                    // 填充白色
                    byte[] pixels = new byte[width * height * 4];
                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        pixels[i] = 255;     // B
                        pixels[i + 1] = 255; // G
                        pixels[i + 2] = 255; // R
                        pixels[i + 3] = 255; // A
                    }
                    _bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);

                    // 2. 设置状态
                    _originalDpiX = 96.0;
                    _originalDpiY = 96.0;
                    BackgroundImage.Source = _bitmap;
               

                    // 查找 Tab 以获取正确的显示名 (如 "未命名 1")
                    var tab = FileTabs.FirstOrDefault(t => t.FilePath == filePath);
                    _currentFileName = tab?.FileName ?? "未命名";
                    _currentFilePath = filePath; // 保持虚拟路径

                    this.CurrentImageFullInfo = "[新建图像] 内存文件";

                    this.FileSize = "未保存";

                    if (_surface == null) _surface = new CanvasSurface(_bitmap);
                    else _surface.Attach(_bitmap);

                    _undo?.ClearUndo();
                    _undo?.ClearRedo();
                    _isEdited = false;

                    _imageSize = $"{width}×{height}像素";
                    OnPropertyChanged(nameof(ImageSize));
                    UpdateWindowTitle();

                  FitToWindow(1); // 默认 100% 或 适应窗口
                    CenterImage();
                    _canvasResizer.UpdateUI();
                    SetPreviewSlider();
                });
                return;
            }


            if (!File.Exists(fileToRead))
            {
                // 只有非虚拟路径不存在时才报错
                s($"找不到图片文件: {fileToRead}");
                return;
            }

            try
            {
                // 步骤 1: 异步读取文件并快速获取最终尺寸
                var imageBytes = await File.ReadAllBytesAsync(fileToRead, token);
                if (token.IsCancellationRequested) return;

                string sizeString = FormatFileSize(imageBytes.Length);
                await Dispatcher.InvokeAsync(() => this.FileSize = sizeString);


                var (originalWidth, originalHeight) = await GetImageDimensionsAsync(imageBytes);
                if (token.IsCancellationRequested) return;

                // 步骤 2: 并行启动中等预览图和完整图的解码任务
                Task<BitmapImage> previewTask = Task.Run(() => DecodePreviewBitmap(imageBytes, token), token);
                Task<BitmapImage> fullResTask = Task.Run(() => DecodeFullResBitmap(imageBytes, token), token);

                // --- 阶段 0: (新增) 立即显示已缓存的缩略图 ---
                bool isInitialLayoutSet = false;
                // 查找与当前文件路径匹配的 Tab 项
                var tabItem = FileTabs.FirstOrDefault(t => t.FilePath == filePath);

                if (tabItem?.Thumbnail != null)
                {
                    // 如果找到了并且它已经有缩略图，立即在UI线程上显示它
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (token.IsCancellationRequested) return;
     

                        // 2. 将采样模式改为线性 (Linear)，避免马赛克锯齿
                        RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.Linear);
                        BackgroundImage.Source = tabItem.Thumbnail;

                        // 更新窗口标题等基本信息
                        _currentFileName = IsVirtualPath(filePath)
           ? (FileTabs.FirstOrDefault(t => t.FilePath == filePath)?.FileName ?? "未命名")
           : System.IO.Path.GetFileName(filePath);
                        _currentFilePath = filePath;
                        UpdateWindowTitle();

                        // 立即适配并居中
                        FitToWindow(1);
                        CenterImage(); // 或者你更新后的 UpdateImagePosition()
                        BackgroundImage.InvalidateVisual();
                        Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                        isInitialLayoutSet = true; // 标记初始布局已完成
                        _canvasResizer.UpdateUI();

                    });
                }

                // --- 阶段 1: 等待 480p 预览图并更新 ---
                var previewBitmap = await previewTask;
                if (token.IsCancellationRequested || previewBitmap == null) return;

                await Dispatcher.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested) return;
                    RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.Linear);

                    if (!isInitialLayoutSet)
                    {
                        BackgroundImage.Source = previewBitmap;
                        _currentFileName = System.IO.Path.GetFileName(filePath);
                        _currentFilePath = filePath;
                        UpdateWindowTitle();
                        FitToWindow();
                        CenterImage();
                        BackgroundImage.InvalidateVisual();
                        Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                        _canvasResizer.UpdateUI();
                    }
                });

                // --- 阶段 2: 等待完整图并最终更新 ---
                var fullResBitmap = await fullResTask;
                if (token.IsCancellationRequested || fullResBitmap == null) return;

                // 获取元数据 (保持不变)
                string metadataString = await GetImageMetadataInfoAsync(imageBytes, filePath, fullResBitmap);

                await Dispatcher.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested) return;

                    // 1. 记录原始 DPI
                    _originalDpiX = fullResBitmap.DpiX;
                    _originalDpiY = fullResBitmap.DpiY;

                    // 计算统一后的尺寸
                    int width = fullResBitmap.PixelWidth;
                    int height = fullResBitmap.PixelHeight;

                    // 2. 准备源数据：确保格式为 BGRA32
                    // 使用 FormatConvertedBitmap 只是为了确保格式，它通常不会立即深拷贝，直到读取像素
                    BitmapSource source = fullResBitmap;
                    if (source.Format != PixelFormats.Bgra32)
                    {
                        var formatted = new FormatConvertedBitmap();
                        formatted.BeginInit();
                        formatted.Source = fullResBitmap;
                        formatted.DestinationFormat = PixelFormats.Bgra32;
                        formatted.EndInit();
                        source = formatted;
                    }

                    // 3. 创建 WriteableBitmap (分配 760MB BackBuffer)
                    // 强制 96 DPI 以匹配你的逻辑
                    _bitmap = new WriteableBitmap(width, height, 96.0, 96.0, PixelFormats.Bgra32, null);

                    // 4. 【核心优化】直接内存拷贝 (Direct Memory Copy)
                    // 避免创建 var pixels = new byte[...]，直接写显存/非托管内存
                    _bitmap.Lock();
                    try
                    {
                        // 使用 CopyPixels 直接写入 BackBuffer
                        // 注意：BackBufferStride 是 WriteableBitmap 计算出的步长，通常等于 width * 4
                        source.CopyPixels(
                            new Int32Rect(0, 0, width, height),
                            _bitmap.BackBuffer, // 目标指针
                            _bitmap.BackBufferStride * height, // 缓冲区总大小
                            _bitmap.BackBufferStride // 步长
                        );

                        // 标记脏区以更新画面
                        _bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                    }
                    catch (Exception copyEx)
                    {
                        Debug.WriteLine("内存拷贝失败: " + copyEx.Message);
                        // 兜底策略：如果直接拷贝失败（极少见），再尝试旧方法
                    }
                    finally
                    {
                        _bitmap.Unlock();
                    }

                    // 5. 【极其重要】主动释放资源并 GC
                    // 解除引用
                    source = null;
                    fullResBitmap = null;
                    RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.NearestNeighbor);

                    BackgroundImage.Source = _bitmap;
                    this.CurrentImageFullInfo = metadataString;

                    if (_surface == null) _surface = new CanvasSurface(_bitmap);
                    else _surface.Attach(_bitmap);

                    _undo?.ClearUndo();
                    _undo?.ClearRedo();
                    _isEdited = false;
                    SetPreviewSlider();
                    _imageSize = $"{_surface.Width}×{_surface.Height}像素";
                    OnPropertyChanged(nameof(ImageSize));

                    FitToWindow();
                    CenterImage();
                    _canvasResizer.UpdateUI();

                    // 6. 强制执行垃圾回收 (LOH 压缩)
                    // 对于画图软件，加载大图后的瞬间卡顿是可以接受的，换取的是内存不崩溃
                    //GC.Collect(2, GCCollectionMode.Forced, true);
                    //GC.WaitForPendingFinalizers();
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); // 稍微降低优先级，确保UI先响应
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"Image load for {filePath} was canceled.");
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => s($"加载图片失败: {ex.Message}"));
            }
        }

    }
}