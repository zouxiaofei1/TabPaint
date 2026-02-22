using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TabPaint;
using static TabPaint.MainWindow;

public partial class PenTool : ToolBase
{
    private void DrawMaskLine(ToolContext ctx, Point p1, Point p2, float pressure)
    {
        if (_maskBitmap == null) return;

        // 1. 准备参数 (使用 float 加速计算)
        float r = (float)ctx.PenThickness * 0.5f;
        float rSq = r * r;
        int xmin = (int)(MathF.Min((float)p1.X, (float)p2.X) - r - 1);
        int ymin = (int)(MathF.Min((float)p1.Y, (float)p2.Y) - r - 1);
        int xmax = (int)(MathF.Max((float)p1.X, (float)p2.X) + r + 1);
        int ymax = (int)(MathF.Max((float)p1.Y, (float)p2.Y) + r + 1);

        _maskBitmap.Lock();
        unsafe
        {
            int w = _maskBitmap.PixelWidth;
            int h = _maskBitmap.PixelHeight;
            int stride = _maskBitmap.BackBufferStride;
            byte* basePtr = (byte*)_maskBitmap.BackBuffer;

            // 3. 边界安全裁剪
            xmin = Math.Max(0, xmin);
            ymin = Math.Max(0, ymin);
            xmax = Math.Min(w - 1, xmax);
            ymax = Math.Min(h - 1, ymax);

            float dx = (float)(p2.X - p1.X);
            float dy = (float)(p2.Y - p1.Y);
            float lenSq = dx * dx + dy * dy;
            float invLenSq = lenSq > 1e-6f ? 1.0f / lenSq : 0;

            // 使用 Parallel.For 并行化
            Parallel.For(ymin, ymax + 1, (y) =>
            {
                byte* rowPtr = basePtr + (long)y * stride;
                float pyDy = (float)(y - p1.Y);
                
                for (int x = xmin; x <= xmax; x++)
                {
                    float pxDx = (float)(x - p1.X);
                    float t = lenSq > 1e-6f ? Math.Clamp((pxDx * dx + pyDy * dy) * invLenSq, 0, 1) : 0;

                    float closestX = (float)p1.X + t * dx;
                    float closestY = (float)p1.Y + t * dy;

                    float dX = x - closestX;
                    float dY = y - closestY;
                    float distSq = dX * dX + dY * dY;

                    if (distSq <= rSq)
                    {
                        byte* p = rowPtr + (long)x * 4;
                        // 直接写入 BGRA (0, 0, 255, 255)
                        *((uint*)p) = 0xFFFF0000; 
                    }
                }
            });
        }
        if (xmax >= xmin && ymax >= ymin)
        {
            _maskBitmap.AddDirtyRect(new Int32Rect(xmin, ymin, xmax - xmin + 1, ymax - ymin + 1));
        }
        _maskBitmap.Unlock();
    }

