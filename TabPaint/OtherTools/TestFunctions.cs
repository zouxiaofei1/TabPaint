using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
//
//TabPaint主程序
//

namespace TabPaint
{
    public static class NoiseTextureGenerator
    {
        private static ImageBrush _cachedLightNoise;
        private static ImageBrush _cachedDarkNoise;

        public static ImageBrush CreateNoiseBrush(bool isDark, int size = 64, double opacity = 0.03)
        {
            // 使用缓存避免重复生成
            if (isDark && _cachedDarkNoise != null) return _cachedDarkNoise;
            if (!isDark && _cachedLightNoise != null) return _cachedLightNoise;

            var bitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
            var pixels = new byte[size * size * 4];
            var rng = new Random(42); // 固定种子，保证每次一致

            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte color = isDark ? (byte)255 : (byte)0;
                byte alpha = (byte)rng.Next(0, (int)(255 * opacity));

                pixels[i + 0] = color; // B
                pixels[i + 1] = color; // G
                pixels[i + 2] = color; // R
                pixels[i + 3] = alpha; // A
            }

            bitmap.WritePixels(new Int32Rect(0, 0, size, size), pixels, size * 4, 0);
            bitmap.Freeze();

            var brush = new ImageBrush(bitmap)
            {
                TileMode = TileMode.Tile,
                Stretch = Stretch.None,
                ViewportUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(0, 0, size, size),
                Opacity = 1.0 // alpha 已经编码在像素里了
            };
            brush.Freeze();

            if (isDark) _cachedDarkNoise = brush;
            else _cachedLightNoise = brush;

            return brush;
        }

