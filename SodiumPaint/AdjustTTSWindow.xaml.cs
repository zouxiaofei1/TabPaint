using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;

namespace SodiumPaint
{
    public partial class AdjustTTSWindow : Window
    {
        private WriteableBitmap _originalBitmap;
        private WriteableBitmap _previewBitmap;

        public WriteableBitmap FinalBitmap { get; private set; }

        public double Temperature => TemperatureSlider.Value;
        public double Tint => TintSlider.Value;
        public double Saturation => SaturationSlider.Value;

        public AdjustTTSWindow(WriteableBitmap bitmapForPreview)
        {
            InitializeComponent();

            _originalBitmap = bitmapForPreview.Clone();
            _previewBitmap = bitmapForPreview;

            // 如果您想在窗口内看到预览，需要一个Image控件。
            // 否则，主窗口的图像会实时更新，因为我们操作的是同一个bitmap对象。
            // 为了简单起见，我们假设主窗口的图像会更新。
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.IsLoaded)
            {
                ApplyPreview();
            }
        }

        private void ApplyPreview()
        {
            // 从原始副本重置像素数据
            int stride = _originalBitmap.BackBufferStride;
            int byteCount = _originalBitmap.PixelHeight * stride;
            byte[] pixelData = new byte[byteCount];
            _originalBitmap.CopyPixels(pixelData, stride, 0);
            _previewBitmap.WritePixels(new Int32Rect(0, 0, _originalBitmap.PixelWidth, _originalBitmap.PixelHeight), pixelData, stride, 0);

            // 应用新的调整
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
            // 恢复主窗口的图像到打开此窗口前的状态
            int stride = _originalBitmap.BackBufferStride;
            int byteCount = _originalBitmap.PixelHeight * stride;
            byte[] pixelData = new byte[byteCount];
            _originalBitmap.CopyPixels(pixelData, stride, 0);
            _previewBitmap.WritePixels(new Int32Rect(0, 0, _originalBitmap.PixelWidth, _originalBitmap.PixelHeight), pixelData, stride, 0);

            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 核心调整函数：色温、色调、饱和度 (高性能优化版)
        /// </summary>
        private void AdjustImage(WriteableBitmap bmp, double temperature, double tint, double saturation)
        {
            // 1. 预计算调整因子
            // 色温: -100 (暖) to 100 (冷)。我们将其映射到一个较小的范围以获得更精细的控制。
            double tempAdj = temperature / 2.0; // 调整范围为 -50 to 50
            // 色调: -100 (绿) to 100 (品红)。同样进行缩放。
            double tintAdj = tint / 2.0; // 调整范围为 -50 to 50
            // 饱和度: -100 (灰度) to 100 (鲜艳)。映射到 0.0 to 2.0 的乘数。
            double satAdj = (saturation + 100.0) / 100.0;

            bmp.Lock();
            unsafe
            {
                byte* basePtr = (byte*)bmp.BackBuffer;
                int stride = bmp.BackBufferStride;
                int height = bmp.PixelHeight;
                int width = bmp.PixelWidth;

                // 2. 使用并行处理
                Parallel.For(0, height, y =>
                {
                    byte* row = basePtr + y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        byte b = row[x * 4];
                        byte g = row[x * 4 + 1];
                        byte r = row[x * 4 + 2];

                        // --- 步骤 A: 调整色温和色调 ---
                        // 色温: 暖色加红，冷色加蓝 (减红)
                        double nr = r + tempAdj;
                        double ng = g;
                        double nb = b - tempAdj;

                        // 色调: 绿色加绿，品红减绿
                        ng -= tintAdj;

                        // --- 步骤 B: 调整饱和度 ---
                        if (saturation != 0) // 饱和度为0时无需计算
                        {
                            // 计算亮度 (灰度值)
                            double luminance = 0.299 * nr + 0.587 * ng + 0.114 * nb;

                            // 线性插值: newColor = luminance + saturation * (oldColor - luminance)
                            nr = luminance + satAdj * (nr - luminance);
                            ng = luminance + satAdj * (ng - luminance);
                            nb = luminance + satAdj * (nb - luminance);
                        }

                        // --- 步骤 C: 限制范围 [0, 255] ---
                        row[x * 4 + 2] = (byte)Math.Max(0, Math.Min(255, nr));
                        row[x * 4 + 1] = (byte)Math.Max(0, Math.Min(255, ng));
                        row[x * 4] = (byte)Math.Max(0, Math.Min(255, nb));
                    }
                });
            }
            bmp.AddDirtyRect(new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight));
            bmp.Unlock();
        }
    }
}
