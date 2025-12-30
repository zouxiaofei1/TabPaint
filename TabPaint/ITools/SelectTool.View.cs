using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

//
//SelectTool类的渲染实现
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public partial class SelectTool : ToolBase
        {
            public void Cleanup(ToolContext ctx)
            {//注意是小写
                HidePreview(ctx);
                ctx.SelectionOverlay.Children.Clear();
                ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
                // 清空状态
                _originalRect = new Int32Rect();
                _selectionRect = new Int32Rect();
                _selecting = false;
                _draggingSelection = false;
                _resizing = false;
                _currentAnchor = ResizeAnchor.None;
                _selectionData = null;
                lag = 0;
            }
            // 在 SelectTool 类内部
            public void RefreshOverlay(ToolContext ctx)
            {
                // 如果当前没有正在选取的区域，或者没有数据，就不重绘
                if (_selectionRect.Width > 0 && _selectionRect.Height > 0)
                {
                    DrawOverlay(ctx, _selectionRect);
                }
            }


            private void DrawOverlay(ToolContext ctx, Int32Rect rect)
            {
              
                double invScale = 1 / ((MainWindow)System.Windows.Application.Current.MainWindow).zoomscale;
                var overlay = ctx.SelectionOverlay;
                overlay.Children.Clear();

                // 虚线框
                var outline = new System.Windows.Shapes.Rectangle
                {
                    Stroke = Brushes.Black,
                    StrokeDashArray = new DoubleCollection { 8, 4 },
                    StrokeThickness = invScale * 1.5,
                    Width = rect.Width,
                    Height = rect.Height
                };
                RenderOptions.SetEdgeMode(outline, EdgeMode.Unspecified);  // 开抗锯齿混合
                outline.SnapsToDevicePixels = false; // 让虚线自由落在亚像素
                Canvas.SetLeft(outline, rect.X);
                Canvas.SetTop(outline, rect.Y);
                overlay.Children.Add(outline);

                // 8个句柄
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
                    RenderOptions.SetEdgeMode(handle, EdgeMode.Unspecified);  // 开抗锯齿混合
                    outline.SnapsToDevicePixels = false; // 让虚线自由落在亚像素
                    Canvas.SetLeft(handle, p.X - HandleSize * invScale / 2);
                    Canvas.SetTop(handle, p.Y - HandleSize * invScale / 2);
                    overlay.Children.Add(handle);
                }
                ctx.SelectionOverlay.IsHitTestVisible = false;
                ctx.SelectionOverlay.Visibility = Visibility.Visible;
            }
            public ResizeAnchor HitTestHandle(Point px, Int32Rect rect)
            {
                double size = 6 / ((MainWindow)System.Windows.Application.Current.MainWindow).zoomscale; // 句柄大小
                double x1 = rect.X;
                double y1 = rect.Y;
                double x2 = rect.X + rect.Width;
                double y2 = rect.Y + rect.Height;
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

            public void ClearSelections(ToolContext ctx)
            {
                ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
                _resizing = false;
                _draggingSelection = false;
                _selecting = false;
                _currentAnchor = ResizeAnchor.None;
                _selectionRect.Width = _selectionRect.Height = 0;
            }
            private Rect GetWindowBoundsInPhysicalPixels(System.Windows.Window window)
            {
                var source = PresentationSource.FromVisual(window);
                if (source == null || source.CompositionTarget == null)
                {
                    // Fallback for cases where the window is not yet fully rendered
                    return new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight);
                }
                Matrix transform = source.CompositionTarget.TransformToDevice;
                double dpiX = transform.M11; // Horizontal scaling
                double dpiY = transform.M22; // Vertical scaling

                // Convert window bounds from DIUs to physical pixels
                return new Rect(
                    window.Left * dpiX,
                    window.Top * dpiY,
                    window.ActualWidth * dpiX,
                    window.ActualHeight * dpiY
                );
            }

            private void SetPreviewPosition(ToolContext ctx, int pixelX, int pixelY)
            {
                // 1. 获取缩放比例（像素到 UI 单位的转换）
                double scaleX = ctx.ViewElement.ActualWidth / ctx.Surface.Bitmap.PixelWidth;
                double scaleY = ctx.ViewElement.ActualHeight / ctx.Surface.Bitmap.PixelHeight;

                ctx.SelectionPreview.Width = _selectionRect.Width * scaleX;
                ctx.SelectionPreview.Height = _selectionRect.Height * scaleY;

                double localX = pixelX * scaleX;
                double localY = pixelY * scaleY;

                ctx.SelectionPreview.RenderTransform = new TranslateTransform(
                    Math.Round(localX, 0),
                    Math.Round(localY, 0)
                );
            }

            private void StartDragDropOperation(ToolContext ctx)
            {
                if (_selectionData == null) return;

                int width = _originalRect.Width > 0 ? _originalRect.Width : _selectionRect.Width;
                int height = _originalRect.Height > 0 ? _originalRect.Height : _selectionRect.Height;
                byte[] data = _selectionData;
                if (width == 0 || height == 0) return;
                int stride = width * 4;
                int expectedStride = _originalRect.Width * 4;
                int actualStride = _selectionData.Length / _originalRect.Height;
                int dataStride = Math.Min(expectedStride, actualStride);
                var bitmapSource = BitmapSource.Create(
                    width, height,
                    ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                    PixelFormats.Bgra32, null,
                    data, dataStride);

                string tempFilePath = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"selection_{Guid.NewGuid()}.png"
                );

                try
                {
                    using (var fileStream = new System.IO.FileStream(tempFilePath, System.IO.FileMode.Create))
                    {
                        PngBitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                        encoder.Save(fileStream);
                    }

                    var dataObject = new System.Windows.DataObject();
                    dataObject.SetData(System.Windows.DataFormats.FileDrop, new string[] { tempFilePath });

                    HidePreview(ctx);
                    ctx.SelectionOverlay.Visibility = Visibility.Collapsed;

                    DragDrop.DoDragDrop(ctx.ViewElement, dataObject, System.Windows.DragDropEffects.Copy);
                    _originalRect = new Int32Rect();
                    _transformStep = 0;
                    _selectionData = null;
                    ctx.IsDirty = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Drag-drop operation failed: {ex.Message}");
                }
                finally
                {
                    if (System.IO.File.Exists(tempFilePath)) System.IO.File.Delete(tempFilePath);

                }
            }
            private void ResetPreviewState(ToolContext ctx)
            {
                ctx.SelectionPreview.Visibility = Visibility.Collapsed;
                ctx.SelectionPreview.Source = null;
                ctx.SelectionPreview.RenderTransform = Transform.Identity;
                ctx.SelectionPreview.Clip = null;
                Canvas.SetLeft(ctx.SelectionPreview, 0);
                Canvas.SetTop(ctx.SelectionPreview, 0);
                _transformStep = 0;
                _originalRect = new Int32Rect();
            }
            public void GiveUpSelection(ToolContext ctx)
            {
                if (ctx == null) return;
                CommitSelection(ctx);
                Cleanup(ctx);
                ctx.Undo.Undo();
            }
            public void CommitSelection(ToolContext ctx, bool shape = false)
            {
                if (_selectionData == null) return;

                // 1. 准备撤销记录
                ctx.Undo.BeginStroke();
                // 注意：这里需要记录的是最终写入区域的脏矩形，不仅仅是 selectionRect
                // 但为了简单，我们先标记这个区域。更严谨的做法是记录混合前的背景图。
                ctx.Undo.AddDirtyRect(_selectionRect);

                // 2. 准备源数据 (处理缩放)
                byte[] finalData = _selectionData;
                int finalWidth = _selectionRect.Width;
                int finalHeight = _selectionRect.Height;
                int finalStride = finalWidth * 4;

                // 如果选区被缩放过，需要重新采样
                if (_originalRect.Width != _selectionRect.Width || _originalRect.Height != _selectionRect.Height)
                {
                    if (_originalRect.Width <= 0 || _originalRect.Height <= 0) return;

                    int expectedStride = _originalRect.Width * 4;
                    int actualStride = _selectionData.Length / _originalRect.Height;
                    int dataStride = Math.Min(expectedStride, actualStride); // 容错处理

                    var src = BitmapSource.Create(
                        _originalRect.Width, _originalRect.Height,
                        ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                        PixelFormats.Bgra32, null, _selectionData, dataStride);

                    var transform = new TransformedBitmap(src, new ScaleTransform(
                        (double)_selectionRect.Width / _originalRect.Width,
                        (double)_selectionRect.Height / _originalRect.Height));

                    var resized = new WriteableBitmap(transform);
                    finalStride = resized.BackBufferStride;
                    finalData = new byte[finalHeight * finalStride];
                    resized.CopyPixels(finalData, finalStride, 0);
                }

                // 3. 执行透明度混合写入 (Alpha Blending)
                BlendPixels(ctx.Surface.Bitmap, _selectionRect.X, _selectionRect.Y, finalWidth, finalHeight, finalData, finalStride);

                // 4. 清理现场

                ctx.Undo.CommitStroke(shape ? UndoActionType.Draw : UndoActionType.Selection);
                HidePreview(ctx);
                _selectionData = null;
                ctx.IsDirty = true;
                lag = 1;
                _transformStep = 0;
                _originalRect = new Int32Rect();
                ((MainWindow)System.Windows.Application.Current.MainWindow).SetUndoRedoButtonState();
                ResetPreviewState(ctx);
            }
            private void BlendPixels(WriteableBitmap targetBmp, int x, int y, int w, int h, byte[] sourcePixels, int sourceStride)
            {
                targetBmp.Lock();
                try
                {
                    int targetW = targetBmp.PixelWidth;
                    int targetH = targetBmp.PixelHeight;

                    // 1. 计算【实际绘制区域】（裁剪逻辑：取 目标画布 和 贴图区域 的交集）
                    // 左上角坐标（如果小于0，则截断为0）
                    int drawX = Math.Max(0, x);
                    int drawY = Math.Max(0, y);

                    // 右下角坐标 (限制在画布宽高雄内，同时不能超过图片本身的右边界 x+w)
                    int right = Math.Min(targetW, x + w);
                    int bottom = Math.Min(targetH, y + h);

                    // 实际需要绘制的宽高
                    int drawW = right - drawX;
                    int drawH = bottom - drawY;

                    // 2. 如果宽高无效（完全在画布外），直接退出，防止崩溃
                    if (drawW <= 0 || drawH <= 0) return;

                    // 3. 计算源数据的起始偏移量
                    // 关键修复点：如果 x < 0，说明图片左侧被裁掉了，源数据读取时要跳过左边 -x 个像素
                    int srcOffsetX = drawX - x;
                    int srcOffsetY = drawY - y;

                    unsafe
                    {
                        byte* pTargetBase = (byte*)targetBmp.BackBuffer;
                        int targetStride = targetBmp.BackBufferStride;

                        for (int r = 0; r < drawH; r++)
                        {
                            // 计算源像素行索引：
                            // (起始Y偏移 + 当前行 r) * stride + (起始X偏移 * 4)
                            // 这里的 srcOffsetX 已经包含了因 x<0 而产生的偏移，确保不会读取到之前的像素
                            long srcRowIndex = (long)(srcOffsetY + r) * sourceStride + (long)srcOffsetX * 4;

                            // 目标像素指针：
                            // Base + (绘制Y + r) * stride + 绘制X * 4
                            byte* pTargetRow = pTargetBase + (drawY + r) * targetStride + drawX * 4;

                            for (int c = 0; c < drawW; c++)
                            {
                                // 安全检查：防止极端情况下数组越界 (防御性)
                                if (srcRowIndex + c * 4 + 3 >= sourcePixels.Length) break;

                                // 获取源像素 Bgra
                                byte srcB = sourcePixels[srcRowIndex + c * 4 + 0];
                                byte srcG = sourcePixels[srcRowIndex + c * 4 + 1];
                                byte srcR = sourcePixels[srcRowIndex + c * 4 + 2];
                                byte srcA = sourcePixels[srcRowIndex + c * 4 + 3];

                                // 优化：全透明跳过
                                if (srcA == 0)
                                {
                                    pTargetRow += 4;
                                    continue;
                                }

                                // 优化：全不透明直接覆盖
                                if (srcA == 255)
                                {
                                    pTargetRow[0] = srcB;
                                    pTargetRow[1] = srcG;
                                    pTargetRow[2] = srcR;
                                    pTargetRow[3] = 255;
                                }
                                else
                                {
                                    // Alpha Blending
                                    byte dstB = pTargetRow[0];
                                    byte dstG = pTargetRow[1];
                                    byte dstR = pTargetRow[2];

                                    // 使用浮点运算混合
                                    double alpha = srcA / 255.0;
                                    double invAlpha = 1.0 - alpha;

                                    pTargetRow[0] = (byte)(srcB * alpha + dstB * invAlpha);
                                    pTargetRow[1] = (byte)(srcG * alpha + dstG * invAlpha);
                                    pTargetRow[2] = (byte)(srcR * alpha + dstR * invAlpha);
                                    pTargetRow[3] = 255;
                                }

                                pTargetRow += 4;
                            }
                        }
                    }

                    // 标记脏区域刷新显示
                    targetBmp.AddDirtyRect(new Int32Rect(drawX, drawY, drawW, drawH));
                }
                catch (Exception ex)
                {
                    // 捕获异常防止崩溃，仅在调试输出
                    System.Diagnostics.Debug.WriteLine("BlendPixels Error: " + ex.Message);
                }
                finally
                {
                    targetBmp.Unlock();
                }
            }


            private void HidePreview(ToolContext ctx)
            {
                ctx.SelectionPreview.Visibility = Visibility.Collapsed;
            }

            private static Int32Rect MakeRect(Point p1, Point p2)
            {
                int x = (int)Math.Min(p1.X, p2.X);
                int y = (int)Math.Min(p1.Y, p2.Y);
                int w = Math.Abs((int)p1.X - (int)p2.X);
                int h = Math.Abs((int)p1.Y - (int)p2.Y);
                return new Int32Rect(x, y, w, h);
            }

            public bool IsPointInSelection(Point px)
            {
                return px.X >= _selectionRect.X &&
                       px.X < _selectionRect.X + _selectionRect.Width &&
                       px.Y >= _selectionRect.Y &&
                       px.Y < _selectionRect.Y + _selectionRect.Height;
            }

            private void ClearRect(ToolContext ctx, Int32Rect rect, Color color)
            {
                ctx.Surface.Bitmap.Lock();
                unsafe
                {
                    byte* basePtr = (byte*)ctx.Surface.Bitmap.BackBuffer;
                    int stride = ctx.Surface.Bitmap.BackBufferStride;
                    for (int y = rect.Y; y < rect.Y + rect.Height; y++)
                    {
                        byte* rowPtr = basePtr + y * stride + rect.X * 4;
                        for (int x = 0; x < rect.Width; x++)
                        {
                            rowPtr[0] = color.B;
                            rowPtr[1] = color.G;
                            rowPtr[2] = color.R;
                            rowPtr[3] = color.A;
                            rowPtr += 4;
                        }
                    }
                }
                ctx.Surface.Bitmap.AddDirtyRect(ClampRect(rect, ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Bitmap.PixelWidth, ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Bitmap.PixelHeight));
                ctx.Surface.Bitmap.Unlock();
            }
        }
    }
}