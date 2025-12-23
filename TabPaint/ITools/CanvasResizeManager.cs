using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {

        public enum ResizeAnchor
        {
            None,
            TopLeft, TopMiddle, TopRight,
            LeftMiddle, RightMiddle,
            BottomLeft, BottomMiddle, BottomRight
        }


        public class CanvasResizeManager
        {
            private readonly MainWindow _mainWindow;
            private readonly Canvas _overlay;
            private bool _isResizing = false;
            private ResizeAnchor _currentAnchor = ResizeAnchor.None;
            private Point _startDragPoint;
            private Int32Rect _startRect; // 拖拽开始时的画布尺寸
            private Rectangle _previewBorder; // 拖拽时的虚线框

            // 样式配置
            private const double HandleSize = 8.0;

            public CanvasResizeManager(MainWindow window)
            {
                _mainWindow = window;
                _overlay = _mainWindow.CanvasResizeOverlay;
            }

            // 每次缩放或画布改变大小时调用此方法刷新 UI
            public void UpdateUI()
            {
                _overlay.Children.Clear();

                // 获取当前画布尺寸
                double w = ((MainWindow)System.Windows.Application.Current.MainWindow).BackgroundImage.Source.Width;
                double h = ((MainWindow)System.Windows.Application.Current.MainWindow).BackgroundImage.Source.Height;

                // 确保 Overlay 大小与图片一致
                _overlay.Width = w;
                _overlay.Height = h;

                double scale = ((MainWindow)System.Windows.Application.Current.MainWindow).zoomscale;
                double invScale = 1.0 / scale;
                double size = HandleSize * invScale;

         
                // 2. 绘制 8 个手柄
                var handles = GetHandlePositions(w, h);
                foreach (var kvp in handles)
                {
                    var rect = new Rectangle
                    {
                        Width = size,
                        Height = size,
                        Fill = Brushes.White,
                        Stroke = new SolidColorBrush(Color.FromRgb(160, 160, 160)), // 建议改用浅灰色
                        StrokeThickness = 1 * invScale,
                        Tag = kvp.Key, // 存储锚点类型
                        Cursor = GetCursor(kvp.Key)
                    };

                    // 居中定位
                    Canvas.SetLeft(rect, kvp.Value.X - size / 2);
                    Canvas.SetTop(rect, kvp.Value.Y - size / 2);

                    // 绑定事件
                    rect.MouseLeftButtonDown += OnHandleDown;
                    rect.MouseLeftButtonUp += OnHandleUp;
                    rect.MouseMove += OnHandleMove;

                    _overlay.Children.Add(rect);
                }
            }

            private void OnHandleDown(object sender, MouseButtonEventArgs e)
            {
                var rect = sender as Rectangle;
                _currentAnchor = (ResizeAnchor)rect.Tag;
                _isResizing = true;
                _startDragPoint = e.GetPosition(((MainWindow)System.Windows.Application.Current.MainWindow).CanvasWrapper); // 获取相对于 Grid 的坐标

                // 记录原始尺寸
                var bmp = ((MainWindow)System.Windows.Application.Current.MainWindow).BackgroundImage.Source as BitmapSource;
                _startRect = new Int32Rect(0, 0, (int)bmp.PixelWidth, (int)bmp.PixelHeight);

                // 捕获鼠标
                rect.CaptureMouse();

                // 创建预览虚线框
                CreatePreviewBorder();
                e.Handled = true;
            }

            private void OnHandleMove(object sender, MouseEventArgs e)
            {
                if (!_isResizing) return;

                var currentPoint = e.GetPosition(((MainWindow)System.Windows.Application.Current.MainWindow).CanvasWrapper);
                var rect = CalculateNewRect(currentPoint);

                // 更新预览框位置和大小
                Canvas.SetLeft(_previewBorder, rect.X);
                Canvas.SetTop(_previewBorder, rect.Y);
                _previewBorder.Width = Math.Max(1, rect.Width);
                _previewBorder.Height = Math.Max(1, rect.Height);
            }

            private void OnHandleUp(object sender, MouseButtonEventArgs e)
            {
                if (!_isResizing) return;

                var rect = sender as Rectangle;
                rect.ReleaseMouseCapture();
                _isResizing = false;

                // 计算最终矩形
                var currentPoint = e.GetPosition(((MainWindow)System.Windows.Application.Current.MainWindow).CanvasWrapper);
                var finalRect = CalculateNewRect(currentPoint); // 这里拿到的是相对于原图左上角的 Rect

                // 移除预览框
                if (_previewBorder != null)
                {
                    _overlay.Children.Remove(_previewBorder);
                    _previewBorder = null;
                }

                // 提交更改
                ApplyResize(finalRect);
            }

            private Rect CalculateNewRect(Point currentMouse)
            {
                double dx = currentMouse.X - _startDragPoint.X;
                double dy = currentMouse.Y - _startDragPoint.Y;

                double x = 0, y = 0, w = _startRect.Width, h = _startRect.Height;

                // 根据锚点计算
                // 注意：向左/上拉时，x/y 会变成负数，这是相对于原始 (0,0) 的坐标
                switch (_currentAnchor)
                {
                    case ResizeAnchor.RightMiddle: w += dx; break;
                    case ResizeAnchor.BottomMiddle: h += dy; break;
                    case ResizeAnchor.BottomRight: w += dx; h += dy; break;

                    case ResizeAnchor.LeftMiddle: x += dx; w -= dx; break;
                    case ResizeAnchor.TopMiddle: y += dy; h -= dy; break;

                    case ResizeAnchor.TopLeft: x += dx; y += dy; w -= dx; h -= dy; break;
                    case ResizeAnchor.TopRight: y += dy; w += dx; h -= dy; break;
                    case ResizeAnchor.BottomLeft: x += dx; w -= dx; h += dy; break;
                }

                return new Rect(x, y, Math.Max(1, w), Math.Max(1, h));
            }

            private void CreatePreviewBorder()
            {
                double invScale = 1.0 / ((MainWindow)System.Windows.Application.Current.MainWindow).zoomscale;
                _previewBorder = new Rectangle
                {
                    Stroke = Brushes.Black,
                    StrokeDashArray = new DoubleCollection { 4, 4 },
                    StrokeThickness = 1 * invScale,
                    IsHitTestVisible = false
                };
                _overlay.Children.Add(_previewBorder);
            }

            private void ApplyResize(Rect newBounds)
            {
                // newBounds.X / Y 表示原点偏移量。
                // 如果 X = -50，表示向左扩展了 50px，原图应该画在 (50, 0) 处。
                // 如果 X = 50，表示向右裁切了 50px，原图应该画在 (-50, 0) 处。

                int newW = (int)newBounds.Width;
                int newH = (int)newBounds.Height;
                int offsetX = -(int)newBounds.X;
                int offsetY = -(int)newBounds.Y;

                if (newW <= 0 || newH <= 0) return;

                // 1. 获取当前图像数据 (Undo 需要)
                var currentBmp = ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Surface.Bitmap; // 假设这是当前的 WriteableBitmap
                var rect = new Int32Rect(0, 0, currentBmp.PixelWidth, currentBmp.PixelHeight);

                // 获取全图数据用于 Undo
                byte[] oldPixels = ((MainWindow)System.Windows.Application.Current.MainWindow)._undo.SafeExtractRegion(rect);

                // 2. 创建新位图
                var newBmp = new WriteableBitmap(newW, newH, currentBmp.DpiX, currentBmp.DpiY, PixelFormats.Bgra32, null);

                // 填充白色背景 (如果不填充，默认为透明)
                // 你可能需要一个 FillRect 方法，这里简化处理
                byte[] whiteBg = new byte[newW * newH * 4];
                for (int i = 0; i < whiteBg.Length; i++) whiteBg[i] = 255;
                newBmp.WritePixels(new Int32Rect(0, 0, newW, newH), whiteBg, newBmp.BackBufferStride, 0);

                // 3. 将旧图像绘制到新图像的指定偏移位置
                // 计算重叠区域
                int copyX = Math.Max(0, offsetX); // 在新图中的起始X
                int copyY = Math.Max(0, offsetY); // 在新图中的起始Y

                // 源图中需要复制的区域 (处理裁剪)
                int srcX = offsetX < 0 ? -offsetX : 0; // 如果 offsetX 是正数(裁剪)，源图从 srcX 开始
                int srcY = offsetY < 0 ? -offsetY : 0;

                int copyW = Math.Min(rect.Width - srcX, newW - copyX);
                int copyH = Math.Min(rect.Height - srcY, newH - copyY);

                if (copyW > 0 && copyH > 0)
                {
                    var srcRect = new Int32Rect(srcX, srcY, copyW, copyH);
                    var srcPixels = ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Surface.ExtractRegion(srcRect); // 这里需要支持从 Surface 获取指定区域

                    newBmp.WritePixels(new Int32Rect(copyX, copyY, copyW, copyH), srcPixels, copyW * 4, 0);
                }

                // 获取新图的全数据用于 Redo
                byte[] newPixels = new byte[newW * newH * 4];
                newBmp.CopyPixels(newPixels, newBmp.BackBufferStride, 0);

                // 4. 执行替换并记录 Undo
                // 注意：这里需要修改你的 UndoRedoManager 以支持 "ReplaceBitmap" 这种操作
                // 或者使用 TransformAction

                ((MainWindow)System.Windows.Application.Current.MainWindow)._undo.PushTransformAction(
                    rect, oldPixels,                // Undo: 回到旧尺寸，旧像素
                    new Int32Rect(0, 0, newW, newH), newPixels // Redo: 回到新尺寸，新像素
                );

                // 5. 替换当前显示的位图
                ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Surface.ReplaceBitmap(newBmp);

                // 6. 刷新界面
                UpdateUI();
            }

            private Dictionary<ResizeAnchor, Point> GetHandlePositions(double w, double h)
            {
                return new Dictionary<ResizeAnchor, Point>
            {
                { ResizeAnchor.TopLeft, new Point(0, 0) },
                { ResizeAnchor.TopMiddle, new Point(w/2, 0) },
                { ResizeAnchor.TopRight, new Point(w, 0) },
                { ResizeAnchor.LeftMiddle, new Point(0, h/2) },
                { ResizeAnchor.RightMiddle, new Point(w, h/2) },
                { ResizeAnchor.BottomLeft, new Point(0, h) },
                { ResizeAnchor.BottomMiddle, new Point(w/2, h) },
                { ResizeAnchor.BottomRight, new Point(w, h) },
            };
            }

            private Cursor GetCursor(ResizeAnchor anchor)
            {
                switch (anchor)
                {
                    case ResizeAnchor.TopLeft: return Cursors.SizeNWSE;
                    case ResizeAnchor.TopMiddle: return Cursors.SizeNS;
                    case ResizeAnchor.TopRight: return Cursors.SizeNESW;
                    case ResizeAnchor.LeftMiddle: return Cursors.SizeWE;
                    case ResizeAnchor.RightMiddle: return Cursors.SizeWE;
                    case ResizeAnchor.BottomLeft: return Cursors.SizeNESW;
                    case ResizeAnchor.BottomMiddle: return Cursors.SizeNS;
                    case ResizeAnchor.BottomRight: return Cursors.SizeNWSE;
                    default: return Cursors.Arrow;
                }
            }
        }
    }
}
