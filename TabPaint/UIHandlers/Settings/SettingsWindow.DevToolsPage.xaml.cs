using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace TabPaint.Pages
{
    public partial class DevToolsPage : UserControl
    {
        public DevToolsPage()
        {
            InitializeComponent();
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
                    resultText + "\n\n" + LocalizationManager.GetString("L_DevTools_CopyHashPrompt"),
             
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
    }
}
