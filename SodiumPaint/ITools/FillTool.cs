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
        public class FillTool : ToolBase
        {
            public override string Name => "Fill";
            public override System.Windows.Input.Cursor Cursor => System.Windows.Input.Cursors.Hand;
            public override void OnPointerDown(ToolContext ctx, Point viewPos)
            {
                a.s("FillTool");
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
            private Int32Rect FloodFill(CanvasSurface s, int x, int y, Color from, Color to)
            { // 返回填充的包围矩形（用于差分撤销）
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

    }
}