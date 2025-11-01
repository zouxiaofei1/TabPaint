using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using static SodiumPaint.MainWindow;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;






namespace SodiumPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private const double ZoomStep = 0.1; // 每次滚轮缩放步进
        private const double MinZoom = 0.1;
        private const double MaxZoom = 8.0;

        private CanvasSurface _surface;
        private UndoRedoManager _undo;
        private ToolContext _ctx;
        private InputRouter _router;
        private ToolRegistry _tools;
        private double zoomscale = 1;
        private byte[]? _preDrawSnapshot = null;

        private WriteableBitmap _bitmap;
        private int _bmpWidth, _bmpHeight;
        private Color _penColor = Colors.Black;
        private bool _isDrawing = false;
        private Point _lastPoint;
        private List<string> _imageFiles = new List<string>();
        private int _currentImageIndex = -1;
        private bool _isEdited = false; // 标记当前画布是否被修改
        private string _currentFileName = "未命名";
        private string _programVersion = "v0.2"; // 可以从 Assembly 读取
        private bool _isFileSaved = true; // 是否有未保存修改

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

        private double _penThickness = 5;
        public double PenThickness
        {
            get => _penThickness;
            set
            {
                if (_penThickness != value)
                {
                    _penThickness = value;
                    OnPropertyChanged(nameof(PenThickness));
                    if (_ctx != null) _ctx.PenThickness = value;
                }
            }
        }


        public interface ITool
        {
            string Name { get; }
            System.Windows.Input.Cursor Cursor { get; }

            void OnPointerDown(ToolContext ctx, Point viewPos);
            void OnPointerMove(ToolContext ctx, Point viewPos);
            void OnPointerUp(ToolContext ctx, Point viewPos);
            void OnKeyDown(ToolContext ctx, System.Windows.Input.KeyEventArgs e);
        }

        public abstract class ToolBase : ITool
        {
            public abstract string Name { get; }
            public virtual System.Windows.Input.Cursor Cursor => System.Windows.Input.Cursors.Arrow;
            public virtual void OnPointerDown(ToolContext ctx, Point viewPos) { }
            public virtual void OnPointerMove(ToolContext ctx, Point viewPos) { }
            public virtual void OnPointerUp(ToolContext ctx, Point viewPos) { }
            public virtual void OnKeyDown(ToolContext ctx, System.Windows.Input.KeyEventArgs e) { }
        }


        public class ToolContext
        {
            public CanvasSurface Surface { get; }
            public UndoRedoManager Undo { get; }
            public Color PenColor { get; set; } = Colors.Black;
            public Color EraserColor { get; set; } = Colors.White;
            public double PenThickness { get; set; } = 5.0;

            public Image ViewElement { get; } // 例如 DrawImage
            public WriteableBitmap Bitmap => Surface.Bitmap;
            public Image SelectionPreview { get; } // 预览层
            public Canvas SelectionOverlay { get; }
            public Canvas EditorOverlay { get; }
            public BrushStyle PenStyle { get; set; } = BrushStyle.Pencil;


            // 文档状态
            public string CurrentFilePath { get; set; } = string.Empty;
            public bool IsDirty { get; set; } = false;

            public ToolContext(CanvasSurface surface, UndoRedoManager undo, Image viewElement, Image previewElement, Canvas overlayElement, Canvas EditorElement)
            {
                Surface = surface;
                Undo = undo;
                ViewElement = viewElement;
                SelectionPreview = previewElement;
                SelectionOverlay = overlayElement; // ← 保存引用
                EditorOverlay = EditorElement;
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

            public void FillRectangle(Int32Rect rect, Color color)
            {
                // 检查合法区域，防止越界
                if (rect.X < 0 || rect.Y < 0) return;
                if (rect.X + rect.Width > Bitmap.PixelWidth) rect.Width = Bitmap.PixelWidth - rect.X;
                if (rect.Y + rect.Height > Bitmap.PixelHeight) rect.Height = Bitmap.PixelHeight - rect.Y;
                if (rect.Width <= 0 || rect.Height <= 0) return;

                Bitmap.Lock();

                unsafe
                {
                    byte* basePtr = (byte*)Bitmap.BackBuffer;
                    int stride = Bitmap.BackBufferStride;
                    int x0 = rect.X;
                    int y0 = rect.Y;
                    int w = rect.Width;
                    int h = rect.Height;

                    for (int y = y0; y < y0 + h; y++)
                    {
                        byte* rowPtr = basePtr + y * stride + x0 * 4;
                        for (int x = 0; x < w; x++)
                        {
                            // BGRA 顺序
                            rowPtr[0] = color.B;
                            rowPtr[1] = color.G;
                            rowPtr[2] = color.R;
                            rowPtr[3] = color.A;
                            rowPtr += 4;
                        }
                    }
                }

                Bitmap.AddDirtyRect(rect);
                Bitmap.Unlock();
            }
            public void WriteRegion(Int32Rect rect, byte[] data, int dataStride, bool transparent = true)
            {
                int bmpW = Bitmap.PixelWidth;
                int bmpH = Bitmap.PixelHeight;

                int dstX0 = Math.Max(0, rect.X);
                int dstY0 = Math.Max(0, rect.Y);
                int dstX1 = Math.Min(bmpW, rect.X + rect.Width);
                int dstY1 = Math.Min(bmpH, rect.Y + rect.Height);
                int w = dstX1 - dstX0;
                int h = dstY1 - dstY0;
                if (w <= 0 || h <= 0) return;

                int srcOffsetX = dstX0 - rect.X;
                int srcOffsetY = dstY0 - rect.Y;

                Bitmap.Lock();
                unsafe
                {

                    byte* basePtr = (byte*)Bitmap.BackBuffer;
                    int destStride = Bitmap.BackBufferStride;
                    for (int row = 0; row < h; row++)
                    {
                        if (!transparent)
                        {
                            byte* dest = basePtr + (dstY0 + row) * destStride + dstX0 * 4;
                            int srcIndex = (srcOffsetY + row) * dataStride + srcOffsetX * 4;

                            for (int col = 0; col < w; col++)
                            {
                                byte b = data[srcIndex + 0];
                                byte g = data[srcIndex + 1];
                                byte r = data[srcIndex + 2];
                                byte a = data[srcIndex + 3];

                                // 只有透明度 > 0 的像素才写入
                                if (a > 0)
                                {
                                    dest[0] = b;
                                    dest[1] = g;
                                    dest[2] = r;
                                    dest[3] = a;
                                }

                                dest += 4;
                                srcIndex += 4;
                            }
                        }
                        else
                        {
                            byte* dest = basePtr + (dstY0 + row) * destStride + dstX0 * 4;
                            int srcIndex = (srcOffsetY + row) * dataStride + srcOffsetX * 4;
                            Marshal.Copy(data, srcIndex, (IntPtr)dest, w * 4);
                        }
                    }
                }
                Bitmap.AddDirtyRect(new Int32Rect(dstX0, dstY0, w, h));
                Bitmap.Unlock();
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


        public enum UndoActionType
        {
            Draw,         // 普通绘图
            Transform,    // 旋转/翻转
            CanvasResize, // 画布拉伸或缩放
            ReplaceImage  // 整图替换（打开新图）
        }

        public record UndoAction(
              Int32Rect Rect,
              byte[] UndoPixels,
              byte[]? RedoPixelsBefore,
            UndoActionType ActionType
            );

        public class UndoRedoManager
        {
            private readonly CanvasSurface _surface;
            private readonly Stack<UndoAction> _undo = new();
            private readonly Stack<UndoAction> _redo = new();

            private byte[]? _preStrokeSnapshot;
            private readonly List<Int32Rect> _strokeRects = new();

            public UndoRedoManager(CanvasSurface surface)
            {
                _surface = surface;
            }

            public bool CanUndo => _undo.Count > 0;
            public bool CanRedo => _redo.Count > 0;

            // ---------- 绘制操作 ----------
            public void BeginStroke()
            {
                if (_surface?.Bitmap == null) return;

                int bytes = _surface.Bitmap.BackBufferStride * _surface.Height;
                _preStrokeSnapshot = new byte[bytes];
                _surface.Bitmap.Lock();
                System.Runtime.InteropServices.Marshal.Copy(_surface.Bitmap.BackBuffer, _preStrokeSnapshot, 0, bytes);
                _surface.Bitmap.Unlock();

                _strokeRects.Clear();
                _redo.Clear(); // 新操作截断重做链
            }

            public void AddDirtyRect(Int32Rect rect) => _strokeRects.Add(rect);

            public void CommitStroke()//一般绘画
            {
                if (_preStrokeSnapshot == null || _strokeRects.Count == 0 || _surface?.Bitmap == null)
                {
                    _preStrokeSnapshot = null;
                    return;
                }

                var combined = CombineRects(_strokeRects);
                byte[] region = ExtractRegionFromSnapshot(_preStrokeSnapshot, combined, _surface.Bitmap.BackBufferStride);
                _undo.Push(new UndoAction(combined, region, null, UndoActionType.Draw));

                _preStrokeSnapshot = null;
            }

            // ---------- 撤销 / 重做 ----------
            public void Undo()
            {
                if (!CanUndo || _surface?.Bitmap == null) return;

                var action = _undo.Pop();

                // 不再去新的位图上取 redo 像素，直接使用操作对象里保存的 RedoPixelsBefore
                var redoData = action.RedoPixelsBefore;
                if (redoData == null)
                {
                    // 若没有提前记录，则取当前位图的合法部分作为退化方案
                    redoData = SafeExtractRegion(action.Rect);
                }

                _redo.Push(new UndoAction(action.Rect, redoData, null, action.ActionType));

                if (action.ActionType == UndoActionType.Transform)
                {
                    var wb = new WriteableBitmap(action.Rect.Width, action.Rect.Height,
                            ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Surface.Bitmap.DpiX, ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);

                    // 替换主位图
                    ((MainWindow)System.Windows.Application.Current.MainWindow)._bitmap = wb;
                    ((MainWindow)System.Windows.Application.Current.MainWindow).BackgroundImage.Source = ((MainWindow)System.Windows.Application.Current.MainWindow)._bitmap;

                    // 让 CanvasSurface 附加新位图
                    _surface.Attach(((MainWindow)System.Windows.Application.Current.MainWindow)._bitmap);

                    // 🟢 Step 3：居中显示
                    ((MainWindow)System.Windows.Application.Current.MainWindow).BackgroundImage.Dispatcher.BeginInvoke(
                        new Action(() => ((MainWindow)System.Windows.Application.Current.MainWindow).CenterImage()),
                        System.Windows.Threading.DispatcherPriority.Loaded
                    );
                }

                _surface.WriteRegion(action.Rect, action.UndoPixels);


            }

            public void Redo()
            {
                if (!CanRedo || _surface?.Bitmap == null) return;

                var action = _redo.Pop();

                // 把当前区域存入 undo 栈
                var undoData = SafeExtractRegion(action.Rect);
                _undo.Push(new UndoAction(action.Rect, undoData, null, UndoActionType.Draw));

                _surface.WriteRegion(action.Rect, action.UndoPixels);
            }

            // ---------- 供整图操作调用 ----------
            /// <summary>
            /// 在整图变换(旋转/翻转/新建)之前，准备一个完整快照并保存redo像素
            /// </summary>
            public void PushFullImageUndo()
            {
                if (_surface?.Bitmap == null) return;

                var rect = new Int32Rect(0, 0,
                    _surface.Bitmap.PixelWidth,
                    _surface.Bitmap.PixelHeight);

                var currentPixels = SafeExtractRegion(rect);
                _undo.Push(new UndoAction(rect, currentPixels, currentPixels, UndoActionType.Draw));
                _redo.Clear();
            }

            // ---------- 辅助 ----------
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

            private byte[] SafeExtractRegion(Int32Rect rect)
            {
                // 检查合法范围，防止尺寸变化导致越界
                if (rect.X < 0 || rect.Y < 0 ||
                    rect.X + rect.Width > _surface.Bitmap.PixelWidth ||
                    rect.Y + rect.Height > _surface.Bitmap.PixelHeight ||
                    rect.Width <= 0 || rect.Height <= 0)
                {
                    // 返回当前整图快照（安全退化）
                    int bytes = _surface.Bitmap.BackBufferStride * _surface.Bitmap.PixelHeight;
                    byte[] data = new byte[bytes];
                    _surface.Bitmap.Lock();
                    System.Runtime.InteropServices.Marshal.Copy(_surface.Bitmap.BackBuffer, data, 0, bytes);
                    _surface.Bitmap.Unlock();
                    return data;
                }

                return _surface.ExtractRegion(rect);
            }
            // 将整图数据压入 Undo 栈
            public void PushUndoRegion(Int32Rect rect, byte[] pixels)
            {
                _undo.Push(new UndoAction(rect, pixels, null, UndoActionType.Draw));
            }

            public void PushUndoRegionTransform(Int32Rect rect, byte[] pixels)
            {
                _undo.Push(new UndoAction(rect, pixels, null, UndoActionType.Transform));
            }

            // 清空重做链
            public void ClearRedo()
            {
                _redo.Clear();
            }
            public void ClearUndo()
            {
                _undo.Clear();
            }
        }





        public class PenTool : ToolBase
        {
            public override string Name => "Pen";
            public override System.Windows.Input.Cursor Cursor => System.Windows.Input.Cursors.Pen;

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


            private void DrawContinuousStroke(ToolContext ctx, Point from, Point to)
            {
                // 线段长度
                double dx = to.X - from.X;
                double dy = to.Y - from.Y;
                double length = Math.Sqrt(dx * dx + dy * dy);
                if (length < 0.5) { DrawBrushAt(ctx, to); return; }

                int steps = 1;
                switch (ctx.PenStyle)
                {
                    case BrushStyle.Round:
                        steps = (int)(length / (ctx.PenThickness)); // 毛刷和喷枪更密集一些
                        if (length < ctx.PenThickness) return;
                        break;
                    case BrushStyle.Square:
                        steps = (int)(length / (ctx.PenThickness / 2));
                        break;
                    case BrushStyle.Pencil:
                        steps = (int)(length);
                        break;
                    case BrushStyle.Brush:
                        steps = (int)(length / (5 / 2));
                        if (length < (5 / 2)) return;
                        break;
                    case BrushStyle.Spray:
                        steps = (int)(length / (ctx.PenThickness));
                        if (length < ctx.PenThickness) return;
                        break;
                }
                double xStep = dx / steps;
                double yStep = dy / steps;

                double x = from.X;
                double y = from.Y;

                for (int i = 0; i <= steps; i++)
                {
                    DrawBrushAt(ctx, new Point(x, y));
                    x += xStep;
                    y += yStep;
                }
            }


            private void DrawBrushAt(ToolContext ctx, Point px)
            {
                switch (ctx.PenStyle)
                {
                    case BrushStyle.Round:
                        DrawRoundStroke(ctx, _lastPixel, px); ctx.Undo.AddDirtyRect(LineBounds(_lastPixel, px));
                        break;
                    case BrushStyle.Square:
                        DrawSquareStroke(ctx, _lastPixel, px); ctx.Undo.AddDirtyRect(LineBounds(_lastPixel, px, (int)ctx.PenThickness));
                        break;
                    case BrushStyle.Brush:
                        DrawBrushStroke(ctx, _lastPixel, px);
                        ctx.Undo.AddDirtyRect(LineBounds(_lastPixel, px, 5));
                        break;
                    case BrushStyle.Spray:
                        DrawSprayStroke(ctx, px);
                        ctx.Undo.AddDirtyRect(LineBounds(_lastPixel, px, (int)ctx.PenThickness * 2));
                        break;
                    case BrushStyle.Pencil:
                        ctx.Surface.DrawLine(_lastPixel, px, ctx.PenColor);
                        ctx.Undo.AddDirtyRect(LineBounds(_lastPixel, px));
                        break;
                }
                _lastPixel = px;

            }
            public override void OnPointerMove(ToolContext ctx, Point viewPos)
            {
                if (!_drawing) return;
                var px = ctx.ToPixel(viewPos);

                DrawContinuousStroke(ctx, _lastPixel, px);


            }

            public override void OnPointerUp(ToolContext ctx, Point viewPos)
            {
                if (!_drawing) return;
                _drawing = false;
                ctx.Undo.CommitStroke();
                ctx.IsDirty = true;
            }

            private static Int32Rect ClampRect(Int32Rect rect, int maxWidth, int maxHeight)
            {
                int x = Math.Max(0, rect.X);
                int y = Math.Max(0, rect.Y);
                int w = Math.Min(rect.Width, maxWidth - x);
                int h = Math.Min(rect.Height, maxHeight - y);
                return new Int32Rect(x, y, w, h);
            }

            private static Int32Rect LineBounds(Point p1, Point p2, int penRadius)
            {
                int expand = penRadius + 2; // 在笔粗半径基础上多扩2像素留余量
                int x = (int)Math.Min(p1.X, p2.X) - expand;
                int y = (int)Math.Min(p1.Y, p2.Y) - expand;
                int w = (int)Math.Abs(p1.X - p2.X) + expand * 2;
                int h = (int)Math.Abs(p1.Y - p2.Y) + expand * 2;
                return ClampRect(new Int32Rect(x, y, w, h), ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Bitmap.PixelWidth, ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Bitmap.PixelHeight);
            }

            //(MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Bitmap.Height

            private static Int32Rect LineBounds(Point p1, Point p2)
            {
                int x = (int)Math.Min(p1.X, p2.X);
                int y = (int)Math.Min(p1.Y, p2.Y);
                int w = (int)Math.Abs(p1.X - p2.X) + 2;
                int h = (int)Math.Abs(p1.Y - p2.Y) + 2;
                return new Int32Rect(x, y, w, h);
            }
            private void DrawRoundStroke(ToolContext ctx, Point p1, Point p2)
            {
                int r = (int)ctx.PenThickness;
                Color c = ctx.PenColor;

                ctx.Surface.Bitmap.Lock();
                unsafe
                {
                    byte* basePtr = (byte*)ctx.Surface.Bitmap.BackBuffer;
                    int stride = ctx.Surface.Bitmap.BackBufferStride;

                    double dx = p2.X - p1.X;
                    double dy = p2.Y - p1.Y;
                    double len = Math.Sqrt(dx * dx + dy * dy);
                    if (len < 0.5)
                    {
                        FillCircle(ctx, (int)p2.X, (int)p2.Y, r, c);
                        ctx.Surface.Bitmap.Unlock();
                        return;
                    }

                    double ux = dx / len;
                    double uy = dy / len;

                    // 法线
                    double nx = -uy, ny = ux;

                    int xmin = (int)Math.Min(p1.X, p2.X) - r;
                    int ymin = (int)Math.Min(p1.Y, p2.Y) - r;
                    int xmax = (int)Math.Max(p1.X, p2.X) + r;
                    int ymax = (int)Math.Max(p1.Y, p2.Y) + r;

                    for (int y = ymin; y <= ymax; y++)
                    {
                        for (int x = xmin; x <= xmax; x++)
                        {
                            // 点到线段距离
                            double vx = x - p1.X;
                            double vy = y - p1.Y;
                            double t = (vx * dx + vy * dy) / (dx * dx + dy * dy);
                            t = Math.Max(0, Math.Min(1, t));
                            double projx = p1.X + t * dx;
                            double projy = p1.Y + t * dy;
                            double dist2 = (x - projx) * (x - projx) + (y - projy) * (y - projy);
                            if (dist2 <= r * r)
                            {
                                if (x >= 0 && x < ctx.Surface.Width && y >= 0 && y < ctx.Surface.Height)
                                {
                                    byte* p = basePtr + y * stride + x * 4;
                                    p[0] = c.B; p[1] = c.G; p[2] = c.R; p[3] = c.A;
                                }
                            }
                        }
                    }
                }

                ctx.Surface.Bitmap.AddDirtyRect(ClampRect(new Int32Rect(
                    (int)Math.Min(p1.X, p2.X) - r,
                    (int)Math.Min(p1.Y, p2.Y) - r,
                    (int)Math.Abs(p2.X - p1.X) + r * 2,
                    (int)Math.Abs(p2.Y - p1.Y) + r * 2), ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Bitmap.PixelWidth, ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Bitmap.PixelHeight));
                ctx.Surface.Bitmap.Unlock();
            }

            // 单独封装圆
            private void FillCircle(ToolContext ctx, int cx, int cy, int r, Color c)
            {
                unsafe
                {
                    byte* basePtr = (byte*)ctx.Surface.Bitmap.BackBuffer;
                    int stride = ctx.Surface.Bitmap.BackBufferStride;
                    for (int y = -r; y <= r; y++)
                    {
                        int yy = cy + y;
                        if (yy < 0 || yy >= ctx.Surface.Height) continue;
                        int dx = (int)Math.Sqrt(r * r - y * y);
                        for (int x = -dx; x <= dx; x++)
                        {
                            int xx = cx + x;
                            if (xx >= 0 && xx < ctx.Surface.Width)
                            {
                                byte* p = basePtr + yy * stride + xx * 4;
                                p[0] = c.B; p[1] = c.G; p[2] = c.R; p[3] = c.A;
                            }
                        }
                    }
                }
            }




            private void DrawSquareStroke(ToolContext ctx, Point p1, Point p2)
            {
                // 方形笔就是画粗一点的正方形块
                int size = (int)ctx.PenThickness;
                ctx.Surface.FillRectangle(new Int32Rect((int)p2.X, (int)p2.Y, size, size), ctx.PenColor);
            }

            private void DrawBrushStroke(ToolContext ctx, Point p1, Point p2)
            {
                // 模拟毛刷：在中心周围加随机微扰
                Random rnd = new Random();
                for (int i = 0; i < 20; i++)
                {
                    int dx = rnd.Next(-2, 3);
                    int dy = rnd.Next(-2, 3);
                    ctx.Surface.SetPixel((int)p2.X + dx, (int)p2.Y + dy, ctx.PenColor);
                }
            }


            // 喷枪 pattern 集合
            private static List<Point[]> _sprayPatterns;
            private static int _patternIndex = 0; // 当前使用索引

            // 生成一组喷枪 pattern
            private static Point[] GenerateSprayPattern(int count)
            {
                Random r = new Random();
                Point[] pts = new Point[count];
                for (int i = 0; i < count; i++)
                {
                    double a = r.NextDouble() * 2 * Math.PI;
                    double d = Math.Sqrt(r.NextDouble()); // 均匀分布
                    pts[i] = new Point(d * Math.Cos(a), d * Math.Sin(a));
                }
                return pts;
            }

            // 一次生成多组 pattern（例如5组）
            private static void InitializeSprayPatterns()
            {
                if (_sprayPatterns != null) return; // 已生成则跳过

                _sprayPatterns = new List<Point[]>();
                for (int i = 0; i < 5; i++)
                    _sprayPatterns.Add(GenerateSprayPattern(200));
            }
            private void DrawSprayStroke(ToolContext ctx, Point p)
            {
                // 初始化喷点模式（只在第一次调用时生成）
                InitializeSprayPatterns();

                int radius = (int)ctx.PenThickness * 2;
                int count = 80;

                // 当前 pattern
                var pattern = _sprayPatterns[_patternIndex];

                // 每次使用后递增索引，循环回头
                _patternIndex = (_patternIndex + 1) % _sprayPatterns.Count;

                for (int i = 0; i < count && i < pattern.Length; i++)
                {
                    int x = (int)(p.X + pattern[i].X * radius);
                    int y = (int)(p.Y + pattern[i].Y * radius);
                    ctx.Surface.SetPixel(x, y, ctx.PenColor);
                }
            }



        }






        public class EraserTool : PenTool
        {
            public override string Name => "Eraser";
            public override System.Windows.Input.Cursor Cursor => System.Windows.Input.Cursors.Cross;
            // 可覆写绘制颜色，或者在 PenTool 里读 ctx.PenColor，调用前把它设为 ctx.EraserColor
        }


        public class EyedropperTool : ToolBase
        {
            public override string Name => "Eyedropper";
            public override System.Windows.Input.Cursor Cursor => System.Windows.Input.Cursors.IBeam;

            public override void OnPointerDown(ToolContext ctx, Point viewPos)
            {
                var px = ctx.ToPixel(viewPos);
                ctx.PenColor = ctx.Surface.GetPixel((int)px.X, (int)px.Y);
            }
        }

        public class FillTool : ToolBase
        {
            public override string Name => "Fill";
            public override System.Windows.Input.Cursor Cursor => System.Windows.Input.Cursors.Hand;


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
            public override System.Windows.Input.Cursor Cursor => System.Windows.Input.Cursors.Cross;

            private bool _selecting = false;
            public bool _draggingSelection = false;

            private Point _startPixel;
            private Point _clickOffset;
            private Int32Rect _selectionRect;
            private Int32Rect _originalRect;
            private byte[]? _selectionData;
            private int _transformStep = 0; // 0 = 未操作，>0 = 已操作
            private byte[]? _clipboardData;
            private int _clipboardWidth;
            private int _clipboardHeight;







            private ResizeAnchor _currentAnchor = ResizeAnchor.None;
            private bool _resizing = false;
            private Point _startMouse;
            private double _startW, _startH, _startX, _startY;

            // 句柄尺寸
            private const double HandleSize = 6;

            public enum ResizeAnchor
            {
                None,
                TopLeft, TopMiddle, TopRight,
                LeftMiddle, RightMiddle,
                BottomLeft, BottomMiddle, BottomRight
            }


            public void CutSelection(ToolContext ctx, bool paste)
            {
                if (_selectionData == null) return;
                int Clipwidth, Clipheight;
                if (_originalRect.Width == 0 || _originalRect.Height == 0)
                {
                    Clipwidth = _selectionRect.Width;
                    Clipheight = _selectionRect.Height;
                }
                else
                {
                    Clipwidth = _originalRect.Width;
                    Clipheight = _originalRect.Height;
                }
                // 复制到剪贴板
                if (paste)
                {
                    _clipboardWidth = Clipwidth;
                    _clipboardHeight = Clipheight;


                    _clipboardData = new byte[_selectionData.Length];
                    Array.Copy(_selectionData, _clipboardData, _selectionData.Length);
                    //s(_selectionData.Length);
                }
                else
                {
                    _clipboardData = null; _clipboardWidth = _clipboardHeight = 0;
                }
                HidePreview(ctx);
                ctx.SelectionOverlay.Children.Clear();
                ctx.SelectionOverlay.Visibility = Visibility.Collapsed;

                // 清空状态
                _selectionData = null;

            }
            public void PasteSelection(ToolContext ctx, bool ins)
            {
                if (_clipboardData == null) return;
                //s(1);


                int width = _clipboardWidth;
                int height = _clipboardHeight;
                int stride = width * 4;

                var srcBmp = BitmapSource.Create(
                    width, height,
                    ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                    PixelFormats.Bgra32, null,
                    _clipboardData, stride);

                // 计算缩放比例（默认 1）
                double scaleX = 1.0, scaleY = 1.0;
                var tg = ctx.SelectionPreview.RenderTransform as TransformGroup;
                var st = tg?.Children.OfType<ScaleTransform>().FirstOrDefault();
                if (st != null)
                {
                    scaleX = st.ScaleX;
                    scaleY = st.ScaleY;
                }

                // WPF 重采样
                var transformedBmp = new TransformedBitmap(srcBmp, new ScaleTransform(scaleX, scaleY));
                var previewBmp = new WriteableBitmap(transformedBmp);

                // 更新数据
                int newStride = previewBmp.PixelWidth * 4;
                var newData = new byte[previewBmp.PixelHeight * newStride];
                previewBmp.CopyPixels(newData, newStride, 0);

                _selectionData = newData;
                _selectionRect = new Int32Rect(0, 0, previewBmp.PixelWidth, previewBmp.PixelHeight);
                _originalRect = _selectionRect;

                // 显示预览
                ctx.SelectionPreview.Source = previewBmp;

                if (ins) // 原位置粘贴
                {
                    Canvas.SetLeft(ctx.SelectionPreview, _selectionRect.X);
                    Canvas.SetTop(ctx.SelectionPreview, _selectionRect.Y);
                    ctx.SelectionPreview.RenderTransform = new TranslateTransform(_selectionRect.X, _selectionRect.Y);
                }
                else // 左上角粘贴
                {

                    Canvas.SetLeft(ctx.SelectionPreview, 0);
                    Canvas.SetTop(ctx.SelectionPreview, 0);
                    ctx.SelectionPreview.RenderTransform = new TranslateTransform(0, 0);
                }

                ctx.SelectionPreview.Visibility = Visibility.Visible;
                DrawOverlay(ctx, _selectionRect);
            }






            public void CopySelection(ToolContext ctx)
            {
                if (_selectionData != null)
                {
                    if (_originalRect.Width == 0 || _originalRect.Height == 0)
                    {
                        _clipboardWidth = _selectionRect.Width;
                        _clipboardHeight = _selectionRect.Height;
                    }
                    else
                    {
                        _clipboardWidth = _originalRect.Width;
                        _clipboardHeight = _originalRect.Height;
                    }

                    _clipboardData = new byte[_selectionData.Length];
                    Array.Copy(_selectionData, _clipboardData, _selectionData.Length);

                    // 直接原位粘贴
                    //   PasteSelection(ctx, true);
                    HidePreview(ctx);
                    ctx.SelectionOverlay.Children.Clear();
                    ctx.SelectionOverlay.Visibility = Visibility.Collapsed;

                    // 清空状态
                    _selectionData = null;
                }
            }


            public void SelectAll(ToolContext ctx)
            {
                //  s(2);
                // 检查画布是否有效
                if (ctx.Surface?.Bitmap == null)
                    return;

                // 选区 = 整幅图像
                _selectionRect = new Int32Rect(0, 0,
            ctx.Surface.Bitmap.PixelWidth,
            ctx.Surface.Bitmap.PixelHeight);
                _originalRect = _selectionRect;

                // 提取整幅像素
                _selectionData = ctx.Surface.ExtractRegion(_selectionRect);
                if (_selectionData == null || _selectionData.Length < _selectionRect.Width * _selectionRect.Height * 4)
                    return;
                ctx.Undo.BeginStroke();
                ctx.Undo.AddDirtyRect(_selectionRect);
                ctx.Undo.CommitStroke(); // 保存这部分像素到栈

                ClearRect(ctx, _selectionRect, ctx.EraserColor);
                // 创建预览位图
                var previewBmp = new WriteableBitmap(_selectionRect.Width, _selectionRect.Height,
                    ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);

                previewBmp.WritePixels(
                    new Int32Rect(0, 0, _selectionRect.Width, _selectionRect.Height),
                    _selectionData, _selectionRect.Width * 4, 0);

                ctx.SelectionPreview.Source = previewBmp;
                ctx.SelectionPreview.Visibility = Visibility.Visible;

                // 放在画布左上角
                Canvas.SetLeft(ctx.SelectionPreview, 0);
                Canvas.SetTop(ctx.SelectionPreview, 0);

                // 绘制虚线框
                DrawOverlay(ctx, _selectionRect);
            }

            //绘制端点和虚线框
            private void DrawOverlay(ToolContext ctx, Int32Rect rect)
            {
                //return;
                double invScale = 1 / ((MainWindow)System.Windows.Application.Current.MainWindow).zoomscale;
                var overlay = ctx.SelectionOverlay;
                overlay.Children.Clear();

                // 虚线框
                var outline = new System.Windows.Shapes.Rectangle
                {
                    Stroke = Brushes.Black,
                    StrokeDashArray = new DoubleCollection { 8, 4 },
                    StrokeThickness = invScale * 1.5,
                    Width = rect.Width,
                    Height = rect.Height
                };
                RenderOptions.SetEdgeMode(outline, EdgeMode.Unspecified);  // 开抗锯齿混合
                outline.SnapsToDevicePixels = false; // 让虚线自由落在亚像素
                Canvas.SetLeft(outline, rect.X);
                Canvas.SetTop(outline, rect.Y);
                overlay.Children.Add(outline);

                // 8个句柄
                foreach (var p in GetHandlePositions(rect))
                {
                    var handle = new System.Windows.Shapes.Rectangle
                    {
                        Width = HandleSize * invScale,
                        Height = HandleSize * invScale,
                        Fill = Brushes.White,
                        Stroke = Brushes.Black,
                        StrokeThickness = invScale
                    };
                    RenderOptions.SetEdgeMode(handle, EdgeMode.Unspecified);  // 开抗锯齿混合
                    outline.SnapsToDevicePixels = false; // 让虚线自由落在亚像素
                    Canvas.SetLeft(handle, p.X - HandleSize * invScale / 2);
                    Canvas.SetTop(handle, p.Y - HandleSize / 2);
                    overlay.Children.Add(handle);
                }
                ctx.SelectionOverlay.IsHitTestVisible = false;

                ctx.SelectionOverlay.Visibility = Visibility.Visible;

            }





            private ResizeAnchor HitTestHandle(Point px, Int32Rect rect)
            {
                double size = 6 / ((MainWindow)System.Windows.Application.Current.MainWindow).zoomscale; // 句柄大小
                //s(size);
                double x1 = rect.X;
                double y1 = rect.Y;
                double x2 = rect.X + rect.Width;
                double y2 = rect.Y + rect.Height;
                double mx = (x1 + x2) / 2;
                double my = (y1 + y2) / 2;

                if (Math.Abs(px.X - x1) <= size && Math.Abs(px.Y - y1) <= size) return ResizeAnchor.TopLeft;
                if (Math.Abs(px.X - mx) <= size && Math.Abs(px.Y - y1) <= size) return ResizeAnchor.TopMiddle;
                if (Math.Abs(px.X - x2) <= size && Math.Abs(px.Y - y1) <= size) return ResizeAnchor.TopRight;
                if (Math.Abs(px.X - x1) <= size && Math.Abs(px.Y - my) <= size) return ResizeAnchor.LeftMiddle;
                if (Math.Abs(px.X - x2) <= size && Math.Abs(px.Y - my) <= size) return ResizeAnchor.RightMiddle;
                if (Math.Abs(px.X - x1) <= size && Math.Abs(px.Y - y2) <= size) return ResizeAnchor.BottomLeft;
                if (Math.Abs(px.X - mx) <= size && Math.Abs(px.Y - y2) <= size) return ResizeAnchor.BottomMiddle;
                if (Math.Abs(px.X - x2) <= size && Math.Abs(px.Y - y2) <= size) return ResizeAnchor.BottomRight;

                return ResizeAnchor.None;
            }

            private List<Point> GetHandlePositions(Int32Rect rect)
            {
                var handles = new List<Point>();
                double x1 = rect.X;
                double y1 = rect.Y;
                double x2 = rect.X + rect.Width;
                double y2 = rect.Y + rect.Height;
                double mx = (x1 + x2) / 2;
                double my = (y1 + y2) / 2;

                handles.Add(new Point(x1, y1)); // TL
                handles.Add(new Point(mx, y1)); // TM
                handles.Add(new Point(x2, y1)); // TR
                handles.Add(new Point(x1, my)); // LM
                handles.Add(new Point(x2, my)); // RM
                handles.Add(new Point(x1, y2)); // BL
                handles.Add(new Point(mx, y2)); // BM
                handles.Add(new Point(x2, y2)); // BR

                return handles;
            }



            public void ClearSelections(ToolContext ctx)
            {
                //  ctx.SelectionPreview.Visibility = Visibility.Collapsed;
                ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
                //_selectionData = null;
                _resizing = false;
                _draggingSelection = false;
                _selecting = false;
                _currentAnchor = ResizeAnchor.None;
                _selectionRect.Width = _selectionRect.Height = 0;
            }

            public override void OnPointerDown(ToolContext ctx, Point viewPos)
            {
                if (ctx.Surface.Bitmap == null)
                    return;

                var px = ctx.ToPixel(viewPos);

                // 在选区外点击 → 提交并清除
                if (_selectionData != null && !IsPointInSelection(px))
                {
                    if (HitTestHandle(px, _selectionRect) == ResizeAnchor.None)
                    {
                        CommitSelection(ctx);
                        ClearSelections(ctx);
                        return;
                    }
                }

                if (_selectionData != null)
                {
                    // 判定点击位置是句柄还是框内
                    _currentAnchor = HitTestHandle(px, _selectionRect);
                    if (_currentAnchor != ResizeAnchor.None)
                    {
                        if (_transformStep == 0) // 第一次缩放
                        {
                            _originalRect = _selectionRect;
                        }
                        _transformStep++;
                        _resizing = true;
                        _startMouse = px;
                        _startW = _selectionRect.Width;
                        _startH = _selectionRect.Height;
                        _startX = _selectionRect.X;
                        _startY = _selectionRect.Y;
                        return;

                    }
                    else if (IsPointInSelection(px))
                    {
                        if (_transformStep == 0) // 第一次拖动
                        {
                            _originalRect = _selectionRect;
                        }
                        _transformStep++;
                        _draggingSelection = true;
                        _clickOffset = new Point(px.X - _selectionRect.X, px.Y - _selectionRect.Y);
                        return;
                    }
                }

                // 开始新框选
                _selecting = true;
                _startPixel = px;
                _selectionRect = new Int32Rect((int)px.X, (int)px.Y, 0, 0);
                HidePreview(ctx);

                if (_selectionRect.Width != 0 && _selectionRect.Height != 0)
                    DrawOverlay(ctx, _selectionRect);
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
                //s(_selectionData.Length);
                var px = ctx.ToPixel(viewPos);

                // 光标样式
                if (_selectionData != null)
                {
                    var anchor = HitTestHandle(px, _selectionRect);
                    switch (anchor)
                    {
                        case ResizeAnchor.TopLeft:
                        case ResizeAnchor.BottomRight:
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeNWSE;
                            break;
                        case ResizeAnchor.TopRight:
                        case ResizeAnchor.BottomLeft:
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeNESW;
                            break;
                        case ResizeAnchor.LeftMiddle:
                        case ResizeAnchor.RightMiddle:
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeWE;
                            break;
                        case ResizeAnchor.TopMiddle:
                        case ResizeAnchor.BottomMiddle:
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeNS;
                            break;
                        default:
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.Arrow;
                            break;
                    }
                }

                // 缩放逻辑
                if (_resizing)
                {
                    double dx = px.X - _startMouse.X;
                    double dy = px.Y - _startMouse.Y;

                    // 更新选区矩形
                    switch (_currentAnchor)
                    {
                        case ResizeAnchor.TopLeft:
                            _selectionRect.X = (int)(_startX + dx);
                            _selectionRect.Y = (int)(_startY + dy);
                            _selectionRect.Width = (int)Math.Max(1, _startW - dx);
                            _selectionRect.Height = (int)Math.Max(1, _startH - dy);
                            break;
                        case ResizeAnchor.TopMiddle:
                            _selectionRect.Y = (int)(_startY + dy);
                            _selectionRect.Height = (int)Math.Max(1, _startH - dy);
                            break;
                        case ResizeAnchor.TopRight:
                            _selectionRect.Y = (int)(_startY + dy);
                            _selectionRect.Width = (int)Math.Max(1, _startW + dx);
                            _selectionRect.Height = (int)Math.Max(1, _startH - dy);
                            break;
                        case ResizeAnchor.LeftMiddle:
                            _selectionRect.X = (int)(_startX + dx);
                            _selectionRect.Width = (int)Math.Max(1, _startW - dx);
                            break;
                        case ResizeAnchor.RightMiddle:
                            _selectionRect.Width = (int)Math.Max(1, _startW + dx);
                            break;
                        case ResizeAnchor.BottomLeft:
                            _selectionRect.X = (int)(_startX + dx);
                            _selectionRect.Width = (int)Math.Max(1, _startW - dx);
                            _selectionRect.Height = (int)Math.Max(1, _startH + dy);
                            break;
                        case ResizeAnchor.BottomMiddle:
                            _selectionRect.Height = (int)Math.Max(1, _startH + dy);
                            break;
                        case ResizeAnchor.BottomRight:
                            _selectionRect.Width = (int)Math.Max(1, _startW + dx);
                            _selectionRect.Height = (int)Math.Max(1, _startH + dy);
                            break;
                    }

                    // 计算缩放比例
                    if (_originalRect.Width > 0 && _originalRect.Height > 0)
                    {
                        double scaleX = (double)_selectionRect.Width / _originalRect.Width;
                        double scaleY = (double)_selectionRect.Height / _originalRect.Height;

                        // 获取或创建 TransformGroup
                        var tg = ctx.SelectionPreview.RenderTransform as TransformGroup;
                        if (tg == null)
                        {
                            tg = new TransformGroup();
                            tg.Children.Add(new ScaleTransform(scaleX, scaleY));
                            tg.Children.Add(new TranslateTransform(_selectionRect.X, _selectionRect.Y));
                            ctx.SelectionPreview.RenderTransform = tg;
                        }
                        else
                        {
                            var s = tg.Children.OfType<ScaleTransform>().FirstOrDefault();
                            if (s != null)
                            {
                                s.ScaleX = scaleX;
                                s.ScaleY = scaleY;
                            }
                            var t = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
                            if (t != null)
                            {
                                t.X = _selectionRect.X;
                                t.Y = _selectionRect.Y;
                            }
                        }

                        ctx.SelectionPreview.Visibility = Visibility.Visible;
                    }

                    DrawOverlay(ctx, _selectionRect);
                    return;
                }

                // 框选逻辑
                if (_selecting)
                {
                    _selectionRect = MakeRect(_startPixel, px);
                    DrawOverlay(ctx, _selectionRect);
                }
                // 拖动逻辑
                else if (_draggingSelection)
                {

                    int newX = (int)(px.X - _clickOffset.X);
                    int newY = (int)(px.Y - _clickOffset.Y);

                    // 更新 TransformGroup 中的 TranslateTransform
                    var tg = ctx.SelectionPreview.RenderTransform as TransformGroup;
                    if (tg != null)
                    {
                        var t = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
                        if (t != null)
                        {
                            t.X = newX;
                            t.Y = newY;
                        }
                    }
                    else if (ctx.SelectionPreview.RenderTransform is TranslateTransform singleT)
                    {
                        singleT.X = newX;
                        singleT.Y = newY;
                    }




                    ctx.SelectionPreview.Clip = new RectangleGeometry(
                        new Rect(0, 0, 1000, 1000)
                    );

                    Int32Rect tmprc = new Int32Rect(newX, newY, _selectionRect.Width, _selectionRect.Height);

                    double canvasW = ctx.Surface.Bitmap.PixelWidth;
                    double canvasH = ctx.Surface.Bitmap.PixelHeight;

                    // 选区左上角相对于画布的偏移
                    double offsetX = tmprc.X;
                    double offsetY = tmprc.Y;

                    double ratioX = (double)_selectionRect.Width / (double)_originalRect.Width;
                    double ratioY = (double)_selectionRect.Height / (double)_originalRect.Height;

                    // 计算在预览自身坐标系中的有效显示范围
                    double visibleX = Math.Max(0, -offsetX / ratioX);
                    double visibleY = Math.Max(0, -offsetY / ratioY);
                    double visibleW = Math.Min(tmprc.Width, (canvasW - offsetX) / ratioX);
                    double visibleH = Math.Min(tmprc.Height, (canvasH - offsetY) / ratioY);
                    Geometry visibleRect = new RectangleGeometry(new Rect(visibleX, visibleY, visibleW, visibleH));
                    if (visibleW > 0 && visibleH > 0)
                    {
                        ctx.SelectionPreview.Clip = visibleRect;
                    }
                    else
                    {
                        // 超出画布完全不可见时可以隐藏掉
                        ctx.SelectionPreview.Clip = null;
                        ctx.SelectionPreview.Visibility = Visibility.Collapsed;
                    }



                    DrawOverlay(ctx, tmprc);

                    // 画布的尺寸

                }

                // 状态栏更新
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ((MainWindow)System.Windows.Application.Current.MainWindow).SelectionSize =
                        $"{_selectionRect.Width}×{_selectionRect.Height}";
                });
            }

            public override void OnKeyDown(ToolContext ctx, System.Windows.Input.KeyEventArgs e)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    switch (e.Key)
                    {
                        case Key.X:
                            //public ITool CurrentTool => Select;
                            CutSelection(ctx, true);
                            e.Handled = true;
                            break;
                        case Key.C:
                            e.Handled = true;
                            CopySelection(ctx);
                            break;
                        case Key.V:
                            PasteSelection(ctx, false);
                            e.Handled = true;
                            break;


                    }
                }
                else
                {
                    switch (e.Key)
                    {
                        case Key.Delete:
                            CutSelection(ctx, false);
                            e.Handled = true;
                            break;
                    }
                }
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
                        _selectionData = ctx.Surface.ExtractRegion(_selectionRect);
                        if (_selectionData == null || _selectionData.Length < _selectionRect.Width * _selectionRect.Height * 4)
                            return;

                        ctx.Undo.BeginStroke();
                        ctx.Undo.AddDirtyRect(_selectionRect);
                        ctx.Undo.CommitStroke(); // 保存这部分像素到栈

                        //    Debug.Print("clean");
                        ClearRect(ctx, _selectionRect, ctx.EraserColor);

                        var previewBmp = new WriteableBitmap(_selectionRect.Width, _selectionRect.Height,
                            ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);
                        int stride = _selectionRect.Width * 4;
                        previewBmp.WritePixels(new Int32Rect(0, 0, _selectionRect.Width, _selectionRect.Height),
                                               _selectionData, stride, 0);

                        ctx.SelectionPreview.Source = previewBmp;
                        SetPreviewPosition(ctx, _selectionRect.X, _selectionRect.Y);

                        ctx.SelectionPreview.Visibility = Visibility.Visible;
                    }
                }
                else if (_draggingSelection)
                {
                    _draggingSelection = false;

                    double finalX = 0, finalY = 0;

                    if (ctx.SelectionPreview.RenderTransform is TranslateTransform t1)
                    {
                        finalX = t1.X;
                        finalY = t1.Y;
                    }
                    else if (ctx.SelectionPreview.RenderTransform is TransformGroup tg)
                    {
                        var tt = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
                        if (tt != null)
                        {
                            finalX = tt.X;
                            finalY = tt.Y;
                        }
                    }

                    _selectionRect = new Int32Rect((int)finalX, (int)finalY, _selectionRect.Width, _selectionRect.Height);
                }

                if (_resizing)
                {
                    _resizing = false;
                    _currentAnchor = ResizeAnchor.None;
                    // 缩放完成后，可更新 SelectionPreview 尺寸或等待用户确认提交
                    return;
                }
                if (_selectionRect.Width != 0 && _selectionRect.Height != 0)
                {
                    DrawOverlay(ctx, _selectionRect);
                }

            }

            public void CommitSelection(ToolContext ctx)
            {
                //Debug.Print("begin");
                if (_selectionData == null) return;
                // 写回后重置计数

                // 缩放或拉伸的比例
                ctx.Undo.BeginStroke();
                ctx.Undo.AddDirtyRect(_selectionRect);
                //ctx.Undo.AddDirtyRect(_originalRect);

                if (_originalRect.Width != _selectionRect.Width || _originalRect.Height != _selectionRect.Height)
                {
                    if (_originalRect.Width <= 0 || _originalRect.Height <= 0) return;

                    int expectedStride = _originalRect.Width * 4;
                    int actualStride = _selectionData.Length / _originalRect.Height;
                    int dataStride = Math.Min(expectedStride, actualStride);


                    var src = BitmapSource.Create(
                        _originalRect.Width, _originalRect.Height,
                        ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                        PixelFormats.Bgra32, null, _selectionData, dataStride);

                    var transform = new TransformedBitmap(src, new ScaleTransform(
                        (double)_selectionRect.Width / _originalRect.Width,
                        (double)_selectionRect.Height / _originalRect.Height));

                    var resized = new WriteableBitmap(transform);
                    int newStride = resized.BackBufferStride;
                    var newData = new byte[_selectionRect.Height * newStride];
                    resized.CopyPixels(newData, newStride, 0);

                    _selectionData = newData;
                    ctx.Surface.WriteRegion(_selectionRect, _selectionData, newStride);
                }
                else
                {
                    ctx.Surface.WriteRegion(_selectionRect, _selectionData, _selectionRect.Width * 4);
                }

                ctx.Undo.CommitStroke();
                HidePreview(ctx);
                _selectionData = null;
                ctx.IsDirty = true;
                _transformStep = 0;
                _originalRect = new Int32Rect();

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


        public SelectTool Select;



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




        /// <summary>
        /// //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// </summary>
        /// 



        public class TextTool : ToolBase
        {
            public override string Name => "Text";
            public override System.Windows.Input.Cursor Cursor => System.Windows.Input.Cursors.IBeam;

            private Int32Rect _textRect;
            private System.Windows.Controls.TextBox _textBox;
            private Point _startPos;
            private bool _dragging = false;

            private ResizeAnchor _currentAnchor = ResizeAnchor.None;
            private bool _resizing = false;
            private Point _startMouse;
            private double _startW, _startH, _startX, _startY;

            // 句柄尺寸
            private const double HandleSize = 6;
            private int lag = 0;

            public enum ResizeAnchor
            {
                None,
                TopLeft, TopMiddle, TopRight,
                LeftMiddle, RightMiddle,
                BottomLeft, BottomMiddle, BottomRight
            }

            private List<Point> GetHandlePositions(Int32Rect rect)
            {
                var handles = new List<Point>();
                double x1 = rect.X;
                double y1 = rect.Y;
                double x2 = rect.X + rect.Width;
                double y2 = rect.Y + rect.Height;
                double mx = (x1 + x2) / 2;
                double my = (y1 + y2) / 2;
                //s(rect);
                handles.Add(new Point(x1, y1)); // TL
                handles.Add(new Point(mx, y1)); // TM
                handles.Add(new Point(x2, y1)); // TR
                handles.Add(new Point(x1, my)); // LM
                handles.Add(new Point(x2, my)); // RM
                handles.Add(new Point(x1, y2)); // BL
                handles.Add(new Point(mx, y2)); // BM
                handles.Add(new Point(x2, y2)); // BR

                return handles;
            }



            
            public void DrawTextboxOverlay(ToolContext ctx)
            {
                if (_textBox == null) return;

                double invScale = 1 / ((MainWindow)System.Windows.Application.Current.MainWindow).zoomscale;
                var overlay = ctx.SelectionOverlay;
                overlay.Children.Clear();

                // 获取 TextBox 坐标和尺寸
                double x = Canvas.GetLeft(_textBox);
                double y = Canvas.GetTop(_textBox);
                double w = _textBox.ActualWidth;
                double h = _textBox.ActualHeight;
                var rect = new Int32Rect((int)x, (int)y, (int)w, (int)h);
                //s(rect);
                // 虚线框
                var outline = new System.Windows.Shapes.Rectangle
                {
                    Stroke = Brushes.Black,
                    StrokeDashArray = new DoubleCollection { 8, 4 },
                    StrokeThickness = invScale * 1.5,
                    Width = rect.Width,
                    Height = rect.Height
                };
                Canvas.SetLeft(outline, rect.X);
                Canvas.SetTop(outline, rect.Y);
                overlay.Children.Add(outline);

                // 八个句柄
                foreach (var p in GetHandlePositions(rect))
                {
                    var handle = new System.Windows.Shapes.Rectangle
                    {
                        Width = HandleSize * invScale,
                        Height = HandleSize * invScale,
                        Fill = Brushes.White,
                        Stroke = Brushes.Black,
                        StrokeThickness = invScale
                    };
                    Canvas.SetLeft(handle, p.X - HandleSize * invScale / 2);
                    Canvas.SetTop(handle, p.Y - HandleSize * invScale / 2);
                    overlay.Children.Add(handle);
                }

                overlay.IsHitTestVisible = false;
                overlay.Visibility = Visibility.Visible;
            }

            // 判断是否点击到句柄
            private ResizeAnchor HitTestTextboxHandle(Point px)
            {
                if (_textBox == null) return ResizeAnchor.None;
                double size = 12 / ((MainWindow)System.Windows.Application.Current.MainWindow).zoomscale;
                double x1 = Canvas.GetLeft(_textBox);
                double y1 = Canvas.GetTop(_textBox);
                double x2 = x1 + _textBox.ActualWidth;
                double y2 = y1 + _textBox.ActualHeight;
                double mx = (x1 + x2) / 2;
                double my = (y1 + y2) / 2;

                if (Math.Abs(px.X - x1) <= size && Math.Abs(px.Y - y1) <= size) return ResizeAnchor.TopLeft;
                if (Math.Abs(px.X - mx) <= size && Math.Abs(px.Y - y1) <= size) return ResizeAnchor.TopMiddle;
                if (Math.Abs(px.X - x2) <= size && Math.Abs(px.Y - y1) <= size) return ResizeAnchor.TopRight;
                if (Math.Abs(px.X - x1) <= size && Math.Abs(px.Y - my) <= size) return ResizeAnchor.LeftMiddle;
                if (Math.Abs(px.X - x2) <= size && Math.Abs(px.Y - my) <= size) return ResizeAnchor.RightMiddle;
                if (Math.Abs(px.X - x1) <= size && Math.Abs(px.Y - y2) <= size) return ResizeAnchor.BottomLeft;
                if (Math.Abs(px.X - mx) <= size && Math.Abs(px.Y - y2) <= size) return ResizeAnchor.BottomMiddle;
                if (Math.Abs(px.X - x2) <= size && Math.Abs(px.Y - y2) <= size) return ResizeAnchor.BottomRight;

                return ResizeAnchor.None;
            }




            public override void OnPointerMove(ToolContext ctx, Point viewPos)
            {
                var px = ctx.ToPixel(viewPos);

                // 💡 一段独立：根据鼠标所在的句柄更新光标形状
                if (_textBox != null)
                {
                    //Debug.Print("11");
                    var anchor = HitTestTextboxHandle(px); // 只检测，不缩放
                    switch (anchor)
                    {
                        case ResizeAnchor.TopLeft:
                        case ResizeAnchor.BottomRight:
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeNWSE;
                            break;
                        case ResizeAnchor.TopRight:
                        case ResizeAnchor.BottomLeft:
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeNESW;
                            break;
                        case ResizeAnchor.LeftMiddle:
                        case ResizeAnchor.RightMiddle:
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeWE;
                            break;
                        case ResizeAnchor.TopMiddle:
                        case ResizeAnchor.BottomMiddle:
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeNS;
                            break;
                        default:
                            // 非句柄位置 → 普通箭头
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.Arrow;
                            break;
                    }
                }

                // 🧩 真正的缩放逻辑，只有在 _resizing == true 时执行
                if (_resizing && _textBox != null)
                {
                    double dx = px.X - _startMouse.X;
                    double dy = px.Y - _startMouse.Y;

                    switch (_currentAnchor)
                    {
                        case ResizeAnchor.TopLeft:
                            Canvas.SetLeft(_textBox, _startX + dx);
                            Canvas.SetTop(_textBox, _startY + dy);
                            _textBox.Width = Math.Max(1, _startW - dx);
                            _textBox.Height = Math.Max(1, _startH - dy);
                            break;
                        case ResizeAnchor.TopMiddle:
                            Canvas.SetTop(_textBox, _startY + dy);
                            _textBox.Height = Math.Max(1, _startH - dy);
                            break;
                        case ResizeAnchor.TopRight:
                            _textBox.Width = Math.Max(1, _startW + dx);
                            Canvas.SetTop(_textBox, _startY + dy);
                            _textBox.Height = Math.Max(1, _startH - dy);
                            break;
                        case ResizeAnchor.LeftMiddle:
                            Canvas.SetLeft(_textBox, _startX + dx);
                            _textBox.Width = Math.Max(1, _startW - dx);
                            break;
                        case ResizeAnchor.RightMiddle:
                            _textBox.Width = Math.Max(1, _startW + dx);
                            break;
                        case ResizeAnchor.BottomLeft:
                            Canvas.SetLeft(_textBox, _startX + dx);
                            _textBox.Width = Math.Max(1, _startW - dx);
                            _textBox.Height = Math.Max(1, _startH + dy);
                            break;
                        case ResizeAnchor.BottomMiddle:
                            _textBox.Height = Math.Max(1, _startH + dy);
                            break;
                        case ResizeAnchor.BottomRight:
                            _textBox.Width = Math.Max(1, _startW + dx);
                            _textBox.Height = Math.Max(1, _startH + dy);
                            break;
                    }

                    DrawTextboxOverlay(ctx);
                }
            }


            public override void OnPointerDown(ToolContext ctx, Point viewPos)
            {
               // Debug.Print("1123245");
                if (_textBox != null)
                {
                    Point p = viewPos;
                    double left = Canvas.GetLeft(_textBox);
                    double top = Canvas.GetTop(_textBox);

                    bool inside = p.X >= left && p.X <= left + _textBox.ActualWidth &&
                                  p.Y >= top && p.Y <= top + _textBox.ActualHeight;

                    if (inside)
                    {
                        // 点击内部 → 选中并进入编辑
                        ctx.EditorOverlay.IsHitTestVisible = true;
                        //   s(1);
                        SelectCurrentBox();
                        return;
                    }
                    else
                    {
                        // 点击外部 → 提交到画布


                        
                        CommitText(ctx);
                        DeselectCurrentBox(ctx);
                        ctx.EditorOverlay.IsHitTestVisible = false;
                        return;
                    }
                }
                else
                {
                    // 没有编辑框 → 记录起点
                    _startPos = viewPos;
                    _dragging = true;
                }

            }
            private bool IsInsideBorder(Point px)
            {
                if (_textBox == null) return false;

                double x = Canvas.GetLeft(_textBox);
                double y = Canvas.GetTop(_textBox);
                double w = _textBox.ActualWidth;
                double h = _textBox.ActualHeight;
                double borderThickness = 5 / ((MainWindow)System.Windows.Application.Current.MainWindow).zoomscale;

                // 外矩形 (扩大边框宽度)
                bool inOuter = px.X >= x - borderThickness &&
                               px.X <= x + w + borderThickness &&
                               px.Y >= y - borderThickness &&
                               px.Y <= y + h + borderThickness;

                // 内矩形 (缩小边框宽度)
                bool inInner = px.X >= x + borderThickness &&
                               px.X <= x + w - borderThickness &&
                               px.Y >= y + borderThickness &&
                               px.Y <= y + h - borderThickness;

               

                // 必须在外矩形内 && 不在内矩形内 → 才是边框区域
                return inOuter && !inInner;
            }


            public override void OnPointerUp(ToolContext ctx, Point viewPos)
            {
                if (_resizing)
                {
                    _resizing = false;// return;
                }
                if (_dragging && _textBox == null)
                {
                    if (lag > 0)
                    {
                        lag -= 1;
                        return;
                    }
                    _dragging = false;

                    _textBox = CreateTextBox(ctx, _startPos.X, _startPos.Y);
                    _textBox.Width = 500;
                    _textBox.MinHeight = 20;
                    _textBox.Height = Double.NaN;



                    // ⬇️ 通知主窗口显示状态栏

                    ctx.EditorOverlay.Visibility = Visibility.Visible;
                    ctx.EditorOverlay.IsHitTestVisible = true;
                    Canvas.SetZIndex(ctx.EditorOverlay, 999);
                    ctx.EditorOverlay.Children.Add(_textBox);



                    ((MainWindow)System.Windows.Application.Current.MainWindow).ShowTextToolbarFor(_textBox);


                    // 绘制虚线框和8个句柄 ⚡⚡
                    _textBox.Loaded += (s, e) =>
                    {
                        DrawTextboxOverlay(ctx); // 已布局完成
                    };

                    ctx.EditorOverlay.PreviewMouseUp += (s, e) =>
                    {
                        Point pos = e.GetPosition(ctx.EditorOverlay);
                        OnPointerUp(ctx, pos);
                    };


                    ctx.EditorOverlay.PreviewMouseMove += (s, e) =>
                    {
                        Point pos = e.GetPosition(ctx.EditorOverlay);
                        OnPointerMove(ctx, pos);
                    };

                    // 👉 在这里添加 PreviewMouseDown 事件绑定
                    ctx.EditorOverlay.PreviewMouseDown += (s, e) =>
                    {
                        Point pos = e.GetPosition(ctx.EditorOverlay);

                        var anchor = HitTestTextboxHandle(ctx.ToPixel(pos));
                        if (anchor != ResizeAnchor.None)
                        {

                            _resizing = true;
                            _currentAnchor = anchor;
                            _startMouse = ctx.ToPixel(pos);
                            _startW = _textBox.ActualWidth;
                            _startH = _textBox.ActualHeight;
                            _startX = Canvas.GetLeft(_textBox);
                            _startY = Canvas.GetTop(_textBox);

                            e.Handled = true; // 防止 TextBox 获取点击焦点
                        }
                        else
                        {
                            a.s(pos);
                            // 点击边框区域时启用拖动整个 TextBox
                            if (IsInsideBorder(ctx.ToPixel(pos)))
                            {
                               
                                _dragging = true;
                                _startMouse = ctx.ToPixel(viewPos);
                                _startX = Canvas.GetLeft(_textBox);
                                _startY = Canvas.GetTop(_textBox);
                            }
                            else
                                OnPointerDown(ctx, pos);
                        }
                        

                    };


                    // 如果需要可以这里设置 Delete 键删除逻辑
                    _textBox.PreviewKeyDown += (s, e) =>
                    {
                        if (e.Key == Key.Delete)
                        {
                            CommitText(ctx);
                            ctx.EditorOverlay.Children.Remove(_textBox);
                            _textBox = null;
                            ctx.EditorOverlay.IsHitTestVisible = false;
                            e.Handled = true;
                        }
                    };

                    _textBox.Focusable = true;
                    _textBox.Loaded += (s, e) => _textBox.Focus();
                }
            }



            private void SelectCurrentBox()
            {
                if (_textBox != null)
                {
                    Keyboard.Focus(_textBox);
                    _textBox.Focus();
                }
            }

            private void DeselectCurrentBox(ToolContext ctx)
            {
                if (_textBox != null)
                {
                    ctx.EditorOverlay.Children.Remove(_textBox);
                    _textBox = null;
                }
            }


            private System.Windows.Controls.TextBox CreateTextBox(ToolContext ctx, double x, double y)
            {
                var tb = new System.Windows.Controls.TextBox
                {
                    FontSize = 16,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(ctx.PenColor),
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent
                };

                Canvas.SetLeft(tb, x);
                Canvas.SetTop(tb, y);
                return tb;
            }


            public void CommitText(ToolContext ctx)
            {
                if (_textBox == null || string.IsNullOrWhiteSpace(_textBox.Text))
                    return;
                double x = Canvas.GetLeft(_textBox);
                double y = Canvas.GetTop(_textBox);
                var dpiInfo = VisualTreeHelper.GetDpi(_textBox);
                ctx.Undo.BeginStroke();
                ctx.Undo.AddDirtyRect(_textRect);
                double pixelsPerDip = VisualTreeHelper.GetDpi(ctx.ViewElement).PixelsPerDip;
                // 将文字渲染到位图
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawText(
                        new FormattedText(
                            _textBox.Text,
                            System.Globalization.CultureInfo.CurrentCulture,
                            System.Windows.FlowDirection.LeftToRight,
                            new Typeface(_textBox.FontFamily, _textBox.FontStyle, _textBox.FontWeight, _textBox.FontStretch),
                            _textBox.FontSize,
                            _textBox.Foreground,
                            pixelsPerDip
                        ),
                        new Point(0, 0));

                }
                TextOptions.SetTextRenderingMode(_textBox, TextRenderingMode.ClearType);
                TextOptions.SetTextFormattingMode(_textBox, TextFormattingMode.Display);

                // 渲染为图像并写入 Surface
                var bmp = new RenderTargetBitmap((int)_textBox.ActualWidth, (int)_textBox.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(visual);

                var wb = new WriteableBitmap(bmp);
                int stride = wb.PixelWidth * 4;
                var pixels = new byte[wb.PixelHeight * stride];
                wb.CopyPixels(pixels, stride, 0);

                ctx.Undo.BeginStroke();
                ctx.Undo.AddDirtyRect(new Int32Rect((int)x, (int)y, wb.PixelWidth, wb.PixelHeight));
                ctx.Surface.WriteRegion(new Int32Rect((int)x, (int)y, wb.PixelWidth, wb.PixelHeight), pixels, stride, false);
                ctx.Undo.CommitStroke();
                ((MainWindow)System.Windows.Application.Current.MainWindow).HideTextToolbar();
                // 从 UI 移除 TextBox
                ctx.SelectionOverlay.Children.Clear();
                ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
                ctx.EditorOverlay.Children.Remove(_textBox);
                lag = 1;
            }
        }



        /// <summary>
        /// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// </summary>


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

            private void ViewElement_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
            {


                var position = e.GetPosition(_ctx.ViewElement);
                if (_ctx.Surface.Bitmap != null)
                {

                    Point px = _ctx.ToPixel(position);
                    ((MainWindow)System.Windows.Application.Current.MainWindow).MousePosition = $"X:{(int)px.X} Y:{(int)px.Y}";
                }
                CurrentTool.OnPointerMove(_ctx, position);
            }

            public void SetTool(ITool tool)
            {
                CurrentTool = tool;
                Mouse.OverrideCursor = tool.Cursor;
            }

            public void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
                => CurrentTool.OnKeyDown(_ctx, e);
        }

        public SolidColorBrush ForegroundBrush { get; set; } = new SolidColorBrush(Colors.Black);
        public SolidColorBrush BackgroundBrush { get; set; } = new SolidColorBrush(Colors.White);

        private void OnForegroundColorClick(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.ColorDialog();
            dlg.Color = System.Drawing.Color.FromArgb(ForegroundBrush.Color.A,
                                                      ForegroundBrush.Color.R,
                                                      ForegroundBrush.Color.G,
                                                      ForegroundBrush.Color.B);
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ForegroundBrush = new SolidColorBrush(
                    Color.FromArgb(255, dlg.Color.R, dlg.Color.G, dlg.Color.B));
                DataContext = this; // 刷新绑定

                _ctx.PenColor = ForegroundBrush.Color;
                UpdateForegroundButtonColor(ForegroundBrush.Color);

            }
        }

        private void OnBackgroundColorClick(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.ColorDialog();
            dlg.Color = System.Drawing.Color.FromArgb(BackgroundBrush.Color.A,
                                                      BackgroundBrush.Color.R,
                                                      BackgroundBrush.Color.G,
                                                      BackgroundBrush.Color.B);
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BackgroundBrush = new SolidColorBrush(
                    Color.FromArgb(255, dlg.Color.R, dlg.Color.G, dlg.Color.B));
                DataContext = this; // 刷新绑定
                UpdateBackgroundButtonColor(BackgroundBrush.Color);
            }
        }


        private void OnColorButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Background is SolidColorBrush brush)
            {
                SelectedBrush = new SolidColorBrush(brush.Color);

                // 如果你有 ToolContext，可同步笔颜色，例如：
                _ctx.PenColor = brush.Color;
                UpdateForegroundButtonColor(_ctx.PenColor);
                // HighlightSelectedButton(btn);
            }
        }

        // 更新前景色按钮颜色
        public void UpdateForegroundButtonColor(Color color)
        {
            ForegroundBrush = new SolidColorBrush(color);
            DataContext = null;             // 断开绑定
            DataContext = this;             // 重新绑定，强制刷新 UI
        }

        // 更新背景色按钮颜色（可选）
        public void UpdateBackgroundButtonColor(Color color)
        {
            BackgroundBrush = new SolidColorBrush(color);
            DataContext = null;
            DataContext = this;
        }

        public class ToolRegistry
        {
            public ITool Pen { get; } = new PenTool();
            public ITool Eraser { get; } = new EraserTool();
            public ITool Eyedropper { get; } = new EyedropperTool();
            public ITool Fill { get; } = new FillTool();
            public ITool Select { get; } = new SelectTool();
            public ITool Text { get; } = new TextTool();
        }

        // 当前画笔颜色属性，可供工具使用
        public SolidColorBrush SelectedBrush { get; set; } = new SolidColorBrush(Colors.Black);

        // 绑定到 ItemsControl 的预设颜色集合
        public ObservableCollection<SolidColorBrush> ColorItems { get; set; }
            = new ObservableCollection<SolidColorBrush>
            {
                new SolidColorBrush(Colors.Black),
                new SolidColorBrush(Colors.Gray),
                new SolidColorBrush(Colors.Red),
                new SolidColorBrush(Colors.Orange),
                new SolidColorBrush(Colors.Yellow),
                new SolidColorBrush(Colors.Green),
                new SolidColorBrush(Colors.Cyan),
                new SolidColorBrush(Colors.Blue),
                new SolidColorBrush(Colors.Purple),
                new SolidColorBrush(Colors.Brown),
                new SolidColorBrush(Colors.Pink),
                new SolidColorBrush(Colors.White)
            };



        // 点击彩虹按钮自定义颜色
        private void OnCustomColorClick(object sender, RoutedEventArgs e)
        {
            var dlg = new ColorDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var color = Color.FromArgb(255, dlg.Color.R, dlg.Color.G, dlg.Color.B);
                var brush = new SolidColorBrush(color);
                SelectedBrush = brush;
                //HighlightSelectedButton(null);

                // 同步到绘图上下文
                _ctx.PenColor = color;
                UpdateForegroundButtonColor(color);
            }
        }

        // 简易高亮 - 给选中的颜色加外框
        private void HighlightSelectedButton(System.Windows.Controls.Button? selected)
        {
            foreach (var item in FindVisualChildren<System.Windows.Controls.Button>(this))
            {
                if (item.ToolTip != null && item.ToolTip.ToString() == "自定义颜色")
                    continue; // 跳过彩虹按钮
                if (selected != null && item == selected)
                    item.BorderBrush = Brushes.DeepSkyBlue;
                else
                    item.BorderBrush = Brushes.Red;
            }
        }

        // 工具函数 - 查找所有子元素
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        public enum BrushStyle { Round, Square, Brush, Spray, Pencil }

        // private BrushStyle _currentBrushStyle = BrushStyle.Round;
        private void ShowRotateMenu(object sender, RoutedEventArgs e)
        {
            var menu = (System.Windows.Controls.ContextMenu)FindResource("RotateContextMenu");
            menu.PlacementTarget = RotateMenuButton;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true; // 弹出菜单
        }

        private void OnBrushStyleClick(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton btn && Enum.TryParse(btn.Tag.ToString(), out BrushStyle style))
            {
                //   _currentBrushStyle = style;
                _ctx.PenStyle = style;
                // 可选：取消其它按钮的选中状态
                var toolbar = (btn.Parent as System.Windows.Controls.ToolBar);
                foreach (var child in toolbar.Items.OfType<ToggleButton>())
                    if (!ReferenceEquals(child, btn)) child.IsChecked = false;
            }
        }

        private void ThicknessSlider_DragStarted(object sender, DragStartedEventArgs e)
        {
            ThicknessPreview.Visibility = Visibility.Visible;
            UpdateThicknessPreviewPosition(); // 初始定位

            ThicknessTip.Visibility = Visibility.Visible;
            SetThicknessSlider_Pos(0);
        }

        private void ThicknessSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            ThicknessPreview.Visibility = Visibility.Collapsed;

            ThicknessTip.Visibility = Visibility.Collapsed;
        }

        private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            PenThickness = e.NewValue;
            UpdateThicknessPreviewPosition();

            if (ThicknessTip == null || ThicknessTipText == null || ThicknessSlider == null)
                return;

            PenThickness = e.NewValue;
            ThicknessTipText.Text = $"{(int)PenThickness} 像素";

            // 让提示显示出来
            ThicknessTip.Visibility = Visibility.Visible;
            SetThicknessSlider_Pos(e.NewValue);

        }

        private void SetThicknessSlider_Pos(double newValue)
        {
            // 根据 Slider 高度和当前值，计算提示位置
            Rect rect = new Rect(
     ThicknessSlider.TransformToAncestor(this).Transform(new Point(0, 0)),
     new Size(ThicknessSlider.ActualWidth, ThicknessSlider.ActualHeight));
            double trackHeight = ThicknessSlider.ActualHeight;
            double relativeValue = (ThicknessSlider.Maximum - newValue) / (ThicknessSlider.Maximum - ThicknessSlider.Minimum);
            double offsetY = relativeValue * trackHeight;

            ThicknessTip.Margin = new Thickness(80, offsetY + rect.Top - 10, 0, 0);
        }

        private void UpdateThicknessPreviewPosition()
        {
            if (ThicknessPreview == null)
                return;

            // 图像缩放比例
            double zoom = ZoomTransform.ScaleX;   // or ScaleY，通常两者相等
            double size = PenThickness * 2 * zoom; // 半径→界面直径 * 缩放

            ThicknessPreview.Width = size;
            ThicknessPreview.Height = size;

            ThicknessPreview.Fill = Brushes.Transparent;
            ThicknessPreview.StrokeThickness = 2;

            // 确保居中
            //ThicknessPreview.HorizontalAlignment = HorizontalAlignment.Center;
            //ThicknessPreview.VerticalAlignment = VerticalAlignment.Center;
        }
        private void OnRotateLeftClick(object sender, RoutedEventArgs e)
        {
            RotateBitmap(-90);
        }

        private void OnRotateRightClick(object sender, RoutedEventArgs e)
        {
            RotateBitmap(90);
        }

        private void OnRotate180Click(object sender, RoutedEventArgs e)
        {
            RotateBitmap(180);
        }


        private void CenterImage()
        {
            if (_bitmap == null || BackgroundImage == null)
                return;

            BackgroundImage.Width = _bitmap.PixelWidth;
            BackgroundImage.Height = _bitmap.PixelHeight;

            // 如果在 ScrollViewer 中，自动滚到中心
            if (ScrollContainer != null)
            {
                ScrollContainer.ScrollToHorizontalOffset(
                    (BackgroundImage.Width - ScrollContainer.ViewportWidth) / 2);
                ScrollContainer.ScrollToVerticalOffset(
                    (BackgroundImage.Height - ScrollContainer.ViewportHeight) / 2);
            }

            // 如果是 Grid 居中，直接用布局居中属性
            //BackgroundImage.HorizontalAlignment = HorizontalAlignment.Center;
            BackgroundImage.VerticalAlignment = VerticalAlignment.Center;
        }
        private void OnFlipVerticalClick(object sender, RoutedEventArgs e)
        {
            FlipBitmap(flipVertical: true);
        }

        private void OnFlipHorizontalClick(object sender, RoutedEventArgs e)
        {
            FlipBitmap(flipVertical: false);
        }


        private void ApplyTransform(System.Windows.Media.Transform transform)
        {
            if (BackgroundImage.Source is not BitmapSource src || _surface?.Bitmap == null)
                return;

            // 🟢 Step 1：在替换前保存旧图到撤销栈
            var rect = new Int32Rect(0, 0,
                                     _surface.Bitmap.PixelWidth,
                                     _surface.Bitmap.PixelHeight);

            // 提取旧的像素数据
            var oldPixels = _surface.ExtractRegion(rect);

            // 压入撤销栈（整图作为一次步骤）
            _undo?.PushUndoRegionTransform(rect, oldPixels);

            // 新操作截断重做链
            _undo?.ClearRedo();

            // 🟢 Step 2：生成旋转/翻转后的新图
            var tb = new TransformedBitmap(src, transform);
            var wb = new WriteableBitmap(tb);

            // 替换主位图
            _bitmap = wb;
            BackgroundImage.Source = _bitmap;

            // 让 CanvasSurface 附加新位图
            _surface.Attach(_bitmap);

            // 🟢 Step 3：居中显示
            BackgroundImage.Dispatcher.BeginInvoke(
                new Action(() => CenterImage()),
                System.Windows.Threading.DispatcherPriority.Loaded
            );
        }



        private void RotateBitmap(int angle)
        {
            ApplyTransform(new RotateTransform(angle));
        }

        private void FlipBitmap(bool flipVertical)
        {
            double cx = _bitmap.PixelWidth / 2.0;
            double cy = _bitmap.PixelHeight / 2.0;
            ApplyTransform(flipVertical ? new ScaleTransform(1, -1, cx, cy) : new ScaleTransform(-1, 1, cx, cy));
        }

        private void UpdateWindowTitle()
        {
            // 如果有未保存的修改，加上 '*'
            string dirtyMark = _isFileSaved ? "" : "*";
            this.Title = $"{dirtyMark}{_currentFileName} - SodiumPaint {_programVersion}";
        }

        private void OnTextClick(object sender, RoutedEventArgs e)
        {
            _router.SetTool(_tools.Text);
        }

        private double _zoomScale = 1.0;
        public double ZoomScale
        {
            get => _zoomScale;
            set
            {
                if (_zoomScale != value)
                {
                    _zoomScale = value;
                    ZoomLevel = $"{Math.Round(_zoomScale * 100)}%";
                    ZoomTransform.ScaleX = ZoomTransform.ScaleY = _zoomScale;
                    OnPropertyChanged();
                }
            }
        }

        private string _zoomLevel = "100%";
        public string ZoomLevel
        {
            get => _zoomLevel;
            set { _zoomLevel = value; OnPropertyChanged(); }
        }



        static void s<T>(T a)
        {
            System.Windows.MessageBox.Show(a.ToString(), "标题", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        static void msgbox<T>(T a)
        {
            System.Windows.MessageBox.Show(a.ToString(), "标题", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        static void s2<T>(T a)
        {
            Debug.Print(a.ToString());
        }

        public static class a
        {
            public static void s(params object[] args)
            {
                // 可以根据需要拼接输出格式
                string message = string.Join(" ", args);
                Debug.WriteLine(message);
            }
        }
















        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// /////////////////////
        /// 
        /// </summary>



















        private System.Windows.Controls.TextBox? _activeTextBox;
        public void ShowTextToolbarFor(System.Windows.Controls.TextBox tb)
        {
            _activeTextBox = tb;
            TextEditBar.Visibility = Visibility.Visible;

            FontFamilyBox.SelectedItem = tb.FontFamily;
            FontSizeBox.Text = tb.FontSize.ToString(CultureInfo.InvariantCulture);
            BoldBtn.IsChecked = tb.FontWeight == FontWeights.Bold;
            ItalicBtn.IsChecked = tb.FontStyle == FontStyles.Italic;
            UnderlineBtn.IsChecked = tb.TextDecorations == TextDecorations.Underline;
        }

        public void HideTextToolbar()
        {
            TextEditBar.Visibility = Visibility.Collapsed;
            _activeTextBox = null;
        }

        private void FontSettingChanged(object? sender, RoutedEventArgs e)
        {
            if (_activeTextBox == null) return;

            if (FontFamilyBox.SelectedItem is FontFamily family)
                _activeTextBox.FontFamily = family;
            if (double.TryParse((FontSizeBox.SelectedItem as ComboBoxItem)?.Content?.ToString(), out double size))
                _activeTextBox.FontSize = size;

            _activeTextBox.FontWeight = BoldBtn.IsChecked == true ? FontWeights.Bold : FontWeights.Normal;
            _activeTextBox.FontStyle = ItalicBtn.IsChecked == true ? FontStyles.Italic : FontStyles.Normal;
            _activeTextBox.TextDecorations = UnderlineBtn.IsChecked == true ? TextDecorations.Underline : null;



            if (_tools.Text is TextTool st) // 强转成 SelectTool
            {
                _activeTextBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    st.DrawTextboxOverlay(_ctx);
                }), DispatcherPriority.Background);
            }
        }



        public MainWindow(string startFilePath)
        {
            //if (startFilePath == null) return;
            _currentFilePath = startFilePath;
            InitializeComponent();
            // DataContext = new ViewModels.MainWindowViewModel();
            DataContext = this;
            LoadImage(_currentFilePath);
            Select = new SelectTool();

            // 初始化字体大小事件
            FontFamilyBox.SelectionChanged += FontSettingChanged;
            FontSizeBox.SelectionChanged += FontSettingChanged;
            BoldBtn.Checked += FontSettingChanged;
            BoldBtn.Unchecked += FontSettingChanged;
            ItalicBtn.Checked += FontSettingChanged;
            ItalicBtn.Unchecked += FontSettingChanged;
            UnderlineBtn.Checked += FontSettingChanged;
            UnderlineBtn.Unchecked += FontSettingChanged;



            ZoomSlider.ValueChanged += (s, e) =>
            {
                ZoomScale = ZoomSlider.Value; // 更新属性而不是直接访问 zoomscale
            };

            _surface = new CanvasSurface(_bitmap);
            _undo = new UndoRedoManager(_surface);
            _ctx = new ToolContext(_surface, _undo, BackgroundImage, SelectionPreview, SelectionOverlayCanvas, EditorOverlayCanvas);
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

        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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
                    case Key.A:
                        if (Keyboard.Modifiers == ModifierKeys.Control)
                        {
                            _router.SetTool(_tools.Select); // 切换到选择工具

                            if (_tools.Select is SelectTool st) // 强转成 SelectTool
                            {
                                st.SelectAll(_ctx); // 调用选择工具的特有方法
                            }
                            e.Handled = true;
                        }
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

            _undoStack.Push(new UndoAction(combined, regionData, null, UndoActionType.Draw));

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

            _undoStack.Push(new UndoAction(new Int32Rect(x, y, width, height), data, null, UndoActionType.Draw));

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
            //DrawOverlay()
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



        private async Task LoadImage(string filePath)
        {
            if (!File.Exists(filePath))
            {
                s($"找不到图片文件: {filePath}");
                return;
            }

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

                    // 扫描同目录图片文件
                    string folder = System.IO.Path.GetDirectoryName(filePath)!;
                    _imageFiles = Directory.GetFiles(folder, "*.*")
                        .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    _currentImageIndex = _imageFiles.IndexOf(filePath);

                    // 窗口调整逻辑
                    double imgWidth = _bitmap.Width;
                    double imgHeight = _bitmap.Height;

                    BackgroundImage.Width = imgWidth;
                    BackgroundImage.Height = imgHeight;

                    double maxWidth = SystemParameters.WorkArea.Width;
                    double maxHeight = SystemParameters.WorkArea.Height;

                    _imageSize = $"{_surface.Width}×{_surface.Height}";
                    OnPropertyChanged(nameof(ImageSize));
                    UpdateWindowTitle();

                    SetZoomAndOffset(
                        Math.Min(maxWidth / imgWidth, maxHeight / imgHeight) * 0.65,
                        10, 10);
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                s($"加载图片失败: {ex.Message}");
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

            if (_undo != null)
            {
                _undo.ClearUndo();
                _undo.ClearRedo();
            }

            if (_surface == null)
                _surface = new CanvasSurface(_bitmap);
            else
                _surface.Attach(_bitmap);
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

            // 调整窗口和画布大小
            double imgWidth = _bitmap.Width;
            double imgHeight = _bitmap.Height;



            BackgroundImage.Width = imgWidth;
            BackgroundImage.Height = imgHeight;

            // 根据屏幕情况（最多占 90%）
            double maxWidth = SystemParameters.WorkArea.Width;
            double maxHeight = SystemParameters.WorkArea.Height;

            _imageSize = $"{_surface.Width}×{_surface.Height}";
            OnPropertyChanged(nameof(ImageSize));
            UpdateWindowTitle();

            SetZoomAndOffset(Math.Min(SystemParameters.WorkArea.Width / imgWidth, SystemParameters.WorkArea.Height / imgHeight) * 0.7, 10, 10);

        }

        private void OnNewClick(object sender, RoutedEventArgs e)
        {
            // 可以弹出对话框让用户输入宽高，也可以用默认尺寸
            _bmpWidth = 1200;
            _bmpHeight = 900;
            _currentFilePath = string.Empty; // 新建后没有路径
            _currentFileName = "未命名";
            Clean_bitmap(_bmpWidth, _bmpHeight);


        }

    }
}
