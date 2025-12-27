using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
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
            // ... [之前的代码: 扫描文件夹，备份当前图] ...
            if (_currentImageIndex == -1) ScanFolderImages(filePath);

            // 触发当前图的备份 (这是异步的)
            TriggerBackgroundBackup();

            // ... [中间的代码: 计算索引，刷新列表] ...
            // 3. 计算新图片的索引
            int newIndex = _imageFiles.IndexOf(filePath);
            _currentImageIndex = newIndex;
            RefreshTabPageAsync(_currentImageIndex, refresh);

            var current = FileTabs.FirstOrDefault(t => t.FilePath == filePath);

            // ... [更新选中状态的代码] ...
            if (current != null)
            {
                foreach (var tab in FileTabs) tab.IsSelected = false;
                current.IsSelected = true;
                _currentTabItem = current;
            }

            // --- 修复开始 ---
            string fileToLoad = filePath;
            bool isFileLoadedFromCache = false;

            // 检查是否有缓存
            if (current != null && current.IsDirty && !string.IsNullOrEmpty(current.BackupPath))
            {
                // 修复点：检查这个 Tab 是否正在进行后台保存
                if (_activeSaveTasks.TryGetValue(current.Id, out Task? pendingSave))
                {
                    // 如果正在保存，等待它完成！
                    // 这样既避免了文件占用冲突，也避免了读取到写了一半的坏图
                    await pendingSave;
                }

                if (File.Exists(current.BackupPath))
                {
                    fileToLoad = current.BackupPath;
                    isFileLoadedFromCache = true;
                }
            }
            // --- 修复结束 ---

            // 继续加载
            await LoadImage(fileToLoad);

            // ... [之后的代码: 重置 dirty 状态] ...
            ResetDirtyTracker();

            if (isFileLoadedFromCache)
            {
                _savedUndoPoint = -1;
                CheckDirtyState();
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

                var current = FileTabs.FirstOrDefault(t => t.FilePath == filePath);
                current.IsSelected=true;
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
        private Task<string> GetImageMetadataInfoAsync(byte[] imageBytes, string filePath, BitmapImage bitmap)
        {
            return Task.Run(() =>
            {
                try
                {
                    StringBuilder sb = new StringBuilder();
                    // 1. 文件与画布信息 (始终显示)
                    sb.AppendLine("[文件信息]");
                    sb.AppendLine($"路径: {filePath}");
                    sb.AppendLine($"大小: {(imageBytes.Length / 1024.0 / 1024.0):F2} MB");
                    sb.AppendLine();
                    sb.AppendLine("[画布信息]");
                    sb.AppendLine($"尺寸: {bitmap.PixelWidth} × {bitmap.PixelHeight} px");
                    sb.AppendLine($"DPI: {bitmap.DpiX:F0} × {bitmap.DpiY:F0}");
                    sb.AppendLine($"格式: {bitmap.Format}");

                    // 2. 尝试读取 EXIF 元数据
                    using var ms = new MemoryStream(imageBytes);
                    var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                    var metadata = decoder.Frames[0].Metadata as BitmapMetadata;

                    if (metadata != null)
                    {
                        StringBuilder exifSb = new StringBuilder();

                        // 拍摄设备
                        string device = "";
                        if (!string.IsNullOrEmpty(metadata.CameraManufacturer)) device += metadata.CameraManufacturer + " ";
                        if (!string.IsNullOrEmpty(metadata.CameraModel)) device += metadata.CameraModel;
                        if (!string.IsNullOrEmpty(device)) exifSb.AppendLine($"设备: {device.Trim()}");

                        // 核心摄影参数 (使用 GetQuery)
                        // 曝光时间 (1/100s)
                        var expTime = TryGetQuery(metadata, "/app1/ifd/exif/{uint=33434}");
                        if (expTime != null) exifSb.AppendLine($"曝光时间: {expTime}s");

                        // 光圈值 (f/2.8)
                        var fNumber = TryGetQuery(metadata, "/app1/ifd/exif/{uint=33437}");
                        if (fNumber != null) exifSb.AppendLine($"光圈值: f/{Convert.ToDouble(fNumber):F1}");

                        // ISO 速度
                        var iso = TryGetQuery(metadata, "/app1/ifd/exif/{uint=34855}");
                        if (iso != null) exifSb.AppendLine($"ISO速度: ISO-{iso}");

                        // 曝光补偿 (EV)
                        var ev = TryGetQuery(metadata, "/app1/ifd/exif/{uint=37380}");
                        if (ev != null) exifSb.AppendLine($"曝光补偿: {ev} step");

                        // 焦距 (mm)
                        var focal = TryGetQuery(metadata, "/app1/ifd/exif/{uint=37386}");
                        if (focal != null) exifSb.AppendLine($"焦距: {focal}mm");

                        // 35mm等效焦距
                        var focal35 = TryGetQuery(metadata, "/app1/ifd/exif/{uint=41989}");
                        if (focal35 != null) exifSb.AppendLine($"35mm焦距: {focal35}mm");

                        // 测光模式
                        var meter = TryGetQuery(metadata, "/app1/ifd/exif/{uint=37383}");
                        if (meter != null) exifSb.AppendLine($"测光模式: {MapMeteringMode(meter)}");

                        // 闪光灯
                        var flash = TryGetQuery(metadata, "/app1/ifd/exif/{uint=37385}");
                        if (flash != null) exifSb.AppendLine($"闪光灯: {((Convert.ToInt32(flash) & 1) == 1 ? "开启" : "关闭")}");

                        // 软件/后期
                        if (!string.IsNullOrEmpty(metadata.ApplicationName)) exifSb.AppendLine($"处理软件: {metadata.ApplicationName}");
                        if (!string.IsNullOrEmpty(metadata.DateTaken)) exifSb.AppendLine($"拍摄日期: {metadata.DateTaken}");

                        // 镜头型号 (部分现代相机支持)
                        var lens = TryGetQuery(metadata, "/app1/ifd/exif/{uint=42036}");
                        if (lens != null) exifSb.AppendLine($"镜头: {lens}");

                        // --- 只有当 exifSb 里面有内容时，才添加标题并合并 ---
                        if (exifSb.Length > 0)
                        {
                            sb.AppendLine();
                            sb.AppendLine("[照片元数据]");
                            sb.Append(exifSb.ToString());
                        }
                    }
                    return sb.ToString().TrimEnd();
                }
                catch (Exception ex)
                {
                    return "无法解析详细信息: " + ex.Message;
                }
            });
        }

        // 辅助方法：安全读取 Query
        private object TryGetQuery(BitmapMetadata metadata, string query)
        {
            try
            {
                if (metadata.ContainsQuery(query))
                    return metadata.GetQuery(query);
            }
            catch { }
            return null;
        }

        // 辅助方法：测光模式映射
        private string MapMeteringMode(object val)
        {
            int code = Convert.ToInt32(val);
            return code switch
            {
                1 => "平均测光",
                2 => "中央重点平均测光",
                3 => "点测光",
                4 => "多点测光",
                5 => "模式测光",
                6 => "部分测光",
                _ => "未知"
            };
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

                string metadataString = await GetImageMetadataInfoAsync(imageBytes, filePath, fullResBitmap);

                await Dispatcher.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested) return;

                    _bitmap = new WriteableBitmap(fullResBitmap);
                    BackgroundImage.Source = _bitmap;
                    this.CurrentImageFullInfo = metadataString;
                    // 更新所有依赖完整图的状态
                    if (_surface == null)
                        _surface = new CanvasSurface(_bitmap);
                    else
                        _surface.Attach(_bitmap);

                    _undo?.ClearUndo();
                    _undo?.ClearRedo();
                    _isEdited = false;

                    SetPreviewSlider();

                    _imageSize = $"{_surface.Width}×{_surface.Height}像素";
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