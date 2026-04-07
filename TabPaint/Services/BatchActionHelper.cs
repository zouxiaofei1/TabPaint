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
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;
using TabPaint.Controls;
using TabPaint.UIHandlers;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private async Task BatchResizeImages(int targetW, int targetH, double refScaleX, double refScaleY, bool isCanvasMode, bool keepRatio)
        {
            string currentTabId = _currentTabItem?.Id;
            foreach (var path in _imageFiles.ToList())
            {
                var tab = FileTabs.FirstOrDefault(t => t.FilePath == path);
                if (tab == null) continue;
                if (tab.Id == currentTabId) continue;

                if (IsVirtualPath(path) && (string.IsNullOrEmpty(tab.BackupPath) || !File.Exists(tab.BackupPath)))
                {
                    var blank = GenerateBlankThumbnail();
                    string fullPath = Path.Combine(_cacheDir, $"{tab.Id}.png");
                    if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);
                    SaveBitmapToPng(blank, fullPath);
                    tab.BackupPath = fullPath;
                }
            }

            var tasksInfo = _imageFiles
                .Select(path =>
                {
                    var existingTab = FileTabs.FirstOrDefault(t => t.FilePath == path);
                    if (existingTab != null && existingTab.Id == currentTabId) return null;

                    string sourcePath = path;
                    string tabId = existingTab?.Id;

                    if (existingTab != null && !string.IsNullOrEmpty(existingTab.BackupPath) && File.Exists(existingTab.BackupPath))
                    {
                        sourcePath = existingTab.BackupPath;
                    }
                    else if (_offScreenBackupInfos.TryGetValue(path, out var offlineInfo) && !string.IsNullOrEmpty(offlineInfo.BackupPath) && File.Exists(offlineInfo.BackupPath))
                    {
                        sourcePath = offlineInfo.BackupPath;
                        tabId = offlineInfo.Id;
                    }

                    if (string.IsNullOrEmpty(tabId)) tabId = Guid.NewGuid().ToString();

                    return new { OriginalPath = path, SourcePath = sourcePath, TabId = tabId };
                })
                .Where(x => x != null && !string.IsNullOrEmpty(x.SourcePath) && File.Exists(x.SourcePath))
                .ToList();

            if (tasksInfo.Count == 0) return;

            string taskTitle = LocalizationManager.GetString("L_Toast_BatchResizeStart") ?? "Batch Resizing...";
            ShowToast(taskTitle);

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                TaskProgressPopup.SetIcon(AppConsts.PathTaskProgress);
                TaskProgressPopup.UpdateProgress(0, taskTitle, $"0 / {tasksInfo.Count}", "");
            });

            int processedCount = 0;
            int maxDegreeOfParallelism = Environment.ProcessorCount;
            using (var semaphore = new SemaphoreSlim(maxDegreeOfParallelism))
            {
                var tasks = new List<Task>();
                foreach (var info in tasksInfo)
                {
                    var tabToUpdate = FileTabs.FirstOrDefault(t => t.FilePath == info.OriginalPath);
                    if (tabToUpdate != null) tabToUpdate.IsLoading = true;

                    var task = Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            string newCachePath = null;
                            BitmapSource thumbnailResult = null;

                            Thread renderThread = new Thread(() =>
                            {
                                try
                                {
                                    var bmp = new BitmapImage();
                                    bmp.BeginInit();
                                    bmp.UriSource = new Uri(Path.GetFullPath(info.SourcePath));
                                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                                    bmp.EndInit();
                                    bmp.Freeze();

                                    BitmapSource resultBmp = null;
                                    if (isCanvasMode)
                                    {
                                        resultBmp = ResizeBitmapCanvas(bmp, targetW, targetH);
                                    }
                                    else
                                    {
                                        int finalW, finalH;
                                        if (keepRatio)
                                        {
                                            finalW = (int)Math.Round(bmp.PixelWidth * refScaleX);
                                            finalH = (int)Math.Round(bmp.PixelHeight * refScaleY);
                                            finalW = Math.Max(1, finalW);
                                            finalH = Math.Max(1, finalH);
                                        }
                                        else
                                        {
                                            finalW = targetW;
                                            finalH = targetH;
                                        }
                                        BitmapSource bgraBmp = (bmp.Format == PixelFormats.Bgra32) ? bmp : new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);

                                        using var skSrc = new SKBitmap(bgraBmp.PixelWidth, bgraBmp.PixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
                                        bgraBmp.CopyPixels(new Int32Rect(0, 0, bgraBmp.PixelWidth, bgraBmp.PixelHeight), skSrc.GetPixels(), bgraBmp.PixelHeight * (bgraBmp.PixelWidth * 4), bgraBmp.PixelWidth * 4);

                                        using var skDest = new SKBitmap(finalW, finalH, SKColorType.Bgra8888, SKAlphaType.Premul);
                                        skSrc.ScalePixels(skDest, SKFilterQuality.High);

                                        resultBmp = SkiaBitmapToWpfSource(skDest);
                                    }

                                    resultBmp.Freeze();
                                    if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);
                                    string fileName = $"{info.TabId}_resize_{DateTime.Now.Ticks}.png";
                                    string fullPath = Path.Combine(_cacheDir, fileName);

                                    using (var fs = new FileStream(fullPath, FileMode.Create))
                                    {
                                        BitmapEncoder encoder = new PngBitmapEncoder();
                                        encoder.Frames.Add(BitmapFrame.Create(resultBmp));
                                        encoder.Save(fs);
                                    }
                                    newCachePath = fullPath;
                                    if (resultBmp.PixelWidth > 200)
                                    {
                                        var scale = 200.0 / resultBmp.PixelWidth;
                                        var thumb = new TransformedBitmap(resultBmp, new ScaleTransform(scale, scale));
                                        thumb.Freeze();
                                        thumbnailResult = thumb;
                                    }
                                    else
                                    {
                                        thumbnailResult = resultBmp;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Batch Resize Error: {ex.Message}");
                                }
                            });
                            renderThread.SetApartmentState(ApartmentState.STA);
                            renderThread.IsBackground = true;
                            renderThread.Start();
                            renderThread.Join();

                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                if (!string.IsNullOrEmpty(newCachePath))
                                {
                                    var tab = FileTabs.FirstOrDefault(t => t.FilePath == info.OriginalPath);
                                    if (tab != null)
                                    {
                                        tab.BackupPath = newCachePath;
                                        tab.IsDirty = true;
                                        tab.LastBackupTime = DateTime.Now;
                                        if (thumbnailResult != null) tab.Thumbnail = thumbnailResult;
                                        tab.IsLoading = false;
                                    }
                                    UpdateSessionBackupInfo(info.OriginalPath, newCachePath, true, info.TabId);
                                }
                                else
                                {
                                    var tab = FileTabs.FirstOrDefault(t => t.FilePath == info.OriginalPath);
                                    if (tab != null) tab.IsLoading = false;
                                }

                                processedCount++;
                                double p = (double)processedCount / tasksInfo.Count * 100;
                                TaskProgressPopup.UpdateProgress(p, null, $"{processedCount} / {tasksInfo.Count}", "");
                            });
                        }
                        finally { semaphore.Release(); }
                    });
                    tasks.Add(task);
                }
                await Task.WhenAll(tasks);
            }

            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                SaveSession();
                TaskProgressPopup.Finish();
            }, System.Windows.Threading.DispatcherPriority.Background);
            ShowToast(LocalizationManager.GetString("L_Toast_BatchResizeComplete") ?? "Batch resize complete.");
        }
        private async Task ApplyWatermarkToAllTabs(WatermarkSettings settings)
        {
            if (settings == null) return;
            string currentTabId = _currentTabItem?.Id;

            // 预处理：确保所有虚拟路径或无物理文件的标签页都有备份文件
            foreach (var path in _imageFiles.ToList())
            {
                var tab = FileTabs.FirstOrDefault(t => t.FilePath == path);
                if (tab == null) continue;
                if (tab.Id == currentTabId) continue;

                if (IsVirtualPath(path) && (string.IsNullOrEmpty(tab.BackupPath) || !File.Exists(tab.BackupPath)))
                {
                    // 为空白虚拟标签页生成备份图
                    var blank = GenerateBlankThumbnail();
                    string fullPath = Path.Combine(_cacheDir, $"{tab.Id}.png");
                    if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);
                    SaveBitmapToPng(blank, fullPath);
                    tab.BackupPath = fullPath;
                }
            }

            var tasksInfo = _imageFiles
                .Select(path =>
                {
                    var existingTab = FileTabs.FirstOrDefault(t => t.FilePath == path);
                    if (existingTab != null && existingTab.Id == currentTabId) return null;

                    string sourcePath = path;
                    string tabId = existingTab?.Id;

                    if (existingTab != null && !string.IsNullOrEmpty(existingTab.BackupPath) && File.Exists(existingTab.BackupPath))
                    {
                        sourcePath = existingTab.BackupPath;
                    }
                    else if (_offScreenBackupInfos.TryGetValue(path, out var offlineInfo) && !string.IsNullOrEmpty(offlineInfo.BackupPath) && File.Exists(offlineInfo.BackupPath))
                    {
                        sourcePath = offlineInfo.BackupPath;
                        tabId = offlineInfo.Id;
                    }

                    if (string.IsNullOrEmpty(tabId)) tabId = Guid.NewGuid().ToString();

                    return new { OriginalPath = path, SourcePath = sourcePath, TabId = tabId };
                })
                .Where(x => x != null && !string.IsNullOrEmpty(x.SourcePath) && File.Exists(x.SourcePath))
                .ToList();

            if (tasksInfo.Count == 0) return;

            string taskTitle = LocalizationManager.GetString("L_Toast_BatchStart") ?? "Batch Processing...";
            ShowToast(taskTitle);

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                TaskProgressPopup.SetIcon(AppConsts.PathTaskProgress);
                TaskProgressPopup.UpdateProgress(0, taskTitle, $"0 / {tasksInfo.Count}", "");
            });

            int processedCount = 0;
            int maxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2);
            using (var semaphore = new SemaphoreSlim(maxDegreeOfParallelism))
            {
                var tasks = new List<Task>();
                foreach (var info in tasksInfo)
                {
                    var tabToUpdate = FileTabs.FirstOrDefault(t => t.FilePath == info.OriginalPath);
                    if (tabToUpdate != null) tabToUpdate.IsLoading = true;

                    var task = Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            string? newCachePath = null;
                            BitmapSource? thumbnailResult = null;

                            // 批量处理性能优化：使用单一长期运行的 STA 任务避免频繁线程创建开销
                            var tcs = new TaskCompletionSource<bool>();
                            var renderThread = new Thread(() =>
                            {
                                try
                                {
                                    var bmp = new BitmapImage();
                                    bmp.BeginInit();
                                    bmp.UriSource = new Uri(Path.GetFullPath(info.SourcePath));
                                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                                    bmp.EndInit();
                                    bmp.Freeze();

                                    var renderedBmp = WatermarkWindow.ApplyWatermarkToBitmap(bmp, settings);

                                    if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);
                                    string fullPath = Path.Combine(_cacheDir, $"{info.TabId}_{DateTime.Now.Ticks}.png");

                                    using (var fileStream = new FileStream(fullPath, FileMode.Create))
                                    {
                                        var encoder = new PngBitmapEncoder();
                                        encoder.Frames.Add(BitmapFrame.Create(renderedBmp));
                                        encoder.Save(fileStream);
                                    }
                                    newCachePath = fullPath;

                                    // 缩略图生成优化
                                    if (renderedBmp.PixelWidth > 200)
                                    {
                                        double scale = 200.0 / renderedBmp.PixelWidth;
                                        var thumb = new TransformedBitmap(renderedBmp, new ScaleTransform(scale, scale));
                                        thumb.Freeze();
                                        thumbnailResult = thumb;
                                    }
                                    else thumbnailResult = renderedBmp;

                                    tcs.SetResult(true);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Batch WM Error: {ex.Message}");
                                    tcs.SetException(ex);
                                }
                            });
                            renderThread.SetApartmentState(ApartmentState.STA);
                            renderThread.Start();
                            await tcs.Task;

                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                if (!string.IsNullOrEmpty(newCachePath))
                                {
                                    var tab = FileTabs.FirstOrDefault(t => t.FilePath == info.OriginalPath);
                                    if (tab != null)
                                    {
                                        tab.BackupPath = newCachePath;
                                        tab.IsDirty = true;
                                        tab.LastBackupTime = DateTime.Now;
                                        if (thumbnailResult != null) tab.Thumbnail = thumbnailResult;
                                        tab.IsLoading = false;
                                    }
                                    UpdateSessionBackupInfo(info.OriginalPath, newCachePath, true, info.TabId);
                                }
                                else
                                {
                                    var tab = FileTabs.FirstOrDefault(t => t.FilePath == info.OriginalPath);
                                    if (tab != null) tab.IsLoading = false;
                                }

                                processedCount++;
                                double p = (double)processedCount / tasksInfo.Count * 100;
                                TaskProgressPopup.UpdateProgress(p, null, $"{processedCount} / {tasksInfo.Count}", "");
                            });
                        }
                        finally { semaphore.Release(); }
                    });
                    tasks.Add(task);
                }
                await Task.WhenAll(tasks);
            }

            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                SaveSession();
                TaskProgressPopup.Finish();
            }, System.Windows.Threading.DispatcherPriority.Background);
            ShowToast(LocalizationManager.GetString("L_Toast_BatchComplete") ?? "Batch watermark applied.");
        }
    }
}