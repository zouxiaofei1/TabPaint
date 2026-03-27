using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using TabPaint.UIHandlers;
using TabPaint.Windows;

namespace TabPaint.Services
{
    public static class TrayIconService
    {
        private const int WM_APP = 0x8000;
        private const int WM_TRAYICON = WM_APP + 0x173;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_CONTEXTMENU = 0x007B;

        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_MODIFY = 0x00000001;
        private const uint NIM_DELETE = 0x00000002;
        private const uint NIM_SETVERSION = 0x00000004;

        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;

        private const uint NOTIFYICON_VERSION_4 = 4;

        private static HwndSource? _messageWindow;
        private static TrayMenuWindow? _menuWindow;
        private static bool _initialized;
        private static bool _iconAdded;
        private static IntPtr _iconHandle = IntPtr.Zero;

        private static NOTIFYICONDATA _notifyData;

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            Initialize();
        }

        public static void Initialize()
        {
            if (_initialized) return;

            var p = new HwndSourceParameters("TabPaintTraySink")
            {
                Width = 0,
                Height = 0,
                WindowStyle = 0,
                ParentWindow = new IntPtr(-3) // HWND_MESSAGE
            };

            _messageWindow = new HwndSource(p);
            _messageWindow.AddHook(WndProc);

            _iconHandle = ResolveTrayIconHandle();

            _notifyData = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _messageWindow.Handle,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _iconHandle,
                szTip = "TabPaint"
            };

            _initialized = true;
        }

        public static void UpdateVisibility()
        {
            EnsureInitialized();
            if (!_initialized || _messageWindow == null) return;

            bool hasVisibleSticky = System.Windows.Application.Current.Windows.OfType<StickyWindow>().Any(w => w.IsVisible);
            bool hasVisibleMain = System.Windows.Application.Current.Windows.OfType<MainWindow>().Any(w => w.IsVisible);
            bool shouldShow = hasVisibleSticky && !hasVisibleMain;

            if (shouldShow && !_iconAdded)
            {
                Shell_NotifyIcon(NIM_ADD, ref _notifyData);
                _notifyData.uTimeoutOrVersion = NOTIFYICON_VERSION_4;
                Shell_NotifyIcon(NIM_SETVERSION, ref _notifyData);
                _iconAdded = true;
            }
            else if (!shouldShow && _iconAdded)
            {
                Shell_NotifyIcon(NIM_DELETE, ref _notifyData);
                _iconAdded = false;
            }

            if (!shouldShow && _menuWindow != null)
            {
                _menuWindow.Close();
                _menuWindow = null;
            }
        }

        public static void Dispose()
        {
            if (!_initialized) return;

            try
            {
                if (_menuWindow != null)
                {
                    _menuWindow.Close();
                    _menuWindow = null;
                }

                if (_iconAdded)
                {
                    Shell_NotifyIcon(NIM_DELETE, ref _notifyData);
                    _iconAdded = false;
                }

                if (_messageWindow != null)
                {
                    _messageWindow.RemoveHook(WndProc);
                    _messageWindow.Dispose();
                    _messageWindow = null;
                }

                if (_iconHandle != IntPtr.Zero)
                {
                    DestroyIcon(_iconHandle);
                    _iconHandle = IntPtr.Zero;
                }
            }
            finally
            {
                _initialized = false;
            }
        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_TRAYICON)
            {
                // NOTIFYICON_VERSION_4 会把事件码放在 lParam 的低 16 位，高位包含 icon id。
                int eventId = LowWord(lParam);
                if (eventId == WM_LBUTTONDBLCLK)
                {
                    RestoreOrCreateMainWindow();
                    handled = true;
                }
                else if (eventId == WM_RBUTTONUP || eventId == WM_CONTEXTMENU)
                {
                    ShowTrayMenu();
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        private static int LowWord(IntPtr value)
        {
            return (ushort)(value.ToInt64() & 0xFFFF);
        }

        private static void ShowTrayMenu()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (_menuWindow != null)
                {
                    _menuWindow.Close();
                    _menuWindow = null;
                }
                if (_messageWindow != null)
                {
                    SetForegroundWindow(_messageWindow.Handle);
                }
                var menu = new TrayMenuWindow();
                menu.OpenRequested += (_, _) => RestoreOrCreateMainWindow();
                menu.ExitRequested += (_, _) => App.GlobalExit();
                menu.Closed += (_, _) =>
                {
                    if (ReferenceEquals(_menuWindow, menu)) _menuWindow = null;
                };

                menu.ShowAtCursor();
                _menuWindow = menu;
            });
        }
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private static void RestoreOrCreateMainWindow()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var target = System.Windows.Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();

                if (target == null)
                {
                    target = new MainWindow(string.Empty, false, loadSession: false);
                    target.Show();
                }
                else if (!target.IsVisible)
                {
                    target.Show();
                }

                MainWindow.RestoreWindow(target);
                target.Activate();
                target.Focus();

                UpdateVisibility();
            });
        }

        private static IntPtr ResolveTrayIconHandle()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (!string.IsNullOrEmpty(exePath))
                {
                    using var icon = Icon.ExtractAssociatedIcon(exePath);
                    if (icon != null)
                    {
                        IntPtr copied = CopyIcon(icon.Handle);
                        if (copied != IntPtr.Zero) return copied;
                    }
                }
            }
            catch
            {
            }

            using var fallback = SystemIcons.Application;
            return CopyIcon(fallback.Handle);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll")]
        private static extern IntPtr CopyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}