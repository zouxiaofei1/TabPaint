using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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


                SwitchToTab(clickedItem);
            }
        }

        private FileTabItem CreateNewUntitledTab()
        {
            var newTab = new FileTabItem(null)
            {
                IsNew = true,
                IsDirty = false,
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
            UpdateImageBarSliderState();
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
            UpdateImageBarSliderState();
        }

        private void OnNewTabClick(object sender, RoutedEventArgs e)
        {
            var newTab = CreateNewUntitledTab();
            FileTabs.Add(newTab);
            if (VisualTreeHelper.GetChildrenCount(FileTabList) > 0)
            {
                FileTabsScroller.ScrollToRightEnd();
            }
            UpdateImageBarSliderState();
        }

        // 3. 放弃所有编辑 (Discard All) - 终极清理版
        private async void OnDiscardAllClick(object sender, RoutedEventArgs e)
        {
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
                ResetDirtyTracker();
            }

            // 强制 GC
            GC.Collect();
            UpdateImageBarSliderState();
        }
        private void OnTabOpenFolderClick(object sender, RoutedEventArgs e)
        {
            // 获取绑定的 Tab 对象
            if (sender is MenuItem item && item.Tag is FileTabItem tab)
            {
                // 1. 检查路径是否有效（防止对 "未命名" 的新建文件操作）
                if (string.IsNullOrEmpty(tab.FilePath))return;
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

            Vector diff = _dragStartPoint - e.GetPosition(null);
            double dragThreshold = 100.0; 
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
        private void OnFileTabDrop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TabPaintReorderItem"))
            {
                var sourceTab = e.Data.GetData("TabPaintReorderItem") as FileTabItem;
                var targetButton = sender as System.Windows.Controls.Button;
                var targetTab = targetButton?.DataContext as FileTabItem;

                if (sourceTab != null && targetTab != null && sourceTab != targetTab)
                {
                    // --- 1. 更新 UI 集合 (FileTabs) ---
                    int oldUIIndex = FileTabs.IndexOf(sourceTab);
                    int newUIIndex = FileTabs.IndexOf(targetTab);

                    if (oldUIIndex >= 0 && newUIIndex >= 0)
                    {
                        FileTabs.Move(oldUIIndex, newUIIndex);

                        // --- 2. 核心：更新底层数据集合 (_imageFiles) ---
                        // 找到对应的文件路径
                        string sourcePath = sourceTab.FilePath;
                        string targetPath = targetTab.FilePath;

                        int srcFileIdx = _imageFiles.IndexOf(sourcePath);
                        int tgtFileIdx = _imageFiles.IndexOf(targetPath);

                        if (srcFileIdx >= 0 && tgtFileIdx >= 0)
                        {
                            _imageFiles.RemoveAt(srcFileIdx);
                            int newTgtIdx = _imageFiles.IndexOf(targetPath);

                            int finalInsertIdx = (newUIIndex > oldUIIndex) ? newTgtIdx + 1 : newTgtIdx;

                            _imageFiles.Insert(finalInsertIdx, sourcePath);
                        }
                        if (_currentTabItem != null)
                        {
                            _currentImageIndex = _imageFiles.IndexOf(_currentTabItem.FilePath);
                        }
                        UpdateWindowTitle();
                    }
                }
                e.Handled = true;
            }
        }

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
            IDataObject data = Clipboard.GetDataObject(); 
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
            else if (data.GetDataPresent(DataFormats.Bitmap))
            {
                try
                {
                    // 获取位图数据
                    BitmapSource source = Clipboard.GetImage();
                    if (source != null)
                    {
                        var newTab = CreateNewUntitledTab();

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
                        newTab.IsDirty = true; 
                        UpdateTabThumbnail(fullCachePath); 
                        FileTabs.Insert(uiInsertIndex, newTab);
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
                UpdateImageBarSliderState();
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

        private void CopyTabToClipboard(FileTabItem tab)
        {
            var dataObject = new DataObject();
            bool hasContent = false;
            BitmapSource heavyBitmap = null;

            try
            {
                if (!string.IsNullOrEmpty(tab.FilePath) && File.Exists(tab.FilePath) && !tab.IsDirty)
                {
                    var fileList = new System.Collections.Specialized.StringCollection();
                    fileList.Add(tab.FilePath);
                    dataObject.SetFileDropList(fileList);
                    hasContent = true;

                }
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
                dataObject = null;
                heavyBitmap = null;

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