    private async void ApplyAiEraser(ToolContext ctx)
    {
        var mw = MainWindow.GetCurrentInstance();
        var aiService = AiService.Instance;

        try
        {
            var maskBounds = GetMaskBounds();
            if (maskBounds.IsEmpty) return;

            string modelPath = System.IO.Path.Combine(mw._cacheDir, AppConsts.Inpaint_ModelName);
            string oldStatus = mw.ImageSize;

            // 开始推理阶段
            mw.ImageSize = LocalizationManager.GetString("L_AI_Eraser_Processing");
            mw.ShowToast("L_AI_Eraser_Processing");
            mw.IsEnabled = false; // 锁定 UI

            var oldBmp = ctx.Surface.Bitmap;
            int origW = oldBmp.PixelWidth;
            int origH = oldBmp.PixelHeight;

            // --- 智能裁剪逻辑：计算包含足够上下文的正方形区域 ---
            double expansionFactor = 2.5; // 扩展因子，越大上下文越多
            int centerX = maskBounds.X + maskBounds.Width / 2;
            int centerY = maskBounds.Y + maskBounds.Height / 2;
            int side = (int)(Math.Max(maskBounds.Width, maskBounds.Height) * expansionFactor);
            
            // 确保切片尺寸不要太小，且至少是正方形
            side = Math.Max(side, AppConsts.AiInpaintSize / 2);

            int cropX = centerX - side / 2;
            int cropY = centerY - side / 2;
            int cropW = side;
            int cropH = side;

            // 边界检查与修正
            if (cropX < 0) cropX = 0;
            if (cropY < 0) cropY = 0;
            if (cropX + cropW > origW) cropW = origW - cropX;
            if (cropY + cropH > origH) cropH = origH - cropY;

            // 再次确保尽可能正方形（向另一侧扩展）
            int finalSide = Math.Min(cropW, cropH);
            Int32Rect cropRect = new Int32Rect(cropX, cropY, cropW, cropH);

            // --- 提取局部切片 ---
            var croppedImg = new CroppedBitmap(oldBmp, cropRect);
            var croppedMask = new CroppedBitmap(_maskBitmap, cropRect);

            int targetW = AppConsts.AiInpaintSize;
            int targetH = AppConsts.AiInpaintSize;

            // 缩放到 512x512
            var scaledImg = new TransformedBitmap(croppedImg, new ScaleTransform((double)targetW / cropW, (double)targetH / cropH));
            var wbImg = new WriteableBitmap(scaledImg);
            byte[] imgBytes = new byte[targetH * wbImg.BackBufferStride];
            wbImg.CopyPixels(imgBytes, wbImg.BackBufferStride, 0);

            var scaledMask = new TransformedBitmap(croppedMask, new ScaleTransform((double)targetW / cropW, (double)targetH / cropH));
            var wbMask = new WriteableBitmap(scaledMask);
            byte[] maskBytes = new byte[targetH * wbMask.BackBufferStride];
            wbMask.CopyPixels(maskBytes, wbMask.BackBufferStride, 0);

            // 执行推理
            byte[] rawResultPixels = await aiService.RunInpaintingAsync(modelPath, imgBytes, maskBytes, targetW, targetH);

            // --- 将结果贴回原图 ---
            var result512 = new WriteableBitmap(targetW, targetH, AppConsts.StandardDpi, AppConsts.StandardDpi, PixelFormats.Bgra32, null);
            result512.WritePixels(new Int32Rect(0, 0, targetW, targetH), rawResultPixels, targetW * 4, 0);

            // 缩放回裁剪时的原始尺寸
            var finalScaled = new TransformedBitmap(result512, new ScaleTransform((double)cropW / targetW, (double)cropH / targetH));
            var wbResultPart = new WriteableBitmap(finalScaled);

            // 创建最终位图：克隆原图，然后只在遮罩区域进行像素混合
            var finalWb = new WriteableBitmap(oldBmp);
            
            // 锁定两个位图进行快速像素处理
            finalWb.Lock();
            wbResultPart.Lock();
            _maskBitmap.Lock();
            unsafe
            {
                byte* pDestBase = (byte*)finalWb.BackBuffer;
                byte* pSrcBase = (byte*)wbResultPart.BackBuffer;
                byte* pMaskBase = (byte*)_maskBitmap.BackBuffer;
                int destStride = finalWb.BackBufferStride;
                int srcStride = wbResultPart.BackBufferStride;
                int maskStride = _maskBitmap.BackBufferStride;

                for (int y = 0; y < cropH; y++)
                {
                    int globalY = cropY + y;
                    byte* pDestRow = pDestBase + (globalY * destStride);
                    byte* pSrcRow = pSrcBase + (y * srcStride);
                    byte* pMaskRow = pMaskBase + (globalY * maskStride);

                    for (int x = 0; x < cropW; x++)
                    {
                        int globalX = cropX + x;
                        byte* pMask = pMaskRow + (globalX * 4);
                        float maskAlpha = pMask[3] / 255.0f; // 获取遮罩透明度

                        if (maskAlpha > 0)
                        {
                            byte* pDest = pDestRow + (globalX * 4);
                            byte* pSrc = pSrcRow + (x * 4);

                            // 使用线性插值混合：Result = (1-alpha)*Original + alpha*AI
                            // 这样可以实现边缘平滑过渡，且非遮罩区绝对不变
                            pDest[0] = (byte)(pDest[0] * (1 - maskAlpha) + pSrc[0] * maskAlpha); // B
                            pDest[1] = (byte)(pDest[1] * (1 - maskAlpha) + pSrc[1] * maskAlpha); // G
                            pDest[2] = (byte)(pDest[2] * (1 - maskAlpha) + pSrc[2] * maskAlpha); // R
                            pDest[3] = 255; 
                        }
                    }
                }
            }
            _maskBitmap.Unlock();
            wbResultPart.Unlock();
            finalWb.AddDirtyRect(new Int32Rect(cropX, cropY, cropW, cropH));
            finalWb.Unlock();

            // 撤销重做逻辑
            var undoRect = new Int32Rect(0, 0, origW, origH);
            byte[] undoPixels = new byte[origH * oldBmp.BackBufferStride];
            oldBmp.CopyPixels(undoPixels, oldBmp.BackBufferStride, 0);
            
            ctx.Surface.ReplaceBitmap(finalWb);

            byte[] redoPixels = new byte[origH * finalWb.BackBufferStride];
            finalWb.CopyPixels(redoPixels, finalWb.BackBufferStride, 0);
            ctx.Undo.PushTransformAction(undoRect, undoPixels, undoRect, redoPixels);

            mw.NotifyCanvasChanged();
            mw.ShowToast("L_AI_Eraser_Success");
            mw.ImageSize = oldStatus;
        }
        catch (Exception ex)
        {
            mw.ShowToast(string.Format(LocalizationManager.GetString("L_AI_Eraser_Error_Prefix"), ex.Message), ex);
        }
        finally
        {
            mw.IsEnabled = true;
            CleanupMask(ctx);
            mw.Focus();
        }
    }


    private Int32Rect GetMaskBounds()
    {
        if (_maskBitmap == null) return Int32Rect.Empty;

        int minX = _maskBitmap.PixelWidth;
        int minY = _maskBitmap.PixelHeight;
        int maxX = -1;
        int maxY = -1;

        _maskBitmap.Lock();
        unsafe
        {
            int w = _maskBitmap.PixelWidth;
            int h = _maskBitmap.PixelHeight;
            int stride = _maskBitmap.BackBufferStride;
            byte* basePtr = (byte*)_maskBitmap.BackBuffer;

            for (int y = 0; y < h; y++)
            {
                uint* rowPtr = (uint*)(basePtr + y * stride);
                for (int x = 0; x < w; x++)
                {
                    if (rowPtr[x] != 0) // 只要不是全透明，就认为是遮罩区域
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
        }
        _maskBitmap.Unlock();

        if (maxX == -1) return Int32Rect.Empty;
        return new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private void CleanupMask(ToolContext ctx)  // 清理遮罩层
    {
        if (_maskBitmap != null)
        {
            _maskBitmap.Clear(); // 扩展方法: 清空像素
            _maskBitmap = null;
        }
        if (_maskImageOverlay != null)
        {
            ctx.EditorOverlay.Children.Remove(_maskImageOverlay);
            _maskImageOverlay = null;
        }
    }

}
