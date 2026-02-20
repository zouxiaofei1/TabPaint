//
//CanvasOperation.cs
//关于图片的一些操作方法，包括画布尺寸调整、旋转、翻转、像素颜色获取和自动裁剪等功能。
//
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using SkiaSharp;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void ResizeCanvasDimensions(int newWidth, int newHeight)
        {
            var oldBitmap = _surface.Bitmap;
            if (oldBitmap == null) return;
            if (oldBitmap.PixelWidth == newWidth && oldBitmap.PixelHeight == newHeight) return;

            int oldW = oldBitmap.PixelWidth;
            int oldH = oldBitmap.PixelHeight;
            int oldStride = oldBitmap.BackBufferStride;
            var undoRect = new Int32Rect(0, 0, oldW, oldH);
            byte[] undoPixels = _surface.ExtractRegion(undoRect);
            var newBitmap = new WriteableBitmap(newWidth, newHeight, oldBitmap.DpiX, oldBitmap.DpiY, PixelFormats.Bgra32, null);
            int newStride = newBitmap.BackBufferStride;

            newBitmap.Lock();
            oldBitmap.Lock();
            try
            {
                unsafe
                {
                    byte* pNewBack = (byte*)newBitmap.BackBuffer;
                    byte* pOldBack = (byte*)oldBitmap.BackBuffer;
                    long totalSize = (long)newHeight * newStride;
                    System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref *pNewBack, (int)totalSize).Fill(AppConsts.ColorComponentMax);
                    int destX = (newWidth - oldW) / 2;
                    int destY = (newHeight - oldH) / 2;
                    int srcX = 0, srcY = 0;
                    int copyW = oldW, copyH = oldH;

                    if (destX < 0) { srcX = -destX; copyW = newWidth; destX = 0; }
                    if (destY < 0) { srcY = -destY; copyH = newHeight; destY = 0; }
                    copyW = Math.Min(copyW, oldW - srcX);
                    copyH = Math.Min(copyH, oldH - srcY);

                    if (copyW > 0 && copyH > 0)
                    {
                        int bytesPerRow = copyW * AppConsts.BytesPerPixel;
                        for (int y = 0; y < copyH; y++)
                        {
                            byte* pSrcLine = pOldBack + (srcY + y) * oldStride + (srcX * AppConsts.BytesPerPixel);
                            byte* pDestLine = pNewBack + (destY + y) * newStride + (destX * AppConsts.BytesPerPixel);
                            Buffer.MemoryCopy(pSrcLine, pDestLine, bytesPerRow, bytesPerRow);
                        }
                    }
                }
                newBitmap.AddDirtyRect(new Int32Rect(0, 0, newWidth, newHeight));
            }
            finally
            {
                oldBitmap.Unlock();
                newBitmap.Unlock();
            }
            var redoRect = new Int32Rect(0, 0, newWidth, newHeight);
            byte[] redoPixels = new byte[newHeight * newStride];
            newBitmap.CopyPixels(redoRect, redoPixels, newStride, 0);

            _surface.ReplaceBitmap(newBitmap);
            _bitmap = newBitmap;
            _undo.PushTransformAction(undoRect, undoPixels, redoRect, redoPixels);

            NotifyCanvasSizeChanged(newWidth, newHeight);
            NotifyCanvasChanged();
            SetUndoRedoButtonState();
        }

        private void ApplyTransform(System.Windows.Media.Transform transform)
        {
            if (BackgroundImage.Source is not BitmapSource src || _surface?.Bitmap == null)
                return;

            var undoRect = new Int32Rect(0, 0, _surface.Bitmap.PixelWidth, _surface.Bitmap.PixelHeight); 
            var undoPixels = _surface.ExtractRegion(undoRect);
            if (undoPixels == null) return; 
            var transformedBmp = new TransformedBitmap(src, transform); 
            var newBitmap = new WriteableBitmap(transformedBmp);

            var redoRect = new Int32Rect(0, 0, newBitmap.PixelWidth, newBitmap.PixelHeight);  
            int redoStride = newBitmap.BackBufferStride;
            var redoPixels = new byte[redoStride * redoRect.Height];
            newBitmap.CopyPixels(redoPixels, redoStride, 0);

            _bitmap = newBitmap;
            BackgroundImage.Source = _bitmap;
            _surface.Attach(_bitmap);
            _surface.ReplaceBitmap(_bitmap);

            _undo.PushTransformAction(undoRect, undoPixels, redoRect, redoPixels);   
            NotifyCanvasSizeChanged(newBitmap.PixelWidth, newBitmap.PixelHeight);
            SetUndoRedoButtonState();
        }

        private void RotateBitmap(int angle)
        {
            if (_tools.Select is SelectTool st && st.HasActiveSelection)  
            {
                st.RotateSelection(_ctx, angle);
                return; 
            }
            if (_router.CurrentTool is ShapeTool shapetool && _router.GetSelectTool()?._selectionData != null)
            {
                _router.GetSelectTool()?.RotateSelection(_ctx, angle);
                return; 
            }
            ApplyTransform(new RotateTransform(angle));
            NotifyCanvasChanged();
            _canvasResizer.UpdateUI(); 
        }

        private Color GetPixelColor(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _bitmap.PixelWidth || y >= _bitmap.PixelHeight) return Colors.Transparent;
            _bitmap.Lock();
            try
            {
                unsafe
                {
                    byte* ptr = (byte*)_bitmap.BackBuffer;
                    int stride = _bitmap.BackBufferStride;
                    byte* pixel = ptr + y * stride + x * AppConsts.BytesPerPixel;
                    return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
                }
            }
            finally {  _bitmap.Unlock(); }
        }

        private void DrawPixel(int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= _bitmap.PixelWidth || y >= _bitmap.PixelHeight) return;

            _bitmap.Lock();
            unsafe
            {
                IntPtr pBackBuffer = _bitmap.BackBuffer;
                int stride = _bitmap.BackBufferStride;
                byte* p = (byte*)pBackBuffer + y * stride + x * AppConsts.BytesPerPixel;
                p[0] = color.B;
                p[1] = color.G;
                p[2] = color.R;
                p[3] = color.A;
            }
            _bitmap.AddDirtyRect(new Int32Rect(x, y, 1, 1));
            _bitmap.Unlock();
        }

        private void FlipBitmap(bool flipVertical)
        {
            double cx = _bitmap.PixelWidth / 2.0;
            double cy = _bitmap.PixelHeight / 2.0;
            ApplyTransform(flipVertical ? new ScaleTransform(1, -1, cx, cy) : new ScaleTransform(-1, 1, cx, cy));
        }
        private BitmapSource CreateWhiteThumbnail()  
        {
            int w = AppConsts.DefaultThumbnailWidth; int h = AppConsts.DefaultThumbnailHeight;
            var bmp = new RenderTargetBitmap(w, h, AppConsts.StandardDpi, AppConsts.StandardDpi, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var ctx = visual.RenderOpen())
            {
                ctx.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, h));
            }
            bmp.Render(visual);
            bmp.Freeze();
            return bmp;
        }

        private byte[] ExtractRegionFromSnapshot(byte[] fullData, Int32Rect rect, int stride)
        {
            const int bytesPerPixel = AppConsts.BytesPerPixel;
            byte[] region = new byte[rect.Height * rect.Width * bytesPerPixel];

            for (int row = 0; row < rect.Height; row++)
            {
                int srcOffset = (rect.Y + row) * stride + rect.X * bytesPerPixel;
                int dstOffset = row * rect.Width * bytesPerPixel;
                Buffer.BlockCopy(fullData, srcOffset, region, dstOffset, rect.Width * bytesPerPixel);
            }
            return region;
        }

        private static Int32Rect ClampRect(Int32Rect rect, int maxWidth, int maxHeight)
        {
            int left = Math.Max(0, rect.X);
            int top = Math.Max(0, rect.Y);
            int right = Math.Min(maxWidth, rect.X + rect.Width);
            int bottom = Math.Min(maxHeight, rect.Y + rect.Height);
            int width = Math.Max(0, right - left);
            int height = Math.Max(0, bottom - top);
            return new Int32Rect(left, top, width, height);
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
                    byte* rowPtr = basePtr + y * stride + rect.X * AppConsts.BytesPerPixel;
                    for (int x = 0; x < rect.Width; x++)
                    {
                        rowPtr[0] = color.B;
                        rowPtr[1] = color.G;
                        rowPtr[2] = color.R;
                        rowPtr[3] = color.A;
                        rowPtr += AppConsts.BytesPerPixel;
                    }
                }
            }
            ctx.Surface.Bitmap.AddDirtyRect(rect);
            ctx.Surface.Bitmap.Unlock();
        }

        private void Clean_bitmap(int _bmpWidth, int _bmpHeight)
        {
            _bitmap = new WriteableBitmap(_bmpWidth, _bmpHeight, AppConsts.StandardDpi, AppConsts.StandardDpi, PixelFormats.Bgra32, null);
            BackgroundImage.Source = _bitmap;

            _bitmap.Lock();

            if (_undo != null)
            {
                _undo.ClearUndo();
                _undo.ClearRedo();
            }

            if (_surface == null)
                _surface = new CanvasSurface(_bitmap);
            else
                _surface.Attach(_bitmap);

            unsafe
            {
                IntPtr pBackBuffer = _bitmap.BackBuffer;
                int stride = _bitmap.BackBufferStride;
                for (int y = 0; y < _bmpHeight; y++)
                {
                    byte* row = (byte*)pBackBuffer + y * stride;
                    for (int x = 0; x < _bmpWidth; x++)
                    {
                        row[x * AppConsts.BytesPerPixel + 0] = AppConsts.ColorComponentMax; // B
                        row[x * AppConsts.BytesPerPixel + 1] = AppConsts.ColorComponentMax; // G
                        row[x * AppConsts.BytesPerPixel + 2] = AppConsts.ColorComponentMax; // R
                        row[x * AppConsts.BytesPerPixel + 3] = AppConsts.ColorComponentMax; // A
                    }
                }
            }
            _bitmap.AddDirtyRect(new Int32Rect(0, 0, _bmpWidth, _bmpHeight));
            _bitmap.Unlock();

            double imgWidth = _bitmap.Width;
            double imgHeight = _bitmap.Height;

            NotifyCanvasSizeChanged(imgWidth, imgHeight);
            UpdateWindowTitle();
            FitToWindow();
            SetBrushStyle(BrushStyle.Round);
        }

        public void NotifyCanvasSizeChanged(double pixwidth, double pixheight)
        {
            BackgroundImage.Width = pixwidth;
            BackgroundImage.Height = pixheight;
            UpdateImageSizeStatus(pixwidth, pixheight);
            UpdateWindowTitle();
        }

        public void UpdateImageSizeStatus(double width, double height)
        {
            _imageSize = $"{(int)width}×{(int)height}" + LocalizationManager.GetString("L_Main_Unit_Pixel");
            OnPropertyChanged(nameof(ImageSize));
        }

        private void ResizeCanvas(int newWidth, int newHeight)
        {
            var oldBitmap = _surface.Bitmap;
            if (oldBitmap == null) return;
            if (oldBitmap.PixelWidth == newWidth && oldBitmap.PixelHeight == newHeight) return;

            var undoRect = new Int32Rect(0, 0, oldBitmap.PixelWidth, oldBitmap.PixelHeight);
            var undoPixels = _surface.ExtractRegion(undoRect);

            WriteableBitmap newBitmap;
            try
            {
                BitmapSource bgraOld = (oldBitmap.Format == PixelFormats.Bgra32) ? oldBitmap : new FormatConvertedBitmap(oldBitmap, PixelFormats.Bgra32, null, 0);
                using var skBitmap = new SKBitmap(bgraOld.PixelWidth, bgraOld.PixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
                bgraOld.CopyPixels(new Int32Rect(0, 0, bgraOld.PixelWidth, bgraOld.PixelHeight), skBitmap.GetPixels(), bgraOld.PixelHeight * (bgraOld.PixelWidth * 4), bgraOld.PixelWidth * 4);

                SKFilterQuality quality = SKFilterQuality.High;
                var appMode = SettingsManager.Instance.Current.ResamplingMode;
                switch (appMode)
                {
                    case AppResamplingMode.Bilinear: quality = SKFilterQuality.Low; break;
                    case AppResamplingMode.Fant: quality = SKFilterQuality.High; break;
                    case AppResamplingMode.HighQuality: quality = SKFilterQuality.High; break;
                }

                using var scaledBitmap = new SKBitmap(newWidth, newHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
                skBitmap.ScalePixels(scaledBitmap, quality);

                newBitmap = new WriteableBitmap(newWidth, newHeight, oldBitmap.DpiX, oldBitmap.DpiY, PixelFormats.Bgra32, null);
                newBitmap.Lock();
                unsafe
                {
                    long bytesToCopy = (long)newHeight * newBitmap.BackBufferStride;
                    Buffer.MemoryCopy((void*)scaledBitmap.GetPixels(), (void*)newBitmap.BackBuffer, bytesToCopy, bytesToCopy);
                }
                newBitmap.AddDirtyRect(new Int32Rect(0, 0, newWidth, newHeight));
                newBitmap.Unlock();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ResizeCanvas Skia Error: {ex.Message}");
                var transform = new ScaleTransform((double)newWidth / oldBitmap.PixelWidth, (double)newHeight / oldBitmap.PixelHeight);
                var transformedBitmap = new TransformedBitmap(oldBitmap, transform);
                newBitmap = new WriteableBitmap(new FormatConvertedBitmap(transformedBitmap, PixelFormats.Bgra32, null, 0));
            }

            var redoRect = new Int32Rect(0, 0, newBitmap.PixelWidth, newBitmap.PixelHeight);
            byte[] redoPixels = new byte[newBitmap.PixelHeight * newBitmap.BackBufferStride];
            newBitmap.CopyPixels(redoRect, redoPixels, newBitmap.BackBufferStride, 0);

            _surface.ReplaceBitmap(newBitmap);
            _bitmap = newBitmap;
            _undo.PushTransformAction(undoRect, undoPixels, redoRect, redoPixels);

            NotifyCanvasSizeChanged(newWidth, newHeight);
            NotifyCanvasChanged();
            SetUndoRedoButtonState();
            if (_canvasResizer != null) _canvasResizer.UpdateUI();
        }
        private void ConvertToBlackAndWhite(WriteableBitmap bmp)
        {
            bmp.Lock();
            unsafe
            {
                byte* basePtr = (byte*)bmp.BackBuffer;
                int stride = bmp.BackBufferStride;
                int height = bmp.PixelHeight;
                int width = bmp.PixelWidth;
                Parallel.For(0, height, y =>
                {
                    byte* row = basePtr + y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        byte b = row[x * AppConsts.BytesPerPixel];
                        byte g = row[x * AppConsts.BytesPerPixel + 1];
                        byte r = row[x * AppConsts.BytesPerPixel + 2];
                        byte gray = (byte)(r * AppConsts.GrayWeightR + g * AppConsts.GrayWeightG + b * AppConsts.GrayWeightB); 
                        row[x * AppConsts.BytesPerPixel] = gray;
                        row[x * AppConsts.BytesPerPixel + 1] = gray;
                        row[x * AppConsts.BytesPerPixel + 2] = gray; 
                    }
                });
            }
            bmp.AddDirtyRect(new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight));
            bmp.Unlock();
        }
        private void AutoCrop()
        {
            if (_surface?.Bitmap == null) return;

            var bmp = _surface.Bitmap;
            int width = bmp.PixelWidth;
            int height = bmp.PixelHeight;

            bmp.Lock();
            Int32Rect cropRect = new Int32Rect(0, 0, width, height);
            unsafe
            {
                byte* basePtr = (byte*)bmp.BackBuffer;
                int stride = bmp.BackBufferStride;

                bool IsEmpty(byte* pixel)
                {
                    byte b = pixel[0];
                    byte g = pixel[1];
                    byte r = pixel[2];
                    byte a = pixel[3];
                    byte max = AppConsts.ColorComponentMax;
                    return a == 0 || (r == max && g == max && b == max);
                }

                int top = 0, bottom = height - 1, left = 0, right = width - 1;

                for (; top < height; top++)
                {
                    bool rowHasContent = false;
                    byte* rowPtr = basePtr + top * stride;
                    for (int x = 0; x < width; x++)
                    {
                        if (!IsEmpty(rowPtr + x * AppConsts.BytesPerPixel)) { rowHasContent = true; break; }
                    }
                    if (rowHasContent) break;
                }

                if (top == height)
                {
                    bmp.Unlock();
                    ShowToast("L_Toast_NoContentToCrop");
                    return;
                }

                for (; bottom >= top; bottom--)
                {
                    bool rowHasContent = false;
                    byte* rowPtr = basePtr + bottom * stride;
                    for (int x = 0; x < width; x++)
                    {
                        if (!IsEmpty(rowPtr + x * AppConsts.BytesPerPixel)) { rowHasContent = true; break; }
                    }
                    if (rowHasContent) break;
                }
                for (; left < width; left++)
                {
                    bool colHasContent = false;
                    for (int y = top; y <= bottom; y++)
                    {
                        byte* pixel = basePtr + y * stride + left * AppConsts.BytesPerPixel;
                        if (!IsEmpty(pixel)) { colHasContent = true; break; }
                    }
                    if (colHasContent) break;
                }
                for (; right >= left; right--)
                {
                    bool colHasContent = false;
                    for (int y = top; y <= bottom; y++)
                    {
                        byte* pixel = basePtr + y * stride + right * AppConsts.BytesPerPixel;
                        if (!IsEmpty(pixel)) { colHasContent = true; break; }
                    }
                    if (colHasContent) break;
                }
                cropRect = new Int32Rect(left, top, right - left + 1, bottom - top + 1);
            }
            bmp.Unlock();
            if (cropRect.Width == width && cropRect.Height == height)
            {
                ShowToast("L_Toast_MinSize");
                return;
            }
            ApplyAutoCrop(cropRect);
        }

        private BitmapSource ConvertToWhiteBackground(BitmapSource source)
        {
            if (source == null) return null;
            var drawingVisual = new DrawingVisual();
            using (var context = drawingVisual.RenderOpen())
            {
                var rect = new Rect(0, 0, source.Width, source.Height);
                context.DrawRectangle(Brushes.White, null, rect);
                context.DrawImage(source, rect);
            }
            var rtb = new RenderTargetBitmap(source.PixelWidth, source.PixelHeight, source.DpiX, source.DpiY, PixelFormats.Pbgra32);
            rtb.Render(drawingVisual);
            rtb.Freeze();
            return rtb;
        }

        private BitmapScalingMode GetWpfScalingMode()
        {
            var appMode = SettingsManager.Instance.Current.ResamplingMode;
            switch (appMode)
            {
                case AppResamplingMode.Bilinear: return BitmapScalingMode.Linear;
                case AppResamplingMode.Fant:
                case AppResamplingMode.HighQuality: return BitmapScalingMode.HighQuality;
                default: return BitmapScalingMode.Unspecified;
            }
        }

        private BitmapSource ResampleBitmap(BitmapSource source, int width, int height)
        {
            try
            {
                BitmapSource bgraSrc = (source.Format == PixelFormats.Bgra32) ? source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                using var skSrc = new SKBitmap(bgraSrc.PixelWidth, bgraSrc.PixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
                bgraSrc.CopyPixels(new Int32Rect(0, 0, bgraSrc.PixelWidth, bgraSrc.PixelHeight), skSrc.GetPixels(), bgraSrc.PixelHeight * (bgraSrc.PixelWidth * 4), bgraSrc.PixelWidth * 4);

                using var skDest = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                SKFilterQuality quality = SKFilterQuality.High;
                var appMode = SettingsManager.Instance.Current.ResamplingMode;
                switch (appMode)
                {
                    case AppResamplingMode.Bilinear: quality = SKFilterQuality.Low; break;
                    case AppResamplingMode.Fant: quality = SKFilterQuality.High; break;
                    case AppResamplingMode.HighQuality: quality = SKFilterQuality.High; break;
                }
                skSrc.ScalePixels(skDest, quality);
                return SkiaBitmapToWpfSource(skDest);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ResampleBitmap Skia Error: {ex.Message}");
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    RenderOptions.SetBitmapScalingMode(visual, GetWpfScalingMode());
                    dc.DrawImage(source, new Rect(0, 0, width, height));
                }
                var rtb = new RenderTargetBitmap(width, height, source.DpiX, source.DpiY, PixelFormats.Pbgra32);
                rtb.Render(visual);
                return (rtb.Format != PixelFormats.Bgra32) ? new FormatConvertedBitmap(rtb, PixelFormats.Bgra32, null, 0) : rtb;
            }
        }
        public static Int32Rect MakeRect(Point p1, Point p2)
        {
            int x = (int)Math.Min(p1.X, p2.X);
            int y = (int)Math.Min(p1.Y, p2.Y);
            int w = Math.Abs((int)p1.X - (int)p2.X);
            int h = Math.Abs((int)p1.Y - (int)p2.Y);
            return new Int32Rect(x, y, w, h);
        }
        private void ApplyAutoCrop(Int32Rect cropRect)
        {
            var oldBitmap = _surface.Bitmap;
            var undoRect = new Int32Rect(0, 0, oldBitmap.PixelWidth, oldBitmap.PixelHeight);
            var undoPixels = _surface.ExtractRegion(undoRect);
            var newPixels = _surface.ExtractRegion(cropRect);
            var newBitmap = new WriteableBitmap(cropRect.Width, cropRect.Height, oldBitmap.DpiX, oldBitmap.DpiY, PixelFormats.Bgra32, null);
            newBitmap.WritePixels(new Int32Rect(0, 0, cropRect.Width, cropRect.Height), newPixels, newBitmap.BackBufferStride, 0);
            var redoRect = new Int32Rect(0, 0, cropRect.Width, cropRect.Height);
            _surface.ReplaceBitmap(newBitmap);
            _bitmap = newBitmap;
            BackgroundImage.Source = _bitmap;
            _undo.PushTransformAction(undoRect, undoPixels, redoRect, newPixels);
            NotifyCanvasSizeChanged(newBitmap.PixelWidth, newBitmap.PixelHeight);
            NotifyCanvasChanged();
            _canvasResizer.UpdateUI();
            ShowToast(string.Format("Cropped: {0}x{1}", cropRect.Width, cropRect.Height));
        }
        private static unsafe void AlphaBlendBatch(byte[] sourcePixels, byte[] destPixels, int width, int height, int stride, int sourceStartIdx, double globalOpacity)
        {
            int opacityScale = (int)(globalOpacity * 255);

            if (opacityScale <= 0) return;

            fixed (byte* pSrcBase = sourcePixels)
            fixed (byte* pDstBase = destPixels)
            {
                for (int row = 0; row < height; row++)
                {
                    byte* pSrcRow = pSrcBase + sourceStartIdx + (row * stride);
                    byte* pDstRow = pDstBase + (row * stride);

                    for (int col = 0; col < width; col++)
                    {
                        byte rawSrcA = pSrcRow[3];
                        if (rawSrcA == 0)
                        {
                            pSrcRow += 4;
                            pDstRow += 4;
                            continue;
                        }

                        int srcA, srcR, srcG, srcB;

                        if (opacityScale == 255)
                        {
                            srcB = pSrcRow[0];// 满不透明度，直接读取
                            srcG = pSrcRow[1];
                            srcR = pSrcRow[2];
                            srcA = rawSrcA;
                        }
                        else
                        {
                            srcB = (pSrcRow[0] * opacityScale) / 255;
                            srcG = (pSrcRow[1] * opacityScale) / 255;
                            srcR = (pSrcRow[2] * opacityScale) / 255;
                            srcA = (rawSrcA * opacityScale) / 255;
                        }

                        if (srcA == 0)
                        {
                            pSrcRow += 4;
                            pDstRow += 4;
                            continue;
                        }

                        if (srcA == 255)
                        {
                            pDstRow[0] = (byte)srcB;
                            pDstRow[1] = (byte)srcG;
                            pDstRow[2] = (byte)srcR;
                            pDstRow[3] = 255; // 目标 alpha 变成 255
                        }
                        else
                        {
                            int invAlpha = 255 - srcA;// Alpha 混合: Out = Src + Dst * (1 - SrcA)

                            pDstRow[0] = (byte)(srcB + (pDstRow[0] * invAlpha) / 255); // B
                            pDstRow[1] = (byte)(srcG + (pDstRow[1] * invAlpha) / 255); // G
                            pDstRow[2] = (byte)(srcR + (pDstRow[2] * invAlpha) / 255); // R
                            pDstRow[3] = (byte)(srcA + (pDstRow[3] * invAlpha) / 255); // A
                        }

                        pSrcRow += 4;
                        pDstRow += 4;
                    }
                }
            }
        }
    }
}
