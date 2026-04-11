//
//ImageBarControl.xaml.cs
//图片标签栏控件，负责显示已打开的图片缩略图、标签切换、关闭以及拖拽排序等交互。
//
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop; // 用于 HwndSource
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using XamlAnimatedGif;
using TabPaint;
using static TabPaint.MainWindow;

namespace TabPaint.Controls
{
   
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)  return b ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v) return v != Visibility.Visible;
            return false;
        }
    }
    public partial class ImageBarControl : UserControl
    {
        private Window _hostWindow;
        private const int WM_MOUSEHWHEEL = AppConsts.WM_MOUSEHWHEEL;

        public ImageBarControl()
        {
            InitializeComponent();
            this.Loaded += ImageBarControl_Loaded;
            this.Unloaded += ImageBarControl_Unloaded;
            this.SizeChanged += (s, e) => UpdateTabWidth();
        }
        private void Internal_OnTabMouseEnter(object sender, MouseEventArgs e)
        {
            if (!IsCompactMode) return;

            var element = sender as FrameworkElement;
            if (element == null) return;
            var tabData = element.DataContext as FileTabItem;
            if (tabData != null && tabData.IsSelected)
            {
                FilePreview.ClosePreview();
                return;
            }

            string filePath = tabData?.FilePath;
            string backupPath = tabData?.BackupPath;
            bool hasBackup = !string.IsNullOrEmpty(backupPath) && File.Exists(backupPath);
            string effectivePath = hasBackup ? backupPath : filePath;

            if (!string.IsNullOrEmpty(effectivePath))
            {
                FilePreview.ShowPreview(effectivePath, element, PlacementMode.Bottom);
            }
        }
        private void Internal_OnTabMouseLeave(object sender, MouseEventArgs e)
        {
            FilePreview.ClosePreview();
        }

        public void ClosePopupAndReset()
        {
            FilePreview.ClosePreview();
        }

        private void ImageBarControl_Loaded(object sender, RoutedEventArgs e)
        {
            var mw = MainWindow.GetCurrentInstance();
            if (mw != null && mw.FileTabs != null)
            {
                mw.FileTabs.CollectionChanged -= FileTabs_CollectionChanged;
                mw.FileTabs.CollectionChanged += FileTabs_CollectionChanged;
            }

            var window = Window.GetWindow(this);
            if (window != null)
            {
                if (!ReferenceEquals(_hostWindow, window))
                {
                    if (_hostWindow != null) _hostWindow.Deactivated -= HostWindow_Deactivated;
                    _hostWindow = window;
                    _hostWindow.Deactivated += HostWindow_Deactivated;
                }

                var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
                source?.AddHook(WndProc);
            }
            UpdateTabWidth();
        }

        private void FileTabs_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateTabWidth();
        }
        private void ImageBarControl_Unloaded(object sender, RoutedEventArgs e)
        {
            var mw = MainWindow.GetCurrentInstance();
            if (mw != null && mw.FileTabs != null)
            {
                mw.FileTabs.CollectionChanged -= FileTabs_CollectionChanged;
            }

            if (_hostWindow != null)
            {
                _hostWindow.Deactivated -= HostWindow_Deactivated;
                _hostWindow = null;
            }

            var window = Window.GetWindow(this);
            if (window != null)
            {
                IntPtr handle = new WindowInteropHelper(window).Handle;
                if (handle != IntPtr.Zero)
                {
                    var source = HwndSource.FromHwnd(handle);
                    source?.RemoveHook(WndProc);
                }
            }
        }

        private void HostWindow_Deactivated(object sender, EventArgs e)
        {
            ClosePopupAndReset();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEHWHEEL && IsMouseOverControl(FileTabsScroller))
            {
                short tilt = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                if (tilt != 0)
                {
                    double scrollAmount = tilt * AppConsts.WheelScrollFactor;
                    FileTabsScroller.ScrollToHorizontalOffset(FileTabsScroller.HorizontalOffset + scrollAmount);
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }
        private bool IsMouseOverControl(UIElement control)
        {
            if (control == null || !control.IsVisible) return false;

            var mousePos = Mouse.GetPosition(control);
            var bounds = new Rect(0, 0, control.RenderSize.Width, control.RenderSize.Height);
            return bounds.Contains(mousePos);
        }
        public ScrollViewer Scroller => FileTabsScroller;
        public ItemsControl TabList => FileTabList;
        public Slider Slider => PreviewSlider;
        public Button AddButton => LeftAddBtn; 

        public static readonly RoutedEvent SaveAllClickEvent = EventManager.RegisterRoutedEvent("SaveAllClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler SaveAllClick { add { AddHandler(SaveAllClickEvent, value); } remove { RemoveHandler(SaveAllClickEvent, value); } }
        private void Internal_OnSaveAllClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SaveAllClickEvent, sender));
        public event RoutedEventHandler TabStickImageClick;
        private void Internal_OnTabStickImageClick(object sender, RoutedEventArgs e)
           => TabStickImageClick?.Invoke(sender, e);

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
        public event RoutedEventHandler TabCloseOthersClick;
        public event RoutedEventHandler TabMoveToNewWindowClick;
        public event RoutedEventHandler TabNewTabRightClick;
        public event RoutedEventHandler TabFileDeleteClick;

        private void Internal_OnTabCopyClick(object sender, RoutedEventArgs e) => TabCopyClick?.Invoke(sender, e);
        private void Internal_OnTabCutClick(object sender, RoutedEventArgs e) => TabCutClick?.Invoke(sender, e);
        private void Internal_OnTabPasteClick(object sender, RoutedEventArgs e) => TabPasteClick?.Invoke(sender, e);
        private void Internal_OnTabOpenFolderClick(object sender, RoutedEventArgs e) => TabOpenFolderClick?.Invoke(sender, e);
        private void Internal_OnTabDeleteClick(object sender, RoutedEventArgs e) => TabDeleteClick?.Invoke(sender, e);
        private void Internal_OnTabCloseOthersClick(object sender, RoutedEventArgs e) => TabCloseOthersClick?.Invoke(sender, e);
        private void Internal_OnTabMoveToNewWindowClick(object sender, RoutedEventArgs e) => TabMoveToNewWindowClick?.Invoke(sender, e);
        private void Internal_OnTabNewTabRightClick(object sender, RoutedEventArgs e) => TabNewTabRightClick?.Invoke(sender, e);
        private void Internal_OnTabFileDeleteClick(object sender, RoutedEventArgs e) => TabFileDeleteClick?.Invoke(sender, e);

        public event MouseButtonEventHandler FileTabPreviewMouseDown;
        private void Internal_OnFileTabPreviewMouseDown(object sender, MouseButtonEventArgs e) => FileTabPreviewMouseDown?.Invoke(sender, e);

        public event MouseEventHandler FileTabPreviewMouseMove;
        private void Internal_OnFileTabPreviewMouseMove(object sender, MouseEventArgs e) => FileTabPreviewMouseMove?.Invoke(sender, e);

        public event DragEventHandler FileTabDrop;
        private void Internal_OnFileTabDrop(object sender, DragEventArgs e) => FileTabDrop?.Invoke(sender, e);
        public event MouseWheelEventHandler FileTabsWheelScroll;
        public event DragEventHandler FileTabReorderDragOver;
        private void Internal_OnFileTabReorderDragOver(object sender, DragEventArgs e) => FileTabReorderDragOver?.Invoke(sender, e);
        private void Internal_OnFileTabsWheelScroll(object sender, MouseWheelEventArgs e)
        {
            var scroller = sender as ScrollViewer;
            if (scroller == null) return;

            // 1. 正常滚轮 -> 纵向转横向
            // 2. 按住 Shift -> 强制横向 (即便系统没转)
            if (e.Delta != 0)
            {
                double scrollAmount = e.Delta * AppConsts.WheelScrollFactor;
                scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset - scrollAmount);
                e.Handled = true;
            }
        }

        public event ScrollChangedEventHandler FileTabsScrollChanged;
        private void Internal_OnFileTabsScrollChanged(object sender, ScrollChangedEventArgs e) => FileTabsScrollChanged?.Invoke(sender, e);

        public event RoutedPropertyChangedEventHandler<double> PreviewSliderValueChanged;
        private void Internal_PreviewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => PreviewSliderValueChanged?.Invoke(sender, e);

        public event MouseWheelEventHandler SliderPreviewMouseWheel;
        private void Internal_Slider_PreviewMouseWheel(object sender, MouseWheelEventArgs e) => SliderPreviewMouseWheel?.Invoke(sender, e);
        public static readonly RoutedEvent SaveAllDoubleClickEvent =
    EventManager.RegisterRoutedEvent("SaveAllDoubleClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));

        public event RoutedEventHandler SaveAllDoubleClick
        {
            add { AddHandler(SaveAllDoubleClickEvent, value); }
            remove { RemoveHandler(SaveAllDoubleClickEvent, value); }
        }

        private void Internal_OnSaveAllDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            RaiseEvent(new RoutedEventArgs(SaveAllDoubleClickEvent, sender));
        }
        private void Internal_ScrollViewer_ManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }
        public FileTabItem GetTabFromPoint(Point pointRelativeToWindow)
        {
            Point pointInList = this.FileTabList.PointFromScreen(this.PointToScreen(new Point(0, 0)));
            Point mousePosInList = this.FileTabList.PointFromScreen(this.PointToScreen(pointRelativeToWindow));
            if (pointRelativeToWindow.Y > 220) return null;
            // 2. 遍历当前可见的 Tab 容器
            for (int i = 0; i < FileTabList.Items.Count; i++)
            {
                var container = FileTabList.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                if (container == null) continue;
                Point relativePos = container.TranslatePoint(new Point(0, 0), FileTabList);
                Rect bounds = new Rect(relativePos.X, relativePos.Y+110, container.ActualWidth, container.ActualHeight);
                if (bounds.Contains(mousePosInList))
                {
                    return FileTabList.Items[i] as FileTabItem;
                }
            }

            return null;
        }

        public static readonly DependencyProperty IsSingleTabModeProperty =
    DependencyProperty.Register("IsSingleTabMode", typeof(bool), typeof(ImageBarControl), new PropertyMetadata(false));

        public bool IsSingleTabMode
        {
            get { return (bool)GetValue(IsSingleTabModeProperty); }
            set { SetValue(IsSingleTabModeProperty, value); }
        }
        public event DragEventHandler FileTabLeave;
           private void Internal_OnFileTabDragLeave(object sender, DragEventArgs e) => FileTabLeave?.Invoke(sender, e);

        public static readonly DependencyProperty IsViewModeProperty =
                   DependencyProperty.Register("IsViewMode", typeof(bool), typeof(ImageBarControl), new PropertyMetadata(false));

        public bool IsViewMode
        {
            get { return (bool)GetValue(IsViewModeProperty); }
            set { SetValue(IsViewModeProperty, value); }
        }
        public static readonly DependencyProperty IsPinnedProperty =
            DependencyProperty.Register("IsPinned", typeof(bool), typeof(ImageBarControl), new PropertyMetadata(false));

        public bool IsPinned
        {
            get { return (bool)GetValue(IsPinnedProperty); }
            set { SetValue(IsPinnedProperty, value); }
        }
        public void TogglePin()
        {
            IsPinned = !IsPinned;
        }
        public static readonly DependencyProperty IsCompactModeProperty =
         DependencyProperty.Register("IsCompactMode", typeof(bool), typeof(ImageBarControl), new PropertyMetadata(false, OnCompactModeChanged));

        public bool IsCompactMode
        {
            get { return (bool)GetValue(IsCompactModeProperty); }
            set { SetValue(IsCompactModeProperty, value); }
        }

        public static readonly DependencyProperty CurrentTabWidthProperty =
            DependencyProperty.Register("CurrentTabWidth", typeof(double), typeof(ImageBarControl), new PropertyMetadata(150.0));

        public double CurrentTabWidth
        {
            get { return (double)GetValue(CurrentTabWidthProperty); }
            set { SetValue(CurrentTabWidthProperty, value); }
        }

        public double DesiredHeight
        {
            get { return (double)GetValue(DesiredHeightProperty); }
            set { SetValue(DesiredHeightProperty, value); }
        }

        public static readonly DependencyProperty DesiredHeightProperty =
            DependencyProperty.Register("DesiredHeight", typeof(double), typeof(ImageBarControl), new PropertyMetadata(100.0));


        private static void OnCompactModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = d as ImageBarControl;
            if (ctrl != null)
            {
                // 切换模式时调整容器的预期高度，以便动画正常工作
                ctrl.DesiredHeight = (bool)e.NewValue ? 45.0 : 100.0;
                ctrl.UpdateTabWidth();
                ctrl.InvalidateVisual();
            }
        }

        private void UpdateTabWidth()
        {
            if (!IsCompactMode)
            {
                CurrentTabWidth = 120.0;
                return;
            }

            var mw = MainWindow.GetCurrentInstance();
            if (mw == null || mw.FileTabs == null || mw.FileTabs.Count == 0)
            {
                CurrentTabWidth = 150.0;
                return;
            }

            double availableWidth = FileTabsScroller.ActualWidth;
            if (availableWidth <= 0)
            {
                // 如果还没加载好，延迟一下再算
                Dispatcher.BeginInvoke(new Action(UpdateTabWidth), System.Windows.Threading.DispatcherPriority.Loaded);
                return;
            }

            // 扣除“新建”按钮的宽度（大约 32-40px，包括 Margin）
            double reservedWidth = NewTabBtn.ActualWidth + NewTabBtn.Margin.Left + NewTabBtn.Margin.Right + 10;
            if (LeftAddBtn.Visibility == Visibility.Visible)
            {
                reservedWidth += LeftAddBtn.ActualWidth + LeftAddBtn.Margin.Left + LeftAddBtn.Margin.Right;
            }

            double tabAreaWidth = availableWidth - reservedWidth;
            int count = mw.FileTabs.Count;

            double idealWidth = tabAreaWidth / count;

            // 限制在 60 到 150 之间
            if (idealWidth > 150.0) idealWidth = 150.0;
            if (idealWidth < 80.0) idealWidth = 80.0;

            CurrentTabWidth = idealWidth;
        }
        private void Internal_OnToggleViewModeClick(object sender, RoutedEventArgs e)
        {
            IsCompactMode = !IsCompactMode;
        }
        private void Internal_OnBackgroundMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;

            var dep = e.OriginalSource as DependencyObject;
            if (dep == null) return;
            if (HasDataContext<FileTabItem>(dep)) return;
            if (FindAncestor<ButtonBase>(dep) != null) return;
            if (FindAncestor<Slider>(dep) != null) return;
            if (FindAncestor<Thumb>(dep) != null) return;
            if (FindAncestor<ScrollBar>(dep) != null) return;

            IsCompactMode = !IsCompactMode;
            e.Handled = true;
        }

        private static bool HasDataContext<T>(DependencyObject d)
        {
            while (d != null)
            {
                if (d is FrameworkElement fe && fe.DataContext is T) return true;
                d = VisualTreeHelper.GetParent(d);
            }
            return false;
        }

        private static T? FindAncestor<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null)
            {
                if (d is T t) return t;
                d = VisualTreeHelper.GetParent(d);
            }
            return null;
        }

        private void Internal_OnFileTabPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            FilePreview.ClosePreview();
        }
    }
}
