using System.ComponentModel;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;


//
//TabPaint主程序
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void ProcessMosaic(byte[] pixels, int width, int height, int stride, int blockSize)
        {
            var parallelOptions = CreatePerformanceParallelOptions();
            unsafe
            {
                fixed (byte* ptr = pixels)
                {
                    IntPtr basePtrInt = (IntPtr)ptr;
                    Parallel.For(0, (height + blockSize - 1) / blockSize, parallelOptions, by =>
                    {
                        byte* basePtr = (byte*)basePtrInt;
                        int yStart = by * blockSize;
                        int yEnd = Math.Min(yStart + blockSize, height);

                        for (int bx = 0; bx < (width + blockSize - 1) / blockSize; bx++)
                        {
                            int xStart = bx * blockSize;
                            int xEnd = Math.Min(xStart + blockSize, width);

                            long sumB = 0, sumG = 0, sumR = 0;
                            int count = 0;

                            for (int y = yStart; y < yEnd; y++)
                            {
                                byte* row = basePtr + y * stride;
                                for (int x = xStart; x < xEnd; x++)
                                {
                                    int offset = x * 4;
                                    sumB += row[offset];
                                    sumG += row[offset + 1];
                                    sumR += row[offset + 2];
                                    count++;
                                }
                            }
                            if (count == 0) continue;
                            byte avgB = (byte)(sumB / count);
                            byte avgG = (byte)(sumG / count);
                            byte avgR = (byte)(sumR / count);

                            for (int y = yStart; y < yEnd; y++)
                            {
                                byte* row = basePtr + y * stride;
                                for (int x = xStart; x < xEnd; x++)
                                {
                                    int offset = x * 4;
                                    row[offset] = avgB;
                                    row[offset + 1] = avgG;
                                    row[offset + 2] = avgR;
                                }
                            }
                        }
                    });
                }
            }
        }

        private void ProcessGaussianBlur(byte[] pixels, int width, int height, int stride, int radius)
        {
            var parallelOptions = CreatePerformanceParallelOptions();
            int kernelSize = radius * 2 + 1;
            float[] kernel = new float[kernelSize];
            float sigma = Math.Max(radius / 2.0f, 1.0f);
            float twoSigmaSquare = 2.0f * sigma * sigma;
            float sum = 0.0f;
            for (int i = 0; i < kernelSize; i++)
            {
                float x = i - radius;
                kernel[i] = (float)Math.Exp(-(x * x) / twoSigmaSquare);
                sum += kernel[i];
            }
            for (int i = 0; i < kernelSize; i++) kernel[i] /= sum;

            byte[] tempPixels = new byte[pixels.Length];
            unsafe
            {
                fixed (byte* pixelsPtr = pixels)
                fixed (byte* tempPtr = tempPixels)
                fixed (float* kPtr = kernel)
                {
                    IntPtr srcInt = (IntPtr)pixelsPtr;
                    IntPtr destInt = (IntPtr)tempPtr;
                    IntPtr kpInt = (IntPtr)kPtr;

                    // Horizontal Pass
                    Parallel.For(0, height, parallelOptions, y =>
                    {
                        byte* src = (byte*)srcInt;
                        byte* dest = (byte*)destInt;
                        float* kp = (float*)kpInt;

                        byte* sRow = src + y * stride;
                        byte* dRow = dest + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            float b = 0, g = 0, r = 0;
                            for (int k = -radius; k <= radius; k++)
                            {
                                int px = Math.Clamp(x + k, 0, width - 1);
                                byte* p = sRow + px * 4;
                                float weight = kp[k + radius];
                                b += p[0] * weight;
                                g += p[1] * weight;
                                r += p[2] * weight;
                            }
                            byte* d = dRow + x * 4;
                            d[0] = (byte)b;
                            d[1] = (byte)g;
                            d[2] = (byte)r;
                            d[3] = sRow[x * 4 + 3];
                        }
                    });

                    // Vertical Pass
                    Parallel.For(0, width, parallelOptions, x =>
                    {
                        byte* src = (byte*)srcInt;
                        byte* dest = (byte*)destInt;
                        float* kp = (float*)kpInt;

                        for (int y = 0; y < height; y++)
                        {
                            float b = 0, g = 0, r = 0;
                            for (int k = -radius; k <= radius; k++)
                            {
                                int py = Math.Clamp(y + k, 0, height - 1);
                                byte* p = dest + py * stride + x * 4;
                                float weight = kp[k + radius];
                                b += p[0] * weight;
                                g += p[1] * weight;
                                r += p[2] * weight;
                            }
                            byte* d = src + y * stride + x * 4;
                            d[0] = (byte)b;
                            d[1] = (byte)g;
                            d[2] = (byte)r;
                            // Alpha remains from src (already in pixels)
                        }
                    });
                }
            }
        }

        private void ProcessBrown(byte[] pixels, int width, int height, int stride)
        {
            var parallelOptions = CreatePerformanceParallelOptions();
            float wr = (float)AppConsts.GrayWeightR;
            float wg = (float)AppConsts.GrayWeightG;
            float wb = (float)AppConsts.GrayWeightB;
            float ar = (float)AppConsts.FilterBrownRAddition;
            float ag = (float)AppConsts.FilterBrownGAddition;
            float ab = (float)AppConsts.FilterBrownBAddition;

            unsafe
            {
                fixed (byte* ptr = pixels)
                {
                    IntPtr basePtrInt = (IntPtr)ptr;
                    Parallel.For(0, height, parallelOptions, y =>
                    {
                        byte* basePtr = (byte*)basePtrInt;
                        byte* row = basePtr + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            byte* p = row + x * 4;
                            float b = p[0];
                            float g = p[1];
                            float r = p[2];
                            float gray = r * wr + g * wg + b * wb;
                            p[2] = (byte)Math.Clamp(gray + ar, 0, 255);
                            p[1] = (byte)Math.Clamp(gray + ag, 0, 255);
                            p[0] = (byte)Math.Clamp(gray + ab, 0, 255);
                        }
                    });
                }
            }
        }

        private void ProcessSharpen(byte[] pixels, int width, int height, int stride)
        {
            var parallelOptions = CreatePerformanceParallelOptions();
            byte[] srcPixels = (byte[])pixels.Clone();

            unsafe
            {
                fixed (byte* destPtr = pixels)
                fixed (byte* srcPtr = srcPixels)
                {
                    IntPtr dpInt = (IntPtr)destPtr;
                    IntPtr spInt = (IntPtr)srcPtr;

                    Parallel.For(1, height - 1, parallelOptions, y => // 跳过边缘行
                    {
                        byte* dp = (byte*)dpInt;
                        byte* sp = (byte*)spInt;
                        byte* pDestRow = dp + y * stride;
                        for (int x = 1; x < width - 1; x++) // 跳过边缘列
                        {
                            int offset = y * stride + x * 4;
                            int sumB = 0, sumG = 0, sumR = 0;
                            for (int ky = -1; ky <= 1; ky++)
                            {
                                byte* row = sp + (y + ky) * stride;
                                for (int kx = -1; kx <= 1; kx++)
                                {
                                    byte* p = row + (x + kx) * 4;
                                    int kernelVal = 0; 
                                    if((ky == 0 && kx == 0)) kernelVal= 5;
                                    else if(ky == 0 || kx == 0) kernelVal= -1; // 4-neighbor laplacian sharpen

                                    sumB += p[0] * kernelVal;
                                    sumG += p[1] * kernelVal;
                                    sumR += p[2] * kernelVal;
                                }
                            }

                            // 写入结果并截断到 0-255
                            pDestRow[x * 4] = (byte)Math.Clamp(sumB, 0, 255);
                            pDestRow[x * 4 + 1] = (byte)Math.Clamp(sumG, 0, 255);
                            pDestRow[x * 4 + 2] = (byte)Math.Clamp(sumR, 0, 255);
                            // Alpha 保持原样
                            pDestRow[x * 4 + 3] = sp[offset + 3];
                        }
                    });
                }
            }
        }

        private void ProcessSepia(byte[] pixels, int width, int height, int stride)
        {
            var parallelOptions = CreatePerformanceParallelOptions();
            float r1 = (float)AppConsts.SepiaR1, r2 = (float)AppConsts.SepiaR2, r3 = (float)AppConsts.SepiaR3;
            float g1 = (float)AppConsts.SepiaG1, g2 = (float)AppConsts.SepiaG2, g3 = (float)AppConsts.SepiaG3;
            float b1 = (float)AppConsts.SepiaB1, b2 = (float)AppConsts.SepiaB2, b3 = (float)AppConsts.SepiaB3;

            unsafe
            {
                fixed (byte* ptr = pixels)
                {
                    IntPtr basePtrInt = (IntPtr)ptr;
                    Parallel.For(0, height, parallelOptions, y =>
                    {
                        byte* basePtr = (byte*)basePtrInt;
                        byte* row = basePtr + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            byte* p = row + x * 4;
                            int b = p[0];
                            int g = p[1];
                            int r = p[2];

                            int tr = (int)(r1 * r + r2 * g + r3 * b);
                            int tg = (int)(g1 * r + g2 * g + g3 * b);
                            int tb = (int)(b1 * r + b2 * g + b3 * b);

                            p[2] = (byte)Math.Min(255, tr);
                            p[1] = (byte)Math.Min(255, tg);
                            p[0] = (byte)Math.Min(255, tb);
                        }
                    });
                }
            }
        }
        private void ProcessAutoLevels()
        {
            if (_surface?.Bitmap == null) return;
            _router.CleanUpSelectionandShape();
            _undo.PushFullImageUndo();

            var bmp = _surface.Bitmap;
            bmp.Lock();
            try
            {
                unsafe
                {
                    byte* basePtr = (byte*)bmp.BackBuffer;
                    int stride = bmp.BackBufferStride;
                    int height = bmp.PixelHeight;
                    int width = bmp.PixelWidth;
                    long totalPixels = width * height;

                    int[] histR = new int[256];
                    int[] histG = new int[256];
                    int[] histB = new int[256];
                    for (int y = 0; y < height; y++)
                    {
                        byte* row = basePtr + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            histB[row[x * 4]]++;
                            histG[row[x * 4 + 1]]++;
                            histR[row[x * 4 + 2]]++;
                        }
                    }
                    float clipPercent = 0.005f;
                    int threshold = (int)(totalPixels * clipPercent);

                    void GetMinMax(int[] hist, out byte min, out byte max)
                    {
                        min = 0; max = 255;
                        int count = 0;
                        for (int i = 0; i < 256; i++)
                        {
                            count += hist[i];
                            if (count > threshold) { min = (byte)i; break; }
                        }
                        count = 0;
                        for (int i = 255; i >= 0; i--)
                        {
                            count += hist[i];
                            if (count > threshold) { max = (byte)i; break; }
                        }
                    }

                    GetMinMax(histB, out byte minB, out byte maxB);
                    GetMinMax(histG, out byte minG, out byte maxG);
                    GetMinMax(histR, out byte minR, out byte maxR);

                    byte[] lutR = BuildLevelLut(minR, maxR);
                    byte[] lutG = BuildLevelLut(minG, maxG);
                    byte[] lutB = BuildLevelLut(minB, maxB);

                    var parallelOptions = CreatePerformanceParallelOptions();
                    Parallel.For(0, height, parallelOptions, y =>
                    {
                        byte* row = basePtr + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            row[x * 4] = lutB[row[x * 4]];
                            row[x * 4 + 1] = lutG[row[x * 4 + 1]];
                            row[x * 4 + 2] = lutR[row[x * 4 + 2]];
                        }
                    });
                }
                bmp.AddDirtyRect(new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight));
            }
            finally
            {
                bmp.Unlock();
            }

            NotifyCanvasChanged();
            SetUndoRedoButtonState();
            ShowToast("L_Toast_Effect_AutoLevels");
        }

        private BitmapSource ResizeBitmapCanvas(BitmapSource source, int targetW, int targetH)
        {
            var rtb = new RenderTargetBitmap(targetW, targetH, 96, 96, PixelFormats.Pbgra32);
            var dv = new DrawingVisual();
            using (var ctx = dv.RenderOpen())
            {
                double x = (targetW - source.PixelWidth) / 2.0;
                double y = (targetH - source.PixelHeight) / 2.0;
                ctx.DrawImage(source, new Rect(x, y, source.PixelWidth, source.PixelHeight));
            }
            rtb.Render(dv);
            return rtb;
        }
        private byte[] BuildLevelLut(byte min, byte max)
        {
            byte[] lut = new byte[256];
            if (max <= min)
            {
                for (int i = 0; i < 256; i++) lut[i] = (byte)i;
                return lut;
            }

            float scale = 255.0f / (max - min);
            for (int i = 0; i < 256; i++)
            {
                if (i <= min) lut[i] = 0;
                else if (i >= max) lut[i] = 255;
                else
                {
                    lut[i] = (byte)((i - min) * scale);
                }
            }
            return lut;
        }
        private void ProcessVignette(byte[] pixels, int width, int height, int stride)
        {
            var parallelOptions = CreatePerformanceParallelOptions();
            float centerX = width / 2.0f;
            float centerY = height / 2.0f;
            float maxDistSq = centerX * centerX + centerY * centerY;

            unsafe
            {
                fixed (byte* ptr = pixels)
                {
                    IntPtr basePtrInt = (IntPtr)ptr;
                    Parallel.For(0, height, parallelOptions, y =>
                    {
                        byte* basePtr = (byte*)basePtrInt;
                        byte* row = basePtr + y * stride;
                        float dy = y - centerY;
                        float dySq = dy * dy;
                        for (int x = 0; x < width; x++)
                        {
                            float dx = x - centerX;
                            float distSq = dx * dx + dySq;
                            float factor = 1.0f - distSq / maxDistSq;
                            if (factor < 0) factor = 0;

                            byte* p = row + x * 4;
                            p[0] = (byte)(p[0] * factor);
                            p[1] = (byte)(p[1] * factor);
                            p[2] = (byte)(p[2] * factor);
                        }
                    });
                }
            }
        }

        private void ProcessGlow(byte[] pixels, int width, int height, int stride)
        {
            var parallelOptions = CreatePerformanceParallelOptions();
            byte[] srcPixels = (byte[])pixels.Clone();
            unsafe
            {
                fixed (byte* destPtr = pixels)
                fixed (byte* srcPtr = srcPixels)
                {
                    IntPtr dpInt = (IntPtr)destPtr;
                    IntPtr spInt = (IntPtr)srcPtr;

                    Parallel.For(0, height, parallelOptions, y =>
                    {
                        byte* dp = (byte*)dpInt;
                        byte* sp = (byte*)spInt;
                        byte* destRow = dp + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            int sumB = 0, sumG = 0, sumR = 0, count = 0;
                            for (int ky = -2; ky <= 2; ky += 2)
                            {
                                int py = Math.Clamp(y + ky, 0, height - 1);
                                byte* srcRowK = sp + py * stride;
                                for (int kx = -2; kx <= 2; kx += 2)
                                {
                                    int px = Math.Clamp(x + kx, 0, width - 1);
                                    byte* p = srcRowK + px * 4;
                                    sumB += p[0];
                                    sumG += p[1];
                                    sumR += p[2];
                                    count++;
                                }
                            }

                            float invCount = 1.0f / count;
                            float blurB = sumB * invCount;
                            float blurG = sumG * invCount;
                            float blurR = sumR * invCount;

                            byte* pOrig = sp + y * stride + x * 4;
                            destRow[x * 4] = (byte)(255 - ((255 - pOrig[0]) * (255 - (int)blurB) / 255));
                            destRow[x * 4 + 1] = (byte)(255 - ((255 - pOrig[1]) * (255 - (int)blurG) / 255));
                            destRow[x * 4 + 2] = (byte)(255 - ((255 - pOrig[2]) * (255 - (int)blurR) / 255));
                        }
                    });
                }
            }
        }

        private void ProcessRedEyeRemoval(byte[] pixels, int width, int height, int stride)
        {
            var parallelOptions = CreatePerformanceParallelOptions();
            unsafe
            {
                fixed (byte* ptr = pixels)
                {
                    IntPtr basePtrInt = (IntPtr)ptr;
                    Parallel.For(0, height, parallelOptions, y =>
                    {
                        byte* basePtr = (byte*)basePtrInt;
                        byte* row = basePtr + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            byte* p = row + x * 4;
                            int b = p[0];
                            int g = p[1];
                            int r = p[2];

                            // 简单的红眼检测：红色分量显著高于绿蓝分量
                            if (r > 128 && r > g * 2 && r > b * 2)
                            {
                                p[2] = (byte)((g + b) / 2);
                            }
                        }
                    });
                }
            }
        }

        private void ProcessEdgeDetection(byte[] pixels, int width, int height, int stride)
        {
            var parallelOptions = CreatePerformanceParallelOptions();
            byte[] srcPixels = (byte[])pixels.Clone();
            unsafe
            {
                fixed (byte* destPtr = pixels)
                fixed (byte* srcPtr = srcPixels)
                {
                    IntPtr dpInt = (IntPtr)destPtr;
                    IntPtr spInt = (IntPtr)srcPtr;

                    Parallel.For(1, height - 1, parallelOptions, y =>
                    {
                        byte* dp = (byte*)dpInt;
                        byte* sp = (byte*)spInt;
                        byte* pDestRow = dp + y * stride;
                        for (int x = 1; x < width - 1; x++)
                        {
                            // Sobel 算子
                            // Gx: -1 0 1    Gy: -1 -2 -1
                            //     -2 0 2         0  0  0
                            //     -1 0 1         1  2  1
                            float gxR = 0, gxG = 0, gxB = 0;
                            float gyR = 0, gyG = 0, gyB = 0;

                            for (int ky = -1; ky <= 1; ky++)
                            {
                                byte* row = sp + (y + ky) * stride;
                                for (int kx = -1; kx <= 1; kx++)
                                {
                                    byte* p = row + (x + kx) * 4;
                                    float valB = p[0];
                                    float valG = p[1];
                                    float valR = p[2];

                                    int weightX = 0;
                                    if (kx == -1) weightX = (ky == 0) ? -2 : -1;
                                    else if (kx == 1) weightX = (ky == 0) ? 2 : 1;

                                    int weightY = 0;
                                    if (ky == -1) weightY = (kx == 0) ? -2 : -1;
                                    else if (ky == 1) weightY = (kx == 0) ? 2 : 1;

                                    gxB += valB * weightX; gxG += valG * weightX; gxR += valR * weightX;
                                    gyB += valB * weightY; gyG += valG * weightY; gyR += valR * weightY;
                                }
                            }

                            int resB = (int)Math.Sqrt(gxB * gxB + gyB * gyB);
                            int resG = (int)Math.Sqrt(gxG * gxG + gyG * gyG);
                            int resR = (int)Math.Sqrt(gxR * gxR + gyR * gyR);

                            pDestRow[x * 4] = (byte)Math.Clamp(resB, 0, 255);
                            pDestRow[x * 4 + 1] = (byte)Math.Clamp(resG, 0, 255);
                            pDestRow[x * 4 + 2] = (byte)Math.Clamp(resR, 0, 255);
                        }
                    });
                }
            }
        }

        private void ProcessPencilSketch(byte[] pixels, int width, int height, int stride)
        {
            var parallelOptions = CreatePerformanceParallelOptions();
            // 1. 转灰度
            byte[] grayPixels = new byte[pixels.Length];
            float wr = (float)AppConsts.GrayWeightR;
            float wg = (float)AppConsts.GrayWeightG;
            float wb = (float)AppConsts.GrayWeightB;

            unsafe
            {
                fixed (byte* srcP = pixels)
                fixed (byte* grayP = grayPixels)
                {
                    IntPtr sPtr = (IntPtr)srcP;
                    IntPtr gPtr = (IntPtr)grayP;
                    Parallel.For(0, height, parallelOptions, y =>
                    {
                        byte* sRow = (byte*)sPtr + y * stride;
                        byte* gRow = (byte*)gPtr + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            byte b = sRow[x * 4], g = sRow[x * 4 + 1], r = sRow[x * 4 + 2];
                            byte gray = (byte)(r * wr + g * wg + b * wb);
                            gRow[x * 4] = gRow[x * 4 + 1] = gRow[x * 4 + 2] = gray;
                            gRow[x * 4 + 3] = sRow[x * 4 + 3];
                        }
                    });
                }
            }

            // 2. 复制灰度图并反转
            byte[] invertedGray = (byte[])grayPixels.Clone();
            unsafe
            {
                fixed (byte* invP = invertedGray)
                {
                    IntPtr iPtr = (IntPtr)invP;
                    Parallel.For(0, height, parallelOptions, y =>
                    {
                        byte* iRow = (byte*)iPtr + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            iRow[x * 4] = (byte)(255 - iRow[x * 4]);
                            iRow[x * 4 + 1] = (byte)(255 - iRow[x * 4 + 1]);
                            iRow[x * 4 + 2] = (byte)(255 - iRow[x * 4 + 2]);
                        }
                    });
                }
            }

            // 3. 对反转图进行高斯模糊
            ProcessGaussianBlur(invertedGray, width, height, stride, 5);

            // 4. 颜色减淡混合 (Color Dodge): Result = Gray / (255 - BlurredInverted)
            unsafe
            {
                fixed (byte* destP = pixels)
                fixed (byte* grayP = grayPixels)
                fixed (byte* blurP = invertedGray)
                {
                    IntPtr dPtr = (IntPtr)destP;
                    IntPtr gPtr = (IntPtr)grayP;
                    IntPtr bPtr = (IntPtr)blurP;

                    Parallel.For(0, height, parallelOptions, y =>
                    {
                        byte* dRow = (byte*)dPtr + y * stride;
                        byte* gRow = (byte*)gPtr + y * stride;
                        byte* bRow = (byte*)bPtr + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                int a = gRow[x * 4 + i];
                                int b = bRow[x * 4 + i];
                                // Color Dodge
                                int res = b == 255 ? 255 : Math.Min(255, (a << 8) / (255 - b));
                                dRow[x * 4 + i] = (byte)res;
                            }
                            dRow[x * 4 + 3] = gRow[x * 4 + 3];
                        }
                    });
                }
            }
        }

        private void ProcessOilPaint(byte[] pixels, int width, int height, int stride, int radius, int intensityLevels)
        {
            var parallelOptions = CreatePerformanceParallelOptions();
            // 优化：预先分配克隆数组，减少内部循环的计算
            byte[] srcPixels = (byte[])pixels.Clone();

            unsafe
            {
                fixed (byte* destPtr = pixels)
                fixed (byte* srcPtr = srcPixels)
                {
                    IntPtr dpInt = (IntPtr)destPtr;
                    IntPtr spInt = (IntPtr)srcPtr;

                    Parallel.For(0, height, parallelOptions, y =>
                    {
                        byte* dp = (byte*)dpInt;
                        byte* sp = (byte*)spInt;
                        byte* destRow = dp + y * stride;

                        // 性能优化：将象限统计移动到局部变量，减少内存写入
                        for (int x = 0; x < width; x++)
                        {
                            float s0=0, s1=0, s2=0, s3=0;
                            int mr0=0, mg0=0, mb0=0, c0=0;
                            int mr1=0, mg1=0, mb1=0, c1=0;
                            int mr2=0, mg2=0, mb2=0, c2=0;
                            int mr3=0, mg3=0, mb3=0, c3=0;

                            // 边界优化：手动处理 Clamp，减少调用开销
                            for (int ky = -radius; ky <= radius; ky++)
                            {
                                int py = y + ky;
                                if (py < 0) py = 0; else if (py >= height) py = height - 1;
                                byte* rowK = sp + py * stride;
                                bool upper = ky <= 0;

                                for (int kx = -radius; kx <= radius; kx++)
                                {
                                    int px = x + kx;
                                    if (px < 0) px = 0; else if (px >= width) px = width - 1;

                                    byte* p = rowK + px * 4;
                                    byte b = p[0], g = p[1], r = p[2];
                                    float val = (r + g + b) * 0.3333f;
                                    float valSq = val * val;

                                    if (upper)
                                    {
                                        if (kx <= 0) { mr0 += r; mg0 += g; mb0 += b; c0++; s0 += valSq; }
                                        else { mr1 += r; mg1 += g; mb1 += b; c1++; s1 += valSq; }
                                    }
                                    else
                                    {
                                        if (kx <= 0) { mr2 += r; mg2 += g; mb2 += b; c2++; s2 += valSq; }
                                        else { mr3 += r; mg3 += g; mb3 += b; c3++; s3 += valSq; }
                                    }
                                }
                            }

                            // 寻找方差最小的象限
                            float minVar = float.MaxValue;
                            int resR=0, resG=0, resB=0;

                            // 辅助方法：内联方差计算
                            void CheckQ(int r, int g, int b, int count, float sigma)
                            {
                                if (count == 0) return;
                                float invC = 1.0f / count;
                                float mean = (r + g + b) * 0.3333f * invC;
                                float variance = (sigma * invC) - (mean * mean);
                                if (variance < minVar)
                                {
                                    minVar = variance;
                                    resR = (int)(r * invC); resG = (int)(g * invC); resB = (int)(b * invC);
                                }
                            }

                            CheckQ(mr0, mg0, mb0, c0, s0);
                            CheckQ(mr1, mg1, mb1, c1, s1);
                            CheckQ(mr2, mg2, mb2, c2, s2);
                            CheckQ(mr3, mg3, mb3, c3, s3);

                            destRow[x * 4] = (byte)resB;
                            destRow[x * 4 + 1] = (byte)resG;
                            destRow[x * 4 + 2] = (byte)resR;
                        }
                    });
                }
            }
        }
    }
}
