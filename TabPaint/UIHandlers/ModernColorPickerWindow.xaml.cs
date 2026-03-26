using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TabPaint.UIHandlers;
using TabPaint.Services;

namespace TabPaint
{
    public partial class ModernColorPickerWindow : Window
    {
        private WindowSnapManager _snapManager;
        private bool _isCompact;
        private bool _isDraggingWheel = false;

        public bool IsSecondaryColor { get; set; } = false;

        private static readonly Dictionary<Color, string> StandardColorNames = new Dictionary<Color, string>
        {
            { Colors.White, "L_Color_White" }, { Colors.Snow, "L_Color_Snow" },  { Colors.Honeydew, "L_Color_Honeydew" }, { Colors.MintCream, "L_Color_MintCream" },
            { Colors.Azure, "L_Color_Azure" }, { Colors.AliceBlue, "L_Color_AliceBlue" },     { Colors.GhostWhite, "L_Color_GhostWhite" },{ Colors.WhiteSmoke, "L_Color_WhiteSmoke" },
            { Colors.Beige, "L_Color_Beige" },{ Colors.OldLace, "L_Color_OldLace" },  { Colors.FloralWhite, "L_Color_FloralWhite" }, { Colors.Ivory, "L_Color_Ivory" },
            { Colors.AntiqueWhite, "L_Color_AntiqueWhite" }, { Colors.Linen, "L_Color_Linen" },   { Colors.LavenderBlush, "L_Color_LavenderBlush" },{ Colors.MistyRose, "L_Color_MistyRose" },
            { Colors.Gainsboro, "L_Color_Gainsboro" }, { Colors.LightGray, "L_Color_LightGray" },   { Colors.Silver, "L_Color_Silver" }, { Colors.DarkGray, "L_Color_DarkGray" },
            { Colors.Gray, "L_Color_Gray" }, { Colors.DimGray, "L_Color_DimGray" },   { Colors.LightSlateGray, "L_Color_LightSlateGray" },{ Colors.SlateGray, "L_Color_SlateGray" },
            { Colors.DarkSlateGray, "L_Color_DarkSlateGray" }, { Colors.Black, "L_Color_Black" },   { Colors.IndianRed, "L_Color_IndianRed" },  { Colors.LightCoral, "L_Color_LightCoral" },
            { Colors.Salmon, "L_Color_Salmon" }, { Colors.DarkSalmon, "L_Color_DarkSalmon" }, { Colors.LightSalmon, "L_Color_LightSalmon" },{ Colors.Crimson, "L_Color_Crimson" },
            { Colors.Red, "L_Color_Red" },{ Colors.Firebrick, "L_Color_FireBrick" },  { Colors.DarkRed, "L_Color_DarkRed" },{ Colors.Pink, "L_Color_Pink" },
            { Colors.LightPink, "L_Color_LightPink" }, { Colors.HotPink, "L_Color_HotPink" },    { Colors.DeepPink, "L_Color_DeepPink" },{ Colors.MediumVioletRed, "L_Color_MediumVioletRed" },
            { Colors.PaleVioletRed, "L_Color_PaleVioletRed" }, { Colors.Coral, "L_Color_Coral" },  { Colors.Tomato, "L_Color_Tomato" },{ Colors.OrangeRed, "L_Color_OrangeRed" },
            { Colors.DarkOrange, "L_Color_DarkOrange" }, { Colors.Orange, "L_Color_Orange" }, { Colors.Gold, "L_Color_Gold" },{ Colors.Yellow, "L_Color_Yellow" },
            { Colors.LightYellow, "L_Color_LightYellow" }, { Colors.LemonChiffon, "L_Color_LemonChiffon" }, { Colors.PapayaWhip, "L_Color_PapayaWhip" }, { Colors.Moccasin, "L_Color_Moccasin" },
            { Colors.PeachPuff, "L_Color_PeachPuff" }, { Colors.PaleGoldenrod, "L_Color_PaleGoldenrod" },   { Colors.Khaki, "L_Color_Khaki" },{ Colors.DarkKhaki, "L_Color_DarkKhaki" },
            { Colors.Lavender, "L_Color_Lavender" }, { Colors.Thistle, "L_Color_Thistle" }, { Colors.Plum, "L_Color_Plum" },{ Colors.Violet, "L_Color_Violet" },
            { Colors.Orchid, "L_Color_Orchid" }, { Colors.Magenta, "L_Color_Magenta" },    { Colors.MediumOrchid, "L_Color_MediumOrchid" },{ Colors.MediumPurple, "L_Color_MediumPurple" },
            { Colors.BlueViolet, "L_Color_BlueViolet" },{ Colors.DarkViolet, "L_Color_DarkViolet" }, { Colors.DarkOrchid, "L_Color_DarkOrchid" }, { Colors.DarkMagenta, "L_Color_DarkMagenta" },
            { Colors.Purple, "L_Color_Purple" }, { Colors.Indigo, "L_Color_Indigo" },  { Colors.SlateBlue, "L_Color_SlateBlue" },  { Colors.DarkSlateBlue, "L_Color_DarkSlateBlue" },
            { Colors.GreenYellow, "L_Color_GreenYellow" }, { Colors.Chartreuse, "L_Color_Chartreuse" },   { Colors.LawnGreen, "L_Color_LawnGreen" }, { Colors.Lime, "L_Color_Lime" },
            { Colors.LimeGreen, "L_Color_LimeGreen" }, { Colors.PaleGreen, "L_Color_PaleGreen" },  { Colors.LightGreen, "L_Color_LightGreen" }, { Colors.MediumSpringGreen, "L_Color_MediumSpringGreen" },
            { Colors.SpringGreen, "L_Color_SpringGreen" },  { Colors.MediumSeaGreen, "L_Color_MediumSeaGreen" },  { Colors.SeaGreen, "L_Color_SeaGreen" },{ Colors.ForestGreen, "L_Color_ForestGreen" },
            { Colors.Green, "L_Color_Green" }, { Colors.DarkGreen, "L_Color_DarkGreen" },  { Colors.YellowGreen, "L_Color_YellowGreen" }, { Colors.OliveDrab, "L_Color_OliveDrab" },
            { Colors.Olive, "L_Color_Olive" },{ Colors.DarkOliveGreen, "L_Color_DarkOliveGreen" },  { Colors.MediumAquamarine, "L_Color_MediumAquamarine" },  { Colors.DarkSeaGreen, "L_Color_DarkSeaGreen" },
            { Colors.LightSeaGreen, "L_Color_LightSeaGreen" },{ Colors.DarkCyan, "L_Color_DarkCyan" },   { Colors.Teal, "L_Color_Teal" },{ Colors.Cyan, "L_Color_Cyan" },
            { Colors.LightCyan, "L_Color_LightCyan" },{ Colors.PaleTurquoise, "L_Color_PaleTurquoise" }, { Colors.Aquamarine, "L_Color_Aquamarine" },  { Colors.Turquoise, "L_Color_Turquoise" },
            { Colors.MediumTurquoise, "L_Color_MediumTurquoise" }, { Colors.DarkTurquoise, "L_Color_DarkTurquoise" },   { Colors.CadetBlue, "L_Color_CadetBlue" },   { Colors.SteelBlue, "L_Color_SteelBlue" },
            { Colors.LightSteelBlue, "L_Color_LightSteelBlue" }, { Colors.PowderBlue, "L_Color_PowderBlue" },    { Colors.LightBlue, "L_Color_LightBlue" },  { Colors.SkyBlue, "L_Color_SkyBlue" },
            { Colors.LightSkyBlue, "L_Color_LightSkyBlue" }, { Colors.DeepSkyBlue, "L_Color_DeepSkyBlue" },    { Colors.DodgerBlue, "L_Color_DodgerBlue" }, { Colors.CornflowerBlue, "L_Color_CornflowerBlue" },
            { Colors.MediumSlateBlue, "L_Color_MediumSlateBlue" }, { Colors.RoyalBlue, "L_Color_RoyalBlue" },  { Colors.Blue, "L_Color_Blue" }, { Colors.MediumBlue, "L_Color_MediumBlue" },
            { Colors.DarkBlue, "L_Color_DarkBlue" }, { Colors.Navy, "L_Color_Navy" }, { Colors.MidnightBlue, "L_Color_MidnightBlue" }, { Colors.Cornsilk, "L_Color_Cornsilk" },
            { Colors.BlanchedAlmond, "L_Color_BlanchedAlmond" },{ Colors.Bisque, "L_Color_Bisque" },  { Colors.NavajoWhite, "L_Color_NavajoWhite" },   { Colors.Wheat, "L_Color_Wheat" },
            { Colors.BurlyWood, "L_Color_BurlyWood" }, { Colors.Tan, "L_Color_Tan" },  { Colors.RosyBrown, "L_Color_RosyBrown" },{ Colors.SandyBrown, "L_Color_SandyBrown" },
            { Colors.Goldenrod, "L_Color_Goldenrod" },  { Colors.DarkGoldenrod, "L_Color_DarkGoldenrod" },  { Colors.Peru, "L_Color_Peru" },  { Colors.Chocolate, "L_Color_Chocolate" },
            { Colors.SaddleBrown, "L_Color_SaddleBrown" },  { Colors.Sienna, "L_Color_Sienna" }, { Colors.Brown, "L_Color_Brown" },   { Colors.Maroon, "L_Color_Maroon" },
            { Color.FromRgb(102, 51, 0), "L_Color_DeepBrown" }, { Color.FromRgb(0, 102, 102), "L_Color_RockCyan" },  { Color.FromRgb(62, 39, 35), "L_Color_Coffee" },  { Color.FromRgb(204, 255, 204), "L_Color_MintGreen" },
            { Color.FromRgb(255, 204, 255), "L_Color_LightPink" },   { Color.FromRgb(255, 204, 204), "L_Color_LightRed" },   { Color.FromRgb(255, 229, 204), "L_Color_LightOrange" },   { Color.FromRgb(204, 0, 0), "L_Color_DeepRed" },
            { Color.FromRgb(204, 102, 0), "L_Color_DeepOrange" },   { Color.FromRgb(153, 76, 0), "L_Color_Ochre" },  { Color.FromRgb(51, 0, 102), "L_Color_DeepViolet" }
        };

