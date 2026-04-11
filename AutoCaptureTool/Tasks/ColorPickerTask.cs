using System;
using System.IO;
using System.Threading;
using System.Drawing;
using System.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.UIA3;

namespace AutoCaptureTool.Tasks
{
    public class ColorPickerTask : ICaptureTask
    {
        public string Name => "颜色选择器";
        public CaptureMode TargetMode => CaptureMode.ColorPicker;

        public void Execute(Window mainWindow, UIA3Automation automation, int processId, string themeName, string langName)
        {
            Console.WriteLine($"正在处理{Name}截图...");
            
            // 重新获取窗口确保元素最新
            var window = automation.FromHandle(mainWindow.Properties.NativeWindowHandle.Value).AsWindow();

            Console.WriteLine("正在搜索调色盘按钮...");
            Button? customColorBtn = null;

            var colorBtn1 = window.FindFirstDescendant(cf => cf.ByAutomationId("ColorBtn1"))?.AsButton();
            if (colorBtn1 != null)
            {
                var cbRect = colorBtn1.BoundingRectangle;
                Console.WriteLine($"找到 ColorBtn1, 位置: {cbRect}");
                
                var allButtons = window.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));
                foreach (var btn in allButtons)
                {
                    var r = btn.BoundingRectangle;
                    if (r.Width > 20 && r.Height > 20) {
                        double btnCenterY = r.Y + (r.Height / 2);
                        double cbCenterY = cbRect.Y + (cbRect.Height / 2);
                        if (r.Right < cbRect.Left + 5 && r.Right >= cbRect.Left - 60 && Math.Abs(btnCenterY - cbCenterY) < 30)
                        {
                            customColorBtn = btn.AsButton();
                            Console.WriteLine($"找到疑似调色盘按钮, 位置: {r}");
                            break;
                        }
                    }
                }
            }

            if (customColorBtn != null)
            {
                Console.WriteLine("执行点击操作...");
                customColorBtn.Focus();
                var point = customColorBtn.GetClickablePoint();
                Mouse.MoveTo(point);
                Thread.Sleep(AppConsts.DelayShort);
                Mouse.Click(point);
            }
            else
            {
                Console.WriteLine("尝试根据相对坐标点击 (兜底)...");
                if (colorBtn1 != null) {
                    var r = colorBtn1.BoundingRectangle;
                    var point = new Point((int)r.Left - 25, (int)(r.Y + r.Height / 2));
                    Mouse.MoveTo(point);
                    Thread.Sleep(AppConsts.DelayShort);
                    Mouse.Click(point);
                }
            }

            Console.WriteLine("等待颜色选择器窗口弹出...");
            var pickerWindow = Utils.FindWindow(automation, processId, window, (title, className) => 
                title.Contains("颜色") || title.Contains("Color") || 
                className.Contains("ModernColorPicker") || className.Contains("HwndWrapper") || className == "Window");

            if (pickerWindow != null)
            {
                Console.WriteLine("捕获到窗口: " + pickerWindow.Name);
                pickerWindow.SetForeground();
                Thread.Sleep(AppConsts.DelayWindowOpen); 
                
                using (var capture = FlaUI.Core.Capturing.Capture.Element(pickerWindow))
                {
                    string fileName = $"ColorPicker_{langName}_{themeName}.webp";
                    Utils.SaveAsWebP(capture.Bitmap, Path.Combine(Config.OutputDir, fileName));
                    Console.WriteLine("成功保存截图: " + fileName);
                }
                try { pickerWindow.Close(); } catch { }
            }
            else
            {
                Console.WriteLine("未能找到颜色选择器窗口。");
            }
        }
    }
}
