using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TabPaint.Services;

namespace TabPaint
{
    public class WatermarkSettings
    {
        public bool IsText { get; set; }
        public string Text { get; set; }
        public BitmapImage ImageSource { get; set; }
        public double FontSize { get; set; }
        public FontFamily FontFamily { get; set; }
        public Color Color { get; set; }
        public double Opacity { get; set; }
        public double Angle { get; set; }
        public int Rows { get; set; }
        public int Cols { get; set; }
        public double ImageScale { get; set; }
        public bool UseRandom { get; set; }
    }

    public partial class WatermarkWindow : Window
    {
        public bool _isWindowLoaded = false;
        public bool ApplyToAll => ChkApplyToAll.IsChecked == true;
        private Image _previewLayer;

        public WatermarkSettings CurrentSettings { get; private set; }
        private WriteableBitmap _originalBitmap; 
        private WriteableBitmap _targetBitmap;   
        private BitmapImage _watermarkImageSource;
        private class ColorItem
        {
            public string Name { get; set; }
            public Color Color { get; set; }
            public bool IsCustom { get; set; }
        }

        private bool _isUpdatingColor = false; 
        public WriteableBitmap FinalBitmap { get; private set; } 

        private bool _isInitialized = false;
        private Color _selectedColor = Colors.White;

        public WatermarkWindow(WriteableBitmap bitmapForPreview, Image previewLayer)
        {
            InitializeComponent();
            this.SupportFocusHighlight();
            _targetBitmap = bitmapForPreview;
            _originalBitmap = bitmapForPreview.Clone();
            _previewLayer = previewLayer;
            
            if (_previewLayer != null)
            {
                _previewLayer.Source = null;
                _previewLayer.Visibility = Visibility.Visible;
                _previewLayer.Width = _originalBitmap.PixelWidth;
                _previewLayer.Height = _originalBitmap.PixelHeight;
                RenderOptions.SetBitmapScalingMode(_previewLayer, BitmapScalingMode.HighQuality);
            }

            InitializeFonts();
            InitializeCommonColors();
            this.Loaded += (s, e) =>
            {
                _isWindowLoaded = true;
                _isInitialized = true;
                InitializeBackgroundPreview();
                UpdatePreview();
            };
        }

        private void InitializeBackgroundPreview()
        {
            if (_originalBitmap == null) return;
            // 预渲染对话框内的背景，仅在打开或重置时执行一次
            double maxPreviewDim = 1200;
            double w = _originalBitmap.PixelWidth;
            double h = _originalBitmap.PixelHeight;
            double scale = Math.Min(maxPreviewDim / w, maxPreviewDim / h);

            int scaledW = (int)(w * scale);
            int scaledH = (int)(h * scale);
            if (scaledW < 1) scaledW = 1;
            if (scaledH < 1) scaledH = 1;

            var rtb = new RenderTargetBitmap(scaledW, scaledH, 96, 96, PixelFormats.Pbgra32);
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawImage(_originalBitmap, new Rect(0, 0, scaledW, scaledH));
            }
            rtb.Render(dv);
            rtb.Freeze();
            var imgBG = this.FindName("ImgWindowBackground") as Image;
            if (imgBG != null) imgBG.Source = rtb;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            MicaAcrylicManager.ApplyEffect(this);
            bool isDark = (ThemeManager.CurrentAppliedTheme == AppTheme.Dark);
            ThemeManager.SetWindowImmersiveDarkMode(this, isDark);
            var src = (HwndSource)PresentationSource.FromVisual(this);
            if (src != null)
            {
                src.CompositionTarget.BackgroundColor = Colors.Transparent;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_previewLayer != null)
            {
                _previewLayer.Source = null;
                _previewLayer.Visibility = Visibility.Collapsed;
            }
            base.OnClosed(e);
        }

