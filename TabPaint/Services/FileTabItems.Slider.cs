
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
                _isUpdatingUiFromScroll = true;
                PreviewSlider.Value = firstIndex;
                _isUpdatingUiFromScroll = false;
            }
            bool needload = false;

            if (FileTabs.Count > 0 && lastIndex >= FileTabs.Count - 2 && FileTabs.Count < _imageFiles.Count) // 阈值调小一点，体验更丝滑
            {
                var lastTab = FileTabs[FileTabs.Count - 1];
                int lastFileIndex = _imageFiles.IndexOf(lastTab.FilePath);

                if (lastFileIndex >= 0 && lastFileIndex < _imageFiles.Count - 1)
                {
                    var nextItems = _imageFiles.Skip(lastFileIndex + 1).Take(PageSize);

                    foreach (var path in nextItems)
                    {
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

        private void OnFileTabsWheelScroll(object sender, MouseWheelEventArgs e)// 鼠标滚轮横向滚动
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

        // 当滑块拖动时触发
        private async void PreviewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUiFromScroll) return;

            if (_isSyncingSlider) return;
            if (_imageFiles == null || _imageFiles.Count == 0) return;

            // 只有当变化量足够大（或者是拖动结束）时才加载图片，防止滑动太快卡顿
            // 这里做一个简单的去抖动处理，或者直接加载
            int index = (int)e.NewValue;

            // 边界检查
            if (index < 0) index = 0;
            if (index >= _imageFiles.Count) index = _imageFiles.Count - 1;

            // 调用你的加载逻辑，并不触发滚动条反向更新 Slider
            _isSyncingSlider = true;
            // a.s("PreviewSlider_ValueChanged");
            // await OpenImageAndTabs(_imageFiles[index],true);
            //这个会导致双重加载!!
            _isSyncingSlider = false;
        }
        private void SetPreviewSlider()
        {
            if (_imageFiles == null || _imageFiles.Count == 0) return;
            PreviewSlider.Minimum = 0;
            PreviewSlider.Maximum = _imageFiles.Count - 1;
            PreviewSlider.Value = _currentImageIndex;
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
            double ratio = position.Y / slider.ActualHeight;
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
    }
}