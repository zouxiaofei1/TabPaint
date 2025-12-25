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
        private void OnPrependTabClick(object sender, RoutedEventArgs e)
        {
            var newTab = new FileTabItem(null)
            {
                IsNew = true,
                IsDirty = false
                // 记得生成一个默认的白色 Thumbnail 赋值进去，否则 UI 上是空的
            };

            var bmp = new RenderTargetBitmap(100, 60, 96, 96, PixelFormats.Pbgra32);
            var drawingVisual = new DrawingVisual();
            using (var context = drawingVisual.RenderOpen())
            {
                context.DrawRectangle(Brushes.White, null, new Rect(0, 0, 100, 60));
            }
            bmp.Render(drawingVisual);
            bmp.Freeze();
            newTab.Thumbnail = bmp;
            FileTabs.Insert(0, newTab); // 👈 关键：插入到 0

            // 滚回去看它
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
        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.N && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {

                CreateNewTab();
                e.Handled = true;
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
        private void OnClearUneditedClick(object sender, RoutedEventArgs e)
        {
            // 记录一下操作前当前选中的是谁，防止删掉后界面错乱
            var originalCurrent = _currentTabItem;
            bool currentRemoved = false;

            // 倒序遍历删除
            for (int i = FileTabs.Count - 1; i >= 0; i--)
            {
                var tab = FileTabs[i];
                if (!tab.IsDirty)
                {
                    // 如果删掉的是当前正在看的，做个标记
                    if (tab == originalCurrent) currentRemoved = true;

                    FileTabs.RemoveAt(i);
                }
            }
            if (currentRemoved)
            {
                if (FileTabs.Count > 0)
                {
                    // 选中最后一个或第一个
                    var newTab = FileTabs.Last();
                    // 模拟点击逻辑切换过去
                    OnFileTabClick(null, null); // 需要调整你的 OnFileTabClick 支持 null sender 或重构切换逻辑
                                                // 或者直接:
                    _ = OpenImageAndTabs(newTab.FilePath);
                }
                else
                {
                    // 全删光了，新建一个白板
                    // Clean_bitmap(...)
                }
            }
        }
        private void OnNewTabClick(object sender, RoutedEventArgs e)
        {
            // 创建一个纯内存的 Tab
            var newTab = new FileTabItem(null)
            {
                IsNew = true,
                IsDirty = false // 新建初始状态可以是 False，画了一笔后变 True
            };

            var bmp = new RenderTargetBitmap(100, 60, 96, 96, PixelFormats.Pbgra32);
            var drawingVisual = new DrawingVisual();
            using (var context = drawingVisual.RenderOpen())
            {
                context.DrawRectangle(Brushes.White, null, new Rect(0, 0, 100, 60));
            }
            bmp.Render(drawingVisual);
            bmp.Freeze();
            newTab.Thumbnail = bmp;

            FileTabs.Add(newTab);

            // 滚动到最后
            if (VisualTreeHelper.GetChildrenCount(FileTabList) > 0)
            {
                FileTabsScroller.ScrollToRightEnd();
            }
        }
        // 3. 放弃所有编辑 (Discard All)
        private async void OnDiscardAllClick(object sender, RoutedEventArgs e)
        {
            var dirtyTabs = FileTabs.Where(t => t.IsDirty).ToList();
            if (dirtyTabs.Count == 0) return;

            var result = System.Windows.MessageBox.Show(
                $"确定要放弃所有 {dirtyTabs.Count} 张图片的修改吗？\n这将还原到上次保存的状态，新建的未保存图片将丢失。",
                "警告",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                bool currentTabReverted = false;

                for (int i = FileTabs.Count - 1; i >= 0; i--)
                {
                    var tab = FileTabs[i];
                    if (tab.IsDirty)
                    {
                        // 清理缓存文件
                        if (File.Exists(tab.BackupPath))
                        {
                            File.Delete(tab.BackupPath);
                            tab.BackupPath = null;
                        }

                        if (tab.IsNew)
                        {
                            // A. 如果是新建的还没存过盘，直接移除
                            if (tab == _currentTabItem) currentTabReverted = true; // 标记当前页面被删了
                            FileTabs.RemoveAt(i);
                        }
                        else
                        {
                            // B. 如果是已存在文件，还原状态
                            tab.IsDirty = false;

                            // 重新加载缩略图 (视觉还原)
                            tab.IsLoading = true;
                            await tab.LoadThumbnailAsync(100, 60);

                            // 如果这个 Tab 恰好是当前正在看的，必须强制重载画布！
                            if (tab == _currentTabItem)
                            {
                                // 重新打开原文件
                                await OpenImageAndTabs(tab.FilePath);
                            }
                        }
                    }
                }

                // 如果当前的新建页面被删了，且列表还有其他图，切换到第一张
                if (currentTabReverted && !_currentTabItem.IsDirty && FileTabs.Count > 0)
                {
                    // 切换到剩下的某张图
                    var nextTab = FileTabs.FirstOrDefault();
                    if (nextTab != null) await OpenImageAndTabs(nextTab.FilePath);
                }
                else if (FileTabs.Count == 0)
                {
                    // 全空了
                    // Clean_bitmap(...)
                }

                // 更新 Session
                SaveSession();
            }
        }

    }
}