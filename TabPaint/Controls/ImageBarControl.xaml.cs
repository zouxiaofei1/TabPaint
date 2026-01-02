using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using static TabPaint.MainWindow;

namespace TabPaint.Controls
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
    public partial class ImageBarControl : UserControl
    {
        public ImageBarControl()
        {
            InitializeComponent();
        }

        // ==========================================
        // 1. 暴露内部控件给 MainWindow (修复 "当前上下文中不存在名称" 的错误)
        // ==========================================
        public ScrollViewer Scroller => FileTabsScroller;
        public ItemsControl TabList => FileTabList;
        public Slider Slider => PreviewSlider;
        public Button AddButton => LeftAddBtn; // 修复 LeftAddBtn 访问

        // ==========================================
        // 2. 简单的点击事件 (使用路由事件冒泡)
        // ==========================================
        public static readonly RoutedEvent SaveAllClickEvent = EventManager.RegisterRoutedEvent("SaveAllClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler SaveAllClick { add { AddHandler(SaveAllClickEvent, value); } remove { RemoveHandler(SaveAllClickEvent, value); } }
        private void Internal_OnSaveAllClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SaveAllClickEvent, sender));

        public static readonly RoutedEvent ClearUneditedClickEvent = EventManager.RegisterRoutedEvent("ClearUneditedClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler ClearUneditedClick { add { AddHandler(ClearUneditedClickEvent, value); } remove { RemoveHandler(ClearUneditedClickEvent, value); } }
        private void Internal_OnClearUneditedClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ClearUneditedClickEvent, sender));

        public static readonly RoutedEvent DiscardAllClickEvent = EventManager.RegisterRoutedEvent("DiscardAllClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler DiscardAllClick { add { AddHandler(DiscardAllClickEvent, value); } remove { RemoveHandler(DiscardAllClickEvent, value); } }
        private void Internal_OnDiscardAllClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(DiscardAllClickEvent, sender));

        public static readonly RoutedEvent PrependTabClickEvent = EventManager.RegisterRoutedEvent("PrependTabClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler PrependTabClick { add { AddHandler(PrependTabClickEvent, value); } remove { RemoveHandler(PrependTabClickEvent, value); } }
        private void Internal_OnPrependTabClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(PrependTabClickEvent, sender));

        public static readonly RoutedEvent NewTabClickEvent = EventManager.RegisterRoutedEvent("NewTabClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler NewTabClick { add { AddHandler(NewTabClickEvent, value); } remove { RemoveHandler(NewTabClickEvent, value); } }
        private void Internal_OnNewTabClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(NewTabClickEvent, sender));

        // ==========================================
        // 3. 标签与右键菜单事件
        // ==========================================
        // 注意：这里使用通用的路由事件转发
        public static readonly RoutedEvent FileTabClickEvent = EventManager.RegisterRoutedEvent("FileTabClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler FileTabClick { add { AddHandler(FileTabClickEvent, value); } remove { RemoveHandler(FileTabClickEvent, value); } }
        private void Internal_OnFileTabClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(FileTabClickEvent, e.OriginalSource)); // 保持 OriginalSource

        public static readonly RoutedEvent FileTabCloseClickEvent = EventManager.RegisterRoutedEvent("FileTabCloseClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler FileTabCloseClick { add { AddHandler(FileTabCloseClickEvent, value); } remove { RemoveHandler(FileTabCloseClickEvent, value); } }
        private void Internal_OnFileTabCloseClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(FileTabCloseClickEvent, e.OriginalSource));

        // 右键菜单转发
        public event RoutedEventHandler TabCopyClick;
        public event RoutedEventHandler TabCutClick;
        public event RoutedEventHandler TabPasteClick;
        public event RoutedEventHandler TabOpenFolderClick;
        public event RoutedEventHandler TabDeleteClick;
        public event RoutedEventHandler TabFileDeleteClick;

        private void Internal_OnTabCopyClick(object sender, RoutedEventArgs e) => TabCopyClick?.Invoke(sender, e);
        private void Internal_OnTabCutClick(object sender, RoutedEventArgs e) => TabCutClick?.Invoke(sender, e);
        private void Internal_OnTabPasteClick(object sender, RoutedEventArgs e) => TabPasteClick?.Invoke(sender, e);
        private void Internal_OnTabOpenFolderClick(object sender, RoutedEventArgs e) => TabOpenFolderClick?.Invoke(sender, e);
        private void Internal_OnTabDeleteClick(object sender, RoutedEventArgs e) => TabDeleteClick?.Invoke(sender, e);
        private void Internal_OnTabFileDeleteClick(object sender, RoutedEventArgs e) => TabFileDeleteClick?.Invoke(sender, e);

        // ==========================================
        // 4. 复杂事件 (直接使用 C# 事件，避开 DragEventArgs 构造函数问题)
        // ==========================================

        public event MouseButtonEventHandler FileTabPreviewMouseDown;
        private void Internal_OnFileTabPreviewMouseDown(object sender, MouseButtonEventArgs e) => FileTabPreviewMouseDown?.Invoke(sender, e);

        public event MouseEventHandler FileTabPreviewMouseMove;
        private void Internal_OnFileTabPreviewMouseMove(object sender, MouseEventArgs e) => FileTabPreviewMouseMove?.Invoke(sender, e);

        public event DragEventHandler FileTabDrop;
        private void Internal_OnFileTabDrop(object sender, DragEventArgs e) => FileTabDrop?.Invoke(sender, e);

        public event DragEventHandler FileTabReorderDragOver;
        private void Internal_OnFileTabReorderDragOver(object sender, DragEventArgs e) => FileTabReorderDragOver?.Invoke(sender, e);

        // 滚动条与滑块
        public event MouseWheelEventHandler FileTabsWheelScroll;
        private void Internal_OnFileTabsWheelScroll(object sender, MouseWheelEventArgs e) => FileTabsWheelScroll?.Invoke(sender, e);

        public event ScrollChangedEventHandler FileTabsScrollChanged;
        private void Internal_OnFileTabsScrollChanged(object sender, ScrollChangedEventArgs e) => FileTabsScrollChanged?.Invoke(sender, e);

        public event RoutedPropertyChangedEventHandler<double> PreviewSliderValueChanged;
        private void Internal_PreviewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => PreviewSliderValueChanged?.Invoke(sender, e);

        public event MouseWheelEventHandler SliderPreviewMouseWheel;
        private void Internal_Slider_PreviewMouseWheel(object sender, MouseWheelEventArgs e) => SliderPreviewMouseWheel?.Invoke(sender, e);

        // 防止窗口震动
        private void Internal_ScrollViewer_ManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }


        public event DragEventHandler FileTabLeave;
        // 1. DragOver: 计算位置并显示竖线
           private void Internal_OnFileTabDragLeave(object sender, DragEventArgs e) => FileTabLeave?.Invoke(sender, e);

        public static readonly DependencyProperty IsViewModeProperty =
                   DependencyProperty.Register("IsViewMode", typeof(bool), typeof(ImageBarControl), new PropertyMetadata(false));

        public bool IsViewMode
        {
            get { return (bool)GetValue(IsViewModeProperty); }
            set { SetValue(IsViewModeProperty, value); }
        }

        // 2. IsPinned: 是否锁定展开 (可以通过快捷键或右键菜单触发)
        public static readonly DependencyProperty IsPinnedProperty =
            DependencyProperty.Register("IsPinned", typeof(bool), typeof(ImageBarControl), new PropertyMetadata(false));

        public bool IsPinned
        {
            get { return (bool)GetValue(IsPinnedProperty); }
            set { SetValue(IsPinnedProperty, value); }
        }

        // 公开方法供外部快捷键调用 (比如按下 Ctrl+T 锁定/解锁)
        public void TogglePin()
        {
            IsPinned = !IsPinned;
        }


    }
}
