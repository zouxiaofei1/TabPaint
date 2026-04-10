using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
            private void StartDragDropOperation(ToolContext ctx)
            {
                if (_selectionData == null) return;

                // 记录旋转信息以便跨窗口传递
                double dragAngle = _rotationAngle;
                Int32Rect dragPreRect = _preRotationRect;
                bool hasRotation = _preRotationSelectionData != null && Math.Abs(dragAngle) > 0.01;

                byte[] data;
                int width, height;

                if (hasRotation)
                {
                    data = _preRotationSelectionData;
                    width = _preRotationDataWidth;
                    height = _preRotationDataHeight;
                }
                else
                {
                    EnsureRotationBaked(ctx);
                    width = _originalRect.Width > 0 ? _originalRect.Width : _selectionRect.Width;
                    height = _originalRect.Height > 0 ? _originalRect.Height : _selectionRect.Height;
                    data = _selectionData;
                }

                if (width == 0 || height == 0) return;
                int dataStride = width * 4;
                var bitmapSource = BitmapSource.Create(
                    width, height,
                    ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                    PixelFormats.Bgra32, null,
                    data, dataStride);

                string tempFilePath = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"selection_{Guid.NewGuid()}.png"
                );
                // s(tempFilePath);
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
                    dataObject.SetData("TabPaintSelectionDrag", true);
                    dataObject.SetData("TabPaintSourceWindow", ctx.ParentWindow.GetHashCode());

                    if (hasRotation)
                    {
                        dataObject.SetData("TabPaintSelectionAngle", dragAngle);
                        dataObject.SetData("TabPaintSelectionPreRect", dragPreRect);
                    }

                    if (_hasLifted)
                    {
                        ctx.Undo.Undo();
                        _hasLifted = false;
                    }
                    HidePreview(ctx);
                    ctx.SelectionOverlay.Visibility = Visibility.Collapsed;

                    // 显示 DropZone 提示
                    var mw = ctx.ParentWindow;
                    if (mw._dropZone == null)
                    {
                        mw._dropZone = new UIHandlers.DropZoneWindow();
                        mw._dropZone.TabDropped += mw.OnDropZoneTabDropped;
                    }
                    mw._dropZone.ShowAtBottom();

                    DragDrop.DoDragDrop(ctx.ViewElement, dataObject, DragDropEffects.Copy | DragDropEffects.Move);

                    if (mw._dropZone != null) mw._dropZone.Hide();

                    _originalRect = new Int32Rect();
                    _selectionRect = new Int32Rect();
                    _transformStep = 0;
                    _selectionData = null;
                    DrawOverlay(ctx, _selectionRect);
                    ctx.ParentWindow.UpdateSelectionToolBarPosition();
                    ctx.IsDirty = true;
                }
                catch (Exception ex) { }
                finally
                {
                    if (System.IO.File.Exists(tempFilePath)) DeleteFileWithDelay(tempFilePath, 5000); // 延迟 5秒
                }
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
                    var bitmapToCopy = BitmapSource.Create(
                        width, height,
                        ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                        PixelFormats.Bgra32, null, data, stride);

                    DataObject dataObj = new DataObject();
                    dataObj.SetImage(bitmapToCopy);
                    using (var pngStream = new System.IO.MemoryStream())
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmapToCopy));
                        encoder.Save(pngStream);
                        var pngData = pngStream.ToArray();
                        var clipStream = new System.IO.MemoryStream(pngData);
                        dataObj.SetData("PNG", clipStream, false);
                    }

                    // 3. 内部标记
                    dataObj.SetData(MainWindow.InternalClipboardFormat, "TabPaintInternal");

                    ClipboardHelper.SetDataObjectWithRetry(dataObj, true);
                }
                catch (Exception) { }
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

            public void PasteSelection(ToolContext ctx, bool ins)
            {
                if (_selectionData != null) CommitSelection(ctx);

                BitmapSource? sourceBitmap = null;
                bool isInternalCopy = false;
                
                var dataObj = ClipboardHelper.GetDataObjectWithRetry();
                if (dataObj != null && dataObj.GetDataPresent(MainWindow.InternalClipboardFormat))
                {
                    isInternalCopy = true;
                }

                if (isInternalCopy && _clipboardData != null && _clipboardWidth > 0 && _clipboardHeight > 0)
                {
                    sourceBitmap = BitmapSource.Create(
                        _clipboardWidth, _clipboardHeight,
                        ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                        PixelFormats.Bgra32, null, _clipboardData, _clipboardWidth * 4);
                }
                else
                {
                    if (dataObj != null && ctx.ParentWindow.TryExtractBitmapFromDataObject(dataObj, out var extracted))
                    {
                        sourceBitmap = extracted;
                    }

                    // ★ 优先级4：文件拖放
                    if (sourceBitmap == null && dataObj != null && dataObj.GetDataPresent(DataFormats.FileDrop))
                    {
                        var fileList = dataObj.GetData(DataFormats.FileDrop) as string[];
                        if (fileList != null && fileList.Length > 0)
                        {
                            string filePath = fileList[0];
                            sourceBitmap = LoadImageFromFile(filePath);
                        }
                    }

                    // ★ 优先级5：内部缓存兜底（没有内部标记但有缓存数据）
                    if (sourceBitmap == null && _clipboardData != null && _clipboardWidth > 0 && _clipboardHeight > 0)
                    {
                        sourceBitmap = BitmapSource.Create(
                            _clipboardWidth, _clipboardHeight,
                            ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                            PixelFormats.Bgra32, null, _clipboardData, _clipboardWidth * 4);
                    }
                }

                if (sourceBitmap != null)
                {
                    InsertImageAsSelection(ctx, sourceBitmap);
                }
            }
            private BitmapSource? LoadImageFromFile(string path)
            {
                try
                {
                    string ext = System.IO.Path.GetExtension(path).ToLower();
                    string[] allowed = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp" };
                    if (!allowed.Contains(ext)) return null;

                    // 获取原始尺寸
                    int originalWidth = 0;
                    int originalHeight = 0;
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                        originalWidth = decoder.Frames[0].PixelWidth;
                        originalHeight = decoder.Frames[0].PixelHeight;
                    }

                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;

                    // 检查尺寸限制
                    const int maxSize = (int)AppConsts.MaxCanvasSize;
                    if (originalWidth > maxSize || originalHeight > maxSize)
                    {
                        if (originalWidth >= originalHeight)
                            bitmap.DecodePixelWidth = maxSize;
                        else
                            bitmap.DecodePixelHeight = maxSize;

                        ctxForTimer?.ParentWindow?.ShowToast("L_Toast_ImageTooLarge");
                    }

                    bitmap.EndInit();
                    bitmap.Freeze(); // 跨线程安全
                    return bitmap;
                }
                catch (Exception ex)
                {
                    try
                    {
                        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        bool isWebp = MainWindow.IsWebpFileOrStream(path, fs);
                        if (!isWebp)
                        {
                            System.Diagnostics.Debug.WriteLine("Load file from clipboard failed: " + ex.Message);
                            return null;
                        }

                        fs.Position = 0;
                        return MainWindow.DecodeWebpWithSkia(fs);
                    }
                    catch
                    {
                        System.Diagnostics.Debug.WriteLine("Load file from clipboard failed: " + ex.Message);
                        return null;
                    }
                }
            }
            public void CopySelection(ToolContext ctx)
            {
                if (_selectionData == null) SelectAll(ctx, false);
                EnsureRotationBaked(ctx);

                if (_selectionData != null)
                {
                    CopyToSystemClipboard(ctx);
                    _clipboardWidth = _originalRect.Width > 0 ? _originalRect.Width : _selectionRect.Width;
                    _clipboardHeight = _originalRect.Height > 0 ? _originalRect.Height : _selectionRect.Height;
                    _clipboardData = new byte[_selectionData.Length];
                    Array.Copy(_selectionData, _clipboardData, _selectionData.Length);
                }
            }

            public BitmapSource? GetSelectionBitmap(ToolContext ctx, int minWidth = 0, int minHeight = 0)
            {
                if (_selectionData == null) return null;
                EnsureRotationBaked(ctx);

                int width = _originalRect.Width > 0 ? _originalRect.Width : _selectionRect.Width;
                int height = _originalRect.Height > 0 ? _originalRect.Height : _selectionRect.Height;
                byte[] data = _selectionData;
                if (width < minWidth || height < minHeight) return null;

                try
                {
                    int stride = width * 4;
                    var bitmapSource = BitmapSource.Create(
                        width, height,
                        ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                        PixelFormats.Bgra32, null,
                        data, stride);
                    bitmapSource.Freeze();
                    return bitmapSource;
                }
                catch
                {
                    return null;
                }
            }

            public void InsertImageAsSelection(ToolContext ctx, BitmapSource sourceBitmap, bool expandCanvas = true, Point? dropPos = null, double? rotationAngle = null)
            {

                // 1. 提交当前的选区（如果有）
                if (_selectionData != null) CommitSelection(ctx);

                if (sourceBitmap == null) return;
                const int maxSize = (int)AppConsts.MaxCanvasSize;
                if (sourceBitmap.PixelWidth > maxSize || sourceBitmap.PixelHeight > maxSize)
                {
                    double scale = Math.Min((double)maxSize / sourceBitmap.PixelWidth, (double)maxSize / sourceBitmap.PixelHeight);
                    sourceBitmap = new TransformedBitmap(sourceBitmap, new ScaleTransform(scale, scale));
                    ctx.ParentWindow?.ShowToast("L_Toast_ImageTooLarge");
                }

                IsPasted = true;
                var mw = ctx.ParentWindow;

                if (sourceBitmap.Format != PixelFormats.Bgra32)
                {
                    sourceBitmap = new FormatConvertedBitmap(sourceBitmap, PixelFormats.Bgra32, null, 0);
                }

                double canvasDpiX = ctx.Surface.Bitmap.DpiX;
                double canvasDpiY = ctx.Surface.Bitmap.DpiY;

                // 允许一点点浮点误差
                if (Math.Abs(sourceBitmap.DpiX - canvasDpiX) > 1.0 || Math.Abs(sourceBitmap.DpiY - canvasDpiY) > 1.0)
                {
                    int w = sourceBitmap.PixelWidth;
                    int h = sourceBitmap.PixelHeight;
                    int stride = w * 4;
                    byte[] rawPixels = new byte[h * stride];
                    sourceBitmap.CopyPixels(rawPixels, stride, 0);
                    sourceBitmap = BitmapSource.Create(
                        w, h,
                        canvasDpiX, canvasDpiY, // 强行使用画布 DPI
                        PixelFormats.Bgra32,
                        null,
                        rawPixels,
                        stride);
                }
                int imgW = sourceBitmap.PixelWidth;
                int imgH = sourceBitmap.PixelHeight;
                int canvasW = ctx.Surface.Bitmap.PixelWidth;
                int canvasH = ctx.Surface.Bitmap.PixelHeight;

                bool _canvasChanged = false;

                if (expandCanvas && (imgW > canvasW || imgH > canvasH))
                {
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
                        newBmp.AddDirtyRect(new Int32Rect(0, 0, newW, newH));
                    }
                    newBmp.Unlock();

                    newBmp.WritePixels(oldRect, oldPixels, canvasW * 4, 0);
                    ctx.Surface.ReplaceBitmap(newBmp);
                    Int32Rect redoRect = new Int32Rect(0, 0, newW, newH);
                    byte[] redoPixels = ctx.Surface.ExtractRegion(redoRect);
                    mw.UpdateSelectionScalingMode();
                    ctx.Undo.PushTransformAction(oldRect, oldPixels, redoRect, redoPixels);
                    mw.NotifyCanvasSizeChanged(newW, newH);
                    mw.OnPropertyChanged("CanvasWidth");
                    mw.OnPropertyChanged("CanvasHeight");
                }


                int strideFinal = imgW * 4;
                var newData = new byte[imgH * strideFinal];
                sourceBitmap.CopyPixels(newData, strideFinal, 0);

                _selectionData = newData;
                int startX = 0;
                int startY = 0;

                if (dropPos.HasValue)
                {
                    Point px = ctx.ToPixel(dropPos.Value);
                    startX = (int)(px.X - imgW / 2.0);
                    startY = (int)(px.Y - imgH / 2.0);
                }
                else
                {
                    // 如果屏幕窗口显示不全画布，则粘贴到视图左上角（而不是画布左上角）
                    var sv = mw.ScrollContainer;
                    if (sv != null && (sv.ExtentWidth > sv.ViewportWidth || sv.ExtentHeight > sv.ViewportHeight))
                    {
                        // 将视图左上角 (0,0) 转换到画布像素坐标
                        Point viewTopLeft = sv.TranslatePoint(new Point(0, 0), ctx.ViewElement);
                        Point pixelPos = ctx.ToPixel(viewTopLeft);
                        startX = (int)Math.Max(0, pixelPos.X);
                        startY = (int)Math.Max(0, pixelPos.Y);
                    }
                }

                _selectionRect = new Int32Rect(startX, startY, imgW, imgH);
                _originalRect = _selectionRect;
                ctx.SelectionPreview.Source = new WriteableBitmap(sourceBitmap);
                Canvas.SetLeft(ctx.SelectionPreview, 0);
                Canvas.SetTop(ctx.SelectionPreview, 0);
                ctx.SelectionPreview.RenderTransform = new TranslateTransform(startX, startY);
                ctx.SelectionPreview.Visibility = Visibility.Visible;
                ctx.SelectionPreview.Width = imgW;
                ctx.SelectionPreview.Height = imgH;

                if (rotationAngle.HasValue && Math.Abs(rotationAngle.Value) > 0.01)
                {
                    _rotationAngle = rotationAngle.Value;
                    _preRotationSelectionData = (byte[])_selectionData.Clone();
                    _preRotationDataWidth = imgW;
                    _preRotationDataHeight = imgH;
                    _preRotationRect = _selectionRect;
                    _originalRect = _selectionRect;
                    _transformStep = 1;

                    // 应用旋转效果
                    UpdateRotation(ctx, (int)_rotationAngle, false);
                }
                else
                {
                    // 绘制 8 个句柄和虚线框
                    DrawOverlay(ctx, _selectionRect);
                    _transformStep = 0;
                }
                _hasLifted = true;

                mw.UpdateSelectionToolBarPosition();
                mw.SetCropButtonState();
                mw._canvasResizer.UpdateUI(); lag = 0;
            }


        }
    }
}
