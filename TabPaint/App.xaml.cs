//
//App.xaml.cs
//应用程序入口点，负责初始化设置、异常处理、单实例检测以及主窗口的启动。
//
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices; // 必须引用，用于置顶窗口
using System.Text;
using System.Windows;
using System.Windows.Threading;
using TabPaint.Services;
using TabPaint.Windows;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public partial class App : Application
    {
        private static bool _isExiting = false;
        private static bool _isHandlingFatalException = false;

        private enum ExceptionSeverity
        {
            Recoverable,
            Fatal
        }

        public static void GlobalExit()
        {
            if (_isExiting) return;
            _isExiting = true;

            TrayIconService.Dispose();

            var windows = Application.Current.Windows.Cast<Window>().ToList();
            foreach (Window window in windows)
            {
                if (window is MainWindow mw)
                {
                    mw.OnClosing();
                }
                else
                {
                    try { window.Close(); } catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
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
                var severity = ClassifyException(e.Exception, "UIThread");

                if (severity == ExceptionSeverity.Recoverable)
                {
                    e.Handled = true;
                    //NotifyRecoverableException(e.Exception);
                    return;
                }

                e.Handled = true;
                HandleFatalException(e.Exception, "UIThread");
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var exception = e.ExceptionObject as Exception;
                LogException(exception, "AppDomain");
                HandleFatalException(exception, "AppDomain");
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogException(e.Exception, "TaskScheduler");
                var severity = ClassifyException(e.Exception, "TaskScheduler");
                e.SetObserved();

                if (severity == ExceptionSeverity.Fatal)
                {
                    HandleFatalException(e.Exception, "TaskScheduler");
                }
            };
        }

        private static ExceptionSeverity ClassifyException(Exception ex, string source)
        {
            if (ex == null)
            {
                return source == "UIThread" ? ExceptionSeverity.Recoverable : ExceptionSeverity.Fatal;
            }

            return IsFatalException(ex) ? ExceptionSeverity.Fatal : ExceptionSeverity.Recoverable;
        }

        private static bool IsFatalException(Exception ex)
        {
            if (ex is OutOfMemoryException
                or StackOverflowException
                or AccessViolationException
                or SEHException)
            {
                return true;
            }

            if (ex is AggregateException aggregate)
            {
                var flattened = aggregate.Flatten();
                return flattened.InnerExceptions.Any(IsFatalException);
            }

            return ex.InnerException != null && IsFatalException(ex.InnerException);
        }

        private void NotifyRecoverableException(Exception ex)
        {
            try
            {
                string errorMessage = ex?.Message ?? "未知异常";
                string msg = $@"TabPaint 捕获到可恢复异常，程序将继续运行。

错误信息: {errorMessage}

建议先保存当前工作。
日志位置: {LogDirectory}";
                FluentMessageBox.Show(msg, "已恢复异常", MessageBoxButton.OK, MessageBoxImage.Warning, null, LogDirectory);
            }
            catch (Exception notifyEx)
            {
                Debug.WriteLine($"Failed to show recoverable exception notification: {notifyEx}");
            }
        }

        private void HandleFatalException(Exception ex, string source)
        {
            if (_isHandlingFatalException) return;
            _isHandlingFatalException = true;

            try
            {
                Debug.WriteLine($"Fatal exception from {source}: {ex}");

                if (Application.Current?.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ShutdownAppWithErrorMessage(ex ?? new Exception($"Fatal exception from {source}"));
                    });
                }
                else
                {
                    Debug.WriteLine($"Dispatcher unavailable when handling fatal exception from {source}.");
                }
            }
            catch (Exception handleEx)
            {
                Debug.WriteLine($"Failed to process fatal exception: {handleEx}");
            }
            finally
            {
                try
                {
                    GlobalExit();
                }
                catch (Exception exitEx)
                {
                    Debug.WriteLine($"GlobalExit failed: {exitEx}");
                }
            }
        }

        private static void LogException(Exception? ex, string source)
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
            string msg = $@"TabPaint 遇到错误需要关闭。

错误信息: {ex.Message}

