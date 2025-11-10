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
        public class SelectTool : ToolBase
        {
            public override string Name => "Select";
            public override System.Windows.Input.Cursor Cursor => System.Windows.Input.Cursors.Cross;

            private bool _selecting = false;
            public bool _draggingSelection = false;

            private Point _startPixel;
            private Point _clickOffset;
            private Int32Rect _selectionRect;
            private Int32Rect _originalRect;
            private byte[]? _selectionData;
            private int _transformStep = 0; // 0 = 未操作，>0 = 已操作
            private byte[]? _clipboardData;
            private int _clipboardWidth;
            private int _clipboardHeight;


            private ResizeAnchor _currentAnchor = ResizeAnchor.None;
            private bool _resizing = false;
            private Point _startMouse;
            private double _startW, _startH, _startX, _startY;

            // 句柄尺寸
            private const double HandleSize = 6;

            public enum ResizeAnchor
            {
                None,
                TopLeft, TopMiddle, TopRight,
                LeftMiddle, RightMiddle,
                BottomLeft, BottomMiddle, BottomRight
            }

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
            private void CopyToSystemClipboard(ToolContext ctx)
            {       // 确定要复制的图像的原始尺寸和数据
                // 如果进行过缩放，_originalRect 存的是原始尺寸
                if (_selectionData == null) return;
                int width = _originalRect.Width > 0 ? _originalRect.Width : _selectionRect.Width;
                int height = _originalRect.Height > 0 ? _originalRect.Height : _selectionRect.Height;
                byte[] data = _selectionData;

                if (width == 0 || height == 0) return;
                int stride = width * 4;
                try
                {
                    var bitmapToCopy = BitmapSource.Create(  // 从原始字节数据创建 BitmapSource
                        width,
                        height,
                        ctx.Surface.Bitmap.DpiX,
                        ctx.Surface.Bitmap.DpiY,
                        PixelFormats.Bgra32,
                        null,
                        data,
                        stride
                    );

                    // 将 BitmapSource 放入系统剪贴板
                    System.Windows.Clipboard.SetImage(bitmapToCopy);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to copy to clipboard: " + ex.Message);
                }
            }

            public void CutSelection(ToolContext ctx, bool paste)
            {//paste = false ->delete , true->cut
                if (_selectionData == null) SelectAll(ctx, true);

                if (_selectionData == null) return;
                int Clipwidth, Clipheight;
                if (_originalRect.Width == 0 || _originalRect.Height == 0)
                {
                    Clipwidth = _selectionRect.Width;
                    Clipheight = _selectionRect.Height;
                }
                else
                {
                    Clipwidth = _originalRect.Width;
                    Clipheight = _originalRect.Height;
                }
                // 复制到剪贴板
                if (paste)
                {
                    CopyToSystemClipboard(ctx);
                    _clipboardWidth = Clipwidth;
                    _clipboardHeight = Clipheight;

                    _clipboardData = new byte[_selectionData.Length];
                    Array.Copy(_selectionData, _clipboardData, _selectionData.Length);
                }
                else
                {
                    _clipboardData = null; _clipboardWidth = _clipboardHeight = 0;
                }
                HidePreview(ctx);
                ctx.SelectionOverlay.Children.Clear();
                ctx.SelectionOverlay.Visibility = Visibility.Collapsed;

                // 清空状态
                _selectionData = null;

            }
            public void PasteSelection(ToolContext ctx, bool ins)
            {
                // 在粘贴前，如果当前有选区，先提交它
                if (_selectionData != null) CommitSelection(ctx);

                BitmapSource? sourceBitmap = null;

                // 1. 优先从系统剪贴板获取图像
                if (System.Windows.Clipboard.ContainsImage())
                {
                    try
                    {
                        sourceBitmap = System.Windows.Clipboard.GetImage();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Failed to get image from clipboard: " + ex.Message);
                        sourceBitmap = null;
                    }
                }

                // 2. 如果系统剪贴板没有图像，则尝试使用内部剪贴板
                if (sourceBitmap == null && _clipboardData != null && _clipboardWidth > 0 && _clipboardHeight > 0)
                {
                    sourceBitmap = BitmapSource.Create(
                        _clipboardWidth, _clipboardHeight,
                        ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                        PixelFormats.Bgra32, null,
                        _clipboardData, _clipboardWidth * 4);
                }

                // 3. 如果没有任何可粘贴的内容，则直接返回
                if (sourceBitmap == null) return;
                if (sourceBitmap.Format != PixelFormats.Bgra32)
                {
                    sourceBitmap = new FormatConvertedBitmap(sourceBitmap, PixelFormats.Bgra32, null, 0);
                }

                // 5. 从处理后的 BitmapSource 提取像素数据
                int width = sourceBitmap.PixelWidth;
                int height = sourceBitmap.PixelHeight;
                int stride = width * 4;
                var newData = new byte[height * stride];
                sourceBitmap.CopyPixels(newData, stride, 0);

                // 6. 更新工具状态，准备进行拖动/缩放
                _selectionData = newData;
                _selectionRect = new Int32Rect(0, 0, width, height);
                _originalRect = _selectionRect; // 新粘贴的内容，原始尺寸就是当前尺寸
                var previewBmp = new WriteableBitmap(sourceBitmap);
                ctx.SelectionPreview.Source = previewBmp;

                // 根据 ins 参数决定粘贴位置
                if (ins) // 原位置粘贴 (这个逻辑可能需要根据你的应用场景调整)
                {
                    // 通常外部粘贴没有“原位置”概念，所以也粘贴到左上角
                    Canvas.SetLeft(ctx.SelectionPreview, 0);
                    Canvas.SetTop(ctx.SelectionPreview, 0);
                    ctx.SelectionPreview.RenderTransform = new TranslateTransform(0, 0);
                    _selectionRect.X = 0;
                    _selectionRect.Y = 0;
                }
                else // 左上角粘贴
                {
                    Canvas.SetLeft(ctx.SelectionPreview, 0);
                    Canvas.SetTop(ctx.SelectionPreview, 0);
                    ctx.SelectionPreview.RenderTransform = new TranslateTransform(0, 0);
                }

                ctx.SelectionPreview.Visibility = Visibility.Visible;
                DrawOverlay(ctx, _selectionRect);
                _transformStep = 0; // 重置变换步骤
            }

            public void CopySelection(ToolContext ctx)
            {
                if (_selectionData == null) SelectAll(ctx, false);

                if (_selectionData != null)
                {
                    CopyToSystemClipboard(ctx);
                    if (_originalRect.Width == 0 || _originalRect.Height == 0)
                    {
                        _clipboardWidth = _selectionRect.Width;
                        _clipboardHeight = _selectionRect.Height;
                    }
                    else
                    {
                        _clipboardWidth = _originalRect.Width;
                        _clipboardHeight = _originalRect.Height;
                    }

                    _clipboardData = new byte[_selectionData.Length];
                    Array.Copy(_selectionData, _clipboardData, _selectionData.Length);

                    // 直接原位粘贴
                    //   PasteSelection(ctx, true);
                    HidePreview(ctx);
                    ctx.SelectionOverlay.Children.Clear();
                    ctx.SelectionOverlay.Visibility = Visibility.Collapsed;

                    // 清空状态
                    _selectionData = null;
                }
            }


            public void SelectAll(ToolContext ctx, bool cut = true)
            {
                if (ctx.Surface?.Bitmap == null)
                    return;

                // 选区 = 整幅图像
                _selectionRect = new Int32Rect(0, 0,
            ctx.Surface.Bitmap.PixelWidth,
            ctx.Surface.Bitmap.PixelHeight);
                _originalRect = _selectionRect;

                // 提取整幅像素
                _selectionData = ctx.Surface.ExtractRegion(_selectionRect);
                if (_selectionData == null || _selectionData.Length < _selectionRect.Width * _selectionRect.Height * 4)
                    return;
                ctx.Undo.BeginStroke();
                ctx.Undo.AddDirtyRect(_selectionRect);
                ctx.Undo.CommitStroke(); // 保存这部分像素到栈

                if (cut) ClearRect(ctx, _selectionRect, ctx.EraserColor);
                // 创建预览位图
                var previewBmp = new WriteableBitmap(_selectionRect.Width, _selectionRect.Height,
                    ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);

                previewBmp.WritePixels(
                    new Int32Rect(0, 0, _selectionRect.Width, _selectionRect.Height),
                    _selectionData, _selectionRect.Width * 4, 0);

                ctx.SelectionPreview.Source = previewBmp;
                ctx.SelectionPreview.Visibility = Visibility.Visible;

                // 放在画布左上角
                Canvas.SetLeft(ctx.SelectionPreview, 0);
                Canvas.SetTop(ctx.SelectionPreview, 0);

                // 绘制虚线框
                DrawOverlay(ctx, _selectionRect);
            }
            public void CropToSelection(ToolContext ctx)
            {
                if (_selectionData == null || _selectionRect.Width <= 0 || _selectionRect.Height <= 0) return;

                var undoRect = new Int32Rect(0, 0, ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);
                var undoPixels = ctx.Surface.ExtractRegion(undoRect);
                var wb = ctx.Surface.Bitmap;
                int stride = wb.PixelWidth * (wb.Format.BitsPerPixel / 8);
                byte[] pixels = new byte[wb.PixelHeight * stride];

                // 将像素复制到数组
                wb.CopyPixels(pixels, stride, 0);
                byte[] finalSelectionData;
                int finalWidth = _selectionRect.Width;
                int finalHeight = _selectionRect.Height;
                int finalStride;

                // 检查是否进行过缩放
                if (_originalRect.Width > 0 && _originalRect.Height > 0 &&
                    (_originalRect.Width != _selectionRect.Width || _originalRect.Height != _selectionRect.Height))
                {
                    // 应用缩放变换来获取最终的像素数据
                    var src = BitmapSource.Create(
                        _originalRect.Width, _originalRect.Height,
                        ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                        PixelFormats.Bgra32, null, _selectionData, _originalRect.Width * 4);

                    var transform = new TransformedBitmap(src, new ScaleTransform(
                        (double)finalWidth / _originalRect.Width,
                        (double)finalHeight / _originalRect.Height));

                    var resized = new WriteableBitmap(transform);
                    finalStride = resized.BackBufferStride;
                    finalSelectionData = new byte[finalHeight * finalStride];
                    resized.CopyPixels(finalSelectionData, finalStride, 0);
                }
                else
                {
                    // 没有缩放，直接使用原始数据
                    finalSelectionData = _selectionData;
                    finalStride = finalWidth * 4;
                }

                // 3. 创建一个新的、尺寸与选区相同的WriteableBitmap
                var newBitmap = new WriteableBitmap(
                    finalWidth,
                    finalHeight,
                    ctx.Surface.Bitmap.DpiX,
                    ctx.Surface.Bitmap.DpiY,
                    PixelFormats.Bgra32,
                    null
                );

                // 4. 将选区数据写入新位图
                newBitmap.WritePixels(
                    new Int32Rect(0, 0, finalWidth, finalHeight),
                    finalSelectionData,
                    finalStride,
                    0
                );

                var redoRect = new Int32Rect(0, 0, newBitmap.PixelWidth, newBitmap.PixelHeight);
                int redoStride = newBitmap.BackBufferStride;
                var redoPixels = new byte[redoStride * redoRect.Height];
                newBitmap.CopyPixels(redoPixels, redoStride, 0);


                ctx.Surface.ReplaceBitmap(newBitmap);
                CleanUp(ctx); // 使用你已有的清理方法
                ctx.Undo.PushTransformAction(undoRect, undoPixels, redoRect, redoPixels);
                ctx.IsDirty = true;

                // 更新UI（例如Undo/Redo按钮的状态）
                ((MainWindow)System.Windows.Application.Current.MainWindow).SetUndoRedoButtonState();
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
                    Canvas.SetTop(handle, p.Y - HandleSize / 2);
                    overlay.Children.Add(handle);
                }
                ctx.SelectionOverlay.IsHitTestVisible = false;
                ctx.SelectionOverlay.Visibility = Visibility.Visible;
            }
            private ResizeAnchor HitTestHandle(Point px, Int32Rect rect)
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

            public override void OnPointerDown(ToolContext ctx, Point viewPos)
            {
                a.s("Selecttool");
                if (ctx.Surface.Bitmap == null) return;
                var px = ctx.ToPixel(viewPos);

                // 在选区外点击 → 提交并清除
                if (_selectionData != null && !IsPointInSelection(px))
                {
                    if (HitTestHandle(px, _selectionRect) == ResizeAnchor.None)
                    {
                        CommitSelection(ctx);
                        ClearSelections(ctx);
                        return;
                    }
                }

                if (_selectionData != null)
                {
                    // 判定点击位置是句柄还是框内
                    _currentAnchor = HitTestHandle(px, _selectionRect);
                    if (_currentAnchor != ResizeAnchor.None)
                    {
                        if (_transformStep == 0) // 第一次缩放
                        {
                            _originalRect = _selectionRect;
                        }
                        _transformStep++;
                        _resizing = true;
                        _startMouse = px;
                        _startW = _selectionRect.Width;
                        _startH = _selectionRect.Height;
                        _startX = _selectionRect.X;
                        _startY = _selectionRect.Y;
                        return;

                    }
                    else if (IsPointInSelection(px))
                    {
                        if (_transformStep == 0) // 第一次拖动
                        {
                            _originalRect = _selectionRect;
                        }
                        _transformStep++;
                        _draggingSelection = true;
                        _clickOffset = new Point(px.X - _selectionRect.X, px.Y - _selectionRect.Y);
                        ctx.ViewElement.CaptureMouse();
                        return;
                    }
                }

                // 开始新框选
                _selecting = true;
                _startPixel = px;
                _selectionRect = new Int32Rect((int)px.X, (int)px.Y, 0, 0);
                HidePreview(ctx);
                ctx.ViewElement.CaptureMouse();
                if (_selectionRect.Width != 0 && _selectionRect.Height != 0)
                    DrawOverlay(ctx, _selectionRect);
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
                // 背景图左上角位置（UI坐标）
                var imgPos = ctx.ViewElement.TranslatePoint(new Point(0, 0), ctx.SelectionPreview.Parent as UIElement);

                // 缩放比例（像素 → UI）
                double scaleX = ctx.ViewElement.ActualWidth / ctx.Surface.Bitmap.PixelWidth;
                double scaleY = ctx.ViewElement.ActualHeight / ctx.Surface.Bitmap.PixelHeight;

                // 转换到 UI 平移
                double uiX = imgPos.X + pixelX * scaleX;
                double uiY = imgPos.Y + pixelY * scaleY;

                ctx.SelectionPreview.RenderTransform = new TranslateTransform(uiX, uiY);
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

            public override void OnPointerMove(ToolContext ctx, Point viewPos)
            {
                //s(_selectionData.Length);
                var px = ctx.ToPixel(viewPos);

                // 光标样式
                if (_selectionData != null)
                {
                    var anchor = HitTestHandle(px, _selectionRect);
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
                            Mouse.OverrideCursor = null;
                            break;
                    }
                }

                // 缩放逻辑
                if (_resizing)
                {
                    double dx = px.X - _startMouse.X;
                    double dy = px.Y - _startMouse.Y;

                    // 更新选区矩形
                    switch (_currentAnchor)
                    {
                        case ResizeAnchor.TopLeft:
                            _selectionRect.X = (int)(_startX + dx);
                            _selectionRect.Y = (int)(_startY + dy);
                            _selectionRect.Width = (int)Math.Max(1, _startW - dx);
                            _selectionRect.Height = (int)Math.Max(1, _startH - dy);
                            break;
                        case ResizeAnchor.TopMiddle:
                            _selectionRect.Y = (int)(_startY + dy);
                            _selectionRect.Height = (int)Math.Max(1, _startH - dy);
                            break;
                        case ResizeAnchor.TopRight:
                            _selectionRect.Y = (int)(_startY + dy);
                            _selectionRect.Width = (int)Math.Max(1, _startW + dx);
                            _selectionRect.Height = (int)Math.Max(1, _startH - dy);
                            break;
                        case ResizeAnchor.LeftMiddle:
                            _selectionRect.X = (int)(_startX + dx);
                            _selectionRect.Width = (int)Math.Max(1, _startW - dx);
                            break;
                        case ResizeAnchor.RightMiddle:
                            _selectionRect.Width = (int)Math.Max(1, _startW + dx);
                            break;
                        case ResizeAnchor.BottomLeft:
                            _selectionRect.X = (int)(_startX + dx);
                            _selectionRect.Width = (int)Math.Max(1, _startW - dx);
                            _selectionRect.Height = (int)Math.Max(1, _startH + dy);
                            break;
                        case ResizeAnchor.BottomMiddle:
                            _selectionRect.Height = (int)Math.Max(1, _startH + dy);
                            break;
                        case ResizeAnchor.BottomRight:
                            _selectionRect.Width = (int)Math.Max(1, _startW + dx);
                            _selectionRect.Height = (int)Math.Max(1, _startH + dy);
                            break;
                    }

                    // 计算缩放比例
                    if (_originalRect.Width > 0 && _originalRect.Height > 0)
                    {
                        double scaleX = (double)_selectionRect.Width / _originalRect.Width;
                        double scaleY = (double)_selectionRect.Height / _originalRect.Height;

                        // 获取或创建 TransformGroup
                        var tg = ctx.SelectionPreview.RenderTransform as TransformGroup;
                        if (tg == null)
                        {
                            tg = new TransformGroup();
                            tg.Children.Add(new ScaleTransform(scaleX, scaleY));
                            tg.Children.Add(new TranslateTransform(_selectionRect.X, _selectionRect.Y));
                            ctx.SelectionPreview.RenderTransform = tg;
                        }
                        else
                        {
                            var s = tg.Children.OfType<ScaleTransform>().FirstOrDefault();
                            if (s != null)
                            {
                                s.ScaleX = scaleX;
                                s.ScaleY = scaleY;
                            }
                            var t = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
                            if (t != null)
                            {
                                t.X = _selectionRect.X;
                                t.Y = _selectionRect.Y;
                            }
                        }

                        ctx.SelectionPreview.Visibility = Visibility.Visible;
                    }

                    DrawOverlay(ctx, _selectionRect);
                    return;
                }

                if (_selecting)// 框选逻辑
                {
                    _selectionRect = MakeRect(_startPixel, px);
                    DrawOverlay(ctx, _selectionRect);
                }

                else if (_draggingSelection) // 拖动逻辑
                {
                    var mainWindow = System.Windows.Application.Current.MainWindow;
                    if (mainWindow != null)
                    {
                        // 将鼠标位置从视图元素坐标系转换到屏幕坐标系
                        Point mouseOnScreen = ctx.ViewElement.PointToScreen(viewPos);
                        var windowBoundsInPixels = GetWindowBoundsInPhysicalPixels(mainWindow);

                        // 检查鼠标是否在窗口边界之外
                        if (!windowBoundsInPixels.Contains(mouseOnScreen))
                        {

                            // 用户正在向外拖动，启动文件拖放操作
                            StartDragDropOperation(ctx);
                            _draggingSelection = false; // 停止内部拖动
                            return; // 退出事件处理器
                        }
                    }
                    int newX = (int)(px.X - _clickOffset.X);
                    int newY = (int)(px.Y - _clickOffset.Y);

                    // 更新 TransformGroup 中的 TranslateTransform
                    var tg = ctx.SelectionPreview.RenderTransform as TransformGroup;
                    if (tg != null)
                    {
                        var t = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
                        if (t != null)
                        {
                            t.X = newX;
                            t.Y = newY;
                        }
                    }
                    else if (ctx.SelectionPreview.RenderTransform is TranslateTransform singleT)
                    {
                        singleT.X = newX;
                        singleT.Y = newY;
                    }

                    ctx.SelectionPreview.Clip = new RectangleGeometry(
                        new Rect(0, 0, 1000, 1000)
                    );

                    Int32Rect tmprc = new Int32Rect(newX, newY, _selectionRect.Width, _selectionRect.Height);

                    double canvasW = ctx.Surface.Bitmap.PixelWidth;
                    double canvasH = ctx.Surface.Bitmap.PixelHeight;

                    // 选区左上角相对于画布的偏移
                    double offsetX = tmprc.X;
                    double offsetY = tmprc.Y;

                    double ratioX = (double)_selectionRect.Width / (double)_originalRect.Width;
                    double ratioY = (double)_selectionRect.Height / (double)_originalRect.Height;

                    // 计算在预览自身坐标系中的有效显示范围
                    double visibleX = Math.Max(0, -offsetX / ratioX);
                    double visibleY = Math.Max(0, -offsetY / ratioY);
                    double visibleW = Math.Min(tmprc.Width, (canvasW - offsetX) / ratioX);
                    double visibleH = Math.Min(tmprc.Height, (canvasH - offsetY) / ratioY);
                    Geometry visibleRect = new RectangleGeometry(new Rect(visibleX, visibleY, visibleW, visibleH));
                    if (visibleW > 0 && visibleH > 0)
                    {
                        ctx.SelectionPreview.Clip = visibleRect;
                    }
                    else
                    {
                        // 超出画布完全不可见时可以隐藏掉
                        ctx.SelectionPreview.Clip = null;
                        ctx.SelectionPreview.Visibility = Visibility.Collapsed;
                    }
                    DrawOverlay(ctx, tmprc);// 画布的尺寸

                }


                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {// 状态栏更新
                    ((MainWindow)System.Windows.Application.Current.MainWindow).SelectionSize =
                        $"{_selectionRect.Width}×{_selectionRect.Height}";
                });
            }

            public override void OnKeyDown(ToolContext ctx, System.Windows.Input.KeyEventArgs e)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    switch (e.Key)
                    {
                        case Key.X:
                            //public ITool CurrentTool => Select;
                            CutSelection(ctx, true);
                            e.Handled = true;
                            break;
                        case Key.C:
                            e.Handled = true;
                            CopySelection(ctx);
                            break;
                        case Key.V:
                            PasteSelection(ctx, false);
                            e.Handled = true;
                            break;
                    }
                }
                else
                {
                    switch (e.Key)
                    {
                        case Key.Delete:
                            CutSelection(ctx, false);
                            e.Handled = true;
                            break;
                    }
                }
            }

            public override void OnPointerUp(ToolContext ctx, Point viewPos)
            {
                ctx.ViewElement.ReleaseMouseCapture();
                var px = ctx.ToPixel(viewPos);

                if (_selecting)
                {
                    _selecting = false;
                    _selectionRect = MakeRect(_startPixel, px);

                    if (_selectionRect.Width > 0 && _selectionRect.Height > 0)
                    {
                        _selectionData = ctx.Surface.ExtractRegion(_selectionRect);
                        if (_selectionData == null || _selectionData.Length < _selectionRect.Width * _selectionRect.Height * 4)
                            return;

                        ctx.Undo.BeginStroke();
                        ctx.Undo.AddDirtyRect(_selectionRect);
                        ctx.Undo.CommitStroke(); // 保存这部分像素到栈
                        ClearRect(ctx, _selectionRect, ctx.EraserColor);

                        var previewBmp = new WriteableBitmap(_selectionRect.Width, _selectionRect.Height,

                            ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);
                        int stride = _selectionRect.Width * 4;
                        previewBmp.WritePixels(new Int32Rect(0, 0, _selectionRect.Width, _selectionRect.Height),
                                               _selectionData, stride, 0);

                        ctx.SelectionPreview.Source = previewBmp;
                        SetPreviewPosition(ctx, _selectionRect.X, _selectionRect.Y);

                        ctx.SelectionPreview.Visibility = Visibility.Visible;
                    }
                }
                else if (_draggingSelection)
                {
                    _draggingSelection = false;

                    double finalX = 0, finalY = 0;
                    _selectionRect = new Int32Rect((int)finalX, (int)finalY, _selectionRect.Width, _selectionRect.Height);

                    if (ctx.SelectionPreview.RenderTransform is TranslateTransform t1)
                    {
                        finalX = t1.X;
                        finalY = t1.Y;
                    }
                    else if (ctx.SelectionPreview.RenderTransform is TransformGroup tg)
                    {
                        var tt = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
                        if (tt != null)
                        {
                            finalX = tt.X;
                            finalY = tt.Y;
                        }
                    }

                    _selectionRect = new Int32Rect((int)finalX, (int)finalY, _selectionRect.Width, _selectionRect.Height);
                }

                if (_resizing)
                {
                    _resizing = false;
                    _currentAnchor = ResizeAnchor.None;
                    return;
                }
                if (_selectionRect.Width != 0 && _selectionRect.Height != 0)
                {
                    DrawOverlay(ctx, _selectionRect);
                }

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

            private bool IsPointInSelection(Point px)
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