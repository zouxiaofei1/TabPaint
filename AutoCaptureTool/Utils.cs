using System;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using SkiaSharp;

namespace AutoCaptureTool
{
    public static class Utils
    {
        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        public static void KillTabPaint()
        {
            foreach (var p in Process.GetProcessesByName("TabPaint"))
            {
                try { p.Kill(); p.WaitForExit(AppConsts.TimeoutProcessExit); } catch { }
            }
        }

        public static void UpdateSettings(int theme, int lang)
        {
            try
            {
                string? dir = Path.GetDirectoryName(Config.SettingsPath);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                JsonNode root;
                if (File.Exists(Config.SettingsPath)) {
                    string text = File.ReadAllText(Config.SettingsPath);
                    root = JsonNode.Parse(text) ?? new JsonObject();
                } else {
                    root = new JsonObject();
                }

                root["theme_mode"] = theme;
                root["language"] = lang;
                root["is_compact_color_picker"] = false; 
                File.WriteAllText(Config.SettingsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                
                string binPath = Path.ChangeExtension(Config.SettingsPath, ".bin");
                if (File.Exists(binPath)) File.Delete(binPath);
            }
            catch (Exception ex) { Console.WriteLine("警告: 更新配置文件失败: " + ex.Message); }
        }

        public static void SaveAsWebP(Bitmap bitmap, string path)
        {
            try
            {
                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, ImageFormat.Png);
                    stream.Seek(0, SeekOrigin.Begin);
                    using (var skBitmap = SKBitmap.Decode(stream))
                    using (var image = SKImage.FromBitmap(skBitmap))
                    using (var data = image.Encode(SKEncodedImageFormat.Webp, 90))
                    using (var outputStream = File.OpenWrite(path))
                    {
                        data.SaveTo(outputStream);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("WebP 转换失败: " + ex.Message);
            }
        }

        public static Window? FindWindow(UIA3Automation automation, int processId, Window mainWindow, Func<string, string, bool> predicate)
        {
            for (int i = 0; i < AppConsts.MaxRetryFindWindow; i++)
            {
                var desktop = automation.GetDesktop();
                var searchList = new List<AutomationElement>();
                
                try { searchList.AddRange(desktop.FindAllChildren()); } catch { }
                try { searchList.AddRange(mainWindow.FindAllChildren()); } catch { }
                try { searchList.AddRange(mainWindow.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window))); } catch { }

                foreach (var w in searchList)
                {
                    try
                    {
                        int winPid = w.Properties.ProcessId.Value;
                        bool isTabPaint = (winPid == processId);
                        if (!isTabPaint)
                        {
                            try {
                                using (var p = Process.GetProcessById(winPid)) {
                                    if (p.ProcessName.Contains("TabPaint", StringComparison.OrdinalIgnoreCase)) isTabPaint = true;
                                }
                            } catch { }
                        }

                        if (isTabPaint && w.Properties.NativeWindowHandle.Value != mainWindow.Properties.NativeWindowHandle.Value)
                        {
                            string title = w.Name ?? "";
                            string className = w.ClassName ?? "";
                            if (predicate(title, className))
                            {
                                return w.AsWindow();
                            }
                        }
                    }
                    catch { }
                }
                Thread.Sleep(AppConsts.DelayNormal);
            }
            return null;
        }
    }
}
