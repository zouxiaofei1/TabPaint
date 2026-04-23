//
//EventHandler.ImageBar.cs
//标签栏相关的事件处理，主要是拖动逻辑
//
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TabPaint.Services;
using TabPaint.Windows;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
     
        private void OnFileTabCloseClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (e.OriginalSource is FrameworkElement element && element.Tag is FileTabItem clickedItem) CloseTab(clickedItem);
        }
        private async void OnFileTabClick(object sender, RoutedEventArgs e)
        {
            if (!(e.OriginalSource is FrameworkElement element) || !(element.DataContext is FileTabItem clickedItem)) return;

            var modifiers = Keyboard.Modifiers;

            if (modifiers == ModifierKeys.Shift)
            {
                var anchor = _selectionAnchorTab ?? _currentTabItem;
                if (anchor != null && FileTabs.Contains(anchor))
                {
                    int start = FileTabs.IndexOf(anchor);
                    int end = FileTabs.IndexOf(clickedItem);

                    if (start > end) { int tmp = start; start = end; end = tmp; }

                    foreach (var tab in FileTabs) tab.IsMultiSelected = false;
                    for (int i = start; i <= end; i++)
                    {
                        FileTabs[i].IsMultiSelected = true;
                    }
                }
                else
                {
                    clickedItem.IsMultiSelected = true;
                    _selectionAnchorTab = clickedItem;
                }
            }
            else if (modifiers == ModifierKeys.Control)
            {
                clickedItem.IsMultiSelected = !clickedItem.IsMultiSelected;
                _selectionAnchorTab = clickedItem;
            }
            else
            {
                foreach (var tab in FileTabs) tab.IsMultiSelected = false;
                _selectionAnchorTab = clickedItem;
                SwitchToTab(clickedItem);
            }
        }

        private FileTabItem CreateNewUntitledTab()
        {
            var newTab = new FileTabItem(null)
            {
                IsNew = true,
                IsDirty = false,
                UntitledNumber = GetNextAvailableUntitledNumber(),
                Thumbnail = GenerateBlankThumbnail()
            };
            return newTab;
        }

        private void OnPrependTabClick(object sender, RoutedEventArgs e)    {   CreateNewTab(TabInsertPosition.AtStart, false);  }

      
        private void OnNewTabClick(object sender, RoutedEventArgs e)
        {
            CreateNewTab(TabInsertPosition.AtEnd, true); CheckFittoWindow();
        }
        private void OnFileTabPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                if (e.OriginalSource is FrameworkElement element && element.DataContext is FileTabItem clickedItem)

                {
                    CloseTab(clickedItem); // 复用已有的关闭逻辑
                    e.Handled = true; // 阻止事件冒泡，防止触发其他点击行为
                }
                return;
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                _dragStartPoint = e.GetPosition(null);
                var button = sender as System.Windows.Controls.Button;
                _mouseDownTabItem = button?.DataContext as FileTabItem;
            }
        }
        private string PrepareDragFilePath(FileTabItem tab)
        {

            if (!tab.IsDirty && !tab.IsNew && !IsVirtualPath(tab.FilePath))return tab.FilePath;
            try
            {
                BitmapSource bitmapToSave = null;

                if (tab == _currentTabItem)
                {
                    bitmapToSave = GetCurrentCanvasSnapshotSafe();
                }
                else
                {
                    // 尝试读取后台备份 (BackupPath)
                    if (!string.IsNullOrEmpty(tab.BackupPath) && File.Exists(tab.BackupPath))
                    {
                        bitmapToSave = LoadBitmapFromFile(tab.BackupPath);
                    }
                    else
                    {
                        if (!IsVirtualPath(tab.FilePath)) return tab.FilePath;
                    }
                }

                if (bitmapToSave != null)
                {
                    string tempFolder = System.IO.Path.Combine(_cacheDir, "DragTemp");
                    if (!Directory.Exists(tempFolder)) Directory.CreateDirectory(tempFolder);

                    string fileName = tab.FileName;
                    if (!fileName.Contains(".")) fileName += ".png";

                    string tempFilePath = System.IO.Path.Combine(tempFolder, fileName);

                    bool isJpeg = fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                  fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);
                    if (isJpeg) bitmapToSave = ConvertToWhiteBackground(bitmapToSave);

                    using (var fs = new FileStream(tempFilePath, FileMode.Create))
                    {
                        BitmapEncoder encoder;
                        if (isJpeg)
                        {
                            var jpgEncoder = new JpegBitmapEncoder();
                            jpgEncoder.QualityLevel = 90; // 建议设置高质量
                            encoder = jpgEncoder;
                        }
                        else
                        {
                            encoder = new PngBitmapEncoder();
                        }

                        encoder.Frames.Add(BitmapFrame.Create(bitmapToSave));
                        encoder.Save(fs);
                    }
                    return tempFilePath;
                }
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_PrepareDragFailed_Prefix"), ex.Message),ex);
            }
            if (!IsVirtualPath(tab.FilePath) && File.Exists(tab.FilePath))
            {
                return tab.FilePath;
            }

            return null;
        }
        private void OnFileTabPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (_mouseDownTabItem == null) return;

            Vector diff = _dragStartPoint - e.GetPosition(null);

            if (Math.Abs(diff.X) < _dragThreshold && Math.Abs(diff.Y) < _dragThreshold) return;
            try
            {
                // 确定参与拖拽的标签集合
                var draggingTabs = new List<FileTabItem>();
                if (_mouseDownTabItem.IsMultiSelected)
                {
                    draggingTabs.AddRange(FileTabs.Where(t => t.IsMultiSelected));
                }
                if (!draggingTabs.Contains(_mouseDownTabItem))
                {
                    draggingTabs.Add(_mouseDownTabItem);
                }

                foreach (var tab in draggingTabs)
                {
                    // 如果拖拽的是当前活跃标签，同步最新的状态到对象中 (包含像素和撤销栈)
                    if (tab == _currentTabItem && _undo != null)
                    {
                        // 1. 同步撤销栈
                        tab.UndoStack = new List<UndoAction>(_undo.GetUndoStack());
                        tab.RedoStack = new List<UndoAction>(_undo.GetRedoStack());
                        tab.SavedUndoPoint = _savedUndoPoint;
                        tab.CanvasVersion = _currentCanvasVersion;
                        tab.LastBackedUpVersion = _lastBackedUpVersion;

                        // 2. 如果有改动，同步当前画布快照
                        if (tab.IsDirty || tab.IsNew)
                        {
                            var bmp = GetCurrentCanvasSnapshotSafe();
                            if (bmp != null)
                            {
                                // 优先存入内存快照，用于跨窗口瞬间传递
                                tab.MemorySnapshot = bmp;

                                if (string.IsNullOrEmpty(tab.BackupPath))
                                {
                                    string cacheFileName = $"{tab.Id}.png";
                                    tab.BackupPath = System.IO.Path.Combine(_cacheDir, cacheFileName);
                                }
                                _ = Task.Run(() =>
                                {
                                    try { SaveBitmapToPng(bmp, tab.BackupPath); } catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
                                });

                                tab.LastBackupTime = DateTime.Now;
                                _lastBackedUpVersion = _currentCanvasVersion; // 标记已同步
                            }
                        }
                    }
                }

                var dataObject = new System.Windows.DataObject();

                dataObject.SetData("TabPaintReorderItem", _mouseDownTabItem);
                dataObject.SetData("TabPaintReorderItems", draggingTabs);
                dataObject.SetData("TabPaintInternalDrag", true);
                dataObject.SetData("TabPaintSourceWindow", this);

                var fileList = new System.Collections.Specialized.StringCollection();
                foreach (var tab in draggingTabs)
                {
                    string path = PrepareDragFilePath(tab);
                    if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                    {
                        fileList.Add(path);
                    }
                }

                if (fileList.Count > 0)
                {
                    dataObject.SetFileDropList(fileList);
                }

                // 初始化悬浮窗
                if (_dropZone == null)
                {
                    _dropZone = new UIHandlers.DropZoneWindow();
                    _dropZone.TabDropped += OnDropZoneTabDropped;
                }
                _dropZone.ShowAtBottom();

                DragDrop.DoDragDrop((DependencyObject)sender, dataObject, DragDropEffects.Copy | DragDropEffects.Move);

                // 拖拽结束，隐藏悬浮窗
                if (_dropZone != null) _dropZone.Hide();

                e.Handled = true;
                _mouseDownTabItem = null;
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_DragStartFailed_Prefix"), ex.Message),ex);
                if (_dropZone != null) _dropZone.Hide();
            }
        }

        private async void OnDropZoneTabDropped(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TabPaintReorderItems"))
            {
                var tabs = e.Data.GetData("TabPaintReorderItems") as List<FileTabItem>;
                if (tabs != null && tabs.Count > 0)
                {
                    // 迁移所有选中的标签
                    // 第一个标签创建新窗口，后续标签加入该窗口
                    var firstTab = tabs[0];
                    if (firstTab == _currentTabItem)
                    {
                        firstTab.UndoStack = new List<UndoAction>(_undo.GetUndoStack());
                        firstTab.RedoStack = new List<UndoAction>(_undo.GetRedoStack());
                        firstTab.SavedUndoPoint = _savedUndoPoint;
                        firstTab.MemorySnapshot = GetCurrentCanvasSnapshotSafe();
                    }

                    MainWindow newWindow = new MainWindow(firstTab.FilePath, !IsVirtualPath(firstTab.FilePath), firstTab, loadSession: false);
                    newWindow.Show();
                    CloseTab(firstTab, slient: true, isMoving: true);

                    for (int i = 1; i < tabs.Count; i++)
                    {
                        var tab = tabs[i];
                    }
                }
            }
            else if (e.Data.GetDataPresent("TabPaintReorderItem"))
            {
                var tab = e.Data.GetData("TabPaintReorderItem") as FileTabItem;
                if (tab != null)
                {
                    await TransferTabToNewWindow(tab);
                }
            }
            else if (e.Data.GetDataPresent("TabPaintSelectionDrag"))
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0)
                    {
                        await CreateNewWindowFromSelection(files[0]);
                    }
                }
            }
        }

        private async Task CreateNewWindowFromSelection(string filePath)
        {
            try
            {
                // 创建新窗口，并将临时文件路径传递过去
                // fileExists 参数应设为 true，因为临时文件已经创建
                MainWindow newWindow = new MainWindow(filePath, fileExists: true, initialTab: null, loadSession: false);
                newWindow.Show();
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_NewWindowFromSelectionFailed"), ex.Message), ex);
            }
        }

        private async Task TransferTabToNewWindow(FileTabItem tab)
        {
         
            if (tab == _currentTabItem)   // 如果是当前标签，先同步最新状态
            {
                tab.UndoStack = new List<UndoAction>(_undo.GetUndoStack());
                tab.RedoStack = new List<UndoAction>(_undo.GetRedoStack());
                tab.SavedUndoPoint = _savedUndoPoint;
                tab.MemorySnapshot = GetCurrentCanvasSnapshotSafe();
            }
            if ((tab.IsDirty || tab.IsNew) && tab.MemorySnapshot == null)
            {
                if (!string.IsNullOrEmpty(tab.BackupPath) && File.Exists(tab.BackupPath)) { }
                else  tab.MemorySnapshot = GetHighResImageForTab(tab);
            }
            try
            {
                MainWindow newWindow = new MainWindow(tab.FilePath, !IsVirtualPath(tab.FilePath), tab, loadSession: false);
                newWindow.Show();
                CloseTab(tab, slient: true, isMoving: true); // 强制关闭且标记为移动，不提示保存且保留备份文件
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_NewWindowFailed"), ex.Message), ex);

            }
        }
        private void OnFileTabLeave(object sender, DragEventArgs e)
        {
            ClearDragFeedback(sender);
        }
        private void OnFileTabReorderDragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TabPaintReorderItem") || e.Data.GetDataPresent("TabPaintReorderItems"))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;

                if (sender is Grid grid)
                {
                    // 获取鼠标在当前 Item 内的相对坐标
                    Point p = e.GetPosition(grid);
                    double width = grid.ActualWidth;

                    var insLine = FindVisualChild<Border>(grid, "InsertLine");

                    if (insLine == null) return;
                    insLine.Visibility = Visibility.Visible;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        private T FindVisualChild<T>(DependencyObject parent, string childName) where T : FrameworkElement
        {
            if (parent == null) return null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                // 如果是要找的类型且名字匹配 (如果传了名字)
                if (child is T tChild && (string.IsNullOrEmpty(childName) || tChild.Name == childName))
                {
                    return tChild;
                }
                var result = FindVisualChild<T>(child, childName);
                if (result != null) return result;
            }
            return null;
        }
        private void ClearDragFeedback(object sender)
        {
            if (sender is Grid grid)
            {
                var leftLine = FindVisualChild<Border>(grid, "InsertLine");
                if (leftLine != null) leftLine.Visibility = Visibility.Collapsed;
            }
        }
        private async void OnFileTabDrop(object sender, System.Windows.DragEventArgs e)
        {
            ClearDragFeedback(sender);

            bool hasMulti = e.Data.GetDataPresent("TabPaintReorderItems");
            bool hasSingle = e.Data.GetDataPresent("TabPaintReorderItem");

            if (hasMulti || hasSingle)
            {
                var sourceTabs = hasMulti
                    ? e.Data.GetData("TabPaintReorderItems") as List<FileTabItem>
                    : new List<FileTabItem> { e.Data.GetData("TabPaintReorderItem") as FileTabItem };

                if (sourceTabs == null || sourceTabs.Count == 0) return;

                var sourceWindow = e.Data.GetData("TabPaintSourceWindow") as MainWindow;
                var targetGrid = sender as Grid;
                var targetTab = targetGrid?.DataContext as FileTabItem;

                // 确定目标位置索引
                int targetUIIndex = targetTab != null ? FileTabs.IndexOf(targetTab) : FileTabs.Count;
                if (targetGrid != null)
                {
                    Point p = e.GetPosition(targetGrid);
                    if (p.X >= targetGrid.ActualWidth / 2) targetUIIndex++;
                }
                if (targetUIIndex < 0) targetUIIndex = 0;
                if (targetUIIndex > FileTabs.Count) targetUIIndex = FileTabs.Count;

                if (sourceWindow != null && sourceWindow != this) // 跨窗口处理
                {
                    foreach (var sourceTab in sourceTabs)
                    {
                        sourceWindow.CloseTab(sourceTab, true);
                        var existingTab = FileTabs.FirstOrDefault(t => t.Id == sourceTab.Id);
                        if (existingTab != null)
                        {
                            if (sourceTab.MemorySnapshot != null) existingTab.MemorySnapshot = sourceTab.MemorySnapshot;
                            existingTab.UndoStack = sourceTab.UndoStack;
                            existingTab.RedoStack = sourceTab.RedoStack;
                            existingTab.IsDirty = sourceTab.IsDirty;
                        }
                        else
                        {
                            if (targetUIIndex > FileTabs.Count) targetUIIndex = FileTabs.Count;
                            FileTabs.Insert(targetUIIndex, sourceTab);
                            if (!string.IsNullOrEmpty(sourceTab.FilePath))
                            {
                                int fileInsertIdx = 0;
                                if (targetUIIndex > 0)
                                {
                                    var prevTab = FileTabs[targetUIIndex - 1];
                                    fileInsertIdx = _imageFiles.FindIndex(f => string.Equals(f, prevTab.FilePath, StringComparison.OrdinalIgnoreCase)) + 1;
                                }
                                if (fileInsertIdx < 0) fileInsertIdx = _imageFiles.Count;
                                _imageFiles.Insert(fileInsertIdx, sourceTab.FilePath);
                                ImageFilesCount = _imageFiles.Count;
                            }
                            targetUIIndex++;
                        }
                    }
                    if (sourceTabs.Count > 0) await OpenImageAndTabs(sourceTabs.Last().FilePath, nobackup: true);
                    UpdateImageBarSliderState();
                }
                else // 本窗口内部排序
                {
                    // 记录目标位置对应的项，以便重插入时定位
                    FileTabItem targetItemAtPosition = targetUIIndex < FileTabs.Count ? FileTabs[targetUIIndex] : null;

                    // 按照原始顺序排序待移动项
                    var tabsToMove = sourceTabs.Where(t => FileTabs.Contains(t)).OrderBy(t => FileTabs.IndexOf(t)).ToList();
                    if (tabsToMove.Count == 0) return;

                    // 批量移除
                    foreach (var t in tabsToMove)
                    {
                        FileTabs.Remove(t);
                        if (!string.IsNullOrEmpty(t.FilePath)) _imageFiles.Remove(t.FilePath);
                    }

                    // 重新确定插入索引
                    int finalInsertIdx = targetItemAtPosition != null ? FileTabs.IndexOf(targetItemAtPosition) : FileTabs.Count;
                    if (finalInsertIdx < 0) finalInsertIdx = FileTabs.Count;

                    // 批量插入
                    for (int i = 0; i < tabsToMove.Count; i++)
                    {
                        var t = tabsToMove[i];
                        FileTabs.Insert(finalInsertIdx + i, t);

                        if (!string.IsNullOrEmpty(t.FilePath))
                        {
                            int fileInsertIdx = 0;
                            if (finalInsertIdx + i > 0)
                            {
                                var prevTab = FileTabs[finalInsertIdx + i - 1];
                                fileInsertIdx = _imageFiles.FindIndex(f => string.Equals(f, prevTab.FilePath, StringComparison.OrdinalIgnoreCase)) + 1;
                            }
                            if (fileInsertIdx < 0) fileInsertIdx = _imageFiles.Count;
                            _imageFiles.Insert(fileInsertIdx, t.FilePath);
                        }
                    }

                    ImageFilesCount = _imageFiles.Count;
                    if (_currentTabItem != null) _currentImageIndex = _imageFiles.FindIndex(f => string.Equals(f, _currentTabItem.FilePath, StringComparison.OrdinalIgnoreCase));
                    UpdateWindowTitle();
                    UpdateImageBarSliderState();
                }

                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }
      
        private async void OnTabRenameClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is FileTabItem tab)
            {
                await RenameTabWithDialog(tab);
            }
        }

        private async Task RenameTabWithDialog(FileTabItem tab)
        {
            if (tab == null) return;

            string oldPath = tab.FilePath;
            bool isVirtual = IsVirtualPath(oldPath);
            string oldFileName = tab.FileName;
            string pureName = oldFileName;
            string suffix = "";

            if (!isVirtual && !string.IsNullOrEmpty(oldPath))
            {
                try
                {
                    pureName = Path.GetFileNameWithoutExtension(oldPath);
                    suffix = Path.GetExtension(oldPath);
                }
                catch { }
            }

            string title = LocalizationManager.GetString("L_Ctx_Rename") ?? "Rename";

            // 准备支持的扩展名列表，将当前的排在第一位
            var supportedExtensions = new List<string> { ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".ico", ".tif", ".tiff" };
            if (!string.IsNullOrEmpty(suffix) && !supportedExtensions.Contains(suffix.ToLower()))
            {
                supportedExtensions.Insert(0, suffix.ToLower());
            }
            else if (!string.IsNullOrEmpty(suffix))
            {
                supportedExtensions.Remove(suffix.ToLower());
                supportedExtensions.Insert(0, suffix.ToLower());
            }

            var (confirmed, newPureName, newSuffix) = UIHandlers.FluentInputDialog.ShowWithSuffix(title, title, pureName, this, supportedExtensions.ToArray());

            if (confirmed && !string.IsNullOrWhiteSpace(newPureName))
            {
                bool nameChanged = newPureName != pureName;
                bool suffixChanged = !string.IsNullOrEmpty(newSuffix) && newSuffix != suffix;

                if (!nameChanged && !suffixChanged) return;

                if (isVirtual)
                {
                    tab.CustomName = newPureName;
                    UpdateWindowTitle();
                }
                else
                {
                    // 物理重命名逻辑
                    try
                    {
                        string directory = Path.GetDirectoryName(oldPath);
                        string finalSuffix = suffixChanged ? newSuffix : suffix;
                        string newFileName = newPureName + finalSuffix;
                        string newPath = Path.Combine(directory, newFileName);

                        if (File.Exists(newPath))
                        {
                            ShowToast(LocalizationManager.GetString("L_Toast_FileExists") ?? "文件已存在");
                            return;
                        }

                        if (suffixChanged)
                        {
                            // 格式转换逻辑
                            BitmapSource bmp = null;
                            if (tab == _currentTabItem) bmp = GetCurrentCanvasSnapshotSafe();
                            else if (!string.IsNullOrEmpty(tab.BackupPath) && File.Exists(tab.BackupPath)) bmp = LoadBitmapFromFile(tab.BackupPath);
                            else bmp = LoadBitmapFromFile(oldPath);

                            if (bmp != null)
                            {
                                using (var fs = new FileStream(newPath, FileMode.Create))
                                {
                                    BitmapEncoder encoder;
                                    string ext = finalSuffix.ToLower();
                                    if (ext == ".jpg" || ext == ".jpeg")
                                    {
                                        bmp = ConvertToWhiteBackground(bmp);
                                        encoder = new JpegBitmapEncoder { QualityLevel = 90 };
                                    }
                                    else if (ext == ".bmp") encoder = new BmpBitmapEncoder();
                                    else if (ext == ".webp") encoder = new PngBitmapEncoder(); // WPF原生不支持写WebP，这里暂且用PNG或提示
                                    else if (ext == ".tif" || ext == ".tiff") encoder = new TiffBitmapEncoder();
                                    else encoder = new PngBitmapEncoder();

                                    encoder.Frames.Add(BitmapFrame.Create(bmp));
                                    encoder.Save(fs);
                                }
                                File.Delete(oldPath);
                            }
                            else
                            {
                                throw new Exception("无法加载图像进行格式转换");
                            }
                        }
                        else
                        {
                            // 仅重命名
                            File.Move(oldPath, newPath);
                        }

                        // 更新标签路径
                        tab.FilePath = newPath;
                        tab.CustomName = null; // 重命名物理文件后，清除自定义名称，让它根据路径显示

                        // 同步主窗口的文件列表
                        int idx = _imageFiles.FindIndex(f => string.Equals(f, oldPath, StringComparison.OrdinalIgnoreCase));
                        if (idx != -1)
                        {
                            _imageFiles[idx] = newPath;
                            if (tab == _currentTabItem)
                            {
                                _currentImageIndex = idx;
                            }
                        }

                        UpdateWindowTitle();
                        ShowToast(LocalizationManager.GetString("L_Toast_RenameSuccess") ?? "重命名成功");
                    }
                    catch (Exception ex)
                    {
                        ShowToast((LocalizationManager.GetString("L_Toast_RenameFailed") ?? "重命名失败: ") + ex.Message, ex);
                    }
                }
            }
        }

        private void OnTabDeleteClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is FileTabItem tab)
            {
                if (tab.IsMultiSelected)
                {
                    var selectedTabs = FileTabs.Where(t => t.IsMultiSelected).ToList();
                    foreach (var t in selectedTabs) CloseTab(t);
                }
                else
                {
                    CloseTab(tab);
                }
            }
        }

        private void OnTabCloseOthersClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem item || item.Tag is not FileTabItem currentTab) return;

            foreach (var tab in FileTabs.ToList())
            {
                if (ReferenceEquals(tab, currentTab)) continue;
                CloseTab(tab,slient:true);
            }
        }
       
        // #endregion
    }
}