
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
//
//TabPaint主程序
//

namespace TabPaint
{

    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void Undo()
        {

            _undo.Undo(); _ctx.IsDirty = true;
            SetUndoRedoButtonState();
            _canvasResizer.UpdateUI();

        }
        private void Redo()
        {
            _undo.Redo(); _ctx.IsDirty = true;
            SetUndoRedoButtonState();
            _canvasResizer.UpdateUI();
        }

        public class UndoAction
        {
            private CompressedBuffer _pixelsBuf;
            private CompressedBuffer _undoPixelsBuf;
            private CompressedBuffer _redoPixelsBuf;
            public Int32Rect Rect { get; }
            public byte[] Pixels => _pixelsBuf?.GetData();
            public Int32Rect UndoRect { get; }      // 撤销时恢复的尺寸
            public byte[] UndoPixels => _undoPixelsBuf?.GetData();
            public Int32Rect RedoRect { get; }      // 重做时恢复的尺寸
            public byte[] RedoPixels => _redoPixelsBuf?.GetData();
            public UndoActionType ActionType { get; }
            public string DeletedFilePath { get; }
            public FileTabItem DeletedTab { get; }
            public int DeletedTabIndex { get; }
            public long LastAccountedSize { get; set; }
            public long MemorySize =>
             (_pixelsBuf?.ActualMemorySize ?? 0) +
             (_undoPixelsBuf?.ActualMemorySize ?? 0) +
             (_redoPixelsBuf?.ActualMemorySize ?? 0);
            public DateTime Timestamp { get; } = DateTime.Now;

            public UndoAction(string filePath, FileTabItem tab, int index)
            {
                ActionType = UndoActionType.FileDelete;
                DeletedFilePath = filePath;
                DeletedTab = tab;
                DeletedTabIndex = index;
            }

            public UndoAction(Int32Rect rect, byte[] pixels, UndoActionType actionType = UndoActionType.Draw)
            {
                ActionType = actionType;
                Rect = rect;
                _pixelsBuf = pixels != null ? new CompressedBuffer(pixels) : null;
            }

            public UndoAction(Int32Rect undoRect, byte[] undoPixels, Int32Rect redoRect, byte[] redoPixels)
            {
                ActionType = UndoActionType.Transform;
                UndoRect = undoRect;
                RedoRect = redoRect;
                _undoPixelsBuf = undoPixels != null ? new CompressedBuffer(undoPixels) : null;
                _redoPixelsBuf = redoPixels != null ? new CompressedBuffer(redoPixels) : null;
            }
            public void CompressAll()
            {
                _pixelsBuf?.Compress();
                _undoPixelsBuf?.Compress();
                _redoPixelsBuf?.Compress();
            }

            public void DecompressAll()
            {
                _pixelsBuf?.Decompress();
                _undoPixelsBuf?.Decompress();
                _redoPixelsBuf?.Decompress();
            }
        }
        public class UndoRedoManager
        {
            private readonly CanvasSurface _surface;
            public List<UndoAction> _undo = new();
            public List<UndoAction> _redo = new();

            private static long _globalUndoMemory = 0;
            private static int _globalUndoSteps = 0;
            private static readonly object _globalLimitLock = new object();

            public List<UndoAction> GetUndoStack() => _undo;
            public List<UndoAction> GetRedoStack() => _redo;
            private byte[]? _preStrokeSnapshot;
            private readonly List<Int32Rect> _strokeRects = new();
            public int UndoCount => _undo.Count;
            public bool HasPendingStroke => _preStrokeSnapshot != null && _strokeRects.Count > 0;

            private void UpdateGlobalStats(UndoAction action, bool adding)
            {
                lock (_globalLimitLock)
                {
                    if (adding)
                    {
                        long size = action.MemorySize;
                        _globalUndoMemory += size;
                        action.LastAccountedSize = size;
                        _globalUndoSteps++;
                    }
                    else
                    {
                        _globalUndoMemory -= action.LastAccountedSize;
                        _globalUndoSteps--;
                        action.LastAccountedSize = 0;
                    }
                }
            }

            private void UpdateUI()
            {
                var mw = MainWindow.GetCurrentInstance();
                mw.SetUndoRedoButtonState(); // 更新按钮可用性
                mw.CheckDirtyState();        // 更新红点状态
                mw.UpdateWindowTitle();
            }
            public UndoRedoManager(CanvasSurface surface) { _surface = surface; }

