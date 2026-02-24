using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TabPaint.Controls
{
    public partial class SelectionRotateFloat : UserControl
    {
        private const int MinAngle = -180;
        private const int MaxAngle = 180;
        private const double VisibleAngleRange = 30.0;

        public event RoutedEventHandler? AngleChanged;

        public SelectionRotateFloat()
        {
            InitializeComponent();
            Loaded += (_, __) => DrawTicks();
            SizeChanged += (_, __) => DrawTicks();
        }

        private void UserControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private bool _isDragging = false;
        private int _currentAngle = 0;

        public int CurrentAngle => _currentAngle;

        private static int ClampAngle(int value)
        {
            if (value < MinAngle) return MinAngle;
            if (value > MaxAngle) return MaxAngle;
            return value;
        }

        private Brush ResolveBrush(string key, Brush fallback)
        {
            return TryFindResource(key) as Brush ?? fallback;
        }

        private void DrawTicks()
        {
            if (RotateTickCanvas == null) return;

            RotateTickCanvas.Children.Clear();

            double width = RotateTickCanvas.ActualWidth;
            double height = RotateTickCanvas.ActualHeight;
            if (width <= 0 || height <= 0) return;

            Brush normalBrush = ResolveBrush("TextSecondaryBrush", new SolidColorBrush(Color.FromArgb(120, 150, 150, 150)));
            Brush focusBrush = ResolveBrush("TextPrimaryBrush", new SolidColorBrush(Color.FromArgb(220, 200, 200, 200)));

            double visibleStart = _currentAngle - VisibleAngleRange / 2.0;
            double visibleEnd = _currentAngle + VisibleAngleRange / 2.0;

            int startAngle = (int)Math.Ceiling(visibleStart);
            int endAngle = (int)Math.Floor(visibleEnd);

            for (int angle = startAngle; angle <= endAngle; angle++)
            {
                if (angle < MinAngle || angle > MaxAngle) continue;

                double x = (angle - visibleStart) / VisibleAngleRange * width;

                bool isSpecial = (angle == 0) || (angle % 15 == 0);
                bool isMidTick = !isSpecial && (angle % 5 == 0);

                double lineHeight = isSpecial ? height * 0.95 : (isMidTick ? height * 0.75 : height * 0.55);
                double y = (height - lineHeight) / 2.0;

                var line = new Line
                {
                    X1 = x,
                    X2 = x,
                    Y1 = y,
                    Y2 = y + lineHeight,
                    StrokeThickness = isSpecial ? 1.3 : 1.0,
                    Stroke = isSpecial ? focusBrush : normalBrush,
                    SnapsToDevicePixels = true,
                    IsHitTestVisible = false
                };

                RotateTickCanvas.Children.Add(line);
            }
        }

        private int PositionToAngle(Point p)
        {
            if (RotateTickCanvas == null || RotateTickCanvas.ActualWidth <= 0) return _currentAngle;

            double x = p.X;
            if (x < 0) x = 0;
            if (x > RotateTickCanvas.ActualWidth) x = RotateTickCanvas.ActualWidth;

            double normalized = x / RotateTickCanvas.ActualWidth;
            double offset = (normalized - 0.5) * VisibleAngleRange;
            int angle = (int)Math.Round(_currentAngle + offset);
            return ClampAngle(angle);
        }

        private void UpdateAngle(int angle, bool raiseEvent)
        {
            angle = ClampAngle(angle);
            _currentAngle = angle;

            if (FindName("RotateAngleText") is TextBlock angleText)
                angleText.Text = $"{angle}°";

            DrawTicks();

            if (raiseEvent)
                AngleChanged?.Invoke(this, new RoutedEventArgs());
        }

        private void RotateBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            RotateBarBorder.CaptureMouse();

            int angle = PositionToAngle(e.GetPosition(RotateTickCanvas));
            UpdateAngle(angle, true);
            e.Handled = true;
        }

        private void RotateBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || e.LeftButton != MouseButtonState.Pressed) return;

            int angle = PositionToAngle(e.GetPosition(RotateTickCanvas));
            UpdateAngle(angle, true);
            e.Handled = true;
        }

        private void RotateBar_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;
            _isDragging = false;
            RotateBarBorder.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void RotateBar_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton != MouseButtonState.Pressed)
            {
                _isDragging = false;
                RotateBarBorder.ReleaseMouseCapture();
            }
        }

        private void RotateBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                _isDragging = false;
                RotateBarBorder.ReleaseMouseCapture();
                UpdateAngle(0, true);
                e.Handled = true;
            }
        }

        public void SetValue(int value)
        {
            UpdateAngle(value, false);
        }
    }
}
