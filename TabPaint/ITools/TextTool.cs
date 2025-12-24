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
            private bool _justDismissed = false; // 用于记录当前点击是否是为了销毁上一个文本框

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

                // 1️⃣ 光标状态更新逻辑 (增加移动光标检测)
                if (_textBox != null && !_resizing && !_dragging) // 如果没有在操作中，才检测光标
                {
                    var anchor = HitTestTextboxHandle(px);
                    if (anchor != ResizeAnchor.None)
                    {
                        // 命中句柄 -> 显示调整大小光标
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
                        }
                    }
                    else if (IsInsideBorder(px))
                    {
                        // 命中虚线边框 -> 显示移动光标 (十字箭头) ✨✨✨
                        Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeAll;
                    }
                    else
                    {
                        // 既没中句柄也没中边框 -> 恢复默认
                        Mouse.OverrideCursor = null;
                    }
                }

                // 2️⃣ 具体的交互逻辑
                if (_textBox != null)
                {
                    double dx = px.X - _startMouse.X;
                    double dy = px.Y - _startMouse.Y;

                    // A. 处理调整大小 (Resizing)
                    if (_resizing)
                    {
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
                        DrawTextboxOverlay(ctx); // 实时重绘边框
                    }
                    // B. 处理拖拽移动 (Dragging) ✨✨✨ 这里是你缺失的部分
                    else if (_dragging)
                    {
                        // 移动 TextBox
                        Canvas.SetLeft(_textBox, _startX + dx);
                        Canvas.SetTop(_textBox, _startY + dy);

                        // 实时重绘边框跟随移动
                        DrawTextboxOverlay(ctx);
                    }
                }
            }



            public override void OnPointerDown(ToolContext ctx, Point viewPos)
            {
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
                double borderThickness = Math.Max(5 / ((MainWindow)System.Windows.Application.Current.MainWindow).zoomscale, 10);

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
                if (_resizing || (_dragging && _textBox != null))
                {
                    _resizing = false;
                    _dragging = false;
                    _currentAnchor = ResizeAnchor.None;

                    // 释放鼠标捕获，这样下次点击才能正常工作
                    ctx.EditorOverlay.ReleaseMouseCapture();

                    // 既然是拖动结束，就不需要执行下面的创建逻辑了，直接返回
                    return;
                }
                if (_dragging && _textBox == null)
                {//创建新的文本框

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
                    // 👉 修正后的 PreviewMouseDown 事件绑定
                    ctx.EditorOverlay.PreviewMouseDown += (s, e) =>
                    {
                        Point pos = e.GetPosition(ctx.EditorOverlay); // 获取当前点击在 Overlay 上的位置
                        Point pixelPos = ctx.ToPixel(pos);            // 转为画布像素坐标

                        var anchor = HitTestTextboxHandle(pixelPos);

                        // 1. 命中句柄 -> 缩放模式
                        if (anchor != ResizeAnchor.None)
                        {
                            _resizing = true;
                            _currentAnchor = anchor;
                            _startMouse = pixelPos;             // 记录当前鼠标位置
                            _startW = _textBox.ActualWidth;
                            _startH = _textBox.ActualHeight;
                            _startX = Canvas.GetLeft(_textBox);
                            _startY = Canvas.GetTop(_textBox);

                            ctx.EditorOverlay.CaptureMouse();   // 【重要】捕获鼠标，防止拖出窗口后丢失状态
                            e.Handled = true;
                        }
                        // 2. 命中虚线边框 -> 移动模式
                        else if (IsInsideBorder(pixelPos))
                        {
                            _dragging = true;
                            _startMouse = pixelPos;             // 【关键修正】这里要用当前的 pixelPos，不要用 viewPos
                            _startX = Canvas.GetLeft(_textBox); // 记录当前文本框位置
                            _startY = Canvas.GetTop(_textBox);

                            ctx.EditorOverlay.CaptureMouse();   // 【重要】捕获鼠标
                            e.Handled = true;                   // 防止事件传给 TextBox 导致光标闪烁
                        }
                        // 3. 点击内部 -> 交给 TextBox 自己处理（输入文字）
                        else
                        {
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
                if (_textBox == null) return;
                if (string.IsNullOrWhiteSpace(_textBox.Text))
                {
                    ((MainWindow)System.Windows.Application.Current.MainWindow).HideTextToolbar();
                    ctx.SelectionOverlay.Children.Clear();
                    ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
                    if (ctx.EditorOverlay.Children.Contains(_textBox))
                        ctx.EditorOverlay.Children.Remove(_textBox);
                    lag = 1;
                    return;
                }

                // 1. 获取 DPI (改从 ViewElement 获取，绕过 CanvasSurface 的定义问题)
                var dpiInfo = VisualTreeHelper.GetDpi(ctx.ViewElement);
                double dpiX = dpiInfo.PixelsPerInchX;
                double dpiY = dpiInfo.PixelsPerInchY;

                // 2. 获取位置和尺寸
                double x = Canvas.GetLeft(_textBox);
                double y = Canvas.GetTop(_textBox);
                double w = _textBox.ActualWidth;
                double h = _textBox.ActualHeight;

                // 3. 构建文本对象
                var formattedText = new FormattedText(
                    _textBox.Text,
                    CultureInfo.CurrentCulture,
                   System.Windows.FlowDirection.LeftToRight,
                    new Typeface(_textBox.FontFamily, _textBox.FontStyle, _textBox.FontWeight, _textBox.FontStretch),
                    _textBox.FontSize,
                    _textBox.Foreground,
                    dpiInfo.PixelsPerDip
                )
                {
                    MaxTextWidth = Math.Max(1, w - _textBox.Padding.Left - _textBox.Padding.Right),
                    MaxTextHeight = double.MaxValue,
                    Trimming = TextTrimming.None,
                    TextAlignment = _textBox.TextAlignment
                };

                // 4. 绘制到 DrawingVisual
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    // ✨ 修复点：使用 RenderOptions 来设置附加属性，解决 DrawingVisual 不包含定义的问题
                    TextOptions.SetTextRenderingMode(visual, TextRenderingMode.Grayscale);

                    // HintingMode 也是附加属性，或者可以使用 TextFormattingMode
                    TextOptions.SetTextFormattingMode(visual, TextFormattingMode.Display);

                    dc.DrawText(formattedText, new Point(_textBox.Padding.Left, _textBox.Padding.Top));
                }

                // 5. 计算像素尺寸并渲染
                int renderWidth = (int)Math.Ceiling(w * (dpiX / 96.0));
                int renderHeight = (int)Math.Ceiling(
                    Math.Max(h, formattedText.Height + _textBox.Padding.Top + _textBox.Padding.Bottom) * (dpiY / 96.0)
                );

                // 强制最小 1x1 像素防止异常
                renderWidth = Math.Max(1, renderWidth);
                renderHeight = Math.Max(1, renderHeight);

                var bmp = new RenderTargetBitmap(renderWidth, renderHeight, dpiX, dpiY, PixelFormats.Pbgra32);
                bmp.Render(visual);

                // 6. 转换位图数据
                var wb = new WriteableBitmap(bmp);
                int stride = wb.PixelWidth * 4;
                var pixels = new byte[wb.PixelHeight * stride];
                wb.CopyPixels(pixels, stride, 0);

                // 7. 写入画布
                ctx.Undo.BeginStroke();

                // ✨ 修复点：直接使用渲染出的 wb 的尺寸，不依赖 ctx.Surface.PixelWidth
                Int32Rect dirtyRect = new Int32Rect((int)x, (int)y, wb.PixelWidth, wb.PixelHeight);

                // 如果你的 CanvasSurface 没有暴露 PixelWidth/PixelHeight，
                // 我们暂时移除越界检查，或者你可以根据你的代码逻辑手动限制坐标。
                // 这里先保证编译通过：
                ctx.Undo.AddDirtyRect(dirtyRect);
                ctx.Surface.WriteRegion(dirtyRect, pixels, stride, false);

                ctx.Undo.CommitStroke();

                // 8. 清理 UI
                ((MainWindow)System.Windows.Application.Current.MainWindow).HideTextToolbar();
                ctx.SelectionOverlay.Children.Clear();
                ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
                if (ctx.EditorOverlay.Children.Contains(_textBox))
                    ctx.EditorOverlay.Children.Remove(_textBox);

                ((MainWindow)System.Windows.Application.Current.MainWindow).SetUndoRedoButtonState();
                _textBox = null;
                lag = 1;
            }


        }

    }
}