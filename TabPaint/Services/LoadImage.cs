using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

//
//TabPaint主程序
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
            // 1. 如果是第一次加载，初始化文件列表
            if (_currentImageIndex == -1) ScanFolderImages(filePath);

            a.s(3);
            // 2. 切图前保存上一个 Tab 的缓存 (如果有的话)
            TriggerBackgroundBackup();
            a.s(4);
            // 3. 计算新图片的索引
            int newIndex = _imageFiles.IndexOf(filePath);
            _currentImageIndex = newIndex;

            RefreshTabPageAsync(_currentImageIndex, refresh);

            var current = FileTabs.FirstOrDefault(t => t.FilePath == filePath);

            // 6. 更新选中状态
            if (current != null)
            {
                foreach (var tab in FileTabs) tab.IsSelected = false;
                current.IsSelected = true;
                _currentTabItem = current; // 更新当前引用
            }
            else
            {
            }

            string fileToLoad = filePath;
            var isFileLoadedFromCache = false;

            if (current != null && current.IsDirty && !string.IsNullOrEmpty(current.BackupPath) && File.Exists(current.BackupPath))
            {
                fileToLoad = current.BackupPath;
                isFileLoadedFromCache = true;
            }

            await LoadImage(fileToLoad);

            // 9. 重置脏状态追踪器
            ResetDirtyTracker();

            if (isFileLoadedFromCache)
            {
                _savedUndoPoint = -1; // 设为 -1，使得 0 != -1，触发脏状态
                CheckDirtyState();    // 立即刷新红点
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
            while (true)
            {
                string filePathToLoad;

                // 进入临界区，检查并获取下一个任务
                lock (_queueLock)
                {
                    // 1. 检查是否还有待办事项
                    if (_pendingFilePath == null)
                    {
                        // 如果没有了，说明工作完成，工人可以下班了
                        _isProcessingQueue = false;
                        break; // 退出循环
                    }

                    // 2. 获取当前最新的待办事项
                    filePathToLoad = _pendingFilePath;

                    // 3. 清空待办事项，表示我们已经接手了这个任务
                    _pendingFilePath = null;
                }
                await LoadAndDisplayImageInternalAsync(filePathToLoad);
            }
        }
        private async Task LoadAndDisplayImageInternalAsync(string filePath)
        {
            try
            {
                int newIndex = _imageFiles.IndexOf(filePath);
                if (newIndex < 0) return;
                _currentImageIndex = newIndex;

                foreach (var tab in FileTabs)
                    tab.IsSelected = false;


                // 3. 加载主图片
                await LoadImage(filePath); // 假设这是您加载大图的方法

                await RefreshTabPageAsync(_currentImageIndex);

                SetPreviewSlider();
            }
            catch (Exception ex)
            {
                // 最好有异常处理
                Debug.WriteLine($"Error loading image {filePath}: {ex.Message}");
            }
        }
        private void ScanFolderImages(string filePath)
        {
            // 扫描同目录图片文件
            string folder = System.IO.Path.GetDirectoryName(filePath)!;
            _imageFiles = Directory.GetFiles(folder, "*.*")
                .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _currentImageIndex = _imageFiles.IndexOf(filePath);
        }

        private BitmapImage DecodePreviewBitmap(byte[] imageBytes, CancellationToken token)
        {
            if (token.IsCancellationRequested) return null;

            using var ms = new System.IO.MemoryStream(imageBytes);
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            // 关键：设置解码宽度，速度极快
            img.DecodePixelWidth = 480; // 建议值，50太小了，可能看不清内容
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
        private async Task LoadImage(string filePath)
        {
            if (!File.Exists(filePath))
            {
                s($"找不到图片文件: {filePath}");
                return;
            }

            _loadImageCts?.Cancel();
            _loadImageCts = new CancellationTokenSource();
            var token = _loadImageCts.Token;

            try
            {
                // 步骤 1: 异步读取文件并快速获取最终尺寸
                var imageBytes = await File.ReadAllBytesAsync(filePath, token);
                if (token.IsCancellationRequested) return;

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

                        // 使用已有的、超低分辨率的缩略图作为第一帧
                        BackgroundImage.Source = tabItem.Thumbnail;

                        // 更新窗口标题等基本信息
                        _currentFileName = System.IO.Path.GetFileName(filePath);
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

                    // 如果阶段 0 没有执行（因为没有缓存的缩略图），
                    // 那么在这里完成初始布局设置。
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

                await Dispatcher.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested) return;

                    _bitmap = new WriteableBitmap(fullResBitmap);
                    BackgroundImage.Source = _bitmap;

                    // 更新所有依赖完整图的状态
                    if (_surface == null)
                        _surface = new CanvasSurface(_bitmap);
                    else
                        _surface.Attach(_bitmap);

                    _undo?.ClearUndo();
                    _undo?.ClearRedo();
                    _isEdited = false;

                    SetPreviewSlider();

                    _imageSize = $"{_surface.Width}×{_surface.Height}";
                    OnPropertyChanged(nameof(ImageSize));

                    // 因为尺寸可能因解码有微小差异，最后再校准一次布局是好习惯
                    FitToWindow();
                    CenterImage();
                    _canvasResizer.UpdateUI();
                });
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