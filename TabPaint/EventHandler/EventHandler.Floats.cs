//
//EventHandler.Floats.cs
//所有悬浮窗控件
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;
using TabPaint.Controls;
using TabPaint.UIHandlers;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {

        private bool _isTextBarDragging = false;
        private System.Windows.Point _textBarLastPoint;
        private bool _isOcrBarDragging = false;
        private System.Windows.Point _ocrBarLastPoint;
        private const double DragSafetyMargin = AppConsts.DragSafetyMargin;
        private void FontSettingChanged(object sender, RoutedEventArgs e)
        {
            if (_tools == null || _router.CurrentTool != _tools.Text) return;
            if (_router.CurrentTool is TextTool textTool)
            {
                if (sender == TextMenu.SubscriptBtn && TextMenu.SubscriptBtn.IsChecked == true) TextMenu.SuperscriptBtn.IsChecked = false;
                if (sender == TextMenu.SuperscriptBtn && TextMenu.SuperscriptBtn.IsChecked == true) TextMenu.SubscriptBtn.IsChecked = false;

                textTool.ApplySelectionAttributes(); // 应用选区样式
                textTool.UpdateCurrentTextBoxAttributes(); // 应用整体样式并重绘边框
            }
        }
        private void FontSizeBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TextMenu.FontSizeBox.Text, out _)) TextMenu.FontSizeBox.Text = _activeTextBox.FontSize.ToString(); // 还原为当前有效字号
        }
        private void TextEditBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Border || e.OriginalSource is StackPanel)
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    _isTextBarDragging = true;
                    _textBarLastPoint = e.GetPosition(this); // 获取相对于窗口的坐标
                    TextMenu.TextEditBar.CaptureMouse();
                    if (_tools?.Text is TextTool tt && tt._richTextBox != null)
                    {
                        tt._richTextBox.Focus();
                    }
                }
            }
        }
        private void TextEditBar_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isTextBarDragging)
            {
                System.Windows.Point currentPoint = e.GetPosition(this);
                double offsetX = currentPoint.X - _textBarLastPoint.X;
                double offsetY = currentPoint.Y - _textBarLastPoint.Y;

                // 2. 预测新的 Transform 值
                double proposedTransformX = TextMenu.TextBarDragTransform.X + offsetX;
                double proposedTransformY = TextMenu.TextBarDragTransform.Y + offsetY;
                double windowWidth = this.ActualWidth;
                double windowHeight = this.ActualHeight;
                double barWidth = TextMenu.TextEditBar.ActualWidth;
                double barHeight = TextMenu.TextEditBar.ActualHeight;

                double initialLeft = (windowWidth - barWidth) / 2;
                double initialTop = TextMenu.TextEditBar.Margin.Top;

                // 计算移动后的绝对位置 (Left, Top)
                double absoluteLeft = initialLeft + proposedTransformX;
                double absoluteTop = initialTop + proposedTransformY;

                if (absoluteLeft + barWidth < DragSafetyMargin)
                {
                    proposedTransformX = DragSafetyMargin - barWidth - initialLeft;
                }
                else if (absoluteLeft > windowWidth - DragSafetyMargin)
                {
                    proposedTransformX = windowWidth - DragSafetyMargin - initialLeft;
                }

                if (absoluteTop < 0)
                {
                    proposedTransformY = -initialTop; // 刚好顶到窗口上边缘
                }
                else if (absoluteTop > windowHeight - DragSafetyMargin)
                {
                    proposedTransformY = windowHeight - DragSafetyMargin - initialTop;
                }
                TextMenu.TextBarDragTransform.X = proposedTransformX;
                TextMenu.TextBarDragTransform.Y = proposedTransformY;
                _textBarLastPoint = currentPoint;
            }
        }
        private void TextEditBar_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isTextBarDragging)
            {
                _isTextBarDragging = false;
                TextMenu.TextEditBar.ReleaseMouseCapture(); // 释放鼠标捕获
            }
        }

        private void OcrFloatBar_BarMouseDown(object sender, MouseButtonEventArgs e)
        {

        }

        private void OcrFloatBar_BarMouseMove(object sender, MouseEventArgs e)
        {

        }

        private void OcrFloatBar_BarMouseUp(object sender, MouseButtonEventArgs e)
        {

        }

        private void TextAlign_Click(object sender, RoutedEventArgs e)
        {
            var mw = MainWindow.GetCurrentInstance();
            if (sender is ToggleButton btn && btn.Tag is string align)
            {
                // 实现互斥
                mw.TextMenu.AlignLeftBtn.IsChecked = (align == "Left");
                mw.TextMenu.AlignCenterBtn.IsChecked = (align == "Center");
                mw.TextMenu.AlignRightBtn.IsChecked = (align == "Right");

                mw.FontSettingChanged(sender, null);
            }
        }
        private void InsertTable_Click(object sender, RoutedEventArgs e)
        {
            if (_router.CurrentTool is TextTool textTool && textTool._richTextBox != null)
            {
                textTool.InsertTableIntoCurrentBox();
            }
            else
            {
                ShowToast(LocalizationManager.GetString("L_Main_Toast_Info")); // "请先创建文本框"
            }
        }
        private void OpacitySlider_Loaded(object sender, RoutedEventArgs e)
        {
            if (OpacitySlider.Template != null)    // 尝试在可视树中查找 Slider 内部的 Thumb
                _opacitySliderThumb = OpacitySlider.Template.FindName("Thumb", OpacitySlider) as Thumb;
        }

        private void OpacitySlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            if (OpacitySlider.ToolTip is System.Windows.Controls.ToolTip toolTip)
            {
                toolTip.PlacementTarget = OpacitySlider;
                toolTip.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
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
            if (!_isInitialLayoutComplete || _isUpdatingToolSettings) return;
            if (OpacitySlider == null) return;

            PenOpacity = e.NewValue;
            if (_ctx != null) _ctx.PenOpacity = e.NewValue;

            if (OpacitySlider.ToolTip is System.Windows.Controls.ToolTip toolTip && toolTip.IsOpen)
            {
                UpdateToolTipOffset(toolTip);
            }
        }
        private void ThicknessSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            ThicknessPreview.Visibility = Visibility.Visible;
            UpdateThicknessPreviewPosition();

            ThicknessTip.Visibility = Visibility.Visible;
            SetThicknessSlider_Pos(ThicknessSlider.Value);
        }

        private void ThicknessSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            ThicknessPreview.Visibility = Visibility.Collapsed;
            ThicknessTip.Visibility = Visibility.Collapsed;
        }

        private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialLayoutComplete) return;
            if (ThicknessTip == null || ThicknessTipText == null || ThicknessSlider == null || ThicknessSlider.Visibility != Visibility.Visible)
                return;
            if (_isUpdatingToolSettings)
            {
                ThicknessTip.Visibility = Visibility.Collapsed;
                return;
            }
            double realSize = PenThickness;

            SetThicknessSlider_Pos(e.NewValue);
            UpdateThicknessPreviewPosition();

            ThicknessTipText.Text = $"{(int)Math.Round(realSize)}" + LocalizationManager.GetString("L_Main_Unit_Pixel");
            ThicknessTip.Visibility = Visibility.Visible;
        }
    }
}