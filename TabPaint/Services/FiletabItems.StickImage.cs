using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TabPaint.Windows;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {

        private void OnTabStickImageClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is FileTabItem tabItem)
            {
                StickTabImage(tabItem);
            }
            // 兼容性处理：如果 ImageBarControl 里的 Invoke 传的是 e.OriginalSource
            else if (e.OriginalSource is MenuItem originItem && originItem.Tag is FileTabItem originTab)
            {
                StickTabImage(originTab);
            }
        }
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out Win32Point lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        };
        private void StickTabImage(FileTabItem tabItem)
        {
            if (tabItem == null) return;

            try
            {
                var bitmap = GetHighResImageForTab(tabItem);

                if (bitmap != null)
                {
                    Win32Point p;
                    GetCursorPos(out p);
                    var mouseX = p.X;
                    var mouseY = p.Y;

                    string? sourceFilePath = null;
                    if (!string.IsNullOrWhiteSpace(tabItem.FilePath) && File.Exists(tabItem.FilePath))
                    {
                        sourceFilePath = tabItem.FilePath;
                    }

                    StickyWindow.CreateAndShowStickyWindow(bitmap, new System.Windows.Point(mouseX, mouseY), sourceFilePath);
                }
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_StickImageFailed"), ex.Message), ex);
            }
        }
    }
}
