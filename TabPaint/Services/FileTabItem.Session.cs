
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

//
//ImageBar后台逻辑 
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
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

            // 再次确保冻结
            if (bitmap.IsFrozen == false) bitmap.Freeze();

            var tabToSave = _currentTabItem;
            string fileId = tabToSave.Id; // 保存 ID，防止闭包变量在线程中变化

            // 2. 启动后台保存任务
            var saveTask = Task.Run(() =>
            {
                try
                {
                    string fileName = $"{fileId}.cache.png";
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
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AutoBackup Failed: {ex.Message}");
                }
                finally
                {
                    // 任务完成后，从字典中移除
                    _activeSaveTasks.TryRemove(fileId, out _);
                }
            });

            // 3. 将任务加入字典，防止还没存完就去读
            _activeSaveTasks.TryAdd(fileId, saveTask);
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
            //a.s("NotifyCanvasChanged");
            _autoSaveTimer.Stop();
            double delayMs = 2000; // 基础延迟 2秒
            if (BackgroundImage.Source is BitmapSource source)
            {
                double pixels = source.PixelWidth * source.PixelHeight;
                if (pixels > 3840 * 2160) delayMs = 5000; // 大图给 5秒
                else if (pixels > 1920 * 1080) delayMs = 3000; // 中图给 3秒
                else delayMs = 1500; // 小图 1.5秒，响应更快
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
            // 1. 清理底层画布
            Clean_bitmap(1200, 900);

            // 2. 重置窗口标题
            _currentFilePath = string.Empty;
            _currentFileName = "未命名";
            UpdateWindowTitle();

            _nextUntitledIndex = 1;
            var newTab = CreateNewUntitledTab();
            newTab.IsSelected = true; // 设为选中态
            FileTabs.Add(newTab);
            _currentTabItem = newTab;

            // 5. 重置撤销栈和脏状态追踪
            ResetDirtyTracker();

            // 6. 滚动视图归位
            FileTabsScroller.ScrollToHorizontalOffset(0);
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
        {
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
            // 立即保存当前的
            if (_currentTabItem != null && _currentTabItem.IsDirty)
            {
                _autoSaveTimer.Stop();
                var bmp = GetCurrentCanvasSnapshot();
                if (bmp != null)
                {
                    string path = System.IO.Path.Combine(_cacheDir, $"{_currentTabItem.Id}.png");
                    using (var fs = new FileStream(path, FileMode.Create))
                    {
                        BitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bmp));
                        encoder.Save(fs);
                    }
                    _currentTabItem.BackupPath = path;
                }
            }
            SaveSession(); // 更新 JSON
            Close();
        }

        private void SaveSession()
        {
            // 1. 准备当前内存中的数据
            var currentSessionTabs = new List<SessionTabInfo>();

            // 获取当前上下文目录 (同之前的逻辑)
            string currentContextDir = null;
            if (!string.IsNullOrEmpty(_currentTabItem?.FilePath))
            {
                currentContextDir = System.IO.Path.GetDirectoryName(_currentTabItem.FilePath);
            }
            else if (_imageFiles != null && _imageFiles.Count > 0)
            {
                currentContextDir = System.IO.Path.GetDirectoryName(_imageFiles[0]);
            }
            // 规范化路径以便比较
            if (currentContextDir != null) currentContextDir = System.IO.Path.GetFullPath(currentContextDir);
            foreach (var tab in FileTabs)
            {
                if (tab.IsDirty || tab.IsNew)
                {
                    string tabDir = currentContextDir;
                    if (!string.IsNullOrEmpty(tab.FilePath))
                    {
                        tabDir = System.IO.Path.GetDirectoryName(tab.FilePath);
                    }
                    if (tabDir != null) tabDir = System.IO.Path.GetFullPath(tabDir);

                    currentSessionTabs.Add(new SessionTabInfo
                    {
                        Id = tab.Id,
                        OriginalPath = tab.FilePath,
                        BackupPath = tab.BackupPath,
                        IsDirty = tab.IsDirty,
                        IsNew = tab.IsNew,
                        WorkDirectory = tabDir
                    });
                }
            }
            var finalTabsToSave = new List<SessionTabInfo>();
            finalTabsToSave.AddRange(currentSessionTabs);

            // B. 再去旧文件里捞那些“没加载进来的”
            if (File.Exists(_sessionPath))
            {
                try
                {
                    var oldJson = File.ReadAllText(_sessionPath);
                    var oldSession = System.Text.Json.JsonSerializer.Deserialize<PaintSession>(oldJson);

                    if (oldSession != null && oldSession.Tabs != null)
                    {
                        if (!string.IsNullOrEmpty(currentContextDir))
                        {
                            foreach (var oldTab in oldSession.Tabs)
                            {
                                string oldTabDir = oldTab.WorkDirectory;
                                // 兼容旧数据
                                if (string.IsNullOrEmpty(oldTabDir) && !string.IsNullOrEmpty(oldTab.OriginalPath))
                                {
                                    oldTabDir = System.IO.Path.GetDirectoryName(oldTab.OriginalPath);
                                }
                                if (oldTabDir != null) oldTabDir = System.IO.Path.GetFullPath(oldTabDir);
                                if (oldTabDir != null && !oldTabDir.Equals(currentContextDir, StringComparison.OrdinalIgnoreCase))
                                {
                                    // 防止 ID 重复添加 (虽然理论上不同目录 ID 不会撞，但为了保险)
                                    if (!finalTabsToSave.Any(t => t.Id == oldTab.Id)) finalTabsToSave.Add(oldTab);

                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 如果读取旧 Session 失败（比如格式坏了），那只能忍痛丢弃旧数据，保新数据
                    System.Diagnostics.Debug.WriteLine("Merge session failed: " + ex.Message);
                }
            }

            // 4. 构建最终对象并保存
            var session = new PaintSession
            {
                LastViewedFile = _currentTabItem?.FilePath ?? (_imageFiles.Count > _currentImageIndex ? _imageFiles[_currentImageIndex] : null),
                Tabs = finalTabsToSave
            };

            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_sessionPath));
                string json = System.Text.Json.JsonSerializer.Serialize(session);
                File.WriteAllText(_sessionPath, json);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("保存会话失败: " + ex.Message);
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
                                IsLoading = true,
                            };

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

        private void CreateNewTab(bool switchto = false)
        {
            var newTab = CreateNewUntitledTab();
            int insertIndex = FileTabs.Count;
            if (_currentTabItem != null)
            {
                int currentIndex = FileTabs.IndexOf(_currentTabItem);
                if (currentIndex >= 0)
                {
                    insertIndex = currentIndex + 1;
                }
            }
            FileTabs.Insert(insertIndex, newTab);
            if (switchto)
            {
                // 更新选中状态
                foreach (var tab in FileTabs) tab.IsSelected = false;
                newTab.IsSelected = true;

                // 切换画布数据
                Clean_bitmap(1200, 900); // 使用默认尺寸
                _currentFilePath = string.Empty;
                _currentFileName = "未命名";
                _currentTabItem = newTab;
                UpdateWindowTitle();
               

                // 重置撤销栈
                ResetDirtyTracker();
               // ResetCanvasView();
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (FileTabsScroller.ViewportWidth > 0)
                    {
                        double itemWidth = 124;

                        // 计算让该元素居中的 Offset
                        double targetOffset = insertIndex * itemWidth - FileTabsScroller.ViewportWidth / 2 + itemWidth / 2;

                        // 边界检查
                        if (targetOffset < 0) targetOffset = 0;
                        double maxOffset = FileTabsScroller.ScrollableWidth;
                        if (targetOffset > maxOffset) targetOffset = maxOffset;

                        FileTabsScroller.ScrollToHorizontalOffset(targetOffset);
                    }
                }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
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

        private void SaveSingleTab(FileTabItem tab)
        {
            try
            {
                if (tab.IsNew && string.IsNullOrEmpty(tab.FilePath)) return;

                // 情况 B: 这是当前正在编辑的 Tab -> 从画布保存
                if (tab == _currentTabItem)
                {
                    var bmp = GetCurrentCanvasSnapshot();
                    if (bmp != null)
                    {
                        using (var fs = new FileStream(tab.FilePath, FileMode.Create))
                        {
                            BitmapEncoder encoder = new PngBitmapEncoder(); // 或 Jpeg
                            encoder.Frames.Add(BitmapFrame.Create(bmp));
                            encoder.Save(fs);
                        }
                    }
                }
                else if (File.Exists(tab.BackupPath))
                {
                    // 直接将缓存文件覆盖到原文件，这是最快的
                    File.Copy(tab.BackupPath, tab.FilePath, true);
                }
                tab.IsDirty = false;
                tab.IsNew = false; // 保存后就不算新文件了

                // 删除缓存文件，因为已经同步了
                if (File.Exists(tab.BackupPath)) File.Delete(tab.BackupPath);
                tab.BackupPath = null;
            }
            catch (Exception ex)
            {
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
    }
}