
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
//包括PreviewSlider滑块和滚轮滚动imagebar的相关代码
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public partial class FileTabItem : INotifyPropertyChanged
        {  // 当滑块拖动时触发
           


        }
        private bool _isSyncingSlider = false; // 防止死循环
        private bool _isUpdatingUiFromScroll = false;
        private void UpdateImageBarSliderState()
        {
            // 使用 Dispatcher 延迟执行，确保 WPF 已经完成了 Tab 控件的增删和布局计算
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_imageFiles == null || _imageFiles.Count == 0)
                {
                    MainImageBar.Slider.Visibility = Visibility.Collapsed;
                    return;
                }
                a.s(_imageFiles.Count);
                double itemWidth = 124.0;

                // --- 改进点 1: 更加鲁棒的宽度获取 ---
                // ViewportWidth 是滚动区域可见宽度。如果为 0，则尝试获取控件实际宽度，
                // 如果还为 0，则说明控件还没加载，此时不应该执行隐藏逻辑。
                double viewportWidth = MainImageBar.Scroller.ViewportWidth;
                if (viewportWidth <= 0) viewportWidth = MainImageBar.Scroller.ActualWidth;

                // 如果依然无法获取宽度（例如控件在后台或者还没初始化），先跳过，等加载后再试
                if (viewportWidth <= 0) return;

                // 理论上需要的总宽度
                double requiredWidth = _imageFiles.Count * itemWidth;

                // --- 改进点 2: 逻辑判断 ---
                // 只有当“所需宽度”明显大于“可见宽度”时才显示 Slider
                // 增加 5 像素的缓冲区，防止因浮点数计算误差导致的闪烁
                bool needSlider = requiredWidth > (viewportWidth + 5);

                if (!needSlider)
                {
                    if (MainImageBar.Slider.Visibility != Visibility.Collapsed)
                    {
                        MainImageBar.Slider.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    // 确保先设置 Maximum，再显示，防止滑块瞬间跳变
                    MainImageBar.Slider.Maximum = Math.Max(0, _imageFiles.Count - 1);

                    if (MainImageBar.Slider.Visibility != Visibility.Visible)
                    {
                        MainImageBar.Slider.Visibility = Visibility.Visible;
                    }

                    // 5. 更新 Slider 位置
                    if (FileTabs.Count > 0 && !_isSyncingSlider)
                    {
                        var firstTab = FileTabs[0];
                        int firstTabGlobalIndex = _imageFiles.IndexOf(firstTab.FilePath);

                        if (firstTabGlobalIndex >= 0)
                        {
                            double maxLeftGlobalIndex = _imageFiles.Count - (viewportWidth / itemWidth);

                            if (maxLeftGlobalIndex > 0)
                            {
                                double currentLeftGlobalIndex = firstTabGlobalIndex + (MainImageBar.Scroller.HorizontalOffset / itemWidth);
                                double ratio = Math.Max(0, Math.Min(1, currentLeftGlobalIndex / maxLeftGlobalIndex));
                                double targetValue = ratio * (_imageFiles.Count - 1);

                                _isUpdatingUiFromScroll = true;
                                MainImageBar.Slider.Value = targetValue;
                                _isUpdatingUiFromScroll = false;
                            }
                        }
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded); // 使用 Loaded 优先级确保布局已就绪
        }

        private void OnFileTabsScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            
            if (_isProgrammaticScroll) return;   
            if (!_isInitialLayoutComplete) return;
            if (e == null) return;

            double itemWidth = 124; // 请确保这与 XAML 中 Tab 的实际宽度(包含Margin)一致
         
            // 1. 计算当前视图内可见的 Tab 范围（局部索引）
            int firstLocalIndex = (int)(MainImageBar.Scroller.HorizontalOffset / itemWidth);
            int visibleCount = (int)(MainImageBar.Scroller.ViewportWidth / itemWidth) + 2;

            // 2. 【核心修复】使用线性映射实现均匀滚动
            if (!_isSyncingSlider && _imageFiles.Count > 0 && FileTabs.Count > 0)
            {
                _isUpdatingUiFromScroll = true;
                try
                {
                    // A. 获取基础参数
                    double totalCount = _imageFiles.Count;
                    double viewportWidth = MainImageBar.Scroller.ViewportWidth;

                    // 计算当前窗口能完整显示多少张图片 (浮点数，例如能显示 5.5 张)
                    double visibleItemsCount = viewportWidth / itemWidth;

                    // B. 计算“视野左边缘”的全局精确索引
                    // 先找到当前加载的第一个 Tab 在全局列表里的位置
                    var firstTab = FileTabs[0];
                    int firstTabGlobalIndex = _imageFiles.IndexOf(firstTab.FilePath);

                    if (firstTabGlobalIndex >= 0)
                    {
                        // 加上当前的物理滚动偏移量 (转换为 item 单位)
                        // currentLeftGlobalIndex 代表：屏幕最左侧的那条像素线，对应的是第几张图
                        double currentLeftGlobalIndex = firstTabGlobalIndex + (MainImageBar.Scroller.HorizontalOffset / itemWidth);

                        // C. 计算映射比例
                        // 当滚动到底部时，左边缘的最大索引应该是 (总数 - 可视数量)
                        double maxLeftGlobalIndex = totalCount - visibleItemsCount;

                        // 防止除以0或负数（图片很少填不满屏幕的情况）
                        if (maxLeftGlobalIndex > 0)
                        {
                            // 计算进度比例 (0.0 ~ 1.0)
                            double ratio = currentLeftGlobalIndex / maxLeftGlobalIndex;

                            // 钳制范围，防止回弹时越界
                            ratio = Math.Max(0, Math.Min(1, ratio));

                            // D. 映射到 Slider 范围 (0 ~ Total-1)
                            double targetValue = ratio * (totalCount - 1);

                            // 只有变化超过微小阈值才赋值，减少计算抖动
                            if (Math.Abs(MainImageBar.Slider.Value - targetValue) > 0.05)
                            {
                                MainImageBar.Slider.Value = targetValue;
                            }
                        }
                        else
                        {
                            // 图片太少，不足以填满一屏，滑块始终在 0 或根据需求处理
                            MainImageBar.Slider.Value = 0;
                        }
                    }
                }
                finally
                {
                    _isUpdatingUiFromScroll = false;
                }
            }


            bool needLoadThumbnail = false;

            // 3. 【向后加载】逻辑优化 (Load Next)
            // 只要看到最后 5 个以内，且还有更多文件，就加载
            // 阈值调大到 5，防止滚动过快时出现空白
            if (FileTabs.Count > 0 &&
        firstLocalIndex + visibleCount >= FileTabs.Count - 5 &&
        FileTabs.Count < _imageFiles.Count)
            {
                var lastTab = FileTabs.Last();
                int lastFileIndex = _imageFiles.IndexOf(lastTab.FilePath);

                if (lastFileIndex >= 0 && lastFileIndex < _imageFiles.Count - 1)
                {
                    int takeCount = PageSize;
                    var nextItems = _imageFiles.Skip(lastFileIndex + 1).Take(takeCount);

                    foreach (var path in nextItems)
                    {
                        // --- [修改部分 START] ---
                        // 增加黑名单检查：如果已经在Tab里，或者被手动关闭过，就跳过
                        if (!FileTabs.Any(t => t.FilePath == path) && !_explicitlyClosedFiles.Contains(path))
                        {
                            FileTabs.Add(new FileTabItem(path));
                        }
                        // --- [修改部分 END] ---
                    }
                    needLoadThumbnail = true;
                }
            }

            // 4. 【向前加载】逻辑优化 (Load Previous)
            if (firstLocalIndex < 3 && FileTabs.Count > 0)
            {
                var firstTab = FileTabs[0];
                int firstFileIndex = _imageFiles.IndexOf(firstTab.FilePath);

                if (firstFileIndex > 0)
                {
                    // ... [保留计算 takeCount 的代码] ...
                    int takeCount = PageSize;
                    int start = Math.Max(0, firstFileIndex - takeCount);
                    int actualTake = firstFileIndex - start;

                    if (actualTake > 0)
                    {
                        var prevPaths = _imageFiles.Skip(start).Take(actualTake).Reverse();

                        int insertCount = 0;
                        foreach (var path in prevPaths)
                        {
                            // --- [修改部分 START] ---
                            if (!FileTabs.Any(t => t.FilePath == path) && !_explicitlyClosedFiles.Contains(path))
                            {
                                FileTabs.Insert(0, new FileTabItem(path));
                                insertCount++;
                            }
                            // --- [修改部分 END] ---
                        }

                        if (insertCount > 0)
                        {
                            MainImageBar.Scroller.ScrollToHorizontalOffset(MainImageBar.Scroller.HorizontalOffset + insertCount * itemWidth);
                            needLoadThumbnail = true;
                        }
                    }
                }
            }

            // 5. 触发缩略图懒加载
            if (needLoadThumbnail || Math.Abs(e.HorizontalChange) > 1 || Math.Abs(e.ExtentWidthChange) > 1)
            {
                int checkStart = Math.Max(0, firstLocalIndex - 2);
                int checkEnd = Math.Min(firstLocalIndex + visibleCount + 2, FileTabs.Count);

                for (int i = checkStart; i < checkEnd; i++)
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
        private void OnFileTabsWheelScroll(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer != null)
            {
                // 增加滚动速度系数 (例如 1.5倍)，让滚轮更跟手
                double scrollSpeed = 1.5;
                double offset = scrollViewer.HorizontalOffset - (e.Delta * scrollSpeed);

                // 边界检查
                if (offset < 0) offset = 0;
                if (offset > scrollViewer.ScrollableWidth) offset = scrollViewer.ScrollableWidth;

                scrollViewer.ScrollToHorizontalOffset(offset);
                e.Handled = true;
            }
        }

        private async void PreviewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUiFromScroll) return;

            if (_imageFiles == null || _imageFiles.Count == 0) return;
            if (_isSyncingSlider) return;

            _isSyncingSlider = true;
            try
            {
                int index = (int)Math.Round(e.NewValue);
                // 边界保护
                if (index < 0) index = 0;
                if (index >= _imageFiles.Count) index = _imageFiles.Count - 1;

                // 跳转逻辑：重新生成 Tab 列表
                // 注意：这里可能会导致 FileTabs 重置，从而触发 ScrollChanged。
                // 由于 _isSyncingSlider = true，ScrollChanged 内的逻辑会被跳过，这是安全的。
                await RefreshTabPageAsync(index, true);
            }
            finally
            {
                _isSyncingSlider = false;
            }
        }
        private void SetPreviewSlider()
        {
            if (_imageFiles == null || _imageFiles.Count == 0) return;
            MainImageBar.Slider.Minimum = 0;
            MainImageBar.Slider.Maximum = _imageFiles.Count - 1;
            MainImageBar.Slider.Value = _currentImageIndex;
        }

        // 补充定义：在类成员里加一个引用，记录当前是谁
        private FileTabItem _currentTabItem;

        private bool _isDragging = false;
        private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseOverThumb(e)) return;

            _isDragging = true;
            var slider = (Slider)sender;
            slider.CaptureMouse();
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

            double step = 1.0;

            // 如果按住 Shift 键，可以加速滚动
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                step = 5.0;
            }


            if (e.Delta < 0)
            {
                // 向下滚，增加 Value
                slider.Value = Math.Min(slider.Maximum, slider.Value + step);
            }
            else
            {
                // 向上滚，减少 Value
                slider.Value = Math.Max(slider.Minimum, slider.Value - step);
            }

            // 标记事件已处理，防止冒泡导致父容器(ScrollViewer)也跟着滚
            e.Handled = true;
        }

        private async void UpdateSliderValueFromPoint(Slider slider, Point position)
        {
            double ratio = position.Y / slider.ActualHeight;

            // 边界检查
            ratio = Math.Max(0, Math.Min(1, ratio));

            // 计算对应的 Slider 值
            double value = slider.Minimum + (slider.Maximum - slider.Minimum) * ratio;

            // 更新 UI
            slider.Value = value;
            if (_imageFiles != null && _imageFiles.Count > 0)
            {
                int index = (int)Math.Round(value);
                if (index >= 0 && index < _imageFiles.Count)
                {
                    await OpenImageAndTabs(_imageFiles[index], true);
                }
            }
        }
        private bool IsMouseOverThumb(MouseButtonEventArgs e)/// 检查鼠标事件的原始源是否是 Thumb 或其内部的任何元素。
        {
            var slider = (Slider)e.Source;
            var track = slider.Template.FindName("PART_Track", slider) as Track;
            if (track == null) return false;

            return track.Thumb.IsMouseOver;
        }
    }
}