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
using System.Windows.Data;
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
            public void ReplaceBitmap(WriteableBitmap newBitmap)
            {
                if (newBitmap == null) return;

                // 更新内部的位图引用
                this.Bitmap = newBitmap;
                ((MainWindow)System.Windows.Application.Current.MainWindow).BackgroundImage.Source = newBitmap;
                ((MainWindow)System.Windows.Application.Current.MainWindow).BackgroundImage.Width = newBitmap.PixelWidth;
                ((MainWindow)System.Windows.Application.Current.MainWindow).BackgroundImage.Height = newBitmap.PixelHeight;
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
                            rowPtr[0] = color.B; // BGRA 顺序
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
                                if (a > 0)
                                {
                                    dest[0] = b;   // 只有透明度 > 0 的像素才写入
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

    }
}


