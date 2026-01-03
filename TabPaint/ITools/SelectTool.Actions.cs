using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

//
//SelectTool类键鼠操作相关方法
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public partial class SelectTool : ToolBase
        {
            public bool HasActiveSelection => _selectionData != null;

            // 添加：执行删除选区的具体逻辑
            public void DeleteSelection(ToolContext ctx)
            {
                if (_selectionData == null) return;

                if (!_hasLifted)
                {
                    ctx.Undo.BeginStroke();

                    // 重要：告诉 Undo 管理器哪个区域变了
                    ctx.Undo.AddDirtyRect(_selectionRect);

                    // 执行擦除
                    ClearRect(ctx, _selectionRect, ctx.EraserColor);

                    // 提交到 Undo 栈
                    ctx.Undo.CommitStroke();
                    ctx.IsDirty = true;
                }
                HidePreview(ctx);
                if (ctx.SelectionOverlay != null)
                {
                    ctx.SelectionOverlay.Children.Clear();
                    ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
                }

                // 3. 彻底重置 SelectTool 状态
                _selectionData = null;
                _selectionRect = new Int32Rect(0, 0, 0, 0);
                _originalRect = new Int32Rect(0, 0, 0, 0);
                _hasLifted = false;
                _transformStep = 0;
                _draggingSelection = false;
                _resizing = false;
                Mouse.OverrideCursor = null;

                // 4. 通知 UI 更新
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                mw.SetCropButtonState();
                mw.SelectionSize = "0×0像素";
                mw.SetUndoRedoButtonState();
            }

            private void CopyToSystemClipboard(ToolContext ctx)
            {
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
                    DataObject dataObj = new DataObject();
                    dataObj.SetImage(bitmapToCopy);
                    dataObj.SetData(MainWindow.InternalClipboardFormat, "TabPaintInternal");

                    System.Windows.Clipboard.SetDataObject(dataObj, true);
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
                DeleteSelection(ctx);

            }

            // 在 SelectTool 类中添加
            public void InsertImageAsSelection(ToolContext ctx, BitmapSource sourceBitmap, bool expandCanvas = true)
            {
                // 1. 提交当前的选区（如果有）
                if (_selectionData != null) CommitSelection(ctx);

                if (sourceBitmap == null) return;
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;

                // --- FIX START: DPI 归一化与格式转换 ---

                // 1. 确保格式是 Bgra32
                if (sourceBitmap.Format != PixelFormats.Bgra32)
                {
                    sourceBitmap = new FormatConvertedBitmap(sourceBitmap, PixelFormats.Bgra32, null, 0);
                }

                // 2. 检查 DPI 是否匹配，如果不匹配，重建一个具有画布 DPI 的 BitmapSource
                // 这一步是修复 Preview 大小错位的关键
                double canvasDpiX = ctx.Surface.Bitmap.DpiX;
                double canvasDpiY = ctx.Surface.Bitmap.DpiY;

                // 允许一点点浮点误差
                if (Math.Abs(sourceBitmap.DpiX - canvasDpiX) > 1.0 || Math.Abs(sourceBitmap.DpiY - canvasDpiY) > 1.0)
                {
                    int w = sourceBitmap.PixelWidth;
                    int h = sourceBitmap.PixelHeight;
                    int stride = w * 4;
                    byte[] rawPixels = new byte[h * stride];

                    // 提取原始像素
                    sourceBitmap.CopyPixels(rawPixels, stride, 0);

                    // 使用画布的 DPI 重新创建 BitmapSource
                    sourceBitmap = BitmapSource.Create(
                        w, h,
                        canvasDpiX, canvasDpiY, // 强行使用画布 DPI
                        PixelFormats.Bgra32,
                        null,
                        rawPixels,
                        stride);
                }
                // --- FIX END ---

                int imgW = sourceBitmap.PixelWidth;
                int imgH = sourceBitmap.PixelHeight;
                int canvasW = ctx.Surface.Bitmap.PixelWidth;
                int canvasH = ctx.Surface.Bitmap.PixelHeight;

                bool _canvasChanged = false;

                // ... (中间扩充画布的逻辑保持不变) ...
                if (expandCanvas && (imgW > canvasW || imgH > canvasH))
                {
                    // ... (此处是你原有的画布扩充代码，无需改动) ...
                    _canvasChanged = true;
                    int newW = Math.Max(imgW, canvasW);
                    int newH = Math.Max(imgH, canvasH);

                    Int32Rect oldRect = new Int32Rect(0, 0, canvasW, canvasH);
                    byte[] oldPixels = ctx.Surface.ExtractRegion(oldRect);

                    var newBmp = new WriteableBitmap(newW, newH, ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);
                    newBmp.Lock();
                    unsafe
                    {
                        byte* p = (byte*)newBmp.BackBuffer;
                        int totalBytes = newBmp.BackBufferStride * newBmp.PixelHeight;
                        for (int i = 0; i < totalBytes; i++) p[i] = 255;
                    }
                    newBmp.Unlock();

                    newBmp.WritePixels(oldRect, oldPixels, canvasW * 4, 0);
                    ctx.Surface.ReplaceBitmap(newBmp);
                    Int32Rect redoRect = new Int32Rect(0, 0, newW, newH);
                    byte[] redoPixels = ctx.Surface.ExtractRegion(redoRect);
                    ctx.Undo.PushTransformAction(oldRect, oldPixels, redoRect, redoPixels);
                    mw.NotifyCanvasSizeChanged(newW, newH);
                    mw.OnPropertyChanged("CanvasWidth");
                    mw.OnPropertyChanged("CanvasHeight");
                }

                // --- 接下来的部分继续使用归一化后的 sourceBitmap ---

                int strideFinal = imgW * 4;
                var newData = new byte[imgH * strideFinal];
                sourceBitmap.CopyPixels(newData, strideFinal, 0);

                _selectionData = newData;
                _selectionRect = new Int32Rect(0, 0, imgW, imgH);
                _originalRect = _selectionRect;

                // 这里直接使用 WriteableBitmap 包装归一化后的 bitmap，DPI 已经是正确的了
                ctx.SelectionPreview.Source = new WriteableBitmap(sourceBitmap);

                // 默认放在左上角 (0,0)
                Canvas.SetLeft(ctx.SelectionPreview, 0);
                Canvas.SetTop(ctx.SelectionPreview, 0);
                ctx.SelectionPreview.RenderTransform = new TranslateTransform(0, 0);
                ctx.SelectionPreview.Visibility = Visibility.Visible;

                // 必须确保 Preview 的尺寸被重置，防止之前的 Transform 残留影响
                // 使用你之前定义的 SetPreviewPosition 或者手动设置
                ctx.SelectionPreview.Width = imgW;
                ctx.SelectionPreview.Height = imgH;

                // 绘制 8 个句柄和虚线框
                DrawOverlay(ctx, _selectionRect);
                _transformStep = 0;
                _hasLifted = true;
                //mw.FitToWindow();
                mw._canvasResizer.UpdateUI();
            }

            public void PasteSelection(ToolContext ctx, bool ins)
            {
                if (_selectionData != null) CommitSelection(ctx);

                BitmapSource? sourceBitmap = null;

                // --- 逻辑 A: 检查剪贴板是否直接包含位图 (如截图、浏览器右键复制图片) ---
                if (System.Windows.Clipboard.ContainsImage())
                {
                    sourceBitmap = System.Windows.Clipboard.GetImage();
                }
                // --- 逻辑 B: 检查剪贴板是否包含文件 (如在资源管理器 Ctrl+C 复制的文件) ---
                else if (System.Windows.Clipboard.ContainsData(System.Windows.DataFormats.FileDrop))
                {
                    var fileList = System.Windows.Clipboard.GetData(System.Windows.DataFormats.FileDrop) as string[];
                    if (fileList != null && fileList.Length > 0)
                    {
                        string filePath = fileList[0]; // 取第一个文件
                        sourceBitmap = LoadImageFromFile(filePath);
                    }
                }
                // --- 逻辑 C: 检查应用内自定义剪贴板 ---
                else if (_clipboardData != null)
                {
                    sourceBitmap = BitmapSource.Create(_clipboardWidth, _clipboardHeight,
                        ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                        PixelFormats.Bgra32, null, _clipboardData, _clipboardWidth * 4);
                }

                // 统一处理获取到的位图
                if (sourceBitmap != null)
                {
                    // 调用上一步建议中提取的统一插入逻辑
                    InsertImageAsSelection(ctx, sourceBitmap);
                }
            }
            private BitmapSource? LoadImageFromFile(string path)
            {
                try
                {
                    // 检查扩展名过滤非图片文件
                    string ext = System.IO.Path.GetExtension(path).ToLower();
                    string[] allowed = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff" };
                    if (!allowed.Contains(ext)) return null;

                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path);
                    // 必须使用 OnLoad，否则粘贴后如果删除/移动原文件，程序会崩溃或锁定文件
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze(); // 跨线程安全
                    return bitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Load file from clipboard failed: " + ex.Message);
                    return null;
                }
            }
            public void CopySelection(ToolContext ctx)
            {
                if (_selectionData == null) SelectAll(ctx, false);

                if (_selectionData != null)
                {
                    CopyToSystemClipboard(ctx);
                    _clipboardWidth = _originalRect.Width > 0 ? _originalRect.Width : _selectionRect.Width;
                    _clipboardHeight = _originalRect.Height > 0 ? _originalRect.Height : _selectionRect.Height;
                    _clipboardData = new byte[_selectionData.Length];
                    Array.Copy(_selectionData, _clipboardData, _selectionData.Length);
                }
            }


            public void SelectAll(ToolContext ctx, bool cut = true)
            {
                if (ctx.Surface?.Bitmap == null) return;

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
                ((MainWindow)System.Windows.Application.Current.MainWindow).SetCropButtonState();
            }
            public void CropToSelection(ToolContext ctx)
            {
                if (_selectionData == null || _selectionRect.Width <= 0 || _selectionRect.Height <= 0) return;

                var undoRect = new Int32Rect(0, 0, ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);
                var undoPixels = ctx.Surface.ExtractRegion(undoRect);
                var wb = ctx.Surface.Bitmap;
                int stride = wb.PixelWidth * (wb.Format.BitsPerPixel / 8);
                byte[] pixels = new byte[wb.PixelHeight * stride];

                wb.CopyPixels(pixels, stride, 0);
                byte[] finalSelectionData;
                int finalWidth = _selectionRect.Width;
                int finalHeight = _selectionRect.Height;
                int finalStride;

                // 检查是否进行过缩放
                if (_originalRect.Width > 0 && _originalRect.Height > 0 &&
                    (_originalRect.Width != _selectionRect.Width || _originalRect.Height != _selectionRect.Height))
                {
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

                var newBitmap = new WriteableBitmap(
                    finalWidth,
                    finalHeight,
                    ctx.Surface.Bitmap.DpiX,
                    ctx.Surface.Bitmap.DpiY,
                    PixelFormats.Bgra32,
                    null
                );

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
                Cleanup(ctx); // 使用你已有的清理方法
                ctx.Undo.PushTransformAction(undoRect, undoPixels, redoRect, redoPixels);
                ctx.IsDirty = true;
                ((MainWindow)System.Windows.Application.Current.MainWindow).NotifyCanvasSizeChanged(finalWidth, finalHeight);
                // 更新UI（例如Undo/Redo按钮的状态）
                ((MainWindow)System.Windows.Application.Current.MainWindow).SetUndoRedoButtonState();
            }

            /// <summary>
            /// 后面是鼠标键盘事件处理
            /// </summary>



            public override void OnPointerDown(ToolContext ctx, Point viewPos)
            {
                if (lag > 0) { lag--; return; }
                if (ctx.Surface.Bitmap == null) return;
                var px = ctx.ToPixel(viewPos);

                // 在选区外点击 → 提交并清除
                //if (_selectionData != null && !IsPointInSelection(px))
                //{
                //    if (HitTestHandle(px, _selectionRect) == ResizeAnchor.None)
                //    {
                //        CommitSelection(ctx);
                //        ClearSelections(ctx);
                //        return;
                //    }
                //}

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
                //if (_selectionRect.Width != 0 && _selectionRect.Height != 0)
                //   DrawOverlay(ctx, _selectionRect);
                ((MainWindow)System.Windows.Application.Current.MainWindow).SetCropButtonState();
            }

            private bool _hasLifted = false;
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
                    if (!_hasLifted) LiftSelectionFromCanvas(ctx);
                    double dx = px.X - _startMouse.X;
                    double dy = px.Y - _startMouse.Y;
                    double rightEdge = _startX + _startW;
                    double bottomEdge = _startY + _startH;
                    // 更新选区矩形
                    switch (_currentAnchor)
                    {
                        case ResizeAnchor.TopLeft:
                            int newW_TL = (int)Math.Max(1, _startW - dx);
                            _selectionRect.Width = newW_TL;
                            _selectionRect.X = (int)(rightEdge - newW_TL);
                            int newH_TL = (int)Math.Max(1, _startH - dy);
                            _selectionRect.Height = newH_TL;
                            _selectionRect.Y = (int)(bottomEdge - newH_TL);
                            break;
                        case ResizeAnchor.TopMiddle:
                            int newH_TM = (int)Math.Max(1, _startH - dy);
                            _selectionRect.Height = newH_TM;
                            _selectionRect.Y = (int)(bottomEdge - newH_TM);
                            break;
                        case ResizeAnchor.TopRight:
                            int newH_TR = (int)Math.Max(1, _startH - dy);
                            _selectionRect.Height = newH_TR;
                            _selectionRect.Y = (int)(bottomEdge - newH_TR);
                            _selectionRect.Width = (int)Math.Max(1, _startW + dx);
                            break;
                        case ResizeAnchor.LeftMiddle:
                            int newW_LM = (int)Math.Max(1, _startW - dx);
                            _selectionRect.Width = newW_LM;
                            _selectionRect.X = (int)(rightEdge - newW_LM);
                            break;
                        case ResizeAnchor.RightMiddle:
                            _selectionRect.Width = (int)Math.Max(1, _startW + dx);
                            break;
                        case ResizeAnchor.BottomLeft:
                            int newW_BL = (int)Math.Max(1, _startW - dx);
                            _selectionRect.Width = newW_BL;
                            _selectionRect.X = (int)(rightEdge - newW_BL);
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
                            if (s == null)
                            {
                                s = new ScaleTransform(1, 1);
                                tg.Children.Insert(0, s); // 插在最前面
                            }

                            // 现在安全地设置属性
                            s.ScaleX = scaleX;
                            s.ScaleY = scaleY;

                            var t = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
                            if (t == null)
                            {
                                t = new TranslateTransform(0, 0);
                                tg.Children.Add(t);
                            }
                            t.X = _selectionRect.X;
                            t.Y = _selectionRect.Y;
                        }

                        ctx.SelectionPreview.Visibility = Visibility.Visible;
                    }

                    DrawOverlay(ctx, _selectionRect);
                    return;
                }

                if (_selecting)// 框选逻辑
                {
                    _hasLifted = false;
                    _selectionRect = MakeRect(_startPixel, px);
                    if (_selectionRect.Width != 0 && _selectionRect.Height != 0)
                        DrawOverlay(ctx, _selectionRect);
                }

                else if (_draggingSelection) // 拖动逻辑
                {
                    if (!_hasLifted)
                    {
                        LiftSelectionFromCanvas(ctx);
                    }
                    var mainWindow = System.Windows.Application.Current.MainWindow;
                    if (mainWindow != null)
                    {
                        Point posInWindow = ctx.ViewElement.TranslatePoint(viewPos, mainWindow);

                        double margin = -5;
                        bool isOutside = posInWindow.X < margin ||
                                         posInWindow.Y < margin ||
                                         posInWindow.X > mainWindow.ActualWidth - margin ||
                                         posInWindow.Y > mainWindow.ActualHeight - margin;
                        if (isOutside)
                        {
                            // 释放捕获，否则系统拖放引擎无法接管鼠标
                            ctx.ViewElement.ReleaseMouseCapture();

                            // 启动拖放
                            StartDragDropOperation(ctx);

                            _draggingSelection = false;
                            return;
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
                    double visibleX = (int)Math.Max(0, -offsetX / ratioX);
                    double visibleY = (int)Math.Max(0, -offsetY / ratioY);


                    double visibleW = (int)Math.Min(tmprc.Width / ratioX, (canvasW - offsetX) / ratioX);
                    double visibleH = (int)Math.Min(tmprc.Height / ratioY, (canvasH - offsetY) / ratioY);

                    Int32Rect intRect = ClampRect(new Int32Rect((int)visibleX, (int)visibleY, (int)visibleW, (int)visibleH), ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);
                    Rect rect = new Rect(intRect.X, intRect.Y, intRect.Width, intRect.Height);
                    Geometry visibleRect = new RectangleGeometry(rect);
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
                        $"{_selectionRect.Width}×{_selectionRect.Height}像素";
                });
            }
            private void LiftSelectionFromCanvas(ToolContext ctx)
            {
                if (_hasLifted) return; // 如果已经抠过了，就别再抠了

                // 1. 记录 Undo（确保撤销能恢复原画布的这个洞）
                ctx.Undo.BeginStroke();
                ctx.Undo.AddDirtyRect(_originalRect);
                ctx.Undo.CommitStroke();

                // 2. 将原位置擦除为透明（或者背景色）
                ClearRect(ctx, ClampRect(_originalRect, ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight), ctx.EraserColor);

                // 3. 标记状态
                _hasLifted = true;
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
                            DeleteSelection(ctx);
                            e.Handled = true;
                            break;
                    }
                }
            }

            public void RotateSelection(ToolContext ctx, int angle)
            {
                if (_selectionData == null || _originalRect.Width == 0 || _originalRect.Height == 0) return;

                // 1. 数据处理部分 (保持不变) ------------------------------
                int oldW = _originalRect.Width;
                int oldH = _originalRect.Height;
                int stride = oldW * 4;

                var srcBmp = BitmapSource.Create(
                    oldW, oldH,
                    ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                    PixelFormats.Bgra32, null,
                    _selectionData, stride);

                var transform = new TransformedBitmap(srcBmp, new RotateTransform(angle));

                int newOriginalW = transform.PixelWidth;
                int newOriginalH = transform.PixelHeight;
                int newStride = newOriginalW * 4;

                byte[] newPixels = new byte[newOriginalH * newStride];
                transform.CopyPixels(newPixels, newStride, 0);

                _selectionData = newPixels;
                _originalRect.Width = newOriginalW;
                _originalRect.Height = newOriginalH;

                // 计算旋转后的中心点位置，保持中心不变
                double centerX = _selectionRect.X + _selectionRect.Width / 2.0;
                double centerY = _selectionRect.Y + _selectionRect.Height / 2.0;

                int newSelectionW = _selectionRect.Width;
                int newSelectionH = _selectionRect.Height;

                // 如果旋转90或270度，交换选区的宽高
                if (angle % 180 != 0)
                {
                    newSelectionW = _selectionRect.Height;
                    newSelectionH = _selectionRect.Width;
                }

                int newX = (int)Math.Round(centerX - newSelectionW / 2.0);
                int newY = (int)Math.Round(centerY - newSelectionH / 2.0);

                _selectionRect = new Int32Rect(newX, newY, newSelectionW, newSelectionH);

                // 2. UI 渲染修复部分 (Fix Start) --------------------------

                // 更新源图片
                var previewBmp = new WriteableBitmap(transform);
                ctx.SelectionPreview.Source = previewBmp;

                double dpiScaleX = 96.0 / ctx.Surface.Bitmap.DpiX;
                double dpiScaleY = 96.0 / ctx.Surface.Bitmap.DpiY;

                ctx.SelectionPreview.Width = newOriginalW * dpiScaleX;
                ctx.SelectionPreview.Height = newOriginalH * dpiScaleY;

                double scaleX = (double)newSelectionW / newOriginalW;
                double scaleY = (double)newSelectionH / newOriginalH;

                // 应用变换：缩放 + 位移
                var tg = new TransformGroup();
                tg.Children.Add(new ScaleTransform(scaleX, scaleY));
                // 再位移
                // 注意：newX, newY 是像素坐标，转换为逻辑坐标
                tg.Children.Add(new TranslateTransform(newX * dpiScaleX, newY * dpiScaleY));

                ctx.SelectionPreview.RenderTransform = tg;

                // 确保对齐
                Canvas.SetLeft(ctx.SelectionPreview, 0);
                Canvas.SetTop(ctx.SelectionPreview, 0);
                ctx.SelectionPreview.Visibility = Visibility.Visible;

                // 刷新虚线框
                DrawOverlay(ctx, _selectionRect);
                var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                mw.SelectionSize = $"{_selectionRect.Width}×{_selectionRect.Height}像素";
                mw.SetCropButtonState();
            }
            public BitmapSource GetSelectionCroppedBitmap()
            {
                if (_selectionData == null || _originalRect.Width <= 0 || _originalRect.Height <= 0)
                    return null;

                try
                {
                    // 1. 从原始字节数据创建基础 BitmapSource
                    // 注意：_originalRect 的宽高始终对应 _selectionData 的像素维度
                    int stride = _originalRect.Width * 4;

                    // 获取当前画布的 DPI，确保 OCR 识别精度一致
                    var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                    double dpiX = mw._surface?.Bitmap.DpiX ?? 96;
                    double dpiY = mw._surface?.Bitmap.DpiY ?? 96;

                    BitmapSource result = BitmapSource.Create(
                        _originalRect.Width,
                        _originalRect.Height,
                        dpiX,
                        dpiY,
                        PixelFormats.Bgra32,
                        null,
                        _selectionData,
                        stride
                    );

                    // 2. 处理缩放 (Resize)
                    // 如果视觉矩形 _selectionRect 的尺寸与原始数据 _originalRect 不一致，说明用户拖动了句柄进行缩放
                    if (_selectionRect.Width != _originalRect.Width || _selectionRect.Height != _originalRect.Height)
                    {
                        double scaleX = (double)_selectionRect.Width / _originalRect.Width;
                        double scaleY = (double)_selectionRect.Height / _originalRect.Height;

                        // 使用 TransformedBitmap 进行高质量缩放
                        result = new TransformedBitmap(result, new ScaleTransform(scaleX, scaleY));
                    }

                    // 冻结对象以便跨线程使用（OCR 通常在 Task 中运行）
                    result.Freeze();
                    return result;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("OCR 裁剪失败: " + ex.Message);
                    return null;
                }
            }

            public override void OnPointerUp(ToolContext ctx, Point viewPos)
            {
                if (lag > 0) { lag--; return; }
                ctx.ViewElement.ReleaseMouseCapture();
                var px = ctx.ToPixel(viewPos);

                if (_selecting)
                {
                    _selecting = false;

                    // 1. 原始计算出的矩形（可能超出画布）
                    var rawRect = MakeRect(_startPixel, px);

                    // 2. 【修复关键】将矩形限制在画布范围内
                    // 必须确保 _selectionRect 和后面提取数据用的矩形完全一致
                    _selectionRect = ClampRect(rawRect, ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);

                    if (_selectionRect.Width > 0 && _selectionRect.Height > 0)
                    {
                        // 3. 提取数据 (因为上面已经Clamp过了，这里提取的数据量就是精确匹配 _selectionRect 的)
                        _selectionData = ctx.Surface.ExtractRegion(_selectionRect);

                        // 4. 记录原始尺寸
                        _originalRect = _selectionRect;
                        var previewBmp = new WriteableBitmap(_selectionRect.Width, _selectionRect.Height,
                            ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);

                        previewBmp.WritePixels(new Int32Rect(0, 0, _selectionRect.Width, _selectionRect.Height),
                                               _selectionData, _selectionRect.Width * 4, 0);

                        ctx.SelectionPreview.Source = previewBmp;
                        ctx.SelectionPreview.RenderTransform = new TranslateTransform(0, 0);

                        SetPreviewPosition(ctx, _selectionRect.X, _selectionRect.Y);
                        ctx.SelectionPreview.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        Cleanup(ctx);
                    }
                }
                else if (_draggingSelection)
                {
                    _draggingSelection = false;

                    Point relativePoint = ctx.SelectionPreview.TranslatePoint(new Point(0, 0), ctx.ViewElement);

                    // 2. 将视觉坐标转换为位图像素坐标
                    Point pixelPoint = ctx.ToPixel(relativePoint);

                    // 3. 更新逻辑选区矩形
                    _selectionRect.X = (int)Math.Round(pixelPoint.X);
                    _selectionRect.Y = (int)Math.Round(pixelPoint.Y);
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
                ((MainWindow)System.Windows.Application.Current.MainWindow).SetCropButtonState();

            }

        }
    }
}