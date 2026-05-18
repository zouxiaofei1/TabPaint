using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace TabPaint
{
    public partial class ResizeCanvasDialog : Window
    {
        public event Action<int, int, bool> PreviewChanged;
        private const int MaxPixelSize = (int)AppConsts.MaxCanvasSize;
        public int ImageWidth { get; private set; }
        public int ImageHeight { get; private set; }
        public bool IsCanvasResizeMode { get; private set; } // True=Canvas, False=Resample
        public bool IsConfirmed { get; private set; }

        // 原始尺寸
        private int _originalWidth;
        private int _originalHeight;
        private double _originalRatio;
        public bool ApplyToAll => ApplyToAllCheckBox.IsChecked == true;
        public bool IsAspectRatioLocked => AspectRatioToggle.IsChecked == true;

        // 防止事件递归标志
        private bool _isUpdating = false;

        public ResizeCanvasDialog(int currentWidth, int currentHeight)
        {
            InitializeComponent();
            this.SupportFocusHighlight();
            _originalWidth = currentWidth;
            _originalHeight = currentHeight;
            _originalRatio = (double)currentWidth / currentHeight;

            ImageWidth = currentWidth;
            ImageHeight = currentHeight;
        }

        public void ReloadDimensions(int width, int height)
        {
            _originalWidth = width;
            _originalHeight = height;
            _originalRatio = (double)width / height;
            ImageWidth = width;
            ImageHeight = height;
            _isUpdating = true;
            WidthTextBox.Text = width.ToString();
            HeightTextBox.Text = height.ToString();
            WidthSlider.Value = 0;
            HeightSlider.Value = 0;
            _isUpdating = false;
            UpdateInfoText();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        { // 初始化 UI
            _isUpdating = true;
            WidthTextBox.Text = _originalWidth.ToString();
            HeightTextBox.Text = _originalHeight.ToString();
            WidthSlider.Value = 0;     // Slider 默认在中间 (0 = 1.0x)
            HeightSlider.Value = 0;

            _isUpdating = false;
            UpdateInfoText();

            WidthTextBox.Focus();
            WidthTextBox.SelectAll();
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            MicaAcrylicManager.ApplyEffect(this);
            bool isDark = (ThemeManager.CurrentAppliedTheme == AppTheme.Dark);
            ThemeManager.SetWindowImmersiveDarkMode(this, isDark);
            var src = (HwndSource)PresentationSource.FromVisual(this);
            if (src != null)
            {
                src.CompositionTarget.BackgroundColor = Colors.Transparent;
            }
        }
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) this.DragMove();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
        private double SliderToScale(double sliderValue)
        {
            return Math.Pow(10, sliderValue);
        }

        private double ScaleToSlider(double scale)
        {
            if (scale <= 0) return -1;
            return Math.Log10(scale);
        }

        private void UpdateInfoText()
        {
            if (ImageWidth == 0) return;
            double scale = (double)ImageWidth / _originalWidth;

            // 检查是否触顶
            bool isLimitReached = (ImageWidth >= MaxPixelSize || ImageHeight >= MaxPixelSize);

            // 重置状态
            InfoPrefixTextBlock.Text = "";
            InfoScaleValueText.Text = "";
            InfoSuffixTextBlock.Text = "";
            InfoScaleValueText.Visibility = Visibility.Visible;
            if (ScaleEditTextBox.Visibility != Visibility.Visible)
            {
                ScaleEditTextBox.Visibility = Visibility.Collapsed;
            }

            var grayBrush = new SolidColorBrush(Color.FromRgb(136, 136, 136));
            var redBrush = new SolidColorBrush(Color.FromRgb(200, 50, 50));

            if (isLimitReached)
            {
                InfoPrefixTextBlock.Text = string.Format(
                    LocalizationManager.GetString("L_ResizeCanvas_LimitReached"),
                    MaxPixelSize);
                InfoPrefixTextBlock.Foreground = redBrush;
                InfoScaleValueText.Visibility = Visibility.Collapsed;
            }
            else
            {
                InfoPrefixTextBlock.Foreground = grayBrush;
                InfoScaleValueText.Foreground = grayBrush;
                InfoSuffixTextBlock.Foreground = grayBrush;

                if (IsCanvasResizeMode)
                {
                    InfoPrefixTextBlock.Text = string.Format(
                        LocalizationManager.GetString("L_Info_CanvasMode_Format"),
                        ImageWidth,
                        ImageHeight);
                    InfoScaleValueText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    string format = LocalizationManager.GetString("L_Info_ResampleMode_Format");
                    string fullText = string.Format(format, scale, _originalWidth, _originalHeight);
                    string scalePercentText = string.Format("{0:P0}", scale);

                    int index = fullText.IndexOf(scalePercentText);
                    if (index >= 0)
                    {
                        InfoPrefixTextBlock.Text = fullText.Substring(0, index);
                        InfoScaleValueText.Text = scalePercentText;
                        InfoSuffixTextBlock.Text = fullText.Substring(index + scalePercentText.Length);

                        if (ScaleEditTextBox.Visibility != Visibility.Visible)
                        {
                            InfoScaleValueText.Visibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        InfoPrefixTextBlock.Text = fullText;
                        InfoScaleValueText.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }
        private void OnWidthChanged(int newWidth, bool fromSlider)  // 统一处理宽度变更
        {
            if (_isUpdating) return;
            _isUpdating = true;

            if (newWidth > MaxPixelSize) newWidth = MaxPixelSize;

            if (AspectRatioToggle.IsChecked == true)
            {
                int calculatedHeight = (int)Math.Round(newWidth / _originalRatio);

                if (calculatedHeight > MaxPixelSize)
                {
                    calculatedHeight = MaxPixelSize;
                    newWidth = (int)Math.Round(calculatedHeight * _originalRatio);
                }

                ImageHeight = calculatedHeight;
                HeightTextBox.Text = calculatedHeight.ToString();
                HeightSlider.Value = ScaleToSlider((double)calculatedHeight / _originalHeight);
            }

            ImageWidth = newWidth;
            double scale = (double)newWidth / _originalWidth;

            if (!fromSlider) WidthSlider.Value = ScaleToSlider(scale);
            WidthTextBox.Text = newWidth.ToString();

            UpdateInfoText();
            _isUpdating = false;
            PreviewChanged?.Invoke(ImageWidth, ImageHeight, IsCanvasResizeMode);
        }
        private void OnHeightChanged(int newHeight, bool fromSlider)
        {
            if (_isUpdating) return;
            _isUpdating = true;
            if (newHeight > MaxPixelSize) newHeight = MaxPixelSize;

            if (AspectRatioToggle.IsChecked == true)
            {
                int calculatedWidth = (int)Math.Round(newHeight * _originalRatio);
                if (calculatedWidth > MaxPixelSize)
                {
                    calculatedWidth = MaxPixelSize;
                    newHeight = (int)Math.Round(calculatedWidth / _originalRatio);
                }

                ImageWidth = calculatedWidth;
                WidthTextBox.Text = calculatedWidth.ToString();
                WidthSlider.Value = ScaleToSlider((double)calculatedWidth / _originalWidth);
            }
            ImageHeight = newHeight;   // 4. 更新高度相关 UI
            double scale = (double)newHeight / _originalHeight;

            if (!fromSlider)
            {
                HeightSlider.Value = ScaleToSlider(scale);
            }

            HeightTextBox.Text = newHeight.ToString();

            UpdateInfoText();
            _isUpdating = false;
            PreviewChanged?.Invoke(ImageWidth, ImageHeight, IsCanvasResizeMode);
        }
        private void WidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating) return;
            double scale = SliderToScale(WidthSlider.Value);
            int newWidth = (int)Math.Round(_originalWidth * scale);
            // 限制最小 1px
            newWidth = Math.Max(1, newWidth);
            OnWidthChanged(newWidth, true);
        }

        private void HeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating) return;
            double scale = SliderToScale(HeightSlider.Value);
            int newHeight = (int)Math.Round(_originalHeight * scale);
            newHeight = Math.Max(1, newHeight);
            OnHeightChanged(newHeight, true);
        }

        private void WidthTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;
            if (int.TryParse(WidthTextBox.Text, out int w) && w > 0)
            {
                OnWidthChanged(w, false);
            }
        }

        private void HeightTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;
            if (int.TryParse(HeightTextBox.Text, out int h) && h > 0)
            {
                OnHeightChanged(h, false);
            }
        }

        private void AspectRatioToggle_Click(object sender, RoutedEventArgs e)
        {
            // 点击锁链时，立即根据当前宽度重新计算高度以对齐
            if (AspectRatioToggle.IsChecked == true)
            {
                if (int.TryParse(WidthTextBox.Text, out int w)) OnWidthChanged(w, false); // 强制触发一次同步
            }
        }

        private void ModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return; // 防止初始化触发

            IsCanvasResizeMode = ModeComboBox.SelectedIndex == 1;

            UpdateInfoText();

            PreviewChanged?.Invoke(ImageWidth, ImageHeight, IsCanvasResizeMode);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // 最终校验
            if (ImageWidth > MaxPixelSize || ImageHeight > MaxPixelSize)
            {
                FluentMessageBox.Show(
          string.Format(LocalizationManager.GetString("L_Msg_SizeTooLarge_Content"), MaxPixelSize),
          LocalizationManager.GetString("L_Msg_SizeTooLarge_Title"),
          MessageBoxButton.OK);

                // 强制修正回来
                if (ImageWidth > MaxPixelSize) OnWidthChanged(MaxPixelSize, false);
                else if (ImageHeight > MaxPixelSize) OnHeightChanged(MaxPixelSize, false);
                return;
            }

            if (ImageWidth > 0 && ImageHeight > 0)
            {
                IsConfirmed = true;
            }
            else
            {
                FluentMessageBox.Show(
          string.Format(LocalizationManager.GetString("L_ResizeCanvas_Error_InvalidSize")),
          LocalizationManager.GetString("L_Common_Error"));
                return;
            }
            this.Close();

        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
        {

        }

        private void InfoScaleValueText_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            if (IsCanvasResizeMode) return;

            double scale = (double)ImageWidth / _originalWidth;
            ScaleEditTextBox.Text = Math.Round(scale * 100).ToString();

            InfoScaleValueText.Visibility = Visibility.Collapsed;
            ScaleEditTextBox.Visibility = Visibility.Visible;
            ScaleEditTextBox.Focus();
            ScaleEditTextBox.SelectAll();
        }

        private void ScaleEditTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyScaleFromTextBox();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelScaleEdit();
                e.Handled = true;
            }
        }

        private void ScaleEditTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyScaleFromTextBox();
        }

        private void ScaleEditTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;
            if (ScaleEditTextBox.Visibility != Visibility.Visible) return;

            if (double.TryParse(ScaleEditTextBox.Text, out double percent) && percent > 0)
            {
                int newWidth = (int)Math.Round(_originalWidth * percent / 100.0);
                if (newWidth < 1) newWidth = 1;
                if (newWidth > MaxPixelSize) newWidth = MaxPixelSize;

                OnWidthChanged(newWidth, false);
            }
        }

        private void ApplyScaleFromTextBox()
        {
            if (ScaleEditTextBox.Visibility != Visibility.Visible) return;

            if (double.TryParse(ScaleEditTextBox.Text, out double percent))
            {
                int newWidth = (int)Math.Round(_originalWidth * percent / 100.0);
                if (newWidth < 1) newWidth = 1;
                if (newWidth > MaxPixelSize) newWidth = MaxPixelSize;

                OnWidthChanged(newWidth, false);
            }

            CancelScaleEdit();
        }

        private void CancelScaleEdit()
        {
            ScaleEditTextBox.Visibility = Visibility.Collapsed;
            UpdateInfoText();
        }
    }
}
