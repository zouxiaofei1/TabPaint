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
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;

//
//TabPaint主程序
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void FontSettingChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_router.CurrentTool is TextTool textTool)
            {
               // s(1);
                textTool.UpdateCurrentTextBoxAttributes();
            }
            
        }
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
                if (((MainWindow)System.Windows.Application.Current.MainWindow).IsViewMode) return;
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
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;

                var tb = new System.Windows.Controls.TextBox
                {
                    FontSize = 24, // 默认值，会被 ApplyTextSettings 覆盖
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(ctx.PenColor),
                    // 初始透明
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent,
                    Padding = new Thickness(5) // 给一点内边距好看
                };

                Canvas.SetLeft(tb, x);
                Canvas.SetTop(tb, y);

                // 立即应用当前工具栏的设置
                mw.ApplyTextSettings(tb);

                return tb;
            }
            public void UpdateCurrentTextBoxAttributes()
            {
                if (_textBox == null) return;

                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;

                // 1. 应用新的字体、大小、颜色等设置
                mw.ApplyTextSettings(_textBox);

                // 2. 【核心修复】强制 WPF 立即更新布局
                // 这样 _textBox.ActualWidth 和 ActualHeight 才会变成应用新字体后的数值
                _textBox.UpdateLayout();

                // 3. 使用更新后的尺寸重绘虚线框和手柄
                DrawTextboxOverlay(mw._ctx);
            }
            private void CleanUpUI(ToolContext ctx)
            {
                ((MainWindow)System.Windows.Application.Current.MainWindow).HideTextToolbar();
                ctx.SelectionOverlay.Children.Clear();
                ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
                if (ctx.EditorOverlay.Children.Contains(_textBox))
                    ctx.EditorOverlay.Children.Remove(_textBox);

                ((MainWindow)System.Windows.Application.Current.MainWindow).SetUndoRedoButtonState();
                _textBox = null;
                lag = 1;
            }
            private unsafe void AlphaBlendBatch(byte[] sourcePixels, byte[] destPixels, int width, int height, int stride, int sourceStartIdx)
            {
                fixed (byte* pSrcBase = sourcePixels)
                fixed (byte* pDstBase = destPixels)
                {
                    for (int row = 0; row < height; row++)
                    {
                        // 计算当前行的指针
                        byte* pSrcRow = pSrcBase + sourceStartIdx + (row * stride);
                        byte* pDstRow = pDstBase + (row * stride);

                        for (int col = 0; col < width; col++)
                        {

                            byte srcA = pSrcRow[3];

                            // 优化：如果源像素完全透明，跳过（保留背景）
                            if (srcA == 0)
                            {
                                pSrcRow += 4;
                                pDstRow += 4;
                                continue;
                            }

                            // 优化：如果源像素完全不透明，直接覆盖
                            if (srcA == 255)
                            {
                                *(int*)pDstRow = *(int*)pSrcRow; // 直接拷贝 4 字节
                                pSrcRow += 4;
                                pDstRow += 4;
                                continue;
                            }

                            // Alpha 混合计算 (使用整数运算优化)
                            int alphaFactor = 255 - srcA;

                            pDstRow[0] = (byte)(pSrcRow[0] + (pDstRow[0] * alphaFactor) / 255); // B
                            pDstRow[1] = (byte)(pSrcRow[1] + (pDstRow[1] * alphaFactor) / 255); // G
                            pDstRow[2] = (byte)(pSrcRow[2] + (pDstRow[2] * alphaFactor) / 255); // R
                            pDstRow[3] = (byte)(pSrcRow[3] + (pDstRow[3] * alphaFactor) / 255); // A (背景的 Alpha 也要被覆盖)

                            pSrcRow += 4;
                            pDstRow += 4;
                        }
                    }
                }
            }
            public void CommitText(ToolContext ctx)
            {
                if (_textBox == null) return;
                if (string.IsNullOrWhiteSpace(_textBox.Text))
                {
                    // 清理逻辑保持不变
                    ((MainWindow)System.Windows.Application.Current.MainWindow).HideTextToolbar();
                    ctx.SelectionOverlay.Children.Clear();
                    ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
                    if (ctx.EditorOverlay.Children.Contains(_textBox))
                        ctx.EditorOverlay.Children.Remove(_textBox);
                    lag = 1;
                    return;
                }
                double tweakX = 2.0;
                double tweakY = 1.0;
                // 1. 获取位置信息
                double tbLeft = Canvas.GetLeft(_textBox);
                double tbTop = Canvas.GetTop(_textBox);
                double tbWidth = _textBox.ActualWidth;
                double tbHeight = _textBox.ActualHeight;

                // 2. 构建 FormattedText (保持之前的 96 DPI 修复)
                var formattedText = new FormattedText(
                    _textBox.Text,
                    CultureInfo.CurrentCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new Typeface(_textBox.FontFamily, _textBox.FontStyle, _textBox.FontWeight, _textBox.FontStretch),
                    _textBox.FontSize,
                    _textBox.Foreground,
                    96.0 // 强制 96 DPI，确保像素大小与逻辑大小 1:1
                )
                {
                    MaxTextWidth = Math.Max(1, tbWidth - _textBox.Padding.Left - _textBox.Padding.Right),
                    MaxTextHeight = double.MaxValue,
                    Trimming = TextTrimming.None,
                    TextAlignment = _textBox.TextAlignment
                };
                formattedText.SetTextDecorations(_textBox.TextDecorations);

                // 3. 渲染到 Visual
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    // 如果文本框有背景色，画背景
                    if (_textBox.Background is SolidColorBrush bgBrush && bgBrush.Color.A > 0)
                    {
                        dc.DrawRectangle(bgBrush, null, new Rect(0, 0, tbWidth, tbHeight));
                    }

                    // 使用 Grayscale 渲染文本，避免 ClearType 在透明背景上产生彩色边缘
                    TextOptions.SetTextRenderingMode(visual, TextRenderingMode.Grayscale);
                    TextOptions.SetTextFormattingMode(visual, TextFormattingMode.Display);

                    dc.DrawText(formattedText, new Point(_textBox.Padding.Left + tweakX, _textBox.Padding.Top + tweakY));
                }

                int width = (int)Math.Ceiling(tbWidth);
                int height = (int)Math.Ceiling(tbHeight);
                if (width <= 0 || height <= 0) return;

                // 4. 生成源像素 (文字层)
                var rtb = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);
                rtb.Render(visual);

                int stride = width * 4;
                byte[] sourcePixels = new byte[height * stride];
                rtb.CopyPixels(sourcePixels, stride, 0);

                // 5. 准备混合区域和目标像素 (画布层)
                // 确保坐标为整数
                int x = (int)tbLeft;
                int y = (int)tbTop;

                var writeableBitmap = ctx.Surface.Bitmap; // 假设 Surface 就是 WriteableBitmap，如果不是请获取它
                int canvasWidth = writeableBitmap.PixelWidth;
                int canvasHeight = writeableBitmap.PixelHeight;

                // 计算实际可操作的矩形区域 (Clip)
                int safeX = Math.Max(0, x);
                int safeY = Math.Max(0, y);
                int safeRight = Math.Min(canvasWidth, x + width);
                int safeBottom = Math.Min(canvasHeight, y + height);
                int safeW = safeRight - safeX;
                int safeH = safeBottom - safeY;

                if (safeW <= 0 || safeH <= 0)
                {
                    // 完全在画布外，不需要处理
                    CleanUpUI(ctx);
                    return;
                }
                byte[] destPixels = new byte[safeH * stride]; 
                Int32Rect dirtyRect = new Int32Rect(safeX, safeY, safeW, safeH);
                writeableBitmap.CopyPixels(dirtyRect, destPixels, stride, 0);

                int sourceOffsetX = safeX - x;
                int sourceOffsetY = safeY - y;
                int sourceStartIndex = sourceOffsetY * stride + sourceOffsetX * 4;

                AlphaBlendBatch(sourcePixels, destPixels, safeW, safeH, stride, sourceStartIndex);

                // 8. 写回混合后的结果
                ctx.Undo.BeginStroke();
                ctx.Undo.AddDirtyRect(dirtyRect);
                // 注意：destPixels 现在包含了混合后的结果
                writeableBitmap.WritePixels(dirtyRect, destPixels, stride, 0);
                ctx.Undo.CommitStroke();

                // UI 清理
                CleanUpUI(ctx);
            }

        }

    }
}