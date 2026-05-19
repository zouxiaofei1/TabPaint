
//
//ITools.cs
//绘图工具接口与注册器，定义了工具的基本行为规范，并管理画笔、选区、文字等多种工具。
//
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.UI.Text;
using static TabPaint.MainWindow;


namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public void UpdateToolSelectionHighlight()
        {
            if (_router == null || MainToolBar == null) return;

            var accentBrush = Application.Current.FindResource("ToolAccentBrush") as Brush;
            var accentSubtleBrush = Application.Current.FindResource("ToolAccentSubtleSelectedBrush") as Brush;

            string currentToolTag = _router.CurrentTool switch
            {
                SelectTool => "SelectTool",
                PenTool when _ctx.PenStyle == BrushStyle.Eraser => "EraserTool",
                PenTool when _ctx.PenStyle == BrushStyle.Pencil => "PenTool",
                EyedropperTool => "EyedropperTool",
                FillTool => "FillTool",
                TextTool => "TextTool",
                GradientTool => "GradientTool",
                _ => ""
            };

            bool isCollapsedMode = MainToolBar.CollapsedToolsPanel.Visibility == Visibility.Visible;

            // ========== 清除所有状态 ==========
            MainToolBar.ToolsMenuToggle.ClearValue(Control.BackgroundProperty);
            MainToolBar.ToolsMenuToggle.ClearValue(Control.BorderBrushProperty);
            MainToolBar.BrushSplitButtonBorder.BorderBrush = Brushes.Transparent;
            MainToolBar.BrushSplitButtonBorder.Background = Brushes.Transparent;
            MainToolBar.BrushMainButton.ClearValue(Control.BackgroundProperty);
            MainToolBar.BrushMainButton.ClearValue(Control.BorderBrushProperty);
            MainToolBar.BrushMainButton.Tag = null;        // ← 清除 Tag
            MainToolBar.BrushToggle.ClearValue(Control.BackgroundProperty);
            MainToolBar.BrushToggle.ClearValue(Control.BorderBrushProperty);
            MainToolBar.BrushToggle.Tag = null;             
            MainToolBar.ShapeSplitButtonBorder.BorderBrush = Brushes.Transparent;
            MainToolBar.ShapeSplitButtonBorder.Background = Brushes.Transparent;
            MainToolBar.ShapeMainButton.ClearValue(Control.BackgroundProperty);
            MainToolBar.ShapeMainButton.ClearValue(Control.BorderBrushProperty);
            MainToolBar.ShapeMainButton.Tag = null;         // ← 清除 Tag
            MainToolBar.ShapeToggle.ClearValue(Control.BackgroundProperty);
            MainToolBar.ShapeToggle.ClearValue(Control.BorderBrushProperty);
            MainToolBar.ShapeToggle.Tag = null;    
            MainToolBar.SelectSplitButtonBorder.BorderBrush = Brushes.Transparent;
            MainToolBar.SelectSplitButtonBorder.Background = Brushes.Transparent;
            MainToolBar.SelectMainButton.ClearValue(Control.BackgroundProperty);
            MainToolBar.SelectMainButton.ClearValue(Control.BorderBrushProperty);
            MainToolBar.SelectMainButton.Tag = null;        // ← 清除 Tag
            MainToolBar.SelectToggle.ClearValue(Control.BackgroundProperty);
            MainToolBar.SelectToggle.ClearValue(Control.BorderBrushProperty);
            MainToolBar.SelectToggle.Tag = null;            // ← 清除 Tag

            var toolControls = new Control[] {
        MainToolBar.PickColorButton, MainToolBar.EraserButton,
        MainToolBar.FillButton, MainToolBar.TextButton, MainToolBar.PenButton
    };

            foreach (var ctrl in toolControls)
            {
                ctrl.ClearValue(Control.BorderBrushProperty);
                ctrl.ClearValue(Control.BackgroundProperty);
            }

            bool isBasicTool = !string.IsNullOrEmpty(currentToolTag);

            if (!isBasicTool || _router.CurrentTool is SelectTool)
            {
                if (_router.CurrentTool is PenTool && _ctx.PenStyle != BrushStyle.Eraser && _ctx.PenStyle != BrushStyle.Pencil)
                {
                    MainToolBar.BrushSplitButtonBorder.BorderBrush = accentBrush;
                    MainToolBar.BrushSplitButtonBorder.Background = accentSubtleBrush;
                    MainToolBar.BrushMainButton.Tag = "Selected";
                    MainToolBar.BrushToggle.Tag = "Selected";

                    string brushTag = _ctx.PenStyle.ToString();
                    UpdateSubMenuHighlight(MainToolBar.SubMenuPopupBrush, brushTag);
                }
                else
                {
                    UpdateSubMenuHighlight(MainToolBar.SubMenuPopupBrush, null);
                }

                // 形状工具高亮
                if (_router.CurrentTool is ShapeTool shapeTool)
                {
                    MainToolBar.ShapeSplitButtonBorder.BorderBrush = accentBrush;
                    MainToolBar.ShapeSplitButtonBorder.Background = accentSubtleBrush;
                    MainToolBar.ShapeMainButton.Tag = "Selected";
                    MainToolBar.ShapeToggle.Tag = "Selected";

                    string shapeTag = shapeTool.CurrentShapeType.ToString();
                    UpdateSubMenuHighlight(MainToolBar.SubMenuPopupShape, shapeTag);
                }
                else
                {
                    UpdateSubMenuHighlight(MainToolBar.SubMenuPopupShape, null);
                }

                // 选区工具高亮
                if (_router.CurrentTool is SelectTool selectTool)
                {
                    MainToolBar.SelectSplitButtonBorder.BorderBrush = accentBrush;
                    MainToolBar.SelectSplitButtonBorder.Background = accentSubtleBrush;
                    // ★ 标记
                    MainToolBar.SelectMainButton.Tag = "Selected";
                    MainToolBar.SelectToggle.Tag = "Selected";

                    string selectTag = selectTool.SelectionType == SelectionType.Lasso ? "Lasso" : "Rectangle";
                    UpdateSubMenuHighlight(MainToolBar.SubMenuPopupSelect, selectTag);
                }
                else
                {
                    UpdateSubMenuHighlight(MainToolBar.SubMenuPopupSelect, null);
                }
            }

            if (isBasicTool)
            {
                if (isCollapsedMode)
                {
                    MainToolBar.ToolsMenuToggle.BorderBrush = accentBrush;
                    MainToolBar.ToolsMenuToggle.Background = accentSubtleBrush;
                    UpdateCollapsedButtonIcon(currentToolTag);
                    UpdateCollapsedMenuHighlight(currentToolTag);
                }
                else
                {
                    Control target = _router.CurrentTool switch
                    {
                        EyedropperTool => MainToolBar.PickColorButton,
                        FillTool => MainToolBar.FillButton,
                        TextTool => MainToolBar.TextButton,
                        PenTool when _ctx.PenStyle == BrushStyle.Eraser => MainToolBar.EraserButton,
                        PenTool when _ctx.PenStyle == BrushStyle.Pencil => MainToolBar.PenButton,
                        GradientTool => MainToolBar.BrushMainButton,
                        _ => null
                    };

                    if (target != null)
                    {
                        target.BorderBrush = accentBrush;
                        target.Background = accentSubtleBrush;
                    }
                }
            }
            else
            {
                if (_router.CurrentTool is PenTool && _ctx.PenStyle != BrushStyle.Eraser && _ctx.PenStyle != BrushStyle.Pencil)
                {
                    // ★ 只高亮外框，不直接设置内部按钮的 BorderBrush/Background
                    MainToolBar.BrushSplitButtonBorder.BorderBrush = accentBrush;
                    MainToolBar.BrushSplitButtonBorder.Background = accentSubtleBrush;
                    MainToolBar.BrushMainButton.Tag = "Selected";
                    MainToolBar.BrushToggle.Tag = "Selected";

                    string brushTag = _ctx.PenStyle.ToString();
                    UpdateSubMenuHighlight(MainToolBar.SubMenuPopupBrush, brushTag);
                }
                else
                {
                    UpdateSubMenuHighlight(MainToolBar.SubMenuPopupBrush, null);
                }

                if (_router.CurrentTool is ShapeTool shapeTool)
                {
                    MainToolBar.ShapeSplitButtonBorder.BorderBrush = accentBrush;
                    MainToolBar.ShapeSplitButtonBorder.Background = accentSubtleBrush;
                    MainToolBar.ShapeMainButton.Tag = "Selected";
                    MainToolBar.ShapeToggle.Tag = "Selected";

                    string shapeTag = shapeTool.CurrentShapeType.ToString();
                    UpdateSubMenuHighlight(MainToolBar.SubMenuPopupShape, shapeTag);
                }
                else
                {
                    UpdateSubMenuHighlight(MainToolBar.SubMenuPopupShape, null);
                }
            }

        }

        private void UpdateSubMenuHighlight(System.Windows.Controls.Primitives.Popup popup, string targetTag)
        {
            if (popup?.Child is Border border && border.Child is StackPanel panel)
            {
                foreach (var item in panel.Children)
                {
                    if (item is MenuItem menuItem)
                    {
                        if (targetTag != null && menuItem.Tag?.ToString() == targetTag)
                        {
                            menuItem.Background = Application.Current.FindResource("ToolAccentSubtleHoverBrush") as Brush;
                            menuItem.FontWeight = FontWeights.Bold;
                        }
                        else
                        {
                            menuItem.ClearValue(Control.BackgroundProperty);
                            menuItem.ClearValue(Control.BorderBrushProperty);
                            menuItem.ClearValue(Control.BorderThicknessProperty);
                            menuItem.ClearValue(Control.FontWeightProperty);
                        }
                    }
                }
            }
        }
        private void UpdateCollapsedButtonIcon(string toolTag)
        {
            MainToolBar.CurrentToolIcon.Visibility = Visibility.Collapsed;

            object resource = null;
            string toolName = "";

            try
            {
                switch (toolTag)
                {
                    case "SelectTool":
                        resource = FindResource("Select_Image"); // 它是 Geometry
                        toolName = LocalizationManager.GetString("L_ToolBar_Tool_Select");
                        break;

                    case "PenTool":
                        resource = FindResource("Pencil_Image"); // 它是 ImageSource
                        toolName = LocalizationManager.GetString("L_ToolBar_Tool_Pen");
                        break;

                    case "EyedropperTool":
                        resource = FindResource("Pick_Colour_Image");
                        toolName = LocalizationManager.GetString("L_ToolBar_Tool_Eyedropper");
                        break;

                    case "EraserTool":
                        resource = FindResource("Eraser_Image");
                        toolName = LocalizationManager.GetString("L_ToolBar_Tool_Eraser");
                        break;

                    case "FillTool":
                        resource = FindResource("Fill_Bucket_Image");
                        toolName = LocalizationManager.GetString("L_ToolBar_Tool_Fill");
                        break;

                    case "TextTool":
                        resource = FindResource("Text_Image"); // 它是 Geometry
                        toolName = LocalizationManager.GetString("L_ToolBar_Tool_Text");
                        break;
                    case "GradientTool":
                        resource = FindResource("Brush_Normal_Image");
                        toolName = LocalizationManager.GetString("L_ToolBar_Brush_Gradient");
                        break;
                }
                if (resource is ImageSource imgSrc)
                {
                    MainToolBar.CurrentToolIcon.Source = imgSrc;
                    MainToolBar.CurrentToolIcon.Visibility = Visibility.Visible;
                }
                if (!string.IsNullOrEmpty(toolName))
                    MainToolBar.ToolsMenuToggle.ToolTip = toolName;
            }
            catch (Exception ex){  Console.WriteLine($"Error loading icon resource: {ex.Message}");  }

        }
        private void UpdateCollapsedMenuHighlight(string toolTag)
        {
            foreach (var item in MainToolBar.CollapsedMenuItems.Children)
            {
                if (item is MenuItem menuItem)
                {
                    if (menuItem.Tag?.ToString() == toolTag)
                    {
                        menuItem.Background = Application.Current.FindResource("ListItemSelectedBackgroundBrush") as Brush  ; // 浅蓝色高亮
                        menuItem.FontWeight = FontWeights.Bold;
                    }
                    else
                    {
                        menuItem.ClearValue(Control.BackgroundProperty);
                        menuItem.ClearValue(Control.FontWeightProperty);
                    }
                }
            }
        }







        public interface ITool
        {
            string Name { get; }
            System.Windows.Input.Cursor Cursor { get; }
            void Cleanup(ToolContext ctx);
            void StopAction(ToolContext ctx);
            void OnMouseLeave(ToolContext ctx);
            void OnPointerDown(ToolContext ctx, Point viewPos, float pressure = 1.0f);
            void OnPointerMove(ToolContext ctx, Point viewPos, float pressure = 1.0f);
            void OnPointerUp(ToolContext ctx, Point viewPos, float pressure = 1.0f);
            void OnKeyDown(ToolContext ctx, System.Windows.Input.KeyEventArgs e);
            void OnKeyUp(ToolContext ctx, System.Windows.Input.KeyEventArgs e);
            void SetCursor(ToolContext ctx);
        }

        public abstract class ToolBase : ITool
        {
            public abstract string Name { get; }
            public virtual System.Windows.Input.Cursor Cursor => System.Windows.Input.Cursors.Arrow;
            public virtual void OnPointerDown(ToolContext ctx, Point viewPos, float pressure = 1.0f) { }
            public virtual void OnPointerMove(ToolContext ctx, Point viewPos, float pressure = 1.0f) { }
            public virtual void OnPointerUp(ToolContext ctx, Point viewPos, float pressure = 1.0f) { }
            public virtual void OnKeyDown(ToolContext ctx, System.Windows.Input.KeyEventArgs e) { }
            public virtual void OnKeyUp(ToolContext ctx, System.Windows.Input.KeyEventArgs e) { }
            public virtual void OnMouseLeave(ToolContext ctx) { }
            public virtual void Cleanup(ToolContext ctx) { }
            public virtual void StopAction(ToolContext ctx) { }
            public virtual void SetCursor(ToolContext ctx) { }
        }

        public class ToolContext
        {
            public MainWindow ParentWindow { get; }
            public CanvasSurface Surface { get; }
            public UndoRedoManager Undo { get; }
            public Color PenColor { get; set; } = Colors.Black;
            public Color EraserColor { get; set; } = Colors.White;
            public double PenThickness { get; set; } = 5.0;
            public double PenOpacity { get; set; } = 1.0;
            public Image ViewElement { get; } // 例如 DrawImage
            public WriteableBitmap Bitmap => Surface.Bitmap;
            public Image SelectionPreview { get; } // 预览层
            public Canvas SelectionOverlay { get; }
            public Canvas EditorOverlay { get; }
            public BrushStyle PenStyle { get; set; } = BrushStyle.Pencil;

            public int FullImageWidth { get; set; }
            public int FullImageHeight { get; set; }

            // 文档状态
            // public string CurrentFilePath { get; set; } = string.Empty;
            public bool IsDirty { get; set; } = false;
            private readonly IInputElement _captureElement;
            public ToolContext(MainWindow parent, CanvasSurface surface, UndoRedoManager undo, Image viewElement, Image previewElement, Canvas overlayElement, Canvas EditorElement, IInputElement captureElement)
            {
                ParentWindow = parent;
                Surface = surface;
                Undo = undo;
                ViewElement = viewElement;
                SelectionPreview = previewElement;
                SelectionOverlay = overlayElement; // ← 保存引用
                EditorOverlay = EditorElement;
                _captureElement = captureElement;
            }

            // 视图坐标 -> 像素坐标
            public Point ToPixel(Point viewPos)
            {
                if (!ViewElement.Dispatcher.CheckAccess())
                {
                    return (Point)ViewElement.Dispatcher.Invoke(() => ToPixel(viewPos));
                }

                var bmp = Surface.Bitmap;
                if (bmp == null || ViewElement.ActualWidth == 0 || ViewElement.ActualHeight == 0)
                    return new Point(0, 0);

                double sx = bmp.PixelWidth / ViewElement.ActualWidth;
                double sy = bmp.PixelHeight / ViewElement.ActualHeight;
                return new Point(viewPos.X * sx, viewPos.Y * sy);
            }
            public Point FromPixel(Point pixelPos)
            {
                if (!ViewElement.Dispatcher.CheckAccess())
                {
                    return (Point)ViewElement.Dispatcher.Invoke(() => FromPixel(pixelPos));
                }

                var bmp = Surface.Bitmap;
                if (bmp == null || ViewElement.ActualWidth == 0 || ViewElement.ActualHeight == 0)
                    return new Point(0, 0);

                double sx = ViewElement.ActualWidth / bmp.PixelWidth;
                double sy = ViewElement.ActualHeight / bmp.PixelHeight;
                return new Point(pixelPos.X * sx, pixelPos.Y * sy);
            }
            public void CapturePointer() { _captureElement?.CaptureMouse(); }

            public void ReleasePointerCapture() { _captureElement?.ReleaseMouseCapture(); }
        }


        public class ToolRegistry//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        {
            public ITool Pen { get; } = new PenTool();
            //public ITool Eraser { get; } = new EraserTool();
            public ITool Eyedropper { get; } = new EyedropperTool();
            public ITool Fill { get; } = new FillTool();
            public ITool Select { get; } = new SelectTool();//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            public ITool Text { get; } = new TextTool();
            public ITool Shape { get; } = new ShapeTool();
            public ITool Gradient { get; } = new GradientTool();
        }

        public class InputRouter /// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        {
            private readonly ToolContext _ctx;
            private readonly MainWindow _parentWindow;
            public ITool CurrentTool { get; private set; }

            public InputRouter(MainWindow parent, ToolContext ctx, ITool defaultTool)
            {
                _parentWindow = parent;
                _ctx = ctx;
                CurrentTool = defaultTool;
                if (_ctx.ViewElement != null)
                {
                    System.Windows.Input.Stylus.SetIsPressAndHoldEnabled(_ctx.ViewElement, false);

                    System.Windows.Input.Stylus.SetIsFlicksEnabled(_ctx.ViewElement, false);

                    System.Windows.Input.Stylus.SetIsTapFeedbackEnabled(_ctx.ViewElement, false);
                    System.Windows.Input.Stylus.SetIsTouchFeedbackEnabled(_ctx.ViewElement, false);
                    _ctx.ViewElement.MouseLeave += ViewElement_MouseLeave;
                }
            }
            private void ViewElement_MouseLeave(object sender, MouseEventArgs e)
            {
                CurrentTool?.OnMouseLeave(_ctx);
            }

            private float GetPressure(System.Windows.Input.MouseEventArgs e)
            {
                if (e.StylusDevice != null)
                {
                    var points = e.StylusDevice.GetStylusPoints(_ctx.ViewElement);
                    if (points.Count > 0)
                    {
                        return points[points.Count - 1].PressureFactor;
                    }
                }
                return 1.0f; // 如果不是笔或者是鼠标，默认最大压力
            }

            public void ViewElement_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
            {
                var position = e.GetPosition(_ctx.ViewElement);
                if (_ctx.Surface.Bitmap != null)
                {
                    Point px = _ctx.ToPixel(position);
                    _parentWindow.MousePosition = $"{(int)px.X}, {(int)px.Y}" + LocalizationManager.GetString("L_Main_Unit_Pixel");
                }
                if (e.StylusDevice != null)
                {
                    var stylusPoints = e.StylusDevice.GetStylusPoints(_ctx.ViewElement);

                    if (stylusPoints.Count > 0)
                    {
                        for (int i = 0; i < stylusPoints.Count; i++)
                        {
                            var sp = stylusPoints[i];
                            var pt = new Point(sp.X, sp.Y);
                            float pressure = sp.PressureFactor;
                            CurrentTool.OnPointerMove(_ctx, pt, pressure);
                        }
                        return; // 处理完毕，直接返回，不再执行下方的鼠标逻辑
                    }
                }
                float mousePressure = 1.0f;
                // 保持原有的鼠标压力获取逻辑作为兼容
                if (e.StylusDevice != null)
                {
                    var points = e.StylusDevice.GetStylusPoints(_ctx.ViewElement);
                    if (points.Count > 0) mousePressure = points[points.Count - 1].PressureFactor;
                }

                CurrentTool.OnPointerMove(_ctx, position, mousePressure);
            }// 定义高亮颜色

            public SelectTool GetSelectTool()
            {
                return _parentWindow._tools.Select as SelectTool;
            }

            public void CleanUpSelectionandShape()
            {
                if (CurrentTool is SelectTool selTool)
                {
                    if (selTool._selectionData != null)
                        selTool.CommitSelection(_ctx);
                    selTool.Cleanup(_ctx);

                }
                if (_parentWindow._router.CurrentTool is ShapeTool shapetool)
                {
                    shapetool.CommitActiveShape(_ctx);
                    GetSelectTool()?.Cleanup(_ctx);
                }
                if (_parentWindow._router.CurrentTool is TextTool textTool)
                {
                    textTool.CommitText(_ctx);
                }

            }

            public void SetTool(ITool tool)
            {

                if (CurrentTool == tool) return; // Optional: Don't do work if it's the same tool.
                CleanUpSelectionandShape();
                CurrentTool?.Cleanup(_ctx);
                CurrentTool = tool;
                tool.SetCursor(_ctx);
                _parentWindow.AutoSetFloatBarVisibility();
                _parentWindow._router.GetSelectTool().UpdateStatusBarSelectionSize(_parentWindow);
                _parentWindow.UpdateToolSelectionHighlight();
                _parentWindow.UpdateGlobalToolSettingsKey();
            }
            public void ViewElement_MouseDown(object sender, MouseButtonEventArgs e)
            {
                float pressure = GetPressure(e);
                CurrentTool?.OnPointerDown(_ctx, e.GetPosition(_ctx.ViewElement), pressure);
            }

            public void ViewElement_MouseUp(object sender, MouseButtonEventArgs e)
            {
                float pressure = GetPressure(e);
                CurrentTool?.OnPointerUp(_ctx, e.GetPosition(_ctx.ViewElement), pressure);
            }
            public void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
            {//快捷键
                CurrentTool.OnKeyDown(_ctx, e);
            }
            public void OnPreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
            {
                CurrentTool.OnKeyUp(_ctx, e);
            }
        }


    }
}
