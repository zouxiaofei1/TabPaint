using System.Collections.Generic;
using System.Windows;

namespace TabPaint
{
    public sealed class OcrLineResult
    {
        public OcrLineResult(string text, Rect? rect)
        {
            Text = text ?? string.Empty;
            Rect = rect;
        }

        public string Text { get; }
        public Rect? Rect { get; }
    }

    public sealed class OcrRecognizeResult
    {
        public OcrRecognizeResult(string fullText, IReadOnlyList<OcrLineResult> lines)
        {
            FullText = fullText ?? string.Empty;
            Lines = lines ?? new List<OcrLineResult>();
        }

        public string FullText { get; }
        public IReadOnlyList<OcrLineResult> Lines { get; }
    }
}