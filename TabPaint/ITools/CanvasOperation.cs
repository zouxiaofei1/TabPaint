using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using static TabPaint.MainWindow;

//
//关于图片的一些操作方法，
//原来被大量放在Mainwindow.cs里
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void ApplyTransform(System.Windows.Media.Transform transform)
        {
            if (BackgroundImage.Source is not BitmapSource src || _surface?.Bitmap == null)
                return;

            var undoRect = new Int32Rect(0, 0, _surface.Bitmap.PixelWidth, _surface.Bitmap.PixelHeight); // --- 1. 捕获变换前的状态 (for UNDO) ---
            var undoPixels = _surface.ExtractRegion(undoRect);
            if (undoPixels == null) return; // 如果提取失败则中止
            var transformedBmp = new TransformedBitmap(src, transform); // --- 2. 计算并生成变换后的新位图 (这是 REDO 的目标状态) ---
            var newBitmap = new WriteableBitmap(transformedBmp);

            var redoRect = new Int32Rect(0, 0, newBitmap.PixelWidth, newBitmap.PixelHeight);  // --- 3. 捕获变换后的状态 (for REDO) ---
            int redoStride = newBitmap.BackBufferStride;
            var redoPixels = new byte[redoStride * redoRect.Height];
            newBitmap.CopyPixels(redoPixels, redoStride, 0);

            _bitmap = newBitmap;
            BackgroundImage.Source = _bitmap;
            _surface.Attach(_bitmap);
            _surface.ReplaceBitmap(_bitmap);

            _undo.PushTransformAction(undoRect, undoPixels, redoRect, redoPixels);   // --- 5. newBitmap.PixelWidth Undo 栈 ---
            ((MainWindow)System.Windows.Application.Current.MainWindow).NotifyCanvasSizeChanged(newBitmap.PixelWidth, newBitmap.PixelHeight);
            SetUndoRedoButtonState();
        }

        private void RotateBitmap(int angle)
        {
            var mw = (MainWindow)Application.Current.MainWindow;
            // 1. 检查当前工具是否为 SelectTool 且有活动选区
            if (_tools.Select is SelectTool st && st.HasActiveSelection)
            {
                // 调用选区旋转
                st.RotateSelection(_ctx, angle);
                    return; // 结束，不旋转画布
            }
            if (mw._router.CurrentTool is ShapeTool shapetool && mw._router.GetSelectTool()?._selectionData != null)
            {
                mw._router.GetSelectTool()?.RotateSelection(_ctx, angle);
                return; // 结束，不旋转画布
            }

            // 2. 原有的画布旋转逻辑
            ApplyTransform(new RotateTransform(angle));
            NotifyCanvasChanged();
            _canvasResizer.UpdateUI(); 
        }


        private void FlipBitmap(bool flipVertical)
        {
            double cx = _bitmap.PixelWidth / 2.0;
            double cy = _bitmap.PixelHeight / 2.0;
            ApplyTransform(flipVertical ? new ScaleTransform(1, -1, cx, cy) : new ScaleTransform(-1, 1, cx, cy));
        }
        private BitmapSource CreateWhiteThumbnail()  // 辅助方法：生成纯白缩略图
        {
            int w = 100; int h = 60;
            var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var ctx = visual.RenderOpen())
            {
                ctx.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, h));
                // 可以在中间画个加号或者 "New" 字样
            }
            bmp.Render(visual);
            bmp.Freeze();
            return bmp;
        }

        private byte[] ExtractRegionFromSnapshot(byte[] fullData, Int32Rect rect, int stride)
        {
            int bytesPerPixel = 4;
            byte[] region = new byte[rect.Height * rect.Width * bytesPerPixel];

            for (int row = 0; row < rect.Height; row++)
            {
                int srcOffset = (rect.Y + row) * stride + rect.X * bytesPerPixel;
                int dstOffset = row * rect.Width * bytesPerPixel;
                Buffer.BlockCopy(fullData, srcOffset, region, dstOffset, rect.Width * bytesPerPixel);
            }
            return region;
        }
        private static Int32Rect ClampRect(Int32Rect rect, int maxWidth, int maxHeight)
        {
            // 1. 计算左上角和右下角的边界坐标
            int left = Math.Max(0, rect.X);
            int top = Math.Max(0, rect.Y);

            // 2. 计算右边界和下边界（不能超过最大宽高）
            int right = Math.Min(maxWidth, rect.X + rect.Width);
            int bottom = Math.Min(maxHeight, rect.Y + rect.Height);

            // 3. 计算新的宽高。确保结果不为负数
            int width = Math.Max(0, right - left);
            int height = Math.Max(0, bottom - top);

            return new Int32Rect(left, top, width, height);
        }

        private void ClearRect(ToolContext ctx, Int32Rect rect, Color color)
        {
            ctx.Surface.Bitmap.Lock();
            unsafe
            {
                byte* basePtr = (byte*)ctx.Surface.Bitmap.BackBuffer;
                int stride = ctx.Surface.Bitmap.BackBufferStride;
                for (int y = rect.Y; y < rect.Y + rect.Height; y++)
                {
                    byte* rowPtr = basePtr + y * stride + rect.X * 4;
                    for (int x = 0; x < rect.Width; x++)
                    {
                        rowPtr[0] = color.B;
                        rowPtr[1] = color.G;
                        rowPtr[2] = color.R;
                        rowPtr[3] = color.A;
                        rowPtr += 4;
                    }
                }
            }
            ctx.Surface.Bitmap.AddDirtyRect(rect);
            ctx.Surface.Bitmap.Unlock();
        }
        private void Clean_bitmap(int _bmpWidth, int _bmpHeight)
        {
            _bitmap = new WriteableBitmap(_bmpWidth, _bmpHeight, 96, 96, PixelFormats.Bgra32, null);
            BackgroundImage.Source = _bitmap;

            // 填充白色背景
            _bitmap.Lock();

            if (_undo != null)
            {
                _undo.ClearUndo();
                _undo.ClearRedo();
            }

            if (_surface == null)
                _surface = new CanvasSurface(_bitmap);
            else
                _surface.Attach(_bitmap);
            unsafe
            {
                IntPtr pBackBuffer = _bitmap.BackBuffer;
                int stride = _bitmap.BackBufferStride;
                for (int y = 0; y < _bmpHeight; y++)
                {
                    byte* row = (byte*)pBackBuffer + y * stride;
                    for (int x = 0; x < _bmpWidth; x++)
                    {
                        row[x * 4 + 0] = 255; // B
                        row[x * 4 + 1] = 255; // G
                        row[x * 4 + 2] = 255; // R
                        row[x * 4 + 3] = 255; // A
                    }
                }
            }
            _bitmap.AddDirtyRect(new Int32Rect(0, 0, _bmpWidth, _bmpHeight));
            _bitmap.Unlock();

            // 调整窗口和画布大小
            double imgWidth = _bitmap.Width;
            double imgHeight = _bitmap.Height;

            NotifyCanvasSizeChanged(imgWidth, imgHeight);
            UpdateWindowTitle();

            FitToWindow();
            SetBrushStyle(BrushStyle.Round);
        }

        public void NotifyCanvasSizeChanged(double pixwidth, double pixheight)
        {
            BackgroundImage.Width = pixwidth;
            BackgroundImage.Height = pixheight;
            _imageSize = $"{pixwidth}×{pixheight}像素";
            OnPropertyChanged(nameof(ImageSize));
            UpdateWindowTitle();
        }
        private void ResizeCanvas(int newWidth, int newHeight)
        {
            var oldBitmap = _surface.Bitmap;
            if (oldBitmap == null) return; // 如果尺寸没有变化，则不执行任何操作
            if (oldBitmap.PixelWidth == newWidth && oldBitmap.PixelHeight == newHeight) return;

            var undoRect = new Int32Rect(0, 0, oldBitmap.PixelWidth, oldBitmap.PixelHeight);  // --- 1. 捕获变换前的完整状态 (for UNDO) ---
            var undoPixels = new byte[oldBitmap.PixelHeight * oldBitmap.BackBufferStride];
            // 从旧位图复制像素
            oldBitmap.CopyPixels(undoRect, undoPixels, oldBitmap.BackBufferStride, 0);

            var transform = new ScaleTransform(// --- 2. 创建新的、缩放后的位图 ---
                (double)newWidth / oldBitmap.PixelWidth, // 创建一个变换，指定缩放比例
                (double)newHeight / oldBitmap.PixelHeight
            );

            var transformedBitmap = new TransformedBitmap(oldBitmap, transform);    // 应用变换
            RenderOptions.SetBitmapScalingMode(transformedBitmap, BitmapScalingMode.NearestNeighbor);

            // 将结果转换为一个新的 WriteableBitmap
            var newFormatedBitmap = new FormatConvertedBitmap(transformedBitmap, PixelFormats.Bgra32, null, 0);
            var newBitmap = new WriteableBitmap(newFormatedBitmap);

            var redoRect = new Int32Rect(0, 0, newBitmap.PixelWidth, newBitmap.PixelHeight);    // --- 3. 捕获变换后的完整状态 (for REDO) ---
            var redoPixels = new byte[newBitmap.PixelHeight * newBitmap.BackBufferStride];
            // 从新创建的位图复制像素
            newBitmap.CopyPixels(redoRect, redoPixels, newBitmap.BackBufferStride, 0);
            _surface.ReplaceBitmap(newBitmap);  // --- 4. 执行变换：用新的位图替换旧的画布 ---
            _ctx.Undo.PushTransformAction(undoRect, undoPixels, redoRect, redoPixels);   // --- 5. 将完整的变换信息压入 Undo 栈 ---
            NotifyCanvasSizeChanged(newWidth, newHeight);
            NotifyCanvasChanged();
            SetUndoRedoButtonState();
            _canvasResizer.UpdateUI();
        }
        private void ConvertToBlackAndWhite(WriteableBitmap bmp)
        {
            bmp.Lock();
            unsafe
            {
                byte* basePtr = (byte*)bmp.BackBuffer;
                int stride = bmp.BackBufferStride;
                int height = bmp.PixelHeight;
                int width = bmp.PixelWidth;
                Parallel.For(0, height, y =>// 使用并行处理来加速计算，每个CPU核心处理一部分行
                {
                    byte* row = basePtr + y * stride;
                    // 像素格式为 BGRA (4 bytes per pixel)
                    for (int x = 0; x < width; x++)
                    {
                        // 获取当前像素的 B, G, R 值
                        byte b = row[x * 4];
                        byte g = row[x * 4 + 1];
                        byte r = row[x * 4 + 2];
                        // 使用亮度公式计算灰度值
                        // 这个公式比简单的 (R+G+B)/3 效果更符合人眼感知
                        byte gray = (byte)(r * 0.2126 + g * 0.7152 + b * 0.0722); // 将计算出的灰度值写回所有三个颜色通道
                        row[x * 4] = gray; // Blue
                        row[x * 4 + 1] = gray; // Green
                        row[x * 4 + 2] = gray; // Red
                                               // Alpha 通道 (row[x * 4 + 3]) 保持不变
                    }
                });
            }
            // 标记整个图像区域已更新
            bmp.AddDirtyRect(new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight));
            bmp.Unlock();
        }
    }
}