using SkiaSharp;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using TabPaint.Services;
using TabPaint.Controls;
using static TabPaint.MainWindow;

//
//更新(广义上)按钮状态的相关代码
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
       
        private void UpdateGlobalToolSettingsKey()
        {
            _isUpdatingToolSettings = true;

            try
            {
                if (_router?.CurrentTool == null || _ctx == null) return;
                string key = "Pen_Round"; // 默认 fallback
                bool isPencil = false;

                if (_router.CurrentTool is PenTool)
                {
                    switch (_ctx.PenStyle)
                    {
                        case BrushStyle.Pencil: key = "Pen_Pencil"; isPencil = true; break;
                        case BrushStyle.Round: key = "Pen_Round"; break;
                        case BrushStyle.Square: key = "Pen_Square"; break;
                        case BrushStyle.Highlighter: key = "Pen_Highlighter"; break;
                        case BrushStyle.Eraser: key = "Pen_Eraser"; break;
                        case BrushStyle.Watercolor: key = "Pen_Watercolor"; break;
                        case BrushStyle.Crayon: key = "Pen_Crayon"; break;
                        case BrushStyle.Spray: key = "Pen_Spray"; break;
                        case BrushStyle.Mosaic: key = "Pen_Mosaic"; break;
                        case BrushStyle.Brush: key = "Pen_Brush"; break;
                    }
                }
                else if (_router.CurrentTool is GradientTool) key = "Gradient";
                else if (_router.CurrentTool is ShapeTool)  key = "Shape";
  
                if (ThicknessSlider != null)  ThicknessSlider.IsEnabled = !isPencil;
                this.CurrentToolKey = key;

                _ctx.PenThickness = this.PenThickness;
                _ctx.PenOpacity = this.PenOpacity;
            }
            finally   { _isUpdatingToolSettings = false;}  
        }

        public void UpdateCurrentColor(Color color, bool secondColor = false) // 更新前景色按钮颜色
        {
            if (secondColor)
            {
                BackgroundBrush = new SolidColorBrush(color);
                OnPropertyChanged(nameof(BackgroundBrush)); // 通知绑定刷新
                _ctx.PenColor = color;
                BackgroundColor = color;
            }
            else
            {
                ForegroundBrush = new SolidColorBrush(color);
                OnPropertyChanged(nameof(ForegroundBrush)); // 通知绑定刷新
                ForegroundColor = color;
                if (!useSecondColor) _ctx.PenColor = color;
            }

            // 实时同步 ShapeTool 预览
            if (_router?.CurrentTool is ShapeTool shapeTool)
            {
                shapeTool.RefreshPreview(_ctx);
            }
        }
        private bool _isProcessingMaximizeWindow = false;
        public async void MaximizeWindowHandler()
        {
            if (_isProcessingMaximizeWindow) return; // 如果正在冷却中，直接跳过
            _isProcessingMaximizeWindow = true;
            ExecuteMaximizeLogic();
           await Task.Delay(500);
            _isProcessingMaximizeWindow = false;
        }

        public void ExecuteMaximizeLogic()
        {
            ShowToast(!_maximized ? "L_Toast_FullScreen_On" : "L_Toast_FullScreen_Off");
            if (!_maximized)
            {
                _restoreBounds = new Rect(Left, Top, Width, Height);
                _maximized = true;
                var workArea = SystemParameters.WorkArea;
                Left = workArea.Left - (SystemParameters.BorderWidth) * 2;
                Top = workArea.Top - (SystemParameters.BorderWidth) * 2;
                Width = workArea.Width + (SystemParameters.BorderWidth * 4);
                Height = workArea.Height + (SystemParameters.BorderWidth * 4);
            }
            else
            {
                _maximized = false;
                Left = _restoreBounds.Left;
                Top = _restoreBounds.Top;
                Width = _restoreBounds.Width;
                Height = _restoreBounds.Height;
                WindowState = WindowState.Normal;
            }
            AutoUpdateMaximizeIcon();
        }

        public void AutoUpdateMaximizeIcon()
        {
            if(_maximized)SetRestoreIcon(); 
            else SetMaximizeIcon();
        }
        private async void SetBrushStyle(BrushStyle style)
        {//设置画笔样式，大部分画笔都是pen工具
            if (style == BrushStyle.AiEraser)  if (!await EnsureAiModelReadyAsync(AiService.AiTaskType.Inpainting)) return;
            if (style == BrushStyle.Gradient)
            {
                _router.SetTool(_tools.Gradient);
            }
            else
            {
                _router.SetTool(_tools.Pen);
                _ctx.PenStyle = style;
            }
            UpdateBrushSplitButtonIcon(style);
            UpdateToolSelectionHighlight();
            _router.CurrentTool.SetCursor(_ctx);
            AutoSetFloatBarVisibility();
            UpdateGlobalToolSettingsKey();
        }

        private void SetThicknessSlider_Pos(double sliderProgressValue)
        {

            Rect rect = new Rect(
                ThicknessSlider.TransformToAncestor(this).Transform(new Point(0, 0)),
                new Size(ThicknessSlider.ActualWidth, ThicknessSlider.ActualHeight));

            double trackHeight = ThicknessSlider.ActualHeight;
            double relativeValue = (ThicknessSlider.Maximum - sliderProgressValue) / (ThicknessSlider.Maximum - ThicknessSlider.Minimum);
            if (double.IsNaN(relativeValue)) relativeValue = 0;
            double offsetY = relativeValue * trackHeight;
            ThicknessTip.Margin = new Thickness(80, offsetY + rect.Top - 10, 0, 0);
        }

        private void UpdateThicknessPreviewPosition()
        {
            if (ThicknessPreview == null) return;
            double zoom = ZoomTransform.ScaleX;
            double size = PenThickness * zoom;
            ThicknessPreview.Width = size;
            ThicknessPreview.Height = size;

            if (_ctx.PenStyle == BrushStyle.Square|| _ctx.PenStyle == BrushStyle.Eraser)
            {
                // 方形：圆角为 0
                ThicknessPreview.RadiusX = 0;
                ThicknessPreview.RadiusY = 0;
            }
            else
            {
                // 圆形或其他：圆角为尺寸的一半
                ThicknessPreview.RadiusX = size / 2;
                ThicknessPreview.RadiusY = size / 2;
            }
            if (_ctx.PenStyle == BrushStyle.Highlighter)
                ThicknessPreview.Stroke = Brushes.Yellow;
            else if (_ctx.PenStyle == BrushStyle.Eraser)
                ThicknessPreview.Stroke = Brushes.Black;
            else
                ThicknessPreview.Stroke = new SolidColorBrush(_ctx.PenColor);

            ThicknessPreview.Fill = Brushes.Transparent;
            ThicknessPreview.StrokeThickness = 2;
        }
        private void UpdateWindowTitle()
        {
          
            if (_currentTabItem == null)
            {
                this.Title = $"TabPaint";
                if (AppTitleBar.TitleTextControl != null) AppTitleBar.TitleTextControl.Text = this.Title;
                return;
            }

            string dirtyMark = _currentTabItem.IsDirty ? "*" : "";

            string displayFileName = _currentTabItem.FileName;

            string countInfo = "";

            if (!_currentTabItem.IsNew)
            {
                int total = _imageFiles.Count;
                int currentIndex = -1;

                if (!string.IsNullOrEmpty(_currentTabItem.FilePath))
                {
                    currentIndex = _imageFiles.IndexOf(_currentTabItem.FilePath);
                }

                // 只有找到了有效的索引且总数大于0才显示
                if (currentIndex >= 0 && total > 0)
                {
                    countInfo = $" ({currentIndex + 1}/{total})";
                }
            }
            string newTitle = $"{dirtyMark}{displayFileName}{countInfo} - TabPaint";

            this.Title = newTitle;
            if (AppTitleBar.TitleTextControl != null) AppTitleBar.TitleTextControl.Text = newTitle;
          
        }



        private void EnsureFontsLoaded()
        {
            if (_fontsLoaded) return;
            if (TextMenu == null) return;

            System.Threading.Tasks.Task.Run(() =>
            {
                var sortedFonts = FontService.GetSystemFonts();

                // 切回 UI 线程更新
                Dispatcher.Invoke(() =>
                {
                    TextMenu.FontFamilyBox.ItemsSource = sortedFonts;
                    TextMenu.FontFamilyBox.DisplayMemberPath = "DisplayName";
                    TextMenu.FontFamilyBox.SelectedValuePath = "FontFamily";
                    
                    var defaultFont = FontService.GetDefaultFont(sortedFonts);
                    TextMenu.FontFamilyBox.SelectedItem = defaultFont;

                    _fontsLoaded = true;
                });
            });
        }

        public void ShowTextToolbarFor(System.Windows.Controls.RichTextBox tb)
        {
            EnsureTextToolLoaded();
            EnsureFontsLoaded();
            _activeTextBox = tb;
            if (TextToolHolder != null)
            {
                TextToolHolder.Visibility = Visibility.Visible;
            }
            TextMenu.TextEditBar.Visibility = Visibility.Visible;

            TextMenu.FontFamilyBox.SelectedItem = tb.FontFamily;
            TextMenu.FontSizeBox.Text = tb.FontSize.ToString(CultureInfo.InvariantCulture);
            TextMenu.BoldBtn.IsChecked = tb.FontWeight == FontWeights.Bold;
            TextMenu.ItalicBtn.IsChecked = tb.FontStyle == FontStyles.Italic;
           // UnderlineBtn.IsChecked = tb.TextDecorations == TextDecorations.Underline;
        }
        private bool HasTextDecoration(RichTextBox tb, TextDecorationCollection target)
        {
            // 这是一个简化检查，实际情况可能需要更复杂的判断
            return tb.Selection.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection col
                   && col.Count > 0
                   && col[0].Location == target[0].Location;
        }
        public void HideTextToolbar()
        {
            if (TextToolHolder != null)
                TextToolHolder.Visibility = Visibility.Collapsed;

            _activeTextBox = null;
        }
        private void SetRestoreIcon()
        {
            AppTitleBar.MaxBtn.Content = new Image
            {
                Source = (DrawingImage)FindResource("Restore_Image"),
                Width = 12,
                Height = 12
            };
        }
        private void SetMaximizeIcon()
        {
            AppTitleBar.MaxBtn.Content = new Viewbox
            {
                Width = 10,
                Height = 10,
                Child = new Rectangle
                {
                    Stroke = Application.Current.FindResource("TextPrimaryBrush") as Brush,
                    StrokeThickness = 1,
                    Width = 12,
                    Height = 12
                }
            };
        }
        public void UpdateColorHighlight()
        {
            if (MainToolBar == null) return;
            MainToolBar.ColorBtn1.Tag = !useSecondColor ? "True" : "False"; // 如果不是色2，那就是色1选中
            MainToolBar.ColorBtn2.Tag = useSecondColor ? "True" : "False";
        }

  
        private void AutoSetFloatBarVisibility()
        {
            var mw = MainWindow.GetCurrentInstance();
            if (mw.ToolPanelGrid == null) return;

            // 1. 判断显示逻辑
            bool showThickness = (_router.CurrentTool is PenTool && _ctx.PenStyle != BrushStyle.Pencil) || _router.CurrentTool is ShapeTool;
            showThickness = showThickness && !IsViewMode;

            bool showOpacity = _router.CurrentTool is PenTool || _router.CurrentTool is TextTool || _router.CurrentTool is GradientTool;
            showOpacity = showOpacity && !IsViewMode;

            mw.ThicknessPanel.Visibility = showThickness ? Visibility.Visible : Visibility.Collapsed;
            mw.OpacityPanel.Visibility = showOpacity ? Visibility.Visible : Visibility.Collapsed;

            // 魔棒容差窗口显示逻辑
            if (mw.WandTolerancePopup != null)
            {
                var selectTool = mw._tools?.Select as SelectTool;
                bool isWand = mw._router?.CurrentTool is SelectTool && selectTool?.SelectionType == SelectionType.MagicWand;
                if (isWand && !mw.IsViewMode) mw.WandTolerancePopup.Show();
                else mw.WandTolerancePopup.Hide();
            }

            var rows = mw.ToolPanelGrid.RowDefinitions;
            Grid.SetRow(mw.OpacityPanel, 3);

            // 重置所有行高为默认配置
            rows[1].Height = new GridLength(10000, GridUnitType.Star); // 上槽位
            rows[2].Height = new GridLength(15);                     // 间距
            rows[3].Height = new GridLength(10000, GridUnitType.Star); // 下槽位

            if (showThickness && showOpacity)
            {
                // 两个都显示：保持默认状态即可 (上槽位给Thickness, 下槽位给Opacity)
            }
            else if (showThickness && !showOpacity)
            {
                // 只显示粗细：隐藏下半部分
                rows[2].Height = new GridLength(0); // 隐藏间距
                rows[3].Height = new GridLength(0); // 隐藏下槽位
            }
            else if (!showThickness && showOpacity)
            {
                Grid.SetRow(mw.OpacityPanel, 1);

                // 2. 隐藏下面的行
                rows[2].Height = new GridLength(0); // 隐藏间距
                rows[3].Height = new GridLength(0); // 隐藏原来的下槽位
            }
            else
            {
                // 都不显示
                rows[1].Height = new GridLength(0);
                rows[2].Height = new GridLength(0);
                rows[3].Height = new GridLength(0);
            }
        }


        public void SetUndoRedoButtonState()
        {
            if (MainMenu == null || _undo == null) return;
            UpdateBrushAndButton(MainMenu.BtnUndo, MainMenu.IconUndo, _undo.CanUndo);
            UpdateBrushAndButton(MainMenu.BtnRedo, MainMenu.IconRedo, _undo.CanRedo);
        }

        public void SetCropButtonState()
        {
            if (MainToolBar == null || MainToolBar.CutImage == null || MainToolBar.CutImageIcon == null) return;

            bool hasSelection = _tools.Select is SelectTool st &&
                     _ctx.SelectionOverlay.Visibility != Visibility.Collapsed;

            bool isShapeToolActive = _router.CurrentTool is ShapeTool;

            bool canCrop = hasSelection && !isShapeToolActive;
            UpdateBrushAndButton(MainToolBar.CutImage, MainToolBar.CutImageIcon, canCrop);
        }

        private void UpdateBrushAndButton(System.Windows.Controls.Button button, System.Windows.Shapes.Path image, bool isEnabled)
        {
            if (button == null || image == null) return;
            button.IsEnabled = isEnabled;
            image.Fill = isEnabled ? Application.Current.FindResource("TextPrimaryBrush") as Brush : Brushes.Gray;
        }
        private byte[] ExtractRegionFromBitmap(WriteableBitmap bmp, Int32Rect rect)
        {
            int stride = bmp.BackBufferStride;
            byte[] region = new byte[rect.Width * rect.Height * 4];

            bmp.Lock();
            for (int row = 0; row < rect.Height; row++)
            {
                IntPtr src = bmp.BackBuffer + (rect.Y + row) * stride + rect.X * 4;
                System.Runtime.InteropServices.Marshal.Copy(src, region, row * rect.Width * 4, rect.Width * 4);
            }
            bmp.Unlock();
            return region;
        }
        private System.Drawing.Bitmap BitmapSourceToDrawingBitmap(BitmapSource source)
        {
            using (MemoryStream outStream = new MemoryStream())     // 使用 PNG 编码器作为中间桥梁，保留透明度
            {
                BitmapEncoder enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(source));
                enc.Save(outStream);
                return new System.Drawing.Bitmap(outStream);
            }
        }

        private void SaveBitmap(string path)
        {
            // 1. 获取当前编辑的像素数据
            int width = _bitmap.PixelWidth;
            int height = _bitmap.PixelHeight;
            int stride = width * 4;

            byte[] pixels = new byte[height * stride];

            try
            {
                _bitmap.CopyPixels(pixels, stride, 0);
            }
            catch (System.ArgumentOutOfRangeException)
            {
                if (_bitmap is WriteableBitmap wb)
                {
                    stride = wb.BackBufferStride;
                    pixels = new byte[height * stride];
                    wb.CopyPixels(pixels, stride, 0);
                }
                else
                {
                    throw;
                }
            }

            string ext = System.IO.Path.GetExtension(path).ToLower();
            if (ext == ".ico")
            {
                BitmapSource baseSource = BitmapSource.Create(
        width, height,
        _originalDpiX, _originalDpiY,
        PixelFormats.Bgra32, null, pixels, stride
    );
                var icoWin = new TabPaint.Windows.IcoExportWindow(baseSource);
                icoWin.ShowOwnerModal(this);

                if (icoWin.IsConfirmed)
                {
                    try
                    {
                        using (var fs = new FileStream(path, FileMode.Create))
                        {
                            // 调用我们第一步写的静态类
                            TabPaint.Core.IcoEncoder.Save(baseSource, icoWin.ResultSizes, fs);
                        }
                        MarkAsSaved();
                        UpdateTabThumbnail(path);
                    }
                    catch (Exception ex)
                    {
                        string msg = string.Format(LocalizationManager.GetString("L_Save_Error_General"), ex.Message);
                        HandleSaveError(msg, path);
                    }
                }
                else
                {
                    return;
                }
                return; // ICO 保存完毕，退出方法
            }
            if (ext == ".webp")
            {
                var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);

                GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
                try
                {
                    IntPtr ptr = handle.AddrOfPinnedObject();

                    // 创建 Pixmap 指向这块内存
                    using (var pixmap = new SKPixmap(info, ptr, stride))
                    {
                        using (var data = pixmap.Encode(SKEncodedImageFormat.Webp, 90))
                        {
                            using (var fs = new FileStream(path, FileMode.Create))
                            {
                                data.SaveTo(fs);
                            }
                        }
                    }
                }
                finally
                {
                    handle.Free();
                }

                // WebP 保存完毕，执行后续收尾工作并直接返回
                MarkAsSaved();
                UpdateTabThumbnail(path);
                return;
            }
            BitmapSource saveSource = BitmapSource.Create(
                width, height,
                _originalDpiX,
                _originalDpiY,
                PixelFormats.Bgra32,
                null,
                pixels,
                stride
            );
            BitmapEncoder encoder;

            if (ext == ".jpg" || ext == ".jpeg")
            {
                saveSource = ConvertToWhiteBackground(saveSource);        // JPG 不支持透明，需要合成白底
                encoder = new JpegBitmapEncoder { QualityLevel = 90 };
            }
            else if (ext == ".bmp")
            {
                saveSource = ConvertToWhiteBackground(saveSource);
                encoder = new BmpBitmapEncoder();
            }
            else if (ext == ".tiff" || ext == ".tif")
            {
                encoder = new TiffBitmapEncoder();
            }
            else   encoder = new PngBitmapEncoder(); // 默认 PNG

            encoder.Frames.Add(BitmapFrame.Create(saveSource));

            // 4. 写入文件
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Create)) encoder.Save(fs);
            }
            catch (UnauthorizedAccessException)
            {
                // 捕获只读或权限错误
                string msg = LocalizationManager.GetString("L_Save_Error_ReadOnly");
                HandleSaveError(msg, path);
                return;
            }
            catch (IOException ex)
            {
                // 捕获文件被占用错误
                string msg = string.Format(LocalizationManager.GetString("L_Save_Error_InUse"), ex.Message);
                HandleSaveError(msg, path);
                return;
            }
            catch (Exception ex)
            {
                string msg = string.Format(LocalizationManager.GetString("L_Save_Error_General"), ex.Message);
                HandleSaveError(msg, path);
                return;
            }

            MarkAsSaved();
            UpdateTabThumbnail(path);  // 5. 更新对应标签页的缩略图
        }

        private void HandleSaveError(string message, string failedPath)
        {
            Logger.Error($"Save failed for {failedPath}: {message}");
            var result = FluentMessageBox.Show(
                string.Format(LocalizationManager.GetString("L_Msg_SaveError_Content"), message),
                LocalizationManager.GetString("L_Msg_SaveError_Title"),
                MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)   OnSaveAsClick(null, null)    ;
        }
        private void UpdateSliderBarValue(double newScale,bool needsetzoom=true)
        {
            if (MyStatusBar != null)
            {
                MyStatusBar.ZoomSliderControl.Value = newScale;
                MyStatusBar.ZoomComboBox.Text = newScale.ToString("P0");
            }
            ZoomLevel = newScale.ToString("P0");
           // a.s("setzoom");
           SetZoom(newScale, slient: true);
        }
    }
}