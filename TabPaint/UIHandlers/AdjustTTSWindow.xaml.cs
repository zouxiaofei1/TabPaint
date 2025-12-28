using System;
using System.Windows;
using System.Windows.Input; // 必须引用，用于 MouseButtonEventArgs
using System.Windows.Controls.Primitives; // 必须引用，用于 DragStartedEventArgs
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;

namespace TabPaint
{
    public partial class AdjustTTSWindow : Window
    {
        private WriteableBitmap _originalBitmap;
        private WriteableBitmap _previewBitmap;

        // 公开最终结果供主窗口获取
        public WriteableBitmap FinalBitmap { get; private set; }

        // 获取当前滑块值
        public double Temperature => TemperatureSlider.Value;
        public double Tint => TintSlider.Value;
        public double Saturation => SaturationSlider.Value;

        public AdjustTTSWindow(WriteableBitmap bitmapForPreview)
        {
            InitializeComponent();

            // 1. 保存原始图像副本用于重置
            _originalBitmap = bitmapForPreview.Clone();

            // 2. 持有主窗口传入的引用用于实时修改
            _previewBitmap = bitmapForPreview;
        }

        // --- 修复 CS1061 错误的关键事件处理程序 ---

        /// <summary>
        /// 标题栏鼠标按下事件，用于拖拽窗口
        /// </summary>
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        /// <summary>
        /// 滑块开始拖拽 (占位符，可用于暂停重绘以提高性能)
        /// </summary>
        private void Slider_DragStarted(object sender, DragStartedEventArgs e)
        {
            // 如果算法很慢，可以在这里设置一个标志位暂停实时预览
        }

        /// <summary>
        /// 滑块拖拽结束 (确保最后一次更新是准确的)
        /// </summary>
        private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            ApplyPreview();
        }

        // ---------------------------------------------

        /// <summary>
        /// 滑块数值改变时触发实时预览
        /// </summary>
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.IsLoaded)
            {
                ApplyPreview();
            }
        }

        private void ApplyPreview()
        {
            if (_originalBitmap == null || _previewBitmap == null) return;

            // 1. 从原始副本重置像素数据 (也就是"撤销"之前的修改，基于原始图重新计算)
            int stride = _originalBitmap.BackBufferStride;
            int byteCount = _originalBitmap.PixelHeight * stride;

            // 注意：频繁分配大数组可能会有GC压力，生产环境可考虑复用buffer
            byte[] pixelData = new byte[byteCount];
            _originalBitmap.CopyPixels(pixelData, stride, 0);

            // 将原始数据写回预览图
            _previewBitmap.WritePixels(new Int32Rect(0, 0, _originalBitmap.PixelWidth, _originalBitmap.PixelHeight), pixelData, stride, 0);

            // 2. 应用新的调整参数
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
            // 点击取消时，必须将图片恢复原状
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

        /// <summary>
        /// 核心调整函数：色温、色调、饱和度 (高性能优化版)
        /// 需要在项目属性中开启 "允许不安全代码 (Allow unsafe code)"
        /// </summary>
        private void AdjustImage(WriteableBitmap bmp, double temperature, double tint, double saturation)
        {
            // 1. 预计算调整因子
            // 色温: -100 (暖) to 100 (冷)。
            double tempAdj = temperature / 2.0;
            // 色调: -100 (绿) to 100 (品红)。
            double tintAdj = tint / 2.0;
            // 饱和度: 映射到 0.0 to 2.0 的乘数。
            double satAdj = (saturation + 100.0) / 100.0;

            bmp.Lock();
            unsafe
            {
                byte* basePtr = (byte*)bmp.BackBuffer;
                int stride = bmp.BackBufferStride;
                int height = bmp.PixelHeight;
                int width = bmp.PixelWidth;

                // 2. 使用并行处理加速
                Parallel.For(0, height, y =>
                {
                    byte* row = basePtr + y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        // 假设格式为 BGRA 或 BGR
                        byte b = row[x * 4];
                        byte g = row[x * 4 + 1];
                        byte r = row[x * 4 + 2];

                        // --- 步骤 A: 调整色温和色调 ---
                        // 色温: 暖色加红，冷色加蓝 (减红)
                        double nr = r + tempAdj;
                        double ng = g;
                        double nb = b - tempAdj;

                        // 色调: 绿色加绿，品红减绿 (简化算法)
                        // 通常 Tint 是调整 G 轴
                        ng += tintAdj;

                        // --- 步骤 B: 调整饱和度 ---
                        if (saturation != 0)
                        {
                            // 计算亮度 (Rec.601 luma)
                            double luminance = 0.299 * nr + 0.587 * ng + 0.114 * nb;

                            // 线性插值: newColor = luminance + saturation * (oldColor - luminance)
                            nr = luminance + satAdj * (nr - luminance);
                            ng = luminance + satAdj * (ng - luminance);
                            nb = luminance + satAdj * (nb - luminance);
                        }

                        // --- 步骤 C: 限制范围 [0, 255] ---
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
