using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

//namespace TabPaint.Resources
//{
//    class deprecated_functions
//    {
//    }
//}


// Note: This file is intentionally left empty as a placeholder for deprecated functions.



//public void SetZoomAndOffset(double scaleFactor, double offsetX, double offsetY)
//{
//    double oldScale = zoomscale;
//    zoomscale = scaleFactor;
//    UpdateSliderBarValue(zoomscale);
//    // 更新缩放变换
//    ZoomTransform.ScaleX = ZoomTransform.ScaleY = scaleFactor;

//    // 计算新的滚动偏移
//    // 偏移要按当前比例转换：ScrollViewer 的偏移单位是可视区域像素，而非原图像素。
//    double newOffsetX = offsetX * scaleFactor;
//    double newOffsetY = offsetY * scaleFactor;

//    // 应用到滚动容器
//    ScrollContainer.ScrollToHorizontalOffset(newOffsetX);
//    ScrollContainer.ScrollToVerticalOffset(newOffsetY);
//}

//public void PushUndoRegion(Int32Rect rect, byte[] pixels)
//{// 将整图数据压入 Undo 栈
//    _undo.Push(new UndoAction(rect, pixels));
//}
//public void PushUndoRegionTransform(Int32Rect rect, byte[] pixels)
//{
//    _undo.Push(new UndoAction(rect, pixels));
//}

// 简易高亮 - 给选中的颜色加外框
//private void HighlightSelectedButton(System.Windows.Controls.Button? selected)
//{
//    foreach (var item in FindVisualChildren<System.Windows.Controls.Button>(this))
//    {
//        if (item.ToolTip != null && item.ToolTip.ToString() == "自定义颜色")
//            continue; // 跳过彩虹按钮
//        if (selected != null && item == selected)
//            item.BorderBrush = Brushes.DeepSkyBlue;
//        else
//            item.BorderBrush = Brushes.Red;
//    }
//}
//public class SelectionOverlay : Canvas
//{
//    public Int32Rect? SelectionRect { get; set; }

//    protected override void OnRender(DrawingContext dc)
//    {
//        base.OnRender(dc);
//        if (SelectionRect.HasValue)
//        {
//            var rectPx = SelectionRect.Value;
//            Rect rect = new Rect(rectPx.X, rectPx.Y, rectPx.Width, rectPx.Height);
//            dc.DrawRectangle(null, new Pen(Brushes.Black, 1) { DashStyle = DashStyles.Dash }, rect);
//        }
//    }
//}


//from mainwindowviewmodel.cs
//using System;
//using System.Collections.Generic;
//using System.ComponentModel;
//using System.Linq;
//using System.Runtime.CompilerServices;
//using System.Text;
//using System.Threading.Tasks;
//using System.Windows.Input;
//using System.Windows.Media;
//using System.Windows.Media.Imaging;

//namespace TabPaint.ViewModels
//{

//    // 简单命令类
//    public class RelayCommand : ICommand
//    {
//        private readonly Action<object?> _execute;
//        private readonly Func<object?, bool>? _canExecute;

//        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
//        {
//            _execute = execute;
//            _canExecute = canExecute;
//        }

//        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
//        public void Execute(object? parameter) => _execute(parameter);
//        public event EventHandler? CanExecuteChanged;
//    }
//}


//private void CenterImage()
//{
//    return;
//    //if (_bitmap == null || BackgroundImage == null)
//    //    return;

//    //BackgroundImage.Width = _bitmap.PixelWidth;
//    //BackgroundImage.Height = _bitmap.PixelHeight;

//    //// 如果在 ScrollViewer 中，自动滚到中心
//    //if (ScrollContainer != null)
//    //{
//    //    ScrollContainer.ScrollToHorizontalOffset(
//    //        (BackgroundImage.Width - ScrollContainer.ViewportWidth) / 2);
//    //    ScrollContainer.ScrollToVerticalOffset(
//    //        (BackgroundImage.Height - ScrollContainer.ViewportHeight) / 2);
//    //}

//    //BackgroundImage.VerticalAlignment = VerticalAlignment.Center;
//}

