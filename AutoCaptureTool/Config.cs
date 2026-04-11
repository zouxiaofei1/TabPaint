using System;
using System.IO;

namespace AutoCaptureTool
{
    public enum CaptureMode
    {
        All = 0,
        ColorPicker = 1,
        SettingsGeneral = 2,
        SettingsPaint = 3,
        SettingsView = 4,
        SettingsShortcuts = 5,
        SettingsAdvanced = 6
    }

    public static class Config
    {
        public static string TabPaintPath = "e:/dev/tabp/TabPaint/bin/Debug/net8.0-windows10.0.19041.0/TabPaint.exe";
        public static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TabPaint", "settings.json");
        public static readonly string OutputDir = "e:/dev/tabp/Captures";

        public static void Initialize()
        {
            if (!File.Exists(TabPaintPath))
            {
                string altPath = "e:/dev/tabp/TabPaint/bin/Release/net8.0-windows10.0.19041.0/TabPaint.exe";
                if (File.Exists(altPath)) TabPaintPath = altPath;
            }
        }
    }
}
