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
            if (_currentImageIndex == -1 && !IsVirtualPath(filePath))
            {
                ScanFolderImages(filePath);
            }

            // 触发当前图的备份
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
                    //s(current.BackupPath);
                    fileToLoad = current.BackupPath;
                    isFileLoadedFromCache = true;
                }
            }
            await LoadImage(fileToLoad);

            ResetDirtyTracker();

            if (isFileLoadedFromCache)
            {
                _savedUndoPoint = -1;
                CheckDirtyState();
            }

            // [适配] 如果是纯新建文件，加载后应该是 Clean 状态，但要确保 UI 状态正确
            if (IsVirtualPath(filePath) && !isFileLoadedFromCache)
            {
                // 纯白纸状态，无需做 Dirty 检查
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
            // 如果是虚拟路径，不执行磁盘扫描（除非你想扫描上次打开的文件夹）
            if (IsVirtualPath(filePath)) return;

            string folder = System.IO.Path.GetDirectoryName(filePath)!;

            // 1. 获取磁盘上的物理文件
            var diskFiles = Directory.GetFiles(folder, "*.*")
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

            // 2. 获取当前已存在于 FileTabs 中的所有虚拟路径 (::TABPAINT_NEW::...)
            // 这样可以保证即使切换了文件夹，之前新建的未保存标签依然在列表中
            var virtualPaths = FileTabs.Where(t => IsVirtualPath(t.FilePath))
                                       .Select(t => t.FilePath)
                                       .ToList();

            // 3. 整合：虚拟路径在前，磁盘文件在后（或者根据你的喜好排序）
            var combinedFiles = new List<string>();
            combinedFiles.AddRange(virtualPaths);
            combinedFiles.AddRange(diskFiles);

            _imageFiles = combinedFiles;
            _currentImageIndex = _imageFiles.IndexOf(filePath);
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
           // a.s(filePath);
            _loadImageCts?.Cancel();
            _loadImageCts = new CancellationTokenSource();
            var token = _loadImageCts.Token;

            if (IsVirtualPath(filePath))
            {
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

                    if (_surface == null) _surface = new CanvasSurface(_bitmap);
                    else _surface.Attach(_bitmap);

                    _undo?.ClearUndo();
                    _undo?.ClearRedo();
                    _isEdited = false;

                    _imageSize = $"{width}×{height}像素";
                    OnPropertyChanged(nameof(ImageSize));
                    UpdateWindowTitle();

                    if (!IsFixedZoom) FitToWindow(1); // 默认 100% 或 适应窗口
                    CenterImage();
                    _canvasResizer.UpdateUI();
                    SetPreviewSlider();
                });
                return;
            }

            // ==========================================
            // 分支 B: 处理物理文件 (普通图片 或 缓存文件)
            // ==========================================

            if (!File.Exists(filePath))
            {
                // 只有非虚拟路径不存在时才报错
                s($"找不到图片文件: {filePath}");
                return;
            }

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
                        if (!IsFixedZoom) FitToWindow(1);
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
                        if (!IsFixedZoom) FitToWindow();
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
                    // 如果 imageBytes 还是局部变量，此时引用计数还没归零，最好将其设为 null 如果它是类成员
                    // 这里 imageBytes 是局部变量，方法结束会自动释放，但为了大图，建议强制回收

                    // 更新 UI
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

                    if (!IsFixedZoom) FitToWindow();
                    CenterImage();
                    _canvasResizer.UpdateUI();

                    // 6. 强制执行垃圾回收 (LOH 压缩)
                    // 对于画图软件，加载大图后的瞬间卡顿是可以接受的，换取的是内存不崩溃
                    GC.Collect(2, GCCollectionMode.Forced, true);
                    GC.WaitForPendingFinalizers();
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