using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using static TabPaint.MainWindow;
namespace TabPaint
{

    public static class ThemeManager
    {
        public static bool IsLazyIconsLoaded { get; private set; } = false;
        private const string RegistryKeyPath = AppConsts.RegistryKeyPathThemes;
        private const string RegistryValueName = AppConsts.RegistryValueNameLightTheme;
        public static event Action ThemeChanged;
        public static AppTheme CurrentAppliedTheme { get; private set; }

        // 监听系统颜色改变
        static ThemeManager()
        {
        }
        public static void StartSystemThemeMonitoring()
        {
            try
            {
                // 确保不重复订阅（虽然 SystemEvents 允许多播，但在这种场景下我们防一手）
                SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
                SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            }
            catch (Exception ex)
            {
                // 记录日志，系统事件挂钩失败不应导致程序崩溃
                Debug.WriteLine($"Failed to subscribe to SystemEvents: {ex.Message}");
            }
        }
        private static void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General)
            {
                if (SettingsManager.Instance.Current.ThemeMode == AppTheme.System)
                {
                    ApplyTheme(AppTheme.System);
                }
                
                string currentAccent = SettingsManager.Instance.Current.ThemeAccentColor;
                if (currentAccent != null && (currentAccent == "Auto" || currentAccent.StartsWith("Auto_")))
                {
                    RefreshAccentColor(currentAccent);
                }
            }
        }

        public static void ApplyTheme(AppTheme theme)
        {
           
            bool isDark = false;
            if (theme == AppTheme.Dark) isDark = true;

            else if (theme == AppTheme.Light) isDark = false;
            else isDark = IsSystemDark(); // 1.8ms

            AppTheme targetTheme = isDark ? AppTheme.Dark : AppTheme.Light; 

            // 1. 检查主题是否真的需要变更
            var mergedDicts = Application.Current.Resources.MergedDictionaries;
            ResourceDictionary oldDict = null;
            
            // 快速查找现有主题字典
            for (int i = 0; i < mergedDicts.Count; i++)
            {
                var d = mergedDicts[i];
                if (d.Source != null && (d.Source.OriginalString.EndsWith("LightTheme.xaml") || d.Source.OriginalString.EndsWith("DarkTheme.xaml")))
                {
                    oldDict = d;
                    break;
                }
            }

            bool themeChanged = (CurrentAppliedTheme != targetTheme) || (oldDict == null);
   
            if (themeChanged)
            {
                string dictPath = isDark ? "Resources/DarkTheme.xaml" : "Resources/LightTheme.xaml";
                var dictUri = new Uri($"pack://application:,,,/{dictPath}", UriKind.Absolute);
                var newDict = new ResourceDictionary() { Source = dictUri };

                if (oldDict != null)
                {
                    int index = mergedDicts.IndexOf(oldDict);
                    mergedDicts[index] = newDict;
                }
                else mergedDicts.Add(newDict);
                
                CurrentAppliedTheme = targetTheme;
            }
            UpdateWindowStyle(isDark);
           
            if (!IsWin11()) ApplyWin10FallbackBackground(isDark);
            RefreshAccentColor(SettingsManager.Instance.Current.ThemeAccentColor); 

        }
        private static void ReloadIconDictionary(string path)
        {
            var uri = new Uri($"pack://application:,,,/{path}");
            var mergedDicts = Application.Current.Resources.MergedDictionaries;
            var existing = mergedDicts.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.EndsWith(path)); // 使用 EndsWith 更稳健

            if (existing != null) mergedDicts.Remove(existing);
            mergedDicts.Add(new ResourceDictionary { Source = uri });
        }
        public static void LoadLazyIcons()
        {
            if (IsLazyIconsLoaded) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                ReloadIconDictionary("Resources/Icons/Icons.xaml");
                IsLazyIconsLoaded = true;
            }, System.Windows.Threading.DispatcherPriority.Background); // 使用 Background 优先级，不阻塞 UI
        }
        private static bool IsWin11()  {return Environment.OSVersion.Version.Build >= 22000;}
        private static void ApplyWin10FallbackBackground(bool isDark)
        {
            var resources = Application.Current.Resources;


            if (isDark)
            {
                var win10DarkBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202020"));
                win10DarkBg.Freeze();
                if (resources.Contains("WindowBackgroundBrush")) resources.Remove("WindowBackgroundBrush");
                resources["WindowBackgroundBrush"] = win10DarkBg;

            }
            else
            {
                var win10LightBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F3F3"));
                win10LightBg.Freeze();

                if (resources.Contains("WindowBackgroundBrush")) resources.Remove("WindowBackgroundBrush");
                resources["WindowBackgroundBrush"] = win10LightBg;
            }
        }
        public static void RefreshAccentColor(string hexColor = null)
        {
            if (string.IsNullOrEmpty(hexColor))
            {
                hexColor = SettingsManager.Instance.Current.ThemeAccentColor;
            }

            if (hexColor == "Auto")
            {
                hexColor = GetSystemAccentColorHex();
            }
            else if (hexColor != null && hexColor.StartsWith("Auto_"))
            {
                string baseHex = GetSystemAccentColorHex();
                Color baseColor = (Color)ColorConverter.ConvertFromString(baseHex);
                
                // 根据索引调整亮度: 0->最浅, 1->较浅, 2->原生, 3->较深
                float factor = 0;
                if (hexColor == "Auto_0") factor = 0.4f;
                else if (hexColor == "Auto_1") factor = 0.3f;
                else if (hexColor == "Auto_2") factor = 0.2f;
                else if (hexColor == "Auto_3") factor = 0.1f;
                else if (hexColor == "Auto_4") factor = 0f;
                else if (hexColor == "Auto_5") factor = -0.1f;
                else if (hexColor == "Auto_6") factor = -0.2f;
                else if (hexColor == "Auto_7") factor = -0.3f;

                Color adjustedColor = ChangeColorBrightness(baseColor, factor);
                hexColor = adjustedColor.ToString();
            }

            UpdateAccentResources(hexColor);
            if (!IsLazyIconsLoaded)  ReloadIconDictionary("Resources/Icons/Icons_Essential.xaml");
            else  ReloadIconDictionary("Resources/Icons/Icons.xaml");
            MainWindow mw = (MainWindow.GetCurrentInstance());
            mw?.UpdateToolSelectionHighlight(); ThemeChanged?.Invoke();
        }

        private static string GetSystemAccentColorHex()
        {
            try
            {
                var color = SystemParameters.WindowGlassColor;
                return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            }
            catch
            {
                return "#0078D4"; // 默认蓝色
            }
        }

        private static void UpdateAccentResources(string hexColor)
        {
            try
            {
                Color baseColor = (Color)ColorConverter.ConvertFromString(hexColor);
                Color hoverColor = ChangeColorBrightness(baseColor, 0.2f); // 变亮 20%
                Color pressedColor = ChangeColorBrightness(baseColor, -0.2f); // 变暗 20%
                Color borderColor = ChangeColorBrightness(baseColor, -0.1f); // 稍微变暗做边框

                // 2. 创建画刷 (Freeze 提高性能)
                var resources = Application.Current.Resources;

                // --- ToolAccent (工具栏图标/选中状态) ---
                SetSolidBrush(resources, "ToolAccentBrush", baseColor);
                SetSolidBrush(resources, "ToolAccentHoverBrush", hoverColor);
                SetSolidBrush(resources, "ToolAccentBorderBrush", borderColor);

                // --- ToolAccentSubtle (透明背景系列) ---
                SetSolidBrush(resources, "ToolAccentSubtleHoverBrush", baseColor, 0.10);
                SetSolidBrush(resources, "ToolAccentSubtlePressedBrush", baseColor, 0.20);
                SetSolidBrush(resources, "ToolAccentSubtleSelectedBrush", baseColor, 0.15);
                SetSolidBrush(resources, "ToolAccentSubtleHoverSelectedBrush", baseColor, 0.25);

                SetSolidBrush(resources, "SystemAccentBrush", baseColor);
                SetSolidBrush(resources, "SystemAccentHoverBrush", hoverColor);
                SetSolidBrush(resources, "SystemAccentPressedBrush", pressedColor);
                SetSolidBrush(resources, "SliderTrackFillBrush", baseColor); // 滑块填充

                // 列表选中项边框
                SetSolidBrush(resources, "ListItemSelectedBorderBrush", baseColor);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting accent color: {ex.Message}");
            }
        }

        private static void SetSolidBrush(ResourceDictionary resources, string key, Color color, double opacity = 1.0)
        {
            var brush = new SolidColorBrush(color) { Opacity = opacity };
            brush.Freeze();

            // 如果资源已存在，移除它以确保覆盖（对于 MergedDictionaries 这种方式比较稳妥）
            if (resources.Contains(key)) resources.Remove(key);
            resources[key] = brush;
        }
        private static Color ChangeColorBrightness(Color color, float correctionFactor)
        {
            float red = (float)color.R;
            float green = (float)color.G;
            float blue = (float)color.B;

            if (correctionFactor < 0)
            {
                correctionFactor = 1 + correctionFactor;
                red *= correctionFactor;
                green *= correctionFactor;
                blue *= correctionFactor;
            }
            else
            {
                red = (255 - red) * correctionFactor + red;
                green = (255 - green) * correctionFactor + green;
                blue = (255 - blue) * correctionFactor + blue;
            }

            return Color.FromArgb(color.A, (byte)red, (byte)green, (byte)blue);
        }
        private static bool IsSystemDark()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
                {
                    object registryValueObject = key?.GetValue(RegistryValueName);
                    if (registryValueObject == null) return false;
                    return (int)registryValueObject == 0;
                }
            }
            catch
            {
                return false; // 读取失败默认浅色
            }
        }

        private static void UpdateWindowStyle(bool isDark)
        {
            foreach (Window window in Application.Current.Windows)
            {
                SetWindowImmersiveDarkMode(window, isDark);

                if (MicaAcrylicManager.IsWin11())
                {
                    var bgBrush =Brushes.Transparent ;
                    window.Background = bgBrush;
                }
                else MicaAcrylicManager.ApplyEffect(window);
            }
        }
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public static bool SetWindowImmersiveDarkMode(Window window, bool enabled)
        {
            if (window == null) return false;
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return false;

            int attribute = AppConsts.DWMWA_USE_IMMERSIVE_DARK_MODE;
            int useImmersiveDarkMode = enabled ? 1 : 0;

            bool success = true;
            if (DwmSetWindowAttribute(handle, attribute, ref useImmersiveDarkMode, sizeof(int)) != 0)
            {
                // 尝试旧版本的 Attribute
                attribute = AppConsts.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
                if (DwmSetWindowAttribute(handle, attribute, ref useImmersiveDarkMode, sizeof(int)) != 0)  success = false;
            }

            if (success)
            {
                // 强制刷新非客户区以应用主题（特别是 Win10）
                SendMessage(handle, AppConsts.WM_NCACTIVATE, (IntPtr)0, (IntPtr)0);
                SendMessage(handle, AppConsts.WM_NCACTIVATE, (IntPtr)1, (IntPtr)0);
            }

            return success;
        }
    }
}
