
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static TabPaint.MainWindow;

//
//TabPaint事件处理cs
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
 
        private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isLoadingImage) return;
            if (e.ChangedButton != MouseButton.Left) return;
            Point pos = e.GetPosition(CanvasWrapper);
            _router.ViewElement_MouseDown(pos, e);
        }
        private Thumb _opacitySliderThumb;

private void OpacitySlider_Loaded(object sender, RoutedEventArgs e)
{
    // 尝试在可视树中查找 Slider 内部的 Thumb
    if (OpacitySlider.Template != null)
    {
        // "Thumb" 是 WPF 默认 Slider 模板中滑块部件的标准名称
        // 如果你的 Win11VerticalSliderStyle 修改了模板，请确认名称是否为 "Thumb"
        _opacitySliderThumb = OpacitySlider.Template.FindName("Thumb", OpacitySlider) as Thumb;
    }
}

        private void OpacitySlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            if (OpacitySlider.ToolTip is System.Windows.Controls.ToolTip toolTip)
            {
                toolTip.PlacementTarget = OpacitySlider;
                toolTip.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;

                // 打开时先更新一次位置
                UpdateToolTipOffset(toolTip);

                toolTip.IsOpen = true;
            }
        }

        private void OpacitySlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (OpacitySlider.ToolTip is System.Windows.Controls.ToolTip toolTip)
            {
                toolTip.IsOpen = false;
            }
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (OpacitySlider.ToolTip is System.Windows.Controls.ToolTip toolTip && toolTip.IsOpen)
            {
                UpdateToolTipOffset(toolTip);
            }
        }

        // 核心计算逻辑
        private void UpdateToolTipOffset(System.Windows.Controls.ToolTip toolTip)
        {
            // 1. 获取 Slider 的实际高度
            double sliderHeight = OpacitySlider.ActualHeight;

            double thumbSize = 20;

            // 3. 计算可滑动区域的有效高度
            double trackHeight = sliderHeight - thumbSize;

            double percent = (OpacitySlider.Value - OpacitySlider.Minimum) / (OpacitySlider.Maximum - OpacitySlider.Minimum);

            double offsetFromTop = (1.0 - percent) * trackHeight;

            toolTip.VerticalOffset = offsetFromTop;

        }


        private void OnCanvasMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isLoadingImage) return;
            Point pos = e.GetPosition(CanvasWrapper);
            _router.ViewElement_MouseMove(pos, e);
        }

        private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isLoadingImage) return;
            Point pos = e.GetPosition(CanvasWrapper);
            _router.ViewElement_MouseUp(pos, e);
        }

        private void OnCanvasMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isLoadingImage) return;
            _router.CurrentTool?.StopAction(_ctx);
        }
        private void OnScrollContainerDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!IsViewMode) return;
            if (e.ChangedButton != MouseButton.Left) return;
            if (_isPanning)
            {
                _isPanning = false;
                ScrollContainer.ReleaseMouseCapture();
                Mouse.OverrideCursor = null; // 恢复光标
            }

            MaximizeWindowHandler();
            e.Handled = true;
        }
   

        private void OnScrollContainerMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) return;
            if (e.ChangedButton != MouseButton.Left) return;
            if (Keyboard.IsKeyDown(Key.Space) && e.ChangedButton == MouseButton.Left || IsViewMode)
            {
                bool canScrollX = ScrollContainer.ScrollableWidth > 0.5; // 用 0.5 容错
                bool canScrollY = ScrollContainer.ScrollableHeight > 0.5;

                // 情况 A: 图片比窗口大 -> 执行平移 (Pan)
                if (canScrollX || canScrollY)
                {
                    _isPanning = true;
                    _lastMousePosition = e.GetPosition(ScrollContainer);
                    ScrollContainer.CaptureMouse();

                    // 改变光标：抓手
                    Mouse.OverrideCursor = System.Windows.Input.Cursors.ScrollAll;
                    e.Handled = true;
                    return;
                }
                else
                {
                    if (e.ButtonState == MouseButtonState.Pressed)
                    {
                        try
                        {
                            this.DragMove();
                        }
                        catch {  }
                        e.Handled = true;
                        return;
                    }
                }
            }
            if (IsViewMode) return;
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
                   // s(1);
                    selTool.CommitSelection(_ctx);
                    selTool.ClearSelections(_ctx);
                    selTool.lag = 0;
                }
            }
        }

    }
}