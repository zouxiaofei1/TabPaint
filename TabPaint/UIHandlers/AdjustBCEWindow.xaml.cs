using System;
using System.Text.RegularExpressions; // 引入正则
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace TabPaint
{
    public partial class AdjustBCEWindow : System.Windows.Window
    {
        private WriteableBitmap _originalBitmap;
        private WriteableBitmap _previewBitmap;
        private Image _targetImage;

        private bool _isDragging = false;
        // 关键标志位：防止 TextBox 和 Slider 互相循环触发更新
        private bool _isUpdatingFromTextBox = false;

        private DispatcherTimer _updateTimer;

        public WriteableBitmap FinalBitmap { get; private set; }

        // 用于外部获取最终值（可选）
        public double Brightness => BrightnessSlider.Value;
        public double Contrast => ContrastSlider.Value;
        public double Exposure => ExposureSlider.Value;

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        public AdjustBCEWindow(WriteableBitmap bitmapForPreview, Image targetImage)
        {
            InitializeComponent();

            _originalBitmap = bitmapForPreview.Clone();
            _targetImage = targetImage;
            _previewBitmap = bitmapForPreview;

            // 确保 targetImage 使用的是我们可操作的位图对象
            // 如果外部传入的已经是 WriteableBitmap 且赋值给了 Source，这一步可能多余，但为了保险：
            // 注意：不要在这里直接设置 targetImage.Source，因为这会改变主界面的引用，
            // 除非我们确定要实时预览到主画布上（看之前的逻辑是这样的）。
            // 保持原样，直接修改 _previewBitmap 像素即可。

            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // 稍微加快响应速度适应打字
            };
            _updateTimer.Tick += UpdateTimer_Tick;

            // 初始化 TextBox 的值（防止初始为空）
            UpdateTextBoxesFromSliders();
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            _updateTimer.Stop();
            ApplyPreview();
        }

        // --- 核心逻辑修改：滑块变动 ---
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded) return;

            // 如果这次变动是由 TextBox 代码触发的，就不再反向写回 TextBox
            // 否则会导致用户输入 "0." 时，被强行格式化为 "0"，小数点无法输入
            if (!_isUpdatingFromTextBox)
            {
                UpdateTextBoxesFromSliders();
            }

            // 应用预览（这里可以根据性能决定是否使用 Timer，
            // 但为了打字跟手，这里简化逻辑，直接用 Timer 去抖动）
            ApplyPreviewWithThrottle();
        }

        // --- 核心逻辑新增：输入框变动 ---
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!this.IsLoaded) return;

            TextBox tb = sender as TextBox;
            if (tb == null) return;

            // 标记开始从 TextBox 更新 Slider
            _isUpdatingFromTextBox = true;

            if (double.TryParse(tb.Text, out double val))
            {
                if (tb == BrightnessBox) BrightnessSlider.Value = val;
                else if (tb == ContrastBox) ContrastSlider.Value = val;
                else if (tb == ExposureBox) ExposureSlider.Value = val;
            }
            // 解析失败（比如空字符串或只有负号）时不更新 Slider，保持原值

            _isUpdatingFromTextBox = false;
        }

        // --- 核心逻辑新增：输入验证（只允许数字、小数点、负号） ---
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            // 正则匹配：允许数字、小数点、负号
            Regex regex = new Regex("[^0-9.-]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        // --- 核心逻辑新增：重置按钮 ---
        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            // 直接设置 Slider，利用 Slider_ValueChanged 事件自动更新 TextBox 和 Preview
            BrightnessSlider.Value = 0;
            ContrastSlider.Value = 0;
            ExposureSlider.Value = 0;
        }

        // 辅助方法：将 Slider 值格式化写入 TextBox
        private void UpdateTextBoxesFromSliders()
        {
            // 亮度对比度取整显示，曝光保留一位小数
            BrightnessBox.Text = BrightnessSlider.Value.ToString("F0");
            ContrastBox.Text = ContrastSlider.Value.ToString("F0");
            ExposureBox.Text = ExposureSlider.Value.ToString("F1");
        }

        // 拖拽相关
        private void Slider_DragStarted(object sender, DragStartedEventArgs e)
        {
            _isDragging = true;
        }

        private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _isDragging = false;
            _updateTimer.Stop();
            ApplyPreview(); // 拖拽结束强制刷新一次
        }

        private void ApplyPreviewWithThrottle()
        {
            // 只有当定时器没在跑的时候才启动它
            if (!_updateTimer.IsEnabled)
            {
                _updateTimer.Start();
            }
        }

        private void ApplyPreview()
        {
            // 这里逻辑不变
            int stride = _originalBitmap.BackBufferStride;
            int byteCount = _originalBitmap.PixelHeight * stride;

            // 注意：频繁创建大数组可能会有GC压力，但在调整窗口生命周期内尚可接受
            // 如果卡顿严重，可以将 pixelData 提升为类成员变量并在构造函数初始化
            byte[] pixelData = new byte[byteCount];

            _originalBitmap.CopyPixels(pixelData, stride, 0);
            _previewBitmap.WritePixels(new Int32Rect(0, 0, _originalBitmap.PixelWidth, _originalBitmap.PixelHeight), pixelData, stride, 0);

            AdjustImage(_previewBitmap, BrightnessSlider.Value, ContrastSlider.Value, ExposureSlider.Value);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            FinalBitmap = _previewBitmap;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _targetImage.Source = _originalBitmap;
            DialogResult = false;
            Close();
        }

        // 保持之前的 Parallel 算法不变
        private void AdjustImage(WriteableBitmap bmp, double brightness, double contrast, double exposure)
        {
            double brAdj = brightness;
            double ctAdj = (100.0 + contrast) / 100.0;
            ctAdj *= ctAdj;
            double expAdj = Math.Pow(2, exposure);

            byte[] lut = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                double val = i + brAdj;
                val = ((((val / 255.0) - 0.5) * ctAdj) + 0.5) * 255.0;
                val *= expAdj;
                if (val > 255) val = 255;
                if (val < 0) val = 0;
                lut[i] = (byte)val;
            }

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
                        row[x * 4] = lut[row[x * 4]];
                        row[x * 4 + 1] = lut[row[x * 4 + 1]];
                        row[x * 4 + 2] = lut[row[x * 4 + 2]];
                    }
                });
            }
            bmp.AddDirtyRect(new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight));
            bmp.Unlock();
        }
    }
}
