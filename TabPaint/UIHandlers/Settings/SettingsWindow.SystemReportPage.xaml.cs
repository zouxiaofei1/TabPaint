using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using TabPaint.Services;

namespace TabPaint.Pages
{
    public partial class SystemReportPage : UserControl
    {
        private bool _isGenerating;

        public SystemReportPage()
        {
            InitializeComponent();
            Loaded += async (_, __) => await GenerateReportAsync();
        }

        private async Task GenerateReportAsync()
        {
            if (_isGenerating) return;
            _isGenerating = true;

            ReportTextBox.Text = "Generating system report...";

            string reportText;
            try
            {
                reportText = await Task.Run(BuildReport);
            }
            finally
            {
                _isGenerating = false;
            }

            ReportTextBox.Text = reportText;
            ReportTextBox.CaretIndex = 0;
            ReportTextBox.ScrollToHome();
        }

        private static string BuildReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================== TabPaint System Report ====================");
            sb.AppendLine($"GeneratedAt: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            AppendSystemInfo(sb);
            AppendProcessInfo(sb);
            AppendProgramSettings(sb);
            AppendImageBarInfo(sb);
            AppendTodayLogs(sb);

            return sb.ToString();
        }

        private static void AppendSystemInfo(StringBuilder sb)
        {
            sb.AppendLine("[System Information]");
            sb.AppendLine($"MachineName: {Environment.MachineName}");
            sb.AppendLine($"UserName: {Environment.UserName}");
            sb.AppendLine($"OSDescription: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
            sb.AppendLine($"OSVersion: {Environment.OSVersion}");
            sb.AppendLine($"Architecture: {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}");
            sb.AppendLine($"ProcessArchitecture: {(Environment.Is64BitProcess ? "x64" : "x86")}");
            sb.AppendLine($"Framework: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
            sb.AppendLine($"ProcessorCount: {Environment.ProcessorCount}");
            sb.AppendLine($"SystemDirectory: {Environment.SystemDirectory}");

            try
            {
                var current = Process.GetCurrentProcess();
                sb.AppendLine($"CurrentProcess: {current.ProcessName} ({current.Id})");
                sb.AppendLine($"CurrentProcessMemoryMB: {(current.WorkingSet64 / 1024.0 / 1024.0):F1}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            try
            {
                var systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                var di = new DriveInfo(systemDrive);
                if (di.IsReady)
                {
                    sb.AppendLine($"SystemDrive: {di.Name}");
                    sb.AppendLine($"SystemDriveFreeGB: {(di.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0):F2}");
                    sb.AppendLine($"SystemDriveTotalGB: {(di.TotalSize / 1024.0 / 1024.0 / 1024.0):F2}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            sb.AppendLine();
        }

        private static void AppendProcessInfo(StringBuilder sb)
        {
            sb.AppendLine("[Process Information - Top 80 by Working Set]");
            var lines = new List<string>();

            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    long ws = p.WorkingSet64;
                    lines.Add($"{p.ProcessName} | PID={p.Id} | WS={(ws / 1024.0 / 1024.0):F1}MB");
                }
                catch
                {
                    try { lines.Add($"{p.ProcessName} | PID={p.Id} | WS=N/A"); } catch (Exception ex) { Debug.WriteLine(ex); }
                }
                finally
                {
                    p.Dispose();
                }
            }

            foreach (var line in lines.OrderByDescending(ParseWorkingSet).Take(80))
            {
                sb.AppendLine(line);
            }

            sb.AppendLine();
        }

        private static double ParseWorkingSet(string line)
        {
            const string token = "| WS=";
            int idx = line.IndexOf(token, StringComparison.Ordinal);
            if (idx < 0) return -1;

            string raw = line[(idx + token.Length)..].Replace("MB", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (double.TryParse(raw, out double mb)) return mb;
            return -1;
        }

        private static void AppendTodayLogs(StringBuilder sb)
        {
            sb.AppendLine("[Today Logs]");
            try
            {
                string logDir = Logger.GetLogDirectory();
                string todayFile = Path.Combine(logDir, $"Log_{DateTime.Now:yyyy-MM-dd}.txt");

                sb.AppendLine($"LogDirectory: {logDir}");
                sb.AppendLine($"TodayLogFile: {todayFile}");
                sb.AppendLine(new string('-', 60));

                if (File.Exists(todayFile))
                {
                    AppendFilteredLogs(sb, todayFile);
                }
                else
                {
                    sb.AppendLine("No log file found for today.");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Read log failed: {ex.Message}");
            }

            sb.AppendLine();
        }

        private static void AppendProgramSettings(StringBuilder sb)
        {
            sb.AppendLine("[Program Settings]");
            try
            {
                var settings = SettingsManager.Instance.Current;
                if (settings == null)
                {
                    sb.AppendLine("Settings: N/A");
                    sb.AppendLine();
                    return;
                }

                sb.AppendLine($"Language: {settings.Language}");
                sb.AppendLine($"ThemeMode: {settings.ThemeMode}");
                sb.AppendLine($"ThemeAccentColor: {settings.ThemeAccentColor}");
                sb.AppendLine($"IsImageBarCompact: {settings.IsImageBarCompact}");
                sb.AppendLine($"AlwaysShowTabCloseButton: {settings.AlwaysShowTabCloseButton}");
                sb.AppendLine($"AutoLoadFolderImages: {settings.AutoLoadFolderImages}");
                sb.AppendLine($"StartInViewMode: {settings.StartInViewMode}");
                sb.AppendLine($"ViewMouseWheelMode: {settings.ViewMouseWheelMode}");
                sb.AppendLine($"EnableClipboardMonitor: {settings.EnableClipboardMonitor}");
                sb.AppendLine($"EnableIccColorCorrection: {settings.EnableIccColorCorrection}");
                sb.AppendLine($"ShowRulers: {settings.ShowRulers}");
                sb.AppendLine($"ResamplingMode: {settings.ResamplingMode}");
                sb.AppendLine($"MaxUndoMemoryMB: {settings.MaxUndoMemoryMB}");
                sb.AppendLine($"MaxGlobalUndoSteps: {settings.MaxGlobalUndoSteps}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Read settings failed: {ex.Message}");
            }

            sb.AppendLine();
        }

        private static void AppendImageBarInfo(StringBuilder sb)
        {
            sb.AppendLine("[ImageBar Files]");
            try
            {
                var mw = MainWindow.GetCurrentInstance();
                if (mw == null)
                {
                    sb.AppendLine("MainWindow: N/A");
                    sb.AppendLine();
                    return;
                }

                var tabs = mw.FileTabs?.ToList() ?? new List<MainWindow.FileTabItem>();
                sb.AppendLine($"TabCount: {tabs.Count}");

                if (tabs.Count == 0)
                {
                    sb.AppendLine("No tabs in ImageBar.");
                    sb.AppendLine();
                    return;
                }

                for (int i = 0; i < tabs.Count; i++)
                {
                    var tab = tabs[i];
                    string path = tab.FilePath ?? string.Empty;
                    bool isVirtual = tab.IsNew || string.IsNullOrWhiteSpace(path) || path.StartsWith(AppConsts.VirtualFilePrefix, StringComparison.OrdinalIgnoreCase);

                    string ext = isVirtual ? "virtual" : NormalizeExtension(Path.GetExtension(path));
                    string maskedName = GetMaskedName(path, ext, i + 1, isVirtual);

                    string dimensions = "N/A";
                    if (tab.Thumbnail != null && tab.Thumbnail.PixelWidth > 0 && tab.Thumbnail.PixelHeight > 0)
                    {
                        dimensions = $"{tab.Thumbnail.PixelWidth}x{tab.Thumbnail.PixelHeight}";
                    }

                    long bytes = 0;
                    if (!isVirtual && File.Exists(path))
                    {
                        try
                        {
                            bytes = new FileInfo(path).Length;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);
                        }
                    }

                    sb.AppendLine($"{i + 1:D3}. Name={maskedName} | Format={ext} | Size={dimensions} | Storage={FormatBytes(bytes)} ({bytes} B)");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Read imagebar info failed: {ex.Message}");
            }

            sb.AppendLine();
        }

        private static string NormalizeExtension(string? extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) return "unknown";
            return extension.Trim().TrimStart('.').ToLowerInvariant();
        }

        private static string GetMaskedName(string path, string ext, int index, bool isVirtual)
        {
            if (isVirtual)
            {
                return $"UNTITLED_{index:D3}";
            }

            string hash = ComputeStableHash(path);
            return $"IMG_{index:D3}_{hash}.{ext}";
        }

        private static string ComputeStableHash(string input)
        {
            try
            {
                byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input ?? string.Empty));
                return Convert.ToHexString(bytes)[..8];
            }
            catch
            {
                return "00000000";
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unitIndex = 0;
            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }
            return $"{value:F2} {units[unitIndex]}";
        }

        private static void AppendFilteredLogs(StringBuilder sb, string logFilePath)
        {
            int keptLines = 0;
            foreach (string line in File.ReadLines(logFilePath))
            {
                if (line.IndexOf("[INFO]", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                sb.AppendLine(line);
                keptLines++;
            }

            if (keptLines == 0)
            {
                sb.AppendLine("No non-INFO log entries found for today.");
            }
        }

        private async void Regenerate_Click(object sender, RoutedEventArgs e)
        {
            await GenerateReportAsync();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ClipboardHelper.SetTextWithRetry(ReportTextBox.Text ?? string.Empty);
                MainWindow.GetCurrentInstance()?.ShowToast("L_Toast_Copied");
            }
            catch (Exception ex)
            {
                MainWindow.GetCurrentInstance()?.ShowToast(string.Format(LocalizationManager.GetString("L_Common_Error") + ": {0}", ex.Message), ex);
            }
        }
    }
}