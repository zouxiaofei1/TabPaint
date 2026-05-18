using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TabPaint.Services;

namespace TabPaint.Pages
{
    public partial class DevToolsPage : UserControl
    {
        public DevToolsPage()
        {
            InitializeComponent();
            this.Tag = "DevTools";
            this.Loaded += (s, e) => UpdatePinState();
            DataContext = SettingsManager.Instance.Current;

            // 仅在 Win11 下显示 Win10 样式开关
            if (MicaAcrylicManager.IsWin11())
            {
                UseWin10StyleCheckBox.Visibility = Visibility.Visible;
            }
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
                TabPaint.MainWindow.GetCurrentInstance()?.ShowToast(string.Format(LocalizationManager.GetString("L_Common_Error") + ": {0}", ex.Message), ex);
            }
        }

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
            catch (Exception) { }
        }

        private void HashDropZone_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private async void HashDropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string filePath = files[0]; // 只处理第一个文件
                    await CalculateHashesAsync(filePath);
                }
            }
        }

        private async void QrCodeDropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string filePath = files[0];
                    await RecognizeQrCodeAsync(filePath);
                }
            }
        }

        private async void HashDropZone_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                if (files.Count > 0)
                {
                    await CalculateHashesAsync(files[0]);
                }
            }
            else
            {
                TabPaint.MainWindow.GetCurrentInstance()?.ShowToast(LocalizationManager.GetString("L_DevTools_DropHint"));
            }
        }

        private async void QrCodeDropZone_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                if (files.Count > 0)
                {
                    await RecognizeQrCodeAsync(files[0]);
                }
            }
            else if (Clipboard.ContainsImage())
            {
                var bitmap = Clipboard.GetImage();
                if (bitmap != null)
                {
                    await RecognizeQrCodeAsync(bitmap);
                }
            }
            else
            {
                TabPaint.MainWindow.GetCurrentInstance()?.ShowToast(LocalizationManager.GetString("L_DevTools_QrCode_DropHint"));
            }
        }

        private async Task RecognizeQrCodeAsync(System.Windows.Media.Imaging.BitmapSource bitmap)
        {
            try
            {
                string resultText = await Task.Run(() => QrCodeDecoder.Decode(bitmap));
                ShowQrCodeResult(resultText);
            }
            catch (Exception ex)
            {
                TabPaint.MainWindow.GetCurrentInstance()?.ShowToast(ex.Message, ex);
            }
        }

        private void ShowQrCodeResult(string resultText)
        {
            if (string.IsNullOrEmpty(resultText))
            {
                TabPaint.MainWindow.GetCurrentInstance()?.ShowToast(
                    LocalizationManager.GetString("L_Common_Error"));
                return;
            }

            var result = FluentMessageBox.Show(
                resultText + " " + LocalizationManager.GetString("L_DevTools_CopyHashPrompt"),
                LocalizationManager.GetString("L_DevTools_QrCode_Title"),
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information
            );

            if (result == MessageBoxResult.OK)
            {
                Clipboard.SetText(resultText);
                TabPaint.MainWindow.GetCurrentInstance()?.ShowToast(
                    LocalizationManager.GetString("L_Toast_Copied"));
            }
        }

        private async Task RecognizeQrCodeAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return;

                string resultText = await Task.Run(() => QrCodeDecoder.Decode(filePath));
                ShowQrCodeResult(resultText);
            }
            catch (Exception ex)
            {
                TabPaint.MainWindow.GetCurrentInstance()?.ShowToast(ex.Message, ex);
            }
        }

        private async Task CalculateHashesAsync(string filePath)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists) return;
                string md5 = await Task.Run(() => ComputeHash(filePath, MD5.Create()));
                string resultText = $"MD5: {md5}";
                // MessageBox 显示结果，点击"确定"复制到剪贴板，点击"取消"则不复制
                var result = FluentMessageBox.Show(
                    resultText + " " + LocalizationManager.GetString("L_DevTools_CopyHashPrompt"),
                    "MD5 Hash",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Information
                );
                if (result == MessageBoxResult.OK)
                {
                    Clipboard.SetText(md5);
                    TabPaint.MainWindow.GetCurrentInstance()?.ShowToast(
                        LocalizationManager.GetString("L_Toast_Copied"));
                }
            }
            catch (Exception ex)
            {
             
            }
        }

        private void CopyHash_Click(object sender, RoutedEventArgs e)
        {
            try
            {
               // Clipboard.SetText(TxtHashResult.Text);
                TabPaint.MainWindow.GetCurrentInstance()?.ShowToast(LocalizationManager.GetString("L_Toast_Copied"));
            }
            catch (Exception ex)
            {
                TabPaint.MainWindow.GetCurrentInstance()?.ShowToast(string.Format(LocalizationManager.GetString("L_Common_Error") + ": {0}", ex.Message), ex);
            }
        }

        private string ComputeHash(string filePath, HashAlgorithm algorithm)
        {
            using (algorithm)
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hashBytes = algorithm.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
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
                bool isPinned = win.IsPagePinned("DevTools");
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
            win?.TogglePinPage("DevTools");
            UpdatePinState();
        }

        private string FormatFileSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unitIndex = 0;
            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }
            return $"{size:F2} {units[unitIndex]} ({bytes} bytes)";
        }

        private void NumberValidationTextBox(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9.]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (double.TryParse(textBox.Text, out double value))
                {
                    if (value < 0) value = 0;
                    if (value > 4) value = 4;
                    textBox.Text = value.ToString("0.0");
                }
                else
                {
                    textBox.Text = "1.0";
                }
            }
        }
    }
}
