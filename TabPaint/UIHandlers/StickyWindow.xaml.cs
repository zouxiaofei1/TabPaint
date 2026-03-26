using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Collections.Specialized;
using System.IO;
using TabPaint.Services;

namespace TabPaint.Windows
{
    public partial class StickyWindow : Window
    {
        private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".ico"
        };

        private const int GWL_STYLE = -16;
        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const int WS_MINIMIZEBOX = 0x00020000;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int WM_SIZING = 0x0214;
        private const int WMSZ_TOPLEFT = 4;
        private const int WMSZ_TOPRIGHT = 5;
        private const int WMSZ_BOTTOMLEFT = 7;
        private const int WMSZ_BOTTOMRIGHT = 8;

        private double _aspectRatio;
        private double _originalWidth; // 记录原始宽度用于重置
        private HwndSource? _hwndSource;
        private readonly string? _sourceFilePath;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public StickyWindow(ImageSource image, string? sourceFilePath = null)
        {
            InitializeComponent();
            this.SupportFocusHighlight();
            this.SourceInitialized += StickyWindow_SourceInitialized;
            this.Loaded += (_, _) => TrayIconService.UpdateVisibility();
            this.IsVisibleChanged += (_, _) => TrayIconService.UpdateVisibility();
            this.Closed += StickyWindow_Closed;
            DisplayImage.Source = image;
            _sourceFilePath = sourceFilePath;

            if (image != null)
            {
                _aspectRatio = image.Width / image.Height;
                double screenHeight = SystemParameters.PrimaryScreenHeight;
                double targetHeight = Math.Min(image.Height, screenHeight / 2);
                this.Height = targetHeight;
                this.Width = targetHeight * _aspectRatio;
                _originalWidth = this.Width; // 保存初始大小
            }
        }

        public static StickyWindow? CreateAndShowStickyWindow(ImageSource image, Point? screenCenter = null, string? sourceFilePath = null)
        {
            if (image == null) return null;

            if (image is Freezable freezable && !freezable.IsFrozen && freezable.CanFreeze)
            {
                freezable.Freeze();
            }

            var stickyWin = new StickyWindow(image, sourceFilePath);

            if (screenCenter.HasValue)
            {
                stickyWin.Left = screenCenter.Value.X - (stickyWin.Width / 2);
                stickyWin.Top = screenCenter.Value.Y - (stickyWin.Height / 2);
            }

            stickyWin.Show();
            return stickyWin;
        }

        private void StickyWindow_Closed(object? sender, EventArgs e)
        {
            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }

            TrayIconService.UpdateVisibility();

            bool hasMainWindow = Application.Current.Windows.OfType<MainWindow>().Any(w => w.IsVisible);
            bool hasOtherSticky = Application.Current.Windows.OfType<StickyWindow>().Any(w => w != this && w.IsVisible);

