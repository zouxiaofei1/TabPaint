
//
//EventHandler.Canvas.cs
//画布相关的事件处理逻辑，包括自动裁剪、OCR、AI背景移除、超分重建以及各种图像滤镜的触发。
//
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Ocr;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private bool IsOcrSupported()
        {
            var os = Environment.OSVersion;
            if (os.Version.Major < 10) return false;
            if (os.Version.Build < 17134) return false;

            return true;
        }
        private bool IsVcRedistInstalled()
        {
            try
            {
                string keyPath = @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64";

                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    if (key != null)
                    {
                        int installed = (int)key.GetValue("Installed", 0);
                        int major = (int)key.GetValue("Major", 0);
                        return installed == 1 && major >= 14;
                    }
                }
            }
            catch{ return true;}
            return false;
        }

       


        private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isLoadingImage) return;
            if (e.ChangedButton != MouseButton.Left) return;
            Point pos = e.GetPosition(CanvasWrapper);
            _router.ViewElement_MouseDown(pos, e);
        }
        private Thumb _opacitySliderThumb;

    
        private void UpdateToolTipOffset(System.Windows.Controls.ToolTip toolTip)
        {
            // 1. 获取 Slider 的实际高度
            double sliderHeight = OpacitySlider.ActualHeight;
            double thumbSize = 20;
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
            UpdateSelectionToolBarPosition();
        }

        private void OnCanvasMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isLoadingImage) return;
            _router.CurrentTool?.StopAction(_ctx);
        }
        private void OnScrollContainerDoubleClick(object sender, MouseButtonEventArgs e)
        {

            if (!IsViewMode) { e.Handled = false; return; }
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
                bool canScrollX = ScrollContainer.ScrollableWidth > 0.5;
                bool canScrollY = ScrollContainer.ScrollableHeight > 0.5;
                if (canScrollX || canScrollY) // 如果图片大于窗口，执行平移
                {
                    _isPanning = true;
                    _lastMousePosition = e.GetPosition(ScrollContainer);
                    ScrollContainer.CaptureMouse(); // 捕获鼠标，防止移出窗口失效
                    SetViewCursor(true);

                    e.Handled = true;
                    return;
                }
                else
                {
                    if (e.ButtonState == MouseButtonState.Pressed) // 图片小于窗口，拖动窗口本身
                    {
                        try
                        {
                            if (IsViewMode)
                            {
                                BeginViewModeWindowDrag();
                            }
                            else
                            {
                                this.DragMove();
                            }
                        }
                        catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
                        e.Handled = true;
                        return;
                    }
                }
            }
            if (IsViewMode) return;
            if (_router.CurrentTool is SelectTool selTool && selTool._selectionData != null)
            {
                // 检查点击的是否是左键
                if (e.ChangedButton != MouseButton.Left) return;

                if (IsVisualAncestorOf<System.Windows.Controls.Primitives.ScrollBar>(e.OriginalSource as DependencyObject))  return;
                Point ptInCanvas = e.GetPosition(CanvasWrapper);
                Point pixelPos = _ctx.ToPixel(ptInCanvas);

                bool hitHandle = selTool.HitTestHandle(pixelPos, selTool._selectionRect) != SelectTool.ResizeAnchor.None;
                bool hitInside = selTool.IsPointInSelection(pixelPos);

                if (hitHandle || hitInside)
                {
                    selTool.OnPointerDown(_ctx, ptInCanvas);

                    e.Handled = true;
                }
                else
                {
                    selTool.CommitSelection(_ctx);
                    selTool.ClearSelections(_ctx);
                    selTool.lag = 0;
                }
            }
        }
    

    }
}