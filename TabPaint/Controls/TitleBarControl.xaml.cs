using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TabPaint.Controls
{
    public partial class TitleBarControl : UserControl
    {
        // --- 1. 新增：暴露内部控件给 MainWindow 访问 ---
        public TextBlock TitleTextControl => TitleTextBlock;
        public Button MaxBtn => MaxRestoreButton;

        // --- 原有的路由事件定义 ---
        public static readonly RoutedEvent MinimizeClickEvent = EventManager.RegisterRoutedEvent(
            "MinimizeClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent MaximizeRestoreClickEvent = EventManager.RegisterRoutedEvent(
            "MaximizeRestoreClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent CloseClickEvent = EventManager.RegisterRoutedEvent(
            "CloseClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));

        public event RoutedEventHandler MinimizeClick { add => AddHandler(MinimizeClickEvent, value); remove => RemoveHandler(MinimizeClickEvent, value); }
        public event RoutedEventHandler MaximizeRestoreClick { add => AddHandler(MaximizeRestoreClickEvent, value); remove => RemoveHandler(MaximizeRestoreClickEvent, value); }
        public event RoutedEventHandler CloseClick { add => AddHandler(CloseClickEvent, value); remove => RemoveHandler(CloseClickEvent, value); }

        public TitleBarControl()
        {
            InitializeComponent();
        }

        private void OnMinimizeClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(MinimizeClickEvent));
        private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(MaximizeRestoreClickEvent));
        private void OnCloseClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(CloseClickEvent));

        private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Window parentWindow = Window.GetWindow(this);
            if (parentWindow != null && e.ClickCount != 2) parentWindow.DragMove();
        }
    }
}