        private void InitializeCommonColors()
        {
            var colors = new List<ColorItem>
            {
                new ColorItem { Name = LocalizationManager.GetString("L_Color_White"), Color = Colors.White },
                new ColorItem { Name = LocalizationManager.GetString("L_Color_Black"), Color = Colors.Black },
                new ColorItem { Name = LocalizationManager.GetString("L_Color_Red"), Color = Colors.Red },
                new ColorItem { Name = LocalizationManager.GetString("L_Color_Green"), Color = Colors.Lime }, 
                new ColorItem { Name = LocalizationManager.GetString("L_Color_Blue"), Color = Colors.Blue },
                new ColorItem { Name = LocalizationManager.GetString("L_Color_Yellow"), Color = Colors.Yellow },
                new ColorItem { Name = LocalizationManager.GetString("L_Color_Gray"), Color = Colors.Gray },
                new ColorItem { Name = LocalizationManager.GetString("L_Color_Orange"), Color = Colors.Orange },
                new ColorItem { Name = LocalizationManager.GetString("L_Color_Purple"), Color = Colors.Purple },
                new ColorItem { Name = LocalizationManager.GetString("L_ToolBar_CustomColor"), Color = Colors.Transparent, IsCustom = true }
            };

            ComboCommonColors.ItemsSource = colors;
            ComboCommonColors.DisplayMemberPath = "Name";
            ComboCommonColors.SelectedValuePath = "Color";
            ComboCommonColors.SelectedIndex = 0;
            UpdateColorInternal(Colors.Black, false);
        }

        private void InitializeFonts()
        {
            var fonts = FontService.GetSystemFonts();
            ComboFontFamily.ItemsSource = fonts;
            ComboFontFamily.SelectedValuePath = "FontFamily";
            var defaultFontItem = FontService.GetDefaultFont(fonts);
            if (defaultFontItem != null)
            {
                ComboFontFamily.SelectedItem = defaultFontItem;
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _isInitialized = false;
            SliderOpacity.Value = 0.5;
            SliderAngle.Value = -45;
            SliderRows.Value = 3;
            SliderCols.Value = 3;
            SliderImgScale.Value = 0.8;
            TxtContent.Text = "TabPaint";
            TxtFontSize.Text = "40";
            ChkRandom.IsChecked = false;
            InitializeFonts();
            ComboCommonColors.SelectedIndex = 0;
            UpdateColorInternal(Colors.White, false);
            _isInitialized = true;
            UpdatePreview();
        }

        private void ColorPreview_Click(object sender, MouseButtonEventArgs e)
        {
            var dlg = new ModernColorPickerWindow(_selectedColor);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                Color c = dlg.SelectedColor;
                c.A = 255;
                UpdateColorInternal(c, true);
                UpdatePreview();
            }
        }

