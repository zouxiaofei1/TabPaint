using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using TabPaint.Services;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Diagnostics;

namespace TabPaint
{
    public class OcrService
    {
        private OcrEngine _ocrEngine;
        private static readonly object _paddleClientLock = new object();
        private static PaddleOcrClient? _paddleClient = new PaddleOcrClient();

        private static PaddleOcrClient GetPaddleClient()
        {
            lock (_paddleClientLock)
            {
                _paddleClient ??= new PaddleOcrClient();
                return _paddleClient;
            }
        }

        public static void ReleasePaddleRuntime()
        {
            lock (_paddleClientLock)
            {
                try
                {
                    _paddleClient?.Dispose();
                }
                catch
                {
                    // ignore dispose failures to keep uninstall flow robust
                }
                _paddleClient = null;
            }
        }

        public OcrService()
        {
            InitBestEngine();
        }
        private string? _initError;
        private void InitBestEngine()
        {
            try
            {
                _ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();

                if (_ocrEngine == null)
                {
                    _ocrEngine = OcrEngine.TryCreateFromLanguage(new Language(Language.CurrentInputMethodLanguageTag));
                }

                if (_ocrEngine == null)
                {
                    var firstLang = OcrEngine.AvailableRecognizerLanguages.FirstOrDefault();
                    if (firstLang != null) _ocrEngine = OcrEngine.TryCreateFromLanguage(firstLang);
                }
            }
            catch (Exception ex)
            {
                _initError = ex.Message;
                // 特别处理 DLL 缺失的情况
                if (ex is FileNotFoundException || ex is DllNotFoundException || ex.Message.Contains("Microsoft.Windows.SDK.NET"))
                {
                    _initError = "Missing Windows SDK Runtime (Microsoft.Windows.SDK.NET.dll).";
                }
            }
        }
        private bool IsCjk(char c)
        {// 包含中日韩字符范围
            if (c >= 0x4E00 && c <= 0x9FFF) return true;
            if (c >= 0xFF00 && c <= 0xFFEF) return true;
            if (c >= 0x3000 && c <= 0x303F) return true;
            return false;
        }
        private (BitmapSource Processed, double Scale) PreprocessImage(BitmapSource source)
        {
            double scale = 1.0;
            if (source.PixelHeight < 400 || source.PixelWidth < 400) scale = 2.0; 
            else scale = 1.5;
            if (scale > 1.0) return (new TransformedBitmap(source, new ScaleTransform(scale, scale)), scale);
            return (source, 1.0);
        }

        public async Task<string> RecognizeTextAsync(BitmapSource wpfBitmap)
        {
            var detailed = await RecognizeDetailedAsync(wpfBitmap);
            return detailed?.FullText ?? string.Empty;
        }