        private string GetFriendlyColorName(Color target)
        {
            Color bestMatch = Color.FromRgb(0, 0, 0);
            double minDiff = double.MaxValue;

            Color rgbTarget = Color.FromRgb(target.R, target.G, target.B);

            foreach (var standard in StandardColorNames.Keys)
            {
                double diff = Math.Pow(rgbTarget.R - standard.R, 2) +
                              Math.Pow(rgbTarget.G - standard.G, 2) +
                              Math.Pow(rgbTarget.B - standard.B, 2);

                if (diff < minDiff)
                {
                    minDiff = diff;
                    bestMatch = standard;
                }
            }

            string nameKey = StandardColorNames[bestMatch];
            string localizedName = LocalizationManager.GetString(nameKey);
            string engName = TryGetResource(nameKey, "en-US") ?? nameKey;

            double transparency = (1 - target.A / 255.0) * 100.0;
            string finalZh;

            if (transparency > 95)
            {
                finalZh = LocalizationManager.GetString("L_ColorPicker_Transparent");
            }
            else if (transparency > 50)
            {
                string prefixZh = LocalizationManager.GetString("L_ColorPicker_TransparentPrefix");
                finalZh = $"{prefixZh}{localizedName}";
            }
            else
            {
                finalZh = localizedName;
            }

            return $"{finalZh}";
        }

