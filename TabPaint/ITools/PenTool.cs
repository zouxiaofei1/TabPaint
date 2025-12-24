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
        public class PenTool : ToolBase
        {
            public override string Name => "Pen";
            public override System.Windows.Input.Cursor Cursor => System.Windows.Input.Cursors.Pen;

            private bool _drawing = false;
            private Point _lastPixel;
            private bool[] _currentStrokeMask;
            private int _maskWidth;
            private int _maskHeight;
            private static Random _rnd = new Random();
            public override void Cleanup(ToolContext ctx)
            {
                base.Cleanup(ctx);
                _drawing = false;
                StopDrawing(ctx);

            }
            public override void OnPointerDown(ToolContext ctx, Point viewPos)
            {
                if (((MainWindow)System.Windows.Application.Current.MainWindow)._router.CurrentTool != ((MainWindow)System.Windows.Application.Current.MainWindow)._tools.Pen) return;

                int totalPixels = ctx.Surface.Width * ctx.Surface.Height;

                // 只有当尺寸变了或者为空时才重新分配内存，否则直接清除，减少GC压力
                if (_currentStrokeMask == null || _currentStrokeMask.Length != totalPixels || _maskWidth != ctx.Surface.Width)
                {
                    _currentStrokeMask = new bool[totalPixels];
                    _maskWidth = ctx.Surface.Width;
                    _maskHeight = ctx.Surface.Height;
                }
                else
                {
                    // 快速清空数组 (全部设为 false)
                    Array.Clear(_currentStrokeMask, 0, _currentStrokeMask.Length);
                }

                // CAPTURE THE POINTER!
                ctx.CapturePointer();

                var px = ctx.ToPixel(viewPos);
                ctx.Undo.BeginStroke();
                _drawing = true;
                _lastPixel = px;
                if (ctx.PenStyle != BrushStyle.Eraser)

                    if(ctx.PenStyle != BrushStyle.Highlighter)
                    ctx.Surface.SetPixel((int)px.X, (int)px.Y, ctx.PenColor);
                else
                    ctx.Surface.SetPixel((int)px.X, (int)px.Y, ctx.EraserColor);

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
                    case BrushStyle.Round://圆形
                        steps = (int)(length / (ctx.PenThickness)); // 毛刷和喷枪更密集一些
                        if (length < ctx.PenThickness) return;
                        break;
                    case BrushStyle.Square://方形
                        steps = (int)(length / (ctx.PenThickness / 2));
                        break;
                    case BrushStyle.Eraser://方形
                        steps = (int)(length / (ctx.PenThickness / 2));
                        break;
                    case BrushStyle.Pencil://铅笔
                        steps = (int)(length);
                        break;
                    case BrushStyle.Brush://毛刷
                        steps = (int)(length / (5 / 2));
                        if (length < (5 / 2)) return;
                        break;
                    case BrushStyle.Spray://喷枪
                        steps = (int)(length / (ctx.PenThickness));
                        if (length < ctx.PenThickness) return;
                        break;
                    case BrushStyle.Watercolor: // 水彩笔需要非常密集的插值
                        steps = (int)(length / (ctx.PenThickness / 4));
                        if (length < (ctx.PenThickness / 4)) return;
                        break;
                    case BrushStyle.Crayon: // 油画笔可以稍微稀疏一些
                        steps = (int)(length / (ctx.PenThickness / 2));
                        break;
                    case BrushStyle.Highlighter:
                        DrawHighlighterLine(ctx, from, to);
                        ctx.Undo.AddDirtyRect(LineBounds(from, to, (int)ctx.PenThickness));
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

                        DrawRoundStroke(ctx, _lastPixel, px); ctx.Undo.AddDirtyRect(LineBounds(_lastPixel, px, (int)ctx.PenThickness));
                        break;
                    case BrushStyle.Square:
                        DrawSquareStroke(ctx, _lastPixel, px); ctx.Undo.AddDirtyRect(LineBounds(_lastPixel, px, (int)ctx.PenThickness));
                        break;
                    case BrushStyle.Eraser:
                        DrawSquareStroke(ctx, _lastPixel, px, true); ctx.Undo.AddDirtyRect(LineBounds(_lastPixel, px, (int)ctx.PenThickness));
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
                    case BrushStyle.Watercolor:
                        DrawWatercolorStroke(ctx, px);
                        // 水彩笔的扩散效果可能更大
                        ctx.Undo.AddDirtyRect(LineBounds(_lastPixel, px, (int)ctx.PenThickness + 5));
                        break;
                    case BrushStyle.Crayon:
                        DrawOilPaintStroke(ctx, px);
                        ctx.Undo.AddDirtyRect(LineBounds(_lastPixel, px, (int)ctx.PenThickness + 5));
                        break;
                    //case BrushStyle.Highlighter:
                    //    DrawHighlighterStroke(ctx, px);
                    //    // 水彩笔的扩散效果可能更大
                    // ctx.Undo.AddDirtyRect(LineBounds(_lastPixel, px, (int)ctx.PenThickness + 5));
                    //  break;
                    case BrushStyle.Mosaic:
                        DrawMosaicStroke(ctx, px);
                        ctx.Undo.AddDirtyRect(LineBounds(_lastPixel, px, (int)ctx.PenThickness + 5));
                        break;


                }
                _lastPixel = px;

            }
            private void DrawWatercolorStroke(ToolContext ctx, Point p)
            {
                int radius = (int)ctx.PenThickness;
                Color baseColor = ctx.PenColor;
                byte alpha = 15; // 水彩笔的核心是低 Alpha 值，这里我们用 15/255
                double irregularRadius = radius * (0.9 + _rnd.NextDouble() * 0.2);  // 为每次下笔创建一个略微不规则的形状

                int x_start = (int)Math.Max(0, p.X - radius);
                int x_end = (int)Math.Min(ctx.Surface.Width, p.X + radius);
                int y_start = (int)Math.Max(0, p.Y - radius);
                int y_end = (int)Math.Min(ctx.Surface.Height, p.Y + radius);
                ctx.Surface.Bitmap.Lock();
                unsafe
                {
                    byte* basePtr = (byte*)ctx.Surface.Bitmap.BackBuffer;
                    int stride = ctx.Surface.Bitmap.BackBufferStride;

                    for (int y = y_start; y < y_end; y++)
                    {
                        for (int x = x_start; x < x_end; x++)
                        {
                            double dist = Math.Sqrt((x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y));

                            if (dist < irregularRadius)
                            {
                                double falloff = 1.0 - (dist / irregularRadius); // 边缘羽化：离中心越远，Alpha 值越低
                                byte finalAlpha = (byte)(alpha * falloff * falloff); // falloff^2 使边缘更柔和

                                if (finalAlpha > 0) // Alpha 混合
                                {

                                    byte* pixelPtr = basePtr + y * stride + x * 4;

                                    // 读取背景色
                                    byte oldB = pixelPtr[0];
                                    byte oldG = pixelPtr[1];
                                    byte oldR = pixelPtr[2];
                                    pixelPtr[0] = (byte)((baseColor.B * finalAlpha + oldB * (255 - finalAlpha)) / 255); // Blue
                                    pixelPtr[1] = (byte)((baseColor.G * finalAlpha + oldG * (255 - finalAlpha)) / 255); // Green
                                    pixelPtr[2] = (byte)((baseColor.R * finalAlpha + oldR * (255 - finalAlpha)) / 255); // Red
                                                                                                                        // 图像本身不处理透明度，所以 Alpha 通道设为255
                                    pixelPtr[3] = 255;
                                }
                            }
                        }
                    }
                }
                ctx.Surface.Bitmap.AddDirtyRect(new Int32Rect(x_start, y_start, x_end - x_start, y_end - y_start));
                ctx.Surface.Bitmap.Unlock();
            }

            private void DrawOilPaintStroke(ToolContext ctx, Point p)
            {
                byte alpha = (byte)(0.2 * 255 / Math.Pow(ctx.PenThickness, 0.5));
                if (alpha == 0) return;

                int radius = (int)ctx.PenThickness;
                if (radius < 1) radius = 1;
                Color baseColor = ctx.PenColor;

                int x_center = (int)p.X;
                int y_center = (int)p.Y;

                int numClumps = radius / 2 + 5;
                int brightnessVariation = 40;

                ctx.Surface.Bitmap.Lock();
                unsafe
                {
                    byte* basePtr = (byte*)ctx.Surface.Bitmap.BackBuffer;
                    int stride = ctx.Surface.Bitmap.BackBufferStride;
                    int width = ctx.Surface.Width;
                    int height = ctx.Surface.Height;

                    for (int i = 0; i < numClumps; i++)
                    {
                        int brightnessOffset = _rnd.Next(-brightnessVariation, brightnessVariation + 1);
                        byte clumpR = ClampColor(baseColor.R + brightnessOffset);
                        byte clumpG = ClampColor(baseColor.G + brightnessOffset);
                        byte clumpB = ClampColor(baseColor.B + brightnessOffset);

                        double angle = _rnd.NextDouble() * 2 * Math.PI;
                        double distFromCenter = Math.Sqrt(_rnd.NextDouble()) * radius;
                        int clumpCenterX = x_center + (int)(distFromCenter * Math.Cos(angle));
                        int clumpCenterY = y_center + (int)(distFromCenter * Math.Sin(angle));

                        int clumpRadius = _rnd.Next(1, radius / 4 + 3);
                        int clumpRadiusSq = clumpRadius * clumpRadius;

                        int startX = Math.Max(0, clumpCenterX - clumpRadius);
                        int endX = Math.Min(width, clumpCenterX + clumpRadius);
                        int startY = Math.Max(0, clumpCenterY - clumpRadius);
                        int endY = Math.Min(height, clumpCenterY + clumpRadius);

                        for (int y = startY; y < endY; y++)
                        {
                            for (int x = startX; x < endX; x++)
                            {
                                int dx = x - clumpCenterX;
                                int dy = y - clumpCenterY;
                                if (dx * dx + dy * dy < clumpRadiusSq)
                                {
                                    byte* pixelPtr = basePtr + y * stride + x * 4;
                                    if (alpha == 255) // 如果完全不透明，使用旧的快速方法
                                    {
                                        pixelPtr[0] = clumpB;
                                        pixelPtr[1] = clumpG;
                                        pixelPtr[2] = clumpR;
                                    }
                                    else // 否则，执行Alpha混合
                                    {
                                        byte oldB = pixelPtr[0];
                                        byte oldG = pixelPtr[1];
                                        byte oldR = pixelPtr[2];
                                        pixelPtr[0] = (byte)((clumpB * alpha + oldB * (255 - alpha)) / 255); // Blue
                                        pixelPtr[1] = (byte)((clumpG * alpha + oldG * (255 - alpha)) / 255); // Green
                                        pixelPtr[2] = (byte)((clumpR * alpha + oldR * (255 - alpha)) / 255); // Red
                                    }
                                }
                            }
                        }
                    }
                }
                int dirtyX = Math.Max(0, x_center - radius);
                int dirtyY = Math.Max(0, y_center - radius);
                int dirtyW = Math.Min(ctx.Surface.Width - dirtyX, radius * 2 + 1);
                int dirtyH = Math.Min(ctx.Surface.Height - dirtyY, radius * 2 + 1);

                if (dirtyW > 0 && dirtyH > 0)
                {
                    ctx.Surface.Bitmap.AddDirtyRect(new Int32Rect(dirtyX, dirtyY, dirtyW, dirtyH));
                }
                ctx.Surface.Bitmap.Unlock();
            }
            private byte ClampColor(int value)
            {
                if (value < 0) return 0;
                if (value > 255) return 255;
                return (byte)value;
            }
            public override void OnPointerMove(ToolContext ctx, Point viewPos)
            {
                if (!_drawing) return;
                var px = ctx.ToPixel(viewPos);

                DrawContinuousStroke(ctx, _lastPixel, px);


            }
            public override void OnPointerUp(ToolContext ctx, Point viewPos)
            {
                StopDrawing(ctx);
            }
            public override void StopAction(ToolContext ctx)
            {
                StopDrawing(ctx);
            }
            public void StopDrawing(ToolContext ctx)
            {
                if (!_drawing) return; // Only act if we are actually drawing

                _drawing = false;
                ctx.Undo.CommitStroke();
                ctx.IsDirty = true;
                ctx.ReleasePointerCapture();
            }
            //private static Int32Rect ClampRect(Int32Rect rect, int maxWidth, int maxHeight)
            //{
            //    int newX = (rect.X > maxWidth) ? maxWidth : rect.X;
            //    int newY = (rect.Y > maxHeight) ? maxHeight : rect.Y;
            //    int x = Math.Max(0, newX);
            //    int y = Math.Max(0, newY);
            //    int w = Math.Min(rect.Width, maxWidth - newX);
            //    int h = Math.Min(rect.Height, maxHeight - newY);
            //    return new Int32Rect(x, y, w, h);
            //}

            private static Int32Rect ClampRect(Int32Rect rect, int maxWidth, int maxHeight)
            {
                // 1. 计算左上角和右下角的边界坐标
                int left = Math.Max(0, rect.X);
                int top = Math.Max(0, rect.Y);

                // 2. 计算右边界和下边界（不能超过最大宽高）
                int right = Math.Min(maxWidth, rect.X + rect.Width);
                int bottom = Math.Min(maxHeight, rect.Y + rect.Height);

                // 3. 计算新的宽高。确保结果不为负数
                int width = Math.Max(0, right - left);
                int height = Math.Max(0, bottom - top);

                return new Int32Rect(left, top, width, height);
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
            private void DrawSquareStroke(ToolContext ctx, Point p1, Point p2, bool Eraser = false)
            {
                // 方形笔就是画粗一点的正方形块
                int size = (int)ctx.PenThickness;
                ctx.Surface.FillRectangle(new Int32Rect((int)p2.X, (int)p2.Y, size, size), Eraser ? ctx.EraserColor : ctx.PenColor);
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

            private static List<Point[]> _sprayPatterns;  // 喷枪 pattern 集合
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

            private void DrawMosaicStroke(ToolContext ctx, Point p)
            {
                int radius = (int)ctx.PenThickness;
                int blockSize = 12; // 马赛克方块的大小，可以根据 PenThickness 动态调整

                int x_start = (int)Math.Max(0, p.X - radius);
                int x_end = (int)Math.Min(ctx.Surface.Width, p.X + radius);
                int y_start = (int)Math.Max(0, p.Y - radius);
                int y_end = (int)Math.Min(ctx.Surface.Height, p.Y + radius);

                ctx.Surface.Bitmap.Lock();
                unsafe
                {
                    byte* basePtr = (byte*)ctx.Surface.Bitmap.BackBuffer;
                    int stride = ctx.Surface.Bitmap.BackBufferStride;

                    for (int y = y_start; y < y_end; y++)
                    {
                        for (int x = x_start; x < x_end; x++)
                        {
                            // 圆形笔触判定
                            double dist = Math.Sqrt((x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y));
                            if (dist < radius)
                            {
                                // 计算当前点所属的马赛克块左上角坐标
                                int blockX = (x / blockSize) * blockSize;
                                int blockY = (y / blockSize) * blockSize;

                                // 确保块基准点不越界
                                blockX = Math.Clamp(blockX, 0, ctx.Surface.Width - 1);
                                blockY = Math.Clamp(blockY, 0, ctx.Surface.Height - 1);

                                byte* sourcePixel = basePtr + blockY * stride + blockX * 4;
                                byte* targetPixel = basePtr + y * stride + x * 4;

                                // 将块基准点的颜色赋给当前像素
                                targetPixel[0] = sourcePixel[0]; // B
                                targetPixel[1] = sourcePixel[1]; // G
                                targetPixel[2] = sourcePixel[2]; // R
                                targetPixel[3] = 255;
                            }
                        }
                    }
                }
                ctx.Surface.Bitmap.AddDirtyRect(
                    ClampRect(new Int32Rect(x_start, y_start, x_end - x_start, y_end - y_start),
                    ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Bitmap.PixelWidth, ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Bitmap.PixelHeight)
                    );
                ctx.Surface.Bitmap.Unlock();
            }
            private void DrawHighlighterLine(ToolContext ctx, Point p1, Point p2)
            {
                int r = (int)ctx.PenThickness;
                // 荧光黄，Alpha设为 30-50 比较合适，因为现在是单层覆盖，不会瞬间变黑
                // 如果觉得太浅，可以调高 Alpha；如果觉得叠加太黑，调低 Alpha
                Color c = Color.FromArgb(30, 255, 255, 0);

                // 计算包围盒，减少循环次数
                int xmin = (int)Math.Min(p1.X, p2.X) - r;
                int ymin = (int)Math.Min(p1.Y, p2.Y) - r;
                int xmax = (int)Math.Max(p1.X, p2.X) + r;
                int ymax = (int)Math.Max(p1.Y, p2.Y) + r;

                // 边界安全检查
                xmin = Math.Max(0, xmin);
                ymin = Math.Max(0, ymin);
                xmax = Math.Min(ctx.Surface.Width - 1, xmax);
                ymax = Math.Min(ctx.Surface.Height - 1, ymax);
                int width = ctx.Surface.Width;
                ctx.Surface.Bitmap.Lock();
                unsafe
                {
                    byte* basePtr = (byte*)ctx.Surface.Bitmap.BackBuffer;
                    int stride = ctx.Surface.Bitmap.BackBufferStride;

                    // 预计算向量 P1 -> P2
                    double dx = p2.X - p1.X;
                    double dy = p2.Y - p1.Y;
                    double lenSq = dx * dx + dy * dy; // 长度平方

                    for (int y = ymin; y <= ymax; y++)
                    {
                        int rowStartIndex = y * width;
                        for (int x = xmin; x <= xmax; x++)
                        {
                            int pixelIndex = rowStartIndex + x;
                            if (_currentStrokeMask[pixelIndex])
                            {
                                // 如果这个像素在这一笔中已经画过了，跳过！
                                // 这完美解决了连接处重叠和反复涂抹变黑的问题
                                continue;
                            }
                            // --- 距离计算核心逻辑 (SDF) ---
                            double t = 0;
                            if (lenSq > 0)
                            {
                                double dot = (x - p1.X) * dx + (y - p1.Y) * dy;
                                t = Math.Max(0, Math.Min(1, dot / lenSq));
                            }
                            double closeX = p1.X + t * dx;
                            double closeY = p1.Y + t * dy;
                            double distSq = (x - closeX) * (x - closeX) + (y - closeY) * (y - closeY);

                            // ... 省略外层循环代码 ...

                            if (distSq <= r * r)
                            {
                                // 标记该像素已处理
                                _currentStrokeMask[pixelIndex] = true;

                                byte* p = basePtr + y * stride + x * 4;

                                // 读取旧的背景数据
                                byte oldB = p[0];
                                byte oldG = p[1];
                                byte oldR = p[2];
                                byte oldA = p[3]; // 获取背景透明度

                                // 预计算反向 Alpha (0..255)
                                int invSA = 255 - c.A;

                                // --- 颜色混合公式 (标准 Premultiplied Alpha 混合) ---
                                // 即使背景是透明的 (old=0)，这里也会算出预乘后的颜色值 (如 50, 50, 0)
                                // 这本身是对的，只要配套正确的 Alpha 就行。
                                p[0] = (byte)((c.B * c.A + oldB * invSA) / 255); // Blue
                                p[1] = (byte)((c.G * c.A + oldG * invSA) / 255); // Green
                                p[2] = (byte)((c.R * c.A + oldR * invSA) / 255); // Red

                                // --- 【核心修复】 Alpha 通道计算 ---
                                // 以前是 p[3] = 255; 导致透明区域变黑
                                // 现在使用公式：ResultAlpha = SrcAlpha + DstAlpha * (1 - SrcAlpha)
                                p[3] = (byte)(c.A + (oldA * invSA) / 255);
                            }

                        }
                    }
                }
                int dirtyW = xmax - xmin + 1;
                int dirtyH = ymax - ymin + 1;

                // 确保矩形合法（在图片范围内）
                if (dirtyW > 0 && dirtyH > 0)
                {
                    ctx.Surface.Bitmap.AddDirtyRect(new Int32Rect(xmin, ymin, dirtyW, dirtyH));
                }

                ctx.Surface.Bitmap.Unlock();
            }




        }

    }
}