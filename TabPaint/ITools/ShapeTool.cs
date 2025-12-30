using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TabPaint;
using static TabPaint.MainWindow;

public class ShapeTool : ToolBase
{
    public override string Name => "Shape";
    public override System.Windows.Input.Cursor Cursor => _isManipulating ? null : System.Windows.Input.Cursors.Cross;

    public enum ShapeType { Rectangle, Ellipse, Line, RoundedRectangle, Arrow }
    private ShapeType _currentShapeType = ShapeType.Rectangle;

    private Point _startPoint;
    private bool _isDrawing;
    private bool _isManipulating = false;
    private System.Windows.Shapes.Shape _previewShape;

    public void SetShapeType(ShapeType type)
    {
        _currentShapeType = type;
    }

    private SelectTool GetSelectTool()
    {
        var mw = (MainWindow)Application.Current.MainWindow;
        return mw._tools.Select as SelectTool;
    }

    public override void OnPointerDown(ToolContext ctx, Point viewPos)
    {
        var selectTool = GetSelectTool();
        var px = ctx.ToPixel(viewPos);

        // --- 逻辑分支 A: 操控模式 ---
        if (_isManipulating && selectTool != null)
        {
            // 检查 SelectTool 是否还有有效数据（可能被 Delete 删掉了）
            if (!selectTool.HasActiveSelection)
            {
                _isManipulating = false;
                goto DrawingLogic; // 跳转到绘制逻辑
            }

            bool hitHandle = selectTool.HitTestHandle(px, selectTool._selectionRect) != SelectTool.ResizeAnchor.None;
            bool hitContent = selectTool.IsPointInSelection(px);

            if (hitHandle || hitContent)
            {
                // 转发事件给 SelectTool
                selectTool.OnPointerDown(ctx, viewPos);
                return;
            }
            else
            {
                selectTool.CommitSelection(ctx, true);
                selectTool.Cleanup(ctx);
                _isManipulating = false;
            }
        }

    DrawingLogic:
        // --- 逻辑分支 B: 绘制新图形 ---
        _startPoint = ctx.ToPixel(viewPos);
        _isDrawing = true;
        ctx.CapturePointer();

        // 初始化预览形状
        switch (_currentShapeType)
        {
            case ShapeType.Rectangle:
                _previewShape = new System.Windows.Shapes.Rectangle();
                break;
            case ShapeType.RoundedRectangle:
                _previewShape = new System.Windows.Shapes.Rectangle { RadiusX = 20, RadiusY = 20 };
                break;
            case ShapeType.Ellipse:
                _previewShape = new System.Windows.Shapes.Ellipse();
                break;
            case ShapeType.Line:
                _previewShape = new System.Windows.Shapes.Line();
                break;
            case ShapeType.Arrow:
                _previewShape = new System.Windows.Shapes.Path();
                break;
        }

        _previewShape.Stroke = new SolidColorBrush(ctx.PenColor);
        _previewShape.StrokeThickness = ctx.PenThickness;
        _previewShape.Fill = null;

        // 初始位置设置（线和箭头不需要设置 Left/Top）
        if (_currentShapeType != ShapeType.Line && _currentShapeType != ShapeType.Arrow)
        {
            Canvas.SetLeft(_previewShape, _startPoint.X);
            Canvas.SetTop(_previewShape, _startPoint.Y);
        }

        ctx.EditorOverlay.Children.Add(_previewShape);
    }

    public void GiveUpSelection(ToolContext ctx)
    {
        if (ctx == null) return;
    
        GetSelectTool()?.CommitSelection(ctx,true);
        GetSelectTool()?.Cleanup(ctx);
       // ctx.Undo.Undo();

    }

    public override void OnPointerMove(ToolContext ctx, Point viewPos)
    {
        if (_isManipulating)
        {
            GetSelectTool()?.OnPointerMove(ctx, viewPos);
            return;
        }

        if (!_isDrawing || _previewShape == null) return;

        var current = ctx.ToPixel(viewPos);
        UpdatePreviewShape(_startPoint, current, ctx.PenThickness);
    }

