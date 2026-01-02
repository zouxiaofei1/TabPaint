using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
            UpdateModeIcon(false);
        }
        public event MouseButtonEventHandler TitleBarMouseDown;

        // 2. 内部 Border 的点击事件处理器

        private void OnMinimizeClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(MinimizeClickEvent));
        private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(MaximizeRestoreClickEvent));
        private void OnCloseClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(CloseClickEvent));

        private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 如果点击的是按钮（最大化/关闭等），通常按钮会拦截事件，但为了保险可以判断 Source
            if (e.OriginalSource is System.Windows.Controls.Button ||
                (e.OriginalSource is FrameworkElement fe && fe.TemplatedParent is System.Windows.Controls.Button))
            {
                return;
            }

            // 3. 将事件转发给外部 (即 MainWindow)
            TitleBarMouseDown?.Invoke(this, e);
        }
        public static readonly RoutedEvent ModeSwitchClickEvent = EventManager.RegisterRoutedEvent(
    "ModeSwitchClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));

        public event RoutedEventHandler ModeSwitchClick
        {
            add => AddHandler(ModeSwitchClickEvent, value);
            remove => RemoveHandler(ModeSwitchClickEvent, value);
        }
        private void OnModeSwitchClick(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ModeSwitchClickEvent));
        }
        public void UpdateModeIcon(bool isViewMode)
        {
            // 逻辑判定：
            // 如果当前是看图模式 (isViewMode == true)，按钮应该显示 "去画图" 的图标
            // 如果当前是画图模式 (isViewMode == false)，按钮应该显示 "去看图" 的图标
            string resourceKey = isViewMode ? "Paint_Mode_Image" : "View_Mode_Image";

            if (ModeIconImage != null)
            {
                // 从资源字典中查找 DrawingImage
                var newImage = Application.Current.TryFindResource(resourceKey) as DrawingImage;

                // 如果在当前 Control 资源里找不到，就去全局找
                if (newImage == null)
                    newImage = this.TryFindResource(resourceKey) as DrawingImage;

                if (newImage != null)
                {
                    ModeIconImage.Source = newImage;
                }

                // 更新提示文字
                ModeSwitchButton.ToolTip = isViewMode ? "切换到画图模式 (Tab)" : "切换到看图模式 (Tab)";
            }
        }

    
    }
}