//public double ZoomScale
//{
//    get => _zoomScale;
//    set
//    {
//        if (_zoomScale != value)
//        {
//            _zoomScale = value;
//            ZoomLevel = $"{Math.Round(_zoomScale * 100)}%";
//            ZoomTransform.ScaleX = ZoomTransform.ScaleY = _zoomScale;
//            OnPropertyChanged();
//        }
//    }
//}

//private void OnDrawUp(object sender, MouseButtonEventArgs e)
//{
//    if (!_isDrawing || _preDrawSnapshot == null) return;
//    _isDrawing = false;

//    // 计算整笔操作的区域
//    if (_currentDrawRegions.Count == 0) return;
//    var combined = ClampRect(CombineRects(_currentDrawRegions), _ctx.Bitmap.PixelWidth, _ctx.Bitmap.PixelHeight);

//    // 从 _preDrawSnapshot 中提取修改区域像素
//    byte[] regionData = ExtractRegionFromSnapshot(
//        _preDrawSnapshot, combined, _bitmap.BackBufferStride);

//    _undoStack.Push(new UndoAction(combined, regionData));

//    _preDrawSnapshot = null; // 清除快照引用
//    _redoStack.Clear();
//    _isEdited = true;

//}


//private Int32Rect CombineRects(List<Int32Rect> rects)
//{
//    if (rects.Count == 0) return new Int32Rect();

//    int minX = rects.Min(r => r.X);
//    int minY = rects.Min(r => r.Y);
//    int maxX = rects.Max(r => r.X + r.Width);
//    int maxY = rects.Max(r => r.Y + r.Height);
//    return new Int32Rect(minX, minY, maxX - minX, maxY - minY);
//}

//private Int32Rect GetLineBoundingBox(Point p1, Point p2)
//{
//    int x = (int)Math.Min(p1.X, p2.X);
//    int y = (int)Math.Min(p1.Y, p2.Y);
//    int w = (int)Math.Abs(p1.X - p2.X) + 2;
//    int h = (int)Math.Abs(p1.Y - p2.Y) + 2;
//    return new Int32Rect(x, y, w, h);
//}

//private void PushUndoRegion(int x, int y, int width, int height)
//{
//    //sgbox("pushundo");
//    // 限制区域不超出画布
//    if (x < 0) { width += x; x = 0; }
//    if (y < 0) { height += y; y = 0; }
//    if (x + width > _bmpWidth) width = _bmpWidth - x;
//    if (y + height > _bmpHeight) height = _bmpHeight - y;
//    if (width <= 0 || height <= 0) return;

//    int stride = _bitmap.BackBufferStride;
//    byte[] data = new byte[height * width * 4]; // BGRA32

//    _bitmap.Lock();
//    unsafe
//    {
//        byte* srcPtr = (byte*)_bitmap.BackBuffer + y * stride + x * 4;
//        for (int row = 0; row < height; row++)
//        {
//            IntPtr srcRow = (IntPtr)(srcPtr + row * stride);
//            System.Runtime.InteropServices.Marshal.Copy(srcRow, data, row * width * 4, width * 4);
//        }
//    }
//    _bitmap.Unlock();

//    _undoStack.Push(new UndoAction(new Int32Rect(x, y, width, height), data));

//    // 可选：限制最大撤销次数
//    if (_undoStack.Count > 1000) _undoStack = new Stack<UndoAction>(_undoStack.Take(1000));
//}



//private void DrawLine(Point p1, Point p2, Color color)
//{
//    int minX = (int)Math.Min(p1.X, p2.X);
//    int minY = (int)Math.Min(p1.Y, p2.Y);
//    int maxX = (int)Math.Max(p1.X, p2.X);
//    int maxY = (int)Math.Max(p1.Y, p2.Y);

//    // 在画线前保存该区域
//    //PushUndoRegion(minX, minY, maxX - minX + 1, maxY - minY + 1);


