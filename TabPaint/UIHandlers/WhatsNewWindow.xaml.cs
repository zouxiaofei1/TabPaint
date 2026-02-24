using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TabPaint.Windows
{
    public partial class WhatsNewWindow : Window
    {
        public WhatsNewWindow(string fromVersion, string toVersion)
        {
            InitializeComponent();

            string fromText = string.IsNullOrWhiteSpace(fromVersion) ? "-" : fromVersion;
            string toText = string.IsNullOrWhiteSpace(toVersion) ? AppConsts.ProgramVersion : toVersion;

            string format = LocalizationManager.GetString("L_WhatsNew_VersionRange_Format");
            if (string.IsNullOrWhiteSpace(format) || format == "L_WhatsNew_VersionRange_Format")
            {
                format = "{0}->v{1}";
            }

            VersionRangeText.Text = string.Format(format, fromText, toText);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source)
            {
                DependencyObject current = source;
                while (current != null)
                {
                    if (current is System.Windows.Controls.Button)
                    {
                        return;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }

            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
