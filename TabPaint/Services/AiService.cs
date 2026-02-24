//
//AiService.cs
//AI服务类，封装了基于ONNX Runtime的背景移除、超分辨率重建和智能图像填补等功能。
//
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TabPaint
{
    public class AiDownloadStatus
    {
        public double Percentage { get; set; }
        public long BytesReceived { get; set; }
        public long TotalBytes { get; set; } // 如果未知则为 -1
        public double SpeedBytesPerSecond { get; set; }
    }
    public class AiService
    {
        private static AiService _instance;
        private static readonly object _instanceLock = new object();
        public static AiService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        _instance ??= new AiService(AppConsts.CacheDir);
                    }
                }
                return _instance;
            }
        }

        private readonly Dictionary<AiTaskType, InferenceSession> _sessions = new();
        private readonly System.Threading.SemaphoreSlim _sessionLock = new(1, 1);

        private const string BgRem_ModelUrl_HF = AppConsts.BgRem_ModelUrl_HF;
        private const string BgRem_ModelUrl_MS = AppConsts.BgRem_ModelUrl_MS;
        private const string BgRem_ModelName = AppConsts.BgRem_ModelName;
        private const string ExpectedMD5 = AppConsts.BgRem_ExpectedMD5;

        private readonly string _cacheDir;

        private const string Sr_ModelUrl_HF = AppConsts.Sr_ModelUrl_HF;
        private const string Sr_ModelUrl_Mirror = AppConsts.Sr_ModelUrl_Mirror;
        private const string Sr_ModelName = AppConsts.Sr_ModelName;
        private const int TileSize = AppConsts.Sr_TileSize; // 切块大小，越小内存占用越低，但推理次数越多
        private const int TileOverlap = AppConsts.AiSrTileOverlap; // 重叠区域，防止拼接缝隙
        private const int ScaleFactor = AppConsts.Sr_ScaleFactor;
        private const string Inpaint_ModelUrl = AppConsts.Inpaint_ModelUrl;
        private const string Inpaint_ModelUrl_Mirror = AppConsts.Inpaint_ModelUrl_Mirror;
        private const string Inpaint_ModelName = AppConsts.Inpaint_ModelName;
        private const string Inpaint_MD5 = AppConsts.Inpaint_ExpectedMD5;
        // 检查模型是否准备好
        public bool IsInpaintModelReady(){  return File.Exists(Path.Combine(_cacheDir, Inpaint_ModelName));  }
        private async Task<InferenceSession> GetSessionAsync(AiTaskType taskType, string modelPath)
        {
            await _sessionLock.WaitAsync();
            try
            {
                if (_sessions.TryGetValue(taskType, out var session))  return session;
                var options = taskType == AiTaskType.Inpainting ? new SessionOptions() : GetSessionOptions();
                if (taskType == AiTaskType.Inpainting)  options.AppendExecutionProvider_CPU();
                var newSession = await Task.Run(() => new InferenceSession(modelPath, options));
                _sessions[taskType] = newSession;
                return newSession;
            }
            finally  { _sessionLock.Release(); }
        }

        public void ReleaseModel(AiTaskType taskType)
        {
            _sessionLock.Wait();
            try
            {
                if (_sessions.TryGetValue(taskType, out var session))
                {
                    session.Dispose();
                    _sessions.Remove(taskType);
                }
            }
            finally  {  _sessionLock.Release(); }
        }

        public void ReleaseAllModels()
        {
            _sessionLock.Wait();
            try
            {
                foreach (var session in _sessions.Values)   {  session.Dispose(); }
                _sessions.Clear();
            }
            finally { _sessionLock.Release();  }
        }
        public async Task<byte[]> RunInpaintingAsync(string modelPath, byte[] imagePixels, byte[] maskPixels, int origW, int origH)
        {
            int targetW = AppConsts.AiInpaintSize;
            int targetH = AppConsts.AiInpaintSize;

            var session = await GetSessionAsync(AiTaskType.Inpainting, modelPath);

            return await Task.Run(() =>
            {
                var imgTensor = PreprocessImageBytesToTensor(imagePixels, targetW, targetH);
                var maskTensor = PreprocessMaskBytesToTensor(maskPixels, targetW, targetH);

                var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("image", imgTensor),
            NamedOnnxValue.CreateFromTensor("mask", maskTensor)
        };

                lock (session)
                {
                    using var results = session.Run(inputs);
                    var outputTensor = results.First().AsTensor<float>();
                    return PostProcessInpaintToBytes(outputTensor, targetW, targetH);
                }
            });
        }

        private DenseTensor<float> PreprocessMaskBytesToTensor(byte[] pixels, int w, int h)
        {
            var tensor = new DenseTensor<float>(new[] { 1, 1, h, w });
            Parallel.For(0, h, y =>
            {
                for (int x = 0; x < w; x++)
                {
                    int offset = (y * w + x) * 4;
                    float val = pixels[offset + 3] > 0 ? 1.0f : 0.0f;
                    tensor[0, 0, y, x] = val;
                }
            });
            return tensor;
        }
        private byte[] PostProcessInpaintToBytes(Tensor<float> tensor, int w, int h)
        {
            int stride = w * 4;
            byte[] pixels = new byte[h * stride];

            Parallel.For(0, h, y =>
            {
                for (int x = 0; x < w; x++)
                {
                    int offset = y * stride + x * 4;
                    float r = tensor[0, 0, y, x];
                    float g = tensor[0, 1, y, x];
                    float b = tensor[0, 2, y, x];
                    pixels[offset + 0] = (byte)Math.Clamp(b, 0, 255); // Blue
                    pixels[offset + 1] = (byte)Math.Clamp(g, 0, 255); // Green
                    pixels[offset + 2] = (byte)Math.Clamp(r, 0, 255); // Red
                    pixels[offset + 3] = 255; // Alpha
                }
            });
            return pixels;
        }

        private DenseTensor<float> PreprocessImageBytesToTensor(byte[] pixels, int w, int h)
        {
            var tensor = new DenseTensor<float>(new[] { 1, 3, h, w });
            Parallel.For(0, h, y =>
            {
                for (int x = 0; x < w; x++)
                {
                    int offset = (y * w + x) * 4;
                    tensor[0, 0, y, x] = pixels[offset + 2] / 255.0f; // R
                    tensor[0, 1, y, x] = pixels[offset + 1] / 255.0f; // G
                    tensor[0, 2, y, x] = pixels[offset + 0] / 255.0f; // B
                }
            });
            return tensor;
        }
        private DenseTensor<float> PreprocessImage(WriteableBitmap bmp, int w, int h)
        {
            var resized = new TransformedBitmap(bmp, new ScaleTransform((double)w / bmp.PixelWidth, (double)h / bmp.PixelHeight));
            var wb = new WriteableBitmap(resized);
            var tensor = new DenseTensor<float>(new[] { 1, 3, h, w });
            int stride = wb.BackBufferStride;
            wb.Lock();
            unsafe
            {
                byte* ptr = (byte*)wb.BackBuffer;
                Parallel.For(0, h, y =>
                {
                    for (int x = 0; x < w; x++)
                    {
                        int offset = y * stride + x * 4;
                        tensor[0, 0, y, x] = ptr[offset + 2] / 255.0f; // R
                        tensor[0, 1, y, x] = ptr[offset + 1] / 255.0f; // G
                        tensor[0, 2, y, x] = ptr[offset + 0] / 255.0f; // B
                    }
                });
            }
            wb.Unlock();
            return tensor;
        }
        private DenseTensor<float> PreprocessMask(WriteableBitmap bmp, int w, int h)
        {
            var resized = new TransformedBitmap(bmp, new ScaleTransform((double)w / bmp.PixelWidth, (double)h / bmp.PixelHeight));
            var wb = new WriteableBitmap(resized);
            var tensor = new DenseTensor<float>(new[] { 1, 1, h, w });
            int stride = wb.BackBufferStride;
            wb.Lock();
            unsafe
            {
                byte* ptr = (byte*)wb.BackBuffer;
                Parallel.For(0, h, y =>
                {
                    for (int x = 0; x < w; x++)
                    {
                        int offset = y * stride + x * 4;
                        float val = ptr[offset + 3] > 0 ? 1.0f : 0.0f;
                        tensor[0, 0, y, x] = val;
                    }
                });
            }
            wb.Unlock();
            return tensor;
        }
        public bool IsModelReady(AiTaskType taskType)
        {
            string modelName;
            switch (taskType)
            {
                case AiTaskType.RemoveBackground: modelName = BgRem_ModelName; break;
                case AiTaskType.SuperResolution: modelName = Sr_ModelName; break;
                case AiTaskType.Inpainting: modelName = Inpaint_ModelName; break;
                default: return false;
            }
            string finalPath = Path.Combine(_cacheDir, modelName);
            return File.Exists(finalPath);
        }

        public AiService(string cacheDir)
        {
            _cacheDir = cacheDir;
            if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);
        }
        public enum AiTaskType { RemoveBackground, SuperResolution, Inpainting }
        private int? _bestGpuId = null;

        private int GetBestGpuDeviceId()
        {
            if (_bestGpuId.HasValue) return _bestGpuId.Value;

            int bestId = 0; // 默认使用 0
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController"))
                {
                    var devices = searcher.Get().Cast<ManagementObject>().ToList();
                    for (int i = 0; i < devices.Count; i++)
                    {
                        var name = devices[i]["Name"]?.ToString() ?? "";
                        if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                            (name.Contains("AMD", StringComparison.OrdinalIgnoreCase) && !name.Contains("Radeon TM Graphics", StringComparison.OrdinalIgnoreCase)) || // 排除 AMD 核显
                            name.Contains("Arc", StringComparison.OrdinalIgnoreCase)) // Intel 独显
                        {
                            bestId = i;
                            System.Diagnostics.Debug.WriteLine($"[AI] Detected High-Perf GPU: {name} (ID: {i})");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AI] GPU Detection failed: {ex.Message}. Defaulting to 0.");
            }

            _bestGpuId = bestId;
            return bestId;
        }
        private SessionOptions GetSessionOptions()
        {
            var options = new SessionOptions();
            int gpuId = GetBestGpuDeviceId();

            try  {   options.AppendExecutionProvider_DML(gpuId);  }
            catch
            {
                if (gpuId != 0)
                {
                    try  { options.AppendExecutionProvider_DML(0);}
                    catch{ options.AppendExecutionProvider_CPU();}
                }
                else  options.AppendExecutionProvider_CPU();
            }
            return options;
        }
        public async Task<string> PrepareModelAsync(AiTaskType taskType, IProgress<AiDownloadStatus> progress, System.Threading.CancellationToken token)
        {
            string modelName;
            string expectedMd5;
            string urlMain;
            string urlMirror;

            switch (taskType)
            {
                case AiTaskType.RemoveBackground:
                    modelName = BgRem_ModelName;
                    expectedMd5 = ExpectedMD5;
                    urlMain = BgRem_ModelUrl_HF;
                    urlMirror = BgRem_ModelUrl_MS;
                    break;
                case AiTaskType.SuperResolution:
                    modelName = Sr_ModelName;
                    expectedMd5 = AppConsts.Sr_ExpectedMD5;
                    urlMain = Sr_ModelUrl_HF;
                    urlMirror = Sr_ModelUrl_Mirror;
                    break;
                case AiTaskType.Inpainting:
                    modelName = Inpaint_ModelName;
                    expectedMd5 = Inpaint_MD5; // 使用上方定义的常量，请务必计算正确MD5
                    urlMain = Inpaint_ModelUrl;
                    urlMirror = Inpaint_ModelUrl_Mirror;
                    break;
                default:
                    throw new ArgumentException("Unknown Task Type");
            }
            bool preferMirror = CultureInfo.CurrentCulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            string primaryUrl = preferMirror ? urlMirror : urlMain;
            string secondaryUrl = preferMirror ? urlMain : urlMirror;

            string finalPath = Path.Combine(_cacheDir, modelName);
            if (File.Exists(finalPath))
            {
                if (expectedMd5 == null) return finalPath; // 未提供MD5，直接返回已存在文件（开发阶段用）
                // 验证现有文件完整性
                if (await VerifyMd5Async(finalPath, expectedMd5))  return finalPath; // 文件存在且校验通过
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[AI] MD5 mismatch for existing file. Deleting {modelName}...");
                    try { File.Delete(finalPath); } catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); } // 校验失败，删除坏文件，准备重新下载
                }
            }

            // 2. 下载逻辑
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "TabPaint-Client/1.0");
                client.Timeout = TimeSpan.FromMinutes(AppConsts.AiDownloadTimeoutMinutes);
                if (!await DownloadAndValidateAsync(client, primaryUrl, finalPath, expectedMd5, progress, token))
                {
                    // 传入 token
                    if (!await DownloadAndValidateAsync(client, secondaryUrl, finalPath, expectedMd5, progress, token))
                    {
                        throw new Exception(string.Format(LocalizationManager.GetString("L_AI_Error_DownloadFailed"), modelName));
                    }
                }
            }
            return finalPath;
        }
        private async Task<bool> DownloadAndValidateAsync(HttpClient client, string url, string destPath, string expectedMd5, IProgress<AiDownloadStatus> progress, System.Threading.CancellationToken token)
        {
            string tempPath = destPath + ".tmp";

            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);

                using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    if (!response.IsSuccessStatusCode) return false;

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;

                    using (var contentStream = await response.Content.ReadAsStreamAsync(token))
                    using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var totalRead = 0L;
                        var buffer = new byte[AppConsts.AiDownloadBufferSize];
                        var isMoreToRead = true;

                        // 用于计算速度
                        var stopwatch = Stopwatch.StartNew();
                        long lastReportedBytes = 0;
                        long lastReportedTime = 0;

                        do
                        {
                            token.ThrowIfCancellationRequested();
                            var read = await contentStream.ReadAsync(buffer, 0, buffer.Length, token);
                            if (read == 0)
                            {
                                isMoreToRead = false;
                            }
                            else
                            {
                                await fileStream.WriteAsync(buffer, 0, read, token);
                                totalRead += read;
                                if (progress != null)
                                {
                                    long currentTime = stopwatch.ElapsedMilliseconds;
                                    if (currentTime - lastReportedTime > 100 || !isMoreToRead)
                                    {
                                        double timeDiffSeconds = (currentTime - lastReportedTime) / 1000.0;
                                        long bytesDiff = totalRead - lastReportedBytes;
                                        double speed = timeDiffSeconds > 0 ? bytesDiff / timeDiffSeconds : 0;

                                        var status = new AiDownloadStatus
                                        {
                                            BytesReceived = totalRead,
                                            TotalBytes = totalBytes,
                                            Percentage = totalBytes > 0 ? (double)totalRead / totalBytes * 100 : 0,
                                            SpeedBytesPerSecond = speed
                                        };

                                        progress.Report(status);

                                        lastReportedBytes = totalRead;
                                        lastReportedTime = currentTime;
                                    }
                                }
                            }
                        } while (isMoreToRead);
                    }
                }
                if (await VerifyMd5Async(tempPath, expectedMd5))
                {
                    if (File.Exists(destPath)) File.Delete(destPath);
                    File.Move(tempPath, destPath);
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[AI] Downloaded file MD5 mismatch.");
                    File.Delete(tempPath);
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AI] Download error: {ex.Message}");
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch (global::System.Exception cleanupEx) { global::System.Diagnostics.Debug.WriteLine(cleanupEx); }
                return false;
            }
        }
        private async Task<bool> VerifyMd5Async(string filePath, string expectedMd5)
        {
            if (string.IsNullOrEmpty(expectedMd5)) return true; // 如果未提供MD5，默认跳过校验（开发阶段用）
            if (!File.Exists(filePath)) return false;

            return await Task.Run(() =>
            {
                using (var md5 = MD5.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = md5.ComputeHash(stream);
                    var hashStr = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    return hashStr.Equals(expectedMd5.ToLowerInvariant());
                }
            });
        }
        public async Task<byte[]> RunInferenceAsync(string modelPath, WriteableBitmap originalBmp)
        {
            int targetW = AppConsts.AiInferenceSizeDefault;
            int targetH = AppConsts.AiInferenceSizeDefault;
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

            var session = await GetSessionAsync(AiTaskType.RemoveBackground, modelPath);
            return await Task.Run(() =>
            {
                var tensor = PreprocessPixelsToTensor(inputPixels, targetW, targetH, inputStride);
                string inputName = session.InputMetadata.Keys.First();
                var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };
                
                lock (session)
                {
                    using var results = session.Run(inputs);

                    // 后处理 (传入原图的 byte[] 数据，而不是 Bitmap 对象)
                    var outputTensor = results.First().AsTensor<float>();
                    return PostProcess(outputTensor, originalPixels, origW, origH, origStride);
                }
            });
        }
        public async Task<WriteableBitmap> RunSuperResolutionAsync(string modelPath, WriteableBitmap inputBitmap, IProgress<double> progress, System.Threading.CancellationToken token = default)
        {
            // 1. 获取原图数据
            int w = inputBitmap.PixelWidth;
            int h = inputBitmap.PixelHeight;
            int stride = inputBitmap.BackBufferStride;
            byte[] inputPixels = new byte[h * stride];
            inputBitmap.CopyPixels(inputPixels, stride, 0);

            // 2. 准备输出
            int outW = w * ScaleFactor;
            int outH = h * ScaleFactor;
            var outputBitmap = new WriteableBitmap(outW, outH, 96, 96, PixelFormats.Bgra32, null);
            int outStride = outputBitmap.BackBufferStride;
            byte[] outputPixels = new byte[outH * outStride];

            var session = await GetSessionAsync(AiTaskType.SuperResolution, modelPath);
            await Task.Run(() =>
            {
                int tilesX = (int)Math.Ceiling((double)w / TileSize);
                int tilesY = (int)Math.Ceiling((double)h / TileSize);
                int totalTiles = tilesX * tilesY;
                int processedTiles = 0;

                for (int y = 0; y < h; y += TileSize)
                {
                    for (int x = 0; x < w; x += TileSize)
                    {
                        token.ThrowIfCancellationRequested();
                        int validW = Math.Min(TileSize, w - x);
                        int validH = Math.Min(TileSize, h - y);
                        var tileTensor = ExtractTileToTensor(inputPixels, x, y, validW, validH, TileSize, stride, w, h);

                        // 3. 推理 (此时输入的 tensor 永远是 [1, 3, TileSize, TileSize])
                        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(session.InputMetadata.Keys.First(), tileTensor) };
                        
                        lock (session)
                        {
                            using var results = session.Run(inputs);
                            var outputTensor = results.First().AsTensor<float>();
                            WriteTensorToPixels(outputTensor, outputPixels, x * ScaleFactor, y * ScaleFactor, validW, validH, outW, outH, outStride);
                        }

                        processedTiles++;
                        progress?.Report((double)processedTiles / totalTiles * 100);
                    }
                }
            });
            // 4. 将像素写回 Bitmap
            outputBitmap.WritePixels(new Int32Rect(0, 0, outW, outH), outputPixels, outStride, 0);

            outputPixels = null; // 解除大数组引用
            inputPixels = null;  // 解除输入数组引用
            GC.Collect(2, GCCollectionMode.Optimized);
            return outputBitmap;
        }
        private DenseTensor<float> ExtractTileToTensor(byte[] pixels, int startX, int startY, int validW, int validH, int fixedSize, int stride, int fullW, int fullH)
        {
            var tensor = new DenseTensor<float>(new[] { 1, 3, fixedSize, fixedSize });
            Parallel.For(0, validH, y =>
            {
                int srcY = startY + y;
                // 安全检查
                if (srcY >= fullH) return;

                for (int x = 0; x < validW; x++)
                {
                    int srcX = startX + x;
                    if (srcX >= fullW) continue;

                    int offset = srcY * stride + srcX * 4;

                    // BGRA -> RGB Normalized
                    float b = pixels[offset + 0] / 255.0f;
                    float g = pixels[offset + 1] / 255.0f;
                    float r = pixels[offset + 2] / 255.0f;

                    // 填入 Tensor
                    tensor[0, 0, y, x] = r;
                    tensor[0, 1, y, x] = g;
                    tensor[0, 2, y, x] = b;
                }
            });
            return tensor;
        }
        private void WriteTensorToPixels(Tensor<float> tensor, byte[] outPixels, int destX, int destY, int validW, int validH, int fullW, int fullH, int stride)
        {
            // 计算放大后的有效区域
            int validTargetH = validH * ScaleFactor;
            int validTargetW = validW * ScaleFactor;
            Parallel.For(0, validTargetH, y =>
            {
                int finalY = destY + y;
                if (finalY >= fullH) return;

                for (int x = 0; x < validTargetW; x++)
                {
                    int finalX = destX + x;
                    if (finalX >= fullW) continue;

                    // RGB -> BGRA
                    float r = tensor[0, 0, y, x];
                    float g = tensor[0, 1, y, x];
                    float b = tensor[0, 2, y, x];

                    // Clamp 0-1
                    r = Math.Clamp(r, 0f, 1f);
                    g = Math.Clamp(g, 0f, 1f);
                    b = Math.Clamp(b, 0f, 1f);

                    int offset = finalY * stride + finalX * 4;

                    outPixels[offset + 0] = (byte)(b * 255);
                    outPixels[offset + 1] = (byte)(g * 255);
                    outPixels[offset + 2] = (byte)(r * 255);
                    outPixels[offset + 3] = 255;
                }
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
                        resultPixels[offset + 3] = (byte)(originalAlpha * Math.Clamp(alphaVal, 0, 1));
                    }
                }
            });
            return resultPixels;
        }
    }
}
