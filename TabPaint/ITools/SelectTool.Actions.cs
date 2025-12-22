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

            // 在 SelectTool 类中添加
            public void InsertImageAsSelection(ToolContext ctx, BitmapSource sourceBitmap)
            {
                // 1. 提交当前的选区（如果有）
                if (_selectionData != null) CommitSelection(ctx);

                if (sourceBitmap == null) return;

                // 强制转换为 Bgra32
                if (sourceBitmap.Format != PixelFormats.Bgra32)
                    sourceBitmap = new FormatConvertedBitmap(sourceBitmap, PixelFormats.Bgra32, null, 0);

                // --- 【自动扩展画布逻辑】 ---
                int imgW = sourceBitmap.PixelWidth;
                int imgH = sourceBitmap.PixelHeight;
                int canvasW = ctx.Surface.Bitmap.PixelWidth;
                int canvasH = ctx.Surface.Bitmap.PixelHeight;

                if (imgW > canvasW || imgH > canvasH)
                {
                    int newW = Math.Max(imgW, canvasW);
                    int newH = Math.Max(imgH, canvasH);

                    // 记录旧状态用于撤销
                    Int32Rect oldRect = new Int32Rect(0, 0, canvasW, canvasH);
                    byte[] oldPixels = ctx.Surface.ExtractRegion(oldRect);

                    // 创建新位图并填充白色
                    var newBmp = new WriteableBitmap(newW, newH, ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);
                    newBmp.Lock();
                    unsafe
                    {
                        byte* p = (byte*)newBmp.BackBuffer;
                        int totalBytes = newBmp.BackBufferStride * newBmp.PixelHeight;
                        // 快速填充白色 (B G R A)
                        for (int i = 0; i < totalBytes; i++) p[i] = 255;
                    }
                    newBmp.Unlock();

                    // 写回旧内容
                    newBmp.WritePixels(oldRect, oldPixels, canvasW * 4, 0);
                    ctx.Surface.ReplaceBitmap(newBmp);

                    // 压入撤销栈
                    Int32Rect redoRect = new Int32Rect(0, 0, newW, newH);
                    byte[] redoPixels = ctx.Surface.ExtractRegion(redoRect);
                    ctx.Undo.PushTransformAction(oldRect, oldPixels, redoRect, redoPixels);

                    // 通知 UI 更新
                    var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                    mw.OnPropertyChanged("CanvasWidth");
                    mw.OnPropertyChanged("CanvasHeight");
                }

                // --- 【初始化选区状态】 ---
                int stride = imgW * 4;
                var newData = new byte[imgH * stride];
                sourceBitmap.CopyPixels(newData, stride, 0);

                _selectionData = newData;
                _selectionRect = new Int32Rect(0, 0, imgW, imgH);
                _originalRect = _selectionRect;

                // 设置预览图源
                ctx.SelectionPreview.Source = new WriteableBitmap(sourceBitmap);

                // 默认放在左上角 (0,0)
                Canvas.SetLeft(ctx.SelectionPreview, 0);
                Canvas.SetTop(ctx.SelectionPreview, 0);
                ctx.SelectionPreview.RenderTransform = new TranslateTransform(0, 0);
                ctx.SelectionPreview.Visibility = Visibility.Visible;

                // 绘制 8 个句柄和虚线框
                DrawOverlay(ctx, _selectionRect);
                _transformStep = 0;
            }

            public void PasteSelection(ToolContext ctx, bool ins)
            {
                // 1. 提交当前的选区（如果有）
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

            // 安全加载文件的辅助方法
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

            /// <summary>
            /// 后面是鼠标键盘事件处理
            /// </summary>



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
                        //s(_clickOffset);
                        //ctx.ViewElement.CaptureMouse();
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
                ((MainWindow)System.Windows.Application.Current.MainWindow).SetCropButtonState();
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
                        // 1. 提取数据
                        _selectionData = ctx.Surface.ExtractRegion(_selectionRect);

                        // --- 核心修复：记录原始尺寸 ---
                        _originalRect = _selectionRect;
                        // ---------------------------

                        ctx.Undo.BeginStroke();
                        ctx.Undo.AddDirtyRect(_selectionRect);
                        ctx.Undo.CommitStroke();

                        // 2. 擦除原画布内容
                        ClearRect(ctx, _selectionRect, ctx.EraserColor);

                        // 3. 创建预览
                        var previewBmp = new WriteableBitmap(_selectionRect.Width, _selectionRect.Height,
                            ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);

                        previewBmp.WritePixels(new Int32Rect(0, 0, _selectionRect.Width, _selectionRect.Height),
                                               _selectionData, _selectionRect.Width * 4, 0);
                        //s(_selectionRect.Width);
                        ctx.SelectionPreview.Source = previewBmp;

                        // 重置变换，确保预览图从 1:1 开始
                        ctx.SelectionPreview.RenderTransform = new TranslateTransform(0, 0);
                        SetPreviewPosition(ctx, _selectionRect.X, _selectionRect.Y);
                        ctx.SelectionPreview.Visibility = Visibility.Visible;
                    }
                }

                else if (_draggingSelection)
                {
                    _draggingSelection = false;

                    // 1. 获取 Preview 控件左上角相对于 ViewElement (画布容器) 的绝对坐标
                    // 无论你是用 Canvas.Left 还是 RenderTransform 移动的，这行代码都能拿到最终视觉位置
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