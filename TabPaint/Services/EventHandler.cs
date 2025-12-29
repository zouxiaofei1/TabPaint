
using Microsoft.VisualBasic.FileIO;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
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
        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {

            if (e.Key == Key.V &&
                (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
                (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {

                PasteClipboardAsNewTab();
                e.Handled = true;
                return; // 处理完毕，直接返回
            }

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.Z:
                        Undo();
                        e.Handled = true;
                        break;
                    case Key.Y:
                        Redo();
                        e.Handled = true;
                        break;
                    case Key.S:
                        OnSaveClick(sender, e);
                        e.Handled = true;
                        break;
                    case Key.N:
                        OnNewClick(sender, e);
                        e.Handled = true;
                        break;
                    case Key.O:
                        OnOpenClick(sender, e);
                        e.Handled = true;
                        break;
                    case Key.W:
                        var currentTab = FileTabs?.FirstOrDefault(t => t.IsSelected);
                        if (currentTab != null) CloseTab(currentTab);
                        e.Handled = true;
                        break;

                    // 这里处理普通的 Ctrl + V (画布内粘贴)
                    case Key.V:
                        _router.SetTool(_tools.Select);
                        if (_tools.Select is SelectTool st)
                        {
                            st.PasteSelection(_ctx, true);
                        }
                        e.Handled = true;
                        break;

                    case Key.A:
                        _router.SetTool(_tools.Select);
                        if (_tools.Select is SelectTool stSelectAll)
                        {
                            stSelectAll.SelectAll(_ctx);
                        }
                        e.Handled = true;
                        break;
                }
            }
            else if (Keyboard.Modifiers == ModifierKeys.None)
            {
                switch (e.Key)
                {
                    case Key.Left:
                        ShowPrevImage();
                        e.Handled = true;
                        break;
                    case Key.Right:
                        ShowNextImage();
                        e.Handled = true;
                        break;
                    case Key.Delete:
                        if (_tools.Select is SelectTool st && st.HasActiveSelection)
                        {
                            st.DeleteSelection(_ctx);
                        }
                        else
                        {
                            // 否则，执行你考虑加入的“删除物理文件”功能
                            HandleDeleteFileAction();
                        }
                        break;
                }
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
            // s();
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
            // When the window loses focus, tell the current tool to stop its action.
            _router.CurrentTool?.StopAction(_ctx);
        }


        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

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

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                if (_isMonitoringClipboard)
                {
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
            //if (ClipboardMonitorToggle.IsChecked == true)
            {
                _isMonitoringClipboard = true;
                // 开启时立即检查一次剪切板
                OnClipboardContentChanged();
            }
        }

        // 4. 剪切板内容处理核心
        private async void OnClipboardContentChanged()
        {
            try
            {
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
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateImageBarSliderState();
        }
        private void OnMouseWheelZoom(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                e.Handled = true; // 阻止默认垂直滚动
                                  // 滚轮向上(Delta>0)向左滚，向下(Delta<0)向右滚
                                  // 48 是常见的行高，你可以根据手感调整倍率
                double scrollAmount = e.Delta > 0 ? -48 : 48;
                ScrollContainer.ScrollToHorizontalOffset(ScrollContainer.HorizontalOffset + scrollAmount);
                return;
            }
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;
            //s(1);
            e.Handled = false; // 阻止默认滚动
            double oldScale = zoomscale;
            double newScale = oldScale * (e.Delta > 0 ? ZoomTimes : 1 / ZoomTimes);
            newScale = Math.Clamp(newScale, MinZoom, MaxZoom);
            zoomscale = newScale;
            ZoomTransform.ScaleX = ZoomTransform.ScaleY = newScale;


            Point mouseInScroll = e.GetPosition(ScrollContainer);

            double offsetX = ScrollContainer.HorizontalOffset;
            double offsetY = ScrollContainer.VerticalOffset;
            UpdateSliderBarValue(zoomscale);
            // 维持鼠标相对画布位置不变的平移公式
            double newOffsetX = (offsetX + mouseInScroll.X) * (newScale / oldScale) - mouseInScroll.X;
            double newOffsetY = (offsetY + mouseInScroll.Y) * (newScale / oldScale) - mouseInScroll.Y;
            ScrollContainer.ScrollToHorizontalOffset(newOffsetX);
            ScrollContainer.ScrollToVerticalOffset(newOffsetY);
            if (_tools.Select is SelectTool st) st.RefreshOverlay(_ctx);

            if (_tools.Text is TextTool tx) tx.DrawTextboxOverlay(_ctx);
            _canvasResizer.UpdateUI();
        }
    }
}