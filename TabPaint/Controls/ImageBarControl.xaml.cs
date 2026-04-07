//
//ImageBarControl.xaml.cs
//图片标签栏控件，负责显示已打开的图片缩略图、标签切换、关闭以及拖拽排序等交互。
//
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop; // 用于 HwndSource
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using XamlAnimatedGif;
using TabPaint;
using static TabPaint.MainWindow;

namespace TabPaint.Controls
{
   
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)  return b ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v) return v != Visibility.Visible;
            return false;
        }
    }
    public partial class ImageBarControl : UserControl
    {
        private static readonly ThumbnailCache _highResPreviewCache = new ThumbnailCache(5);
        private Brush _checkerboardBrush;
        private DispatcherTimer _closeTimer;
        private Window _hostWindow;
        private const int WM_MOUSEHWHEEL = AppConsts.WM_MOUSEHWHEEL;
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

            _checkerboardBrush = new DrawingBrush(drawing)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 16, 16),
                ViewportUnits = BrushMappingMode.Absolute
            };
            _checkerboardBrush.Freeze();
            return _checkerboardBrush;
        }

        public ImageBarControl()
        {
            InitializeComponent();
            this.Loaded += ImageBarControl_Loaded;
            this.Unloaded += ImageBarControl_Unloaded;

            _hoverTimer = new DispatcherTimer();
            _hoverTimer.Interval = TimeSpan.FromSeconds(0.2); // 0.2s 后显示基础预览
            _hoverTimer.Tick += HoverTimer_Tick;
            _closeTimer = new DispatcherTimer();
            _closeTimer.Interval = TimeSpan.FromMilliseconds(100); // 100ms 缓冲期
            _closeTimer.Tick += CloseTimer_Tick;

            _highResTimer = new DispatcherTimer();
            _highResTimer.Interval = TimeSpan.FromSeconds(0.05); 
            _highResTimer.Tick += HighResTimer_Tick;
        }
        private void Internal_OnTabMouseEnter(object sender, MouseEventArgs e)
        {
            if (!IsCompactMode) return;

            var element = sender as FrameworkElement;
            if (element == null) return;
            var tabData = element.DataContext as FileTabItem;
            if (tabData != null && tabData.IsSelected)
            {
                ClosePopupAndReset();
                return;
            }
            if (_currentHoveredElement != element)
            {
                _highResTimer.Stop();
                _previewCts?.Cancel();
            }

            _currentHoveredElement = element;

            _closeTimer.Stop();
            if (LargePreviewPopup.IsOpen)
            {
                _hoverTimer.Stop();
                UpdatePreviewPopup(); // 立即更新内容
            }
            else
            {
                _hoverTimer.Stop();
                _hoverTimer.Start();
            }
        }
        private void Internal_OnTabMouseLeave(object sender, MouseEventArgs e)
        {
            _hoverTimer.Stop(); // 还没显示的就别显示了
            _closeTimer.Start(); // 准备关闭
        }
        private void CloseTimer_Tick(object sender, EventArgs e)
        {
            _closeTimer.Stop();

            if (_currentHoveredElement == null)
            {
                ClosePopupAndReset();
                return;
            }
            Point mousePos = Mouse.GetPosition(_currentHoveredElement);
            bool isStillOver = mousePos.X >= 0 &&
                               mousePos.X <= _currentHoveredElement.ActualWidth &&
                               mousePos.Y >= 0 &&
                               mousePos.Y <= _currentHoveredElement.ActualHeight;

            if (isStillOver)
            {
                if (!LargePreviewPopup.IsOpen) LargePreviewPopup.IsOpen = true;
                return;
            }
            ClosePopupAndReset();
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
                hasAlpha = format == PixelFormats.Bgra32
                        || format == PixelFormats.Pbgra32
                        || format == PixelFormats.Rgba64
                        || format == PixelFormats.Rgba128Float
                        || format == PixelFormats.Prgba64
                        || format == PixelFormats.Prgba128Float
                        || (format.Masks.Count >= 4);
            }
            catch { hasAlpha = false;}
            CheckerboardBorder.Background = hasAlpha ? GetCheckerboardBrush() : Brushes.Transparent;
        }

        public void ClosePopupAndReset()
        {
            LargePreviewPopup.IsOpen = false;
            _currentHoveredElement = null;
            _highResTimer.Stop();
            _previewCts?.Cancel();
            AnimationBehavior.RemoveLoadedHandler(PopupPreviewImage, OnGifLoaded); // ★ 防止残留回调
            AnimationBehavior.SetSourceUri(PopupPreviewImage, null);
            PopupPreviewImage.Source = null;
            PopupPreviewImageBase.Source = null;  // ★ 清理底层
            CheckerboardBorder.Background = Brushes.Transparent;
        }
        private void HoverTimer_Tick(object sender, EventArgs e)
        {
            _hoverTimer.Stop();
            if (!IsCompactMode || _currentHoveredElement == null) return;

            // 开启前也要把关闭计时器停掉，防止意外
            _closeTimer.Stop();
            UpdatePreviewPopup();
        }
        private void UpdatePreviewPopup()
        {
            if (_currentHoveredElement == null) return;

            var tabData = _currentHoveredElement.DataContext as FileTabItem;
            if (tabData != null)
            {
                AnimationBehavior.SetSourceUri(PopupPreviewImage, null);
                PopupPreviewImage.Source = null;

                // 1. 缩略图同时设置到底层和顶层
                if (tabData.Thumbnail != null)
                {
                    PopupPreviewImageBase.Source = tabData.Thumbnail;  // ★ 底层保底
                    PopupPreviewImage.Source = tabData.Thumbnail;       // 顶层也显示
                    UpdateCheckerboardVisibility(tabData.Thumbnail);
                }
                else
                {
                    PopupPreviewImageBase.Source = null;
                    PopupPreviewImage.Source = null;
                    CheckerboardBorder.Background = Brushes.Transparent;
                }
                string filePath = tabData.FilePath;
                string backupPath = tabData.BackupPath;
                bool hasBackup = !string.IsNullOrEmpty(backupPath) && File.Exists(backupPath);
                string effectivePath = hasBackup ? backupPath : filePath;
                bool isNewFile = string.IsNullOrEmpty(filePath) || filePath.StartsWith("::TABPAINT_NEW::");
                bool dimsFound = false;

                // 新建未落盘图片：优先显示实际新建画布尺寸，避免回退到缩略图 100×60。
                if (!dimsFound && isNewFile)
                {
                    PopupDimensionsText.Text = $"{SettingsManager.Instance.Current.DefaultBlankCanvasWidth} × {SettingsManager.Instance.Current.DefaultBlankCanvasHeight} px";
                    dimsFound = true;
                }

                if (tabData.IsSelected)
                {
                    var mw = MainWindow.GetCurrentInstance();
                    if (mw?._surface?.Bitmap != null)
                    {
                        PopupDimensionsText.Text = $"{mw._surface.Bitmap.PixelWidth} × {mw._surface.Bitmap.PixelHeight} px";
                        dimsFound = true;
                    }
                }

                if (!dimsFound && !string.IsNullOrEmpty(effectivePath) && File.Exists(effectivePath))
                {
                    var dims = GetImageDimensionsFast(effectivePath);
                    if (dims.Width > 0)
                    {
                        PopupDimensionsText.Text = $"{dims.Width} × {dims.Height} px";
                        dimsFound = true;
                    }
                }

                if (!dimsFound)
                {
                    var thumb = tabData.Thumbnail;
                    if (thumb != null && thumb.PixelWidth > 0 && thumb.PixelWidth != 100)
                    {
                        PopupDimensionsText.Text = $"{thumb.PixelWidth} × {thumb.PixelHeight} px";
                        dimsFound = true;
                    }
                }
                if (hasBackup || (!isNewFile && File.Exists(filePath)))
                {
                    try
                    {
                        var fi = new FileInfo(effectivePath);
                        PopupFileSizeText.Text = FormatFileSize(fi.Length);
                    }
                    catch { PopupFileSizeText.Text = ""; }
                }
                else
                {
                    PopupFileSizeText.Text = "";
                    if (!dimsFound) PopupDimensionsText.Text = LocalizationManager.GetString("L_ImgBar_NewImage");
                }
                _highResTimer.Stop();
                if (!string.IsNullOrEmpty(effectivePath) && File.Exists(effectivePath))
                {
                    var cached = _highResPreviewCache.Get(effectivePath);
                    if (cached != null)
                    {
                        PopupPreviewImage.Source = cached;
                        PopupPreviewImageBase.Source = null;
                        UpdateCheckerboardVisibility(cached);
                    }
                    else _highResTimer.Start();
                }
                LargePreviewPopup.PlacementTarget = _currentHoveredElement;

                if (!LargePreviewPopup.IsOpen) LargePreviewPopup.IsOpen = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var method = typeof(Popup).GetMethod("Reposition",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        method?.Invoke(LargePreviewPopup, null);
                    }
                    catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
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

                var decoder = BitmapDecoder.Create(fs,
                    BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile,
                    BitmapCacheOption.None);

                if (decoder.Frames.Count > 0)
                {
                    int bestIndex = MainWindow.GetLargestFrameIndex(decoder); //多帧图像（ICO等）选择最大帧，与预览渲染逻辑一致
                    var frame = decoder.Frames[bestIndex];
                    return (frame.PixelWidth, frame.PixelHeight);
                }
            }
            catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
            return (0, 0);
        }


        private async void HighResTimer_Tick(object sender, EventArgs e)
        {
            _highResTimer.Stop();

            if (_currentHoveredElement == null) return;
            var tabData = _currentHoveredElement.DataContext as FileTabItem;
            if (tabData == null) return;

            string filePath = tabData.FilePath;
            string backupPath = tabData.BackupPath;
            bool hasBackup = !string.IsNullOrEmpty(backupPath) && File.Exists(backupPath);
            string effectivePath = hasBackup ? backupPath : filePath;

            if (string.IsNullOrEmpty(effectivePath) || !File.Exists(effectivePath)) return;

            _previewCts?.Cancel(); // GIF 特殊处理
            if (effectivePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            {
                AnimationBehavior.AddLoadedHandler(PopupPreviewImage, OnGifLoaded);
                AnimationBehavior.SetSourceUri(PopupPreviewImage, new Uri(effectivePath));
                CheckerboardBorder.Background = GetCheckerboardBrush();
                return;
            }
            AnimationBehavior.SetSourceUri(PopupPreviewImage, null);

            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;

            try
            {
                var result = await Task.Run(() => LoadHighResPreviewInternal(effectivePath, token), token);

                if (token.IsCancellationRequested) return;

                if (result.Image != null)
                {
                    PopupPreviewImage.Source = result.Image;
                    PopupPreviewImageBase.Source = null;  // ★ 高清图加载完成，清理底层
                    UpdateCheckerboardVisibility(result.Image);
                    _highResPreviewCache.Add(effectivePath, result.Image);
                }
                if (string.IsNullOrEmpty(PopupDimensionsText.Text) || PopupDimensionsText.Text == "")
                {
                    if (result.Width > 0 && result.Height > 0)
                    {
                        PopupDimensionsText.Text = $"{result.Width} × {result.Height} px";
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"High res preview failed: {ex.Message}");
            }
        }
        private void OnGifLoaded(object sender, RoutedEventArgs e)
        {
            PopupPreviewImageBase.Source = null;
            AnimationBehavior.RemoveLoadedHandler(PopupPreviewImage, OnGifLoaded);
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
                        if (decoder.Frames.Count > 1)
                        {
                            double scale = 300.0 / frame.PixelWidth;
                            if (scale > 1.0) scale = 1.0;

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
            catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
            return res;
        }
        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = (decimal)bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }
            return string.Format("{0:n1} {1}", number, suffixes[counter]);
        }
        private void ImageBarControl_Loaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                if (!ReferenceEquals(_hostWindow, window))
                {
                    if (_hostWindow != null) _hostWindow.Deactivated -= HostWindow_Deactivated;
                    _hostWindow = window;
                    _hostWindow.Deactivated += HostWindow_Deactivated;
                }

                var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
                source?.AddHook(WndProc);
            }
        }
        private void ImageBarControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_hostWindow != null)
            {
                _hostWindow.Deactivated -= HostWindow_Deactivated;
                _hostWindow = null;
            }

            var window = Window.GetWindow(this);
            if (window != null)
            {
                IntPtr handle = new WindowInteropHelper(window).Handle;
                if (handle != IntPtr.Zero)
                {
                    var source = HwndSource.FromHwnd(handle);
                    source?.RemoveHook(WndProc);
                }
            }
        }

        private void HostWindow_Deactivated(object sender, EventArgs e)
        {
            ClosePopupAndReset();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEHWHEEL && IsMouseOverControl(FileTabsScroller))
            {
                int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                FileTabsScroller.ScrollToHorizontalOffset(FileTabsScroller.HorizontalOffset + delta);

                handled = true; // 标记消息已处理
            }

            return IntPtr.Zero;
        }
        private bool IsMouseOverControl(UIElement control)
        {
            if (control == null || !control.IsVisible) return false;

            var mousePos = Mouse.GetPosition(control);
            var bounds = new Rect(0, 0, control.RenderSize.Width, control.RenderSize.Height);
            return bounds.Contains(mousePos);
        }
        public ScrollViewer Scroller => FileTabsScroller;
        public ItemsControl TabList => FileTabList;
        public Slider Slider => PreviewSlider;
        public Button AddButton => LeftAddBtn; 

        public static readonly RoutedEvent SaveAllClickEvent = EventManager.RegisterRoutedEvent("SaveAllClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler SaveAllClick { add { AddHandler(SaveAllClickEvent, value); } remove { RemoveHandler(SaveAllClickEvent, value); } }
        private void Internal_OnSaveAllClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SaveAllClickEvent, sender));
        public event RoutedEventHandler TabStickImageClick;
        private void Internal_OnTabStickImageClick(object sender, RoutedEventArgs e)
           => TabStickImageClick?.Invoke(sender, e);

        public static readonly RoutedEvent ClearUneditedClickEvent = EventManager.RegisterRoutedEvent("ClearUneditedClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler ClearUneditedClick { add { AddHandler(ClearUneditedClickEvent, value); } remove { RemoveHandler(ClearUneditedClickEvent, value); } }
        private void Internal_OnClearUneditedClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ClearUneditedClickEvent, sender));

        public static readonly RoutedEvent DiscardAllClickEvent = EventManager.RegisterRoutedEvent("DiscardAllClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler DiscardAllClick { add { AddHandler(DiscardAllClickEvent, value); } remove { RemoveHandler(DiscardAllClickEvent, value); } }
        private void Internal_OnDiscardAllClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(DiscardAllClickEvent, sender));

        public static readonly RoutedEvent PrependTabClickEvent = EventManager.RegisterRoutedEvent("PrependTabClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler PrependTabClick { add { AddHandler(PrependTabClickEvent, value); } remove { RemoveHandler(PrependTabClickEvent, value); } }
        private void Internal_OnPrependTabClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(PrependTabClickEvent, sender));

        public static readonly RoutedEvent NewTabClickEvent = EventManager.RegisterRoutedEvent("NewTabClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler NewTabClick { add { AddHandler(NewTabClickEvent, value); } remove { RemoveHandler(NewTabClickEvent, value); } }
        private void Internal_OnNewTabClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(NewTabClickEvent, sender));


        public static readonly RoutedEvent FileTabClickEvent = EventManager.RegisterRoutedEvent("FileTabClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler FileTabClick { add { AddHandler(FileTabClickEvent, value); } remove { RemoveHandler(FileTabClickEvent, value); } }
        private void Internal_OnFileTabClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(FileTabClickEvent, e.OriginalSource)); // 保持 OriginalSource

        public static readonly RoutedEvent FileTabCloseClickEvent = EventManager.RegisterRoutedEvent("FileTabCloseClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));
        public event RoutedEventHandler FileTabCloseClick { add { AddHandler(FileTabCloseClickEvent, value); } remove { RemoveHandler(FileTabCloseClickEvent, value); } }
        private void Internal_OnFileTabCloseClick(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(FileTabCloseClickEvent, e.OriginalSource));

        // 右键菜单转发
        public event RoutedEventHandler TabCopyClick;
        public event RoutedEventHandler TabCutClick;
        public event RoutedEventHandler TabPasteClick;
        public event RoutedEventHandler TabOpenFolderClick;
        public event RoutedEventHandler TabDeleteClick;
        public event RoutedEventHandler TabCloseOthersClick;
        public event RoutedEventHandler TabFileDeleteClick;

        private void Internal_OnTabCopyClick(object sender, RoutedEventArgs e) => TabCopyClick?.Invoke(sender, e);
        private void Internal_OnTabCutClick(object sender, RoutedEventArgs e) => TabCutClick?.Invoke(sender, e);
        private void Internal_OnTabPasteClick(object sender, RoutedEventArgs e) => TabPasteClick?.Invoke(sender, e);
        private void Internal_OnTabOpenFolderClick(object sender, RoutedEventArgs e) => TabOpenFolderClick?.Invoke(sender, e);
        private void Internal_OnTabDeleteClick(object sender, RoutedEventArgs e) => TabDeleteClick?.Invoke(sender, e);
        private void Internal_OnTabCloseOthersClick(object sender, RoutedEventArgs e) => TabCloseOthersClick?.Invoke(sender, e);
        private void Internal_OnTabFileDeleteClick(object sender, RoutedEventArgs e) => TabFileDeleteClick?.Invoke(sender, e);

        public event MouseButtonEventHandler FileTabPreviewMouseDown;
        private void Internal_OnFileTabPreviewMouseDown(object sender, MouseButtonEventArgs e) => FileTabPreviewMouseDown?.Invoke(sender, e);

        private void Internal_OnFileTabPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ClosePopupAndReset();
        }

        public event MouseEventHandler FileTabPreviewMouseMove;
        private void Internal_OnFileTabPreviewMouseMove(object sender, MouseEventArgs e) => FileTabPreviewMouseMove?.Invoke(sender, e);

        public event DragEventHandler FileTabDrop;
        private void Internal_OnFileTabDrop(object sender, DragEventArgs e) => FileTabDrop?.Invoke(sender, e);
        public event MouseWheelEventHandler FileTabsWheelScroll;
        public event DragEventHandler FileTabReorderDragOver;
        private void Internal_OnFileTabReorderDragOver(object sender, DragEventArgs e) => FileTabReorderDragOver?.Invoke(sender, e);
        private void Internal_OnFileTabsWheelScroll(object sender, MouseWheelEventArgs e)
        {
            var scroller = sender as ScrollViewer;
            if (scroller == null) return;
            if (e.Delta != 0)
            {
                scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset - e.Delta);
                e.Handled = true;
            }
        }

        public event ScrollChangedEventHandler FileTabsScrollChanged;
        private void Internal_OnFileTabsScrollChanged(object sender, ScrollChangedEventArgs e) => FileTabsScrollChanged?.Invoke(sender, e);

        public event RoutedPropertyChangedEventHandler<double> PreviewSliderValueChanged;
        private void Internal_PreviewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => PreviewSliderValueChanged?.Invoke(sender, e);

        public event MouseWheelEventHandler SliderPreviewMouseWheel;
        private void Internal_Slider_PreviewMouseWheel(object sender, MouseWheelEventArgs e) => SliderPreviewMouseWheel?.Invoke(sender, e);
        public static readonly RoutedEvent SaveAllDoubleClickEvent =
    EventManager.RegisterRoutedEvent("SaveAllDoubleClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageBarControl));

        public event RoutedEventHandler SaveAllDoubleClick
        {
            add { AddHandler(SaveAllDoubleClickEvent, value); }
            remove { RemoveHandler(SaveAllDoubleClickEvent, value); }
        }

        private void Internal_OnSaveAllDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            RaiseEvent(new RoutedEventArgs(SaveAllDoubleClickEvent, sender));
        }
        private void Internal_ScrollViewer_ManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }
        public FileTabItem GetTabFromPoint(Point pointRelativeToWindow)
        {
            Point pointInList = this.FileTabList.PointFromScreen(this.PointToScreen(new Point(0, 0)));
            Point mousePosInList = this.FileTabList.PointFromScreen(this.PointToScreen(pointRelativeToWindow));
            if (pointRelativeToWindow.Y > 220) return null;
            // 2. 遍历当前可见的 Tab 容器
            for (int i = 0; i < FileTabList.Items.Count; i++)
            {
                var container = FileTabList.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                if (container == null) continue;
                Point relativePos = container.TranslatePoint(new Point(0, 0), FileTabList);
                Rect bounds = new Rect(relativePos.X, relativePos.Y+110, container.ActualWidth, container.ActualHeight);
                if (bounds.Contains(mousePosInList))
                {
                    return FileTabList.Items[i] as FileTabItem;
                }
            }

            return null;
        }

        private DispatcherTimer _highResTimer; // 用于1秒后加载大图
        private CancellationTokenSource _previewCts; // 用于取消正在进行的加载任务

        public static readonly DependencyProperty IsSingleTabModeProperty =
    DependencyProperty.Register("IsSingleTabMode", typeof(bool), typeof(ImageBarControl), new PropertyMetadata(false));

        public bool IsSingleTabMode
        {
            get { return (bool)GetValue(IsSingleTabModeProperty); }
            set { SetValue(IsSingleTabModeProperty, value); }
        }
        public event DragEventHandler FileTabLeave;
           private void Internal_OnFileTabDragLeave(object sender, DragEventArgs e) => FileTabLeave?.Invoke(sender, e);

        public static readonly DependencyProperty IsViewModeProperty =
                   DependencyProperty.Register("IsViewMode", typeof(bool), typeof(ImageBarControl), new PropertyMetadata(false));

        public bool IsViewMode
        {
            get { return (bool)GetValue(IsViewModeProperty); }
            set { SetValue(IsViewModeProperty, value); }
        }
        public static readonly DependencyProperty IsPinnedProperty =
            DependencyProperty.Register("IsPinned", typeof(bool), typeof(ImageBarControl), new PropertyMetadata(false));

        public bool IsPinned
        {
            get { return (bool)GetValue(IsPinnedProperty); }
            set { SetValue(IsPinnedProperty, value); }
        }
        public void TogglePin()
        {
            IsPinned = !IsPinned;
        }
        public static readonly DependencyProperty IsCompactModeProperty =
         DependencyProperty.Register("IsCompactMode", typeof(bool), typeof(ImageBarControl), new PropertyMetadata(false, OnCompactModeChanged));

        public bool IsCompactMode
        {
            get { return (bool)GetValue(IsCompactModeProperty); }
            set { SetValue(IsCompactModeProperty, value); }
        }
        public double DesiredHeight
        {
            get { return (double)GetValue(DesiredHeightProperty); }
            set { SetValue(DesiredHeightProperty, value); }
        }

        public static readonly DependencyProperty DesiredHeightProperty =
            DependencyProperty.Register("DesiredHeight", typeof(double), typeof(ImageBarControl), new PropertyMetadata(100.0));


        private static void OnCompactModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = d as ImageBarControl;
            if (ctrl != null)
            {
                // 切换模式时调整容器的预期高度，以便动画正常工作
                ctrl.DesiredHeight = (bool)e.NewValue ? 45.0 : 100.0;
                ctrl.InvalidateVisual();
            }
        }
        private void Internal_OnToggleViewModeClick(object sender, RoutedEventArgs e)
        {
            IsCompactMode = !IsCompactMode;
        }
        private void Internal_OnBackgroundMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;

            var dep = e.OriginalSource as DependencyObject;
            if (dep == null) return;
            if (HasDataContext<FileTabItem>(dep)) return;
            if (FindAncestor<ButtonBase>(dep) != null) return;
            if (FindAncestor<Slider>(dep) != null) return;
            if (FindAncestor<Thumb>(dep) != null) return;
            if (FindAncestor<ScrollBar>(dep) != null) return;

            IsCompactMode = !IsCompactMode;
            e.Handled = true;
        }
        private DispatcherTimer _hoverTimer;
        private FrameworkElement _currentHoveredElement;
        private static bool HasDataContext<T>(DependencyObject d)
        {
            while (d != null)
            {
                if (d is FrameworkElement fe && fe.DataContext is T) return true;
                d = VisualTreeHelper.GetParent(d);
            }
            return false;
        }

        private static T? FindAncestor<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null)
            {
                if (d is T t) return t;
                d = VisualTreeHelper.GetParent(d);
            }
            return null;
        }
    }
}
