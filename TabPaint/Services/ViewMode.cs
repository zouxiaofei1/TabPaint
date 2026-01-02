
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TabPaint.Controls;
using static TabPaint.MainWindow;

//
//看图模式
//

namespace TabPaint
{
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                // 如果是 True，则隐藏 (Collapsed)；如果是 False，则显示 (Visible)
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v)
            {
                return v != Visibility.Visible;
            }
            return false;
        }
    }
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private long _lastModeSwitchTick = 0;
        private const long ModeSwitchCooldown = 200 * 10000;

        private void TriggerModeChange()
        {
            long currentTick = DateTime.Now.Ticks;
            if (currentTick - _lastModeSwitchTick < ModeSwitchCooldown)return;
            _lastModeSwitchTick = currentTick;
            IsViewMode = !IsViewMode;
            OnModeChanged(IsViewMode);
        }

        private void OnModeChanged(bool isView)
        {
            ShowToast(isView ? "进入看图模式" : "进入画图模式");

            if (isView)
            {
              SetPenResizeBarVisibility(false);
                _router.CleanUpSelectionandShape();
                if(_router.CurrentTool is TextTool textTool)
                {
                    textTool.Cleanup(_ctx);
                }
                if (_router.CurrentTool is PenTool penTool)
                {
                    penTool.StopDrawing(_ctx);
                }
                MainImageBar.MainContainer.Height = 5;
            }
            else
            {
                SetPenResizeBarVisibility((_router.CurrentTool is PenTool && _ctx.PenStyle != BrushStyle.Pencil) || _router.CurrentTool is ShapeTool);
                MainImageBar.MainContainer.Height = 100;
            }
            AppTitleBar.UpdateModeIcon(IsViewMode);
            _canvasResizer.UpdateUI();
        }
        private void OnTitleBarModeSwitch(object sender, RoutedEventArgs e)
        {
            // 1. 切换布尔值
    TriggerModeChange();
        }

        private static void OnIsViewModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var window = (MainWindow)d;
            bool isView = (bool)e.NewValue;
            // 这里可以处理模式切换时的额外逻辑，例如：
            // 1. 清除当前的选区
            // 2. 强制设焦点
            // 3. 记录日志
            if (isView)
            {
                // 比如: window.CancelCurrentOperation();
            }
        }

    }
}
