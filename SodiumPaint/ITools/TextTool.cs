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
//SodiumPaint主程序
//

namespace SodiumPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public class TextTool : ToolBase
        {
            public override string Name => "Text";
            public override System.Windows.Input.Cursor Cursor => System.Windows.Input.Cursors.IBeam;

            private Int32Rect _textRect;
            private System.Windows.Controls.TextBox _textBox;
            private Point _startPos;
            private bool _dragging = false;

            private ResizeAnchor _currentAnchor = ResizeAnchor.None;
            private bool _resizing = false;
            private Point _startMouse;
            private double _startW, _startH, _startX, _startY;

            // 句柄尺寸
            private const double HandleSize = 6;
            private int lag = 0;

            public enum ResizeAnchor
            {
                None,
                TopLeft, TopMiddle, TopRight,
                LeftMiddle, RightMiddle,
                BottomLeft, BottomMiddle, BottomRight
            }
            public override void Cleanup(ToolContext ctx)
            {
                if (_textBox != null && !string.IsNullOrWhiteSpace(_textBox.Text)) CommitText(ctx);

                if (_textBox != null && ctx.EditorOverlay.Children.Contains(_textBox))
                {
                    ctx.EditorOverlay.Children.Remove(_textBox);
                    _textBox = null;
                }
                if (ctx.SelectionOverlay != null)
                {
                    ctx.SelectionOverlay.Children.Clear();
                    ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
                }
               ((MainWindow)System.Windows.Application.Current.MainWindow).HideTextToolbar();

                // 5️⃣ 重置工具状态
                _dragging = false;
                _resizing = false;
                _currentAnchor = ResizeAnchor.None;
                _textRect = new Int32Rect();
                lag = 0;

                Mouse.OverrideCursor = null;
            }

            private List<Point> GetHandlePositions(Int32Rect rect)
            {
                var handles = new List<Point>();
                double x1 = rect.X;
                double y1 = rect.Y;
                double x2 = rect.X + rect.Width;
                double y2 = rect.Y + rect.Height;
                double mx = (x1 + x2) / 2;
                double my = (y1 + y2) / 2;
                //s(rect);
                handles.Add(new Point(x1, y1)); // TL
                handles.Add(new Point(mx, y1)); // TM
                handles.Add(new Point(x2, y1)); // TR
                handles.Add(new Point(x1, my)); // LM
                handles.Add(new Point(x2, my)); // RM
                handles.Add(new Point(x1, y2)); // BL
                handles.Add(new Point(mx, y2)); // BM
                handles.Add(new Point(x2, y2)); // BR

                return handles;
            }

            public void DrawTextboxOverlay(ToolContext ctx)
            {
                if (_textBox == null) return;

                double invScale = 1 / ((MainWindow)System.Windows.Application.Current.MainWindow).zoomscale;
                var overlay = ctx.SelectionOverlay;
                overlay.Children.Clear();

                // 获取 TextBox 坐标和尺寸
                double x = Canvas.GetLeft(_textBox);
                double y = Canvas.GetTop(_textBox);
                double w = _textBox.ActualWidth;
                double h = _textBox.ActualHeight;
                var rect = new Int32Rect((int)x, (int)y, (int)w, (int)h);

                var outline = new System.Windows.Shapes.Rectangle  // 虚线框
                {
                    Stroke = Brushes.Black,
                    StrokeDashArray = new DoubleCollection { 8, 4 },
                    StrokeThickness = invScale * 1.5,
                    Width = rect.Width,
                    Height = rect.Height
                };
                Canvas.SetLeft(outline, rect.X);
                Canvas.SetTop(outline, rect.Y);
                overlay.Children.Add(outline);

                // 八个句柄
                foreach (var p in GetHandlePositions(rect))
                {
                    var handle = new System.Windows.Shapes.Rectangle
                    {
                        Width = HandleSize * invScale,
                        Height = HandleSize * invScale,
                        Fill = Brushes.White,
                        Stroke = Brushes.Black,
                        StrokeThickness = invScale
                    };
                    Canvas.SetLeft(handle, p.X - HandleSize * invScale / 2);
                    Canvas.SetTop(handle, p.Y - HandleSize * invScale / 2);
                    overlay.Children.Add(handle);
                }

                overlay.IsHitTestVisible = false;
                overlay.Visibility = Visibility.Visible;
            }

            // 判断是否点击到句柄
            private ResizeAnchor HitTestTextboxHandle(Point px)
            {
                if (_textBox == null) return ResizeAnchor.None;
                double size = 12 / ((MainWindow)System.Windows.Application.Current.MainWindow).zoomscale;
                double x1 = Canvas.GetLeft(_textBox);
                double y1 = Canvas.GetTop(_textBox);
                double x2 = x1 + _textBox.ActualWidth;
                double y2 = y1 + _textBox.ActualHeight;
                double mx = (x1 + x2) / 2;
                double my = (y1 + y2) / 2;

                if (Math.Abs(px.X - x1) <= size && Math.Abs(px.Y - y1) <= size) return ResizeAnchor.TopLeft;
                if (Math.Abs(px.X - mx) <= size && Math.Abs(px.Y - y1) <= size) return ResizeAnchor.TopMiddle;
                if (Math.Abs(px.X - x2) <= size && Math.Abs(px.Y - y1) <= size) return ResizeAnchor.TopRight;
                if (Math.Abs(px.X - x1) <= size && Math.Abs(px.Y - my) <= size) return ResizeAnchor.LeftMiddle;
                if (Math.Abs(px.X - x2) <= size && Math.Abs(px.Y - my) <= size) return ResizeAnchor.RightMiddle;
                if (Math.Abs(px.X - x1) <= size && Math.Abs(px.Y - y2) <= size) return ResizeAnchor.BottomLeft;
                if (Math.Abs(px.X - mx) <= size && Math.Abs(px.Y - y2) <= size) return ResizeAnchor.BottomMiddle;
                if (Math.Abs(px.X - x2) <= size && Math.Abs(px.Y - y2) <= size) return ResizeAnchor.BottomRight;

                return ResizeAnchor.None;
            }

            public override void OnPointerMove(ToolContext ctx, Point viewPos)
            {
                var px = ctx.ToPixel(viewPos);

                // 💡 一段独立：根据鼠标所在的句柄更新光标形状
                if (_textBox != null)
                {
                    var anchor = HitTestTextboxHandle(px); // 只检测，不缩放
                    switch (anchor)
                    {
                        case ResizeAnchor.TopLeft:
                        case ResizeAnchor.BottomRight:
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeNWSE;
                            break;
                        case ResizeAnchor.TopRight:
                        case ResizeAnchor.BottomLeft:
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeNESW;
                            break;
                        case ResizeAnchor.LeftMiddle:
                        case ResizeAnchor.RightMiddle:
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeWE;
                            break;
                        case ResizeAnchor.TopMiddle:
                        case ResizeAnchor.BottomMiddle:
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeNS;
                            break;
                        default:
                            // 非句柄位置 → 普通箭头
                            Mouse.OverrideCursor = null;
                            break;
                    }
                }

                // 🧩 真正的缩放逻辑，只有在 _resizing == true 时执行
                if (_resizing && _textBox != null)
                {
                    double dx = px.X - _startMouse.X;
                    double dy = px.Y - _startMouse.Y;

                    switch (_currentAnchor)
                    {
                        case ResizeAnchor.TopLeft:
                            Canvas.SetLeft(_textBox, _startX + dx);
                            Canvas.SetTop(_textBox, _startY + dy);
                            _textBox.Width = Math.Max(1, _startW - dx);
                            _textBox.Height = Math.Max(1, _startH - dy);
                            break;
                        case ResizeAnchor.TopMiddle:
                            Canvas.SetTop(_textBox, _startY + dy);
                            _textBox.Height = Math.Max(1, _startH - dy);
                            break;
                        case ResizeAnchor.TopRight:
                            _textBox.Width = Math.Max(1, _startW + dx);
                            Canvas.SetTop(_textBox, _startY + dy);
                            _textBox.Height = Math.Max(1, _startH - dy);
                            break;
                        case ResizeAnchor.LeftMiddle:
                            Canvas.SetLeft(_textBox, _startX + dx);
                            _textBox.Width = Math.Max(1, _startW - dx);
                            break;
                        case ResizeAnchor.RightMiddle:
                            _textBox.Width = Math.Max(1, _startW + dx);
                            break;
                        case ResizeAnchor.BottomLeft:
                            Canvas.SetLeft(_textBox, _startX + dx);
                            _textBox.Width = Math.Max(1, _startW - dx);
                            _textBox.Height = Math.Max(1, _startH + dy);
                            break;
                        case ResizeAnchor.BottomMiddle:
                            _textBox.Height = Math.Max(1, _startH + dy);
                            break;
                        case ResizeAnchor.BottomRight:
                            _textBox.Width = Math.Max(1, _startW + dx);
                            _textBox.Height = Math.Max(1, _startH + dy);
                            break;
                    }
                    DrawTextboxOverlay(ctx);
                }
            }


            public override void OnPointerDown(ToolContext ctx, Point viewPos)
            {
                a.s("texttool");
                if (_textBox != null)
                {
                    Point p = viewPos;
                    double left = Canvas.GetLeft(_textBox);
                    double top = Canvas.GetTop(_textBox);

                    bool inside = p.X >= left && p.X <= left + _textBox.ActualWidth &&
                                  p.Y >= top && p.Y <= top + _textBox.ActualHeight;

                    if (inside)
                    {
                        // 点击内部 → 选中并进入编辑
                        ctx.EditorOverlay.IsHitTestVisible = true;
                        SelectCurrentBox();
                        return;
                    }
                    else
                    {
                        CommitText(ctx);
                        DeselectCurrentBox(ctx);
                        ctx.EditorOverlay.IsHitTestVisible = false;
                        return;
                    }
                }
                else
                {
                    // 没有编辑框 → 记录起点
                    _startPos = viewPos;
                    _dragging = true;
                }

            }
            private bool IsInsideBorder(Point px)
            {
                if (_textBox == null) return false;

                double x = Canvas.GetLeft(_textBox);
                double y = Canvas.GetTop(_textBox);
                double w = _textBox.ActualWidth;
                double h = _textBox.ActualHeight;
                double borderThickness = 5 / ((MainWindow)System.Windows.Application.Current.MainWindow).zoomscale;

                // 外矩形 (扩大边框宽度)
                bool inOuter = px.X >= x - borderThickness &&
                               px.X <= x + w + borderThickness &&
                               px.Y >= y - borderThickness &&
                               px.Y <= y + h + borderThickness;

                // 内矩形 (缩小边框宽度)
                bool inInner = px.X >= x + borderThickness &&
                               px.X <= x + w - borderThickness &&
                               px.Y >= y + borderThickness &&
                               px.Y <= y + h - borderThickness;
                // 必须在外矩形内 && 不在内矩形内 → 才是边框区域
                return inOuter && !inInner;
            }
            public override void OnPointerUp(ToolContext ctx, Point viewPos)
            {

                if (((MainWindow)System.Windows.Application.Current.MainWindow)._router.CurrentTool != ((MainWindow)System.Windows.Application.Current.MainWindow)._tools.Text) return;
                if (_resizing) _resizing = false;// return;

                if (_dragging && _textBox == null)
                {
                    if (lag > 0)
                    {
                        lag -= 1;
                        return;
                    }
                    _dragging = false;

                    _textBox = CreateTextBox(ctx, _startPos.X, _startPos.Y);
                    _textBox.Width = 500;
                    _textBox.MinHeight = 20;
                    _textBox.Height = Double.NaN;
                    // ⬇️ 通知主窗口显示状态栏

                    ctx.EditorOverlay.Visibility = Visibility.Visible;
                    ctx.EditorOverlay.IsHitTestVisible = true;
                    Canvas.SetZIndex(ctx.EditorOverlay, 999);
                    ctx.EditorOverlay.Children.Add(_textBox);


                    ((MainWindow)System.Windows.Application.Current.MainWindow).ShowTextToolbarFor(_textBox);


                    // 绘制虚线框和8个句柄 ⚡⚡
                    _textBox.Loaded += (s, e) =>
                    {
                        DrawTextboxOverlay(ctx); // 已布局完成
                    };

                    ctx.EditorOverlay.PreviewMouseUp += (s, e) =>
                    {
                        Point pos = e.GetPosition(ctx.EditorOverlay);
                        OnPointerUp(ctx, pos);
                    };


                    ctx.EditorOverlay.PreviewMouseMove += (s, e) =>
                    {
                        Point pos = e.GetPosition(ctx.EditorOverlay);
                        OnPointerMove(ctx, pos);
                    };

                    // 👉 在这里添加 PreviewMouseDown 事件绑定
                    ctx.EditorOverlay.PreviewMouseDown += (s, e) =>
                    {
                        Point pos = e.GetPosition(ctx.EditorOverlay);

                        var anchor = HitTestTextboxHandle(ctx.ToPixel(pos));
                        if (anchor != ResizeAnchor.None)
                        {

                            _resizing = true;
                            _currentAnchor = anchor;
                            _startMouse = ctx.ToPixel(pos);
                            _startW = _textBox.ActualWidth;
                            _startH = _textBox.ActualHeight;
                            _startX = Canvas.GetLeft(_textBox);
                            _startY = Canvas.GetTop(_textBox);

                            e.Handled = true; // 防止 TextBox 获取点击焦点
                        }
                        else
                        {
                            // 点击边框区域时启用拖动整个 TextBox
                            if (IsInsideBorder(ctx.ToPixel(pos)))
                            {

                                _dragging = true;
                                _startMouse = ctx.ToPixel(viewPos);
                                _startX = Canvas.GetLeft(_textBox);
                                _startY = Canvas.GetTop(_textBox);
                            }
                            else
                                OnPointerDown(ctx, pos);
                        }


                    };
                    _textBox.PreviewKeyDown += (s, e) =>
                    {
                        if (e.Key == Key.Delete)
                        {
                            CommitText(ctx);
                            ctx.EditorOverlay.Children.Remove(_textBox);
                            _textBox = null;
                            ctx.EditorOverlay.IsHitTestVisible = false;
                            e.Handled = true;
                        }
                    };

                    _textBox.Focusable = true;
                    _textBox.Loaded += (s, e) => _textBox.Focus();
                }
            }



            private void SelectCurrentBox()
            {
                if (_textBox != null)
                {
                    Keyboard.Focus(_textBox);
                    _textBox.Focus();
                }
            }

            private void DeselectCurrentBox(ToolContext ctx)
            {
                if (_textBox != null)
                {
                    ctx.EditorOverlay.Children.Remove(_textBox);
                    _textBox = null;
                }
            }
            private System.Windows.Controls.TextBox CreateTextBox(ToolContext ctx, double x, double y)
            {
                var tb = new System.Windows.Controls.TextBox
                {
                    FontSize = 16,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(ctx.PenColor),
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent
                };

                Canvas.SetLeft(tb, x);
                Canvas.SetTop(tb, y);
                return tb;
            }


            public void CommitText(ToolContext ctx)
            {
                if (_textBox == null || string.IsNullOrWhiteSpace(_textBox.Text))
                    return;
                double x = Canvas.GetLeft(_textBox);
                double y = Canvas.GetTop(_textBox);
                var dpiInfo = VisualTreeHelper.GetDpi(_textBox);
                ctx.Undo.BeginStroke();
                ctx.Undo.AddDirtyRect(_textRect);
                double pixelsPerDip = VisualTreeHelper.GetDpi(ctx.ViewElement).PixelsPerDip;
                // 将文字渲染到位图
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawText(
                        new FormattedText(
                            _textBox.Text,
                            System.Globalization.CultureInfo.CurrentCulture,
                            System.Windows.FlowDirection.LeftToRight,
                            new Typeface(_textBox.FontFamily, _textBox.FontStyle, _textBox.FontWeight, _textBox.FontStretch),
                            _textBox.FontSize,
                            _textBox.Foreground,
                            pixelsPerDip
                        ),
                        new Point(0, 0));

                }
                TextOptions.SetTextRenderingMode(_textBox, TextRenderingMode.ClearType);
                TextOptions.SetTextFormattingMode(_textBox, TextFormattingMode.Display);

                // 渲染为图像并写入 Surface
                var bmp = new RenderTargetBitmap((int)_textBox.ActualWidth, (int)_textBox.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(visual);

                var wb = new WriteableBitmap(bmp);
                int stride = wb.PixelWidth * 4;
                var pixels = new byte[wb.PixelHeight * stride];
                wb.CopyPixels(pixels, stride, 0);

                ctx.Undo.BeginStroke();
                ctx.Undo.AddDirtyRect(new Int32Rect((int)x, (int)y, wb.PixelWidth, wb.PixelHeight));
                ctx.Surface.WriteRegion(new Int32Rect((int)x, (int)y, wb.PixelWidth, wb.PixelHeight), pixels, stride, false);
                ctx.Undo.CommitStroke();
                ((MainWindow)System.Windows.Application.Current.MainWindow).HideTextToolbar();
                // 从 UI 移除 TextBox
                ctx.SelectionOverlay.Children.Clear();
                ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
                ctx.EditorOverlay.Children.Remove(_textBox);
                ((MainWindow)System.Windows.Application.Current.MainWindow).SetUndoRedoButtonState();
                lag = 1;
            }
        }

    }
}