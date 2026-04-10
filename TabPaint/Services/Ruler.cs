using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TabPaint
{
  
    public enum RulerOrientation { Horizontal, Vertical }

    public class Ruler : FrameworkElement
    {
        private const double SegmentHeight = AppConsts.RulerSegmentHeight; // 短刻度高度
        private const double SegmentHeightMid = AppConsts.RulerSegmentHeightMid; // 中刻度高度
        private const double SegmentHeightLong = AppConsts.RulerSegmentHeightLong; // 长刻度高度
        public static readonly DependencyProperty SelectionStartProperty =
    DependencyProperty.Register("SelectionStart", typeof(double), typeof(Ruler),
        new FrameworkPropertyMetadata(-1.0, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty DrawOffsetProperty =
    DependencyProperty.Register("DrawOffset", typeof(double), typeof(Ruler),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double DrawOffset
        {
            get { return (double)GetValue(DrawOffsetProperty); }
            set { SetValue(DrawOffsetProperty, value); }
        }
        public double SelectionStart
        {
            get { return (double)GetValue(SelectionStartProperty); }
            set { SetValue(SelectionStartProperty, value); }
        }
        public static readonly DependencyProperty SelectionEndProperty =
            DependencyProperty.Register("SelectionEnd", typeof(double), typeof(Ruler),
                new FrameworkPropertyMetadata(-1.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double SelectionEnd
        {
            get { return (double)GetValue(SelectionEndProperty); }
            set { SetValue(SelectionEndProperty, value); }
        }
        public static readonly DependencyProperty ZoomFactorProperty =
            DependencyProperty.Register("ZoomFactor", typeof(double), typeof(Ruler),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));
        public double ZoomFactor
        {
            get { return (double)GetValue(ZoomFactorProperty); }
            set { SetValue(ZoomFactorProperty, value); }
        }
        public static readonly DependencyProperty OriginOffsetProperty =
            DependencyProperty.Register("OriginOffset", typeof(double), typeof(Ruler),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double OriginOffset
        {
            get { return (double)GetValue(OriginOffsetProperty); }
            set { SetValue(OriginOffsetProperty, value); }
        }
        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register("Orientation", typeof(RulerOrientation), typeof(Ruler),
                new FrameworkPropertyMetadata(RulerOrientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsRender));

        public RulerOrientation Orientation
        {
            get { return (RulerOrientation)GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, value); }
        }
        public static readonly DependencyProperty MouseMarkerProperty =
            DependencyProperty.Register("MouseMarker", typeof(double), typeof(Ruler),
                 new FrameworkPropertyMetadata(-100.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double MouseMarker
        {
            get { return (double)GetValue(MouseMarkerProperty); }
            set { SetValue(MouseMarkerProperty, value); }
        }
        private void DrawSelectionHighlight(DrawingContext drawingContext, double zoom)
        {
            // 允许 SelectionStart 为负值：当选区拖出画布左/上边界时，
            // 通过后续屏幕坐标裁剪逻辑（offset/maxVal）来决定可见高亮范围。
            if (SelectionEnd <= SelectionStart) return;
            double screenStart = SelectionStart * zoom + OriginOffset;
            double screenEnd = SelectionEnd * zoom + OriginOffset;

            double offset = DrawOffset; // ★
            double maxVal = (Orientation == RulerOrientation.Horizontal) ? ActualWidth : ActualHeight;
            screenStart = Math.Max(offset, screenStart); // ★ 从偏移处开始
            screenEnd = Math.Min(maxVal, screenEnd);

            if (screenEnd <= screenStart) return;

            Brush accentBrush = (Brush)TryFindResource("SystemAccentBrush") ?? Brushes.DodgerBlue;
            Color accentColor;
            if (accentBrush is SolidColorBrush scb)
                accentColor = scb.Color;
            else
                accentColor = Colors.DodgerBlue;

            Brush highlightBrush = new SolidColorBrush(Color.FromArgb(15, accentColor.R, accentColor.G, accentColor.B));
            if (highlightBrush.CanFreeze) highlightBrush.Freeze();
            Brush edgeBrush = new SolidColorBrush(Color.FromArgb(200, accentColor.R, accentColor.G, accentColor.B));
            if (edgeBrush.CanFreeze) edgeBrush.Freeze();
            Pen edgePen = new Pen(edgeBrush, 1);
            edgePen.Freeze();

            if (Orientation == RulerOrientation.Horizontal)
            {
                drawingContext.DrawRectangle(highlightBrush, null,
                    new Rect(screenStart, 0, screenEnd - screenStart, ActualHeight));
                drawingContext.DrawLine(edgePen, new Point(screenStart + 0.5, 0), new Point(screenStart + 0.5, ActualHeight));
                drawingContext.DrawLine(edgePen, new Point(screenEnd - 0.5, 0), new Point(screenEnd - 0.5, ActualHeight));
            }
            else
            {
                drawingContext.DrawRectangle(highlightBrush, null,
                    new Rect(0, screenStart, ActualWidth, screenEnd - screenStart));
                drawingContext.DrawLine(edgePen, new Point(0, screenStart + 0.5), new Point(ActualWidth, screenStart + 0.5));
                drawingContext.DrawLine(edgePen, new Point(0, screenEnd - 0.5), new Point(ActualWidth, screenEnd - 0.5));
            }
        }
        protected override void OnRender(DrawingContext drawingContext)
        {
            
            base.OnRender(drawingContext);
            Brush textBrush = (Brush)TryFindResource("TextPrimaryBrush") ?? Brushes.Black;
            Brush tickBrush = (Brush)TryFindResource("TextTertiaryBrush") ?? Brushes.Gray;
            Brush borderBrush = (Brush)TryFindResource("BorderMediumBrush") ?? Brushes.Gray;
            Brush bgBrush = (Brush)TryFindResource("GlassBackgroundMediumBrush") ?? Brushes.Transparent; Pen borderPen = new Pen(borderBrush, 1);
            borderPen.Freeze();
            Pen tickPen = new Pen(tickBrush, 1);
            tickPen.Freeze();
            Pen textPen = new Pen(textBrush, 1);
            textPen.Freeze();

            double offset = DrawOffset; // ★ 获取偏移量

            // ★ 背景只画偏移之后的区域
            if (Orientation == RulerOrientation.Horizontal)
            {
                drawingContext.DrawRectangle(bgBrush, null,
                    new Rect(offset, 0, ActualWidth - offset, ActualHeight));
            }
            else
            {
                drawingContext.DrawRectangle(bgBrush, null,
                    new Rect(0, offset, ActualWidth, ActualHeight - offset));
            }

            // ★ 边框线从偏移处开始画
            if (Orientation == RulerOrientation.Horizontal)
                drawingContext.DrawLine(borderPen,
                    new Point(offset, ActualHeight), new Point(ActualWidth, ActualHeight));
            else
                drawingContext.DrawLine(borderPen,
                    new Point(ActualWidth, offset), new Point(ActualWidth, ActualHeight));

            double zoom = ZoomFactor;
            if (zoom <= 0.0001) zoom = 0.0001;
            DrawSelectionHighlight(drawingContext, zoom);

            double desiredPixelSpacing = 80.0;
            double rawStep = desiredPixelSpacing / zoom;
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
            double residual = rawStep / magnitude;

            double logicalStep;
            if (residual > 5) logicalStep = 10 * magnitude;
            else if (residual > 2) logicalStep = 5 * magnitude;
            else if (residual > 1) logicalStep = 2 * magnitude;
            else logicalStep = magnitude;

            double midStep = logicalStep / 2.0;
            double smallStep = logicalStep / 10.0;
            bool drawSmallTicks = (smallStep * zoom) > 4.0;
            double maxVal = (Orientation == RulerOrientation.Horizontal ? ActualWidth : ActualHeight); Typeface typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

            double startValue = -OriginOffset / zoom;
            double startStepIndex = Math.Floor(startValue / smallStep);
            double currentVal = startStepIndex * smallStep;
            int safetyCount = 0;

            double selScreenStart = -1, selScreenEnd = -1;
            // 同样允许负起点，只要区间有效即可参与刻度高亮判断。
            bool hasSelection = SelectionEnd > SelectionStart;
            if (hasSelection)
            {
                selScreenStart = SelectionStart * zoom + OriginOffset;
                selScreenEnd = SelectionEnd * zoom + OriginOffset;
            }
            Brush accentTextBrush = (Brush)TryFindResource("SystemAccentBrush") ?? Brushes.DodgerBlue;
            Pen accentTickPen = new Pen(accentTextBrush, 1);
            accentTickPen.Freeze();

            while (safetyCount++ < 2000)
            {
                double screenPos = (currentVal * zoom) + OriginOffset;

                if (screenPos > maxVal) break;

                // ★ 关键改动：跳过偏移区域内的刻度
                if (screenPos >= offset - 20) // -20 给文字留一点余量
                {
                    bool isMainTick = IsCloseToMultiple(currentVal, logicalStep);
                    bool isMidTick = !isMainTick && IsCloseToMultiple(currentVal, midStep);

                    if (!isMainTick && !isMidTick && !drawSmallTicks)
                    {
                        currentVal += smallStep;
                        continue;
                    }

                    // ★ 刻度线只在偏移之后才画
                    if (screenPos >= offset)
                    {
                        double tickHeight = isMainTick ? SegmentHeightLong : (isMidTick ? SegmentHeightMid : SegmentHeight);
                        bool inSelection = hasSelection && screenPos >= selScreenStart - 0.5 && screenPos <= selScreenEnd + 0.5; Pen currentTickPen = inSelection ? accentTickPen : tickPen;
                        Brush currentTextBrush = inSelection ? accentTextBrush : textBrush;

                        if (Orientation == RulerOrientation.Horizontal)
                        {
                            drawingContext.DrawLine(currentTickPen,
                                new Point(screenPos, ActualHeight - tickHeight),
                                new Point(screenPos, ActualHeight));

                            if (isMainTick)
                            {
                                FormattedText text = new FormattedText(
                                    currentVal.ToString("F0"),
                                    CultureInfo.InvariantCulture,
                                    FlowDirection.LeftToRight,
                                    typeface, 10, currentTextBrush,
                                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                                // ★ 文字不超出偏移区域
                                double textX = screenPos + 2;
                                if (textX + text.Width > offset || textX >= offset)
                                {
                                    drawingContext.DrawText(text, new Point(textX, 0));
                                }
                            }
                        }
                        else // Vertical
                        {
                            drawingContext.DrawLine(currentTickPen,
                                new Point(ActualWidth - tickHeight, screenPos),
                                new Point(ActualWidth, screenPos));

                            if (isMainTick)
                            {
                                FormattedText text = new FormattedText(
                                    currentVal.ToString("F0"),
                                    CultureInfo.InvariantCulture,
                                    FlowDirection.LeftToRight,
                                    typeface, 10, currentTextBrush,
                                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                                double xBase = ActualWidth - text.Height - 2;
                                double yBase = screenPos + (text.Width / 2) + 10;
                                double textTop = yBase - text.Width;
                                double textBottom = yBase;

                                // ★ 文字不超出偏移区域
                                if (textTop >= offset && textBottom <= maxVal)
                                {
                                    drawingContext.PushTransform(new RotateTransform(-90, xBase, yBase));
                                    drawingContext.DrawText(text, new Point(xBase, yBase));
                                    drawingContext.Pop();
                                }
                            }
                        }
                    }
                }
                currentVal += smallStep;
                currentVal = Math.Round(currentVal / smallStep) * smallStep;
            }

            // ★ 鼠标标记也要在偏移之后才画
            Pen markerPen = new Pen(Brushes.Red, 1);
            markerPen.Freeze();

            if (MouseMarker >= offset && MouseMarker <= maxVal)
            {
                if (Orientation == RulerOrientation.Horizontal)
                    drawingContext.DrawLine(markerPen, new Point(MouseMarker, 0), new Point(MouseMarker, ActualHeight));
                else
                    drawingContext.DrawLine(markerPen, new Point(0, MouseMarker), new Point(ActualWidth, MouseMarker));
            }
        }
        private bool IsCloseToMultiple(double value, double step)
        {
            double tolerance = step * 0.001;
            double remainder = Math.Abs(value % step);
            return remainder < tolerance || Math.Abs(remainder - step) < tolerance;
        }

    }
}
