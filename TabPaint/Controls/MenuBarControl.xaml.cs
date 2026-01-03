using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TabPaint.Controls
{
    public partial class MenuBarControl : UserControl
    {
        // ================== 事件定义 (Bubbling) ==================

        // File Menu
        public static readonly RoutedEvent NewClickEvent = EventManager.RegisterRoutedEvent("NewClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent OpenClickEvent = EventManager.RegisterRoutedEvent("OpenClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent SaveClickEvent = EventManager.RegisterRoutedEvent("SaveClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent SaveAsClickEvent = EventManager.RegisterRoutedEvent("SaveAsClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent ExitClickEvent = EventManager.RegisterRoutedEvent("ExitClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));

        // Edit Menu
        public static readonly RoutedEvent CopyClickEvent = EventManager.RegisterRoutedEvent("CopyClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent CutClickEvent = EventManager.RegisterRoutedEvent("CutClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent PasteClickEvent = EventManager.RegisterRoutedEvent("PasteClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent ResizeCanvasClickEvent = EventManager.RegisterRoutedEvent("ResizeCanvasClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));

        // Effects Menu
        public static readonly RoutedEvent BCEClickEvent = EventManager.RegisterRoutedEvent("BCEClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl)); // Brightness/Contrast/Exposure
        public static readonly RoutedEvent TTSClickEvent = EventManager.RegisterRoutedEvent("TTSClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl)); // Temp/Tint/Saturation
        public static readonly RoutedEvent BlackWhiteClickEvent = EventManager.RegisterRoutedEvent("BlackWhiteClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));

        // Quick Actions
        public static readonly RoutedEvent UndoClickEvent = EventManager.RegisterRoutedEvent("UndoClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent RedoClickEvent = EventManager.RegisterRoutedEvent("RedoClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent SettingsClickEvent = EventManager.RegisterRoutedEvent("SettingsClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));

        // ================== 事件包装 ==================
        public event RoutedEventHandler NewClick { add { AddHandler(NewClickEvent, value); } remove { RemoveHandler(NewClickEvent, value); } }
        public event RoutedEventHandler OpenClick { add { AddHandler(OpenClickEvent, value); } remove { RemoveHandler(OpenClickEvent, value); } }
        public event RoutedEventHandler SaveClick { add { AddHandler(SaveClickEvent, value); } remove { RemoveHandler(SaveClickEvent, value); } }
        public event RoutedEventHandler SaveAsClick { add { AddHandler(SaveAsClickEvent, value); } remove { RemoveHandler(SaveAsClickEvent, value); } }
        public event RoutedEventHandler ExitClick { add { AddHandler(ExitClickEvent, value); } remove { RemoveHandler(ExitClickEvent, value); } }
        public event RoutedEventHandler CopyClick { add { AddHandler(CopyClickEvent, value); } remove { RemoveHandler(CopyClickEvent, value); } }
        public event RoutedEventHandler CutClick { add { AddHandler(CutClickEvent, value); } remove { RemoveHandler(CutClickEvent, value); } }
        public event RoutedEventHandler PasteClick { add { AddHandler(PasteClickEvent, value); } remove { RemoveHandler(PasteClickEvent, value); } }
        public event RoutedEventHandler ResizeCanvasClick { add { AddHandler(ResizeCanvasClickEvent, value); } remove { RemoveHandler(ResizeCanvasClickEvent, value); } }
        public event RoutedEventHandler BCEClick { add { AddHandler(BCEClickEvent, value); } remove { RemoveHandler(BCEClickEvent, value); } }
        public event RoutedEventHandler TTSClick { add { AddHandler(TTSClickEvent, value); } remove { RemoveHandler(TTSClickEvent, value); } }
        public event RoutedEventHandler BlackWhiteClick { add { AddHandler(BlackWhiteClickEvent, value); } remove { RemoveHandler(BlackWhiteClickEvent, value); } }
        public event RoutedEventHandler UndoClick { add { AddHandler(UndoClickEvent, value); } remove { RemoveHandler(UndoClickEvent, value); } }
        public event RoutedEventHandler RedoClick { add { AddHandler(RedoClickEvent, value); } remove { RemoveHandler(RedoClickEvent, value); } }
        public event RoutedEventHandler SettingsClick { add { AddHandler(SettingsClickEvent, value); } remove { RemoveHandler(SettingsClickEvent, value); } }

        // ================== 公开属性 (为了让 MainWindow 能控制撤销重做状态) ==================

        /// <summary>
        /// 获取或设置是否允许撤销
        /// </summary>
        public bool IsUndoEnabled
        {
            get { return UndoButton.IsEnabled; }
            set { UndoButton.IsEnabled = value; }
        }

        /// <summary>
        /// 获取或设置是否允许重做
        /// </summary>
        public bool IsRedoEnabled
        {
            get { return RedoButton.IsEnabled; }
            set { RedoButton.IsEnabled = value; }
        }
        private void OnRootBorderMouseDown(object sender, MouseButtonEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window == null) return;
            if (e.LeftButton == MouseButtonState.Pressed)
            { try
                { window.DragMove(); }
                catch{ }
            }
        }
        // 如果你的代码里需要直接访问 Button 对象 (例如做动画)，可以这样暴露：
        public Button BtnUndo => UndoButton;
        public Button BtnRedo => RedoButton;
        public System.Windows.Shapes.Path IconUndo => UndoIcon;
        public System.Windows.Shapes.Path IconRedo => RedoIcon;
        public MenuBarControl()
        {
            InitializeComponent();
        }

        // ================== 内部触发器 ==================
        private void OnNewClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(NewClickEvent));
        private void OnOpenClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(OpenClickEvent));
        private void OnSaveClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SaveClickEvent));
        private void OnSaveAsClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SaveAsClickEvent));
        private void OnExitClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ExitClickEvent));
        private void OnCopyClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(CopyClickEvent));
        private void OnCutClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(CutClickEvent));
        private void OnPasteClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(PasteClickEvent));
        private void OnResizeCanvasClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ResizeCanvasClickEvent));
        private void OnBCEClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(BCEClickEvent));
        private void OnTTSClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(TTSClickEvent));
        private void OnBlackWhiteClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(BlackWhiteClickEvent));
        private void OnUndoClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(UndoClickEvent));
        private void OnRedoClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(RedoClickEvent));
        private void OnSettingsClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SettingsClickEvent));
        public event EventHandler<string> RecentFileClick;
        public event EventHandler ClearRecentFilesClick;
        private void OnFileMenuOpened(object sender, RoutedEventArgs e)
        {
            // 防止事件冒泡导致的多次触发（如果子菜单也有Opened事件）
            var menuItem = e.OriginalSource as MenuItem;
            if (menuItem == null || menuItem.Header.ToString() != "文件") return;
        
            UpdateRecentFilesMenu();
        }

        private void UpdateRecentFilesMenu()
        {
            RecentFilesMenuItem.Items.Clear();

            var files = TabPaint.SettingsManager.Instance.Current.RecentFiles;

            if (files == null || files.Count == 0)
            {
                var emptyItem = new MenuItem { Header = "(无最近文件)", IsEnabled = false, Style = (Style)FindResource("SubMenuItemStyle") };
                RecentFilesMenuItem.Items.Add(emptyItem);
            }
            else
            {
                // 1. 添加文件列表
                foreach (var file in files)
                {
                    // 为了美观，菜单文字可以截断过长的路径，但 ToolTip 显示全路径
                    var headerText = file.Length > 50 ? "..." + file.Substring(file.Length - 50) : file;

                    var item = new MenuItem
                    {
                        Header = headerText,
                        ToolTip = file,
                        Tag = file, // 将路径存在 Tag 中
                        Style = (Style)FindResource("SubMenuItemStyle")
                    };
                    item.Click += OnRecentFileItemClick;
                    RecentFilesMenuItem.Items.Add(item);
                }

                // 2. 添加分割线
                RecentFilesMenuItem.Items.Add(new Separator { Style = (Style)FindResource("MenuSeparator") });

                // 3. 添加清除按钮
                var clearItem = new MenuItem { Header = "清除最近文件列表", Style = (Style)FindResource("SubMenuItemStyle") };
                clearItem.Click += (s, e) => { ClearRecentFilesClick?.Invoke(this, EventArgs.Empty); };
                RecentFilesMenuItem.Items.Add(clearItem);
            }
        }

        private void OnRecentFileItemClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string path)
            {
                RecentFileClick?.Invoke(this, path);
            }
        }
    }
}
