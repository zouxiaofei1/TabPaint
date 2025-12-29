using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace TabPaint.Controls
{
    public partial class StatusBarControl : UserControl
    {
        // 1. 定义路由事件
        public static readonly RoutedEvent ClipboardMonitorClickEvent = EventManager.RegisterRoutedEvent(
            "ClipboardMonitorClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(StatusBarControl));

        public static readonly RoutedEvent FitToWindowClickEvent = EventManager.RegisterRoutedEvent(
            "FitToWindowClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(StatusBarControl));

        public static readonly RoutedEvent ZoomOutClickEvent = EventManager.RegisterRoutedEvent(
            "ZoomOutClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(StatusBarControl));

        public static readonly RoutedEvent ZoomInClickEvent = EventManager.RegisterRoutedEvent(
            "ZoomInClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(StatusBarControl));

        public static readonly RoutedEvent ZoomSelectionChangedEvent = EventManager.RegisterRoutedEvent(
            "ZoomSelectionChanged", RoutingStrategy.Bubble, typeof(SelectionChangedEventHandler), typeof(StatusBarControl));

        // 2. 暴露事件
        public event RoutedEventHandler ClipboardMonitorClick
        {
            add { AddHandler(ClipboardMonitorClickEvent, value); }
            remove { RemoveHandler(ClipboardMonitorClickEvent, value); }
        }
        public event RoutedEventHandler FitToWindowClick
        {
            add { AddHandler(FitToWindowClickEvent, value); }
            remove { RemoveHandler(FitToWindowClickEvent, value); }
        }
        public event RoutedEventHandler ZoomOutClick
        {
            add { AddHandler(ZoomOutClickEvent, value); }
            remove { RemoveHandler(ZoomOutClickEvent, value); }
        }
        public event RoutedEventHandler ZoomInClick
        {
            add { AddHandler(ZoomInClickEvent, value); }
            remove { RemoveHandler(ZoomInClickEvent, value); }
        }
        public event SelectionChangedEventHandler ZoomSelectionChanged
        {
            add { AddHandler(ZoomSelectionChangedEvent, value); }
            remove { RemoveHandler(ZoomSelectionChangedEvent, value); }
        }

        // 3. 暴露内部控件 (为了保持 MainWindow 代码兼容性)
        public ComboBox ZoomComboBox => ZoomMenu;
        public ToggleButton ClipboardToggle => ClipboardMonitorToggle;
        public Slider ZoomSliderControl => ZoomSlider;


        public StatusBarControl()
        {
            InitializeComponent();
        }

        // 4. 内部事件触发逻辑
        private void OnClipboardMonitorToggleClick(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ClipboardMonitorClickEvent));
        }

        private void OnFitToWindowClick(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(FitToWindowClickEvent));
        }

        private void OnZoomOutClick(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ZoomOutClickEvent));
        }

        private void OnZoomInClick(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ZoomInClickEvent));
        }

        private void OnZoomMenuSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 重新包装 SelectionChangedEventArgs 以保留选择信息
            var newEventArgs = new SelectionChangedEventArgs(ZoomSelectionChangedEvent, e.RemovedItems, e.AddedItems);
            RaiseEvent(newEventArgs);
        }
    }
}
