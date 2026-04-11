using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using AutoCaptureTool.Tasks;

#pragma warning disable CA1416

namespace AutoCaptureTool
{
    class Program
    {
        static void Main(string[] args)
        {
            Utils.SetProcessDPIAware();
            Config.Initialize();
            
            Console.WriteLine("=== TabPaint 自动化截图工具 ===");
            
            CaptureMode mode = CaptureMode.All;
            Console.WriteLine("请选择工作模式:");
            Console.WriteLine("0. 截取所有图 (默认)");
            Console.WriteLine("1. 仅截取颜色选择器");
            Console.WriteLine("2. 仅截取设置-通用页");
            Console.WriteLine("3. 仅截取设置-画图页");
            Console.WriteLine("4. 仅截取设置-看图页");
            Console.WriteLine("5. 仅截取设置-快捷键页");
            Console.WriteLine("6. 仅截取设置-高级页");
            Console.Write("输入选项 (0/1/2/3/4/5/6): ");
            var input = Console.ReadLine();
            if (input == "1") mode = CaptureMode.ColorPicker;
            else if (input == "2") mode = CaptureMode.SettingsGeneral;
            else if (input == "3") mode = CaptureMode.SettingsPaint;
            else if (input == "4") mode = CaptureMode.SettingsView;
            else if (input == "5") mode = CaptureMode.SettingsShortcuts;
            else if (input == "6") mode = CaptureMode.SettingsAdvanced;

            Utils.KillTabPaint();

            Console.WriteLine($"使用可执行文件: {Config.TabPaintPath}");

            if (Directory.Exists(Config.OutputDir)) {
                try { 
                    foreach(var f in Directory.GetFiles(Config.OutputDir)) File.Delete(f);
                    Console.WriteLine("清空旧截图目录。");
                } catch {}
            }
            Directory.CreateDirectory(Config.OutputDir);

            string backupPath = Config.SettingsPath + ".bak";
            if (File.Exists(Config.SettingsPath))
            {
                try { File.Copy(Config.SettingsPath, backupPath, true); Console.WriteLine("已备份 settings.json"); } catch { }
            }

            try
            {
                var themes = new[] { new { ID = 0, Name = "Light" }, new { ID = 1, Name = "Dark" } };
                var langs = new[] { new { ID = 0, Name = "zh-CN" }, new { ID = 1, Name = "en-US" } };

                foreach (var lang in langs)
                {
                    foreach (var theme in themes)
                    {
                        Console.WriteLine("");
                        Console.WriteLine($">>> 正在处理方案: 语言={lang.Name}, 主题={theme.Name}");
                        Utils.UpdateSettings(theme.ID, lang.ID);
                        
                        RunCaptureWorkflow(mode, theme.Name, lang.Name);
                        
                        Utils.KillTabPaint();
                        Thread.Sleep(AppConsts.DelayBetweenSchemes); 
                    }
                }
            }
            finally
            {
                if (File.Exists(backupPath))
                {
                    try { 
                        File.Copy(backupPath, Config.SettingsPath, true); 
                        File.Delete(backupPath); 
                        Console.WriteLine("已恢复原始 settings.json"); 
                    } catch { }
                }
            }

            Console.WriteLine("");
            Console.WriteLine("任务结束。截图保存在: " + Config.OutputDir);
        }

        static void RunCaptureWorkflow(CaptureMode mode, string themeName, string langName)
        {
            var tasks = new List<ICaptureTask>
            {
                new ColorPickerTask(),
                new SettingsGeneralTask(),
                new SettingsPaintTask(),
                new SettingsViewTask(),
                new SettingsShortcutsTask(),
                new SettingsAdvancedTask()
            };

            using (var automation = new UIA3Automation())
            {
                var app = Application.Launch(Config.TabPaintPath);
                try
                {
                    var window = app.GetMainWindow(automation, AppConsts.TimeoutMainWindow);
                    if (window == null) { 
                        Console.WriteLine("无法通过 Launch 获取主窗口，尝试从进程获取...");
                        var processes = Process.GetProcessesByName("TabPaint");
                        if (processes.Length > 0) {
                            window = automation.FromHandle(processes[0].MainWindowHandle).AsWindow();
                        }
                    }

                    if (window == null) { Console.WriteLine("无法获取主窗口。"); return; }
                    
                    window.SetForeground();
                    Console.WriteLine("等待 UI 初始化");
                    Thread.Sleep(AppConsts.DelayUIInit); 

                    int processId = app.ProcessId;

                    foreach (var task in tasks)
                    {
                        if (mode == CaptureMode.All || mode == task.TargetMode)
                        {
                            if (mode == CaptureMode.All && tasks.IndexOf(task) > 0) Thread.Sleep(AppConsts.DelayBetweenTasks);
                            task.Execute(window, automation, processId, themeName, langName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("执行异常: " + ex.Message);
                }
                finally
                {
                    try { app.Close(); } catch { }
                    Thread.Sleep(AppConsts.DelayNormal);
                }
            }
        }
    }
}
