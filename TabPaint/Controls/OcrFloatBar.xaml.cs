using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TabPaint.Controls
{
    public partial class OcrFloatBar : UserControl
    {
        public OcrFloatBar()
        {
            InitializeComponent();
            CopyAllBtn.Click += (_, e) => CopyAllClick?.Invoke(this, e);
            ConfirmBtn.Click += (_, e) => ConfirmClick?.Invoke(this, e);
        }

        public event RoutedEventHandler? CopyAllClick;
        public event RoutedEventHandler? ConfirmClick;

        // 拖动事件，由宿主（MainWindow）订阅处理
        public event MouseButtonEventHandler? BarMouseDown;
        public event MouseEventHandler? BarMouseMove;
        public event MouseButtonEventHandler? BarMouseUp;

        private void RootBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 如果点击的是按钮，不触发拖动
            if (e.OriginalSource is Button) return;
            BarMouseDown?.Invoke(this, e);
        }

        private void RootBorder_MouseMove(object sender, MouseEventArgs e)
        {
            BarMouseMove?.Invoke(this, e);
        }

        private void RootBorder_MouseUp(object sender, MouseButtonEventArgs e)
        {
            BarMouseUp?.Invoke(this, e);
        }
    }
}
