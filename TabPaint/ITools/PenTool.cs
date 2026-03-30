using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using TabPaint;
using Windows.ApplicationModel.Background;
using static TabPaint.MainWindow;

public partial class PenTool : ToolBase
{
    public override string Name => "Pen";

    private System.Windows.Shapes.Path _brushCursor;
    private TranslateTransform _cursorTransform; // 用于高性能移动
    private EllipseGeometry _circleGeometry;     // 缓存圆形
    private RectangleGeometry _squareGeometry;   // 缓存方形
    private SolidColorBrush _cachedFillBrush;
    private Color _lastColor;
    private double _lastOpacity = -1;

    private bool _drawing = false;
    private Point _lastPixel;
    private float _lastPressure = 1.0f;
    private float _smoothedPressure = 1.0f;       // 平滑后的压力值
    private float _smoothedVelocity = 0f;          // 平滑后的速度
    private long _lastTimestamp = 0;                // 上次事件时间戳
    private readonly List<StrokePoint> _strokePoints = new();
    private struct StrokePoint
    {
        public float X, Y;
        public float Pressure;    // 平滑后的压力
        public float Velocity;    // 平滑后的速度
        public long Timestamp;
    }
    private byte[] _currentStrokeMask;
    private int _maskWidth;
    private int _maskHeight;
    private uint _brushSeed;
    private Int32Rect _lastStrokeDirtyRect; // 记录上一笔的脏矩形，用于局部清理 Mask
    private WriteableBitmap _maskBitmap; // 专门用于存储 mask 数据的位图
    private Image _maskImageOverlay;     // 用于显示红色遮罩的控件
    private static List<Point[]> _sprayPatterns;
    private static int _patternIndex = 0;
    private static readonly ThreadLocal<Random> _rnd = new ThreadLocal<Random>(() => new Random());
    private DispatcherTimer _pencilTimedUndoTimer;

    private bool ShouldUseTimedUndo(ToolContext ctx)
    {
        var mw = MainWindow.GetCurrentInstance();
        return ctx.PenStyle == BrushStyle.Pencil && mw != null && mw.zoomscale >= AppConsts.PencilTimedUndoMinZoom;
    }

