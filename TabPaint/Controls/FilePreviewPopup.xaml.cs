using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using XamlAnimatedGif;
using TabPaint.Services;

namespace TabPaint.Controls
{
    public partial class FilePreviewPopup : UserControl
    {
        private static readonly ThumbnailCache _highResPreviewCache = new ThumbnailCache(5);
        private Brush _checkerboardBrush;
        private DispatcherTimer _hoverTimer;
        private DispatcherTimer _closeTimer;
        private DispatcherTimer _highResTimer;
        private CancellationTokenSource _previewCts;
        private FrameworkElement _currentHoveredElement;
        private string _currentFilePath;

        public FilePreviewPopup()
        {
            InitializeComponent();

            _hoverTimer = new DispatcherTimer();
            _hoverTimer.Interval = TimeSpan.FromSeconds(0.2);
            _hoverTimer.Tick += HoverTimer_Tick;

            _closeTimer = new DispatcherTimer();
            _closeTimer.Interval = TimeSpan.FromMilliseconds(100);
            _closeTimer.Tick += CloseTimer_Tick;

            _highResTimer = new DispatcherTimer();
            _highResTimer.Interval = TimeSpan.FromSeconds(0.05);
            _highResTimer.Tick += HighResTimer_Tick;
        }

        public void ShowPreview(string filePath, FrameworkElement target, PlacementMode placement = PlacementMode.Right)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            if (_currentFilePath == filePath && LargePreviewPopup.IsOpen && _currentHoveredElement == target)
            {
                _closeTimer.Stop();
                return;
            }

            _currentFilePath = filePath;
            _currentHoveredElement = target;
            LargePreviewPopup.PlacementTarget = target;
            LargePreviewPopup.Placement = placement;

            _closeTimer.Stop();
            if (LargePreviewPopup.IsOpen)
            {
                _hoverTimer.Stop();
                UpdatePreviewPopup();
            }
            else
            {
                _hoverTimer.Stop();
                _hoverTimer.Start();
            }
        }

        public void ClosePreview()
        {
            _hoverTimer.Stop();
            _closeTimer.Start();
        }

        public void ClosePreviewImmediately()
        {
            _hoverTimer.Stop();
            _closeTimer.Stop();
            ClosePopupAndReset();
        }

        private void CloseTimer_Tick(object sender, EventArgs e)
        {
            _closeTimer.Stop();
            if (_currentHoveredElement != null)
            {
                try
                {
                    Point mousePos = Mouse.GetPosition(_currentHoveredElement);
                    bool isStillOver = mousePos.X >= 0 &&
                                       mousePos.X <= _currentHoveredElement.ActualWidth &&
                                       mousePos.Y >= 0 &&
                                       mousePos.Y <= _currentHoveredElement.ActualHeight;

                    if (isStillOver) return;
                }
                catch { }
            }
            ClosePopupAndReset();
        }

        private void HoverTimer_Tick(object sender, EventArgs e)
        {
            _hoverTimer.Stop();
            UpdatePreviewPopup();
        }