        public async Task<OcrRecognizeResult?> RecognizeDetailedAsync(BitmapSource wpfBitmap)
        {
            var preprocess = PreprocessImage(wpfBitmap);
            var processedBitmap = preprocess.Processed;
            double scale = preprocess.Scale;
            var safeBitmap = processedBitmap;

            // BitmapSource is thread-affine unless frozen; OCR calls can resume on worker threads.
            if (!safeBitmap.IsFrozen)
            {
                safeBitmap = safeBitmap.Clone();
                if (safeBitmap.CanFreeze) safeBitmap.Freeze();
            }

            var settings = SettingsManager.Instance.Current;
            if (settings?.EnableAiOcr == true)
            {
                try
                {
                    OcrRecognizeResult? paddleResult = await GetPaddleClient().RecognizeDetailedAsync(safeBitmap);

                    if (!string.IsNullOrWhiteSpace(paddleResult?.FullText))
                    {
                        return NormalizeResultScale(paddleResult, scale);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("[PyOCR] PaddleOCR recognize failed, fallback to Windows OCR.", ex);
                }
            }

            var windowsResult = await RecognizeWithWindowsOcrAsync(safeBitmap);
            return NormalizeResultScale(windowsResult, scale);
        }

        private static OcrRecognizeResult? NormalizeResultScale(OcrRecognizeResult? result, double scale)
        {
            if (result == null || scale <= 1.0001) return result;

            var lines = new List<OcrLineResult>(result.Lines.Count);
            foreach (var line in result.Lines)
            {
                Rect? rect = line.Rect;
                if (rect.HasValue)
                {
                    var r = rect.Value;
                    rect = new Rect(r.X / scale, r.Y / scale, r.Width / scale, r.Height / scale);
                }
                lines.Add(new OcrLineResult(line.Text, rect));
            }
            return new OcrRecognizeResult(result.FullText, lines);
        }

        private async Task<OcrRecognizeResult?> RecognizeWithWindowsOcrAsync(BitmapSource processedBitmap)
        {
            if (_ocrEngine == null)
            {
                InitBestEngine();
                if (_ocrEngine == null)
                {
                    if (!string.IsNullOrEmpty(_initError)) return new OcrRecognizeResult(_initError, new List<OcrLineResult>());
                    return new OcrRecognizeResult(LocalizationManager.GetString("L_OCR_Error_NoLangPack"), new List<OcrLineResult>());
                }
            }

            try
            {
                using (var ms = new MemoryStream())
                {
                    var encoder = new BmpBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(processedBitmap));
                    encoder.Save(ms);
                    ms.Seek(0, SeekOrigin.Begin);

                    var randomAccessStream = ms.AsRandomAccessStream();
                    var decoder = await global::Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(randomAccessStream);
                    using (var softwareBitmap = await decoder.GetSoftwareBitmapAsync())
                    {
                        var ocrResult = await _ocrEngine.RecognizeAsync(softwareBitmap);

                        if (ocrResult.Lines.Count == 0) return null;
                        StringBuilder sb = new StringBuilder();
                        var lines = new List<OcrLineResult>();
                        foreach (var line in ocrResult.Lines)
                        {
                            string lineText = string.Empty;
                            for (int i = 0; i < line.Words.Count; i++)
                            {
                                var currentWord = line.Words[i];
                                sb.Append(currentWord.Text);
                                lineText += currentWord.Text;
                                if (i < line.Words.Count - 1)
                                {
                                    var nextWord = line.Words[i + 1];
                                    bool currentIsCjk = currentWord.Text.Any(IsCjk);
                                    bool nextIsCjk = nextWord.Text.Any(IsCjk);
                                    if (!currentIsCjk && !nextIsCjk)
                                    {
                                        sb.Append(" ");
                                        lineText += " ";
                                    }
                                }
                            }

                            Rect? lineRect = null;
                            try
                            {
                                if (line.Words.Count > 0)
                                {
                                    double minX = double.MaxValue;
                                    double minY = double.MaxValue;
                                    double maxX = double.MinValue;
                                    double maxY = double.MinValue;

                                    foreach (var word in line.Words)
                                    {
                                        var r = word.BoundingRect;
                                        minX = Math.Min(minX, r.X);
                                        minY = Math.Min(minY, r.Y);
                                        maxX = Math.Max(maxX, r.X + r.Width);
                                        maxY = Math.Max(maxY, r.Y + r.Height);
                                    }

                                    if (maxX > minX && maxY > minY)
                                    {
                                        lineRect = new Rect(minX, minY, maxX - minX, maxY - minY);
                                    }
                                }
                            }
                            catch
                            {
                                lineRect = null;
                            }

                            if (!string.IsNullOrWhiteSpace(lineText))
                            {
                                lines.Add(new OcrLineResult(lineText.Trim(), lineRect));
                            }
                            sb.AppendLine();
                        }

                        string fullText = sb.ToString().Trim();
                        if (string.IsNullOrWhiteSpace(fullText)) return null;
                        return new OcrRecognizeResult(fullText, lines);
                    }
                }
            }
            catch (Exception ex)
            {
                return new OcrRecognizeResult(string.Format(LocalizationManager.GetString("L_OCR_Failed_Prefix"), ex.Message), new List<OcrLineResult>());
            }
        }
    }
}
