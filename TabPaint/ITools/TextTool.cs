
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

//
//TEXTtool
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {

        public partial class TextTool : ToolBase
        {
            public void InsertTableIntoCurrentBox(int rows = 3, int cols = 3)
            {
                if (_richTextBox == null) return;
                var table = new Table();
                table.CellSpacing = 0;
                table.BorderBrush = Brushes.Black;
                table.BorderThickness = new Thickness(1);
                for (int x = 0; x < cols; x++) table.Columns.Add(new TableColumn());
                table.RowGroups.Add(new TableRowGroup());
                for (int r = 0; r < rows; r++)
                {
                    var row = new TableRow();
                    table.RowGroups[0].Rows.Add(row);
                    for (int c = 0; c < cols; c++)
                    {
                        var cell = new TableCell(new Paragraph(new Run("")));
                        cell.BorderBrush = Brushes.Gray;
                        cell.BorderThickness = new Thickness(0.5);
                        cell.Padding = new Thickness(5);
                        row.Cells.Add(cell);
                    }
                }

                var selection = _richTextBox.Selection;
                if (!selection.IsEmpty) selection.Text = ""; // 删除选中文本

                TextPointer ptr = selection.Start;
                Paragraph curPara = ptr.Paragraph;

                if (curPara != null)
                {
                    if (curPara.Parent is FlowDocument doc) doc.Blocks.InsertAfter(curPara, table);
                    else if (curPara.Parent is Section sec) sec.Blocks.InsertAfter(curPara, table);
                    else _richTextBox.Document.Blocks.Add(table);
                    TextPointer cellPtr = table.RowGroups[0].Rows[0].Cells[0].ContentStart;
                    _richTextBox.CaretPosition = cellPtr;
                }
                else _richTextBox.Document.Blocks.Add(table);
                _richTextBox.Focus();
            }


            public void ApplySelectionAttributes()
            {
                if (_richTextBox == null) return;
                var mw = MainWindow.GetCurrentInstance();
                var selection = _richTextBox.Selection;
                if (mw.TextMenu.SubscriptBtn.IsChecked == true)  // 1. 上下标
                    selection.ApplyPropertyValue(Inline.BaselineAlignmentProperty, BaselineAlignment.Subscript);
                else if (mw.TextMenu.SuperscriptBtn.IsChecked == true)
                    selection.ApplyPropertyValue(Inline.BaselineAlignmentProperty, BaselineAlignment.Superscript);
                else
                    selection.ApplyPropertyValue(Inline.BaselineAlignmentProperty, BaselineAlignment.Baseline);
                if (mw.TextMenu.HighlightBtn.IsChecked == true)// 2. 高亮
                    selection.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Yellow);
                else
                    selection.ApplyPropertyValue(TextElement.BackgroundProperty, null); // 清除高亮

                // 3. 字体/粗体/斜体同步
                selection.ApplyPropertyValue(TextElement.FontWeightProperty, (mw.TextMenu.BoldBtn.IsChecked == true) ? FontWeights.Bold : FontWeights.Normal);
                selection.ApplyPropertyValue(TextElement.FontStyleProperty, (mw.TextMenu.ItalicBtn.IsChecked == true) ? FontStyles.Italic : FontStyles.Normal);
                var decors = new TextDecorationCollection(); // 4. 装饰线
                if (mw.TextMenu.UnderlineBtn.IsChecked == true) decors.Add(TextDecorations.Underline);
                if (mw.TextMenu.StrikeBtn.IsChecked == true) decors.Add(TextDecorations.Strikethrough);
                selection.ApplyPropertyValue(Inline.TextDecorationsProperty, decors);
                ApplyTextSettings(_richTextBox);
            }

            public override void Cleanup(ToolContext ctx)
            {
                MainWindow mw = MainWindow.GetCurrentInstance();
                if (_richTextBox != null && ctx.EditorOverlay.Children.Contains(_richTextBox))
                {
                    ctx.EditorOverlay.Children.Remove(_richTextBox);
                    _richTextBox = null;
                }
                if (ctx.SelectionOverlay != null)
                {
                    ctx.SelectionOverlay.Children.Clear();
                    ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
                }
                mw.HideTextToolbar();
                _dragging = false;
                _resizing = false;
                _currentAnchor = ResizeAnchor.None;
                _textRect = new Int32Rect();
                lag = 0;
                Mouse.OverrideCursor = null;
                if (mw._canvasResizer != null) mw._canvasResizer.SetHandleVisibility(true);
            }
            public void GiveUpText(ToolContext ctx)
            {
                Cleanup(ctx);
                ctx.Undo.Undo();
                ctx.Undo._redo.Pop();
                (MainWindow.GetCurrentInstance()).SetUndoRedoButtonState();

            }
            public override void SetCursor(ToolContext ctx)
            {
                System.Windows.Input.Mouse.OverrideCursor = null;
                if (ctx.ViewElement != null)  ctx.ViewElement.Cursor = this.Cursor;
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
                MainWindow mw = MainWindow.GetCurrentInstance();
                if (_richTextBox == null) return;

                double invScale = 1 / mw.zoomscale;
                var overlay = ctx.SelectionOverlay;
                overlay.Children.Clear();

                double x = Canvas.GetLeft(_richTextBox);
                double y = Canvas.GetTop(_richTextBox);
                double w = _richTextBox.ActualWidth;
                double h = _richTextBox.ActualHeight;
                var rect = new Int32Rect((int)x, (int)y, (int)w, (int)h);

                var outlineBlack = new System.Windows.Shapes.Rectangle  // 黑色虚线框
                {
                    Stroke = Brushes.Black,
                    StrokeDashArray = new DoubleCollection { 4, 4 },
                    StrokeThickness = invScale * AppConsts.TextToolOutlineThickness,
                    Width = rect.Width,
                    Height = rect.Height
                };
                Canvas.SetLeft(outlineBlack, rect.X);
                Canvas.SetTop(outlineBlack, rect.Y);
                overlay.Children.Add(outlineBlack);

                var outlineWhite = new System.Windows.Shapes.Rectangle  // 白色虚线框，错位后形成黑白交替线
                {
                    Stroke = Brushes.White,
                    StrokeDashArray = new DoubleCollection { 4, 4 },
                    StrokeDashOffset = 4,
                    StrokeThickness = invScale * AppConsts.TextToolOutlineThickness,
                    Width = rect.Width,
                    Height = rect.Height
                };
                Canvas.SetLeft(outlineWhite, rect.X);
                Canvas.SetTop(outlineWhite, rect.Y);
                overlay.Children.Add(outlineWhite);

                // 八个句柄
                foreach (var p in GetHandlePositions(rect))
                {
                    var handle = new System.Windows.Shapes.Rectangle
                    {
                        Width = AppConsts.SelectToolHandleSize * invScale,
                        Height = AppConsts.SelectToolHandleSize * invScale,
                        Fill = Brushes.White,
                        Stroke = mw._darkBackgroundBrush,
                        StrokeThickness = invScale
                    };
                    Canvas.SetLeft(handle, p.X - AppConsts.SelectToolHandleSize * invScale / 2);
                    Canvas.SetTop(handle, p.Y - AppConsts.SelectToolHandleSize * invScale / 2);
                    overlay.Children.Add(handle);
                }
                overlay.IsHitTestVisible = false;
                overlay.Visibility = Visibility.Visible;
                if (mw._canvasResizer != null) mw._canvasResizer.SetHandleVisibility(false);
            }
            private ResizeAnchor HitTestTextboxHandle(Point px)// 判断是否点击到句柄
            {
                if (_richTextBox == null) return ResizeAnchor.None;
                double size = AppConsts.TextToolHandleHitTestSize / (MainWindow.GetCurrentInstance()).zoomscale;
                double x1 = Canvas.GetLeft(_richTextBox);
                double y1 = Canvas.GetTop(_richTextBox);
                double x2 = x1 + _richTextBox.ActualWidth;
                double y2 = y1 + _richTextBox.ActualHeight;
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

            private double GetTextboxBorderHitThickness()
            {
                double zoom = Math.Max((MainWindow.GetCurrentInstance()).zoomscale, AppConsts.MinZoom);
                double scaledThickness = AppConsts.TextToolBorderThicknessMin / zoom;
                return Math.Clamp(scaledThickness, AppConsts.TextToolBorderThicknessMin, AppConsts.TextToolBorderThicknessMax);
            }

            public override void OnPointerMove(ToolContext ctx, Point viewPos, float pressure = 1.0f)
            {
                if ((_dragging || _resizing) && Mouse.LeftButton == MouseButtonState.Released)
                {
                    _dragging = false;
                    _resizing = false;
                    _currentAnchor = ResizeAnchor.None;
                    if (ctx.EditorOverlay.IsMouseCaptured)
                    {
                        ctx.EditorOverlay.ReleaseMouseCapture();
                    }
                    Mouse.OverrideCursor = null;
                    return; // 直接退出，不执行后面的移动逻辑
                }
                var px = ctx.ToPixel(viewPos);

                // 1️⃣ 光标状态更新逻辑 (增加移动光标检测)
                if (_richTextBox != null && !_resizing && !_dragging) // 如果没有在操作中，才检测光标
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
                    else if (IsInsideBorder(px)) Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeAll;
                    else
                    {
                        // 既没中句柄也没中边框 -> 恢复默认
                        Mouse.OverrideCursor = null;
                    }
                }

                // 2️⃣ 具体的交互逻辑
                if (_richTextBox != null)
                {
                    double dx = px.X - _startMouse.X;
                    double dy = px.Y - _startMouse.Y;
                    if (_resizing)
                    {
                        double rightEdge = _startX + _startW;
                        double bottomEdge = _startY + _startH;
                        switch (_currentAnchor)
                        {
                            case ResizeAnchor.TopLeft:
                                {
                                    double newW = Math.Max(1, _startW - dx);
                                    _richTextBox.Width = newW;
                                    Canvas.SetLeft(_richTextBox, rightEdge - newW);
                                    double newH = Math.Max(1, _startH - dy);
                                    _richTextBox.Height = newH;
                                    Canvas.SetTop(_richTextBox, bottomEdge - newH);
                                }
                                break;

                            case ResizeAnchor.TopMiddle:
                                {
                                    double newH = Math.Max(1, _startH - dy);
                                    _richTextBox.Height = newH;
                                    Canvas.SetTop(_richTextBox, bottomEdge - newH);
                                }
                                break;

                            case ResizeAnchor.TopRight:
                                {
                                    _richTextBox.Width = Math.Max(1, _startW + dx);
                                    double newH = Math.Max(1, _startH - dy);
                                    _richTextBox.Height = newH;
                                    Canvas.SetTop(_richTextBox, bottomEdge - newH);
                                }
                                break;

                            case ResizeAnchor.LeftMiddle:
                                {
                                    double newW = Math.Max(1, _startW - dx);
                                    _richTextBox.Width = newW;
                                    Canvas.SetLeft(_richTextBox, rightEdge - newW);
                                }
                                break;

                            case ResizeAnchor.RightMiddle:
                                _richTextBox.Width = Math.Max(1, _startW + dx);
                                break;

                            case ResizeAnchor.BottomLeft:
                                {
                                    double newW = Math.Max(1, _startW - dx);
                                    _richTextBox.Width = newW;
                                    Canvas.SetLeft(_richTextBox, rightEdge - newW);
                                    _richTextBox.Height = Math.Max(1, _startH + dy);
                                }
                                break;

                            case ResizeAnchor.BottomMiddle:
                                _richTextBox.Height = Math.Max(1, _startH + dy);
                                break;

                            case ResizeAnchor.BottomRight:
                                _richTextBox.Width = Math.Max(1, _startW + dx);
                                _richTextBox.Height = Math.Max(1, _startH + dy);
                                break;
                        }
                        DrawTextboxOverlay(ctx); // 实时重绘边框
                    }
                    else if (_dragging)
                    {
                        // 移动 TextBox
                        Canvas.SetLeft(_richTextBox, _startX + dx);
                        Canvas.SetTop(_richTextBox, _startY + dy);

                        // 实时重绘边框跟随移动
                        DrawTextboxOverlay(ctx);
                    }
                }
            }

            private void AutoFitContent(System.Windows.Controls.RichTextBox rtb)
            {
                if (rtb == null) return;
                rtb.Width = double.NaN;
                rtb.Height = double.NaN;

                rtb.MinWidth = 50;
                rtb.MaxWidth = AppConsts.MaxTextBoxWidth;

                rtb.UpdateLayout();
                DrawTextboxOverlay((MainWindow.GetCurrentInstance())._ctx);
            }


            public override void OnPointerDown(ToolContext ctx, Point viewPos, float pressure = 1.0f)
            {
                MainWindow mw = (MainWindow.GetCurrentInstance());
                if (mw.IsViewMode) return;

                // 如果文本框存在，优先检测交互逻辑
                if (_richTextBox != null)
                {
                    Point pixelPos = ctx.ToPixel(viewPos); // 转换为像素坐标用于检测
                    var anchor = HitTestTextboxHandle(pixelPos);
                    if (anchor != ResizeAnchor.None)
                    {
                        _resizing = true;
                        _currentAnchor = anchor;
                        _startMouse = pixelPos; // 记录鼠标像素位置
                        _startW = _richTextBox.ActualWidth;
                        _startH = _richTextBox.ActualHeight;
                        _startX = Canvas.GetLeft(_richTextBox);
                        _startY = Canvas.GetTop(_richTextBox);

                        // 捕获鼠标以保证拖动流畅
                        if (ctx.EditorOverlay.IsHitTestVisible)
                            ctx.EditorOverlay.CaptureMouse();
                        return;
                    }

                    // 2. 检测是否点击了【边框区域】 (Move / Drag)
                    if (IsInsideBorder(pixelPos))
                    {
                        _dragging = true;
                        _startMouse = pixelPos;
                        _startX = Canvas.GetLeft(_richTextBox);
                        _startY = Canvas.GetTop(_richTextBox);

                        if (ctx.EditorOverlay.IsHitTestVisible)
                            ctx.EditorOverlay.CaptureMouse();
                        return;
                    }
                    double left = Canvas.GetLeft(_richTextBox);
                    double top = Canvas.GetTop(_richTextBox);
                    bool inside = pixelPos.X >= left && pixelPos.X <= left + _richTextBox.ActualWidth &&
                                  pixelPos.Y >= top && pixelPos.Y <= top + _richTextBox.ActualHeight;

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
                        //if (_richTextBox == null)
                        //{
                        //    _startPos = viewPos;
                        //    _dragging = true; // 这里的 dragging 是指“拖拽创建新框”
                        //}
                        //lag = 2;
                        return;
                    }
                }
                else
                {
                    // 没有编辑框 → 记录起点，准备创建新框
                    _startPos = viewPos;
                    _dragging = true;
                }
            }
            private bool HasImagesOrTables(System.Windows.Controls.RichTextBox rtb)
            {
                // 简单遍历 Block 检查是否有 Table 或 BlockUIContainer
                foreach (var block in rtb.Document.Blocks)
                {
                    if (block is Table || block is BlockUIContainer) return true;
                }
                return false;
            }

            private bool IsInsideBorder(Point px)
            {
                if (_richTextBox == null) return false;

                double x = Canvas.GetLeft(_richTextBox);
                double y = Canvas.GetTop(_richTextBox);
                double w = _richTextBox.ActualWidth;
                double h = _richTextBox.ActualHeight;
                double borderThickness = GetTextboxBorderHitThickness();
                bool inOuter = px.X >= x - borderThickness &&
                               px.X <= x + w + borderThickness &&
                               px.Y >= y - borderThickness &&
                               px.Y <= y + h + borderThickness;
                bool inInner = px.X >= x + borderThickness &&
                               px.X <= x + w - borderThickness &&
                               px.Y >= y + borderThickness &&
                               px.Y <= y + h - borderThickness;
                // 必须在外矩形内 && 不在内矩形内 → 才是边框区域
                return inOuter && !inInner;
            }

            public override void OnPointerUp(ToolContext ctx, Point viewPos, float pressure = 1.0f)
            {
                MainWindow mw = MainWindow.GetCurrentInstance();
                if (mw._router.CurrentTool != mw._tools.Text) return;
                if (_resizing || (_dragging && _richTextBox != null))
                {
                    _resizing = false;
                    _dragging = false;
                    _currentAnchor = ResizeAnchor.None;
                    ctx.EditorOverlay.ReleaseMouseCapture();
                    return;
                }
                if (_dragging && _richTextBox == null)
                {
                    if (lag > 0)
                    {
                        lag -= 1;
                        _dragging = false; // 【关键修复2】必须重置拖拽状态，否则会卡在创建模式
                        return;
                    }

                    _dragging = false;
                    _richTextBox = CreateRichTextBox(ctx, _startPos.X, _startPos.Y);
                    _richTextBox.Width = AppConsts.DefaultTextBoxWidth;
                    _richTextBox.MinHeight = AppConsts.MinTextBoxHeight;

                    // 调用统一的事件绑定
                    SetupRichTextBoxEvents(ctx, _richTextBox);
                    ctx.EditorOverlay.Visibility = Visibility.Visible;
                    ctx.EditorOverlay.IsHitTestVisible = true;
                    Canvas.SetZIndex(ctx.EditorOverlay, AppConsts.EditorOverlayZIndex);
                    ctx.EditorOverlay.Children.Add(_richTextBox);
                    MainWindow.GetCurrentInstance().ShowTextToolbarFor(_richTextBox);
                    _richTextBox.Focus();
                }
            }
            private void SetupRichTextBoxEvents(ToolContext ctx, System.Windows.Controls.RichTextBox rtb)
            {
                rtb.Loaded += (s, e) => { DrawTextboxOverlay(ctx); rtb.Focus(); };
                rtb.SelectionChanged += (s, e) =>
                {
                    MainWindow.GetCurrentInstance().SyncTextToolbarState(rtb);
                };
                rtb.PreviewKeyDown += (s, e) =>
                {
                    if (e.Key == Key.Delete && rtb.Selection.IsEmpty && new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd).Text.Trim() == "")
                    {
                        CleanUpUI(ctx);
                        e.Handled = true;
                    }
                };
                rtb.TextChanged += (s, e) => { AutoFitContent(rtb); };
                // 【关键修复1】必须在这里挂载 EditorOverlay 的拦截事件，否则新创建的框无法被拖动！
                ctx.EditorOverlay.PreviewMouseUp -= Overlay_PreviewMouseUp;
                ctx.EditorOverlay.PreviewMouseUp += Overlay_PreviewMouseUp;
                ctx.EditorOverlay.PreviewMouseMove -= Overlay_PreviewMouseMove;
                ctx.EditorOverlay.PreviewMouseMove += Overlay_PreviewMouseMove;
                ctx.EditorOverlay.PreviewMouseDown -= Overlay_PreviewMouseDown;
                ctx.EditorOverlay.PreviewMouseDown += Overlay_PreviewMouseDown;
            }
            public void CommitText(ToolContext ctx)
            {
                if (_richTextBox == null) return;

                _richTextBox.CaretBrush = Brushes.Transparent;
                // 清空选区（防止蓝色的选中背景被画进去）
                var end = _richTextBox.Document.ContentEnd;
                _richTextBox.Selection.Select(end, end);
                // 禁止获取焦点
                _richTextBox.Focusable = false;
                _richTextBox.IsReadOnly = true;
                _richTextBox.UpdateLayout();
                string plainText = new TextRange(_richTextBox.Document.ContentStart, _richTextBox.Document.ContentEnd).Text;
                if (string.IsNullOrWhiteSpace(plainText) && !HasImagesOrTables(_richTextBox))
                {
                    CleanUpUI(ctx);
                    lag = 2;
                    return;
                }

                // 获取参数
                double canvasLeft = Canvas.GetLeft(_richTextBox);
                double canvasTop = Canvas.GetTop(_richTextBox);
                int width = (int)Math.Ceiling(_richTextBox.ActualWidth);
                int height = (int)Math.Ceiling(_richTextBox.ActualHeight);

                // 安全检查
                if (width <= 0 || height <= 0) { CleanUpUI(ctx); lag = 2; return; }

                try
                {
                    var rtbBitmap = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);

                    var drawingVisual = new DrawingVisual();
                    using (var context = drawingVisual.RenderOpen())
                    {
                        var brush = new VisualBrush(_richTextBox)
                        {
                            Stretch = Stretch.None,
                            TileMode = TileMode.None,
                            AlignmentX = AlignmentX.Left,
                            AlignmentY = AlignmentY.Top
                        };
                        context.DrawRectangle(brush, null, new Rect(0, 0, width, height));
                    }
                    rtbBitmap.Render(drawingVisual);
                    int canvasW = ctx.Surface.Bitmap.PixelWidth;
                    int canvasH = ctx.Surface.Bitmap.PixelHeight;

                    // 计算目标矩形 (Canvas 坐标系)
                    int destX = (int)Math.Max(0, canvasLeft);
                    int destY = (int)Math.Max(0, canvasTop);
                    int destRight = (int)Math.Min(canvasW, canvasLeft + width);
                    int destBottom = (int)Math.Min(canvasH, canvasTop + height);

                    int drawW = destRight - destX;
                    int drawH = destBottom - destY;

                    if (drawW <= 0 || drawH <= 0)
                    {
                        CleanUpUI(ctx);
                        lag = 2;
                        return;
                    }

                    int srcX = (int)(destX - canvasLeft);
                    int srcY = (int)(destY - canvasTop);

                    // 5. 提取像素
                    int stride = drawW * 4;
                    int bufferSize = drawH * stride;

                    byte[] sourcePixels = new byte[bufferSize];
                    byte[] destPixels = new byte[bufferSize];
                    rtbBitmap.CopyPixels(new Int32Rect(srcX, srcY, drawW, drawH), sourcePixels, stride, 0);

                    // 从 Canvas 读取
                    var writeableBitmap = ctx.Surface.Bitmap;
                    Int32Rect dirtyRect = new Int32Rect(destX, destY, drawW, drawH);
                    writeableBitmap.CopyPixels(dirtyRect, destPixels, stride, 0);

                    // 6. 混合
                    double globalOpacityFactor = _richTextBox.Opacity;
                    AlphaBlendBatch(sourcePixels, destPixels, drawW, drawH, stride, 0, globalOpacityFactor);

                    // 7. 写回 Canvas
                    ctx.Undo.BeginStroke();
                    ctx.Undo.AddDirtyRect(dirtyRect);
                    writeableBitmap.WritePixels(dirtyRect, destPixels, stride, 0);
                    ctx.Undo.CommitStroke();
                }

                catch (Exception ex)
                {
                    Debug.WriteLine("CommitText Error: " + ex.Message);
                }
                finally
                {
                    CleanUpUI(ctx);
                    lag = 2;
                }
            }


            private void SelectCurrentBox()
            {
                if (_richTextBox != null)
                {
                    Keyboard.Focus(_richTextBox);
                    _richTextBox.Focus();
                }
            }

            private void DeselectCurrentBox(ToolContext ctx)
            {
                if (_richTextBox != null)
                {
                    ctx.EditorOverlay.Children.Remove(_richTextBox);
                    _richTextBox = null;
                }
            }
            private System.Windows.Controls.RichTextBox CreateRichTextBox(ToolContext ctx, double x, double y)
            {
                var mw = MainWindow.GetCurrentInstance();

                var rtb = new System.Windows.Controls.RichTextBox
                {
                    FontSize = AppConsts.DefaultFontSize,
                    Foreground = new SolidColorBrush(ctx.PenColor),
                    Opacity = TabPaint.SettingsManager.Instance.Current.PenOpacity,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent, // 必须透明
                    Padding = new Thickness(AppConsts.TextToolPadding),
                    AcceptsReturn = true,
                    AcceptsTab = true,
                    Document = new FlowDocument()
                    {
                        PagePadding = new Thickness(0), // 去除文档默认边距
                        LineHeight = 1, // 防止行距过大
                    }
                };
                rtb.Document.TextAlignment = TextAlignment.Left;

                Canvas.SetLeft(rtb, x);
                Canvas.SetTop(rtb, y);

                // 应用初始设置
                ApplyTextSettings(rtb);

                return rtb;
            }
            public void ApplyTextSettings(System.Windows.Controls.RichTextBox tb)
            {
                var mw = MainWindow.GetCurrentInstance();
                if (tb == null) return;
                if (mw.TextMenu == null) return;

                if (mw.TextMenu.FontFamilyBox.SelectedValue != null) // 1. 字体与大小
                    tb.FontFamily = new FontFamily(mw.TextMenu.FontFamilyBox.SelectedValue.ToString());

                if (double.TryParse(mw.TextMenu.FontSizeBox.Text, out double size))
                    tb.FontSize = Math.Max(1, size);

                tb.FontWeight = (mw.TextMenu.BoldBtn.IsChecked == true) ? FontWeights.Bold : FontWeights.Normal;     // 2. 粗体/斜体
                tb.FontStyle = (mw.TextMenu.ItalicBtn.IsChecked == true) ? FontStyles.Italic : FontStyles.Normal;
                var decors = new TextDecorationCollection();
                if (mw.TextMenu.UnderlineBtn.IsChecked == true) decors.Add(TextDecorations.Underline);
                if (mw.TextMenu.StrikeBtn.IsChecked == true) decors.Add(TextDecorations.Strikethrough);

                // 获取整个文档的范围并应用装饰线
                TextRange allText = new TextRange(tb.Document.ContentStart, tb.Document.ContentEnd);
                allText.ApplyPropertyValue(Inline.TextDecorationsProperty, decors);

                // 4. 对齐 - 作用于 Document ✨
                if (mw.TextMenu.AlignLeftBtn.IsChecked == true) tb.Document.TextAlignment = TextAlignment.Left;
                else if (mw.TextMenu.AlignCenterBtn.IsChecked == true) tb.Document.TextAlignment = TextAlignment.Center;
                else if (mw.TextMenu.AlignRightBtn.IsChecked == true) tb.Document.TextAlignment = TextAlignment.Right;

                // 5. 颜色与背景
                tb.Foreground = mw.SelectedBrush;
                if (mw.TextMenu.TextBackgroundBtn.IsChecked == true)
                    tb.Background = mw.BackgroundBrush;
                else
                    tb.Background = Brushes.Transparent;
            }

            public void UpdateCurrentTextBoxAttributes()
            {
                if (_richTextBox == null) return;

                var mw = MainWindow.GetCurrentInstance();
                ApplyTextSettings(_richTextBox);

                _richTextBox.UpdateLayout();
                DrawTextboxOverlay(mw._ctx);
            }
            private void CleanUpUI(ToolContext ctx)
            {
                MainWindow mw = MainWindow.GetCurrentInstance();
                ctx.SelectionOverlay.Children.Clear();
                ctx.SelectionOverlay.Visibility = Visibility.Collapsed;

                if (ctx.EditorOverlay.Children.Contains(_richTextBox))
                    ctx.EditorOverlay.Children.Remove(_richTextBox);
                // 【关键修复4】必须在清理时注销事件，防止残留导致下次操作出错
                ctx.EditorOverlay.PreviewMouseUp -= Overlay_PreviewMouseUp;
                ctx.EditorOverlay.PreviewMouseMove -= Overlay_PreviewMouseMove;
                ctx.EditorOverlay.PreviewMouseDown -= Overlay_PreviewMouseDown;
                mw.SetUndoRedoButtonState();
                _richTextBox = null;
                lag = 2; // 统一在这里赋予 lag
                if (mw._canvasResizer != null) mw._canvasResizer.SetHandleVisibility(true);
            }
       
            private void SetupTextBoxEvents(ToolContext ctx, System.Windows.Controls.RichTextBox rtb)
            {
                // 绘制虚线框和句柄
                rtb.Loaded += (s, e) => { DrawTextboxOverlay(ctx); };
                rtb.SelectionChanged += (s, e) =>
                {
                    (MainWindow.GetCurrentInstance()).SyncTextToolbarState(rtb);
                };
                rtb.PreviewKeyDown += (s, e) =>
                {
                    if (e.Key == Key.Delete)
                    {
                        CommitText(ctx);
                        ctx.EditorOverlay.Children.Remove(rtb);
                        _richTextBox = null;
                        ctx.EditorOverlay.IsHitTestVisible = false;
                        e.Handled = true;
                    }
                };

                rtb.Focusable = true;
                rtb.Loaded += (s, e) => rtb.Focus();
            }// [新增] 处理全局点击逻辑


            public void SpawnTextBox(ToolContext ctx, Point viewPos, string text)
            {
                _dragging = false;
                _resizing = false;
                if (ctx.EditorOverlay.IsMouseCaptured) ctx.EditorOverlay.ReleaseMouseCapture();

                if (_richTextBox != null) CommitText(ctx);
                Point px = ctx.ToPixel(viewPos);
                _richTextBox = CreateRichTextBox(ctx, px.X, px.Y);
                if (!string.IsNullOrEmpty(text))
                {
                    var range = new TextRange(_richTextBox.Document.ContentStart, _richTextBox.Document.ContentEnd);
                    range.Text = text;
                }


                _richTextBox.MaxWidth = AppConsts.MaxTextBoxWidth;
                _richTextBox.Width = Double.NaN; // 让宽度自适应内容
                _richTextBox.Height = Double.NaN;
                // 显示 UI
                ctx.EditorOverlay.Visibility = Visibility.Visible;
                ctx.EditorOverlay.IsHitTestVisible = true;
                Canvas.SetZIndex(ctx.EditorOverlay, AppConsts.EditorOverlayZIndex);
                ctx.EditorOverlay.Children.Add(_richTextBox);

                (MainWindow.GetCurrentInstance()).ShowTextToolbarFor(_richTextBox);
                SetupTextBoxEvents(ctx, _richTextBox);

                ctx.EditorOverlay.PreviewMouseUp -= Overlay_PreviewMouseUp; // 防止重复订阅
                ctx.EditorOverlay.PreviewMouseUp += Overlay_PreviewMouseUp;

                ctx.EditorOverlay.PreviewMouseMove -= Overlay_PreviewMouseMove;
                ctx.EditorOverlay.PreviewMouseMove += Overlay_PreviewMouseMove;

                ctx.EditorOverlay.PreviewMouseDown -= Overlay_PreviewMouseDown;
                ctx.EditorOverlay.PreviewMouseDown += Overlay_PreviewMouseDown;

                _richTextBox.UpdateLayout();
                DrawTextboxOverlay(ctx);
            }
            private void Overlay_PreviewMouseUp(object sender, MouseButtonEventArgs e)
            {
                var mw = MainWindow.GetCurrentInstance();
                Point pos = e.GetPosition(mw._ctx.EditorOverlay);
                OnPointerUp(mw._ctx, pos);
            }

            private void Overlay_PreviewMouseMove(object sender, MouseEventArgs e)
            {
                var mw = MainWindow.GetCurrentInstance();
                Point pos = e.GetPosition(mw._ctx.EditorOverlay);
                OnPointerMove(mw._ctx, pos);
            }

            private void Overlay_PreviewMouseDown(object sender, MouseButtonEventArgs e)
            {
                var mw = MainWindow.GetCurrentInstance();
                var ctx = mw._ctx;

                Point pos = e.GetPosition(ctx.EditorOverlay);
                Point pixelPos = ctx.ToPixel(pos);

                var anchor = HitTestTextboxHandle(pixelPos);

                if (anchor != ResizeAnchor.None)
                {
                    _resizing = true;
                    _currentAnchor = anchor;
                    _startMouse = pixelPos;
                    _startW = _richTextBox.ActualWidth;
                    _startH = _richTextBox.ActualHeight;
                    _startX = Canvas.GetLeft(_richTextBox);
                    _startY = Canvas.GetTop(_richTextBox);
                    ctx.EditorOverlay.CaptureMouse();
                    e.Handled = true;
                }
                else if (IsInsideBorder(pixelPos))
                {
                    _dragging = true;
                    _startMouse = pixelPos;
                    _startX = Canvas.GetLeft(_richTextBox);
                    _startY = Canvas.GetTop(_richTextBox);
                    ctx.EditorOverlay.CaptureMouse();
                    e.Handled = true;
                }
                else   OnPointerDown(ctx, pos);
            }
        }
    }
}