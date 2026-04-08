using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TabPaint;
using static TabPaint.MainWindow;

public partial class ShapeTool : ToolBase
{
    public override string Name => "Shape";
    public override System.Windows.Input.Cursor Cursor => _isEditing ? System.Windows.Input.Cursors.Arrow : System.Windows.Input.Cursors.Cross;
    public enum ShapeType { Rectangle, Ellipse, Line, RoundedRectangle, Arrow, Triangle, Diamond, Pentagon, Star, Bubble }

    public ShapeType _currentShapeType = ShapeType.Rectangle;
    public ShapeType CurrentShapeType => _currentShapeType;
    private Point _startPoint;
    private bool _isDrawing;
    private bool _isEditing = false;
    private Rect _editingRect;
    private double _rotationAngle = 0;
    private System.Windows.Shapes.Shape _previewShape;
    private Point _arrowStartPoint;
    private Point _arrowEndPoint;
    private bool _hasArrowEndpoints;
    private bool _lineUsesNegativeDiagonal;
    
    // 专业模式相关
    private enum ManipulationAnchor { None, N, S, W, E, NW, NE, SW, SE, Move, Rotate }
    private ManipulationAnchor _activeAnchor = ManipulationAnchor.None;
    private Point _lastMousePos;
    private Point _startMouse;
    private double _startW, _startH, _startX, _startY;
    private readonly List<FrameworkElement> _handleVisuals = new List<FrameworkElement>();
    private const double HandleSize = 8;
    private const double HitRange = 12;
    private const double RotateHandleOffset = 30;

    public override void SetCursor(ToolContext ctx)
    {
        System.Windows.Input.Mouse.OverrideCursor = null;
        if (ctx.ViewElement != null) ctx.ViewElement.Cursor = this.Cursor;
    }

    public void SetShapeType(ShapeType type)
    {
        if (_isEditing && CurrentContext != null) CommitActiveShape(CurrentContext); // 如果切换形状类型时正在编辑，先提交
        _currentShapeType = type;
    }

    private ToolContext CurrentContext;

    public override void OnPointerDown(ToolContext ctx, Point viewPos, float pressure = 1.0f)
    {
        var mw = ctx.ParentWindow;
        if (mw.IsViewMode) return;
        CurrentContext = ctx;
        var px = ctx.ToPixel(viewPos);

        if (_isEditing)
        {
            _activeAnchor = HitTestAnchors(ctx, px);
            if (_activeAnchor != ManipulationAnchor.None)
            {
                _lastMousePos = px;
                _startMouse = px;
                _startW = _editingRect.Width;
                _startH = _editingRect.Height;
                _startX = _editingRect.X;
                _startY = _editingRect.Y;

                if (Math.Abs(_rotationAngle) > 0.01)
                {
                    Point center = new Point(_editingRect.Left + _editingRect.Width / 2, _editingRect.Top + _editingRect.Height / 2);
                    var rt = new RotateTransform(-_rotationAngle, center.X, center.Y);
                    _startMouse = rt.Transform(px);
                }

                ctx.CapturePointer();
                return;
            }
            else
            {
                // 点击外部，提交
                CommitActiveShape(ctx);
                // 继续处理，允许立即开始画新形状
            }
        }

        _startPoint = px;
        _lineUsesNegativeDiagonal = false;
        if (_currentShapeType == ShapeType.Arrow)
        {
            _arrowStartPoint = px;
            _arrowEndPoint = px;
            _hasArrowEndpoints = true;
        }
        CreatePreviewShape(ctx);
        UpdatePreviewShape(ctx, _startPoint, _startPoint, ctx.PenThickness);
        
        _isDrawing = true;
        ctx.CapturePointer();
        
        if (_previewShape != null) ctx.EditorOverlay.Children.Add(_previewShape);
    }

