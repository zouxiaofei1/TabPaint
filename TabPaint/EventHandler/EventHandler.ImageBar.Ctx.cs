
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
using TabPaint.Windows;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void OnTabFileDeleteClick(object sender, RoutedEventArgs e)  // 这里的“删除”是物理删除文件
        {
            if (sender is MenuItem item && item.Tag is FileTabItem tab)
            {
                if (tab.IsNew && string.IsNullOrEmpty(tab.FilePath))
                {
                    CloseTab(tab); // 如果是没保存的新建画布，直接关掉
                    return;
                }
                if (!SettingsManager.Instance.Current.SkipResetConfirmation)
                {
                    var result = FluentMessageBox.Show(
                     string.Format(LocalizationManager.GetString("L_Msg_DeleteFile_Content"), tab.FileName),
                     LocalizationManager.GetString("L_Msg_DeleteFile_Title"),
                     MessageBoxButton.YesNo
                   );
                    if (result != MessageBoxResult.Yes) return;
                }
                string path = tab.FilePath;

                CloseTab(tab);
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
                catch (Exception ex) { ShowToast(string.Format(LocalizationManager.GetString("L_Toast_DeleteFailed"), ex.Message),ex); }

            }
        }
        private void CopyTabToClipboard(FileTabItem tab)
        {
            var dataObject = new DataObject();
            BitmapSource heavyBitmap = null;

            try
            {
                if (tab.IsDirty || tab.IsNew)
                {
                    // 如果是脏文件，从画布或缓存获取最新状态
                    heavyBitmap = GetCurrentCanvasSnapshotSafe();
                    if (heavyBitmap == null && !string.IsNullOrEmpty(tab.BackupPath))
                    {
                        heavyBitmap = LoadBitmapFromFile(tab.BackupPath);
                    }
                }
                else
                {
                    if (!IsVirtualPath(tab.FilePath))
                    {
                        heavyBitmap = LoadBitmapFromFile(tab.FilePath);
                    }
                }

                if (heavyBitmap != null)
                {
                    dataObject.SetImage(heavyBitmap);
                }
                string pathForClipboard = null;

                if (!tab.IsDirty && !tab.IsNew && !string.IsNullOrEmpty(tab.FilePath) && File.Exists(tab.FilePath)) pathForClipboard = tab.FilePath;
                else if (heavyBitmap != null)
                {
                    string clipDir = System.IO.Path.Combine(_cacheDir, "ClipboardTemp");
                    if (!Directory.Exists(clipDir)) Directory.CreateDirectory(clipDir);
                    string fileName = tab.FileName;
                    if (string.IsNullOrEmpty(System.IO.Path.GetExtension(fileName))) fileName += ".png";
                    string tempPath = System.IO.Path.Combine(clipDir, fileName);

                    // 保存临时文件
                    using (var fs = new FileStream(tempPath, FileMode.Create))
                    {
                        BitmapEncoder encoder;
                        string ext = System.IO.Path.GetExtension(fileName).ToLower();

                        if (ext == ".jpg" || ext == ".jpeg") encoder = new JpegBitmapEncoder { QualityLevel = 90 };
                        else encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(heavyBitmap));
                        encoder.Save(fs);
                    }
                    pathForClipboard = tempPath;
                }

                // 将路径放入剪贴板
                if (pathForClipboard != null)
                {
                    var fileList = new System.Collections.Specialized.StringCollection();
                    fileList.Add(pathForClipboard);
                    dataObject.SetFileDropList(fileList);
                }
                ClipboardHelper.SetDataObjectWithRetry(dataObject, true);
            }
            catch (Exception ex)
            {
                ShowToast("L_Toast_CopyFailed", ex);
            }
            finally
            {
                if (heavyBitmap != null && (tab.IsDirty || tab.IsNew))
                {
                    heavyBitmap = null;
                    GC.Collect();
                }
            }
        }
        private void OnTabCopyClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is FileTabItem tab) CopyTabToClipboard(tab);
        }

        private void OnTabCutClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is FileTabItem tab)
            {
                CopyTabToClipboard(tab);
                CloseTab(tab);
            }
        }

        private async void OnTabPasteClick(object sender, RoutedEventArgs e)
        {
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
            FileTabItem tabToSelect = null; // 用于记录最后需要选中的 Tab

            IDataObject data = ClipboardHelper.GetDataObjectWithRetry();
            if (data != null && data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    int addedCount = 0;
                    foreach (string file in files)
                    {
                        if (!IsImageFile(file)) continue;

                        // 1. 跨窗口互斥检查
                        var (foundWindow, foundTab) = FindWindowHostingFile(file);
                        if (foundWindow != null && foundTab != null)
                        {
                            foundWindow.FocusAndSelectTab(foundTab);
                            continue;
                        }

                        // 2. 检查是否已存在
                        if (!_imageFiles.Contains(file))
                        {
                            // 1. 不存在：插入新 Tab
                            _imageFiles.Insert(insertIndex + addedCount, file);

                            var newTab = new FileTabItem(file) { IsLoading = true };
                            FileTabs.Insert(uiInsertIndex + addedCount, newTab);
                            _ = newTab.LoadThumbnailAsync(100, 60);

                            tabToSelect = newTab; // 标记需要切换到新文件
                            addedCount++;
                        }
                        else
                        {
                            // 2. 已存在：查找对应的 Tab
                            var existingTab = FileTabs.FirstOrDefault(t => string.Equals(t.FilePath, file, StringComparison.OrdinalIgnoreCase));
                            if (existingTab != null)
                            {
                                tabToSelect = existingTab; // 标记需要切换到已存在的文件
                            }
                        }
                    }
                    if (addedCount > 0 || tabToSelect != null) hasHandled = true;
                }
            }
            else if (data != null && data.GetDataPresent(DataFormats.Bitmap))
            {
                try
                {
                    BitmapSource source = data.GetData(DataFormats.Bitmap) as BitmapSource;

                    if (source != null)
                    {
                        var newTab = CreateNewUntitledTab();

                        string cacheFileName = $"{newTab.Id}.png";
                        string fullCachePath = System.IO.Path.Combine(_cacheDir, cacheFileName);

                        using (var fileStream = new FileStream(fullCachePath, FileMode.Create))
                        {
                            BitmapEncoder encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(source));
                            encoder.Save(fileStream);
                        }

                        newTab.BackupPath = fullCachePath;
                        newTab.IsDirty = true;
                        UpdateTabThumbnailFromBitmap(newTab, source);
                        FileTabs.Insert(uiInsertIndex, newTab);

                        // 滚动条稍微向右移动一点，增加视觉反馈
                        MainImageBar.Scroller.ScrollToHorizontalOffset(MainImageBar.Scroller.HorizontalOffset + 120);

                        tabToSelect = newTab; // 标记需要切换
                        hasHandled = true;
                    }
                }
                catch (Exception ex)
                {
                    ShowToast(string.Format(LocalizationManager.GetString("L_Toast_PasteFailed"), ex.Message),ex);
                }
            }

            if (hasHandled)
            {
                ImageFilesCount = _imageFiles.Count;
                SetPreviewSlider();
                UpdateImageBarSliderState();
                if (tabToSelect != null)
                {
                    SwitchToTab(tabToSelect);
                }
            }
        }
        private void OnTabOpenFolderClick(object sender, RoutedEventArgs e)
        {
            // 获取绑定的 Tab 对象
            if (sender is MenuItem item && item.Tag is FileTabItem tab)
            {
                if (string.IsNullOrEmpty(tab.FilePath)) return;
                if (!System.IO.File.Exists(tab.FilePath))
                {
                    ShowToast("L_Toast_FileNotFound");
                    return;
                }
                try
                {
                    string argument = $"/select, \"{tab.FilePath}\"";
                    System.Diagnostics.Process.Start("explorer.exe", argument);
                }
                catch (Exception ex)
                {
                    ShowToast(string.Format(LocalizationManager.GetString("L_Toast_OpenFolderFailed_Prefix"), ex.Message), ex);
                }
            }
        }
    }
}