    public override void OnPointerUp(ToolContext ctx, Point viewPos)
    {
        if (_isManipulating)
        {
            GetSelectTool()?.OnPointerUp(ctx, viewPos);
            return;
        }

        if (!_isDrawing) return;

        var endPoint = ctx.ToPixel(viewPos);
        _isDrawing = false;
        ctx.ReleasePointerCapture();

        if (_previewShape != null)
        {
            ctx.EditorOverlay.Children.Remove(_previewShape);
            _previewShape = null;
        }

        // 计算逻辑包围盒
        var rect = MakeRect(_startPoint, endPoint);
        if (rect.Width <= 1 || rect.Height <= 1) return;

        // --- 修复核心 A: 安全的 Padding 计算 ---
        // 为了防止笔触太粗被裁切，Padding 至少要是笔触的一半，这里给全尺寸+2px余量更安全
        double padding = ctx.PenThickness + 2;

        // 生成位图
        var shapeBitmap = RenderShapeToBitmap(_startPoint, endPoint, rect, ctx.PenColor, ctx.PenThickness, padding, ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY);

        var selectTool = GetSelectTool();
        if (selectTool != null)
        {
            // 1. 将生成的图片插入选区 (SelectTool 会初始化 _selectionRect 为 0,0,imgW,imgH)
            selectTool.InsertImageAsSelection(ctx, shapeBitmap,false);

            // 2. 计算 Bitmap 在画布上的真实左上角
            // 逻辑：用户画的框(rect) - 边距(padding) = 图片左上角
            int realX = (int)(rect.X - padding);
            int realY = (int)(rect.Y - padding);

            // 3. 强制同步 SelectTool 的数据状态
            // 这一步非常关键：必须用 shapeBitmap 的实际物理尺寸，而不是 rect 的尺寸
            selectTool._selectionRect = new Int32Rect(realX, realY, shapeBitmap.PixelWidth, shapeBitmap.PixelHeight);
            selectTool._originalRect = selectTool._selectionRect;

            // 4. --- 修复核心 B: 强制同步 UI 尺寸 ---
            // 很多时候裁剪是因为 Image 控件的 Width/Height 没更新
            double zoom = ((MainWindow)System.Windows.Application.Current.MainWindow).zoomscale;
            double scaleX = 1.0; // 这里的 Scale 指的是相对于原图的缩放，刚生成时是 1:1
            double scaleY = 1.0;

            // 更新 Image 控件的显示大小 (考虑画布缩放 ViewElement 的尺寸)
            // 注意：ToolContext 里没有直接暴露 zoom，通常通过 ViewElement / Surface 比例计算
            double uiScaleX = ctx.ViewElement.ActualWidth / ctx.Surface.Bitmap.PixelWidth;
            double uiScaleY = ctx.ViewElement.ActualHeight / ctx.Surface.Bitmap.PixelHeight;

            ctx.SelectionPreview.Width = selectTool._selectionRect.Width * uiScaleX;
            ctx.SelectionPreview.Height = selectTool._selectionRect.Height * uiScaleY;

            // 5. 初始化 RenderTransform (包含 ScaleTransform 以便后续 SelectTool 能缩放)
            var tg = new TransformGroup();
            tg.Children.Add(new ScaleTransform(1, 1));
            tg.Children.Add(new TranslateTransform(realX, realY));
            ctx.SelectionPreview.RenderTransform = tg;

            double canvasW = ctx.Surface.Bitmap.PixelWidth;
            double canvasH = ctx.Surface.Bitmap.PixelHeight;

            // 这里的 Clip 是相对于 SelectionPreview 控件自身的坐标系 (0,0 是图片左上角)
            // 图片被放在 (realX, realY)，所以画布左上角相对于图片就是 (-realX, -realY)
            Rect clipRect = new Rect(
                -realX,
                -realY,
                canvasW,
                canvasH
            );
            ctx.SelectionPreview.Clip = new RectangleGeometry(clipRect);

            // 6. 重新绘制虚线框
            selectTool.RefreshOverlay(ctx);

            _isManipulating = true;
        }
    }

    public override void OnKeyDown(ToolContext ctx, KeyEventArgs e)
    {
        if (_isManipulating)
        {
            var st = GetSelectTool();
            if (st != null)
            {
                st.OnKeyDown(ctx, e);
                // 检查按键后状态：如果选区没了（被剪切或删除），退出操控模式
                if (!st.HasActiveSelection)
                {
                    _isManipulating = false;
                }
                return;
            }
        }
        base.OnKeyDown(ctx, e);
    }

    private void UpdatePreviewShape(Point start, Point end, double thickness)
    {
        double x = Math.Min(start.X, end.X);
        double y = Math.Min(start.Y, end.Y);
        double w = Math.Abs(end.X - start.X);
        double h = Math.Abs(end.Y - start.Y);

        if (_currentShapeType == ShapeType.Line)
        {
            var line = (System.Windows.Shapes.Line)_previewShape;
            line.X1 = start.X; line.Y1 = start.Y;
            line.X2 = end.X; line.Y2 = end.Y;
        }
        else if (_currentShapeType == ShapeType.Arrow)
        {
            var path = (System.Windows.Shapes.Path)_previewShape;
            path.Data = BuildArrowGeometry(start, end, thickness * 3);
        }
        else
        {
            Canvas.SetLeft(_previewShape, x);
            Canvas.SetTop(_previewShape, y);
            _previewShape.Width = w;
            _previewShape.Height = h;
        }
    }

