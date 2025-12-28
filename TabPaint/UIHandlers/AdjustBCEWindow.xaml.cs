using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; // Thumb
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;           // DispatcherTimer

namespace TabPaint
{
    public partial class AdjustBCEWindow : System.Windows.Window
    {
        private WriteableBitmap _originalBitmap;
        private WriteableBitmap _previewBitmap;
        private Image _targetImage;

        // --- 新增的成员变量 ---
        private bool _isDragging = false;
        private DispatcherTimer _updateTimer;
        // ----------------------
        public WriteableBitmap FinalBitmap { get; private set; }
        public double Brightness => BrightnessSlider.Value;
        public double Contrast => ContrastSlider.Value;
        public double Exposure => ExposureSlider.Value;
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
        public AdjustBCEWindow(WriteableBitmap bitmapForPreview,Image targetImage)
        {
            InitializeComponent();
            
            // 保存这个副本的原始状态，以便在拖动滑块时重置
            _originalBitmap = bitmapForPreview.Clone();
            _targetImage = targetImage;
            // _previewBitmap 就是我们要在窗口中操作和显示的位图
            _previewBitmap = bitmapForPreview;

            // 在窗口内部创建一个Image控件来显示预览，或者直接操作传入的bitmap
            // 为了解耦，最好是在AdjustBCEWindow的XAML里放一个Image控件
            targetImage.Source = _previewBitmap; // 假设你在XAML里有个叫PreviewImage的控件

            // 初始化定时器
            _updateTimer = new DispatcherTimer
            {
                // 设置一个延迟，例如100毫秒。意味着预览最多每秒更新10次。
                // 这个值可以调整，值越小响应越快，但CPU占用越高。50-100ms是比较好的平衡点。
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _updateTimer.Tick += UpdateTimer_Tick;
        }

        // 当定时器触发时，执行真正的预览更新
        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            _updateTimer.Stop(); // 定时器只触发一次
            ApplyPreview();
        }

        // --- 核心逻辑修改 ---
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded) return;
            ApplyPreview();//可优化
        }

        // --- 新增的事件处理器 ---
        private void Slider_DragStarted(object sender, DragStartedEventArgs e)
        {
            _isDragging = true;
        }

        private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _isDragging = false;
            // 拖动结束后，停止任何待处理的定时器更新
            _updateTimer.Stop();
            ApplyPreview();
        }
        // ------------------------

        private void ApplyPreview()
        {
            // 关键改动：从_originalBitmap重置_previewBitmap的像素
            int stride = _originalBitmap.BackBufferStride;
            int byteCount = _originalBitmap.PixelHeight * stride;
            byte[] pixelData = new byte[byteCount];
            _originalBitmap.CopyPixels(pixelData, stride, 0);
            _previewBitmap.WritePixels(new Int32Rect(0, 0, _originalBitmap.PixelWidth, _originalBitmap.PixelHeight), pixelData, stride, 0);

            // 在重置后的预览位图上应用当前滑块的调整
            AdjustImage(_previewBitmap, BrightnessSlider.Value, ContrastSlider.Value, ExposureSlider.Value);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // 将最终处理好的位图存入公共属性
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

        // AdjustImage 方法保持之前优化过的版本（使用LUT和并行计算）
        private void AdjustImage(WriteableBitmap bmp, double brightness, double contrast, double exposure)
        {
            // ... (此处代码与之前优化后的版本完全相同) ...
            // 1. 预计算调整因子
            double brAdj = brightness;
            double ctAdj = (100.0 + contrast) / 100.0;
            ctAdj *= ctAdj;
            double expAdj = Math.Pow(2, exposure);

            // 2. 创建查找表 (LUT)
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

                // 3. 使用并行处理
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
