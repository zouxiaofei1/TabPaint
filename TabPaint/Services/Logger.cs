using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace TabPaint.Services
{
    public static class Logger
    {
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TabPaint", "Logs");

        private static readonly object _lock = new object();

        static Logger()
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }
            }
            catch { }
        }

        public static void Info(string message)
        {
            Log("INFO", message);
        }

        public static void Error(string message, Exception ex = null)
        {
            Log("ERROR", FormatException(message, ex));
        }

        public static void Fatal(string message, Exception ex = null)
        {
            Log("FATAL", FormatException(message, ex));
        }

        private static string FormatException(string message, Exception ex)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(message);
            if (ex != null)
            {
                sb.AppendLine($"Exception Type: {ex.GetType().FullName}");
                sb.AppendLine($"Exception Message: {ex.Message}");
                sb.AppendLine("Stack Trace:");
                sb.AppendLine(ex.StackTrace);
                if (ex.InnerException != null)
                {
                    sb.AppendLine("Inner Exception:");
                    sb.AppendLine(ex.InnerException.Message);
                    sb.AppendLine(ex.InnerException.StackTrace);
                }
            }
            return sb.ToString();
        }

        private static void Log(string level, string message)
        {
            Task.Run(() =>
            {
                lock (_lock)
                {
                    try
                    {
                        string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
                        string fileName = $"Log_{dateStr}.txt";
                        string fullPath = Path.Combine(LogDirectory, fileName);

                        string logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}{new string('-', 30)}{Environment.NewLine}";
                        File.AppendAllText(fullPath, logEntry, Encoding.UTF8);

                        // 清理旧日志（超过 30 天）
                        CleanupOldLogs();
                    }
                    catch
                    {
                        // 忽略日志写入错误
                    }
                }
            });
        }

        private static void CleanupOldLogs()
        {
            try
            {
                var files = Directory.GetFiles(LogDirectory, "Log_*.txt");
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < DateTime.Now.AddDays(-30))
                    {
                        fileInfo.Delete();
                    }
                }
            }
            catch { }
        }

        public static string GetLogDirectory() => LogDirectory;
    }
}
