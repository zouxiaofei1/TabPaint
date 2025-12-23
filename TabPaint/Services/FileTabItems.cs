using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

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
        // 加载当前页 + 前后页文件到显示区
        //private void LoadTabPageAsync(int centerIndex)
        //{//全部清空并重新加载!!!
        //    if (_imageFiles == null || _imageFiles.Count == 0) return;


        //    FileTabs.Clear();
        //    int start = Math.Max(0, centerIndex - PageSize);
        //    int end = Math.Min(_imageFiles.Count - 1, centerIndex + PageSize);
        //    //s(centerIndex);
        //    foreach (var path in _imageFiles.Skip(start).Take(end - start + 1))
        //        FileTabs.Add(new FileTabItem(path));

        //    foreach (var tab in FileTabs)
        //        if (tab.Thumbnail == null && !tab.IsLoading)
        //        {
        //            tab.IsLoading = true;
        //            _ = tab.LoadThumbnailAsync(100, 60);
        //        }
        //}

        // 修改 LoadTabPageAsync 的开头逻辑
        private void LoadTabPageAsync(int centerIndex)
        {
            // 1. 找出需要保留的 Tab (脏文件、新文件、外部文件)
            var keepTabs = FileTabs.Where(t => t.IsDirty || t.IsNew).ToList();

            // 2. 清空
           // FileTabs.Clear();

            // 3. 先把保留的 Tab 加回来 (或者加到末尾，看你喜好)
            // 策略 A：固定在左侧 (类似 VSCode Pinned)
            foreach (var t in keepTabs) FileTabs.Add(t);

            // 4. 加载文件夹内的文件 (原有逻辑)
            if (_imageFiles != null && _imageFiles.Count > 0)
            {
                int start = Math.Max(0, centerIndex - PageSize);
                int end = Math.Min(_imageFiles.Count - 1, centerIndex + PageSize);

                foreach (var path in _imageFiles.Skip(start).Take(end - start + 1))
                {
                    // 防止重复添加已经在 keepTabs 里的文件
                    if (!keepTabs.Any(t => t.FilePath == path))
                    {
                        FileTabs.Add(new FileTabItem(path));
                    }
                }
            }

            // 5. 触发加载缩略图
            foreach (var tab in FileTabs)
            {
                if (tab.Thumbnail == null && !tab.IsLoading && !tab.IsNew) // IsNew 的不用加载
                {
                    tab.IsLoading = true;
                    _ = tab.LoadThumbnailAsync(100, 60);
                }
            }
        }

        private async Task RefreshTabPageAsync(int centerIndex, bool refresh = false)
        {

            if (_imageFiles == null || _imageFiles.Count == 0) return;

            if (refresh)
                LoadTabPageAsync(centerIndex);

            // 计算当前选中图片在 FileTabs 中的索引
            var currentTab = FileTabs.FirstOrDefault(t => t.FilePath == _imageFiles[centerIndex]);
            if (currentTab == null) return;

            int selectedIndex = FileTabs.IndexOf(currentTab);
            if (selectedIndex < 0) return;

            double itemWidth = 124;                   // 与 Button 实际宽度一致
            double viewportWidth = FileTabsScroller.ViewportWidth;
            double targetOffset = selectedIndex * itemWidth - viewportWidth / 2 + itemWidth / 2;

            targetOffset = Math.Max(0, targetOffset); // 防止负数偏移
            double maxOffset = Math.Max(0, FileTabs.Count * itemWidth - viewportWidth);
            targetOffset = Math.Min(targetOffset, maxOffset); // 防止超出范围

            FileTabsScroller.ScrollToHorizontalOffset(targetOffset);
        }

        // 文件总数绑定属性
        public int ImageFilesCount;
        private bool _isInitialLayoutComplete = false;
        private void OnFileTabsScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!_isInitialLayoutComplete) return;

            double itemWidth = 124;
            int firstIndex = (int)(FileTabsScroller.HorizontalOffset / itemWidth);
            int visibleCount = (int)(FileTabsScroller.ViewportWidth / itemWidth) + 2;
            int lastIndex = firstIndex + visibleCount;
            PreviewSlider.Value = firstIndex;
            bool needload = false;

            // 尾部加载
            if (lastIndex >= FileTabs.Count - 10 && FileTabs.Count < _imageFiles.Count)
            {
                int currentFirstIndex = _imageFiles.IndexOf(FileTabs[FileTabs.Count - 1].FilePath);
                if (currentFirstIndex > 0)
                {
                    int start = Math.Min(_imageFiles.Count - 1, currentFirstIndex);
                    foreach (var path in _imageFiles.Skip(start).Take(PageSize))
                        FileTabs.Add(new FileTabItem(path));
                    needload = true;
                }
            }

            // 前端加载
            if (FileTabs.Count > 0 && firstIndex < 10 && FileTabs[0].FilePath != _imageFiles[0])
            {
                int currentFirstIndex = _imageFiles.IndexOf(FileTabs[0].FilePath);
                if (currentFirstIndex > 0)
                {
                    int start = Math.Min(0, currentFirstIndex - PageSize);
                    double offsetBefore = FileTabsScroller.HorizontalOffset;

                    var prevPaths = _imageFiles.Skip(start).Take(currentFirstIndex - start);

                    foreach (var path in prevPaths.Reverse())
                        FileTabs.Insert(0, new FileTabItem(path));
                    FileTabsScroller.ScrollToHorizontalOffset(offsetBefore + prevPaths.Count() * itemWidth);
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
        private void FileTabsScroller_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            if (sender is ScrollViewer scroller)
            {
                // ManipulationDelta.Translation 包含了手指在 X 和 Y 方向上的移动距离
                // 我们只关心 X 方向的移动 (水平)
                var offset = scroller.HorizontalOffset - e.DeltaManipulation.Translation.X;

                // 滚动到新的位置
                scroller.ScrollToHorizontalOffset(offset);

                // 标记事件已处理，防止其他控件响应
                e.Handled = true;
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

        private async void OnFileTabClick(object sender, RoutedEventArgs e)// 点击标签打开图片
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is FileTabItem clickedItem)
            {
                // 1. 核心修复：先将所有项设为未选中
                //foreach (var tab in FileTabs)
                //{
                //    tab.IsSelected = false;
                //}

                //// 2. 选中当前项
                //clickedItem.IsSelected = true;

                // 3. 打开图片
                await OpenImageAndTabs(clickedItem.FilePath);
            }
           // if (sender is System.Windows.Controls.Button btn && btn.DataContext is FileTabItem item) await OpenImageAndTabs(item.FilePath);
        }

        // 鼠标滚轮横向滑动标签栏
        //private void OnFileTabsWheelScroll(object sender, MouseWheelEventArgs e)
        //{
        //    //s(1);
        //    double offset = FileTabsScroller.HorizontalOffset - e.Delta / 2;
        //    FileTabsScroller.ScrollToHorizontalOffset(offset);
        //    e.Handled = true;
        //}

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
            //if(_imageFiles.Count < 30)
            //    PreviewSlider.Visibility= Visibility.Collapsed;
            //else
            //    PreviewSlider.Visibility = Visibility.Visible;
        }


    }
}