
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static TabPaint.MainWindow;

//
//ImageBar后台逻辑 
//

namespace TabPaint
{
  
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public bool _isSavingFile = false;
        private System.Collections.Concurrent.ConcurrentDictionary<string, Task> _activeSaveTasks
    = new System.Collections.Concurrent.ConcurrentDictionary<string, Task>();
        private void TriggerBackgroundBackup(BitmapSource? existingSnapshot = null)
        {
            if (_currentTabItem == null) return;
            // 如果没有修改且不是新文件，不需要备份
            if (_currentTabItem.IsDirty == false && !_currentTabItem.IsNew) return;
            if (_currentCanvasVersion == _lastBackedUpVersion &&
        !string.IsNullOrEmpty(_currentTabItem.BackupPath) &&
        File.Exists(_currentTabItem.BackupPath))
            {
                return;
            }
         
            long versionToRecord = _currentCanvasVersion;
            _lastBackedUpVersion = versionToRecord;
            // 1. 获取快照 (必须在 UI 线程完成)
            // 修复点：这里获取到的必须是纯净的、与渲染线程解绑的 Bitmap
            BitmapSource bitmap = existingSnapshot ?? GetCurrentCanvasSnapshotSafe();

            if (bitmap == null) return;
            _isSavingFile = true;
            // 再次确保冻结
            if (bitmap.IsFrozen == false) bitmap.Freeze();

            var tabToSave = _currentTabItem;
            string fileId = tabToSave.Id; // 保存 ID，防止闭包变量在线程中变化

            // 2. 启动后台保存任务
            var saveTask = Task.Run(() =>
            {
                try
                {
                    string fileName = $"{fileId}.png";
                    string fullPath = System.IO.Path.Combine(_cacheDir, fileName);

                   
                    // 确保目录存在
                    if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);

                    using (var fileStream = new FileStream(fullPath, FileMode.Create))
                    {
                        BitmapEncoder encoder = new PngBitmapEncoder();
                        // 此时 bitmap 是 WriteableBitmap，后台线程可以安全访问
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        encoder.Save(fileStream);
                    }

                    // 更新 Tab 信息 (注意线程安全，虽然属性设置通常会自动 Marshaling，但最好 Invoke)
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var targetTab = FileTabs.FirstOrDefault(t => t.Id == fileId);
                        if (targetTab != null)
                        {
                            targetTab.BackupPath = fullPath;
                            targetTab.LastBackupTime = DateTime.Now;
                        }
                    }); _isSavingFile = false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AutoBackup Failed: {ex.Message}"); _isSavingFile = false;
                }
                finally
                {
                    // 任务完成后，从字典中移除
                    _activeSaveTasks.TryRemove(fileId, out _);
                    _isSavingFile = false;
                }
            });

            // 3. 将任务加入字典，防止还没存完就去读
            _activeSaveTasks.TryAdd(fileId, saveTask);
            SaveSession(); // 更新 JSON
        }

        private BitmapSource RenderCurrentCanvasToBitmap()
        {
            double width = BackgroundImage.ActualWidth;
            double height = BackgroundImage.ActualHeight;

            if (width <= 0 || height <= 0) return null;

            // 2. 获取屏幕 DPI (适配高分屏)
            Matrix m = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;
            double dpiX = m.M11 * 96.0;
            double dpiY = m.M22 * 96.0;

            // 3. 创建渲染目标
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
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // 关键：加载后释放文件占用
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch { return null; }
        }

        private void InitializeAutoSave()
        {
            // 确保缓存目录存在
            if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);

            _autoSaveTimer = new System.Windows.Threading.DispatcherTimer();
            _autoSaveTimer.Interval = TimeSpan.FromSeconds(3); // 3秒停笔后触发
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;
        }

        // 在 MainWindow 类中修改 NotifyCanvasChanged 方法

        public void NotifyCanvasChanged()
        {
            _currentCanvasVersion++;
            _autoSaveTimer.Stop();
            double delayMs = 2000; // 基础延迟 2秒
            if (BackgroundImage.Source is BitmapSource source)
            {
                double pixels = source.PixelWidth * source.PixelHeight;
                if (pixels > 3840 * 2160) delayMs = 2000; // 大图给 5秒
                else if (pixels > 1920 * 1080) delayMs = 1000; // 中图给 3秒
                else delayMs = 500; // 小图 1.5秒，响应更快
            }
            _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(delayMs);
            _autoSaveTimer.Start();
            CheckDirtyState();
        }

        private BitmapSource GenerateBlankThumbnail()
        {
            int width = 100;
            int height = 60;
            var bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            var drawingVisual = new DrawingVisual();
            using (var context = drawingVisual.RenderOpen())
            {
                // 绘制白色背景
                context.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
                // 可选：画一个浅灰色边框让它看得清楚
                context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(220, 220, 220)), 1), new Rect(0.5, 0.5, width - 1, height - 1));
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
            // 在 ObservableCollection 中找到对应的 Tab
            var tab = FileTabs.FirstOrDefault(t => t.FilePath == path);
            if (tab == null) return;

            double targetWidth = 100;
            double scale = targetWidth / _bitmap.PixelWidth;

            var transformedBitmap = new TransformedBitmap(_bitmap, new ScaleTransform(scale, scale));

            var newThumb = new WriteableBitmap(transformedBitmap);
            newThumb.Freeze();

            // 触发 UI 更新
            tab.Thumbnail = newThumb;
        }
        private void UpdateTabThumbnail(FileTabItem tabItem)
        {//用当前canvas更新tabitem的thumbail
            if (tabItem == null || BackgroundImage.ActualWidth <= 0) return;

            try
            {
                RenderTargetBitmap rtb = new RenderTargetBitmap(
                    (int)BackgroundImage.ActualWidth,
                    (int)BackgroundImage.ActualHeight,
                    96d, 96d, PixelFormats.Pbgra32);

                rtb.Render(BackgroundImage);
                rtb.Freeze(); // 冻结以便跨线程使用（如果需要）

                double scale = 60.0 / rtb.PixelHeight;
                if (scale > 1) scale = 1; // 不放大

                var scaleTransform = new ScaleTransform(scale, scale);
                var transformedBitmap = new TransformedBitmap(rtb, scaleTransform);
                transformedBitmap.Freeze();

                // 3. 立即更新 UI (ViewModel)
                tabItem.Thumbnail = transformedBitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Thumbnail update failed: {ex.Message}");
            }
        }
        private void UpdateTabThumbnailFromBitmap(FileTabItem tabItem,BitmapSource bitmap)
        {//用当前canvas更新tabitem的thumbail
            if (tabItem == null || BackgroundImage.ActualWidth <= 0) return;

            try
            {
                RenderTargetBitmap rtb = new RenderTargetBitmap(
                    (int)bitmap.PixelWidth,
                    (int)bitmap.PixelHeight,
                    96d, 96d, PixelFormats.Pbgra32);

     
                double scale = 60.0 / rtb.PixelHeight;
                if (scale > 1) scale = 1; // 不放大

                var scaleTransform = new ScaleTransform(scale, scale);
                var transformedBitmap = new TransformedBitmap(bitmap, scaleTransform);
                transformedBitmap.Freeze();

                // 3. 立即更新 UI (ViewModel)
                tabItem.Thumbnail = transformedBitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Thumbnail update failed: {ex.Message}");
            }
        }

        private async void AutoSaveTimer_Tick(object sender, EventArgs e)
        {
            _autoSaveTimer.Stop(); // 停止计时，直到下次改动
            if (_currentTabItem != null)
            {
                // 1. 先更新 UI 缩略图 (用户立刻看到变化)
                UpdateTabThumbnail(_currentTabItem);
                TriggerBackgroundBackup();
            }
        }
        private BitmapSource GetCurrentCanvasSnapshotSafe()
        {
            if (BackgroundImage == null || BackgroundImage.ActualWidth <= 0) return null;

            try
            {
                // 1. 渲染
                RenderTargetBitmap rtb = new RenderTargetBitmap(
                    (int)BackgroundImage.ActualWidth,
                    (int)BackgroundImage.ActualHeight,
                    96d, 96d, PixelFormats.Pbgra32);

                rtb.Render(BackgroundImage);

                // 2. 关键修复：深拷贝为 WriteableBitmap
                // 这会将显存/MIL资源中的数据拷贝到系统内存，彻底切断线程关联
                var safeBitmap = new WriteableBitmap(rtb);
                safeBitmap.Freeze(); // 冻结以供跨线程使用

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
        protected async void OnClosing()
        {

            try
            {
                this.Hide();
                SaveAppState();
                // 立即保存当前的
                if (_currentTabItem != null && _currentTabItem.IsDirty && !_isSavingFile)
                {
                    _autoSaveTimer.Stop();
                    var bmp = GetCurrentCanvasSnapshot();
                    if (bmp != null)
                    {
                        // 【修复核心】：检查并重建目录
                        if (!System.IO.Directory.Exists(_cacheDir))
                        {
                            System.IO.Directory.CreateDirectory(_cacheDir);
                        }

                        string path = System.IO.Path.Combine(_cacheDir, $"{_currentTabItem.Id}.png");

                        // 使用 try-catch 包裹具体的文件写入，防止单个文件写入失败导致崩溃
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
                            // 可以在这里记录日志，但不要抛出异常，让程序继续关闭
                            System.Diagnostics.Debug.WriteLine($"保存退出缓存失败: {ex.Message}");
                        }
                    }
                }

                SaveSession(); // 更新 JSON
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnClosing 全局异常: {ex.Message}");
            }
            finally
            {
                // 确保无论如何都会执行关闭逻辑
                this.Hide();
                _programClosed = true;
                Close();
            }
        }
        private int GetNextAvailableUntitledNumber()
        {
            // 获取当前正在使用的所有“未命名”编号
            var usedNumbers = new HashSet<int>();

            foreach (var path in _imageFiles)
            {
                if (IsVirtualPath(path))
                {
                    // 提取 ::TABPAINT_NEW:: 之后的数字
                    string numPart = path.Replace(VirtualFilePrefix, "");
                    if (int.TryParse(numPart, out int num))
                    {
                        usedNumbers.Add(num);
                    }
                }
            }

            // 从 1 开始找，第一个不在 HashSet 里的数字就是我们要的
            int candidate = 1;
            while (usedNumbers.Contains(candidate))
            {
                candidate++;
            }
            return candidate;
        }

        private void SaveSession()
        {
            // 1. 准备当前内存中的数据
            var currentSessionTabs = new List<SessionTabInfo>();

            // 获取当前上下文目录：必须基于真实的物理文件路径
            string currentContextDir = null;

            // 优先从 _imageFiles 列表中找第一个真实的文件路径来确定当前“工作目录”
            string? firstRealFile = _imageFiles?.FirstOrDefault(f => !string.IsNullOrEmpty(f) && !IsVirtualPath(f));

            if (firstRealFile != null)
            {
                try
                {
                    currentContextDir = System.IO.Path.GetDirectoryName(firstRealFile);
                    if (currentContextDir != null)
                        currentContextDir = System.IO.Path.GetFullPath(currentContextDir);
                }
                catch { currentContextDir = null; }
            }

            foreach (var tab in FileTabs)
            {
                // 只有修改过的或新建的才需要记入 Session
                if (tab.IsDirty || tab.IsNew)
                {
                    string? tabDir = null;

                    // 如果是真实文件，获取它的实际目录
                    if (!string.IsNullOrEmpty(tab.FilePath) && !IsVirtualPath(tab.FilePath))
                    {
                        try
                        {
                            tabDir = System.IO.Path.GetDirectoryName(tab.FilePath);
                            if (tabDir != null) tabDir = System.IO.Path.GetFullPath(tabDir);
                        }
                        catch { tabDir = null; }
                    }

                    // 如果是新建文件（虚拟路径）或者获取目录失败，
                    // 将其 WorkDirectory 归类为当前活跃目录，这样它会随当前文件夹一起加载
                    if (string.IsNullOrEmpty(tabDir))
                    {
                        tabDir = currentContextDir;
                    }

                    currentSessionTabs.Add(new SessionTabInfo
                    {
                        Id = tab.Id,
                        OriginalPath = tab.FilePath, // 这里可能是 ::TABPAINT_NEW::1
                        BackupPath = tab.BackupPath,
                        IsDirty = tab.IsDirty,
                        IsNew = tab.IsNew,
                        WorkDirectory = tabDir,
                        UntitledNumber = tab.UntitledNumber
                    });
                }
            }

            var finalTabsToSave = new List<SessionTabInfo>();
            finalTabsToSave.AddRange(currentSessionTabs);

            // 2. 合并旧 Session 数据
            if (File.Exists(_sessionPath))
            {
                try
                {
                    var oldJson = File.ReadAllText(_sessionPath);
                    var oldSession = System.Text.Json.JsonSerializer.Deserialize<PaintSession>(oldJson);

                    if (oldSession != null && oldSession.Tabs != null)
                    {
                        // 如果当前有确定的目录，则保留其他目录的 Tab
                        foreach (var oldTab in oldSession.Tabs)
                        {
                            string? oldTabDir = oldTab.WorkDirectory;

                            // 兼容逻辑：处理旧数据中没有 WorkDirectory 的情况
                            if (string.IsNullOrEmpty(oldTabDir) && !string.IsNullOrEmpty(oldTab.OriginalPath) && !IsVirtualPath(oldTab.OriginalPath))
                            {
                                try { oldTabDir = System.IO.Path.GetDirectoryName(oldTab.OriginalPath); } catch { }
                            }

                            if (oldTabDir != null)
                            {
                                try { oldTabDir = System.IO.Path.GetFullPath(oldTabDir); } catch { }
                            }

                            bool isDifferentDir = (currentContextDir == null) ||
                                                 (oldTabDir != null && !oldTabDir.Equals(currentContextDir, StringComparison.OrdinalIgnoreCase));

                            if (isDifferentDir)
                            {
                                if (!finalTabsToSave.Any(t => t.Id == oldTab.Id))
                                    finalTabsToSave.Add(oldTab);
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
                Tabs = finalTabsToSave
            };

            try
            {
                string? dir = System.IO.Path.GetDirectoryName(_sessionPath);
                if (dir != null) Directory.CreateDirectory(dir);

                string json = System.Text.Json.JsonSerializer.Serialize(session);
                File.WriteAllText(_sessionPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("保存会话失败: " + ex.Message);
            }
        }


        private void LoadSession()
        {
            if (!File.Exists(_sessionPath)) return;

            try
            {
                var json = File.ReadAllText(_sessionPath);
                var session = System.Text.Json.JsonSerializer.Deserialize<PaintSession>(json);
                string startupDir = null;
                if (!string.IsNullOrEmpty(_currentFilePath))
                {
                    startupDir = System.IO.Path.GetDirectoryName(_currentFilePath);
                    // 统一路径格式（去掉末尾斜杠等，防止匹配失败），可选
                    if (startupDir != null) startupDir = System.IO.Path.GetFullPath(startupDir);
                }

                if (session.Tabs != null)
                {
                    foreach (var info in session.Tabs)
                    {
                        // [新增] 2. 过滤逻辑
                        if (startupDir != null)
                        {
                            string tabDir = info.WorkDirectory;

                            // 如果旧版本没有记录 WorkDirectory，尝试从 OriginalPath 推断
                            if (string.IsNullOrEmpty(tabDir) && !string.IsNullOrEmpty(info.OriginalPath))
                            {
                                tabDir = System.IO.Path.GetDirectoryName(info.OriginalPath);
                            }

                            if (tabDir != null) tabDir = System.IO.Path.GetFullPath(tabDir);

                            if (string.Compare(tabDir, startupDir, StringComparison.OrdinalIgnoreCase) != 0)
                            {
                                continue;
                            }
                        }

                        if (!string.IsNullOrEmpty(info.BackupPath) && File.Exists(info.BackupPath))
                        {
                            var tab = new FileTabItem(info.OriginalPath)
                            {
                                Id = info.Id,
                                IsNew = info.IsNew,
                                IsDirty = info.IsDirty,
                                BackupPath = info.BackupPath,
                                UntitledNumber = info.UntitledNumber,
                                IsLoading = true,
                            };
                            //s(tab.BackupPath);
                            FileTabs.Add(tab);

                            _ = tab.LoadThumbnailAsync(100, 60).ContinueWith(t =>
                            {
                                tab.IsLoading = false;
                            }, TaskScheduler.FromCurrentSynchronizationContext());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Session Load Error: {ex.Message}");
            }
        }
        private void ResetDirtyTracker()
        {
            // 1. 清空撤销栈
            if (_undo != null)
            {
                _undo.ClearUndo();
                _undo.ClearRedo();
            }

            // 2. 智能重置保存点
            if (_currentTabItem != null && _currentTabItem.IsDirty)
            {
                _savedUndoPoint = -1;
            }
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
        private void CreateNewTab(TabInsertPosition tabposition= TabInsertPosition.AfterCurrent, bool switchto = false)
        {
            // 1. 【核心修改】动态计算下一个可用的编号 (找空位)
            int availableNumber = GetNextAvailableUntitledNumber();
            string virtualPath = $"{VirtualFilePrefix}{availableNumber}";

            // 2. 创建 Tab 对象
            var newTab = new FileTabItem(virtualPath)
            {
                IsNew = true,
                UntitledNumber = availableNumber, // 关键：记录这个编号
                IsDirty = false
            };

            newTab.Thumbnail = GenerateBlankThumbnail();

            // 3. 确定插入位置 (保持之前的逻辑)
            int listInsertIndex = _imageFiles.Count;
            if (_currentTabItem != null)
            {
                int currentListIndex = _imageFiles.IndexOf(_currentTabItem.FilePath);
                if (currentListIndex >= 0) listInsertIndex = currentListIndex + 1;
            }

            // 4. 更新数据源
            _imageFiles.Insert(listInsertIndex, virtualPath);

            // UI 插入逻辑 (这里为了简单，我们插在当前选中项后面，或者末尾)
          
            int uiInsertIndex = _currentTabItem != null ? FileTabs.IndexOf(_currentTabItem) + 1 : FileTabs.Count;
            if (tabposition== TabInsertPosition.AfterCurrent) 
                FileTabs.Insert(uiInsertIndex, newTab);
            if (tabposition == TabInsertPosition.AtEnd)
            {
                FileTabs.Add(newTab); if (VisualTreeHelper.GetChildrenCount(MainImageBar.TabList) > 0)
                {
                    MainImageBar.Scroller.ScrollToRightEnd();
                }
            }
            if (tabposition == TabInsertPosition.AtStart)
                FileTabs.Insert(0, newTab);

            // 5. 切换逻辑
            if (switchto)
            {
                SwitchToTab(newTab); // 建议封装一下切换逻辑
            }

            UpdateImageBarSliderState();
        }

        private async void SwitchToTab(FileTabItem tab)
        {
           
            if (_currentTabItem == tab) return;
            if (tab == null) return;
           
            if (_currentTabItem != null)
            {
                _autoSaveTimer.Stop();

                if (_currentTabItem.IsDirty || _currentTabItem.IsNew)
                {
                    UpdateTabThumbnail(_currentTabItem);
                }
            }
            _router.CleanUpSelectionandShape();
            // 1. UI 选中状态同步
            foreach (var t in FileTabs) t.IsSelected = (t == tab);
           

            // 2. 核心变量同步
            _currentFilePath = tab.FilePath;
            _currentFileName = tab.FileName;
            _currentImageIndex = _imageFiles.IndexOf(tab.FilePath);
            //if (tab.IsNew)
            //{
            //    // 如果有备份（缓存），加载备份
            //    if (!string.IsNullOrEmpty(tab.BackupPath) && File.Exists(tab.BackupPath))
            //    {
            //        await OpenImageAndTabs(tab.BackupPath);
            //    }
            //    else
            //    {
            //        // 纯内存新图
            //        Clean_bitmap(1200, 900);
            //    }
            //}
            //else
            {
                await OpenImageAndTabs(tab.FilePath);
            }
    
                // 4. 状态重置
                ResetDirtyTracker();
 _currentTabItem = tab;//删除会导致创建未命名→不绘画→切回原图失败bug
            UpdateWindowTitle();
        }

        // 放在 MainWindow 内部
        private void ResetCanvasView()
        {
            // 使用 Loaded 优先级，确保 ScrollViewer 已经感知到了新的图片尺寸
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (ScrollContainer.ViewportWidth > 0 && ScrollContainer.ExtentWidth > 0)
                {
                    double offsetX = (ScrollContainer.ExtentWidth - ScrollContainer.ViewportWidth) / 2;
                    double offsetY = (ScrollContainer.ExtentHeight - ScrollContainer.ViewportHeight) / 2;

                    // 3. 执行滚动 (防止负数)
                    ScrollContainer.ScrollToHorizontalOffset(Math.Max(0, offsetX));
                    ScrollContainer.ScrollToVerticalOffset(Math.Max(0, offsetY));
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private bool SaveSingleTab(FileTabItem tab)
        {
            try
            {
                // 1. 虚拟路径处理 (新建文件)
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
                        string oldPath = tab.FilePath; // 记录虚拟路径

                        // 保存文件
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

                        // 更新数据列表
                        int index = _imageFiles.IndexOf(oldPath);
                        if (index >= 0) _imageFiles[index] = realPath;
                        else _imageFiles.Add(realPath);

                        // 更新 Tab 状态
                        tab.FilePath = realPath;
                        tab.IsNew = false;
                        tab.IsDirty = false;

                        if (tab == _currentTabItem)
                        {
                            _currentFilePath = realPath;
                            _currentFileName = tab.FileName;
                            UpdateWindowTitle();
                        }

                        // 清理备份
                        if (File.Exists(tab.BackupPath)) File.Delete(tab.BackupPath);
                        tab.BackupPath = null;

                        return true; // 保存成功
                    }
                    return false; // 用户取消了对话框
                }
                else
                {
                    // 2. 普通文件保存 (已有路径)
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

                            encoder.Frames.Add(BitmapFrame.Create(bmp)); // 修复：确保 Frame 被添加
                            encoder.Save(fs);
                        }
                    }
                    else if (File.Exists(tab.BackupPath))
                    {
                        // 后台标签：将缓存覆盖到原位
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
                ShowToast($"保存失败: {ex.Message}");
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
        private bool IsVirtualPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith(VirtualFilePrefix);
        }

        // 辅助方法：生成唯一的虚拟路径
        private string GenerateVirtualPath()
        {
            // 格式： ::TABPAINT_NEW::{ID}
            return $"{VirtualFilePrefix}{GetNextAvailableUntitledNumber()}";
        }
    }
}