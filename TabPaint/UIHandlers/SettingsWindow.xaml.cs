using System.Windows;
using System.Windows.Controls;

namespace TabPaint
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void NavListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SettingsPanel == null || AboutPanel == null) return;

            int index = NavListBox.SelectedIndex;
            if (index == 0) // 常规设置
            {
                SettingsPanel.Visibility = Visibility.Visible;
                AboutPanel.Visibility = Visibility.Collapsed;
            }
            else if (index == 1) // 关于
            {
                SettingsPanel.Visibility = Visibility.Collapsed;
                AboutPanel.Visibility = Visibility.Visible;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // 这里以后可以添加保存设置的逻辑
            this.DialogResult = true;
            this.Close();
        }
    }
}
