using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

//
//SodiumPaint主程序
//

namespace SodiumPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        #region s

        static void s<T>(T a)
        {
            System.Windows.MessageBox.Show(a.ToString(), "标题", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        static void s(){ System.Windows.MessageBox.Show("空messagebox", "标题", MessageBoxButton.OK, MessageBoxImage.Information);}
        static void msgbox<T>(T a) {System.Windows.MessageBox.Show(a.ToString(), "标题", MessageBoxButton.OK, MessageBoxImage.Information);}
        static void s2<T>(T a) {Debug.Print(a.ToString()); }
        public static class a
        {
            public static void s(params object[] args)
            {
                // 可以根据需要拼接输出格式
                string message = string.Join(" ", args);
                Debug.WriteLine(message);
            }
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////临时测试代码段
        ///








        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    }
}