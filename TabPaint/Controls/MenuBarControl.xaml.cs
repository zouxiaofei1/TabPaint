//
//MenuBarControl.xaml.cs
//顶部菜单栏控件，包含文件、编辑、效果等菜单项，以及最近打开文件列表的维护。
//
//
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using TabPaint.Services;

namespace TabPaint.Controls
{
    public partial class MenuBarControl : UserControl
    {
        private bool _isFileMenuLoaded = false;
        private bool _isEditMenuLoaded = false;
        private bool _isEffectMenuLoaded = false;
        public static readonly RoutedEvent NewClickEvent = EventManager.RegisterRoutedEvent("NewClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent OpenClickEvent = EventManager.RegisterRoutedEvent("OpenClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent SaveClickEvent = EventManager.RegisterRoutedEvent("SaveClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent SaveAsClickEvent = EventManager.RegisterRoutedEvent("SaveAsClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent SaveAsPdfClickEvent = EventManager.RegisterRoutedEvent("SaveAsPdfClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent PrintClickEvent = EventManager.RegisterRoutedEvent("PrintClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent ExitClickEvent = EventManager.RegisterRoutedEvent("ExitClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent RecycleBinClickEvent = EventManager.RegisterRoutedEvent("RecycleBinClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent CopyClickEvent = EventManager.RegisterRoutedEvent("CopyClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent CutClickEvent = EventManager.RegisterRoutedEvent("CutClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent PasteClickEvent = EventManager.RegisterRoutedEvent("PasteClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent ResizeCanvasClickEvent = EventManager.RegisterRoutedEvent("ResizeCanvasClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent BCEClickEvent = EventManager.RegisterRoutedEvent("BCEClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl)); // Brightness/Contrast/Exposure
        public static readonly RoutedEvent TTSClickEvent = EventManager.RegisterRoutedEvent("TTSClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl)); // Temp/Tint/Saturation
        public static readonly RoutedEvent BlackWhiteClickEvent = EventManager.RegisterRoutedEvent("BlackWhiteClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent UndoClickEvent = EventManager.RegisterRoutedEvent("UndoClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent RedoClickEvent = EventManager.RegisterRoutedEvent("RedoClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent SettingsClickEvent = EventManager.RegisterRoutedEvent("SettingsClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public event RoutedEventHandler NewClick { add { AddHandler(NewClickEvent, value); } remove { RemoveHandler(NewClickEvent, value); } }
        public event RoutedEventHandler OpenClick { add { AddHandler(OpenClickEvent, value); } remove { RemoveHandler(OpenClickEvent, value); } }
        public event RoutedEventHandler SaveClick { add { AddHandler(SaveClickEvent, value); } remove { RemoveHandler(SaveClickEvent, value); } }
        public event RoutedEventHandler SaveAsClick { add { AddHandler(SaveAsClickEvent, value); } remove { RemoveHandler(SaveAsClickEvent, value); } }
        public event RoutedEventHandler SaveAsPdfClick { add { AddHandler(SaveAsPdfClickEvent, value); } remove { RemoveHandler(SaveAsPdfClickEvent, value); } }
        public event RoutedEventHandler PrintClick { add { AddHandler(PrintClickEvent, value); } remove { RemoveHandler(PrintClickEvent, value); } }
        public event RoutedEventHandler ExitClick { add { AddHandler(ExitClickEvent, value); } remove { RemoveHandler(ExitClickEvent, value); } }
        public event RoutedEventHandler CopyClick { add { AddHandler(CopyClickEvent, value); } remove { RemoveHandler(CopyClickEvent, value); } }
        public event RoutedEventHandler CutClick { add { AddHandler(CutClickEvent, value); } remove { RemoveHandler(CutClickEvent, value); } }
        public event RoutedEventHandler RecycleBinClick { add { AddHandler(RecycleBinClickEvent, value); } remove { RemoveHandler(RecycleBinClickEvent, value); } }
        public event RoutedEventHandler PasteClick { add { AddHandler(PasteClickEvent, value); } remove { RemoveHandler(PasteClickEvent, value); } }
        public event RoutedEventHandler ResizeCanvasClick { add { AddHandler(ResizeCanvasClickEvent, value); } remove { RemoveHandler(ResizeCanvasClickEvent, value); } }
        public event RoutedEventHandler BCEClick { add { AddHandler(BCEClickEvent, value); } remove { RemoveHandler(BCEClickEvent, value); } }
        public event RoutedEventHandler TTSClick { add { AddHandler(TTSClickEvent, value); } remove { RemoveHandler(TTSClickEvent, value); } }
        public event RoutedEventHandler BlackWhiteClick { add { AddHandler(BlackWhiteClickEvent, value); } remove { RemoveHandler(BlackWhiteClickEvent, value); } }
        public event RoutedEventHandler UndoClick { add { AddHandler(UndoClickEvent, value); } remove { RemoveHandler(UndoClickEvent, value); } }
        public event RoutedEventHandler RedoClick { add { AddHandler(RedoClickEvent, value); } remove { RemoveHandler(RedoClickEvent, value); } }
        public event RoutedEventHandler SettingsClick { add { AddHandler(SettingsClickEvent, value); } remove { RemoveHandler(SettingsClickEvent, value); } }
        public static readonly RoutedEvent DiscardImageClickEvent = EventManager.RegisterRoutedEvent("DiscardImageClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public event RoutedEventHandler DiscardImageClick { add { AddHandler(DiscardImageClickEvent, value); } remove { RemoveHandler(DiscardImageClickEvent, value); } }

        public bool IsUndoEnabled
        {
            get { return UndoButton.IsEnabled; }
            set { UndoButton.IsEnabled = value; }
        }

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
                catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
            }
        }
        public Button BtnUndo => UndoButton;
        public Button BtnRedo => RedoButton;
        public System.Windows.Shapes.Path IconUndo => UndoIcon;
        public System.Windows.Shapes.Path IconRedo => RedoIcon;
        public MenuBarControl()
        {
            InitializeComponent();
        }
        private void OnNewClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(NewClickEvent));
        private void OnOpenClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(OpenClickEvent));
        private void OnSaveClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SaveClickEvent));
        private void OnSaveAsClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SaveAsClickEvent));
        private void OnSaveAsPdfClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SaveAsPdfClickEvent));
        private void OnPrintClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(PrintClickEvent));
        private void OnExitClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ExitClickEvent));
        private void OnCopyClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(CopyClickEvent));
        private void OnCutClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(CutClickEvent));
        private void OnRecycleBinClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(RecycleBinClickEvent));
        private void OnPasteClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(PasteClickEvent));
        private void OnResizeCanvasClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ResizeCanvasClickEvent));
        private void OnBCEClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(BCEClickEvent));
        private void OnTTSClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(TTSClickEvent));
        private void OnBlackWhiteClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(BlackWhiteClickEvent));
        private void OnUndoClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(UndoClickEvent));
        private void OnRedoClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(RedoClickEvent));
        private void OnSettingsClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SettingsClickEvent));
        public static readonly RoutedEvent InvertClickEvent = EventManager.RegisterRoutedEvent("InvertClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent AutoLevelsClickEvent = EventManager.RegisterRoutedEvent("AutoLevelsClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public event RoutedEventHandler InvertClick { add { AddHandler(InvertClickEvent, value); } remove { RemoveHandler(InvertClickEvent, value); } }
        public event RoutedEventHandler AutoLevelsClick { add { AddHandler(AutoLevelsClickEvent, value); } remove { RemoveHandler(AutoLevelsClickEvent, value); } }
        private void OnInvertClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(InvertClickEvent));
        private void OnAutoLevelsClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(AutoLevelsClickEvent));
        public static readonly RoutedEvent WatermarkClickEvent = EventManager.RegisterRoutedEvent("WatermarkClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public event RoutedEventHandler WatermarkClick { add { AddHandler(WatermarkClickEvent, value); } remove { RemoveHandler(WatermarkClickEvent, value); } }

        private void OnWatermarkClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(WatermarkClickEvent));
        public event EventHandler<string> RecentFileClick;
        public event EventHandler ClearRecentFilesClick;

        public event RoutedEventHandler NewTabClick;
        private MenuItem CreateMenuItem(string headerResKey, string iconResKey, RoutedEventHandler clickHandler, string shortcutKey = null)
        {
            var item = new MenuItem
            {
                Style = (Style)FindResource("Win11MenuItemStyle")
            };
            item.SetResourceReference(HeaderedItemsControl.HeaderProperty, headerResKey);

            if (!string.IsNullOrEmpty(shortcutKey)) ShortcutService.SetShortcutKey(item, shortcutKey);

            if (clickHandler != null)item.Click += clickHandler;
            if (!string.IsNullOrEmpty(iconResKey))
            {
                var iconGeometry = TryGetResource(iconResKey) as Geometry;
                if (iconGeometry != null)
                {
                    var path = new Path
                    {
                        Data = iconGeometry,
                        Stretch = Stretch.Uniform,
                        Width = 16,
                        Height = 16
                    };
                    path.SetResourceReference(Shape.FillProperty, "IconFillBrush");
                    if (iconResKey == "Exit_Image" )
                    {
                        path.Fill = Brushes.Transparent;
                        path.SetResourceReference(Shape.StrokeProperty, "IconFillBrush");
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

        private object TryGetResource(string key)    { return this.TryFindResource(key);}
        private void OnFileMenuOpened(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;
            if (!_isFileMenuLoaded)
            {
                menuItem.Items.Clear(); // 清除占位符

                menuItem.Items.Add(CreateMenuItem("L_Menu_File_New", "NewFile_Image", OnNewClick, "File.New"));
                menuItem.Items.Add(CreateMenuItem("L_Menu_File_Open", "Open_Folder_Image", OnOpenClick, "File.Open"));
                menuItem.Items.Add(CreateMenuItem("L_Menu_File_OpenFolder", "Open_Folder_Image", OnOpenWorkspaceClick, "File.OpenWorkspace"));
                menuItem.Items.Add(CreateMenuItem("L_Menu_File_NewWindow", "New_Window_Image", OnNewWindowClick));
                RecentFilesMenuItem = new MenuItem
                {
                    Style = (Style)FindResource("Win11MenuItemStyle")
                };
                RecentFilesMenuItem.SetResourceReference(HeaderedItemsControl.HeaderProperty, "L_Menu_File_Recent");

                var resetPath = new Path { Stretch = Stretch.Uniform, Width = 16, Height = 16 };
                resetPath.SetResourceReference(Shape.FillProperty, "IconFillBrush");
                resetPath.SetResourceReference(Path.DataProperty, "Reset_Image");
                RecentFilesMenuItem.Icon = resetPath;

                menuItem.Items.Add(RecentFilesMenuItem);
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
        private void OnEffectMenuOpened(object sender, RoutedEventArgs e)
        {
            if (_isEffectMenuLoaded) return;
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            menuItem.Items.Clear();
            var bceItem = CreateMenuItem("L_Menu_Effect_BCE", "Brightness_Image", OnBCEClick, "Effect.Brightness");
            if (bceItem.Icon is Path p) // 修正BCE图标样式
            {
                p.Fill = Brushes.Transparent;
                p.SetResourceReference(Shape.StrokeProperty, "IconFillBrush");
                p.StrokeThickness = 1.5;
            }
            menuItem.Items.Add(bceItem);

            menuItem.Items.Add(CreateMenuItem("L_Menu_Effect_TTS", "Color_Temperature_Image", OnTTSClick, "Effect.Temperature"));
            var autoLevelsItem = CreateMenuItem("L_Menu_Effect_AutoLevels", null, OnAutoLevelsClick, "Effect.AutoLevels");
            var alPath = new Path
            {
                Data = Geometry.Parse("M5 7C5 5.34314 6.34315 4 8 4C9.65685 4 11 5.34315 11 7V25C11 26.6569 9.65685 28 8 28C6.34315 28 5 26.6569 5 25V7ZM8 6C7.44771 6 7 6.44772 7 7V25C7 25.5523 7.44772 26 8 26C8.55229 26 9 25.5523 9 25V7C9 6.44772 8.55228 6 8 6ZM13 13C13 11.3431 14.3431 10 16 10C17.6569 10 19 11.3431 19 13V25C19 26.6569 17.6569 28 16 28C14.3431 28 13 26.6569 13 25V13ZM16 12C15.4477 12 15 12.4477 15 13V25C15 25.5523 15.4477 26 16 26C16.5523 26 17 25.5523 17 25V13C17 12.4477 16.5523 12 16 12ZM24 16C22.3431 16 21 17.3431 21 19V25C21 26.6569 22.3431 28 24 28C25.6569 28 27 26.6569 27 25V19C27 17.3431 25.6569 16 24 16ZM23 19C23 18.4477 23.4477 18 24 18C24.5523 18 25 18.4477 25 19V25C25 25.5523 24.5523 26 24 26C23.4477 26 23 25.5523 23 25V19Z"),
                Stretch = Stretch.Uniform,
                Width = 16,
                Height = 16
            };
            alPath.SetResourceReference(Shape.FillProperty, "IconFillBrush");
            autoLevelsItem.Icon = alPath;
            menuItem.Items.Add(autoLevelsItem);

            menuItem.Items.Add(new Separator { Style = (Style)FindResource("MenuSeparator") });
            var filterItem = new MenuItem
            {
                Style = (Style)FindResource("Win11MenuItemStyle")
            };
            filterItem.SetResourceReference(HeaderedItemsControl.HeaderProperty, "L_Menu_Effect_Filter");

            var filterIcon = new Path { Stretch = Stretch.Uniform, Width = 16, Height = 16 };
            filterIcon.SetResourceReference(Path.DataProperty, "Filter_Image");
            filterIcon.SetResourceReference(Shape.FillProperty, "IconFillBrush");
            filterItem.Icon = filterIcon;
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Sepia", "Sepia_Image", OnSepiaClick)); // 填充滤镜子项
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Oil", "OilPaint_Image", OnOilPaintingClick));
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Vignette", "Vignette_Image", OnVignetteClick));
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Glow", "Glow_Image", OnGlowClick));

            filterItem.Items.Add(new Separator { Style = (Style)FindResource("MenuSeparator") });

            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_BW", "Black_And_White_Image", OnBlackWhiteClick, "Effect.Grayscale"));
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Invert", "Invert_Color_Image", OnInvertClick, "Effect.Invert"));

            var sharpenItem = CreateMenuItem("L_Menu_Effect_Sharpen", null, OnSharpenClick);
            var shPath = new Path
            {
                Data = Geometry.Parse("M12,2L1,21H23M12,6L19.53,19H4.47"),
                Stretch = Stretch.Uniform,
                Width = 16,
                Height = 16
            };
            shPath.SetResourceReference(Shape.FillProperty, "IconFillBrush");
            sharpenItem.Icon = shPath;
            filterItem.Items.Add(sharpenItem);

            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Brown", "Sepia_Image", OnBrownClick));
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Mosaic", "Mosaic_Image", OnMosaicClick));
            var blurItem = CreateMenuItem("L_Menu_Effect_GaussianBlur", null, OnGaussianBlurClick);
            var blurPath = new Path
            {
                Data = Geometry.Parse("M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4M12,6A6,6 0 0,0 6,12A6,6 0 0,0 12,18A6,6 0 0,0 18,12A6,6 0 0,0 12,6M12,8A4,4 0 0,1 16,12A4,4 0 0,1 12,16A4,4 0 0,1 8,12A4,4 0 0,1 12,8Z"),
                Stretch = Stretch.Uniform,
                Width = 16,
                Height = 16
            };
            blurPath.SetResourceReference(Shape.FillProperty, "IconFillBrush");
            blurItem.Icon = blurPath;
            filterItem.Items.Add(blurItem);

            filterItem.Items.Add(new Separator { Style = (Style)FindResource("MenuSeparator") });
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_RedEye", "Eye_Image", OnRedEyeClick));
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Sketch", "Crayon_Image", OnSketchClick));
            filterItem.Items.Add(CreateMenuItem("L_Menu_Effect_Edge", "FitToWindow_Image", OnEdgeClick));

            menuItem.Items.Add(filterItem);
            menuItem.Items.Add(CreateMenuItem("L_Menu_Effect_Resize", "Resize_Image", OnResizeCanvasClick, "Effect.Resize"));
            var wmItem = CreateMenuItem("L_Menu_Effect_Watermark", "Watermark_Image", OnWatermarkClick);// 画布调整
            if (wmItem.Icon is Path wp)
            {
                wp.Fill = Brushes.Transparent;
                wp.SetResourceReference(Shape.StrokeProperty, "IconFillBrush");
                wp.StrokeThickness = 0.8;
            }
            menuItem.Items.Add(wmItem);

            _isEffectMenuLoaded = true;
        }
        private MenuItem RecentFilesMenuItem;
        private void OnNewTabClick(object sender, RoutedEventArgs e)
        {
            NewTabClick?.Invoke(this, e);
        }
        private void UpdateRecentFilesMenu()
        {
            RecentFilesMenuItem.Items.Clear();

            var files = TabPaint.SettingsManager.Instance.Current.RecentFiles;

            if (files == null || files.Count == 0)
            {
                var emptyItem = new MenuItem
                {
                    IsEnabled = false,
                    Style = (Style)FindResource("SubMenuItemStyle")
                };
                emptyItem.SetResourceReference(HeaderedItemsControl.HeaderProperty, "L_Menu_Recent_None");

                RecentFilesMenuItem.Items.Add(emptyItem);
            }
            else
            {
                foreach (var file in files)
                {
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
                RecentFilesMenuItem.Items.Add(new Separator { Style = (Style)FindResource("MenuSeparator") });
                var clearItem = new MenuItem { Style = (Style)FindResource("SubMenuItemStyle") };
                clearItem.SetResourceReference(HeaderedItemsControl.HeaderProperty, "L_Menu_Recent_Clear");

                clearItem.Click += (s, e) => { ClearRecentFilesClick?.Invoke(this, EventArgs.Empty); };
                RecentFilesMenuItem.Items.Add(clearItem);
            }
        }
        public static readonly RoutedEvent DiscardAllClickEvent = EventManager.RegisterRoutedEvent("DiscardAllClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public event RoutedEventHandler DiscardAllClick
        {
            add { AddHandler(DiscardAllClickEvent, value); }
            remove { RemoveHandler(DiscardAllClickEvent, value); }
        }
        private void OnDiscardAllClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(DiscardAllClickEvent));
        private void OnDiscardImageClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(DiscardImageClickEvent));

        private void OnRecentFileItemClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string path)
            {
                RecentFileClick?.Invoke(this, path);
            }
        }
        public static readonly RoutedEvent OpenWorkspaceClickEvent = EventManager.RegisterRoutedEvent(
    "OpenWorkspaceClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public event RoutedEventHandler OpenWorkspaceClick
        {
            add { AddHandler(OpenWorkspaceClickEvent, value); }
            remove { RemoveHandler(OpenWorkspaceClickEvent, value); }
        }
        private void OnOpenWorkspaceClick(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(OpenWorkspaceClickEvent));
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
        }
        public static readonly RoutedEvent NewWindowClickEvent = EventManager.RegisterRoutedEvent(
    "NewWindowClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public event RoutedEventHandler NewWindowClick
        {
            add { AddHandler(NewWindowClickEvent, value); }
            remove { RemoveHandler(NewWindowClickEvent, value); }
        }
        private void OnNewWindowClick(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(NewWindowClickEvent));
        }
        public static readonly RoutedEvent SepiaClickEvent = EventManager.RegisterRoutedEvent("SepiaClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent OilPaintingClickEvent = EventManager.RegisterRoutedEvent("OilPaintingClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent VignetteClickEvent = EventManager.RegisterRoutedEvent("VignetteClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent GlowClickEvent = EventManager.RegisterRoutedEvent("GlowClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));

        public event RoutedEventHandler SepiaClick { add { AddHandler(SepiaClickEvent, value); } remove { RemoveHandler(SepiaClickEvent, value); } }
        public event RoutedEventHandler OilPaintingClick { add { AddHandler(OilPaintingClickEvent, value); } remove { RemoveHandler(OilPaintingClickEvent, value); } }
        public event RoutedEventHandler VignetteClick { add { AddHandler(VignetteClickEvent, value); } remove { RemoveHandler(VignetteClickEvent, value); } }
        public event RoutedEventHandler GlowClick { add { AddHandler(GlowClickEvent, value); } remove { RemoveHandler(GlowClickEvent, value); } }

        private void OnSepiaClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SepiaClickEvent));
        private void OnOilPaintingClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(OilPaintingClickEvent));
        private void OnVignetteClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(VignetteClickEvent));
        private void OnGlowClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(GlowClickEvent));

        public static readonly RoutedEvent SharpenClickEvent = EventManager.RegisterRoutedEvent("SharpenClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent BrownClickEvent = EventManager.RegisterRoutedEvent("BrownClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));

        public event RoutedEventHandler SharpenClick { add { AddHandler(SharpenClickEvent, value); } remove { RemoveHandler(SharpenClickEvent, value); } }
        public event RoutedEventHandler BrownClick { add { AddHandler(BrownClickEvent, value); } remove { RemoveHandler(BrownClickEvent, value); } }

        private void OnSharpenClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SharpenClickEvent));
        private void OnBrownClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(BrownClickEvent));
        public static readonly RoutedEvent MosaicClickEvent = EventManager.RegisterRoutedEvent("MosaicClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent GaussianBlurClickEvent = EventManager.RegisterRoutedEvent("GaussianBlurClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent RedEyeClickEvent = EventManager.RegisterRoutedEvent("RedEyeClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent SketchClickEvent = EventManager.RegisterRoutedEvent("SketchClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));
        public static readonly RoutedEvent EdgeClickEvent = EventManager.RegisterRoutedEvent("EdgeClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuBarControl));

        public event RoutedEventHandler MosaicClick { add { AddHandler(MosaicClickEvent, value); } remove { RemoveHandler(MosaicClickEvent, value); } }
        public event RoutedEventHandler GaussianBlurClick { add { AddHandler(GaussianBlurClickEvent, value); } remove { RemoveHandler(GaussianBlurClickEvent, value); } }
        public event RoutedEventHandler RedEyeClick { add { AddHandler(RedEyeClickEvent, value); } remove { RemoveHandler(RedEyeClickEvent, value); } }
        public event RoutedEventHandler SketchClick { add { AddHandler(SketchClickEvent, value); } remove { RemoveHandler(SketchClickEvent, value); } }
        public event RoutedEventHandler EdgeClick { add { AddHandler(EdgeClickEvent, value); } remove { RemoveHandler(EdgeClickEvent, value); } }

        private void OnMosaicClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(MosaicClickEvent));
        private void OnGaussianBlurClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(GaussianBlurClickEvent));
        private void OnRedEyeClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(RedEyeClickEvent));
        private void OnSketchClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SketchClickEvent));
        private void OnEdgeClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(EdgeClickEvent));

    }
}