    private void CreatePreviewShape(ToolContext ctx)
    {
        _previewShape = null;
        switch (_currentShapeType)
        {
            case ShapeType.Rectangle:
                _previewShape = new System.Windows.Shapes.Rectangle();
                break;
            case ShapeType.RoundedRectangle:
                _previewShape = new System.Windows.Shapes.Rectangle { RadiusX = AppConsts.ShapeToolRoundedRectRadius, RadiusY = AppConsts.ShapeToolRoundedRectRadius };
                break;
            case ShapeType.Ellipse:
                _previewShape = new System.Windows.Shapes.Ellipse();
                break;
            case ShapeType.Line:
                _previewShape = new System.Windows.Shapes.Line();
                break;
            case ShapeType.Arrow:
            case ShapeType.Triangle:
            case ShapeType.Diamond:
            case ShapeType.Pentagon:
            case ShapeType.Star:
            case ShapeType.Bubble:
                _previewShape = new System.Windows.Shapes.Path();
                break;
        }

        if (_previewShape != null)
        {
            _previewShape.Stroke = new SolidColorBrush(ctx.PenColor);
            _previewShape.StrokeThickness = ctx.PenThickness;
            _previewShape.Fill = null;
            _previewShape.IsHitTestVisible = false;
        }
    }

    public override void OnPointerMove(ToolContext ctx, Point viewPos, float pressure = 1.0f)
    {
        var px = ctx.ToPixel(viewPos);
        if (!_isDrawing && _isEditing)
        {
            // 更新悬停光标
            UpdateCursorByPos(ctx, px);
        }

        if (_isDrawing)
        {
            if (_currentShapeType == ShapeType.Arrow)
            {
                _arrowEndPoint = px;
            }
            UpdatePreviewShape(ctx, _startPoint, px, ctx.PenThickness);
            int w = (int)Math.Abs(px.X - _startPoint.X);
            int h = (int)Math.Abs(px.Y - _startPoint.Y);
            ctx.ParentWindow.SelectionSize = string.Format(LocalizationManager.GetString("L_Selection_Size_Format"), w, h);
        }
        else if (_isEditing && _activeAnchor != ManipulationAnchor.None)
        {
            if (_activeAnchor == ManipulationAnchor.Rotate)
            {
                Point center = new Point(_editingRect.Left + _editingRect.Width / 2, _editingRect.Top + _editingRect.Height / 2);
                Vector v = px - center;
                _rotationAngle = Math.Atan2(v.Y, v.X) * 180 / Math.PI + 90;
            }
            else if (_activeAnchor == ManipulationAnchor.Move)
            {
                Vector delta = px - _lastMousePos;
                _editingRect.X += delta.X;
                _editingRect.Y += delta.Y;
            }
            else
            {
                Point currentMouse = px;
                if (Math.Abs(_rotationAngle) > 0.01)
                {
                    Point startCenter = new Point(_startX + _startW / 2, _startY + _startH / 2);
                    var rt = new RotateTransform(-_rotationAngle, startCenter.X, startCenter.Y);
                    currentMouse = rt.Transform(px);
                }
                ApplyResizing(currentMouse);
            }
            _lastMousePos = px;
            UpdatePreviewFromRect(ctx);
            RefreshHandles(ctx);
        }
    }

    public override void OnPointerUp(ToolContext ctx, Point viewPos, float pressure = 1.0f)
    {
        if (_isDrawing)
        {
            _isDrawing = false;
            ctx.ReleasePointerCapture();
            
            var endPoint = ctx.ToPixel(viewPos);
            if (_currentShapeType == ShapeType.Arrow)
            {
                _arrowEndPoint = endPoint;
            }
            else if (_currentShapeType == ShapeType.Line)
            {
                _lineUsesNegativeDiagonal = UsesNegativeDiagonal(_startPoint, endPoint);
            }
            _editingRect = new Rect(_startPoint, endPoint);

            if (_editingRect.Width <= 1 && _editingRect.Height <= 1)
            {
                ClearPreview(ctx);
                return;
            }

            if (SettingsManager.Instance.Current.IsProfessionalMode)
            {
                _isEditing = true;
                RefreshHandles(ctx);
                if (ctx.ViewElement != null) ctx.ViewElement.Cursor = System.Windows.Input.Cursors.Arrow;
            }
            else
            {
                CommitActiveShape(ctx);
            }
        }
        else if (_isEditing)
        {
            _activeAnchor = ManipulationAnchor.None;
            ctx.ReleasePointerCapture();
        }
    }

