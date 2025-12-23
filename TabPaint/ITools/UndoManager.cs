using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

//
//TabPaint主程序
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public class UndoAction
        {
            public Int32Rect Rect { get; }
            public byte[] Pixels { get; }
            public Int32Rect UndoRect { get; }      // 撤销时恢复的尺寸
            public byte[] UndoPixels { get; }       // 撤销时恢复的像素
            public Int32Rect RedoRect { get; }      // 重做时恢复的尺寸
            public byte[] RedoPixels { get; }       // 重做时恢复的像素
            public UndoActionType ActionType { get; }
            public UndoAction(Int32Rect rect, byte[] pixels)
            {
                ActionType = UndoActionType.Draw;
                Rect = rect;
                Pixels = pixels;
            }
            public UndoAction(Int32Rect undoRect, byte[] undoPixels, Int32Rect redoRect, byte[] redoPixels)
            {
                ActionType = UndoActionType.Transform;
                UndoRect = undoRect;
                UndoPixels = undoPixels;
                RedoRect = redoRect;
                RedoPixels = redoPixels;
            }
        }
        public class UndoRedoManager
        {
            private readonly CanvasSurface _surface;
            private readonly Stack<UndoAction> _undo = new();
            private readonly Stack<UndoAction> _redo = new();
            private byte[]? _preStrokeSnapshot;
            private readonly List<Int32Rect> _strokeRects = new();
            public UndoRedoManager(CanvasSurface surface) { _surface = surface; }

            public bool CanUndo => _undo.Count > 0;
            public bool CanRedo => _redo.Count > 0;
            public void PushTransformAction(Int32Rect undoRect, byte[] undoPixels, Int32Rect redoRect, byte[] redoPixels)
            {//自动SetUndoRedoButtonState和_redo.Clear()
                _undo.Push(new UndoAction(undoRect, undoPixels, redoRect, redoPixels));
                _redo.Clear(); // 新操作截断重做链
                ((MainWindow)System.Windows.Application.Current.MainWindow).SetUndoRedoButtonState();
            }
            // ---------- 绘制操作 ----------
            public void BeginStroke()
            {
                if (_surface?.Bitmap == null) return;

                int bytes = _surface.Bitmap.BackBufferStride * _surface.Height;
                _preStrokeSnapshot = new byte[bytes];
                _surface.Bitmap.Lock();
                System.Runtime.InteropServices.Marshal.Copy(_surface.Bitmap.BackBuffer, _preStrokeSnapshot, 0, bytes);
                _surface.Bitmap.Unlock();
                _strokeRects.Clear();
                _redo.Clear(); // 新操作截断重做链
            }

            public void AddDirtyRect(Int32Rect rect) => _strokeRects.Add(rect);

            public void CommitStroke()//一般绘画
            {
                if (_preStrokeSnapshot == null || _strokeRects.Count == 0 || _surface?.Bitmap == null)
                {
                    _preStrokeSnapshot = null;
                    return;
                }

                var combined = ClampRect(CombineRects(_strokeRects), ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Bitmap.PixelWidth, ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Bitmap.PixelHeight);

                byte[] region = ExtractRegionFromSnapshot(_preStrokeSnapshot, combined, _surface.Bitmap.BackBufferStride);
                _undo.Push(new UndoAction(combined, region));
                ((MainWindow)System.Windows.Application.Current.MainWindow).SetUndoRedoButtonState();
                _preStrokeSnapshot = null;
            }

            // ---------- 撤销 / 重做 ----------
            public void Undo()
            {
                if (!CanUndo || _surface?.Bitmap == null) return;

                var action = _undo.Pop();

                if (action.ActionType == UndoActionType.Transform)
                {
                    var currentRect = new Int32Rect(0, 0, _surface.Bitmap.PixelWidth, _surface.Bitmap.PixelHeight);
                    var currentPixels = _surface.ExtractRegion(currentRect);
                    // 创建一个反向的 Transform Action
                    _redo.Push(new UndoAction(
                        currentRect,       // 撤销这个 Redo 会回到当前状态
                        currentPixels,
                        action.RedoRect,   // 执行这个 Redo 会回到裁剪后的状态
                        action.RedoPixels
                    ));

                    // 2. 执行 Undo 操作 (恢复到变换前的状态)
                    var wb = new WriteableBitmap(action.UndoRect.Width, action.UndoRect.Height,
                            ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Surface.Bitmap.DpiX, ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);

                    wb.WritePixels(action.UndoRect, action.UndoPixels, wb.BackBufferStride, 0);
                    // 替换主位图
                    _surface.ReplaceBitmap(wb); // 假设你有这个方法
                }
                else // Draw Action
                {
                    // 准备 Redo Action
                    var redoPixels = _surface.ExtractRegion(action.Rect);
                    _redo.Push(new UndoAction(action.Rect, redoPixels));
                    // 执行 Undo
                    _surface.WriteRegion(action.Rect, action.Pixels);
                }
     ((MainWindow)System.Windows.Application.Current.MainWindow).SetUndoRedoButtonState();
                // 触发UI更新，如居中等
               // ((MainWindow)System.Windows.Application.Current.MainWindow).CenterImage();
            }

            public void Redo()
            {
                if (!CanRedo || _surface?.Bitmap == null) return;

                var action = _redo.Pop();

                if (action.ActionType == UndoActionType.Transform)
                {
                    // 1. 准备对应的 Undo Action
                    var currentRect = new Int32Rect(0, 0, _surface.Bitmap.PixelWidth, _surface.Bitmap.PixelHeight);
                    var currentPixels = _surface.ExtractRegion(currentRect);

                    _undo.Push(new UndoAction(
                        currentRect,       // 撤销这个 Redo 会回到当前状态
                        currentPixels,
                        action.RedoRect,   // 执行这个 Redo 会回到裁剪后的状态
                        action.RedoPixels
                    ));
                    var wb = new WriteableBitmap(action.RedoRect.Width, action.RedoRect.Height,
                            ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Surface.Bitmap.DpiX, ((MainWindow)System.Windows.Application.Current.MainWindow)._ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);
                    wb.WritePixels(action.RedoRect, action.RedoPixels, wb.BackBufferStride, 0);
                    // 替换主位图
                    _surface.ReplaceBitmap(wb);
                }
                else // Draw Action
                {
                    // 准备 Undo Action
                    var undoPixels = _surface.ExtractRegion(action.Rect);
                    _undo.Push(new UndoAction(action.Rect, undoPixels));
                    _surface.WriteRegion(action.Rect, action.Pixels);
                }

                ((MainWindow)System.Windows.Application.Current.MainWindow).SetUndoRedoButtonState();
                //((MainWindow)System.Windows.Application.Current.MainWindow).CenterImage();
            }
            public void PushFullImageUndo()
            { // ---------- 供整图操作调用 ----------
                if (_surface?.Bitmap == null) return; /// 在整图变换(旋转/翻转/新建)之前，准备一个完整快照并保存redo像素

                var rect = new Int32Rect(0, 0,
                    _surface.Bitmap.PixelWidth,
                    _surface.Bitmap.PixelHeight);

                var currentPixels = SafeExtractRegion(rect);
                _undo.Push(new UndoAction(rect, currentPixels));
                _redo.Clear();
            }
            private static Int32Rect CombineRects(List<Int32Rect> rects)
            {
                int minX = rects.Min(r => r.X);
                int minY = rects.Min(r => r.Y);
                int maxX = rects.Max(r => r.X + r.Width);
                int maxY = rects.Max(r => r.Y + r.Height);
                return new Int32Rect(minX, minY, maxX - minX, maxY - minY);
            }
            private static byte[] ExtractRegionFromSnapshot(byte[] fullData, Int32Rect rect, int stride)
            {

                byte[] region = new byte[rect.Width * rect.Height * 4];
                for (int row = 0; row < rect.Height; row++)
                {
                    int srcOffset = (rect.Y + row) * stride + rect.X * 4;
                    Buffer.BlockCopy(fullData, srcOffset, region, row * rect.Width * 4, rect.Width * 4);
                }
                return region;
            }

            public byte[] SafeExtractRegion(Int32Rect rect)
            {
                // 检查合法范围，防止尺寸变化导致越界
                if (rect.X < 0 || rect.Y < 0 ||
                    rect.X + rect.Width > _surface.Bitmap.PixelWidth ||
                    rect.Y + rect.Height > _surface.Bitmap.PixelHeight ||
                    rect.Width <= 0 || rect.Height <= 0)
                {
                    // 返回当前整图快照（安全退化）
                    int bytes = _surface.Bitmap.BackBufferStride * _surface.Bitmap.PixelHeight;
                    byte[] data = new byte[bytes];
                    _surface.Bitmap.Lock();
                    System.Runtime.InteropServices.Marshal.Copy(_surface.Bitmap.BackBuffer, data, 0, bytes);
                    _surface.Bitmap.Unlock();
                    return data;
                }

                return _surface.ExtractRegion(rect);
            }

            // 清空重做链
            public void ClearRedo()
            {
                _redo.Clear();
            }
            public void ClearUndo()
            {
                _undo.Clear();
            }
        }
    }
}