            public bool CanUndo => _undo.Count > 0;
            public bool CanRedo => _redo.Count > 0;
            private void TrimStack()
            {
                CompressColdActions(_undo);
                CompressColdActions(_redo);
                CheckGlobalUndoLimits();
            }
            private static void CompressColdActions(List<UndoAction> stack)
            {
                if (stack.Count <= AppConsts.UndoHotZoneSize) return;
                int coldEnd = stack.Count - AppConsts.UndoHotZoneSize;
                for (int i = 0; i < coldEnd; i++)
                {
                    var action = stack[i];
                    if (action.MemorySize > AppConsts.UndoCompressThreshold)
                    {
                        Task.Run(() =>
                        {
                            action.CompressAll();
                            long newSize = action.MemorySize;
                            lock (_globalLimitLock)
                            {
                                if (action.LastAccountedSize > 0) // 仍在栈中
                                {
                                    long diff = action.LastAccountedSize - newSize;
                                    if (diff > 0)
                                    {
                                        _globalUndoMemory -= diff;
                                        action.LastAccountedSize = newSize;
                                    }
                                }
                            }
                        });
                    }
                }
            }
            public static void CheckGlobalUndoLimits()
            {
                var mw = MainWindow.GetCurrentInstance();
                if (mw == null) return;

                var settings = SettingsManager.Instance.Current;
                long maxMemory = (long)settings.MaxUndoMemoryMB * 1024 * 1024;
                int maxGlobalSteps = settings.MaxGlobalUndoSteps;

                lock (_globalLimitLock)
                {
                    while (_globalUndoMemory > maxMemory || _globalUndoSteps > maxGlobalSteps)
                    {
                        List<UndoAction> oldestStack = null;
                        DateTime oldestTime = DateTime.MaxValue;

                        foreach (var tab in mw.FileTabs)
                        {
                            var u = (tab == mw._currentTabItem && mw._undo != null) ? mw._undo._undo : tab.UndoStack;
                            var r = (tab == mw._currentTabItem && mw._undo != null) ? mw._undo._redo : tab.RedoStack;

                            if (u.Count > 0 && u[0].Timestamp < oldestTime) { oldestTime = u[0].Timestamp; oldestStack = u; }
                            if (r.Count > 0 && r[0].Timestamp < oldestTime) { oldestTime = r[0].Timestamp; oldestStack = r; }
                        }

                        if (oldestStack != null)
                        {
                            var action = oldestStack[0];
                            _globalUndoMemory -= action.LastAccountedSize;
                            _globalUndoSteps--;
                            action.LastAccountedSize = 0;
                            oldestStack.RemoveAt(0);
                        }
                        else break;
                    }
                }
            }

            public void PushTransformAction(Int32Rect undoRect, byte[] undoPixels, Int32Rect redoRect, byte[] redoPixels)
            {//自动SetUndoRedoButtonState和_redo.Clear()
                var action = new UndoAction(undoRect, undoPixels, redoRect, redoPixels);
                _undo.Add(action);
                UpdateGlobalStats(action, true);

                foreach (var r in _redo) UpdateGlobalStats(r, false);
                _redo.Clear(); // 新操作截断重做链

                TrimStack();
                UpdateUI();
            }
            // ---------- 绘制操作 ----------
            public void BeginStroke()
            {
                if (_surface?.Bitmap == null) return;

                _preStrokeSnapshot = CaptureFullBitmapSnapshot();
                _strokeRects.Clear();
                ClearRedo(); // 新操作截断重做链
            }

            public List<Int32Rect> GetCurrentStrokeRects() => _strokeRects;

            public void AddDirtyRect(Int32Rect rect) => _strokeRects.Add(rect);

            private byte[]? CaptureFullBitmapSnapshot()
            {
                if (_surface?.Bitmap == null) return null;

                int bytes = _surface.Bitmap.BackBufferStride * _surface.Height;
                var snapshot = new byte[bytes];
                _surface.Bitmap.Lock();
                System.Runtime.InteropServices.Marshal.Copy(_surface.Bitmap.BackBuffer, snapshot, 0, bytes);
                _surface.Bitmap.Unlock();
                return snapshot;
            }