日志已保存至: {LogDirectory}";
            FluentMessageBox.Show(msg, "程序崩溃", MessageBoxButton.OK, MessageBoxImage.Error, null, LogDirectory);

            try
            {
            }
            catch (global::System.Exception ignoredEx) { global::System.Diagnostics.Debug.WriteLine(ignoredEx); }

          //  Environment.Exit(1);
        }
        protected override void OnStartup(StartupEventArgs e)
        {//680ms
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            StartupPerformanceTracer.StartSession($"Startup_PID{Environment.ProcessId}");
            using var __perfOnStartup = StartupPerformanceTracer.Measure("App.OnStartup");
            StartupPerformanceTracer.Point("App.OnStartup.Enter");

            var settingsTask = Task.Run(() => SettingsManager.Instance);

            // 清理待删除的插件文件
            _ = Task.Run(() =>
            {
                try
                {
                    if (Directory.Exists(AppConsts.PluginsDir))
                    {
                        var deleteFiles = Directory.GetFiles(AppConsts.PluginsDir, "*.delete");
                        foreach (var file in deleteFiles)
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }
                }
                catch { }
            });

            try
            { // 启用分级 JIT 编译优化
                using var __perfProfileOpt = StartupPerformanceTracer.Measure("App.OnStartup.ProfileOptimization");
                string profileRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TabPaint", "Profiles");
                Directory.CreateDirectory(profileRoot);
                System.Runtime.ProfileOptimization.SetProfileRoot(profileRoot);
                System.Runtime.ProfileOptimization.StartProfile("Startup.profile");
            }//4ms
            catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
 
            using (StartupPerformanceTracer.Measure("App.SetupExceptionHandling")) SetupExceptionHandling();//检查单实例0.9ms

            bool isFirstInstance;
            using (StartupPerformanceTracer.Measure("App.SingleInstance.IsFirstInstance"))
            {
                isFirstInstance = SingleInstance.IsFirstInstance();
            }

            if (!isFirstInstance)//0.3ms
            {
                if (e.Args != null && e.Args.Length > 0)
                {
                    foreach (var arg in e.Args)
                    {
                        SingleInstance.SendArgsToFirstInstance(new string[] { arg });
                    }
                }
                else
                {
                    SingleInstance.SendArgsToFirstInstance(new string[] { "" });
                }
                Environment.Exit(0);
                return;
            } 
          
            _ = Task.Run(() =>//创建线程池，10ms
            {
                SingleInstance.ListenForArgs((filePath) =>
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    static void EnsureWindowVisible(MainWindow window)
                    {
                        if (window != null && !window.IsVisible)
                        {
                            window.Show();
                        }
                    }

                    if (string.IsNullOrEmpty(filePath))
                    {
                        var newWindow = new MainWindow("", fileExists: false, loadSession: false);
                        newWindow.Show();
                        RestoreWindow(newWindow);
                        return;
                    }

                    var (existingWindow, existingTab) = TabPaint.MainWindow.FindWindowHostingFile(filePath);
                    if (existingWindow != null && existingTab != null)
                    {
                        EnsureWindowVisible(existingWindow);
                        RestoreWindow(existingWindow);
                        existingWindow.FocusAndSelectTab(existingTab);
                        return;
                    }
                    var targetWindow = TabPaint.MainWindow.GetCurrentInstance();
                    if (targetWindow != null)
                    {
                        EnsureWindowVisible(targetWindow);
                        RestoreWindow(targetWindow);
                        var tab = targetWindow.FileTabs.FirstOrDefault(t => string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

                        if (tab == null)
                        {
                            int indexInList = targetWindow._imageFiles.FindIndex(f => string.Equals(f, filePath, StringComparison.OrdinalIgnoreCase));
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
                    else
                    {
                        bool inputExists = File.Exists(filePath) || Directory.Exists(filePath);
                        var newWindow = new MainWindow(filePath, fileExists: inputExists, loadSession: false);
                        newWindow.Show();
                        RestoreWindow(newWindow);
                    }
                });
            });
            });
            // 1. 获取启动路径并检查其有效性（一次性检查）

  
            string filePath = "";//<0.1ms
            bool fileExists = false;
            List<string> extraFiles = new List<string>();

            if (e.Args is { Length: > 0 })
            {
                foreach (var arg in e.Args)
                {
                    if (System.IO.File.Exists(arg) || System.IO.Directory.Exists(arg))
                    {
                        if (string.IsNullOrEmpty(filePath))
                        {
                            filePath = arg;
                            fileExists = true;
                        }
                        else
                        {
                            extraFiles.Add(arg);
                        }
                    }
                }
            }
          
            // 等待配置加载完成
            AppSettings currentSettings;
            using (StartupPerformanceTracer.Measure("App.WaitSettingsTask"))
            {
                var settingsManager = settingsTask.Result;
                currentSettings = settingsManager.Current; //15ms
            }
            StartupPerformanceTracer.Point("App.SettingsLoaded");
         
            using (StartupPerformanceTracer.Measure("App.LocalizationManager.ApplyLanguage"))
                LocalizationManager.ApplyLanguage(currentSettings.Language);//2ms

            currentSettings.PropertyChanged += Settings_PropertyChanged;
           
            AppTheme targetTheme = currentSettings.ThemeMode;//<0.1ms
            if (currentSettings.StartInViewMode && currentSettings.ViewUseDarkCanvasBackground && fileExists)
            {
                targetTheme = AppTheme.Dark;
            }
            using (StartupPerformanceTracer.Measure("App.ThemeManager.ApplyTheme"))
                ThemeManager.ApplyTheme(targetTheme);  //2ms
            using (StartupPerformanceTracer.Measure("App.ThemeManager.StartSystemThemeMonitoring"))
                ThemeManager.StartSystemThemeMonitoring();

            // 3. 创建并启动主窗口
            using (StartupPerformanceTracer.Measure("App.base.OnStartup")) base.OnStartup(e);//<0.1ms
            using (StartupPerformanceTracer.Measure("App.MainWindow.Ctor"))
            {
                _mainWindow = new MainWindow(filePath, fileExists, extraFiles: extraFiles);//240ms
            }
            StartupPerformanceTracer.Point("App.MainWindow.Created");
            using (StartupPerformanceTracer.Measure("App.MainWindow.Show")) _mainWindow.Show();//340ms
            StartupPerformanceTracer.Point("App.MainWindow.Shown");
            using (StartupPerformanceTracer.Measure("App.TrayIconService.UpdateVisibility"))
                TrayIconService.UpdateVisibility();

            _mainWindow.Dispatcher.BeginInvoke(new Action(() =>
            {
                TryShowWhatsNew(currentSettings);
            }), DispatcherPriority.ApplicationIdle);
         

        }

        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                AssemblyName assemblyName = new AssemblyName(args.Name);
                if (assemblyName.Name == "zxing")
                {
                    string dllPath = Path.Combine(AppConsts.PluginsDir, AppConsts.ZXing_DllName);
                    if (File.Exists(dllPath))
                    {
                        return Assembly.LoadFrom(dllPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AssemblyResolve error: {ex.Message}");
            }
            return null;
        }

        private void TryShowWhatsNew(AppSettings settings)
        {
            if (settings == null) return;

            try
            {
                string currentVersion = NormalizeVersionText(AppConsts.ProgramVersion);
                string previousVersion = NormalizeVersionText(settings.LastLaunchedVersion);
                string newestInstalledVersion = NormalizeVersionText(settings.NewestInstalledVersion);

                bool hasPreviousVersion = !string.IsNullOrWhiteSpace(previousVersion);

                // 仅当当前版本超过“历史最高已安装版本”时才显示，避免多版本并存反复触发。
                bool shouldShowWhatsNew = hasPreviousVersion && CompareVersion(currentVersion, newestInstalledVersion) > 0;

                if (shouldShowWhatsNew)
                {
                    var win = new WhatsNewWindow(previousVersion, currentVersion)
                    {
                        Owner = _mainWindow
                    };
                    win.ShowDialog();
                }

                settings.LastLaunchedVersion = currentVersion;
                if (CompareVersion(currentVersion, newestInstalledVersion) > 0)
                {
                    settings.NewestInstalledVersion = currentVersion;
                }
                SettingsManager.Instance.Save();
            }
            catch (Exception ex)
            {
                Logger.Error("[WhatsNew] Failed to process upgrade popup.", ex);
            }
        }

        private static string NormalizeVersionText(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return string.Empty;
            return version.Trim().TrimStart('v', 'V');
        }

        private static int CompareVersion(string current, string previous)
        {
            var currentVer = ParseVersionOrZero(current);
            var previousVer = ParseVersionOrZero(previous);
            return currentVer.CompareTo(previousVer);
        }

        private static Version ParseVersionOrZero(string text)
        {
            string normalized = NormalizeVersionText(text);
            return Version.TryParse(normalized, out var v) ? v : new Version(0, 0, 0, 0);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                TrayIconService.Dispose();
                AiService.Instance.ReleaseAllModels();
                SingleInstance.Release();
            }
            catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
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
