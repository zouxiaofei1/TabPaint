using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using System.Runtime.InteropServices.WindowsRuntime;

namespace TabPaint
{
    public class OcrService
    {
        private OcrEngine _ocrEngine;

        public OcrService()
        {
            InitBestEngine();
        }

        private void InitBestEngine()
        {
            // 策略优化：
            // 1. 优先尝试使用用户配置的首选语言（通常是系统显示语言）
            // 2. 如果失败，尝试使用当前输入法语言
            // 3. 最后尝试使用 OCR 引擎支持的第一个可用语言
            
            // Windows截图工具通常默认使用 UserProfileLanguages
            _ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();

            if (_ocrEngine == null)
            {
                // 尝试当前输入法
                try 
                {
                    _ocrEngine = OcrEngine.TryCreateFromLanguage(new Language(Language.CurrentInputMethodLanguageTag));
                }
                catch { }
            }

            if (_ocrEngine == null)
            {
                // 最后的保底，随便找一个支持的
                var firstLang = OcrEngine.AvailableRecognizerLanguages.FirstOrDefault();
                if (firstLang != null)
                {
                    _ocrEngine = OcrEngine.TryCreateFromLanguage(firstLang);
                }
            }
        }

        private bool IsCjk(char c)
        {
            // 包含中日韩字符范围
            if (c >= 0x4E00 && c <= 0x9FFF) return true;
            if (c >= 0xFF00 && c <= 0xFFEF) return true;
            if (c >= 0x3000 && c <= 0x303F) return true;
            return false;
        }

        // 辅助方法：放大图片以提高识别率
        private BitmapSource PreprocessImage(BitmapSource source)
        {
            // 如果图片太小，OCR 效果极差。
            // 这里简单的逻辑是：如果宽或高较小，强制放大 2 倍。
            // 也可以更激进，统一放大 1.5x - 2.0x，这对小字体识别提升巨大。
            
            double scale = 1.0;
            if (source.PixelHeight < 400 || source.PixelWidth < 400)
            {
                scale = 2.0; 
            }
            else
            {
                // 即使是大图，稍微放大一点通常也有助于提升清晰度
                scale = 1.5;
            }

            if (scale > 1.0)
            {
                return new TransformedBitmap(source, new ScaleTransform(scale, scale));
            }
            return source;
        }

        public async Task<string> RecognizeTextAsync(BitmapSource wpfBitmap)
        {
            if (_ocrEngine == null)
            {
                InitBestEngine();
                if (_ocrEngine == null) return "错误：未安装 OCR 语言包，请在 Windows 设置 -> 时间和语言 -> 语言中添加语言包。";
            }

            try
            {
                // 1. 预处理：放大图片
                var processedBitmap = PreprocessImage(wpfBitmap);

                using (var ms = new MemoryStream())
                {
                    // 2. 优化：使用 BMP 编码器，速度比 PNG 快，且无压缩损耗
                    var encoder = new BmpBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(processedBitmap));
                    encoder.Save(ms);
                    ms.Seek(0, SeekOrigin.Begin);

                    var randomAccessStream = ms.AsRandomAccessStream();
                    var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(randomAccessStream);

                    // 获取 SoftwareBitmap
                    using (var softwareBitmap = await decoder.GetSoftwareBitmapAsync())
                    {
                        // 3. 执行 OCR 识别
                        var ocrResult = await _ocrEngine.RecognizeAsync(softwareBitmap);

                        if (ocrResult.Lines.Count == 0) return null;

                        // 4. 拼接结果
                        StringBuilder sb = new StringBuilder();
                        foreach (var line in ocrResult.Lines)
                        {
                            for (int i = 0; i < line.Words.Count; i++)
                            {
                                var currentWord = line.Words[i];
                                sb.Append(currentWord.Text);

                                // 智能空格处理
                                if (i < line.Words.Count - 1)
                                {
                                    var nextWord = line.Words[i + 1];
                                    // 只有当前字和下一个字都不是CJK字符时，才加空格
                                    // (比如 "Hello" 和 "World" 之间加，"你" 和 "好" 之间不加)
                                    bool currentIsCjk = currentWord.Text.Any(IsCjk);
                                    bool nextIsCjk = nextWord.Text.Any(IsCjk);

                                    if (!currentIsCjk && !nextIsCjk)
                                    {
                                        sb.Append(" ");
                                    }
                                }
                            }
                            sb.AppendLine(); 
                        }
                        return sb.ToString().Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                return $"OCR 识别失败: {ex.Message}";
            }
        }
    }
}