        private static ResourceDictionary _enDict;
        private string TryGetResource(string key, string culture = null)
        {
            if (string.IsNullOrEmpty(key)) return null;

            if (culture == "en-US")
            {
                if (_enDict == null)
                {
                    try
                    {
                        _enDict = new ResourceDictionary { Source = new Uri("pack://application:,,,/Resources/Lang.en-US.xaml") };
                    }
                    catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
                }
                if (_enDict != null && _enDict.Contains(key)) return _enDict[key] as string;
            }

            return Application.Current.TryFindResource(key) as string;
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
            if (_snapManager != null)
            {
                _snapManager.Detach();
                _snapManager = null;
            }
            base.OnClosed(e);
        }

        public Color SelectedColor { get; private set; }
        private enum ColorMode { RGB, HSV }
        private ColorMode _currentMode = ColorMode.RGB;
        private bool _isDraggingSpectrum = false;
        private bool _isDraggingAlpha = false;
        private bool _isDraggingHue = false;
        private bool _isUpdatingInputs = false;

        private double _currentHue = 0;
        private double _currentSat = 1;
        private double _currentVal = 1;
        private double _currentAlpha = 255;

        public ModernColorPickerWindow(Color initialColor, bool isSecondary = false)
        {
            InitializeComponent();
            this.SupportFocusHighlight();
            SelectedColor = initialColor;
            IsSecondaryColor = isSecondary;

            _isCompact = SettingsManager.Instance.Current.IsCompactColorPicker;

            OriginalColorRect.Fill = new SolidColorBrush(initialColor);
            RenderHueGradient();
            RenderColorWheel();
            SetColorFromRgb(initialColor.R, initialColor.G, initialColor.B);
            LoadCustomColorsFromSettings();

            this.MouseDown += (s, e) => { Keyboard.ClearFocus(); };
            this.KeyDown += (s, e) => { if (e.Key == Key.Escape) { this.Close(); } };

            Loaded += (s, e) =>
            {
                ApplyModeUI();
                UpdateUI();
                UpdateBasicColorTooltips();
                UpdateCompactButtons();
                
                if (ToggleBtnIcon != null)
                {
                    ToggleBtnIcon.Text = _isCompact ? "\uE76C" : "\uE76B";
                }
            };
        }

