using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using System.Text.RegularExpressions; // 需要引用正则

namespace TabPaint
{
    public partial class AdjustTTSWindow : Window
    {
        private WriteableBitmap _originalBitmap;
        private WriteableBitmap _previewBitmap;

        // 公开最终结果供主窗口获取
        public WriteableBitmap FinalBitmap { get; private set; }

        public double Temperature => TemperatureSlider.Value;
        public double Tint => TintSlider.Value;
        public double Saturation => SaturationSlider.Value;

        public AdjustTTSWindow(WriteableBitmap bitmapForPreview)
        {
            InitializeComponent();
            _originalBitmap = bitmapForPreview.Clone();
            _previewBitmap = bitmapForPreview;
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void Slider_DragStarted(object sender, DragStartedEventArgs e) { }

        private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            ApplyPreview();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.IsLoaded)
            {
                ApplyPreview();
            }
        }

        // --- 新增：数字输入验证 ---
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            // 仅允许输入数字和负号
            Regex regex = new Regex("[^0-9-]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        // --- 新增：回车键确认输入 ---
        // --- 1. 输入字符过滤 (防止输入字母等非法字符) ---


        // --- 2. 文本变化实时更新 (解决输入不立即生效问题) ---
        private void Input_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox == null) return;

            // 利用 Tag 属性获取对应的 Slider 控件
            Slider targetSlider = textBox.Tag as Slider;
            if (targetSlider == null) return;

            string input = textBox.Text;

            // 情况A：输入为空或只有负号，不进行数值更新，也不报错
            if (string.IsNullOrEmpty(input) || input == "-")
                return;

            // 情况B：尝试解析数值
            if (double.TryParse(input, out double result))
            {
                // 限制数值范围 (防止输入 999 导致崩溃或无效)
                if (result > targetSlider.Maximum) result = targetSlider.Maximum;
                if (result < targetSlider.Minimum) result = targetSlider.Minimum;

                // 只有当数值真的改变时才赋值，避免光标跳动问题
                if (Math.Abs(targetSlider.Value - result) > 0.01)
                {
                    targetSlider.Value = result;
                    // Slider.Value 的改变会自动触发 Slider_ValueChanged -> ApplyPreview
                }
            }
        }


        // --- 新增：重置功能 ---
        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            // 归零，由于绑定了 Slider_ValueChanged，会自动触发重绘
            TemperatureSlider.Value = 0;
            TintSlider.Value = 0;
            SaturationSlider.Value = 0;
        }

        private void ApplyPreview()
        {
            if (_originalBitmap == null || _previewBitmap == null) return;

            // 1. 还原
            int stride = _originalBitmap.BackBufferStride;
            int byteCount = _originalBitmap.PixelHeight * stride;
            byte[] pixelData = new byte[byteCount];
            _originalBitmap.CopyPixels(pixelData, stride, 0);
            _previewBitmap.WritePixels(new Int32Rect(0, 0, _originalBitmap.PixelWidth, _originalBitmap.PixelHeight), pixelData, stride, 0);

            // 2. 应用
            AdjustImage(_previewBitmap, Temperature, Tint, Saturation);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            FinalBitmap = _previewBitmap;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // 还原到初始状态
            if (_originalBitmap != null && _previewBitmap != null)
            {
                int stride = _originalBitmap.BackBufferStride;
                int byteCount = _originalBitmap.PixelHeight * stride;
                byte[] pixelData = new byte[byteCount];
                _originalBitmap.CopyPixels(pixelData, stride, 0);
                _previewBitmap.WritePixels(new Int32Rect(0, 0, _originalBitmap.PixelWidth, _originalBitmap.PixelHeight), pixelData, stride, 0);
            }
            DialogResult = false;
            Close();
        }

        private void AdjustImage(WriteableBitmap bmp, double temperature, double tint, double saturation)
        {
            double tempAdj = temperature / 2.0;
            double tintAdj = tint / 2.0;
            double satAdj = (saturation + 100.0) / 100.0;

            bmp.Lock();
            unsafe
            {
                byte* basePtr = (byte*)bmp.BackBuffer;
                int stride = bmp.BackBufferStride;
                int height = bmp.PixelHeight;
                int width = bmp.PixelWidth;

                Parallel.For(0, height, y =>
                {
                    byte* row = basePtr + y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        byte b = row[x * 4];
                        byte g = row[x * 4 + 1];
                        byte r = row[x * 4 + 2];

                        double nr = r + tempAdj;
                        double ng = g + tintAdj;
                        double nb = b - tempAdj;

                        if (saturation != 0)
                        {
                            double luminance = 0.299 * nr + 0.587 * ng + 0.114 * nb;
                            nr = luminance + satAdj * (nr - luminance);
                            ng = luminance + satAdj * (ng - luminance);
                            nb = luminance + satAdj * (nb - luminance);
                        }

                        row[x * 4 + 2] = (byte)(nr < 0 ? 0 : (nr > 255 ? 255 : nr));
                        row[x * 4 + 1] = (byte)(ng < 0 ? 0 : (ng > 255 ? 255 : ng));
                        row[x * 4] = (byte)(nb < 0 ? 0 : (nb > 255 ? 255 : nb));
                    }
                });
            }
            bmp.AddDirtyRect(new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight));
            bmp.Unlock();
        }
    }
}
