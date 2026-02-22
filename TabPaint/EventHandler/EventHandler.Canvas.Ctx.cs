
//
//EventHandler.Canvas.cs
//画布相关的事件处理逻辑，包括自动裁剪、OCR、AI背景移除、超分重建以及各种图像滤镜的触发。
//
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Ocr;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void OnAddBorderClick(object sender, RoutedEventArgs e)
        {
            // 1. 检查画布是否存在
            if (_surface?.Bitmap == null) return;
            _router.CleanUpSelectionandShape();
            var bmp = _surface.Bitmap;
            int w = bmp.PixelWidth;
            int h = bmp.PixelHeight;
            int borderSize = AppConsts.DefaultBorderThickness; // 边框厚度
            if (w <= borderSize * 2 || h <= borderSize * 2)  // 如果图片太小，不足以画边框，直接返回
            {
                ShowToast("L_Toast_SizeTooSmallForBorder");
                return;
            }
            _undo.BeginStroke();
            bmp.Lock();
            try
            {
                Color c = ForegroundColor;

                unsafe
                {
                    byte* basePtr = (byte*)bmp.BackBuffer;
                    int stride = bmp.BackBufferStride;
                    void FillRect(int rectX, int rectY, int rectW, int rectH)
                    {
                        for (int y = rectY; y < rectY + rectH; y++)
                        {
                            byte* rowPtr = basePtr + (y * stride) + (rectX * 4);
                            for (int x = 0; x < rectW; x++)
                            {
                                rowPtr[0] = c.B;
                                rowPtr[1] = c.G;
                                rowPtr[2] = c.R;
                                rowPtr[3] = c.A;
                                rowPtr += 4; // 移动到下一个像素
                            }
                        }
                    }
                    FillRect(0, 0, w, borderSize);
                    FillRect(0, h - borderSize, w, borderSize);
                    FillRect(0, borderSize, borderSize, h - 2 * borderSize);
                    FillRect(w - borderSize, borderSize, borderSize, h - 2 * borderSize);
                }
                var rTop = new Int32Rect(0, 0, w, borderSize);
                bmp.AddDirtyRect(rTop);
                _undo.AddDirtyRect(rTop);
                var rBottom = new Int32Rect(0, h - borderSize, w, borderSize);
                bmp.AddDirtyRect(rBottom);
                _undo.AddDirtyRect(rBottom);
                var rLeft = new Int32Rect(0, 0, borderSize, h);
                bmp.AddDirtyRect(rLeft);
                _undo.AddDirtyRect(rLeft);
                var rRight = new Int32Rect(w - borderSize, 0, borderSize, h);
                bmp.AddDirtyRect(rRight);
                _undo.AddDirtyRect(rRight);
            }
            finally
            {
                bmp.Unlock();
            }
            _undo.CommitStroke();
            _isEdited = true;
            _ctx.IsDirty = true;
            NotifyCanvasChanged();
            ShowToast("L_Toast_BorderAdded");
        }

        private Point _lastRightClickPosition; // 记录右键点击时的相对坐标
        private void OnAutoCropClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isLoadingImage) return;
                if (_router.CurrentTool is SelectTool st && st.HasActiveSelection)
                {
                    st.CommitSelection(_ctx);
                }

                AutoCrop();
            }
            catch (Exception ex)
            {
                ShowToast("L_Toast_CropFailed_Prefix", ex);
            }
        }
        private void OnCopyColorCodeClick(object sender, RoutedEventArgs e)
        {
            if (_bitmap == null || BackgroundImage.ActualWidth <= 0 || BackgroundImage.ActualHeight <= 0) return;

            try
            {
                Point targetPoint;
                // 判断是否为快捷键触发 (通过 sender 类型或特定的路由事件参数判断)
                if (sender is MenuItem)
                {
                    targetPoint = _lastRightClickPosition;
                }
                else
                {
                    // 快捷键或其他方式触发，获取当前鼠标位置
                    targetPoint = Mouse.GetPosition(BackgroundImage);
                }
                a.s(_lastRightClickPosition, targetPoint);
                double scaleX = _bitmap.PixelWidth / BackgroundImage.ActualWidth;
                double scaleY = _bitmap.PixelHeight / BackgroundImage.ActualHeight;

                int x = (int)(targetPoint.X * scaleX);
                int y = (int)(targetPoint.Y * scaleY);

                // 增加边界保护，防止微小误差导致越界
                x = Math.Clamp(x, 0, _bitmap.PixelWidth - 1);
                y = Math.Clamp(y, 0, _bitmap.PixelHeight - 1);

                Color color = GetPixelColor(x, y);
                string hexCode = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

                ClipboardHelper.SetTextWithRetry(hexCode);
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_ColorCopied_Format"), hexCode));
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Common_Error") + ": {0}", ex.Message), ex);
            }
        }
        private void OnScreenColorPickerClick(object sender, RoutedEventArgs e)
        {
            // 获取当前的 Dispatcher
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var picker = new ColorPickerWindow();
                bool? result = picker.ShowDialog();

                if (result == true && picker.IsColorPicked)
                {
                    Color c = picker.PickedColor; //应用颜色逻辑
                    ApplyPickedColor(c);
                }
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }


        private void ApplyPickedColor(Color c)
        {
            if (!useSecondColor)
            {
                this.ForegroundColor = c;
                this.ForegroundBrush = new SolidColorBrush(c);
            }
            else
            {
                this.BackgroundColor = c;
                this.BackgroundBrush = new SolidColorBrush(c);
            }


            // 通知UI更新
            OnPropertyChanged(nameof(SelectedBrush));
            OnPropertyChanged(nameof(ForegroundBrush));
            OnPropertyChanged(nameof(BackgroundBrush));
            // 简单的提示
            ShowToast(string.Format(LocalizationManager.GetString("L_Toast_ColorPicked_Format"), c.R, c.G, c.B));
        }
        private void OnScrollContainerContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (IsViewMode)
            {
                e.Handled = true;
                return;
            }
            _lastRightClickPosition = Mouse.GetPosition(BackgroundImage);
        }
        private void OnChromaKeyClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_bitmap == null || BackgroundImage.ActualWidth <= 0 || BackgroundImage.ActualHeight <= 0) return;
                _router.CleanUpSelectionandShape();
                Point targetPoint;

                if (sender is MenuItem)
                {
                    targetPoint = _lastRightClickPosition;
                }
                else
                {
                    targetPoint = Mouse.GetPosition(BackgroundImage);
                }

                double scaleX = _bitmap.PixelWidth / BackgroundImage.ActualWidth;
                double scaleY = _bitmap.PixelHeight / BackgroundImage.ActualHeight;

                int x = (int)(targetPoint.X * scaleX);
                int y = (int)(targetPoint.Y * scaleY);

                x = Math.Clamp(x, 0, _bitmap.PixelWidth - 1);
                y = Math.Clamp(y, 0, _bitmap.PixelHeight - 1);

                Color targetColor = GetPixelColor(x, y);

                ApplyColorKey(targetColor, AppConsts.DefaultChromaKeyTolerance);
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_RemoveBgFailed_Prefix"), ex.Message), ex);
            }
        }
        private async void OnAiOcrClick(object sender, RoutedEventArgs e)
        {
            ShowToast("L_Toast_AiOcr_NotAvailable");
        }
        private byte[] BitmapSourceToBytes(BitmapSource source)
        {
            var encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                return ms.ToArray();
            }
        }
        private void ApplyColorKey(Color targetColor, int tolerance)
        {
            if (_surface?.Bitmap == null) return;

            _undo.BeginStroke();

            _bitmap.Lock();
            unsafe
            {
                byte* basePtr = (byte*)_bitmap.BackBuffer;
                int stride = _bitmap.BackBufferStride;
                int width = _bitmap.PixelWidth;
                int height = _bitmap.PixelHeight;
                int tR = targetColor.R;
                int tG = targetColor.G;
                int tB = targetColor.B;
                int toleranceSq = tolerance * tolerance;
                Parallel.For(0, height, y =>
                {
                    byte* row = basePtr + y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        byte b = row[x * 4];
                        byte g = row[x * 4 + 1];
                        byte r = row[x * 4 + 2];
                        byte a = row[x * 4 + 3];
                        if (a == 0) continue;
                        int diffR = r - tR;
                        int diffG = g - tG;
                        int diffB = b - tB;

                        int distSq = (diffR * diffR) + (diffG * diffG) + (diffB * diffB);
                        if (distSq <= 3 * toleranceSq) row[x * 4 + 3] = 0;
                    }
                });
            }
            var fullRect = new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight);
            _bitmap.AddDirtyRect(fullRect);
            _bitmap.Unlock();

            _undo.AddDirtyRect(fullRect);
            _undo.CommitStroke();

            NotifyCanvasChanged();
        }

        private async void OnOcrClick(object sender, RoutedEventArgs e)
        {
            if (_surface?.Bitmap == null) return;
            if (!IsOcrSupported()) { ShowToast("L_Toast_OCR_VersionError"); return; }
            BitmapSource sourceToRecognize = _surface.Bitmap;
            if (_router.CurrentTool is SelectTool selTool && selTool.HasActiveSelection) sourceToRecognize = selTool.GetSelectionCroppedBitmap(this);
            try
            {
                var oldStatus = _imageSize;// UI 提示
                _imageSize = LocalizationManager.GetString("L_OCR_Status_Processing");
                this.Cursor = System.Windows.Input.Cursors.Wait;
                var ocrService = new OcrService();  // 调用服务
                string text = await ocrService.RecognizeTextAsync(sourceToRecognize);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    ClipboardHelper.SetTextWithRetry(text);
                    ShowToast(string.Format(LocalizationManager.GetString("L_Toast_OCR_Success_Format"), text.Length));
                }
                else ShowToast("L_Toast_OCR_NoText");
                _imageSize = oldStatus;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("0x80004005") || ex.Message.Contains("Language"))
                {
                    ShowToast("L_Toast_OCR_InitFailed", ex);
                }
                else if (ex is PlatformNotSupportedException)
                {
                    ShowToast("L_Toast_OCR_NotSupported", ex);
                }
                else ShowToast(string.Format(LocalizationManager.GetString("L_Toast_OCR_Error_Prefix"), ex.Message), ex);
            }
            finally
            {
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }
        private async void OnAiUpscaleClick(object sender, RoutedEventArgs e)
        {
            // 1. 基础检查
            if (_surface?.Bitmap == null) return;
            _router.CleanUpSelectionandShape();

            if (!IsVcRedistInstalled())
            {
                ShowToast("L_Error_MissingVCRedist");
                return;
            }

            bool ready = await EnsureAiModelReadyAsync(AiService.AiTaskType.SuperResolution);
            if (!ready) return;

            // 2. 状态保存与 UI 锁定
            var statusText = _imageSize;
            _downloadCts = new System.Threading.CancellationTokenSource();
            EventHandler cancelHandler = (s, args) => _downloadCts.Cancel();
            TaskProgressPopup.CancelRequested += cancelHandler;

            try
            {
                var aiService = AiService.Instance;
                string modelPath = Path.Combine(_cacheDir, AppConsts.Sr_ModelName);

                WriteableBitmap inputBmp = _surface.Bitmap;
                const int MaxLongSide = AppConsts.AiUpscaleMaxLongSide; // 限制长边最大 4096

                if (inputBmp.PixelWidth > MaxLongSide || inputBmp.PixelHeight > MaxLongSide)
                {
                    _imageSize = "图片过大，正在进行预缩小...";
                    OnPropertyChanged(nameof(ImageSize));

                    double scale = (double)MaxLongSide / Math.Max(inputBmp.PixelWidth, inputBmp.PixelHeight);
                    int targetW = (int)(inputBmp.PixelWidth * scale);
                    int targetH = (int)(inputBmp.PixelHeight * scale);

                    var resampledSource = ResampleBitmap(inputBmp, targetW, targetH);
                    inputBmp = new WriteableBitmap(resampledSource);
                }
                _imageSize = LocalizationManager.GetString("L_AI_Status_Thinking");
                OnPropertyChanged(nameof(ImageSize));
                TaskProgressPopup.UpdateProgress(0, LocalizationManager.GetString("L_AI_Status_Upscaling"), "", "");

                var inferProgress = new Progress<double>(p =>
                {
                    _imageSize = string.Format(LocalizationManager.GetString("L_AI_Status_Upscaling_Format"), p);
                    OnPropertyChanged(nameof(ImageSize));
                    TaskProgressPopup.UpdateProgress(p, LocalizationManager.GetString("L_AI_Status_Upscaling"), "", "");
                });
                var resultBitmap = await aiService.RunSuperResolutionAsync(modelPath, inputBmp, inferProgress, _downloadCts.Token);
                ApplyUpscaleResult(resultBitmap);
                GC.Collect(2, GCCollectionMode.Forced, true);
                ShowToast("L_Toast_Apply_Success");
            }
            catch (OperationCanceledException)
            {

                TaskProgressPopup.Finish();
            }
            catch (Exception ex)
            {
                string errorMsg = ex.Message;
                if (ex is OverflowException) errorMsg = "图片尺寸超出硬件/软件限制";
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_Upscale_Error_Prefix"), errorMsg),ex);

            }
            finally
            {
                TaskProgressPopup.CancelRequested -= cancelHandler;
                TaskProgressPopup.Finish();
                _downloadCts?.Dispose();
                _downloadCts = null;
                if (_surface?.Bitmap != null)
                    _imageSize = $"{_surface.Bitmap.PixelWidth}×{_surface.Bitmap.PixelHeight}" + LocalizationManager.GetString("L_Main_Unit_Pixel");

                OnPropertyChanged(nameof(ImageSize));
                NotifyCanvasChanged();
                this.Focus();
            }
        }
        private System.Threading.CancellationTokenSource _downloadCts;
        private void OnDownloadCancelRequested(object sender, EventArgs e)
        {
            if (_downloadCts != null && !_downloadCts.IsCancellationRequested)
            {
                _downloadCts.Cancel();
                ShowToast("L_Toast_DownloadCancelled");
            }
        }
        private void ApplyUpscaleResult(WriteableBitmap newBitmap)
        {
            var oldBitmap = _surface.Bitmap;
            int oldW = oldBitmap.PixelWidth;
            int oldH = oldBitmap.PixelHeight;
            var undoRect = new Int32Rect(0, 0, oldW, oldH);
            byte[] undoPixels = new byte[oldH * oldBitmap.BackBufferStride];
            oldBitmap.CopyPixels(undoRect, undoPixels, oldBitmap.BackBufferStride, 0);
            _surface.ReplaceBitmap(newBitmap);
            _bitmap = newBitmap;
            BackgroundImage.Source = _bitmap;
            int newW = newBitmap.PixelWidth;
            int newH = newBitmap.PixelHeight;
            var redoRect = new Int32Rect(0, 0, newW, newH);
            byte[] redoPixels = new byte[newH * newBitmap.BackBufferStride];
            newBitmap.CopyPixels(redoRect, redoPixels, newBitmap.BackBufferStride, 0);
            _undo.PushTransformAction(undoRect, undoPixels, redoRect, redoPixels);
            NotifyCanvasSizeChanged(newW, newH);
            SetUndoRedoButtonState();
            if (_canvasResizer != null) _canvasResizer.UpdateUI();
        }

        private async void OnRemoveBackgroundClick(object sender, RoutedEventArgs e)
        {
            if (_surface?.Bitmap == null) return;
            var selectTool = _router.CurrentTool as SelectTool;
            bool isSelectionMode = selectTool != null && selectTool.HasActiveSelection;
            if (!isSelectionMode) _router.CleanUpSelectionandShape();

            if (!IsVcRedistInstalled())
            {
                var result = FluentMessageBox.Show(
                    LocalizationManager.GetString("L_AI_RMBG_MissingRuntime_Content"),
                    LocalizationManager.GetString("L_AI_RMBG_MissingRuntime_Title"),
                    MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://aka.ms/vs/17/release/vc_redist.x64.exe",
                        UseShellExecute = true
                    });
                }
                return;
            }

            bool ready = await EnsureAiModelReadyAsync(AiService.AiTaskType.RemoveBackground);
            if (!ready) return;

            var statusText = _imageSize; // 暂存状态栏文本

            try
            {
                var aiService = AiService.Instance;
                string modelPath = Path.Combine(_cacheDir, AppConsts.BgRem_ModelName);

                _imageSize = LocalizationManager.GetString("L_AI_Status_Thinking");
                OnPropertyChanged(nameof(ImageSize));

                byte[] resultPixels;
                if (isSelectionMode)
                {
                    if (selectTool.IsIrregularSelection)
                    {
                        var boundingBoxBmp = selectTool.GetSelectionBoundingBoxBitmap(_ctx);
                        if (boundingBoxBmp == null) boundingBoxBmp = selectTool.GetSelectionWriteableBitmap(this);
                        if (boundingBoxBmp == null) return;

                        int newW = boundingBoxBmp.PixelWidth;
                        int newH = boundingBoxBmp.PixelHeight;
                        resultPixels = await aiService.RunInferenceAsync(modelPath, boundingBoxBmp);
                        selectTool.ReplaceSelectionDataWithMask(_ctx, resultPixels, newW, newH);
                    }
                    else
                    {
                        var cropBmp = selectTool.GetSelectionWriteableBitmap(this);
                        if (cropBmp == null) return;

                        int newW = cropBmp.PixelWidth;
                        int newH = cropBmp.PixelHeight;
                        resultPixels = await aiService.RunInferenceAsync(modelPath, cropBmp);
                        selectTool.ReplaceSelectionData(_ctx, resultPixels, newW, newH);
                    }
                }
                else
                {
                    resultPixels = await aiService.RunInferenceAsync(modelPath, _surface.Bitmap);

                    ApplyAiResult(resultPixels);
                }
                ShowToast("L_Toast_Apply_Success");
            }
            catch (OperationCanceledException) { TaskProgressPopup.Finish(); }
            catch (Exception ex)
            {
                if (ex is DllNotFoundException ||
                    ex.InnerException is DllNotFoundException ||
                    ex.Message.Contains("onnxruntime"))
                {
                    ShowToast("L_AI_Error_DllNotFound", ex);
                }
                else
                {
                    ShowToast(string.Format(LocalizationManager.GetString("L_Toast_RemoveBgFailed_Prefix"), ex.Message),ex);

                }
            }
            finally
            {
                _downloadCts?.Dispose();
                _downloadCts = null;
                _imageSize = statusText; // 恢复状态栏
                OnPropertyChanged(nameof(ImageSize));
                NotifyCanvasChanged();
                this.Focus();
            }
        }


        private void ApplyAiResult(byte[] newPixels)
        {
            _undo.PushFullImageUndo();
            _surface.Bitmap.Lock(); // 更新 Bitmap
            _surface.Bitmap.WritePixels(
                new Int32Rect(0, 0, _surface.Bitmap.PixelWidth, _surface.Bitmap.PixelHeight),
                newPixels,
                _surface.Bitmap.BackBufferStride,
                0
            );
            _surface.Bitmap.AddDirtyRect(new Int32Rect(0, 0, _surface.Bitmap.PixelWidth, _surface.Bitmap.PixelHeight));
            _surface.Bitmap.Unlock();
            _ctx.IsDirty = true;//更新 UI
            CheckDirtyState();
            SetUndoRedoButtonState();
        }
    }
}
