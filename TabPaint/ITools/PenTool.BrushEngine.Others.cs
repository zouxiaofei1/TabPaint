using System.Buffers;
using System.Collections.Concurrent;

using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TabPaint;
using static TabPaint.MainWindow;

public partial class PenTool : ToolBase
{
    private unsafe void DrawBlurStrokeUnsafeParallel(
     ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h)
    {
        int size = (int)ctx.PenThickness;
        int r = size / 2;
        if (r < 1) r = 1;

        float opacity = (float)ctx.PenOpacity;
        int kernelRadius = 1 + (int)(opacity * r * 0.2);

        // ★ 关键改动：用3次迭代的小核 box blur 代替1次大核
        // 3次 box blur ≈ 高斯模糊，消除方块纹理
        const int BlurPasses = 2;

        // 为3次迭代分配合适的核半径（总效果等价于原来的kernelRadius）
        // 3次 box blur 等效标准差 σ = sqrt(n * (2r+1)^2 - 1) / 12)
        // 简化处理：每次迭代用 kernelRadius / sqrt(3) 的核
        int passRadius = Math.Max(1, (int)Math.Round(kernelRadius / Math.Sqrt(BlurPasses)));

        int cx = (int)p.X;
        int cy = (int)p.Y;

        // ROI 需要包含所有迭代的扩展区域
        int totalKernelExtent = passRadius * BlurPasses;
        int roiX = Math.Max(0, cx - r - totalKernelExtent);
        int roiY = Math.Max(0, cy - r - totalKernelExtent);
        int roiR = Math.Min(w, cx + r + totalKernelExtent);
        int roiB = Math.Min(h, cy + r + totalKernelExtent);
        int roiW = roiR - roiX;
        int roiH = roiB - roiY;
        if (roiW <= 0 || roiH <= 0) return;

        // 分配临时缓冲区用于多次迭代
        int pixelCount = roiW * roiH;
        byte[] tempBuffer = ArrayPool<byte>.Shared.Rent(pixelCount * 4);

        try
        {
            var snapHandle = System.Runtime.InteropServices.GCHandle.Alloc(
                _blurSourceSnapshot, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                byte* snapBase = (byte*)snapHandle.AddrOfPinnedObject();
                int snapStride = _blurSnapshotStride;

                // 第一次迭代从快照读取，后续从临时缓冲区读取
                for (int pass = 0; pass < BlurPasses; pass++)
                {
                    int satW = roiW + 1;
                    int satH = roiH + 1;
                    int satStride4 = satW * 4;
                    int totalNeeded = satH * satStride4;

                    if (_satBufferInt == null || _satIntCapacity < totalNeeded)
                    {
                        _satIntCapacity = totalNeeded;
                        _satBufferInt = new int[_satIntCapacity];
                    }
                    else Array.Clear(_satBufferInt, 0, totalNeeded);

                    int[] localSatBuffer = _satBufferInt;
                    var parallelOptions = ctx.ParentWindow.CreatePerformanceParallelOptions();

                    // 构建积分图
                    if (pass == 0)
                    {
                        // 首次从快照读取
                        Parallel.For(0, roiH, parallelOptions, (iy) =>
                        {
                            byte* srcRow = snapBase + (long)(roiY + iy) * snapStride + roiX * 4;
                            fixed (int* sat = localSatBuffer)
                            {
                                int* curRow = sat + (iy + 1) * satStride4;
                                int prefB = 0, prefG = 0, prefR = 0, prefA = 0;
                                for (int ix = 0; ix < roiW; ix++)
                                {
                                    byte* px = srcRow + ix * 4;
                                    prefB += px[0]; prefG += px[1];
                                    prefR += px[2]; prefA += px[3];
                                    int idx = (ix + 1) * 4;
                                    curRow[idx] = prefB; curRow[idx + 1] = prefG;
                                    curRow[idx + 2] = prefR; curRow[idx + 3] = prefA;
                                }
                            }
                        });
                    }
                    else
                    {
                        // 后续迭代从临时缓冲区读取
                        fixed (byte* tempPtr = tempBuffer)
                        {
                            byte* temps = tempPtr;
                            Parallel.For(0, roiH, parallelOptions, (iy) =>
                            {
                             
                                byte* srcRow = temps + iy * roiW * 4;
                                fixed (int* sat = localSatBuffer)
                                {
                                    int* curRow = sat + (iy + 1) * satStride4;
                                    int prefB = 0, prefG = 0, prefR = 0, prefA = 0;
                                    for (int ix = 0; ix < roiW; ix++)
                                    {
                                        byte* px = srcRow + ix * 4;
                                        prefB += px[0]; prefG += px[1];
                                        prefR += px[2]; prefA += px[3];
                                        int idx = (ix + 1) * 4;
                                        curRow[idx] = prefB; curRow[idx + 1] = prefG;
                                        curRow[idx + 2] = prefR; curRow[idx + 3] = prefA;
                                    }
                                }
                            });
                        }
                    }

                    // 列前缀和
                    Parallel.For(1, roiW + 1, parallelOptions, (ix) =>
                    {
                        int idx4 = ix * 4;
                        fixed (int* sat = localSatBuffer)
                        {
                            for (int iy = 1; iy < roiH; iy++)
                            {
                                int* curRow = sat + (iy + 1) * satStride4 + idx4;
                                int* prevRow = sat + iy * satStride4 + idx4;
                                curRow[0] += prevRow[0]; curRow[1] += prevRow[1];
                                curRow[2] += prevRow[2]; curRow[3] += prevRow[3];
                            }
                        }
                    });

                    // 应用 box blur 到临时缓冲区（最后一次写入目标）
                    fixed (int* sat = localSatBuffer)
                    fixed (byte* tempPtr = tempBuffer)
                    {
                        // ★ 将 fixed 指针复制到普通局部变量
                        int* satPtr = sat;
                        byte* tmpPtr = tempPtr;
                        int localPassRadius = passRadius;
                        int localRoiW = roiW;
                        int localRoiH = roiH;
                        int localSatStride4 = satStride4;

                        Parallel.For(0, roiH, parallelOptions, (iy) =>
                        {
                            for (int ix = 0; ix < localRoiW; ix++)
                            {
                                int boxX1 = Math.Max(0, ix - localPassRadius);
                                int boxY1 = Math.Max(0, iy - localPassRadius);
                                int boxX2 = Math.Min(localRoiW, ix + localPassRadius + 1);
                                int boxY2 = Math.Min(localRoiH, iy + localPassRadius + 1);
                                int area = (boxX2 - boxX1) * (boxY2 - boxY1);

                                int i22 = boxY2 * localSatStride4 + boxX2 * 4;
                                int i12 = boxY1 * localSatStride4 + boxX2 * 4;
                                int i21 = boxY2 * localSatStride4 + boxX1 * 4;
                                int i11 = boxY1 * localSatStride4 + boxX1 * 4;

                                byte* dst = tmpPtr + (iy * localRoiW + ix) * 4;
                                dst[0] = (byte)((satPtr[i22] - satPtr[i12] - satPtr[i21] + satPtr[i11]) / area);
                                dst[1] = (byte)((satPtr[i22 + 1] - satPtr[i12 + 1] - satPtr[i21 + 1] + satPtr[i11 + 1]) / area);
                                dst[2] = (byte)((satPtr[i22 + 2] - satPtr[i12 + 2] - satPtr[i21 + 2] + satPtr[i11 + 2]) / area);
                                dst[3] = (byte)((satPtr[i22 + 3] - satPtr[i12 + 3] - satPtr[i21 + 3] + satPtr[i11 + 3]) / area);
                            }
                        });
                    }

                }

                // 最后将临时缓冲区中的结果写入目标（仅圆形区域内）
                int paintXMin = Math.Max(0, cx - r);
                int paintYMin = Math.Max(0, cy - r);
                int paintXMax = Math.Min(w, cx + r);
                int paintYMax = Math.Min(h, cy + r);
                int rSq = r * r;
                byte[] maskRef = _currentStrokeMask;

                fixed (byte* tempPtr = tempBuffer)
                {
                    for (int py = paintYMin; py < paintYMax; py++)
                    {
                        int dy = py - cy;
                        int dySq = dy * dy;
                        int maxDx = (int)Math.Sqrt(rSq - dySq);
                        int rowXMin = Math.Max(paintXMin, cx - maxDx);
                        int rowXMax = Math.Min(paintXMax, cx + maxDx + 1);
                        byte* dstRow = basePtr + (long)py * stride;
                        int rowIdx = py * w;

                        for (int px = rowXMin; px < rowXMax; px++)
                        {
                            int pixelIndex = rowIdx + px;
                            if (maskRef[pixelIndex] >= 255) continue;
                            int dx2 = px - cx;
                            if (dx2 * dx2 + dySq > rSq) continue;
                            maskRef[pixelIndex] = 255;

                            int localX = px - roiX;
                            int localY = py - roiY;
                            byte* src = tempPtr + (localY * roiW + localX) * 4;
                            byte* dst = dstRow + px * 4;
                            dst[0] = src[0]; dst[1] = src[1];
                            dst[2] = src[2]; dst[3] = src[3];
                        }
                    }
                }
            }
            finally
            {
                snapHandle.Free();
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(tempBuffer);
        }
    }



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void BlurApplyRow(
        int* sat, int satStride4,
        byte* basePtr, int stride, int imgW,
        byte[] strokeMask,
        int py, int paintXMin, int paintXMax,
        int cx, int cy, int rSq,
        int roiX, int roiY, int roiW, int roiH,
        int kernelRadius)
    {
        int dy = py - cy;
        int dySq = dy * dy;

        int maxDx = (int)Math.Sqrt(rSq - dySq);
        int rowXMin = Math.Max(paintXMin, cx - maxDx);
        int rowXMax = Math.Min(paintXMax, cx + maxDx + 1);

        byte* dstRow = basePtr + (long)py * stride;
        int rowIdx = py * imgW;
        int localY = py - roiY;

        for (int px = rowXMin; px < rowXMax; px++)
        {
            int pixelIndex = rowIdx + px;
            if (strokeMask[pixelIndex] >= 255) continue;

            int dx = px - cx;
            if (dx * dx + dySq > rSq) continue;

            strokeMask[pixelIndex] = 255;

            int localX = px - roiX;
            int boxX1 = Math.Max(0, localX - kernelRadius);
            int boxY1 = Math.Max(0, localY - kernelRadius);
            int boxX2 = Math.Min(roiW, localX + kernelRadius + 1);
            int boxY2 = Math.Min(roiH, localY + kernelRadius + 1);

            int area = (boxX2 - boxX1) * (boxY2 - boxY1);
            int i22 = boxY2 * satStride4 + boxX2 * 4;
            int i12 = boxY1 * satStride4 + boxX2 * 4;
            int i21 = boxY2 * satStride4 + boxX1 * 4;
            int i11 = boxY1 * satStride4 + boxX1 * 4;
            byte* dstPtr = dstRow + px * 4;
            dstPtr[0] = (byte)((sat[i22 + 0] - sat[i12 + 0] - sat[i21 + 0] + sat[i11 + 0]) / area);
            dstPtr[1] = (byte)((sat[i22 + 1] - sat[i12 + 1] - sat[i21 + 1] + sat[i11 + 1]) / area);
            dstPtr[2] = (byte)((sat[i22 + 2] - sat[i12 + 2] - sat[i21 + 2] + sat[i11 + 2]) / area);
            dstPtr[3] = (byte)((sat[i22 + 3] - sat[i12 + 3] - sat[i21 + 3] + sat[i11 + 3]) / area);
        }
    }
    private unsafe void DrawSprayStrokeUnsafe(ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h)
    {
        if (_sprayPatterns == null) InitializeSprayPatterns();
        int radius = (int)(ctx.PenThickness / 2.0);
        if (radius < 1) radius = 1;

        int count = 80;
        var pattern = _sprayPatterns[_patternIndex];
        _patternIndex = (_patternIndex + 1) % _sprayPatterns.Count;
        Color targetColor = ctx.PenColor;
        float globalOpacity = (float)ctx.PenOpacity;
        if (globalOpacity <= 0.005f) return;
        float tB = targetColor.B; float tG = targetColor.G;
        float tR = targetColor.R; float tA = targetColor.A;

        for (int i = 0; i < count && i < pattern.Length; i++)
        {
            int xx = (int)(p.X + pattern[i].X * radius);
            int yy = (int)(p.Y + pattern[i].Y * radius);
            if (xx >= 0 && xx < w && yy >= 0 && yy < h)
            {
                byte* pPx = basePtr + yy * stride + xx * 4;
                pPx[0] = (byte)(pPx[0] + (tB - pPx[0]) * globalOpacity);
                pPx[1] = (byte)(pPx[1] + (tG - pPx[1]) * globalOpacity);
                pPx[2] = (byte)(pPx[2] + (tR - pPx[2]) * globalOpacity);
                pPx[3] = (byte)(pPx[3] + (tA - pPx[3]) * globalOpacity);
            }
        }
    }

    private unsafe void DrawMosaicLineUnsafe(ToolContext ctx, Point p1, Point p2,
     byte* basePtr, int stride, int w, int h)
    {
        int radius = (int)(ctx.PenThickness / 2.0);
        if (radius < 1) radius = 1;
        float radiusSq = radius * radius;
        int blockSize = Math.Max(4, (int)(ctx.PenThickness / 4.0));

        int aabbLeft = Math.Max(0, (int)(Math.Min(p1.X, p2.X) - radius));
        int aabbTop = Math.Max(0, (int)(Math.Min(p1.Y, p2.Y) - radius));
        int aabbRight = Math.Min(w, (int)(Math.Max(p1.X, p2.X) + radius));
        int aabbBottom = Math.Min(h, (int)(Math.Max(p1.Y, p2.Y) + radius));

        if (aabbLeft >= aabbRight || aabbTop >= aabbBottom) return;

        float segDx = (float)(p2.X - p1.X);
        float segDy = (float)(p2.Y - p1.Y);
        float segLenSq = segDx * segDx + segDy * segDy;
        float invSegLenSq = segLenSq > 0 ? 1.0f / segLenSq : 0;

        Parallel.For(aabbTop, aabbBottom, ctx.ParentWindow.CreatePerformanceParallelOptions(), (y) =>
        {
            byte* rowPtr = basePtr + (long)y * stride;
            int rowIdx = y * w;
            float dyVal = (float)(y - p1.Y);
            int by = (y / blockSize) * blockSize;

            for (int x = aabbLeft; x < aabbRight; x++)
            {
                int pixelIndex = rowIdx + x;
                if (_currentStrokeMask[pixelIndex] >= 255) continue;

                float t = 0;
                if (segLenSq > 0)
                {
                    t = Math.Clamp(((x - (float)p1.X) * segDx + dyVal * segDy) * invSegLenSq, 0, 1);
                }
                float closestX = (float)p1.X + t * segDx;
                float closestY = (float)p1.Y + t * segDy;
                float ddx = x - closestX;
                float ddy = y - closestY;

                if (ddx * ddx + ddy * ddy <= radiusSq)
                {
                    int bx = (x / blockSize) * blockSize;
                    int sampleX = Math.Clamp(bx, 0, w - 1);
                    int sampleY = Math.Clamp(by, 0, h - 1);
                    byte* srcPixel = basePtr + (long)sampleY * stride + sampleX * 4;
                    byte mB = srcPixel[0], mG = srcPixel[1], mR = srcPixel[2];

                    _currentStrokeMask[pixelIndex] = 255;
                    byte* destPixel = rowPtr + x * 4;
                    destPixel[0] = mB; destPixel[1] = mG; destPixel[2] = mR;
                }
            }
        });
    }
    private unsafe void DrawWatercolorStrokeUnsafe(ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h)
    {
        float radius = (float)ctx.PenThickness * 0.5f;
        if (radius < 0.5f) radius = 0.5f;
        Color targetColor = ctx.PenColor;
        float globalOpacity = (float)ctx.PenOpacity;
        if (globalOpacity <= 0.005f) return;
        float baseOpacity = globalOpacity * 0.5f;
        float tB = targetColor.B, tG = targetColor.G, tR = targetColor.R, tA = targetColor.A;

        float irregularRadius = radius * (0.9f + (float)_rnd.Value.NextDouble() * 0.2f);
        float irregularRadiusSq = irregularRadius * irregularRadius;

        int x_start = Math.Max(0, (int)(p.X - irregularRadius - 1));
        int x_end = Math.Min(w, (int)(p.X + irregularRadius + 1));
        int y_start = Math.Max(0, (int)(p.Y - irregularRadius - 1));
        int y_end = Math.Min(h, (int)(p.Y + irregularRadius + 1));

        float pX = (float)p.X, pY = (float)p.Y;
        float invIrregularRadiusSq = 1.0f / irregularRadiusSq;

        // 对水彩画笔应用并行化
        Parallel.For(y_start, y_end, ctx.ParentWindow.CreatePerformanceParallelOptions(), (y) =>
        {
            byte* rowPtr = basePtr + (long)y * stride;
            float dyVal = y - pY;
            float dySq = dyVal * dyVal;

            for (int x = x_start; x < x_end; x++)
            {
                float dxVal = x - pX;
                float distSq = dxVal * dxVal + dySq;
                if (distSq >= irregularRadiusSq) continue;

                float normalizedDistSq = distSq * invIrregularRadiusSq;
                float falloff = 1.0f - normalizedDistSq;
                float localOpacity = baseOpacity * falloff * falloff;

                if (localOpacity > 0.001f)
                {
                    byte* pPx = rowPtr + (long)x * 4;
                    pPx[0] = (byte)(pPx[0] + (tB - pPx[0]) * localOpacity);
                    pPx[1] = (byte)(pPx[1] + (tG - pPx[1]) * localOpacity);
                    pPx[2] = (byte)(pPx[2] + (tR - pPx[2]) * localOpacity);
                    pPx[3] = (byte)(pPx[3] + (tA - pPx[3]) * localOpacity);
                }
            }
        });
    }
    private unsafe void DrawOilPaintStrokeUnsafe(ToolContext ctx, Point p, byte* basePtr, int stride, int w, int h)
    {
        double globalOpacity = ctx.PenOpacity;
        float baseAlpha = (float)(0.2f * 255.0f / MathF.Max(1.0f, MathF.Sqrt((float)ctx.PenThickness))) * (float)globalOpacity;
        int alpha = (int)baseAlpha;
        if (alpha == 0) return;

        int radius = (int)(ctx.PenThickness * 0.5);
        if (radius < 1) radius = 1;
        Color baseColor = ctx.PenColor;
        int x_center = (int)p.X; int y_center = (int)p.Y;
        int numClumps = radius / 2 + 5;

        // 对油画笔应用并行化处理每一个 Clump (团块)
        Parallel.For(0, numClumps, ctx.ParentWindow.CreatePerformanceParallelOptions(), (i) =>
        {
            var r = _rnd.Value;
            int brightnessOffset = r.Next(-AppConsts.OilPaintBrightnessVariation, AppConsts.OilPaintBrightnessVariation + 1);
            byte clumpR = ClampColor(baseColor.R + brightnessOffset);
            byte clumpG = ClampColor(baseColor.G + brightnessOffset);
            byte clumpB = ClampColor(baseColor.B + brightnessOffset);

            double angle = r.NextDouble() * 2 * Math.PI;
            double distFromCenter = MathF.Sqrt((float)r.NextDouble()) * radius;
            int clumpCenterX = x_center + (int)(distFromCenter * Math.Cos(angle));
            int clumpCenterY = y_center + (int)(distFromCenter * Math.Sin(angle));
            int clumpRadius = r.Next(1, radius / 4 + 3);
            int clumpRadiusSq = clumpRadius * clumpRadius;

            int startX = Math.Max(0, clumpCenterX - clumpRadius);
            int endX = Math.Min(w, clumpCenterX + clumpRadius);
            int startY = Math.Max(0, clumpCenterY - clumpRadius);
            int endY = Math.Min(h, clumpCenterY + clumpRadius);

            int invAlpha = 255 - alpha;

            for (int y = startY; y < endY; y++)
            {
                byte* rowPtr = basePtr + (long)y * stride;
                int dySq = (y - clumpCenterY) * (y - clumpCenterY);
                for (int x = startX; x < endX; x++)
                {
                    int dx = x - clumpCenterX;
                    if (dx * dx + dySq < clumpRadiusSq)
                    {
                        byte* pixelPtr = rowPtr + (long)x * 4;
                        // 快速混合 (Div255 优化)
                        pixelPtr[0] = Div255(clumpB * alpha + pixelPtr[0] * invAlpha);
                        pixelPtr[1] = Div255(clumpG * alpha + pixelPtr[1] * invAlpha);
                        pixelPtr[2] = Div255(clumpR * alpha + pixelPtr[2] * invAlpha);
                    }
                }
            }
        });
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe Int32Rect? DrawCatmullRomSegment(
        ToolContext ctx,
        StrokePoint p0, StrokePoint p1,
        StrokePoint p2, StrokePoint p3,
        byte* buffer, int stride, int w, int h)
    {
        float segDx = p2.X - p1.X;
        float segDy = p2.Y - p1.Y;
        float segLen = MathF.Sqrt(segDx * segDx + segDy * segDy);
        
        float thickness = (float)ctx.PenThickness;
        // 动态步长策略：
        // 1. 小笔刷 (<32px): 保持高密度 (1.5px 步长)，确保曲线圆滑且无多边形感。
        // 2. 大笔刷 (>100px): 采样步长随半径增大，最大可达半径的 40%。
        // 因为大半径笔刷在极短距离内的形状变化肉眼不可见，减少采样可极大提升性能。
        float stepSize;
        if (thickness < 32) stepSize = 1.5f;
        else if (thickness < 128) stepSize = thickness * 0.15f;
        else stepSize = thickness * 0.35f; // 1200px 时步长约为 420px

        int steps = Math.Max(1, (int)MathF.Ceiling(segLen / stepSize));
        if (steps > 150) steps = 150;

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        bool hit = false;

        float prevX = p1.X, prevY = p1.Y, prevP = p1.Pressure;

        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            float t2 = t * t;
            float t3 = t2 * t;

            // Catmull-Rom 系数
            float c0 = -0.5f * t3 + t2 - 0.5f * t;
            float c1 = 1.5f * t3 - 2.5f * t2 + 1.0f;
            float c2 = -1.5f * t3 + 2.0f * t2 + 0.5f * t;
            float c3 = 0.5f * t3 - 0.5f * t2;

            float curX = c0 * p0.X + c1 * p1.X + c2 * p2.X + c3 * p3.X;
            float curY = c0 * p0.Y + c1 * p1.Y + c2 * p2.Y + c3 * p3.Y;
            float curP = c0 * p0.Pressure + c1 * p1.Pressure + c2 * p2.Pressure + c3 * p3.Pressure;
            curP = Math.Clamp(curP, 0.1f, 1.0f);

            Int32Rect? rect;
            if (ctx.PenStyle == BrushStyle.Square || ctx.PenStyle == BrushStyle.Eraser)
            {
                DrawSquareStrokeLineUnsafe(ctx, new Point(prevX, prevY), prevP, new Point(curX, curY), curP, buffer, stride, w, h);
                rect = LineBounds(new Point(prevX, prevY), new Point(curX, curY), (int)ctx.PenThickness + 2);
            }
            else
            {
                rect = DrawRoundStrokeUnsafe_Internal(
                    ctx,
                    new Point(prevX, prevY), prevP,
                    new Point(curX, curY), curP,
                    buffer, stride, w, h);
            }

            if (rect.HasValue)
            {
                hit = true;
                if (rect.Value.X < minX) minX = rect.Value.X;
                if (rect.Value.Y < minY) minY = rect.Value.Y;
                int rx = rect.Value.X + rect.Value.Width;
                int ry = rect.Value.Y + rect.Value.Height;
                if (rx > maxX) maxX = rx;
                if (ry > maxY) maxY = ry;
            }

            prevX = curX; prevY = curY; prevP = curP;
        }

        if (!hit) return null;
        return new Int32Rect(minX, minY, maxX - minX, maxY - minY);
    }
    private unsafe void DrawHighlighterLineUnsafe(ToolContext ctx, Point p1, Point p2, byte* basePtr, int stride, int w, int h)
    {
        int r = (int)(ctx.PenThickness / 2.0);
        if (r < 1) r = 1;
        float globalOpacity = (float)ctx.PenOpacity;
        byte baseAlpha = 30;
        Color c = Color.FromArgb((byte)(baseAlpha * globalOpacity), 255, 255, 0);
        int xmin = Math.Max(0, (int)Math.Min(p1.X, p2.X) - r);
        int ymin = Math.Max(0, (int)Math.Min(p1.Y, p2.Y) - r);
        int xmax = Math.Min(w - 1, (int)Math.Max(p1.X, p2.X) + r);
        int ymax = Math.Min(h - 1, (int)Math.Max(p1.Y, p2.Y) + r);

        float dx = (float)(p2.X - p1.X), dy = (float)(p2.Y - p1.Y);
        float lenSq = dx * dx + dy * dy;
        float invLenSq = lenSq > 0 ? 1.0f / lenSq : 0;
        int invSA = 255 - c.A, cA = c.A, cB = c.B, cG = c.G, cR = c.R;
        float rSq = r * r;

        Parallel.For(ymin, ymax + 1, ctx.ParentWindow.CreatePerformanceParallelOptions(), (y) =>
        {
            int rowStartIndex = y * w;
            byte* rowPtr = basePtr + (long)y * stride;
            float dyVal = (float)(y - p1.Y);

            for (int x = xmin; x <= xmax; x++)
            {
                int pixelIndex = rowStartIndex + x;
                if (_currentStrokeMask[pixelIndex] >= 255) continue;

                float t = 0;
                if (lenSq > 0)
                {
                    t = Math.Clamp(((float)(x - p1.X) * dx + dyVal * dy) * invLenSq, 0, 1);
                }
                float closeX = (float)p1.X + t * dx, closeY = (float)p1.Y + t * dy;
                float dX = x - closeX, dY = y - closeY;
                if (dX * dX + dY * dY <= rSq)
                {
                    _currentStrokeMask[pixelIndex] = 255;
                    byte* p = rowPtr + (long)x * 4;
                    p[0] = Div255(cB * cA + p[0] * invSA);
                    p[1] = Div255(cG * cA + p[1] * invSA);
                    p[2] = Div255(cR * cA + p[2] * invSA);
                }
            }
        });
    }
    private unsafe Int32Rect? DrawCalligraphySegmentSubdivided(
        ToolContext ctx, Point from, float fromP, Point to, float toP,
        byte* buffer, int stride, int w, int h)
    {
        float dx = (float)(to.X - from.X);
        float dy = (float)(to.Y - from.Y);
        float length = MathF.Sqrt(dx * dx + dy * dy);

        if (length < 0.5f) return DrawBrushLineUnsafe(ctx, from, fromP, to, toP, buffer, stride, w, h);
        float baseThickness = (float)ctx.PenThickness;
        float maxRadiusChange = baseThickness * 0.15f; // 每子段最大半径变化
        float radiusDiff = MathF.Abs(toP - fromP) * baseThickness * 0.5f;
        int pressureSteps = radiusDiff > 0.1f ? (int)MathF.Ceiling(radiusDiff / maxRadiusChange) : 1;

        int distanceSteps = (int)MathF.Ceiling(length / 2.0f);

        int steps = Math.Max(1, Math.Max(pressureSteps, distanceSteps));
        steps = Math.Min(steps, 500);

        float stepX = dx / steps;
        float stepY = dy / steps;
        float stepP = (toP - fromP) / steps;

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        bool hit = false;

        float curX = (float)from.X;
        float curY = (float)from.Y;
        float curP = fromP;

        for (int i = 0; i < steps; i++)
        {
            float nextX = curX + stepX;
            float nextY = curY + stepY;
            float nextP = curP + stepP;

            var rect = DrawRoundStrokeUnsafe_Internal(
                ctx,
                new Point(curX, curY), curP,
                new Point(nextX, nextY), nextP,
                buffer, stride, w, h);

            if (rect.HasValue)
            {
                hit = true;
                if (rect.Value.X < minX) minX = rect.Value.X;
                if (rect.Value.Y < minY) minY = rect.Value.Y;
                int rx = rect.Value.X + rect.Value.Width;
                int ry = rect.Value.Y + rect.Value.Height;
                if (rx > maxX) maxX = rx;
                if (ry > maxY) maxY = ry;
            }

            curX = nextX;
            curY = nextY;
            curP = nextP;
        }

        if (!hit) return null;
        return new Int32Rect(minX, minY, maxX - minX, maxY - minY);
    }
}
