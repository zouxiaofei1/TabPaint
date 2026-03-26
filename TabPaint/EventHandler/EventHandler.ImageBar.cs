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
            if (e.OriginalSource is FrameworkElement element && element.DataContext is FileTabItem clickedItem)  SwitchToTab(clickedItem);
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

            // 如果上面失败了，且原路径是真实存在的，作为兜底返回原路径
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
                // 如果拖拽的是当前活跃标签，同步最新的状态到对象中 (包含像素和撤销栈)
                if (_mouseDownTabItem == _currentTabItem && _undo != null)
                {
                    // 1. 同步撤销栈
                    _mouseDownTabItem.UndoStack = new List<UndoAction>(_undo.GetUndoStack());
                    _mouseDownTabItem.RedoStack = new List<UndoAction>(_undo.GetRedoStack());
                    _mouseDownTabItem.SavedUndoPoint = _savedUndoPoint;
                    _mouseDownTabItem.CanvasVersion = _currentCanvasVersion;
                    _mouseDownTabItem.LastBackedUpVersion = _lastBackedUpVersion;

                    // 2. 如果有改动，同步当前画布快照
                    if (_mouseDownTabItem.IsDirty || _mouseDownTabItem.IsNew)
                    {
                        var bmp = GetCurrentCanvasSnapshotSafe();
                        if (bmp != null)
                        {
                            // 优先存入内存快照，用于跨窗口瞬间传递
                            _mouseDownTabItem.MemorySnapshot = bmp;

                            // 后台异步备份到磁盘作为兜底
                            if (string.IsNullOrEmpty(_mouseDownTabItem.BackupPath))
                            {
                                string cacheFileName = $"{_mouseDownTabItem.Id}.png";
                                _mouseDownTabItem.BackupPath = System.IO.Path.Combine(_cacheDir, cacheFileName);
                            }
                            _ = Task.Run(() => {
                                try { SaveBitmapToPng(bmp, _mouseDownTabItem.BackupPath); } catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
                            });
                            
                            _mouseDownTabItem.LastBackupTime = DateTime.Now;
                            _lastBackedUpVersion = _currentCanvasVersion; // 标记已同步
                        }
                    }
                }

                var dataObject = new System.Windows.DataObject();

                dataObject.SetData("TabPaintReorderItem", _mouseDownTabItem);
                dataObject.SetData("TabPaintInternalDrag", true);
                dataObject.SetData("TabPaintSourceWindow", this);

                string externalDragPath = PrepareDragFilePath(_mouseDownTabItem);

                if (!string.IsNullOrEmpty(externalDragPath) && System.IO.File.Exists(externalDragPath))
                {
                    var fileList = new System.Collections.Specialized.StringCollection();
                    fileList.Add(externalDragPath);
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
            if (e.Data.GetDataPresent("TabPaintReorderItem"))
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
            if (e.Data.GetDataPresent("TabPaintReorderItem"))
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

            if (e.Data.GetDataPresent("TabPaintReorderItem"))
            {
                var sourceTab = e.Data.GetData("TabPaintReorderItem") as FileTabItem;
                var sourceWindow = e.Data.GetData("TabPaintSourceWindow") as MainWindow;

                var targetGrid = sender as Grid;
                var targetTab = targetGrid?.DataContext as FileTabItem;

                if (sourceTab != null)
                {
                    int oldUIIndex = FileTabs.IndexOf(sourceTab);
                    int targetUIIndex = targetTab != null ? FileTabs.IndexOf(targetTab) : FileTabs.Count;

                    // 跨窗口处理
                    if (oldUIIndex == -1 && sourceWindow != null)
                    {
                        sourceWindow.CloseTab(sourceTab, true);
                        var existingTab = FileTabs.FirstOrDefault(t => t.Id == sourceTab.Id) ??
                                         FileTabs.FirstOrDefault(t => !IsVirtualPath(t.FilePath) && string.Equals(t.FilePath, sourceTab.FilePath, StringComparison.OrdinalIgnoreCase));
                        if (existingTab != null)
                        {
                            if (sourceTab.MemorySnapshot != null) existingTab.MemorySnapshot = sourceTab.MemorySnapshot;
                            existingTab.UndoStack = sourceTab.UndoStack;
                            existingTab.RedoStack = sourceTab.RedoStack;
                            existingTab.IsDirty = sourceTab.IsDirty;

                            await OpenImageAndTabs(existingTab.FilePath, nobackup: true);
                        }
                        else
                        {
                            int newUIIndex = targetUIIndex;
                            if (targetGrid != null)
                            {
                                Point p = e.GetPosition(targetGrid);
                                if (p.X >= targetGrid.ActualWidth / 2) newUIIndex++;
                            }

                            if (newUIIndex < 0) newUIIndex = 0;
                            if (newUIIndex > FileTabs.Count) newUIIndex = FileTabs.Count;

                            FileTabs.Insert(newUIIndex, sourceTab);
                            if (!string.IsNullOrEmpty(sourceTab.FilePath))
                            {
                                int fileInsertIdx = 0;
                                if (newUIIndex > 0)
                                {
                                    var prevTab = FileTabs[newUIIndex - 1];
                                    fileInsertIdx = _imageFiles.IndexOf(prevTab.FilePath) + 1;
                                }
                                if (fileInsertIdx < 0) fileInsertIdx = _imageFiles.Count;
                                _imageFiles.Insert(fileInsertIdx, sourceTab.FilePath);
                                ImageFilesCount = _imageFiles.Count;
                            }

                            // 4. 切换并激活
                            await OpenImageAndTabs(sourceTab.FilePath, nobackup: true);
                        }
                        UpdateImageBarSliderState();
                    }
                    else if (targetTab != null && sourceTab != targetTab)
                    {
                        Point p = e.GetPosition(targetGrid);
                        bool insertAfter = p.X >= targetGrid.ActualWidth / 2;

                        int newUIIndex = targetUIIndex;

                        if (insertAfter) newUIIndex++;
                        if (oldUIIndex < newUIIndex) newUIIndex--;
                        if (newUIIndex < 0) newUIIndex = 0;
                        if (newUIIndex >= FileTabs.Count) newUIIndex = FileTabs.Count - 1;
                        if (oldUIIndex != newUIIndex)
                        {
                            FileTabs.Move(oldUIIndex, newUIIndex);
                            string sourcePath = sourceTab.FilePath;
                            if (!string.IsNullOrEmpty(sourcePath) && _imageFiles.Contains(sourcePath))
                            {
                                _imageFiles.Remove(sourcePath);
                                int newTgtIdx = -1;
                                if (newUIIndex > 0)
                                {
                                    var prevTab = FileTabs[newUIIndex - 1];
                                    newTgtIdx = _imageFiles.IndexOf(prevTab.FilePath);
                                    // 插在它后面
                                    if (newTgtIdx >= 0) _imageFiles.Insert(newTgtIdx + 1, sourcePath);
                                    else _imageFiles.Add(sourcePath); // Fallback
                                }
                                else _imageFiles.Insert(0, sourcePath);
                            }

                            if (_currentTabItem != null)  _currentImageIndex = _imageFiles.IndexOf(_currentTabItem.FilePath);
                            UpdateWindowTitle();
                        }
                    }
                }
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }
      
        private void OnTabDeleteClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is FileTabItem tab)
            {
                CloseTab(tab);
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