    private void EnsurePencilTimedUndoTimer()
    {
        if (_pencilTimedUndoTimer != null) return;

        _pencilTimedUndoTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(AppConsts.PencilTimedUndoIntervalMs)
        };
        _pencilTimedUndoTimer.Tick += PencilTimedUndoTimer_Tick;
    }

    private void StartPencilTimedUndoTimer(ToolContext ctx)
    {
        if (!ShouldUseTimedUndo(ctx))
        {
            StopPencilTimedUndoTimer();
            return;
        }

        EnsurePencilTimedUndoTimer();
        _pencilTimedUndoTimer.Interval = TimeSpan.FromMilliseconds(AppConsts.PencilTimedUndoIntervalMs);
        _pencilTimedUndoTimer.Start();
    }

    private void StopPencilTimedUndoTimer()
    {
        _pencilTimedUndoTimer?.Stop();
    }

    private void PencilTimedUndoTimer_Tick(object? sender, EventArgs e)
    {
        if (!_drawing)
        {
            StopPencilTimedUndoTimer();
            return;
        }

        var mw = MainWindow.GetCurrentInstance();
        if (mw?._ctx == null || !ShouldUseTimedUndo(mw._ctx))
        {
            StopPencilTimedUndoTimer();
            return;
        }

        if (mw._ctx.Undo.HasPendingStroke)
        {
            mw._ctx.Undo.CommitStrokeAndContinue();
            mw._ctx.IsDirty = true;
        }
    }

    public override void Cleanup(ToolContext ctx)
    {
        StopPencilTimedUndoTimer();
        base.Cleanup(ctx); CleanupMask(ctx);
        _drawing = false;
        StopDrawing(ctx);

        // 清理自定义光标
        if (_brushCursor != null && ctx.EditorOverlay.Children.Contains(_brushCursor))
        {
            ctx.EditorOverlay.Children.Remove(_brushCursor);
            _brushCursor = null;
            _cursorTransform = null;
            _circleGeometry = null;
            _squareGeometry = null;
        }

        // 恢复系统光标
        if (ctx.ViewElement != null)
        {
            ctx.ViewElement.Cursor = System.Windows.Input.Cursors.Arrow;
        }
        System.Windows.Input.Mouse.OverrideCursor = null;
    }
    public override void OnMouseLeave(ToolContext ctx)
    {
        if (_brushCursor != null)  _brushCursor.Visibility = Visibility.Collapsed;
    }
    public override void SetCursor(ToolContext ctx)
    {
   
        if ((MainWindow.GetCurrentInstance()).IsViewMode) return;
        if (ctx.EditorOverlay != null)
        {
            ctx.EditorOverlay.ClipToBounds = false;
        }

        if (_brushCursor == null)
        {
            _brushCursor = new System.Windows.Shapes.Path
            {
                IsHitTestVisible = false,
                SnapsToDevicePixels = false,
                UseLayoutRounding = false,    CacheMode = new BitmapCache
                {
                    SnapsToDevicePixels = false,
                    RenderAtScale = 1.0  // 如果光标不大，1.0 就够了
                }
            };
            _cursorTransform = new TranslateTransform();
            _brushCursor.RenderTransform = _cursorTransform;
            _circleGeometry = new EllipseGeometry();
            _squareGeometry = new RectangleGeometry();
            Panel.SetZIndex(_brushCursor, AppConsts.PenCursorZIndex);
        }

        // 2. 将光标添加到图层（如果尚未添加）
        if (!ctx.EditorOverlay.Children.Contains(_brushCursor))  ctx.EditorOverlay.Children.Add(_brushCursor);
        Point currentPos = System.Windows.Input.Mouse.GetPosition(ctx.ViewElement);
        UpdateCursorVisual(ctx, currentPos); 
    }
    private void UpdateCursorVisual(ToolContext ctx, Point viewPos)
    {
        if (_brushCursor == null || _cursorTransform == null) return;
        var t = new TimeRecorder(); t.Toggle();
        double size = ctx.PenThickness;

        // 如果处于预览模式（显示尺寸小于原始尺寸），缩放光标以维持视觉比例一致
        if (ctx.FullImageWidth > 0 && ctx.ViewElement?.Source is BitmapSource bs)
        {
            if (bs.PixelWidth < ctx.FullImageWidth)
            {
                size *= (double)bs.PixelWidth / ctx.FullImageWidth;
            }
        }

        // ★ 修改：始终显示十字光标，不再因为有自定义光标就隐藏
    if (ctx.ViewElement != null && ctx.ViewElement.Cursor != System.Windows.Input.Cursors.Cross)
    {
        ctx.ViewElement.Cursor = System.Windows.Input.Cursors.Cross;
    }

    if (ctx.PenStyle == BrushStyle.Pencil || size < AppConsts.MinCustomCursorSize)
    {
        // 画笔太小时只显示十字光标，不显示自定义光标
        _brushCursor.Visibility = Visibility.Collapsed;
        return; 
    }
    else
    {
        // ★ 修改：只控制自定义光标的可见性，不再隐藏系统光标
        if (_brushCursor.Visibility != Visibility.Visible)
            _brushCursor.Visibility = Visibility.Visible;
        // ★ 删除了原来的: ctx.ViewElement.Cursor = Cursors.None;
    }

        double halfSize = size / 2.0;

        _cursorTransform.X = viewPos.X - halfSize;
        _cursorTransform.Y = viewPos.Y - halfSize;

        bool isSquare = ctx.PenStyle == BrushStyle.Square ||
                        ctx.PenStyle == BrushStyle.Eraser ||
                        ctx.PenStyle == BrushStyle.Mosaic;

        if (isSquare)
        {
            if (_brushCursor.Data != _squareGeometry) _brushCursor.Data = _squareGeometry;

            if (_squareGeometry.Rect.Width != size)   _squareGeometry.Rect = new Rect(0, 0, size, size);
        }
        else
        {
            if (_brushCursor.Data != _circleGeometry) _brushCursor.Data = _circleGeometry;
            if (_circleGeometry.RadiusX != halfSize)
            {
                _circleGeometry.Center = new Point(halfSize, halfSize);
                _circleGeometry.RadiusX = halfSize;
                _circleGeometry.RadiusY = halfSize;
            }
        }
        if (ctx.PenStyle == BrushStyle.Highlighter)
        {
            _brushCursor.Fill = new SolidColorBrush(Color.FromArgb(AppConsts.HighlighterAlpha, 255, 255, 0));
            _brushCursor.Stroke = Brushes.Yellow;
            _brushCursor.StrokeThickness = AppConsts.PenDefaultStrokeThickness;
        }else 
        if (ctx.PenStyle == BrushStyle.AiEraser)
        {
            _brushCursor.Fill = new SolidColorBrush(Color.FromArgb(AppConsts.AiEraserCursorAlpha, 255, 0, 0));
            _brushCursor.Stroke = Brushes.Red;
            _brushCursor.StrokeThickness = AppConsts.PenDefaultStrokeThickness;
        }
        else if(ctx.PenStyle == BrushStyle.Eraser)
        {
            _brushCursor.Fill = Brushes.Transparent;
            _brushCursor.Stroke = Brushes.Black;
            _brushCursor.StrokeThickness = AppConsts.PenDefaultStrokeThickness;
        }
            else if (ctx.PenStyle == BrushStyle.GaussianBlur)
            {
                _brushCursor.Fill = new SolidColorBrush(Color.FromArgb(AppConsts.GaussianBlurCursorAlpha, 0, 255, 255));
                _brushCursor.Stroke = Brushes.Cyan;
                _brushCursor.StrokeThickness = AppConsts.PenDefaultStrokeThickness;
            }

            else
            {
            double globalOpacity = ctx.PenOpacity;
            Color penColor = ctx.PenColor;

            if (_cachedFillBrush == null || _lastColor != penColor || Math.Abs(_lastOpacity - globalOpacity) > 0.001)
            {
                byte a = (byte)(penColor.A * globalOpacity);
                Color displayColor = Color.FromArgb(a, penColor.R, penColor.G, penColor.B);

                _cachedFillBrush = new SolidColorBrush(displayColor);
                if (_cachedFillBrush.CanFreeze) _cachedFillBrush.Freeze();

                _lastColor = penColor;
                _lastOpacity = globalOpacity;
            }

            _brushCursor.Fill = _cachedFillBrush;

            if (globalOpacity < AppConsts.PenLowOpacityThreshold)
            {
                _brushCursor.Stroke = new SolidColorBrush(Color.FromArgb(AppConsts.PenLowOpacityStrokeAlpha, 128, 128, 128));
                _brushCursor.StrokeThickness = AppConsts.PenLowOpacityStrokeThickness;
            }
            else  _brushCursor.Stroke = null;
        } t.Toggle(slient: true);
    }
    public override void OnPointerDown(ToolContext ctx, Point viewPos, float pressure = 1.0f)
    {
        if ((MainWindow.GetCurrentInstance()).IsViewMode) return;
        if ((MainWindow.GetCurrentInstance())._router.CurrentTool != (MainWindow.GetCurrentInstance())._tools.Pen) return;

        var px = ctx.ToPixel(viewPos);
        UpdateCursorVisual(ctx, viewPos); // 更新光标
        if (ctx.PenStyle == BrushStyle.AiEraser)
        {
            if (_maskImageOverlay == null)
            {
                _maskImageOverlay = new Image
                {
                    IsHitTestVisible = false,
                    Opacity = AppConsts.AiEraserMaskOpacity // 半透明显示
                };
                ctx.EditorOverlay.Children.Insert(0, _maskImageOverlay);
            }
            int w = ctx.Surface.Bitmap.PixelWidth;
            int h = ctx.Surface.Bitmap.PixelHeight;
            if (_maskBitmap == null || _maskBitmap.PixelWidth != w || _maskBitmap.PixelHeight != h)
            {
                _maskBitmap = new WriteableBitmap(w, h, AppConsts.StandardDpi, AppConsts.StandardDpi, PixelFormats.Bgra32, null);
                _maskImageOverlay.Source = _maskBitmap;// 确保 Image 控件填满画布
                Canvas.SetLeft(_maskImageOverlay, 0);
                Canvas.SetTop(_maskImageOverlay, 0);
                _maskImageOverlay.Width = ctx.ViewElement.ActualWidth;
                _maskImageOverlay.Height = ctx.ViewElement.ActualHeight;
            }
            ctx.CapturePointer(); System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Cross;
            _drawing = true;
            _lastPixel = ctx.ToPixel(viewPos);
            _lastPressure = pressure;
            DrawMaskLine(ctx, _lastPixel, _lastPixel, pressure); // 绘制第一笔到 Mask 上
            return; 
        }


        if (ctx.PenStyle == BrushStyle.Calligraphy || ctx.PenStyle == BrushStyle.Round || ctx.PenStyle == BrushStyle.Eraser || ctx.PenStyle == BrushStyle.Square)
        {
            if (ctx.PenStyle == BrushStyle.Calligraphy) pressure = 1.0f;
            _smoothedPressure = pressure;
            _smoothedVelocity = 0f;
            _lastTimestamp = 0;
            _strokePoints.Clear();
            // Catmull-Rom 需要重复首点来触发起始段绘制
            var startPt = new StrokePoint { X = (float)px.X, Y = (float)px.Y, Pressure = pressure };
            _strokePoints.Add(startPt);
            _strokePoints.Add(startPt);
        }
        int totalPixels = ctx.Surface.Width * ctx.Surface.Height;
        if (_currentStrokeMask == null || _currentStrokeMask.Length != totalPixels || _maskWidth != ctx.Surface.Width)
        {
            _currentStrokeMask = new byte[totalPixels];
            _maskWidth = ctx.Surface.Width;
            _maskHeight = ctx.Surface.Height;
            _lastStrokeDirtyRect = new Int32Rect(0, 0, _maskWidth, _maskHeight);
        }
        else
        {
            if (_lastStrokeDirtyRect.Width > 0 && _lastStrokeDirtyRect.Height > 0)
            {
                int xStart = _lastStrokeDirtyRect.X;
                int yStart = _lastStrokeDirtyRect.Y;
                int xEnd = xStart + _lastStrokeDirtyRect.Width;
                int yEnd = yStart + _lastStrokeDirtyRect.Height;

                for (int y = yStart; y < yEnd; y++)
                {
                    int rowOffset = y * _maskWidth;
                    Array.Clear(_currentStrokeMask, rowOffset + xStart, _lastStrokeDirtyRect.Width);
                }
            }
            _lastStrokeDirtyRect = new Int32Rect(0, 0, 0, 0); // 重置
        }
        if (ctx.PenStyle == BrushStyle.GaussianBlur)
        {
            int snapshotBytes = ctx.Surface.Bitmap.PixelHeight * ctx.Surface.Bitmap.BackBufferStride;
            if (_blurSourceSnapshot == null || _blurSourceSnapshot.Length != snapshotBytes)
                _blurSourceSnapshot = new byte[snapshotBytes];
            _blurSnapshotStride = ctx.Surface.Bitmap.BackBufferStride;

            ctx.Surface.Bitmap.Lock();
            unsafe
            {
                byte* src = (byte*)ctx.Surface.Bitmap.BackBuffer;
                fixed (byte* dst = _blurSourceSnapshot)
                {
                    Buffer.MemoryCopy(src, dst, snapshotBytes, snapshotBytes);
                }
            }
            ctx.Surface.Bitmap.Unlock();
        }


        ctx.CapturePointer(); System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Cross;
        _brushSeed = (uint)_rnd.Value.Next();
        ctx.Undo.BeginStroke();
        StartPencilTimedUndoTimer(ctx);
        _drawing = true;
        _lastPixel = px;
        _lastPressure = pressure;
        Int32Rect? dirty = null;
        ctx.Surface.Bitmap.Lock();
        unsafe
        {
            byte* backBuffer = (byte*)ctx.Surface.Bitmap.BackBuffer;
            int stride = ctx.Surface.Bitmap.BackBufferStride;
            int width = ctx.Surface.Bitmap.PixelWidth;
            int height = ctx.Surface.Bitmap.PixelHeight;

            if (IsLineBasedBrush(ctx.PenStyle)) dirty = DrawBrushLineUnsafe(ctx, px, pressure, px, pressure, backBuffer, stride, width, height);
            else  dirty = DrawBrushAtUnsafe(ctx, px, backBuffer, stride, width, height);
        }
        if (dirty.HasValue)
        {
            var finalRect = ClampRect(dirty.Value, ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);
            if (finalRect.Width > 0 && finalRect.Height > 0)
            {
                ctx.Surface.Bitmap.AddDirtyRect(finalRect);
                ctx.Undo.AddDirtyRect(finalRect);
            }
        }
        ctx.Surface.Bitmap.Unlock();
    }
    public override void OnPointerMove(ToolContext ctx, Point viewPos, float pressure = 1.0f)
    {
        if ((MainWindow.GetCurrentInstance()).IsViewMode)
        {
            if (_brushCursor != null)
            {
                _brushCursor.Visibility = Visibility.Collapsed;
            }
            return;
        }

        UpdateCursorVisual(ctx, viewPos);

        if (!_drawing) return;
        var px = ctx.ToPixel(viewPos);

        if (ctx.PenStyle == BrushStyle.AiEraser)
        {
            DrawMaskLine(ctx, _lastPixel, px, pressure);
            _lastPixel = px;
            _lastPressure = pressure;
            return;
        }

        if (ctx.PenStyle == BrushStyle.Calligraphy || ctx.PenStyle == BrushStyle.Round || ctx.PenStyle == BrushStyle.Eraser || ctx.PenStyle == BrushStyle.Square)
        {
            if (ctx.PenStyle == BrushStyle.Calligraphy)
                pressure = ComputeSmoothedCalligraphyPressure(px, _lastPixel);

            _strokePoints.Add(new StrokePoint
            {
                X = (float)px.X,
                Y = (float)px.Y,
                Pressure = pressure
            });

            if (_strokePoints.Count >= 4)
            {
                int n = _strokePoints.Count;
                var p0 = _strokePoints[n - 4];
                var p1 = _strokePoints[n - 3];
                var p2 = _strokePoints[n - 2];
                var p3 = _strokePoints[n - 1];

                ctx.Surface.Bitmap.Lock();
                try
                {
                    unsafe
                    {
                        byte* buf = (byte*)ctx.Surface.Bitmap.BackBuffer;
                        int s = ctx.Surface.Bitmap.BackBufferStride;
                        int bw = ctx.Surface.Bitmap.PixelWidth;
                        int bh = ctx.Surface.Bitmap.PixelHeight;

                        var dirty = DrawCatmullRomSegment(
                            ctx, p0, p1, p2, p3, buf, s, bw, bh);

                        if (dirty.HasValue)
                        {
                            var fr = ClampRect(dirty.Value, bw, bh);
                            if (fr.Width > 0 && fr.Height > 0)
                            {
                                ctx.Surface.Bitmap.AddDirtyRect(fr);
                                ctx.Undo.AddDirtyRect(fr);
                            }
                        }
                    }
                }
                finally { ctx.Surface.Bitmap.Unlock(); }

                if (_strokePoints.Count > 100)
                    _strokePoints.RemoveRange(0, 50);

                _lastPixel = px;
                _lastPressure = pressure;
                return;
            }
            else
            {
                // 点还不够，先连直线以减少延迟感，或者等待。
                // 样条曲线通常需要 4 个点来绘制中间那段 (p1-p2)。
                // 为了避免起笔断触，可以先画一小段。
                _lastPixel = px;
                _lastPressure = pressure;
                return;
            }
        }

        ctx.Surface.Bitmap.Lock();
        try
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            bool hasUpdate = false;

            unsafe
            {
                byte* backBuffer = (byte*)ctx.Surface.Bitmap.BackBuffer;
                int stride = ctx.Surface.Bitmap.BackBufferStride;
                int width = ctx.Surface.Bitmap.PixelWidth;
                int height = ctx.Surface.Bitmap.PixelHeight;
                Int32Rect? dirty;
                if (ctx.PenStyle == BrushStyle.Calligraphy)
                {
                    dirty = DrawCalligraphySegmentSubdivided(
                        ctx, _lastPixel, _lastPressure, px, pressure,
                        backBuffer, stride, width, height);
                }
                else
                {
                    dirty = DrawContinuousStrokeUnsafe(
                        ctx, _lastPixel, _lastPressure, px, pressure,
                        backBuffer, stride, width, height);
                }

                if (dirty.HasValue)
                {
                    hasUpdate = true;
                    minX = dirty.Value.X;
                    minY = dirty.Value.Y;
                    maxX = dirty.Value.X + dirty.Value.Width;
                    maxY = dirty.Value.Y + dirty.Value.Height;
                }
            }

            if (hasUpdate && maxX >= minX && maxY >= minY)
            {
                var finalRect = ClampRect(new Int32Rect(minX, minY, maxX - minX, maxY - minY), ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);
                if (finalRect.Width > 0 && finalRect.Height > 0)
                {
                    ctx.Surface.Bitmap.AddDirtyRect(finalRect);
                    ctx.Undo.AddDirtyRect(finalRect);
                }
            }
        }
        finally { ctx.Surface.Bitmap.Unlock();  }
        _lastPixel = px;
        _lastPressure = pressure; 
    }

    private bool IsLineBasedBrush(BrushStyle style)
    {
        return style == BrushStyle.Round ||
               style == BrushStyle.Pencil ||
               style == BrushStyle.Highlighter ||
               style == BrushStyle.Watercolor ||
               style == BrushStyle.Crayon ||
               style == BrushStyle.Calligraphy ||
               style == BrushStyle.Brush ||
               style == BrushStyle.Mosaic ||
               style == BrushStyle.Square ||
               style == BrushStyle.Eraser;
    }

    public override void OnPointerUp(ToolContext ctx, Point viewPos, float pressure = 1.0f)
    {
        if (_drawing && (ctx.PenStyle == BrushStyle.Calligraphy || ctx.PenStyle == BrushStyle.Round || ctx.PenStyle == BrushStyle.Eraser || ctx.PenStyle == BrushStyle.Square))
        {
            // 通过重复尾点完成样条曲线的最后一段
            if (_strokePoints.Count >= 1)
            {
                var last = _strokePoints[_strokePoints.Count - 1];
                _strokePoints.Add(last);
                if (_strokePoints.Count >= 4)
                {
                    int n = _strokePoints.Count;
                    ctx.Surface.Bitmap.Lock();
                    try
                    {
                        unsafe
                        {
                            byte* buf = (byte*)ctx.Surface.Bitmap.BackBuffer;
                            int s = ctx.Surface.Bitmap.BackBufferStride;
                            int bw = ctx.Surface.Bitmap.PixelWidth;
                            int bh = ctx.Surface.Bitmap.PixelHeight;
                            var dirty = DrawCatmullRomSegment(ctx, _strokePoints[n - 4], _strokePoints[n - 3], _strokePoints[n - 2], _strokePoints[n - 1], buf, s, bw, bh);
                            if (dirty.HasValue)
                            {
                                var fr = ClampRect(dirty.Value, bw, bh);
                                if (fr.Width > 0 && fr.Height > 0)
                                {
                                    ctx.Surface.Bitmap.AddDirtyRect(fr);
                                    ctx.Undo.AddDirtyRect(fr);
                                }
                            }
                        }
                    }
                    finally { ctx.Surface.Bitmap.Unlock(); }
                }
            }
        }
        StopDrawing(ctx);
    }
    public override void StopAction(ToolContext ctx) { StopDrawing(ctx); }

    public void StopDrawing(ToolContext ctx)
    {
        StopPencilTimedUndoTimer();
        if (!_drawing) return;
        _drawing = false;
        var dirtyRects = ctx.Undo.GetCurrentStrokeRects();
        if (dirtyRects != null && dirtyRects.Count > 0)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var r in dirtyRects)
            {
                if (r.X < minX) minX = r.X;
                if (r.Y < minY) minY = r.Y;
                if (r.X + r.Width > maxX) maxX = r.X + r.Width;
                if (r.Y + r.Height > maxY) maxY = r.Y + r.Height;
            }
            _lastStrokeDirtyRect = ClampRect(new Int32Rect(minX, minY, maxX - minX, maxY - minY), _maskWidth, _maskHeight);
        }
        ctx.Undo.CommitStroke();
        ctx.IsDirty = true;
        ctx.ReleasePointerCapture(); System.Windows.Input.Mouse.OverrideCursor = null;

        if (ctx.PenStyle == BrushStyle.AiEraser) {  ApplyAiEraser(ctx);  return;   }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ClampColor(int value)
    {
        if (value < 0) return 0;
        if (value > 255) return 255;
        return (byte)value;
    }

    private static void InitializeSprayPatterns()
    {
        if (_sprayPatterns != null) return;
        _sprayPatterns = new List<Point[]>();
        for (int i = 0; i < 5; i++)
            _sprayPatterns.Add(GenerateSprayPattern(200));
    }

    private static Point[] GenerateSprayPattern(int count)
    {
        Random r = new Random();
        Point[] pts = new Point[count];
        for (int i = 0; i < count; i++)
        {
            double a = r.NextDouble() * 2 * Math.PI;
            double d = Math.Sqrt(r.NextDouble());
            pts[i] = new Point(d * Math.Cos(a), d * Math.Sin(a));
        }
        return pts;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Int32Rect ClampRect(Int32Rect rect, int maxWidth, int maxHeight)
    {
        int left = Math.Max(0, rect.X);
        int top = Math.Max(0, rect.Y);
        int right = Math.Min(maxWidth, rect.X + rect.Width);
        int bottom = Math.Min(maxHeight, rect.Y + rect.Height);
        int width = Math.Max(0, right - left);
        int height = Math.Max(0, bottom - top);
        return new Int32Rect(left, top, width, height);
    }
    private static Int32Rect LineBounds(Point p1, Point p2, int penRadius)
    {
        int expand = penRadius + 2;
        int x = (int)Math.Min(p1.X, p2.X) - expand;
        int y = (int)Math.Min(p1.Y, p2.Y) - expand;
        int w = (int)Math.Abs(p1.X - p2.X) + expand * 2;
        int h = (int)Math.Abs(p1.Y - p2.Y) + expand * 2;
        return ClampRect(new Int32Rect(x, y, w, h),
            (MainWindow.GetCurrentInstance())._ctx.Bitmap.PixelWidth,
            (MainWindow.GetCurrentInstance())._ctx.Bitmap.PixelHeight);
    }
    private float ComputeSmoothedCalligraphyPressure(Point currentPixel, Point lastPixel)
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        long freq = System.Diagnostics.Stopwatch.Frequency;

        // 计算真实时间间隔（秒）
        float dt;
        if (_lastTimestamp == 0)
        {
            dt = 0.016f; // 假设 ~60fps
        }
        else
        {
            dt = (float)(now - _lastTimestamp) / freq;
            dt = Math.Clamp(dt, 0.001f, 0.1f); // 防止极端值
        }
        _lastTimestamp = now;

        // 计算像素距离
        float dx = (float)(currentPixel.X - lastPixel.X);
        float dy = (float)(currentPixel.Y - lastPixel.Y);
        float distance = MathF.Sqrt(dx * dx + dy * dy);
        float velocity = distance / dt;
        _smoothedVelocity = _smoothedVelocity + (_smoothedVelocity == 0 ? 1.0f : AppConsts.PenVelocitySmoothFactor) * (velocity - _smoothedVelocity);
        float normalizedSpeed = Math.Clamp(
            (_smoothedVelocity - AppConsts.CalligraphyMinSpeed) / (AppConsts.CalligraphyMaxSpeedPx - AppConsts.CalligraphyMinSpeed), 0f, 1f);
        float targetPressure = 1.0f - (1.0f - AppConsts.CalligraphyMinPressureVal) * MathF.Pow(normalizedSpeed, 0.6f);
        float pressureSmoothFactor;
        if (targetPressure < _smoothedPressure)
            pressureSmoothFactor = Math.Clamp(dt * 8f, 0.05f, 0.5f);
        else
            pressureSmoothFactor = Math.Clamp(dt * 4f, 0.03f, 0.3f);

        _smoothedPressure += pressureSmoothFactor * (targetPressure - _smoothedPressure);
        _smoothedPressure = Math.Clamp(_smoothedPressure, (float)AppConsts.CalligraphyMinPressureVal, 1.0f);

        return _smoothedPressure;
    }
}