    private void ApplyResizing(Point currentMouse)
    {
        double dx = currentMouse.X - _startMouse.X;
        double dy = currentMouse.Y - _startMouse.Y;

        double fixedRight = _startX + _startW;
        double fixedBottom = _startY + _startH;

        double proposedW = _startW;
        double proposedH = _startH;
        double x = _startX;
        double y = _startY;

        bool isShiftDown = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        switch (_activeAnchor)
        {
            case ManipulationAnchor.NW:
                proposedW = _startW - dx;
                proposedH = _startH - dy;
                break;
            case ManipulationAnchor.NE:
                proposedW = _startW + dx;
                proposedH = _startH - dy;
                break;
            case ManipulationAnchor.SW:
                proposedW = _startW - dx;
                proposedH = _startH + dy;
                break;
            case ManipulationAnchor.SE:
                proposedW = _startW + dx;
                proposedH = _startH + dy;
                break;
            case ManipulationAnchor.N:
                proposedH = _startH - dy;
                break;
            case ManipulationAnchor.S:
                proposedH = _startH + dy;
                break;
            case ManipulationAnchor.W:
                proposedW = _startW - dx;
                break;
            case ManipulationAnchor.E:
                proposedW = _startW + dx;
                break;
        }

        if (isShiftDown && _startW > 0 && _startH > 0)
        {
            double ratio = _startW / _startH;
            if (_activeAnchor == ManipulationAnchor.NW || _activeAnchor == ManipulationAnchor.NE ||
                _activeAnchor == ManipulationAnchor.SW || _activeAnchor == ManipulationAnchor.SE)
            {
                if (Math.Abs(proposedW / _startW) > Math.Abs(proposedH / _startH))
                    proposedH = proposedW / ratio;
                else
                    proposedW = proposedH * ratio;
            }
        }

        proposedW = Math.Max(1, proposedW);
        proposedH = Math.Max(1, proposedH);

        // 根据锚点计算 X/Y，确保对角点固定
        if (_activeAnchor == ManipulationAnchor.NW || _activeAnchor == ManipulationAnchor.W || _activeAnchor == ManipulationAnchor.SW)
            x = fixedRight - proposedW;

        if (_activeAnchor == ManipulationAnchor.NW || _activeAnchor == ManipulationAnchor.N || _activeAnchor == ManipulationAnchor.NE)
            y = fixedBottom - proposedH;

        _editingRect = new Rect(x, y, proposedW, proposedH);
    }

    public void RefreshPreview(ToolContext ctx)
    {
        if (_previewShape == null || !_isEditing) return;
        _previewShape.Stroke = new SolidColorBrush(ctx.PenColor);
        UpdatePreviewFromRect(ctx);
        RefreshHandles(ctx);
    }

    private void UpdatePreviewFromRect(ToolContext ctx)
    {
        if (_previewShape == null) return;
        GetCommittedShapeEndpoints(out Point renderStart, out Point renderEnd);
        UpdatePreviewShape(ctx, renderStart, renderEnd, ctx.PenThickness);
    }

    private void UpdateCursorByPos(ToolContext ctx, Point px)
    {
        var anchor = HitTestAnchors(ctx, px);
        System.Windows.Input.Cursor cursor = this.Cursor;

        switch (anchor)
        {
            case ManipulationAnchor.NW:
            case ManipulationAnchor.SE:
                cursor = System.Windows.Input.Cursors.SizeNWSE;
                break;
            case ManipulationAnchor.NE:
            case ManipulationAnchor.SW:
                cursor = System.Windows.Input.Cursors.SizeNESW;
                break;
            case ManipulationAnchor.N:
            case ManipulationAnchor.S:
                cursor = System.Windows.Input.Cursors.SizeNS;
                break;
            case ManipulationAnchor.W:
            case ManipulationAnchor.E:
                cursor = System.Windows.Input.Cursors.SizeWE;
                break;
            case ManipulationAnchor.Move:
                cursor = System.Windows.Input.Cursors.SizeAll;
                break;
            case ManipulationAnchor.Rotate:
                cursor = System.Windows.Input.Cursors.Hand;
                break;
        }

        if (ctx.ViewElement != null && ctx.ViewElement.Cursor != cursor)
        {
            ctx.ViewElement.Cursor = cursor;
        }
    }

