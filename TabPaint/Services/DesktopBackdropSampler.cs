using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TabPaint
{
    internal static class DesktopBackdropSampler
    {
        private const int SPI_GETDESKWALLPAPER = 0x0073;
        private const int MAX_PATH = 260;
        private const int BackdropDownsampleFactor = 3;
        private const int BackdropBlurRadius = 120;
        private const int MaxBackdropBlurRadius = 300;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SystemParametersInfo(int uiAction, int uiParam, System.Text.StringBuilder pvParam, int fWinIni);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        private static string _cachedPath;
        private static DateTime _cachedWriteTimeUtc;
        private static BitmapSource _cachedWallpaper;

        public static Brush CreateBackdropBrushForWindow(Window window, bool isDark)
        {
            try
            {
                var wallpaper = GetWallpaperBitmap();
                if (wallpaper == null) return null;

                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var winRect)) return null;

                if (!TryGetMonitorBounds(hwnd, out var monitorRect)) return null;

                int screenW = Math.Max(1, monitorRect.Right - monitorRect.Left);
                int screenH = Math.Max(1, monitorRect.Bottom - monitorRect.Top);
                int winW = Math.Max(1, winRect.Right - winRect.Left);
                int winH = Math.Max(1, winRect.Bottom - winRect.Top);

                int localLeft = winRect.Left - monitorRect.Left;
                int localTop = winRect.Top - monitorRect.Top;

                double scale = Math.Max((double)screenW / wallpaper.PixelWidth, (double)screenH / wallpaper.PixelHeight);
                double scaledW = wallpaper.PixelWidth * scale;
                double scaledH = wallpaper.PixelHeight * scale;

                double offsetX = (scaledW - screenW) * 0.5;
                double offsetY = (scaledH - screenH) * 0.5;

                int srcX = (int)Math.Round((localLeft + offsetX) / scale);
                int srcY = (int)Math.Round((localTop + offsetY) / scale);
                int srcW = (int)Math.Round(winW / scale);
                int srcH = (int)Math.Round(winH / scale);

                srcW = Math.Max(1, srcW);
                srcH = Math.Max(1, srcH);
                srcX = Math.Max(0, Math.Min(wallpaper.PixelWidth - 1, srcX));
                srcY = Math.Max(0, Math.Min(wallpaper.PixelHeight - 1, srcY));
                srcW = Math.Max(1, Math.Min(srcW, wallpaper.PixelWidth - srcX));
                srcH = Math.Max(1, Math.Min(srcH, wallpaper.PixelHeight - srcY));

                BitmapSource crop = new CroppedBitmap(wallpaper, new Int32Rect(srcX, srcY, srcW, srcH));
                BitmapSource blurred = FastBlur(crop, BackdropDownsampleFactor, BackdropBlurRadius);

                Color baseColor = isDark ? Colors.Black : Colors.White;
                double backdropOpacity = isDark ? 0.15 : 0.18;
                Color tint = isDark ? Color.FromArgb(34, 0, 0, 0) : Color.FromArgb(12, 255, 255, 255);

                BitmapSource composed = ComposeBackdrop(blurred, baseColor, backdropOpacity, tint);
                if (composed.CanFreeze) composed.Freeze();

                var brush = new ImageBrush(composed)
                {
                    Stretch = Stretch.Fill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center,
                    Opacity = 1.0
                };

                if (brush.CanFreeze) brush.Freeze();
                return brush;
            }
            catch
            {
                return null;
            }
        }

        private static BitmapSource GetWallpaperBitmap()
        {
            string path = GetWallpaperPath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

            DateTime writeTime = File.GetLastWriteTimeUtc(path);
            if (_cachedWallpaper != null && string.Equals(_cachedPath, path, StringComparison.OrdinalIgnoreCase) && writeTime == _cachedWriteTimeUtc)
            {
                return _cachedWallpaper;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            _cachedPath = path;
            _cachedWriteTimeUtc = writeTime;
            _cachedWallpaper = bitmap;
            return _cachedWallpaper;
        }

        private static string GetWallpaperPath()
        {
            var sb = new System.Text.StringBuilder(MAX_PATH);
            return SystemParametersInfo(SPI_GETDESKWALLPAPER, sb.Capacity, sb, 0) ? sb.ToString() : null;
        }

        private static bool TryGetMonitorBounds(IntPtr hwnd, out RECT monitorRect)
        {
            monitorRect = default;
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero) return false;

            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref monitorInfo)) return false;

            monitorRect = monitorInfo.rcMonitor;
            return true;
        }

        private static BitmapSource FastBlur(BitmapSource source, int downsampleFactor, int radius)
        {
            int safeRadius = Math.Max(1, Math.Min(radius, MaxBackdropBlurRadius));
            int adaptiveDownsample = Math.Max(1, downsampleFactor + (safeRadius / 35));

            int smallW = Math.Max(1, source.PixelWidth / adaptiveDownsample);
            int smallH = Math.Max(1, source.PixelHeight / adaptiveDownsample);

            var scaledDown = new TransformedBitmap(source, new ScaleTransform((double)smallW / source.PixelWidth, (double)smallH / source.PixelHeight));
            var formatted = new FormatConvertedBitmap(scaledDown, PixelFormats.Bgra32, null, 0);

            int stride = smallW * 4;
            byte[] src = new byte[stride * smallH];
            byte[] dst = new byte[src.Length];
            formatted.CopyPixels(src, stride, 0);

            int scaledRadius = Math.Max(1, safeRadius / adaptiveDownsample);
            int passes = scaledRadius > 12 ? 3 : 2;

            for (int i = 0; i < passes; i++)
            {
                BoxBlurHorizontal(src, dst, smallW, smallH, scaledRadius);
                BoxBlurVertical(dst, src, smallW, smallH, scaledRadius);
            }

            var wb = new WriteableBitmap(smallW, smallH, 96, 96, PixelFormats.Bgra32, null);
            wb.WritePixels(new Int32Rect(0, 0, smallW, smallH), src, stride, 0);

            return new TransformedBitmap(wb, new ScaleTransform((double)source.PixelWidth / smallW, (double)source.PixelHeight / smallH));
        }

        private static void BoxBlurHorizontal(byte[] input, byte[] output, int width, int height, int radius)
        {
            int windowSize = radius * 2 + 1;
            int maxX = width - 1;

            for (int y = 0; y < height; y++)
            {
                int rowStart = y * width * 4;
                int sumB = 0, sumG = 0, sumR = 0, sumA = 0;

                for (int k = -radius; k <= radius; k++)
                {
                    int sampleX = Math.Max(0, Math.Min(maxX, k));
                    int idx = rowStart + sampleX * 4;
                    sumB += input[idx + 0];
                    sumG += input[idx + 1];
                    sumR += input[idx + 2];
                    sumA += input[idx + 3];
                }

                for (int x = 0; x < width; x++)
                {
                    int outIdx = rowStart + x * 4;
                    output[outIdx + 0] = (byte)(sumB / windowSize);
                    output[outIdx + 1] = (byte)(sumG / windowSize);
                    output[outIdx + 2] = (byte)(sumR / windowSize);
                    output[outIdx + 3] = (byte)(sumA / windowSize);

                    int removeX = Math.Max(0, x - radius);
                    int addX = Math.Min(maxX, x + radius + 1);

                    int removeIdx = rowStart + removeX * 4;
                    int addIdx = rowStart + addX * 4;

                    sumB += input[addIdx + 0] - input[removeIdx + 0];
                    sumG += input[addIdx + 1] - input[removeIdx + 1];
                    sumR += input[addIdx + 2] - input[removeIdx + 2];
                    sumA += input[addIdx + 3] - input[removeIdx + 3];
                }
            }
        }

        private static void BoxBlurVertical(byte[] input, byte[] output, int width, int height, int radius)
        {
            int windowSize = radius * 2 + 1;
            int maxY = height - 1;

            for (int x = 0; x < width; x++)
            {
                int baseIdx = x * 4;
                int sumB = 0, sumG = 0, sumR = 0, sumA = 0;

                for (int k = -radius; k <= radius; k++)
                {
                    int sampleY = Math.Max(0, Math.Min(maxY, k));
                    int idx = (sampleY * width * 4) + baseIdx;
                    sumB += input[idx + 0];
                    sumG += input[idx + 1];
                    sumR += input[idx + 2];
                    sumA += input[idx + 3];
                }

                for (int y = 0; y < height; y++)
                {
                    int outIdx = (y * width * 4) + baseIdx;
                    output[outIdx + 0] = (byte)(sumB / windowSize);
                    output[outIdx + 1] = (byte)(sumG / windowSize);
                    output[outIdx + 2] = (byte)(sumR / windowSize);
                    output[outIdx + 3] = (byte)(sumA / windowSize);

                    int removeY = Math.Max(0, y - radius);
                    int addY = Math.Min(maxY, y + radius + 1);

                    int removeIdx = (removeY * width * 4) + baseIdx;
                    int addIdx = (addY * width * 4) + baseIdx;

                    sumB += input[addIdx + 0] - input[removeIdx + 0];
                    sumG += input[addIdx + 1] - input[removeIdx + 1];
                    sumR += input[addIdx + 2] - input[removeIdx + 2];
                    sumA += input[addIdx + 3] - input[removeIdx + 3];
                }
            }
        }

        private static BitmapSource ComposeBackdrop(BitmapSource source, Color baseColor, double backdropOpacity, Color tint)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(baseColor), null, new Rect(0, 0, source.PixelWidth, source.PixelHeight));

                dc.PushOpacity(Math.Max(0.0, Math.Min(1.0, backdropOpacity)));
                dc.DrawImage(source, new Rect(0, 0, source.PixelWidth, source.PixelHeight));
                dc.Pop();

                dc.DrawRectangle(new SolidColorBrush(tint), null, new Rect(0, 0, source.PixelWidth, source.PixelHeight));
            }

            var rtb = new RenderTargetBitmap(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }
    }
}