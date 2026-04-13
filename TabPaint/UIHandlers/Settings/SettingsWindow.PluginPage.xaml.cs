using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TabPaint.Controls;

namespace TabPaint.Pages
{
    public partial class PluginPage : UserControl
    {
        private CancellationTokenSource _ctsRMBG;
        private CancellationTokenSource _ctsSR;
        private CancellationTokenSource _ctsInpaint;
        private CancellationTokenSource _ctsOcrRuntime;
        private CancellationTokenSource _ctsZXing;
        private CancellationTokenSource _ctsPSD;
        private ScrollViewer? _pluginScrollViewer;
        private int _ocrRuntimeStatusRequestId;
        private int _zxingStatusRequestId;
        private int _psdStatusRequestId;
        private int _rmbgInstallRequestId;
        private int _srInstallRequestId;
        private int _inpaintInstallRequestId;
        private int _ocrRuntimeInstallRequestId;
        private int _zxingInstallRequestId;
        private int _psdInstallRequestId;
        private int _installAllRequestId;
        private readonly PythonRuntimeManager _pythonRuntimeManager = new PythonRuntimeManager();

        public PluginPage()
        {
            InitializeComponent();
            this.Tag = "Plugins";
            this.Loaded += PluginPage_Loaded;
            this.Unloaded += PluginPage_Unloaded;
            InitializeComboBoxes();
            UpdateRmbgWarningVisibility();

            // 订阅取消事件
            FloatRMBG.CancelRequested += (s, e) => _ctsRMBG?.Cancel();
            FloatSR.CancelRequested += (s, e) => _ctsSR?.Cancel();
            FloatInpaint.CancelRequested += (s, e) => _ctsInpaint?.Cancel();
            FloatOcrRuntime.CancelRequested += (s, e) => _ctsOcrRuntime?.Cancel();
            FloatZXing.CancelRequested += (s, e) => _ctsZXing?.Cancel();
            FloatPSD.CancelRequested += (s, e) => _ctsPSD?.Cancel();
        }

        private void InitializeComboBoxes()
        {
            var currentModel = SettingsManager.Instance.Current.RmbgModel;
            foreach (ComboBoxItem item in ComboRmbgModel.Items)
            {
                if (item.Tag?.ToString() == currentModel.ToString())
                {
                    ComboRmbgModel.SelectedItem = item;
                    break;
                }
            }
        }

