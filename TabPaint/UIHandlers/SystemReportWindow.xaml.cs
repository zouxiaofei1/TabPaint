using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;
using System.Windows;
using TabPaint.Services;

namespace TabPaint.Windows
{
    public partial class SystemReportWindow : Window
    {
        public SystemReportWindow()
        {
            InitializeComponent();
            this.SupportFocusHighlight();
            Loaded += (_, __) => GenerateReport();
        }

        private void GenerateReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================== TabPaint System Report ====================");
            sb.AppendLine($"GeneratedAt: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            AppendSystemInfo(sb);
            AppendProcessInfo(sb);
            AppendTodayLogs(sb);

            ReportTextBox.Text = sb.ToString();
            ReportTextBox.CaretIndex = 0;
            ReportTextBox.ScrollToHome();
        }

        private static void AppendSystemInfo(StringBuilder sb)
        {
            sb.AppendLine("[System Information]");
            sb.AppendLine($"MachineName: {Environment.MachineName}");
            sb.AppendLine($"UserName: {Environment.UserName}");
            sb.AppendLine($"OSDescription: {RuntimeInformation.OSDescription}");
            sb.AppendLine($"OSVersion: {Environment.OSVersion}");
            sb.AppendLine($"Architecture: {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}");
            sb.AppendLine($"ProcessArchitecture: {(Environment.Is64BitProcess ? "x64" : "x86")}");
            sb.AppendLine($"Framework: {RuntimeInformation.FrameworkDescription}");
            sb.AppendLine($"ProcessorCount: {Environment.ProcessorCount}");
            sb.AppendLine($"SystemDirectory: {Environment.SystemDirectory}");

            try
            {
                var current = Process.GetCurrentProcess();
                sb.AppendLine($"CurrentProcess: {current.ProcessName} ({current.Id})");
                sb.AppendLine($"CurrentProcessMemoryMB: {(current.WorkingSet64 / 1024.0 / 1024.0):F1}");
            }
            catch { }

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
            catch { }

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
                    try { lines.Add($"{p.ProcessName} | PID={p.Id} | WS=N/A"); } catch { }
                }
                finally
                {
                    p.Dispose();
                }
            }

            foreach (var line in lines
                .OrderByDescending(x => ParseWorkingSet(x))
                .Take(80))
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

        private static void AppendFilteredLogs(StringBuilder sb, string logFilePath)
        {
            int keptLines = 0;
            foreach (string line in File.ReadLines(logFilePath))
            {
                // 默认过滤 INFO 日志
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

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
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

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Regenerate_Click(object sender, RoutedEventArgs e)
        {
            GenerateReport();
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

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}