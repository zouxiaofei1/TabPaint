
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TabPaint.Windows;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private async void SaveAll(bool isDoubleClick)
        {
            int successCount = 0;
            var processedPaths = new HashSet<string>();

            // 1. 统计脏数据
            var dirtyTabs = FileTabs.Where(t => t.IsDirty).ToList();
            if (IsVirtualPath(_currentTabItem?.FilePath) && !isDoubleClick)
            {
                // 如果当前未命名且不是双击，统计时排除它以保持逻辑一致
                dirtyTabs = dirtyTabs.Where(t => !IsVirtualPath(t.FilePath)).ToList();
            }

            var offlineDirtyItems = new List<SessionTabInfo>();
            if (_offScreenBackupInfos != null)
            {
                var visiblePaths = new HashSet<string>(FileTabs.Select(t => t.FilePath).Where(p => !string.IsNullOrEmpty(p)));
                offlineDirtyItems = _offScreenBackupInfos.Values
                    .Where(info => info.IsDirty && !info.IsNew && !visiblePaths.Contains(info.OriginalPath))
                    .Where(info => !string.IsNullOrEmpty(info.BackupPath) && File.Exists(info.BackupPath))
                    .ToList();
            }

            int totalToSave = dirtyTabs.Count + offlineDirtyItems.Count;
            if (totalToSave == 0) return;

            bool showProgress = totalToSave > 20;
            if (showProgress)
            {
                TaskProgressPopup.SetIcon(AppConsts.PathTaskProgress);
                TaskProgressPopup.UpdateProgress(0, LocalizationManager.GetString("L_Progress_Saving"));
                TaskProgressPopup.Visibility = Visibility.Visible;
            }

            int currentProcessed = 0;

            // 2. 处理当前内存中的标签页
            foreach (var tab in dirtyTabs)
            {
                if (!string.IsNullOrEmpty(tab.FilePath)) processedPaths.Add(tab.FilePath);

                if (SaveSingleTab(tab)) successCount++;

                currentProcessed++;
                if (showProgress)
                {
                    TaskProgressPopup.UpdateProgress((double)currentProcessed / totalToSave * 100, LocalizationManager.GetString("L_Progress_Saving"));
                    await Task.Delay(1); // 允许 UI 刷新
                }
            }

            // 3. 处理离线脏文件
            foreach (var info in offlineDirtyItems)
            {
                if (processedPaths.Contains(info.OriginalPath)) continue;

                try
                {
                    File.Copy(info.BackupPath, info.OriginalPath, true);
                    info.IsDirty = false;
                    successCount++;

                    // 清理已同步的备份
                    try { File.Delete(info.BackupPath); info.BackupPath = null; } catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SaveAll offline failed for {info.OriginalPath}: {ex.Message}");
                }

                currentProcessed++;
                if (showProgress)
                {
                    TaskProgressPopup.UpdateProgress((double)currentProcessed / totalToSave * 100, LocalizationManager.GetString("L_Progress_Saving"));
                    await Task.Delay(1); // 允许 UI 刷新
                }
            }

            if (showProgress)
            {
                TaskProgressPopup.Finish();
            }

            System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { SaveSession(); }, System.Windows.Threading.DispatcherPriority.Background);

            if (successCount > 0)
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_SavedCount_Format"), successCount));

            UpdateWindowTitle();
        }
        private void OnSaveAllDoubleClick(object sender, RoutedEventArgs e) { SaveAll(true); }
        private void OnSaveAllClick(object sender, RoutedEventArgs e) { SaveAll(false); }
        private void OnClearUneditedClick(object sender, RoutedEventArgs e)
        {
            var dirtyTabs = FileTabs.Where(t => t.IsDirty).ToList();
            var dirtyPaths = new HashSet<string>(dirtyTabs.Select(t => t.FilePath));

            var filesToRemove = _imageFiles.Where(f => !dirtyPaths.Contains(f)).ToList();

            foreach (var path in filesToRemove)
            {
                if (!IsVirtualPath(path)) _explicitlyClosedFiles.Add(path);
            }

            _imageFiles = _imageFiles.Where(f => dirtyPaths.Contains(f)).ToList();
            ImageFilesCount = _imageFiles.Count;

            var originalCurrent = _currentTabItem;
            bool currentWasRemoved = originalCurrent != null && !originalCurrent.IsDirty;

            for (int i = FileTabs.Count - 1; i >= 0; i--)
            {
                var tab = FileTabs[i];
                if (!tab.IsDirty)
                {
                    if (!string.IsNullOrEmpty(tab.BackupPath) && File.Exists(tab.BackupPath))
                    {
                        try { File.Delete(tab.BackupPath); } catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
                    }
                    FileTabs.RemoveAt(i);
                }
            }
            if (FileTabs.Count == 0) ResetToNewCanvas();
            else
            {
                if (currentWasRemoved)
                {
                    var nextTab = FileTabs.Last();
                    SwitchToTab(nextTab);
                }
                else
                {
                    if (_currentTabItem != null) _currentImageIndex = _imageFiles.IndexOf(_currentTabItem.FilePath);
                }
            }

            UpdateImageBarSliderState();
            ShowToast(string.Format(LocalizationManager.GetString("L_Toast_CleanedCount_Format"), filesToRemove.Count));
        }
        private async Task DiscardAllInternalAsync(bool skipConfirmation = false)
        {
            if (!skipConfirmation && !SettingsManager.Instance.Current.SkipResetConfirmation)
            {
                var result = FluentMessageBox.Show(
                  LocalizationManager.GetString("L_Msg_ResetWorkspace_Content"),
                  LocalizationManager.GetString("L_Msg_ResetWorkspace_Title"),
                  MessageBoxButton.YesNo
                 );

                if (result != MessageBoxResult.Yes) return;
            }

            _autoSaveTimer.Stop();
            _activeSaveTasks.Clear(); // 清空任务字典
            FileTabs.Clear();
            if (_surface != null && _surface.Bitmap != null)
            {
                try
                {
                    _surface.Bitmap.Lock();
                    int w = _surface.Bitmap.PixelWidth;
                    int h = _surface.Bitmap.PixelHeight;
                    int stride = _surface.Bitmap.BackBufferStride;
                    // 全白填充 (假设是 Bgra32)
                    unsafe
                    {
                        byte* pPixels = (byte*)_surface.Bitmap.BackBuffer;
                        int len = h * stride;
                        for (int i = 0; i < len; i++)
                        {
                            pPixels[i] = 255;
                        }
                    }
                    _surface.Bitmap.AddDirtyRect(new Int32Rect(0, 0, w, h));
                    _surface.Bitmap.Unlock();
                }
                catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
            }
            if (_currentTabItem != null)
            {
                _currentTabItem.IsDirty = false;
                _currentTabItem.IsNew = false;
                _currentTabItem.BackupPath = null; // 切断缓存路径关联
            }
            CloseTab(_currentTabItem);
            try
            {
                _router.CleanUpSelectionandShape();

                // 1. 删除 Session.json
                if (File.Exists(_sessionPath))
                {
                    File.Delete(_sessionPath);
                }

                if (Directory.Exists(_cacheDir))
                {
                    string[] cacheFiles = Directory.GetFiles(_cacheDir);
                    foreach (string file in cacheFiles)
                    {
                        try
                        {
                            if (file.EndsWith(".onnx")) continue;
                            File.Delete(file);
                        }
                        catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_CleanupFailed_Prefix"), ex.Message),ex);

            }
            var originalCurrentTab = _currentTabItem;
            bool currentTabAffected = false;
            for (int i = FileTabs.Count - 1; i >= 0; i--)
            {
                var tab = FileTabs[i];
                tab.BackupPath = null;

                if (tab.IsNew)
                {
                    // A. 对于新建的文件：直接移除
                    if (tab == originalCurrentTab) currentTabAffected = true;

                    if (_imageFiles.Contains(tab.FilePath))
                    {
                        _imageFiles.Remove(tab.FilePath);
                        ImageFilesCount = _imageFiles.Count;
                    }

                    FileTabs.RemoveAt(i);
                }
                else if (tab.IsDirty)
                {
                    tab.IsDirty = false;
                    if (tab == originalCurrentTab) currentTabAffected = true;
                    tab.IsLoading = true;
                    await tab.LoadThumbnailAsync(100, 60);
                    tab.IsLoading = false;
                }
            }

            if (FileTabs.Count == 0)
            {
                _imageFiles.Clear();
                ImageFilesCount = _imageFiles.Count;
                ResetToNewCanvas();
            }
            else if (currentTabAffected)
            {
                if (!FileTabs.Contains(originalCurrentTab))
                {
                    var firstTab = FileTabs.FirstOrDefault();
                    if (firstTab != null)
                    {
                        foreach (var t in FileTabs) t.IsSelected = false;
                        firstTab.IsSelected = true;
                        SwitchToTab(firstTab);
                    }
                }
                else
                {
                    if (_currentTabItem != null)
                    {
                        await OpenImageAndTabs(_currentTabItem.FilePath);
                        ResetDirtyTracker();
                    }
                }
            }
            else ResetDirtyTracker();
            GC.Collect();
            UpdateImageBarSliderState();
            if (File.Exists(_workingPath)) await SwitchWorkspaceToNewFile(_workingPath);
            if (!string.IsNullOrEmpty(_workingPath) && Directory.Exists(_workingPath))
            {
                _workingPath = FindFirstImageInDirectory(_workingPath); await SwitchWorkspaceToNewFile(_workingPath);
            }
        }

        private async void OnDiscardAllClick(object sender, RoutedEventArgs e)
        {
            await DiscardAllInternalAsync();
        }

        private async void OnDiscardImageClick(object sender, RoutedEventArgs e)
        {
            if (_currentTabItem == null) return;

            if (!SettingsManager.Instance.Current.SkipResetConfirmation)
            {
                var result = FluentMessageBox.Show(
                  LocalizationManager.GetString("L_Msg_ResetImage_Content") ?? "Are you sure you want to discard changes for this image?",
                  LocalizationManager.GetString("L_Msg_ResetWorkspace_Title"),
                  MessageBoxButton.YesNo
                 );

                if (result != MessageBoxResult.Yes) return;
            }

            if (_currentTabItem.IsNew)
            {
                // 如果是新创建但未保存的文件，重置为空白画布
                _currentTabItem.IsDirty = false;
                _currentTabItem.BackupPath = null;
                _currentTabItem.UndoStack?.Clear();
                _currentTabItem.RedoStack?.Clear();
                _currentTabItem.SavedUndoPoint = 0;
                _currentTabItem.CanvasVersion = 0;
                _currentTabItem.LastBackedUpVersion = -1;

                if (_surface != null && _surface.Bitmap != null)
                {
                    try
                    {
                        _surface.Bitmap.Lock();
                        int w = _surface.Bitmap.PixelWidth;
                        int h = _surface.Bitmap.PixelHeight;
                        int stride = _surface.Bitmap.BackBufferStride;
                        unsafe
                        {
                            byte* pPixels = (byte*)_surface.Bitmap.BackBuffer;
                            int len = h * stride;
                            for (int i = 0; i < len; i++) pPixels[i] = 255;
                        }
                        _surface.Bitmap.AddDirtyRect(new Int32Rect(0, 0, w, h));
                        _surface.Bitmap.Unlock();
                    }
                    catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
                }

                _currentTabItem.Thumbnail = GenerateBlankThumbnail();
                ResetDirtyTracker();
                UpdateWindowTitle();
                ShowToast(LocalizationManager.GetString("L_Toast_ImageReset"));
            }
            else if (_currentTabItem.IsDirty)
            {
                // 如果是已存在但有更改的文件，重新加载
                _currentTabItem.IsDirty = false;
                _currentTabItem.BackupPath = null;
                _currentTabItem.IsLoading = true;
                
                // 清理选区和形状
                _router.CleanUpSelectionandShape();

                // 重新从原始路径加载
                await OpenImageAndTabs(_currentTabItem.FilePath);
                
                ResetDirtyTracker();
                UpdateTabThumbnail(_currentTabItem);
                _currentTabItem.IsLoading = false;
                
                UpdateWindowTitle();
                ShowToast(LocalizationManager.GetString("L_Toast_ImageReset"));
            }
            
            GC.Collect();
            UpdateImageBarSliderState();
        }
    }
}
