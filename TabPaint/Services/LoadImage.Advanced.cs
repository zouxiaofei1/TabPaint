using SkiaSharp;
using Svg.Skia;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using TabPaint.Services;
using XamlAnimatedGif; // 添加这一行
//
//图片加载,包括icc,ico,svg等高级格式
//以及加载动画

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public static bool IsWebpPath(string? filePath)
        {
            return string.Equals(System.IO.Path.GetExtension(filePath), ".webp", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsWebpStream(Stream stream)
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

        public static bool IsWebpFileOrStream(string? filePath, Stream stream)
        {
            return IsWebpPath(filePath) || IsWebpStream(stream);
        }

        public static bool IsPsdPath(string? filePath)
        {
            return string.Equals(System.IO.Path.GetExtension(filePath), ".psd", StringComparison.OrdinalIgnoreCase);
        }

        public static BitmapSource DecodePsd(Stream stream, int? targetMaxWidth = null, int? targetMaxHeight = null)
        {
            if (!PsdPluginHelper.IsPsdPluginAvailable())
            {
                Logger.Error("PSD Plugin: DecodePsd called but plugin is not available.");
                return null;
            }
            try
            {
                stream.Position = 0;
                Type magickImageType = PsdPluginHelper.GetMagickImageType()!;
                Assembly magickAssembly = PsdPluginHelper.GetAssembly()!;
                // ★ 创建 MagickImage 实例
                using dynamic image = Activator.CreateInstance(magickImageType, new object[] { stream })!;
                int currentWidth = (int)image.Width;
                int currentHeight = (int)image.Height;
                Logger.Info($"PSD Plugin: Image loaded, size={currentWidth}x{currentHeight}");
                // ★ 缩放（如果需要）
                if (targetMaxWidth.HasValue || targetMaxHeight.HasValue)
                {
                    double scale = 1.0;
                    if (targetMaxWidth.HasValue && currentWidth > targetMaxWidth.Value)
                        scale = Math.Min(scale, (double)targetMaxWidth.Value / currentWidth);
                    if (targetMaxHeight.HasValue && currentHeight > targetMaxHeight.Value)
                        scale = Math.Min(scale, (double)targetMaxHeight.Value / currentHeight);
                    if (scale < 1.0)
                    {
                        uint newWidth = (uint)Math.Max(1, (int)(currentWidth * scale));
                        uint newHeight = (uint)Math.Max(1, (int)(currentHeight * scale));
                        image.Resize(newWidth, newHeight);
                        currentWidth = (int)image.Width;
                        currentHeight = (int)image.Height;
                    }
                }
                // ★ 方法1：使用 ToByteArray + MagickFormat.Png 转为 PNG 再解码
                //    这种方式最安全，兼容所有版本
                BitmapSource result = DecodePsdViaPng(image, currentWidth, currentHeight);
                if (result != null) return result;
                // ★ 方法2（备用）：直接读取像素
                return DecodePsdViaPixels(image, magickAssembly, currentWidth, currentHeight);
            }
            catch (Exception ex)
            {
                Logger.Error("PSD Plugin: DecodePsd Exception", ex);
                return null;
            }
        }
        /// <summary>
        /// 通过 PNG 中转解码——最安全的方式
        /// </summary>
        private static BitmapSource DecodePsdViaPng(dynamic image, int width, int height)
        {
            try
            {
                // image.Format = MagickFormat.Png32;  (带 Alpha 的 PNG)
                // 用反射设置 Format
                Assembly asm = PsdPluginHelper.GetAssembly()!;
                // 搜索 MagickFormat 枚举（可能在 Core 或主程序集中）
                Type formatType = FindType(asm, "ImageMagick.MagickFormat");
                if (formatType != null)
                {
                    // 尝试使用 Png32（32位PNG，保留Alpha）
                    object png32Value = null;
                    try { png32Value = Enum.Parse(formatType, "Png32"); } catch { }
                    if (png32Value == null)
                        try { png32Value = Enum.Parse(formatType, "Png"); } catch { }
                    if (png32Value != null)
                    {
                        // 设置输出格式
                        var formatProp = ((object)image).GetType().GetProperty("Format");
                        formatProp?.SetValue(image, png32Value);
                    }
                }
                // 转为 byte[]
                byte[] pngBytes = image.ToByteArray();
                if (pngBytes != null && pngBytes.Length > 0)
                {
                    using var ms = new MemoryStream(pngBytes);
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = ms;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                    Logger.Info($"PSD Plugin: Decoded via PNG, {bitmapImage.PixelWidth}x{bitmapImage.PixelHeight}");
                    return bitmapImage;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("PSD Plugin: PNG method failed", ex);
            }
            return null;
        }
        /// <summary>
        /// 直接读取像素数据——备用方式
        /// </summary>
        private static BitmapSource DecodePsdViaPixels(dynamic image, Assembly asm, int width, int height)
        {
            try
            {
                // ★ 尝试各种获取像素的方式
                // 方式 A：GetPixelsUnsafe().ToByteArray("BGRA") — 字符串参数
                try
                {
                    dynamic pixels = image.GetPixelsUnsafe();
                    // 先尝试字符串参数（新版本 API）
                    byte[] data = null;
                    try
                    {
                        data = pixels.ToByteArray("BGRA");
                    }
                    catch
                    {
                        // 尝试枚举参数（旧版本）
                        Type mappingType = FindType(asm, "ImageMagick.PixelMapping");
                        if (mappingType != null)
                        {
                            object bgraMapping = Enum.Parse(mappingType, "BGRA");
                            data = pixels.ToByteArray(bgraMapping);
                        }
                    }
                    if (data != null && data.Length > 0)
                    {
                        var wb = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                        wb.WritePixels(new Int32Rect(0, 0, width, height), data, width * 4, 0);
                        wb.Freeze();
                        Logger.Info($"PSD Plugin: Decoded via pixels, {width}x{height}");
                        return wb;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("PSD Plugin: Pixel method A failed", ex);
                }
                // 方式 B：GetPixels().GetArea() 
                try
                {
                    dynamic pixels2 = image.GetPixels();
                    // GetArea returns ushort[] in some versions
                    var area = pixels2.GetArea(0, 0, (uint)width, (uint)height);
                    if (area is byte[] byteArea)
                    {
                        var wb = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                        wb.WritePixels(new Int32Rect(0, 0, width, height), byteArea, width * 4, 0);
                        wb.Freeze();
                        return wb;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("PSD Plugin: Pixel method B failed", ex);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("PSD Plugin: All pixel methods failed", ex);
            }
            return null;
        }
        /// <summary>
        /// 在已加载的所有 Magick 相关程序集中查找类型
        /// </summary>
        private static Type FindType(Assembly primaryAssembly, string fullTypeName)
        {
            // 1. 先从主程序集找
            var t = primaryAssembly?.GetType(fullTypeName);
            if (t != null) return t;
            // 2. 从 Core 程序集找
            string corePath = System.IO.Path.Combine(AppConsts.PluginsDir, "Magick.NET.Core.dll");
            if (File.Exists(corePath))
            {
                try
                {
                    var coreAsm = Assembly.LoadFrom(corePath);
                    t = coreAsm?.GetType(fullTypeName);
                    if (t != null) return t;
                }
                catch { }
            }
            // 3. 从所有已加载的程序集中搜索
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.FullName?.Contains("Magick") == true)
                {
                    t = asm.GetType(fullTypeName);
                    if (t != null) return t;
                }
            }
            return null;
        }

        internal static (int Width, int Height)? GetPsdDimensions(Stream stream)
        {
            if (!PsdPluginHelper.IsPsdPluginAvailable())
            {
                Logger.Error("PSD Plugin: GetPsdDimensions called but plugin is not available.");
                return null;
            }

            try
            {
                stream.Position = 0;
                Type infoType = PsdPluginHelper.GetMagickImageInfoType()!;
                if (infoType == null)
                {
                    Logger.Error("PSD Plugin: GetMagickImageInfoType returned null.");
                    return null;
                }
                dynamic info = Activator.CreateInstance(infoType, new object[] { stream })!;
                return ((int)info.Width, (int)info.Height);
            }
            catch (Exception ex)
            {
                Logger.Error("PSD Plugin: GetPsdDimensions Exception", ex);
                return null;
            }
        }

        public static BitmapSource DecodeWebpWithSkia(Stream stream, int? targetMaxWidth = null, int? targetMaxHeight = null)
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

        internal static (int Width, int Height)? GetWebpDimensionsWithSkia(Stream stream)
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

        internal static int GetLargestFrameIndex(BitmapDecoder decoder)
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

        internal static BitmapSource SkiaBitmapToWpfSource(SKBitmap skBitmap)
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

                // 3. 根据设置的解码大小进行缩放
                int targetSize = SettingsManager.Instance.Current.SvgDecodeSize;
                float scale = 1.0f;
                if (srcWidth > 0 && srcHeight > 0)
                {
                    scale = Math.Min((float)targetSize / srcWidth, (float)targetSize / srcHeight);
                }

                int width = (int)Math.Max(1, srcWidth * scale);
                int height = (int)Math.Max(1, srcHeight * scale);

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
