using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using static TabPaint.MainWindow;
namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void HandleDeleteFileAction()
        {
            if (_currentTabItem == null) return;

            var tab = _currentTabItem;

            _lastDeletedTabForUndo = tab;
            _lastDeletedTabIndex = FileTabs.IndexOf(tab);

            _pendingDeletionTabs.Add(tab);
            FileTabs.Remove(tab);
            _imageFiles.Remove(tab.FilePath);
            ImageFilesCount = _imageFiles.Count;

            if (FileTabs.Count == 0)
            {
                ResetToNewCanvas();
            }
            else
            {
                int nextIndex = Math.Min(_lastDeletedTabIndex, FileTabs.Count - 1);
                SwitchToTab(FileTabs[nextIndex]);
            }
            _deleteCommitTimer.Stop();
            _deleteCommitTimer.Start();

            ShowToast("L_Toast_Deleted");
        }
        private void CommitPendingDeletions()
        {
            _deleteCommitTimer.Stop();

            foreach (var tab in _pendingDeletionTabs)
            {
                try
                {
                    if (!IsVirtualPath(tab.FilePath) && File.Exists(tab.FilePath))   // 只有真实存在的文件才进回收站
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                            tab.FilePath,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    }
                    // 如果有备份文件，也顺便清理
                    if (!string.IsNullOrEmpty(tab.BackupPath) && File.Exists(tab.BackupPath))
                    {
                        File.Delete(tab.BackupPath);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"删除提交失败: {ex.Message}");
                }
            }

            // 清空列表和撤销引用
            _pendingDeletionTabs.Clear();
            _lastDeletedTabForUndo = null;
        }

        private void RestoreLastDeletedTab()
        {
            if (_pendingDeletionTabs.Count == 0) return;

            // 取出最后一个删除的 Tab
            var tabToRestore = _pendingDeletionTabs.Last();
            _pendingDeletionTabs.Remove(tabToRestore);
        
            // 停止计时器 (如果列表空了)
            if (_pendingDeletionTabs.Count == 0) _deleteCommitTimer.Stop();

            // 恢复到 UI 列表
            if (_lastDeletedTabIndex >= 0 && _lastDeletedTabIndex <= FileTabs.Count)
                FileTabs.Insert(_lastDeletedTabIndex, tabToRestore);
            else
                FileTabs.Add(tabToRestore);

            // 恢复到路径列表
            if (!_imageFiles.Contains(tabToRestore.FilePath))
            {
                if (_lastDeletedTabIndex < _imageFiles.Count)
                    _imageFiles.Insert(_lastDeletedTabIndex, tabToRestore.FilePath);
                else
                    _imageFiles.Add(tabToRestore.FilePath);
                ImageFilesCount = _imageFiles.Count;
            }

            // 马上切回这个 Tab
            SwitchToTab(tabToRestore);
            ShowToast("L_Toast_UndoDelete");
        }
    }
}