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
        private void OnFileTabCloseClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is System.Windows.Controls.Button btn && btn.Tag is FileTabItem item)
            {
                CloseTab(item);
            }
        }
        private async void OnFileTabClick(object sender, RoutedEventArgs e)
        {
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
                if (_currentTabItem != null)
                {
                    _autoSaveTimer.Stop();

                    if (_currentTabItem.IsDirty || _currentTabItem.IsNew)
                    {
                        UpdateTabThumbnail(_currentTabItem);
                        TriggerBackgroundBackup();
                    }
                }

                foreach (var tab in FileTabs) tab.IsSelected = false;
                clickedItem.IsSelected = true;
                ScrollToTabCenter(clickedItem);
                if (clickedItem.IsNew)
                {
                    Clean_bitmap(1200, 900); // 这里可以使用默认尺寸或上次记忆的尺寸

                    _currentFilePath = string.Empty;
                    _currentFileName = "未命名";
                    _currentTabItem = clickedItem;
                    UpdateWindowTitle();
                }
                else
                {
                    await OpenImageAndTabs(clickedItem.FilePath);
                }
                _currentTabItem = clickedItem;
            }
        }

        private FileTabItem CreateNewUntitledTab()
        {
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
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ||
                e.Data.GetDataPresent(System.Windows.DataFormats.Bitmap))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
                ShowDragOverlay("添加到当前列表", "将图片作为新标签页加入");
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
            HideDragOverlay(); // 隐藏遮罩
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
            // 这里不再预先判断 hasTargets，因为即使内存里没脏数据，缓存文件夹里可能有垃圾需要清理
            // 提示文案稍微改重一点，强调会清除缓存
            var result = System.Windows.MessageBox.Show(
                "确定要重置当前工作区吗？\n" +
                "· 所有“未命名”的新建画布将被删除\n" +
                "· 所有打开的图片将还原至磁盘文件状态\n" +
                "· 撤销记录、所有临时缓存文件及会话记录将被清空",
                "放弃更改",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            _autoSaveTimer.Stop();

            // --- 核心新增：强制清理物理文件 (Cache & Session) ---
            try
            {
                // 1. 删除 Session.json
                if (File.Exists(_sessionPath))
                {
                    File.Delete(_sessionPath);
                }

                // 2. 清空 Cache 文件夹下的所有文件 (无论程序是否知道它们)
                if (Directory.Exists(_cacheDir))
                {
                    string[] cacheFiles = Directory.GetFiles(_cacheDir);
                    foreach (string file in cacheFiles)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch
                        {
                            // 忽略占用错误，尽力而为
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cleanup failed: {ex.Message}");
            }
            // ----------------------------------------------------

            var originalCurrentTab = _currentTabItem;
            bool currentTabAffected = false;

            // 倒序遍历内存中的 Tabs 进行重置
            for (int i = FileTabs.Count - 1; i >= 0; i--)
            {
                var tab = FileTabs[i];

                // 先把 BackupPath 指针置空，因为物理文件刚才已经被我们强删了
                tab.BackupPath = null;

                if (tab.IsNew)
                {
                    // A. 对于新建的文件：直接移除
                    if (tab == originalCurrentTab) currentTabAffected = true;
                    FileTabs.RemoveAt(i);
                }
                else if (tab.IsDirty)
                {
                    // B. 对于磁盘上已有的文件 (且被修改过)：还原状态
                    tab.IsDirty = false;

                    // 标记受影响
                    if (tab == originalCurrentTab) currentTabAffected = true;

                    // 还原缩略图 (重新从原图读取)
                    tab.IsLoading = true;
                    await tab.LoadThumbnailAsync(100, 60);
                    tab.IsLoading = false;
                }
                // C. 没改过的文件保持原样
            }

            // 后续 UI 处理
            if (FileTabs.Count == 0)
            {
                // 如果全删光了，重置为一张白纸
                ResetToNewCanvas();
            }
            else if (currentTabAffected)
            {
                // 如果当前页没了，或者当前页被重置了
                if (!FileTabs.Contains(originalCurrentTab))
                {
                    // 找个新的选中 (默认第一个)
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
                    // 重新加载原图 (因为缓存已经被删了，OpenImageAndTabs 内部会去读原文件)
                    await OpenImageAndTabs(_currentTabItem.FilePath);
                    ResetDirtyTracker();
                }
            }
            else
            {
                // 即使当前 Tab 没受影响（比如当前 Tab 本来就是干净的），
                // 但因为我们强删了 Session 和 Cache，最好还是重置一下 Undo 栈以防万一
                ResetDirtyTracker();
            }

            // 强制 GC
            GC.Collect();
        }
        private void OnTabOpenFolderClick(object sender, RoutedEventArgs e)
        {
            // 获取绑定的 Tab 对象
            if (sender is MenuItem item && item.Tag is FileTabItem tab)
            {
                // 1. 检查路径是否有效（防止对 "未命名" 的新建文件操作）
                if (string.IsNullOrEmpty(tab.FilePath))
                {
                    return;
                }

                // 2. 再次确认文件是否存在（防止文件已被外部删除）
                if (!System.IO.File.Exists(tab.FilePath))
                {
                    System.Windows.MessageBox.Show("文件已不存在，无法定位文件夹。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    // 3. 使用 explorer.exe 的 /select 参数来打开文件夹并选中文件
                    string argument = $"/select, \"{tab.FilePath}\"";
                    System.Diagnostics.Process.Start("explorer.exe", argument);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"无法打开文件夹: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        private void OnFileTabPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                if (sender is System.Windows.Controls.Button btn && btn.DataContext is FileTabItem item)
                {
                    CloseTab(item); // 复用已有的关闭逻辑
                    e.Handled = true; // 阻止事件冒泡，防止触发其他点击行为
                }
                return;
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                _dragStartPoint = e.GetPosition(null);
            }
        }
        private void OnFileTabPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // 1. 如果左键没按住，直接返回
            if (e.LeftButton != MouseButtonState.Pressed) return;

            // 2. [核心修复] 计算移动距离，增加“防抖动”阈值
            // 系统默认是 4px，太灵敏了。我们改为 10px 或者 15px。
            Vector diff = _dragStartPoint - e.GetPosition(null);
            double dragThreshold = 100.0; // 手动设置阈值为 100 像素

            // 如果 X 和 Y 方向移动都小于阈值，视为误触，不执行拖拽
            if (Math.Abs(diff.X) < dragThreshold && Math.Abs(diff.Y) < dragThreshold)
            {
                return;
            }

            // --- 以下是原有的拖拽逻辑，保持不变 ---

            var button = sender as System.Windows.Controls.Button;
            var tabItem = button?.DataContext as FileTabItem;
            if (tabItem == null) return;

            // === 分支 1: Ctrl + 拖拽 = 导出文件 ===
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                // ... (保持之前的导出文件代码不变) ...
                // 省略代码以节省篇幅，请保留你现有的 Ctrl+拖拽 逻辑
                // ...
                string finalDragPath = tabItem.FilePath;
                // ...
                if (!string.IsNullOrEmpty(finalDragPath) && System.IO.File.Exists(finalDragPath))
                {
                    var dataObject = new System.Windows.DataObject();
                    var fileList = new System.Collections.Specialized.StringCollection();
                    fileList.Add(finalDragPath);
                    dataObject.SetFileDropList(fileList);
                    dataObject.SetData("TabPaintInternalDrag", true);
                    DragDrop.DoDragDrop(button, dataObject, System.Windows.DragDropEffects.Copy);
                    e.Handled = true;
                }
            }
            // === 分支 2: 直接拖拽 = 内部排序 ===
            else
            {
                var dataObject = new System.Windows.DataObject();
                dataObject.SetData("TabPaintReorderItem", tabItem);

                // 这里的 DoDragDrop 会阻塞线程直到松手
                DragDrop.DoDragDrop(button, dataObject, System.Windows.DragDropEffects.Move);

                // 拖拽结束后，标记已处理，防止触发 Click 事件
                e.Handled = true;
            }
        }


        private void OnFileTabReorderDragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TabPaintReorderItem"))
            {
                e.Effects = System.Windows.DragDropEffects.Move;
                e.Handled = true;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
        }

        // [新增] 放下时交换顺序
        private void OnFileTabDrop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TabPaintReorderItem"))
            {
                var sourceTab = e.Data.GetData("TabPaintReorderItem") as FileTabItem;
                var targetButton = sender as System.Windows.Controls.Button;
                var targetTab = targetButton?.DataContext as FileTabItem;

                if (sourceTab != null && targetTab != null && sourceTab != targetTab)
                {
                    // 1. 在 UI 集合 (FileTabs) 中移动位置
                    int oldIndex = FileTabs.IndexOf(sourceTab);
                    int newIndex = FileTabs.IndexOf(targetTab);

                    if (oldIndex >= 0 && newIndex >= 0)
                    {
                        FileTabs.Move(oldIndex, newIndex);
                    }
                    if (!string.IsNullOrEmpty(sourceTab.FilePath) &&
                        !string.IsNullOrEmpty(targetTab.FilePath))
                    {
                        int srcFileIdx = _imageFiles.IndexOf(sourceTab.FilePath);
                        int tgtFileIdx = _imageFiles.IndexOf(targetTab.FilePath);

                        if (srcFileIdx >= 0 && tgtFileIdx >= 0)
                        {
                            // 简单的列表移动逻辑
                            string item = _imageFiles[srcFileIdx];
                            _imageFiles.RemoveAt(srcFileIdx);
                            int newTgtIdx = _imageFiles.IndexOf(targetTab.FilePath);
                            if (newIndex > oldIndex)
                            {
                                if (newTgtIdx >= 0)
                                {
                                    _imageFiles.Insert(newTgtIdx, item);
                                }
                            }
                            else
                            {
                                // 向前移：直接插在目标前面
                                if (newTgtIdx >= 0) _imageFiles.Insert(newTgtIdx, item);
                            }
                        }
                    }
                }
                e.Handled = true;
            }
        }
        // #region 右键菜单事件 (Context Menu Events)

        private void OnTabCopyClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is FileTabItem tab)
            {
                CopyTabToClipboard(tab);
            }
        }

        private void OnTabCutClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is FileTabItem tab)
            {
                // 1. 先复制
                CopyTabToClipboard(tab);
                // 2. 后关闭 (实现剪切效果)
                CloseTab(tab);
            }
        }

        private async void OnTabPasteClick(object sender, RoutedEventArgs e)
        {
            // 获取插入位置：在右键点击的 Tab 后面
            int insertIndex = -1; // 默认最后
            int uiInsertIndex = FileTabs.Count;

            if (sender is MenuItem item && item.Tag is FileTabItem targetTab)
            {
                int targetUiIndex = FileTabs.IndexOf(targetTab);
                if (targetUiIndex >= 0)
                {
                    uiInsertIndex = targetUiIndex + 1;
                    // 尝试在 _imageFiles 里找到对应位置
                    if (!string.IsNullOrEmpty(targetTab.FilePath))
                    {
                        int fileIndex = _imageFiles.IndexOf(targetTab.FilePath);
                        if (fileIndex >= 0) insertIndex = fileIndex + 1;
                    }
                }
            }
            if (insertIndex == -1) insertIndex = _imageFiles.Count;

            bool hasHandled = false;
            IDataObject data = Clipboard.GetDataObject(); // 获取剪贴板数据对象

            // ---------------------------------------------------------
            // 情况 A: 粘贴的是文件 (从资源管理器复制)
            // ---------------------------------------------------------
            if (data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    int addedCount = 0;
                    foreach (string file in files)
                    {
                        if (IsImageFile(file) && !_imageFiles.Contains(file))
                        {
                            _imageFiles.Insert(insertIndex + addedCount, file);

                            var newTab = new FileTabItem(file) { IsLoading = true };
                            FileTabs.Insert(uiInsertIndex + addedCount, newTab);
                            _ = newTab.LoadThumbnailAsync(100, 60);

                            addedCount++;
                        }
                    }
                    if (addedCount > 0) hasHandled = true;
                }
            }
            // ---------------------------------------------------------
            // 情况 B: 粘贴的是图片 (从网页/QQ/截图工具复制)
            // ---------------------------------------------------------
            else if (data.GetDataPresent(DataFormats.Bitmap))
            {
                try
                {
                    // 获取位图数据
                    BitmapSource source = Clipboard.GetImage();
                    if (source != null)
                    {
                        // 1. 创建一个新的未命名 Tab
                        var newTab = CreateNewUntitledTab();

                        // 2. 立即将剪贴板图片保存为缓存文件
                        // 必须这样做，否则下次加载这个 Tab 时它就是空的
                        string cacheFileName = $"{newTab.Id}.cache.png";
                        string fullCachePath = System.IO.Path.Combine(_cacheDir, cacheFileName);

                        using (var fileStream = new FileStream(fullCachePath, FileMode.Create))
                        {
                            BitmapEncoder encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(source));
                            encoder.Save(fileStream);
                        }

                        // 3. 设置 Tab 属性
                        newTab.BackupPath = fullCachePath;
                        newTab.IsDirty = true; // 标记为脏，提示用户保存

                        // 生成一个小缩略图给 UI 显示
                        UpdateTabThumbnail(fullCachePath); // 这一步可以优化，暂时先这样

                        // 4. 插入 UI
                        FileTabs.Insert(uiInsertIndex, newTab);

                        // 5. 自动选中并滚动
                        // 注意：这里我们手动把 UI 滚动过去
                        FileTabsScroller.ScrollToHorizontalOffset(FileTabsScroller.HorizontalOffset + 120);

                        hasHandled = true;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("粘贴图片失败: " + ex.Message);
                }
            }

            if (hasHandled)
            {
                // 刷新底部 Slider 数量
                ImageFilesCount = _imageFiles.Count;
                SetPreviewSlider();
            }
        }


        private void OnTabDeleteClick(object sender, RoutedEventArgs e)
        {
            // 这里的“关闭”等同于界面上的 X 按钮
            if (sender is MenuItem item && item.Tag is FileTabItem tab)
            {
                CloseTab(tab);
            }
        }

        private void OnTabFileDeleteClick(object sender, RoutedEventArgs e)
        {
            // 这里的“删除”是物理删除文件
            if (sender is MenuItem item && item.Tag is FileTabItem tab)
            {
                if (tab.IsNew && string.IsNullOrEmpty(tab.FilePath))
                {
                    CloseTab(tab); // 如果是没保存的新建画布，直接关掉
                    return;
                }

                var result = MessageBox.Show(
                    $"确定要将文件 '{tab.FileName}' 放入回收站吗？\n此操作不可撤销（取决于系统设置）。",
                    "删除文件",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    string path = tab.FilePath;

                    // 1. 先从 TabPaint 关闭
                    CloseTab(tab);

                    // 2. 再执行物理删除
                    try
                    {
                        if (File.Exists(path))
                        {
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                                path,
                                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("删除失败: " + ex.Message);
                    }
                }
            }
        }

        // 辅助方法：复制 Tab 到剪贴板
        private void CopyTabToClipboard(FileTabItem tab)
        {
            var dataObject = new DataObject();
            bool hasContent = false;
            BitmapSource heavyBitmap = null;

            try
            {
                // ---------------------------------------------------------
                // 策略 A: 优先使用文件路径 (内存消耗 ≈ 0)
                // 适用场景: 文件已存在于磁盘，且当前没有未保存的修改
                // ---------------------------------------------------------
                if (!string.IsNullOrEmpty(tab.FilePath) && File.Exists(tab.FilePath) && !tab.IsDirty)
                {
                    var fileList = new System.Collections.Specialized.StringCollection();
                    fileList.Add(tab.FilePath);
                    dataObject.SetFileDropList(fileList);
                    hasContent = true;

                    // 注意：这里我们故意不调用 SetImage。
                    // 99% 的软件（QQ, 微信, PS, 资源管理器）都能识别文件路径。
                    // 这样做避免了将几十MB的像素数据解压到内存中。
                }
                // ---------------------------------------------------------
                // 策略 B: 只有在迫不得已时才渲染像素 (高内存消耗)
                // 适用场景: 新建的未命名画布，或者有未保存的涂鸦
                // ---------------------------------------------------------
                else
                {
                    heavyBitmap = GetHighResImageForTab(tab);
                    if (heavyBitmap != null)
                    {
                        dataObject.SetImage(heavyBitmap);
                        hasContent = true;
                    }
                }

                if (hasContent)
                {
                    // 第二个参数 true 表示在此应用程序退出后，剪贴板数据仍然有效
                    // 这会促使数据被系统复制，但也会增加内存压力
                    Clipboard.SetDataObject(dataObject, true);
                }
            }
            catch (Exception ex)
            {
                // 剪贴板不仅内存敏感，还容易被其他程序锁定导致 COMException
                System.Diagnostics.Debug.WriteLine($"Copy failed: {ex.Message}");
            }
            finally
            {
                // ---------------------------------------------------------
                // 策略 C: 激进的资源清理
                // ---------------------------------------------------------
                dataObject = null;
                heavyBitmap = null;

                // 只有在执行了策略 B (大图渲染) 时，才需要强制 GC
                // 如果只是复制了路径，没必要强制 GC，以免造成界面卡顿
                if (tab.IsDirty || tab.IsNew)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect(); // 再次调用以确保完全释放
                }
            }
        }


        // #endregion



    }
}