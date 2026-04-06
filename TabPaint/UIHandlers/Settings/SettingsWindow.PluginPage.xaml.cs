using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using TabPaint.Controls;

namespace TabPaint.Pages
{
    public partial class PluginPage : UserControl
    {
        private CancellationTokenSource _ctsRMBG;
        private CancellationTokenSource _ctsSR;
        private CancellationTokenSource _ctsInpaint;
        private CancellationTokenSource _ctsOcrRuntime;
        private ScrollViewer? _pluginScrollViewer;
        private int _ocrRuntimeStatusRequestId;
        private int _rmbgInstallRequestId;
        private int _srInstallRequestId;
        private int _inpaintInstallRequestId;
        private int _ocrRuntimeInstallRequestId;
        private int _installAllRequestId;
        private readonly PythonRuntimeManager _pythonRuntimeManager = new PythonRuntimeManager();

        public PluginPage()
        {
            InitializeComponent();
            this.Loaded += PluginPage_Loaded;
            this.Unloaded += PluginPage_Unloaded;

            // 订阅取消事件
            FloatRMBG.CancelRequested += (s, e) => _ctsRMBG?.Cancel();
            FloatSR.CancelRequested += (s, e) => _ctsSR?.Cancel();
            FloatInpaint.CancelRequested += (s, e) => _ctsInpaint?.Cancel();
            FloatOcrRuntime.CancelRequested += (s, e) => _ctsOcrRuntime?.Cancel();
        }

        private void PluginPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Interlocked.Increment(ref _ocrRuntimeStatusRequestId);
            Interlocked.Increment(ref _rmbgInstallRequestId);
            Interlocked.Increment(ref _srInstallRequestId);
            Interlocked.Increment(ref _inpaintInstallRequestId);
            Interlocked.Increment(ref _ocrRuntimeInstallRequestId);
            Interlocked.Increment(ref _installAllRequestId);
            _ctsRMBG?.Cancel();
            _ctsSR?.Cancel();
            _ctsInpaint?.Cancel();
            _ctsOcrRuntime?.Cancel();
        }

        private async void PluginPage_Loaded(object sender, RoutedEventArgs e)
        {
            _pluginScrollViewer = FindName("PluginScrollViewer") as ScrollViewer;
            await UpdateAllStatusesAsync();
        }

        private async Task UpdateAllStatusesAsync()
        {
            var aiService = AiService.Instance;

            UpdateStatus(aiService.IsModelReady(AiService.AiTaskType.RemoveBackground),
                         AiService.AiTaskType.RemoveBackground,
                         TxtStatusRMBG, BtnInstallRMBG, BtnUninstallRMBG);
            UpdateStatus(aiService.IsModelReady(AiService.AiTaskType.SuperResolution),
                         AiService.AiTaskType.SuperResolution,
                         TxtStatusSR, BtnInstallSR, BtnUninstallSR);
            UpdateStatus(aiService.IsModelReady(AiService.AiTaskType.Inpainting),
                         AiService.AiTaskType.Inpainting,
                         TxtStatusInpaint, BtnInstallInpaint, BtnUninstallInpaint);

            await UpdateOcrRuntimeStatusAsync();
            UpdateInstallAllQuickActionState();
        }

        private void UpdateInstallAllQuickActionState()
        {
            var installAllButton = FindInstallAllButton();
            if (installAllButton == null)
                return;

            int installedCount = 0;
            if (AiService.Instance.IsModelReady(AiService.AiTaskType.RemoveBackground)) installedCount++;
            if (AiService.Instance.IsModelReady(AiService.AiTaskType.SuperResolution)) installedCount++;
            if (AiService.Instance.IsModelReady(AiService.AiTaskType.Inpainting)) installedCount++;
            if (PythonRuntimeManager.IsRuntimeInstalled()) installedCount++;

            installAllButton.Content = string.Format(
                LocalizationManager.GetString("L_Settings_Plugins_InstallAllWithProgress"),
                installedCount);
            installAllButton.Visibility = installedCount >= 4 ? Visibility.Collapsed : Visibility.Visible;
        }

