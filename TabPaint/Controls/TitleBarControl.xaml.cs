using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TabPaint.Services;
using TabPaint;

namespace TabPaint.Controls
{
    public partial class TitleBarControl : UserControl
    {
        private CancellationTokenSource _previewCts;
        public TextBlock TitleTextControl => TitleTextBlock;
        public Button MaxBtn => MaxRestoreButton;
        public Button BtnUndo => UndoButton;
        public Button BtnRedo => RedoButton;
        public System.Windows.Shapes.Path IconUndo => UndoIcon;
        public System.Windows.Shapes.Path IconRedo => RedoIcon;

        private bool _isFileMenuLoaded = false;
        private bool _isEditMenuLoaded = false;
        private bool _isEffectMenuLoaded = false;

        // ── 窗口控制事件 ──
        public static readonly RoutedEvent MinimizeClickEvent = EventManager.RegisterRoutedEvent(
            "MinimizeClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent MaximizeRestoreClickEvent = EventManager.RegisterRoutedEvent(
            "MaximizeRestoreClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent CloseClickEvent = EventManager.RegisterRoutedEvent(
            "CloseClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));

        public event RoutedEventHandler MinimizeClick { add => AddHandler(MinimizeClickEvent, value); remove => RemoveHandler(MinimizeClickEvent, value); }
        public event RoutedEventHandler MaximizeRestoreClick { add => AddHandler(MaximizeRestoreClickEvent, value); remove => RemoveHandler(MaximizeRestoreClickEvent, value); }
        public event RoutedEventHandler CloseClick { add => AddHandler(CloseClickEvent, value); remove => RemoveHandler(CloseClickEvent, value); }

        // ── 模式切换 ──
        public static readonly RoutedEvent ModeSwitchClickEvent = EventManager.RegisterRoutedEvent(
            "ModeSwitchClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public event RoutedEventHandler ModeSwitchClick { add => AddHandler(ModeSwitchClickEvent, value); remove => RemoveHandler(ModeSwitchClickEvent, value); }

        // ── 文件菜单事件（路由事件） ──
        public static readonly RoutedEvent NewClickEvent = EventManager.RegisterRoutedEvent("NewClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent OpenClickEvent = EventManager.RegisterRoutedEvent("OpenClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent OpenWorkspaceClickEvent = EventManager.RegisterRoutedEvent("OpenWorkspaceClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent NewWindowClickEvent = EventManager.RegisterRoutedEvent("NewWindowClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent SaveClickEvent = EventManager.RegisterRoutedEvent("SaveClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent SaveAsClickEvent = EventManager.RegisterRoutedEvent("SaveAsClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent SaveAsPdfClickEvent = EventManager.RegisterRoutedEvent("SaveAsPdfClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent PrintClickEvent = EventManager.RegisterRoutedEvent("PrintClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent ExitClickEvent = EventManager.RegisterRoutedEvent("ExitClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent SettingsClickEvent = EventManager.RegisterRoutedEvent("SettingsClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));

        public event RoutedEventHandler NewClick { add => AddHandler(NewClickEvent, value); remove => RemoveHandler(NewClickEvent, value); }
        public event RoutedEventHandler OpenClick { add => AddHandler(OpenClickEvent, value); remove => RemoveHandler(OpenClickEvent, value); }
        public event RoutedEventHandler OpenWorkspaceClick { add => AddHandler(OpenWorkspaceClickEvent, value); remove => RemoveHandler(OpenWorkspaceClickEvent, value); }
        public event RoutedEventHandler NewWindowClick { add => AddHandler(NewWindowClickEvent, value); remove => RemoveHandler(NewWindowClickEvent, value); }
        public event RoutedEventHandler SaveClick { add => AddHandler(SaveClickEvent, value); remove => RemoveHandler(SaveClickEvent, value); }
        public event RoutedEventHandler SaveAsClick { add => AddHandler(SaveAsClickEvent, value); remove => RemoveHandler(SaveAsClickEvent, value); }
        public event RoutedEventHandler SaveAsPdfClick { add => AddHandler(SaveAsPdfClickEvent, value); remove => RemoveHandler(SaveAsPdfClickEvent, value); }
        public event RoutedEventHandler PrintClick { add => AddHandler(PrintClickEvent, value); remove => RemoveHandler(PrintClickEvent, value); }
        public event RoutedEventHandler ExitClick { add => AddHandler(ExitClickEvent, value); remove => RemoveHandler(ExitClickEvent, value); }
        public event RoutedEventHandler SettingsClick { add => AddHandler(SettingsClickEvent, value); remove => RemoveHandler(SettingsClickEvent, value); }

        // ── 编辑菜单事件 ──
        public static readonly RoutedEvent CopyClickEvent = EventManager.RegisterRoutedEvent("CopyClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent CutClickEvent = EventManager.RegisterRoutedEvent("CutClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent PasteClickEvent = EventManager.RegisterRoutedEvent("PasteClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));

        public event RoutedEventHandler CopyClick { add => AddHandler(CopyClickEvent, value); remove => RemoveHandler(CopyClickEvent, value); }
        public event RoutedEventHandler CutClick { add => AddHandler(CutClickEvent, value); remove => RemoveHandler(CutClickEvent, value); }
        public event RoutedEventHandler PasteClick { add => AddHandler(PasteClickEvent, value); remove => RemoveHandler(PasteClickEvent, value); }

        // ── 效果菜单事件 ──
        public static readonly RoutedEvent BCEClickEvent = EventManager.RegisterRoutedEvent("BCEClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent TTSClickEvent = EventManager.RegisterRoutedEvent("TTSClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent AutoLevelsClickEvent = EventManager.RegisterRoutedEvent("AutoLevelsClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent SepiaClickEvent = EventManager.RegisterRoutedEvent("SepiaClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent OilPaintingClickEvent = EventManager.RegisterRoutedEvent("OilPaintingClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent VignetteClickEvent = EventManager.RegisterRoutedEvent("VignetteClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent GlowClickEvent = EventManager.RegisterRoutedEvent("GlowClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent BlackWhiteClickEvent = EventManager.RegisterRoutedEvent("BlackWhiteClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent InvertClickEvent = EventManager.RegisterRoutedEvent("InvertClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent SharpenClickEvent = EventManager.RegisterRoutedEvent("SharpenClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent BrownClickEvent = EventManager.RegisterRoutedEvent("BrownClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent MosaicClickEvent = EventManager.RegisterRoutedEvent("MosaicClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent GaussianBlurClickEvent = EventManager.RegisterRoutedEvent("GaussianBlurClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent RedEyeClickEvent = EventManager.RegisterRoutedEvent("RedEyeClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent SketchClickEvent = EventManager.RegisterRoutedEvent("SketchClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent EdgeClickEvent = EventManager.RegisterRoutedEvent("EdgeClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent ResizeCanvasClickEvent = EventManager.RegisterRoutedEvent("ResizeCanvasClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public static readonly RoutedEvent WatermarkClickEvent = EventManager.RegisterRoutedEvent("WatermarkClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));

        public event RoutedEventHandler BCEClick { add => AddHandler(BCEClickEvent, value); remove => RemoveHandler(BCEClickEvent, value); }
        public event RoutedEventHandler TTSClick { add => AddHandler(TTSClickEvent, value); remove => RemoveHandler(TTSClickEvent, value); }
        public event RoutedEventHandler AutoLevelsClick { add => AddHandler(AutoLevelsClickEvent, value); remove => RemoveHandler(AutoLevelsClickEvent, value); }
        public event RoutedEventHandler SepiaClick { add => AddHandler(SepiaClickEvent, value); remove => RemoveHandler(SepiaClickEvent, value); }
        public event RoutedEventHandler OilPaintingClick { add => AddHandler(OilPaintingClickEvent, value); remove => RemoveHandler(OilPaintingClickEvent, value); }
        public event RoutedEventHandler VignetteClick { add => AddHandler(VignetteClickEvent, value); remove => RemoveHandler(VignetteClickEvent, value); }
        public event RoutedEventHandler GlowClick { add => AddHandler(GlowClickEvent, value); remove => RemoveHandler(GlowClickEvent, value); }
        public event RoutedEventHandler BlackWhiteClick { add => AddHandler(BlackWhiteClickEvent, value); remove => RemoveHandler(BlackWhiteClickEvent, value); }
        public event RoutedEventHandler InvertClick { add => AddHandler(InvertClickEvent, value); remove => RemoveHandler(InvertClickEvent, value); }
        public event RoutedEventHandler SharpenClick { add => AddHandler(SharpenClickEvent, value); remove => RemoveHandler(SharpenClickEvent, value); }
        public event RoutedEventHandler BrownClick { add => AddHandler(BrownClickEvent, value); remove => RemoveHandler(BrownClickEvent, value); }
        public event RoutedEventHandler MosaicClick { add => AddHandler(MosaicClickEvent, value); remove => RemoveHandler(MosaicClickEvent, value); }
        public event RoutedEventHandler GaussianBlurClick { add => AddHandler(GaussianBlurClickEvent, value); remove => RemoveHandler(GaussianBlurClickEvent, value); }
        public event RoutedEventHandler RedEyeClick { add => AddHandler(RedEyeClickEvent, value); remove => RemoveHandler(RedEyeClickEvent, value); }
        public event RoutedEventHandler SketchClick { add => AddHandler(SketchClickEvent, value); remove => RemoveHandler(SketchClickEvent, value); }
        public event RoutedEventHandler EdgeClick { add => AddHandler(EdgeClickEvent, value); remove => RemoveHandler(EdgeClickEvent, value); }
        public event RoutedEventHandler ResizeCanvasClick { add => AddHandler(ResizeCanvasClickEvent, value); remove => RemoveHandler(ResizeCanvasClickEvent, value); }
        public event RoutedEventHandler WatermarkClick { add => AddHandler(WatermarkClickEvent, value); remove => RemoveHandler(WatermarkClickEvent, value); }

        // ── 标签页/丢弃事件 ──
        public static readonly RoutedEvent NewTabClickEvent = EventManager.RegisterRoutedEvent("NewTabClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public event RoutedEventHandler NewTabClick { add => AddHandler(NewTabClickEvent, value); remove => RemoveHandler(NewTabClickEvent, value); }

        public static readonly RoutedEvent DiscardImageClickEvent = EventManager.RegisterRoutedEvent("DiscardImageClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public event RoutedEventHandler DiscardImageClick { add => AddHandler(DiscardImageClickEvent, value); remove => RemoveHandler(DiscardImageClickEvent, value); }

        public static readonly RoutedEvent DiscardAllClickEvent = EventManager.RegisterRoutedEvent("DiscardAllClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public event RoutedEventHandler DiscardAllClick { add => AddHandler(DiscardAllClickEvent, value); remove => RemoveHandler(DiscardAllClickEvent, value); }

        // ── 撤销/重做 ──
        public static readonly RoutedEvent UndoClickEvent = EventManager.RegisterRoutedEvent("UndoClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public event RoutedEventHandler UndoClick { add => AddHandler(UndoClickEvent, value); remove => RemoveHandler(UndoClickEvent, value); }

        public static readonly RoutedEvent RedoClickEvent = EventManager.RegisterRoutedEvent("RedoClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public event RoutedEventHandler RedoClick { add => AddHandler(RedoClickEvent, value); remove => RemoveHandler(RedoClickEvent, value); }

        // ── 帮助 ──
        public static readonly RoutedEvent HelpClickEvent = EventManager.RegisterRoutedEvent(
            "HelpClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public event RoutedEventHandler HelpClick { add => AddHandler(HelpClickEvent, value); remove => RemoveHandler(HelpClickEvent, value); }

        public static readonly RoutedEvent TitleBarRightClickEvent = EventManager.RegisterRoutedEvent(
            "TitleBarRightClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TitleBarControl));
        public event RoutedEventHandler TitleBarRightClick { add => AddHandler(TitleBarRightClickEvent, value); remove => RemoveHandler(TitleBarRightClickEvent, value); }

        public static readonly DependencyProperty WorkingDirectoryProperty =
            DependencyProperty.Register("WorkingDirectory", typeof(string), typeof(TitleBarControl), new PropertyMetadata(string.Empty));

        public string WorkingDirectory
        {
            get { return (string)GetValue(WorkingDirectoryProperty); }
            set { SetValue(WorkingDirectoryProperty, value); }
        }

        // ── 最近文件 ──
        public event EventHandler<string> RecentFileClick;
        public event RoutedEventHandler LogoMiddleClick;
        public bool IsLogoMenuEnabled { get; set; } = false;

        // ── 响应式断点常量 ──
        private const double BreakpointHideDiscard = 850;
        private const double BreakpointHideUndoRedo = 750;
        private const double BreakpointHideSave = 700;
        private const double MenuCollapseWidth = 600;
        private const double BreakpointHideHelp = 500;

        public TitleBarControl()
        {
            InitializeComponent();
            this.Loaded += TitleBarControl_Loaded;
            this.SizeChanged += TitleBarControl_SizeChanged;
            UpdateModeIcon(false);
        }

        private void TitleBarControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateResponsiveLayout();
        }

        private void UpdateResponsiveLayout()
        {
            if (!SettingsManager.Instance.Current.UseNewStyle) return;

            var window = Window.GetWindow(this);
            double w = window?.ActualWidth ?? this.ActualWidth;

            // 菜单折叠为汉堡按钮
            UpdateMenuCollapseState(w);

            // 逐级隐藏工具栏按钮组
            // 850px: 隐藏 新标签+放弃
            SetGroupVisibility(ToolbarGroup_Discard, w >= BreakpointHideDiscard);
            SetGroupVisibility(ToolbarSeparator_BeforeNewTab, w >= BreakpointHideDiscard);

            // 750px: 隐藏 撤销/重做
            SetGroupVisibility(ToolbarGroup_UndoRedo, w >= BreakpointHideUndoRedo);

            // 700px: 隐藏 保存
            SetGroupVisibility(ToolbarGroup_Save, w >= BreakpointHideSave);

            // 当保存也隐藏时，隐藏尾部分隔符
            SetGroupVisibility(ToolbarSeparator_End, w >= BreakpointHideSave);

            // 500px: 隐藏 帮助按钮
            if (HelpButton != null)
                HelpButton.Visibility = w >= BreakpointHideHelp ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void SetGroupVisibility(FrameworkElement element, bool visible)
        {
            if (element == null) return;
            element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateCollapsibleToolbarVisibility()
        {
            // 保留方法以免外部调用报错，统一转发
            UpdateResponsiveLayout();
        }

        private DispatcherTimer _helpHintTimer;
        private DispatcherTimer _logoMenuHintTimer;
        private bool _pendingLogoMenuOpen;

        private void CheckFirstRunHelp()
        {
            if (SettingsManager.Instance.Current.IsFirstRun)
            {
                SettingsManager.Instance.Current.IsFirstRun = false;
                SettingsManager.Instance.Save();
                var delayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                delayTimer.Tick += (s, e) =>
                {
                    delayTimer.Stop();
                    ShowHelpHint();
                };
                delayTimer.Start();
            }
        }

        private void ShowHelpHint()
        {
            var popup = this.FindName("HelpHintPopup") as Popup;
            if (popup != null)
            {
                popup.IsOpen = true;
                _helpHintTimer?.Stop();
                _helpHintTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                _helpHintTimer.Tick += (s, e) => { CloseHelpHint(); };
                _helpHintTimer.Start();
            }
        }

        private void CloseHelpHint()
        {
            var popup = this.FindName("HelpHintPopup") as Popup;
            if (popup != null && popup.IsOpen)
            {
                popup.IsOpen = false;
                _helpHintTimer?.Stop();
            }
        }

        private void CloseHelpHint_Click(object sender, RoutedEventArgs e) => CloseHelpHint();

        public void TryShowLogoMenuHintOnce()
        {
            var settings = SettingsManager.Instance.Current;
            if (settings.ViewLogoMenuHintShown) return;
            if (!IsLogoMenuEnabled) return;

            void showAndPersist()
            {
                ShowLogoMenuHint();
                settings.ViewLogoMenuHintShown = true;
                SettingsManager.Instance.Save();
            }

            if (!IsLoaded || !AppIcon.IsLoaded)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    if (!IsLogoMenuEnabled) return;
                    showAndPersist();
                }));
                return;
            }
            showAndPersist();
        }

        private void ShowLogoMenuHint()
        {
            var popup = this.FindName("LogoMenuHintPopup") as Popup;
            if (popup == null) return;
            popup.PlacementTarget = AppIcon;
            popup.Placement = PlacementMode.Bottom;
            popup.HorizontalOffset = 0;
            popup.VerticalOffset = 6;
            popup.IsOpen = true;
            _logoMenuHintTimer?.Stop();
            _logoMenuHintTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _logoMenuHintTimer.Tick += (s, e) => { CloseLogoMenuHint(); };
            _logoMenuHintTimer.Start();
        }

        private void CloseLogoMenuHint()
        {
            var popup = this.FindName("LogoMenuHintPopup") as Popup;
            if (popup != null && popup.IsOpen) popup.IsOpen = false;
            _logoMenuHintTimer?.Stop();
        }

        private void CloseLogoMenuHint_Click(object sender, RoutedEventArgs e) => CloseLogoMenuHint();

        public event MouseButtonEventHandler TitleBarMouseDown;

        private async void TitleBarControl_Loaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= TitleBarControl_Loaded;
            CheckFirstRunHelp();

            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.SizeChanged += (s, args) => UpdateResponsiveLayout();
            }

            // 初始化时也执行一次
            UpdateResponsiveLayout();

            var imageSource = await Task.Run(() =>
            {
                var uri = new Uri("pack://application:,,,/Resources/TabPaint.ico");
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            });
            AppIcon.Source = imageSource;
        }

        // ── 窗口控制按钮 ──
        private void OnMinimizeClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(MinimizeClickEvent));
        private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(MaximizeRestoreClickEvent));
        private void OnCloseClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(CloseClickEvent));

        private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Button ||
                (e.OriginalSource is FrameworkElement fe && fe.TemplatedParent is Button)) return;
            if (e.OriginalSource == AppIcon || e.Source == AppIcon) return;
            TitleBarMouseDown?.Invoke(this, e);
        }

        private void OnTitleTextBlockRightClick(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(TitleBarRightClickEvent));
            e.Handled = true;
        }

        private void OnModeSwitchClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ModeSwitchClickEvent));

        // ── 所有菜单项事件处理：全部走路由事件 ──
        private void OnNewTabClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(NewTabClickEvent));
        private void OnDiscardImageClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(DiscardImageClickEvent));
        private void OnDiscardAllClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(DiscardAllClickEvent));
        private void OnUndoClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(UndoClickEvent));
        private void OnRedoClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(RedoClickEvent));
        private void OnSaveClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SaveClickEvent));
        private void OnSaveAsClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SaveAsClickEvent));
        private void OnSaveAsPdfClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SaveAsPdfClickEvent));
        private void OnPrintClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(PrintClickEvent));
        private void OnSettingsClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SettingsClickEvent));
        private void OnNewClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(NewClickEvent));
        private void OnOpenClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(OpenClickEvent));
        private void OnOpenWorkspaceClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(OpenWorkspaceClickEvent));
        private void OnNewWindowClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(NewWindowClickEvent));
        private void OnExitClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ExitClickEvent));
        private void OnCopyClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(CopyClickEvent));
        private void OnCutClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(CutClickEvent));
        private void OnPasteClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(PasteClickEvent));
        private void OnBCEClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(BCEClickEvent));
        private void OnTTSClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(TTSClickEvent));
        private void OnAutoLevelsClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(AutoLevelsClickEvent));
        private void OnSepiaClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SepiaClickEvent));
        private void OnOilPaintingClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(OilPaintingClickEvent));
        private void OnVignetteClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(VignetteClickEvent));
        private void OnGlowClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(GlowClickEvent));
        private void OnBlackWhiteClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(BlackWhiteClickEvent));
        private void OnInvertClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(InvertClickEvent));
        private void OnSharpenClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SharpenClickEvent));
        private void OnBrownClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(BrownClickEvent));
        private void OnMosaicClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(MosaicClickEvent));
        private void OnGaussianBlurClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(GaussianBlurClickEvent));
        private void OnRedEyeClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(RedEyeClickEvent));
        private void OnSketchClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SketchClickEvent));
        private void OnEdgeClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(EdgeClickEvent));
        private void OnResizeCanvasClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ResizeCanvasClickEvent));
        private void OnWatermarkClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(WatermarkClickEvent));

        private void OnHelpClick(object sender, RoutedEventArgs e)
        {
            CloseHelpHint();
            RaiseEvent(new RoutedEventArgs(HelpClickEvent));
        }

        // ── 菜单构建 ──
        private MenuItem _recentFilesMenuItem;

        private void OnFileMenuOpened(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;
            if (!_isFileMenuLoaded)
            {
                menuItem.Items.Clear();
                menuItem.Items.Add(CreateMenuItem("L_Menu_File_New", "NewFile_Image", OnNewClick, "File.New"));
                menuItem.Items.Add(CreateMenuItem("L_Menu_File_Open", "Open_Folder_Image", OnOpenClick, "File.Open"));
                menuItem.Items.Add(CreateMenuItem("L_Menu_File_OpenFolder", "Open_Folder_Image", OnOpenWorkspaceClick, "File.OpenWorkspace"));
                menuItem.Items.Add(CreateMenuItem("L_Menu_File_NewWindow", "New_Window_Image", OnNewWindowClick));

                _recentFilesMenuItem = new MenuItem { Style = (Style)FindResource("Win11MenuItemStyle") };
                _recentFilesMenuItem.SetResourceReference(HeaderedItemsControl.HeaderProperty, "L_Menu_File_Recent");
                var resetPath = new System.Windows.Shapes.Path { Stretch = Stretch.Uniform, Width = 16, Height = 16 };
                resetPath.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "IconFillBrush");
                resetPath.SetResourceReference(System.Windows.Shapes.Path.DataProperty, "Reset_Image");
                _recentFilesMenuItem.Icon = resetPath;
                menuItem.Items.Add(_recentFilesMenuItem);

                menuItem.Items.Add(new Separator { Style = (Style)FindResource("MenuSeparator") });
                menuItem.Items.Add(CreateMenuItem("L_Menu_File_Save", "Save_Normal_Image", OnSaveClick, "File.Save"));
                menuItem.Items.Add(CreateMenuItem("L_Menu_File_SaveAs", "Save_Button_Image", OnSaveAsClick, "File.SaveAs"));
                menuItem.Items.Add(CreateMenuItem("L_Menu_File_SaveAsPDF", "Save_Button_Image", OnSaveAsPdfClick));
                menuItem.Items.Add(CreateMenuItem("L_Menu_File_Print", "Print_Image", OnPrintClick, "File.Print"));
                menuItem.Items.Add(CreateMenuItem("L_Menu_File_Exit", "Exit_Image", OnExitClick));
                _isFileMenuLoaded = true;
            }
            UpdateRecentFilesMenu();
        }

        private void OnEditMenuOpened(object sender, RoutedEventArgs e)
        {
            if (_isEditMenuLoaded) return;
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;
            menuItem.Items.Clear();
            menuItem.Items.Add(CreateMenuItem("L_Menu_Edit_Copy", "Copy_Image", OnCopyClick, "Edit.Copy"));
            menuItem.Items.Add(CreateMenuItem("L_Menu_Edit_Cut", "Cut_Image", OnCutClick, "Edit.Cut"));
            menuItem.Items.Add(CreateMenuItem("L_Menu_Edit_Paste", "Paste_Image", OnPasteClick, "Edit.Paste"));
            _isEditMenuLoaded = true;
        }

        // TitleBarControl.xaml.cs - 替换 OnEffectMenuOpened 方法

        private void OnEffectMenuOpened(object sender, RoutedEventArgs e)
        {
            if (_isEffectMenuLoaded) return;
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;
            menuItem.Items.Clear();

            // ★ BCE图标需要描边而非填充
            var bceItem = CreateMenuItem("L_Menu_Effect_BCE", "Brightness_Image", OnBCEClick, "Effect.Brightness");
            if (bceItem.Icon is System.Windows.Shapes.Path bceP)
            {
                bceP.Fill = Brushes.Transparent;
                bceP.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "IconFillBrush");
                bceP.StrokeThickness = 1.5;
            }
            menuItem.Items.Add(bceItem);

            menuItem.Items.Add(CreateMenuItem("L_Menu_Effect_TTS", "Color_Temperature_Image", OnTTSClick, "Effect.Temperature"));

            // ★ 自动色阶需要自定义图标Data
            var autoLevelsItem = CreateMenuItem("L_Menu_Effect_AutoLevels", null, OnAutoLevelsClick, "Effect.AutoLevels");
            var alPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M5 7C5 5.34314 6.34315 4 8 4C9.65685 4 11 5.34315 11 7V25C11 26.6569 9.65685 28 8 28C6.34315 28 5 26.6569 5 25V7ZM8 6C7.44771 6 7 6.44772 7 7V25C7 25.5523 7.44772 26 8 26C8.55229 26 9 25.5523 9 25V7C9 6.44772 8.55228 6 8 6ZM13 13C13 11.3431 14.3431 10 16 10C17.6569 10 19 11.3431 19 13V25C19 26.6569 17.6569 28 16 28C14.3431 28 13 26.6569 13 25V13ZM16 12C15.4477 12 15 12.4477 15 13V25C15 25.5523 15.4477 26 16 26C16.5523 26 17 25.5523 17 25V13C17 12.4477 16.5523 12 16 12ZM24 16C22.3431 16 21 17.3431 21 19V25C21 26.6569 22.3431 28 24 28C25.6569 28 27 26.6569 27 25V19C27 17.3431 25.6569 16 24 16ZM23 19C23 18.4477 23.4477 18 24 18C24.5523 18 25 18.4477 25 19V25C25 25.5523 24.5523 26 24 26C23.4477 26 23 25.5523 23 25V19Z"),
                Stretch = Stretch.Uniform,
                Width = 16,
                Height = 16
            };
            alPath.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "IconFillBrush");
            autoLevelsItem.Icon = alPath;
            menuItem.Items.Add(autoLevelsItem);

            menuItem.Items.Add(new Separator { Style = (Style)FindResource("MenuSeparator") });

            // 滤镜子菜单
            var filterItem = new MenuItem { Style = (Style)FindResource("Win11MenuItemStyle") };
            filterItem.SetResourceReference(HeaderedItemsControl.HeaderProperty, "L_Menu_Effect_Filter");
            var filterIcon = new System.Windows.Shapes.Path { Stretch = Stretch.Uniform, Width = 16, Height = 16 };
            filterIcon.SetResourceReference(System.Windows.Shapes.Path.DataProperty, "Filter_Image");
            filterIcon.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "IconFillBrush");
            filterItem.Icon = filterIcon;

            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Sepia", "Sepia_Image", OnSepiaClick, filterTag: "Sepia"));
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Oil", "OilPaint_Image", OnOilPaintingClick, filterTag: "OilPaint"));
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Vignette", "Vignette_Image", OnVignetteClick, filterTag: "Vignette"));
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Glow", "Glow_Image", OnGlowClick, filterTag: "Glow"));
            filterItem.Items.Add(new Separator { Style = (Style)FindResource("MenuSeparator") });
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_BW", "Black_And_White_Image", OnBlackWhiteClick, "Effect.Grayscale", filterTag: "Gray"));
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Invert", "Invert_Color_Image", OnInvertClick, "Effect.Invert", filterTag: "Invert"));

            // ★ 锐化图标
            var sharpenItem = CreateMenuItem("L_Menu_Effect_Sharpen", null, OnSharpenClick, filterTag: "Sharpen");
            var shPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M12,2L1,21H23M12,6L19.53,19H4.47"),
                Stretch = Stretch.Uniform,
                Width = 16,
                Height = 16
            };
            shPath.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "IconFillBrush");
            sharpenItem.Icon = shPath;
            filterItem.Items.Add(sharpenItem);

            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Brown", "Sepia_Image", OnBrownClick, filterTag: "Brown"));
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Mosaic", "Mosaic_Image", OnMosaicClick, filterTag: "Mosaic"));

            // ★ 高斯模糊图标
            var blurItem = CreateMenuItem("L_Menu_Effect_GaussianBlur", null, OnGaussianBlurClick, filterTag: "Blur");
            var blurPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4M12,6A6,6 0 0,0 6,12A6,6 0 0,0 12,18A6,6 0 0,0 18,12A6,6 0 0,0 12,6M12,8A4,4 0 0,1 16,12A4,4 0 0,1 12,16A4,4 0 0,1 8,12A4,4 0 0,1 12,8Z"),
                Stretch = Stretch.Uniform,
                Width = 16,
                Height = 16
            };
            blurPath.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "IconFillBrush");
            blurItem.Icon = blurPath;
            filterItem.Items.Add(blurItem);

            filterItem.Items.Add(new Separator { Style = (Style)FindResource("MenuSeparator") });
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_RedEye", "Eye_Image", OnRedEyeClick, filterTag: "RedEye"));
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Sketch", "Crayon_Image", OnSketchClick, filterTag: "Pencil"));
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Edge", "FitToWindow_Image", OnEdgeClick, filterTag: "Edge"));

            menuItem.Items.Add(filterItem);
            menuItem.Items.Add(CreateMenuItem("L_Menu_Effect_Resize", "Resize_Image", OnResizeCanvasClick, "Effect.Resize"));

            // ★ 水印图标需要描边
            var wmItem = CreateMenuItem("L_Menu_Effect_Watermark", "Watermark_Image", OnWatermarkClick);
            if (wmItem.Icon is System.Windows.Shapes.Path wp)
            {
                wp.Fill = Brushes.Transparent;
                wp.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "IconFillBrush");
                wp.StrokeThickness = 0.8;
            }
            menuItem.Items.Add(wmItem);

            _isEffectMenuLoaded = true;
        }


        private void UpdateRecentFilesMenu()
        {
            if (_recentFilesMenuItem == null) return;
            _recentFilesMenuItem.Items.Clear();
            var files = SettingsManager.Instance.Current.RecentFiles;
            if (files == null || files.Count == 0)
            {
                var emptyItem = new MenuItem { IsEnabled = false, Style = (Style)FindResource("SubMenuItemStyle") };
                emptyItem.SetResourceReference(HeaderedItemsControl.HeaderProperty, "L_Menu_Recent_None");
                _recentFilesMenuItem.Items.Add(emptyItem);
            }
            else
            {
                foreach (var file in files)
                {
                    var headerText = file.Length > 50 ? "..." + file.Substring(file.Length - 50) : file;
                    var item = new MenuItem { Header = headerText, ToolTip = file, Tag = file, Style = (Style)FindResource("SubMenuItemStyle") };
                    var filePath = file;
                    item.Click += (s, ev) => RecentFileClick?.Invoke(this, filePath);
                    item.MouseEnter += (s, ev) =>
                    {
                        FilePreview.ShowPreview(filePath, item, PlacementMode.Right);
                    };
                    item.MouseLeave += (s, ev) =>
                    {
                        FilePreview.ClosePreview();
                    };
                    _recentFilesMenuItem.Items.Add(item);
                }
                _recentFilesMenuItem.Items.Add(new Separator { Style = (Style)FindResource("MenuSeparator") });
                var clearItem = new MenuItem { Style = (Style)FindResource("SubMenuItemStyle") };
                clearItem.SetResourceReference(HeaderedItemsControl.HeaderProperty, "L_Menu_Recent_Clear");
                clearItem.Click += (s, ev) => SettingsManager.Instance.ClearRecentFiles();
                _recentFilesMenuItem.Items.Add(clearItem);
            }
        }

        private MenuItem CreateMenuItem(string headerResKey, string iconResKey, RoutedEventHandler clickHandler, string shortcutKey = null, string filterTag = null)
        {
            var item = new MenuItem { Style = (Style)FindResource("Win11MenuItemStyle") };
            item.SetResourceReference(HeaderedItemsControl.HeaderProperty, headerResKey);

            if (!string.IsNullOrEmpty(filterTag)) BindFilterPreview(item, filterTag);

            if (!string.IsNullOrEmpty(shortcutKey)) ShortcutService.SetShortcutKey(item, shortcutKey);

            if (clickHandler != null) item.Click += clickHandler;

            if (!string.IsNullOrEmpty(iconResKey))
            {
                var iconGeometry = Application.Current.TryFindResource(iconResKey) as Geometry;
                if (iconGeometry != null)
                {
                    var path = new System.Windows.Shapes.Path { Data = iconGeometry, Stretch = Stretch.Uniform, Width = 16, Height = 16 };
                    path.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "IconFillBrush");

                    // ★ 与 MenuBarControl 保持一致的描边型图标处理
                    if (iconResKey == "Exit_Image")
                    {
                        path.Fill = Brushes.Transparent;
                        path.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "IconFillBrush");
                        path.StrokeThickness = 1.5;
                        path.StrokeLineJoin = PenLineJoin.Round;
                        path.StrokeEndLineCap = PenLineCap.Round;
                        path.StrokeStartLineCap = PenLineCap.Round;
                    }

                    item.Icon = path;
                }
            }
            return item;
        }

        public void UpdateModeIcon(bool isViewMode)
        {
            string resourceKey = isViewMode ? "Paint_Mode_Image" : "View_Mode_Image";
            if (ModeIconImage != null)
            {
                var newImage = Application.Current.TryFindResource(resourceKey) as Geometry;
                if (newImage == null) newImage = this.TryFindResource(resourceKey) as Geometry;
                if (newImage != null) ModeIconImage.Data = newImage;
                ModeSwitchButton.ToolTip = isViewMode
                    ? LocalizationManager.GetString("L_Mode_Switch_ToPaint")
                    : LocalizationManager.GetString("L_Mode_Switch_ToView");
            }
        }

        // ── Logo 菜单 ──
        private void ToggleLogoMenu()
        {
            CloseLogoMenuHint();
            if (AppIcon.ContextMenu == null) LoadLogoContextMenu();
            if (AppIcon.ContextMenu == null) return;

            var menu = AppIcon.ContextMenu;
            if (menu.IsOpen)
            {
                _pendingLogoMenuOpen = false;
                menu.IsOpen = false;
                return;
            }

            menu.PlacementTarget = AppIcon;
            menu.Placement = PlacementMode.Bottom;
            menu.HorizontalOffset = 0;
            menu.VerticalOffset = 2;

            _pendingLogoMenuOpen = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (!_pendingLogoMenuOpen) return;
                if (!IsLogoMenuEnabled) return;
                menu.IsOpen = true;
                _pendingLogoMenuOpen = false;
            }));
        }

        private void OnAppIconMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                LogoMiddleClick?.Invoke(this, e);
                e.Handled = true;
            }
        }

        private void LoadLogoContextMenu()
        {
            try
            {
                var resourceUri = new Uri("pack://application:,,,/Controls/ContextMenus/TitleBarMenu.xaml");
                var dictionary = new ResourceDictionary { Source = resourceUri };
                var menu = dictionary["LogoContextMenu"] as ContextMenu;
                if (menu != null)
                {
                    menu.Placement = PlacementMode.Bottom;
                    menu.HorizontalOffset = 0;
                    menu.VerticalOffset = 2;
                    menu.Opened += OnLogoContextMenuOpened;
                    foreach (var item in menu.Items)
                    {
                        if (item is MenuItem menuItem) BindMenuEvents(menuItem);
                    }
                    AppIcon.ContextMenu = menu;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"加载菜单失败: {ex.Message}"); }
        }

        private void BindMenuEvents(MenuItem item)
        {
            if (item.Tag?.ToString() == "New") item.Click += OnNewClick;
            else if (item.Tag?.ToString() == "Open") item.Click += OnOpenClick;
            else if (item.Tag?.ToString() == "Save") item.Click += OnSaveClick;
            else if (item.Tag?.ToString() == "SaveAs") item.Click += OnSaveAsClick;
            else if (item.Tag?.ToString() == "Settings") item.Click += OnSettingsClick;
            else if (item.Tag?.ToString() == "Exit") item.Click += OnExitClick;
            else if (item.Tag?.ToString() == "OpenFolder") item.Click += OnOpenWorkspaceClick;
            else if (item.Tag?.ToString() == "WheelZoom") item.Click += OnWheelZoomMenuItemClick;
            else if (item.Tag?.ToString() == "WheelSwitchImage") item.Click += OnWheelSwitchImageMenuItemClick;
            else if (item.Tag?.ToString() == "WheelVerticalScroll") item.Click += OnWheelVerticalScrollMenuItemClick;

            foreach (var subItem in item.Items)
            {
                if (subItem is MenuItem subMenuItem) BindMenuEvents(subMenuItem);
            }
        }

        private void OnLogoContextMenuOpened(object? sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu menu) return;
            var currentMode = SettingsManager.Instance.Current.ViewMouseWheelMode;
            UpdateWheelMenuCheckState(menu, currentMode);
        }

        private static void UpdateWheelMenuCheckState(ItemsControl parent, MouseWheelMode currentMode)
        {
            foreach (var item in parent.Items)
            {
                if (item is not MenuItem menuItem) continue;
                var tag = menuItem.Tag?.ToString();
                if (tag == "WheelZoom") menuItem.IsChecked = currentMode == MouseWheelMode.Zoom;
                else if (tag == "WheelSwitchImage") menuItem.IsChecked = currentMode == MouseWheelMode.SwitchImage;
                else if (tag == "WheelVerticalScroll") menuItem.IsChecked = currentMode == MouseWheelMode.VerticalScroll;
                if (menuItem.HasItems) UpdateWheelMenuCheckState(menuItem, currentMode);
            }
        }

        private static void SetViewMouseWheelMode(MouseWheelMode mode)
        {
            var settings = SettingsManager.Instance.Current;
            if (settings.ViewMouseWheelMode == mode) return;
            settings.ViewMouseWheelMode = mode;
            SettingsManager.Instance.Save();
        }

        private void OnWheelZoomMenuItemClick(object sender, RoutedEventArgs e)
        {
            SetViewMouseWheelMode(MouseWheelMode.Zoom);
            if (AppIcon.ContextMenu != null) UpdateWheelMenuCheckState(AppIcon.ContextMenu, MouseWheelMode.Zoom);
        }

        private void OnWheelSwitchImageMenuItemClick(object sender, RoutedEventArgs e)
        {
            SetViewMouseWheelMode(MouseWheelMode.SwitchImage);
            if (AppIcon.ContextMenu != null) UpdateWheelMenuCheckState(AppIcon.ContextMenu, MouseWheelMode.SwitchImage);
        }

        private void OnWheelVerticalScrollMenuItemClick(object sender, RoutedEventArgs e)
        {
            SetViewMouseWheelMode(MouseWheelMode.VerticalScroll);
            if (AppIcon.ContextMenu != null) UpdateWheelMenuCheckState(AppIcon.ContextMenu, MouseWheelMode.VerticalScroll);
        }

        // ── 拖拽 ──
        public event EventHandler<MouseButtonEventArgs> IconDragRequest;
        private Point _dragStartPoint;

        private void OnAppIconPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(this);
        }

        private void OnAppIconPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!IsLogoMenuEnabled) return;
            CloseLogoMenuHint();
            ToggleLogoMenu();
            e.Handled = true;
        }

        private void OnAppIconPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if ((Window.GetWindow(this) as MainWindow)?.IsViewMode == true) return;
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(this);
                if (Math.Abs(currentPosition.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(currentPosition.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    IconDragRequest?.Invoke(this, null);
                }
            }
        }

        // ── 汉堡菜单相关 ──
        private bool _isHamburgerFileLoaded = false;
        private bool _isHamburgerEditLoaded = false;
        private bool _isHamburgerEffectLoaded = false;

        private void UpdateMenuCollapseState(double windowWidth)
        {
            if (HamburgerButton == null || MainMenuBar == null) return;

            if (windowWidth < MenuCollapseWidth)
            {
                MainMenuBar.Visibility = Visibility.Collapsed;
                HamburgerButton.Visibility = Visibility.Visible;
            }
            else
            {
                MainMenuBar.Visibility = Visibility.Visible;
                HamburgerButton.Visibility = Visibility.Collapsed;
            }
        }

        private void OnHamburgerClick(object sender, RoutedEventArgs e)
        {
            if (HamburgerButton.ContextMenu != null)
            {
                HamburgerButton.ContextMenu.PlacementTarget = HamburgerButton;
                HamburgerButton.ContextMenu.Placement = PlacementMode.Bottom;
                HamburgerButton.ContextMenu.IsOpen = true;
            }
        }

        private void OnHamburgerMenuOpened(object sender, RoutedEventArgs e)
        {
            BuildHamburgerFileMenu();
            BuildHamburgerEditMenu();
            BuildHamburgerEffectMenu();
        }

        private void BuildHamburgerFileMenu()
        {
            if (_isHamburgerFileLoaded)
            {
                UpdateHamburgerRecentFiles();
                return;
            }

            var menu = HamburgerFileMenu;
            menu.Items.Clear();

            var fileIcon = new System.Windows.Shapes.Path { Stretch = Stretch.Uniform, Width = 16, Height = 16 };
            fileIcon.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "IconFillBrush");
            fileIcon.SetResourceReference(System.Windows.Shapes.Path.DataProperty, "Open_Folder_Image");
            menu.Icon = fileIcon;

            menu.Items.Add(CreateMenuItem("L_Menu_File_New", "NewFile_Image", OnNewClick, "File.New"));
            menu.Items.Add(CreateMenuItem("L_Menu_File_Open", "Open_Folder_Image", OnOpenClick, "File.Open"));
            menu.Items.Add(CreateMenuItem("L_Menu_File_OpenFolder", "Open_Folder_Image", OnOpenWorkspaceClick, "File.OpenWorkspace"));
            menu.Items.Add(CreateMenuItem("L_Menu_File_NewWindow", "New_Window_Image", OnNewWindowClick));

            _hamburgerRecentFilesMenuItem = new MenuItem { Style = (Style)FindResource("Win11MenuItemStyle") };
            _hamburgerRecentFilesMenuItem.SetResourceReference(HeaderedItemsControl.HeaderProperty, "L_Menu_File_Recent");
            var resetPath = new System.Windows.Shapes.Path { Stretch = Stretch.Uniform, Width = 16, Height = 16 };
            resetPath.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "IconFillBrush");
            resetPath.SetResourceReference(System.Windows.Shapes.Path.DataProperty, "Reset_Image");
            _hamburgerRecentFilesMenuItem.Icon = resetPath;
            menu.Items.Add(_hamburgerRecentFilesMenuItem);

            menu.Items.Add(new Separator { Style = (Style)FindResource("MenuSeparator") });
            menu.Items.Add(CreateMenuItem("L_Menu_File_Save", "Save_Normal_Image", OnSaveClick, "File.Save"));
            menu.Items.Add(CreateMenuItem("L_Menu_File_SaveAs", "Save_Button_Image", OnSaveAsClick, "File.SaveAs"));
            menu.Items.Add(CreateMenuItem("L_Menu_File_SaveAsPDF", "Save_Button_Image", OnSaveAsPdfClick));
            menu.Items.Add(CreateMenuItem("L_Menu_File_Print", "Print_Image", OnPrintClick, "File.Print"));
            menu.Items.Add(CreateMenuItem("L_Menu_File_Exit", "Exit_Image", OnExitClick));

            _isHamburgerFileLoaded = true;
            UpdateHamburgerRecentFiles();
        }

        private MenuItem _hamburgerRecentFilesMenuItem;

        public void BindFilterPreview(MenuItem item, string filterTag)
        {
            item.MouseEnter += async (s, e) =>
            {
                _previewCts?.Cancel();
                _previewCts = new CancellationTokenSource();
                var token = _previewCts.Token;

                try
                {
                    await Task.Delay(50, token); // 防抖
                    var mw = MainWindow.GetCurrentInstance();
                    if (mw == null) return;

                    var preview = await mw.GetFilterPreviewAsync(filterTag, token);
                    if (preview != null && !token.IsCancellationRequested)
                    {
                        FilePreview.ShowFilterPreview(preview, item.Header?.ToString() ?? "", item);
                    }
                }
                catch (TaskCanceledException) { }
                catch { }
            };
            item.MouseLeave += (s, e) =>
            {
                _previewCts?.Cancel();
                FilePreview.ClosePreview();
            };
        }

        private void UpdateHamburgerRecentFiles()
        {
            if (_hamburgerRecentFilesMenuItem == null) return;
            _hamburgerRecentFilesMenuItem.Items.Clear();
            var files = SettingsManager.Instance.Current.RecentFiles;
            if (files == null || files.Count == 0)
            {
                var emptyItem = new MenuItem { IsEnabled = false, Style = (Style)FindResource("SubMenuItemStyle") };
                emptyItem.SetResourceReference(HeaderedItemsControl.HeaderProperty, "L_Menu_Recent_None");
                _hamburgerRecentFilesMenuItem.Items.Add(emptyItem);
            }
            else
            {
                foreach (var file in files)
                {
                    var headerText = file.Length > 50 ? "..." + file.Substring(file.Length - 50) : file;
                    var item = new MenuItem { Header = headerText, ToolTip = file, Tag = file, Style = (Style)FindResource("SubMenuItemStyle") };
                    var filePath = file;
                    item.Click += (s, ev) => RecentFileClick?.Invoke(this, filePath);
                    item.MouseEnter += (s, ev) =>
                    {
                        FilePreview.ShowPreview(filePath, item, PlacementMode.Right);
                    };
                    item.MouseLeave += (s, ev) =>
                    {
                        FilePreview.ClosePreview();
                    };
                    _hamburgerRecentFilesMenuItem.Items.Add(item);
                }
                _hamburgerRecentFilesMenuItem.Items.Add(new Separator { Style = (Style)FindResource("MenuSeparator") });
                var clearItem = new MenuItem { Style = (Style)FindResource("SubMenuItemStyle") };
                clearItem.SetResourceReference(HeaderedItemsControl.HeaderProperty, "L_Menu_Recent_Clear");
                clearItem.Click += (s, ev) => SettingsManager.Instance.ClearRecentFiles();
                _hamburgerRecentFilesMenuItem.Items.Add(clearItem);
            }
        }

        private void BuildHamburgerEditMenu()
        {
            if (_isHamburgerEditLoaded) return;

            var menu = HamburgerEditMenu;
            menu.Items.Clear();

            var editIcon = new System.Windows.Shapes.Path { Stretch = Stretch.Uniform, Width = 16, Height = 16 };
            editIcon.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "IconFillBrush");
            editIcon.SetResourceReference(System.Windows.Shapes.Path.DataProperty, "Copy_Image");
            menu.Icon = editIcon;

            menu.Items.Add(CreateMenuItem("L_Menu_Edit_Copy", "Copy_Image", OnCopyClick, "Edit.Copy"));
            menu.Items.Add(CreateMenuItem("L_Menu_Edit_Cut", "Cut_Image", OnCutClick, "Edit.Cut"));
            menu.Items.Add(CreateMenuItem("L_Menu_Edit_Paste", "Paste_Image", OnPasteClick, "Edit.Paste"));

            _isHamburgerEditLoaded = true;
        }

        private void BuildHamburgerEffectMenu()
        {
            if (_isHamburgerEffectLoaded) return;

            var menu = HamburgerEffectMenu;
            menu.Items.Clear();

            var effectIcon = new System.Windows.Shapes.Path { Stretch = Stretch.Uniform, Width = 16, Height = 16 };
            effectIcon.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "IconFillBrush");
            effectIcon.SetResourceReference(System.Windows.Shapes.Path.DataProperty, "Brightness_Image");
            menu.Icon = effectIcon;

            // ★ BCE图标描边处理
            var bceItem = CreateMenuItem("L_Menu_Effect_BCE", "Brightness_Image", OnBCEClick, "Effect.Brightness");
            if (bceItem.Icon is System.Windows.Shapes.Path bceP)
            {
                bceP.Fill = Brushes.Transparent;
                bceP.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "IconFillBrush");
                bceP.StrokeThickness = 1.5;
            }
            menu.Items.Add(bceItem);

            menu.Items.Add(CreateMenuItem("L_Menu_Effect_TTS", "Color_Temperature_Image", OnTTSClick, "Effect.Temperature"));

            // ★ 自动色阶自定义图标
            var autoLevelsItem = CreateMenuItem("L_Menu_Effect_AutoLevels", null, OnAutoLevelsClick, "Effect.AutoLevels");
            var alPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M5 7C5 5.34314 6.34315 4 8 4C9.65685 4 11 5.34315 11 7V25C11 26.6569 9.65685 28 8 28C6.34315 28 5 26.6569 5 25V7ZM8 6C7.44771 6 7 6.44772 7 7V25C7 25.5523 7.44772 26 8 26C8.55229 26 9 25.5523 9 25V7C9 6.44772 8.55228 6 8 6ZM13 13C13 11.3431 14.3431 10 16 10C17.6569 10 19 11.3431 19 13V25C19 26.6569 17.6569 28 16 28C14.3431 28 13 26.6569 13 25V13ZM16 12C15.4477 12 15 12.4477 15 13V25C15 25.5523 15.4477 26 16 26C16.5523 26 17 25.5523 17 25V13C17 12.4477 16.5523 12 16 12ZM24 16C22.3431 16 21 17.3431 21 19V25C21 26.6569 22.3431 28 24 28C25.6569 28 27 26.6569 27 25V19C27 17.3431 25.6569 16 24 16ZM23 19C23 18.4477 23.4477 18 24 18C24.5523 18 25 18.4477 25 19V25C25 25.5523 24.5523 26 24 26C23.4477 26 23 25.5523 23 25V19Z"),
                Stretch = Stretch.Uniform,
                Width = 16,
                Height = 16
            };
            alPath.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "IconFillBrush");
            autoLevelsItem.Icon = alPath;
            menu.Items.Add(autoLevelsItem);

            menu.Items.Add(new Separator { Style = (Style)FindResource("MenuSeparator") });

            var filterItem = new MenuItem { Style = (Style)FindResource("Win11MenuItemStyle") };
            filterItem.SetResourceReference(HeaderedItemsControl.HeaderProperty, "L_Menu_Effect_Filter");
            var filterIcon = new System.Windows.Shapes.Path { Stretch = Stretch.Uniform, Width = 16, Height = 16 };
            filterIcon.SetResourceReference(System.Windows.Shapes.Path.DataProperty, "Filter_Image");
            filterIcon.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "IconFillBrush");
            filterItem.Icon = filterIcon;

            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Sepia", "Sepia_Image", OnSepiaClick, filterTag: "Sepia"));
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Oil", "OilPaint_Image", OnOilPaintingClick, filterTag: "OilPaint"));
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Vignette", "Vignette_Image", OnVignetteClick, filterTag: "Vignette"));
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Glow", "Glow_Image", OnGlowClick, filterTag: "Glow"));
            filterItem.Items.Add(new Separator { Style = (Style)FindResource("MenuSeparator") });
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_BW", "Black_And_White_Image", OnBlackWhiteClick, "Effect.Grayscale", filterTag: "Gray"));
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Invert", "Invert_Color_Image", OnInvertClick, "Effect.Invert", filterTag: "Invert"));

            // ★ 锐化
            var sharpenItem = CreateMenuItem("L_Menu_Effect_Sharpen", null, OnSharpenClick, filterTag: "Sharpen");
            var shPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M12,2L1,21H23M12,6L19.53,19H4.47"),
                Stretch = Stretch.Uniform,
                Width = 16,
                Height = 16
            };
            shPath.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "IconFillBrush");
            sharpenItem.Icon = shPath;
            filterItem.Items.Add(sharpenItem);

            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Brown", "Sepia_Image", OnBrownClick, filterTag: "Brown"));
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Mosaic", "Mosaic_Image", OnMosaicClick, filterTag: "Mosaic"));

            // ★ 高斯模糊
            var blurItem = CreateMenuItem("L_Menu_Effect_GaussianBlur", null, OnGaussianBlurClick, filterTag: "Blur");
            var blurPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4M12,6A6,6 0 0,0 6,12A6,6 0 0,0 12,18A6,6 0 0,0 18,12A6,6 0 0,0 12,6M12,8A4,4 0 0,1 16,12A4,4 0 0,1 12,16A4,4 0 0,1 8,12A4,4 0 0,1 12,8Z"),
                Stretch = Stretch.Uniform,
                Width = 16,
                Height = 16
            };
            blurPath.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "IconFillBrush");
            blurItem.Icon = blurPath;
            filterItem.Items.Add(blurItem);

            filterItem.Items.Add(new Separator { Style = (Style)FindResource("MenuSeparator") });
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_RedEye", "Eye_Image", OnRedEyeClick, filterTag: "RedEye"));
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Sketch", "Crayon_Image", OnSketchClick, filterTag: "Pencil"));
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Edge", "FitToWindow_Image", OnEdgeClick, filterTag: "Edge"));

            menu.Items.Add(filterItem);
            menu.Items.Add(CreateMenuItem("L_Menu_Effect_Resize", "Resize_Image", OnResizeCanvasClick, "Effect.Resize"));

            // ★ 水印描边处理
            var wmItem = CreateMenuItem("L_Menu_Effect_Watermark", "Watermark_Image", OnWatermarkClick);
            if (wmItem.Icon is System.Windows.Shapes.Path wp)
            {
                wp.Fill = Brushes.Transparent;
                wp.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "IconFillBrush");
                wp.StrokeThickness = 0.8;
            }
            menu.Items.Add(wmItem);

            _isHamburgerEffectLoaded = true;
        }


        private void Window_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            Point relativePoint = e.GetPosition(this);
            if (relativePoint.Y < 60 && relativePoint.X > 20 && !MainWindow.GetCurrentInstance().IsViewMode)
            {
                MainWindow.GetCurrentInstance().MaximizeWindowHandler();
                e.Handled = true;
            }
        }
    }
}