    private void RefreshHandles(ToolContext ctx)
    {
        foreach (var h in _handleVisuals) ctx.EditorOverlay.Children.Remove(h);
        _handleVisuals.Clear();

        if (!_isEditing) return;

        double zoom = ctx.ParentWindow.zoomscale;
        double adaptiveHandleSize = HandleSize / zoom;
        double adaptiveThickness = 1.0 / zoom;

        ManipulationAnchor[] anchors = { ManipulationAnchor.NW, ManipulationAnchor.N, ManipulationAnchor.NE, ManipulationAnchor.W, ManipulationAnchor.E, ManipulationAnchor.SW, ManipulationAnchor.S, ManipulationAnchor.SE, ManipulationAnchor.Rotate };
        
        // 绘制连接旋转手柄的线
        Point pN = ctx.FromPixel(GetAnchorPoint(ctx, ManipulationAnchor.N));
        Point pR = ctx.FromPixel(GetAnchorPoint(ctx, ManipulationAnchor.Rotate));
        var line = new System.Windows.Shapes.Line
        {
            X1 = pN.X, Y1 = pN.Y, X2 = pR.X, Y2 = pR.Y,
            Stroke = Brushes.DeepSkyBlue,
            StrokeThickness = adaptiveThickness,
            IsHitTestVisible = false
        };
        ctx.EditorOverlay.Children.Add(line);
        _handleVisuals.Add(line);
        
        foreach (var anchor in anchors)
        {
            Point p = ctx.FromPixel(GetAnchorPoint(ctx, anchor));
            if (anchor == ManipulationAnchor.Rotate)
            {
                double rotateSize = adaptiveHandleSize * 2.5;
                var grid = new Grid { Width = rotateSize, Height = rotateSize, IsHitTestVisible = false };
                // 外圈
                grid.Children.Add(new Ellipse
                {
                    Stroke = Brushes.DeepSkyBlue,
                    StrokeThickness = adaptiveThickness
                });
                // 内圆背景
                grid.Children.Add(new Ellipse
                {
                    Fill = Brushes.White,
                    Margin = new Thickness(rotateSize * 0.1)
                });
                var iconPath = new System.Windows.Shapes.Path
                {
                    Data = Application.Current.TryFindResource("Rotate_Right_Image") as Geometry,
                    Fill = Brushes.DeepSkyBlue,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(rotateSize * 0.25)
                };
                grid.Children.Add(iconPath);
                Canvas.SetLeft(grid, p.X - rotateSize / 2);
                Canvas.SetTop(grid, p.Y - rotateSize / 2);
                ctx.EditorOverlay.Children.Add(grid);
                _handleVisuals.Add(grid);
            }
            else
            {
                var ellipse = new Ellipse
                {
                    Width = adaptiveHandleSize,
                    Height = adaptiveHandleSize,
                    Fill = Brushes.White,
                    Stroke = Brushes.DeepSkyBlue,
                    StrokeThickness = adaptiveThickness,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(ellipse, p.X - adaptiveHandleSize / 2);
                Canvas.SetTop(ellipse, p.Y - adaptiveHandleSize / 2);
                ctx.EditorOverlay.Children.Add(ellipse);
                _handleVisuals.Add(ellipse);
            }
        }
    }

    private Point GetAnchorPoint(ToolContext ctx, ManipulationAnchor anchor)
    {
        Point p;
        double zoom = ctx.ParentWindow.zoomscale;
        double adaptiveRotateOffset = RotateHandleOffset / zoom;

        switch (anchor)
        {
            case ManipulationAnchor.NW: p = _editingRect.TopLeft; break;
            case ManipulationAnchor.N: p = new Point(_editingRect.Left + _editingRect.Width / 2, _editingRect.Top); break;
            case ManipulationAnchor.NE: p = _editingRect.TopRight; break;
            case ManipulationAnchor.W: p = new Point(_editingRect.Left, _editingRect.Top + _editingRect.Height / 2); break;
            case ManipulationAnchor.E: p = new Point(_editingRect.Right, _editingRect.Top + _editingRect.Height / 2); break;
            case ManipulationAnchor.SW: p = _editingRect.BottomLeft; break;
            case ManipulationAnchor.S: p = new Point(_editingRect.Left + _editingRect.Width / 2, _editingRect.Bottom); break;
            case ManipulationAnchor.SE: p = _editingRect.BottomRight; break;
            case ManipulationAnchor.Rotate: p = new Point(_editingRect.Left + _editingRect.Width / 2, _editingRect.Top - adaptiveRotateOffset); break;
            default: p = new Point(); break;
        }

        if (Math.Abs(_rotationAngle) > 0.01 && anchor != ManipulationAnchor.Move && anchor != ManipulationAnchor.None)
        {
            var rt = new RotateTransform(_rotationAngle, _editingRect.Left + _editingRect.Width / 2, _editingRect.Top + _editingRect.Height / 2);
            p = rt.Transform(p);
        }
        return p;
    }

    private ManipulationAnchor HitTestAnchors(ToolContext ctx, Point p)
    {
        double zoom = ctx.ParentWindow.zoomscale;
        double adaptiveHitRange = HitRange / zoom;

        ManipulationAnchor[] anchors = { ManipulationAnchor.Rotate, ManipulationAnchor.NW, ManipulationAnchor.NE, ManipulationAnchor.SW, ManipulationAnchor.SE, ManipulationAnchor.N, ManipulationAnchor.S, ManipulationAnchor.W, ManipulationAnchor.E };
        foreach (var anchor in anchors)
        {
            Point ap = GetAnchorPoint(ctx, anchor);
            if (Math.Abs(p.X - ap.X) <= adaptiveHitRange && Math.Abs(p.Y - ap.Y) <= adaptiveHitRange) return anchor;
        }
        if (_editingRect.Contains(p)) return ManipulationAnchor.Move;
        return ManipulationAnchor.None;
    }

    private void CommitActiveShape(ToolContext ctx)
    {
        if (_previewShape == null) return;

        GetCommittedShapeEndpoints(out Point renderStart, out Point renderEnd);

        // 基于真实几何渲染边界计算提交区域，避免小尺寸尖角图形被裁剪
        Rect shapeGlobalBounds = CalculateShapeRenderBounds(renderStart, renderEnd, ctx.PenThickness);

        Rect canvasBounds = new Rect(0, 0, ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);
        Rect intersectBounds = Rect.Intersect(shapeGlobalBounds, canvasBounds);

        if (intersectBounds != Rect.Empty && intersectBounds.Width >= 1 && intersectBounds.Height >= 1)
        {
            // 关键：对齐到整数像素边界，消除偏移错位
            int ix = (int)Math.Floor(intersectBounds.X);
            int iy = (int)Math.Floor(intersectBounds.Y);
            int iw = (int)Math.Ceiling(intersectBounds.Right) - ix;
            int ih = (int)Math.Ceiling(intersectBounds.Bottom) - iy;
            Rect validBounds = new Rect(ix, iy, iw, ih);

            var shapeBitmap = RenderShapeToBitmapClipped(renderStart, renderEnd, validBounds, ctx.PenColor, ctx.PenThickness, ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY);
            if (shapeBitmap != null)
            {
                ctx.Undo.BeginStroke();
                
                int stride = iw * 4;
                byte[] pixels = new byte[stride * ih];
                shapeBitmap.CopyPixels(pixels, stride, 0);
                
                ctx.Surface.WriteRegion(new Int32Rect(ix, iy, iw, ih), pixels, stride, false);
                
                ctx.Undo.AddDirtyRect(new Int32Rect(ix, iy, iw, ih));
                ctx.Undo.CommitStroke();
                
                ctx.ParentWindow.SetUndoRedoButtonState();
            }
        }

        ClearPreview(ctx);
    }

    private Rect CalculateShapeRenderBounds(Point globalStart, Point globalEnd, double thickness)
    {
        Rect logicalRect = new Rect(
            Math.Min(globalStart.X, globalEnd.X),
            Math.Min(globalStart.Y, globalEnd.Y),
            Math.Abs(globalStart.X - globalEnd.X),
            Math.Abs(globalStart.Y - globalEnd.Y));

        Geometry geometry = BuildGeometryForCurrentShape(globalStart, globalEnd, logicalRect);
        if (geometry == null) return Rect.Empty;

        Pen pen = CreateShapePen(Brushes.Black, thickness);
        Rect bounds = geometry.GetRenderBounds(pen);

        if (Math.Abs(_rotationAngle) > 0.01)
        {
            var rt = new RotateTransform(_rotationAngle, _editingRect.X + _editingRect.Width / 2, _editingRect.Y + _editingRect.Height / 2);
            bounds = rt.TransformBounds(bounds);
        }

        // 额外给抗锯齿预留像素余量，避免边缘被截断
        bounds.Inflate(1.5, 1.5);
        return bounds;
    }

    private Geometry BuildGeometryForCurrentShape(Point globalStart, Point globalEnd, Rect logicalRect)
    {
        switch (_currentShapeType)
        {
            case ShapeType.Rectangle:
                return new RectangleGeometry(logicalRect);
            case ShapeType.RoundedRectangle:
                return new RectangleGeometry(logicalRect, AppConsts.ShapeToolRoundedRectRadius, AppConsts.ShapeToolRoundedRectRadius);
            case ShapeType.Ellipse:
                return new EllipseGeometry(logicalRect);
            case ShapeType.Line:
                return new LineGeometry(globalStart, globalEnd);
            case ShapeType.Arrow:
                return BuildArrowGeometry(globalStart, globalEnd, Gethandlength(globalStart, globalEnd));
            case ShapeType.Triangle:
                return BuildRegularPolygon(logicalRect, 3, -Math.PI / 2);
            case ShapeType.Diamond:
                return BuildRegularPolygon(logicalRect, 4, 0);
            case ShapeType.Pentagon:
                return BuildRegularPolygon(logicalRect, 5, -Math.PI / 2);
            case ShapeType.Star:
                return BuildStarGeometry(logicalRect);
            case ShapeType.Bubble:
                return BuildBubbleGeometry(logicalRect);
            default:
                return null;
        }
    }

    private Pen CreateShapePen(Brush strokeBrush, double thickness)
    {
        Pen pen = new Pen(strokeBrush, thickness);
        if (_currentShapeType == ShapeType.Rectangle || _currentShapeType == ShapeType.Diamond || _currentShapeType == ShapeType.Triangle)
        {
            pen.LineJoin = PenLineJoin.Miter;
            pen.StartLineCap = PenLineCap.Square;
            pen.EndLineCap = PenLineCap.Square;
        }
        else
        {
            pen.LineJoin = PenLineJoin.Round;
            pen.StartLineCap = PenLineCap.Round;
            pen.EndLineCap = PenLineCap.Round;
        }
        return pen;
    }

    private void ClearPreview(ToolContext ctx)
    {
        if (_previewShape != null) ctx.EditorOverlay.Children.Remove(_previewShape);
        foreach (var h in _handleVisuals) ctx.EditorOverlay.Children.Remove(h);
        _handleVisuals.Clear();
        _previewShape = null;
        _isEditing = false;
        _rotationAngle = 0;
        _hasArrowEndpoints = false;
        _lineUsesNegativeDiagonal = false;
        if (ctx.ViewElement != null) ctx.ViewElement.Cursor = this.Cursor;
    }

    private static bool UsesNegativeDiagonal(Point start, Point end)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        return dx * dy < 0;
    }

