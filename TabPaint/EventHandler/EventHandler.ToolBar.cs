
using System.ComponentModel;
using System.Drawing;
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
//Toolbar包括上下两个工具栏！
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
       
        /// <summary>
        /// 清空当前所有状态，并切换到新文件所在的文件夹上下文
        /// </summary>
        private async Task SwitchWorkspaceToNewFile(string filePath)
        {
            try
            {
                // 1. 停止当前所有可能的加载任务
                _loadImageCts?.Cancel();
                lock (_queueLock) { _pendingFilePath = null; }
                FileTabs.Clear();
                _imageFiles.Clear(); // 清空之前的文件夹扫描缓存
                _currentImageIndex = -1;

                // 4. 重置画布状态
                _undo?.ClearUndo();
                _undo?.ClearRedo();
                ResetDirtyTracker();
                _workingPath = filePath;
                await OpenImageAndTabs(filePath, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"切换工作区失败: {ex.Message}");
            }
        }

        private void OnPenClick(object sender, RoutedEventArgs e)
        {
            SetBrushStyle(BrushStyle.Pencil);
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

        private void OnShapeStyleClick(object sender, RoutedEventArgs e)
        {
            // 注意：这里要用 e.OriginalSource
            if (e.OriginalSource is System.Windows.Controls.MenuItem item && item.Tag is string tag)
            {
                var shapeTool = _tools.Shape as ShapeTool;
                if (shapeTool == null) return;

                if (Enum.TryParse(tag, out ShapeTool.ShapeType type))
                {
                    shapeTool.SetShapeType(type);
                    _router.SetTool(shapeTool);

                    // 更新图标逻辑保持不变
                    switch (type)
                    {
                        case ShapeTool.ShapeType.Rectangle:
                            MainToolBar.CurrentShapeIcon.Source = (ImageSource)FindResource("Icon_Shape_Rectangle");
                            break;
                        case ShapeTool.ShapeType.Ellipse:
                            MainToolBar.CurrentShapeIcon.Source = (ImageSource)FindResource("Icon_Shape_Ellipse");
                            break;
                        case ShapeTool.ShapeType.Line:
                            MainToolBar.CurrentShapeIcon.Source = (ImageSource)FindResource("Icon_Shape_Line");
                            break;
                        case ShapeTool.ShapeType.Arrow:
                            MainToolBar.CurrentShapeIcon.Source = (ImageSource)FindResource("Icon_Shape_Arrow");
                            break;
                        case ShapeTool.ShapeType.RoundedRectangle:
                            MainToolBar.CurrentShapeIcon.Source = (ImageSource)FindResource("Icon_Shape_RoundedRect");
                            break;
                    }
                }
                MainToolBar.ShapeToggle.IsChecked = false;
            }
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
        private void OnRotateLeftClick(object sender, RoutedEventArgs e)
        {
            RotateBitmap(-90); MainToolBar.RotateFlipMenuToggle.IsChecked = false;
        }

        private void OnRotateRightClick(object sender, RoutedEventArgs e)
        {
            RotateBitmap(90); MainToolBar.RotateFlipMenuToggle.IsChecked = false;
        }

        private void OnRotate180Click(object sender, RoutedEventArgs e)
        {
            RotateBitmap(180); MainToolBar.RotateFlipMenuToggle.IsChecked = false;
        }


        private void OnFlipVerticalClick(object sender, RoutedEventArgs e)
        {
            FlipBitmap(flipVertical: true); MainToolBar.RotateFlipMenuToggle.IsChecked = false;
        }

        private void OnFlipHorizontalClick(object sender, RoutedEventArgs e)
        {
            FlipBitmap(flipVertical: false); MainToolBar.RotateFlipMenuToggle.IsChecked = false;
        }
        private void FontSettingChanged(object sender, RoutedEventArgs e)
        {
            // 防止初始化时触发
            if (_tools == null || _router.CurrentTool != _tools.Text) return;

            // 调用 TextTool 内部方法刷新当前 TextBox
            // 注意：需要将 _tools.Text 强制转换为 TextTool 类型才能访问 UpdateCurrentTextBoxAttributes
            (_tools.Text as TextTool)?.UpdateCurrentTextBoxAttributes();
        }

        private void OnColorOneClick(object sender, RoutedEventArgs e)
        {
            useSecondColor = false;
            _ctx.PenColor = ForegroundColor;
            SelectedBrush = new SolidColorBrush(ForegroundColor);
            UpdateColorHighlight(); // 更新高亮
        }

        private void OnColorTwoClick(object sender, RoutedEventArgs e)
        {
            useSecondColor = true;
            _ctx.PenColor = BackgroundColor;
            SelectedBrush = new SolidColorBrush(BackgroundColor);
            UpdateColorHighlight(); // 更新高亮
        }

        private void OnColorButtonClick(object sender, RoutedEventArgs e)//选色按钮
        {
            if (e.OriginalSource is System.Windows.Controls.Button btn && btn.Background is SolidColorBrush brush)
            {
                SelectedBrush = new SolidColorBrush(brush.Color);

                // 如果你有 ToolContext，可同步笔颜色，例如：
                _ctx.PenColor = brush.Color;
                UpdateCurrentColor(_ctx.PenColor, useSecondColor);
            }
        }

        private void OnCustomColorClick(object sender, RoutedEventArgs e)
        {
            // 获取当前颜色作为初始值
            System.Windows.Media.Color initialColor = _ctx.PenColor;
            // 如果 _ctx.PenColor 是 Colors.Transparent 或者其他特殊值，最好给个默认值
            if (initialColor == Colors.Transparent) initialColor = Colors.Black;

            // 使用新的现代化窗口
            var dlg = new ModernColorPickerWindow(initialColor);
            dlg.Owner = this; // 确保在主窗口之上

            if (dlg.ShowDialog() == true)
            {
                var color = dlg.SelectedColor;
                var brush = new SolidColorBrush(color);

                // 你的原有逻辑
                SelectedBrush = brush;
                // HighlightSelectedButton(null);

                // 同步到绘图上下文
                _ctx.PenColor = color;
                UpdateCurrentColor(_ctx.PenColor, useSecondColor);
            }
        }

        private void OnBrushStyleClick(object sender, RoutedEventArgs e)
        {
            // 注意：这里要用 e.OriginalSource，因为 sender 现在是 ToolBarControl
            if (e.OriginalSource is System.Windows.Controls.MenuItem menuItem
                && menuItem.Tag is string tagString
                && Enum.TryParse(tagString, out BrushStyle style))
            {
               
                _router.SetTool(_tools.Pen);
                
                _ctx.PenStyle = style;

                UpdateToolSelectionHighlight();
                SetPenResizeBarVisibility(_ctx.PenStyle != BrushStyle.Pencil);

                // 关闭 UserControl 里的 ToggleButton
                MainToolBar.BrushToggle.IsChecked = false;
            }
        }
        private void ZoomMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = MyStatusBar.ZoomComboBox;

            if (combo != null && combo.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                double selectedScale = Convert.ToDouble(item.Tag);

                // 这里的 zoomscale, MinZoom, MaxZoom 应该是 MainWindow 里的变量
                zoomscale = Math.Clamp(selectedScale, MinZoom, MaxZoom);
                ZoomTransform.ScaleX = ZoomTransform.ScaleY = zoomscale;

                UpdateSliderBarValue(zoomscale);
            }
        }

        private void OnTextClick(object sender, RoutedEventArgs e)
        {
            _router.SetTool(_tools.Text);
        }
        private void TextEditBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 如果点击的是工具栏背景本身，而不是子控件
            if (e.OriginalSource is Border || e.OriginalSource is StackPanel)
            {
                if (_activeTextBox != null)
                {
                    _activeTextBox.Focus();
                }
                e.Handled = true;
            }
        }

        private void FontSizeBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(FontSizeBox.Text, out _))
            {
                FontSizeBox.Text = _activeTextBox.FontSize.ToString(); // 还原为当前有效字号
            }
        }
    }
}