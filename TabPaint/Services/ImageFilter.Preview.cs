using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace TabPaint
{
    public partial class MainWindow
    {
        public async Task<BitmapSource?> GetFilterPreviewAsync(string filterTag, CancellationToken token)
        {
            if (_bitmap == null) return null;

            // 1. 获取一个缩小的位图，用于快速处理
            BitmapSource? thumbnail = null;
            Dispatcher.Invoke(() =>
            {
                double scale = 250.0 / Math.Max(_bitmap.PixelWidth, _bitmap.PixelHeight);
                if (scale >= 1.0)
                {
                    thumbnail = _bitmap.Clone();
                }
                else
                {
                    thumbnail = new TransformedBitmap(_bitmap, new System.Windows.Media.ScaleTransform(scale, scale));
                }
                thumbnail.Freeze();
            });

            if (thumbnail == null) return null;

            // 2. 转换为字节数组并应用滤镜
            return await Task.Run(() =>
            {
                try
                {
                    int width = thumbnail.PixelWidth;
                    int height = thumbnail.PixelHeight;
                    int stride = width * 4;
                    byte[] pixels = new byte[height * stride];
                    thumbnail.CopyPixels(pixels, stride, 0);

                    if (token.IsCancellationRequested) return null;

                    switch (filterTag)
                    {
                        case "Sepia": ProcessSepia(pixels, width, height, stride); break;
                        case "OilPaint": ProcessOilPaint(pixels, width, height, stride, 3, 10); break;
                        case "Vignette": ProcessVignette(pixels, width, height, stride); break;
                        case "Glow": ProcessGlow(pixels, width, height, stride); break;
                        case "Gray":
                            {
                                float wr = (float)AppConsts.GrayWeightR;
                                float wg = (float)AppConsts.GrayWeightG;
                                float wb = (float)AppConsts.GrayWeightB;
                                for (int i = 0; i < pixels.Length; i += 4)
                                {
                                    byte gray = (byte)(pixels[i + 2] * wr + pixels[i + 1] * wg + pixels[i] * wb);
                                    pixels[i] = pixels[i + 1] = pixels[i + 2] = gray;
                                }
                            }
                            break;
                        case "Invert":
                            for (int i = 0; i < pixels.Length; i += 4)
                            {
                                pixels[i] = (byte)(255 - pixels[i]);
                                pixels[i + 1] = (byte)(255 - pixels[i + 1]);
                                pixels[i + 2] = (byte)(255 - pixels[i + 2]);
                            }
                            break;
                        case "Sharpen": ProcessSharpen(pixels, width, height, stride); break;
                        case "Brown": ProcessBrown(pixels, width, height, stride); break;
                        case "Mosaic": ProcessMosaic(pixels, width, height, stride, 8); break;
                        case "Blur": ProcessGaussianBlur(pixels, width, height, stride, 5); break;
                        case "RedEye": ProcessRedEyeRemoval(pixels, width, height, stride); break;
                        case "Pencil": ProcessPencilSketch(pixels, width, height, stride); break;
                        case "Edge": ProcessEdgeDetection(pixels, width, height, stride); break;
                    }

                    if (token.IsCancellationRequested) return null;

                    var resBmp = new WriteableBitmap(width, height, 96, 96, thumbnail.Format, null);
                    resBmp.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
                    resBmp.Freeze();
                    return resBmp as BitmapSource;
                }
                catch { return null; }
            }, token);
        }
    }
}
