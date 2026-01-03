using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TabPaint
{
    public partial class ColorPickerWindow : Window
    {
        public System.Windows.Media.Color PickedColor { get; private set; }
        public bool IsColorPicked { get; private set; } = false;

        private double _dpiX = 1.0;
        private double _dpiY = 1.0;

        public ColorPickerWindow()
        {
            InitializeComponent();
            Loaded += ColorPickerWindow_Loaded;
        }

        private void ColorPickerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 1. 获取当前屏幕的 DPI 缩放比例
            ScreenColorPickerHelper.GetDpiScale(this, out _dpiX, out _dpiY);

            // 2. 设置窗口大小为逻辑尺寸 (覆盖整个虚拟屏幕)
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;

            // 3. 截取物理像素的截图
            var screenshot = ScreenColorPickerHelper.CaptureScreen(_dpiX, _dpiY);
            ScreenShotImage.Source = screenshot;

            // 4. 关键：图片拉伸模式设为 Fill，让物理像素的图片填满逻辑像素的窗口
            ScreenShotImage.Stretch = Stretch.Fill;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            UpdateMagnifier(e.GetPosition(this));
        }

        private void UpdateMagnifier(System.Windows.Point logicalPos)
        {
            // 显示放大镜 UI 逻辑...
            Magnifier.Visibility = Visibility.Visible;
            Canvas.SetLeft(Magnifier, logicalPos.X + 20);
            Canvas.SetTop(Magnifier, logicalPos.Y + 20);

            // --- 核心修复 ---
            // 将 WPF 的逻辑坐标 (Logical) 转换回 屏幕物理坐标 (Physical)
            // 公式：物理坐标 = 逻辑坐标 * DPI缩放 + 屏幕左上角偏移
            int physicalX = (int)((SystemParameters.VirtualScreenLeft + logicalPos.X) * _dpiX);
            int physicalY = (int)((SystemParameters.VirtualScreenTop + logicalPos.Y) * _dpiY);

            // 使用物理坐标去取色
            var c = ScreenColorPickerHelper.GetColorAtPhysical(physicalX, physicalY);

            MagnifierBrush.Color = c;
            ColorText.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var logicalPos = e.GetPosition(this);

                // 同样的坐标转换逻辑
                int physicalX = (int)((SystemParameters.VirtualScreenLeft + logicalPos.X) * _dpiX);
                int physicalY = (int)((SystemParameters.VirtualScreenTop + logicalPos.Y) * _dpiY);

                PickedColor = ScreenColorPickerHelper.GetColorAtPhysical(physicalX, physicalY);
                IsColorPicked = true;
                this.DialogResult = true;
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