    private void GetCommittedShapeEndpoints(out Point renderStart, out Point renderEnd)
    {
        if (_currentShapeType == ShapeType.Arrow && _hasArrowEndpoints)
        {
            renderStart = _arrowStartPoint;
            renderEnd = _arrowEndPoint;
            return;
        }

        if (_currentShapeType == ShapeType.Line)
        {
            if (_lineUsesNegativeDiagonal)
            {
                renderStart = _editingRect.BottomLeft;
                renderEnd = _editingRect.TopRight;
            }
            else
            {
                renderStart = _editingRect.TopLeft;
                renderEnd = _editingRect.BottomRight;
            }
            return;
        }

        renderStart = _editingRect.TopLeft;
        renderEnd = _editingRect.BottomRight;
    }

    public void GiveUpSelection(ToolContext ctx)
    {
        ClearPreview(ctx);
    }

    public override void OnKeyDown(ToolContext ctx, KeyEventArgs e)
    {
        if (e.IsRepeat) return;

        if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (_isEditing)
            {
                ClearPreview(ctx);
                e.Handled = true;
                return;
            }
        }
        
        if (e.Key == Key.Enter && _isEditing)
        {
            CommitActiveShape(ctx);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && _isEditing)
        {
            ClearPreview(ctx);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(ctx, e);
    }

