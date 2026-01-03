using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using static TabPaint.MainWindow;

//
//更新(广义上)按钮状态的相关代码
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
       
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
            }

        }
        public void MaximizeWindowHandler()
        {
            ShowToast(!_maximized ? "进入全屏模式" : "退出全屏模式");
            if (!_maximized)
            {
                _restoreBounds = new Rect(Left, Top, Width, Height);
                _maximized = true;

                var workArea = SystemParameters.WorkArea;
                //s((SystemParameters.BorderWidth));
                Left = workArea.Left - (SystemParameters.BorderWidth) * 2;
                Top = workArea.Top - (SystemParameters.BorderWidth) * 2;
                Width = workArea.Width + (SystemParameters.BorderWidth * 4);
                Height = workArea.Height + (SystemParameters.BorderWidth * 4);

                SetRestoreIcon();  // 切换到还原图标
            }
            else
            {
                _maximized = false;
                Left = _restoreBounds.Left;
                Top = _restoreBounds.Top;
                Width = _restoreBounds.Width;
                Height = _restoreBounds.Height;
                WindowState = WindowState.Normal;

                // 切换到最大化矩形图标
                SetMaximizeIcon();
            }
        }
        private void SetBrushStyle(BrushStyle style)
        {//设置画笔样式，所有画笔都是pen工具
            _router.SetTool(_tools.Pen);
            _ctx.PenStyle = style;
            UpdateToolSelectionHighlight();

            AutoSetFloatBarVisibility();
        }

        private void SetThicknessSlider_Pos(double sliderProgressValue)
        {

            Rect rect = new Rect(
                ThicknessSlider.TransformToAncestor(this).Transform(new Point(0, 0)),
                new Size(ThicknessSlider.ActualWidth, ThicknessSlider.ActualHeight));

            double trackHeight = ThicknessSlider.ActualHeight;

   
            double relativeValue = (ThicknessSlider.Maximum - sliderProgressValue) / (ThicknessSlider.Maximum - ThicknessSlider.Minimum);

            // 防止除以 0 或越界
            if (double.IsNaN(relativeValue)) relativeValue = 0;

            double offsetY = relativeValue * trackHeight;
            ThicknessTip.Margin = new Thickness(80, offsetY + rect.Top - 10, 0, 0);
        }

        private void UpdateThicknessPreviewPosition()
        {
            if (ThicknessPreview == null) return;

            // 1. 获取尺寸
            double zoom = ZoomTransform.ScaleX;
            double size = PenThickness * zoom;

            // 2. 设置宽高
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

            // (可选) 针对橡皮擦等特殊工具，也可以改变颜色以示区分
            if (_ctx.PenStyle == BrushStyle.Eraser)
            {
                ThicknessPreview.Stroke = Brushes.Black; // 橡皮擦用黑色虚线
            }
            else
            {
                ThicknessPreview.Stroke = Brushes.Purple;
            }

            ThicknessPreview.Fill = Brushes.Transparent;
            ThicknessPreview.StrokeThickness = 2;
        }



        private void UpdateWindowTitle()
        {
          
            if (_currentTabItem == null)
            {
                this.Title = $"TabPaint {ProgramVersion}";
                if (AppTitleBar.TitleTextControl != null) AppTitleBar.TitleTextControl.Text = this.Title;
                return;
            }

            string dirtyMark = _currentTabItem.IsDirty ? "*" : "";

            // 如果是新建的未保存文件(IsNew)，通常显示 "未命名-0" 之类，这里取 FileName
            string displayFileName = _currentTabItem.FileName;

            string countInfo = "";

            // 逻辑修正：只要不是新建的纯内存图片(IsNew)，都去文件列表里找位置
            if (!_currentTabItem.IsNew)
            {
                int total = _imageFiles.Count;
                int currentIndex = -1;

                // 核心修复：直接在总文件列表(_imageFiles)中查找路径，而不是在标签列表(FileTabs)中查对象
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

            // 4. 拼接标题
            string newTitle = $"{dirtyMark}{displayFileName}{countInfo} - TabPaint {ProgramVersion}";

            // 5. 更新 UI
            this.Title = newTitle;
            if (AppTitleBar.TitleTextControl != null) AppTitleBar.TitleTextControl.Text = newTitle;
          
        }

        private bool _fontsLoaded = false;

        // 懒加载字体的核心方法
        private void EnsureFontsLoaded()
        {
            if (_fontsLoaded) return;

            // 可以在后台线程加载以防卡顿，但字体列表通常很快，这里用简单的 Task 包装
            System.Threading.Tasks.Task.Run(() =>
            {
                // 获取系统字体
                var fonts = Fonts.SystemFontFamilies.OrderBy(f => f.Source).ToList();

                // 切回 UI 线程更新
                Dispatcher.Invoke(() =>
                {
                    FontFamilyBox.ItemsSource = fonts;
                    // 设置默认字体 (例如 Microsoft YaHei 或 Segoe UI)
                    FontFamilyBox.SelectedItem = fonts.FirstOrDefault(f => f.Source.Contains("Microsoft YaHei"))
                                              ?? fonts.FirstOrDefault(f => f.Source.Contains("Segoe UI"))
                                              ?? fonts.FirstOrDefault();
                    _fontsLoaded = true;
                });
            });
        }

        public void ShowTextToolbarFor(System.Windows.Controls.TextBox tb)
        {
            EnsureFontsLoaded();
            _activeTextBox = tb;
            TextEditBar.Visibility = Visibility.Visible;

            FontFamilyBox.SelectedItem = tb.FontFamily;
            FontSizeBox.Text = tb.FontSize.ToString(CultureInfo.InvariantCulture);
            BoldBtn.IsChecked = tb.FontWeight == FontWeights.Bold;
            ItalicBtn.IsChecked = tb.FontStyle == FontStyles.Italic;
            UnderlineBtn.IsChecked = tb.TextDecorations == TextDecorations.Underline;
        }

        public void HideTextToolbar()
        {
            TextEditBar.Visibility = Visibility.Collapsed;
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
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Width = 12,
                    Height = 12
                }
            };
        }
        private void UpdateColorHighlight()
        {

            // 假设你的两个颜色按钮在 XAML 里设置了 Name="ColorBtn1" 和 Name="ColorBtn2"
            MainToolBar.ColorBtn1.Tag = !useSecondColor ? "True" : "False"; // 如果不是色2，那就是色1选中
            MainToolBar.ColorBtn2.Tag = useSecondColor ? "True" : "False";
        }

  
        private void AutoSetFloatBarVisibility()
        {
            var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
            if (mw.ToolPanelGrid == null) return;

            // 1. 判断显示逻辑
            bool showThickness = (_router.CurrentTool is PenTool && _ctx.PenStyle != BrushStyle.Pencil) || _router.CurrentTool is ShapeTool;
            showThickness = showThickness && !IsViewMode;

            bool showOpacity = _router.CurrentTool is PenTool || _router.CurrentTool is TextTool/* || _router.CurrentTool is ShapeTool*/;
            showOpacity = showOpacity && !IsViewMode;

            mw.ThicknessPanel.Visibility = showThickness ? Visibility.Visible : Visibility.Collapsed;
            mw.OpacityPanel.Visibility = showOpacity ? Visibility.Visible : Visibility.Collapsed;

            var rows = mw.ToolPanelGrid.RowDefinitions;

            // === 重置基础状态 ===
            // 先把 Opacity 放回它原来的位置 (Row 3)，防止状态残留
            Grid.SetRow(mw.OpacityPanel, 3);

            // 重置所有行高为默认配置
            rows[1].Height = new GridLength(10000, GridUnitType.Star); // 上槽位
            rows[2].Height = new GridLength(15);                     // 间距
            rows[3].Height = new GridLength(10000, GridUnitType.Star); // 下槽位

            // === 根据情况调整 ===
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
            UpdateBrushAndButton(MainMenu.BtnUndo, MainMenu.IconUndo, _undo.CanUndo);
            UpdateBrushAndButton(MainMenu.BtnRedo, MainMenu.IconRedo, _undo.CanRedo);

        }

        public void SetCropButtonState()
        {
            UpdateBrushAndButton(MainToolBar.CutImage, MainToolBar.CutImageIcon, _tools.Select is SelectTool st && _ctx.SelectionOverlay.Visibility != Visibility.Collapsed);
        }

        private void UpdateBrushAndButton(System.Windows.Controls.Button button, System.Windows.Shapes.Path image, bool isEnabled)
        {
            button.IsEnabled = isEnabled;
            image.Fill = isEnabled ? Brushes.Black : Brushes.Gray;
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
        private void SaveBitmap(string path)
        {
            // 1. 获取当前编辑的像素数据
            // _bitmap 是 96 DPI 的，我们只取它的像素内容
            int width = _bitmap.PixelWidth;
            int height = _bitmap.PixelHeight;
            int stride = width * 4;

            byte[] pixels = new byte[height * stride];

            try
            {
                // 现在 stride 是根据 bitmap 自身的宽度计算的，绝对不会报错
                _bitmap.CopyPixels(pixels, stride, 0);
            }
            catch (System.ArgumentOutOfRangeException)
            {
                // 防御性编程：如果 _bitmap 还是旧的引用，或者 BackBufferStride 特殊
                // 尝试直接使用 WriteableBitmap 的 BackBufferStride (如果 _bitmap 是 WriteableBitmap)
                if (_bitmap is WriteableBitmap wb)
                {
                    stride = wb.BackBufferStride;
                    pixels = new byte[height * stride]; // 重新分配数组大小以匹配 stride
                    wb.CopyPixels(pixels, stride, 0);
                }
                else
                {
                    throw;
                }
            }

            // 2. 创建用于保存的 BitmapSource，并恢复原始 DPI
            var saveSource = BitmapSource.Create(
                width, height,
                _originalDpiX,
                _originalDpiY,
                PixelFormats.Bgra32,
                null,
                pixels,
                stride
            );

            // 3. 根据扩展名选择编码器
            BitmapEncoder encoder;
            string ext = System.IO.Path.GetExtension(path).ToLower();

            // 这里简单判断，你可以根据 PicFilterString 里的逻辑扩展
            if (ext == ".jpg" || ext == ".jpeg")
            {
                encoder = new JpegBitmapEncoder { QualityLevel = 90 }; // JPG 质量
            }
            else if (ext == ".bmp")
            {
                encoder = new BmpBitmapEncoder();
            }
            else if (ext == ".tiff" || ext == ".tif")
            {
                encoder = new TiffBitmapEncoder();
            }
            else // 默认 PNG
            {
                encoder = new PngBitmapEncoder();
            }

            encoder.Frames.Add(BitmapFrame.Create(saveSource));

            // 4. 写入文件
            using (FileStream fs = new FileStream(path, FileMode.Create))
            {
                encoder.Save(fs);
            }

            MarkAsSaved();

            // 5. 新增：更新对应标签页的缩略图
            UpdateTabThumbnail(path);
        }


       
        private void UpdateSliderBarValue(double newScale)
        {
           
            MyStatusBar.ZoomSliderControl.Value = newScale;
            ZoomLevel = newScale.ToString("P0");
            MyStatusBar.ZoomComboBox.Text = newScale.ToString("P0");
            SetZoom(newScale);
        }
    }
}