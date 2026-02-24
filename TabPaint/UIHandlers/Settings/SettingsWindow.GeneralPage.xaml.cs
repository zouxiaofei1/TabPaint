using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
            if (sender is RadioButton rb && rb.Background is SolidColorBrush brush)
            {
                if (SettingsManager.Instance?.Current != null)
                {
                    SettingsManager.Instance.Current.ThemeAccentColor = brush.Color.ToString();
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
