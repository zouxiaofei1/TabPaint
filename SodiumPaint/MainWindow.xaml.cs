using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;






namespace SodiumPaint
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const double ZoomStep = 0.1; // 每次滚轮缩放步进
        private const double MinZoom = 0.1;
        private const double MaxZoom = 8.0;

        private CanvasSurface _surface;
        private UndoRedoManager _undo;
        private ToolContext _ctx;
        private InputRouter _router;
        private ToolRegistry _tools;

        private string _mousePosition = "X:0, Y:0";
        public string MousePosition
        {
            get => _mousePosition;
            set { _mousePosition = value; OnPropertyChanged(); }
        }

        private string _imageSize = "0×0";
        public string ImageSize
        {
            get => _imageSize;
            set { _imageSize = value; OnPropertyChanged(); }
        }

        private string _selectionSize = "0×0";
        public string SelectionSize
        {
            get => _selectionSize;
            set { _selectionSize = value; OnPropertyChanged(); }
        }


    public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));



        public interface ITool
        {
            string Name { get; }
            Cursor Cursor { get; }

            void OnPointerDown(ToolContext ctx, Point viewPos);
            void OnPointerMove(ToolContext ctx, Point viewPos);
            void OnPointerUp(ToolContext ctx, Point viewPos);
            void OnKeyDown(ToolContext ctx, KeyEventArgs e);
        }

        public abstract class ToolBase : ITool
        {
            public abstract string Name { get; }
            public virtual Cursor Cursor => Cursors.Arrow;
            public virtual void OnPointerDown(ToolContext ctx, Point viewPos) { }
            public virtual void OnPointerMove(ToolContext ctx, Point viewPos) { }
            public virtual void OnPointerUp(ToolContext ctx, Point viewPos) { }
            public virtual void OnKeyDown(ToolContext ctx, KeyEventArgs e) { }
        }


        public class ToolContext
        {
            public CanvasSurface Surface { get; }
            public UndoRedoManager Undo { get; }
            public Color PenColor { get; set; } = Colors.Black;
            public Color EraserColor { get; set; } = Colors.White;
            public double PenThickness { get; set; } = 1.0;

            public Image ViewElement { get; } // 例如 DrawImage
            public WriteableBitmap Bitmap => Surface.Bitmap;
            public Image SelectionPreview { get; } // 预览层

            // 文档状态
            public string CurrentFilePath { get; set; } = string.Empty;
            public bool IsDirty { get; set; } = false;

            public ToolContext(CanvasSurface surface, UndoRedoManager undo, Image viewElement, Image previewElement)
            {
                Surface = surface;
                Undo = undo;
                ViewElement = viewElement;
                SelectionPreview = previewElement;
            }

            // 视图坐标 -> 像素坐标
            public Point ToPixel(Point viewPos)
            {
                var bmp = Surface.Bitmap;
                double sx = bmp.PixelWidth / ViewElement.ActualWidth;
                double sy = bmp.PixelHeight / ViewElement.ActualHeight;
                return new Point(viewPos.X * sx, viewPos.Y * sy);
            }
        }


        public class CanvasSurface
        {
            public WriteableBitmap Bitmap { get; private set; }
            public int Width => Bitmap.PixelWidth;
            public int Height => Bitmap.PixelHeight;

            public CanvasSurface(WriteableBitmap bmp) => Bitmap = bmp;
            public void Attach(WriteableBitmap bmp) => Bitmap = bmp;

            public Color GetPixel(int x, int y)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) return Colors.Transparent;
                Bitmap.Lock();
                unsafe
                {
                    byte* p = (byte*)Bitmap.BackBuffer + y * Bitmap.BackBufferStride + x * 4;
                    Color c = Color.FromArgb(p[3], p[2], p[1], p[0]);
                    Bitmap.Unlock();
                    return c;
                }
            }

            public void SetPixel(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) return;
                Bitmap.Lock();
                unsafe
                {
                    byte* p = (byte*)Bitmap.BackBuffer + y * Bitmap.BackBufferStride + x * 4;
                    p[0] = c.B; p[1] = c.G; p[2] = c.R; p[3] = c.A;
                }
                Bitmap.AddDirtyRect(new Int32Rect(x, y, 1, 1));
                Bitmap.Unlock();
            }

            // 简单整数直线（Bresenham）
            public void DrawLine(Point p1, Point p2, Color color)
            {
                int x0 = (int)p1.X, y0 = (int)p1.Y;
                int x1 = (int)p2.X, y1 = (int)p2.Y;
                int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
                int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx - dy;

                Bitmap.Lock();
                unsafe
                {
                    int stride = Bitmap.BackBufferStride;
                    while (true)
                    {
                        byte* p = (byte*)Bitmap.BackBuffer + y0 * stride + x0 * 4;
                        p[0] = color.B; p[1] = color.G; p[2] = color.R; p[3] = color.A;
                        if (x0 == x1 && y0 == y1) break;
                        int e2 = 2 * err;
                        if (e2 > -dy) { err -= dy; x0 += sx; }
                        if (e2 < dx) { err += dx; y0 += sy; }
                    }
                }
                // 更新包围矩形可在 Undo.AddDirtyRect 处理，这里只做最小刷新
                Bitmap.AddDirtyRect(new Int32Rect(
                    Math.Min((int)p1.X, (int)p2.X),
                    Math.Min((int)p1.Y, (int)p2.Y),
                    Math.Abs((int)p1.X - (int)p2.X) + 1,
                    Math.Abs((int)p1.Y - (int)p2.Y) + 1));
                Bitmap.Unlock();
            }

            public byte[] ExtractRegion(Int32Rect rect)
            {
                int stride = Bitmap.BackBufferStride;
                byte[] data = new byte[rect.Width * rect.Height * 4];
                Bitmap.Lock();
                for (int row = 0; row < rect.Height; row++)
                {
                    IntPtr src = Bitmap.BackBuffer + (rect.Y + row) * stride + rect.X * 4;
                    System.Runtime.InteropServices.Marshal.Copy(src, data, row * rect.Width * 4, rect.Width * 4);
                }
                Bitmap.Unlock();
                return data;
            }

            public void WriteRegion(Int32Rect rect, byte[] data)
            {
                if (data == null || data.Length == 0) return;
                if (rect.Width <= 0 || rect.Height <= 0) return;

                int bmpW = Bitmap.PixelWidth;
                int bmpH = Bitmap.PixelHeight;

                // 求目标与位图交集范围
                int dstX0 = Math.Max(0, rect.X);
                int dstY0 = Math.Max(0, rect.Y);
                int dstX1 = Math.Min(bmpW, rect.X + rect.Width);
                int dstY1 = Math.Min(bmpH, rect.Y + rect.Height);
                int w = dstX1 - dstX0;
                int h = dstY1 - dstY0;
                if (w <= 0 || h <= 0) return;

                // 源数据偏移（源以 rect.X/Y 为左上角）
                int srcOffsetX = dstX0 - rect.X;
                int srcOffsetY = dstY0 - rect.Y;
                int srcStride = rect.Width * 4;
                
                Bitmap.Lock();
                try
                {
                    unsafe
                    {
                        int destStride = Bitmap.BackBufferStride;
                        byte* basePtr = (byte*)Bitmap.BackBuffer;
                        for (int row = 0; row < h; row++)
                        {
                            byte* dest = basePtr + (dstY0 + row) * destStride + dstX0 * 4;
                            int srcIndex = ((srcOffsetY + row) * rect.Width + srcOffsetX) * 4;
                            System.Runtime.InteropServices.Marshal.Copy(data, srcIndex, (IntPtr)dest, w * 4);
                        }
                    }

                    Bitmap.AddDirtyRect(new Int32Rect(dstX0, dstY0, w, h));
                }
                finally
                {
                    Bitmap.Unlock();
                }
            }


        }



        public record UndoAction(Int32Rect Rect, byte[] Pixels);

        public class UndoRedoManager
        {
            private readonly CanvasSurface _surface;
            private readonly Stack<UndoAction> _undo = new();
            private readonly Stack<UndoAction> _redo = new();

            private byte[]? _preStrokeSnapshot;     // 全图快照（绘制前）
            private List<Int32Rect> _strokeRects = new(); // 本笔所有脏区

            public UndoRedoManager(CanvasSurface surface) => _surface = surface;

            public void BeginStroke()
            {
                int bytes = _surface.Bitmap.BackBufferStride * _surface.Height;
                _preStrokeSnapshot = new byte[bytes];
                _surface.Bitmap.Lock();
                System.Runtime.InteropServices.Marshal.Copy(_surface.Bitmap.BackBuffer, _preStrokeSnapshot, 0, bytes);
                _surface.Bitmap.Unlock();

                _strokeRects.Clear();
                _redo.Clear(); // 新操作截断重做链
            }

            public void AddDirtyRect(Int32Rect rect) => _strokeRects.Add(rect);

            public void CommitStroke()
            {
                if (_preStrokeSnapshot == null || _strokeRects.Count == 0) { _preStrokeSnapshot = null; return; }

                var combined = CombineRects(_strokeRects);
                byte[] region = ExtractRegionFromSnapshot(_preStrokeSnapshot, combined, _surface.Bitmap.BackBufferStride);
                _undo.Push(new UndoAction(combined, region));

                _preStrokeSnapshot = null;
            }

            public bool CanUndo => _undo.Count > 0;
            public bool CanRedo => _redo.Count > 0;

            public void Undo()
            {
                if (_undo.Count == 0) return;
                var action = _undo.Pop();

                // 保存当前区域到 redo
                var redoData = _surface.ExtractRegion(action.Rect);
                _redo.Push(new UndoAction(action.Rect, redoData));

                _surface.WriteRegion(action.Rect, action.Pixels);
            }

            public void Redo()
            {
                if (_redo.Count == 0) return;
                var action = _redo.Pop();

                // 保存当前区域到 undo
                var undoData = _surface.ExtractRegion(action.Rect);
                _undo.Push(new UndoAction(action.Rect, undoData));

                _surface.WriteRegion(action.Rect, action.Pixels);
            }

            private static Int32Rect CombineRects(List<Int32Rect> rects)
            {
                int minX = rects.Min(r => r.X);
                int minY = rects.Min(r => r.Y);
                int maxX = rects.Max(r => r.X + r.Width);
                int maxY = rects.Max(r => r.Y + r.Height);
                return new Int32Rect(minX, minY, maxX - minX, maxY - minY);
            }

            private static byte[] ExtractRegionFromSnapshot(byte[] fullData, Int32Rect rect, int stride)
            {
                byte[] region = new byte[rect.Width * rect.Height * 4];
                for (int row = 0; row < rect.Height; row++)
                {
                    int srcOffset = (rect.Y + row) * stride + rect.X * 4;
                    Buffer.BlockCopy(fullData, srcOffset, region, row * rect.Width * 4, rect.Width * 4);
                }
                return region;
            }
        }

        public class PenTool : ToolBase
        {
            public override string Name => "Pen";
            public override Cursor Cursor => Cursors.Pen;

            private bool _drawing = false;
            private Point _lastPixel;

            public override void OnPointerDown(ToolContext ctx, Point viewPos)
            {
                var px = ctx.ToPixel(viewPos);
                ctx.Undo.BeginStroke();
                _drawing = true;
                _lastPixel = px;
                ctx.Surface.SetPixel((int)px.X, (int)px.Y, ctx.PenColor);
                ctx.Undo.AddDirtyRect(new Int32Rect((int)px.X, (int)px.Y, 1, 1));
            }

            public override void OnPointerMove(ToolContext ctx, Point viewPos)
            {
                if (!_drawing) return;
                var px = ctx.ToPixel(viewPos);
                ctx.Surface.DrawLine(_lastPixel, px, ctx.PenColor);
                ctx.Undo.AddDirtyRect(LineBounds(_lastPixel, px));
                _lastPixel = px;
            }

            public override void OnPointerUp(ToolContext ctx, Point viewPos)
            {
                if (!_drawing) return;
                _drawing = false;
                ctx.Undo.CommitStroke();
                ctx.IsDirty = true;
            }

            private static Int32Rect LineBounds(Point p1, Point p2)
            {
                int x = (int)Math.Min(p1.X, p2.X);
                int y = (int)Math.Min(p1.Y, p2.Y);
                int w = (int)Math.Abs(p1.X - p2.X) + 2;
                int h = (int)Math.Abs(p1.Y - p2.Y) + 2;
                return new Int32Rect(x, y, w, h);
            }
        }

        public class EraserTool : PenTool
        {
            public override string Name => "Eraser";
            public override Cursor Cursor => Cursors.Cross;
            // 可覆写绘制颜色，或者在 PenTool 里读 ctx.PenColor，调用前把它设为 ctx.EraserColor
        }


        public class EyedropperTool : ToolBase
        {
            public override string Name => "Eyedropper";
            public override Cursor Cursor => Cursors.IBeam;

            public override void OnPointerDown(ToolContext ctx, Point viewPos)
            {
                var px = ctx.ToPixel(viewPos);
                ctx.PenColor = ctx.Surface.GetPixel((int)px.X, (int)px.Y);
            }
        }

        public class FillTool : ToolBase
        {
            public override string Name => "Fill";
            public override Cursor Cursor => Cursors.Hand;


            public override void OnPointerDown(ToolContext ctx, Point viewPos)
            {            
                var start = ctx.ToPixel(viewPos);
                // 记录绘制前
                ctx.Undo.BeginStroke();

                var target = ctx.Surface.GetPixel((int)start.X, (int)start.Y);
                if (target == ctx.PenColor) return;

                var rect = FloodFill(ctx.Surface, (int)start.X, (int)start.Y, target, ctx.PenColor);
                ctx.Undo.AddDirtyRect(rect);

                // 结束整笔
                ctx.Undo.CommitStroke();
                ctx.IsDirty = true;
            }

            // 返回填充的包围矩形（用于差分撤销）
            private Int32Rect FloodFill(CanvasSurface s, int x, int y, Color from, Color to)
            {
                int minX = x, maxX = x, minY = y, maxY = y;
                var w = s.Width; var h = s.Height;
                var q = new Queue<(int x, int y)>();
                var visited = new bool[w, h];
                q.Enqueue((x, y)); visited[x, y] = true;

                s.Bitmap.Lock();
                unsafe
                {
                    int stride = s.Bitmap.BackBufferStride;
                    while (q.Count > 0)
                    {
                        var (cx, cy) = q.Dequeue();

                        byte* p = (byte*)s.Bitmap.BackBuffer + cy * stride + cx * 4;
                        var c = Color.FromArgb(p[3], p[2], p[1], p[0]);
                        if (c.A == from.A && c.R == from.R && c.G == from.G && c.B == from.B)
                        {
                            p[0] = to.B; p[1] = to.G; p[2] = to.R; p[3] = to.A;

                            minX = Math.Min(minX, cx); maxX = Math.Max(maxX, cx);
                            minY = Math.Min(minY, cy); maxY = Math.Max(maxY, cy);

                            if (cx > 0 && !visited[cx - 1, cy]) { visited[cx - 1, cy] = true; q.Enqueue((cx - 1, cy)); }
                            if (cx < w - 1 && !visited[cx + 1, cy]) { visited[cx + 1, cy] = true; q.Enqueue((cx + 1, cy)); }
                            if (cy > 0 && !visited[cx, cy - 1]) { visited[cx, cy - 1] = true; q.Enqueue((cx, cy - 1)); }
                            if (cy < h - 1 && !visited[cx, cy + 1]) { visited[cx, cy + 1] = true; q.Enqueue((cx, cy + 1)); }
                        }
                    }
                }
                var rect = new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
                s.Bitmap.AddDirtyRect(rect);
                s.Bitmap.Unlock();
                return rect;
            }
        }


        public class SelectTool : ToolBase
        {
            public override string Name => "Select";
            public override Cursor Cursor => Cursors.Cross;

            private bool _selecting = false;
            private bool _draggingSelection = false;

            private Point _startPixel;
            private Point _clickOffset;
            private Int32Rect _selectionRect;
            private Int32Rect _originalRect;
            private byte[]? _selectionData;

            public override void OnPointerDown(ToolContext ctx, Point viewPos)
            {
                // 位图初始化检查
                if (ctx.Surface.Bitmap == null)
                    return;

                var px = ctx.ToPixel(viewPos);
                if (_selectionData != null && !IsPointInSelection(ctx.ToPixel(viewPos)))
                {
                    CommitSelection(ctx);
                    return;
                }
                if (_selectionData != null && IsPointInSelection(px))
                {
                    // 在已有选区内：进入拖动模式
                    _draggingSelection = true;
                    _clickOffset = new Point(px.X - _selectionRect.X, px.Y - _selectionRect.Y);
                    _originalRect = _selectionRect; // 保留原位置
                }
                else
                {
                    // 开始框选
                    _selecting = true;
                    _startPixel = px;
                    _selectionRect = new Int32Rect((int)px.X, (int)px.Y, 0, 0);
                    HidePreview(ctx);
                }


            }
            private void SetPreviewPosition(ToolContext ctx, int pixelX, int pixelY)
            {
                // 背景图左上角位置（UI坐标）
                var imgPos = ctx.ViewElement.TranslatePoint(new Point(0, 0), ctx.SelectionPreview.Parent as UIElement);

                // 缩放比例（像素 → UI）
                double scaleX = ctx.ViewElement.ActualWidth / ctx.Surface.Bitmap.PixelWidth;
                double scaleY = ctx.ViewElement.ActualHeight / ctx.Surface.Bitmap.PixelHeight;

                // 转换到 UI 平移
                double uiX = imgPos.X + pixelX * scaleX;
                double uiY = imgPos.Y + pixelY * scaleY;

                ctx.SelectionPreview.RenderTransform = new TranslateTransform(uiX, uiY);
            }

            public override void OnPointerMove(ToolContext ctx, Point viewPos)
            {
                var px = ctx.ToPixel(viewPos);

                if (_selecting)
                {
                    _selectionRect = MakeRect(_startPixel, px);
                    // 此时仅记录框选，不预览
                }
                else if (_draggingSelection)
                {
                    // 拖动预览层时的位置更新
                    int newX = (int)(px.X - _clickOffset.X);
                    int newY = (int)(px.Y - _clickOffset.Y);
                    SetPreviewPosition(ctx, newX, newY);
                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ((MainWindow)Application.Current.MainWindow).SelectionSize = $"{_selectionRect.Width}×{_selectionRect.Height}";
                });

            }

            public override void OnPointerUp(ToolContext ctx, Point viewPos)
            {
                var px = ctx.ToPixel(viewPos);

                if (_selecting)
                {
                    _selecting = false;
                    _selectionRect = MakeRect(_startPixel, px);

                    if (_selectionRect.Width > 0 && _selectionRect.Height > 0)
                    {
                        // 提取区域数据
                        _selectionData = ctx.Surface.ExtractRegion(_selectionRect);


                        ClearRect(ctx, _selectionRect, ctx.EraserColor);

                        // 创建预览位图
                        var previewBmp = new WriteableBitmap(_selectionRect.Width, _selectionRect.Height,
                            ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);

                        previewBmp.Lock();
                        Marshal.Copy(_selectionData, 0, previewBmp.BackBuffer, _selectionData.Length);
                        previewBmp.AddDirtyRect(new Int32Rect(0, 0, _selectionRect.Width, _selectionRect.Height));
                        previewBmp.Unlock();

                       // var imgPos = ctx.ViewElement.TranslatePoint(new Point(0, 0), ctx.SelectionPreview.Parent as UIElement);
                        ctx.SelectionPreview.Source = previewBmp;
                        //ctx.SelectionPreview.RenderTransform = new TranslateTransform(_selectionRect.X, _selectionRect.Y);
                        SetPreviewPosition(ctx, _selectionRect.X, _selectionRect.Y);

                        ctx.SelectionPreview.Visibility = Visibility.Visible;
                        
                    }
                }
                else if (_draggingSelection)
                {
                    _draggingSelection = false;
                    
                    // 应用到主图：先清空原位置
                    
                    int finalX = (int)((TranslateTransform)ctx.SelectionPreview.RenderTransform).X;
                    int finalY = (int)((TranslateTransform)ctx.SelectionPreview.RenderTransform).Y;

                    _selectionRect = new Int32Rect(finalX, finalY, _selectionRect.Width, _selectionRect.Height);


                }
            }

            public void CommitSelection(ToolContext ctx)
            {
                if (_selectionData == null) return;

                // 再写到新位置
                ctx.Surface.WriteRegion(_selectionRect, _selectionData);

                HidePreview(ctx);
                _selectionData = null;
                ctx.IsDirty = true;
            }


            private void HidePreview(ToolContext ctx)
            {
                ctx.SelectionPreview.Visibility = Visibility.Collapsed;
            }

            private static Int32Rect MakeRect(Point p1, Point p2)
            {
                int x = (int)Math.Min(p1.X, p2.X);
                int y = (int)Math.Min(p1.Y, p2.Y);
                int w = Math.Abs((int)p1.X - (int)p2.X);
                int h = Math.Abs((int)p1.Y - (int)p2.Y);
                return new Int32Rect(x, y, w, h);
            }

            private bool IsPointInSelection(Point px)
            {
                return px.X >= _selectionRect.X &&
                       px.X < _selectionRect.X + _selectionRect.Width &&
                       px.Y >= _selectionRect.Y &&
                       px.Y < _selectionRect.Y + _selectionRect.Height;
            }

            private void ClearRect(ToolContext ctx, Int32Rect rect, Color color)
            {
                ctx.Surface.Bitmap.Lock();
                unsafe
                {
                    byte* basePtr = (byte*)ctx.Surface.Bitmap.BackBuffer;
                    int stride = ctx.Surface.Bitmap.BackBufferStride;
                    for (int y = rect.Y; y < rect.Y + rect.Height; y++)
                    {
                        byte* rowPtr = basePtr + y * stride + rect.X * 4;
                        for (int x = 0; x < rect.Width; x++)
                        {
                            rowPtr[0] = color.B;
                            rowPtr[1] = color.G;
                            rowPtr[2] = color.R;
                            rowPtr[3] = color.A;
                            rowPtr += 4;
                        }
                    }
                }
                ctx.Surface.Bitmap.AddDirtyRect(rect);
                ctx.Surface.Bitmap.Unlock();
            }
        }


        public ITool Select { get; } = new SelectTool();



        public class SelectionOverlay : Canvas
        {
            public Int32Rect? SelectionRect { get; set; }

            protected override void OnRender(DrawingContext dc)
            {
                base.OnRender(dc);
                if (SelectionRect.HasValue)
                {
                    var rectPx = SelectionRect.Value;
                    Rect rect = new Rect(rectPx.X, rectPx.Y, rectPx.Width, rectPx.Height);
                    dc.DrawRectangle(null, new Pen(Brushes.Black, 1) { DashStyle = DashStyles.Dash }, rect);
                }
            }
        }

        public class InputRouter
        {
            private readonly ToolContext _ctx;
            public ITool CurrentTool { get; private set; }

            public InputRouter(ToolContext ctx, ITool defaultTool)
            {
                _ctx = ctx;
                CurrentTool = defaultTool;
                _ctx.ViewElement.MouseDown += (s, e) => CurrentTool.OnPointerDown(_ctx, e.GetPosition(_ctx.ViewElement));
                _ctx.ViewElement.MouseMove += ViewElement_MouseMove;

                //_ctx.ViewElement.MouseMove += (s, e) => CurrentTool.OnPointerMove(_ctx, e.GetPosition(_ctx.ViewElement));
                _ctx.ViewElement.MouseUp += (s, e) => CurrentTool.OnPointerUp(_ctx, e.GetPosition(_ctx.ViewElement));
            }

            private void ViewElement_MouseMove(object sender, MouseEventArgs e)
            {
               

                var position = e.GetPosition(_ctx.ViewElement);
                if (_ctx.Surface.Bitmap != null)
                {

                    Point px = _ctx.ToPixel(position);
                    ((MainWindow)Application.Current.MainWindow).MousePosition = $"X:{(int)px.X} Y:{(int)px.Y}";
                }
                CurrentTool.OnPointerMove(_ctx, position);
            }

            public void SetTool(ITool tool)
            {
                //msgbox(tool.ToString());
                CurrentTool = tool;
                Mouse.OverrideCursor = tool.Cursor;
            }

            public void OnPreviewKeyDown(object sender, KeyEventArgs e)
                => CurrentTool.OnKeyDown(_ctx, e);
        }

        public class ToolRegistry
        {
            public ITool Pen { get; } = new PenTool();
            public ITool Eraser { get; } = new EraserTool();
            public ITool Eyedropper { get; } = new EyedropperTool();
            public ITool Fill { get; } = new FillTool();
            public ITool Select { get; } = new SelectTool();
            //public ITool Text { get; } = new TextTool();
        }








        static void s<T>(T a)
        {
            Console.WriteLine(a);
        }

        static void msgbox<T>(T a)
        {
            MessageBox.Show(a.ToString(), "标题", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public MainWindow()
        {
            _currentFilePath = @"E:\dev\106117173_p12.jpg";
            InitializeComponent();
           // DataContext = new ViewModels.MainWindowViewModel();
            DataContext = this;
            LoadImage(_currentFilePath);
            


            ZoomSlider.ValueChanged += (s, e) =>
            {
                double scale = ((ViewModels.MainWindowViewModel)DataContext).ZoomScale;
                ZoomTransform.ScaleX = ZoomTransform.ScaleY = scale;
            };


            _surface = new CanvasSurface(_bitmap);
            _undo = new UndoRedoManager(_surface);
            _ctx = new ToolContext(_surface, _undo, BackgroundImage,SelectionPreview);
            _tools = new ToolRegistry();

            _router = new InputRouter(_ctx, _tools.Pen); // 默认画笔
            this.PreviewKeyDown += (s, e) =>
            {
                // 保持原有 Ctrl+Z/Y/S/N/O 与方向键导航逻辑
                MainWindow_PreviewKeyDown(s, e);
                // 再路由给当前工具（例如文本工具用键盘输入）
                _router.OnPreviewKeyDown(s, e);
            };
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
            this.Focusable = true;
            this.Focus();
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.Z:
                        Undo();
                        e.Handled = true;
                        break;
                    case Key.Y:
                        Redo();
                        e.Handled = true;
                        break;
                    case Key.S:
                        OnSaveClick(sender, e);
                        e.Handled = true;
                        break;
                    case Key.N:
                        OnNewClick(sender, e);
                        e.Handled = true;
                        break;
                    case Key.O:
                        OnOpenClick(sender, e);
                        e.Handled = true;
                        break;
                }
            }
            else
            {
                switch (e.Key)
                {
                    case Key.Left:
                        ShowPrevImage();
                        e.Handled = true; // 防止焦点导航
                        break;
                    case Key.Right:
                        ShowNextImage();
                        e.Handled = true;
                        break;
                }
            }
        }



        //private record UndoAction(Int32Rect Rect, byte[] Pixels);
        private Stack<UndoAction> _undoStack = new Stack<UndoAction>();
        private List<Int32Rect> _currentDrawRegions = new List<Int32Rect>(); // 当前笔的区域记录
        private Stack<UndoAction> _redoStack = new Stack<UndoAction>();

     

        private byte[] ExtractRegionFromSnapshot(byte[] fullData, Int32Rect rect, int stride)
        {
            int bytesPerPixel = 4;
            byte[] region = new byte[rect.Height * rect.Width * bytesPerPixel];

            for (int row = 0; row < rect.Height; row++)
            {
                int srcOffset = (rect.Y + row) * stride + rect.X * bytesPerPixel;
                int dstOffset = row * rect.Width * bytesPerPixel;
                Buffer.BlockCopy(fullData, srcOffset, region, dstOffset, rect.Width * bytesPerPixel);
            }
            return region;
        }

        private void OnUndoClick(object sender, RoutedEventArgs e) => Undo();
        private void OnRedoClick(object sender, RoutedEventArgs e) => Redo();
        private void OnDrawUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDrawing || _preDrawSnapshot == null) return;
            _isDrawing = false;

            // 计算整笔操作的区域
            if (_currentDrawRegions.Count == 0) return;
            var combined = CombineRects(_currentDrawRegions);

            // 从 _preDrawSnapshot 中提取修改区域像素
            byte[] regionData = ExtractRegionFromSnapshot(
                _preDrawSnapshot, combined, _bitmap.BackBufferStride);

            _undoStack.Push(new UndoAction(combined, regionData));

            _preDrawSnapshot = null; // 清除快照引用
            _redoStack.Clear();
            _isEdited = true;

        }
        private Int32Rect CombineRects(List<Int32Rect> rects)
        {
            if (rects.Count == 0) return new Int32Rect();

            int minX = rects.Min(r => r.X);
            int minY = rects.Min(r => r.Y);
            int maxX = rects.Max(r => r.X + r.Width);
            int maxY = rects.Max(r => r.Y + r.Height);
            return new Int32Rect(minX, minY, maxX - minX, maxY - minY);
        }

        private Int32Rect GetLineBoundingBox(Point p1, Point p2)
        {
            int x = (int)Math.Min(p1.X, p2.X);
            int y = (int)Math.Min(p1.Y, p2.Y);
            int w = (int)Math.Abs(p1.X - p2.X) + 2;
            int h = (int)Math.Abs(p1.Y - p2.Y) + 2;
            return new Int32Rect(x, y, w, h);
        }
        private void Undo() { _undo.Undo(); _ctx.IsDirty = true; }
        private void Redo() { _undo.Redo(); _ctx.IsDirty = true; }
       

        private byte[] ExtractRegionFromBitmap(WriteableBitmap bmp, Int32Rect rect)
        {
            int stride = bmp.BackBufferStride;
            byte[] region = new byte[rect.Width * rect.Height * 4];

            bmp.Lock();
            for (int row = 0; row < rect.Height; row++)
            {
                IntPtr src = bmp.BackBuffer + (rect.Y + row) * stride + rect.X * 4;
                System.Runtime.InteropServices.Marshal.Copy(src, region, row * rect.Width * 4, rect.Width * 4);
            }
            bmp.Unlock();
            return region;
        }



        private void SaveBitmap(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Create))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(_bitmap));
                encoder.Save(fs);
            }
        }
        private Color GetPixelColor(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _bmpWidth || y >= _bmpHeight) return Colors.Transparent;

            _bitmap.Lock();
            unsafe
            {
                IntPtr pBackBuffer = _bitmap.BackBuffer;
                int stride = _bitmap.BackBufferStride;
                byte* p = (byte*)pBackBuffer + y * stride + x * 4;
                byte b = p[0];
                byte g = p[1];
                byte r = p[2];
                byte a = p[3];
                _bitmap.Unlock();
                return Color.FromArgb(a, r, g, b);
            }
        }
        private void DrawPixel(int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= _bmpWidth || y >= _bmpHeight) return;

            _bitmap.Lock();
            unsafe
            {
                IntPtr pBackBuffer = _bitmap.BackBuffer;
                int stride = _bitmap.BackBufferStride;
                byte* p = (byte*)pBackBuffer + y * stride + x * 4;
                p[0] = color.B;
                p[1] = color.G;
                p[2] = color.R;
                p[3] = color.A;
            }
            _bitmap.AddDirtyRect(new Int32Rect(x, y, 1, 1));
            _bitmap.Unlock();
        }


        private void PushUndoRegion(int x, int y, int width, int height)
        {
            //sgbox("pushundo");
            // 限制区域不超出画布
            if (x < 0) { width += x; x = 0; }
            if (y < 0) { height += y; y = 0; }
            if (x + width > _bmpWidth) width = _bmpWidth - x;
            if (y + height > _bmpHeight) height = _bmpHeight - y;
            if (width <= 0 || height <= 0) return;

            int stride = _bitmap.BackBufferStride;
            byte[] data = new byte[height * width * 4]; // BGRA32

            _bitmap.Lock();
            unsafe
            {
                byte* srcPtr = (byte*)_bitmap.BackBuffer + y * stride + x * 4;
                for (int row = 0; row < height; row++)
                {
                    IntPtr srcRow = (IntPtr)(srcPtr + row * stride);
                    System.Runtime.InteropServices.Marshal.Copy(srcRow, data, row * width * 4, width * 4);
                }
            }
            _bitmap.Unlock();

            _undoStack.Push(new UndoAction(new Int32Rect(x, y, width, height), data));

            // 可选：限制最大撤销次数
            if (_undoStack.Count > 1000) _undoStack = new Stack<UndoAction>(_undoStack.Take(1000));
        }



        private void DrawLine(Point p1, Point p2, Color color)
        {
            int minX = (int)Math.Min(p1.X, p2.X);
            int minY = (int)Math.Min(p1.Y, p2.Y);
            int maxX = (int)Math.Max(p1.X, p2.X);
            int maxY = (int)Math.Max(p1.Y, p2.Y);

            // 在画线前保存该区域
            //PushUndoRegion(minX, minY, maxX - minX + 1, maxY - minY + 1);


            int x0 = (int)p1.X, y0 = (int)p1.Y;
            int x1 = (int)p2.X, y1 = (int)p2.Y;
            int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                DrawPixel(x0, y0, color);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }

        public void SetZoomAndOffset(double scaleFactor, double offsetX, double offsetY)
        {
            const double MinZoom = 0.1;
            const double MaxZoom = 16.0; // 自设上限

            scaleFactor = Math.Clamp(scaleFactor, MinZoom, MaxZoom);

            // 更新ViewModel缩放比例
            //var vm = (ViewModels.MainWindowViewModel)DataContext;
            double oldScale = zoomscale;
            zoomscale = scaleFactor;

            // 更新缩放变换
            ZoomTransform.ScaleX = ZoomTransform.ScaleY = scaleFactor;

            // 计算新的滚动偏移
            // 偏移要按当前比例转换：ScrollViewer 的偏移单位是可视区域像素，而非原图像素。
            double newOffsetX = offsetX * scaleFactor;
            double newOffsetY = offsetY * scaleFactor;

            // 应用到滚动容器
            ScrollContainer.ScrollToHorizontalOffset(newOffsetX);
            ScrollContainer.ScrollToVerticalOffset(newOffsetY);
        }


        // Ctrl + 滚轮 缩放事件
        private void OnMouseWheelZoom(object sender, MouseWheelEventArgs e)
        {

            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                return;

            e.Handled = true; // 阻止默认滚动

            //var vm = (ViewModels.MainWindowViewModel)DataContext;
            double oldScale = zoomscale;
            double newScale = oldScale + (e.Delta > 0 ? ZoomStep : -ZoomStep);
            newScale = Math.Clamp(newScale, MinZoom, MaxZoom);
            zoomscale = newScale;
            ZoomTransform.ScaleX = ZoomTransform.ScaleY = newScale;


            Point mouseInScroll = e.GetPosition(ScrollContainer);

            double offsetX = ScrollContainer.HorizontalOffset;
            double offsetY = ScrollContainer.VerticalOffset;

            // 维持鼠标相对画布位置不变的平移公式
            double newOffsetX = (offsetX + mouseInScroll.X) * (newScale / oldScale) - mouseInScroll.X;
            double newOffsetY = (offsetY + mouseInScroll.Y) * (newScale / oldScale) - mouseInScroll.Y;

            ScrollContainer.ScrollToHorizontalOffset(newOffsetX);
            ScrollContainer.ScrollToVerticalOffset(newOffsetY);
        }

        private void OnOpenClick(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "图像文件|*.png;*.jpg;*.jpeg;*.bmp"
            };
            if (dlg.ShowDialog() == true)
            {
                _currentFilePath = dlg.FileName;
                LoadImage(_currentFilePath);
                // Clean_bitmap(_bmpWidth, _bmpHeight);
            }

        }

        private double zoomscale=1;
        private byte[]? _preDrawSnapshot = null;

        private WriteableBitmap _bitmap;
        private int _bmpWidth, _bmpHeight;
        private Color _penColor = Colors.Black;
        private bool _isDrawing = false;
        private Point _lastPoint;
        private List<string> _imageFiles = new List<string>();
        private int _currentImageIndex = -1;
        private bool _isEdited = false; // 标记当前画布是否被修改



        private void ShowNextImage()
        {
            if (_imageFiles.Count == 0 || _currentImageIndex < 0) return;
            
            // 自动保存已编辑图片
            //if (_isEdited && !string.IsNullOrEmpty(_currentFilePath))
            //{
            //    SaveBitmap(_currentFilePath);
            //    _isEdited = false;
            //}

            _currentImageIndex++;
            if (_currentImageIndex >= _imageFiles.Count)
                _currentImageIndex = 0; // 循环到第一张

            LoadImage(_imageFiles[_currentImageIndex]);
        }

        private void ShowPrevImage()
        {
            if (_imageFiles.Count == 0 || _currentImageIndex < 0) return;

            // 自动保存已编辑图片
            if (_isEdited && !string.IsNullOrEmpty(_currentFilePath))
            {
                SaveBitmap(_currentFilePath);
                _isEdited = false;
            }

            _currentImageIndex--;
            if (_currentImageIndex < 0)
                _currentImageIndex = _imageFiles.Count - 1; // 循环到最后一张

            LoadImage(_imageFiles[_currentImageIndex]);
        }
        private void ClearRect(ToolContext ctx, Int32Rect rect, Color color)
        {
            ctx.Surface.Bitmap.Lock();
            unsafe
            {
                byte* basePtr = (byte*)ctx.Surface.Bitmap.BackBuffer;
                int stride = ctx.Surface.Bitmap.BackBufferStride;
                for (int y = rect.Y; y < rect.Y + rect.Height; y++)
                {
                    byte* rowPtr = basePtr + y * stride + rect.X * 4;
                    for (int x = 0; x < rect.Width; x++)
                    {
                        rowPtr[0] = color.B;
                        rowPtr[1] = color.G;
                        rowPtr[2] = color.R;
                        rowPtr[3] = color.A;
                        rowPtr += 4;
                    }
                }
            }
            ctx.Surface.Bitmap.AddDirtyRect(rect);
            ctx.Surface.Bitmap.Unlock();
        }

        private WriteableBitmap LoadBitmapWith96Dpi(string path)
        {
            // 原始加载
            var bmpImage = new BitmapImage();
            bmpImage.BeginInit();
            bmpImage.CacheOption = BitmapCacheOption.OnLoad;
            bmpImage.CreateOptions = BitmapCreateOptions.None;
            bmpImage.UriSource = new Uri(path, UriKind.Absolute);
            bmpImage.EndInit();
            bmpImage.Freeze();

            // 读取原像素数据
            int width = bmpImage.PixelWidth;
            int height = bmpImage.PixelHeight;
            int stride = width * (bmpImage.Format.BitsPerPixel / 8);
            byte[] pixels = new byte[height * stride];
            bmpImage.CopyPixels(pixels, stride, 0);

            // 创建新的 BitmapSource，设置 DPI=96
            var newSource = BitmapSource.Create(
                width,
                height,
                96,             // DpiX
                96,             // DpiY
                bmpImage.Format,
                bmpImage.Palette,
                pixels,
                stride
            );

            // 转成 WriteableBitmap
            return new WriteableBitmap(newSource);
        }


        private void LoadImage(string filePath)
        {
            if (!File.Exists(filePath))
            {
                MessageBox.Show($"找不到图片文件: {filePath}");
                return;
            }

            try
            {

                //var bitmap = new BitmapImage(new Uri(filePath, UriKind.Absolute));
                //_bmpWidth = bitmap.PixelWidth;
                //_bmpHeight = bitmap.PixelHeight;

                //bitmap.CacheOption = BitmapCacheOption.OnLoad; // 一次性读完整像素
                //bitmap.CreateOptions = BitmapCreateOptions.None; // 不延迟创建
                _bitmap = LoadBitmapWith96Dpi(filePath);
                BackgroundImage.Source = _bitmap;
                if (_surface == null)
                    _surface = new CanvasSurface(_bitmap);
                else
                    _surface.Attach(_bitmap);

                

                // BackgroundImage.Source = _bitmap;

                _currentFilePath = filePath;
                _isEdited = false; // 新加载时标记未修改

                // === 新增：扫描当前目录所有图片 ===
                string folder = System.IO.Path.GetDirectoryName(filePath);
                _imageFiles = Directory.GetFiles(folder, "*.*")
                                  .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                                           || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                                           || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                                           || f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                                  .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                  .ToList();

                _currentImageIndex = _imageFiles.IndexOf(filePath);

                // 调整窗口和画布大小
                double imgWidth = _bitmap.Width;
                double imgHeight = _bitmap.Height;



                BackgroundImage.Width = imgWidth;
                BackgroundImage.Height = imgHeight;

                // 根据屏幕情况（最多占 90%）
                double maxWidth = SystemParameters.WorkArea.Width ;
                double maxHeight = SystemParameters.WorkArea.Height;

                _imageSize = $"{_surface.Width}×{_surface.Height}";
                OnPropertyChanged(nameof(ImageSize));


                SetZoomAndOffset(Math.Min(SystemParameters.WorkArea.Width / imgWidth, SystemParameters.WorkArea.Height / imgHeight) * 0.7 , 10, 10);
                // Width = Math.Min(imgWidth + 100, maxWidth);
                //Height = Math.Min(imgHeight + 150, maxHeight);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载图片失败: {ex.Message}");
            }
        }


        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                OnSaveAsClick(sender, e); // 如果没有当前路径，就走另存为
            }
            else
            {
                SaveBitmap(_currentFilePath);
            }
        }
        private string _currentFilePath = string.Empty;

        private void OnSaveAsClick(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG 图像|*.png|JPEG 图像|*.jpg;*.jpeg|BMP 图像|*.bmp",
                FileName = "image.png"
            };
            if (dlg.ShowDialog() == true)
            {
                _currentFilePath = dlg.FileName;
                SaveBitmap(_currentFilePath);
            }
        }

        private enum ToolMode
        {
            Pen,
            Eyedropper,
            Eraser,
            Fill
        }

        private ToolMode _currentTool = ToolMode.Pen;
        private Color _eraserColor = Colors.White;
        private void OnPenClick(object s, RoutedEventArgs e) => _router.SetTool(_tools.Pen);
        private void OnPickColorClick(object s, RoutedEventArgs e) => _router.SetTool(_tools.Eyedropper);
        private void OnEraserClick(object s, RoutedEventArgs e) => _router.SetTool(_tools.Eraser);
        private void OnFillClick(object s, RoutedEventArgs e) => _router.SetTool(_tools.Fill);
        private void OnSelectClick(object s, RoutedEventArgs e) => _router.SetTool(_tools.Select);




        private void Clean_bitmap(int _bmpWidth, int _bmpHeight)
        {
            _bitmap = new WriteableBitmap(_bmpWidth, _bmpHeight, 96, 96, PixelFormats.Bgra32, null);
            BackgroundImage.Source = _bitmap;

            // 填充白色背景
            _bitmap.Lock();
            unsafe
            {
                IntPtr pBackBuffer = _bitmap.BackBuffer;
                int stride = _bitmap.BackBufferStride;
                for (int y = 0; y < _bmpHeight; y++)
                {
                    byte* row = (byte*)pBackBuffer + y * stride;
                    for (int x = 0; x < _bmpWidth; x++)
                    {
                        row[x * 4 + 0] = 255; // B
                        row[x * 4 + 1] = 255; // G
                        row[x * 4 + 2] = 255; // R
                        row[x * 4 + 3] = 255; // A
                    }
                }
            }
            _bitmap.AddDirtyRect(new Int32Rect(0, 0, _bmpWidth, _bmpHeight));
            _bitmap.Unlock();
        }

        private void OnNewClick(object sender, RoutedEventArgs e)
        {
            // 可以弹出对话框让用户输入宽高，也可以用默认尺寸
            _bmpWidth = 800;
            _bmpHeight = 600;

            Clean_bitmap(_bmpWidth, _bmpHeight);

            _currentFilePath = string.Empty; // 新建后没有路径
        }

    }
}
