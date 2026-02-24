using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using TabPaint.Controls;
using static TabPaint.MainWindow;

//
//启动项

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public void CheckFilePathAvailibility(string path)
        {
            if (string.IsNullOrEmpty(path)) _currentFileExists = false;
            if((File.Exists(path)|| System.IO.Directory.Exists(path))&&(!IsVirtualPath(path)))
            {
                _currentFileExists = true;
            }
            else  _currentFileExists = false;
        }
        static public void RestoreWindow(System.Windows.Window window)
        {
            if (window == null) return;

            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                window.Activate();
                window.Focus();
                return;
            }

            if (window.WindowState == WindowState.Minimized)
            {
                ShowWindowAsync(hwnd, SW_RESTORE);
            }
            else
            {
                ShowWindowAsync(hwnd, SW_SHOW);
            }

            window.Activate();
            window.Topmost = true;  // 临时置顶
            window.Topmost = false; // 取消置顶
            window.Focus();

            if (!SetForegroundWindow(hwnd))
            {
                ForceSetForegroundWindow(hwnd);
            }

            SetFocus(hwnd);
        }

        private static void ForceSetForegroundWindow(IntPtr hwnd)
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            uint currentThreadId = GetCurrentThreadId();
            uint foregroundThreadId = foregroundWindow != IntPtr.Zero
                ? GetWindowThreadProcessId(foregroundWindow, IntPtr.Zero)
                : 0;

            bool attached = false;
            try
            {
                if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
                {
                    attached = AttachThreadInput(foregroundThreadId, currentThreadId, true);
                }

                BringWindowToTop(hwnd);
                ShowWindowAsync(hwnd, SW_SHOW);
                SetForegroundWindow(hwnd);
                SetFocus(hwnd);
            }
            finally
            {
                if (attached)
                {
                    AttachThreadInput(foregroundThreadId, currentThreadId, false);
                }
            }
        }

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);
    }
}