        private async Task UpdateOcrRuntimeStatusAsync()
        {
            int requestId = Interlocked.Increment(ref _ocrRuntimeStatusRequestId);
            bool installed = PythonRuntimeManager.IsRuntimeInstalled();
            if (installed)
            {
                TxtStatusOcrRuntime.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_Installed");
                TxtStatusOcrRuntime.Foreground = (System.Windows.Media.Brush)FindResource("SystemAccentBrush");
                BtnInstallOcrRuntime.Visibility = Visibility.Collapsed;
                BtnInstallOcrRuntime.IsEnabled = true;
                BtnUninstallOcrRuntime.Visibility = Visibility.Visible;

                long sizeBytes = await Task.Run(PythonRuntimeManager.GetInstalledSizeBytes);
                if (requestId != Volatile.Read(ref _ocrRuntimeStatusRequestId) || !IsLoaded)
                    return;

                string sizeText = FormatSize(sizeBytes);
                TxtStatusOcrRuntime.Text = string.Format(LocalizationManager.GetString("L_Settings_Plugins_Status_InstalledWithSize"), sizeText);
            }
            else
            {
                TxtStatusOcrRuntime.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_NotInstalled");
                TxtStatusOcrRuntime.Foreground = (System.Windows.Media.Brush)FindResource("TextTertiaryBrush");
                BtnInstallOcrRuntime.Visibility = Visibility.Visible;
                BtnInstallOcrRuntime.IsEnabled = true;
                BtnUninstallOcrRuntime.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateStatus(bool isReady, AiService.AiTaskType type, TextBlock txt, Button btnInstall, Button btnUninstall)
        {
            if (isReady)
            {
                string sizeText = GetInstalledModelSizeText(type);
                txt.Text = string.Format(LocalizationManager.GetString("L_Settings_Plugins_Status_InstalledWithSize"), sizeText);
                txt.Foreground = (System.Windows.Media.Brush)FindResource("SystemAccentBrush");
                btnInstall.Visibility = Visibility.Collapsed;
                btnInstall.IsEnabled = true;
                btnUninstall.Visibility = Visibility.Visible;
            }
            else
            {
                txt.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_NotInstalled");
                txt.Foreground = (System.Windows.Media.Brush)FindResource("TextTertiaryBrush");
                btnInstall.Visibility = Visibility.Visible;
                btnInstall.IsEnabled = true;
                btnUninstall.Visibility = Visibility.Collapsed;
            }
        }

        private static string GetInstalledModelSizeText(AiService.AiTaskType type)
        {
            string modelName = type switch
            {
                AiService.AiTaskType.RemoveBackground => AppConsts.BgRem_ModelName,
                AiService.AiTaskType.SuperResolution => AppConsts.Sr_ModelName,
                AiService.AiTaskType.Inpainting => AppConsts.Inpaint_ModelName,
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(modelName)) return "0 B";

            string modelPath = Path.Combine(GetCurrentAiModelDir(), modelName);
            if (!File.Exists(modelPath)) return "0 B";

            long bytes = new FileInfo(modelPath).Length;
            return FormatSize(bytes);
        }

        private static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = Math.Max(0, bytes);
            int unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return unitIndex == 0 ? $"{size:0} {units[unitIndex]}" : $"{size:0.##} {units[unitIndex]}";
        }

        private static string GetCurrentAiModelDir()
        {
            try
            {
                string configured = SettingsManager.Instance.Current?.AiModelDefaultSaveDir ?? AppConsts.AiModelDefaultSaveDir;
                if (string.IsNullOrWhiteSpace(configured))
                {
                    configured = AppConsts.AiModelDefaultSaveDir;
                }

                string normalized = Path.GetFullPath(configured);
                if (!Directory.Exists(normalized))
                {
                    Directory.CreateDirectory(normalized);
                }

                return normalized;
            }
            catch
            {
                if (!Directory.Exists(AppConsts.AiModelDefaultSaveDir))
                {
                    Directory.CreateDirectory(AppConsts.AiModelDefaultSaveDir);
                }
                return AppConsts.AiModelDefaultSaveDir;
            }
        }

        private static string[] GetManagedAiModelNames()
        {
            return new[]
            {
                AppConsts.BgRem_ModelName,
                AppConsts.Sr_ModelName,
                AppConsts.Inpaint_ModelName
            };
        }

        private Button? FindInstallAllButton()
        {
            return FindName("BtnInstallAllPlugins") as Button;
        }

        private async void BrowseAiModelSaveDir_Click(object sender, RoutedEventArgs e)
        {
            string oldDir = GetCurrentAiModelDir();

            var dialog = new OpenFolderDialog
            {
                Title = LocalizationManager.GetString("L_Settings_Plugins_ModelDir_Browse"),
                InitialDirectory = oldDir,
                Multiselect = false
            };

            bool? result = dialog.ShowDialog(Window.GetWindow(this));
            if (result != true)
            {
                return;
            }

            string newDir;
            try
            {
                newDir = Path.GetFullPath(dialog.FolderName ?? string.Empty);
            }
            catch
            {
                return;
            }

            if (string.Equals(oldDir, newDir, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            bool migrationOk = await MigrateAiModelsAsync(oldDir, newDir);
            if (!migrationOk)
            {
                return;
            }

            var settings = SettingsManager.Instance.Current;
            settings.AiModelDefaultSaveDir = newDir;
            SettingsManager.Instance.Save();

            await UpdateAllStatusesAsync();
        }

        private async Task<bool> MigrateAiModelsAsync(string oldDir, string newDir)
        {
            string[] modelNames = GetManagedAiModelNames();
            int total = modelNames.Length;
            int current = 0;

            try
            {
                if (!Directory.Exists(newDir))
                {
                    Directory.CreateDirectory(newDir);
                }

                // 防止模型文件被会话占用
                AiService.Instance.ReleaseAllModels();

                foreach (string modelName in modelNames)
                {
                    current++;

                    string src = Path.Combine(oldDir, modelName);
                    string dest = Path.Combine(newDir, modelName);

                    if (!File.Exists(src))
                    {
                        FloatModelMigration.UpdateProgress(
                            (double)current / total * 100,
                            LocalizationManager.GetString("L_Settings_Plugins_ModelDir_Migrating"),
                            string.Format(LocalizationManager.GetString("L_Settings_Plugins_ModelDir_Migrating_Left"), current, total),
                            modelName);
                        continue;
                    }

                    await Task.Run(() =>
                    {
                        if (File.Exists(dest))
                        {
                            File.Delete(dest);
                        }
                        File.Move(src, dest);
                    });

                    FloatModelMigration.UpdateProgress(
                        (double)current / total * 100,
                        LocalizationManager.GetString("L_Settings_Plugins_ModelDir_Migrating"),
                        string.Format(LocalizationManager.GetString("L_Settings_Plugins_ModelDir_Migrating_Left"), current, total),
                        modelName);
                }

                FloatModelMigration.Finish();
                return true;
            }
            catch (Exception ex)
            {
                FloatModelMigration.Finish();
                FluentMessageBox.Show(
                    string.Format(LocalizationManager.GetString("L_Settings_Plugins_ModelDir_MigrateFailed"), ex.Message),
                    LocalizationManager.GetString("L_Settings_Title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error,
                    Window.GetWindow(this));
                return false;
            }
        }

        private async void InstallRMBG_Click(object sender, RoutedEventArgs e)
        {
            _ctsRMBG?.Cancel();
            _ctsRMBG = new CancellationTokenSource();
            int requestId = Interlocked.Increment(ref _rmbgInstallRequestId);
            await InstallModel(AiService.AiTaskType.RemoveBackground,
                               TxtStatusRMBG, BtnInstallRMBG, FloatRMBG,
                               LocalizationManager.GetString("L_Settings_Plugins_Model_RMBG_Title"),
                               _ctsRMBG,
                               requestId,
                               () => Volatile.Read(ref _rmbgInstallRequestId));
        }

        private async void InstallAllPlugins_Click(object sender, RoutedEventArgs e)
        {
            int requestId = Interlocked.Increment(ref _installAllRequestId);
            var installAllButton = FindInstallAllButton();
            if (installAllButton != null)
            {
                installAllButton.IsEnabled = false;
            }

            try
            {
                if (!AiService.Instance.IsModelReady(AiService.AiTaskType.RemoveBackground))
                {
                    _ctsRMBG?.Cancel();
                    _ctsRMBG = new CancellationTokenSource();
                    int modelRequestId = Interlocked.Increment(ref _rmbgInstallRequestId);
                    await InstallModel(
                        AiService.AiTaskType.RemoveBackground,
                        TxtStatusRMBG,
                        BtnInstallRMBG,
                        FloatRMBG,
                        LocalizationManager.GetString("L_Settings_Plugins_Model_RMBG_Title"),
                        _ctsRMBG,
                        modelRequestId,
                        () => Volatile.Read(ref _rmbgInstallRequestId));
                }

                if (!AiService.Instance.IsModelReady(AiService.AiTaskType.SuperResolution))
                {
                    _ctsSR?.Cancel();
                    _ctsSR = new CancellationTokenSource();
                    int modelRequestId = Interlocked.Increment(ref _srInstallRequestId);
                    await InstallModel(
                        AiService.AiTaskType.SuperResolution,
                        TxtStatusSR,
                        BtnInstallSR,
                        FloatSR,
                        LocalizationManager.GetString("L_Settings_Plugins_Model_SR_Title"),
                        _ctsSR,
                        modelRequestId,
                        () => Volatile.Read(ref _srInstallRequestId));
                }

                if (!AiService.Instance.IsModelReady(AiService.AiTaskType.Inpainting))
                {
                    _ctsInpaint?.Cancel();
                    _ctsInpaint = new CancellationTokenSource();
                    int modelRequestId = Interlocked.Increment(ref _inpaintInstallRequestId);
                    await InstallModel(
                        AiService.AiTaskType.Inpainting,
                        TxtStatusInpaint,
                        BtnInstallInpaint,
                        FloatInpaint,
                        LocalizationManager.GetString("L_Settings_Plugins_Model_Inpaint_Title"),
                        _ctsInpaint,
                        modelRequestId,
                        () => Volatile.Read(ref _inpaintInstallRequestId));
                }

                if (!PythonRuntimeManager.IsRuntimeInstalled())
                {
                    _ctsOcrRuntime?.Cancel();
                    _ctsOcrRuntime = new CancellationTokenSource();
                    int modelRequestId = Interlocked.Increment(ref _ocrRuntimeInstallRequestId);
                    await InstallOcrRuntimeAsync(modelRequestId, _ctsOcrRuntime.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // 允许用户通过页面切换或取消动作中断一键下载流程。
            }
            finally
            {
                if (requestId == Volatile.Read(ref _installAllRequestId) && IsLoaded)
                {
                    await UpdateAllStatusesAsync();
                    if (installAllButton != null)
                    {
                        installAllButton.IsEnabled = true;
                    }
                }
            }
        }

        private async void InstallSR_Click(object sender, RoutedEventArgs e)
        {
            _ctsSR?.Cancel();
            _ctsSR = new CancellationTokenSource();
            int requestId = Interlocked.Increment(ref _srInstallRequestId);
            await InstallModel(AiService.AiTaskType.SuperResolution,
                               TxtStatusSR, BtnInstallSR, FloatSR,
                               LocalizationManager.GetString("L_Settings_Plugins_Model_SR_Title"),
                               _ctsSR,
                               requestId,
                               () => Volatile.Read(ref _srInstallRequestId));
        }

        private async void InstallInpaint_Click(object sender, RoutedEventArgs e)
        {
            _ctsInpaint?.Cancel();
            _ctsInpaint = new CancellationTokenSource();
            int requestId = Interlocked.Increment(ref _inpaintInstallRequestId);
            await InstallModel(AiService.AiTaskType.Inpainting,
                               TxtStatusInpaint, BtnInstallInpaint, FloatInpaint,
                               LocalizationManager.GetString("L_Settings_Plugins_Model_Inpaint_Title"),
                               _ctsInpaint,
                               requestId,
                               () => Volatile.Read(ref _inpaintInstallRequestId));
        }

        private async void InstallOcrRuntime_Click(object sender, RoutedEventArgs e)
        {
            _ctsOcrRuntime?.Cancel();
            _ctsOcrRuntime = new CancellationTokenSource();
            int requestId = Interlocked.Increment(ref _ocrRuntimeInstallRequestId);
            await InstallOcrRuntimeAsync(requestId, _ctsOcrRuntime.Token);
        }

        private async Task InstallOcrRuntimeAsync(int requestId, CancellationToken token)
        {
            BtnInstallOcrRuntime.IsEnabled = false;
            TxtStatusOcrRuntime.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_Downloading");

            try
            {
                var progress = new Progress<PythonRuntimeManager.PyRuntimeProgressStatus>(status =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        string stageTitle = status.Stage switch
                        {
                            PythonRuntimeManager.PyRuntimeStage.DownloadPython => LocalizationManager.GetString("L_PyOCR_Runtime_DownloadPython"),
                            PythonRuntimeManager.PyRuntimeStage.ExtractPython => LocalizationManager.GetString("L_PyOCR_Runtime_ExtractPython"),
                            PythonRuntimeManager.PyRuntimeStage.InstallPip => LocalizationManager.GetString("L_PyOCR_Runtime_InstallPip"),
                            _ => LocalizationManager.GetString("L_PyOCR_Runtime_Preparing")
                        };

                        FloatOcrRuntime.UpdateProgress(status.Percentage, stageTitle, status.LeftText ?? string.Empty, status.RightText ?? string.Empty);
                    });
                });

                await _pythonRuntimeManager.EnsureReadyAsync(progress, token);

                var settings = SettingsManager.Instance.Current;
                if (settings != null)
                {
                    settings.EnableAiOcr = true;
                    settings.AiOcrPromptShown = true;
                    SettingsManager.Instance.Save();
                }

                FloatOcrRuntime.Finish();
            }
            catch (OperationCanceledException)
            {
                if (requestId != Volatile.Read(ref _ocrRuntimeInstallRequestId) || !IsLoaded)
                    return;

                FloatOcrRuntime.Finish();
                TxtStatusOcrRuntime.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_NotInstalled");
                BtnInstallOcrRuntime.IsEnabled = true;
            }
            catch (Exception ex)
            {
                if (requestId != Volatile.Read(ref _ocrRuntimeInstallRequestId) || !IsLoaded)
                    return;

                FloatOcrRuntime.Finish();
                TxtStatusOcrRuntime.Text = "Error: " + ex.Message;
                BtnInstallOcrRuntime.IsEnabled = true;
            }
            finally
            {
                if (requestId == Volatile.Read(ref _ocrRuntimeInstallRequestId) && IsLoaded)
                {
                    await UpdateAllStatusesAsync();
                }
            }
        }
        private async Task InstallModel(
            AiService.AiTaskType type,
            TextBlock txtStatus,
            Button btnInstall,
            TaskProgressFloat floatProgress,
            string taskName,
            CancellationTokenSource cts,
            int requestId,
            Func<int> getLatestRequestId)
        {
            btnInstall.IsEnabled = false;
            txtStatus.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_Downloading");

            try
            {
                var aiService = AiService.Instance;

                var progressReporter = new Progress<AiDownloadStatus>(status =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        floatProgress.UpdateProgress(status, taskName);
                    });
                });

                await aiService.PrepareModelAsync(type, progressReporter, cts.Token);

                // 下载完成 → 淡出进度条
                floatProgress.Finish();
            }
            catch (OperationCanceledException)
            {
                if (requestId != getLatestRequestId() || !IsLoaded)
                    return;

                floatProgress.Finish();
                txtStatus.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_NotInstalled");
                btnInstall.IsEnabled = true;
            }
            catch (Exception ex)
            {
                if (requestId != getLatestRequestId() || !IsLoaded)
                    return;

                floatProgress.Finish();
                txtStatus.Text = "Error: " + ex.Message;
                btnInstall.IsEnabled = true;
            }
            finally
            {
                if (requestId == getLatestRequestId() && IsLoaded)
                {
                    await UpdateAllStatusesAsync();
                }
            }
        }

