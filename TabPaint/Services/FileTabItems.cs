
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


        private void LoadTabPageAsync(int centerIndex)
        {
            if (_imageFiles == null || _imageFiles.Count == 0) return;

            int start = Math.Max(0, centerIndex - PageSize);
            int end = Math.Min(_imageFiles.Count - 1, centerIndex + PageSize);
            var viewportPaths = new HashSet<string>();

            string centerPath = (centerIndex >= 0 && centerIndex < _imageFiles.Count) ? _imageFiles[centerIndex] : null;

            for (int i = start; i <= end; i++) viewportPaths.Add(_imageFiles[i]);

            // 1. 清理：移除不可见 且 不是脏数据/新数据 的Tab
            // 注意：如果是虚拟路径的Tab，它肯定是IsNew，所以这里会被保留，这是正确的
            for (int i = FileTabs.Count - 1; i >= 0; i--)
            {
                var tab = FileTabs[i];
                bool isViewport = viewportPaths.Contains(tab.FilePath);
                bool isKeepAlive = tab.IsDirty || tab.IsNew;

                if (!isViewport && !isKeepAlive)
                {
                    FileTabs.RemoveAt(i);
                }
            }

            // 2. 添加/排序
            for (int i = start; i <= end; i++)
            {
                string path = _imageFiles[i];

                // 过滤黑名单
                if (_explicitlyClosedFiles.Contains(path) && path != centerPath) continue;
                if (path == centerPath && _explicitlyClosedFiles.Contains(path)) _explicitlyClosedFiles.Remove(path);

                var existingTab = FileTabs.FirstOrDefault(t => t.FilePath == path);

                // 如果存在，不做处理 (位置调整太复杂，暂时忽略，WPF ObservableCollection 会自动处理绑定)
                // 如果不存在，需要添加
                if (existingTab == null)
                {
                    // 【核心修正】：如果是虚拟路径，且当前 FileTabs 里没找到，
                    // 说明这是一个逻辑错误（虚拟文件肯定在内存里），或者是在 Session Restore 刚开始。
                    // 无论如何，LoadThumbnailAsync 对虚拟路径会返回 null (因为File.Exists为false)，
                    // 这会导致显示空白缩略图，这是符合预期的。

                    var newTab = new FileTabItem(path);

                    if (IsVirtualPath(path))
                    {
                        newTab.IsNew = true;
                        // 尝试解析 ID 以恢复 UntitledNumber? 
                        // 暂时不解析，显示 "未命名" 即可，或者你可以存 Session 时把 Number 存进去
                        newTab.Thumbnail = GenerateBlankThumbnail();
                    }
                    else
                    {
                        newTab.IsLoading = true;
                        _ = newTab.LoadThumbnailAsync(100, 60);
                    }

                    // 插入排序逻辑
                    int insertIndex = 0;
                    bool inserted = false;
                    for (int j = 0; j < FileTabs.Count; j++)
                    {
                        var t = FileTabs[j];
                        // 如果当前遍历到的 FileTab 在 _imageFiles 里的位置，比目标 path 的位置靠后，说明 path 应该插在它前面
                        int tIndex = _imageFiles.IndexOf(t.FilePath);

                        // 如果 tIndex == -1 (说明这个 Tab 可能刚被删了? 或者异常)，把它往后放
                        if (tIndex == -1 || tIndex > i)
                        {
                            FileTabs.Insert(j, newTab);
                            inserted = true;
                            break;
                        }
                    }
                    if (!inserted) FileTabs.Add(newTab);
                }
            }

            // 不在这里频繁调 UpdateImageBarSliderState，防止递归或卡顿
            // 由外部 Scroll 事件或 Add/Remove 事件去触发 Slider 更新
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
                }
                finally
                {
                    _isProgrammaticScroll = false; // 🔓 解锁
                }
            });
        }

        private void ScrollToTabCenter(FileTabItem targetTab)
        {
            if (targetTab == null) return;

            // 使用 ContextIdle 优先级，确保在 UI 布局更新（比如 Tab 变大或变色后）再执行滚动
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (FileTabList == null) return;
                    var container = FileTabList.ItemContainerGenerator.ContainerFromItem(targetTab) as FrameworkElement;

                    if (container != null)
                    {
                        var transform = container.TransformToAncestor(FileTabList);
                        var rootPoint = transform.Transform(new Point(0, 0));

                        double itemLeft = rootPoint.X;
                        double itemWidth = container.ActualWidth;
                        double viewportWidth = FileTabsScroller.ViewportWidth;

                        double centerOffset = (itemLeft + itemWidth / 2) - (viewportWidth / 2);

                        if (centerOffset < 0) centerOffset = 0;
                        if (centerOffset > FileTabsScroller.ScrollableWidth) centerOffset = FileTabsScroller.ScrollableWidth;

                        FileTabsScroller.ScrollToHorizontalOffset(centerOffset);

                    }
                }
                catch (Exception ex)
                {
                }
            }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        }


        private void ScrollViewer_ManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)// 阻止边界反馈
        {
            e.Handled = true;
        }

        private async void CloseTab(FileTabItem item)
        {
            // 1. 脏检查
            if (item.IsDirty)
            {
                var result = System.Windows.MessageBox.Show(
                    $"图片 {item.DisplayName} 尚未保存，是否保存？",
                    "保存提示",
                    MessageBoxButton.YesNoCancel);

                if (result == MessageBoxResult.Cancel) return;
                if (result == MessageBoxResult.Yes) SaveSingleTab(item);
            }

            // 记录移除前的位置和状态
            string pathToRemove = item.FilePath;
            int removedUiIndex = FileTabs.IndexOf(item);
            bool wasSelected = item.IsSelected;

            // 2. 从集合中移除
            FileTabs.Remove(item);
            if (!string.IsNullOrEmpty(pathToRemove) && _imageFiles.Contains(pathToRemove))
            {
                _imageFiles.Remove(pathToRemove);
            }

            // 3. 清理缓存物理文件
            if (!string.IsNullOrEmpty(item.BackupPath) && File.Exists(item.BackupPath))
            {
                try { File.Delete(item.BackupPath); } catch { }
            }

            // 4. 处理极端情况：没有标签页了
            if (FileTabs.Count == 0)
            {
                _imageFiles.Clear();
                ResetToNewCanvas(); // ResetToNewCanvas 内部通常会调用 CreateNewTab 确保至少有一个页
                UpdateImageBarSliderState();
                return;
            }

            // 5. 【核心改动】处理选中项切换
            if (wasSelected)
            {
                // 计算新的选中项：优先选择原来位置的前一个，如果已经是第一个，则选现在的第一个
                int newIndex = Math.Max(0, Math.Min(removedUiIndex - 1, FileTabs.Count - 1));
                var nextTab = FileTabs[newIndex];

                // 直接调用封装好的切换方法
                SwitchToTab(nextTab);
            }
            else
            {
                // 如果关闭的不是当前选中的，虽然当前选中项没变，但它在 _imageFiles 里的 Index 可能变了
                if (_currentTabItem != null)
                {
                    _currentImageIndex = _imageFiles.IndexOf(_currentTabItem.FilePath);
                }
            }

            // 6. UI 状态同步
            UpdateImageBarSliderState();
            UpdateWindowTitle();
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
       
        private void MarkAsSaved()
        {//仅mark不负责保存!!
            if (_currentTabItem == null) return;
            _savedUndoPoint = _undo.UndoCount;

            _currentTabItem.IsDirty = false;

           // SaveSession();
        }
    }
}