            private UndoAction? BuildStrokeUndoAction(UndoActionType undoActionType)
            {
                var mw = MainWindow.GetCurrentInstance();
                if (_preStrokeSnapshot == null || _strokeRects.Count == 0 || _surface?.Bitmap == null || mw?._ctx?.Bitmap == null)
                    return null;

                var combined = ClampRect(
                    CombineRects(_strokeRects),
                    mw._ctx.Bitmap.PixelWidth,
                    mw._ctx.Bitmap.PixelHeight);

                if (combined.Width <= 0 || combined.Height <= 0) return null;

                byte[] region = ExtractRegionFromSnapshot(_preStrokeSnapshot, combined, _surface.Bitmap.BackBufferStride);
                return new UndoAction(combined, region, undoActionType);
            }

            public void CommitStroke(UndoActionType undoActionType = UndoActionType.Draw)//一般绘画
            {

                var action = BuildStrokeUndoAction(undoActionType);
                if (action == null)
                {
                    _preStrokeSnapshot = null;
                    _strokeRects.Clear();
                    return;
                }

                _undo.Push(action);
                UpdateGlobalStats(action, true);

                TrimStack();
                UpdateUI();
                _preStrokeSnapshot = null;
                _strokeRects.Clear();

                (MainWindow.GetCurrentInstance()).NotifyCanvasChanged();
            }

            public bool CommitStrokeAndContinue(UndoActionType undoActionType = UndoActionType.Draw)
            {
                if (_surface?.Bitmap == null) return false;

                var action = BuildStrokeUndoAction(undoActionType);
                if (action == null) return false;

                _undo.Push(action);
                UpdateGlobalStats(action, true);

                TrimStack();
                UpdateUI();
                _preStrokeSnapshot = CaptureFullBitmapSnapshot();
                _strokeRects.Clear();

                (MainWindow.GetCurrentInstance()).NotifyCanvasChanged();
                return true;
            }

            public void internalUndoAction(UndoAction action)
            {
                var redoPixels = _surface.ExtractRegion(action.Rect);
                var redoAction = new UndoAction(action.Rect, redoPixels);
                _redo.Push(redoAction);
                UpdateGlobalStats(redoAction, true);

                // 执行 Undo
                _surface.WriteRegion(action.Rect, action.Pixels);
            }
            // ---------- 撤销 / 重做 ----------
            public void Undo()
            {
                var mw = MainWindow.GetCurrentInstance();
                if (mw._deleteCommitTimer.IsEnabled && mw._pendingDeletionTabs.Count > 0)
                {
                    mw.RestoreLastDeletedTab();
                    return; // 拦截成功，不再执行画布撤销
                }
                if (_undo != null) // 确保 UndoManager 存在
                {
                    ImageUndo();
                }
            }
            public void ImageUndo()
            {
                var mw = MainWindow.GetCurrentInstance();
                if (mw._router.CurrentTool is SelectTool sselTool && sselTool.HasActiveSelection)
                {
                    if (sselTool.IsPasted)
                    {
                        sselTool.Cleanup(mw._ctx);
                        mw.SetCropButtonState();
                        return; // 直接返回，不执行下面的 Stack Pop
                    }
                    if (!sselTool._hasLifted)
                    {
                        sselTool.Cleanup(mw._ctx);
                        mw.SetCropButtonState();
                        return;
                    }
                }

                if (!CanUndo || _surface?.Bitmap == null) return;

                var action = _undo.Pop();
                UpdateGlobalStats(action, false);

                action.DecompressAll();
                if (mw._router.CurrentTool is SelectTool selTool) selTool.Cleanup(mw._ctx);

                if (action.ActionType == UndoActionType.Transform)
                {
                    var currentRect = new Int32Rect(0, 0, _surface.Bitmap.PixelWidth, _surface.Bitmap.PixelHeight);
                    var currentPixels = _surface.ExtractRegion(currentRect);
                    var redoAction = new UndoAction(currentRect, currentPixels, action.RedoRect, action.RedoPixels);
                    _redo.Push(redoAction);
                    UpdateGlobalStats(redoAction, true);

                    var wb = new WriteableBitmap(action.UndoRect.Width, action.UndoRect.Height,
                            mw._ctx.Surface.Bitmap.DpiX, mw._ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);

                    wb.WritePixels(action.UndoRect, action.UndoPixels, wb.BackBufferStride, 0);
                    _surface.ReplaceBitmap(wb);
                    mw.NotifyCanvasSizeChanged(action.UndoRect.Width, action.UndoRect.Height);
                }
                else // Draw Action
                {
                    internalUndoAction(action);
                    if (action.ActionType == UndoActionType.Selection && _undo.Count > 0)
                    {
                        var pairedAction = _undo.Pop();
                        UpdateGlobalStats(pairedAction, false);
                        pairedAction.DecompressAll();
                        internalUndoAction(pairedAction);
                    }
                }

                UpdateUI();
                mw.NotifyCanvasChanged();
            }


