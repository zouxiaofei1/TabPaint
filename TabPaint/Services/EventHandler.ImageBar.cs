using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using static TabPaint.MainWindow;

//
//TabPaint事件处理cs
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        // 这里的 1 表示下一个新建文件的编号
        private int _nextUntitledIndex = 1;

        private FileTabItem CreateNewUntitledTab()
        {
            a.s(1);
            var newTab = new FileTabItem(null)
            {
                IsNew = true,
                IsDirty = false,
                // 🔥 核心修复：赋值并递增全局计数器
                UntitledNumber = _nextUntitledIndex++,
                Thumbnail = GenerateBlankThumbnail()
            };
            return newTab;
        }

        private void OnPrependTabClick(object sender, RoutedEventArgs e)
        {
            var newTab = CreateNewUntitledTab();
            FileTabs.Insert(0, newTab);
            FileTabsScroller.ScrollToHorizontalOffset(0);
        }
        private void OnImageBarDragOver(object sender, System.Windows.DragEventArgs e)
        {
            // 修改点：同时检查 FileDrop 和 Bitmap
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ||
                e.Data.GetDataPresent(System.Windows.DataFormats.Bitmap))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
            e.Handled = true;
        }


        // 2. 拖拽放下处理：核心逻辑
        private async void OnImageBarDrop(object sender, System.Windows.DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) return;

            string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files == null || files.Length == 0) return;

            // 1. 确定插入位置
            int insertIndex = _imageFiles.Count; // 默认插到最后
            int uiInsertIndex = FileTabs.Count;

            // 如果当前有选中的 Tab，且不是新建的空文件，则插在它后面
            if (_currentTabItem != null && !_currentTabItem.IsNew)
            {
                int currentIndexInFiles = _imageFiles.IndexOf(_currentTabItem.FilePath);
                if (currentIndexInFiles >= 0)
                {
                    insertIndex = currentIndexInFiles + 1;
                }

                int currentIndexInTabs = FileTabs.IndexOf(_currentTabItem);
                if (currentIndexInTabs >= 0)
                {
                    uiInsertIndex = currentIndexInTabs + 1;
                }
            }

            FileTabItem firstNewTab = null;
            int addedCount = 0;

            foreach (string file in files)
            {
                if (IsImageFile(file))
                {
                    // 去重检查（可选，如果允许重复打开则去掉这行）
                    if (_imageFiles.Contains(file)) continue;

                    // 2. 插入到底层数据源 _imageFiles
                    // 注意：每次插入后，insertIndex 要递增，保证多张图片按顺序排在后面
                    _imageFiles.Insert(insertIndex + addedCount, file);

                    // 3. 插入到 UI 列表 FileTabs
                    var newTab = new FileTabItem(file);
                    newTab.IsLoading = true;

                    // 安全检查：防止 uiIndex 越界（虽然逻辑上不太可能）
                    if (uiInsertIndex + addedCount <= FileTabs.Count)
                    {
                        FileTabs.Insert(uiInsertIndex + addedCount, newTab);
                    }
                    else
                    {
                        FileTabs.Add(newTab);
                    }

                    // 异步加载缩略图
                    _ = newTab.LoadThumbnailAsync(100, 60);

                    // 记录第一张新图，用于稍后跳转
                    if (firstNewTab == null) firstNewTab = newTab;

                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                // 更新 Slider 范围
                ImageFilesCount = _imageFiles.Count;
                SetPreviewSlider();

                // 4. 自动切换到第一张新加入的图片
                if (firstNewTab != null)
                {
                    // 取消当前选中状态
                    if (_currentTabItem != null) _currentTabItem.IsSelected = false;

                    // 选中新图
                    firstNewTab.IsSelected = true;
                    _currentTabItem = firstNewTab;

                    await OpenImageAndTabs(firstNewTab.FilePath);

                    // 确保新加的图片在视野内
                    FileTabsScroller.ScrollToHorizontalOffset(FileTabsScroller.HorizontalOffset + 1); // 微调触发 Layout 更新
                                                                                                      // 如果需要精确滚动到新元素，可能需要计算 Offset，或者依赖之后的 Layout 刷新
                }
            }
        }

        private void OnSaveAllClick(object sender, RoutedEventArgs e)
        {
            // 筛选出所有脏文件
            var dirtyTabs = FileTabs.Where(t => t.IsDirty).ToList();
            if (dirtyTabs.Count == 0) return;

            int successCount = 0;
            foreach (var tab in dirtyTabs)
            {
                // 跳过没有路径的新建文件 (避免弹出10个保存对话框)
                if (tab.IsNew && string.IsNullOrEmpty(tab.FilePath)) continue;

                SaveSingleTab(tab);
                successCount++;
            }

            // 更新 Session (防止保存后 session.json 还记录着脏状态)
            SaveSession();

            // 简单提示 (实际项目中建议用 Statusbar)
            if (successCount > 0)
                System.Windows.MessageBox.Show($"已保存 {successCount} 张图片。");
        }

        // 2. 清空未编辑 (Clear Unedited)
        // 2. 清空未编辑 (Clear Unedited)
        private void OnClearUneditedClick(object sender, RoutedEventArgs e)
        {
            var originalCurrent = _currentTabItem;
            bool currentRemoved = false;

            for (int i = FileTabs.Count - 1; i >= 0; i--)
            {
                var tab = FileTabs[i];
                if (!tab.IsDirty)
                {
                    if (tab == originalCurrent) currentRemoved = true;
                    FileTabs.RemoveAt(i);
                }
            }

            // 如果列表空了，或者剩下的全是新建未保存的（一般逻辑上不太可能，除非全是脏的新建页）
            if (FileTabs.Count == 0)
            {
                ResetToNewCanvas();
            }
            else if (currentRemoved)
            {
                // 之前选中的被删了，切换到最后一个
                var newTab = FileTabs.Last();
                foreach (var t in FileTabs) t.IsSelected = false;
                newTab.IsSelected = true;
                _currentTabItem = newTab;

                if (newTab.IsNew)
                {
                    // 如果切到了一个新建页，清理画布（或者恢复该新建页的内容，如果有缓存的话）
                    // 这里简化处理：如果是新建页，且没有BackupPath，视为空白
                    if (string.IsNullOrEmpty(newTab.BackupPath))
                        Clean_bitmap(1200, 900);
                    else
                        _ = OpenImageAndTabs(newTab.BackupPath); // 尝试加载缓存
                }
                else
                {
                    _ = OpenImageAndTabs(newTab.FilePath);
                }
            }
        }

        private void OnNewTabClick(object sender, RoutedEventArgs e)
        {
            var newTab = CreateNewUntitledTab();
            FileTabs.Add(newTab);
            if (VisualTreeHelper.GetChildrenCount(FileTabList) > 0)
            {
                FileTabsScroller.ScrollToRightEnd();
            }
        }

        // 3. 放弃所有编辑 (Discard All) - 终极清理版
        private async void OnDiscardAllClick(object sender, RoutedEventArgs e)
        {
            // [修改点1]：现在的目标不仅是脏文件，还包括所有新建的未命名文件
            // 只要有任何改动，或者有任何新建的临时页，都允许执行重置
            bool hasTargets = FileTabs.Any(t => t.IsDirty || t.IsNew);
            if (!hasTargets) return;

            var result = System.Windows.MessageBox.Show(
                "确定要重置当前工作区吗？\n" +
                "· 所有“未命名”的新建画布将被删除\n" +
                "· 所有打开的图片将还原至磁盘文件状态\n" +
                "· 撤销记录将被清空",
                "放弃更改",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            _autoSaveTimer.Stop();

            var originalCurrentTab = _currentTabItem;
            bool currentTabAffected = false;

            // 倒序遍历
            for (int i = FileTabs.Count - 1; i >= 0; i--)
            {
                var tab = FileTabs[i];

                // 1. 先清理缓存文件 (如果有)
                if (tab.IsDirty && !string.IsNullOrEmpty(tab.BackupPath))
                {
                    try { if (File.Exists(tab.BackupPath)) File.Delete(tab.BackupPath); } catch { }
                    tab.BackupPath = null;
                }

                // [修改点2]：分类处理
                if (tab.IsNew)
                {
                    // A. 对于新建的文件 (无论是否修改过)：直接移除！
                    // 因为我们要"放弃所有更改"，新建的文件本身就是一种"更改"，所以要删掉
                    if (tab == originalCurrentTab) currentTabAffected = true;
                    FileTabs.RemoveAt(i);
                }
                else if (tab.IsDirty)
                {
                    // B. 对于磁盘上已有的文件 (且被修改过)：还原状态
                    tab.IsDirty = false;

                    // 标记受影响
                    if (tab == originalCurrentTab) currentTabAffected = true;

                    // 还原缩略图
                    tab.IsLoading = true;
                    await tab.LoadThumbnailAsync(100, 60);
                    tab.IsLoading = false;
                }
                // C. 对于磁盘上已有且未修改的文件：保持原样，不动它
            }

            // 3. 后续处理 (同前)
            if (FileTabs.Count == 0)
            {
                // 如果全删光了（比如全是新建的），重置为一张白纸
                ResetToNewCanvas();
            }
            else if (currentTabAffected)
            {
                // 如果当前页没了，或者当前页被重置了
                if (!FileTabs.Contains(originalCurrentTab))
                {
                    // 找个新的选中
                    var firstTab = FileTabs.FirstOrDefault();
                    if (firstTab != null)
                    {
                        foreach (var t in FileTabs) t.IsSelected = false;
                        firstTab.IsSelected = true;
                        _currentTabItem = firstTab;
                    }
                }

                // 刷新画布并清空 Undo
                if (_currentTabItem != null)
                {
                    await OpenImageAndTabs(_currentTabItem.FilePath);
                    ResetDirtyTracker(); // 必须清空撤销栈！
                }
            }

            SaveSession();
            GC.Collect();
        }


    }
}