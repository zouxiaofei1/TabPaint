using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TabPaint
{
    public class AiService
    {
        private const string ModelUrl_HF = "https://huggingface.co/briaai/RMBG-1.4/resolve/main/onnx/model.onnx";

        // ModelScope 镜像 (示例链接，如果不可用需替换为其他可用的 RMBG 镜像或 U2Net)
        private const string ModelUrl_MS = "https://modelscope.cn/models/AI-ModelScope/RMBG-1.4/resolve/master/onnx/model.onnx";

        private const string ModelName = "rmbg-1.4.onnx";

        // 开发时先设为 null，第一次下载成功后在控制台输出 MD5，然后填入此处
        private const string ExpectedMD5 = null; // "你的MD5值"; 

        private readonly string _cacheDir;

        public AiService(string cacheDir)
        {
            _cacheDir = cacheDir;
            if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);
        }

        public async Task<string> PrepareModelAsync(IProgress<double> progress)
        {
            string modelPath = Path.Combine(_cacheDir, ModelName);

            // 1. 检查是否存在
            if (File.Exists(modelPath))
            {
                // 如果设置了MD5且校验失败，则删除重下
                if (!string.IsNullOrEmpty(ExpectedMD5) && !VerifyMd5(modelPath, ExpectedMD5))
                {
                    System.Diagnostics.Debug.WriteLine("MD5校验失败，重新下载...");
                    File.Delete(modelPath);
                }
                else
                {
                    return modelPath;
                }
            }

            // 2. 决定下载源
            string url = CultureInfo.CurrentCulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                ? ModelUrl_MS // 中文环境首选 ModelScope
                : ModelUrl_HF;

            // 3. 下载 (修复 403 Forbidden)
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                // 有些CDN需要 Referer
                if (url.Contains("huggingface"))
                    client.DefaultRequestHeaders.Add("Referer", "https://huggingface.co/");

                using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        url = (url == ModelUrl_MS) ? ModelUrl_HF : ModelUrl_MS;
                        response.Dispose(); // 释放上一个
                        var fallbackResponse = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                        fallbackResponse.EnsureSuccessStatusCode();
                    }
                    else
                    {
                        response.EnsureSuccessStatusCode();
                    }

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(modelPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var totalRead = 0L;
                        var buffer = new byte[8192];
                        var isMoreToRead = true;

                        do
                        {
                            var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                            if (read == 0)
                            {
                                isMoreToRead = false;
                            }
                            else
                            {
                                await fileStream.WriteAsync(buffer, 0, read);
                                totalRead += read;
                                if (totalBytes != -1 && progress != null)
                                    progress.Report((double)totalRead / totalBytes * 100);
                            }
                        } while (isMoreToRead);
                    }
                }
            }

            CalculateAndPrintMd5(modelPath);

            return modelPath;
        }

        private bool VerifyMd5(string path, string expected)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(path))
            {
                var hash = md5.ComputeHash(stream);
                var hashStr = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                return hashStr.Equals(expected.ToLowerInvariant());
            }
        }

        private void CalculateAndPrintMd5(string path)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(path))
            {
                var hash = md5.ComputeHash(stream);
                var hashStr = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                System.Diagnostics.Debug.WriteLine($"[AI Model MD5] {hashStr}");
            }
        }

        public async Task<byte[]> RunInferenceAsync(string modelPath, WriteableBitmap originalBmp)
        {
            int targetW = 1024;
            int targetH = 1024;
            var resized = new TransformedBitmap(originalBmp,
                new ScaleTransform((double)targetW / originalBmp.PixelWidth, (double)targetH / originalBmp.PixelHeight));
            var wb = new WriteableBitmap(resized);
            int inputStride = wb.BackBufferStride;
            byte[] inputPixels = new byte[targetH * inputStride];
            wb.CopyPixels(inputPixels, inputStride, 0);

            // B. 准备原图数据 (用于最后合成) - UI 线程
            int origW = originalBmp.PixelWidth;
            int origH = originalBmp.PixelHeight;
            int origStride = originalBmp.BackBufferStride;
            byte[] originalPixels = new byte[origH * origStride];
            originalBmp.CopyPixels(originalPixels, origStride, 0); // 必须在主线程读原图

            // C. 后台线程
            return await Task.Run(() =>
            {
                using var session = new InferenceSession(modelPath);

                // 转换 Tensor
                var tensor = PreprocessPixelsToTensor(inputPixels, targetW, targetH, inputStride);

                // 推理
                string inputName = session.InputMetadata.Keys.First();
                var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };
                using var results = session.Run(inputs);

                // 后处理 (传入原图的 byte[] 数据，而不是 Bitmap 对象)
                var outputTensor = results.First().AsTensor<float>();
                return PostProcess(outputTensor, originalPixels, origW, origH, origStride);
            });
        }
        private DenseTensor<float> PreprocessPixelsToTensor(byte[] pixels, int w, int h, int stride)
        {
            var tensor = new DenseTensor<float>(new[] { 1, 3, h, w });

            // 使用 Parallel 加速纯数据循环
            Parallel.For(0, h, y =>
            {
                for (int x = 0; x < w; x++)
                {
                    int offset = y * stride + x * 4;

                    // 注意数组越界保护
                    if (offset + 2 >= pixels.Length) continue;

                    // WPF 是 BGRA 顺序
                    float b = pixels[offset + 0] / 255.0f;
                    float g = pixels[offset + 1] / 255.0f;
                    float r = pixels[offset + 2] / 255.0f;

                    // 标准化 (0-1)
                    tensor[0, 0, y, x] = r;
                    tensor[0, 1, y, x] = g;
                    tensor[0, 2, y, x] = b;
                }
            });

            return tensor;
        }

        private DenseTensor<float> Preprocess(WriteableBitmap bmp, int targetW, int targetH)
        {
            // 高质量缩放
            var resized = new TransformedBitmap(bmp, new ScaleTransform((double)targetW / bmp.PixelWidth, (double)targetH / bmp.PixelHeight));
            var wb = new WriteableBitmap(resized);

            var tensor = new DenseTensor<float>(new[] { 1, 3, targetH, targetW });
            int stride = wb.BackBufferStride;

            wb.Lock();
            unsafe
            {
                byte* ptr = (byte*)wb.BackBuffer;

                // 使用 Parallel 加速预处理循环
                Parallel.For(0, targetH, y =>
                {
                    for (int x = 0; x < targetW; x++)
                    {
                        int offset = y * stride + x * 4;
                        // RMBG 归一化: 0-1 range
                        // 注意 WPF 是 BGRA 顺序
                        float b = ptr[offset + 0] / 255.0f;
                        float g = ptr[offset + 1] / 255.0f;
                        float r = ptr[offset + 2] / 255.0f;

                        tensor[0, 0, y, x] = r;
                        tensor[0, 1, y, x] = g;
                        tensor[0, 2, y, x] = b;
                    }
                });
            }
            wb.Unlock();
            return tensor;
        }

        private byte[] PostProcess(Tensor<float> maskTensor, byte[] originalPixels, int w, int h, int stride)
        {
            // 创建结果数组，先复制原图内容
            byte[] resultPixels = new byte[originalPixels.Length];
            Array.Copy(originalPixels, resultPixels, originalPixels.Length);

            int maskW = maskTensor.Dimensions[3];
            int maskH = maskTensor.Dimensions[2];

            Parallel.For(0, h, y =>
            {
                int maskY = (int)((float)y / h * maskH);
                if (maskY >= maskH) maskY = maskH - 1;

                for (int x = 0; x < w; x++)
                {
                    int maskX = (int)((float)x / w * maskW);
                    if (maskX >= maskW) maskX = maskW - 1;

                    float alphaVal = maskTensor[0, 0, maskY, maskX];

                    int offset = y * stride + x * 4;

                    if (offset + 3 < resultPixels.Length)
                    {
                        byte originalAlpha = resultPixels[offset + 3];
                        // 混合 Alpha
                        resultPixels[offset + 3] = (byte)(originalAlpha * Math.Clamp(alphaVal, 0, 1));
                    }
                }
            });

            return resultPixels;
        }
    }
}
