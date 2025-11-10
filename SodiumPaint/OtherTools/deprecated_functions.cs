using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

//namespace SodiumPaint.Resources
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

//namespace SodiumPaint.ViewModels
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