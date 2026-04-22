
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
        private bool _isWheelScrollingSlider = false;
        private System.Windows.Threading.DispatcherTimer _wheelLockTimer;
 
        // 在构造函数或 Window_Loaded 中初始化这个 Timer
        private void InitWheelLockTimer()
        {
            _wheelLockTimer = new System.Windows.Threading.DispatcherTimer();
            // 200ms 内没有新的滚轮操作，就认为操作结束，释放锁
            _wheelLockTimer.Interval = TimeSpan.FromMilliseconds(200);
            _wheelLockTimer.Tick += (s, e) =>
            {
                _isWheelScrollingSlider = false;
                _wheelLockTimer.Stop();
            };
        }

        public void UpdateImageBarSliderState()
        {
            a.s("UpdateImageBarSliderState");
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (MainImageBar == null || MainImageBar.Slider == null || MainImageBar.Scroller == null) return;

                if (_imageFiles == null || _imageFiles.Count == 0)
                {
                    // 以前是隐藏，现在改为禁用
                    MainImageBar.Slider.IsEnabled = false;
                    // 确保它是可见的（只是被禁用了）
                    if (MainImageBar.Slider.Visibility != Visibility.Visible)
                        MainImageBar.Slider.Visibility = Visibility.Visible;
                    return;
                }

                double itemWidth = 124.0;
                double viewportWidth = MainImageBar.Scroller.ViewportWidth;
                if (viewportWidth <= 0) viewportWidth = MainImageBar.Scroller.ActualWidth;
                if (viewportWidth <= 0) return;

                double requiredWidth = _imageFiles.Count * itemWidth;
                bool needSlider = requiredWidth > (viewportWidth + 5);

                if (MainImageBar.Slider.Visibility != Visibility.Visible)
                {
                    MainImageBar.Slider.Visibility = Visibility.Visible;
                }

                if (!needSlider)
                {
                    // 不需要滑动时，禁用控件
                    MainImageBar.Slider.IsEnabled = false;
                     MainImageBar.Slider.Value = 0; 
                }
                else
                {
                    // 需要滑动时，启用控件
                    MainImageBar.Slider.IsEnabled = true;
                    MainImageBar.Slider.Maximum = Math.Max(0, _imageFiles.Count - 1);
                    if (FileTabs.Count > 0 && !_isSyncingSlider)
                    {
                        var firstTab = FileTabs[0];
                        int firstTabGlobalIndex = _imageFiles.FindIndex(f => string.Equals(f, firstTab.FilePath, StringComparison.OrdinalIgnoreCase));

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
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        private void CleanupOffScreenTabs(double itemWidth, double viewportWidth)
        {
            if (FileTabs.Count <= 40) return; 
            double currentOffset = MainImageBar.Scroller.HorizontalOffset;

            // 计算当前视野内的第一个 Item 的索引（相对于 FileTabs 集合）
            int firstVisibleLocalIndex = (int)(currentOffset / itemWidth);

            int bufferCount = 15;
            int itemsToRemoveFromLeft = firstVisibleLocalIndex - bufferCount;

            if (itemsToRemoveFromLeft > 0)
            {
                _isProgrammaticScroll = true;

                // 批量移除左侧
                for (int i = 0; i < itemsToRemoveFromLeft; i++)
                {
                    if (FileTabs.Count > 0)
                    {
                        var tab = FileTabs[0];
                        // 在移除前记录备份信息，防止信息丢失
                        if (tab.IsDirty || !string.IsNullOrEmpty(tab.BackupPath))
                        {
                            UpdateSessionBackupInfo(tab.FilePath, tab.BackupPath, tab.IsDirty, tab.Id);
                        }
                        FileTabs.RemoveAt(0);
                    }
                }
                double offsetCorrection = itemsToRemoveFromLeft * itemWidth;
                MainImageBar.Scroller.ScrollToHorizontalOffset(currentOffset - offsetCorrection);

                _isProgrammaticScroll = false;
            }

            currentOffset = MainImageBar.Scroller.HorizontalOffset;
            firstVisibleLocalIndex = (int)(currentOffset / itemWidth);
            int visibleCount = (int)(viewportWidth / itemWidth) + 1;

            // 保留视野右边约 15 个作为缓冲
            int keepEndIndex = firstVisibleLocalIndex + visibleCount + bufferCount;

            // 从后往前删，避免索引错乱
            while (FileTabs.Count > keepEndIndex)
            {
                var tab = FileTabs[FileTabs.Count - 1];
                // 在移除前记录备份信息
                if (tab.IsDirty || !string.IsNullOrEmpty(tab.BackupPath))
                {
                    UpdateSessionBackupInfo(tab.FilePath, tab.BackupPath, tab.IsDirty, tab.Id);
                }
                FileTabs.RemoveAt(FileTabs.Count - 1);
            }
        }

        private void OnFileTabsScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            
            if (_isProgrammaticScroll) return;   
            if (!_isInitialLayoutComplete) return;
            if (e == null) return;
            double itemWidth = 124; 
            int firstLocalIndex = (int)(MainImageBar.Scroller.HorizontalOffset / itemWidth);
            int visibleCount = (int)(MainImageBar.Scroller.ViewportWidth / itemWidth) + 2;
            if (!_isSyncingSlider && !_isWheelScrollingSlider && _imageFiles.Count > 0 && FileTabs.Count > 0)
            {
                _isUpdatingUiFromScroll = true;
                try
                {
                    // A. 获取基础参数
                    double totalCount = _imageFiles.Count;
                    double viewportWidth = MainImageBar.Scroller.ViewportWidth;

                    // 计算当前窗口能完整显示多少张图片 (浮点数，例如能显示 5.5 张)
                    double visibleItemsCount = viewportWidth / itemWidth;

                    var firstTab = FileTabs[0];
                    int firstTabGlobalIndex = _imageFiles.FindIndex(f => string.Equals(f, firstTab.FilePath, StringComparison.OrdinalIgnoreCase));

                    if (firstTabGlobalIndex >= 0)
                    {
                        double currentLeftGlobalIndex = firstTabGlobalIndex + (MainImageBar.Scroller.HorizontalOffset / itemWidth);

                        double maxLeftGlobalIndex = totalCount - visibleItemsCount;
                        if (maxLeftGlobalIndex > 0)
                        {
                            // 计算进度比例 (0.0 ~ 1.0)
                            double ratio = currentLeftGlobalIndex / maxLeftGlobalIndex;
                            ratio = Math.Max(0, Math.Min(1, ratio));
                            double targetValue = ratio * (totalCount - 1);
                            if (Math.Abs(MainImageBar.Slider.Value - targetValue) > 0.05)
                            {
                                MainImageBar.Slider.Value = targetValue;
                            }
                        }
                        else MainImageBar.Slider.Value = 0;
                    }
                }
                finally
                {
                    _isUpdatingUiFromScroll = false;
                }
            }


            bool needLoadThumbnail = false;

            if (FileTabs.Count > 0 &&
        firstLocalIndex + visibleCount >= FileTabs.Count - 5 &&
        FileTabs.Count < _imageFiles.Count)
            {
                var lastTab = FileTabs.Last();
                int lastFileIndex = _imageFiles.FindIndex(f => string.Equals(f, lastTab.FilePath, StringComparison.OrdinalIgnoreCase));

                if (lastFileIndex >= 0 && lastFileIndex < _imageFiles.Count - 1)
                {
                    int takeCount = PageSize;
                    var nextItems = _imageFiles.Skip(lastFileIndex + 1).Take(takeCount);

                    foreach (var path in nextItems)
                    {
                        if (!FileTabs.Any(t => t.FilePath == path) && !_explicitlyClosedFiles.Contains(path))
                        {
                            FileTabs.Add(CreateTabFromPath(path));
                        }
                    }
                    needLoadThumbnail = true;
                }
            }

            //向前加载
            if (firstLocalIndex < 3 && FileTabs.Count > 0)
            {
                var firstTab = FileTabs[0];
                int firstFileIndex = _imageFiles.FindIndex(f => string.Equals(f, firstTab.FilePath, StringComparison.OrdinalIgnoreCase));

                if (firstFileIndex > 0)
                {
                    int takeCount = PageSize;
                    int start = Math.Max(0, firstFileIndex - takeCount);
                    int actualTake = firstFileIndex - start;

                    if (actualTake > 0)
                    {
                        var prevPaths = _imageFiles.Skip(start).Take(actualTake).Reverse();

                        int insertCount = 0;
                        foreach (var path in prevPaths)
                        {
                            if (!FileTabs.Any(t => t.FilePath == path) && !_explicitlyClosedFiles.Contains(path))
                            {
                                FileTabs.Insert(0, CreateTabFromPath(path));
                                insertCount++;
                            }
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
            CleanupOffScreenTabs(itemWidth, MainImageBar.Scroller.ViewportWidth);
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
        private System.Windows.Threading.DispatcherTimer _sliderDebounceTimer;
        private void InitDebounceTimer()
        {
          
            _sliderDebounceTimer = new System.Windows.Threading.DispatcherTimer(); 
       
            _sliderDebounceTimer.Interval = TimeSpan.FromMilliseconds(10);
            _sliderDebounceTimer.Tick += OnSliderDebounceTick;
        }
        private async void OnSliderDebounceTick(object sender, EventArgs e)
        {
            _sliderDebounceTimer.Stop(); // 停止计时器
           
            if (_imageFiles == null || _imageFiles.Count == 0) return;

            // 获取当前 Slider 对应的最终 Index
            int index = (int)Math.Round(MainImageBar.Slider.Value);

            // 边界修正
            if (index < 0) index = 0;
            if (index >= _imageFiles.Count) index = _imageFiles.Count - 1;

            // 执行耗时操作，此时 _isSyncingSlider 锁应该只包裹这里
            if (_isSyncingSlider) return;

            _isSyncingSlider = true;
            try
            {
                var currentTab = FileTabs.FirstOrDefault(t => t.FilePath == _imageFiles[index]);
                if (currentTab == null)
                {
                    await RefreshTabPageAsync(index, true);
                } // 如果已经在 Tab 里，可能只需要轻量切换
                else  await RefreshTabPageAsync(index, false);

                // 确保 UI 同步
                ScrollToTabCenter(currentTab ?? FileTabs.FirstOrDefault(t => t.FilePath == _imageFiles[index]));
            }
            finally
            {
                _isSyncingSlider = false;
            }
        }
        private void PreviewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // 如果是代码引起的滚动（比如滚轮导致Slider变动），不要触发重载
            if (_isUpdatingUiFromScroll) return;
            int index = (int)Math.Round(MainImageBar.Slider.Value);


            _sliderDebounceTimer?.Stop();
            _sliderDebounceTimer?.Start();
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
            if (_isDragging)
            {
                _isDragging = false;
                var slider = (Slider)sender;
                slider.ReleaseMouseCapture();
                _sliderDebounceTimer?.Stop();
                OnSliderDebounceTick(null, null);

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
            _isWheelScrollingSlider = true;

            // 2. 重置复位计时器（如果在200ms内连续滚动，锁会一直保持）
            _wheelLockTimer?.Stop();
            _wheelLockTimer?.Start();
  // 向下滚，增加 Value
            if (e.Delta < 0)  slider.Value = Math.Min(slider.Maximum, slider.Value + step);
 
            else slider.Value = Math.Max(slider.Minimum, slider.Value - step); // 向上滚，减少 Value
         
            // 标记事件已处理，防止冒泡导致父容器(ScrollViewer)也跟着滚
            e.Handled = true;
        }
        private FileTabItem CreateTabFromPath(string path)
        {
            var newTab = new FileTabItem(path);

            if (IsVirtualPath(path))
            {
                newTab.IsNew = true;

                // 1. 恢复正确的 ID 和编号
                string numPart = path.Replace(VirtualFilePrefix, "");

                if (int.TryParse(numPart, out int num))
                {
                    newTab.UntitledNumber = num;
                    newTab.Id = $"Virtual_{num}"; 
                }
                newTab.Id = $"Virtual_{Guid.NewGuid():N}";
                // 2. 检查是否有缓存文件（恢复内容）
                string potentialBackup = System.IO.Path.Combine(_cacheDir, $"{newTab.Id}.png");
                if (System.IO.File.Exists(potentialBackup))
                {
                    newTab.BackupPath = potentialBackup;
                    newTab.IsDirty = true; // 标记为脏，表示有未保存内容

                    // 异步加载缓存作为缩略图
                    newTab.IsLoading = true;
                    _ = newTab.LoadThumbnailAsync(100, 60).ContinueWith(t =>
                    {
                        newTab.IsLoading = false;
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }
                else  newTab.Thumbnail = GenerateBlankThumbnail();
            }
            else
            {
                // 普通文件逻辑
                
                // 检查是否有离线备份信息需要恢复
                if (_offScreenBackupInfos.TryGetValue(path, out var offlineInfo))
                {
                    newTab.Id = offlineInfo.Id;
                    newTab.BackupPath = offlineInfo.BackupPath;
                    newTab.IsDirty = offlineInfo.IsDirty;
                }

                newTab.IsLoading = true;
                _ = newTab.LoadThumbnailAsync(100, 60);
            }

            return newTab;
        }

        private void UpdateSliderValueFromPoint(Slider slider, Point position)
        {
            double ratio = position.Y / slider.ActualHeight;

            // 边界检查
            ratio = Math.Max(0, Math.Min(1, ratio));

            // 计算对应的 Slider 值
            double val = slider.Minimum + (slider.Maximum - slider.Minimum) * ratio;
            if (ratio > 0.99) val = slider.Maximum;
            if (ratio < 0.01) val = slider.Minimum;
            slider.Value = val;
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