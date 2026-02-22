//
//App.xaml.cs
//应用程序入口点，负责初始化设置、异常处理、单实例检测以及主窗口的启动。
//
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices; // 必须引用，用于置顶窗口
using System.Text;
using System.Windows;
using System.Windows.Threading;
using TabPaint.Services;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public partial class App : Application
    {
        private static bool _isExiting = false;
        public static void GlobalExit()
        {
            if (_isExiting) return;
            _isExiting = true;

            var windows = Application.Current.Windows.Cast<Window>().ToList();
            foreach (Window window in windows)
            {
                if (window is MainWindow mw)
                {
                    mw.OnClosing();
                }
                else
                {
                    try { window.Close(); } catch { }
                }
            }
            Application.Current.Shutdown();
        }
        private static MainWindow _mainWindow;
        private static readonly string LogDirectory = System.IO.Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "TabPaint", "CrashLogs");
        public static class a
        {
            public static void s(params object[] args)
            {
                // 可以根据需要拼接输出格式
                string message = string.Join(" ", args);
                Debug.WriteLine(message);
            }
        }
        private void SetupExceptionHandling()
        {
            this.DispatcherUnhandledException += (s, e) =>
            {
                LogException(e.Exception, "UIThread");
                e.Handled = true; // 设置为 true 可以防止程序直接闪退，但建议视情况决定是否继续运行
                ShutdownAppWithErrorMessage(e.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var exception = e.ExceptionObject as Exception;
                LogException(exception, "AppDomain");
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogException(e.Exception, "TaskScheduler");
                e.SetObserved(); 
            };
        }
        private static void LogException(Exception ex, string source)
        {
            try
            {
                if (ex == null) return;
                Logger.Fatal($"Unhandled crash from {source}", ex);
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string filename = $"Crash_{timestamp}.txt";
                string fullPath = System.IO.Path.Combine(LogDirectory, filename);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Time: {DateTime.Now}");
                sb.AppendLine($"Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
                sb.AppendLine($"Source: {source}");
                sb.AppendLine(new string('-', 50));
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine($"Type: {ex.GetType().FullName}");
                sb.AppendLine(new string('-', 50));
                sb.AppendLine("Stack Trace:");
                sb.AppendLine(ex.StackTrace);
                if (ex.InnerException != null)
                {
                    sb.AppendLine(new string('=', 50));
                    sb.AppendLine("Inner Exception:");
                    sb.AppendLine(ex.InnerException.Message);
                    sb.AppendLine(ex.InnerException.StackTrace);
                }

                File.WriteAllText(fullPath, sb.ToString());
            }
            catch (Exception logEx)
            {
                // 如果写日志都失败了，只能写到调试输出里
                Debug.WriteLine($"Failed to log crash: {logEx.Message}");
            }
        }
        private void ShutdownAppWithErrorMessage(Exception ex)
        {
            string msg = $"TabPaint 遇到错误需要关闭。\n\n错误信息: {ex.Message}\n\n日志已保存至: {LogDirectory}";
            FluentMessageBox.Show(msg, "程序崩溃", MessageBoxButton.OK, MessageBoxImage.Error, null, LogDirectory);

            try
            {
            }
            catch { }

          //  Environment.Exit(1);
        }
        protected override void OnStartup(StartupEventArgs e)
        {//680ms
            var settingsTask = Task.Run(() => SettingsManager.Instance);

           
            try
            { // 启用分级 JIT 编译优化
                string profileRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TabPaint", "Profiles");
                Directory.CreateDirectory(profileRoot);
                System.Runtime.ProfileOptimization.SetProfileRoot(profileRoot);
                System.Runtime.ProfileOptimization.StartProfile("Startup.profile");
            }//4ms
            catch { }
 
            SetupExceptionHandling();//检查单实例0.9ms
            if (!SingleInstance.IsFirstInstance())//0.3ms
            {
                SingleInstance.SendArgsToFirstInstance(e.Args);
                Environment.Exit(0);
                return;
            } 
          
            _ = Task.Run(() =>//创建线程池，10ms
            {
                SingleInstance.ListenForArgs((filePath) =>
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var (existingWindow, existingTab) = TabPaint.MainWindow.FindWindowHostingFile(filePath);
                    if (existingWindow != null && existingTab != null)
                    {
                        existingWindow.FocusAndSelectTab(existingTab);
                        return;
                    }
                    var targetWindow = TabPaint.MainWindow.GetCurrentInstance();
                    if (targetWindow != null)
                    {
                        var tab = targetWindow.FileTabs.FirstOrDefault(t => t.FilePath == filePath);

                        if (tab == null)
                        {
                            int indexInList = targetWindow._imageFiles.IndexOf(filePath);
                            if (indexInList >= 0)
                            {
                                var newTab = new FileTabItem(filePath)
                                {
                                    IsNew = false, 
                                    IsDirty = false
                                };

                                targetWindow.FileTabs.Add(newTab);
                                targetWindow.FocusAndSelectTab(newTab);
                            }
                            else
                            {
                                _ = targetWindow.OpenFilesAsNewTabs(new string[] { filePath });
                            }
                        }
                        else
                        {
                            targetWindow.FocusAndSelectTab(tab);
                        }
                    }
                });
            });
            });
            // 1. 获取启动路径并检查其有效性（一次性检查）

  
            string filePath = "";//<0.1ms
            bool fileExists = false;
            if (e.Args is { Length: > 0 })
            {
                string inputPath = e.Args[0];
                if (System.IO.File.Exists(inputPath) || System.IO.Directory.Exists(inputPath))
                {
                    filePath = inputPath;
                    fileExists = true;
                }
            }
          
            // 等待配置加载完成
            var settingsManager = settingsTask.Result;
            var currentSettings = settingsManager.Current; //15ms
         
            LocalizationManager.ApplyLanguage(currentSettings.Language);//2ms

            currentSettings.PropertyChanged += Settings_PropertyChanged;
           
            AppTheme targetTheme = currentSettings.ThemeMode;//<0.1ms
            if (currentSettings.StartInViewMode && currentSettings.ViewUseDarkCanvasBackground && fileExists)
            {
                targetTheme = AppTheme.Dark;
            }
            ThemeManager.ApplyTheme(targetTheme);  //2ms

            // 3. 创建并启动主窗口
            base.OnStartup(e);//<0.1ms
            _mainWindow = new MainWindow(filePath, fileExists);//240ms
                _mainWindow.Show();//340ms
         

        }
        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                AiService.Instance.ReleaseAllModels();
                SingleInstance.Release();
            }
            catch { }
            base.OnExit(e);
            Environment.Exit(0);
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var settings = (AppSettings)sender;

            if (e.PropertyName == nameof(AppSettings.ThemeMode))
            {
                ThemeManager.ApplyTheme(settings.ThemeMode);
                foreach (Window w in Application.Current.Windows)
                {
                    if (w is MainWindow mw)
                    {
                        mw.SetUndoRedoButtonState();
                        mw.AutoUpdateMaximizeIcon();
                        mw.UpdateRulerPositions();
                    }
                    DwmBorderHelper.UpdateWindowBorder(w);
                }
            }
            else if (e.PropertyName == nameof(AppSettings.ThemeAccentColor))
            {
                ThemeManager.RefreshAccentColor(settings.ThemeAccentColor);
            }
            if (e.PropertyName == nameof(AppSettings.Language))
            {
                LocalizationManager.ApplyLanguage(settings.Language);
            }

        }

    }
}