        private void ApplyModeUI()
        {
            if (_isCompact)
            {
                NormalModeRoot.Visibility = Visibility.Collapsed;
                CompactModeRoot.Visibility = Visibility.Visible;

                this.MinWidth = 0;
                this.MinHeight = 0;
                this.MaxWidth = double.PositiveInfinity;
                this.MaxHeight = double.PositiveInfinity;

                this.Width = 180;
                this.Height = 260;
                this.MinWidth = 180;
                this.MinHeight = 260;
                this.MaxWidth = 180;
                this.MaxHeight = 260;

                this.ResizeMode = ResizeMode.NoResize;
                TitleText.Visibility = Visibility.Collapsed;

                if (ToggleBtnIcon != null) ToggleBtnIcon.Text = "\uE76C"; // ChevronRight
                ToggleCompactBtn.ToolTip = "Normal Mode";

                if (_snapManager == null && this.Owner != null)
                {
                    _snapManager = new WindowSnapManager(this, this.Owner, SnapEdge.Right);
                    _snapManager.Attach();
                }
            }
            else
            {
                CompactModeRoot.Visibility = Visibility.Collapsed;
                NormalModeRoot.Visibility = Visibility.Visible;
                
                this.MaxWidth = double.PositiveInfinity;
                this.MaxHeight = double.PositiveInfinity;
                this.MinWidth = 650;
                this.MinHeight = 640;
                this.Width = 700;
                this.Height = 640;

                this.ResizeMode = ResizeMode.CanResize;
                TitleText.Visibility = Visibility.Visible;
                if (ToggleBtnIcon != null) ToggleBtnIcon.Text = "\uE76B"; // ChevronLeft
                ToggleCompactBtn.ToolTip = "Compact Mode";

                if (_snapManager != null)
                {
                    _snapManager.Detach();
                    _snapManager = null;
                }
            }
        }

        private void ToggleCompact_Click(object sender, RoutedEventArgs e)
        {
            _isCompact = !_isCompact;
            SettingsManager.Instance.Current.IsCompactColorPicker = _isCompact;
            SettingsManager.Instance.Save();

            ApplyModeUI();
            UpdateUI();
        }

        private void UpdateBasicColorTooltips()
        {
            foreach (var child in BasicColorsGrid.Children)
            {
                if (child is Button btn && btn.Background is SolidColorBrush brush)
                {
                    btn.ToolTip = GetFriendlyColorName(brush.Color);
                }
            }
        }

        private void LoadCustomColorsFromSettings()
        {
            var savedHexColors = SettingsManager.Instance.Current.CustomColors;
            _customColors.Clear();
            if (savedHexColors != null)
            {
                foreach (var hex in savedHexColors)
                {
                    try
                    {
                        var color = (Color)ColorConverter.ConvertFromString(hex);
                        _customColors.Add(color);
                    }
                    catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
                }
            }
            RenderCustomColors();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (_isCompact && _snapManager != null)
                {
                    _snapManager.BeginDrag(_snapManager.GetCursorPosDIU());
                    this.CaptureMouse();
                }
                else
                {
                    this.DragMove();
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isCompact && _snapManager != null && _snapManager.IsDragging)
            {
                _snapManager.UpdateDrag(_snapManager.GetCursorPosDIU());
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_isCompact && _snapManager != null && _snapManager.IsDragging)
            {
                _snapManager.EndDrag();
                this.ReleaseMouseCapture();
            }
        }

