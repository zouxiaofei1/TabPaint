using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace TabPaint.Pages
{

    public partial class GeneralPage : UserControl
    {
        private bool _isSyncingShellIntegrationCheck;

        public GeneralPage()
        {
            InitializeComponent();
            Loaded += GeneralPage_Loaded;
        }

        private void GeneralPage_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshShellIntegrationCheckState();
        }

        private void OnColorRadioClick(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb)
            {
                if (rb.Tag?.ToString() == "Auto")
                {
                    if (SettingsManager.Instance?.Current != null)
                    {
                        SettingsManager.Instance.Current.ThemeAccentColor = "Auto";
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

            // 计算每行个数
            // RadioButton Width=40, Margin=0,0,6,5. 总宽度约为 46
            // 我们通过 WrapPanel 的实际宽度来计算
            double panelWidth = AccentColorItemsControl.ActualWidth;
            if (double.IsNaN(panelWidth) || panelWidth <= 0)
            {
                // 如果还未渲染，回退到固定估算值 (40 + 6 = 46)
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
