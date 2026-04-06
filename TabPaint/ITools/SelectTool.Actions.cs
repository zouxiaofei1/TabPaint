using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

//
//SelectTool类键鼠操作相关方法
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public partial class SelectTool : ToolBase
        {
            public bool HasActiveSelection => _selectionData != null;
            public void DeleteSelection(ToolContext ctx)     // 添加：执行删除选区的具体逻辑
            {
                if (_selectionData == null) return;
                EnsureRotationBaked(ctx);

                if (!_hasLifted)
                {
                    ctx.Undo.BeginStroke();
                    ctx.Undo.AddDirtyRect(_selectionRect);

                    if (IsIrregularSelection && _selectionAlphaMap != null) ClearLassoRegion(ctx, _selectionRect, ctx.EraserColor);
                    else ClearRect(ctx, _selectionRect, ctx.EraserColor);
                    ctx.Undo.CommitStroke();  // 提交到 Undo 栈
                    ctx.IsDirty = true;
                }
                HidePreview(ctx);
                if (ctx.SelectionOverlay != null)
                {
                    ctx.SelectionOverlay.Children.Clear();
                    ctx.SelectionOverlay.Visibility = Visibility.Collapsed;
                }
                _selectionData = null;
                _selectionRect = new Int32Rect(0, 0, 0, 0);
                _originalRect = new Int32Rect(0, 0, 0, 0);
                _hasLifted = false;
                _transformStep = 0;
                _draggingSelection = false;
                _resizing = false;
                Mouse.OverrideCursor = null;
                var mw = ctx.ParentWindow;
                mw.SetCropButtonState();
                mw.SelectionSize = string.Format(LocalizationManager.GetString("L_Selection_Size_Format"), 0, 0);
                mw.SetUndoRedoButtonState(); mw.UpdateSelectionToolBarPosition();
                LastSelectionDeleteTime = DateTime.Now; mw.ClearRulerSelection();
            }
            public void ResetLastDeleteTime()
            {
                LastSelectionDeleteTime = DateTime.MinValue;
            }

            public void RefreshWandPreview(ToolContext ctx)
            {
                if (SelectionType != SelectionType.MagicWand || _wandStartPoint.X < 0) return;

                _isWandAdjusting = true;
                bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                RunMagicWand(ctx, _wandStartPoint, _wandTolerance, isShift);
                _isWandAdjusting = false;
            }

            public void ReplaceSelectionData(ToolContext ctx, byte[] newPixels, int w, int h)
            {
                if (newPixels == null) return;

                _selectionRect = new Int32Rect(_selectionRect.X, _selectionRect.Y, w, h);

                _originalRect = _selectionRect;
                _transformStep = 0;
                _selectionData = newPixels;
                int stride = w * 4;
                if (_selectionData.Length < h * stride) return; // 安全检查

                _selectionAlphaMap = new byte[h * stride];
                System.Threading.Tasks.Parallel.For(0, h, y =>
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * stride + x * 4;
                        // 复制 Alpha 通道作为 Mask
                        if (i + 3 < newPixels.Length)
                        {
                            byte alpha = newPixels[i + 3];
                            _selectionAlphaMap[i + 3] = alpha;
                        }
                    }
                });

                // 6. 刷新预览
                CreatePreviewFromSelectionData(ctx);

                // 7. 更新UI状态
                var mw = ctx.ParentWindow;
                mw.SelectionSize = $"{w}×{h}" + LocalizationManager.GetString("L_Main_Unit_Pixel");
                mw.UpdateSelectionToolBarPosition();
            }
            public WriteableBitmap GetSelectionWriteableBitmap(MainWindow mw)
            {
                var source = GetSelectionCroppedBitmap(mw);
                if (source == null) return null;

                // 1. 统一转换为 BGRA32
                if (source.Format != PixelFormats.Bgra32)
                {
                    source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                }

                // 2. 强制统一 DPI 为 96，防止 WriteableBitmap 计算 Stride 时出现舍入误差
                int w = source.PixelWidth;
                int h = source.PixelHeight;
                int stride = w * 4;
                byte[] pixels = new byte[h * stride];
                source.CopyPixels(pixels, stride, 0);

                var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
                wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
                return wb;
            }

            private DateTime _hoverStartTime;
            private bool _isHoveringForSwitch = false;
            public void ForceDragState(MainWindow mw)
            {
                if (_selectionData != null)
                {
                    _hasLifted = true; // 视为已经浮起
                    _draggingSelection = true;
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        _clickOffset = new Point(_selectionRect.Width / 2, _selectionRect.Height / 2);
                        Mouse.Capture(mw.CanvasWrapper);
                    });
                }
            }


            public void SelectAll(ToolContext ctx, bool cut = true)
            {
                if (ctx.Surface?.Bitmap == null) return;
                IsPasted = false;
                ctx.SelectionPreview.Clip = null;             // 清除残留的裁剪
                ctx.SelectionPreview.RenderTransform = null;  // 清除残留的位移/缩放
                Canvas.SetLeft(ctx.SelectionPreview, 0);      // 归位布局坐标
                Canvas.SetTop(ctx.SelectionPreview, 0);       // 归位布局坐标

                _selectionRect = new Int32Rect(0, 0,
                    ctx.Surface.Bitmap.PixelWidth,
                    ctx.Surface.Bitmap.PixelHeight);
                _originalRect = _selectionRect;

                // 提取整幅像素
                _selectionData = ctx.Surface.ExtractRegion(_selectionRect);
                if (_selectionData == null || _selectionData.Length < _selectionRect.Width * _selectionRect.Height * 4)
                    return;

                // Undo 记录逻辑
                ctx.Undo.BeginStroke();
                ctx.Undo.AddDirtyRect(_selectionRect);
                ctx.Undo.CommitStroke();
                if (cut)
                {
                    ClearRect(ctx, _selectionRect, ctx.EraserColor);
                    _hasLifted = true; // 标记已经提起来了
                }
                else _hasLifted = false;
                // 创建预览位图
                var previewBmp = new WriteableBitmap(_selectionRect.Width, _selectionRect.Height,
                    ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);

                previewBmp.WritePixels(
                    new Int32Rect(0, 0, _selectionRect.Width, _selectionRect.Height),
                    _selectionData, _selectionRect.Width * 4, 0);

                ctx.SelectionPreview.Source = previewBmp;
                ctx.SelectionPreview.Visibility = Visibility.Visible;

                double dpiScaleX = AppConsts.StandardDpi / ctx.Surface.Bitmap.DpiX;
                double dpiScaleY = AppConsts.StandardDpi / ctx.Surface.Bitmap.DpiY;

                ctx.SelectionPreview.Width = _selectionRect.Width * dpiScaleX;
                ctx.SelectionPreview.Height = _selectionRect.Height * dpiScaleY;

                // 确保它填充空间
                ctx.SelectionPreview.Stretch = System.Windows.Media.Stretch.Fill;
                ctx.SelectionPreview.RenderTransform = new TranslateTransform(0, 0);
                _transformStep = 0;

                // 绘制虚线框
                DrawOverlay(ctx, _selectionRect);
                var mw = ctx.ParentWindow;
                mw.UpdateSelectionToolBarPosition();
                mw.SetCropButtonState(); mw.UpdateRulerSelection();
            }
            public void CropToSelection(ToolContext ctx)
            {
                if (_selectionData == null || _selectionRect.Width <= 0 || _selectionRect.Height <= 0) return;
                EnsureRotationBaked(ctx);

                var undoRect = new Int32Rect(0, 0, ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);
                var undoPixels = ctx.Surface.ExtractRegion(undoRect);
                var wb = ctx.Surface.Bitmap;
                int stride = wb.PixelWidth * (wb.Format.BitsPerPixel / 8);
                byte[] pixels = new byte[wb.PixelHeight * stride];

                wb.CopyPixels(pixels, stride, 0);
                byte[] finalSelectionData;
                int finalWidth = _selectionRect.Width;
                int finalHeight = _selectionRect.Height;
                int finalStride;

                // 检查是否进行过缩放
                if (_originalRect.Width > 0 && _originalRect.Height > 0 &&
      (_originalRect.Width != _selectionRect.Width || _originalRect.Height != _selectionRect.Height))
                {
                    var src = BitmapSource.Create(
                        _originalRect.Width, _originalRect.Height,
                        ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                        PixelFormats.Bgra32, null, _selectionData, _originalRect.Width * 4);
                    var resizedBitmap = (ctx.ParentWindow).ResampleBitmap(src, finalWidth, finalHeight);

                    finalStride = resizedBitmap.PixelWidth * 4;
                    finalSelectionData = new byte[finalHeight * finalStride];
                    resizedBitmap.CopyPixels(finalSelectionData, finalStride, 0);
                }
                else
                {
                    // 没有缩放，直接使用原始数据
                    finalSelectionData = _selectionData;
                    finalStride = finalWidth * 4;
                }

                var newBitmap = new WriteableBitmap(finalWidth, finalHeight, ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);

                newBitmap.WritePixels(new Int32Rect(0, 0, finalWidth, finalHeight), finalSelectionData, finalStride, 0);

                var redoRect = new Int32Rect(0, 0, newBitmap.PixelWidth, newBitmap.PixelHeight);
                int redoStride = newBitmap.BackBufferStride;
                var redoPixels = new byte[redoStride * redoRect.Height];
                newBitmap.CopyPixels(redoPixels, redoStride, 0);


                ctx.Surface.ReplaceBitmap(newBitmap);
                Cleanup(ctx);
                ctx.Undo.PushTransformAction(undoRect, undoPixels, redoRect, redoPixels);
                ctx.IsDirty = true;
                (ctx.ParentWindow).NotifyCanvasSizeChanged(finalWidth, finalHeight);
                // 更新UI（例如Undo/Redo按钮的状态）
                (ctx.ParentWindow).SetUndoRedoButtonState(); (ctx.ParentWindow).ClearRulerSelection();
            }
            public override void OnPointerDown(ToolContext ctx, Point viewPos, float pressure = 1.0f) ////////////////////////////////////////////////////////////////////////////// 后面是鼠标键盘事件处理
            {
                var mw = ctx.ParentWindow;
                if (mw.IsViewMode) return;
                if (lag > 0) { lag--; return; }
                if (ctx.Surface.Bitmap == null) return;
                var px = ctx.ToPixel(viewPos);
                bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

                if (_selectionData != null && !isShift)
                {
                    // 判定点击位置是句柄还是框内
                    _currentAnchor = HitTestHandle(px, _selectionRect);
                    if (_currentAnchor != ResizeAnchor.None)
                    {
                        if (_transformStep == 0) _originalRect = _selectionRect;
                        _transformStep++;
                        _resizing = true; mw.UpdateSelectionToolBarPosition();
                        _startMouse = px;
                        if (_preRotationSelectionData != null && Math.Abs(_rotationAngle) > 0.01)
                        {
                            // 旋转状态下，缩放参考系应该是旋转前的矩形
                            double centerX = _preRotationRect.X + _preRotationRect.Width / 2.0;
                            double centerY = _preRotationRect.Y + _preRotationRect.Height / 2.0;
                            var rt = new RotateTransform(-_rotationAngle, centerX, centerY);
                            _startMouse = rt.Transform(px);
                            _startW = _preRotationRect.Width;
                            _startH = _preRotationRect.Height;
                            _startX = _preRotationRect.X;
                            _startY = _preRotationRect.Y;
                        }
                        else
                        {
                            _startW = _selectionRect.Width;
                            _startH = _selectionRect.Height;
                            _startX = _selectionRect.X;
                            _startY = _selectionRect.Y;
                        }
                        ctx.ViewElement.CaptureMouse();
                        return;
                    }
                    else if (IsPointInSelection(px))
                    {
                        if (_transformStep == 0) _originalRect = _selectionRect;
                        _transformStep++;
                        _draggingSelection = true; mw.UpdateSelectionToolBarPosition();
                        _clickOffset = new Point(px.X - _selectionRect.X, px.Y - _selectionRect.Y);
                        ctx.ViewElement.CaptureMouse();
                        return;
                    }
                }
                if (SelectionType == SelectionType.MagicWand)
                {
                    _selecting = true;
                    _isWandAdjusting = true; // 标记开始调整容差
                    _wandStartPoint = px;
                    _startPixel = px; // 借用这个记录一下，方便计算距离
                    _wandStartColor = mw.GetPixelColor((int)px.X, (int)px.Y);
                    if (!isShift)
                    {
                        HidePreview(ctx);
                        if (ctx.SelectionOverlay != null) ctx.SelectionOverlay.Children.Clear();
                        _selectionData = null; // 逻辑清除
                    }
                    RunMagicWand(ctx, _wandStartPoint, _wandTolerance, isShift);

                    ctx.ViewElement.CaptureMouse();
                    mw.SetCropButtonState();
                    return;
                }

                if (SelectionType == SelectionType.Lasso)
                {
                    _lassoPoints = new List<Point>();
                    _lassoPoints.Add(px);
                }

                _selecting = true;
                _startPixel = px; mw.UpdateSelectionToolBarPosition();
                _selectionRect = new Int32Rect((int)px.X, (int)px.Y, 0, 0);
                HidePreview(ctx);
                ctx.ViewElement.CaptureMouse();
                mw.SetCropButtonState();
            }

            public bool _hasLifted = false;
            public override void OnPointerMove(ToolContext ctx, Point viewPos, float pressure = 1.0f)
            {
                var mw = ctx.ParentWindow;

                ctxForTimer = ctx; // 缓存 Context 供 Timer 使用
                EnsureTimer();
                if ((_selecting || _draggingSelection || _resizing) &&
        Mouse.LeftButton == MouseButtonState.Released)
                {
                    ResetSwitchTimer();
                    OnPointerUp(ctx, viewPos);
                    return;
                }
                var px = ctx.ToPixel(viewPos);

                if (_draggingSelection && _selectionData != null)
                {
                    // 1. 坐标转换
                    Point windowPos = ctx.ViewElement.TranslatePoint(viewPos, mw);
                    var targetTab = mw.MainImageBar.GetTabFromPoint(windowPos);

                    if (targetTab != null && targetTab != mw._currentTabItem)
                    {
                        // 如果鼠标移动到了一个新的 Tab 上
                        if (_pendingTab != targetTab)
                        {
                            _pendingTab = targetTab;
                            _hoverStartTime = DateTime.Now;
                            _tabSwitchTimer.Start(); // 启动定时器！
                        }
                    }
                    else
                    {
                        if (_pendingTab != null) ResetSwitchTimer();
                    }
                }
                else
                {
                    // 如果没在拖拽，确保 Timer 是关掉的
                    if (_pendingTab != null) ResetSwitchTimer();
                }
                // 光标样式
                if (_selectionData != null)
                {
                    var anchor = HitTestHandle(px, _selectionRect);
                    switch (anchor)
                    {
                        case ResizeAnchor.TopLeft:
                        case ResizeAnchor.BottomRight:
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeNWSE;
                            break;
                        case ResizeAnchor.TopRight:
                        case ResizeAnchor.BottomLeft:
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeNESW;
                            break;
                        case ResizeAnchor.LeftMiddle:
                        case ResizeAnchor.RightMiddle:
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeWE;
                            break;
                        case ResizeAnchor.TopMiddle:
                        case ResizeAnchor.BottomMiddle:
                            Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeNS;
                            break;
                        default:
                            Mouse.OverrideCursor = IsPointInSelection(px)
                                ? System.Windows.Input.Cursors.SizeAll
                                : null;
                            break;
                    }
                }
                if (_resizing)
                {   // 缩放逻辑
                    if (!_hasLifted) LiftSelectionFromCanvas(ctx);

                    // 如果在旋转状态下缩放
                    bool inRotation = _preRotationSelectionData != null && Math.Abs(_rotationAngle) > 0.01;
                    Point currentPx = px;
                    if (inRotation)
                    {
                        // 将鼠标点变换到旋转前的坐标系进行缩放计算
                        double centerX = _preRotationRect.X + _preRotationRect.Width / 2.0;
                        double centerY = _preRotationRect.Y + _preRotationRect.Height / 2.0;
                        var rt = new RotateTransform(-_rotationAngle, centerX, centerY);
                        currentPx = rt.Transform(px);
                    }

                    double fixedRight = _startX + _startW;
                    double fixedBottom = _startY + _startH;

                    bool isShiftDown = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                    double dx = currentPx.X - _startMouse.X;
                    double dy = currentPx.Y - _startMouse.Y;

                    double proposedW = _startW;
                    double proposedH = _startH;

                    switch (_currentAnchor)
                    {
                        case ResizeAnchor.TopLeft:
                            proposedW = _startW - dx;
                            proposedH = _startH - dy;
                            break;
                        case ResizeAnchor.TopMiddle:
                            proposedH = _startH - dy;
                            break;
                        case ResizeAnchor.TopRight:
                            proposedW = _startW + dx;
                            proposedH = _startH - dy;
                            break;
                        case ResizeAnchor.LeftMiddle:
                            proposedW = _startW - dx;
                            break;
                        case ResizeAnchor.RightMiddle:
                            proposedW = _startW + dx;
                            break;
                        case ResizeAnchor.BottomLeft:
                            proposedW = _startW - dx;
                            proposedH = _startH + dy;
                            break;
                        case ResizeAnchor.BottomMiddle:
                            proposedH = _startH + dy;
                            break;
                        case ResizeAnchor.BottomRight:
                            proposedW = _startW + dx;
                            proposedH = _startH + dy;
                            break;
                    }

                    if (isShiftDown && (_startH != 0))
                    {
                        double aspectRatio = (double)_startW / _startH;

                        if (_currentAnchor == ResizeAnchor.TopLeft || _currentAnchor == ResizeAnchor.TopRight ||
                            _currentAnchor == ResizeAnchor.BottomLeft || _currentAnchor == ResizeAnchor.BottomRight)
                        {
                            // 取变化幅度较大的一边作为主导
                            if (Math.Abs(proposedW / _startW) > Math.Abs(proposedH / _startH)) proposedH = proposedW / aspectRatio;
                            else proposedW = proposedH * aspectRatio;
                        }
                    }

                    if (proposedW < 1) proposedW = 1;
                    if (proposedH < 1) proposedH = 1;

                    double finalX = _startX;
                    double finalY = _startY;

                    // 如果锚点在左侧，X 必须由 右边界 - 宽度 算出
                    if (_currentAnchor == ResizeAnchor.TopLeft ||
                        _currentAnchor == ResizeAnchor.LeftMiddle ||
                        _currentAnchor == ResizeAnchor.BottomLeft)
                    {
                        finalX = fixedRight - proposedW;
                    }
                    else
                    {
                        finalX = _startX;
                    }

                    // 如果锚点在上方，Y 必须由 下边界 - 高度 算出
                    if (_currentAnchor == ResizeAnchor.TopLeft ||
                        _currentAnchor == ResizeAnchor.TopMiddle ||
                        _currentAnchor == ResizeAnchor.TopRight)
                    {
                        finalY = fixedBottom - proposedH;
                    }
                    else
                    {
                        finalY = _startY;
                    }

                    if (inRotation)
                    {
                        // 更新逻辑矩形
                        _preRotationRect.Width = (int)Math.Max(1, proposedW);
                        _preRotationRect.Height = (int)Math.Max(1, proposedH);
                        _preRotationRect.X = (int)finalX;
                        _preRotationRect.Y = (int)finalY;

                        // 同步更新 _selectionRect 坐标
                        _selectionRect = _preRotationRect;

                        // 重新旋转渲染
                        UpdateRotation(ctx, (int)_rotationAngle, false);
                    }
                    else
                    {
                        // 普通缩放应用结果
                        _selectionRect.Width = (int)proposedW;
                        _selectionRect.Height = (int)proposedH;
                        _selectionRect.X = (int)finalX;
                        _selectionRect.Y = (int)finalY;
                        if (_originalRect.Width > 0 && _originalRect.Height > 0)
                        {
                            double scaleX = (double)_selectionRect.Width / _originalRect.Width;
                            double scaleY = (double)_selectionRect.Height / _originalRect.Height;

                            double dpiScaleX = AppConsts.StandardDpi / ctx.Surface.Bitmap.DpiX;
                            double dpiScaleY = AppConsts.StandardDpi / ctx.Surface.Bitmap.DpiY;
                            double diff = mw.CanvasWrapper.RenderSize.Width - (int)mw.CanvasWrapper.RenderSize.Width;

                            var tg = ctx.SelectionPreview.RenderTransform as TransformGroup;
                            if (tg == null)
                            {
                                tg = new TransformGroup();
                                tg.Children.Add(new ScaleTransform(scaleX, scaleY));
                                tg.Children.Add(new TranslateTransform(_selectionRect.X * dpiScaleX + diff * 0.75, _selectionRect.Y * dpiScaleY));
                                ctx.SelectionPreview.RenderTransform = tg;
                            }
                            else
                            {
                                var s = tg.Children.OfType<ScaleTransform>().FirstOrDefault();
                                if (s == null)
                                {
                                    s = new ScaleTransform(1, 1);
                                    tg.Children.Insert(0, s);
                                }
                                s.ScaleX = scaleX;
                                s.ScaleY = scaleY;

                                var t = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
                                if (t == null)
                                {
                                    t = new TranslateTransform(0, 0);
                                    tg.Children.Add(t);
                                }
                                t.X = _selectionRect.X * dpiScaleX + diff * 0.75;
                                t.Y = _selectionRect.Y * dpiScaleY;
                            }
                            ctx.SelectionPreview.Visibility = Visibility.Visible;
                        }
                        DrawOverlay(ctx, _selectionRect);
                    }

                    UpdateStatusBarSelectionSize(mw); mw.UpdateSelectionToolBarPosition();
                    mw.UpdateRulerSelection();
                    return;
                }

                if (_selecting && SelectionType == SelectionType.MagicWand && _isWandAdjusting)
                {
                    // 计算鼠标拖拽距离，映射为容差 (例如 1px = 0.5 容差，最大 255)
                    double dist = Math.Sqrt(Math.Pow(px.X - _startPixel.X, 2) + Math.Pow(px.Y - _startPixel.Y, 2));
                    int newTolerance = (int)(dist / 2.0); // 灵敏度调节
                    if (newTolerance > 255) newTolerance = 255;

                    // 只有容差变化了才重算，节省性能
                    if (newTolerance != _wandTolerance)
                    {
                        _wandTolerance = newTolerance;
                        bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                        RunMagicWand(ctx, _wandStartPoint, _wandTolerance, isShift);
                        mw.SelectionSize = $"{LocalizationManager.GetString("L_Tool_MagicWand")}: {_wandTolerance}";
                    }
                    return;
                }
                if (_selecting)// 框选逻辑
                {
                    _hasLifted = false;
                    if (SelectionType == SelectionType.Lasso)
                    {
                        var pxs = ctx.ToPixel(viewPos);
                        // 简单过滤一下距离，避免点太密集
                        if (_lassoPoints.Count > 0)
                        {
                            var last = _lassoPoints[_lassoPoints.Count - 1];
                            if (Math.Abs(pxs.X - last.X) > 2 || Math.Abs(pxs.Y - last.Y) > 2)
                            {
                                _lassoPoints.Add(pxs);
                                DrawLassoTrace(ctx); // 专门绘制轨迹的方法
                            }
                        }
                    }
                    else // 矩形逻辑 (保持原有)
                    {
                        var pxs = ctx.ToPixel(viewPos);
                        _selectionRect = MakeRect(_startPixel, pxs);
                        if (_selectionRect.Width != 0 && _selectionRect.Height != 0)
                            DrawOverlay(ctx, _selectionRect);
                    }
                    mw.UpdateRulerSelection();
                }
                else if (_draggingSelection) // 拖动逻辑
                {

                    if (!_hasLifted) LiftSelectionFromCanvas(ctx);

                    var mainWindow = mw;
                    if (mainWindow != null)
                    {
                        Point posInWindow = ctx.ViewElement.TranslatePoint(viewPos, mainWindow);
                        double margin = -5;
                        bool isOutside = posInWindow.X < margin ||
                                         posInWindow.Y < margin ||
                                         posInWindow.X > mainWindow.ActualWidth - margin ||
                                         posInWindow.Y > mainWindow.ActualHeight - margin;
                        if (isOutside)
                        {
                            ctx.ViewElement.ReleaseMouseCapture();
                            StartDragDropOperation(ctx);
                            _draggingSelection = false;
                            return;
                        }
                    }
                    int newX = (int)(px.X - _clickOffset.X);
                    int newY = (int)(px.Y - _clickOffset.Y);
                    int dx = newX - _selectionRect.X;
                    int dy = newY - _selectionRect.Y;
                    _selectionRect.X = newX;
                    _selectionRect.Y = newY;

                    if (_preRotationSelectionData != null)
                    {
                        _preRotationRect.X += dx;
                        _preRotationRect.Y += dy;
                    }

                    mw.UpdateSelectionToolBarPosition();

                    double dpiScaleX = AppConsts.StandardDpi / ctx.Surface.Bitmap.DpiX;
                    double dpiScaleY = AppConsts.StandardDpi / ctx.Surface.Bitmap.DpiY;
                    double diff = mw.CanvasWrapper.RenderSize.Width - (int)mw.CanvasWrapper.RenderSize.Width;

                    var tg = ctx.SelectionPreview.RenderTransform as TransformGroup;
                    if (tg != null)
                    {
                        var t = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
                        if (t != null)
                        {
                            t.X = newX * dpiScaleX + diff * 0.75;
                            t.Y = newY * dpiScaleY;
                        }
                    }
                    else if (ctx.SelectionPreview.RenderTransform is TranslateTransform singleT)
                    {
                        singleT.X = newX * dpiScaleX + diff * 0.75;
                        singleT.Y = newY * dpiScaleY;
                    }


                    ctx.SelectionPreview.Clip = null;

                    Int32Rect tmprc = new Int32Rect(newX, newY, _selectionRect.Width, _selectionRect.Height);

                    double canvasW = ctx.Surface.Bitmap.PixelWidth;
                    double canvasH = ctx.Surface.Bitmap.PixelHeight;

                    // 选区左上角相对于画布的偏移
                    double offsetX = tmprc.X;
                    double offsetY = tmprc.Y;

                    double ratioX = (double)_selectionRect.Width / (double)_originalRect.Width;
                    double ratioY = (double)_selectionRect.Height / (double)_originalRect.Height;
                    double visibleX = (int)Math.Max(0, -offsetX / ratioX);
                    double visibleY = (int)Math.Max(0, -offsetY / ratioY);


                    double visibleW = Math.Max(0, Math.Min(tmprc.Width / ratioX, (canvasW - offsetX) / ratioX));
                    double visibleH = Math.Max(0, Math.Min(tmprc.Height / ratioY, (canvasH - offsetY) / ratioY));

                    Int32Rect intRect = ClampRect(new Int32Rect((int)visibleX, (int)visibleY, (int)visibleW, (int)visibleH), ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);
                    Rect rect = new Rect(intRect.X, intRect.Y, intRect.Width, intRect.Height);
                    Geometry visibleRect = new RectangleGeometry(rect);
                    if (visibleW > 0 && visibleH > 0)
                    {
                        // 确保 X 和 Y 也是合法的（虽然通常 offsetX 逻辑不会错，但为了保险）
                        double validX = Math.Max(0, visibleX);
                        double validY = Math.Max(0, visibleY);

                        ctx.SelectionPreview.Clip = new RectangleGeometry(new Rect(validX, validY, visibleW, visibleH));

                        // 确保可见
                        if (ctx.SelectionPreview.Visibility != Visibility.Visible)
                            ctx.SelectionPreview.Visibility = Visibility.Visible;
                    }
                    else ctx.SelectionPreview.Clip = Geometry.Empty;
                    DrawOverlay(ctx, tmprc); mw.UpdateRulerSelection();

                }

                UpdateStatusBarSelectionSize(mw);
            }

            public void UpdateStatusBarSelectionSize(MainWindow mw)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {// 状态栏更新
                    mw.SelectionSize =
                        $"{_selectionRect.Width}×{_selectionRect.Height}" + LocalizationManager.GetString("L_Main_Unit_Pixel");
                });
            }
            public void LiftSelectionFromCanvas(ToolContext ctx)
            {
                if (_hasLifted) return;

                // 1. Undo 记录
                ctx.Undo.BeginStroke();
                ctx.Undo.AddDirtyRect(_originalRect);
                ctx.Undo.CommitStroke();

                // 2. 执行清除 (区分矩形模式和套索模式)
                if (IsIrregularSelection && _selectionAlphaMap != null)
                {
                    ClearLassoRegion(ctx, ClampRect(_originalRect, ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight), ctx.EraserColor);
                }
                else ClearRect(ctx, ClampRect(_originalRect, ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight), ctx.EraserColor);
                _hasLifted = true;
            }

            public override void OnKeyDown(ToolContext ctx, System.Windows.Input.KeyEventArgs e)
            {
                if (ctx.ParentWindow.IsViewMode) return;
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    switch (e.Key)
                    {
                        case Key.X:
                            //public ITool CurrentTool => Select;
                            CutSelection(ctx, true);
                            e.Handled = true;
                            break;
                        case Key.C:
                            e.Handled = true;
                            CopySelection(ctx);
                            break;
                    }
                }
                else
                {
                    switch (e.Key)
                    {
                        case Key.Delete:
                            DeleteSelection(ctx);
                            e.Handled = true;
                            break;
                        case Key.Escape:
                            GiveUpSelection(ctx);
                          //  Cleanup(ctx);
                            e.Handled = true;
                            break;
                    }
                }
            }

            public void PrepareRotation(ToolContext ctx)
            {
                if (_selectionData == null) return;
                _rotationAngle = 0;
                if (!_hasLifted) LiftSelectionFromCanvas(ctx);

                // 如果有缩放，先应用缩放 (Bake current scale into pixels)
                if (_originalRect.Width > 0 && (_selectionRect.Width != _originalRect.Width || _selectionRect.Height != _originalRect.Height))
                {
                    var mw = ctx.ParentWindow;
                    var bmp = GetSelectionCroppedBitmap(mw);
                    if (bmp != null)
                    {
                        int w = bmp.PixelWidth;
                        int h = bmp.PixelHeight;
                        int stride = w * 4;
                        byte[] pixels = new byte[h * stride];
                        bmp.CopyPixels(pixels, stride, 0);
                        _selectionData = pixels;
                        _originalRect = _selectionRect;
                    }
                }

                // 备份原始状态以便实时旋转
                _preRotationSelectionData = (byte[])_selectionData.Clone();
                _preRotationDataWidth = _selectionRect.Width;
                _preRotationDataHeight = _selectionRect.Height;
                _preRotationRect = _selectionRect;
                _originalRect = _selectionRect;
                _transformStep++;
            }

            public void UpdateRotation(ToolContext ctx, int angle, bool commit)
            {
                if (_preRotationSelectionData == null || ctx == null) return;
                _rotationAngle = angle;

                // 强制限制角度在 [-180, 180] 范围内，避免 UI 滑块等引起的问题
                if (_rotationAngle > 180) _rotationAngle -= 360;
                if (_rotationAngle < -180) _rotationAngle += 360;

                // 性能优化：矩形选区在预览阶段不重绘像素，仅使用 RenderTransform
                // 注意：由于不应更改套索和魔棒逻辑，此处仅针对矩形选区进行预览优化
                if (!commit && SelectionType == SelectionType.Rectangle)
                {
                    UpdatePreviewTransform(ctx);

                    // DrawOverlay 内部已处理 _preRotationSelectionData != null 时的旋转虚线框绘制
                    DrawOverlay(ctx, _selectionRect);

                    var mw = ctx.ParentWindow;
                    mw.UpdateSelectionToolBarPosition();
                    mw.SelectionSize = $"{_preRotationRect.Width}×{_preRotationRect.Height}" + LocalizationManager.GetString("L_Main_Unit_Pixel");
                    mw.UpdateRulerSelection();
                    return;
                }

                // 提交路径：执行高质量像素重绘 (RenderTargetBitmap)
                int dataW = _preRotationDataWidth;
                int dataH = _preRotationDataHeight;
                int stride = dataW * 4;

                var srcBmp = BitmapSource.Create(
                    dataW, dataH,
                    ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                    PixelFormats.Bgra32, null,
                    _preRotationSelectionData, stride);

                double targetW = _preRotationRect.Width;
                double targetH = _preRotationRect.Height;

                var rotateTransform = new RotateTransform(angle, targetW / 2.0, targetH / 2.0);
                Rect rect = new Rect(0, 0, targetW, targetH);
                Rect rotatedBounds = rotateTransform.TransformBounds(rect);

                int newW = (int)Math.Ceiling(rotatedBounds.Width);
                int newH = (int)Math.Ceiling(rotatedBounds.Height);
                if (newW <= 0) newW = 1;
                if (newH <= 0) newH = 1;

                DrawingVisual dv = new DrawingVisual();
                using (DrawingContext dc = dv.RenderOpen())
                {
                    RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.HighQuality);
                    dc.PushTransform(new TranslateTransform(-rotatedBounds.X, -rotatedBounds.Y));
                    dc.PushTransform(new RotateTransform(angle, targetW / 2.0, targetH / 2.0));
                    dc.DrawImage(srcBmp, rect);
                }

                RenderTargetBitmap rtb = new RenderTargetBitmap(
                    newW, newH,
                    ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY,
                    PixelFormats.Pbgra32);
                rtb.Render(dv);

                var finalBmp = new FormatConvertedBitmap(rtb, PixelFormats.Bgra32, null, 0);

                double centerX = _preRotationRect.X + _preRotationRect.Width / 2.0;
                double centerY = _preRotationRect.Y + _preRotationRect.Height / 2.0;
                int newX = (int)Math.Round(centerX - newW / 2.0);
                int newY = (int)Math.Round(centerY - newH / 2.0);

                int newStride = newW * 4;
                byte[] newPixels = new byte[newH * newStride];
                finalBmp.CopyPixels(newPixels, newStride, 0);

                _selectionData = newPixels;
                _selectionRect = new Int32Rect(newX, newY, newW, newH);
                _originalRect = _selectionRect;

                if (_selectionAlphaMap != null)
                {
                    _selectionAlphaMap = new byte[newPixels.Length];
                    for (int i = 0; i < newPixels.Length; i += 4)
                    {
                        _selectionAlphaMap[i + 3] = newPixels[i + 3];
                    }
                }

                ctx.SelectionPreview.Source = finalBmp;
                double dpiScaleX_rt = AppConsts.StandardDpi / ctx.Surface.Bitmap.DpiX;
                double dpiScaleY_rt = AppConsts.StandardDpi / ctx.Surface.Bitmap.DpiY;
                double diffBaked = ctx.ParentWindow.CanvasWrapper.RenderSize.Width - (int)ctx.ParentWindow.CanvasWrapper.RenderSize.Width;
                ctx.SelectionPreview.RenderTransform = new TranslateTransform(newX * dpiScaleX_rt + diffBaked * 0.75, newY * dpiScaleY_rt);
                ctx.SelectionPreview.Width = newW * dpiScaleX_rt;
                ctx.SelectionPreview.Height = newH * dpiScaleY_rt;
                ctx.SelectionPreview.Visibility = Visibility.Visible;

                DrawOverlay(ctx, _selectionRect);
                var mwCommit = ctx.ParentWindow;
                mwCommit.UpdateSelectionToolBarPosition();
                mwCommit.SelectionSize = $"{_selectionRect.Width}×{_selectionRect.Height}" + LocalizationManager.GetString("L_Main_Unit_Pixel");
                mwCommit.UpdateRulerSelection();

                if (commit)
                {
                    _preRotationSelectionData = null;
                    _rotationAngle = 0;
                    ctx.IsDirty = true;
                }
            }

            private void EnsureRotationBaked(ToolContext ctx)
            {
                if (_preRotationSelectionData != null && Math.Abs(_rotationAngle) > 0.01)
                {
                    UpdateRotation(ctx, (int)_rotationAngle, true);
                }
            }

            public void UpdatePreviewTransform(ToolContext ctx)
            {
                if (_selectionData == null || ctx == null) return;

                double dpiScaleX = AppConsts.StandardDpi / ctx.Surface.Bitmap.DpiX;
                double dpiScaleY = AppConsts.StandardDpi / ctx.Surface.Bitmap.DpiY;
                double diff = ctx.ParentWindow.CanvasWrapper.RenderSize.Width - (int)ctx.ParentWindow.CanvasWrapper.RenderSize.Width;

                if (_preRotationSelectionData != null && SelectionType == SelectionType.Rectangle)
                {
                    // 旋转预览路径 (针对矩形选区的性能优化)
                    double logicW = _preRotationRect.Width;
                    double logicH = _preRotationRect.Height;
                    double localX = _preRotationRect.X * dpiScaleX + diff * 0.75;
                    double localY = _preRotationRect.Y * dpiScaleY;

                    var tg = new TransformGroup();
                    tg.Children.Add(new RotateTransform(_rotationAngle, (logicW * dpiScaleX) / 2.0, (logicH * dpiScaleY) / 2.0));
                    tg.Children.Add(new TranslateTransform(localX, localY));

                    ctx.SelectionPreview.RenderTransform = tg;
                    ctx.SelectionPreview.Width = logicW * dpiScaleX;
                    ctx.SelectionPreview.Height = logicH * dpiScaleY;
                }
                else
                {
                    // 普通路径 (拖拽、普通缩放、或已烘焙像素的旋转)
                    ctx.SelectionPreview.RenderTransform = new TranslateTransform(_selectionRect.X * dpiScaleX + diff * 0.75, _selectionRect.Y * dpiScaleY);
                }
            }

            public void RotateSelection(ToolContext ctx, int angle)
            {
                PrepareRotation(ctx);
                UpdateRotation(ctx, angle, true);
            }
            public BitmapSource GetSelectionCroppedBitmap(MainWindow mw)
            {
                if (_selectionData == null || _originalRect.Width <= 0 || _originalRect.Height <= 0)
                    return null;

                try
                {
                    int stride = _originalRect.Width * 4;
                    double dpiX = mw._surface?.Bitmap.DpiX ?? AppConsts.StandardDpi;
                    double dpiY = mw._surface?.Bitmap.DpiY ?? AppConsts.StandardDpi;

                    BitmapSource result = BitmapSource.Create(_originalRect.Width, _originalRect.Height, dpiX, dpiY,
                        PixelFormats.Bgra32, null, _selectionData, stride);

                    if (_selectionRect.Width != _originalRect.Width || _selectionRect.Height != _originalRect.Height)
                    {
                        double scaleX = (double)_selectionRect.Width / _originalRect.Width;
                        double scaleY = (double)_selectionRect.Height / _originalRect.Height;

                        // 使用 TransformedBitmap 进行高质量缩放
                        result = new TransformedBitmap(result, new ScaleTransform(scaleX, scaleY));
                    }
                    result.Freeze();
                    return result;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("OCR 裁剪失败: " + ex.Message);
                    return null;
                }
            }

            private void CreatePreviewFromSelectionData(ToolContext ctx)
            {
                var mw = ctx.ParentWindow;
                var previewBmp = new WriteableBitmap(_selectionRect.Width, _selectionRect.Height,
                    ctx.Surface.Bitmap.DpiX, ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);

                previewBmp.WritePixels(new Int32Rect(0, 0, _selectionRect.Width, _selectionRect.Height),
                                       _selectionData, _selectionRect.Width * 4, 0);

                ctx.SelectionPreview.Source = previewBmp;
                ctx.SelectionPreview.Clip = null;

                SetPreviewPosition(ctx, _selectionRect.X, _selectionRect.Y);
                mw.UpdateSelectionScalingMode();
                ctx.SelectionPreview.Visibility = Visibility.Visible;
                UpdateStatusBarSelectionSize(mw);

                DrawOverlay(ctx, _selectionRect);
                mw.SetCropButtonState();
            }
            public Rect GetViewportInPixelCoords(ToolContext ctx)
            {
                var mw = ctx.ParentWindow;
                var sv = mw.ScrollContainer; // 你的 ScrollViewer 引用
                if (sv == null) return Rect.Empty;
                Point topLeft = new Point(sv.HorizontalOffset, sv.VerticalOffset);
                Point bottomRight = new Point(
                    sv.HorizontalOffset + sv.ViewportWidth,
                    sv.VerticalOffset + sv.ViewportHeight);
                Point pxTL = ctx.ToPixel(topLeft);
                Point pxBR = ctx.ToPixel(bottomRight);

                return new Rect(pxTL, pxBR);
            }

            public override void OnPointerUp(ToolContext ctx, Point viewPos, float pressure = 1.0f)
            {
                if (lag > 0) { lag--; return; }
                ctx.ViewElement.ReleaseMouseCapture();
                var px = ctx.ToPixel(viewPos);
                var mw = ctx.ParentWindow;
                if (_selecting && SelectionType == SelectionType.MagicWand)
                {
                    _selecting = false;
                    _isWandAdjusting = false;

                    // 释放鼠标时，执行最后一次魔棒算法以提取正式数据
                    bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                    RunMagicWand(ctx, _wandStartPoint, _wandTolerance, isShift);

                    if (_selectionAlphaMap != null && _selectionRect.Width > 0 && _selectionRect.Height > 0)
                    {
                        _originalRect = _selectionRect;

                        double zoom = mw.zoomscale; Rect? viewport = null;

                        // 放大时传入视口，缩小时传 null（降采样处理全图）
                        if (zoom >= 1.0)
                        {
                            viewport = GetViewportInPixelCoords(ctx);
                        }

                        _selectionGeometry = GeneratePixelEdgeGeometry(
                            _selectionAlphaMap,
                            _selectionRect.Width,
                            _selectionRect.Height,
                            _selectionRect.X,
                            _selectionRect.Y,
                            zoom,
                            viewport);

                        UpdateStatusBarSelectionSize(mw);
                        DrawOverlay(ctx, _selectionRect);
                    }
                    else
                    {
                        Cleanup(ctx);
                    }
                    mw.SetCropButtonState();
                    return;
                }
                if (_selecting)
                {
                    _selecting = false;
                    if (SelectionType == SelectionType.Lasso) ProcessLassoSelection(ctx);
                    else
                    {
                        var rawRect = MakeRect(_startPixel, px);
                        _selectionRect = ClampRect(rawRect, ctx.Surface.Bitmap.PixelWidth, ctx.Surface.Bitmap.PixelHeight);

                        if (_selectionRect.Width > 0 && _selectionRect.Height > 0)
                        {
                            _selectionData = ctx.Surface.ExtractRegion(_selectionRect);

                            // 4. 记录原始尺寸
                            _originalRect = _selectionRect;
                            CreatePreviewFromSelectionData(ctx);
                        }
                        else
                        {
                            Cleanup(ctx);
                        }
                    }
                }
                else if (_draggingSelection)
                {
                    _draggingSelection = false;
                    // 已在 OnPointerMove 中实时更新了 _selectionRect 和 _preRotationRect，无需基于 TranslatePoint 重算
                }

                if (_resizing)
                {
                    _resizing = false;
                    _currentAnchor = ResizeAnchor.None;
                    return;
                }
                if (_selectionRect.Width != 0 && _selectionRect.Height != 0)
                {
                    DrawOverlay(ctx, _selectionRect); mw.UpdateSelectionToolBarPosition();
                }
                mw.SetCropButtonState(); mw.UpdateRulerSelection();

            }

        }
    }
}
