
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

        private BitmapSource GetBestImageFromClipboard()
        {
            var dataObj = ClipboardHelper.GetDataObjectWithRetry();
            if (dataObj == null) return null;

            if (dataObj.GetDataPresent("System.Drawing.Bitmap"))
            {
                try
                {
                    var drawingBitmap = dataObj.GetData("System.Drawing.Bitmap") as System.Drawing.Bitmap;
                    if (drawingBitmap != null)
                    {
                        return ConvertDrawingBitmapToWPF(drawingBitmap);
                    }
                }
                catch (Exception)
                {
                }
            }
            if (dataObj.GetDataPresent("Bitmap"))
            {
                try
                {
                    var drawingBitmap = dataObj.GetData("Bitmap") as System.Drawing.Bitmap;
                    if (drawingBitmap != null)
                    {
                        return ConvertDrawingBitmapToWPF(drawingBitmap);
                    }
                }
                catch { }
            }

            if (dataObj.GetDataPresent(DataFormats.Bitmap))
            {
                return dataObj.GetData(DataFormats.Bitmap) as BitmapSource;
            }
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
                else if (dataObj != null && dataObj.GetDataPresent(DataFormats.Bitmap))
                {
                    var bitmapSource = GetBestImageFromClipboard();
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
