using System;
using System.Drawing; // NuGet: System.Drawing.Common
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Point = System.Drawing.Point;

namespace TabPaint
{
    public class ScreenColorPickerHelper
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

        // 获取设备各方向的逻辑像素与物理像素的比率
        public static void GetDpiScale(Visual visual, out double dpiX, out double dpiY)
        {
            var source = PresentationSource.FromVisual(visual);
            if (source != null && source.CompositionTarget != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
                return;
            }
            // 默认值
            dpiX = 1.0;
            dpiY = 1.0;
        }

        // 修改后的截屏方法：捕获物理像素，并处理多屏幕边界
        public static BitmapSource CaptureScreen(double dpiScaleX, double dpiScaleY)
        {
            // 1. 获取 WPF 逻辑坐标系的虚拟屏幕尺寸
            double screenLeft = SystemParameters.VirtualScreenLeft;
            double screenTop = SystemParameters.VirtualScreenTop;
            double screenWidth = SystemParameters.VirtualScreenWidth;
            double screenHeight = SystemParameters.VirtualScreenHeight;

            // 2. 转换为物理像素尺寸 (用于 Bitmap 大小)
            int physicalWidth = (int)(screenWidth * dpiScaleX);
            int physicalHeight = (int)(screenHeight * dpiScaleY);

            using (var screenBmp = new Bitmap(physicalWidth, physicalHeight))
            {
                using (var bmpGraphics = Graphics.FromImage(screenBmp))
                {
                    // 3. 这里的 CopyFromScreen 通常接受的是物理坐标（取决于 App 的 DPI 感知级别）
                    // 为了保险，我们从逻辑左上角开始截取整个物理区域
                    bmpGraphics.CopyFromScreen(
                        (int)(screenLeft * dpiScaleX),
                        (int)(screenTop * dpiScaleY),
                        0, 0,
                        screenBmp.Size);

                    // 转换为 WPF BitmapSource
                    var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                        screenBmp.GetHbitmap(),
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    // 重要：标记该图片不需要再被系统拉伸，否则可能再次模糊
                    bitmapSource.Freeze();
                    return bitmapSource;
                }
            }
        }

        // 直接通过 GDI 获取屏幕物理坐标的颜色
        public static System.Windows.Media.Color GetColorAtPhysical(int x, int y)
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            uint pixel = GetPixel(hdc, x, y);
            ReleaseDC(IntPtr.Zero, hdc);

            byte r = (byte)(pixel & 0x000000FF);
            byte g = (byte)((pixel & 0x0000FF00) >> 8);
            byte b = (byte)((pixel & 0x00FF0000) >> 16);

            return System.Windows.Media.Color.FromRgb(r, g, b);
        }
    }
}
