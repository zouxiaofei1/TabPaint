using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TabPaint;
using System.Windows.Input;
using System.Windows.Media;
namespace TabPaint.Pages
{
    public partial class AdvancedPage : UserControl
    {
        private enum MemoryUnit { KB, MB, GB }
        private MemoryUnit _currentUnit = MemoryUnit.MB;

        public AdvancedPage()
        {
            InitializeComponent();
            this.Loaded += AdvancedPage_Loaded;
        }

        private void AdvancedPage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateMemoryTextBox();
        }

        private void UpdateMemoryTextBox()
        {
            var settings = SettingsManager.Instance.Current;
            double mbValue = settings.MaxUndoMemoryMB;
            double displayValue = mbValue;

            switch (_currentUnit)
            {
                case MemoryUnit.KB: displayValue = mbValue * 1024; break;
                case MemoryUnit.GB: displayValue = mbValue / 1024.0; break;
            }

            if (UndoMemoryTextBox != null)
                UndoMemoryTextBox.Text = displayValue % 1 == 0 ? displayValue.ToString("0") : displayValue.ToString("0.##");
            if (UnitToggleButton != null)
                UnitToggleButton.Content = _currentUnit.ToString();
        }

        private void UnitToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (UndoMemoryTextBox == null) return;
            if (double.TryParse(UndoMemoryTextBox.Text, out double currentValue))
            {
                // 先转换回 MB
                double mbValue = currentValue;
                switch (_currentUnit)
                {
                    case MemoryUnit.KB: mbValue = currentValue / 1024.0; break;
                    case MemoryUnit.GB: mbValue = currentValue * 1024.0; break;
                }

                // 切换单位
                _currentUnit = (MemoryUnit)(((int)_currentUnit + 1) % 3);

                // 更新显示
                double nextDisplayValue = mbValue;
                switch (_currentUnit)
                {
                    case MemoryUnit.KB: nextDisplayValue = mbValue * 1024; break;
                    case MemoryUnit.GB: nextDisplayValue = mbValue / 1024.0; break;
                }
                UndoMemoryTextBox.Text = nextDisplayValue % 1 == 0 ? nextDisplayValue.ToString("0") : nextDisplayValue.ToString("0.##");
                UnitToggleButton.Content = _currentUnit.ToString();
            }
            else
            {
                _currentUnit = (MemoryUnit)(((int)_currentUnit + 1) % 3);
                UpdateMemoryTextBox();
            }
        }

        private void UndoMemoryTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (UndoMemoryTextBox == null) return;

            if (double.TryParse(UndoMemoryTextBox.Text, out double value))
            {
                double mbValue = value;
                switch (_currentUnit)
                {
                    case MemoryUnit.KB: mbValue = value / 1024.0; break;
                    case MemoryUnit.GB: mbValue = value * 1024.0; break;
                }
                if (mbValue < 0) mbValue = 0;
                if (mbValue > 10240) mbValue = 10240;

                SettingsManager.Instance.Current.MaxUndoMemoryMB = (int)mbValue;
                global::TabPaint.MainWindow.UndoRedoManager.CheckGlobalUndoLimits();
                UpdateMemoryTextBox();
            }
            else
            {
                UpdateMemoryTextBox();
            }
        }

        private void OpenPlugins_Click(object sender, RoutedEventArgs e)
        {
            var win = Window.GetWindow(this) as SettingsWindow;
            if (win != null)
            {
                win.NavListBox.SelectedIndex = 5; // Plugins item index
            }
        }

        private void CollectSystemReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var reportWindow = new TabPaint.Windows.SystemReportWindow();
                reportWindow.Owner = Window.GetWindow(this);
                reportWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MainWindow.GetCurrentInstance()?.ShowToast(string.Format(LocalizationManager.GetString("L_Common_Error") + ": {0}", ex.Message), ex);
            }
        }

        // 打开缓存文件夹
        private void OpenCacheFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string cachePath = Path.Combine(localAppData, "TabPaint");
                if (!Directory.Exists(cachePath)) Directory.CreateDirectory(cachePath);
                Process.Start(new ProcessStartInfo
                {
                    FileName = cachePath,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception) {}
        }

        // 恢复出厂设置
        private void FactoryReset_Click(object sender, RoutedEventArgs e)
        {
            var result = FluentMessageBox.Show(
              LocalizationManager.GetString("L_Settings_Advanced_FactoryReset_Confirm"),
              LocalizationManager.GetString("L_Settings_Advanced_FactoryReset"),
              MessageBoxButton.YesNo,
              MessageBoxImage.Information,
             MainWindow.GetCurrentInstance());//设置窗口用这个，否则左边栏会显示白色背景

            if (result != MessageBoxResult.Yes) return;

            try
            {
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TabPaint");
                string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                string tempBatPath = Path.Combine(Path.GetTempPath(), "tabpaint_reset.bat");
                string batContent = $@"
                        @echo off
                        timeout /t 1 /nobreak > NUL
                        rd /s /q ""{appDataPath}""
                        start """" ""{currentExe}""
                        del ""%~f0""
                        ";
                File.WriteAllText(tempBatPath, batContent);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = tempBatPath,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);

                // 关闭当前应用
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                FluentMessageBox.Show(
                   string.Format(LocalizationManager.GetString("L_Msg_ResetFailed"), ex.Message),
                   LocalizationManager.GetString("L_Common_Error"),
                   MessageBoxButton.OK,
                   MessageBoxImage.Error,
                   Window.GetWindow(this));
            }
        }
        private void TextBox_LostFocus(object sender, RoutedEventArgs e)// 输入框失去焦点时进行数值校验
        {
            if (sender is TextBox textBox)
            {
                if (double.TryParse(textBox.Text, out double value))
                {
                    // 限制范围
                    if (value < 0) value = 0;
                    if (value > 5000) value = 5000;

                    textBox.Text = value.ToString("0");

                    // 显式更新绑定源
                    var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                    binding?.UpdateSource(); global::TabPaint.MainWindow.UndoRedoManager.CheckGlobalUndoLimits();
                }
                else
                {
                    textBox.Text = "0";
                }
            }
        }
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void UndoMemoryTextBox_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            UndoMemoryBorder.BorderBrush = (Brush)FindResource("SystemAccentBrush");
        }

        private void UndoMemoryTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            UndoMemoryBorder.BorderBrush = (Brush)FindResource("BorderBrush");
        }

    }
}
