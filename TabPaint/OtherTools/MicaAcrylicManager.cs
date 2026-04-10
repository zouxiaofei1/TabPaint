using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

//
//复制管理mica和亚克力特效的js
//

namespace TabPaint
{

    public static class MicaAcrylicManager
    {
        private static readonly Dictionary<Window, DispatcherTimer> _refreshTimers = new();
        private static readonly HashSet<Window> _initializedWindows = new();

        // ===== DWM API（Win11 Mica） =====
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, ref int pvAttribute, int cbAttribute);
        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;

            public MARGINS(int allMargins)
            {
                cxLeftWidth = allMargins;
                cxRightWidth = allMargins;
                cyTopHeight = allMargins;
                cyBottomHeight = allMargins;
            }
        }
        private enum DWMWINDOWATTRIBUTE : int
        {
            DWMWA_SYSTEMBACKDROP_TYPE2 = 33,
            DWMWA_SYSTEMBACKDROP_TYPE = 38
        }

        private enum DWMSBT : int
        {
            DWMSBT_AUTO = 0,
            DWMSBT_NONE = 1,
            DWMSBT_MAINWINDOW = 2, // Mica
            DWMSBT_TRANSIENTWINDOW = 3,
            DWMSBT_TABBEDWINDOW = 4 // Acrylic
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct ACCENT_POLICY
        {
            public int AccentState;
            public int AccentFlags;
            public uint GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWCOMPOSITIONATTRIBUTE_DATA
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        private enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4 // Windows 10 Acrylic
        }

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WINDOWCOMPOSITIONATTRIBUTE_DATA data);

        public static void ApplyEffect(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;

            bool forceWin10Style = SettingsManager.Instance.Current.UseWin10StyleOnWin11;

            if (IsWin11() && !forceWin10Style)
            {
                EnableMica(hwnd, window);
                //RemoveWin10Style(window);
            }
            else
            {
                if (IsWin11() && forceWin10Style)
                {
                    DisableMica(hwnd, window);
                }

                // 如果是主窗口且是 Win10，我们保持 AllowsTransparency 逻辑
                if (window is MainWindow)
                {
                    window.Background = Brushes.Transparent;
                }
                ApplyFallbackBackground(window);
            }
        }

        public static void DisableEffect(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;

            if (IsWin11())
            {
                DisableMica(hwnd, window);
            }

            // 回退到普通背景
            window.Background = Application.Current.FindResource("WindowBackgroundBrush") as Brush;
        }
        private static void ApplyFallbackBackground(Window window)
        {
            bool isDark = ThemeManager.CurrentAppliedTheme == AppTheme.Dark;
            Brush sampledBrush = DesktopBackdropSampler.CreateBackdropBrushForWindow(window, isDark);

            // 壁纸采样失败时回退为静态渐变，确保稳定可用。
            Brush backgroundBrush = sampledBrush ?? CreateGradientFallback(isDark);

            // 如果是 MainWindow，我们应用背景到 WindowRootBorder 以支持透明阴影
            if (window is MainWindow mw)
            {
                var border = mw.FindName("WindowRootBorder") as Border;
                if (border != null)
                {
                    border.Background = backgroundBrush;
                }
            }
            else
            {
                window.Background = backgroundBrush;
            }

            if (sampledBrush == null)
            {
                // 叠加噪点纹理（在 MainWindow 中，LayoutRoot 是更好的选择）
                Grid rootGrid = null;
                if (window is MainWindow mw2) rootGrid = mw2.FindName("LayoutRoot") as Grid;
                if (rootGrid == null && window.Content is Grid g) rootGrid = g;

                if (rootGrid != null)
                {
                    const string NOISE_TAG = "Win10NoiseOverlay";
                    var existing = rootGrid.Children.OfType<System.Windows.Shapes.Rectangle>()
                        .FirstOrDefault(r => r.Tag as string == NOISE_TAG);

                    if (existing != null)
                    {
                        existing.Fill = NoiseTextureGenerator.CreateNoiseBrush(isDark);
                    }
                    else
                    {
                        var noiseRect = new System.Windows.Shapes.Rectangle
                        {
                            Tag = NOISE_TAG,
                            Fill = NoiseTextureGenerator.CreateNoiseBrush(isDark),
                            IsHitTestVisible = false,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Stretch,
                        };
                        Panel.SetZIndex(noiseRect, -1);
                        rootGrid.Children.Insert(0, noiseRect);
                    }
                }
            }
            AttachRefreshHandlers(window);
        }

        private static void AttachRefreshHandlers(Window window)
        {
            if (_initializedWindows.Contains(window)) return;

            window.Activated += (s, e) => ApplyFallbackBackground(window);
            window.Deactivated += (s, e) => ApplyFallbackBackground(window);
            window.Closed += (s, e) => _initializedWindows.Remove(window);

            _initializedWindows.Add(window);
        }

        private static Brush CreateGradientFallback(bool isDark)
        {
            LinearGradientBrush gradient;
            if (isDark)
            {
                gradient = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(0x20, 0x20, 0x20), 0.0),
                        new GradientStop(Color.FromRgb(0x1C, 0x1E, 0x24), 0.5),
                        new GradientStop(Color.FromRgb(0x1A, 0x1A, 0x22), 1.0),
                    }
                };
            }
            else
            {
                gradient = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(0xF9, 0xF9, 0xF9), 0.0),
                        new GradientStop(Color.FromRgb(0xF3, 0xF3, 0xF8), 0.5),
                        new GradientStop(Color.FromRgb(0xEF, 0xEF, 0xF4), 1.0),
                    }
                };
            }

            gradient.Freeze();
            return gradient;
        }



        public static void DisableMica(IntPtr hwnd, Window window)
        {

            int backdropType = (int)DWMSBT.DWMSBT_NONE;
            DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
        }
        private static void EnableMica(IntPtr hwnd, Window window)
        {
            window.Background = Brushes.Transparent;

            int cornerPref = AppConsts.DwmCornerPreferenceRounded; // 圆角
            DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE2, ref cornerPref, sizeof(int));

            int backdropType = (int)DWMSBT.DWMSBT_MAINWINDOW;
            DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
        }
        [DllImport("dwmapi.dll")]
        private static extern int DwmIsCompositionEnabled(out bool enabled);

        public static bool IsWin11()
        {// 粗略判断：Win11 Version >= 22000
            var version = Environment.OSVersion.Version.Build;
            return version >= AppConsts.Windows11BuildThreshold;
        }

    }
}
