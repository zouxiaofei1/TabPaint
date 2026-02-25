using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TabPaint.Services;

namespace TabPaint.Windows
{
    public partial class StickyWindow : Window
    {
        private const int WM_SIZING = 0x0214;
        private const int WMSZ_TOPLEFT = 4;
        private const int WMSZ_TOPRIGHT = 5;
        private const int WMSZ_BOTTOMLEFT = 7;
        private const int WMSZ_BOTTOMRIGHT = 8;

        private double _aspectRatio;
        private double _originalWidth; // 记录原始宽度用于重置
        private HwndSource? _hwndSource;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public StickyWindow(ImageSource image)
        {
            InitializeComponent();
            this.SupportFocusHighlight();
            this.SourceInitialized += StickyWindow_SourceInitialized;
            this.Loaded += (_, _) => TrayIconService.UpdateVisibility();
            this.IsVisibleChanged += (_, _) => TrayIconService.UpdateVisibility();
            this.Closed += StickyWindow_Closed;
            DisplayImage.Source = image;

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