        public static void ClearCache()
        {
            _cachedLightNoise = null;
            _cachedDarkNoise = null;
        }
    }
    public static class DwmBorderHelper
    {
        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void DwmSetWindowAttribute(
            IntPtr hwnd,
            int attribute,
            ref uint pvAttribute,
            int cbAttribute);

        public static void SetBorderColor(Window window, Color color)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;

                // DWM 使用 COLORREF 格式: 0x00BBGGRR
                uint colorRef = (uint)(color.R | (color.G << 8) | (color.B << 16));
                DwmSetWindowAttribute(hwnd, AppConsts.DWMWA_BORDER_COLOR, ref colorRef, sizeof(uint));
            }
            catch
            {
                // Win10 或旧版 Win11 不支持，静默忽略
            }
        }

        public static void UpdateWindowBorder(Window window)
        {
            if (!window.IsLoaded) return;

            bool shouldHighlight = window.IsActive;

            // MainWindow 特殊处理：看图模式下即使激活也不高亮
            if (window is MainWindow mw && mw.IsViewMode)
            {
                shouldHighlight = false;
            }

            if (window is MainWindow mainWindow && !mainWindow.IsWin11)
            {
                var border = mainWindow.FindName("WindowRootBorder") as Border;
                if (border != null)
                {
                    if (shouldHighlight)
                    {
                        border.BorderBrush = window.FindResource("SystemAccentPressedBrush") as Brush ?? border.BorderBrush;
                        if (border.Effect is DropShadowEffect shadow)
                        {
                            var newShadow = shadow.Clone();
                            newShadow.Opacity = 0.6;
                            border.Effect = newShadow;
                        }
                    }
                    else
                    {
                        border.BorderBrush = window.FindResource("BorderMediumBrush") as Brush ?? border.BorderBrush;
                        if (border.Effect is DropShadowEffect shadow)
                        {
                            var newShadow = shadow.Clone();
                            newShadow.Opacity = 0.3;
                            border.Effect = newShadow;
                        }
                    }
                }
                return;
            }

            if (shouldHighlight)
            {
                var accentBrush = window.FindResource("SystemAccentPressedBrush") as SolidColorBrush;
                if (accentBrush != null) SetBorderColor(window, accentBrush.Color);
            }
            else
            {
                var borderBrush = window.FindResource("BorderMediumBrush") as SolidColorBrush;
                if (borderBrush != null) SetBorderColor(window, borderBrush.Color);
            }
        }

        public static void SupportFocusHighlight(this Window window)
        {
            window.Activated += (s, e) => UpdateWindowBorder(window);
            window.Deactivated += (s, e) => UpdateWindowBorder(window);
            
            // 确保首次加载时应用
            if (window.IsLoaded) UpdateWindowBorder(window);
            else window.Loaded += (s, e) => UpdateWindowBorder(window);
        }

        public static void SetDefaultBorderColor(Window window)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;

                uint colorDefault = 0xFFFFFFFF;
                DwmSetWindowAttribute(hwnd, AppConsts.DWMWA_BORDER_COLOR, ref colorDefault, sizeof(uint));
            }
            catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
        }
        public static void HideBorder(Window window)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;

                uint colorNone = 0xFFFFFFFE;
                DwmSetWindowAttribute(hwnd, AppConsts.DWMWA_BORDER_COLOR, ref colorNone, sizeof(uint));
            }
            catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
        }
    }

    public static class WindowHelper
    {
        private static readonly DependencyProperty LocalModalResultProperty =
        DependencyProperty.RegisterAttached(
            "LocalModalResult",
            typeof(bool?),
            typeof(WindowHelper),
            new PropertyMetadata(null));

        // 标记窗口是否以局部模态方式打开
        private static readonly DependencyProperty IsLocalModalProperty =
            DependencyProperty.RegisterAttached(
                "IsLocalModal",
                typeof(bool),
                typeof(WindowHelper),
                new PropertyMetadata(false));

        public static bool? ShowOwnerModal(this Window dialog, Window owner, bool disableOwner = true)
        {
            if (owner != null)
            {
                dialog.Owner = owner;
                if (disableOwner) owner.IsEnabled = false;
            }

            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialog.SetValue(IsLocalModalProperty, true);

            bool? result = null;
            var frame = new System.Windows.Threading.DispatcherFrame();

            CancelEventHandler ownerClosing = null;
            ownerClosing = (s, e) =>
            {
                if (dialog.IsVisible)
                    dialog.Close();
            };

            if (owner != null) owner.Closing += ownerClosing;

            dialog.Closed += (s, e) =>
            {
                // ★ 从附加属性读取结果
                result = (bool?)dialog.GetValue(LocalModalResultProperty);
                if (owner != null)
                {
                    owner.Closing -= ownerClosing;
                    if (disableOwner) owner.IsEnabled = true;
                    if (owner.IsVisible)
                        owner.Activate();
                }
                frame.Continue = false;
            };

            dialog.Show();
            System.Windows.Threading.Dispatcher.PushFrame(frame);

            return result;
        }

        public static void SetDialogResultSafe(this Window window, bool? value)
        {
            bool isLocalModal = (bool)window.GetValue(IsLocalModalProperty);
            if (isLocalModal)
            {
                // 局部模态模式：存到附加属性，然后关闭
                window.SetValue(LocalModalResultProperty, value);
            }
            else
            {
                // 标准 ShowDialog 模式：直接设置
                window.DialogResult = value;
            }
        }
       
    }
    public class ColorMatchConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;

            string stored = value.ToString();
            string param = parameter.ToString();

            if (stored == "Auto" || param == "Auto" || stored.StartsWith("Auto_") || param.StartsWith("Auto_"))
            {
                return stored.Equals(param, StringComparison.OrdinalIgnoreCase);
            }

            stored = NormalizeColor(stored);
            param = NormalizeColor(param);

            return stored.Equals(param, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
            {
                string param = parameter.ToString();
                if (param == "Auto") return "Auto";
                return NormalizeColor(param); // 统一存储为6位格式
            }
            return Binding.DoNothing;
        }
        private static string NormalizeColor(string color)
        {
            if (string.IsNullOrEmpty(color)) return string.Empty;

            color = color.Trim();
            if (color == "Auto") return "Auto";
            if (color.Length == 9 && color.StartsWith("#"))
            {
                return "#" + color.Substring(3);
            }

            return color;
        }
    }

    public class TagToCornerRadiusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string tag = value?.ToString();
            if (tag == "Auto_0") return new CornerRadius(6, 0, 0, 6);
            if (tag == "Auto_7") return new CornerRadius(0, 6, 6, 0);
            return new CornerRadius(0);
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ZoomToInverseValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double scale && scale > 0)
            {
                double baseSize = 1.0;
                if (parameter != null && double.TryParse(parameter.ToString(), out double p))
                {
                    baseSize = p;
                }
                double result = baseSize / scale ;

                if (targetType == typeof(Thickness))
                {
                    return new Thickness(result);
                }
                return result;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class PixelSnapper : Decorator
    {
        public PixelSnapper()
        {
            this.SnapsToDevicePixels = true;
            this.UseLayoutRounding = true;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            var size = base.MeasureOverride(constraint);
            return new Size(Math.Ceiling(size.Width), Math.Ceiling(size.Height));
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            var snappedSize = new Size(Math.Ceiling(arrangeSize.Width), Math.Ceiling(arrangeSize.Height));
            base.ArrangeOverride(snappedSize);
            return snappedSize;
        }
    }
    public class NonLinearConverter : IValueConverter
    {
        private const double MaxDataValue = AppConsts.ConverterMaxDataValue;
        private const double MaxSliderValue = AppConsts.ConverterMaxSliderValue;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double dVal)
            {
                return MaxSliderValue * Math.Sqrt(dVal / MaxDataValue);
            }
            if (value is int iVal)
            {
                return MaxSliderValue * Math.Sqrt((double)iVal / MaxDataValue);
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Slider 位置 -> 数据 (平方，恢复数据)
            if (value is double sVal)
            {
                double ratio = sVal / MaxSliderValue;
                return Math.Round(MaxDataValue * ratio * ratio); // 取整，避免小数点
            }
            return 0.0;
        }
    }
    public class NaturalStringComparer : IComparer<string>
    {
        public static readonly NaturalStringComparer Default = new NaturalStringComparer();

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [SuppressUnmanagedCodeSecurity] 
        private static extern int StrCmpLogicalW(string psz1, string psz2);

        public int Compare(string? x, string? y)
        {
            // 处理 null 情况
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            return StrCmpLogicalW(x, y);
        }
    }
    public static class QuickBenchmark
    {
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetPhysicallyInstalledSystemMemory(out long TotalMemoryInKilobytes);

        public static int EstimatePerformanceScore()
        {
            var settings = SettingsManager.Instance.Current;
            DateTime now = DateTime.Now;
            if (settings.PerformanceScore > 0 && (now - settings.LastBenchmarkDate).TotalDays < 30)return settings.PerformanceScore; //30 天内有评分返回缓存值
            double score = 0;
            var sw = Stopwatch.StartNew();
            try
            {
                int coreCount = Environment.ProcessorCount;
                if (coreCount >= 20) score += 2.5;       // i7-13700K, i9, R9 等
                else if (coreCount >= 12) score += 2.0;  // 现代标压 i5/i7, R7
                else if (coreCount >= 8) score += 1.5;   // 主流轻薄本
                else if (coreCount >= 4) score += 0.5;   // 入门级/老旧双核4核以下 0分
                long memKb = 0;
                if (GetPhysicallyInstalledSystemMemory(out memKb))
                {
                    long memGb = memKb / 1024 / 1024;
                    if (memGb >= 30) score += 1.5;       // 32GB及以上
                    else if (memGb >= 15) score += 1.0;  // 16GB
                    else if (memGb >= 7) score += 0.5;   // 8GB// 8GB以下 0分
                }
                long cpuTicks = RunStrictMicroTest();
                if (cpuTicks < 1800) score += 6.0;
                else if (cpuTicks < 2200) score += 5.0;
                else if (cpuTicks < 3000) score += 4.0;
                else if (cpuTicks < 4500) score += 3.0;
                else if (cpuTicks < 6500) score += 2.0;   // 普通办公本区间
                else if (cpuTicks < 9000) score += 1.0; 
                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.PrimaryScreenHeight; // 大于 2K 分辨率 (约360万像素)
                if (screenWidth * screenHeight > 3600000)if (cpuTicks > 4500) score -= 1.5;
                if (score > 10) score = 10;
                if (score < 1) score = 1; // 缓存结果
                settings.PerformanceScore = (int)Math.Round(score);
                settings.LastBenchmarkDate = now;
            }
            catch  {   return 4;   }
            finally
            {
                sw.Stop();
                Debug.WriteLine($"[Benchmark] Ticks: {RunStrictMicroTest()} | Score: {score} | Time: {sw.Elapsed.TotalMilliseconds:F4}ms");
            }
            return (int)Math.Round(score);
        }
        private static long RunStrictMicroTest()
        {
            var sw = Stopwatch.StartNew();
            int result = 0;
            for (int i = 1; i < 150000; i++)  result += (i * 3) ^ (i % 7);
            sw.Stop();
            if (result == 999999) Debug.WriteLine("");
            return sw.ElapsedTicks;
        }
    }
    public class DynamicRangeConverter : IMultiValueConverter
    {
        private const double MinSize = AppConsts.DynamicRangeMinSize;
        private double GetMaxSizeForKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return AppConsts.DynamicRangeDefaultMaxSize; // 默认
         //   
            if (key == "Shape") return AppConsts.DynamicRangeShapeMaxSize; // ★ Shape 工具上限锁死为 24
            if (key == "Pen_Pencil") return AppConsts.DynamicRangePenPencilMaxSize;

            return AppConsts.DynamicRangeDefaultMaxSize; // 其他画笔默认 400
        }

        private string _lastToolKey;
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] == DependencyProperty.UnsetValue)
                return 0.0;

            double thickness = System.Convert.ToDouble(values[0]);
            string toolKey = values[1] as string;
            _lastToolKey = toolKey;

            double maxSize = GetMaxSizeForKey(toolKey);
            double ratio = (thickness - MinSize) / (maxSize - MinSize);

            // 开根号还原线性进度
            double sliderVal = Math.Sqrt(Math.Max(0.0, Math.Min(1.0, ratio)));
      
            return sliderVal;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            if (value is double sliderVal)
            {
                string toolKey = _lastToolKey ?? SettingsManager.Instance.Current.CurrentToolKey;
                double maxSize = GetMaxSizeForKey(toolKey);

                double t = Math.Max(0.0, Math.Min(1.0, sliderVal));
                double result = MinSize + (maxSize - MinSize) * (t * t);
                return new object[] { Math.Round(result), Binding.DoNothing };
            }

            return new object[] { MinSize, Binding.DoNothing };
        }
    }
    public class ScaleToTileRectConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double scale = 1.0;
            if (value is double d) scale = d;
            if (parameter != null && parameter.ToString().Contains("_IsLess"))
            {
                if (double.TryParse(parameter.ToString().Replace("_IsLess", ""), out double threshold))
                {
                    return scale < threshold;
                }
            }

            if (scale < 0.01) scale = 0.01;
            double baseSize = 20.0;
            if (parameter != null && double.TryParse(parameter.ToString(), out double parsedSize))
            {
                baseSize = parsedSize;
            }

            double newSize = baseSize / scale;

            // 返回一个新的 Rect 用于 Viewport
            return new Rect(0, 0, newSize, newSize);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        #region Debug工具


        public static void s<T>(T a) { System.Windows.MessageBox.Show(a.ToString(), "标题", MessageBoxButton.OK, MessageBoxImage.Information); }
        public static void s(){ System.Windows.MessageBox.Show("空messagebox", "标题", MessageBoxButton.OK, MessageBoxImage.Information);}
        public static void msgbox<T>(T a) {System.Windows.MessageBox.Show(a.ToString(), "标题", MessageBoxButton.OK, MessageBoxImage.Information);}
        public static void s2<T>(T a) {Debug.Print(a.ToString()); }
        public static class a
        {
            public static void s(params object[] args)
            {
                string message = string.Join(" ", args);
                Debug.WriteLine(message);
            }
        }
        public class TimeRecorder
        {
            private Stopwatch _stopwatch;

            public void Toggle(bool slient = false)
            {
                if (_stopwatch == null)
                {
                    _stopwatch = Stopwatch.StartNew();
                    //    a.s("计时开始...");
                }
                else if (_stopwatch.IsRunning)
                {
                    _stopwatch.Stop();
                    if (!slient)
                        s($"耗时：{_stopwatch.Elapsed.TotalMilliseconds} 毫秒");
                    else
                        a.s($"耗时：{_stopwatch.Elapsed.TotalMilliseconds} 毫秒"); ;
                }
                else
                {
                    Console.WriteLine("计时器已结束。如需重新开始，请重置状态。");
                }
            }
            public void Reset()
            {
                _stopwatch = null;
            }
        }

        #endregion

        private const double MinZoomReal = AppConsts.ZoomSliderMinReal;  // 10%
        private const double MaxZoomReal = AppConsts.ZoomSliderMaxReal; // 1600%
        private double ZoomToSlider(double realZoom)
        {
            // 越界保护
            if (realZoom < MinZoomReal) realZoom = MinZoomReal;
            if (realZoom > MaxZoomReal) realZoom = MaxZoomReal;
            return 100.0 * Math.Log(realZoom / MinZoomReal) / Math.Log(MaxZoomReal / MinZoomReal);
        }
        private double SliderToZoom(double sliderValue)
        {
            // 公式: y = min * (max/min)^(x/100)
            double percent = sliderValue / 100.0;
            return MinZoomReal * Math.Pow(MaxZoomReal / MinZoomReal, percent);
        }

        

    }
}