//    int x0 = (int)p1.X, y0 = (int)p1.Y;
//    int x1 = (int)p2.X, y1 = (int)p2.Y;
//    int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
//    int sx = x0 < x1 ? 1 : -1;
//    int sy = y0 < y1 ? 1 : -1;
//    int err = dx - dy;

//    while (true)
//    {
//        DrawPixel(x0, y0, color);
//        if (x0 == x1 && y0 == y1) break;
//        int e2 = 2 * err;
//        if (e2 > -dy) { err -= dy; x0 += sx; }
//        if (e2 < dx) { err += dx; y0 += sy; }
//    }
//}


//private WriteableBitmap LoadBitmapWith96Dpi(string path)
//{
//    // 原始加载
//    var bmpImage = new BitmapImage();
//    bmpImage.BeginInit();
//    bmpImage.CacheOption = BitmapCacheOption.OnLoad;
//    bmpImage.CreateOptions = BitmapCreateOptions.None;
//    bmpImage.UriSource = new Uri(path, UriKind.Absolute);
//    bmpImage.EndInit();
//    bmpImage.Freeze();

//    // 读取原像素数据
//    int width = bmpImage.PixelWidth;
//    int height = bmpImage.PixelHeight;
//    int stride = width * (bmpImage.Format.BitsPerPixel / 8);
//    byte[] pixels = new byte[height * stride];
//    bmpImage.CopyPixels(pixels, stride, 0);

//    // 创建新的 BitmapSource，设置 DPI=96
//    var newSource = BitmapSource.Create(
//        width,
//        height,
//        96,             // DpiX
//        96,             // DpiY
//        bmpImage.Format,
//        bmpImage.Palette,
//        pixels,
//        stride
//    );

//    // 转成 WriteableBitmap
//    return new WriteableBitmap(newSource);
//}

//private void PreviewSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
//{
//    // 检查点击的原始来源是否已经是 Thumb。如果是，则什么都不做，让默认行为发生。
//    if (e.OriginalSource is Thumb) return;

//    var slider = sender as Slider;
//    if (slider == null) return;

//    // 找到 Slider 内部的 Track 控件
//    var track = FindVisualChild<Track>(slider);
//    if (track == null)
//    {
//        return;
//    }

//    // 找到 Track 内部的 Thumb 控件
//    var thumb = track.Thumb;
//    if (thumb != null)
//    {
//        thumb.Focus();
//        thumb.CaptureMouse();
//        e.Handled = true;
//    }
//}

//double finalX = 0, finalY = 0;
//_selectionRect = new Int32Rect((int)finalX, (int)finalY, _selectionRect.Width, _selectionRect.Height);

//if (ctx.SelectionPreview.RenderTransform is TranslateTransform t1)
//{
//    finalX = t1.X;
//    finalY = t1.Y;
//}
//else if (ctx.SelectionPreview.RenderTransform is TransformGroup tg)
//{
//    var tt = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
//    if (tt != null)
//    {
//        finalX = tt.X;
//        finalY = tt.Y;
//    }
//}

//_selectionRect = new Int32Rect((int)finalX, (int)finalY, _selectionRect.Width, _selectionRect.Height);

//< !--< MenuItem Header = "撤销    " Style = "{StaticResource SubMenuItemStyle}" InputGestureText = "Ctrl+Z" Click = "OnUndoClick" >
//    < MenuItem.Icon >
//        < Image Source = "{StaticResource Undo_Image}" Width = "16" Height = "16" />
//    </ MenuItem.Icon >
//</ MenuItem >
//< MenuItem Header = "重做    " Style = "{StaticResource SubMenuItemStyle}" InputGestureText = "Ctrl+Y" Click = "OnRedoClick" >
//    < MenuItem.Icon >
//        < Image Source = "{StaticResource Redo_Image}" Width = "16" Height = "16" />
//    </ MenuItem.Icon >
//</ MenuItem > -->
//< !--< Separator Style = "{StaticResource MenuSeparator}" /> -->



//private void SetPreviewPosition(ToolContext ctx, int pixelX, int pixelY)
//{

