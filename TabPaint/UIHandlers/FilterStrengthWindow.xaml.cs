using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace TabPaint.Windows
{
    public partial class FilterStrengthWindow : Window
    {
        public int ResultValue { get; private set; }
        public bool IsConfirmed { get; private set; } = false;

        private bool _isUpdatingFromCode = false;

        public FilterStrengthWindow(string title, int initialValue, int min, int max, string labelText = null)
        {
            InitializeComponent();
            this.SupportFocusHighlight();
            if (!string.IsNullOrEmpty(title))
            {
                TitleTextBlock.Text = title;
                this.Title = title;
            }
            if (!string.IsNullOrEmpty(labelText))
            {
                LabelTextBlock.Text = labelText;
            }
            StrengthSlider.Minimum = min;
            StrengthSlider.Maximum = max;
            StrengthSlider.Value = initialValue;
            UpdateTextBox(initialValue);
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
            if (e.ButtonState == MouseButtonState.Pressed)
                this.DragMove();
        }
        private void StrengthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingFromCode) return;
            UpdateTextBox((int)e.NewValue);
        }

        private void ValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingFromCode) return;

            if (int.TryParse(ValueTextBox.Text, out int val))
            {
                // 限制输入范围
                if (val > StrengthSlider.Maximum) val = (int)StrengthSlider.Maximum;
                if (val < StrengthSlider.Minimum) val = (int)StrengthSlider.Minimum;

                _isUpdatingFromCode = true;
                StrengthSlider.Value = val;
                _isUpdatingFromCode = false;
            }
        }

        private void UpdateTextBox(int value)
        {
            _isUpdatingFromCode = true;
            ValueTextBox.Text = value.ToString();
            _isUpdatingFromCode = false;
        }
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)//输入验证
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
        private void OK_Click(object sender, RoutedEventArgs e)//按钮逻辑
        {
            ResultValue = (int)StrengthSlider.Value;
            IsConfirmed = true;
            Close();
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            Close();
        }
        private void RootGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!ValueTextBox.IsMouseOver)  RootGrid.Focus(); 
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                btn.Content = "\uE922";
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                btn.Content = "\uE923";
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            Close();
        }
    }
}
