
using Microsoft.VisualBasic.FileIO;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

//
//TabPaint事件处理cs
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {

        private bool HandleGlobalShortcuts(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                // 切换模式
                IsViewMode = !IsViewMode;

                // 执行切换时的额外逻辑
                OnModeChanged(IsViewMode);
                e.Handled = true;
                return true;
            }
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.L:
                        RotateBitmap(-90);
                        e.Handled = true;
                        return true;
                    case Key.R:
                        RotateBitmap(90);
                        e.Handled = true;
                        return true;
                }
            }
            else if (Keyboard.Modifiers == ModifierKeys.None)
            {
                switch (e.Key)
                {
                    case Key.Left:
                        ShowPrevImage();
                        e.Handled = true;
                        return true;
                    case Key.Right:
                        ShowNextImage();
                        e.Handled = true;
                        return true;
                    case Key.F11:
                        MaximizeWindowHandler(); e.Handled = true;
                        return true;
                }
            }
            return false;
        }
        private void HandleViewModeShortcuts(object sender, System.Windows.Input.KeyEventArgs e)
        {
        }
        private void HandlePaintModeShortcuts(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.V &&
                (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
                (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {

                PasteClipboardAsNewTab();
                e.Handled = true;
                return; // 处理完毕，直接返回
            }

            if (e.Key == Key.P &&
    (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
    (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                var settings = SettingsManager.Instance.Current;
                settings.EnableClipboardMonitor = !settings.EnableClipboardMonitor;

                e.Handled = true;
                return; // 处理完毕，直接返回
            }

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.Z: Undo(); e.Handled = true; break;
                    case Key.Y: Redo(); e.Handled = true; break;
                    case Key.S: OnSaveClick(sender, e); e.Handled = true; break;
                    case Key.N: OnNewClick(sender, e); e.Handled = true; break;
                    case Key.O: OnOpenClick(sender, e); e.Handled = true; break;
                    case Key.W:
                        var currentTab = FileTabs?.FirstOrDefault(t => t.IsSelected);
                        if (currentTab != null) CloseTab(currentTab);
                        e.Handled = true;
                        break;
                    case Key.V:// 这里处理普通的 Ctrl + V (画布内粘贴)
                        bool isMultiFilePaste = false;
                        if (System.Windows.Clipboard.ContainsFileDropList())
                        {
                            var fileList = System.Windows.Clipboard.GetFileDropList();
                            if (fileList != null)
                            {
                                var validImages = new List<string>();
                                foreach (string file in fileList)
                                {
                                    if (IsImageFile(file)) validImages.Add(file);
                                }
                                if (validImages.Count > 1)
                                {
                                    _ = OpenFilesAsNewTabs(validImages.ToArray());
                                    isMultiFilePaste = true;
                                }
                            }
                        }
                        if (!isMultiFilePaste)
                        {
                            _router.SetTool(_tools.Select);
                            if (_tools.Select is SelectTool st) st.PasteSelection(_ctx, true);
                        }
                        e.Handled = true;
                        break;
                    case Key.A:
                        _router.SetTool(_tools.Select);
                        if (_tools.Select is SelectTool stSelectAll) stSelectAll.SelectAll(_ctx);
                        e.Handled = true;
                        break;

                }
            }
            else if (Keyboard.Modifiers == ModifierKeys.None)
            {
                switch (e.Key)
                {

                    case Key.Delete:
                        if (_tools.Select is SelectTool st && st.HasActiveSelection)
                        {
                            st.DeleteSelection(_ctx);
                        }
                        else
                        {
                            HandleDeleteFileAction();
                        }
                        e.Handled = true;
                        break;

                }
            }
        }

        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (HandleGlobalShortcuts(sender, e)) return;

            // 2. 根据模式分发
            if (IsViewMode)
            {
                HandleViewModeShortcuts(sender, e);
            }
            else
            {
                HandlePaintModeShortcuts(sender, e);
            }


        }
        private void HandleDeleteFileAction()
        {
            // 2. 处理物理文件删除
            var currentTab = FileTabs?.FirstOrDefault(t => t.IsSelected);
            if (currentTab == null || string.IsNullOrEmpty(currentTab.FilePath)) return;

            // 检查文件是否存在（防止 0.7.2 中的 Undo null 异常或路径错误）
            if (!System.IO.File.Exists(currentTab.FilePath)) return;

            var result = System.Windows.MessageBox.Show(
                $"确定要将图片移至回收站吗？\n{currentTab.FileName}",
                "删除文件",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                string pathToDelete = currentTab.FilePath;

                try
                {
                    CloseTab(currentTab);

                    // 5. 调用系统接口移至回收站
                    // 这样用户万一删错了还能在回收站找回来，比 File.Delete 安全得多
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                        pathToDelete,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"删除失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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
                // 默认注册监听，通过 bool 标志控制逻辑
                AddClipboardFormatListener(helper.Handle);
            }
        }
        private void MainWindow_Deactivated(object sender, EventArgs e)
        {
            _router.CurrentTool?.StopAction(_ctx);
        }


        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //  s(1);
            if (e.ClickCount == 2) // 双击标题栏切换最大化/还原
            {

                MaximizeRestore_Click(sender, null);
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (_maximized)
                {
                    // 记录按下位置，准备看是否拖动
                    _dragStartPoint = e.GetPosition(this);
                    _draggingFromMaximized = true;
                    MouseMove += Border_MouseMoveFromMaximized;
                }
                else
                {
                    DragMove(); // 普通拖动
                }
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
        // 在类成员变量区域添加

        // 在构造函数 MainWindow() 中调用此方法，或者直接把代码放进去

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        // 状态变量
        private uint _lastClipboardSequenceNumber = 0;
        private DateTime _lastClipboardActionTime = DateTime.MinValue;
        private const int CLIPBOARD_COOLDOWN_MS = 1000;
        // 计时器触发事件：真正的执行逻辑


        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                if (SettingsManager.Instance.Current.EnableClipboardMonitor)
                {
                    // 1. 获取当前剪切板的系统序列号
                    uint currentSeq = GetClipboardSequenceNumber();

                    // 2. 检查是否完全重复的消息 (序列号没变 = 剪切板内容没变，纯粹是系统发神经)
                    if (currentSeq == _lastClipboardSequenceNumber)
                    {
                        // 忽略
                        return IntPtr.Zero;
                    }

                    var timeSinceLast = (DateTime.Now - _lastClipboardActionTime).TotalMilliseconds;
                    if (timeSinceLast < CLIPBOARD_COOLDOWN_MS)
                    {

                        // 虽然跳过逻辑，但要更新序列号，以免冷却结束后把旧消息当新消息
                        _lastClipboardSequenceNumber = currentSeq;
                        return IntPtr.Zero;
                    }

                    // 4. 通过所有检查，记录状态并执行
                    _lastClipboardSequenceNumber = currentSeq;
                    _lastClipboardActionTime = DateTime.Now;
                    OnClipboardContentChanged();
                }
            }
            if (msg == WM_NCHITTEST)
            {
                if (_maximized)
                {
                    handled = true;
                    return (IntPtr)1; // HTCLIENT
                }
                // 获得鼠标相对于窗口的位置
                var mousePos = PointFromScreen(new Point(
                    (short)(lParam.ToInt32() & 0xFFFF),
                    (short)((lParam.ToInt32() >> 16) & 0xFFFF)));

                double width = ActualWidth;
                double height = ActualHeight;
                int resizeBorder = 12; // 可拖动边框宽度

                handled = true;

                // 判断边缘区域
                if (mousePos.Y <= resizeBorder)
                {
                    if (mousePos.X <= resizeBorder) return (IntPtr)HTTOPLEFT;
                    if (mousePos.X >= width - resizeBorder) return (IntPtr)HTTOPRIGHT;
                    return (IntPtr)HTTOP;
                }
                else if (mousePos.Y >= height - resizeBorder)
                {
                    if (mousePos.X <= resizeBorder) return (IntPtr)HTBOTTOMLEFT;
                    if (mousePos.X >= width - resizeBorder) return (IntPtr)HTBOTTOMRIGHT;
                    return (IntPtr)HTBOTTOM;
                }
                else
                {
                    if (mousePos.X <= resizeBorder) return (IntPtr)HTLEFT;
                    if (mousePos.X >= width - resizeBorder) return (IntPtr)HTRIGHT;
                }

                // 否则返回客户区
                return (IntPtr)1; // HTCLIENT
            }

            return IntPtr.Zero;
        }
        private void ClipboardMonitorToggle_Click(object sender, RoutedEventArgs e)
        {

        }

        // 4. 剪切板内容处理核心
        private async void OnClipboardContentChanged()
        {
            try
            {
                var dataObj = System.Windows.Clipboard.GetDataObject();
                if (dataObj != null && dataObj.GetDataPresent(InternalClipboardFormat)) return;

                List<string> filesToLoad = new List<string>();

                // 情况 A: 剪切板是文件列表 (复制了文件)
                if (System.Windows.Clipboard.ContainsFileDropList())
                {
                    var files = System.Windows.Clipboard.GetFileDropList();
                    foreach (var file in files) if (IsImageFile(file)) filesToLoad.Add(file);
                }
                // 情况 B: 剪切板是位图数据 (截图)
                else if (System.Windows.Clipboard.ContainsImage())
                {
                    var bitmapSource = System.Windows.Clipboard.GetImage();
                    if (bitmapSource != null)
                    {
                        // TabPaint 架构依赖文件路径，所以我们需要保存为临时缓存文件
                        string cachePath = SaveClipboardImageToCache(bitmapSource);
                        if (!string.IsNullOrEmpty(cachePath))
                        {
                            filesToLoad.Add(cachePath);
                        }
                    }
                }
                if (filesToLoad.Count > 0)
                {
                    await InsertImagesToTabs(filesToLoad.ToArray());
                }
            }
            catch (Exception ex)
            {
                // 处理剪切板被占用等异常，静默失败即可
                System.Diagnostics.Debug.WriteLine("Clipboard Access Error: " + ex.Message);
            }
        }
        private bool IsVisualAncestorOf<T>(DependencyObject node) where T : DependencyObject
        {
            while (node != null)
            {
                if (node is T) return true;
                node = VisualTreeHelper.GetParent(node); // 关键：获取视觉树父级
            }
            return false;
        }


        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                _restoreBounds = new Rect(Left, Top, Width, Height);
                _maximized = true;

                var workArea = SystemParameters.WorkArea;
                Left = workArea.Left;
                Top = workArea.Top;
                Width = workArea.Width;
                Height = workArea.Height;

                // 切换到还原图标
                SetRestoreIcon();
                WindowState = WindowState.Normal;
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
                if (_activeTextBox != null)
                {
                    _activeTextBox.Focus();
                    // 将光标移到文字末尾
                    _activeTextBox.SelectionStart = _activeTextBox.Text.Length;
                }
                e.Handled = true; // 阻止回车产生额外的换行或响铃
            }
        }
        private void UpdateUIStatus(double realScale)
        {
            // 更新文本显示
            MyStatusBar.ZoomComboBox.Text = realScale.ToString("P0");
            ZoomLevel = realScale.ToString("P0"); // 如果你有绑定的属性

            // 更新滑块位置 (反向计算)
            double targetSliderVal = ZoomToSlider(realScale);

            // 【重要】设置标志位，告诉 Slider_ValueChanged 事件：“这是我改的，别触发缩放逻辑”
            _isInternalZoomUpdate = true;
            MyStatusBar.ZoomSliderControl.Value = targetSliderVal;
            _isInternalZoomUpdate = false;
        }
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateImageBarSliderState();
        }
        private void SetZoom(double targetScale, Point? center = null)
        {
            double oldScale = zoomscale;

            // 1. 计算最小缩放比例限制 (原有逻辑: 自适应图片大小)
            double minrate = 1.0;
            if (_bitmap != null)
            {
                // 确保除数不为0
                double maxDim = Math.Max(_bitmap.PixelWidth, _bitmap.PixelHeight);
                if (maxDim > 0)
                    minrate = 1500.0 / maxDim;
            }

            // 2. 限制缩放范围
            double newScale = Math.Clamp(targetScale, MinZoom * minrate, MaxZoom);

            // 如果缩放比例没有变化（例如已经到了极限），直接返回，避免不必要的UI刷新
            if (Math.Abs(newScale - oldScale) < 0.0001) return;

            // 3. 确定缩放锚点（鼠标位置 或 视图中心）
            Point anchorPoint;
            if (center.HasValue)
            {
                anchorPoint = center.Value;
            }
            else
            {
                // 如果没有指定点（比如通过按钮缩放），则以当前 ScrollViewer 可视区域的中心为锚点
                anchorPoint = new Point(ScrollContainer.ViewportWidth / 2, ScrollContainer.ViewportHeight / 2);
            }

            // 4. 更新数据
            zoomscale = newScale;
            ZoomTransform.ScaleX = ZoomTransform.ScaleY = newScale;
            UpdateUIStatus(zoomscale);
            if (zoomscale < (IsViewMode?1.6: 0.8))
            {
                RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.Linear);
            }
            else
            {
                RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.NearestNeighbor);
            }
            // 5. 计算并应用滚动条偏移量 (维持锚点相对位置不变的平移公式)
            double offsetX = ScrollContainer.HorizontalOffset;
            double offsetY = ScrollContainer.VerticalOffset;

            double newOffsetX = (offsetX + anchorPoint.X) * (newScale / oldScale) - anchorPoint.X;
            double newOffsetY = (offsetY + anchorPoint.Y) * (newScale / oldScale) - anchorPoint.Y;

            ScrollContainer.ScrollToHorizontalOffset(newOffsetX);
            ScrollContainer.ScrollToVerticalOffset(newOffsetY);

            // 6. 刷新工具图层 (原有逻辑)
            if (_tools.Select is SelectTool st) st.RefreshOverlay(_ctx);
            if (_tools.Text is TextTool tx) tx.DrawTextboxOverlay(_ctx);
            _canvasResizer.UpdateUI();
            if (IsViewMode) { ShowToast(newScale.ToString("P0")); }
        }
        private void OnMouseWheelZoom(object sender, MouseWheelEventArgs e)
        {
            // 1. 处理 Shift + 滚轮 (水平滚动)
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                e.Handled = true;
                double scrollAmount = e.Delta > 0 ? -48 : 48;
                ScrollContainer.ScrollToHorizontalOffset(ScrollContainer.HorizontalOffset + scrollAmount);
                return;
            }

            // 2. 处理 Ctrl + 滚轮 (缩放)
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;

            e.Handled = true; // 阻止默认滚动行为，防止画面抖动

            // 计算目标倍率
            double deltaFactor = e.Delta > 0 ? ZoomTimes : 1 / ZoomTimes;
            double targetScale = zoomscale * deltaFactor;

            // 获取鼠标在 ScrollContainer 中的位置作为缩放中心
            Point mousePos = e.GetPosition(ScrollContainer);

            // 调用抽象出的方法
            SetZoom(targetScale, mousePos);
        }
    }
}