//
//ToolBarControl.xaml.cs
//工具栏控件，包含画笔、选区、填充、文字等工具的选择按钮，以及颜色板和旋转镜像功能。
//
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
namespace TabPaint.Controls
{
    public partial class ToolBarControl : UserControl
    {
        public static readonly RoutedEvent PenClickEvent = RegisterEvent("PenClick");
        public static readonly RoutedEvent PickColorClickEvent = RegisterEvent("PickColorClick");
        public static readonly RoutedEvent EraserClickEvent = RegisterEvent("EraserClick");
        public static readonly RoutedEvent SelectClickEvent = RegisterEvent("SelectClick");
        public static readonly RoutedEvent FillClickEvent = RegisterEvent("FillClick");
        public static readonly RoutedEvent TextClickEvent = RegisterEvent("TextClick");
        public static readonly RoutedEvent BrushStyleClickEvent = RegisterEvent("BrushStyleClick");
        public static readonly RoutedEvent ShapeStyleClickEvent = RegisterEvent("ShapeStyleClick");
        public static readonly RoutedEvent CropClickEvent = RegisterEvent("CropClick");
        public static readonly RoutedEvent RotateLeftClickEvent = RegisterEvent("RotateLeftClick");
        public static readonly RoutedEvent RotateRightClickEvent = RegisterEvent("RotateRightClick");
        public static readonly RoutedEvent Rotate180ClickEvent = RegisterEvent("Rotate180Click");
        public static readonly RoutedEvent FlipVerticalClickEvent = RegisterEvent("FlipVerticalClick");
        public static readonly RoutedEvent FlipHorizontalClickEvent = RegisterEvent("FlipHorizontalClick");
        public static readonly RoutedEvent CustomColorClickEvent = RegisterEvent("CustomColorClick");
        public static readonly RoutedEvent ColorOneClickEvent = RegisterEvent("ColorOneClick");
        public static readonly RoutedEvent ColorTwoClickEvent = RegisterEvent("ColorTwoClick");
        public static readonly RoutedEvent ColorButtonClickEvent = RegisterEvent("ColorButtonClick");
        public static readonly RoutedEvent BackgroundMouseDownEvent = RegisterEvent("BackgroundMouseDown");

        public event RoutedEventHandler PenClick { add => AddHandler(PenClickEvent, value); remove => RemoveHandler(PenClickEvent, value); }
        public event RoutedEventHandler PickColorClick { add => AddHandler(PickColorClickEvent, value); remove => RemoveHandler(PickColorClickEvent, value); }
        public event RoutedEventHandler EraserClick { add => AddHandler(EraserClickEvent, value); remove => RemoveHandler(EraserClickEvent, value); }
        public event RoutedEventHandler SelectClick { add => AddHandler(SelectClickEvent, value); remove => RemoveHandler(SelectClickEvent, value); }
        public event RoutedEventHandler FillClick { add => AddHandler(FillClickEvent, value); remove => RemoveHandler(FillClickEvent, value); }
        public event RoutedEventHandler TextClick { add => AddHandler(TextClickEvent, value); remove => RemoveHandler(TextClickEvent, value); }

        public event RoutedEventHandler BrushStyleClick { add => AddHandler(BrushStyleClickEvent, value); remove => RemoveHandler(BrushStyleClickEvent, value); }
        public event RoutedEventHandler ShapeStyleClick { add => AddHandler(ShapeStyleClickEvent, value); remove => RemoveHandler(ShapeStyleClickEvent, value); }

        public event RoutedEventHandler CropClick { add => AddHandler(CropClickEvent, value); remove => RemoveHandler(CropClickEvent, value); }
        public event RoutedEventHandler RotateLeftClick { add => AddHandler(RotateLeftClickEvent, value); remove => RemoveHandler(RotateLeftClickEvent, value); }
        public event RoutedEventHandler RotateRightClick { add => AddHandler(RotateRightClickEvent, value); remove => RemoveHandler(RotateRightClickEvent, value); }
        public event RoutedEventHandler Rotate180Click { add => AddHandler(Rotate180ClickEvent, value); remove => RemoveHandler(Rotate180ClickEvent, value); }
        public event RoutedEventHandler FlipVerticalClick { add => AddHandler(FlipVerticalClickEvent, value); remove => RemoveHandler(FlipVerticalClickEvent, value); }
        public event RoutedEventHandler FlipHorizontalClick { add => AddHandler(FlipHorizontalClickEvent, value); remove => RemoveHandler(FlipHorizontalClickEvent, value); }

