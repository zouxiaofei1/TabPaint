//
//EventHandler.Menu.cs
//effect
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TabPaint.Windows;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void OnMosaicClick(object sender, RoutedEventArgs e)
        {
            var dialog = new FilterStrengthWindow(LocalizationManager.GetString("L_Menu_Effect_Mosaic"), 10, 2, 100)
            { Owner = this };
            dialog.Closed += (s, args) =>
            {
                if (dialog.IsConfirmed) ApplyFilter(FilterType.Mosaic, dialog.ResultValue);
            };
            dialog.Show();
        }
        private void OnGaussianBlurClick(object sender, RoutedEventArgs e)
        {
            var dialog = new FilterStrengthWindow(LocalizationManager.GetString("L_Menu_Effect_GaussianBlur"), 5, 1, 50)
            { Owner = this };
            dialog.Closed += (s, args) =>
            {
                if (dialog.IsConfirmed) ApplyFilter(FilterType.GaussianBlur, dialog.ResultValue);
            };
            dialog.Show();
        }
        private void OnRedEyeClick(object sender, RoutedEventArgs e) => ApplyFilter(FilterType.RedEye);
        private void OnSketchClick(object sender, RoutedEventArgs e) => ApplyFilter(FilterType.Sketch);
        private void OnEdgeClick(object sender, RoutedEventArgs e) => ApplyFilter(FilterType.Edge);

        private void OnSepiaClick(object sender, RoutedEventArgs e) => ApplyFilter(FilterType.Sepia);
        private void OnOilPaintingClick(object sender, RoutedEventArgs e) => ApplyFilter(FilterType.OilPainting);
        private void OnVignetteClick(object sender, RoutedEventArgs e) => ApplyFilter(FilterType.Vignette);
        private void OnGlowClick(object sender, RoutedEventArgs e) => ApplyFilter(FilterType.Glow);

        private enum FilterType { Sepia, OilPainting, Vignette, Glow, Sharpen, Brown, Mosaic, GaussianBlur, RedEye, Sketch, Edge }

        private void OnSharpenClick(object sender, RoutedEventArgs e) => ApplyFilter(FilterType.Sharpen);
        private void OnBrownClick(object sender, RoutedEventArgs e) => ApplyFilter(FilterType.Brown);

        private async void ApplyFilter(FilterType type, int strength = 0)
        {
            if (_surface?.Bitmap == null) return;
            _router.CleanUpSelectionandShape();
            _undo.PushFullImageUndo();

            var bmp = _surface.Bitmap;
            int width = bmp.PixelWidth;
            int height = bmp.PixelHeight;
            int stride = bmp.BackBufferStride;

            byte[] rawPixels = new byte[height * stride];
            bmp.CopyPixels(rawPixels, stride, 0);

            await Task.Run(() =>
            {
                switch (type)
                {
                    case FilterType.Sepia:
                        ProcessSepia(rawPixels, width, height, stride);
                        break;
                    case FilterType.Vignette:
                        ProcessVignette(rawPixels, width, height, stride);
                        break;
                    case FilterType.Glow:
                        ProcessGlow(rawPixels, width, height, stride);
                        break;
                    case FilterType.OilPainting:
                        ProcessOilPaint(rawPixels, width, height, stride, 4, 10);
                        break;
                    case FilterType.Sharpen:
                        ProcessSharpen(rawPixels, width, height, stride);
                        break;
                    case FilterType.Brown:
                        ProcessBrown(rawPixels, width, height, stride);
                        break;
                    case FilterType.Mosaic:
                        ProcessMosaic(rawPixels, width, height, stride, strength);
                        break;
                    case FilterType.GaussianBlur:
                        ProcessGaussianBlur(rawPixels, width, height, stride, strength);
                        break;
                    case FilterType.RedEye:
                        ProcessRedEyeRemoval(rawPixels, width, height, stride);
                        break;
                    case FilterType.Sketch:
                        ProcessPencilSketch(rawPixels, width, height, stride);
                        break;
                    case FilterType.Edge:
                        ProcessEdgeDetection(rawPixels, width, height, stride);
                        break;
                }
            });

            bmp.WritePixels(new Int32Rect(0, 0, width, height), rawPixels, stride, 0);

            CheckDirtyState();
            NotifyCanvasChanged();
            SetUndoRedoButtonState();
            ShowToast($"L_Toast_Effect_{type}");
        }
        private void OnInvertColorsClick(object sender, RoutedEventArgs e)
        {
            if (_surface?.Bitmap == null) return;
            _router.CleanUpSelectionandShape();
            _undo.PushFullImageUndo();

            var bmp = _surface.Bitmap;
            bmp.Lock();
            try
            {
                unsafe
                {
                    byte* basePtr = (byte*)bmp.BackBuffer;
                    int stride = bmp.BackBufferStride;
                    int height = bmp.PixelHeight;
                    int width = bmp.PixelWidth;

                    var parallelOptions = CreatePerformanceParallelOptions();
                    Parallel.For(0, height, parallelOptions, y =>
                    {
                        byte* row = basePtr + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            row[x * 4] = (byte)(255 - row[x * 4]);
                            row[x * 4 + 1] = (byte)(255 - row[x * 4 + 1]);
                            row[x * 4 + 2] = (byte)(255 - row[x * 4 + 2]);
                        }
                    });
                }
                bmp.AddDirtyRect(new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight));
            }
            finally
            {
                bmp.Unlock();
            }
            NotifyCanvasChanged();
            SetUndoRedoButtonState();
            ShowToast("L_Toast_Effect_Inverted");
        }

        private void OnAutoLevelsClick(object sender, RoutedEventArgs e)
        {
            ProcessAutoLevels();
        }

     

        private AdjustColorWindow _adjustColorWindow;
        private WatermarkWindow _watermarkWindow;
        private ResizeCanvasDialog _resizeCanvasDialog;
        private WriteableBitmap _adjustColorUndoState;
        private byte[] _watermarkUndoPixels;
        private Int32Rect _watermarkUndoRect;

        private void OpenAdjustColorWindowSafe(int initialTabIndex)
        {
            if (_surface.Bitmap == null) return;

            if (_adjustColorWindow != null && _adjustColorWindow.IsLoaded)
            {
                _adjustColorWindow.Activate();
                return;
            }

            _router.CleanUpSelectionandShape();
            _adjustColorUndoState = _surface.Bitmap.Clone();

            _adjustColorWindow = new AdjustColorWindow(_surface.Bitmap, initialTabIndex)
            {
                Owner = this
            };

            _adjustColorWindow.Closed += (s, e) =>
            {
                if (_adjustColorWindow.Result == true)
                {
                    _undo.PushExplicitImageUndo(_adjustColorUndoState);
                    NotifyCanvasChanged();
                    CheckDirtyState();
                    SetUndoRedoButtonState();
                }
                _adjustColorWindow = null;
            };

            _adjustColorWindow.Show();
        }

        private void OnBrightnessContrastExposureClick(object sender, RoutedEventArgs e)
        {
            OpenAdjustColorWindowSafe(0);
        }
        private void OnColorTempTintSaturationClick(object sender, RoutedEventArgs e)
        {
            OpenAdjustColorWindowSafe(1);
        }

        private void OnConvertToBlackAndWhiteClick(object sender, RoutedEventArgs e)
        {
            if (_bitmap == null) return;
            _router.CleanUpSelectionandShape();
            _undo.PushFullImageUndo();
            ConvertToBlackAndWhite(_bitmap);
            CheckDirtyState();
            SetUndoRedoButtonState();
            ShowToast("L_Toast_Effect_BW");
        }

        private async void OnResizeCanvasClick(object sender, RoutedEventArgs e)
        {
            if (_surface?.Bitmap == null) return;
            _router.CleanUpSelectionandShape();
            int originalW = _surface.Bitmap.PixelWidth;
            int originalH = _surface.Bitmap.PixelHeight;

            var oldScalingMode = RenderOptions.GetBitmapScalingMode(BackgroundImage);
            _canvasResizer.SetHandleVisibility(false);
            _resizeCanvasDialog = new ResizeCanvasDialog(originalW, originalH) { Owner = this };
            var dialog = _resizeCanvasDialog;

            dialog.PreviewChanged += (w, h, isCanvasMode) =>
            {
                if (isCanvasMode)
                {
                    RenderOptions.SetBitmapScalingMode(BackgroundImage, oldScalingMode);
                    BackgroundImage.RenderTransform = Transform.Identity;
                    double offsetX = -(w - originalW) / 2.0;
                    double offsetY = -(h - originalH) / 2.0;
                    _canvasResizer.ShowPreviewRect(new Rect(offsetX, offsetY, w, h));
                }
                else
                {
                    RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.Linear);
                    double sx = (double)w / originalW;
                    double sy = (double)h / originalH;
                    BackgroundImage.RenderTransform = new ScaleTransform(sx, sy);
                    _canvasResizer.ShowPreviewRect(new Rect(0, 0, w, h));
                }
            };

            dialog.Closed += async (s, args) =>
            {
                ClearResizePreview(oldScalingMode);

                if (dialog.IsConfirmed)
                {
                    int targetWidth = dialog.ImageWidth;
                    int targetHeight = dialog.ImageHeight;
                    bool isCanvasMode = dialog.IsCanvasResizeMode;
                    bool keepRatio = dialog.IsAspectRatioLocked;
                    double scaleX = (double)targetWidth / originalW;
                    double scaleY = (double)targetHeight / originalH;

                    if (isCanvasMode) ResizeCanvasDimensions(targetWidth, targetHeight);
                    else ResizeCanvas(targetWidth, targetHeight);

                    CheckDirtyState();
                    if (_canvasResizer != null) _canvasResizer.UpdateUI();
                    if (dialog.ApplyToAll)
                    {
                        await BatchResizeImages(targetWidth, targetHeight, scaleX, scaleY, isCanvasMode, keepRatio);
                    }
                }
                _resizeCanvasDialog = null;
            };
            dialog.Show();
        }

        private void ClearResizePreview(BitmapScalingMode originalScalingMode)
        {
            RenderOptions.SetBitmapScalingMode(BackgroundImage, originalScalingMode);
            BackgroundImage.RenderTransform = Transform.Identity;
            _canvasResizer.HidePreviewRect();
            _canvasResizer.SetHandleVisibility(true);
            _canvasResizer.UpdateUI();
        }

        private void ClearResizePreview()
        {
            BackgroundImage.RenderTransform = Transform.Identity;
            _canvasResizer?.HidePreviewRect();
            _canvasResizer?.SetHandleVisibility(true);
            _canvasResizer?.UpdateUI();
        }

        private void UpdateEffectPanelTargets()
        {
            var bmp = _surface?.Bitmap;
            if (bmp == null) return;

            if (_adjustColorWindow != null && _adjustColorWindow.IsLoaded)
            {
                _adjustColorUndoState = bmp.Clone();
                _adjustColorWindow.ReloadTarget(bmp);
            }

            if (_watermarkWindow != null && _watermarkWindow.IsLoaded)
            {
                _watermarkUndoRect = new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight);
                _watermarkUndoPixels = new byte[_watermarkUndoRect.Height * bmp.BackBufferStride];
                bmp.CopyPixels(_watermarkUndoRect, _watermarkUndoPixels, bmp.BackBufferStride, 0);
                _watermarkWindow.ReloadTarget(bmp);
            }

            if (_resizeCanvasDialog != null && _resizeCanvasDialog.IsLoaded)
            {
                ClearResizePreview();
                _resizeCanvasDialog.ReloadDimensions(bmp.PixelWidth, bmp.PixelHeight);
            }
        }

        private void RefreshEffectWindowPreviews()
        {
            if (PerformanceScore <= 6) return;

            var bmp = _surface?.Bitmap;
            if (bmp == null) return;

            if (_adjustColorWindow != null && _adjustColorWindow.IsLoaded)
            {
                _adjustColorWindow.RefreshPreviewFromCanvas(bmp);
            }

            if (_watermarkWindow != null && _watermarkWindow.IsLoaded)
            {
                _watermarkWindow.RefreshDialogBackground(bmp);
            }
        }

        private void OnWatermarkClick(object sender, RoutedEventArgs e)
        {
            var oldBitmap = _surface.Bitmap;
            _watermarkUndoRect = new Int32Rect(0, 0, oldBitmap.PixelWidth, oldBitmap.PixelHeight);

            _watermarkUndoPixels = new byte[_watermarkUndoRect.Height * oldBitmap.BackBufferStride];
            oldBitmap.CopyPixels(_watermarkUndoRect, _watermarkUndoPixels, oldBitmap.BackBufferStride, 0);
            _watermarkWindow = new WatermarkWindow(_surface.Bitmap, WatermarkPreviewLayer) { Owner = this };
            var dlg = _watermarkWindow;

            dlg.Closed += async (s, args) =>
            {
                if (dlg.CurrentSettings != null)
                {
                    var newBitmap = _surface.Bitmap;
                    var redoPixels = new byte[_watermarkUndoRect.Height * newBitmap.BackBufferStride];
                    newBitmap.CopyPixels(_watermarkUndoRect, redoPixels, newBitmap.BackBufferStride, 0);

                    _undo.PushTransformAction(_watermarkUndoRect, _watermarkUndoPixels, _watermarkUndoRect, redoPixels);
                    NotifyCanvasChanged();
                    SetUndoRedoButtonState();

                    if (dlg.ApplyToAll) await ApplyWatermarkToAllTabs(dlg.CurrentSettings);
                }
                else { NotifyCanvasChanged(); }
                _watermarkWindow = null;
            };
            dlg.Show();
        }



    }
}