        private void UpdatePreviewPopup()
        {
            if (string.IsNullOrEmpty(_currentFilePath)) return;

            _highResTimer.Stop();
            _previewCts?.Cancel();
            AnimationBehavior.SetSourceUri(PopupPreviewImage, null);
            PopupPreviewImage.Source = null;
            PopupPreviewImageBase.Source = null;
            CheckerboardBorder.Background = Brushes.Transparent;

            PopupFileSizeText.Text = "";

            if (File.Exists(_currentFilePath))
            {
                PopupDimensionsText.Text = Application.Current.TryFindResource("L_Loading") as string ?? "Loading...";

                try
                {
                    var fi = new FileInfo(_currentFilePath);
                    PopupFileSizeText.Text = FormatFileSize(fi.Length);

                    var dims = GetImageDimensionsFast(_currentFilePath);
                    if (dims.Width > 0)
                    {
                        PopupDimensionsText.Text = $"{dims.Width} × {dims.Height} px";
                    }
                }
                catch { }

                var cached = _highResPreviewCache.Get(_currentFilePath);
                if (cached != null)
                {
                    PopupPreviewImage.Source = cached;
                    UpdateCheckerboardVisibility(cached);
                }
                else
                {
                    _highResTimer.Start();
                }
            }
            else
            {
                PopupDimensionsText.Text = Application.Current.TryFindResource("L_Toast_FileNotFound") as string ?? "File not found";
            }

            if (!LargePreviewPopup.IsOpen) LargePreviewPopup.IsOpen = true;
            
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var method = typeof(System.Windows.Controls.Primitives.Popup).GetMethod("Reposition",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    method?.Invoke(LargePreviewPopup, null);
                }
                catch { }
            }), DispatcherPriority.Render);
        }

        private async void HighResTimer_Tick(object sender, EventArgs e)
        {
            _highResTimer.Stop();
            if (string.IsNullOrEmpty(_currentFilePath) || !File.Exists(_currentFilePath)) return;

            _previewCts?.Cancel();
            if (_currentFilePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            {
                AnimationBehavior.AddLoadedHandler(PopupPreviewImage, OnGifLoaded);
                AnimationBehavior.SetSourceUri(PopupPreviewImage, new Uri(_currentFilePath));
                CheckerboardBorder.Background = GetCheckerboardBrush();
                return;
            }

            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;

            try
            {
                var result = await Task.Run(() => LoadHighResPreviewInternal(_currentFilePath, token), token);
                if (token.IsCancellationRequested) return;

                if (result.Image != null)
                {
                    PopupPreviewImage.Source = result.Image;
                    PopupPreviewImageBase.Source = null;
                    UpdateCheckerboardVisibility(result.Image);
                    _highResPreviewCache.Add(_currentFilePath, result.Image);
                }
                if (result.Width > 0)
                {
                    PopupDimensionsText.Text = $"{result.Width} × {result.Height} px";
                }
            }
            catch { }
        }

        private void OnGifLoaded(object sender, RoutedEventArgs e)
        {
            PopupPreviewImageBase.Source = null;
            AnimationBehavior.RemoveLoadedHandler(PopupPreviewImage, OnGifLoaded);
        }

        private void ClosePopupAndReset()
        {
            LargePreviewPopup.IsOpen = false;
            _currentHoveredElement = null;
            _currentFilePath = null;
            _highResTimer.Stop();
            _previewCts?.Cancel();
            AnimationBehavior.RemoveLoadedHandler(PopupPreviewImage, OnGifLoaded);
            AnimationBehavior.SetSourceUri(PopupPreviewImage, null);
            PopupPreviewImage.Source = null;
            PopupPreviewImageBase.Source = null;
            CheckerboardBorder.Background = Brushes.Transparent;
        }

        private (int Width, int Height) GetImageDimensionsFast(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (MainWindow.IsWebpFileOrStream(filePath, fs))
                {
                    var webpSize = MainWindow.GetWebpDimensionsWithSkia(fs);
                    if (webpSize != null) return (webpSize.Value.Width, webpSize.Value.Height);
                }

                var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                if (decoder.Frames.Count > 0)
                {
                    int bestIndex = MainWindow.GetLargestFrameIndex(decoder);
                    var frame = decoder.Frames[bestIndex];
                    return (frame.PixelWidth, frame.PixelHeight);
                }
            }
            catch { }
            return (0, 0);
        }

        private struct PreviewResult
        {
            public BitmapSource Image;
            public int Width;
            public int Height;
        }

        private PreviewResult LoadHighResPreviewInternal(string filePath, CancellationToken token)
        {
            var res = new PreviewResult();
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (MainWindow.IsWebpFileOrStream(filePath, fs))
                    {
                        var dims = MainWindow.GetWebpDimensionsWithSkia(fs);
                        if (dims != null)
                        {
                            res.Width = dims.Value.Width;
                            res.Height = dims.Value.Height;
                            res.Image = MainWindow.DecodeWebpWithSkia(fs, targetMaxWidth: 300);
                            return res;
                        }
                    }

                    var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                    if (decoder.Frames.Count > 0)
                    {
                        int bestIndex = MainWindow.GetLargestFrameIndex(decoder);
                        var frame = decoder.Frames[bestIndex];
                        res.Width = frame.PixelWidth;
                        res.Height = frame.PixelHeight;

                        if (token.IsCancellationRequested) return res;
                        
                        double scale = 300.0 / frame.PixelWidth;
                        if (scale > 1.0) scale = 1.0;

                        if (decoder.Frames.Count > 1 || scale < 1.0)
                        {
                            var transformed = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
                            var resultBmp = new WriteableBitmap(transformed);
                            resultBmp.Freeze();
                            res.Image = resultBmp;
                        }
                        else
                        {
                            fs.Position = 0;
                            var img = new BitmapImage();
                            img.BeginInit();
                            img.CacheOption = BitmapCacheOption.OnLoad;
                            img.StreamSource = fs;
                            img.DecodePixelWidth = 300;
                            img.EndInit();
                            img.Freeze();
                            res.Image = img;
                        }
                    }
                }
            }
            catch { }
            return res;
        }

        private void UpdateCheckerboardVisibility(BitmapSource source)
        {
            if (source == null)
            {
                CheckerboardBorder.Background = Brushes.Transparent;
                return;
            }

            bool hasAlpha = false;
            try
            {
                var format = source.Format;
                hasAlpha = format == PixelFormats.Bgra32 || format == PixelFormats.Pbgra32 || 
                           format == PixelFormats.Rgba64 || format == PixelFormats.Rgba128Float ||
                           format == PixelFormats.Prgba64 || format == PixelFormats.Prgba128Float ||
                           (format.Masks.Count >= 4);
            }
            catch { }
            CheckerboardBorder.Background = hasAlpha ? GetCheckerboardBrush() : Brushes.Transparent;
        }

        private Brush GetCheckerboardBrush()
        {
            if (_checkerboardBrush != null) return _checkerboardBrush;
            var lightBrush = (Brush)FindResource("CheckerboardLightBrush");
            var darkBrush = (Brush)FindResource("CheckerboardDarkBrush");
            var drawing = new DrawingGroup();
            drawing.Children.Add(new GeometryDrawing(lightBrush, null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
            var darkGeometry = new GeometryGroup();
            darkGeometry.Children.Add(new RectangleGeometry(new Rect(0, 0, 8, 8)));
            darkGeometry.Children.Add(new RectangleGeometry(new Rect(8, 8, 8, 8)));
            drawing.Children.Add(new GeometryDrawing(darkBrush, null, darkGeometry));
            _checkerboardBrush = new DrawingBrush(drawing) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 16, 16), ViewportUnits = BrushMappingMode.Absolute };
            _checkerboardBrush.Freeze();
            return _checkerboardBrush;
        }

        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = (decimal)bytes;
            while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
            {
                number = number / 1024;
                counter++;
            }
            return string.Format("{0:n1} {1}", number, suffixes[counter]);
        }
    }
}
