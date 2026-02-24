using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace TabPaint
{
    public partial class ColorPickerWindow : Window
    {
        public System.Windows.Media.Color PickedColor { get; private set; }
        public bool IsColorPicked { get; private set; } = false;

        private double _dpiX = 1.0;
        private double _dpiY = 1.0;

        private const int ZoomPixelSize = AppConsts.ColorPickerZoomPixelSize;

        // UI 引用缓存
        private Grid _magnifierContainer;
        private ScaleTransform _magnifierScale;
        private TranslateTransform _magnifierTransform;
        private VisualBrush _zoomBrush;
        private TextBlock _colorTextHex;
        private TextBlock _colorTextRgb;
        private Ellipse _outerRing;

        public ColorPickerWindow()
        {
            InitializeComponent();
            this.SupportFocusHighlight();
        }

        private void ColorPickerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ScreenColorPickerHelper.GetDpiScale(this, out _dpiX, out _dpiY);

            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;

            var screenshot = ScreenColorPickerHelper.CaptureScreen(_dpiX, _dpiY);
            ScreenShotImage.Source = screenshot;
            ScreenShotImage.Stretch = Stretch.Fill;

            // 初始化控件引用，防止某些环境下自动生成的字段不可用
            _magnifierContainer = FindName("MagnifierContainer") as Grid;
            _magnifierScale = FindName("MagnifierScale") as ScaleTransform;
            _magnifierTransform = FindName("MagnifierTransform") as TranslateTransform;
            _zoomBrush = FindName("ZoomBrush") as VisualBrush;
            _colorTextHex = FindName("ColorTextHex") as TextBlock;
            _colorTextRgb = FindName("ColorTextRgb") as TextBlock;
            _outerRing = FindName("OuterRing") as Ellipse;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            UpdateMagnifier(e.GetPosition(this));
        }

        private void UpdateMagnifier(System.Windows.Point logicalPos)
        {
            if (_magnifierContainer == null) return;

            // 1. 处理显示/隐藏动画 (这部分只需触发一次，保留动画是可以的)
            if (_magnifierContainer.Visibility != Visibility.Visible)
            {
                _magnifierContainer.Visibility = Visibility.Visible;

                if (_magnifierScale != null)
                {
                    // 弹出的缩放动画保留，增加高级感
                    DoubleAnimation scaleAnim = new DoubleAnimation(0.5, 1.0, TimeSpan.FromMilliseconds(150))
                    {
                        EasingFunction = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut }
                    };
                    _magnifierScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    _magnifierScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                }
            }

            // 2. 计算位置 (保持原有的边界检测逻辑)
            double offset = 30;
            double magWidth = _magnifierContainer.Width;
            double magHeight = _magnifierContainer.Height;

            double targetLeft = logicalPos.X + offset;
            double targetTop = logicalPos.Y + offset;

            // 边界检测：防止超出屏幕右侧和下侧
            if (targetLeft + magWidth > this.ActualWidth) targetLeft = logicalPos.X - magWidth - offset;
            // 下方预留更多空间给数值卡片
            if (targetTop + magHeight + 65 > this.ActualHeight) targetTop = logicalPos.Y - magHeight - offset - 65;

            // 3. 【关键修改】直接设置位置，移除动画
            if (_magnifierTransform != null)
            {
                // 必须先断开之前的任何动画锁定（如果有的话），否则直接赋值无效
                _magnifierTransform.BeginAnimation(TranslateTransform.XProperty, null);
                _magnifierTransform.BeginAnimation(TranslateTransform.YProperty, null);

                // 直接赋值，实现 1:1 瞬间跟随
                _magnifierTransform.X = targetLeft;
                _magnifierTransform.Y = targetTop;
            }

            // 4. 更新放大镜内容 Viewbox (保持不变)
            double halfSize = ZoomPixelSize / 2.0;

            if (_zoomBrush != null)
            {
                _zoomBrush.Viewbox = new Rect(
                    logicalPos.X - halfSize,
                    logicalPos.Y - halfSize,
                    ZoomPixelSize,
                    ZoomPixelSize);
            }

            // 5. 获取颜色并更新 UI (保持不变)
            int physicalX = (int)((SystemParameters.VirtualScreenLeft + logicalPos.X) * _dpiX);
            int physicalY = (int)((SystemParameters.VirtualScreenTop + logicalPos.Y) * _dpiY);

            if (physicalX >= 0 && physicalY >= 0)
            {
                try
                {
                    var c = ScreenColorPickerHelper.GetColorAtPhysical(physicalX, physicalY);
                    if (_colorTextHex != null) _colorTextHex.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                    if (_colorTextRgb != null) _colorTextRgb.Text = $"RGB: {c.R}, {c.G}, {c.B}";
                    if (_outerRing != null) _outerRing.Stroke = new SolidColorBrush(c);
                }
                catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
            }
        }


        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var logicalPos = e.GetPosition(this);
                int physicalX = (int)((SystemParameters.VirtualScreenLeft + logicalPos.X) * _dpiX);
                int physicalY = (int)((SystemParameters.VirtualScreenTop + logicalPos.Y) * _dpiY);

                try
                {
                    PickedColor = ScreenColorPickerHelper.GetColorAtPhysical(physicalX, physicalY);
                    IsColorPicked = true;
                    this.DialogResult = true;
                    this.Close();
                }
                catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                this.DialogResult = false;
                this.Close();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.DialogResult = false;
                this.Close();
            }
        }
    }
}
