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
            public void CleanUp(ToolContext ctx)
            {
                HidePreview(ctx);
                ctx.SelectionOverlay.Children.Clear();
                ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
                // 清空状态
                _originalRect = new Int32Rect();
                _selecting = false;
                _draggingSelection = false;
                _resizing = false;
                _currentAnchor = ResizeAnchor.None;
                _selectionData = null;
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
                //return;
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
                    Canvas.SetTop(handle, p.Y - HandleSize  * invScale/ 2);
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

                // 2. 强制设置预览图的 UI 尺寸
                // 这一步非常关键：必须让 Image 控件的大小正好等于其对应的像素大小 * 缩放
                ctx.SelectionPreview.Width = _selectionRect.Width * scaleX;
                ctx.SelectionPreview.Height = _selectionRect.Height * scaleY;

                // 3. 计算相对于 ViewElement (底图) 的偏移
                // 我们不再依赖父容器偏移，而是计算相对于底图左上角的偏移
                double localX = pixelX * scaleX;
                double localY = pixelY * scaleY;

                // 4. 对坐标进行取整，防止半像素偏移
                // 我们使用 Math.Round(n, 0) 来确保它对齐到 UI 像素点
                // 这里的 RenderTransform 将相对于 Image 控件在布局中的原始位置进行平移
                ctx.SelectionPreview.RenderTransform = new TranslateTransform(
                    Math.Round(localX, 0),
                    Math.Round(localY, 0)
                );

                // 5. 补充：如果你的 SelectionPreview 还在 Canvas 中，
                // 请确保它的 Canvas.Left 和 Top 都设为 0，避免重复偏移
                // Canvas.SetLeft(ctx.SelectionPreview, 0);
                // Canvas.SetTop(ctx.SelectionPreview, 0);
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

                // 2. Create a temporary file
                string tempFilePath = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"selection_{Guid.NewGuid()}.png"
                );

                try
                {
                    // 3. Encode the bitmap to the temporary PNG file
                    using (var fileStream = new System.IO.FileStream(tempFilePath, System.IO.FileMode.Create))
                    {
                        PngBitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                        encoder.Save(fileStream);
                    }

                    // 4. Prepare the data object for drag-drop
                    var dataObject = new System.Windows.DataObject();
                    // The data is a string array containing the full path(s) to the file(s)
                    dataObject.SetData(System.Windows.DataFormats.FileDrop, new string[] { tempFilePath });

                    // Hide the selection preview during the external drag for a cleaner look
                    HidePreview(ctx);
                    ctx.SelectionOverlay.Visibility = Visibility.Collapsed;

                    // 5. Start the blocking drag-drop operation
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
                    // 6. Clean up: ALWAYS delete the temporary file
                    if (System.IO.File.Exists(tempFilePath)) System.IO.File.Delete(tempFilePath);

                }
            }
            private void ResetPreviewState(ToolContext ctx)
            {
                // 1. 隐藏预览
                ctx.SelectionPreview.Visibility = Visibility.Collapsed;

                // 2. 彻底清除位图源，释放内存
                ctx.SelectionPreview.Source = null;

                // 3. 关键：重置所有变换
                ctx.SelectionPreview.RenderTransform = Transform.Identity;

                // 4. 关键：清除之前的裁剪区域
                ctx.SelectionPreview.Clip = null;

                // 5. 重置 Canvas 位置（可选，但建议）
                Canvas.SetLeft(ctx.SelectionPreview, 0);
                Canvas.SetTop(ctx.SelectionPreview, 0);

                // 6. 重置逻辑状态
                _transformStep = 0;
                _originalRect = new Int32Rect();
            }
            public void CommitSelection(ToolContext ctx)
            {
                if (_selectionData == null) return;
                // 写回后重置计数

                // 缩放或拉伸的比例
                ctx.Undo.BeginStroke();
                ctx.Undo.AddDirtyRect(_selectionRect);

                if (_originalRect.Width != _selectionRect.Width || _originalRect.Height != _selectionRect.Height)
                {
                    if (_originalRect.Width <= 0 || _originalRect.Height <= 0) return;

                    int expectedStride = _originalRect.Width * 4;
                    int actualStride = _selectionData.Length / _originalRect.Height;
                    int dataStride = Math.Min(expectedStride, actualStride);
                    var src = BitmapSource.Create(
                        _originalRect.Width, _originalRect.Height,
                        ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                        PixelFormats.Bgra32, null, _selectionData, dataStride);

                    var transform = new TransformedBitmap(src, new ScaleTransform(
                        (double)_selectionRect.Width / _originalRect.Width,
                        (double)_selectionRect.Height / _originalRect.Height));

                    var resized = new WriteableBitmap(transform);
                    int newStride = resized.BackBufferStride;
                    var newData = new byte[_selectionRect.Height * newStride];
                    resized.CopyPixels(newData, newStride, 0);

                    _selectionData = newData;
                    ctx.Surface.WriteRegion(_selectionRect, _selectionData, newStride);
                }
                else ctx.Surface.WriteRegion(_selectionRect, _selectionData, _selectionRect.Width * 4);
                ctx.Undo.CommitStroke();
                HidePreview(ctx);
                _selectionData = null;
                ctx.IsDirty = true;

                _transformStep = 0;
                _originalRect = new Int32Rect();
                ((MainWindow)System.Windows.Application.Current.MainWindow).SetUndoRedoButtonState();
                ResetPreviewState(ctx);
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
                ctx.Surface.Bitmap.AddDirtyRect(rect);
                ctx.Surface.Bitmap.Unlock();
            }
        }
    }
}