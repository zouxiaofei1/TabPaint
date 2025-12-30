using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Forms;
//
//TabPaint主程序
//

namespace TabPaint
{
    public class NonLinearRangeConverter : IValueConverter
    {
        // 最小粗细
        private const double MinSize = 1.0;
        // 最大粗细
        private const double MaxSize = 400.0;

        // ConvertBack: Slider (0~1) -> 实际粗细 (1~400)
        // 公式：y = Min + (Max - Min) * x^2
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double sliderVal)
            {
                // 确保 sliderVal 在 0~1 之间
                double t = Math.Max(0.0, Math.Min(1.0, sliderVal));

                // 使用平方曲线 (t * t) 使得小数值部分调节更细腻
                double result = MinSize + (MaxSize - MinSize) * (t * t);

                return Math.Round(result); // 取整，因为像素通常是整数
            }
            return MinSize;
        }

        // Convert: 实际粗细 (1~400) -> Slider (0~1)
        // 公式：x = Sqrt((y - Min) / (Max - Min))
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double thickness || value is int || value is float)
            {
                double v = System.Convert.ToDouble(value);

                // 反向计算比例
                double ratio = (v - MinSize) / (MaxSize - MinSize);

                // 开根号还原线性进度
                double sliderVal = Math.Sqrt(Math.Max(0.0, ratio));

                return sliderVal;
            }
            return 0.0;
        }
    }
    public class ScaleToTileRectConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double scale = 1.0;
            if (value is double d) scale = d;

            // 防止除以0或极小值
            if (scale < 0.01) scale = 0.01;

            // 参数传入基础格子大小 (例如 20)
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
        #region s

        public static void s<T>(T a)
        {
            System.Windows.MessageBox.Show(a.ToString(), "标题", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        public static void s(){ System.Windows.MessageBox.Show("空messagebox", "标题", MessageBoxButton.OK, MessageBoxImage.Information);}
        public static void msgbox<T>(T a) {System.Windows.MessageBox.Show(a.ToString(), "标题", MessageBoxButton.OK, MessageBoxImage.Information);}
        public static void s2<T>(T a) {Debug.Print(a.ToString()); }
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
   
        public class TimeRecorder
        {
            private Stopwatch _stopwatch;

            public void Toggle()
            {
                // 第一次调用：Stopwatch 为空，开始计时
                if (_stopwatch == null)
                {
                    _stopwatch = Stopwatch.StartNew();
                    a.s("计时开始...");
                }
                // 第二次调用：Stopwatch 正在运行，停止并打印耗时
                else if (_stopwatch.IsRunning)
                {
                    _stopwatch.Stop();
                    a.s($"耗时：{_stopwatch.Elapsed.TotalMilliseconds} 毫秒");

                    // 可选：如果希望第三次调用重新开始，可以重置
                    // _stopwatch = null; 
                }
                else
                {
                    Console.WriteLine("计时器已结束。如需重新开始，请重置状态。");
                }
            }

            // 重置方法（可选）
            public void Reset()
            {
                _stopwatch = null;
            }
        }








        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    }
}