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
        private const double VisibleAngleRange = 60.0;

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
        private double _currentAngle = 0;
        private double _dragStartAngle = 0;
        private double _dragStartMouseX = 0;

        public double CurrentAngle => _currentAngle;

        private static double ClampAngle(double value)
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

            // 计算可见角度范围，支持平滑滚动
            double visibleStart = _currentAngle - VisibleAngleRange / 2.0;
            double visibleEnd = _currentAngle + VisibleAngleRange / 2.0;

            // 稍微扩大绘制边界，避免边缘刻度突然消失
            int startAngle = (int)Math.Floor(visibleStart) - 1;
            int endAngle = (int)Math.Ceiling(visibleEnd) + 1;

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

        private void UpdateAngle(double angle, bool raiseEvent)
        {
            angle = ClampAngle(angle);
            
            // Shift 键吸附
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                angle = Math.Round(angle / 15.0) * 15.0;
            }

            double oldAngle = _currentAngle;
            _currentAngle = angle;

            if (FindName("RotateAngleText") is TextBlock angleText)
                angleText.Text = $"{(int)Math.Round(angle)}°";

            DrawTicks();

            if (raiseEvent && Math.Abs(oldAngle - _currentAngle) > 0.001)
                AngleChanged?.Invoke(this, new RoutedEventArgs());
        }

        private void RotateBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            RotateBarBorder.CaptureMouse();

            Point pos = e.GetPosition(RotateTickCanvas);
            _dragStartMouseX = pos.X;
            _dragStartAngle = _currentAngle;

            e.Handled = true;
        }

        private void RotateBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || e.LeftButton != MouseButtonState.Pressed) return;

            Point pos = e.GetPosition(RotateTickCanvas);
            double deltaX = pos.X - _dragStartMouseX;
            double width = RotateTickCanvas.ActualWidth;

            if (width > 0)
            {
                // 线性映射：鼠标移动一个容器宽度，旋转一个量程的角度
                double deltaAngle = (deltaX / width) * VisibleAngleRange;
                UpdateAngle(_dragStartAngle + deltaAngle, true);
            }
            
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

        public void SetValue(double value)
        {
            UpdateAngle(value, false);
        }
    }
}
