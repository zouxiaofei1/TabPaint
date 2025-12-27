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
    public override System.Windows.Input.Cursor Cursor => _isManipulating ? null : System.Windows.Input.Cursors.Cross; // 操控时交由SelectTool决定光标

    public enum ShapeType { Rectangle, Ellipse, Line, RoundedRectangle, Arrow }
    private ShapeType _currentShapeType = ShapeType.Rectangle;

    private Point _startPoint;
    private bool _isDrawing;

    // 新增：状态标志，是否正在操控刚刚画好的图形
    private bool _isManipulating = false;

    // 预览用的 UI 元素
    private System.Windows.Shapes.Shape _previewShape;

    public void SetShapeType(ShapeType type)
    {
        _currentShapeType = type;
    }

    // 辅助方法：获取 SelectTool 实例
    private SelectTool GetSelectTool()
    {
        var mw = (MainWindow)Application.Current.MainWindow;
        return mw._tools.Select as SelectTool;
    }

    public override void OnPointerDown(ToolContext ctx, Point viewPos)
    {
        var selectTool = GetSelectTool();
        var px = ctx.ToPixel(viewPos);

        // --- 逻辑分支 A: 如果当前正在操控上一个图形 ---
        if (_isManipulating && selectTool != null)
        {
            // 1. 检查点击位置是否在选区内或句柄上
            bool hitHandle = selectTool.HitTestHandle(px, selectTool._selectionRect) != SelectTool.ResizeAnchor.None;
            bool hitContent = selectTool.IsPointInSelection(px);

            if (hitHandle || hitContent)
            {
                // 如果点中了图形，将事件转发给 SelectTool 处理拖拽/缩放
                selectTool.OnPointerDown(ctx, viewPos);
                return;
            }
            else
            {
                // 如果点在了空白处 -> 提交上一个图形，结束操控状态
                selectTool.CommitSelection(ctx);
                selectTool.CleanUp(ctx); // 清理虚线框
                _isManipulating = false;
                // 继续向下执行，进入逻辑分支 B (开始绘制新图形)
            }
        }

        // --- 逻辑分支 B: 开始绘制新图形 ---
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

        if (_currentShapeType != ShapeType.Line && _currentShapeType != ShapeType.Arrow)
        {
            Canvas.SetLeft(_previewShape, _startPoint.X);
            Canvas.SetTop(_previewShape, _startPoint.Y);
        }

        ctx.EditorOverlay.Children.Add(_previewShape);
    }

    public override void OnPointerMove(ToolContext ctx, Point viewPos)
    {
        // 如果正在操控，转发给 SelectTool
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
        // 如果正在操控，转发给 SelectTool
        if (_isManipulating)
        {
            GetSelectTool()?.OnPointerUp(ctx, viewPos);
            return;
        }

        if (!_isDrawing) return;

        var endPoint = ctx.ToPixel(viewPos);
        _isDrawing = false;
        ctx.ReleasePointerCapture();

        ctx.EditorOverlay.Children.Remove(_previewShape);
        _previewShape = null;

        // 计算包围盒
        var rect = MakeRect(_startPoint, endPoint);
        // 防止误触（太小的图形不生成）
        if (rect.Width <= 2 || rect.Height <= 2) return;

        // 渲染位图
        var shapeBitmap = RenderShapeToBitmap(_startPoint, endPoint, rect, ctx.PenColor, ctx.PenThickness, ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY);

        var selectTool = GetSelectTool();
        if (selectTool != null)
        {
            // === 核心修改 ===
            // 不再调用 router.SetTool(selectTool)

            // 1. 让 SelectTool 把图片加载进选区系统
            selectTool.InsertImageAsSelection(ctx, shapeBitmap);

            // 2. 手动修正 SelectTool 的位置数据，使其与刚才画的位置完全重合
            selectTool._selectionRect.X = rect.X;
            selectTool._selectionRect.Y = rect.Y;
            selectTool._originalRect = selectTool._selectionRect; // 这一点很重要，确保缩放比例正确

            // 3. 更新 SelectTool 的预览图位置
            var tg = new TransformGroup();
            tg.Children.Add(new TranslateTransform(rect.X, rect.Y));
            ctx.SelectionPreview.RenderTransform = tg;
            Canvas.SetLeft(ctx.SelectionPreview, 0);
            Canvas.SetTop(ctx.SelectionPreview, 0);

            // 4. 让 SelectTool 绘制虚线框和手柄
            selectTool.RefreshOverlay(ctx);

            // 5. 标记为“操控模式”，接下来的鼠标事件将转发给 SelectTool
            _isManipulating = true;
        }
    }

    public override void OnKeyDown(ToolContext ctx, KeyEventArgs e)
    {
        // 支持在 ShapeTool 下直接按 Delete 删除或者 Ctrl+C/V
        if (_isManipulating)
        {
            var st = GetSelectTool();
            if (st != null)
            {
                st.OnKeyDown(ctx, e);
                // 如果 SelectTool 执行了剪切或删除，应该退出操控模式
                if (st._selectionData == null)
                {
                    _isManipulating = false;
                }
                return;
            }
        }
        base.OnKeyDown(ctx, e);
    }

    // 切换工具时（例如用户手动点了其他工具），必须强制提交
    //public override void Deactivate(ToolContext ctx)
    //{
    //    if (_isManipulating)
    //    {
    //        var st = GetSelectTool();
    //        if (st != null)
    //        {
    //            st.CommitSelection(ctx);
    //            st.CleanUp(ctx);
    //        }
    //        _isManipulating = false;
    //    }
    //    base.Deactivate(ctx);
    //}

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

    private BitmapSource RenderShapeToBitmap(Point globalStart, Point globalEnd, Int32Rect rect, Color color, double thickness, double dpiX, double dpiY)
    {
        double padding = thickness;
        int pixelWidth = rect.Width + (int)(padding * 2);
        int pixelHeight = rect.Height + (int)(padding * 2);

        if (pixelWidth <= 0) pixelWidth = 1;
        if (pixelHeight <= 0) pixelHeight = 1;

        Point localStart = new Point(globalStart.X - rect.X + padding, globalStart.Y - rect.Y + padding);
        Point localEnd = new Point(globalEnd.X - rect.X + padding, globalEnd.Y - rect.Y + padding);

        DrawingVisual drawingVisual = new DrawingVisual();
        using (DrawingContext dc = drawingVisual.RenderOpen())
        {
            Pen pen = new Pen(new SolidColorBrush(color), thickness);
            pen.Freeze();

            Rect insetRect = new Rect(padding + thickness / 2, padding + thickness / 2,
                                      Math.Max(0, rect.Width - thickness), Math.Max(0, rect.Height - thickness));

            switch (_currentShapeType)
            {
                case ShapeType.Rectangle:
                    dc.DrawRectangle(null, pen, insetRect);
                    break;
                case ShapeType.RoundedRectangle:
                    dc.DrawRoundedRectangle(null, pen, insetRect, 20, 20);
                    break;
                case ShapeType.Ellipse:
                    dc.DrawEllipse(null, pen,
                        new Point(insetRect.X + insetRect.Width / 2, insetRect.Y + insetRect.Height / 2),
                        insetRect.Width / 2, insetRect.Height / 2);
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
