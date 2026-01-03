using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime; // 需要此命名空间
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging; // UWP 图像处理
using Windows.Media.Ocr; // UWP OCR 引擎
using Windows.Storage.Streams;

namespace TabPaint
{
    public class OcrService
    {
        private OcrEngine _ocrEngine;

        public OcrService()
        {
            // 尝试初始化为系统当前语言
            TryInitEngine();
        }

        private void TryInitEngine()
        {
            // 检查系统是否支持 OCR
            if (OcrEngine.IsLanguageSupported(new Windows.Globalization.Language(Language.CurrentInputMethodLanguageTag)))
            {
                _ocrEngine = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language(Language.CurrentInputMethodLanguageTag));
            }
            else
            {
                // 如果当前输入法语言不支持，尝试用英语兜底，或者取第一个可用的语言
                _ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
            }
        }
        private bool IsCjk(char c)
        {
            // 基本汉字范围 (\u4E00-\u9FFF)
            if (c >= 0x4E00 && c <= 0x9FFF) return true;

            // 扩展A区、兼容区等 (可选，视需求添加)
            // 也可以简单粗暴判断 c > 127，虽然不严谨但对区分中英很有效

            // 包含中文标点符号范围 (FF00-FFEF 包含全角标点)
            if (c >= 0xFF00 && c <= 0xFFEF) return true;

            // CJK 标点符号 (3000-303F)
            if (c >= 0x3000 && c <= 0x303F) return true;

            return false;
        }
        public async Task<string> RecognizeTextAsync(BitmapSource wpfBitmap)
        {
            if (_ocrEngine == null)
            {
                // 再次尝试初始化，或者抛出异常提示用户安装语言包
                TryInitEngine();
                if (_ocrEngine == null) return "错误：未找到支持 OCR 的语言包，请在 Windows 设置中添加。";
            }

            try
            {
                // 1. 将 WPF BitmapSource 转换为 UWP SoftwareBitmap
                using (var ms = new MemoryStream())
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(wpfBitmap));
                    encoder.Save(ms);
                    ms.Seek(0, SeekOrigin.Begin);

                    // 使用 AsRandomAccessStream (需要引用 Windows.Storage.Streams)
                    var randomAccessStream = ms.AsRandomAccessStream();
                    var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(randomAccessStream);

                    // 获取 SoftwareBitmap
                    using (var softwareBitmap = await decoder.GetSoftwareBitmapAsync())
                    {
                        // 2. 执行 OCR 识别
                        var ocrResult = await _ocrEngine.RecognizeAsync(softwareBitmap);

                        // 3. 拼接结果
                        StringBuilder sb = new StringBuilder();
                        foreach (var line in ocrResult.Lines)
                        {
                            for (int i = 0; i < line.Words.Count; i++)
                            {
                                var currentWord = line.Words[i];
                                sb.Append(currentWord.Text);

                                // 如果不是这行的最后一个词，需要判断是否加空格
                                if (i < line.Words.Count - 1)
                                {
                                    var nextWord = line.Words[i + 1];

                                    // 核心逻辑：
                                    // 只有当“当前词”结尾是英文/数字 AND “下一个词”开头是英文/数字时，才加空格
                                    // 只要有一方是中文（CJK字符），就不加空格
                                    if (!IsCjk(currentWord.Text.Last()) && !IsCjk(nextWord.Text.First()))
                                    {
                                        sb.Append(" ");
                                    }
                                }
                            }
                            sb.AppendLine(); // 每一行识别完换行
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