    private BitmapSource RenderShapeToBitmapClipped(Point globalStart, Point globalEnd, Rect validBounds, Color color, double thickness, double dpiX, double dpiY)
    {
        int pixelWidth = (int)validBounds.Width;
        int pixelHeight = (int)validBounds.Height;
        if (pixelWidth <= 0 || pixelHeight <= 0) return null;

        DrawingVisual drawingVisual = new DrawingVisual();
        using (DrawingContext dc = drawingVisual.RenderOpen())
        {
            // 使用整数偏移
            dc.PushTransform(new TranslateTransform(-validBounds.X, -validBounds.Y));
            if (Math.Abs(_rotationAngle) > 0.01)
            {
                dc.PushTransform(new RotateTransform(_rotationAngle, _editingRect.X + _editingRect.Width / 2, _editingRect.Y + _editingRect.Height / 2));
            }

            Pen pen = CreateShapePen(new SolidColorBrush(color), thickness);
            pen.Freeze();

            Rect logicalRect = new Rect(Math.Min(globalStart.X, globalEnd.X), Math.Min(globalStart.Y, globalEnd.Y), Math.Abs(globalStart.X - globalEnd.X), Math.Abs(globalStart.Y - globalEnd.Y));

            switch (_currentShapeType)
            {
                case ShapeType.Rectangle:
                    dc.DrawRectangle(null, pen, logicalRect);
                    break;
                case ShapeType.RoundedRectangle:
                    dc.DrawRoundedRectangle(null, pen, logicalRect, AppConsts.ShapeToolRoundedRectRadius, AppConsts.ShapeToolRoundedRectRadius);
                    break;
                case ShapeType.Ellipse:
                    dc.DrawEllipse(null, pen,
                        new Point(logicalRect.X + logicalRect.Width / 2.0, logicalRect.Y + logicalRect.Height / 2.0),
                        logicalRect.Width / 2.0, logicalRect.Height / 2.0);
                    break;
                case ShapeType.Line:
                    dc.DrawLine(pen, globalStart, globalEnd);
                    break;
                case ShapeType.Arrow:
                    dc.DrawGeometry(null, pen, BuildArrowGeometry(globalStart, globalEnd, Gethandlength(globalStart, globalEnd)));
                    break;
                case ShapeType.Triangle:
                    dc.DrawGeometry(null, pen, BuildRegularPolygon(logicalRect, 3, -Math.PI / 2));
                    break;
                case ShapeType.Diamond:
                    dc.DrawGeometry(null, pen, BuildRegularPolygon(logicalRect, 4, 0));
                    break;
                case ShapeType.Pentagon:
                    dc.DrawGeometry(null, pen, BuildRegularPolygon(logicalRect, 5, -Math.PI / 2));
                    break;
                case ShapeType.Star:
                    dc.DrawGeometry(null, pen, BuildStarGeometry(logicalRect));
                    break;
                case ShapeType.Bubble:
                    dc.DrawGeometry(null, pen, BuildBubbleGeometry(logicalRect));
                    break;
            }

            if (Math.Abs(_rotationAngle) > 0.01) dc.Pop();
            dc.Pop();
        }

        RenderTargetBitmap bmp = new RenderTargetBitmap(pixelWidth, pixelHeight, dpiX, dpiY, PixelFormats.Pbgra32);
        bmp.Render(drawingVisual);
        bmp.Freeze();
        return bmp;
    }