        private void ComboRmbgModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboRmbgModel.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                if (Enum.TryParse<RmbgModelType>(item.Tag.ToString(), out var modelType))
                {
                    if (SettingsManager.Instance.Current.RmbgModel != modelType)
                    {
                        // 切换模型时卸载旧模型
                        AiService.Instance.ReleaseModel(AiService.AiTaskType.RemoveBackground);

                        SettingsManager.Instance.Current.RmbgModel = modelType;
                        SettingsManager.Instance.Save();
                        _ = UpdateAllStatusesAsync();
                        UpdateRmbgWarningVisibility();
                    }
                }
            }
        }

        private void UpdateRmbgWarningVisibility()
        {
            if (TxtRmbgLowEndWarning == null) return;

            bool isRmbg20 = SettingsManager.Instance.Current.RmbgModel == RmbgModelType.Rmbg20;
            bool isLowEnd = AiService.IsLowEndHardware();

            TxtRmbgLowEndWarning.Visibility = (isRmbg20 && isLowEnd) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Grid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            UpdatePinState();
        }

        private void UpdatePinState()
        {
            var win = Window.GetWindow(this) as SettingsWindow;
            if (win != null)
            {
                bool isPinned = win.IsPagePinned("Plugins");
                BtnPinTop.IsChecked = isPinned;
                TxtPin.Text = isPinned ? LocalizationManager.GetString("L_Settings_UnpinPage") : LocalizationManager.GetString("L_Settings_PinPage");
                BtnPinTop.ToolTip = TxtPin.Text;

                string pinData = isPinned
                    ? "M2,5.27L3.28,4L20,20.72L18.73,22L12.8,16.07V22H11.2V16H6V14L8,12V11.27L2,5.27M16,12L18,14V16H16.17L12.8,12.63V4H14V2H7V4H8V11.17L6.11,9.28L7.27,8.12L16,16.85V12H16Z"
                    : "M16,12V4H17V2H7V4H8V12L6,14V16H11.2V22H12.8V16H18V14L16,12M8.8,14L10,12.8V4H14V12.8L15.2,14H8.8Z";

                PinIcon.Data = Geometry.Parse(pinData);
                PinIconTop.Data = Geometry.Parse(pinData);
            }
        }

        private void MenuPin_Click(object sender, RoutedEventArgs e)
        {
            var win = Window.GetWindow(this) as SettingsWindow;
            win?.TogglePinPage("Plugins");
            UpdatePinState();
        }

        private void PluginPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Interlocked.Increment(ref _ocrRuntimeStatusRequestId);
            Interlocked.Increment(ref _zxingStatusRequestId);
            Interlocked.Increment(ref _rmbgInstallRequestId);
            Interlocked.Increment(ref _srInstallRequestId);
            Interlocked.Increment(ref _inpaintInstallRequestId);
            Interlocked.Increment(ref _ocrRuntimeInstallRequestId);
            Interlocked.Increment(ref _zxingInstallRequestId);
            Interlocked.Increment(ref _psdInstallRequestId);
            Interlocked.Increment(ref _installAllRequestId);
            _ctsRMBG?.Cancel();
            _ctsSR?.Cancel();
            _ctsInpaint?.Cancel();
            _ctsOcrRuntime?.Cancel();
            _ctsZXing?.Cancel();
            _ctsPSD?.Cancel();
        }

        private async void PluginPage_Loaded(object sender, RoutedEventArgs e)
        {
            _pluginScrollViewer = FindName("PluginScrollViewer") as ScrollViewer;
            UpdatePinState();
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
            UpdateZXingStatus();
            UpdatePSDStatus();
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
            if (TabPaint.Services.QrCodeDecoder.IsZXingAvailable()) installedCount++;
            if (TabPaint.Services.PsdPluginHelper.IsPsdPluginInstalled()) installedCount++;

            installAllButton.Content = string.Format(
                LocalizationManager.GetString("L_Settings_Plugins_InstallAllWithProgress"),
                installedCount);
            installAllButton.Visibility = installedCount >= 6 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void UpdateZXingStatus()
        {
            int requestId = Interlocked.Increment(ref _zxingStatusRequestId);
            bool installed = TabPaint.Services.QrCodeDecoder.IsZXingAvailable();
            if (installed)
            {
                TxtStatusZXing.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_Installed");
                TxtStatusZXing.Foreground = (System.Windows.Media.Brush)FindResource("SystemAccentBrush");
                BtnInstallZXing.Visibility = Visibility.Collapsed;
                BtnInstallZXing.IsEnabled = true;
                BtnUninstallZXing.Visibility = Visibility.Visible;

                try
                {
                    string dllPath = Path.Combine(AppConsts.PluginsDir, AppConsts.ZXing_DllName);
                    if (File.Exists(dllPath))
                    {
                        long sizeBytes = new FileInfo(dllPath).Length;
                        string sizeText = FormatSize(sizeBytes);
                        TxtStatusZXing.Text = string.Format(LocalizationManager.GetString("L_Settings_Plugins_Status_InstalledWithSize"), sizeText);
                    }
                }
                catch { }
            }
            else
            {
                TxtStatusZXing.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_NotInstalled");
                TxtStatusZXing.Foreground = (System.Windows.Media.Brush)FindResource("TextTertiaryBrush");
                BtnInstallZXing.Visibility = Visibility.Visible;
                BtnInstallZXing.IsEnabled = true;
                BtnUninstallZXing.Visibility = Visibility.Collapsed;
            }
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
            string modelName = string.Empty;
            switch (type)
            {
                case AiService.AiTaskType.RemoveBackground:
                    var modelType = SettingsManager.Instance.Current.RmbgModel;
                    modelName = modelType == RmbgModelType.Rmbg20 ? AppConsts.BgRem20_ModelName : AppConsts.BgRem14_ModelName;
                    break;
                case AiService.AiTaskType.SuperResolution:
                    modelName = AppConsts.Sr_ModelName;
                    break;
                case AiService.AiTaskType.Inpainting:
                    modelName = AppConsts.Inpaint_ModelName;
                    break;
            }

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
                AppConsts.BgRem14_ModelName,
                AppConsts.BgRem20_ModelName,
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

                if (!TabPaint.Services.QrCodeDecoder.IsZXingAvailable())
                {
                    _ctsZXing?.Cancel();
                    _ctsZXing = new CancellationTokenSource();
                    int modelRequestId = Interlocked.Increment(ref _zxingInstallRequestId);
                    await DownloadAndExtractZXingAsync(modelRequestId, _ctsZXing.Token);
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

        private async void InstallZXing_Click(object sender, RoutedEventArgs e)
        {
            _ctsZXing?.Cancel();
            _ctsZXing = new CancellationTokenSource();
            int requestId = Interlocked.Increment(ref _zxingInstallRequestId);
            await DownloadAndExtractZXingAsync(requestId, _ctsZXing.Token);
        }

        private async Task DownloadAndExtractZXingAsync(int requestId, CancellationToken token)
        {
            BtnInstallZXing.IsEnabled = false;
            TxtStatusZXing.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_Downloading");

            try
            {
                if (!Directory.Exists(AppConsts.PluginsDir)) Directory.CreateDirectory(AppConsts.PluginsDir);

                string nupkgPath = Path.Combine(AppConsts.PluginsDir, "zxing.nupkg");
                string dllPath = Path.Combine(AppConsts.PluginsDir, AppConsts.ZXing_DllName);

                using (var client = new System.Net.Http.HttpClient())
                {
                    var progressReporter = new Progress<AiDownloadStatus>(status =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            FloatZXing.UpdateProgress(status.Percentage,
                                LocalizationManager.GetString("L_Settings_Plugins_ZXing_Title"),
                                FormatSize((long)status.BytesReceived),
                                status.TotalBytes > 0 ? FormatSize(status.TotalBytes) : "");
                        });
                    });

                    string downloadUrl = AppConsts.ZXing_DownloadUrl;
                    string lang = CultureInfo.CurrentUICulture.Name;
                    if (lang == "zh-CN" || lang == "zh-TW" || lang == "zh-HK")
                    {
                        downloadUrl = AppConsts.ZXing_DownloadUrl_Mirror;
                    }

                    using (var response = await client.GetAsync(downloadUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, token))
                    {
                        response.EnsureSuccessStatusCode();
                        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                        using (var contentStream = await response.Content.ReadAsStreamAsync(token))
                        using (var fileStream = new FileStream(nupkgPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            var totalRead = 0L;
                            var buffer = new byte[8192];
                            var isMoreToRead = true;
                            do
                            {
                                token.ThrowIfCancellationRequested();
                                var read = await contentStream.ReadAsync(buffer, 0, buffer.Length, token);
                                if (read == 0) isMoreToRead = false;
                                else
                                {
                                    await fileStream.WriteAsync(buffer, 0, read, token);
                                    totalRead += read;
                                    ((IProgress<AiDownloadStatus>)progressReporter).Report(new AiDownloadStatus
                                    {
                                        BytesReceived = totalRead,
                                        TotalBytes = totalBytes,
                                        Percentage = totalBytes > 0 ? (double)totalRead / totalBytes * 100 : 0
                                    });
                                }
                            } while (isMoreToRead);
                        }
                    }
                }

                // 解压 zxing.dll
                await Task.Run(() =>
                {
                    using (var archive = System.IO.Compression.ZipFile.OpenRead(nupkgPath))
                    {
                        // 寻找最合适的 DLL
                        var entry = archive.Entries.FirstOrDefault(e => e.FullName.Equals("lib/net8.0-windows7.0/zxing.dll", StringComparison.OrdinalIgnoreCase))
                                 ?? archive.Entries.FirstOrDefault(e => e.FullName.Equals("lib/net6.0/zxing.dll", StringComparison.OrdinalIgnoreCase))
                                 ?? archive.Entries.FirstOrDefault(e => e.FullName.Equals("lib/netstandard2.0/zxing.dll", StringComparison.OrdinalIgnoreCase));

                        if (entry != null)
                        {
                            if (File.Exists(dllPath)) File.Delete(dllPath);
                            entry.ExtractToFile(dllPath);
                           // entry.ExtractToFile
                        }
                        else
                        {
                            throw new Exception("Could not find zxing.dll in package");
                        }
                    }
                    if (File.Exists(nupkgPath)) File.Delete(nupkgPath);
                }, token);

                FloatZXing.Finish();
            }
            catch (OperationCanceledException)
            {
                if (requestId != Volatile.Read(ref _zxingInstallRequestId) || !IsLoaded) return;
                FloatZXing.Finish();
                TxtStatusZXing.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_NotInstalled");
                BtnInstallZXing.IsEnabled = true;
            }
            catch (Exception ex)
            {
                if (requestId != Volatile.Read(ref _zxingInstallRequestId) || !IsLoaded) return;
                FloatZXing.Finish();
                TxtStatusZXing.Text = "Error: " + ex.Message;
                BtnInstallZXing.IsEnabled = true;
            }
            finally
            {
                if (requestId == Volatile.Read(ref _zxingInstallRequestId) && IsLoaded)
                {
                    TabPaint.Services.QrCodeDecoder.ResetAvailability();
                    await UpdateAllStatusesAsync();
                }
            }
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

        private void UninstallZXing_Click(object sender, RoutedEventArgs e)
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
                TabPaint.Services.QrCodeDecoder.ResetAvailability();

                string dllPath = Path.Combine(AppConsts.PluginsDir, AppConsts.ZXing_DllName);
                if (File.Exists(dllPath))
                    File.Delete(dllPath);

                UpdateZXingStatus();
                UpdateInstallAllQuickActionState();
            }
            catch (Exception ex)
            {
                FluentMessageBox.Show("Uninstall failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error, Window.GetWindow(this));
            }
        }

        private void UninstallOcrRuntime_Click(object sender, RoutedEventArgs e)
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
                _ = UpdateAllStatusesAsync();
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

                string modelName = string.Empty;
                switch (type)
                {
                    case AiService.AiTaskType.RemoveBackground:
                        var modelType = SettingsManager.Instance.Current.RmbgModel;
                        modelName = modelType == RmbgModelType.Rmbg20 ? AppConsts.BgRem20_ModelName : AppConsts.BgRem14_ModelName;
                        break;
                    case AiService.AiTaskType.SuperResolution:
                        modelName = AppConsts.Sr_ModelName;
                        break;
                    case AiService.AiTaskType.Inpainting:
                        modelName = AppConsts.Inpaint_ModelName;
                        break;
                }

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

        private void UpdatePSDStatus()
        {
            int requestId = Interlocked.Increment(ref _psdStatusRequestId);
            bool installed = TabPaint.Services.PsdPluginHelper.IsPsdPluginInstalled();
            if (installed)
            {
                TxtStatusPSD.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_Installed");
                TxtStatusPSD.Foreground = (System.Windows.Media.Brush)FindResource("SystemAccentBrush");
                BtnInstallPSD.Visibility = Visibility.Collapsed;
                BtnInstallPSD.IsEnabled = true;
                BtnUninstallPSD.Visibility = Visibility.Visible;

                try
                {
                    long totalBytes = 0;
                    string[] dllNames = { AppConsts.Magick_DllName, AppConsts.MagickCore_DllName, AppConsts.MagickNative_DllName };
                    foreach (var name in dllNames)
                    {
                        string p = Path.Combine(AppConsts.PluginsDir, name);
                        if (File.Exists(p)) totalBytes += new FileInfo(p).Length;
                    }

                    if (totalBytes > 0)
                    {
                        string sizeText = FormatSize(totalBytes);
                        TxtStatusPSD.Text = string.Format(LocalizationManager.GetString("L_Settings_Plugins_Status_InstalledWithSize"), sizeText);
                    }
                }
                catch { }
            }
            else
            {
                TxtStatusPSD.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_NotInstalled");
                TxtStatusPSD.Foreground = (System.Windows.Media.Brush)FindResource("TextTertiaryBrush");
                BtnInstallPSD.Visibility = Visibility.Visible;
                BtnInstallPSD.IsEnabled = true;
                BtnUninstallPSD.Visibility = Visibility.Collapsed;
            }
        }

        private async void InstallPSD_Click(object sender, RoutedEventArgs e)
        {
            _ctsPSD?.Cancel();
            _ctsPSD = new CancellationTokenSource();
            int requestId = Interlocked.Increment(ref _psdInstallRequestId);
            await DownloadAndExtractPSDAsync(requestId, _ctsPSD.Token);
        }

        private async Task DownloadAndExtractPSDAsync(int requestId, CancellationToken token)
        {
            BtnInstallPSD.IsEnabled = false;
            TxtStatusPSD.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_Downloading");

            try
            {
                if (!Directory.Exists(AppConsts.PluginsDir)) Directory.CreateDirectory(AppConsts.PluginsDir);

                async Task DownloadPkg(string url, string mirrorUrl, string savePath, string title)
                {
                    using (var client = new System.Net.Http.HttpClient())
                    {
                        var progressReporter = new Progress<AiDownloadStatus>(status =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                FloatPSD.UpdateProgress(status.Percentage, title,
                                    FormatSize((long)status.BytesReceived),
                                    status.TotalBytes > 0 ? FormatSize(status.TotalBytes) : "");
                            });
                        });

                        string dUrl = (CultureInfo.CurrentUICulture.Name.StartsWith("zh")) ? mirrorUrl : url;
                        using (var response = await client.GetAsync(dUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, token))
                        {
                            response.EnsureSuccessStatusCode();
                            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                            using (var contentStream = await response.Content.ReadAsStreamAsync(token))
                            using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                var totalRead = 0L;
                                var buffer = new byte[8192];
                                var isMoreToRead = true;
                                do
                                {
                                    token.ThrowIfCancellationRequested();
                                    var read = await contentStream.ReadAsync(buffer, 0, buffer.Length, token);
                                    if (read == 0) isMoreToRead = false;
                                    else
                                    {
                                        await fileStream.WriteAsync(buffer, 0, read, token);
                                        totalRead += read;
                                        ((IProgress<AiDownloadStatus>)progressReporter).Report(new AiDownloadStatus
                                        {
                                            BytesReceived = totalRead,
                                            TotalBytes = totalBytes,
                                            Percentage = totalBytes > 0 ? (double)totalRead / totalBytes * 100 : 0
                                        });
                                    }
                                } while (isMoreToRead);
                            }
                        }
                    }
                }

                string pkgMain = Path.Combine(AppConsts.PluginsDir, "magick_main.nupkg");
                string pkgCore = Path.Combine(AppConsts.PluginsDir, "magick_core.nupkg");
                string baseTitle = LocalizationManager.GetString("L_Settings_Plugins_PSD_Title");

                await DownloadPkg(AppConsts.Magick_DownloadUrl, AppConsts.Magick_DownloadUrl_Mirror, pkgMain, baseTitle + " (1/2)");
                await DownloadPkg(AppConsts.MagickCore_DownloadUrl, AppConsts.MagickCore_DownloadUrl_Mirror, pkgCore, baseTitle + " (2/2)");

                await Task.Run(() =>
                {
                    // Extract Main & Native
                    using (var archive = ZipFile.OpenRead(pkgMain))
                    {
                        var entry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("/" + AppConsts.Magick_DllName, StringComparison.OrdinalIgnoreCase))
                                 ?? archive.Entries.FirstOrDefault(e => e.FullName.Contains(AppConsts.Magick_DllName));
                        if (entry != null)
                        {
                            string target = Path.Combine(AppConsts.PluginsDir, AppConsts.Magick_DllName);
                            if (File.Exists(target)) File.Delete(target);
                            entry.ExtractToFile(target);
                        }

                        var nativeEntry = archive.Entries.FirstOrDefault(e => e.FullName.Contains(AppConsts.MagickNative_DllName, StringComparison.OrdinalIgnoreCase));
                        if (nativeEntry != null)
                        {
                            string target = Path.Combine(AppConsts.PluginsDir, AppConsts.MagickNative_DllName);
                            if (File.Exists(target)) File.Delete(target);
                            nativeEntry.ExtractToFile(target);
                        }
                    }

                    // Extract Core
                    using (var archive = ZipFile.OpenRead(pkgCore))
                    {
                        var entry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("/" + AppConsts.MagickCore_DllName, StringComparison.OrdinalIgnoreCase))
                                 ?? archive.Entries.FirstOrDefault(e => e.FullName.Contains(AppConsts.MagickCore_DllName));
                        if (entry != null)
                        {
                            string target = Path.Combine(AppConsts.PluginsDir, AppConsts.MagickCore_DllName);
                            if (File.Exists(target)) File.Delete(target);
                            entry.ExtractToFile(target);
                        }
                    }

                    if (File.Exists(pkgMain)) File.Delete(pkgMain);
                    if (File.Exists(pkgCore)) File.Delete(pkgCore);
                }, token);

                FloatPSD.Finish();
            }
            catch (OperationCanceledException)
            {
                if (requestId != Volatile.Read(ref _psdInstallRequestId) || !IsLoaded) return;
                FloatPSD.Finish();
                TxtStatusPSD.Text = LocalizationManager.GetString("L_Settings_Plugins_Status_NotInstalled");
                BtnInstallPSD.IsEnabled = true;
            }
            catch (Exception ex)
            {
                if (requestId != Volatile.Read(ref _psdInstallRequestId) || !IsLoaded) return;
                FloatPSD.Finish();
                TxtStatusPSD.Text = "Error: " + ex.Message;
                BtnInstallPSD.IsEnabled = true;
            }
            finally
            {
                if (requestId == Volatile.Read(ref _psdInstallRequestId) && IsLoaded)
                {
                    TabPaint.Services.PsdPluginHelper.ResetAvailability();
                    await UpdateAllStatusesAsync();
                }
            }
        }

        private void UninstallPSD_Click(object sender, RoutedEventArgs e)
        {
            var result = FluentMessageBox.Show(
                LocalizationManager.GetString("L_Settings_Plugins_PSD_Uninstall_Confirm"),
                LocalizationManager.GetString("L_Settings_Plugins_Uninstall"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                Window.GetWindow(this));

            if (result != MessageBoxResult.Yes) return;

            try
            {
                TabPaint.Services.PsdPluginHelper.ResetAvailability();

                string[] files = { AppConsts.Magick_DllName, AppConsts.MagickCore_DllName, AppConsts.MagickNative_DllName };
                bool needRestart = false;

                foreach (var f in files)
                {
                    string p = Path.Combine(AppConsts.PluginsDir, f);
                    if (File.Exists(p))
                    {
                        try { File.Delete(p); }
                        catch (IOException)
                        {
                            string del = p + ".delete";
                            if (File.Exists(del)) File.Delete(del);
                            File.Move(p, del);
                            needRestart = true;
                        }
                    }
                }

                if (needRestart)
                {
                    FluentMessageBox.Show(
                        LocalizationManager.GetString("L_Settings_Plugins_Uninstall_NeedRestart"),
                        LocalizationManager.GetString("L_Settings_Title"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information,
                        Window.GetWindow(this));
                }

                UpdatePSDStatus();
                UpdateInstallAllQuickActionState();
            }
            catch (Exception ex)
            {
                FluentMessageBox.Show("Uninstall failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error, Window.GetWindow(this));
            }
        }
    }
}
