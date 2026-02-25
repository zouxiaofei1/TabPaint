using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TabPaint;
using System.Windows.Media;

namespace TabPaint.Pages
{
    public partial class AdvancedPage : UserControl
    {
        private enum MemoryUnit { KB, MB, GB }
        private sealed class FactoryResetDeleteProgress
        {
            public int DeletedFiles { get; init; }
            public int TotalFiles { get; init; }
            public string CurrentFileName { get; init; } = string.Empty;
        }

        private const int FactoryResetFloatDelayMs = 350;
        private const int FactoryResetFloatFadeOutMs = 550;

        private MemoryUnit _currentUnit = MemoryUnit.MB;
        private CancellationTokenSource? _factoryResetCts;

        public AdvancedPage()
        {
            InitializeComponent();
            this.Loaded += AdvancedPage_Loaded;
            FloatFactoryReset.CancelRequested += (s, e) => _factoryResetCts?.Cancel();
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
            win?.NavigateToTag("Plugins");
        }

        private void CollectSystemReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = Window.GetWindow(this) as SettingsWindow;
                win?.NavigateToTag("SystemReport");
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
        private async void FactoryReset_Click(object sender, RoutedEventArgs e)
        {
            var result = FluentMessageBox.Show(
              LocalizationManager.GetString("L_Settings_Advanced_FactoryReset_Confirm"),
              LocalizationManager.GetString("L_Settings_Advanced_FactoryReset"),
              MessageBoxButton.YesNo,
              MessageBoxImage.Information,
             MainWindow.GetCurrentInstance());//设置窗口用这个，否则左边栏会显示白色背景

            if (result != MessageBoxResult.Yes) return;

            _factoryResetCts?.Cancel();
            _factoryResetCts = new CancellationTokenSource();

            try
            {
                bool deleted = await DeletePythonRuntimeWithProgressAsync(_factoryResetCts.Token);
                if (!deleted)
                    return;

                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TabPaint");
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? Environment.ProcessPath ?? string.Empty;
                if (string.IsNullOrWhiteSpace(currentExe))
                    throw new InvalidOperationException("Current executable path not found.");

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
            catch (OperationCanceledException)
            {
                FloatFactoryReset.Finish();
            }
            catch (Exception ex)
            {
                FloatFactoryReset.Finish();
                FluentMessageBox.Show(
                   string.Format(LocalizationManager.GetString("L_Msg_ResetFailed"), ex.Message),
                   LocalizationManager.GetString("L_Common_Error"),
                   MessageBoxButton.OK,
                   MessageBoxImage.Error,
                   Window.GetWindow(this));
            }
        }

        private async Task<bool> DeletePythonRuntimeWithProgressAsync(CancellationToken cancellationToken)
        {
            if (!Directory.Exists(AppConsts.PyOcrRuntimeDir))
                return true;

            var stopwatch = Stopwatch.StartNew();
            bool floatShown = false;

            FloatFactoryReset.SetIcon("🧹");

            var progress = new Progress<FactoryResetDeleteProgress>(state =>
            {
                if (!floatShown && stopwatch.ElapsedMilliseconds < FactoryResetFloatDelayMs)
                    return;

                floatShown = true;

                int safeTotal = Math.Max(1, state.TotalFiles);
                double percentage = Math.Min(100, state.DeletedFiles * 100.0 / safeTotal);

                FloatFactoryReset.UpdateProgress(
                    percentage,
                    "恢复出厂设置",
                    $"已删除 {state.DeletedFiles}/{safeTotal}",
                    TrimFileName(state.CurrentFileName, 44));
            });

            await Task.Run(() => DeletePythonRuntimeFiles(cancellationToken, progress), cancellationToken);

            if (floatShown)
            {
                FloatFactoryReset.UpdateProgress(100, "恢复出厂设置", "已删除完成", string.Empty);
                FloatFactoryReset.Finish();
                await Task.Delay(FactoryResetFloatFadeOutMs);
            }

            return true;
        }

        private static void DeletePythonRuntimeFiles(CancellationToken cancellationToken, IProgress<FactoryResetDeleteProgress> progress)
        {
            string root = AppConsts.PyOcrRuntimeDir;
            if (!Directory.Exists(root))
                return;

            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            int totalFiles = Math.Max(1, files.Length);
            int deletedFiles = 0;

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch
                {
                    // Ignore locked files here; reset batch will retry after app exits.
                }

                deletedFiles++;
                progress.Report(new FactoryResetDeleteProgress
                {
                    DeletedFiles = deletedFiles,
                    TotalFiles = totalFiles,
                    CurrentFileName = Path.GetFileName(file)
                });
            }

            var dirs = Directory.GetDirectories(root, "*", SearchOption.AllDirectories);
            Array.Sort(dirs, (a, b) => b.Length.CompareTo(a.Length));

            foreach (var dir in dirs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try { Directory.Delete(dir, false); } catch { }
            }

            try { Directory.Delete(root, false); } catch { }
        }

        private static string TrimFileName(string? fileName, int maxLength)
        {
            if (string.IsNullOrEmpty(fileName)) return string.Empty;
            if (maxLength < 8 || fileName.Length <= maxLength) return fileName;

            int keep = (maxLength - 3) / 2;
            int tail = maxLength - 3 - keep;
            return fileName.Substring(0, keep) + "..." + fileName.Substring(fileName.Length - tail);
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
