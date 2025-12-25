
using System.ComponentModel;
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
                    case Key.V:
                        if (Keyboard.Modifiers == ModifierKeys.Control)
                        {
                            _router.SetTool(_tools.Select); // 切换到选择工具

                            if (_tools.Select is SelectTool st) // 强转成 SelectTool
                            {
                                st.PasteSelection(_ctx, true);
                            }
                            e.Handled = true;
                        }
                        break;


                    case Key.A:
                        if (Keyboard.Modifiers == ModifierKeys.Control)
                        {
                            _router.SetTool(_tools.Select); // 切换到选择工具

                            if (_tools.Select is SelectTool st) // 强转成 SelectTool
                            {
                                st.SelectAll(_ctx); // 调用选择工具的特有方法
                            }
                            e.Handled = true;
                        }
                        break;
                }
            }
            else
            {
                switch (e.Key)
                {
                    case Key.Left:
                        ShowPrevImage();
                        e.Handled = true; // 防止焦点导航
                        break;
                    case Key.Right:
                        ShowNextImage();
                        e.Handled = true;
                        break;
                }
            }
        }
        private void EmptyClick(object sender, RoutedEventArgs e)
        {
            RotateFlipMenuToggle.IsChecked = false;
            BrushToggle.IsChecked = false;
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
        private void OnMouseWheelZoom(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;

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