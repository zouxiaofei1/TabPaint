
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TabPaint.Services;
using TabPaint.UIHandlers;
using static TabPaint.MainWindow;

//
//ImageBar后台逻辑 
//

namespace TabPaint
{

    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public bool _isSavingFile = false;
        // 用于追踪每个 Tab ID 对应的正在进行的保存任务
        private Dictionary<string, Task> _activeSaveTasks = new Dictionary<string, Task>();

        private CancellationTokenSource _saveCts;

        private void TriggerBackgroundBackup()
        {
        
            if (_currentTabItem == null) return;
            if (_surface?.Bitmap == null) return;

            // 1. 基础检查 - 只有在脏状态下才备份。
            if (_currentTabItem.IsDirty == false) return;

            if (_currentCanvasVersion == _lastBackedUpVersion &&
                !string.IsNullOrEmpty(_currentTabItem.BackupPath) &&
                File.Exists(_currentTabItem.BackupPath)) return;

            if (_saveCts != null)
            {
                _saveCts.Cancel();
                _saveCts.Dispose();
            }
            var myCts = new CancellationTokenSource();
            var token = myCts.Token;
            long versionToRecord = _currentCanvasVersion;
            string fileId = _currentTabItem.Id;
            string cacheDir = _cacheDir; // 避免闭包捕获

            var w = _surface.Bitmap.PixelWidth;
            var h = _surface.Bitmap.PixelHeight;
            var dpiX = _surface.Bitmap.DpiX;
            var dpiY = _surface.Bitmap.DpiY;
            var format = _surface.Bitmap.Format;
            var palette = _surface.Bitmap.Palette;

            // 计算步长
            int stride = (w * format.BitsPerPixel + 7) / 8;
            // 分配内存
            byte[] rawPixels = new byte[h * stride];
            try { _surface.Bitmap.CopyPixels(new Int32Rect(0, 0, w, h), rawPixels, stride, 0); }
            catch (Exception ex) { Debug.WriteLine($"CopyPixels failed: {ex.Message}"); return; }
            _lastBackedUpVersion = versionToRecord;
            _isSavingFile = true;

            Task saveTask = Task.Run(() =>
            {
                try
                {
                    if (token.IsCancellationRequested) return;

                    string fileName = $"{fileId}.png";
                    string fullPath = Path.Combine(cacheDir, fileName);

                    if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);

                    var frame = BitmapSource.Create(w, h, dpiX, dpiY, format, palette, rawPixels, stride);

                    string tempPath = fullPath + ".tmp";

                    using (var fileStream = new FileStream(tempPath, FileMode.Create))
                    {
                        BitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(frame));
                        encoder.Save(fileStream);
                    }

                    if (token.IsCancellationRequested)
                    {
                        File.Delete(tempPath);
                        return;
                    }

                    System.IO.File.Move(tempPath, fullPath, true);
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var targetTab = FileTabs.FirstOrDefault(t => t.Id == fileId);
                        if (targetTab != null)
                        {
                            targetTab.BackupPath = fullPath;
                            targetTab.LastBackupTime = DateTime.Now;
                        }
                    }, DispatcherPriority.Background);
                }
                catch (Exception ex)
                {
                    Logger.Error("AutoBackup Failed", ex);
                }
                finally
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (_activeSaveTasks.ContainsKey(fileId) && _activeSaveTasks[fileId].Id == Task.CurrentId)
                        {
                            _activeSaveTasks.Remove(fileId);
                        }
                        _isSavingFile = false;
                    });
                    myCts.Dispose(); 
                }
            }, token);
            if (_activeSaveTasks.ContainsKey(fileId)) _activeSaveTasks[fileId] = saveTask;
            else _activeSaveTasks.Add(fileId, saveTask);
        }

        private BitmapSource RenderCurrentCanvasToBitmap()
        {
            double width = BackgroundImage.ActualWidth;
            double height = BackgroundImage.ActualHeight;

            if (width <= 0 || height <= 0) return null;
            Matrix m = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;
            double dpiX = m.M11 * 96.0;
            double dpiY = m.M22 * 96.0;
            RenderTargetBitmap rtb = new RenderTargetBitmap(
                (int)(width * m.M11),
                (int)(height * m.M22),
                dpiX,
                dpiY,
                PixelFormats.Pbgra32);
            rtb.Render(BackgroundImage);

            rtb.Freeze();

            return rtb;
        }
        private BitmapSource GetHighResImageForTab(FileTabItem tabItem)
        {
            if (tabItem == null) return null;
            if (tabItem == _currentTabItem)
            {
                return RenderCurrentCanvasToBitmap();
            }
            else
            {
                if (!string.IsNullOrEmpty(tabItem.FilePath) && System.IO.File.Exists(tabItem.FilePath))
                {
                    return LoadBitmapFromFile(tabItem.FilePath);
                }
                return tabItem.Thumbnail as BitmapSource;
            }
        }
        private BitmapSource LoadBitmapFromFile(string path)
        {
            try
            {
                string ext = System.IO.Path.GetExtension(path)?.ToLower();
                if (ext == ".svg")
                {
                    using var svgFs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return DecodeSvg(svgFs, CancellationToken.None);
                }

                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                int frameIndex = GetLargestFrameIndex(decoder);
                var frame = decoder.Frames[frameIndex];

                int originalWidth = frame.PixelWidth;
                int originalHeight = frame.PixelHeight;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                fs.Position = 0;
                bitmap.StreamSource = fs;

                const int maxSize = (int)AppConsts.MaxCanvasSize;
                if (originalWidth > maxSize || originalHeight > maxSize)
                {
                    if (originalWidth >= originalHeight)
                        bitmap.DecodePixelWidth = maxSize;
                    else
                        bitmap.DecodePixelHeight = maxSize;

                    ShowToast("L_Toast_ImageTooLarge");
                }

                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                try
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (!IsWebpFileOrStream(path, fs)) return null;
                    fs.Position = 0;
                    return DecodeWebpWithSkia(fs);
                }
                catch
                {
                    return null;
                }
            }
        }

        private void InitializeAutoSave()
        {
            if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);

            _autoSaveTimer = new System.Windows.Threading.DispatcherTimer();
            _autoSaveTimer.Interval = TimeSpan.FromSeconds(AppConsts.AutoSaveDefaultDelaySeconds);
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;
        }
        public void NotifyCanvasChanged()
        {
            if (_currentTabItem == null) return;
            _pendingDeleteUndo = null;
            _currentCanvasVersion++;
            if (Mouse.LeftButton == MouseButtonState.Pressed) return;

            _autoSaveTimer.Stop();
            double delayMs = AppConsts.AutoSaveBaseDelayMs;
            if (BackgroundImage.Source is BitmapSource source)
            {
                double pixels = source.PixelWidth * source.PixelHeight;
                // 确保 PerformanceScore 不为 0，防止除以零导致 delayMs 为无穷大
                int score = Math.Max(1, PerformanceScore);
                delayMs = pixels / AppConsts.AutoSavePerformanceFactor / score;
            }

            // 限制延迟时间在 100ms 到 60s 之间，防止 TimeSpan 溢出或延迟过长
            delayMs = Math.Clamp(delayMs, 100, 60000);

            _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(delayMs);
            _autoSaveTimer.Start();
            CheckDirtyState();
        }

        private BitmapSource GenerateBlankThumbnail()
        {
            int width = AppConsts.DefaultThumbnailWidth;
            int height = AppConsts.DefaultThumbnailHeight;
            var bmp = new RenderTargetBitmap(width, height, AppConsts.StandardDpi, AppConsts.StandardDpi, PixelFormats.Pbgra32);
            var drawingVisual = new DrawingVisual();
            using (var context = drawingVisual.RenderOpen())
            {
                context.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
                context.DrawRectangle(null, new Pen(Application.Current.FindResource("ListItemPressedBrush") as Brush, 1), new Rect(0.5, 0.5, width - 1, height - 1));
            }
            bmp.Render(drawingVisual);
            bmp.Freeze();
            return bmp;
        }

        private void ResetToNewCanvas()
        {
            CreateNewTab(TabInsertPosition.AtEnd, true);
        }
        private void UpdateTabThumbnail(string path)
        {
            var tab = FileTabs.FirstOrDefault(t => t.FilePath == path);
            if (tab == null) return;

            double targetWidth = AppConsts.DefaultThumbnailWidth;
            double scale = targetWidth / _bitmap.PixelWidth;

            var transformedBitmap = new TransformedBitmap(_bitmap, new ScaleTransform(scale, scale));

            var newThumb = new WriteableBitmap(transformedBitmap);
            newThumb.Freeze();

            tab.Thumbnail = newThumb;
            string key = tab.FilePath;
            if (tab.IsNew && !string.IsNullOrEmpty(tab.BackupPath)) key = tab.BackupPath;

            if (!string.IsNullOrEmpty(key))
            {
                MainWindow.GlobalThumbnailCache.Add(key, transformedBitmap);
            }
        }
        private void UpdateTabThumbnail(FileTabItem tabItem)
        {
            if (tabItem == null || BackgroundImage.ActualWidth <= 0) return;

            try
            {
                Dispatcher.InvokeAsync(() =>
                {
                    RenderTargetBitmap rtb = new RenderTargetBitmap(
                        (int)BackgroundImage.ActualWidth,
                        (int)BackgroundImage.ActualHeight,
                        96d, 96d, PixelFormats.Pbgra32);

                    rtb.Render(BackgroundImage);
                    rtb.Freeze();

                    double scale = (double)AppConsts.DefaultThumbnailHeight / rtb.PixelHeight;
                    if (scale > 1) scale = 1;

                    var scaleTransform = new ScaleTransform(scale, scale);
                    var transformedBitmap = new TransformedBitmap(rtb, scaleTransform);
                    transformedBitmap.Freeze();

                    tabItem.Thumbnail = transformedBitmap;
                }, DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Thumbnail update failed: {ex.Message}");
            }
        }
        private void UpdateTabThumbnailFromBitmap(FileTabItem tabItem, BitmapSource bitmap)
        {
            if (tabItem == null || BackgroundImage.ActualWidth <= 0) return;

            try
            {
                RenderTargetBitmap rtb = new RenderTargetBitmap(
                    (int)bitmap.PixelWidth,
                    (int)bitmap.PixelHeight,
                    96d, 96d, PixelFormats.Pbgra32);

                double scale = (double)AppConsts.DefaultThumbnailHeight / rtb.PixelHeight;
                if (scale > 1) scale = 1;

                var scaleTransform = new ScaleTransform(scale, scale);
                var transformedBitmap = new TransformedBitmap(bitmap, scaleTransform);
                transformedBitmap.Freeze();

                tabItem.Thumbnail = transformedBitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Thumbnail update failed: {ex.Message}");
            }
        }
        private async void AutoSaveTimer_Tick(object sender, EventArgs e)
        {
            if (System.Windows.Input.Mouse.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                _autoSaveTimer.Stop();
                _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(500);
                _autoSaveTimer.Start();
                return;
            }

            _autoSaveTimer.Stop();
            if (_currentTabItem != null)
            {
                UpdateTabThumbnail(_currentTabItem);
                TriggerBackgroundBackup();
            }
        }
        private BitmapSource GetCurrentCanvasSnapshotSafe()
        {
            if (BackgroundImage == null || BackgroundImage.ActualWidth <= 0) return null;

            try
            {
                RenderTargetBitmap rtb = new RenderTargetBitmap(
                    (int)BackgroundImage.ActualWidth,
                    (int)BackgroundImage.ActualHeight,
                    96d, 96d, PixelFormats.Pbgra32);

                rtb.Render(BackgroundImage);
                var safeBitmap = new WriteableBitmap(rtb);
                safeBitmap.Freeze();

                return safeBitmap;
            }
            catch
            {
                return null;
            }
        }
        private BitmapSource GetCurrentCanvasSnapshot()
        {
            return GetCurrentCanvasSnapshotSafe();
        }
        public async void OnClosing()
        {
            if (_programClosed) return;
            _programClosed = true;

            try
            {
                if (SettingsManager.Instance.Current.DiscardAllOnExit)
                {
                    await DiscardAllInternalAsync(skipConfirmation: true);
                }

                SaveAppState();
                var favWin = FavoriteWindowManager.GetInstance();
                if (favWin != null && favWin.IsLoaded)
                {
                    favWin.Close();
                }

                this.Hide();

                if (_hwndSource != null)
                {
                    _hwndSource.RemoveHook(WndProc);
                    _hwndSource.Dispose();
                    _hwndSource = null;
                }

                if (_activeMonitorInstance == this)
                {
                    _activeMonitorInstance = null;
                    TryTransferClipboardMonitor();
                }

                SaveAppState();
                CommitPendingDeletions();
                if (_currentTabItem != null && _currentTabItem.IsDirty && !_isSavingFile)
                {
                    _autoSaveTimer.Stop();
                    var bmp = GetCurrentCanvasSnapshot();
                    if (bmp != null)
                    {
                        if (!System.IO.Directory.Exists(_cacheDir))
                        {
                            System.IO.Directory.CreateDirectory(_cacheDir);
                        }

                        string path = System.IO.Path.Combine(_cacheDir, $"{_currentTabItem.Id}.png");

                        try
                        {
                            using (var fs = new FileStream(path, FileMode.Create))
                            {
                                BitmapEncoder encoder = new PngBitmapEncoder();
                                encoder.Frames.Add(BitmapFrame.Create(bmp));
                                encoder.Save(fs);
                            }
                            _currentTabItem.BackupPath = path;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"保存退出缓存失败: {ex.Message}");
                        }
                    }
                }

                SaveSession();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnClosing 全局异常: {ex.Message}");
            }
        }
        private int GetNextAvailableUntitledNumber()
        {
            var usedNumbers = new HashSet<int>();

            foreach (var path in _imageFiles)
            {
                if (IsVirtualPath(path))
                {
                    string afterPrefix = path.Substring(VirtualFilePrefix.Length);
                    int separatorIndex = afterPrefix.IndexOf("::");
                    string numPart = separatorIndex >= 0 ? afterPrefix.Substring(0, separatorIndex) : afterPrefix;
                    if (int.TryParse(numPart, out int num))
                    {
                        usedNumbers.Add(num);
                    }
                }
            }
            foreach (var pendingTab in _pendingDeletionTabs)
            {
                if (IsVirtualPath(pendingTab.FilePath))
                {
                    string numPart = pendingTab.FilePath.Replace(VirtualFilePrefix, "");
                    if (int.TryParse(numPart, out int num))
                    {
                        usedNumbers.Add(num);
                    }
                }
            }
            int candidate = 1;
            while (usedNumbers.Contains(candidate))
            {
                candidate++;
            }
            return candidate;
        }
        public HashSet<string> _closedTabIds = new HashSet<string>();
        
        private Dictionary<string, SessionTabInfo> _offScreenBackupInfos = new Dictionary<string, SessionTabInfo>();

        private void UpdateSessionBackupInfo(string originalPath, string backupPath, bool isDirty, string id = null)
        {
            if (string.IsNullOrEmpty(originalPath)) return;

            if (!_offScreenBackupInfos.TryGetValue(originalPath, out var info))
            {
                info = new SessionTabInfo
                {
                    Id = id ?? Guid.NewGuid().ToString(),
                    OriginalPath = originalPath,
                    IsNew = IsVirtualPath(originalPath)
                };
                _offScreenBackupInfos[originalPath] = info;
            }
            else if (!string.IsNullOrEmpty(id))
            {
                info.Id = id;
            }

            info.BackupPath = backupPath;
            info.IsDirty = isDirty;
        }

        private void SaveSession()
        {
            var currentSessionTabs = new List<SessionTabInfo>();
            string currentContextDir = null;

            string? firstRealFile = _imageFiles?.FirstOrDefault(f => !string.IsNullOrEmpty(f) && !IsVirtualPath(f));

            if (firstRealFile != null)
            {
                try
                {
                    currentContextDir = System.IO.Path.GetDirectoryName(firstRealFile);
                    if (currentContextDir != null) currentContextDir = System.IO.Path.GetFullPath(currentContextDir);
                }
                catch { currentContextDir = null; }
            }

            foreach (var tab in FileTabs)
            {
                if (tab.IsNew && !tab.IsDirty) continue;

                string? tabDir = null;
                if (!string.IsNullOrEmpty(tab.FilePath) && !IsVirtualPath(tab.FilePath))
                {
                    try
                    {
                        tabDir = System.IO.Path.GetDirectoryName(tab.FilePath);
                        if (tabDir != null) tabDir = System.IO.Path.GetFullPath(tabDir);
                    }
                    catch { tabDir = null; }
                }
                if (string.IsNullOrEmpty(tabDir)) tabDir = currentContextDir;

                var info = new SessionTabInfo
                {
                    Id = tab.Id,
                    OriginalPath = tab.FilePath,
                    BackupPath = tab.BackupPath,
                    IsDirty = tab.IsDirty,
                    IsNew = tab.IsNew,
                    WorkDirectory = tabDir,
                    UntitledNumber = tab.UntitledNumber,
                    IsCleanDiskFile = (!tab.IsDirty && !tab.IsNew && !IsVirtualPath(tab.FilePath))
                };
                currentSessionTabs.Add(info);
            }

            int activeTabIndex = 0;
            if (_currentTabItem != null)
            {
                activeTabIndex = currentSessionTabs.FindIndex(t => t.Id == _currentTabItem.Id);
                if (activeTabIndex < 0) activeTabIndex = 0;
            }

            var finalTabsToSave = new List<SessionTabInfo>();
            finalTabsToSave.AddRange(currentSessionTabs);

            foreach (var offscreen in _offScreenBackupInfos.Values)
            {
                if (!finalTabsToSave.Any(t => t.OriginalPath == offscreen.OriginalPath))
                {
                    finalTabsToSave.Add(offscreen);
                }
            }
          
            if (File.Exists(_sessionPath) || File.Exists(AppConsts.LegacySessionPath))
            {
                try
                {
                    PaintSession oldSession = null;
                    if (File.Exists(_sessionPath))
                    {
                        using (var fs = new FileStream(_sessionPath, FileMode.Open, FileAccess.Read))
                        using (var reader = new BinaryReader(fs))
                        {
                            oldSession = PaintSession.Read(reader);
                        }
                    }  
                    else if (File.Exists(AppConsts.LegacySessionPath))
                    {
                        var oldJson = File.ReadAllText(AppConsts.LegacySessionPath);
                        oldSession = System.Text.Json.JsonSerializer.Deserialize<PaintSession>(oldJson);
                    }

                    if (oldSession != null && oldSession.Tabs != null)
                    {
                        // 预先收集当前已分配的编号，防止合并时冲突
                        var usedUntitledNumbers = new HashSet<int>();
                        foreach (var t in finalTabsToSave)
                        {
                            if (t.IsNew || IsVirtualPath(t.OriginalPath))
                                usedUntitledNumbers.Add(t.UntitledNumber);
                        }

                        foreach (var oldTab in oldSession.Tabs)
                        {
                            if (_closedTabIds.Contains(oldTab.Id)) continue;
                            if (oldTab.IsNew && !oldTab.IsDirty) continue;

                            string? oldTabDir = oldTab.WorkDirectory;
                            if (string.IsNullOrEmpty(oldTabDir) && !string.IsNullOrEmpty(oldTab.OriginalPath) && !IsVirtualPath(oldTab.OriginalPath))
                            {
                                try { oldTabDir = System.IO.Path.GetDirectoryName(oldTab.OriginalPath); } catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
                            }
                            if (oldTabDir != null)
                            {
                                try { oldTabDir = System.IO.Path.GetFullPath(oldTabDir); } catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
                            }

                            bool isDifferentDir = (currentContextDir == null) ||
                                                 (oldTabDir != null && !oldTabDir.Equals(currentContextDir, StringComparison.OrdinalIgnoreCase));

                            if (isDifferentDir)
                            {
                                if (!finalTabsToSave.Any(t => t.Id == oldTab.Id))
                                {
                                    // 检查未命名编号冲突
                                    if (oldTab.IsNew || IsVirtualPath(oldTab.OriginalPath))
                                    {
                                        while (usedUntitledNumbers.Contains(oldTab.UntitledNumber))
                                        {
                                            oldTab.UntitledNumber++;
                                            // 同步更新虚拟路径
                                            if (IsVirtualPath(oldTab.OriginalPath))
                                            {
                                                int sep = oldTab.OriginalPath.IndexOf("::");
                                                string idPart = sep >= 0 ? oldTab.OriginalPath.Substring(sep) : $"::{oldTab.Id}";
                                                oldTab.OriginalPath = $"{VirtualFilePrefix}{oldTab.UntitledNumber}{idPart}";
                                            }
                                        }
                                        usedUntitledNumbers.Add(oldTab.UntitledNumber);
                                    }
                                    finalTabsToSave.Add(oldTab);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Merge session failed: " + ex.Message);
                }
            }

            var session = new PaintSession
            {
                LastViewedFile = _currentTabItem?.FilePath ?? (_imageFiles.Count > _currentImageIndex ? _imageFiles[_currentImageIndex] : null),
                Tabs = finalTabsToSave,
                ActiveTabIndex = activeTabIndex 
            };

            try
            {
                string? dir = System.IO.Path.GetDirectoryName(_sessionPath);
                if (dir != null) Directory.CreateDirectory(dir);

                using (var fs = new FileStream(_sessionPath, FileMode.Create, FileAccess.Write))
                using (var writer = new BinaryWriter(fs))
                {
                    session.Write(writer);
                }
                if (File.Exists(AppConsts.LegacySessionPath))
                {
                    try { File.Delete(AppConsts.LegacySessionPath); } catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("保存会话失败: " + ex.Message);
            }
        }

        private async Task LoadSessionAsync()
        {
            if (!File.Exists(_sessionPath) && !File.Exists(AppConsts.LegacySessionPath)) return;

            try
            {
                PaintSession session = await Task.Run(() =>
                {
                    try
                    {
                        if (File.Exists(_sessionPath))
                        {
                            using (var fs = new FileStream(_sessionPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                            using (var reader = new BinaryReader(fs))
                            {
                                return PaintSession.Read(reader);
                            }
                        }
                        else if (File.Exists(AppConsts.LegacySessionPath))
                        {
                            var json = File.ReadAllText(AppConsts.LegacySessionPath);
                            return System.Text.Json.JsonSerializer.Deserialize<PaintSession>(json);
                        }
                        return null;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Session file read error: {ex.Message}");
                        return null;
                    }
                });

                if (session?.Tabs == null || session.Tabs.Count == 0) return;

                var validEntries = await Task.Run(() =>
                {
                    var results = new List<(SessionTabInfo info, bool isCleanDisk, bool hasBackup, bool fileExists)>();

                    foreach (var info in session.Tabs)
                    {
                        if (_closedTabIds.Contains(info.Id)) continue;
                        if (info.IsNew && !info.IsDirty) continue;

                        bool isCleanDisk = info.IsCleanDiskFile;
                        bool fileExists = !string.IsNullOrEmpty(info.OriginalPath) && File.Exists(info.OriginalPath);
                        bool hasBackup = !string.IsNullOrEmpty(info.BackupPath) && File.Exists(info.BackupPath);

                        if (isCleanDisk && !fileExists) continue;
                        if (!isCleanDisk && !hasBackup) continue;

                        results.Add((info, isCleanDisk, hasBackup, fileExists));
                    }

                    return results;
                });

                if (validEntries.Count == 0) return;

                var tabsToLoadThumbnails = new List<FileTabItem>();

                foreach (var (info, isCleanDisk, hasBackup, fileExists) in validEntries)
                {
                    if (FileTabs.Any(t => t.Id == info.Id)) continue;

                    FileTabItem tab;
                    if (isCleanDisk)
                    {
                        tab = new FileTabItem(info.OriginalPath)
                        {
                            Id = info.Id,
                            IsNew = false,
                            IsDirty = false,
                            BackupPath = null,
                            IsLoading = true,
                        };
                    }
                    else
                    {
                        // 检查并修复未命名编号冲突
                        if (info.IsNew || IsVirtualPath(info.OriginalPath))
                        {
                            bool conflict = FileTabs.Any(t => (t.IsNew || IsVirtualPath(t.FilePath)) && t.UntitledNumber == info.UntitledNumber);
                            if (conflict)
                            {
                                info.UntitledNumber = GetNextAvailableUntitledNumber();
                                if (IsVirtualPath(info.OriginalPath))
                                {
                                    int sep = info.OriginalPath.IndexOf("::");
                                    string idPart = sep >= 0 ? info.OriginalPath.Substring(sep) : $"::{info.Id}";
                                    info.OriginalPath = $"{VirtualFilePrefix}{info.UntitledNumber}{idPart}";
                                }
                            }
                        }

                        tab = new FileTabItem(info.OriginalPath)
                        {
                            Id = info.Id,
                            IsNew = info.IsNew,
                            IsDirty = info.IsDirty,
                            BackupPath = info.BackupPath,
                            UntitledNumber = info.UntitledNumber,
                            IsLoading = true,
                        };
                    }

                    FileTabs.Add(tab);

                    if (!_imageFiles.Contains(info.OriginalPath))
                    {
                        _imageFiles.Add(info.OriginalPath);
                    }

                    tabsToLoadThumbnails.Add(tab);
                }

                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);

                using (var semaphore = new SemaphoreSlim(AppConsts.MaxDiskConcurrency))
                {
                    var thumbnailTasks = tabsToLoadThumbnails.Select(async tab =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            await tab.LoadThumbnailAsync(AppConsts.DefaultThumbnailWidth, AppConsts.DefaultThumbnailHeight);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Thumbnail load failed for {tab.Id}: {ex.Message}");
                        }
                        finally
                        {
                            semaphore.Release();
                            await Dispatcher.InvokeAsync(() =>
                            {
                                tab.IsLoading = false;
                            }, DispatcherPriority.Background);
                        }
                    });

                    await Task.WhenAll(thumbnailTasks);
                }

                if (session.ActiveTabIndex >= 0 && session.ActiveTabIndex < FileTabs.Count)
                {
                    var targetTab = FileTabs[session.ActiveTabIndex];
                    //  SwitchToTab(targetTab, needsave: false);
                }

                UpdateImageBarVisibilityState(); UpdateImageBarSliderState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadSessionAsync Error: {ex.Message}");
            }
        }


        private void ResetDirtyTracker()
        {
            if (_undo != null) { _undo.ClearUndo(); _undo.ClearRedo(); }
            if (_currentTabItem != null && _currentTabItem.IsDirty) { _savedUndoPoint = -1; }
            else
            {
                _savedUndoPoint = 0;
                if (_currentTabItem != null) _currentTabItem.IsDirty = false;
            }
            SetUndoRedoButtonState();
        }
        enum TabInsertPosition
        {
            AfterCurrent,
            AtEnd,
            AtStart
        }
        private void CreateNewTab(TabInsertPosition tabposition = TabInsertPosition.AfterCurrent, bool switchto = false)
        {
            int availableNumber = GetNextAvailableUntitledNumber();
            string uniqueId = $"Virtual_{Guid.NewGuid():N}";
            string virtualPath = $"{VirtualFilePrefix}{availableNumber}::{uniqueId}";
            var newTab = new FileTabItem(virtualPath)
            {
                IsNew = true,
                UntitledNumber = availableNumber,
                IsDirty = false,
                Id = uniqueId,
                BackupPath = null
            };

            newTab.Thumbnail = GenerateBlankThumbnail();

            int listInsertIndex = _imageFiles.Count;

            if (_currentTabItem != null)
            {
                int currentListIndex = _imageFiles.IndexOf(_currentTabItem.FilePath);
                if (currentListIndex >= 0) listInsertIndex = currentListIndex + 1;
            }

            _imageFiles.Insert(listInsertIndex, virtualPath);

            int uiInsertIndex = _currentTabItem != null ? FileTabs.IndexOf(_currentTabItem) + 1 : FileTabs.Count;
            if (tabposition == TabInsertPosition.AfterCurrent)
                FileTabs.Insert(uiInsertIndex, newTab);
            if (tabposition == TabInsertPosition.AtEnd)
            {
                FileTabs.Add(newTab);
                if (VisualTreeHelper.GetChildrenCount(MainImageBar.TabList) > 0) MainImageBar.Scroller.ScrollToRightEnd();
            }
            if (tabposition == TabInsertPosition.AtStart)
                FileTabs.Insert(0, newTab);

            if (switchto) SwitchToTab(newTab);

            UpdateImageBarVisibilityState();
            UpdateImageBarSliderState(); 
        }

        public async void SwitchToTab(FileTabItem tab,bool needsave= true)
        {
            if (_currentTabItem == tab) return;
            if (tab == null) return;

            if (_currentTabItem != null)
            {
                _autoSaveTimer.Stop();

                bool hasNewEdits = _currentCanvasVersion != _lastBackedUpVersion;

                if (_currentTabItem.IsDirty || _currentTabItem.IsNew)
                {
                    UpdateTabThumbnail(_currentTabItem);
                }

                _currentTabItem.UndoStack = new List<UndoAction>(_undo.GetUndoStack());
                _currentTabItem.RedoStack = new List<UndoAction>(_undo.GetRedoStack());
                _currentTabItem.SavedUndoPoint = _savedUndoPoint;
                _currentTabItem.CanvasVersion = _currentCanvasVersion;
                _currentTabItem.LastBackedUpVersion = _lastBackedUpVersion;

                // 优化：只有 IsDirty 且有新编辑或备份丢失时才备份。避免空白新图片切图变脏。
                bool shouldBackup = _currentTabItem.IsDirty && (hasNewEdits || string.IsNullOrEmpty(_currentTabItem.BackupPath) || !File.Exists(_currentTabItem.BackupPath));

                if (shouldBackup && needsave)
                {
                    TriggerBackgroundBackup();
                }
            }

            tab.LastAccessTime = DateTime.Now;

            if (!IsTransferringSelection) _router.CleanUpSelectionandShape();

            foreach (var t in FileTabs) t.IsSelected = (t == tab);

            _currentFilePath = tab.FilePath;
            _currentFileName = tab.FileName;
            _currentImageIndex = _imageFiles.IndexOf(tab.FilePath);

            await OpenImageAndTabs(tab.FilePath, nobackup: true);

            _undo.ClearUndo();
            _undo.ClearRedo();

            if (tab.UndoStack != null) foreach (var action in tab.UndoStack) _undo.GetUndoStack().Add(action);
            if (tab.RedoStack != null) foreach (var action in tab.RedoStack) _undo.GetRedoStack().Add(action);

            _savedUndoPoint = tab.SavedUndoPoint;

            _currentCanvasVersion = tab.CanvasVersion;
            _lastBackedUpVersion = tab.LastBackedUpVersion;

            _currentTabItem = tab;
            SetUndoRedoButtonState();
            UpdateWindowTitle();

            if (IsTransferringSelection)
            {
                await Task.Delay(50);
                RestoreTransferredSelection();
            } 
        }

        private void ResetCanvasView()
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (ScrollContainer.ViewportWidth > 0 && ScrollContainer.ExtentWidth > 0)
                {
                    double offsetX = (ScrollContainer.ExtentWidth - ScrollContainer.ViewportWidth) / 2;
                    double offsetY = (ScrollContainer.ExtentHeight - ScrollContainer.ViewportHeight) / 2;

                    ScrollContainer.ScrollToHorizontalOffset(Math.Max(0, offsetX));
                    ScrollContainer.ScrollToVerticalOffset(Math.Max(0, offsetY));
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private bool SaveSingleTab(FileTabItem tab)
        {
            try
            {
                if (tab.IsNew && IsVirtualPath(tab.FilePath))
                {
                    var bmp = GetHighResImageForTab(tab);
                    if (bmp == null) return false;

                    Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                    dlg.FileName = tab.FileName;
                    dlg.DefaultExt = ".png";
                    dlg.Filter = "PNG Image (.png)|*.png|JPEG Image (.jpg)|*.jpg|All files (*.*)|*.*";

                    if (dlg.ShowDialog() == true)
                    {
                        string realPath = dlg.FileName;
                        string oldPath = tab.FilePath;

                        using (var fs = new FileStream(realPath, FileMode.Create))
                        {
                            BitmapEncoder encoder;
                            if (realPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                realPath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                                encoder = new JpegBitmapEncoder();
                            else
                                encoder = new PngBitmapEncoder();

                            encoder.Frames.Add(BitmapFrame.Create(bmp));
                            encoder.Save(fs);
                        }

                        int index = _imageFiles.IndexOf(oldPath);
                        if (index >= 0) _imageFiles[index] = realPath;
                        else _imageFiles.Add(realPath);

                        tab.FilePath = realPath;
                        tab.IsNew = false;
                        tab.IsDirty = false;

                        if (tab == _currentTabItem)
                        {
                            _currentFilePath = realPath;
                            _currentFileName = tab.FileName;
                            UpdateWindowTitle();
                        }

                        if (File.Exists(tab.BackupPath)) File.Delete(tab.BackupPath);
                        tab.BackupPath = null;

                        return true;
                    }
                    return false;
                }
                else
                {
                    if (tab == _currentTabItem)
                    {
                        var bmp = GetCurrentCanvasSnapshot();
                        if (bmp == null) return false;

                        using (var fs = new FileStream(tab.FilePath, FileMode.Create))
                        {
                            BitmapEncoder encoder;
                            if (tab.FilePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                tab.FilePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                                encoder = new JpegBitmapEncoder();
                            else
                                encoder = new PngBitmapEncoder();

                            encoder.Frames.Add(BitmapFrame.Create(bmp));
                            encoder.Save(fs);
                        }
                    }
                    else if (File.Exists(tab.BackupPath))
                    {
                        File.Copy(tab.BackupPath, tab.FilePath, true);
                    }
                    else
                    {
                        tab.IsDirty = false;
                        return true;
                    }

                    tab.IsDirty = false;
                    tab.IsNew = false;

                    if (File.Exists(tab.BackupPath)) File.Delete(tab.BackupPath);
                    tab.BackupPath = null;

                    return true;
                }
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_SaveFailed_Prefix"), ex.Message), ex);
                return false;
            }
        }

        public void CheckDirtyState()
        {
            if (_currentTabItem == null || _undo == null) return;

            int currentCount = _undo.UndoCount;
            bool isDirty = currentCount != _savedUndoPoint;
            if (_currentTabItem.IsDirty != isDirty)
            {
                _currentTabItem.IsDirty = isDirty;
            }
        }
        private void SaveBitmapToPng(BitmapSource bitmap, string filePath)
        {
            if (bitmap == null) return;

            using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(fileStream);
            }
        }
        private void LoadSessionForCurrentWorkspace(string workspaceFilePath)
        {
            if (!File.Exists(_sessionPath) && !File.Exists(AppConsts.LegacySessionPath)) return;

            try
            {
                string workspaceDir = System.IO.Path.GetDirectoryName(workspaceFilePath);
                if (workspaceDir != null) workspaceDir = System.IO.Path.GetFullPath(workspaceDir);

                PaintSession session = null;
                if (File.Exists(_sessionPath))
                {
                    using (var fs = new FileStream(_sessionPath, FileMode.Open, FileAccess.Read))
                    using (var reader = new BinaryReader(fs))
                    {
                        session = PaintSession.Read(reader);
                    }
                }
                else
                {
                    var json = File.ReadAllText(AppConsts.LegacySessionPath);
                    session = System.Text.Json.JsonSerializer.Deserialize<PaintSession>(json);
                }

                if (session != null && session.Tabs != null)
                {
                    foreach (var info in session.Tabs)
                    {
                        string tabDir = info.WorkDirectory;
                        if (string.IsNullOrEmpty(tabDir) && !string.IsNullOrEmpty(info.OriginalPath) && !IsVirtualPath(info.OriginalPath))
                        {
                            try { tabDir = System.IO.Path.GetDirectoryName(info.OriginalPath); } catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
                        }
                        if (tabDir != null) tabDir = System.IO.Path.GetFullPath(tabDir);

                        bool shouldLoad = false;
                        if (workspaceDir != null && tabDir != null &&
                            string.Equals(tabDir, workspaceDir, StringComparison.OrdinalIgnoreCase))
                        {
                            shouldLoad = true;
                        }

                        if (shouldLoad)
                        {
                            if (FileTabs.Any(t => t.Id == info.Id))
                            {
                                var existingTab = FileTabs.First(t => t.Id == info.Id);
                                if (info.IsCleanDiskFile)   continue;
                                if (!string.IsNullOrEmpty(info.BackupPath) && File.Exists(info.BackupPath))
                                {
                                    existingTab.BackupPath = info.BackupPath;
                                    existingTab.IsDirty = info.IsDirty;
                                    existingTab.IsNew = info.IsNew;
                                    _ = existingTab.LoadThumbnailAsync(100, 60);
                                }
                            }
                            else
                            {
                                if (info.IsCleanDiskFile)
                                {
                                    if (!string.IsNullOrEmpty(info.OriginalPath) && File.Exists(info.OriginalPath))
                                    {
                                        var tab = new FileTabItem(info.OriginalPath)
                                        {
                                            Id = info.Id,
                                            IsNew = false,
                                            IsDirty = false,
                                        };
                                        FileTabs.Add(tab);
                                        if (!_imageFiles.Contains(info.OriginalPath))
                                            _imageFiles.Add(info.OriginalPath);
                                        _ = tab.LoadThumbnailAsync(AppConsts.DefaultThumbnailWidth, AppConsts.DefaultThumbnailHeight);
                                    }
                                }
                                else if (!string.IsNullOrEmpty(info.BackupPath) && File.Exists(info.BackupPath))
                                {
                                    // 检查并修复未命名编号冲突
                                    if (info.IsNew || IsVirtualPath(info.OriginalPath))
                                    {
                                        bool conflict = FileTabs.Any(t => (t.IsNew || IsVirtualPath(t.FilePath)) && t.UntitledNumber == info.UntitledNumber);
                                        if (conflict)
                                        {
                                            info.UntitledNumber = GetNextAvailableUntitledNumber();
                                            if (IsVirtualPath(info.OriginalPath))
                                            {
                                                int sep = info.OriginalPath.IndexOf("::");
                                                string idPart = sep >= 0 ? info.OriginalPath.Substring(sep) : $"::{info.Id}";
                                                info.OriginalPath = $"{VirtualFilePrefix}{info.UntitledNumber}{idPart}";
                                            }
                                        }
                                    }

                                    var tab = new FileTabItem(info.OriginalPath)
                                    {
                                        Id = info.Id,
                                        IsNew = info.IsNew,
                                        IsDirty = info.IsDirty,
                                        BackupPath = info.BackupPath,
                                        UntitledNumber = info.UntitledNumber
                                    };
                                    FileTabs.Add(tab);
                                    if (!_imageFiles.Contains(info.OriginalPath))
                                        _imageFiles.Add(info.OriginalPath);
                                    _ = tab.LoadThumbnailAsync(AppConsts.DefaultThumbnailWidth, AppConsts.DefaultThumbnailHeight);
                                }
                            }
                        }
                    }
                }
                UpdateImageBarVisibilityState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Workspace Session Load Error: {ex.Message}");
            }
        }


        private bool IsVirtualPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith(VirtualFilePrefix);
        }
        private string GenerateVirtualPath()
        {
            int num = GetNextAvailableUntitledNumber();
            string uniqueId = $"Virtual_{Guid.NewGuid():N}";
            return $"{VirtualFilePrefix}{num}::{uniqueId}";
        }
    }
}
