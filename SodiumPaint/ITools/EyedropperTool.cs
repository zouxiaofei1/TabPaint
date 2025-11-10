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
        public class EyedropperTool : ToolBase
        {
            public override string Name => "Eyedropper";
            public Stream cursorStream = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Resources/Cursors/Eyedropper.cur")
            ).Stream;
            public override System.Windows.Input.Cursor Cursor => new System.Windows.Input.Cursor(cursorStream);

            public override void OnPointerDown(ToolContext ctx, Point viewPos)
            {
                a.s("EyedropperTool");
                var px = ctx.ToPixel(viewPos);
                ctx.PenColor = ctx.Surface.GetPixel((int)px.X, (int)px.Y);
                ((MainWindow)System.Windows.Application.Current.MainWindow).UpdateForegroundButtonColor(ctx.PenColor);
                ((MainWindow)System.Windows.Application.Current.MainWindow)._router.SetTool(((MainWindow)System.Windows.Application.Current.MainWindow).LastTool);
            }
        }
    }
}