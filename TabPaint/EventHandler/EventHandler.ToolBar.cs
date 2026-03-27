
//
//EventHandler.ToolBar.cs
//工具栏相关的逻辑处理，包括工具切换（画笔、选区等）、旋转翻转操作、颜色选择以及缩放控制。
//
using System.ComponentModel;
using System.Drawing;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TabPaint.Controls;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private async Task SwitchWorkspaceToNewFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            var (existingWindow, existingTab) = FindWindowHostingFile(filePath);
            if (existingWindow != null && existingTab != null)
            {
                existingWindow.FocusAndSelectTab(existingTab);
                return;
            }

            try
            {
                if (_currentTabItem != null)
                {
                    UpdateTabThumbnail(_currentTabItem); // 更新缩略图
                    TriggerBackgroundBackup(); // 触发保存
                }
                int waitCount = 0;
                while (_isSavingFile && waitCount < 10)
                {
                    await Task.Delay(100);
                    waitCount++;
                }
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SaveSession();
                }, System.Windows.Threading.DispatcherPriority.Background);
                _loadImageCts?.Cancel();
                lock (_queueLock) { _pendingFilePath = null; }
                FileTabs.Clear();
                _imageFiles.Clear(); // 清空之前的文件夹扫描缓存
                ImageFilesCount = _imageFiles.Count;
                _currentImageIndex = -1;
                _undo?.ClearUndo();
                _undo?.ClearRedo();
                ResetDirtyTracker();
               _currentFilePath =  _workingPath = filePath;
                CheckFilePathAvailibility(filePath);
                await OpenImageAndTabs(filePath, refresh: true, lazyload: false, forceFolderScan: true); LoadSessionForCurrentWorkspace(filePath);
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_SwitchWorkspaceFailed_Prefix"), ex.Message), ex);
            }
        }

        private void OnPenClick(object sender, RoutedEventArgs e) => SetBrushStyle(BrushStyle.Pencil);  
        private void OnPickColorClick(object s, RoutedEventArgs e)
        {
            LastTool = (MainWindow.GetCurrentInstance())._router.CurrentTool;
            _router.SetTool(_tools.Eyedropper);
        }

        private void OnEraserClick(object s, RoutedEventArgs e) => SetBrushStyle(BrushStyle.Eraser); 
        private void OnFillClick(object s, RoutedEventArgs e) => _router.SetTool(_tools.Fill);
        private void OnSelectClick(object s, RoutedEventArgs e) => _router.SetTool(_tools.Select);
        private void OnEffectButtonClick(object sender, RoutedEventArgs e)
        {
            var btn = (System.Windows.Controls.Button)sender;
            btn.ContextMenu.IsOpen = true;
        }

        private void OnShapeStyleClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is System.Windows.Controls.MenuItem item && item.Tag is string tag)
            {
                var shapeTool = _tools.Shape as ShapeTool;
                if (shapeTool == null) return;

                if (Enum.TryParse(tag, out ShapeTool.ShapeType type))
                {
                    shapeTool.SetShapeType(type);
                    _router.SetTool(shapeTool); UpdateShapeSplitButtonIcon(type);
                    UpdateToolSelectionHighlight();
                }
                MainToolBar.ShapeToggle.IsChecked = false;
            }
        }
        private void UpdateShapeSplitButtonIcon(ShapeTool.ShapeType type)
        {
            string resKey = "Shapes_Image";

            switch (type)
            {
                case ShapeTool.ShapeType.Rectangle: resKey = "Icon_Shape_Rectangle"; break;
                case ShapeTool.ShapeType.Ellipse: resKey = "Icon_Shape_Ellipse"; break;
                case ShapeTool.ShapeType.Line: resKey = "Icon_Shape_Line"; break;
                case ShapeTool.ShapeType.Arrow: resKey = "Icon_Shape_Arrow"; break;
                case ShapeTool.ShapeType.RoundedRectangle: resKey = "Icon_Shape_RoundedRect"; break;
                case ShapeTool.ShapeType.Triangle: resKey = "Icon_Shape_Triangle"; break; // 需在资源中定义
                case ShapeTool.ShapeType.Diamond: resKey = "Icon_Shape_Diamond"; break;
                case ShapeTool.ShapeType.Pentagon: resKey = "Icon_Shape_Pentagon"; break;
                case ShapeTool.ShapeType.Star: resKey = "Icon_Shape_Star"; break;
                case ShapeTool.ShapeType.Bubble: resKey = "Icon_Shape_Bubble"; break;
            }
            MainToolBar.UpdateShapeIcon(resKey);
        }

   
        private void OnRotateLeftClick(object sender, RoutedEventArgs e)
        {
            RotateBitmap(-90); MainToolBar.RotateFlipMenuToggle.IsChecked = false;
        }

        private void OnRotateRightClick(object sender, RoutedEventArgs e)
        {
            RotateBitmap(90); MainToolBar.RotateFlipMenuToggle.IsChecked = false;
        }

        private void OnRotate180Click(object sender, RoutedEventArgs e)
        {
            RotateBitmap(180); MainToolBar.RotateFlipMenuToggle.IsChecked = false;
        }
        private void OnFlipVerticalClick(object sender, RoutedEventArgs e)
        {
            FlipBitmap(flipVertical: true); MainToolBar.RotateFlipMenuToggle.IsChecked = false;
        }

        private void OnFlipHorizontalClick(object sender, RoutedEventArgs e)
        {
            FlipBitmap(flipVertical: false); MainToolBar.RotateFlipMenuToggle.IsChecked = false;
        }

        private void OnColorOneClick(object sender, RoutedEventArgs e)
        {
            useSecondColor = false;
            _ctx.PenColor = ForegroundColor;
            SelectedBrush = new SolidColorBrush(ForegroundColor);
            UpdateColorHighlight(); // 更新高亮
        }
        private void OnColorTwoClick(object sender, RoutedEventArgs e)
        {
            useSecondColor = true;
            _ctx.PenColor = BackgroundColor;
            SelectedBrush = new SolidColorBrush(BackgroundColor);
            UpdateColorHighlight(); // 更新高亮
        }

        private void OnColorButtonClick(object sender, RoutedEventArgs e)//选色按钮
        {
            if (e.OriginalSource is System.Windows.Controls.Button btn && btn.Background is SolidColorBrush brush)
            {
                SelectedBrush = new SolidColorBrush(brush.Color);
                _ctx.PenColor = brush.Color;
                UpdateCurrentColor(_ctx.PenColor, useSecondColor);
            }
        }

        private ModernColorPickerWindow? _activeColorPicker;
        private void OnCustomColorClick(object sender, RoutedEventArgs e)
        {
            if (_activeColorPicker != null && _activeColorPicker.IsVisible)
            {
                _activeColorPicker.Focus();
                return;
            }

            System.Windows.Media.Color initialColor = _ctx.PenColor;
            if (initialColor == Colors.Transparent) initialColor = Colors.Black;

            _activeColorPicker = new ModernColorPickerWindow(initialColor, useSecondColor);
            _activeColorPicker.Owner = this;
            _activeColorPicker.Closed += (s, args) =>
            {
                if (ReferenceEquals(_activeColorPicker, s))
                {
                    _activeColorPicker = null;
                }
            };
            
            bool isCompact = SettingsManager.Instance.Current.IsCompactColorPicker;
            if (isCompact)
            {
                _activeColorPicker.Show();
            }
            else
            {
                if (_activeColorPicker.ShowOwnerModal(this, disableOwner: false) == true)
                {
                    var color = _activeColorPicker.SelectedColor;
                    var brush = new SolidColorBrush(color);
                    SelectedBrush = brush;
                    _ctx.PenColor = color;
                    UpdateCurrentColor(_ctx.PenColor, useSecondColor);
                }
            }
        }
        private void OnTextClick(object sender, RoutedEventArgs e) => _router.SetTool(_tools.Text);
        private void OnBrushStyleClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is System.Windows.Controls.MenuItem menuItem
                && menuItem.Tag is string tagString
                && Enum.TryParse(tagString, out BrushStyle style))
            {
                SetBrushStyle(style);
                MainToolBar.BrushToggle.IsChecked = false;
            }
        }
        private void UpdateBrushSplitButtonIcon(BrushStyle style)
        {
            string resKey = "Brush_Normal_Image"; // 默认
            bool isPath = true; // 大部分是 Path

            switch (style)
            {
                case BrushStyle.Round: resKey = "Brush_Normal_Image"; break;
                case BrushStyle.Square: resKey = "Brush_Rect_Image"; break;
                case BrushStyle.Brush: resKey = "Brush_Normal_Image"; break;
                case BrushStyle.Calligraphy: resKey = "Brush_Image"; isPath = false; break; // 它是 Image
                case BrushStyle.Spray: resKey = "Paint_Spray_Image"; break;
                case BrushStyle.Crayon: resKey = "Crayon_Image"; break;
                case BrushStyle.Watercolor: resKey = "Watercolor_Image"; break;
                case BrushStyle.Highlighter: resKey = "Highlighter_Image"; isPath = false; break; // Image
                case BrushStyle.Mosaic: resKey = "Mosaic_Image"; break;
                case BrushStyle.AiEraser: resKey = "AIEraser_Image"; isPath = true; break;
                case BrushStyle.GaussianBlur: resKey = "Blur_Image"; isPath = true;  break;
                case BrushStyle.Gradient: resKey = "Brush_Normal_Image"; isPath = true; break;
            }
            if(MainToolBar!=null)
            MainToolBar.UpdateBrushIcon(resKey, isPath);
        }
        public async Task<bool> EnsureAiModelReadyAsync(AiService.AiTaskType taskType)
        {
            var aiService = AiService.Instance;
            if (aiService.IsModelReady(taskType)) return true;

            string contentKey = "";
            switch (taskType)
            {
                case AiService.AiTaskType.Inpainting: contentKey = "L_AI_Download_Inpaint_Content"; break;
                case AiService.AiTaskType.RemoveBackground: contentKey = "L_AI_Download_RMBG_Content"; break;
                case AiService.AiTaskType.SuperResolution: contentKey = "L_AI_Download_SR_Content"; break;
            }

            var result = FluentMessageBox.Show(
                LocalizationManager.GetString(contentKey),
                LocalizationManager.GetString("L_AI_Download_Title"),
                MessageBoxButton.YesNo);

            if (result != MessageBoxResult.Yes) return false;

            var cts = new System.Threading.CancellationTokenSource();
            EventHandler cancelHandler = (s, args) => cts.Cancel();
            TaskProgressPopup.CancelRequested += cancelHandler;

            try
            {
                string oldStatus = ImageSize;
                ImageSize = LocalizationManager.GetString("L_AI_Status_Preparing");

                var dlProgress = new Progress<AiDownloadStatus>(status =>
                {
                    if (cts.Token.IsCancellationRequested) return;
                    ImageSize = LocalizationManager.GetString("L_AI_Downloading") + $"{status.Percentage:F0}% ";
                    TaskProgressPopup.UpdateProgress(status, LocalizationManager.GetString("L_AI_Downloading"));
                });

                await aiService.PrepareModelAsync(taskType, dlProgress, cts.Token);
                TaskProgressPopup.Finish();
                ImageSize = oldStatus;
                return true;
            }
            catch (OperationCanceledException)
            {
                TaskProgressPopup.Finish();
                ShowToast("L_Toast_DownloadCancelled");
                return false;
            }
            catch (Exception ex)
            {
                TaskProgressPopup.Finish();
                ShowToast(string.Format(LocalizationManager.GetString("L_AI_Eraser_Error_Prefix"), ex.Message), ex);
                return false;
            }
            finally
            {
                TaskProgressPopup.CancelRequested -= cancelHandler;
                cts.Dispose();
            }
        }

        public enum SelectionType { Rectangle, Lasso, MagicWand }
        private void OnSelectMainClick(object sender, RoutedEventArgs e) => _router.SetTool(_tools.Select);
        private void OnSelectStyleClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string tag)
            {
                var selectTool = _tools.Select as SelectTool;
                if (selectTool == null) return;
                switch (tag)
                {
                    case "Rectangle":
                        selectTool.SelectionType = SelectionType.Rectangle;
                        break;
                    case "Lasso":
                        selectTool.SelectionType = SelectionType.Lasso;
                        break;
                    case "MagicWand": // 新增逻辑
                        selectTool.SelectionType = SelectionType.MagicWand;
                        WandTolerancePopup.Tolerance = selectTool.GetWandTolerance();
                        WandTolerancePopup.Show();
                        break;
                }
                if (selectTool.SelectionType != SelectionType.MagicWand)
                {
                    WandTolerancePopup.Hide();
                }
                _router.SetTool(_tools.Select);
                MainToolBar.SubMenuPopupSelect.IsOpen = false;
                UpdateToolSelectionHighlight();
                UpdateSelectToolIcon(selectTool.SelectionType);
            }
        }
        private void UpdateSelectToolIcon(SelectionType type)
        {
            var iconHost = MainToolBar.CurrentSelectIconHost;
            iconHost.Content = null;

            if (type == SelectionType.MagicWand)
            {
                var wandPath = new System.Windows.Shapes.Path
                {
                    Data = TryFindResource("Wand_Image") as Geometry,
                    Fill = (System.Windows.Media.Brush)FindResource("IconFillBrush"),
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Width = 16,
                    Height = 16
                };
                iconHost.Content = wandPath;
            }
            else if (type == SelectionType.Lasso)
            {
                var lassoPath = new System.Windows.Shapes.Path
                {
                    Data = TryFindResource("Lasso_Image") as Geometry,
                    Stroke = (System.Windows.Media.Brush)FindResource("IconFillBrush"),
                   
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Width = 16, // 统一为 16
                    Height = 16
                };
                iconHost.Content = lassoPath;
            }
            else
            {
                var rectImg = new System.Windows.Controls.Image
                {
                    Source = (ImageSource)FindResource("Select_Image"),
                    Width = 16,
                    Height = 16
                };
                iconHost.Content = rectImg;
            }
        }

        private void OnBrushMainClick(object sender, RoutedEventArgs e)
        {
            if (!(_router.CurrentTool is PenTool))  _router.SetTool(_tools.Pen);
            if (_ctx.PenStyle == BrushStyle.Pencil ||
                _ctx.PenStyle == BrushStyle.Eraser ||
                _ctx.PenStyle == BrushStyle.AiEraser)
            {
                _ctx.PenStyle = BrushStyle.Brush;
                UpdateBrushSplitButtonIcon(_ctx.PenStyle);
                UpdateToolSelectionHighlight();
                AutoSetFloatBarVisibility();
                UpdateGlobalToolSettingsKey();
            }
        }
        private void OnShapeMainClick(object sender, RoutedEventArgs e)
        {
            if (!(_router.CurrentTool is ShapeTool))  _router.SetTool(_tools.Shape);
        }

    }
}
