using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using XamlAnimatedGif; // 添加这一行
using SkiaSharp;
using Svg.Skia;
//
//图片加载,包括icc,ico,svg等高级格式
//以及加载动画

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private static bool IsWebpPath(string? filePath)
        {
            return string.Equals(System.IO.Path.GetExtension(filePath), ".webp", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWebpStream(Stream stream)
        {
            if (stream == null || !stream.CanSeek) return false;

            long oldPos = stream.Position;
            try
            {
                stream.Position = 0;
                Span<byte> header = stackalloc byte[12];
                int read = stream.Read(header);
                if (read < 12) return false;

                return header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F'
                    && header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P';
            }
            catch
            {
                return false;
            }
            finally
            {
                stream.Position = oldPos;
            }
        }

        private static bool IsWebpFileOrStream(string? filePath, Stream stream)
        {
            return IsWebpPath(filePath) || IsWebpStream(stream);
        }

        private BitmapSource DecodeWebpWithSkia(Stream stream, int? targetMaxWidth = null, int? targetMaxHeight = null)
        {
            try
            {
                stream.Position = 0;
                using var codec = SKCodec.Create(stream);
                if (codec == null || codec.EncodedFormat != SKEncodedImageFormat.Webp) return null;

                int srcW = codec.Info.Width;
                int srcH = codec.Info.Height;
                int dstW = srcW;
                int dstH = srcH;

                double scale = 1.0;
                if (targetMaxWidth.HasValue && srcW > targetMaxWidth.Value)
                {
                    scale = Math.Min(scale, (double)targetMaxWidth.Value / srcW);
                }
                if (targetMaxHeight.HasValue && srcH > targetMaxHeight.Value)
                {
                    scale = Math.Min(scale, (double)targetMaxHeight.Value / srcH);
                }

                if (scale < 1.0)
                {
                    dstW = Math.Max(1, (int)Math.Round(srcW * scale));
                    dstH = Math.Max(1, (int)Math.Round(srcH * scale));
                }

                using var colorSpace = SKColorSpace.CreateSrgb();
                var info = new SKImageInfo(dstW, dstH, SKColorType.Bgra8888, SKAlphaType.Premul, colorSpace);
                using var skBitmap = new SKBitmap(info);

                var result = codec.GetPixels(info, skBitmap.GetPixels());
                if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
                {
                    return null;
                }

                return SkiaBitmapToWpfSource(skBitmap);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebP Skia Decode Error: {ex.Message}");
                return null;
            }
        }

        private (int Width, int Height)? GetWebpDimensionsWithSkia(Stream stream)
        {
            try
            {
                stream.Position = 0;
                using var codec = SKCodec.Create(stream);
                if (codec == null || codec.EncodedFormat != SKEncodedImageFormat.Webp) return null;
                return (codec.Info.Width, codec.Info.Height);
            }
            catch
            {
                return null;
            }
        }

        private int GetLargestFrameIndex(BitmapDecoder decoder)
        {
            if (decoder.Frames == null || decoder.Frames.Count == 0) return 0;
            if (decoder.Frames.Count == 1) return 0;

            int bestIndex = 0;
            long maxArea = 0;
            int maxBpp = 0;

            for (int i = 0; i < decoder.Frames.Count; i++)
            {
                try
                {
                    var frame = decoder.Frames[i];
                    long area = (long)frame.PixelWidth * frame.PixelHeight;
                    int bpp = frame.Format.BitsPerPixel;

                    // 优先比较面积，面积相同时比较颜色位深
                    if (area > maxArea || (area == maxArea && bpp > maxBpp))
                    {
                        maxArea = area;
                        maxBpp = bpp;
                        bestIndex = i;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error evaluating frame {i}: {ex.Message}");
                }
            }
            return bestIndex;
        }

        private BitmapSource DecodeWithSkiaAndIcc(Stream stream)
        {
            try
            {
                stream.Position = 0;
                using var codec = SKCodec.Create(stream);
                if (codec == null) return null;

                // --- 1. 准备色彩空间和参数 ---
                using var srgbSpace = SKColorSpace.CreateSrgb();
                var info = new SKImageInfo(
                    codec.Info.Width,
                    codec.Info.Height,
                    SKColorType.Bgra8888,
                    SKAlphaType.Premul,
                    srgbSpace);
                using var originalBitmap = new SKBitmap(info);

                // 这一步 Skia 会同时完成：解码 + ICC转sRGB + 格式转Bgra + 填充Alpha
                var result = codec.GetPixels(info, originalBitmap.GetPixels());

                if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
                {
                    Debug.WriteLine($"Skia decode status: {result}");
                    return null;
                }
                var origin = codec.EncodedOrigin;

                // 如果不需要旋转，直接转换并返回
                if (origin == SKEncodedOrigin.TopLeft)
                {
                    return SkiaBitmapToWpfSource(originalBitmap);
                }
                int newWidth = (origin == SKEncodedOrigin.RightTop || origin == SKEncodedOrigin.LeftBottom) ? info.Height : info.Width;
                int newHeight = (origin == SKEncodedOrigin.RightTop || origin == SKEncodedOrigin.LeftBottom) ? info.Width : info.Height;

                var rotatedInfo = info.WithSize(newWidth, newHeight);
                using var rotatedBitmap = new SKBitmap(rotatedInfo);
                using var canvas = new SKCanvas(rotatedBitmap);
                canvas.Clear(SKColors.Transparent);

                // 坐标系变换
                switch (origin)
                {
                    case SKEncodedOrigin.RightTop: // 90度
                        canvas.Translate(newWidth, 0);
                        canvas.RotateDegrees(90);
                        break;
                    case SKEncodedOrigin.BottomRight: // 180度
                        canvas.Translate(newWidth, newHeight);
                        canvas.RotateDegrees(180);
                        break;
                    case SKEncodedOrigin.LeftBottom: // 270度
                        canvas.Translate(0, newHeight);
                        canvas.RotateDegrees(270);
                        break;
                        // 其他镜像模式暂略，通常只需处理这几个
                }
                using (var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High })
                {
                    canvas.DrawBitmap(originalBitmap, 0, 0, paint);
                }
                canvas.Flush();

                // 转换回 WPF 对象
                return SkiaBitmapToWpfSource(rotatedBitmap);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Skia ICC Decode Error: {ex.Message}");
                return null;
            }
        }

        private BitmapSource SkiaBitmapToWpfSource(SKBitmap skBitmap)
        {
            // 1. 创建 WPF 的 WriteableBitmap
            var wb = new WriteableBitmap(skBitmap.Width, skBitmap.Height, 96, 96, PixelFormats.Bgra32, null);

            wb.Lock();
            try
            {
                // 2. 检查源数据信息
                var info = skBitmap.Info;
                unsafe
                {
                    void* srcPtr = (void*)skBitmap.GetPixels();  // 获取源地址 (Skia)
                    void* dstPtr = (void*)wb.BackBuffer;
                    long bytesToCopy = (long)skBitmap.Height * skBitmap.RowBytes;
                    Buffer.MemoryCopy(srcPtr, dstPtr, bytesToCopy, bytesToCopy);  // 执行拷贝
                }
                wb.AddDirtyRect(new Int32Rect(0, 0, skBitmap.Width, skBitmap.Height));//  标记脏区，通知 WPF 更新画面
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Bitmap Copy Error: {ex.Message}");
                return null;
            }
            finally
            {
                wb.Unlock();
            }

            wb.Freeze();
            return wb;
        }


        internal BitmapSource DecodeSvg(Stream stream, CancellationToken token)
        {
            if (token.IsCancellationRequested) return null;
            try
            {
                stream.Position = 0;
                using var svg = new SKSvg();
                svg.Load(stream);

                if (svg.Picture == null) return null;

                // 1. 获取 SVG 的原始设计尺寸
                float srcWidth = svg.Picture.CullRect.Width;
                float srcHeight = svg.Picture.CullRect.Height;

                // 2. 如果 SVG 没有定义尺寸，给个默认值
                if (srcWidth <= 0) srcWidth = AppConsts.FallbackImageWidth;
                if (srcHeight <= 0) srcHeight = AppConsts.FallbackImageHeight;
                float minSide = AppConsts.SvgMinSide;
                float scaleToMin = 1.0f;
                if (srcWidth < minSide || srcHeight < minSide)
                {
                    // 找出需要放大多少倍才能让最小边达到 512
                    float scaleW = minSide / srcWidth;
                    float scaleH = minSide / srcHeight;
                    scaleToMin = Math.Max(scaleW, scaleH);
                }

                // 应用最小缩放
                int width = (int)(srcWidth * scaleToMin);
                int height = (int)(srcHeight * scaleToMin);
                const int maxSize = (int)AppConsts.MaxCanvasSize;
                if (width > maxSize || height > maxSize)
                {
                    float scaleDown = Math.Min((float)maxSize / width, (float)maxSize / height);
                    width = (int)(width * scaleDown);
                    height = (int)(height * scaleDown);
                }

                // 4. 开始绘图
                var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var skiaBitmap = new SKBitmap(info);
                using var canvas = new SKCanvas(skiaBitmap);
                canvas.Clear(SKColors.Transparent);

                // 计算最终渲染时的缩放矩阵
                float finalScaleX = (float)width / srcWidth;
                float finalScaleY = (float)height / srcHeight;
                var matrix = SKMatrix.CreateScale(finalScaleX, finalScaleY);

                canvas.DrawPicture(svg.Picture, ref matrix);
                canvas.Flush();

                // 5. 直接转换为 WPF 对象，移除 PNG 中转
                return SkiaBitmapToWpfSource(skiaBitmap);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SVG Decode Error: " + ex.Message);
                return null;
            }
        }

        private async Task SimulateProgressAsync(CancellationToken token, long totalPixels, Action<string> progressCallback)
        {
            // 1. 初始进度 (假设元数据和缩略图已完成)
            double currentProgress = AppConsts.ProgressStartPercent;
            string loadingFormat = LocalizationManager.GetString("L_Progress_Loading_Format");
            progressCallback(string.Format(loadingFormat, (int)currentProgress));
            int performanceScore = PerformanceScore; // 假设这是之前定义的全局静态变量
            if (performanceScore <= 0) performanceScore = 5; // 默认值

            double scoreFactor = 0.5 + (performanceScore * 0.25);
            double estimatedMs = (totalPixels / 60000.0) / scoreFactor;
            if (estimatedMs < AppConsts.ProgressMinDurationMs) estimatedMs = AppConsts.ProgressMinDurationMs;
            int interval = AppConsts.ProgressIntervalMs;
            double steps = estimatedMs / interval;
            double incrementPerStep = (AppConsts.ProgressMaxPercent - currentProgress) / steps;

            try
            {
                while (!token.IsCancellationRequested && currentProgress < AppConsts.ProgressMaxPercent)
                {
                    await Task.Delay(interval, token).ConfigureAwait(false);
                    currentProgress += incrementPerStep;
                    if (currentProgress > AppConsts.ProgressLimitPercent) currentProgress = AppConsts.ProgressLimitPercent;

                    // 回调更新 UI
                    progressCallback(string.Format(loadingFormat, (int)currentProgress));
                }
            }
            catch (TaskCanceledException)
            {
            }
        }
    }
}