    private void UpdatePreviewShape(ToolContext ctx, Point start, Point end, double thickness)
    {
        if (ctx?.ViewElement == null || _previewShape == null) return;

        double x = Math.Min(start.X, end.X);
        double y = Math.Min(start.Y, end.Y);
        double w = Math.Abs(end.X - start.X);
        double h = Math.Abs(end.Y - start.Y);

        double viewScale = ctx.ViewElement.ActualWidth / ctx.FullImageWidth;
        double displayThickness = thickness * viewScale;

        if (_currentShapeType == ShapeType.Line)
        {
            var line = _previewShape as System.Windows.Shapes.Line;
            if (line == null) return;
            Point p1 = ctx.FromPixel(start);
            Point p2 = ctx.FromPixel(end);
            line.X1 = p1.X; line.Y1 = p1.Y; line.X2 = p2.X; line.Y2 = p2.Y;
            line.StrokeThickness = displayThickness;
        }
        else
        {
            Point viewPos = ctx.FromPixel(new Point(x, y));
            Point viewSize = ctx.FromPixel(new Point(x + w, y + h));
            double vw = viewSize.X - viewPos.X;
            double vh = viewSize.Y - viewPos.Y;

            if (_previewShape is System.Windows.Shapes.Path path)
            {
                Rect r = new Rect(viewPos.X, viewPos.Y, vw, vh);
                Point vStart = ctx.FromPixel(start);
                Point vEnd = ctx.FromPixel(end);

                switch (_currentShapeType)
                {
                    case ShapeType.Arrow:
                        // 使用原始像素坐标计算头部长度，确保预览和渲染一致
                        path.Data = BuildArrowGeometry(vStart, vEnd, Gethandlength(start, end));
                        break;
                    case ShapeType.Triangle:
                        path.Data = BuildRegularPolygon(r, 3, -Math.PI / 2);
                        break;
                    case ShapeType.Diamond:
                        path.Data = BuildRegularPolygon(r, 4, 0);
                        break;
                    case ShapeType.Pentagon:
                        path.Data = BuildRegularPolygon(r, 5, -Math.PI / 2);
                        break;
                    case ShapeType.Star:
                        path.Data = BuildStarGeometry(r);
                        break;
                    case ShapeType.Bubble:
                        path.Data = BuildBubbleGeometry(r);
                        break;
                }
                path.StrokeThickness = displayThickness;
            }
            else
            {
                Canvas.SetLeft(_previewShape, viewPos.X);
                Canvas.SetTop(_previewShape, viewPos.Y);
                _previewShape.Width = Math.Max(0, vw);
                _previewShape.Height = Math.Max(0, vh);
                _previewShape.StrokeThickness = displayThickness;
            }
        }

        if (_previewShape != null)
        {
            _previewShape.RenderTransformOrigin = new Point(0.5, 0.5);
            _previewShape.RenderTransform = new RotateTransform(_rotationAngle);
        }
    }

    private double Gethandlength(Point start, Point end)
    {
        return Math.Pow(Math.Pow(start.X - end.X, 2) + Math.Pow(start.Y - end.Y, 2), 0.5) * AppConsts.ArrowHeadRatio;
    }
}