            if (!hasMainWindow && !hasOtherSticky)
            {
                Application.Current.Shutdown();
            }
        }

        private void StickyWindow_SourceInitialized(object? sender, EventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            if (helper.Handle == IntPtr.Zero) return;

            // Disable Aero Snap by removing Maximize and Minimize box styles
            int style = GetWindowLong(helper.Handle, GWL_STYLE);
            SetWindowLong(helper.Handle, GWL_STYLE, style & ~WS_MAXIMIZEBOX & ~WS_MINIMIZEBOX);

            _hwndSource = HwndSource.FromHwnd(helper.Handle);
            _hwndSource?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == AppConsts.WM_NCHITTEST)
            {
                handled = true;
                return HitTestResizeCorner(lParam);
            }

            if (msg == WM_SIZING && _aspectRatio > 0 && lParam != IntPtr.Zero)
            {
                int edge = wParam.ToInt32();
                if (edge == WMSZ_TOPLEFT || edge == WMSZ_TOPRIGHT || edge == WMSZ_BOTTOMLEFT || edge == WMSZ_BOTTOMRIGHT)
                {
                    var rect = System.Runtime.InteropServices.Marshal.PtrToStructure<RECT>(lParam);
                    ConstrainCornerSizingRect(edge, ref rect);
                    System.Runtime.InteropServices.Marshal.StructureToPtr(rect, lParam, false);
                }

                handled = true;
                return new IntPtr(1);
            }

            return IntPtr.Zero;
        }

        private IntPtr HitTestResizeCorner(IntPtr lParam)
        {
            var screenPoint = new Point(
                (short)(lParam.ToInt32() & 0xFFFF),
                (short)((lParam.ToInt32() >> 16) & 0xFFFF));
            var mousePos = PointFromScreen(screenPoint);

            double width = ActualWidth;
            double height = ActualHeight;
            int cornerArea = AppConsts.WindowCornerArea;

            if (mousePos.Y <= cornerArea && mousePos.X <= cornerArea) return (IntPtr)AppConsts.HTTOPLEFT;
            if (mousePos.Y <= cornerArea && mousePos.X >= width - cornerArea) return (IntPtr)AppConsts.HTTOPRIGHT;
            if (mousePos.Y >= height - cornerArea && mousePos.X <= cornerArea) return (IntPtr)AppConsts.HTBOTTOMLEFT;
            if (mousePos.Y >= height - cornerArea && mousePos.X >= width - cornerArea) return (IntPtr)AppConsts.HTBOTTOMRIGHT;

            return (IntPtr)AppConsts.HTCLIENT;
        }

        private void ConstrainCornerSizingRect(int edge, ref RECT rect)
        {
            double width = Math.Max(1, rect.Right - rect.Left);
            double height = Math.Max(1, rect.Bottom - rect.Top);

            double hFromW = width / _aspectRatio;
            double wFromH = height * _aspectRatio;

            double newWidth;
            double newHeight;
            if (Math.Abs(hFromW - height) < Math.Abs(wFromH - width))
            {
                newWidth = width;
                newHeight = hFromW;
            }
            else
            {
                newWidth = wFromH;
                newHeight = height;
            }

            const double minShortSide = 50;
            double minWidth = _aspectRatio >= 1 ? minShortSide * _aspectRatio : minShortSide;
            double minHeight = _aspectRatio >= 1 ? minShortSide : minShortSide / _aspectRatio;

            if (newWidth < minWidth)
            {
                newWidth = minWidth;
                newHeight = newWidth / _aspectRatio;
            }

            if (newHeight < minHeight)
            {
                newHeight = minHeight;
                newWidth = newHeight * _aspectRatio;
            }

            int targetW = (int)Math.Round(newWidth);
            int targetH = (int)Math.Round(newHeight);

            switch (edge)
            {
                case WMSZ_TOPLEFT:
                    rect.Left = rect.Right - targetW;
                    rect.Top = rect.Bottom - targetH;
                    break;
                case WMSZ_TOPRIGHT:
                    rect.Right = rect.Left + targetW;
                    rect.Top = rect.Bottom - targetH;
                    break;
                case WMSZ_BOTTOMLEFT:
                    rect.Left = rect.Right - targetW;
                    rect.Bottom = rect.Top + targetH;
                    break;
                case WMSZ_BOTTOMRIGHT:
                    rect.Right = rect.Left + targetW;
                    rect.Bottom = rect.Top + targetH;
                    break;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e){if (e.ButtonState == MouseButtonState.Pressed) this.DragMove(); } // 拖动窗口

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e){this.Close();   } // 双击关闭

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
       
            Point mousePos = e.GetPosition(this);
            double oldWidth = this.Width;
            double oldHeight = this.Height;

            double scale = e.Delta > 0 ? 1.1 : 0.9;

            if (this.Height >= SystemParameters.PrimaryScreenHeight || this.Width >= SystemParameters.PrimaryScreenWidth)
                if(e.Delta > 0)
                    return;
            double newHeight = oldHeight * scale;
            double newWidth = newHeight * _aspectRatio;

            if (newWidth > 50 && newHeight > 50)
            {
                double scaleX = newWidth / oldWidth;
                double scaleY = newHeight / oldHeight;

                // Keep the content under mouse cursor stable while zooming.
                this.Left = this.Left + mousePos.X - (mousePos.X * scaleX);
                this.Top = this.Top + mousePos.Y - (mousePos.Y * scaleY);
                this.Width = newWidth;
                this.Height = newHeight;
            }
        }
        private void OnCloseClick(object sender, RoutedEventArgs e){ this.Close(); }

        private void OnTopMostClick(object sender, RoutedEventArgs e) { if (sender is MenuItem item)this.Topmost = item.IsChecked; }

        private void OnOpacityChangeClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag != null)
            {
                if (double.TryParse(item.Tag.ToString(), out double opacity)) this.Opacity = opacity;
            }
        }

        private void OnResetScaleClick(object sender, RoutedEventArgs e)
        {
            // 重置大小
            if (_originalWidth > 0 && _aspectRatio > 0)
            {
                this.Width = _originalWidth;
                this.Height = _originalWidth / _aspectRatio;
            }
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            try
            {
                string? filePath = ResolveClipboardFilePath();
                if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath)) return;

                var dataObject = new DataObject();
                var fileList = new StringCollection
                {
                    filePath
                };
                dataObject.SetFileDropList(fileList);
                Clipboard.SetDataObject(dataObject, true);
            }
            catch
            {
                // Ignore clipboard exceptions to keep sticky actions non-blocking.
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.C)
            {
                OnCopyClick(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void OnCloseAllClick(object sender, RoutedEventArgs e)
        {
            var allStickyWindows = Application.Current.Windows.OfType<StickyWindow>().ToList();
            foreach (var win in allStickyWindows)
            {
                win.Close();
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            bool hasImageFile = TryGetFirstImageFilePath(e.Data, out _);
            e.Effects = hasImageFile ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (!TryGetFirstImageFilePath(e.Data, out var filePath))
                {
                    e.Handled = true;
                    return;
                }

                var bitmap = LoadBitmapFromFile(filePath);
                if (bitmap == null)
                {
                    e.Handled = true;
                    return;
                }

                Point localPos = e.GetPosition(this);
                Point screenPos = PointToScreen(localPos);
                CreateAndShowStickyWindow(bitmap, screenPos, filePath);
            }
            catch
            {
                // Ignore drop errors and keep current sticky window usable.
            }

            e.Handled = true;
        }

        private static bool TryGetFirstImageFilePath(IDataObject dataObject, out string filePath)
        {
            filePath = string.Empty;
            if (dataObject == null || !dataObject.GetDataPresent(DataFormats.FileDrop)) return false;

            if (dataObject.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return false;

            string? firstImage = files.FirstOrDefault(IsSupportedImageFile);
            if (string.IsNullOrWhiteSpace(firstImage)) return false;

            filePath = firstImage;
            return true;
        }

        private static bool IsSupportedImageFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            string ext = System.IO.Path.GetExtension(filePath);
            return SupportedImageExtensions.Contains(ext);
        }

        private static BitmapSource? LoadBitmapFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath)) return null;

            using var fs = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
            var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0) return null;

            BitmapSource frame = decoder.Frames[0];
            if (frame.CanFreeze) frame.Freeze();
            return frame;
        }

        private string? ResolveClipboardFilePath()
        {
            if (!string.IsNullOrWhiteSpace(_sourceFilePath) && System.IO.File.Exists(_sourceFilePath))
            {
                return _sourceFilePath;
            }

            if (DisplayImage.Source is not BitmapSource bitmap) return null;

            string clipDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache", "ClipboardTemp");
            if (!Directory.Exists(clipDir)) Directory.CreateDirectory(clipDir);

            string filePath = System.IO.Path.Combine(clipDir, $"Sticky_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(fs);
            return filePath;
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            ShowHoverCloseButton();
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            HideHoverCloseButton();
        }

        private void ShowHoverCloseButton()
        {
            var closeButton = this.FindName("HoverCloseButton") as Button;
            if (closeButton == null) return;

            closeButton.Visibility = Visibility.Visible;
            closeButton.BeginAnimation(OpacityProperty, null);
            closeButton.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
        }

        private void HideHoverCloseButton()
        {
            var closeButton = this.FindName("HoverCloseButton") as Button;
            if (closeButton == null) return;

            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(120));
            fadeOut.Completed += (_, __) =>
            {
                if (!closeButton.IsMouseOver)
                {
                    closeButton.Visibility = Visibility.Collapsed;
                }
            };
            closeButton.BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}
