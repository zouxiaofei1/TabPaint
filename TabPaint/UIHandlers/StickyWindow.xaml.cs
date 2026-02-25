using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TabPaint.Windows
{
    public partial class StickyWindow : Window
    {
        private double _aspectRatio;
        private double _originalWidth; // 记录原始宽度用于重置
        public StickyWindow(ImageSource image)
        {
            InitializeComponent();
            this.SupportFocusHighlight();
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
    }
}