//    // 背景图左上角位置（UI坐标）
//    var imgPos = ctx.ViewElement.TranslatePoint(new Point(0, 0), ctx.SelectionPreview.Parent as UIElement);

//    // 缩放比例（像素 → UI）
//    double scaleX = ctx.ViewElement.ActualWidth / ctx.Surface.Bitmap.PixelWidth;
//    double scaleY = ctx.ViewElement.ActualHeight / ctx.Surface.Bitmap.PixelHeight;

//    // 转换到 UI 平移
//    double uiX = imgPos.X + pixelX * scaleX;
//    double uiY = imgPos.Y + pixelY * scaleY;

//    ctx.SelectionPreview.RenderTransform = new TranslateTransform(uiX, uiY);
//    s(pixelX);
//}


//public void PasteSelection(ToolContext ctx, bool ins)
//{
//    // 1. 提交当前的选区（如果有）
//    if (_selectionData != null) CommitSelection(ctx);

//    BitmapSource? sourceBitmap = null;
//    // ... (此处保持你原来的获取 sourceBitmap 的逻辑不变) ...
//    if (System.Windows.Clipboard.ContainsImage()) { sourceBitmap = System.Windows.Clipboard.GetImage(); }
//    if (sourceBitmap == null && _clipboardData != null) { /* 内部剪贴板逻辑 */ }
//    if (sourceBitmap == null) return;

//    if (sourceBitmap.Format != PixelFormats.Bgra32)
//        sourceBitmap = new FormatConvertedBitmap(sourceBitmap, PixelFormats.Bgra32, null, 0);

//    // --- 【新增逻辑：自动扩展画布】 ---
//    int imgW = sourceBitmap.PixelWidth;
//    int imgH = sourceBitmap.PixelHeight;
//    int canvasW = ctx.Surface.Bitmap.PixelWidth;
//    int canvasH = ctx.Surface.Bitmap.PixelHeight;

//    if (imgW > canvasW || imgH > canvasH)
//    {
//        //  s("Auto-expanding canvas for paste operation.");
//        // A. 记录调整前的全图状态 (用于 Undo)
//        Int32Rect oldRect = new Int32Rect(0, 0, canvasW, canvasH);
//        byte[] oldPixels = ctx.Surface.ExtractRegion(oldRect);

//        // B. 计算新尺寸并创建新位图
//        int newW = Math.Max(imgW, canvasW);
//        int newH = Math.Max(imgH, canvasH);
//        var newBmp = new WriteableBitmap(newW, newH,
//            ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
//            PixelFormats.Bgra32, null);
//        // --- 【新增：快速填充白色】 ---
//        newBmp.Lock();
//        unsafe
//        {
//            byte* pBackBuffer = (byte*)newBmp.BackBuffer;
//            int strides = newBmp.BackBufferStride;
//            int heights = newBmp.PixelHeight;
//            int totalBytes = strides * heights;
//            for (int i = 0; i < totalBytes; i++)
//            {
//                pBackBuffer[i] = 255;
//            }
//        }
//        newBmp.AddDirtyRect(new Int32Rect(0, 0, newW, newH));
//        newBmp.Unlock();
//        // C. 将旧画布内容写回新位图的左上角
//        newBmp.WritePixels(oldRect, oldPixels, canvasW * 4, 0);

//        // D. 替换 Surface 中的主位图
//        ctx.Surface.ReplaceBitmap(newBmp);

//        // E. 记录调整后的全图状态 (用于 Redo)
//        Int32Rect redoRect = new Int32Rect(0, 0, newW, newH);
//        byte[] redoPixels = ctx.Surface.ExtractRegion(redoRect);

//        // F. 压入 Transform 撤销栈
//        ctx.Undo.PushTransformAction(oldRect, oldPixels, redoRect, redoPixels);

//        // G. 更新 MainWindow 的 UI 状态
//        var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
//        mw.OnPropertyChanged("CanvasWidth"); // 假设你绑定了这些属性
//        mw.OnPropertyChanged("CanvasHeight");
//    }
//    // --- 【自动扩展逻辑结束】 ---

