
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

            // 在 MainWindow.cs -> FileTabItem 类内部

            public async Task LoadThumbnailAsync(int containerWidth, int containerHeight)
            {
                // 1. 确定要加载的路径：优先由 BackupPath (缓存)，其次是 FilePath (原图)
                string targetPath = null;

                if (!string.IsNullOrEmpty(BackupPath) && File.Exists(BackupPath))
                {
                    targetPath = BackupPath;
                }
                else if (!string.IsNullOrEmpty(FilePath) && File.Exists(FilePath))
                {
                    targetPath = FilePath;
                }

                if (targetPath == null) return;

                var thumbnail = await Task.Run(() =>
                {
                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(targetPath);

                        if (targetPath == FilePath)
                        {
                            bmp.DecodePixelWidth = 100;
                        }

                        bmp.CacheOption = BitmapCacheOption.OnLoad; // 关键：加载完立即释放文件锁
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    }
                    catch
                    {
                        return null;
                    }
                });

                if (thumbnail != null)
                {
                    // 回到 UI 线程设置属性
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        Thumbnail = thumbnail;
                    });
                }
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
                    //FileTabsScroller.ScrollToHorizontalOffset(targetOffset);
                    //FileTabsScroller.UpdateLayout(); 
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

        // 修改方法签名，允许传入已生成的截图
        private void TriggerBackgroundBackup(BitmapSource? existingSnapshot = null)
        {
            if (_currentTabItem == null) return;
            if (_currentTabItem.IsDirty == false && !_currentTabItem.IsNew) return;

            // 1. 如果外部传了截图就用外部的，否则自己截
            BitmapSource bitmap = existingSnapshot ?? GetCurrentCanvasSnapshot();

            if (bitmap == null) return;
            if (bitmap.IsFrozen == false) bitmap.Freeze();

            var tabToSave = _currentTabItem;

            _ = Task.Run(() =>
            {
                try
                {
                    // ... 原有的保存逻辑 ...
                    string fileName = $"{tabToSave.Id}.cache.png";
                    string fullPath = System.IO.Path.Combine(_cacheDir, fileName);

                    using (var fileStream = new FileStream(fullPath, FileMode.Create))
                    {
                        BitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        encoder.Save(fileStream);
                    }

                    tabToSave.BackupPath = fullPath;
                    tabToSave.LastBackupTime = DateTime.Now;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AutoBackup Failed: {ex.Message}");
                }
            });
        }



        private async void OnFileTabClick(object sender, RoutedEventArgs e)
        {
            // a.s(1); // 调试代码可保留或删除
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is FileTabItem clickedItem)
            {
                // 1. 拦截：如果点击的是当前正在激活的 Tab，不做任何操作
                if (_currentTabItem == clickedItem) return;

                // 2. 拦截：如果点击的是同一个文件路径（非新建页），也不做操作
                if (!clickedItem.IsNew &&
                    !string.IsNullOrEmpty(_currentFilePath) &&
                    clickedItem.FilePath == _currentFilePath)
                {
                    return;
                }

                // ==========================================================
                // 3. 【核心同步逻辑】：在离开当前页面前，保存并同步缩略图
                // ==========================================================
                if (_currentTabItem != null)
                {
                    // 停止正在倒计时的自动保存（因为我们要马上手动存了）
                    _autoSaveTimer.Stop();

                    // 只有当页面是脏的（有修改）或者是新建页时，才需要同步
                    if (_currentTabItem.IsDirty || _currentTabItem.IsNew)
                    {
                        // A. 立即更新 UI 缩略图 (让用户觉得“已经存好了”)
                        UpdateTabThumbnail(_currentTabItem);

                        // B. 后台保存文件 (不卡顿界面)
                        TriggerBackgroundBackup();
                    }
                }

                // ==========================================================
                // 4. 切换 UI 选中状态
                // ==========================================================
                foreach (var tab in FileTabs) tab.IsSelected = false;
                clickedItem.IsSelected = true;

                // ==========================================================
                // 5. 加载新页面内容
                // ==========================================================
                if (clickedItem.IsNew)
                {
                    // 情况 A：切换到“未命名”新建页
                    // 既然前面已经保存了旧图，这里直接清空画布即可
                    Clean_bitmap(1200, 900); // 这里可以使用默认尺寸或上次记忆的尺寸

                    _currentFilePath = string.Empty;
                    _currentFileName = "未命名";
                    UpdateWindowTitle();
                }
                else
                {
                    // 情况 B：切换到已存在的图片文件
                    // a.s(2); 
                    await OpenImageAndTabs(clickedItem.FilePath);
                }

                // 6. 更新当前引用，完成切换
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

        // 在 MainWindow 类中修改 NotifyCanvasChanged 方法

        public void NotifyCanvasChanged()
        {
            _autoSaveTimer.Stop();

            // --- 动态调整时间逻辑 ---
            double delayMs = 2000; // 基础延迟 2秒

            if (BackgroundImage.Source is BitmapSource source)
            {
                double pixels = source.PixelWidth * source.PixelHeight;

                // 阈值根据实际性能调整
                if (pixels > 3840 * 2160) // 4K以上
                {
                    delayMs = 5000; // 大图给 5秒
                }
                else if (pixels > 1920 * 1080) // 1080P以上
                {
                    delayMs = 3000; // 中图给 3秒
                }
                else
                {
                    delayMs = 1500; // 小图 1.5秒，响应更快
                }
            }

            _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(delayMs);
            _autoSaveTimer.Start();

            // 标记状态
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

        /// <summary>
        /// 当列表被清空时，强制重置为一个新建的未命名画板
        /// </summary>
        private void ResetToNewCanvas()
        {
            // 1. 清理底层画布 (假设 Clean_bitmap 已定义，使用默认尺寸或上次记忆尺寸)
            Clean_bitmap(1200, 900);

            // 2. 重置窗口标题和状态变量
            _currentFilePath = string.Empty;
            _currentFileName = "未命名";
            UpdateWindowTitle();

            // 3. 创建一个新的 Tab 对象
            var newTab = new FileTabItem(null)
            {
                IsNew = true,
                IsDirty = false,
                IsSelected = true, // 默认选中
                Thumbnail = GenerateBlankThumbnail() // 核心：立即同步缩略图
            };

            // 4. 加入列表并更新引用
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
                // 1. 获取当前画布的全尺寸截图
                // 注意：RenderTargetBitmap 比较消耗资源，但为了准确性必须渲染
                RenderTargetBitmap rtb = new RenderTargetBitmap(
                    (int)BackgroundImage.ActualWidth,
                    (int)BackgroundImage.ActualHeight,
                    96d, 96d, PixelFormats.Pbgra32);

                rtb.Render(BackgroundImage);
                rtb.Freeze(); // 冻结以便跨线程使用（如果需要）

                // 2. 生成高性能缩略图 (缩放到高度 60px 左右，匹配 ImageBar)
                // 这一步至关重要，防止 ImageBar 持有几十个 4K 截图导致内存爆炸
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

        // 4. 计时器触发：后台静默保存
        private async void AutoSaveTimer_Tick(object sender, EventArgs e)
        {
            _autoSaveTimer.Stop(); // 停止计时，直到下次改动
            if (_currentTabItem != null)
            {
                // 1. 先更新 UI 缩略图 (用户立刻看到变化)
                UpdateTabThumbnail(_currentTabItem);

                // 2. 再触发后台文件备份 (不卡顿 UI)
                TriggerBackgroundBackup();
            }
        }

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

        // 在 MainWindow.cs 中

        private void LoadSession()
        {
            // 如果没有会话文件，直接返回
            if (!File.Exists(_sessionPath)) return;

            try
            {
                var json = File.ReadAllText(_sessionPath);
                var session = System.Text.Json.JsonSerializer.Deserialize<PaintSession>(json);

                if (session.Tabs != null)
                {
                    foreach (var info in session.Tabs)
                    {
                        // 只有当缓存文件真实存在时才恢复，防止死链
                        if (!string.IsNullOrEmpty(info.BackupPath) && File.Exists(info.BackupPath))
                        {
                            // 恢复 Tab 对象
                            var tab = new FileTabItem(info.OriginalPath)
                            {
                                Id = info.Id,
                                IsNew = info.IsNew,
                                IsDirty = info.IsDirty,
                                BackupPath = info.BackupPath,
                                IsLoading = true
                            };

                            // 加入列表
                            FileTabs.Add(tab);

                            // 🔥 核心修复：显式触发缩略图加载
                            // 因为是在启动时，为了不阻塞 UI，扔给线程池去跑
                            _ = tab.LoadThumbnailAsync(100, 60).ContinueWith(t =>
                            {
                                // 加载完后取消 Loading 状态
                                tab.IsLoading = false;
                            }, TaskScheduler.FromCurrentSynchronizationContext());
                        }
                    }
                }

                // 恢复上次查看的文件（可选优化：如果上次看的是 dirty 的，需要特殊处理，这里暂且保持原逻辑）
                if (!string.IsNullOrEmpty(session.LastViewedFile))
                {
                    // 可以在这里写逻辑自动选中上次的文件
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
                // 🔥 核心修复：
                // 如果 Tab 已经是 Dirty 的（说明是从 Session 恢复的未保存图片），
                // 且此时 Undo 栈是空的（Count=0），我们不能把 _savedUndoPoint 设为 0，
                // 否则 CheckDirtyState 会认为 0==0 (已保存)。
                // 我们将其设为 -1，这样 CheckDirtyState 比较时：UndoCount(0) != SavedPoint(-1) => True (保持 Dirty)
                _savedUndoPoint = -1;
            }
            else
            {
                // 正常情况：打开一个新文件，认为是干净的
                _savedUndoPoint = 0;
                if (_currentTabItem != null) _currentTabItem.IsDirty = false;
            }

            SetUndoRedoButtonState();
        }

        private void CreateNewTab(bool switchto = false)
        {
            var newTab = CreateNewUntitledTab();

            FileTabs.Add(newTab);
            if (VisualTreeHelper.GetChildrenCount(FileTabList) > 0)
            {
                FileTabsScroller.ScrollToRightEnd();
            }
            if(switchto)
            {
                foreach (var tab in FileTabs) tab.IsSelected = false;
                newTab.IsSelected = true;
                // 切换到新建画布
                Clean_bitmap(1200, 900); // 使用默认尺寸或上次记忆的尺寸
                _currentFilePath = string.Empty;
                _currentFileName = "未命名";
                UpdateWindowTitle();
                _currentTabItem = newTab;
                // 重置撤销栈和脏状态追踪
                ResetDirtyTracker();
            }
        }

        // 2. 实现关闭逻辑
        private void OnFileTabCloseClick(object sender, RoutedEventArgs e)
        {
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
            FileTabs.Remove(item);

            // 如果移除的是当前选中的，需要选中另一个
            if (item.IsSelected && FileTabs.Count > 0)
            {
                var last = FileTabs.Last();
                last.IsSelected = true;
            }
        }
        private void InitializeScrollPosition()
        {
            // 强制刷新一次布局，确保 LeftAddBtn.ActualWidth 能取到值
            FileTabsScroller.UpdateLayout();
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
        private void MarkAsSaved()
        {
            if (_currentTabItem == null) return;
            _savedUndoPoint = _undo.UndoCount;

            _currentTabItem.IsDirty = false;

            SaveSession();
        }
    }
}