        private async void UninstallRMBG_Click(object sender, RoutedEventArgs e)
        {
            await UninstallModelAsync(AiService.AiTaskType.RemoveBackground);
        }

        private async void UninstallSR_Click(object sender, RoutedEventArgs e)
        {
            await UninstallModelAsync(AiService.AiTaskType.SuperResolution);
        }

        private async void UninstallInpaint_Click(object sender, RoutedEventArgs e)
        {
            await UninstallModelAsync(AiService.AiTaskType.Inpainting);
        }

        private async void UninstallOcrRuntime_Click(object sender, RoutedEventArgs e)
        {
            var result = FluentMessageBox.Show(
                LocalizationManager.GetString("L_Settings_Plugins_OCR_Uninstall_Confirm"),
                LocalizationManager.GetString("L_Settings_Plugins_Uninstall"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                Window.GetWindow(this));

            if (result != MessageBoxResult.Yes) return;

            try
            {
                OcrService.ReleasePaddleRuntime();
                PythonRuntimeManager.UninstallRuntime();
                await UpdateAllStatusesAsync();
            }
            catch (Exception ex)
            {
                FluentMessageBox.Show("Uninstall failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error, Window.GetWindow(this));
            }
        }

        private async Task UninstallModelAsync(AiService.AiTaskType type)
        {
            var result = FluentMessageBox.Show(
                LocalizationManager.GetString("L_Settings_Plugins_Uninstall_Confirm"),
                LocalizationManager.GetString("L_Settings_Plugins_Uninstall"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                Window.GetWindow(this));

            if (result != MessageBoxResult.Yes) return;

            try
            {
                AiService.Instance.ReleaseModel(type);

                string modelName = type switch
                {
                    AiService.AiTaskType.RemoveBackground => AppConsts.BgRem_ModelName,
                    AiService.AiTaskType.SuperResolution => AppConsts.Sr_ModelName,
                    AiService.AiTaskType.Inpainting => AppConsts.Inpaint_ModelName,
                    _ => ""
                };

                string modelPath = Path.Combine(GetCurrentAiModelDir(), modelName);
                if (File.Exists(modelPath))
                    File.Delete(modelPath);

                await UpdateAllStatusesAsync();
            }
            catch (Exception ex)
            {
                FluentMessageBox.Show("Uninstall failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error, Window.GetWindow(this));
            }
        }

        private void PluginScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var currentScrollViewer = sender as ScrollViewer ?? _pluginScrollViewer;
            if (currentScrollViewer == null)
                return;

            if (TryScroll(currentScrollViewer, e.Delta))
            {
                e.Handled = true;
                return;
            }

            // 内层滚不到时，把滚轮交给父级容器，避免嵌套 ScrollViewer 导致滚轮失效。
            var parentScrollViewer = FindParentScrollViewer(currentScrollViewer);
            if (parentScrollViewer == null)
                return;

            var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = MouseWheelEvent,
                Source = sender
            };

            parentScrollViewer.RaiseEvent(eventArg);
            e.Handled = true;
        }

        private static bool TryScroll(ScrollViewer? scrollViewer, int delta)
        {
            if (scrollViewer == null || scrollViewer.ScrollableHeight <= 0)
                return false;

            double target = Math.Max(0, Math.Min(scrollViewer.ScrollableHeight, scrollViewer.VerticalOffset - delta));
            if (Math.Abs(target - scrollViewer.VerticalOffset) > 0.1)
            {
                scrollViewer.ScrollToVerticalOffset(target);
                return true;
            }

            return false;
        }

        private static ScrollViewer? FindParentScrollViewer(DependencyObject start)
        {
            DependencyObject current = VisualTreeHelper.GetParent(start);
            while (current != null)
            {
                if (current is ScrollViewer parentScrollViewer)
                    return parentScrollViewer;

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
