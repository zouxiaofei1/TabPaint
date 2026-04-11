using System;
using System.IO;
using System.Threading;
using System.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.UIA3;
using FlaUI.Core.Definitions;

namespace AutoCaptureTool.Tasks
{
    public class SettingsGeneralTask : ICaptureTask
    {
        public string Name => "设置-通用页";
        public CaptureMode TargetMode => CaptureMode.SettingsGeneral;

        public void Execute(Window mainWindow, UIA3Automation automation, int processId, string themeName, string langName)
        {
            Console.WriteLine($"正在处理{Name}截图...");
            
            var window = automation.FromHandle(mainWindow.Properties.NativeWindowHandle.Value).AsWindow();

            var settingsBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("SettingsBtn"))?.AsButton();
            if (settingsBtn == null)
            {
                var allButtons = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
                settingsBtn = allButtons.FirstOrDefault(b => (b.Name?.Contains("设置") == true || b.Name?.Contains("Settings") == true))?.AsButton();
            }

            if (settingsBtn != null)
            {
                settingsBtn.Focus();
                Mouse.Click(settingsBtn.GetClickablePoint());
            }
            else
            {
                Console.WriteLine("未能找到设置按钮。");
                return;
            }

            var settingsWindow = Utils.FindWindow(automation, processId, window, (title, className) => 
                title.Contains("设置") || title.Contains("Settings") || className.Contains("HwndWrapper") || className == "Window");

            if (settingsWindow != null)
            {
                settingsWindow.SetForeground();
                Thread.Sleep(AppConsts.DelayWindowOpen); 
                
                using (var capture = FlaUI.Core.Capturing.Capture.Element(settingsWindow))
                {
                    string fileName = $"Settings_General_{langName}_{themeName}.webp";
                    Utils.SaveAsWebP(capture.Bitmap, Path.Combine(Config.OutputDir, fileName));
                    Console.WriteLine("成功保存截图: " + fileName);
                }
                try { settingsWindow.Close(); Thread.Sleep(AppConsts.DelayNormal); } catch { }
            }
            else
            {
                Console.WriteLine("未能找到设置窗口。");
            }
        }
    }
}
