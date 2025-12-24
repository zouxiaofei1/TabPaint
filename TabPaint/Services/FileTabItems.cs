
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

//
//TabPaint主程序
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {


        public class FileTabItem : INotifyPropertyChanged
        {
            public string FilePath { get; set; } // 允许 set，因为新建文件可能一开始没有路径

            // 逻辑文件名：如果有路径显示文件名，如果是新建的显示 "未命名"
            public string FileName => !string.IsNullOrEmpty(FilePath) ? System.IO.Path.GetFileName(FilePath) : "未命名";
            public string DisplayName => !string.IsNullOrEmpty(FilePath) ? System.IO.Path.GetFileNameWithoutExtension(FilePath) : "未命名";

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

            // 预留给 UI 绑定的关闭命令（可选，或者直接在 View 处理 Click）
            public ICommand CloseCommand { get; set; }
            // 🔥 新增：唯一ID，用于生成缓存文件名
            public string Id { get; set; } = Guid.NewGuid().ToString();
            // 🔥 新增：缓存文件的路径
            public string BackupPath { get; set; }
            // 🔥 新增：最后一次备份的时间 (可选，用于调试)
            public DateTime LastBackupTime { get; set; }
            public FileTabItem(string path)
            {
                FilePath = path;
            }

            // ... LoadThumbnailAsync 方法保持不变 ...
            public async Task LoadThumbnailAsync(int containerWidth, int containerHeight)
            {
                // 保持你原有的逻辑
                // 注意：如果是 IsNew=True 的文件，Thumbnail 应该直接从 Canvas 生成，而不是读取磁盘
                if (IsNew || string.IsNullOrEmpty(FilePath)) return;

                var thumbnail = await Task.Run(() =>
                {
                    try
                    {
                        // 你的 System.Drawing 逻辑...
                        // 略... (保持你原有的代码)

                        // 为了演示完整性，这里简写，请保留你原有的完整代码
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

                    // 插入逻辑：找到合适的位置
                    // 我们希望 viewport 内的图片保持有序，且在列表的最左侧
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



        // 在 MainWindow 类成员变量里加一个锁标记
        private bool _isProgrammaticScroll = false;

        private async Task RefreshTabPageAsync(int centerIndex, bool refresh = false)
        {
            if (_imageFiles == null || _imageFiles.Count == 0) return;

            if (refresh)
            {
                LoadTabPageAsync(centerIndex);
                // 强制给 UI 一点时间去生成那些 Tab 的控件
                // 使用 DispatcherPriority.ContextIdle 等待布局渲染完成
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
                    _isProgrammaticScroll = true; // 🔒 上锁：告诉 ScrollChanged 别乱动
                    FileTabsScroller.ScrollToHorizontalOffset(targetOffset);
                    FileTabsScroller.UpdateLayout(); // 强制刷新一下确保位置正确
                }
                finally
                {
                    _isProgrammaticScroll = false; // 🔓 解锁
                }
            });
        }


        // 文件总数绑定属性
        public int ImageFilesCount;
        private bool _isInitialLayoutComplete = false;

        private void OnFileTabsScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isProgrammaticScroll) return;
            if (!_isInitialLayoutComplete || _isUpdatingUiFromScroll) return;

            double itemWidth = 124;
            int firstIndex = (int)(FileTabsScroller.HorizontalOffset / itemWidth);
            int visibleCount = (int)(FileTabsScroller.ViewportWidth / itemWidth) + 2;
            int lastIndex = firstIndex + visibleCount;

            if (PreviewSlider.Value != firstIndex)
            {
                _isUpdatingUiFromScroll = true;  // 🔒 上锁：告诉 Slider "别激动，这只是同步显示，不是用户在拖你"
                PreviewSlider.Value = firstIndex;
                _isUpdatingUiFromScroll = false; // 🔓 解锁
            }
            bool needload = false;

            // 尾部加载
            // 尾部加载 (修复版)
            if (FileTabs.Count > 0&&lastIndex >= FileTabs.Count - 2 && FileTabs.Count < _imageFiles.Count) // 阈值调小一点，体验更丝滑
            {
                // 获取当前列表最后一个文件的真实索引
                var lastTab = FileTabs[FileTabs.Count - 1];
                int lastFileIndex = _imageFiles.IndexOf(lastTab.FilePath);

                // 只有当它在文件列表中存在，且不是最后一张时才加载
                if (lastFileIndex >= 0 && lastFileIndex < _imageFiles.Count - 1)
                {
                    // 关键修复：从 lastFileIndex + 1 开始取，防止重复！
                    var nextItems = _imageFiles.Skip(lastFileIndex + 1).Take(PageSize);

                    foreach (var path in nextItems)
                    {
                        // 双重保险：防止异步滚动时的并发重复
                        if (!FileTabs.Any(t => t.FilePath == path))
                        {
                            FileTabs.Add(new FileTabItem(path));
                        }
                    }
                    needload = true;
                }
            }


            // 前端加载 (修复版)
            if (firstIndex < 2 && FileTabs.Count > 0)
            {
                // 获取当前列表第一个文件的真实索引
                var firstTab = FileTabs[0];
                int firstFileIndex = _imageFiles.IndexOf(firstTab.FilePath);

                if (firstFileIndex > 0) // 如果前面还有图
                {
                    // 计算需要拿多少张
                    int takeCount = PageSize;
                    // 如果前面不够 PageSize 张了，就只拿剩下的
                    if (firstFileIndex < PageSize) takeCount = firstFileIndex;

                    // 关键修复：从 firstFileIndex - takeCount 开始拿
                    int start = firstFileIndex - takeCount;

                    var prevPaths = _imageFiles.Skip(start).Take(takeCount);

                    // 使用 Insert(0, ...) 会导致大量 UI 重绘，建议反转顺序逐个插入
                    int insertPos = 0;
                    foreach (var path in prevPaths)
                    {
                        if (!FileTabs.Any(t => t.FilePath == path))
                        {
                            FileTabs.Insert(insertPos, new FileTabItem(path));
                            insertPos++; // 保持插入顺序
                        }
                    }

                    // 修正滚动条位置，防止因为插入元素导致视图跳动
                    FileTabsScroller.ScrollToHorizontalOffset(FileTabsScroller.HorizontalOffset + insertPos * itemWidth);
                    needload = true;
                }
            }

            if (needload || e.HorizontalChange != 0 || e.ExtentWidthChange != 0)  // 懒加载缩略图，仅当有新增或明显滚动时触发
            {
                int end = Math.Min(lastIndex, FileTabs.Count);
                for (int i = firstIndex; i < end; i++)
                {
                    var tab = FileTabs[i];
                    if (tab.Thumbnail == null && !tab.IsLoading)
                    {
                        tab.IsLoading = true;
                        _ = tab.LoadThumbnailAsync(100, 60);
                    }
                }
            }
        }

        // 鼠标滚轮横向滚动
        private void OnFileTabsWheelScroll(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer != null)
            {
                // 横向滚动
                double offset = scrollViewer.HorizontalOffset - (e.Delta);
                scrollViewer.ScrollToHorizontalOffset(offset);
                e.Handled = true;
            }
        }

        // 阻止边界反馈
        private void ScrollViewer_ManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }

        // 如果需要鼠标拖动滚动（模拟触摸）
        private Point? _scrollMousePoint = null;
        private double _scrollHorizontalOffset;

        private void FileTabsScroller_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _scrollMousePoint = e.GetPosition(FileTabsScroller);
            _scrollHorizontalOffset = FileTabsScroller.HorizontalOffset;
            FileTabsScroller.CaptureMouse();
        }

        private void FileTabsScroller_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_scrollMousePoint.HasValue && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(FileTabsScroller);
                var offset = _scrollHorizontalOffset + (_scrollMousePoint.Value.X - currentPoint.X);
                FileTabsScroller.ScrollToHorizontalOffset(offset);
            }
        }

        private void FileTabsScroller_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _scrollMousePoint = null;
            FileTabsScroller.ReleaseMouseCapture();
        }

        private async void OnFileTabClick(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is FileTabItem clickedItem)
            {
                // 1. 更新选中视觉状态 (可选，如果你用了RadioButton风格可省略)
                foreach (var tab in FileTabs) tab.IsSelected = false;
                clickedItem.IsSelected = true;

                // 2. 核心修复：分支判断
                if (clickedItem.IsNew)
                {
                    if (_currentTabItem != clickedItem)
                    {
                        Clean_bitmap(1200, 900); // 调用你旧有的初始化白板方法
                        _currentFilePath = string.Empty;
                        _currentFileName = "未命名";
                        UpdateWindowTitle();
                    }
                }
                else
                {
                    // B. 普通文件，走原有逻辑
                    await OpenImageAndTabs(clickedItem.FilePath);
                }

                // 记录当前正在激活的 Tab，方便后续插入位置计算
                _currentTabItem = clickedItem;
            }
        }

        // 补充定义：在类成员里加一个引用，记录当前是谁
        private FileTabItem _currentTabItem;

        private bool _isDragging = false;
        private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 如果点击的目标是 Thumb 本身或其子元素，则不作任何处理。
            // 让 Slider 的默认 Thumb 拖动逻辑去工作。
            if (IsMouseOverThumb(e)) return;

            // 如果点击的是轨道部分
            _isDragging = true;
            var slider = (Slider)sender;

            // 捕获鼠标，这样即使鼠标移出 Slider 范围，我们也能继续收到 MouseMove 事件
            slider.CaptureMouse();

            // 更新 Slider 的值到当前点击的位置
            UpdateSliderValueFromPoint(slider, e.GetPosition(slider));

            // 标记事件已处理，防止其他控件响应
            e.Handled = true;
        }

        private void Slider_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // 仅当我们通过点击轨道开始拖动时，才处理 MouseMove 事件
            if (_isDragging)
            {
                var slider = (Slider)sender;
                // 持续更新 Slider 的值
                UpdateSliderValueFromPoint(slider, e.GetPosition(slider));
            }
        }

        private void Slider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 如果我们正在拖动
            if (_isDragging)
            {
                _isDragging = false;
                var slider = (Slider)sender;
                // 释放鼠标捕获
                slider.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void Slider_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var slider = (Slider)sender;

            // 根据滚轮方向调整值
            double change = slider.LargeChange; // 使用 LargeChange 作为滚动步长
            if (e.Delta < 0)
            {
                change = -change;
            }

            slider.Value += change;
            e.Handled = true;
        }
        private async void UpdateSliderValueFromPoint(Slider slider, Point position)
        {
            double ratio = position.Y / slider.ActualHeight; // 计算点击位置在总高度中的比例

            // 将比例转换为滑块的值范围
            double value = slider.Minimum + (slider.Maximum - slider.Minimum) * (1 - ratio);

            value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, value)); // 确保值在有效范围内

            slider.Value = value;
            await OpenImageAndTabs(_imageFiles[(int)value], true);
        }
        private bool IsMouseOverThumb(MouseButtonEventArgs e)/// 检查鼠标事件的原始源是否是 Thumb 或其内部的任何元素。
        {
            var slider = (Slider)e.Source;
            var track = slider.Template.FindName("PART_Track", slider) as Track;
            if (track == null) return false;

            return track.Thumb.IsMouseOver;
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


        private void SetPreviewSlider()
        {
            if (_imageFiles == null || _imageFiles.Count == 0) return;
            PreviewSlider.Minimum = 0;
            PreviewSlider.Maximum = _imageFiles.Count - 1;
            PreviewSlider.Value = _currentImageIndex;
        }
    }
}