//    // 提取粘贴图片的像素数据
//    int width = sourceBitmap.PixelWidth;
//    int height = sourceBitmap.PixelHeight;
//    int stride = width * 4;
//    var newData = new byte[height * stride];
//    sourceBitmap.CopyPixels(newData, stride, 0);

//    // 更新工具状态，准备预览
//    _selectionData = newData;
//    _selectionRect = new Int32Rect(0, 0, width, height);
//    _originalRect = _selectionRect;

//    var previewBmp = new WriteableBitmap(sourceBitmap);
//    ctx.SelectionPreview.Source = previewBmp;

//    Canvas.SetLeft(ctx.SelectionPreview, 0);
//    Canvas.SetTop(ctx.SelectionPreview, 0);
//    ctx.SelectionPreview.RenderTransform = new TranslateTransform(0, 0);
//    ctx.SelectionPreview.Visibility = Visibility.Visible;
//    DrawOverlay(ctx, _selectionRect);
//    _transformStep = 0;
//}

//private async Task LoadImage(string filePath)
//{//不推荐直接使用
//    if (!File.Exists(filePath)) { s($"找不到图片文件: {filePath}"); return; }

//    try
//    {
//        // 🧩 后台线程进行解码和位图创建
//        var wb = await Task.Run(() =>
//        {
//            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

//            // 先用解码器获取原始尺寸
//            var decoder = BitmapDecoder.Create(
//                fs,
//                BitmapCreateOptions.IgnoreColorProfile,
//                BitmapCacheOption.None
//            );
//            int originalWidth = decoder.Frames[0].PixelWidth;
//            int originalHeight = decoder.Frames[0].PixelHeight;

//            fs.Position = 0; // 重置流位置以重新读取

//            var img = new BitmapImage();
//            img.BeginInit();
//            img.CacheOption = BitmapCacheOption.OnLoad;
//            img.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
//            img.StreamSource = fs;

//            // 如果超过 16384，就等比例缩放
//            const int maxSize = 16384;
//            if (originalWidth > maxSize || originalHeight > maxSize)
//            {
//                if (originalWidth >= originalHeight)
//                {
//                    img.DecodePixelWidth = maxSize;
//                }
//                else
//                {
//                    img.DecodePixelHeight = maxSize;
//                }
//            }

//            img.EndInit();
//            img.Freeze();

//            return img;
//        });

//        // ✅ 回到 UI 线程更新
//        await Dispatcher.InvokeAsync(() =>
//        {
//            _bitmap = new WriteableBitmap(wb);

//            _currentFileName = System.IO.Path.GetFileName(filePath);
//            BackgroundImage.Source = _bitmap;

//            if (_surface == null)
//                _surface = new CanvasSurface(_bitmap);
//            else
//                _surface.Attach(_bitmap);

//            _undo?.ClearUndo();
//            _undo?.ClearRedo();

//            _currentFilePath = filePath;
//            _isEdited = false;

//            SetPreviewSlider();

//            // 窗口调整逻辑
//            double imgWidth = _bitmap.Width;
//            double imgHeight = _bitmap.Height;

//            BackgroundImage.Width = imgWidth;
//            BackgroundImage.Height = imgHeight;

//            _imageSize = $"{_surface.Width}×{_surface.Height}";
//            OnPropertyChanged(nameof(ImageSize));
//            UpdateWindowTitle();

//            FitToWindow();

//        }, System.Windows.Threading.DispatcherPriority.Background);
//    }
//    catch (Exception ex)
//    {
//        s($"加载图片失败: {ex.Message}");
//    }
//}

//private void FileTabsScroller_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
//{
//    if (sender is ScrollViewer scroller)
//    {
//        // ManipulationDelta.Translation 包含了手指在 X 和 Y 方向上的移动距离
//        // 我们只关心 X 方向的移动 (水平)
//        var offset = scroller.HorizontalOffset - e.DeltaManipulation.Translation.X;

//        // 滚动到新的位置
//        scroller.ScrollToHorizontalOffset(offset);

//        // 标记事件已处理，防止其他控件响应
//        e.Handled = true;
//    }
//}