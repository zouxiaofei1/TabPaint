using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
namespace SodiumPaint
{
    public static class MicaAcrylicManager
    {

        static void s<T>(T a)
        {
            System.Windows.MessageBox.Show(a.ToString(), "标题", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        static void msgbox<T>(T a)
        {
            System.Windows.MessageBox.Show(a.ToString(), "标题", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        static void s2<T>(T a)
        {
            Debug.Print(a.ToString());
        }


        // ===== DWM API（Win11 Mica） =====
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, ref int pvAttribute, int cbAttribute);

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

        // ===== Win10 Acrylic API =====
        [StructLayout(LayoutKind.Sequential)]
        private struct ACCENT_POLICY
        {
            public int AccentState;
            public int AccentFlags;
            public int GradientColor;
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

        /// <summary>
        /// 自动检测系统并启用 Mica（Win11）或 Acrylic（Win10）。
        /// </summary>
        public static void ApplyEffect(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;

            if (IsWin11())
            {
               
                EnableMica(hwnd);
            }
            else if (IsWin10OrLater())
            {
                EnableAcrylic(hwnd);
            }
            else
            {
                // 其他平台使用普通背景
            }
        }

        /// <summary>
        /// 启用 Win11 Mica 效果
        /// </summary>
        private static void EnableMica(IntPtr hwnd)
        {
           // var hwnd = new WindowInteropHelper(this).Handle;
            int cornerPref = 2; // 2 = rounded
            DwmSetWindowAttribute(hwnd, (DWMWINDOWATTRIBUTE)33, ref cornerPref, sizeof(int)); // DWMWA_WINDOW_CORNER_PREFERENCE

            int backdropType = (int)DWMSBT.DWMSBT_MAINWINDOW;
            DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
        }

        /// <summary>
        /// 启用 Win10 Acrylic（透明模糊）
        /// </summary>
        private static void EnableAcrylic(IntPtr hwnd)
        {
            var accent = new ACCENT_POLICY
            {
                AccentState = (int)AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                AccentFlags = 2,
                GradientColor = (150 << 24) | (255 << 16) | (255 << 8) | 255 // 0xAARRGGBB
            };

            int accentSize = Marshal.SizeOf(accent);
            IntPtr accentPtr = Marshal.AllocHGlobal(accentSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WINDOWCOMPOSITIONATTRIBUTE_DATA
            {
                Attribute = 19, // WCA_ACCENT_POLICY
                SizeOfData = accentSize,
                Data = accentPtr
            };

            SetWindowCompositionAttribute(hwnd, ref data);
            Marshal.FreeHGlobal(accentPtr);
        }

        private static bool IsWin11()
        {
            // 粗略判断：Win11 Version >= 22000
            var version = Environment.OSVersion.Version.Build;
            return version >= 22000;
        }

        private static bool IsWin10OrLater()
        {
            var version = Environment.OSVersion.Version.Major;
            return version >= 10;
        }
    }
}
