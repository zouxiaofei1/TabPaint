
//
//EventHandler.Zoom.cs
//事件处理部分，缩放。
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
        public void CheckFittoWindow()
        {
            if (!_hasUserManuallyZoomed && _bitmap != null && _startupFinished)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    FitToWindow();
                }, DispatcherPriority.Loaded);
            }
        }
        private void SetZoom(double targetScale, Point? center = null, bool isIntermediate = false, bool slient = false)
        {
            try
            {
                double oldScale = zoomscale;
                double minrate = 1.0;
                if (_bitmap != null)
                {
                    double maxDim = Math.Max(Math.Max(BackgroundImage.Width, _bitmap.PixelWidth), Math.Max(BackgroundImage.Height, _bitmap.PixelHeight));
                    if (maxDim > 0) minrate = 1500.0 / maxDim;
                }
                double newScale = Math.Clamp(targetScale, MinZoom * minrate, MaxZoom);

                Point anchorPoint;
                if (center.HasValue)  anchorPoint = center.Value;
                else   anchorPoint = new Point(ScrollContainer.ViewportWidth / 2, ScrollContainer.ViewportHeight / 2);
                zoomscale = newScale;
                UpdateUIStatus(zoomscale);
                ZoomTransform.ScaleX = ZoomTransform.ScaleY = newScale;

                RefreshBitmapScalingMode();
                double offsetX = ScrollContainer.HorizontalOffset;
                double offsetY = ScrollContainer.VerticalOffset;

                double margin = IsViewMode ? 5.0 : AppConsts.CanvasMargin;
                double newOffsetX = (offsetX + anchorPoint.X - margin) * (newScale / oldScale) + margin - anchorPoint.X;
                double newOffsetY = (offsetY + anchorPoint.Y - margin) * (newScale / oldScale) + margin - anchorPoint.Y;

                ScrollContainer.ScrollToHorizontalOffset(newOffsetX);
                ScrollContainer.ScrollToVerticalOffset(newOffsetY);
                if (IsViewMode) CheckBirdEyeVisibility();
                if (!IsViewMode) UpdateSelectionScalingMode();
                _canvasResizer.UpdateUI();
                if (_tools.Select is SelectTool st) st.RefreshOverlay(_ctx);
                if (_tools.Text is TextTool tx) tx.DrawTextboxOverlay(_ctx);


                UpdateRulerPositions(); UpdateSelectionToolBarPosition();
                if (IsViewMode && _startupFinished && !slient) ShowToast(newScale.ToString("P0"));
            }
            catch (Exception) { }
        }
        // 动画相关字段
        private double _targetZoomScale; // 动画最终要达到的缩放比例
        private Point _zoomCenter;       // 缩放中心（鼠标位置）
        private bool _isZoomAnimating = false;

        private void StartSmoothZoom(double targetScale, Point center)
        {
            try
            {
                double minrate = 1.0;
                if (_bitmap != null)
                {
                    double maxDim = Math.Max(Math.Max(BackgroundImage.Width, _bitmap.PixelWidth), Math.Max(BackgroundImage.Height, _bitmap.PixelHeight));
                    if (maxDim > 0)
                        minrate = 1500.0 / maxDim;
                }

                _targetZoomScale = Math.Clamp(targetScale, MinZoom * minrate, MaxZoom);
                if (Math.Abs(_targetZoomScale - zoomscale) < 0.0001) return;
                if (!_isZoomAnimating || (_zoomCenter != center))
                {
                    _zoomStartScale = zoomscale;
                    _zoomStartScrollH = ScrollContainer.HorizontalOffset;
                    _zoomStartScrollV = ScrollContainer.VerticalOffset;
                    _zoomCenter = center;
                }

                if (!_isZoomAnimating)
                {
                    _isZoomAnimating = true;
                    CompositionTarget.Rendering += OnZoomRendering;
                }
            }
            catch (Exception) { }
        }
        private void OnZoomRendering(object sender, EventArgs e)
        {
            double delta = _targetZoomScale - zoomscale;
            double nextScale;
            bool isEnding = false;

            if (Math.Abs(delta) < AppConsts.ZoomSnapThreshold || Math.Abs(delta) < 0.000001)
            {
                nextScale = _targetZoomScale;
                isEnding = true;
            }
            else
            {
                nextScale = zoomscale + delta * AppConsts.ZoomLerpFactor / Math.Max(1, (PerformanceScore / 2.0));
            }
            double totalScaleRatio = nextScale / _zoomStartScale;
            double margin = IsViewMode ? 5.0 : AppConsts.CanvasMargin;

            double targetScrollH = (_zoomStartScrollH + _zoomCenter.X - margin) * totalScaleRatio + margin - _zoomCenter.X;
            double targetScrollV = (_zoomStartScrollV + _zoomCenter.Y - margin) * totalScaleRatio + margin - _zoomCenter.Y;

            // 应用缩放和滚动
            zoomscale = nextScale;
            ZoomTransform.ScaleX = ZoomTransform.ScaleY = nextScale;

            ScrollContainer.ScrollToHorizontalOffset(targetScrollH);
            ScrollContainer.ScrollToVerticalOffset(targetScrollV);

            // 刷新相关 UI 和状态
            RefreshBitmapScalingMode();
            _canvasResizer.UpdateUI();
            if (IsViewMode) CheckBirdEyeVisibility();
            if (!IsViewMode) UpdateSelectionScalingMode();
            if (_tools.Select is SelectTool st) st.RefreshOverlay(_ctx);
            if (_tools.Text is TextTool tx) tx.DrawTextboxOverlay(_ctx);
            UpdateSelectionToolBarPosition();
            UpdateRulerPositions();

            if (isEnding)
            {
                StopSmoothZoom();
                UpdateUIStatus(zoomscale, updateSlider: true);
            }
            if (IsViewMode && _startupFinished) { ShowToast(zoomscale.ToString("P0")); }
        }


        private void StopSmoothZoom()
        {
            if (_isZoomAnimating)
            {
                _isZoomAnimating = false;
                CompositionTarget.Rendering -= OnZoomRendering;
            }
        }
        private void OnMouseWheelZoom(object sender, MouseWheelEventArgs e)
        {
            // 1. 处理 Shift + 滚轮 (水平滚动) - 优先级最高
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                e.Handled = true;
                double scrollAmount = -e.Delta * AppConsts.WheelScrollFactor;
                ScrollContainer.ScrollToHorizontalOffset(ScrollContainer.HorizontalOffset + scrollAmount);
                return;
            }
            bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            var wheelMode = SettingsManager.Instance.Current.ViewMouseWheelMode;

            bool isViewMode = IsViewMode;

            if (isViewMode && !isCtrl && wheelMode == MouseWheelMode.SwitchImage)
            {
                e.Handled = true; 
                if (e.Delta < 0) ShowNextImage();
                else ShowPrevImage();
                return;
            }

            if (isViewMode && !isCtrl && wheelMode == MouseWheelMode.VerticalScroll)
            {
                e.Handled = true;
                double scrollAmount = -e.Delta * AppConsts.WheelScrollFactor;
                ScrollContainer.ScrollToVerticalOffset(ScrollContainer.VerticalOffset + scrollAmount);
                return;
            }

            if (isCtrl || (isViewMode && wheelMode == MouseWheelMode.Zoom))
            {
                e.Handled = true;
                _hasUserManuallyZoomed = true;
                Point mousePos = e.GetPosition(ScrollContainer);
                double currentBase = _isZoomAnimating ? _targetZoomScale : zoomscale;
                double deltaFactor = e.Delta > 0 ? ZoomTimes : 1 / ZoomTimes;
                double targetScale = currentBase * deltaFactor;
                StartSmoothZoom(targetScale, mousePos);
            }
            else
            {
                e.Handled = true;
                double scrollAmount = -e.Delta * AppConsts.WheelScrollFactor;
                ScrollContainer.ScrollToVerticalOffset(ScrollContainer.VerticalOffset + scrollAmount);
            }
        }
    }
}
