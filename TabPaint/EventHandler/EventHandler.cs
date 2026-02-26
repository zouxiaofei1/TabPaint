
//
//EventHandler.cs
//主窗口的事件处理部分，杂项。
//
using Microsoft.VisualBasic.FileIO;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Streaming.Adaptive;


namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private bool IsEditingTextField()
        {
            if (Keyboard.FocusedElement is System.Windows.Controls.TextBox ||
                Keyboard.FocusedElement is System.Windows.Controls.PasswordBox ||
                Keyboard.FocusedElement is System.Windows.Controls.RichTextBox)
            {
                return true;
            }
            return false;
        }
    



        private void EmptyClick(object sender, RoutedEventArgs e)
        {
            MainToolBar.RotateFlipMenuToggle.IsChecked = false;
            MainToolBar.BrushToggle.IsChecked = false;
        }

        private void InitializeClipboardMonitor()
        {

            var helper = new WindowInteropHelper(this);
            if (helper.Handle != IntPtr.Zero)
            {
                _hwndSource = HwndSource.FromHwnd(helper.Handle);
                _hwndSource.AddHook(WndProc);
                AddClipboardFormatListener(helper.Handle); // 默认注册监听，通过 bool 标志控制逻辑

                // 如果全局配置开启且当前没有活跃实例，则抢占为活跃实例
                if (SettingsManager.Instance.Current.EnableClipboardMonitor && _activeMonitorInstance == null)
                {
                    _activeMonitorInstance = this;
                }
            }
        }
        private void MainWindow_Deactivated(object sender, EventArgs e)
        {
            _router.CurrentTool?.StopAction(_ctx);
            MainImageBar?.ClosePopupAndReset();
        }


        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (_maximized)
                {
                    // 记录按下位置，准备看是否拖动
                    _dragStartPoint = e.GetPosition(this);
                    _draggingFromMaximized = true;
                    MouseMove += Border_MouseMoveFromMaximized;
                }
                else   DragMove(); // 普通拖动
            }
        }

        private void BeginViewModeWindowDrag()
        {
            StopViewModeWindowDrag();

            _isWindowViewDragging = true;
            _viewDragMouseStartScreen = GetMouseScreenDip();
            _viewDragWindowStart = new Point(Left, Top);

            CaptureMouse();
            MouseMove += Window_ViewDragMouseMove;
            MouseLeftButtonUp += Window_ViewDragMouseUp;
        }

        private Point GetMouseScreenDip()
        {
            GetCursorPos(out POINT p);
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget == null) return new Point(p.X, p.Y);
            return source.CompositionTarget.TransformFromDevice.Transform(new Point(p.X, p.Y));
        }

        private Rect GetCurrentMonitorWorkAreaDip()
        {
            GetCursorPos(out POINT p);
            var monitor = MonitorFromPoint(p, MONITOR_DEFAULTTONEAREST);

            MONITORINFO mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref mi))
            {
                return SystemParameters.WorkArea;
            }

            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget == null)
            {
                return new Rect(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Right - mi.rcWork.Left, mi.rcWork.Bottom - mi.rcWork.Top);
            }

            var transform = source.CompositionTarget.TransformFromDevice;
            var topLeft = transform.Transform(new Point(mi.rcWork.Left, mi.rcWork.Top));
            var bottomRight = transform.Transform(new Point(mi.rcWork.Right, mi.rcWork.Bottom));
            return new Rect(topLeft, bottomRight);
        }

        private void Window_ViewDragMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isWindowViewDragging || e.LeftButton != MouseButtonState.Pressed) return;

            Point currentScreen = GetMouseScreenDip();
            double targetLeft = _viewDragWindowStart.X + (currentScreen.X - _viewDragMouseStartScreen.X);
            double targetTop = _viewDragWindowStart.Y + (currentScreen.Y - _viewDragMouseStartScreen.Y);

            var workArea = GetCurrentMonitorWorkAreaDip();
            double visibleWidth = Math.Min(ViewModeDragMinVisibleWidth, ActualWidth);
            double visibleHeight = Math.Min(ViewModeDragMinVisibleHeight, ActualHeight);

            // 允许窗口部分出屏，但保留最小可见区域，防止完全丢失。
            double minLeft = workArea.Left - Math.Max(0, ActualWidth - visibleWidth);
            double maxLeft = workArea.Right - visibleWidth;
            double minTop = workArea.Top - Math.Max(0, ActualHeight - visibleHeight);
            double maxTop = workArea.Bottom - visibleHeight;

            Left = Math.Clamp(targetLeft, minLeft, maxLeft);
            Top = Math.Clamp(targetTop, minTop, maxTop);

            e.Handled = true;
        }

        private void Window_ViewDragMouseUp(object sender, MouseButtonEventArgs e)
        {
            StopViewModeWindowDrag();
            e.Handled = true;
        }

        private void StopViewModeWindowDrag()
        {
            if (!_isWindowViewDragging) return;
            _isWindowViewDragging = false;
            MouseMove -= Window_ViewDragMouseMove;
            MouseLeftButtonUp -= Window_ViewDragMouseUp;
            if (IsMouseCaptured) ReleaseMouseCapture();
        }

        private void Border_MouseMoveFromMaximized(object sender, System.Windows.Input.MouseEventArgs e)
        {

            if (_draggingFromMaximized && e.LeftButton == MouseButtonState.Pressed)
            {

                // 鼠标移动的阈值，比如 5px
                var currentPos = e.GetPosition(this);
                if (Math.Abs(currentPos.X - _dragStartPoint.X) > 5 ||
                    Math.Abs(currentPos.Y - _dragStartPoint.Y) > 5)
                {
                    // 超过阈值，恢复窗口大小，并开始拖动
                    _draggingFromMaximized = false;
                    MouseMove -= Border_MouseMoveFromMaximized;

                    _maximized = false;

                    var percentX = _dragStartPoint.X / ActualWidth;

                    Left = e.GetPosition(this).X - _restoreBounds.Width * percentX;
                    Top = e.GetPosition(this).Y;
                    Width = _restoreBounds.Width;
                    Height = _restoreBounds.Height;
                    SetMaximizeIcon();
                    DragMove();
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        // 状态变量
        private uint _lastClipboardSequenceNumber = 0;
        private DateTime _lastClipboardActionTime = DateTime.MinValue;
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == AppConsts.WM_MOUSEHWHEEL)
            {
                if (ScrollContainer != null && !_isZoomAnimating)
                {
                    short tilt = (short)((wParam.ToInt64() >> 16) & 0xFFFF);

                    if (tilt != 0)
                    {
                        double scrollAmount = tilt * AppConsts.WheelScrollFactor;
                        ScrollContainer.ScrollToHorizontalOffset(ScrollContainer.HorizontalOffset + scrollAmount);
                        handled = true;
                    }
                }
            }
            if (msg == AppConsts.WM_CLIPBOARDUPDATE)
            {
                if (SettingsManager.Instance.Current.EnableClipboardMonitor && _activeMonitorInstance == this)
                {
                    // 1. 获取当前剪切板的系统序列号
                    uint currentSeq = GetClipboardSequenceNumber();

                    if (currentSeq == _lastClipboardSequenceNumber)  return IntPtr.Zero;

                    var timeSinceLast = (DateTime.Now - _lastClipboardActionTime).TotalMilliseconds;
                    if (timeSinceLast < AppConsts.ClipboardCooldownMs)
                    {
                        _lastClipboardSequenceNumber = currentSeq;
                        return IntPtr.Zero;
                    }
                    _lastClipboardSequenceNumber = currentSeq;
                    _lastClipboardActionTime = DateTime.Now;
                    OnClipboardContentChanged();
                }
            }
            if (msg == AppConsts.WM_NCHITTEST)
            {
                if (_maximized)
                {
                    handled = true;
                    return (IntPtr)AppConsts.HTCLIENT; // HTCLIENT
                }

                var mousePos = PointFromScreen(new Point(
                    (short)(lParam.ToInt32() & 0xFFFF),
                    (short)((lParam.ToInt32() >> 16) & 0xFFFF)));

                double width = ActualWidth;
                double height = ActualHeight;

                int cornerArea = AppConsts.WindowCornerArea; // 角落区域大一点，方便对角线拖拽
                int sideArea = AppConsts.WindowSideArea;    // 侧边区域非常小，避让滚动条 (推荐4-6px)

                handled = true;

                if (mousePos.Y <= cornerArea && mousePos.X <= cornerArea) return (IntPtr)AppConsts.HTTOPLEFT;
                if (mousePos.Y <= cornerArea && mousePos.X >= width - cornerArea) return (IntPtr)AppConsts.HTTOPRIGHT;
                // 左下
                if (mousePos.Y >= height - cornerArea && mousePos.X <= cornerArea) return (IntPtr)AppConsts.HTBOTTOMLEFT;
                // 右下 (这是最常用的调整区域，保持大范围)
                if (mousePos.Y >= height - cornerArea && mousePos.X >= width - cornerArea) return (IntPtr)AppConsts.HTBOTTOMRIGHT;


                if (mousePos.Y <= sideArea) return (IntPtr)AppConsts.HTTOP;
                if (mousePos.Y >= height - sideArea) return (IntPtr)AppConsts.HTBOTTOM;

                if (mousePos.X <= sideArea) return (IntPtr)AppConsts.HTLEFT;
                if (mousePos.X >= width - sideArea) return (IntPtr)AppConsts.HTRIGHT;
                return (IntPtr)AppConsts.HTCLIENT; // HTCLIENT
            }

            return IntPtr.Zero;
        }
        private void ClipboardMonitorToggle_Click(object sender, RoutedEventArgs e)
        {
            // 配置会自动更新 (Two-Way Binding)
            if (SettingsManager.Instance.Current.EnableClipboardMonitor)
            {
                // 手动开启时，将当前窗口设为活跃监听实例
                _activeMonitorInstance = this;
                OnClipboardContentChanged();
            }
            else
            {
                // 如果当前窗口是活跃实例且被关闭了监听，清空引用
                if (_activeMonitorInstance == this)
                {
                    _activeMonitorInstance = null;
                    // 尝试寻找下一个合适的窗口接管（如果有窗口也开启了开关但之前被抑制了）
                    TryTransferClipboardMonitor();
                }
            }
        }

        private void TryTransferClipboardMonitor()
        {
            if (SettingsManager.Instance.Current.EnableClipboardMonitor)
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is MainWindow mw && mw != this && window.IsLoaded)
                    {
                        _activeMonitorInstance = mw;
                        break;
                    }
                }
            }
        }

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        private bool HasBitmapLikeData(IDataObject? dataObj)
        {
            if (dataObj == null) return false;
            return dataObj.GetDataPresent("PNG")
                   || dataObj.GetDataPresent("System.Drawing.Bitmap")
                   || dataObj.GetDataPresent("Bitmap")
                   || dataObj.GetDataPresent(DataFormats.Bitmap)
                   || dataObj.GetDataPresent(DataFormats.Dib)
                   || dataObj.GetDataPresent("DeviceIndependentBitmap");
        }

        private BitmapSource? DecodeBitmapFromPngPayload(object payload)
        {
            try
            {
                if (payload is byte[] pngBytes && pngBytes.Length > 0)
                {
                    using var ms = new MemoryStream(pngBytes);
                    var decoder = new PngBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    if (decoder.Frames.Count > 0) return decoder.Frames[0];
                }

                if (payload is Stream stream)
                {
                    if (stream.CanSeek)
                    {
                        stream.Position = 0;
                        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        if (decoder.Frames.Count > 0) return decoder.Frames[0];
                    }
                    else
                    {
                        using var copy = new MemoryStream();
                        stream.CopyTo(copy);
                        copy.Position = 0;
                        var decoder = new PngBitmapDecoder(copy, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        if (decoder.Frames.Count > 0) return decoder.Frames[0];
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        private BitmapSource? DecodeBitmapFromDibPayload(object payload)
        {
            try
            {
                byte[]? dibBytes = null;
                if (payload is byte[] raw && raw.Length > 0)
                {
                    dibBytes = raw;
                }
                else if (payload is MemoryStream ms)
                {
                    dibBytes = ms.ToArray();
                }
                else if (payload is Stream stream)
                {
                    using var copy = new MemoryStream();
                    if (stream.CanSeek) stream.Position = 0;
                    stream.CopyTo(copy);
                    dibBytes = copy.ToArray();
                }

                if (dibBytes == null || dibBytes.Length < 40) return null;

                int headerSize = BitConverter.ToInt32(dibBytes, 0);
                if (headerSize < 40 || headerSize > dibBytes.Length) return null;

                short bpp = BitConverter.ToInt16(dibBytes, 14);
                int colorsUsed = (headerSize >= 36) ? BitConverter.ToInt32(dibBytes, 32) : 0;
                int colorTableSize = 0;
                if (bpp > 0 && bpp <= 8)
                {
                    int paletteCount = colorsUsed > 0 ? colorsUsed : (1 << bpp);
                    colorTableSize = paletteCount * 4;
                }

                const int fileHeaderSize = 14;
                int pixelOffset = fileHeaderSize + headerSize + colorTableSize;
                if (pixelOffset < fileHeaderSize || pixelOffset > fileHeaderSize + dibBytes.Length)
                {
                    pixelOffset = fileHeaderSize + headerSize;
                }

                byte[] bmpBytes = new byte[fileHeaderSize + dibBytes.Length];
                bmpBytes[0] = (byte)'B';
                bmpBytes[1] = (byte)'M';
                Buffer.BlockCopy(BitConverter.GetBytes(bmpBytes.Length), 0, bmpBytes, 2, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(0), 0, bmpBytes, 6, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(pixelOffset), 0, bmpBytes, 10, 4);
                Buffer.BlockCopy(dibBytes, 0, bmpBytes, fileHeaderSize, dibBytes.Length);

                using var bmpStream = new MemoryStream(bmpBytes);
                var decoder = BitmapDecoder.Create(bmpStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count > 0) return decoder.Frames[0];
            }
            catch (Exception) { }

            return null;
        }

        private BitmapSource NormalizeClipboardBitmap(BitmapSource source)
        {
            BitmapSource bmp = source;
            if (bmp.Format != PixelFormats.Bgra32)
            {
                bmp = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
            }

            int w = bmp.PixelWidth;
            int h = bmp.PixelHeight;
            if (w <= 0 || h <= 0) return bmp;

            int stride = w * 4;
            byte[] pixels = new byte[h * stride];
            bmp.CopyPixels(pixels, stride, 0);

            bool allAlphaZero = true;
            bool hasVisibleRgb = false;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte a = pixels[i + 3];
                if (a != 0)
                {
                    allAlphaZero = false;
                    break;
                }

                if (!hasVisibleRgb && (pixels[i] != 0 || pixels[i + 1] != 0 || pixels[i + 2] != 0))
                {
                    hasVisibleRgb = true;
                }
            }

            if (allAlphaZero && hasVisibleRgb)
            {
                for (int i = 0; i < pixels.Length; i += 4) pixels[i + 3] = 255;

                var fixedBmp = BitmapSource.Create(w, h, bmp.DpiX, bmp.DpiY, PixelFormats.Bgra32, null, pixels, stride);
                fixedBmp.Freeze();
                return fixedBmp;
            }

            if (!bmp.IsFrozen && bmp.CanFreeze) bmp.Freeze();
            return bmp;
        }

        private bool TryExtractBitmapFromDataObject(IDataObject? dataObj, out BitmapSource? bitmap)
        {
            bitmap = null;
            if (dataObj == null) return false;

            // 1) PNG（常见于 Acrobat/Office/浏览器）
            if (dataObj.GetDataPresent("PNG"))
            {
                var pngPayload = dataObj.GetData("PNG");
                var pngBitmap = pngPayload != null ? DecodeBitmapFromPngPayload(pngPayload) : null;
                if (pngBitmap != null)
                {
                    bitmap = NormalizeClipboardBitmap(pngBitmap);
                    return true;
                }
            }

            // 2) GDI Bitmap
            if (dataObj.GetDataPresent("System.Drawing.Bitmap"))
            {
                try
                {
                    var drawingBitmap = dataObj.GetData("System.Drawing.Bitmap") as System.Drawing.Bitmap;
                    if (drawingBitmap != null)
                    {
                        bitmap = NormalizeClipboardBitmap(ConvertDrawingBitmapToWPF(drawingBitmap));
                        return true;
                    }
                }
                catch (Exception) { }
            }

            if (dataObj.GetDataPresent("Bitmap"))
            {
                try
                {
                    var drawingBitmap = dataObj.GetData("Bitmap") as System.Drawing.Bitmap;
                    if (drawingBitmap != null)
                    {
                        bitmap = NormalizeClipboardBitmap(ConvertDrawingBitmapToWPF(drawingBitmap));
                        return true;
                    }
                }
                catch (Exception) { }
            }

            // 3) 标准位图
            if (dataObj.GetDataPresent(DataFormats.Bitmap))
            {
                try
                {
                    if (dataObj.GetData(DataFormats.Bitmap) is BitmapSource bitmapSource)
                    {
                        bitmap = NormalizeClipboardBitmap(bitmapSource);
                        return true;
                    }

                    if (dataObj.GetData(DataFormats.Bitmap) is System.Drawing.Bitmap drawingBitmap)
                    {
                        bitmap = NormalizeClipboardBitmap(ConvertDrawingBitmapToWPF(drawingBitmap));
                        return true;
                    }
                }
                catch (Exception) { }
            }

            // 4) DIB / DeviceIndependentBitmap
            if (dataObj.GetDataPresent(DataFormats.Dib))
            {
                var dibPayload = dataObj.GetData(DataFormats.Dib);
                var dibBitmap = dibPayload != null ? DecodeBitmapFromDibPayload(dibPayload) : null;
                if (dibBitmap != null)
                {
                    bitmap = NormalizeClipboardBitmap(dibBitmap);
                    return true;
                }
            }

            if (dataObj.GetDataPresent("DeviceIndependentBitmap"))
            {
                var dibPayload = dataObj.GetData("DeviceIndependentBitmap");
                var dibBitmap = dibPayload != null ? DecodeBitmapFromDibPayload(dibPayload) : null;
                if (dibBitmap != null)
                {
                    bitmap = NormalizeClipboardBitmap(dibBitmap);
                    return true;
                }
            }

            return false;
        }

        private BitmapSource GetBestImageFromClipboard()
        {
            var dataObj = ClipboardHelper.GetDataObjectWithRetry();
            if (dataObj == null) return null;
            if (TryExtractBitmapFromDataObject(dataObj, out var bitmap)) return bitmap;
            return null;
        }

        // 转换方法：GDI+ Bitmap -> WPF BitmapSource
        private BitmapSource ConvertDrawingBitmapToWPF(System.Drawing.Bitmap bitmap)
        {
            IntPtr hBitmap = IntPtr.Zero;
            try
            {
                hBitmap = bitmap.GetHbitmap();

                var wpfBitmap = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                wpfBitmap.Freeze();

                return wpfBitmap;
            }
            finally
            {
                if (hBitmap != IntPtr.Zero)
                {
                    DeleteObject(hBitmap);
                }
            }
        }

        private async void OnClipboardContentChanged()
        {
            try
            {
                if (IsViewMode) return;
                var dataObj = ClipboardHelper.GetDataObjectWithRetry();
                if (dataObj != null && dataObj.GetDataPresent(InternalClipboardFormat)) return;

                List<string> filesToLoad = new List<string>();

                // 情况 A: 剪切板是文件列表 (复制了文件)
                if (dataObj != null && dataObj.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = dataObj.GetData(DataFormats.FileDrop) as string[];
                    if (files != null)
                    {
                        foreach (var file in files) if (IsImageFile(file)) filesToLoad.Add(file);
                    }
                }
                // 情况 B: 剪切板是位图数据 (截图)
                else if (TryExtractBitmapFromDataObject(dataObj, out var bitmapSource))
                {
                    if (bitmapSource != null)
                    {
                        string cachePath = SaveClipboardImageToCache(bitmapSource);
                        if (!string.IsNullOrEmpty(cachePath))  filesToLoad.Add(cachePath);
                    }
                }
                if (filesToLoad.Count > 0)
                {
                    await InsertImagesToTabs(filesToLoad.ToArray());
                    var settings = SettingsManager.Instance.Current;
                    if (settings.AutoPopupOnClipboardImage) RestoreWindow(this);
                }
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_ClipboardError_Prefix"), ex.Message), ex);

            }
        }
        private bool IsVisualAncestorOf<T>(DependencyObject node) where T : DependencyObject
        {
            while (node != null)
            {
                if (node is T) return true;
                node = VisualTreeHelper.GetParent(node);
            }
            return false;
        }


        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                _restoreBounds = new Rect(Left, Top, Width, Height);
                _maximized = true;

                if (!IsWin11)
                {
                    var border = FindName("WindowRootBorder") as System.Windows.Controls.Border;
                    if (border != null)
                    {
                        border.Margin = new Thickness(0);
                        border.CornerRadius = new CornerRadius(0);
                        border.BorderThickness = new Thickness(0);
                        border.Effect = null;
                    }
                }

                var workArea = SystemParameters.WorkArea;
                Left = workArea.Left;
                Top = workArea.Top;
                Width = workArea.Width;
                Height = workArea.Height;

                // 切换到还原图标
                SetRestoreIcon();
                WindowState = WindowState.Normal;
            }
            else if (WindowState == WindowState.Normal)
            {
                if (!IsWin11)
                {
                    var border = FindName("WindowRootBorder") as System.Windows.Controls.Border;
                    if (border != null)
                    {
                        border.Margin = new Thickness(12);
                        border.CornerRadius = new CornerRadius(8);
                        border.BorderThickness = new Thickness(1);
                        border.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 12, ShadowDepth = 0, Opacity = 0.4, Color = Colors.Black, RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance };
                    }
                }
            }
        }
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            bool isNext = IsShortcut("View.NextImage", e);
            bool isPrev = IsShortcut("View.PrevImage", e);

            if (isNext || isPrev)
            {
                // 重置状态
                _isNavigating = false;
                _navKeyPressStartTime = DateTime.MinValue;
            }
        }


        private void Control_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // 1. 强制让当前的 ComboBox 失去焦点并应用更改
                DependencyObject focusScope = FocusManager.GetFocusScope((System.Windows.Controls.Control)sender);
                FocusManager.SetFocusedElement(focusScope, _activeTextBox);

                // 2. 将焦点还给画布上的文本框，让用户可以继续打字
                if (_activeTextBox != null)  _activeTextBox.Focus();
                e.Handled = true; // 阻止回车产生额外的换行 or 响铃
            }
        }
        private void UpdateUIStatus(double realScale, bool updateSlider = true)
        {
            if (MyStatusBar == null) return;
            MyStatusBar.ZoomComboBox.Text = realScale.ToString("P0");
            ZoomLevel = realScale.ToString("P0");

            if (updateSlider)
            {
                double targetSliderVal = ZoomToSlider(realScale);
                _isInternalZoomUpdate = true;
                MyStatusBar.ZoomSliderControl.Value = targetSliderVal;
                _isInternalZoomUpdate = false;
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateImageBarSliderState(); UpdateToolSelectionHighlight();
            CheckFittoWindow();
        }



    }
}
