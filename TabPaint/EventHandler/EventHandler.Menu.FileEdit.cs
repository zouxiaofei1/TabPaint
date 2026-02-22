//
//EventHandler.Menu.cs
//fileedit两菜单
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;
using TabPaint.Controls;
using TabPaint.UIHandlers;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {

        private void OnNewWindowClick(object sender, RoutedEventArgs e)
        {
            try
            {
                MainWindow newWindow = new MainWindow(string.Empty, false, loadSession: false);
                newWindow.Show();
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Common_Error"), ex.Message), ex);
            }
        }
        private async void OnRecentFileClick(object sender, string filePath)
        {
            if (File.Exists(filePath))
            {
                var (existingWindow, existingTab) = FindWindowHostingFile(filePath);
                if (existingWindow != null && existingTab != null)
                {
                    existingWindow.FocusAndSelectTab(existingTab);
                    return;
                }

                string[] files = [filePath];
                await OpenFilesAsNewTabs(files);

                UpdateImageBarSliderState();
            }
            else ShowToast(string.Format(LocalizationManager.GetString("L_Toast_FileNotFound_Format"), filePath));
        }
        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath) || IsVirtualPath(_currentFilePath)) OnSaveAsClick(sender, e);
            else SaveBitmap(_currentFilePath);
        }

        private void OnSaveAsClick(object sender, RoutedEventArgs e)
        {
            string defaultName = _currentTabItem?.DisplayName ?? "image";
            if (!defaultName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                defaultName += ".png";

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = PicFilterString,
                FileName = defaultName
            };
            string initialDir = "";
            if (!string.IsNullOrEmpty(_currentFilePath))
                initialDir = System.IO.Path.GetDirectoryName(_currentFilePath);
            else if (_imageFiles != null && _imageFiles.Count > 0)
                initialDir = System.IO.Path.GetDirectoryName(_imageFiles[0]);

            if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir))
                dlg.InitialDirectory = initialDir;

            if (dlg.ShowDialog() == true)
            {
                string newPath = dlg.FileName;
                SaveBitmap(newPath);
                _currentFilePath = newPath;
                _currentFileName = System.IO.Path.GetFileName(newPath);

                if (_currentTabItem != null)
                {
                    _currentTabItem.FilePath = newPath;
                    if (_currentTabItem.IsNew)
                    {
                        _currentTabItem.IsNew = false;
                        if (!_imageFiles.Contains(newPath)) _imageFiles.Add(newPath);
                    }
                    else if (!_imageFiles.Contains(newPath))
                    {
                        _imageFiles.Add(newPath);
                    }
                    _currentImageIndex = _imageFiles.IndexOf(newPath);
                }

                _isFileSaved = true;
                UpdateWindowTitle();
            }
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            if (_router.CurrentTool != _tools.Select)
                _router.SetTool(_tools.Select);

            if (_router.CurrentTool is SelectTool selectTool)
                selectTool.CopySelection(_ctx);
        }

        private void OnCutClick(object sender, RoutedEventArgs e)
        {
            if (_router.CurrentTool != _tools.Select)
                _router.SetTool(_tools.Select);

            if (_router.CurrentTool is SelectTool selectTool)
                selectTool.CutSelection(_ctx, true);
        }

        private void OnPasteClick(object sender, RoutedEventArgs e)
        {
            if (_router.CurrentTool != _tools.Select)
                _router.SetTool(_tools.Select);

            if (_router.CurrentTool is SelectTool selectTool)
                selectTool.PasteSelection(_ctx, false);
        }
        private async void OnOpenWorkspaceClick(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = LocalizationManager.GetString("L_CreateNewWorkSpate"),
                Filter = PicFilterString,
                Multiselect = false
            };

            if (dlg.ShowDialog() == true)
            {
                string file = dlg.FileName;
                SettingsManager.Instance.AddRecentFile(file);
                await SwitchWorkspaceToNewFile(file);
                UpdateImageBarSliderState();
            }
        }

        private async void OnOpenClick(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = PicFilterString,
                Multiselect = true
            };
            if (dlg.ShowDialog() == true)
            {
                string[] files = dlg.FileNames;
                await OpenFilesAsNewTabs(files);
                foreach (var file in files)
                    SettingsManager.Instance.AddRecentFile(file);
                UpdateImageBarSliderState();
            }
        }

    }
}