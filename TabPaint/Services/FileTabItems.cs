
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
//ImageBar图片选择框相关代码
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public partial class FileTabItem : INotifyPropertyChanged
        {
            public string FilePath { get; set; } // 允许 set，因为新建文件可能一开始没有路径
            private int _untitledNumber;
            public int UntitledNumber
            {
                get => _untitledNumber;
                set
                {
                    _untitledNumber = value;
                    OnPropertyChanged(nameof(UntitledNumber));
                    OnPropertyChanged(nameof(FileName));
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
            public string FileName
            {
                get
                {
                    if (!string.IsNullOrEmpty(FilePath))
                        return System.IO.Path.GetFileName(FilePath);
                    if (IsNew) // 如果是新建文件，显示 "未命名 X"
                        return $"未命名 {UntitledNumber}";

                    return "未命名";
                }
            }

            public string DisplayName// 🔄 修改：DisplayName (不带扩展名) 的显示逻辑同理
            {
                get
                {
                    if (!string.IsNullOrEmpty(FilePath))
                        return System.IO.Path.GetFileNameWithoutExtension(FilePath);

                    if (IsNew)
                        return $"未命名 {UntitledNumber}";

                    return "未命名";
                }
            }
            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
            }

            private bool _isLoading;
            public bool IsLoading
            {
                get => _isLoading;
                set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
            }

            // 🔴 状态：是否修改未保存
            private bool _isDirty;
            public bool IsDirty
            {
                get => _isDirty;
                set { _isDirty = value; OnPropertyChanged(nameof(IsDirty)); }
            }

            // 🔵 状态：是否是纯新建的内存文件
            private bool _isNew;
            public bool IsNew
            {
                get => _isNew;
                set { _isNew = value; OnPropertyChanged(nameof(IsNew)); }
            }

            private BitmapSource? _thumbnail;
            public BitmapSource? Thumbnail
            {
                get => _thumbnail;
                set { _thumbnail = value; OnPropertyChanged(nameof(Thumbnail)); }
            }

            public ICommand CloseCommand { get; set; }
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string BackupPath { get; set; }
            public DateTime LastBackupTime { get; set; }
            public FileTabItem(string path)
            {
                FilePath = path;
            }

            // ... LoadThumbnailAsync 方法保持不变 ...
            public async Task LoadThumbnailAsync(int containerWidth, int containerHeight)
            {
                if (IsNew || string.IsNullOrEmpty(FilePath)) return;

                var thumbnail = await Task.Run(() =>
                {
                    try
                    {
                        using (var img = System.Drawing.Image.FromFile(FilePath)) { /*...*/ }

                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(FilePath);
                        bmp.DecodePixelWidth = 100;
                        bmp.CacheOption = BitmapCacheOption.OnLoad; // 关键
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    }
                    catch { return null; }
                });
                if (thumbnail != null) Thumbnail = thumbnail;
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }


        private const int PageSize = 10; // 每页标签数量（可调整）

        public ObservableCollection<FileTabItem> FileTabs { get; }
            = new ObservableCollection<FileTabItem>();
        private bool _isProgrammaticScroll = false;
        // 文件总数绑定属性
        public int ImageFilesCount;
        private bool _isInitialLayoutComplete = false;

        private void LoadTabPageAsync(int centerIndex)
        {
            if (_imageFiles == null || _imageFiles.Count == 0) return;

            // 1. 确定当前“文件夹视图”的范围
            int start = Math.Max(0, centerIndex - PageSize);
            int end = Math.Min(_imageFiles.Count - 1, centerIndex + PageSize);
            var viewportPaths = new HashSet<string>();
            for (int i = start; i <= end; i++) viewportPaths.Add(_imageFiles[i]);

            // 2. 清理阶段：只移除那些 "既不在视野内，又不是脏数据，也不是新文件" 的项
            for (int i = FileTabs.Count - 1; i >= 0; i--)
            {
                var tab = FileTabs[i];
                bool isViewport = viewportPaths.Contains(tab.FilePath);
                bool isKeepAlive = tab.IsDirty || tab.IsNew; // 🔥 关键：只要脏了或者新了，就永远不删

                if (!isViewport && !isKeepAlive)
                {
                    FileTabs.RemoveAt(i);
                }
            }

            for (int i = start; i <= end; i++)
            {
                string path = _imageFiles[i];
                var existingTab = FileTabs.FirstOrDefault(t => t.FilePath == path);

                if (existingTab == null)
                {
                    // 创建新 Tab
                    var newTab = new FileTabItem(path);
                    newTab.IsLoading = true;
                    _ = newTab.LoadThumbnailAsync(100, 60);

                    int insertIndex = 0;
                    bool inserted = false;

                    for (int j = 0; j < FileTabs.Count; j++)
                    {
                        var t = FileTabs[j];

                        // 如果遇到新建文件(IsNew)或视野外的脏文件(不在viewportPaths里)，说明如果不插在这里，后面就都是特殊文件了
                        bool isSpecial = t.IsNew || (!string.IsNullOrEmpty(t.FilePath) && !viewportPaths.Contains(t.FilePath));

                        if (isSpecial)
                        {
                            FileTabs.Insert(j, newTab);
                            inserted = true;
                            break;
                        }

                        // 如果是普通视野文件，按索引比较
                        int tIndex = _imageFiles.IndexOf(t.FilePath);
                        if (tIndex > i)
                        {
                            FileTabs.Insert(j, newTab);
                            inserted = true;
                            break;
                        }
                    }

                    if (!inserted) FileTabs.Add(newTab);
                }
                else
                {
                }
            }

        }

        

        private async Task RefreshTabPageAsync(int centerIndex, bool refresh = false)
        {
            if (_imageFiles == null || _imageFiles.Count == 0) return;

            if (refresh)
            {
                LoadTabPageAsync(centerIndex);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            }

            // 计算当前选中图片在 FileTabs 中的索引
            var currentTab = FileTabs.FirstOrDefault(t => t.FilePath == _imageFiles[centerIndex]);
            if (currentTab == null) return;

            int selectedIndex = FileTabs.IndexOf(currentTab);
            if (selectedIndex < 0) return;

            double itemWidth = 124;
            double viewportWidth = FileTabsScroller.ViewportWidth;

            // 如果窗口还没加载完，ViewportWidth 可能是 0，这时候滚动没意义且可能报错
            if (viewportWidth <= 0) return;

            double targetOffset = selectedIndex * itemWidth - viewportWidth / 2 + itemWidth / 2;

            targetOffset = Math.Max(0, targetOffset);
            double maxOffset = Math.Max(0, FileTabs.Count * itemWidth - viewportWidth);
            targetOffset = Math.Min(targetOffset, maxOffset);

            // 🔥 关键修复：使用 Dispatcher 并在滚动期间上锁
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    _isProgrammaticScroll = true; 
                    FileTabsScroller.ScrollToHorizontalOffset(targetOffset);
                    FileTabsScroller.UpdateLayout(); 
                }
                finally
                {
                    _isProgrammaticScroll = false; // 🔓 解锁
                }
            });
        }

      
        
        private void ScrollViewer_ManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)// 阻止边界反馈
        {
            e.Handled = true;
        }

        // 修改方法名为 TriggerBackgroundBackup，因为它不再是“同步等待保存完成”
        private void TriggerBackgroundBackup()
        {
            
            if (_currentTabItem == null) return;
            if (_currentTabItem.IsDirty == false) return; // 仅当有修改时才备份
            // 1. 【UI线程】 瞬间完成：获取截图并冻结
            // GetCurrentCanvasSnapshot 应该是使用 RenderTargetBitmap，这是毫秒级的
            BitmapSource bitmap = GetCurrentCanvasSnapshot();
            if (bitmap == null) return;

            if (bitmap.IsFrozen == false) bitmap.Freeze(); // 必须冻结才能跨线程传递

            // 2. 【UI线程】 捕获当前 Tab 的引用，防止后续 _currentTabItem 变化
            var tabToSave = _currentTabItem;

            // 3. 【后台线程】 启动保存任务，且不等待它完成 (_ = Task.Run)
            _ = Task.Run(() =>
            {
                try
                {
                    string fileName = $"{tabToSave.Id}.cache.png"; // 建议加后缀区分
                    string fullPath = System.IO.Path.Combine(_cacheDir, fileName);

                    using (var fileStream = new FileStream(fullPath, FileMode.Create))
                    {
                        // 优化建议：如果是缓存，追求速度，可以用 BmpBitmapEncoder (体积大但极快)
                        // 这里暂时保持 PngBitmapEncoder，但它是在后台跑，不会卡顿 UI
                        BitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        encoder.Save(fileStream);
                    }

                    // 更新数据模型（注意：如果是绑定属性，且在非UI线程更新，需确保 ViewModel 处理了跨线程问题
                    // 或者使用 Dispatcher 更新 UI 相关属性）
                    tabToSave.BackupPath = fullPath;
                    tabToSave.LastBackupTime = DateTime.Now;

                    // 调试日志
                    System.Diagnostics.Debug.WriteLine($"[后台] 自动保存完成: {tabToSave.FileName}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AutoBackup Failed: {ex.Message}");
                }
            });
        }


        private async void OnFileTabClick(object sender, RoutedEventArgs e)
        {
            a.s(1);
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is FileTabItem clickedItem)
            {
                if (_currentTabItem != null && _currentTabItem == clickedItem)
                {
                    return;
                }

                if (!string.IsNullOrEmpty(_currentFilePath) &&
                    clickedItem.FilePath == _currentFilePath &&
                    !clickedItem.IsNew)
                {
                    return;
                }
                foreach (var tab in FileTabs) tab.IsSelected = false;
                clickedItem.IsSelected = true;

                // 2. 核心逻辑：分支判断
                if (clickedItem.IsNew)
                {
                    // 如果是从普通文件切换到“新建未命名”
                    if (_currentTabItem != clickedItem)
                    {
                        // 保存上一个文件的缓存（如果需要）
                        if (_currentTabItem != null)
                        {
                            TriggerBackgroundBackup();
                        }

                        Clean_bitmap(1200, 900); // 初始化白板
                        _currentFilePath = string.Empty;
                        _currentFileName = "未命名";
                        UpdateWindowTitle();
                    }
                }
                else
                {
                    a.s(2);
                    await OpenImageAndTabs(clickedItem.FilePath);
                }

                // 3. 记录当前正在激活的 Tab
                _currentTabItem = clickedItem;
            }
        }



        public static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject    // 这是一个通用的辅助方法，用于在可视化树中查找特定类型的子控件
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T)
                {
                    return (T)child;
                }
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }
            return null;
        }

        private void OnFileTabPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
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


        // 2. 鼠标移动：判断是否触发拖拽
        private void OnFileTabPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return; // 必须按 Ctrl

            Vector diff = _dragStartPoint - e.GetPosition(null);
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var button = sender as System.Windows.Controls.Button;
                var tabItem = button?.DataContext as FileTabItem;
                if (tabItem == null) return;

                // --- 核心修改开始 ---

                string finalDragPath = tabItem.FilePath;
                bool needTempFile = false;
                if (string.IsNullOrEmpty(finalDragPath) || !System.IO.File.Exists(finalDragPath) || tabItem.IsDirty)
                {
                    needTempFile = true;
                }

                if (needTempFile)
                {
                    try
                    {
                        // 1. 获取高清图 (调用上面的方法)
                        BitmapSource highResBitmap = GetHighResImageForTab(tabItem);

                        if (highResBitmap != null)
                        {
                            // 2. 生成临时路径
                            string fileName = string.IsNullOrEmpty(tabItem.FileName) ? "Image" : System.IO.Path.GetFileNameWithoutExtension(tabItem.FileName);
                            string tempFileName = $"{fileName}_{DateTime.Now:HHmmss}.png";
                            finalDragPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), tempFileName);

                            // 3. 保存到 Temp
                            SaveBitmapToPng(highResBitmap, finalDragPath);
                        }
                        else
                        {
                            return; // 获取失败，不拖拽
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Drag gen failed: " + ex.Message);
                        return;
                    }
                }
                if (!string.IsNullOrEmpty(finalDragPath) && System.IO.File.Exists(finalDragPath))
                {
                    var dataObject = new System.Windows.DataObject(System.Windows.DataFormats.FileDrop, new string[] { finalDragPath });
                    DragDrop.DoDragDrop(button, dataObject, System.Windows.DragDropEffects.Copy);
                    e.Handled = true;
                }
            }
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

        // 3. 外部调用接口：当用户绘画结束（MouseUp）或修改内容时调用此方法
        // 例如：在 InkCanvas_MouseUp 或 StrokeCollected 事件中调用
        public void NotifyCanvasChanged(int timeleft = 3)
        {

            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }

        // 4. 计时器触发：后台静默保存
        private async void AutoSaveTimer_Tick(object sender, EventArgs e)
        {
            _autoSaveTimer.Stop(); // 停止计时，直到下次改动
            TriggerBackgroundBackup();
        }

        // 5. 核心保存逻辑


        // 辅助：获取当前画布截图 (请根据你的实际 Canvas 控件名称修改)
        private BitmapSource GetCurrentCanvasSnapshot()
        {
            if (BackgroundImage == null || BackgroundImage.ActualWidth <= 0) return null;

            RenderTargetBitmap rtb = new RenderTargetBitmap(
                (int)BackgroundImage.ActualWidth,
                (int)BackgroundImage.ActualHeight,
                96d, 96d, PixelFormats.Default);

            rtb.Render(BackgroundImage);
            return rtb;
        }
        protected async void OnClosing()
        {
            // 立即保存当前的
            if (_currentTabItem != null && _currentTabItem.IsDirty)
            {
                _autoSaveTimer.Stop();
                // 同步保存逻辑（简化版，直接复用代码但去掉 Task.Run）
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
            var session = new PaintSession
            {
                LastViewedFile = _currentTabItem?.FilePath ?? (_imageFiles.Count > _currentImageIndex ? _imageFiles[_currentImageIndex] : null),
                Tabs = new List<SessionTabInfo>()
            };

            foreach (var tab in FileTabs)
            {
                if (tab.IsDirty || tab.IsNew)
                {
                    session.Tabs.Add(new SessionTabInfo
                    {
                        Id = tab.Id,
                        OriginalPath = tab.FilePath,
                        BackupPath = tab.BackupPath, // 关键：下次启动加载这个图片
                        IsDirty = tab.IsDirty,
                        IsNew = tab.IsNew
                    });
                }
            }
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_sessionPath));
            string json = System.Text.Json.JsonSerializer.Serialize(session);
            File.WriteAllText(_sessionPath, json);
        }

        private void LoadSession()
        {
            if (!File.Exists(_sessionPath)) return;

            try
            {
                var json = File.ReadAllText(_sessionPath);
                var session = System.Text.Json.JsonSerializer.Deserialize<PaintSession>(json);

                if (session.Tabs != null)
                {
                    foreach (var info in session.Tabs)
                    {
                        if (!string.IsNullOrEmpty(info.BackupPath) && File.Exists(info.BackupPath))
                        {
                            var tab = new FileTabItem(info.OriginalPath)
                            {
                                Id = info.Id,
                                IsNew = info.IsNew,
                                IsDirty = info.IsDirty, // 恢复脏状态
                                BackupPath = info.BackupPath
                            };
                            tab.IsLoading = true;
                            FileTabs.Add(tab);
                        }
                    }
                }
            }
            catch { /* Handle Json Error */ }
        }

        private void ResetDirtyTracker()
        {
            if (_undo != null)
            {
                _undo.ClearUndo();
                _undo.ClearRedo();
            }
            _savedUndoPoint = 0; // 归零
                                 // 注意：如果是 session 恢复的“脏”文件，这里处理会比较特殊(见下文补充)，
                                 // 但对于普通打开图片，它是干净的。
            if (_currentTabItem != null) _currentTabItem.IsDirty = false;
            SetUndoRedoButtonState();
        }
        private void CreateNewTab()
        {
            // 创建一个新的 ViewModel
            var newTab = new FileTabItem(null)
            {
                IsNew = true,
                IsSelected = true
            };

            foreach (var tab in FileTabs) tab.IsSelected = false;
            FileTabs.Add(newTab);
            FileTabsScroller.ScrollToRightEnd();
        }

        // 2. 实现关闭逻辑
        private void OnFileTabCloseClick(object sender, RoutedEventArgs e)
        {
            // 阻止事件冒泡到 Item 的点击事件
            e.Handled = true;

            if (sender is System.Windows.Controls.Button btn && btn.Tag is FileTabItem item)
            {
                CloseTab(item);
            }
        }

        private void CloseTab(FileTabItem item)
        {
            if (item.IsDirty)
            {
                var result = System.Windows.MessageBox.Show($"图片 {item.FileName} 尚未保存，是否保存？", "保存提示", MessageBoxButton.YesNoCancel);
                if (result == MessageBoxResult.Cancel) return;
                if (result == MessageBoxResult.Yes)
                {
                    // TODO: 调用保存逻辑
                    // SaveFile(item);
                }
            }

            // 从 UI 集合移除
            FileTabs.Remove(item);

            // 如果移除的是当前选中的，需要选中另一个
            if (item.IsSelected && FileTabs.Count > 0)
            {
                // 简单策略：选中最后一个，或者选中相邻的
                var last = FileTabs.Last();
                last.IsSelected = true;
            }
        }
        private void InitializeScrollPosition()
        {
            // 强制刷新一次布局，确保 LeftAddBtn.ActualWidth 能取到值
            FileTabsScroller.UpdateLayout();

            // 计算要隐藏的宽度 = 按钮宽度 + Margin
            double hiddenWidth = LeftAddBtn.ActualWidth + LeftAddBtn.Margin.Left + LeftAddBtn.Margin.Right;
            if (FileTabsScroller.HorizontalOffset == 0)
            {
                FileTabsScroller.ScrollToHorizontalOffset(hiddenWidth);
            }
        }
        private void SaveSingleTab(FileTabItem tab)
        {
            try
            {
                // 情况 A: 这是一个新建的未命名文件，还没路径
                if (tab.IsNew && string.IsNullOrEmpty(tab.FilePath)) return;

                // 情况 B: 这是当前正在编辑的 Tab -> 从画布保存
                if (tab == _currentTabItem)
                {
                    // 获取当前画布截图 (复用上一轮的截图方法)
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
                // 情况 C: 这是后台 Tab -> 从缓存文件恢复
                else if (File.Exists(tab.BackupPath))
                {
                    // 直接将缓存文件覆盖到原文件，这是最快的
                    File.Copy(tab.BackupPath, tab.FilePath, true);
                }
                // 保存成功后的清理工作
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
        // 🔥 新增：专门用于检查是否应该显示红点
        public void CheckDirtyState()
        {
            if (_currentTabItem == null || _undo == null) return;

            int currentCount = _undo.UndoCount;

            // 核心逻辑：如果当前步数 不等于 上次保存时的步数，就是脏的
            bool isDirty = currentCount != _savedUndoPoint;

            // 只有状态真正改变时才更新 UI，避免死循环或闪烁
            if (_currentTabItem.IsDirty != isDirty)
            {
                _currentTabItem.IsDirty = isDirty;
            }
        }
        private void MarkAsSaved()
        {
            if (_currentTabItem == null) return;

            // 🔥 关键：将当前的撤销步数记为“干净点”
            _savedUndoPoint = _undo.UndoCount;

            _currentTabItem.IsDirty = false;

            SaveSession();
        }
    }
}