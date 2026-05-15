using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace TabPaint
{
    public partial class AdjustColorWindow : Window
    {
        // 原始图像（用于最终应用）
        private WriteableBitmap _originalFullBitmap;

        // 预览用的低分辨率图像（源数据和显示数据）
        private WriteableBitmap _previewSource;
        private WriteableBitmap _previewTarget;

        // 统计直方图数组
        private readonly int[] _redCounts = new int[256];
        private readonly int[] _greenCounts = new int[256];
        private readonly int[] _blueCounts = new int[256];

        // 防抖动计时器
        private DispatcherTimer _updateTimer;
        private bool _isUpdatingFromTextBox = false;
        public bool? Result { get; private set; }
        public AdjustColorWindow(WriteableBitmap fullBitmap, int initialTabIndex = 0)
        {
            InitializeComponent();
            this.SupportFocusHighlight();
            _originalFullBitmap = fullBitmap;
            CreatePreviewBitmaps(fullBitmap);
            PreviewImage.Source = _previewTarget;

            if (initialTabIndex > 0 && initialTabIndex < AdjustTabControl.Items.Count) AdjustTabControl.SelectedIndex = initialTabIndex;

            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) }; // 25fps 左右
            _updateTimer.Tick += (s, e) => { _updateTimer.Stop(); UpdatePreview(); };

            UpdateTextBoxes();
            UpdatePreview(); // 初始渲染
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // 确保句柄已经准备好
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            bool isDark = ThemeManager.CurrentAppliedTheme == AppTheme.Dark;
            ThemeManager.SetWindowImmersiveDarkMode(this, isDark);

            MicaAcrylicManager.ApplyEffect(this);
            if (!MicaAcrylicManager.IsWin11())
            {
                var chromeLow = FindResource("ChromeLowBrush") as Brush;
                //this.Background = FindResource("WindowBackgroundBrush") as Brush;
            }
        }

        private void AdjustTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl)
            {
                var sb = FindResource("TabFadeIn") as System.Windows.Media.Animation.Storyboard;
                if (sb != null)
                {
                    if (AdjustTabControl.SelectedIndex == 0) sb.Begin(BceScrollViewer);
                    else if (AdjustTabControl.SelectedIndex == 1) sb.Begin(TtsScrollViewer);
                }
            }
        }

        private void CreatePreviewBitmaps(WriteableBitmap source)
        {
            // 限制预览图的最大尺寸，避免长图导致内存占用过高或布局溢出
            double maxDim = 800; 
            double scale = 1.0;

            if (source.PixelWidth > maxDim || source.PixelHeight > maxDim)
                scale = Math.Min(maxDim / source.PixelWidth, maxDim / source.PixelHeight);

            int w = (int)(source.PixelWidth * scale);
            int h = (int)(source.PixelHeight * scale);
            if (w < 1) w = 1; if (h < 1) h = 1;

            // 使用 RenderTargetBitmap 进行高质量缩放
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                // 设置缩放模式
                RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
                // 绘制缩放后的图像
                dc.DrawImage(source, new Rect(0, 0, w, h));
            }
            rtb.Render(visual);

            // 创建源副本和目标预览图
            _previewSource = new WriteableBitmap(rtb);
            _previewTarget = _previewSource.Clone();
        }

        // --- 事件处理 ---

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return;
            if (!_isUpdatingFromTextBox) UpdateTextBoxes();
            ThrottleUpdate();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded) return;
            var box = sender as TextBox;
            var slider = box?.Tag as Slider;
            if (slider != null && double.TryParse(box.Text, out double val))
            {
                _isUpdatingFromTextBox = true;
                // 限制范围
                if (val < slider.Minimum) val = slider.Minimum;
                if (val > slider.Maximum) val = slider.Maximum;
                slider.Value = val;
                _isUpdatingFromTextBox = false;
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e) =>
            e.Handled = new Regex("[^0-9.-]+").IsMatch(e.Text);

        private void Slider_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider s) s.Value = 0;
        }

        private void Slider_DragStarted(object sender, DragStartedEventArgs e) { }
        private void Slider_DragCompleted(object sender, DragCompletedEventArgs e) => ThrottleUpdate();

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            // 暂停更新，一次性重置
            _isUpdatingFromTextBox = true;
            BrightnessSlider.Value = 0;
            ContrastSlider.Value = 0;
            ExposureSlider.Value = 0;
            TemperatureSlider.Value = 0;
            TintSlider.Value = 0;
            SaturationSlider.Value = 0;
            _isUpdatingFromTextBox = false;

            UpdateTextBoxes();
            ThrottleUpdate();
        }

        private void UpdateTextBoxes()
        {
            BrightnessBox.Text = BrightnessSlider.Value.ToString("F0");
            ContrastBox.Text = ContrastSlider.Value.ToString("F0");
            ExposureBox.Text = ExposureSlider.Value.ToString("F1");
            TemperatureBox.Text = TemperatureSlider.Value.ToString("F0");
            TintBox.Text = TintSlider.Value.ToString("F0");
            SaturationBox.Text = SaturationSlider.Value.ToString("F0");
        }

        private void ThrottleUpdate()
        {
            if (!_updateTimer.IsEnabled) _updateTimer.Start();
        }
        private void UpdatePreview()
        {
            // 1. 从纯净源拷贝像素到目标
            int stride = _previewSource.BackBufferStride;
            int len = _previewSource.PixelHeight * stride;
            _previewSource.CopyPixels(new Int32Rect(0, 0, _previewSource.PixelWidth, _previewSource.PixelHeight), _previewTarget.BackBuffer, len, stride);

            // 2. 原地修改 _previewTarget
            _previewTarget.Lock();
            ProcessBitmapUnsafe(_previewTarget,
                BrightnessSlider.Value, ContrastSlider.Value, ExposureSlider.Value,
                TemperatureSlider.Value, TintSlider.Value, SaturationSlider.Value);

            // 3. 更新直方图 (在 Lock 期间读取最快)
            UpdateHistogramUnsafe(_previewTarget);

            _previewTarget.AddDirtyRect(new Int32Rect(0, 0, _previewTarget.PixelWidth, _previewTarget.PixelHeight));
            _previewTarget.Unlock();
        }

        // 静态处理函数，包含所有算法管线
        private static unsafe void ProcessBitmapUnsafe(WriteableBitmap bmp,
            double brightness, double contrast, double exposure,
            double temp, double hueShift, double saturation)
        {
            double tempAdj = temp / 2.0;
            double satAdj = (saturation + 100.0) / 100.0;
            bool hasTTS = (temp != 0 || hueShift != 0 || saturation != 0);

            double ctFactor = (100.0 + contrast) / 100.0;
            ctFactor *= ctFactor;
            double expFactor = Math.Pow(2, exposure);
            double brAdj = brightness;
            bool hasBCE = (brightness != 0 || contrast != 0 || exposure != 0);

            if (!hasTTS && !hasBCE) return; // 无变化

            byte* basePtr = (byte*)bmp.BackBuffer;
            int stride = bmp.BackBufferStride;
            int width = bmp.PixelWidth;
            int height = bmp.PixelHeight;

            // 并行循环处理像素
            Parallel.For(0, height, y =>
            {
                byte* row = basePtr + (y * stride);
                for (int x = 0; x < width; x++)
                {
                    // BGRA 顺序
                    double b = row[x * 4];
                    double g = row[x * 4 + 1];
                    double r = row[x * 4 + 2];

                    if (hasTTS)
                    {
                        // 1. 色温 (简易模拟)
                        r += tempAdj;
                        b -= tempAdj;

                        // 2. 色相旋转 (Hue Rotation) & 饱和度
                        if (hueShift != 0 || saturation != 0)
                        {
                            // 转换为 HSL 或简易旋转矩阵
                            // 这里使用旋转矩阵实现色相偏移，效率更高且效果对应彩虹条
                            double h = hueShift * Math.PI / 180.0;
                            double cosH = Math.Cos(h);
                            double sinH = Math.Sin(h);

                            // 色相旋转矩阵系数 (基于 Luma 保持)
                            double r_r = 0.213 + cosH * 0.787 - sinH * 0.213;
                            double r_g = 0.715 - cosH * 0.715 - sinH * 0.715;
                            double r_b = 0.072 - cosH * 0.072 + sinH * 0.928;

                            double g_r = 0.213 - cosH * 0.213 + sinH * 0.143;
                            double g_g = 0.715 + cosH * 0.285 + sinH * 0.140;
                            double g_b = 0.072 - cosH * 0.072 - sinH * 0.283;

                            double b_r = 0.213 - cosH * 0.213 - sinH * 0.787;
                            double b_g = 0.715 - cosH * 0.715 + sinH * 0.715;
                            double b_b = 0.072 + cosH * 0.928 + sinH * 0.072;

                            double nr = r * r_r + g * r_g + b * r_b;
                            double ng = r * g_r + g * g_g + b * g_b;
                            double nb = r * b_r + g * b_g + b * b_b;

                            r = nr; g = ng; b = nb;

                            // 饱和度 (在色相旋转后应用)
                            if (saturation != 0)
                            {
                                double luma = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                                r = luma + satAdj * (r - luma);
                                g = luma + satAdj * (g - luma);
                                b = luma + satAdj * (b - luma);
                            }
                        }
                    }

                    // 3. 应用亮度/对比度/曝光 (BCE)
                    if (hasBCE)
                    {
                        // 应用亮度
                        r += brAdj;
                        g += brAdj;
                        b += brAdj;
                        r = (r - 127.5) * ctFactor + 127.5;
                        g = (g - 127.5) * ctFactor + 127.5;
                        b = (b - 127.5) * ctFactor + 127.5;

                        // 应用曝光
                        r *= expFactor;
                        g *= expFactor;
                        b *= expFactor;
                    }

                    // 3. 钳制并回写
                    row[x * 4 + 2] = (byte)(r < 0 ? 0 : (r > 255 ? 255 : r));
                    row[x * 4 + 1] = (byte)(g < 0 ? 0 : (g > 255 ? 255 : g));
                    row[x * 4] = (byte)(b < 0 ? 0 : (b > 255 ? 255 : b));
                }
            });
        }

        private unsafe void UpdateHistogramUnsafe(WriteableBitmap bmp)
        {
            // 清空数组
            Array.Clear(_redCounts, 0, 256);
            Array.Clear(_greenCounts, 0, 256);
            Array.Clear(_blueCounts, 0, 256);

            byte* ptr = (byte*)bmp.BackBuffer;
            int width = bmp.PixelWidth;
            int height = bmp.PixelHeight;
            int stride = bmp.BackBufferStride;

            // 性能优化：对于大图使用采样步长
            int step = 1;
            if (width * height > 500000) step = 2; // 大于50万像素时，隔一个点采一次样

            for (int y = 0; y < height; y += step)
            {
                byte* row = ptr + (y * stride);
                for (int x = 0; x < width; x += step)
                {
                    if (row[3] > 0)
                    {
                        _blueCounts[row[0]]++;
                        _greenCounts[row[1]]++;
                        _redCounts[row[2]]++;
                    }
                    row += 4 * step;
                }
            }

            int[] displayRed = SmoothHistogram(_redCounts);
            int[] displayGreen = SmoothHistogram(_greenCounts);
            int[] displayBlue = SmoothHistogram(_blueCounts);
            int max = 0;
            for (int i = 0; i < 256; i++)
            {
                if (displayRed[i] > max) max = displayRed[i];
                if (displayGreen[i] > max) max = displayGreen[i];
                if (displayBlue[i] > max) max = displayBlue[i];
            }
            int minStableMax = (width * height) / (step * step) / 50;
            if (max < minStableMax) max = minStableMax;
            if (max < 10) max = 10;

            // UI 更新
            Dispatcher.Invoke(() =>
            {
                UpdatePolyline(HistoRed, displayRed, max);
                UpdatePolyline(HistoGreen, displayGreen, max);
                UpdatePolyline(HistoBlue, displayBlue, max);
            });
        }

        private int[] SmoothHistogram(int[] input)
        {
            int[] output = new int[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                if (i == 0)
                {
                    output[i] = (int)(input[i] * 0.75 + input[i + 1] * 0.25);
                }
                else if (i == input.Length - 1)
                {
                    output[i] = (int)(input[i - 1] * 0.25 + input[i] * 0.75);
                }
                else
                {
                    output[i] = (int)(input[i - 1] * 0.25 + input[i] * 0.5 + input[i + 1] * 0.25);
                }
            }
            return output;
        }


        private void UpdatePolyline(Polyline polyline, int[] counts, int maxVal)
        {
            if (maxVal < 1) maxVal = 1;
            double width = HistogramGrid.ActualWidth > 0 ? HistogramGrid.ActualWidth : 280;
            double height = HistogramGrid.Height; // 通常是 60

            PointCollection points = new PointCollection(258);
            // 左下角起始点
            points.Add(new Point(0, height));

            double stepX = width / 255.0;
            double logMax = Math.Log(maxVal + 1);

            for (int i = 0; i < 256; i++)
            {
                double count = counts[i];

                // 只有当有像素时才计算高度
                if (count > 0)
                {
                    double logVal = Math.Log(count + 1);

                    double normalizedHeight = logVal / logMax;

                    // 绘制点
                    points.Add(new Point(i * stepX, height - (normalizedHeight * height)));
                }
                else points.Add(new Point(i * stepX, height));
            }
            points.Add(new Point(width, height));
            polyline.Points = points;
        }
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                _originalFullBitmap.Lock();
                ProcessBitmapUnsafe(_originalFullBitmap,
                    BrightnessSlider.Value, ContrastSlider.Value, ExposureSlider.Value,
                    TemperatureSlider.Value, TintSlider.Value, SaturationSlider.Value);
                _originalFullBitmap.AddDirtyRect(new Int32Rect(0, 0, _originalFullBitmap.PixelWidth, _originalFullBitmap.PixelHeight));
                _originalFullBitmap.Unlock();

                this.Result = true;
            }
            finally
            {
                Mouse.OverrideCursor = null;
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Result = false;
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) this.DragMove();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            this.Result = false;
            Close();
        }
    }
}
