using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
//
//画笔工具
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

            // 荧光笔专用遮罩
            private bool[] _currentStrokeMask;
            private int _maskWidth;
            private int _maskHeight;

            // 喷枪缓存
            private static List<Point[]> _sprayPatterns;
            private static int _patternIndex = 0;
            private static Random _rnd = new Random();

            public override void Cleanup(ToolContext ctx)
            {
                base.Cleanup(ctx);
                _drawing = false;
                StopDrawing(ctx);
            }

            // 判断是否是“连续线段”类型的笔刷（不需要插值打点，而是直接画线）
            private bool IsLineBasedBrush(BrushStyle style)
            {
                return style == BrushStyle.Round ||
                       style == BrushStyle.Pencil ||
                       style == BrushStyle.Highlighter ||
                       style == BrushStyle.Watercolor ||
                       style == BrushStyle.Crayon;
            }

            public override void OnPointerDown(ToolContext ctx, Point viewPos)
            {
            //  s(TabPaint.SettingsManager.Instance.Current.PenOpacity);
                if (((MainWindow)System.Windows.Application.Current.MainWindow)._router.CurrentTool != ((MainWindow)System.Windows.Application.Current.MainWindow)._tools.Pen) return;

                // --- 荧光笔遮罩初始化 ---
                int totalPixels = ctx.Surface.Width * ctx.Surface.Height;
                if (_currentStrokeMask == null || _currentStrokeMask.Length != totalPixels || _maskWidth != ctx.Surface.Width)
                {
                    _currentStrokeMask = new bool[totalPixels];
                    _maskWidth = ctx.Surface.Width;
                    _maskHeight = ctx.Surface.Height;
                }
                else
                {
                    Array.Clear(_currentStrokeMask, 0, _currentStrokeMask.Length);
                }

                ctx.CapturePointer();
                var px = ctx.ToPixel(viewPos);
                ctx.Undo.BeginStroke();
                _drawing = true;
                _lastPixel = px;

                Int32Rect? dirty = null;

                // --- 修复：PointerDown 时根据笔刷类型分流 ---
                ctx.Surface.Bitmap.Lock();
                unsafe
                {
                    byte* backBuffer = (byte*)ctx.Surface.Bitmap.BackBuffer;
                    int stride = ctx.Surface.Bitmap.BackBufferStride;
                    int width = ctx.Surface.Bitmap.PixelWidth;
                    int height = ctx.Surface.Bitmap.PixelHeight;

                    if (IsLineBasedBrush(ctx.PenStyle))
                    {
                        // 线段型笔刷：原地画一条长度为0的线（即一个点）
                        dirty = DrawBrushLineUnsafe(ctx, px, px, backBuffer, stride, width, height);
                    }
                    else
                    {
                        // 印章型笔刷（方块、喷枪等）：直接盖一个章
                        dirty = DrawBrushAtUnsafe(ctx, px, backBuffer, stride, width, height);
                    }
                }

                if (dirty.HasValue)
                {
                    var finalRect = ClampRect(dirty.Value, ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);
                    if (finalRect.Width > 0 && finalRect.Height > 0)
                    {
                        ctx.Surface.Bitmap.AddDirtyRect(finalRect); // 更新屏幕
                        ctx.Undo.AddDirtyRect(finalRect);           // 修复：通知 Undo 系统
                    }
                }
                ctx.Surface.Bitmap.Unlock();
            }

            public override void OnPointerMove(ToolContext ctx, Point viewPos)
            {
                if (!_drawing) return;
                var px = ctx.ToPixel(viewPos);

                ctx.Surface.Bitmap.Lock();

                // 计算本次绘制的脏矩形范围
                int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                bool hasUpdate = false;

                unsafe
                {
                    byte* backBuffer = (byte*)ctx.Surface.Bitmap.BackBuffer;
                    int stride = ctx.Surface.Bitmap.BackBufferStride;
                    int width = ctx.Surface.Bitmap.PixelWidth;
                    int height = ctx.Surface.Bitmap.PixelHeight;

                    // 传入指针进行极速绘制
                    var dirty = DrawContinuousStrokeUnsafe(ctx, _lastPixel, px, backBuffer, stride, width, height);

                    if (dirty.HasValue)
                    {
                        hasUpdate = true;
                        minX = dirty.Value.X;
                        minY = dirty.Value.Y;
                        maxX = dirty.Value.X + dirty.Value.Width;
                        maxY = dirty.Value.Y + dirty.Value.Height;
                    }
                }

                // 更新
                if (hasUpdate && maxX >= minX && maxY >= minY)
                {
                    var finalRect = ClampRect(new Int32Rect(minX, minY, maxX - minX, maxY - minY), ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);
                    if (finalRect.Width > 0 && finalRect.Height > 0)
                    {
                        ctx.Surface.Bitmap.AddDirtyRect(finalRect); // 1. 更新屏幕
                        ctx.Undo.AddDirtyRect(finalRect);           // 2. 修复：关键！必须通知 Undo 系统
                    }
                }

                ctx.Surface.Bitmap.Unlock();

                _lastPixel = px;
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
                if (!_drawing) return;
                _drawing = false;
                ctx.Undo.CommitStroke();
                ctx.IsDirty = true;
                ctx.ReleasePointerCapture();
            }

            // ---------------- 核心绘制逻辑 (Unsafe) ----------------

            private unsafe Int32Rect? DrawContinuousStrokeUnsafe(ToolContext ctx, Point from, Point to, byte* buffer, int stride, int w, int h)
            {
                // 1. 连续型笔刷：直接画线段，效率最高
                if (IsLineBasedBrush(ctx.PenStyle))
                {
                    return DrawBrushLineUnsafe(ctx, from, to, buffer, stride, w, h);
                }

                // 2. 间断型笔刷：需要插值“盖章”
                double dx = to.X - from.X;
                double dy = to.Y - from.Y;
                double length = Math.Sqrt(dx * dx + dy * dy);

                if (length < 0.5) return DrawBrushAtUnsafe(ctx, to, buffer, stride, w, h);

                int steps = 1;
                switch (ctx.PenStyle)
                {
                    case BrushStyle.Square:
                    case BrushStyle.Eraser:
                        steps = (int)(length / (Math.Max(1, ctx.PenThickness / 2.0)));
                        break;
                    case BrushStyle.Brush:
                        steps = (int)(length / 2.0);
                        break;
                    case BrushStyle.Spray:
                        steps = (int)(length / (Math.Max(1, ctx.PenThickness)));
                        break;
                    case BrushStyle.Mosaic:
                        steps = (int)(length / 5.0);
                        break;
                }

                if (steps < 1) steps = 1;

                double xStep = dx / steps;
                double yStep = dy / steps;
                double x = from.X;
                double y = from.Y;

                int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                bool hit = false;

                for (int i = 0; i <= steps; i++)
                {
                    var rect = DrawBrushAtUnsafe(ctx, new Point(x, y), buffer, stride, w, h);
                    if (rect.HasValue)
                    {
                        hit = true;
                        if (rect.Value.X < minX) minX = rect.Value.X;
                        if (rect.Value.Y < minY) minY = rect.Value.Y;
                        if (rect.Value.X + rect.Value.Width > maxX) maxX = rect.Value.X + rect.Value.Width;
                        if (rect.Value.Y + rect.Value.Height > maxY) maxY = rect.Value.Y + rect.Value.Height;
                    }
                    x += xStep;
                    y += yStep;
                }

                if (!hit) return null;
                return new Int32Rect(minX, minY, maxX - minX, maxY - minY);
            }

            private unsafe Int32Rect? DrawBrushLineUnsafe(ToolContext ctx, Point p1, Point p2, byte* buffer, int stride, int w, int h)
            {
                switch (ctx.PenStyle)
                {
                    case BrushStyle.Round:
                        DrawRoundStrokeUnsafe(ctx, p1, p2, buffer, stride, w, h);
                        return LineBounds(p1, p2, (int)ctx.PenThickness);
                    case BrushStyle.Pencil:
                        DrawPencilLineUnsafe(ctx, p1, p2, buffer, stride, w, h);
                        return LineBounds(p1, p2, 1);
                    case BrushStyle.Highlighter:
                        DrawHighlighterLineUnsafe(ctx, p1, p2, buffer, stride, w, h);
                        return LineBounds(p1, p2, (int)ctx.PenThickness);
                    case BrushStyle.Watercolor:
                        // 简单起见，水彩和蜡笔在内部做局部插值
                        double dist = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
                        int steps = (int)(dist / (ctx.PenThickness / 4));
                        if (steps == 0) steps = 1;
                        double sx = (p2.X - p1.X) / steps;
                        double sy = (p2.Y - p1.Y) / steps;
                        double cx = p1.X, cy = p1.Y;
                        for (int i = 0; i <= steps; i++)
                        {
                            DrawWatercolorStrokeUnsafe(ctx, new Point(cx, cy), buffer, stride, w, h);
                            cx += sx; cy += sy;
                        }
                        return LineBounds(p1, p2, (int)ctx.PenThickness + 5);
                    case BrushStyle.Crayon:
                        double dist2 = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
                        int steps2 = (int)(dist2 / (ctx.PenThickness / 2));
                        if (steps2 == 0) steps2 = 1;
                        double sx2 = (p2.X - p1.X) / steps2;
                        double sy2 = (p2.Y - p1.Y) / steps2;
                        double cx2 = p1.X, cy2 = p1.Y;
                        for (int i = 0; i <= steps2; i++)
                        {
                            DrawOilPaintStrokeUnsafe(ctx, new Point(cx2, cy2), buffer, stride, w, h);
                            cx2 += sx2; cy2 += sy2;
                        }
                        return LineBounds(p1, p2, (int)ctx.PenThickness + 5);
                }
                return null;
            }

            private unsafe Int32Rect? DrawBrushAtUnsafe(ToolContext ctx, Point px, byte* buffer, int stride, int w, int h)
            {
                // 用于那些不支持线段绘制，只能打点的笔刷
                switch (ctx.PenStyle)
                {
                    case BrushStyle.Square:
                        DrawSquareStrokeUnsafe(ctx, px, buffer, stride, w, h, false);
                        return LineBounds(px, px, (int)ctx.PenThickness);
                    case BrushStyle.Eraser:
                        DrawSquareStrokeUnsafe(ctx, px, buffer, stride, w, h, true);
                        return LineBounds(px, px, (int)ctx.PenThickness);
                    case BrushStyle.Brush:
                        DrawBrushStrokeUnsafe(ctx, px, buffer, stride, w, h);
                        return LineBounds(px, px, 5);
                    case BrushStyle.Spray:
                        DrawSprayStrokeUnsafe(ctx, px, buffer, stride, w, h);
                        return LineBounds(px, px, (int)ctx.PenThickness * 2);
                    case BrushStyle.Mosaic:
                        DrawMosaicStrokeUnsafe(ctx, px, buffer, stride, w, h);
                        return LineBounds(px, px, (int)ctx.PenThickness + 5);
                }
                return null;
            }

            // ---------------- 具体笔刷实现 (Unsafe) ----------------

            private unsafe void DrawRoundStrokeUnsafe(ToolContext ctx, Point p1, Point p2, byte* basePtr, int stride, int w, int h)
            {
                int r = (int)ctx.PenThickness;
                Color c = ctx.PenColor;
                byte finalAlpha = GetCurrentAlpha(c.A);
                // 如果完全透明，直接不画，节省性能
                if (finalAlpha == 0) return;
                byte cb = c.B, cg = c.G, cr = c.R, ca = finalAlpha;

                double dx = p2.X - p1.X;
                double dy = p2.Y - p1.Y;
                double lenSq = dx * dx + dy * dy;

                int xmin = (int)Math.Min(p1.X, p2.X) - r;
                int ymin = (int)Math.Min(p1.Y, p2.Y) - r;
                int xmax = (int)Math.Max(p1.X, p2.X) + r;
                int ymax = (int)Math.Max(p1.Y, p2.Y) + r;

                xmin = Math.Max(0, xmin); ymin = Math.Max(0, ymin);
                xmax = Math.Min(w - 1, xmax); ymax = Math.Min(h - 1, ymax);

                int rSq = r * r;

                for (int y = ymin; y <= ymax; y++)
                {
                    byte* rowPtr = basePtr + y * stride; int rowIdx = y * w;
                    for (int x = xmin; x <= xmax; x++)
                    {
                        int pixelIndex = rowIdx + x;
                        if (_currentStrokeMask[pixelIndex]) continue;
                        double t = 0;
                        if (lenSq > 0)
                        {
                            t = ((x - p1.X) * dx + (y - p1.Y) * dy) / lenSq;
                            t = Math.Max(0, Math.Min(1, t));
                        }
                        double projx = p1.X + t * dx;
                        double projy = p1.Y + t * dy;
                        double distSq = (x - projx) * (x - projx) + (y - projy) * (y - projy);

                        if (distSq <= rSq)
                        {
                            _currentStrokeMask[pixelIndex] = true;
                            byte* p = rowPtr + x * 4; 
                            if (ca == 255)
                            {
                                // 不透明优化：直接覆盖
                                p[0] = cb; p[1] = cg; p[2] = cr; p[3] = 255;
                            }
                            else
                            {
                                float alphaNorm = ca / 255.0f;
                                float invAlpha = 1.0f - alphaNorm;

                                p[0] = (byte)(cb * alphaNorm + p[0] * invAlpha);
                                p[1] = (byte)(cg * alphaNorm + p[1] * invAlpha);
                                p[2] = (byte)(cr * alphaNorm + p[2] * invAlpha);
                                p[3] = (byte)Math.Min(255, p[3] + ca);
                            }
                            
                        }
                    }
                }
            }

            private unsafe void DrawPencilLineUnsafe(ToolContext ctx, Point p1, Point p2, byte* basePtr, int stride, int w, int h)
            {
                int x0 = (int)p1.X; int y0 = (int)p1.Y;
                int x1 = (int)p2.X; int y1 = (int)p2.Y;
                Color c = ctx.PenColor;
                byte finalAlpha = GetCurrentAlpha(c.A);
                if (finalAlpha == 0) return;
                byte cb = c.B, cg = c.G, cr = c.R, ca = finalAlpha;

                int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
                int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
                int err = dx + dy, e2;

                while (true)
                {
                    if (x0 >= 0 && x0 < w && y0 >= 0 && y0 < h)
                    {
                        byte* p = basePtr + y0 * stride + x0 * 4;

                        // 同样加入混合逻辑
                        if (ca == 255)
                        {
                            p[0] = cb; p[1] = cg; p[2] = cr; p[3] = 255;
                        }
                        else
                        {
                            float alphaNorm = ca / 255.0f;
                            float invAlpha = 1.0f - alphaNorm;
                            p[0] = (byte)(cb * alphaNorm + p[0] * invAlpha);
                            p[1] = (byte)(cg * alphaNorm + p[1] * invAlpha);
                            p[2] = (byte)(cr * alphaNorm + p[2] * invAlpha);
                            p[3] = 255;
                        }
                    }
                    if (x0 == x1 && y0 == y1) break;
                    e2 = 2 * err;
                    if (e2 >= dy) { err += dy; x0 += sx; }
                    if (e2 <= dx) { err += dx; y0 += sy; }
                }
            }

            private unsafe void DrawSquareStrokeUnsafe(ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h, bool isEraser)
            {
                int size = (int)ctx.PenThickness;
                int half = size / 2;
                int x = (int)p.X - half;
                int y = (int)p.Y - half;

                Color c = isEraser ? ((MainWindow)System.Windows.Application.Current.MainWindow).BackgroundColor : ctx.PenColor;
                byte finalAlpha = GetCurrentAlpha(c.A); float alphaNorm = finalAlpha / 255.0f; float invAlpha = 1.0f - alphaNorm;
                // 如果完全透明，直接不画，节省性能
                if (finalAlpha == 0) return;
                byte cb = c.B, cg = c.G, cr = c.R, ca = isEraser ? c.A : finalAlpha;

                int xend = Math.Min(w, x + size);
                int yend = Math.Min(h, y + size);
                int xstart = Math.Max(0, x);
                int ystart = Math.Max(0, y);

                for (int yy = ystart; yy < yend; yy++)
                {
                    byte* row = basePtr + yy * stride; int rowIdx = yy * w;
                    for (int xx = xstart; xx < xend; xx++)
                    {
                        int pixelIndex = rowIdx + xx;
                        byte* ptr = row + xx * 4;
                        if (!isEraser && _currentStrokeMask[pixelIndex]) continue;

                        // 标记该像素已处理
                        if (!isEraser) _currentStrokeMask[pixelIndex] = true;
                        if (isEraser && finalAlpha == 255)
                        {
                            // 橡皮擦且不透明时，直接覆盖以提高性能
                            ptr[0] = cb; ptr[1] = cg; ptr[2] = cr; ptr[3] = 255;
                        }
                        else
                        {
                            // --- 核心修复：混合模式 ---
                            ptr[0] = (byte)(cb * alphaNorm + ptr[0] * invAlpha);
                            ptr[1] = (byte)(cg * alphaNorm + ptr[1] * invAlpha);
                            ptr[2] = (byte)(cr * alphaNorm + ptr[2] * invAlpha);
                            // 保持完全不透明，除非你想画出半透明图层
                            ptr[3] = 255;
                        }
                    }
                }
            }

            private unsafe void DrawBrushStrokeUnsafe(ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h)
            {
                Color c = ctx.PenColor;
                for (int i = 0; i < 20; i++)
                {
                    int dx = _rnd.Next(-2, 3);
                    int dy = _rnd.Next(-2, 3);
                    int xx = (int)p.X + dx;
                    int yy = (int)p.Y + dy;
                    if (xx >= 0 && xx < w && yy >= 0 && yy < h)
                    {
                        byte* ptr = basePtr + yy * stride + xx * 4;
                        ptr[0] = c.B; ptr[1] = c.G; ptr[2] = c.R; ptr[3] = c.A;
                    }
                }
            }

            private unsafe void DrawSprayStrokeUnsafe(ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h)
            {
                if (_sprayPatterns == null) InitializeSprayPatterns();
                int radius = (int)ctx.PenThickness * 2;
                int count = 80; // 粒子数量
                var pattern = _sprayPatterns[_patternIndex];
                _patternIndex = (_patternIndex + 1) % _sprayPatterns.Count;

                Color c = ctx.PenColor;
                // --- 核心修复：获取调节后的透明度 ---
                byte finalAlpha = GetCurrentAlpha(c.A);
                if (finalAlpha == 0) return;

                float alphaNorm = finalAlpha / 255.0f;
                float invAlpha = 1.0f - alphaNorm;

                for (int i = 0; i < count && i < pattern.Length; i++)
                {
                    int xx = (int)(p.X + pattern[i].X * radius);
                    int yy = (int)(p.Y + pattern[i].Y * radius);
                    if (xx >= 0 && xx < w && yy >= 0 && yy < h)
                    {
                        byte* ptr = basePtr + yy * stride + xx * 4;
                        // --- 核心修复：混合 ---
                        ptr[0] = (byte)(c.B * alphaNorm + ptr[0] * invAlpha);
                        ptr[1] = (byte)(c.G * alphaNorm + ptr[1] * invAlpha);
                        ptr[2] = (byte)(c.R * alphaNorm + ptr[2] * invAlpha);
                        ptr[3] = (byte)Math.Min(255, ptr[3] + finalAlpha);
                    }
                }
            }


            private unsafe void DrawMosaicStrokeUnsafe(ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h)
            {
                int radius = (int)ctx.PenThickness;
                int blockSize = 12;

                int x_start = (int)Math.Max(0, p.X - radius);
                int x_end = (int)Math.Min(w, p.X + radius);
                int y_start = (int)Math.Max(0, p.Y - radius);
                int y_end = (int)Math.Min(h, p.Y + radius);

                for (int y = y_start; y < y_end; y++)
                {
                    for (int x = x_start; x < x_end; x++)
                    {
                        double dist = Math.Sqrt((x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y));
                        if (dist < radius)
                        {
                            int blockX = (x / blockSize) * blockSize;
                            int blockY = (y / blockSize) * blockSize;
                            blockX = Math.Clamp(blockX, 0, w - 1);
                            blockY = Math.Clamp(blockY, 0, h - 1);

                            byte* sourcePixel = basePtr + blockY * stride + blockX * 4;
                            byte* targetPixel = basePtr + y * stride + x * 4;

                            targetPixel[0] = sourcePixel[0];
                            targetPixel[1] = sourcePixel[1];
                            targetPixel[2] = sourcePixel[2];
                            targetPixel[3] = 255;
                        }
                    }
                }
            }

            private unsafe void DrawWatercolorStrokeUnsafe(ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h)
            {
                int radius = (int)ctx.PenThickness;
                Color baseColor = ctx.PenColor;

                // --- 核心修复：获取全局透明度比例 (0.0 - 1.0) ---
                double globalOpacityFactor = TabPaint.SettingsManager.Instance.Current.PenOpacity;

                // 基础 Alpha 很低，为了模拟水彩层层叠加的效果
                byte baseAlpha = 15;

                double irregularRadius = radius * (0.9 + _rnd.NextDouble() * 0.2);
                int x_start = (int)Math.Max(0, p.X - radius);
                int x_end = (int)Math.Min(w, p.X + radius);
                int y_start = (int)Math.Max(0, p.Y - radius);
                int y_end = (int)Math.Min(h, p.Y + radius);

                for (int y = y_start; y < y_end; y++)
                {
                    byte* rowPtr = basePtr + y * stride;
                    for (int x = x_start; x < x_end; x++)
                    {
                        double dist = Math.Sqrt((x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y));
                        if (dist < irregularRadius)
                        {
                            double falloff = 1.0 - (dist / irregularRadius);

                            // --- 核心修复：将全局透明度因子乘入最终计算 ---
                            byte finalAlpha = (byte)(baseAlpha * falloff * falloff * globalOpacityFactor);

                            if (finalAlpha > 0)
                            {
                                float alphaNorm = finalAlpha / 255.0f;
                                float invAlpha = 1.0f - alphaNorm;

                                byte* pixelPtr = rowPtr + x * 4;

                                pixelPtr[0] = (byte)(baseColor.B * alphaNorm + pixelPtr[0] * invAlpha);
                                pixelPtr[1] = (byte)(baseColor.G * alphaNorm + pixelPtr[1] * invAlpha);
                                pixelPtr[2] = (byte)(baseColor.R * alphaNorm + pixelPtr[2] * invAlpha);
                                pixelPtr[3] = 255;
                            }
                        }
                    }
                }
            }


            private unsafe void DrawOilPaintStrokeUnsafe(ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h)
            {
                double globalOpacity = TabPaint.SettingsManager.Instance.Current.PenOpacity;
                byte alpha = (byte)((0.2 * 255 / Math.Max(1, Math.Pow(ctx.PenThickness, 0.5))) * globalOpacity);

                if (alpha == 0) return;
               
                int radius = (int)ctx.PenThickness;
                if (radius < 1) radius = 1;
                Color baseColor = ctx.PenColor;
                int x_center = (int)p.X;
                int y_center = (int)p.Y;

                int numClumps = radius / 2 + 5;
                int brightnessVariation = 40;

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
                    int endX = Math.Min(w, clumpCenterX + clumpRadius);
                    int startY = Math.Max(0, clumpCenterY - clumpRadius);
                    int endY = Math.Min(h, clumpCenterY + clumpRadius);

                    for (int y = startY; y < endY; y++)
                    {
                        for (int x = startX; x < endX; x++)
                        {
                            int dx = x - clumpCenterX;
                            int dy = y - clumpCenterY;
                            if (dx * dx + dy * dy < clumpRadiusSq)
                            {
                                byte* pixelPtr = basePtr + y * stride + x * 4;
                                byte oldB = pixelPtr[0];
                                byte oldG = pixelPtr[1];
                                byte oldR = pixelPtr[2];
                                pixelPtr[0] = (byte)((clumpB * alpha + oldB * (255 - alpha)) / 255);
                                pixelPtr[1] = (byte)((clumpG * alpha + oldG * (255 - alpha)) / 255);
                                pixelPtr[2] = (byte)((clumpR * alpha + oldR * (255 - alpha)) / 255);
                            }
                        }
                    }
                }
            }

            private unsafe void DrawHighlighterLineUnsafe(ToolContext ctx, Point p1, Point p2, byte* basePtr, int stride, int w, int h)
            {
                int r = (int)ctx.PenThickness; 
                double globalOpacity = TabPaint.SettingsManager.Instance.Current.PenOpacity;
                byte baseAlpha = 30;
                Color c = Color.FromArgb((byte)(baseAlpha * globalOpacity), 255, 255, 0);


                int xmin = (int)Math.Min(p1.X, p2.X) - r;
                int ymin = (int)Math.Min(p1.Y, p2.Y) - r;
                int xmax = (int)Math.Max(p1.X, p2.X) + r;
                int ymax = (int)Math.Max(p1.Y, p2.Y) + r;

                xmin = Math.Max(0, xmin); ymin = Math.Max(0, ymin);
                xmax = Math.Min(w - 1, xmax); ymax = Math.Min(h - 1, ymax);

                double dx = p2.X - p1.X;
                double dy = p2.Y - p1.Y;
                double lenSq = dx * dx + dy * dy;

                int invSA = 255 - c.A;

                for (int y = ymin; y <= ymax; y++)
                {
                    int rowStartIndex = y * w;
                    byte* rowPtr = basePtr + y * stride;
                    for (int x = xmin; x <= xmax; x++)
                    {
                        int pixelIndex = rowStartIndex + x;
                        if (_currentStrokeMask[pixelIndex]) continue;

                        double t = 0;
                        if (lenSq > 0)
                        {
                            t = ((x - p1.X) * dx + (y - p1.Y) * dy) / lenSq;
                            t = Math.Max(0, Math.Min(1, t));
                        }
                        double closeX = p1.X + t * dx;
                        double closeY = p1.Y + t * dy;
                        double distSq = (x - closeX) * (x - closeX) + (y - closeY) * (y - closeY);

                        if (distSq <= r * r)
                        {
                            _currentStrokeMask[pixelIndex] = true;
                            byte* p = rowPtr + x * 4;

                            byte oldB = p[0];
                            byte oldG = p[1];
                            byte oldR = p[2];
                            byte oldA = p[3];

                            p[0] = (byte)((c.B * c.A + oldB * invSA) / 255);
                            p[1] = (byte)((c.G * c.A + oldG * invSA) / 255);
                            p[2] = (byte)((c.R * c.A + oldR * invSA) / 255);
                            p[3] = (byte)(c.A + (oldA * invSA) / 255);
                        }
                    }
                }
            }

            // --- 辅助方法 ---

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
            private byte GetCurrentAlpha(byte originalAlpha)
            {
                // 获取全局设置
                var globalOpacity = TabPaint.SettingsManager.Instance.Current.PenOpacity;
                // 计算最终 Alpha (0-255)
                return (byte)(originalAlpha * globalOpacity);
            }

            private static Int32Rect LineBounds(Point p1, Point p2, int penRadius)
            {
                int expand = penRadius + 2;
                int x = (int)Math.Min(p1.X, p2.X) - expand;
                int y = (int)Math.Min(p1.Y, p2.Y) - expand;
                int w = (int)Math.Abs(p1.X - p2.X) + expand * 2;
                int h = (int)Math.Abs(p1.Y - p2.Y) + expand * 2;
                return ClampRect(new Int32Rect(x, y, w, h),
                    ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Bitmap.PixelWidth,
                    ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Bitmap.PixelHeight);
            }
        }


    }
}