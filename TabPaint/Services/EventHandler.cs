using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

//
//TabPaint主程序
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                OnSaveAsClick(sender, e); // 如果没有当前路径，就走另存为
            }
            else SaveBitmap(_currentFilePath);
        }
        private string _currentFilePath = string.Empty;

        private void OnSaveAsClick(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = PicFilterString,
                FileName = "image.png"
            };
            if (dlg.ShowDialog() == true)
            {
                _currentFilePath = dlg.FileName;
                SaveBitmap(_currentFilePath);
            }
        }

        private void OnPickColorClick(object s, RoutedEventArgs e)
        {
            LastTool = ((MainWindow)System.Windows.Application.Current.MainWindow)._router.CurrentTool;
            _router.SetTool(_tools.Eyedropper);
        }

        private void OnEraserClick(object s, RoutedEventArgs e)
        {
            SetBrushStyle(BrushStyle.Eraser);
        }
        private void OnFillClick(object s, RoutedEventArgs e) => _router.SetTool(_tools.Fill);
        private void OnSelectClick(object s, RoutedEventArgs e) => _router.SetTool(_tools.Select);

        private void OnEffectButtonClick(object sender, RoutedEventArgs e)
        {
            var btn = (System.Windows.Controls.Button)sender;
            btn.ContextMenu.IsOpen = true;
        }


        private void FitToWindow_Click(object sender, RoutedEventArgs e)
        {
            FitToWindow();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            double newScale = zoomscale / ZoomTimes;
            zoomscale = Math.Clamp(newScale, MinZoom, MaxZoom);
            ZoomTransform.ScaleX = ZoomTransform.ScaleY = zoomscale;
            UpdateSliderBarValue(zoomscale);
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            double newScale = zoomscale * ZoomTimes;
            zoomscale = Math.Clamp(newScale, MinZoom, MaxZoom);
            ZoomTransform.ScaleX = ZoomTransform.ScaleY = zoomscale;
            UpdateSliderBarValue(zoomscale);
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            // 确保 SelectTool 是当前工具
            if (_router.CurrentTool != _tools.Select)
                _router.SetTool(_tools.Select); // 切换到选择工具

            if (_router.CurrentTool is SelectTool selectTool)
                selectTool.CopySelection(_ctx);
        }

        private void OnCutClick(object sender, RoutedEventArgs e)
        {
            if (_router.CurrentTool != _tools.Select)
                _router.SetTool(_tools.Select); // 切换到选择工具

            if (_router.CurrentTool is SelectTool selectTool)
                selectTool.CutSelection(_ctx, true);
        }

        private void OnPasteClick(object sender, RoutedEventArgs e)
        {
            if (_router.CurrentTool != _tools.Select)
                _router.SetTool(_tools.Select); // 切换到选择工具

            if (_router.CurrentTool is SelectTool selectTool)
                selectTool.PasteSelection(_ctx, false);

        }

        private void OnUndoClick(object sender, RoutedEventArgs e) => Undo();
        private void OnRedoClick(object sender, RoutedEventArgs e) => Redo();
        private void EmptyClick(object sender, RoutedEventArgs e)
        {
            RotateFlipMenuToggle.IsChecked = false;
            BrushToggle.IsChecked = false;
        }

        private void OnBrightnessContrastExposureClick(object sender, RoutedEventArgs e)
        {
            if (_bitmap == null) return;// 1. (为Undo做准备) 保存当前图像的完整快照
            var fullRect = new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight);
            _undo.PushFullImageUndo(); // 2. 创建对话框，并传入主位图的一个克隆体用于预览
            var dialog = new AdjustBCEWindow(_bitmap, BackgroundImage);   // 3. 显示对话框并根据结果操作
            if (dialog.ShowDialog() == true)
            {// 4. 从对话框获取处理后的位图
                WriteableBitmap adjustedBitmap = dialog.FinalBitmap;   // 5. 将处理后的像素数据写回到主位图 (_bitmap) 中
                int stride = adjustedBitmap.BackBufferStride;
                int byteCount = adjustedBitmap.PixelHeight * stride;
                byte[] pixelData = new byte[byteCount];
                adjustedBitmap.CopyPixels(pixelData, stride, 0);
                _bitmap.WritePixels(fullRect, pixelData, stride, 0);
                SetUndoRedoButtonState();
            }
            else
            {  // 用户点击了 "取消" 或关闭了窗口
                _undo.Undo(); // 弹出刚刚压入的快照
                _undo.ClearRedo(); // 清空因此产生的Redo项
                SetUndoRedoButtonState();
            }
        }


        private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Get the position relative to the scaled CanvasWrapper
            Point pos = e.GetPosition(CanvasWrapper);
            _router.ViewElement_MouseDown(pos, e);
        }

        private void OnCanvasMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Point pos = e.GetPosition(CanvasWrapper);
            _router.ViewElement_MouseMove(pos, e);
        }

        private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(CanvasWrapper);
            _router.ViewElement_MouseUp(pos, e);
        }

        private void OnCanvasMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _router.CurrentTool?.StopAction(_ctx);
        }


        private void Close_Click(object sender, RoutedEventArgs e)
        {
            SaveSession();
            Close();
        }
        private void CropMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 假设你的当前工具存储在一个属性 CurrentTool 中
            // 并且你的 SelectTool 实例是可访问的
            if (_router.CurrentTool is SelectTool selectTool)
            {
                // 创建或获取当前的 ToolContext
                // var toolContext = CreateToolContext(); // 你应该已经有类似的方法

                selectTool.CropToSelection(_ctx);
            }
        }

        private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
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

        private void FontSettingChanged(object? sender, RoutedEventArgs e)
        {
            if (_activeTextBox == null) return;

            // --- 1. 处理字体 (兼容手动输入和选择) ---
            if (FontFamilyBox.SelectedItem is FontFamily family)
            {
                _activeTextBox.FontFamily = family;
            }
            else if (!string.IsNullOrWhiteSpace(FontFamilyBox.Text))
            {
                try
                {
                    // 尝试根据输入的字符串创建字体
                    _activeTextBox.FontFamily = new FontFamily(FontFamilyBox.Text);
                }
                catch { /* 输入了非法字体名则忽略 */ }
            }

            // --- 2. 处理字号 (兼容手动输入) ---
            // 注意：ComboBox 可编辑时，FontSizeBox.Text 是获取输入值的最直接方式
            if (double.TryParse(FontSizeBox.Text, out double size))
            {
                if (size > 0 && size < 1000) // 限制一个合理的范围
                {
                    _activeTextBox.FontSize = size;
                }
            }

            // --- 3. 处理样式按钮 ---
            _activeTextBox.FontWeight = BoldBtn.IsChecked == true ? FontWeights.Bold : FontWeights.Normal;
            _activeTextBox.FontStyle = ItalicBtn.IsChecked == true ? FontStyles.Italic : FontStyles.Normal;
            _activeTextBox.TextDecorations = UnderlineBtn.IsChecked == true ? TextDecorations.Underline : null;

            // --- 4. 强制布局更新并重绘虚线框 ---
            if (_tools.Text is TextTool st)
            {
                // 关键：先让 TextBox 根据新属性重新计算自己的实际宽高
                _activeTextBox.UpdateLayout();

                // 使用 Render 优先级确保在界面渲染时更新虚线框位置
                _activeTextBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    st.DrawTextboxOverlay(_ctx);
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
        }


        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }





        private void MainWindow_Deactivated(object sender, EventArgs e)
        {
            // When the window loses focus, tell the current tool to stop its action.
            _router.CurrentTool?.StopAction(_ctx);
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        private Point _dragStartPoint;
        private bool _draggingFromMaximized = false;

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

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            hwndSource.AddHook(WndProc);
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


        private void OnRotateLeftClick(object sender, RoutedEventArgs e)
        {
            RotateBitmap(-90); RotateFlipMenuToggle.IsChecked = false;
        }

        private void OnRotateRightClick(object sender, RoutedEventArgs e)
        {
            RotateBitmap(90); RotateFlipMenuToggle.IsChecked = false;
        }

        private void OnRotate180Click(object sender, RoutedEventArgs e)
        {
            RotateBitmap(180); RotateFlipMenuToggle.IsChecked = false;
        }


        private void OnFlipVerticalClick(object sender, RoutedEventArgs e)
        {
            FlipBitmap(flipVertical: true); RotateFlipMenuToggle.IsChecked = false;
        }

        private void OnFlipHorizontalClick(object sender, RoutedEventArgs e)
        {
            FlipBitmap(flipVertical: false); RotateFlipMenuToggle.IsChecked = false;
        }


        private void ThicknessSlider_DragStarted(object sender, DragStartedEventArgs e)
        {
            ThicknessPreview.Visibility = Visibility.Visible;
            UpdateThicknessPreviewPosition(); // 初始定位

            ThicknessTip.Visibility = Visibility.Visible;
            SetThicknessSlider_Pos(0);
        }

        private void ThicknessSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            ThicknessPreview.Visibility = Visibility.Collapsed;

            ThicknessTip.Visibility = Visibility.Collapsed;
        }

        private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialLayoutComplete) return;
            PenThickness = e.NewValue;
            UpdateThicknessPreviewPosition();

            if (ThicknessTip == null || ThicknessTipText == null || ThicknessSlider == null)
                return;

            PenThickness = e.NewValue;
            ThicknessTipText.Text = $"{(int)PenThickness} 像素";

            // 让提示显示出来
            ThicknessTip.Visibility = Visibility.Visible;
            SetThicknessSlider_Pos(e.NewValue);

        }

        private void OnBrushStyleClick(object sender, RoutedEventArgs e)
        {
            //  _currentTool = ToolMode.Pen;
            if (sender is System.Windows.Controls.MenuItem menuItem
                && menuItem.Tag is string tagString
                && Enum.TryParse(tagString, out BrushStyle style))
            {
                _router.SetTool(_tools.Pen);
                _ctx.PenStyle = style; // 你的画笔样式枚举
            }
            UpdateToolSelectionHighlight();
            SetPenResizeBarVisibility(_ctx.PenStyle != BrushStyle.Pencil);
            // 点击后关闭下拉按钮
            BrushToggle.IsChecked = false;
        }

        private void OnColorOneClick(object sender, RoutedEventArgs e)
        {
            useSecondColor = false;
            _ctx.PenColor = ForegroundColor;
            UpdateColorHighlight(); // 更新高亮
        }

        private void OnColorTwoClick(object sender, RoutedEventArgs e)
        {
            useSecondColor = true;
            _ctx.PenColor = BackgroundColor;
            UpdateColorHighlight(); // 更新高亮
        }

        private void OnColorButtonClick(object sender, RoutedEventArgs e)//选色按钮
        {
            if (sender is System.Windows.Controls.Button btn && btn.Background is SolidColorBrush brush)
            {
                SelectedBrush = new SolidColorBrush(brush.Color);

                // 如果你有 ToolContext，可同步笔颜色，例如：
                _ctx.PenColor = brush.Color;
                UpdateCurrentColor(_ctx.PenColor,useSecondColor);
            }
        }

        private void OnCustomColorClick(object sender, RoutedEventArgs e)// 点击彩虹按钮自定义颜色
        {
            var dlg = new ColorDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var color = Color.FromArgb(255, dlg.Color.R, dlg.Color.G, dlg.Color.B);
                var brush = new SolidColorBrush(color);
                SelectedBrush = brush;
                //HighlightSelectedButton(null);

                // 同步到绘图上下文
                _ctx.PenColor = color;
                UpdateCurrentColor(_ctx.PenColor, useSecondColor);
            }
        }

        private void OnScrollContainerMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_router.CurrentTool is SelectTool selTool && selTool._selectionData != null)
            {
                // 1. 检查点击的是否是左键（通常右键用于弹出菜单，不应触发提交）
                if (e.ChangedButton != MouseButton.Left) return;

                // 2. 深度判定：点击来源是否属于滚动条的任何组成部分
                if (IsVisualAncestorOf<System.Windows.Controls.Primitives.ScrollBar>(e.OriginalSource as DependencyObject))
                {
                    return; // 点击在滚动条上（轨道、滑块、箭头等），不执行提交
                }

                // 获取逻辑坐标
                Point pt = e.GetPosition(CanvasWrapper);

                // 3. 判定：点击是否不在选区内，且不在缩放句柄上
                bool hitHandle = selTool.HitTestHandle(pt, selTool._selectionRect) != SelectTool.ResizeAnchor.None;
                bool hitInside = selTool.IsPointInSelection(pt);

                if (!hitHandle && !hitInside)
                {
                    // 执行提交
                    selTool.CommitSelection(this._ctx);
                    selTool.CleanUp(this._ctx);

                    // 如果不希望 Canvas 接收这次点击（例如防止开始一次新的拖拽），可以拦截
                    // e.Handled = true; 
                }
            }
        }

        /// <summary>
        /// 辅助方法：向上查找视觉树，判断是否包含指定类型的祖先
        /// </summary>
        private bool IsVisualAncestorOf<T>(DependencyObject node) where T : DependencyObject
        {
            while (node != null)
            {
                if (node is T) return true;
                node = VisualTreeHelper.GetParent(node); // 关键：获取视觉树父级
            }
            return false;
        }


        private void OnTextClick(object sender, RoutedEventArgs e)
        {
            _router.SetTool(_tools.Text);
        }

        private void ZoomMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox combo && combo.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                double selectedScale = Convert.ToDouble(item.Tag);
                zoomscale = Math.Clamp(selectedScale, MinZoom, MaxZoom);
                ZoomTransform.ScaleX = ZoomTransform.ScaleY = zoomscale;
                // s(zoomscale);
                UpdateSliderBarValue(zoomscale);
            }
        }

        private void OnOpenClick(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = PicFilterString
            };
            if (dlg.ShowDialog() == true)
            {
                _currentFilePath = dlg.FileName;
                _currentImageIndex = -1;
                OpenImageAndTabs(_currentFilePath, true);
            }
        }

        private void OnColorTempTintSaturationClick(object sender, RoutedEventArgs e)
        {
            if (_bitmap == null) return;
            _undo.PushFullImageUndo();// 1. (为Undo做准备) 保存当前图像的完整快照
            var dialog = new AdjustTTSWindow(_bitmap); // 2. 创建对话框，并传入主位图的一个克隆体用于预览
                                                       // 注意：这里我们传入的是 _bitmap 本身，因为 AdjustTTSWindow 内部会自己克隆一个原始副本


            if (dialog.ShowDialog() == true) // 更新撤销/重做按钮的状态
                SetUndoRedoButtonState();
            else// 用户点击了 "取消"
            {
                _undo.Undo();
                _undo.ClearRedo();
                SetUndoRedoButtonState();
            }
        }
        private void OnConvertToBlackAndWhiteClick(object sender, RoutedEventArgs e)
        {

            if (_bitmap == null) return;  // 1. 检查图像是否存在
            _undo.PushFullImageUndo();
            ConvertToBlackAndWhite(_bitmap);
            SetUndoRedoButtonState();
        }

        private void OnResizeCanvasClick(object sender, RoutedEventArgs e)
        {
            if (_surface?.Bitmap == null) return;
            var dialog = new ResizeCanvasDialog(// 1. 创建并配置对话框
                _surface.Bitmap.PixelWidth,
                _surface.Bitmap.PixelHeight
            );
            dialog.Owner = this; // 设置所有者，使对话框显示在主窗口中央
            if (dialog.ShowDialog() == true)  // 2. 显示对话框，并检查用户是否点击了“确定”
            {
                // 3. 如果用户点击了“确定”，获取新尺寸并调用缩放方法
                int newWidth = dialog.ImageWidth;
                int newHeight = dialog.ImageHeight;
                ResizeCanvas(newWidth, newHeight);
            }
        }

        private void OnNewClick(object sender, RoutedEventArgs e)
        {
            // 1. 尝试确定插入锚点
            int insertIndex = -1;

            // 优先使用明确点击过的 Tab
            if (_currentTabItem != null && FileTabs.Contains(_currentTabItem))
            {
                insertIndex = FileTabs.IndexOf(_currentTabItem) + 1;
            }
            // 如果没有明确点击过，尝试查找 UI 上被选中的 Tab
            else
            {
                var selectedTab = FileTabs.FirstOrDefault(t => t.IsSelected);
                if (selectedTab != null)
                {
                    insertIndex = FileTabs.IndexOf(selectedTab) + 1;
                    _currentTabItem = selectedTab; // 顺便修正引用
                }
                // 如果 UI 也没选中，尝试通过当前文件路径查找
                else if (!string.IsNullOrEmpty(_currentFilePath))
                {
                    var pathTab = FileTabs.FirstOrDefault(t => t.FilePath == _currentFilePath);
                    if (pathTab != null)
                    {
                        insertIndex = FileTabs.IndexOf(pathTab) + 1;
                        _currentTabItem = pathTab; // 顺便修正引用
                    }
                }
            }

            // 保底逻辑：如果还是算不出来（比如列表为空），就插在最后
            if (insertIndex < 0 || insertIndex > FileTabs.Count)
            {
                insertIndex = FileTabs.Count;
            }

            // 2. 创建新 Tab
            var newTab = new FileTabItem(null)
            {
                IsNew = true,
                IsDirty = false,
                IsSelected = true, // 新建的自然是被选中的
                Thumbnail = CreateWhiteThumbnail() // 调用之前写的生成白图方法
            };

            // 3. 插入集合
            FileTabs.Insert(insertIndex, newTab);

            // 4. 立即激活新 Tab
            // 更新选中状态
            foreach (var tab in FileTabs)
                if (tab != newTab) tab.IsSelected = false;

            // 更新引用
            _currentTabItem = newTab;

            // 执行清空画布操作
            Clean_bitmap(1200, 900);
            _currentFilePath = string.Empty;
            _currentFileName = "未命名";
            UpdateWindowTitle();

            // 5. 滚动到可见位置 (简单处理，往后滚一点)
            if (insertIndex > FileTabs.Count - 2)
                FileTabsScroller.ScrollToRightEnd();
        }

        // 辅助方法：生成纯白缩略图
        private BitmapSource CreateWhiteThumbnail()
        {
            int w = 100; int h = 60;
            var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var ctx = visual.RenderOpen())
            {
                ctx.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, h));
                // 可以在中间画个加号或者 "New" 字样
            }
            bmp.Render(visual);
            bmp.Freeze();
            return bmp;
        }

    }
}