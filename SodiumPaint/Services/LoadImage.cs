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
//SodiumPaint主程序
//

namespace SodiumPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public async Task OpenImageAndTabs(string filePath, bool refresh = false)
        {
            foreach (var tab in FileTabs)
                tab.IsSelected = false;

            // 找到当前点击的标签并选中
            var current = FileTabs.FirstOrDefault(t => t.FilePath == filePath);
            if (current != null)
                current.IsSelected = true;

            if (_currentImageIndex == -1) ScanFolderImages(filePath);
            // 加载对应图片
            RefreshTabPageAsync(_currentImageIndex, refresh);
            await LoadImage(filePath);


            // 标签栏刷新后，重新选中对应项
            var reopened = FileTabs.FirstOrDefault(t => t.FilePath == filePath);
            if (reopened != null)
                reopened.IsSelected = true;
        }
        private readonly object _queueLock = new object();

        // “待办事项”：只存放最新的一个图片加载请求
        private string _pendingFilePath = null;

        // 标志位：表示图像加载“引擎”是否正在工作中
        private bool _isProcessingQueue = false;
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
            // 这里就是您原来 OpenImageAndTabs 的核心代码
            try
            {
                // 找到当前图片在总列表中的索引
                int newIndex = _imageFiles.IndexOf(filePath);
                if (newIndex < 0) return;
                _currentImageIndex = newIndex;

                // --- UI 更新逻辑 ---

                // 1. 清除所有旧的选中状态
                foreach (var tab in FileTabs)
                    tab.IsSelected = false;

                // 2. 找到并选中新标签（如果它已在可视区域）
                var currentTab = FileTabs.FirstOrDefault(t => t.FilePath == filePath);
                if (currentTab != null)
                    currentTab.IsSelected = true;

                // 3. 加载主图片
                await LoadImage(filePath); // 假设这是您加载大图的方法

                // 4. 刷新和滚动标签栏
                await RefreshTabPageAsync(_currentImageIndex);

                // 5. 再次确保标签被选中（因为RefreshTabPageAsync可能重建了列表）
                var reopenedTab = FileTabs.FirstOrDefault(t => t.FilePath == filePath);
                if (reopenedTab != null)
                    reopenedTab.IsSelected = true;

                // 6. 更新Slider
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
    }

      private async Task LoadImage(string filePath)
        {//不推荐直接使用
            if (!File.Exists(filePath)) { s($"找不到图片文件: {filePath}"); return; }

            try
            {
                // 🧩 后台线程进行解码和位图创建
                var wb = await Task.Run(() =>
                {
                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                    // 先用解码器获取原始尺寸
                    var decoder = BitmapDecoder.Create(
                        fs,
                        BitmapCreateOptions.IgnoreColorProfile,
                        BitmapCacheOption.None
                    );
                    int originalWidth = decoder.Frames[0].PixelWidth;
                    int originalHeight = decoder.Frames[0].PixelHeight;

                    fs.Position = 0; // 重置流位置以重新读取

                    var img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    img.StreamSource = fs;

                    // 如果超过 16384，就等比例缩放
                    const int maxSize = 16384;
                    if (originalWidth > maxSize || originalHeight > maxSize)
                    {
                        if (originalWidth >= originalHeight)
                        {
                            img.DecodePixelWidth = maxSize;
                        }
                        else
                        {
                            img.DecodePixelHeight = maxSize;
                        }
                    }

                    img.EndInit();
                    img.Freeze();

                    return img;
                });

                // ✅ 回到 UI 线程更新
                await Dispatcher.InvokeAsync(() =>
                {
                    _bitmap = new WriteableBitmap(wb);

                    _currentFileName = System.IO.Path.GetFileName(filePath);
                    BackgroundImage.Source = _bitmap;

                    if (_surface == null)
                        _surface = new CanvasSurface(_bitmap);
                    else
                        _surface.Attach(_bitmap);

                    _undo?.ClearUndo();
                    _undo?.ClearRedo();

                    _currentFilePath = filePath;
                    _isEdited = false;

                    SetPreviewSlider();

                    // 窗口调整逻辑
                    double imgWidth = _bitmap.Width;
                    double imgHeight = _bitmap.Height;

                    BackgroundImage.Width = imgWidth;
                    BackgroundImage.Height = imgHeight;

                    _imageSize = $"{_surface.Width}×{_surface.Height}";
                    OnPropertyChanged(nameof(ImageSize));
                    UpdateWindowTitle();

                    FitToWindow();

                }, System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                s($"加载图片失败: {ex.Message}");
            }
        }
    }