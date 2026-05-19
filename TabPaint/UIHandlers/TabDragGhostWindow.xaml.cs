using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TabPaint;

namespace TabPaint.UIHandlers
{
    public partial class TabDragGhostWindow : Window
    {
        private Point _dpiScale = new Point(1, 1);

        public TabDragGhostWindow()
        {
            InitializeComponent();
            this.SupportFocusHighlight();
        }

        public void UpdateContent(BitmapSource imageSource, string title, int tabCount)
        {
            GhostImage.Source = imageSource;
            GhostTitle.Text = tabCount > 1 ? $"{title} +{tabCount - 1}" : title;
        }

        public void UpdateCompactMode(bool isCompactMode, double tabWidth = 120)
        {
            Width = isCompactMode ? tabWidth + 36 : 140;
            Height = isCompactMode ? 44 : 84;
            GhostImage.Visibility = isCompactMode ? Visibility.Collapsed : Visibility.Visible;
            GhostTitle.Margin = isCompactMode ? new Thickness(6, 0, 22, 0) : new Thickness(0, 4, 0, 0);
            GhostTitle.TextAlignment = isCompactMode ? TextAlignment.Left : TextAlignment.Center;
            GhostTitle.VerticalAlignment = isCompactMode ? VerticalAlignment.Center : VerticalAlignment.Stretch;
        }

        public void SetDpiFromVisual(Visual visual)
        {
            var source = PresentationSource.FromVisual(visual);
            if (source?.CompositionTarget != null)
            {
                var m = source.CompositionTarget.TransformToDevice;
                _dpiScale = new Point(Math.Abs(m.M11), Math.Abs(m.M22));
            }
            else
            {
                _dpiScale = new Point(1, 1);
            }
        }

        public void UpdatePosition(Point screenPoint, Point pointerOffset)
        {
            Left = screenPoint.X / _dpiScale.X - pointerOffset.X;
            Top = screenPoint.Y / _dpiScale.Y - pointerOffset.Y;
        }
    }
}