            public void Redo()
            {
                if (!CanRedo || _surface?.Bitmap == null) return;

                var action = _redo.Pop();
                UpdateGlobalStats(action, false);

                action.DecompressAll();
                if (action.ActionType == UndoActionType.Transform)
                {
                    // 1. 准备对应的 Undo Action
                    var currentRect = new Int32Rect(0, 0, _surface.Bitmap.PixelWidth, _surface.Bitmap.PixelHeight);
                    var currentPixels = _surface.ExtractRegion(currentRect);

                    var undoAction = new UndoAction(
                        currentRect,       // 撤销这个 Redo 会回到当前状态
                        currentPixels,
                        action.RedoRect,   // 执行这个 Redo 会回到裁剪后的状态
                        action.RedoPixels
                    );
                    _undo.Push(undoAction);
                    UpdateGlobalStats(undoAction, true);

                    var wb = new WriteableBitmap(action.RedoRect.Width, action.RedoRect.Height,
                            (MainWindow.GetCurrentInstance())._ctx.Surface.Bitmap.DpiX, (MainWindow.GetCurrentInstance())._ctx.Surface.Bitmap.DpiY, PixelFormats.Bgra32, null);
                    wb.WritePixels(action.RedoRect, action.RedoPixels, wb.BackBufferStride, 0);
                    // 替换主位图
                    _surface.ReplaceBitmap(wb);
                }
                else // Draw Action
                {
                    // 准备 Undo Action
                    var undoPixels = _surface.ExtractRegion(action.Rect);
                    var undoAction = new UndoAction(action.Rect, undoPixels);
                    _undo.Push(undoAction);
                    UpdateGlobalStats(undoAction, true);

                    _surface.WriteRegion(action.Rect, action.Pixels);
                }

                UpdateUI();
                (MainWindow.GetCurrentInstance()).NotifyCanvasChanged();
            }
            public void PushExplicitImageUndo(WriteableBitmap oldBitmap)
            {
                if (oldBitmap == null) return;
                Int32Rect rect = new Int32Rect(0, 0, oldBitmap.PixelWidth, oldBitmap.PixelHeight);
                int stride = oldBitmap.BackBufferStride;
                byte[] pixels = new byte[stride * oldBitmap.PixelHeight];
                oldBitmap.CopyPixels(pixels, stride, 0);
                var action = new UndoAction(rect, pixels, UndoActionType.Draw);
                _undo.Push(action);
                UpdateGlobalStats(action, true);

                foreach (var r in _redo) UpdateGlobalStats(r, false);
                _redo.Clear();  // 4. 清空重做链并更新 UI

                TrimStack();
                UpdateUI();
            }

            public void PushFullImageUndo()
            { // ---------- 供整图操作调用 ----------
                if (_surface?.Bitmap == null) return; /// 在整图变换(旋转/翻转/新建)之前，准备一个完整快照并保存redo像素

                var rect = new Int32Rect(0, 0,
                    _surface.Bitmap.PixelWidth,
                    _surface.Bitmap.PixelHeight);

                var currentPixels = SafeExtractRegion(rect);
                var action = new UndoAction(rect, currentPixels);
                _undo.Push(action);
                UpdateGlobalStats(action, true);

                foreach (var r in _redo) UpdateGlobalStats(r, false);
                _redo.Clear();
                TrimStack();
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
                if (rect.X < 0 || rect.Y < 0 ||
                    rect.X + rect.Width > _surface.Bitmap.PixelWidth ||
                    rect.Y + rect.Height > _surface.Bitmap.PixelHeight ||
                    rect.Width <= 0 || rect.Height <= 0)
                {
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
                foreach (var r in _redo) UpdateGlobalStats(r, false);
                _redo.Clear();
            }
            public void ClearUndo()
            {
                foreach (var u in _undo) UpdateGlobalStats(u, false);
                _undo.Clear();
            }
        }
    }
}