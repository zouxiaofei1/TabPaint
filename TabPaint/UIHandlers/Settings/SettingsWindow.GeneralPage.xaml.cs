using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace TabPaint.Pages
{

    public partial class GeneralPage : UserControl, System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSyncingShellIntegrationCheck;

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        private SolidColorBrush _systemColorShade0;
        public SolidColorBrush SystemColorShade0 { get => _systemColorShade0; set { _systemColorShade0 = value; OnPropertyChanged(); } }
        private SolidColorBrush _systemColorShade1;
        public SolidColorBrush SystemColorShade1 { get => _systemColorShade1; set { _systemColorShade1 = value; OnPropertyChanged(); } }
        private SolidColorBrush _systemColorShade2;
        public SolidColorBrush SystemColorShade2 { get => _systemColorShade2; set { _systemColorShade2 = value; OnPropertyChanged(); } }
        private SolidColorBrush _systemColorShade3;
        public SolidColorBrush SystemColorShade4 { get => _systemColorShade4; set { _systemColorShade4 = value; OnPropertyChanged(); } }
        private SolidColorBrush _systemColorShade4;
        public SolidColorBrush SystemColorShade5 { get => _systemColorShade5; set { _systemColorShade5 = value; OnPropertyChanged(); } }
        private SolidColorBrush _systemColorShade5;
        public SolidColorBrush SystemColorShade6 { get => _systemColorShade6; set { _systemColorShade6 = value; OnPropertyChanged(); } }
        private SolidColorBrush _systemColorShade6;
        public SolidColorBrush SystemColorShade7 { get => _systemColorShade7; set { _systemColorShade7 = value; OnPropertyChanged(); } }
        private SolidColorBrush _systemColorShade7;
        public SolidColorBrush SystemColorShade3 { get => _systemColorShade3; set { _systemColorShade3 = value; OnPropertyChanged(); } }
        public GeneralPage()
        {
            InitializeComponent();
            this.DataContext = this;
            Loaded += GeneralPage_Loaded;
            Unloaded += GeneralPage_Unloaded;
            UpdateSystemShades();
        }

        private void GeneralPage_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshShellIntegrationCheckState();
            ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;
        }

        private void GeneralPage_Unloaded(object sender, RoutedEventArgs e)
        {
            ThemeManager.ThemeChanged -= ThemeManager_ThemeChanged;
        }

        private void ThemeManager_ThemeChanged()
        {
            UpdateSystemShades();
        }

        private void UpdateSystemShades()
        {
            try
            {
                Color baseColor = SystemParameters.WindowGlassColor;
                SystemColorShade0 = new SolidColorBrush(ChangeColorBrightness(baseColor, 0.4f));
                SystemColorShade1 = new SolidColorBrush(ChangeColorBrightness(baseColor, 0.3f));
                SystemColorShade2 = new SolidColorBrush(ChangeColorBrightness(baseColor, 0.2f));
                SystemColorShade3 = new SolidColorBrush(ChangeColorBrightness(baseColor, 0.1f));
                SystemColorShade4 = new SolidColorBrush(ChangeColorBrightness(baseColor, 0f));
                SystemColorShade5 = new SolidColorBrush(ChangeColorBrightness(baseColor, -0.1f));
                SystemColorShade6 = new SolidColorBrush(ChangeColorBrightness(baseColor, -0.2f));
                SystemColorShade7 = new SolidColorBrush(ChangeColorBrightness(baseColor, -0.3f));

                SystemColorShade0.Freeze();
                SystemColorShade1.Freeze();
                SystemColorShade2.Freeze();
                SystemColorShade3.Freeze();
                SystemColorShade4.Freeze();
                SystemColorShade5.Freeze();
                SystemColorShade6.Freeze();
                SystemColorShade7.Freeze();
            }
            catch { }
        }

        private Color ChangeColorBrightness(Color color, float correctionFactor)
        {
            float red = (float)color.R;
            float green = (float)color.G;
            float blue = (float)color.B;

            if (correctionFactor < 0)
            {
                correctionFactor = 1 + correctionFactor;
                red *= correctionFactor;
                green *= correctionFactor;
                blue *= correctionFactor;
            }
            else
            {
                red = (255 - red) * correctionFactor + red;
                green = (255 - green) * correctionFactor + green;
                blue = (255 - blue) * correctionFactor + blue;
            }

            return Color.FromArgb(color.A, (byte)red, (byte)green, (byte)blue);
        }

        private void OnColorRadioClick(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb)
            {
                string tag = rb.Tag?.ToString();
                if (tag == "Auto" || (tag != null && tag.StartsWith("Auto_")))
                {
                    if (SettingsManager.Instance?.Current != null)
                    {
                        SettingsManager.Instance.Current.ThemeAccentColor = tag;
                    }
                    return;
                }

                if (rb.Background is SolidColorBrush brush)
                {
                    if (SettingsManager.Instance?.Current != null)
                    {
                        SettingsManager.Instance.Current.ThemeAccentColor = brush.Color.ToString();
                    }
                }
            }
        }

        private void OnAccentColorPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!(e.OriginalSource is RadioButton currentRb)) return;

            var items = AccentColorItemsControl.Items;
            int currentIndex = items.IndexOf(currentRb);
            if (currentIndex == -1) return;

            int nextIndex = -1;
            int itemsCount = items.Count;

            double panelWidth = AccentColorItemsControl.ActualWidth;
            if (double.IsNaN(panelWidth) || panelWidth <= 0)
            {
                panelWidth = 200; // GeneralPage 默认宽度大概在 400 左右，Grid 列宽 200+*
            }
            int itemsPerRow = (int)Math.Max(1, Math.Floor(panelWidth / 46));

            switch (e.Key)
            {
                case Key.Left:
                case Key.A:
                    nextIndex = currentIndex - 1;
                    break;
                case Key.Right:
                case Key.D:
                    nextIndex = currentIndex + 1;
                    break;
                case Key.Up:
                case Key.W:
                    nextIndex = currentIndex - itemsPerRow;
                    break;
                case Key.Down:
                case Key.S:
                    nextIndex = currentIndex + itemsPerRow;
                    break;
                default:
                    return;
            }

            if (nextIndex >= 0 && nextIndex < itemsCount)
            {
                if (AccentColorItemsControl.ItemContainerGenerator.ContainerFromIndex(nextIndex) is RadioButton nextRb)
                {
                    nextRb.Focus();
                    nextRb.IsChecked = true;
                    OnColorRadioClick(nextRb, new RoutedEventArgs());
                    e.Handled = true;
                }
                else
                {
                    // ItemsControl 里的项可能是直接定义的 RadioButton，而不是绑定的数据
                    if (items[nextIndex] is RadioButton nextRbLiteral)
                    {
                        nextRbLiteral.Focus();
                        nextRbLiteral.IsChecked = true;
                        OnColorRadioClick(nextRbLiteral, new RoutedEventArgs());
                        e.Handled = true;
                    }
                }
            }
        }

        private void RefreshShellIntegrationCheckState()
        {
            var shellIntegrationCheckBox = FindName("ShellIntegrationCheckBox") as CheckBox;
            if (shellIntegrationCheckBox == null) return;

            _isSyncingShellIntegrationCheck = true;
            shellIntegrationCheckBox.IsChecked = ShellIntegrationService.IsShellIntegrationEnabled();
            _isSyncingShellIntegrationCheck = false;
        }

        private void OnShellIntegrationChecked(object sender, RoutedEventArgs e)
        {
            if (_isSyncingShellIntegrationCheck) return;

            try
            {
                ShellIntegrationService.EnableShellIntegration();
            }
            catch (Exception ex)
            {
                var settingsWindow = Window.GetWindow(this) as TabPaint.SettingsWindow;
                settingsWindow?.ShowToast(string.Format(
                    LocalizationManager.GetString("L_Settings_Toast_ShellIntegration_EnableFailed"),
                    ex.Message));
            }
            finally
            {
                RefreshShellIntegrationCheckState();
            }
        }

        private void OnShellIntegrationUnchecked(object sender, RoutedEventArgs e)
        {
            if (_isSyncingShellIntegrationCheck) return;

            try
            {
                ShellIntegrationService.DisableShellIntegration();
            }
            catch (Exception ex)
            {
                var settingsWindow = Window.GetWindow(this) as TabPaint.SettingsWindow;
                settingsWindow?.ShowToast(string.Format(
                    LocalizationManager.GetString("L_Settings_Toast_ShellIntegration_DisableFailed"),
                    ex.Message));
            }
            finally
            {
                RefreshShellIntegrationCheckState();
            }
        }
    }
}