    private BitmapSource RenderShapeToBitmap(Point globalStart, Point globalEnd, Int32Rect rect, Color color, double thickness, double padding, double dpiX, double dpiY)
    {
        // 位图的总像素尺寸 = 形状宽 + 左边距 + 右边距
        int pixelWidth = rect.Width + (int)Math.Ceiling(padding * 2);
        int pixelHeight = rect.Height + (int)Math.Ceiling(padding * 2);

        // 确保尺寸有效
        if (pixelWidth <= 0) pixelWidth = 1;
        if (pixelHeight <= 0) pixelHeight = 1;

        // 计算局部坐标：
        // 如果 globalStart 在左上角，它相对于 rect.X 是 0，所以 localStart = padding
        // 加上 padding 是为了把画的内容移到图片中间，防止边缘被切
        Point localStart = new Point(globalStart.X - rect.X + padding, globalStart.Y - rect.Y + padding);
        Point localEnd = new Point(globalEnd.X - rect.X + padding, globalEnd.Y - rect.Y + padding);

        DrawingVisual drawingVisual = new DrawingVisual();
        using (DrawingContext dc = drawingVisual.RenderOpen())
        {
            Pen pen = new Pen(new SolidColorBrush(color), thickness);
            // 设置线帽，防止直角线端点看起来也是切掉的
            pen.StartLineCap = PenLineCap.Round;
            pen.EndLineCap = PenLineCap.Round;
            pen.LineJoin = PenLineJoin.Round;
            pen.Freeze();

            // 矩形绘制逻辑：在 padding 偏移处绘制 rect 尺寸的框
            // 这样笔触的一半 (thickness/2) 会向外延伸，但因为我们 padding > thickness/2，所以它是安全的
            Rect drawRect = new Rect(padding, padding, rect.Width, rect.Height);

            switch (_currentShapeType)
            {
                case ShapeType.Rectangle:
                    dc.DrawRectangle(null, pen, drawRect);
                    break;
                case ShapeType.RoundedRectangle:
                    dc.DrawRoundedRectangle(null, pen, drawRect, 20, 20);
                    break;
                case ShapeType.Ellipse:
                    // 椭圆圆心
                    dc.DrawEllipse(null, pen,
                        new Point(padding + rect.Width / 2.0, padding + rect.Height / 2.0),
                        rect.Width / 2.0, rect.Height / 2.0);
                    break;
                case ShapeType.Line:
                    dc.DrawLine(pen, localStart, localEnd);
                    break;
                case ShapeType.Arrow:
                    var arrowGeo = BuildArrowGeometry(localStart, localEnd, thickness * 3);
                    dc.DrawGeometry(null, pen, arrowGeo);
                    break;
            }
        }

        RenderTargetBitmap bmp = new RenderTargetBitmap(pixelWidth, pixelHeight, dpiX, dpiY, PixelFormats.Pbgra32);
        bmp.Render(drawingVisual);
        bmp.Freeze();
        return bmp;
    }

    private Geometry BuildArrowGeometry(Point start, Point end, double headSize)
    {
        Vector vec = end - start;
        // 防止长度为0崩溃
        if (vec.LengthSquared < 0.001) vec = new Vector(0, 1);

        vec.Normalize();
        if (Double.IsNaN(vec.X)) vec = new Vector(1, 0);

        Vector backVec = -vec;
        Matrix m1 = Matrix.Identity; m1.Rotate(30);
        Matrix m2 = Matrix.Identity; m2.Rotate(-30);

        Vector wing1 = m1.Transform(backVec) * headSize;
        Vector wing2 = m2.Transform(backVec) * headSize;

        Point p1 = end + wing1;
        Point p2 = end + wing2;

        StreamGeometry geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.LineTo(end, true, false);
            ctx.LineTo(p1, true, false);
            ctx.BeginFigure(end, false, false);
            ctx.LineTo(p2, true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static Int32Rect MakeRect(Point p1, Point p2)
    {
        int x = (int)Math.Min(p1.X, p2.X);
        int y = (int)Math.Min(p1.Y, p2.Y);
        int w = Math.Abs((int)p1.X - (int)p2.X);
        int h = Math.Abs((int)p1.Y - (int)p2.Y);
        return new Int32Rect(x, y, w, h);
    }
}