        public event RoutedEventHandler CustomColorClick { add => AddHandler(CustomColorClickEvent, value); remove => RemoveHandler(CustomColorClickEvent, value); }
        public event RoutedEventHandler ColorOneClick { add => AddHandler(ColorOneClickEvent, value); remove => RemoveHandler(ColorOneClickEvent, value); }
        public event RoutedEventHandler ColorTwoClick { add => AddHandler(ColorTwoClickEvent, value); remove => RemoveHandler(ColorTwoClickEvent, value); }
        public event RoutedEventHandler ColorButtonClick { add => AddHandler(ColorButtonClickEvent, value); remove => RemoveHandler(ColorButtonClickEvent, value); }
        public event RoutedEventHandler BackgroundMouseDown { add => AddHandler(BackgroundMouseDownEvent, value); remove => RemoveHandler(BackgroundMouseDownEvent, value); }

        public static readonly RoutedEvent BrushMainClickEvent = RegisterEvent("BrushMainClick");
        public event RoutedEventHandler BrushMainClick { add => AddHandler(BrushMainClickEvent, value); remove => RemoveHandler(BrushMainClickEvent, value); }
        private bool _isSelectMenuLoaded = false;
        private bool _isCollapsedToolsLoaded = false;
        private bool _isBrushMenuLoaded = false;
        private bool _isShapeMenuLoaded = false;
        private bool _isRotateMenuLoaded = false;
        // 新增：当点击形状主按钮（左侧）时触发
        public static readonly RoutedEvent ShapeMainClickEvent = RegisterEvent("ShapeMainClick");
        public event RoutedEventHandler ShapeMainClick { add => AddHandler(ShapeMainClickEvent, value); remove => RemoveHandler(ShapeMainClickEvent, value); }
        private void OnBrushMainButtonClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(BrushMainClickEvent));
        private void OnShapeMainButtonClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ShapeMainClickEvent));
        public ToolBarControl()
        {
            InitializeComponent();
            this.Loaded += (s, e) => EnsureColorsLoaded();
        }
        private bool _isColorsLoaded = false;
        private void EnsureColorsLoaded()
        {
            if (_isColorsLoaded || BasicColorsGrid == null) return;

            string[] colors = {
                "#FFFFFF", "#C0C0C0", "#FF0000", "#FF8000", "#FFFF00", "#00FF00", "#00FFFF", "#0080FF", "#8000FF", "#FF00FF", "#D2691E",
                "#000000", "#808080", "#CC0000", "#CC6600", "#FFD700", "#008000", "#008080", "#0000FF", "#800080", "#C71585", "#A0522D"
            };

            string[] tooltips = {
                "L_ToolBar_Color_White", "L_ToolBar_Color_Silver", "L_ToolBar_Color_Red", "L_ToolBar_Color_Orange", "L_ToolBar_Color_Yellow", "L_ToolBar_Color_Lime", "L_ToolBar_Color_Cyan", "L_ToolBar_Color_SkyBlue", "L_ToolBar_Color_Violet", "L_ToolBar_Color_Magenta", "L_ToolBar_Color_Chocolate",
                "L_ToolBar_Color_Black", "L_ToolBar_Color_Gray", "L_ToolBar_Color_DarkRed", "L_ToolBar_Color_DarkOrange", "L_ToolBar_Color_Gold", "L_ToolBar_Color_Green", "L_ToolBar_Color_Teal", "L_ToolBar_Color_Blue", "L_ToolBar_Color_Purple", "L_ToolBar_Color_Maroon", "L_ToolBar_Color_Sienna"
            };

            var swatchStyle = FindResource("ColorSwatch") as Style;

            for (int i = 0; i < colors.Length; i++)
            {
                var btn = new Button
                {
                    Style = swatchStyle,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]))
                };
                btn.SetResourceReference(FrameworkElement.ToolTipProperty, tooltips[i]);
                btn.Click += Swatch_Click;
                BasicColorsGrid.Children.Add(btn);
            }

            _isColorsLoaded = true;
            UpdateColorPaletteVisibility();
        }
        public void UpdateBrushIcon(string resourceKey, bool isPathData)
        {
            if (isPathData)
            {
                var pathData = TryFindResource(resourceKey) as Geometry;
                if (pathData != null)
                {
                    var path = new System.Windows.Shapes.Path
                    {
                        Data = pathData,
                        Stretch = Stretch.Uniform,
                        Fill = (Brush)TryFindResource("IconFillBrush"), // 确保你有这个Brush资源
                        Width = 16,
                        Height = 16
                    };
                    CurrentBrushIconHost.Content = path;
                }
            }
            else
            {
                var imgSrc = TryFindResource(resourceKey) as ImageSource;
                if (imgSrc != null)
                {
                    var img = new Image { Source = imgSrc, Width = 16, Height = 16 };
                    CurrentBrushIconHost.Content = img;
                }
            }
        }
        private MenuItem CreateMenuItem(string headerKey, object iconSource, RoutedEventHandler clickHandler, string tag = null, bool isPathIcon = false, bool isStrokePath = false)
        {
            var item = new MenuItem
            {
                Style = (Style)FindResource("SubMenuItemStyle"),
                Tag = tag
            };
            item.SetResourceReference(HeaderedItemsControl.HeaderProperty, headerKey);

            if (clickHandler != null) item.Click += clickHandler;

            if (iconSource != null)
            {
                if (isPathIcon)
                {
                    var path = new System.Windows.Shapes.Path
                    {
                        Stretch = Stretch.Uniform,
                        Width = 16,
                        Height = 16
                    };
                    if (iconSource is string keyOrData)
                    {
                        var geoRes = TryFindResource(keyOrData) as Geometry;
                        if (geoRes != null)
                        {
                            path.Data = geoRes;
                        }
                        else
                        {
                            try   {   path.Data = Geometry.Parse(keyOrData); }
                            catch { /* 忽略解析错误 */ }
                        }
                    }
                    else if (iconSource is Geometry geo)  path.Data = geo;

                    if (isStrokePath)
                    {
                        path.Fill = Brushes.Transparent;
                        path.SetResourceReference(Shape.StrokeProperty, "IconFillBrush");
                        path.StrokeThickness = 1.5;
                        path.StrokeLineJoin = PenLineJoin.Round;
                        path.StrokeEndLineCap = PenLineCap.Round;
                        path.StrokeStartLineCap = PenLineCap.Round;
                    }
                    else  path.SetResourceReference(Shape.FillProperty, "IconFillBrush");
                    item.Icon = path;
                }
                else
                {
                    var img = new Image { Width = 16, Height = 16 };
                    if (iconSource is string key)
                        img.SetResourceReference(Image.SourceProperty, key);

                    item.Icon = img;
                }
            }

            return item;
        }
        private void OnSelectMenuOpened(object sender, RoutedEventArgs e)
        {
            if (_isSelectMenuLoaded) return;

            StackPanelSelect.Children.Add(CreateMenuItem("L_ToolBar_Tool_Select", "Select_Image", OnSelectStyleClick_Forward, "Rectangle", isPathIcon: false));
            StackPanelSelect.Children.Add(CreateMenuItem("L_ToolBar_Tool_Select_Lasso", "Lasso_Image", OnSelectStyleClick_Forward, "Lasso", isPathIcon: true));
            StackPanelSelect.Children.Add(CreateMenuItem("L_ToolBar_Tool_Select_MagicWand", "Wand_Image", OnSelectStyleClick_Forward, "MagicWand", isPathIcon: true));

            _isSelectMenuLoaded = true;
        }
        private void OnCollapsedToolsOpened(object sender, RoutedEventArgs e)
        {
            if (_isCollapsedToolsLoaded) return;

            CollapsedMenuItems.Children.Add(CreateMenuItem("L_ToolBar_Tool_Select", "Select_Image", OnSelectClick_Forward, "SelectTool"));
            CollapsedMenuItems.Children.Add(CreateMenuItem("L_ToolBar_Tool_Pen", "Pencil_Image", OnPenClick_Forward, "PenTool"));
            CollapsedMenuItems.Children.Add(CreateMenuItem("L_ToolBar_Tool_Eyedropper", "Pick_Colour_Image", OnPickColorClick_Forward, "EyedropperTool"));
            CollapsedMenuItems.Children.Add(CreateMenuItem("L_ToolBar_Tool_Eraser", "Eraser_Image", OnEraserClick_Forward, "EraserTool"));
            CollapsedMenuItems.Children.Add(CreateMenuItem("L_ToolBar_Tool_Fill", "Fill_Bucket_Image", OnFillClick_Forward, "FillTool"));
            CollapsedMenuItems.Children.Add(CreateMenuItem("L_ToolBar_Tool_Text", "Text_Image", OnTextClick_Forward, "TextTool"));

            _isCollapsedToolsLoaded = true;
        }
        private void OnBrushMenuOpened(object sender, RoutedEventArgs e)
        {
            if (_isBrushMenuLoaded) return;

            StackPanelBrush.Children.Add(CreateMenuItem("L_ToolBar_Brush_Round", "Brush_Normal_Image", OnBrushStyleClick_Forward, "Round", true));
            StackPanelBrush.Children.Add(CreateMenuItem("L_ToolBar_Brush_Square", "Brush_Rect_Image", OnBrushStyleClick_Forward, "Square", true));
            StackPanelBrush.Children.Add(CreateMenuItem("L_ToolBar_Brush_Brush", "Brush_Normal_Image", OnBrushStyleClick_Forward, "Brush", true));
            StackPanelBrush.Children.Add(CreateMenuItem("L_ToolBar_Brush_Calligraphy", "Brush_Image", OnBrushStyleClick_Forward, "Calligraphy", false)); // 这是一个 Image
            StackPanelBrush.Children.Add(CreateMenuItem("L_ToolBar_Brush_Spray", "Paint_Spray_Image", OnBrushStyleClick_Forward, "Spray", true));

            StackPanelBrush.Children.Add(new Separator { Style = (Style)FindResource("MenuSeparator") });

            StackPanelBrush.Children.Add(CreateMenuItem("L_ToolBar_Brush_Crayon", "Crayon_Image", OnBrushStyleClick_Forward, "Crayon", true));
            StackPanelBrush.Children.Add(CreateMenuItem("L_ToolBar_Brush_Watercolor", "Watercolor_Image", OnBrushStyleClick_Forward, "Watercolor", true));
            StackPanelBrush.Children.Add(CreateMenuItem("L_ToolBar_Brush_Highlighter", "Highlighter_Image", OnBrushStyleClick_Forward, "Highlighter", false)); // 这是一个 Image
            StackPanelBrush.Children.Add(CreateMenuItem("L_ToolBar_Brush_Mosaic", "Mosaic_Image", OnBrushStyleClick_Forward, "Mosaic", true));
            StackPanelBrush.Children.Add(CreateMenuItem("L_ToolBar_Brush_Blur", "Blur_Image", OnBrushStyleClick_Forward, "GaussianBlur", true));
            StackPanelBrush.Children.Add(CreateMenuItem("L_ToolBar_AIEraser", "AIEraser_Image", OnBrushStyleClick_Forward, "AiEraser", true));
            StackPanelBrush.Children.Add(CreateMenuItem("L_ToolBar_Brush_Gradient", "Brush_Normal_Image", OnBrushStyleClick_Forward, "Gradient", true));

            _isBrushMenuLoaded = true;
        }
        private void OnShapeMenuOpened(object sender, RoutedEventArgs e)
        {
            if (_isShapeMenuLoaded) return;
            StackPanelShape.Children.Add(CreateMenuItem("L_ToolBar_Shape_Rectangle", "Icon_Shape_Rectangle", OnShapeStyleClick_Forward, "Rectangle", true, true));
            StackPanelShape.Children.Add(CreateMenuItem("L_ToolBar_Shape_RoundedRectangle", "Icon_Shape_RoundedRect", OnShapeStyleClick_Forward, "RoundedRectangle", true, true));
            StackPanelShape.Children.Add(CreateMenuItem("L_ToolBar_Shape_Ellipse", "Icon_Shape_Ellipse", OnShapeStyleClick_Forward, "Ellipse", true, true));
            StackPanelShape.Children.Add(CreateMenuItem("L_ToolBar_Shape_Line", "Icon_Shape_Line", OnShapeStyleClick_Forward, "Line", true, true));
            StackPanelShape.Children.Add(CreateMenuItem("L_ToolBar_Shape_Arrow", "Icon_Shape_Arrow", OnShapeStyleClick_Forward, "Arrow", true, true));

            StackPanelShape.Children.Add(new Separator { Style = (Style)FindResource("MenuSeparator") });
            string triangleData = "M 8,2 L 15,14 L 1,14 Z";
            string diamondData = "M 8,1 L 15,8 L 8,15 L 1,8 Z";
            string pentagonData = "M 8,1 L 15,6 L 12,15 L 4,15 L 1,6 Z";
            string starData = "M 8,1 L 10,6 L 16,6 L 11,10 L 13,15 L 8,12 L 3,15 L 5,10 L 0,6 L 6,6 Z";
            string bubbleData = "M 2,2 H 14 A 2,2 0 0 1 16,4 V 11 A 2,2 0 0 1 14,13 H 10 L 10,16 L 7,13 H 2 A 2,2 0 0 1 0,11 V 4 A 2,2 0 0 1 2,2 Z";

            StackPanelShape.Children.Add(CreateMenuItem("L_ToolBar_Shape_Triangle", triangleData, OnShapeStyleClick_Forward, "Triangle", true, true)); // True, True
            StackPanelShape.Children.Add(CreateMenuItem("L_ToolBar_Shape_Diamond", diamondData, OnShapeStyleClick_Forward, "Diamond", true, true));   // True, True
            StackPanelShape.Children.Add(CreateMenuItem("L_ToolBar_Shape_Pentagon", pentagonData, OnShapeStyleClick_Forward, "Pentagon", true, true)); // True, True
            StackPanelShape.Children.Add(CreateMenuItem("L_ToolBar_Shape_Star", starData, OnShapeStyleClick_Forward, "Star", true, true));         // True, True
            StackPanelShape.Children.Add(CreateMenuItem("L_ToolBar_Shape_Bubble", bubbleData, OnShapeStyleClick_Forward, "Bubble", true, true));     // True, True

            _isShapeMenuLoaded = true;
        }
        private void OnRotateMenuOpened(object sender, RoutedEventArgs e)
        {
            if (_isRotateMenuLoaded) return;

            StackPanelRotate.Children.Add(CreateMenuItem("L_ToolBar_Rotate_Left", "Rotate_Left_Image", OnRotateLeftClick_Forward, null, true));
            StackPanelRotate.Children.Add(CreateMenuItem("L_ToolBar_Rotate_Right", "Rotate_Right_Image", OnRotateRightClick_Forward, null, true));
            StackPanelRotate.Children.Add(CreateMenuItem("L_ToolBar_Rotate_180", "Spin180_Image", OnRotate180Click_Forward, null, false)); // Image

            StackPanelRotate.Children.Add(new Separator { Style = (Style)FindResource("MenuSeparator") });

            // 翻转图标是 Stroke 风格的 Path
            StackPanelRotate.Children.Add(CreateMenuItem("L_ToolBar_Flip_Vertical", "Flip_Vertical_Image", OnFlipVerticalClick_Forward, null, true, true));
            StackPanelRotate.Children.Add(CreateMenuItem("L_ToolBar_Flip_Horizontal", "Flip_Horizontal_Image", OnFlipHorizontalClick_Forward, null, true, true));

            _isRotateMenuLoaded = true;
        }
        public event RoutedEventHandler SelectMainClick;   // 对应主按钮点击
        public event RoutedEventHandler SelectStyleClick;  // 对应下拉菜单点击
        private void OnSelectMainButtonClick(object sender, RoutedEventArgs e)
        {
            SelectMainClick?.Invoke(this, e);
        }
        private void OnSelectStyleClick_Forward(object sender, RoutedEventArgs e)
        {
            SubMenuPopupSelect.IsOpen = false;
            SelectStyleClick?.Invoke(sender, e);
        }
        public void UpdateShapeIcon(string resourceKey)
        {
            var pathData = TryFindResource(resourceKey) as Geometry;
            if (pathData != null)
            {
                var path = new System.Windows.Shapes.Path
                {
                    Data = pathData,
                    Stretch = Stretch.Uniform,
                    Fill = Brushes.Transparent,
                    Stroke = (Brush)TryFindResource("IconFillBrush"),
                    StrokeThickness = 1.5,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    Width = 16,
                    Height = 16
                };
                CurrentShapeIconHost.Content = path;
            }
        }
        private static RoutedEvent RegisterEvent(string name)
        {
            return EventManager.RegisterRoutedEvent(name, RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToolBarControl));
        }
        private void OnPenClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(PenClickEvent));
        private void OnPickColorClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(PickColorClickEvent));
        private void OnEraserClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(EraserClickEvent));
        private void OnSelectClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SelectClickEvent));
        private void OnFillClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(FillClickEvent));
        private void OnTextClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(TextClickEvent));
        private void OnBrushStyleClick_Forward(object sender, RoutedEventArgs e)
        {
            BrushToggle.IsChecked = false;
            RaiseEvent(new RoutedEventArgs(BrushStyleClickEvent, sender));
        }

        private void OnShapeStyleClick_Forward(object sender, RoutedEventArgs e)
        {
            ShapeToggle.IsChecked = false;
            RaiseEvent(new RoutedEventArgs(ShapeStyleClickEvent, sender));
        }

        private void CropMenuItem_Click_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(CropClickEvent));
        private void OnRotateLeftClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(RotateLeftClickEvent));
        private void OnRotateRightClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(RotateRightClickEvent));

        private void OnRotate180Click_Forward(object sender, RoutedEventArgs e)
        {
            RotateFlipMenuToggle.IsChecked = false;
            RaiseEvent(new RoutedEventArgs(Rotate180ClickEvent));
        }
        private void OnFlipVerticalClick_Forward(object sender, RoutedEventArgs e)
        {
            RotateFlipMenuToggle.IsChecked = false;
            RaiseEvent(new RoutedEventArgs(FlipVerticalClickEvent));
        }
        private void OnFlipHorizontalClick_Forward(object sender, RoutedEventArgs e)
        {
            RotateFlipMenuToggle.IsChecked = false;
            RaiseEvent(new RoutedEventArgs(FlipHorizontalClickEvent));
        }

        private void OnCustomColorClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(CustomColorClickEvent));
        private void OnColorOneClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ColorOneClickEvent, sender)); // 需要 sender 获取 Tag
        private void OnColorTwoClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ColorTwoClickEvent, sender));

        private void OnColorButtonClick_Forward(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ColorButtonClickEvent, sender));
        }

        private void OnBackgroundMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Border || e.OriginalSource is DockPanel || e.OriginalSource is StackPanel)
            {
                var args = new System.Windows.Input.MouseButtonEventArgs(e.MouseDevice, e.Timestamp, e.ChangedButton)
                {
                    RoutedEvent = BackgroundMouseDownEvent
                };
                RaiseEvent(args);
            }
        }

        private void OnToolBarSizeChanged(object sender, SizeChangedEventArgs e)
        {
            double collapseThreshold = 580;

            if (e.NewSize.Width < collapseThreshold)
            {
                if (ExpandedToolsPanel.Visibility == Visibility.Visible)
                {
                    ExpandedToolsPanel.Visibility = Visibility.Collapsed;
                    CollapsedToolsPanel.Visibility = Visibility.Visible;
                }
            }
            else
            {
                if (ExpandedToolsPanel.Visibility == Visibility.Collapsed)
                {
                    ExpandedToolsPanel.Visibility = Visibility.Visible;
                    CollapsedToolsPanel.Visibility = Visibility.Collapsed;
                    ToolsMenuToggle.IsChecked = false;
                }
            }
            RightColorSeperator.Visibility = e.NewSize.Width>435 ? Visibility.Visible : Visibility.Collapsed;
            UpdateColorPaletteVisibility();
        }
        private void UpdateColorPaletteVisibility()
        {
            if (BasicColorsGrid == null) return;

            // 1. 基础配置
            double buttonWidth = 19.0; // 16px宽度 + 3px Margin(左右各1.5)
            double rightPadding = 45.0;

            double leftUsedWidth = 0;
            try
            {
                Point p = BasicColorsGrid.TranslatePoint(new Point(0, 0), this);
                leftUsedWidth = p.X;
            }
            catch
            {
                leftUsedWidth = 600;
            }

            double availableWidth = this.ActualWidth - leftUsedWidth - rightPadding;
            if (availableWidth < 0) availableWidth = 0;
            int visibleColumns = (int)(availableWidth / buttonWidth);

            if (visibleColumns > 11) visibleColumns = 11;
            if (visibleColumns < 1) visibleColumns = 1; // 至少显示1列

            BasicColorsGrid.Columns = visibleColumns;

            int totalItems = BasicColorsGrid.Children.Count;
            int originalColumns = 11;

            for (int i = 0; i < totalItems; i++)
            {
                UIElement child = BasicColorsGrid.Children[i];

                int originalColIndex = i % originalColumns;

                if (originalColIndex < visibleColumns)
                {
                    if (child.Visibility != Visibility.Visible)
                        child.Visibility = Visibility.Visible;
                }
                else
                {
                    if (child.Visibility != Visibility.Collapsed)
                        child.Visibility = Visibility.Collapsed;
                }
            }
        }


        private void Swatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Background is SolidColorBrush brush)
            {
                var mw = MainWindow.GetCurrentInstance();
                Color selectedColor = brush.Color;

                mw.SelectedBrush = new SolidColorBrush(selectedColor);

                mw._ctx.PenColor = selectedColor;

                mw.UpdateCurrentColor(selectedColor, mw.useSecondColor);

                mw.UpdateColorHighlight(); 
            }
        }

    }
}