        private void Swatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Background is SolidColorBrush brush)
            {
                var c = brush.Color;
                _currentAlpha = c.A;
                SetColorFromRgb(c.R, c.G, c.B);
                UpdateUI();
            }
        }

        private void UpdateHueColorVisual()
        {
            var hueColor = ColorFromHsv(_currentHue, 1, 1);
            SpectrumBaseColor.Fill = new SolidColorBrush(hueColor);
            var pureColor = GetRgbFromHsv(false);
            if (AlphaGradientStop != null)
                AlphaGradientStop.Color = pureColor;
        }

        private void SetColorFromRgb(byte r, byte g, byte b)
        {
            var c = System.Drawing.Color.FromArgb(r, g, b);
            _currentHue = c.GetHue();
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            _currentVal = max / 255.0;
            if (max == 0) _currentSat = 0;
            else _currentSat = (max - min) / max;
            UpdateHueColorVisual();
        }

        private Color GetRgbFromHsv(bool includeAlpha = true)
        {
            var c = ColorFromHsv(_currentHue, _currentSat, _currentVal);
            return includeAlpha ? Color.FromArgb((byte)_currentAlpha, c.R, c.G, c.B) : c;
        }

        private void UpdateUI()
        {
            if (_isUpdatingInputs) return;
            _isUpdatingInputs = true;

            try
            {
                var c = GetRgbFromHsv(true);
                SelectedColor = c;

                if (NewColorRect != null) NewColorRect.Fill = new SolidColorBrush(c);
                if (HexInput != null) HexInput.Text = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                if (SpectrumLayerGroup != null) SpectrumLayerGroup.Opacity = _currentAlpha / 255.0;

                if (_isCompact)
                {
                    if (BrightnessSlider != null) BrightnessSlider.Value = _currentVal * 100;
                    if (SaturationSlider != null) SaturationSlider.Value = _currentSat * 100;
                    UpdateWheelCursor();
                    UpdateCompactButtons();
                }

                if (Input1 != null && Input2 != null && Input3 != null && InputAlpha != null)
                {
                    InputAlpha.Text = ((int)_currentAlpha).ToString();
                    if (_currentMode == ColorMode.RGB)
                    {
                        Input1.Text = c.R.ToString();
                        Input2.Text = c.G.ToString();
                        Input3.Text = c.B.ToString();
                    }
                    else
                    {
                        Input1.Text = Math.Round(_currentHue).ToString();
                        Input2.Text = Math.Round(_currentSat * 100).ToString();
                        Input3.Text = Math.Round(_currentVal * 100).ToString();
                    }
                }

                if (HueSliderGrid != null && HueSliderGrid.ActualHeight > 0 && HueCursor != null)
                {
                    double h = HueSliderGrid.ActualHeight;
                    double hueY = (1 - (_currentHue / 360.0)) * h;
                    Canvas.SetTop(HueCursor, Math.Clamp(hueY, 0, h));
                }

                if (SpectrumBaseColor != null && SpectrumBaseColor.ActualWidth > 0 && CursorContainer != null)
                {
                    double w = SpectrumBaseColor.ActualWidth;
                    double h = SpectrumBaseColor.ActualHeight;
                    Canvas.SetLeft(CursorContainer, _currentSat * w);
                    Canvas.SetTop(CursorContainer, (1 - _currentVal) * h);
                }

                if (AlphaSliderGrid != null && AlphaSliderGrid.ActualHeight > 0 && AlphaCursor != null)
                {
                    double h = AlphaSliderGrid.ActualHeight;
                    double alphaY = (1 - (_currentAlpha / 255.0)) * h;
                    Canvas.SetTop(AlphaCursor, Math.Clamp(alphaY, 0, h));
                    UpdateHueColorVisual();
                }

                if (this.Owner is MainWindow mw)
                {
                    mw.UpdateCurrentColor(SelectedColor, IsSecondaryColor);
                }
                else if (this.Owner is WatermarkWindow ww)
                {
                    ww.UpdateColorFromPicker(SelectedColor);
                }
            }
            finally { _isUpdatingInputs = false; }
        }

        private void RenderHueGradient()
        {
            int w = 20;
            int h = 360;
            var bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            int[] pixels = new int[w * h];
            for (int y = 0; y < h; y++)
            {
                double hue = 360 - (y * 360.0 / h);
                var color = ColorFromHsv(hue, 1, 1);
                int colorData = (255 << 24) | (color.R << 16) | (color.G << 8) | color.B;
                for (int x = 0; x < w; x++) pixels[y * w + x] = colorData;
            }
            bitmap.WritePixels(new Int32Rect(0, 0, w, h), pixels, w * 4, 0);
            if (HueSliderImage != null) HueSliderImage.Source = bitmap;
        }

        private void RenderColorWheel()
        {
            int size = 200;
            var bitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
            int[] pixels = new int[size * size];
            double radius = size / 2.0;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    double dx = x - radius;
                    double dy = y - radius;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist <= radius)
                    {
                        double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
                        if (angle < 0) angle += 360;
                        double hue = angle;
                        double sat = dist / radius;
                        var color = ColorFromHsv(hue, sat, 1);
                        pixels[y * size + x] = (255 << 24) | (color.R << 16) | (color.G << 8) | color.B;
                    }
                    else pixels[y * size + x] = 0;
                }
            }
            bitmap.WritePixels(new Int32Rect(0, 0, size, size), pixels, size * 4, 0);
            if (WheelImageBrush != null) WheelImageBrush.ImageSource = bitmap;
        }

        private static Color ColorFromHsv(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);
            value = value * 255;
            byte v = Convert.ToByte(value);
            byte p = Convert.ToByte(value * (1 - saturation));
            byte q = Convert.ToByte(value * (1 - f * saturation));
            byte t = Convert.ToByte(value * (1 - (1 - f) * saturation));
            if (hi == 0) return Color.FromRgb(v, t, p);
            else if (hi == 1) return Color.FromRgb(q, v, p);
            else if (hi == 2) return Color.FromRgb(p, v, t);
            else if (hi == 3) return Color.FromRgb(p, q, v);
            else if (hi == 4) return Color.FromRgb(t, p, v);
            else return Color.FromRgb(v, p, q);
        }

        private void Spectrum_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Keyboard.ClearFocus();
            e.Handled = true;
            _isDraggingSpectrum = true;
            if (sender is Grid grid)
            {
                grid.CaptureMouse();
                UpdateSpectrumFromMouse(e.GetPosition(grid), grid.ActualWidth, grid.ActualHeight);
            }
        }

        private void Spectrum_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingSpectrum && sender is Grid grid)
                UpdateSpectrumFromMouse(e.GetPosition(grid), grid.ActualWidth, grid.ActualHeight);
        }

        private void Spectrum_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSpectrum = false;
            if (sender is IInputElement el) el.ReleaseMouseCapture();
            if (ColorToolTipBorder != null) ColorToolTipBorder.Visibility = Visibility.Collapsed;
        }

        private void UpdateSpectrumFromMouse(Point p, double w, double h)
        {
            double x = Math.Clamp(p.X, 0, w);
            double y = Math.Clamp(p.Y, 0, h);
            _currentSat = x / w;
            _currentVal = 1 - (y / h);
            UpdateUI();
            if (ColorToolTipBorder != null)
            {
                var colorAtMouse = ColorFromHsv(_currentHue, _currentSat, _currentVal);
                if (ColorToolTipText != null) ColorToolTipText.Text = GetFriendlyColorName(colorAtMouse);
                ColorToolTipBorder.Visibility = Visibility.Visible;
            }
        }

        private void Hue_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Keyboard.ClearFocus();
            e.Handled = true;
            _isDraggingHue = true;
            if (sender is Grid grid)
            {
                grid.CaptureMouse();
                UpdateHueFromMouse(e.GetPosition(grid), grid.ActualHeight);
            }
        }

        private void UpdateHueFromMouse(Point p, double h)
        {
            if (h <= 0) return;
            double y = Math.Clamp(p.Y, 0, h);
            _currentHue = 360 - (y / h * 360);
            _currentHue = Math.Clamp(_currentHue, 0, 360);
            UpdateHueColorVisual();
            UpdateUI();
        }

        private void Hue_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingHue && sender is Grid grid)
                UpdateHueFromMouse(e.GetPosition(grid), grid.ActualHeight);
        }

        private void Hue_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingHue = false;
            if (sender is Grid grid) grid.ReleaseMouseCapture();
        }

        private void Wheel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingWheel = true;
            if (WheelContainer != null)
            {
                WheelContainer.CaptureMouse();
                UpdateWheelFromMouse(e.GetPosition(WheelContainer));
            }
        }

        private void Wheel_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingWheel && WheelContainer != null)
                UpdateWheelFromMouse(e.GetPosition(WheelContainer));
        }

        private void Wheel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingWheel = false;
            if (WheelContainer != null) WheelContainer.ReleaseMouseCapture();
        }

        private void UpdateWheelFromMouse(Point p)
        {
            if (WheelContainer == null) return;
            double radius = WheelContainer.ActualWidth / 2.0;
            double dx = p.X - radius;
            double dy = p.Y - radius;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
            if (angle < 0) angle += 360;
            _currentHue = angle;
            _currentSat = Math.Clamp(dist / radius, 0, 1);
            UpdateUI();
        }

        private void UpdateWheelCursor()
        {
            if (WheelContainer == null || WheelCursor == null) return;
            double radius = WheelContainer.ActualWidth / 2.0;
            double angleRad = _currentHue * Math.PI / 180.0;
            double x = radius + Math.Cos(angleRad) * _currentSat * radius;
            double y = radius + Math.Sin(angleRad) * _currentSat * radius;
            Canvas.SetLeft(WheelCursor, x);
            Canvas.SetTop(WheelCursor, y);
        }

        private void CompactSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingInputs) return;
            if (BrightnessSlider == null || SaturationSlider == null) return;
            _currentVal = BrightnessSlider.Value / 100.0;
            _currentSat = SaturationSlider.Value / 100.0;
            UpdateUI();
        }

        private void UpdateCompactButtons()
        {
            if (CompactPrimaryBtn == null || CompactSecondaryBtn == null) return;
            if (this.Owner is MainWindow mw)
            {
                CompactPrimaryBtn.Background = new SolidColorBrush(mw.ForegroundColor);
                CompactSecondaryBtn.Background = new SolidColorBrush(mw.BackgroundColor);
            }
        }

        private void CompactPrimaryBtn_Click(object sender, RoutedEventArgs e)
        {
            if (this.Owner is MainWindow mw)
            {
                var c = mw.ForegroundColor;
                SetColorFromRgb(c.R, c.G, c.B);
                _currentAlpha = c.A;
                UpdateUI();
            }
        }

        private void CompactSecondaryBtn_Click(object sender, RoutedEventArgs e)
        {
            if (this.Owner is MainWindow mw)
            {
                var c = mw.BackgroundColor;
                SetColorFromRgb(c.R, c.G, c.B);
                _currentAlpha = c.A;
                UpdateUI();
            }
        }

        private void Alpha_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            _isDraggingAlpha = true;
            if (AlphaSliderGrid != null)
            {
                AlphaSliderGrid.CaptureMouse();
                UpdateAlphaFromMouse(e.GetPosition(AlphaSliderGrid), AlphaSliderGrid.ActualHeight);
            }
        }

        private void Alpha_Thumb_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Keyboard.ClearFocus();
            e.Handled = true;
            _isDraggingAlpha = true;
            if (AlphaSliderGrid != null)
            {
                AlphaSliderGrid.CaptureMouse();
                UpdateAlphaFromMouse(e.GetPosition(AlphaSliderGrid), AlphaSliderGrid.ActualHeight);
            }
        }

        private void Hue_Thumb_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Keyboard.ClearFocus();
            e.Handled = true;
            _isDraggingHue = true;
            if (HueSliderGrid != null)
            {
                HueSliderGrid.CaptureMouse();
                UpdateHueFromMouse(e.GetPosition(HueSliderGrid), HueSliderGrid.ActualHeight);
            }
        }

        private void Alpha_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingAlpha && sender is Grid grid)
                UpdateAlphaFromMouse(e.GetPosition(grid), grid.ActualHeight);
        }

        private void Alpha_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingAlpha = false;
            if (sender is Grid grid) grid.ReleaseMouseCapture();
        }

        private void UpdateAlphaFromMouse(Point p, double h)
        {
            if (h <= 0) return;
            double y = Math.Clamp(p.Y, 0, h);
            _currentAlpha = 255 - (y / h * 255);
            _currentAlpha = Math.Clamp(_currentAlpha, 0, 255);
            UpdateUI();
        }

        private void HexInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (HexInput == null || NewColorRect == null || _isUpdatingInputs) return;
            string hex = HexInput.Text.Trim('#');
            if (hex.Length == 6)
            {
                try
                {
                    _currentAlpha = 255;
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    SetColorFromRgb(r, g, b);
                    var c = GetRgbFromHsv(true);
                    NewColorRect.Fill = new SolidColorBrush(c);
                    SelectedColor = c;
                    if (InputAlpha != null) InputAlpha.Text = "255";
                }
                catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
            }
            else if (hex.Length == 8)
            {
                try
                {
                    byte a = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte r = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(4, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(6, 2), 16);
                    _currentAlpha = a;
                    SetColorFromRgb(r, g, b);
                    var c = GetRgbFromHsv(true);
                    NewColorRect.Fill = new SolidColorBrush(c);
                    SelectedColor = c;
                    if (InputAlpha != null) InputAlpha.Text = a.ToString();
                }
                catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
            }
        }

        private void NumericInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingInputs || Input1 == null || Input2 == null || Input3 == null || InputAlpha == null) return;
            if (double.TryParse(Input1.Text, out double v1) &&
                double.TryParse(Input2.Text, out double v2) &&
                double.TryParse(Input3.Text, out double v3) &&
                double.TryParse(InputAlpha.Text, out double vAlpha))
            {
                _currentAlpha = Math.Clamp(vAlpha, 0, 255);
                if (_currentMode == ColorMode.RGB)
                {
                    byte r = (byte)Math.Clamp(v1, 0, 255);
                    byte g = (byte)Math.Clamp(v2, 0, 255);
                    byte b = (byte)Math.Clamp(v3, 0, 255);
                    SetColorFromRgb(r, g, b);
                }
                else
                {
                    _currentHue = Math.Clamp(v1, 0, 360);
                    _currentSat = Math.Clamp(v2, 0, 100) / 100.0;
                    _currentVal = Math.Clamp(v3, 0, 100) / 100.0;
                    UpdateHueColorVisual();
                }
                var c = GetRgbFromHsv(true);
                SelectedColor = c;
                if (NewColorRect != null) NewColorRect.Fill = new SolidColorBrush(c);
                _isUpdatingInputs = true;
                if (HexInput != null) HexInput.Text = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                if (AlphaSliderGrid != null && AlphaSliderGrid.ActualHeight > 0 && AlphaCursor != null)
                    Canvas.SetTop(AlphaCursor, (1 - (_currentAlpha / 255.0)) * AlphaSliderGrid.ActualHeight);
                if (SpectrumBaseColor != null && SpectrumBaseColor.ActualWidth > 0 && CursorContainer != null)
                {
                    double w = SpectrumBaseColor.ActualWidth;
                    double h = SpectrumBaseColor.ActualHeight;
                    Canvas.SetLeft(CursorContainer, _currentSat * w);
                    Canvas.SetTop(CursorContainer, (1 - _currentVal) * h);
                }
                if (HueSliderGrid != null && HueSliderGrid.ActualHeight > 0 && HueCursor != null)
                {
                    double hGrid = HueSliderGrid.ActualHeight;
                    Canvas.SetTop(HueCursor, (1 - (_currentHue / 360.0)) * hGrid);
                }
                _isUpdatingInputs = false;
            }
        }

        private void ColorModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Label1 == null) return;
            if (ColorModeCombo.SelectedIndex == 0)
            {
                _currentMode = ColorMode.RGB;
                Label1.Text = LocalizationManager.GetString("L_ColorPicker_Red");
                Label2.Text = LocalizationManager.GetString("L_ColorPicker_Green");
                Label3.Text = LocalizationManager.GetString("L_ColorPicker_Blue");
            }
            else
            {
                _currentMode = ColorMode.HSV;
                Label1.Text = LocalizationManager.GetString("L_ColorPicker_Hue");
                Label2.Text = LocalizationManager.GetString("L_ColorPicker_Saturation");
                Label3.Text = LocalizationManager.GetString("L_ColorPicker_Brightness");
            }
            UpdateUI();
        }

        private List<Color> _customColors = new List<Color>();

        private void AddCustomColor_Click(object sender, RoutedEventArgs e)
        {
            var newColor = SelectedColor;
            if (_customColors.Contains(newColor)) return;
            _customColors.Insert(0, newColor);
            if (_customColors.Count > 24) _customColors.RemoveAt(24);
            RenderCustomColors();
            SaveCustomColorsToSettings();
        }

        private void SaveCustomColorsToSettings()
        {
            var hexList = new List<string>();
            foreach (var color in _customColors) hexList.Add(color.ToString());
            SettingsManager.Instance.Current.CustomColors = hexList;
            SettingsManager.Instance.Save();
        }

        private void RenderCustomColors()
        {
            if (CustomColorsGrid == null) return;
            for (int i = 0; i < CustomColorsGrid.Children.Count; i++)
            {
                if (CustomColorsGrid.Children[i] is Grid slot)
                {
                    slot.Children.Clear();
                    if (i < _customColors.Count)
                    {
                        var c = _customColors[i];
                        var btn = new Button
                        {
                            Style = (Style)Resources["MiniColorSwatch"],
                            Background = new SolidColorBrush(c),
                            ToolTip = GetFriendlyColorName(c)
                        };
                        btn.Click += (s, e2) => { _currentAlpha = c.A; SetColorFromRgb(c.R, c.G, c.B); UpdateUI(); };

                        slot.Children.Add(btn);
                    }
                    else
                    {
                        slot.Children.Add(new System.Windows.Shapes.Ellipse
                        {
                            Stroke = (Brush)Application.Current.FindResource("TextSecondaryBrush"),
                            StrokeThickness = 1,
                            StrokeDashArray = new DoubleCollection { 3, 2 },
                            Opacity = 0.5
                        });
                    }
                }
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.Owner is WatermarkWindow ww)
            {
                ww.UpdateColorFromPicker(SelectedColor);
            }
            this.SetDialogResultSafe(true);
            Close();
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e) { this.SetDialogResultSafe(false); Close(); }
    }
}