        private void CommonColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || _isUpdatingColor) return;
            if (ComboCommonColors.SelectedItem is ColorItem item)
            {
                if (item.IsCustom) return;
                UpdateColorInternal(item.Color, false);
                UpdatePreview();
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var finalBmp = ApplyWatermarkToBitmap(_originalBitmap, GetCurrentSettings());
            int w = finalBmp.PixelWidth;
            int h = finalBmp.PixelHeight;
            int stride = w * 4;
            byte[] data = new byte[h * stride];
            finalBmp.CopyPixels(data, stride, 0);
            _targetBitmap.WritePixels(new Int32Rect(0, 0, w, h), data, stride, 0);
            FinalBitmap = _targetBitmap;
            CurrentSettings = GetCurrentSettings();
            this.SetDialogResultSafe(true);
            Close();
        }

        private WatermarkSettings GetCurrentSettings()
        {
            return new WatermarkSettings
            {
                IsText = RadioText.IsChecked == true,
                Text = TxtContent.Text,
                ImageSource = _watermarkImageSource,
                FontSize = double.TryParse(TxtFontSize.Text, out double fs) ? fs : 40,
                FontFamily = ComboFontFamily.SelectedValue as FontFamily ?? new FontFamily("Microsoft YaHei"),
                Color = _selectedColor,
                Opacity = SliderOpacity.Value,
                Angle = SliderAngle.Value,
                Rows = (int)SliderRows.Value,
                Cols = (int)SliderCols.Value,
                ImageScale = SliderImgScale.Value,
                UseRandom = ChkRandom.IsChecked == true
            };
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.SetDialogResultSafe(false);
            Close();
        }

        private void Param_Changed_Combo(object sender, SelectionChangedEventArgs e) { if (_isInitialized) UpdatePreview(); }
        
        private void Mode_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            bool isText = RadioText.IsChecked == true;
            if (PanelText != null) PanelText.Visibility = isText ? Visibility.Visible : Visibility.Collapsed;
            if (PanelImage != null) PanelImage.Visibility = !isText ? Visibility.Visible : Visibility.Collapsed;
            
            var sb = this.Resources["FadeInAnimation"] as System.Windows.Media.Animation.Storyboard;
            if (sb != null)
            {
                if (isText && PanelText != null) sb.Begin(PanelText);
                else if (!isText && PanelImage != null) sb.Begin(PanelImage);
            }
            UpdatePreview();
        }

        private void Param_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (_isInitialized) UpdatePreview(); }
        private void Input_TextChanged(object sender, TextChangedEventArgs e) { if (_isInitialized) UpdatePreview(); }
        
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = new Regex("[^0-9]+").IsMatch(e.Text);
        }

        private void Image_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0) LoadWatermarkImage(files[0]);
            }
        }

        private void ImageSelect_Click(object sender, MouseButtonEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" };
            if (dlg.ShowDialog() == true) LoadWatermarkImage(dlg.FileName);
        }

        private void LoadWatermarkImage(string path)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                _watermarkImageSource = bmp;
                if (ImgPreview != null) ImgPreview.Source = bmp;
                if (TxtImageName != null) TxtImageName.Text = System.IO.Path.GetFileName(path);
                UpdatePreview();
            }
            catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
        }

        private void HexInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized || _isUpdatingColor) return;
            string hex = TxtHexColor.Text.Trim();
            try
            {
                if (!hex.StartsWith("#")) hex = "#" + hex;
                Color temp = (Color)ColorConverter.ConvertFromString(hex);
                Color c = Color.FromRgb(temp.R, temp.G, temp.B); 
                _selectedColor = c;
                if (RectColorPreview != null) RectColorPreview.Fill = new SolidColorBrush(_selectedColor);
                _isUpdatingColor = true;
                ComboCommonColors.SelectedIndex = -1;
                _isUpdatingColor = false;
                UpdatePreview();
            }
            catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
        }

        public void UpdateColorFromPicker(Color color)
        {
            UpdateColorInternal(color, true);
            UpdatePreview();
        }

        private void UpdateColorInternal(Color color, bool isCustom)
        {
            _isUpdatingColor = true;
            _selectedColor = color;
            if (TxtHexColor != null) TxtHexColor.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            if (RectColorPreview != null) RectColorPreview.Fill = new SolidColorBrush(color);
            if (isCustom && ComboCommonColors != null) ComboCommonColors.SelectedIndex = -1;
            _isUpdatingColor = false;
        }

        private void Generic_Changed(object sender, RoutedEventArgs e) { if (_isInitialized) UpdatePreview(); }
        
        private void UpdatePreview()
        {
            if (!_isInitialized || _originalBitmap == null) return;
            var settings = GetCurrentSettings();

            // 统一使用 1.0 作为渲染 DPI，确保预览效果与最终应用效果一致（位图导出固定为 96DPI）
            double pixelsPerDip = 1.0;

            // 实时预览优化：生成统一的全尺寸矢量 Drawing 指令
            var fullDrawing = CreateWatermarkDrawing(_originalBitmap, settings, true, pixelsPerDip);
            var di = new DrawingImage(fullDrawing);
            di.Freeze(); // 必须冻结以供跨线程/高性能渲染使用

            // 1. 同步到主窗口实时预览图层
            if (_previewLayer != null)
            {
                _previewLayer.Source = di;
            }

            // 2. 同步到对话框内部预览区域
            if (_isWindowLoaded)
            {
                var imgPreview = this.FindName("ImgWindowPreview") as Image;
                if (imgPreview != null) imgPreview.Source = di;
            }
        }

        private static Drawing CreateWatermarkDrawing(BitmapSource source, WatermarkSettings settings, bool onlyWatermark, double pixelsPerDip = 1.0)
        {
            int w = source.PixelWidth;
            int h = source.PixelHeight;
            if (settings.Rows < 1) settings.Rows = 1;
            if (settings.Cols < 1) settings.Cols = 1;

            var drawingGroup = new DrawingGroup();
            // 强制设置裁剪区域，防止旋转或溢出的水印改变 DrawingImage 的原始比例，确保预览与实际对齐
            drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0, 0, w, h));

            using (var dc = drawingGroup.Open())
            {
                // 即使是“仅水印”模式，也绘制一个透明矩形，以确保 DrawingImage 的边界与原图一致
                // 这样在 Image Stretch="Uniform" 时，预览层和底图层能完美对齐
                if (onlyWatermark)
                {
                    dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
                }
                else
                {
                    dc.DrawImage(source, new Rect(0, 0, w, h));
                }

                if (settings.Opacity > 0)
                {
                    dc.PushOpacity(settings.Opacity);
                    double cellW = (double)w / settings.Cols;
                    double cellH = (double)h / settings.Rows;

                    // 统一使用循环模式绘制，允许水印在单元格间溢出重叠，解决截断问题
                    Random rnd = new Random(12345);
                    Brush wmBrush = new SolidColorBrush(settings.Color);
                    wmBrush.Freeze();
                    
                    FormattedText? ft = null;
                    if (settings.IsText && !string.IsNullOrEmpty(settings.Text))
                    {
                        var tf = new Typeface(settings.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                        ft = new FormattedText(settings.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tf, settings.FontSize, wmBrush, pixelsPerDip);
                    }

                    for (int r = 0; r < settings.Rows; r++)
                    {
                        for (int c = 0; c < settings.Cols; c++)
                        {
                            double cx = c * cellW + cellW / 2;
                            double cy = r * cellH + cellH / 2;
                            
                            if (settings.UseRandom)
                            {
                                double offsetX = (rnd.NextDouble() - 0.5) * (cellW * 0.5);
                                double offsetY = (rnd.NextDouble() - 0.5) * (cellH * 0.5);
                                cx += offsetX;
                                cy += offsetY;
                            }

                            if (settings.Angle != 0) dc.PushTransform(new RotateTransform(settings.Angle, cx, cy));
                            
                            if (ft != null)
                            {
                                dc.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
                            }
                            else if (!settings.IsText && settings.ImageSource != null)
                            {
                                double iw = settings.ImageSource.PixelWidth;
                                double ih = settings.ImageSource.PixelHeight;
                                double scaleX = (cellW * 0.8) / iw;
                                double scaleY = (cellH * 0.8) / ih;
                                double finalScale = Math.Min(scaleX, scaleY) * settings.ImageScale;
                                double fw = iw * finalScale;
                                double fh = ih * finalScale;
                                dc.DrawImage(settings.ImageSource, new Rect(cx - fw / 2, cy - fh / 2, fw, fh));
                            }
                            
                            if (settings.Angle != 0) dc.Pop();
                        }
                    }
                    dc.Pop();
                }
            }
            return drawingGroup;
        }

        public static BitmapSource ApplyWatermarkToBitmap(BitmapSource original, WatermarkSettings settings)
        {
            if (!original.IsFrozen && original.CheckAccess() == false)
                throw new InvalidOperationException("Source bitmap must be frozen.");
            
            // 使用 1.0 作为默认离线渲染 DPI
            var drawing = CreateWatermarkDrawing(original, settings, onlyWatermark: false, 1.0);
            var rtb = new RenderTargetBitmap(original.PixelWidth, original.PixelHeight, 96, 96, PixelFormats.Pbgra32);
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawDrawing(drawing);
            }
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            this.SetDialogResultSafe(false);
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }
    }
}
