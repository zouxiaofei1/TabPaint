
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
//Toolbar包括上下两个工具栏！
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
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
                UpdateCurrentColor(_ctx.PenColor, useSecondColor);
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