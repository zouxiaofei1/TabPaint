using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace TabPaint.Pages
{
    public partial class AboutPage : UserControl
    {
        public string ProgramVersion => AppConsts.ProgramVersion;

        public AboutPage()
        {
            InitializeComponent();
            SetHighResIcon();
        }

        private void SetHighResIcon()
        {
            try
            {
                var iconUri = new Uri("pack://application:,,,/Resources/TabPaint.ico");
                var decoder = BitmapDecoder.Create(iconUri, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnLoad);
                // 找到最大尺寸的图标帧以保证清晰度
                var bestFrame = decoder.Frames.OrderByDescending(f => f.Width).FirstOrDefault();
                if (bestFrame != null)
                {
                    if (AppIcon != null) AppIcon.Source = bestFrame;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Icon load failed: " + ex.Message);
            }
        }

        private void OnOpenUrlClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                string url = btn.Tag.ToString();
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    FluentMessageBox.Show($"{LocalizationManager.GetString("L_Toast_OpenUrlFailed")}: {ex.Message}",
                                LocalizationManager.GetString("L_Common_Error"),
                                MessageBoxButton.OK,
                                MessageBoxImage.Error,
                                Window.GetWindow(this));
                }
            }
        }

        private void OnOpenAgreementClick(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is SettingsWindow settingsWindow)
            {
                settingsWindow.NavigateToTag("Agreement");
            }
        }

        private void OnCheckUpdateClick(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is SettingsWindow settingsWindow)
            {
                settingsWindow.CheckForUpdatesManually();
            }
        }
    }
}
