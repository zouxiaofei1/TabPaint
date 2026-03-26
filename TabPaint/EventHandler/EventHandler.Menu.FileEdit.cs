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
using System.Windows.Xps;
using System.Printing;
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
                        if (!_imageFiles.Contains(newPath))
                        {
                            _imageFiles.Add(newPath);
                            ImageFilesCount = _imageFiles.Count;
                        }
                    }
                    else if (!_imageFiles.Contains(newPath))
                    {
                        _imageFiles.Add(newPath);
                        ImageFilesCount = _imageFiles.Count;
                    }
                    _currentImageIndex = _imageFiles.IndexOf(newPath);
                }

                _isFileSaved = true;
                UpdateWindowTitle();
            }
        }

        private async void OnSaveAsPdfClick(object sender, RoutedEventArgs e)
        {
            if (FileTabs.Count == 0) return;

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = "Combined_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".pdf",
                DefaultExt = ".pdf",
                Title = LocalizationManager.GetString("L_Menu_File_SaveAsPDF")
            };

            if (saveDialog.ShowDialog() == true)
            {
                string targetPath = saveDialog.FileName;
                TaskProgressPopup.SetIcon("📄");
                TaskProgressPopup.UpdateProgress(0, LocalizationManager.GetString("L_Toast_SavingPDF_Title") ?? "Saving PDF...", "0%", "");

                try
                {
                    await Task.Run(() =>
                    {
                        using (var stream = new FileStream(targetPath, FileMode.Create))
                        using (var document = SkiaSharp.SKDocument.CreatePdf(stream))
                        {
                            int count = FileTabs.Count;
                            for (int i = 0; i < count; i++)
                            {
                                var tab = FileTabs[i];
                                this.Dispatcher.Invoke(() =>
                                {
                                    TaskProgressPopup.UpdateProgress((double)i / count * 100, null, $"{i + 1} / {count}", tab.FileName);
                                });

                                using (var imgStream = GetImageStreamForTab(tab))
                                {
                                    if (imgStream == null) continue;

                                    using (var skData = SkiaSharp.SKData.Create(imgStream))
                                    using (var skBitmap = SkiaSharp.SKBitmap.Decode(skData))
                                    {
                                        if (skBitmap == null) continue;

                                        using (var canvas = document.BeginPage(skBitmap.Width, skBitmap.Height))
                                        {
                                            canvas.DrawBitmap(skBitmap, 0, 0);
                                            document.EndPage();
                                        }
                                    }
                                }
                            }
                            document.Close();
                        }
                    });

                    ShowToast("L_Toast_SaveSuccess");
                }
                catch (Exception ex)
                {
                    ShowToast(string.Format(LocalizationManager.GetString("L_Toast_SaveFailed_Prefix"), ex.Message), ex);
                }
                finally
                {
                    TaskProgressPopup.Finish();
                }
            }
        }

        private Stream GetImageStreamForTab(FileTabItem tab)
        {
            // 核心逻辑：如果是当前正在编辑的标签，且有未提交的改动，从内存位图获取
            if (tab == _currentTabItem && _bitmap != null)
            {
                var ms = new MemoryStream();
                this.Dispatcher.Invoke(() =>
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(_bitmap));
                    encoder.Save(ms);
                });
                ms.Position = 0;
                return ms;
            }

            // 如果有自动保存/备份路径，从备份读取最新状态
            if (!string.IsNullOrEmpty(tab.BackupPath) && File.Exists(tab.BackupPath))
            {
                return new FileStream(tab.BackupPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }

            // 最后尝试从原始文件读取
            if (!string.IsNullOrEmpty(tab.FilePath) && File.Exists(tab.FilePath))
            {
                return new FileStream(tab.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }

            return null;
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

        private async void OnPrintClick(object sender, RoutedEventArgs e)
        {
            if (BackgroundImage.Source == null) return;

            // 优先使用 WinRT PrintManager 以支持现代打印预览
            if (_printManager != null)
            {
                await ShowPrintUIAsync();
                return;
            }

            try
            {
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    var viewbox = new Viewbox
                    {
                        Stretch = Stretch.Uniform,
                        Child = new Image
                        {
                            Source = BackgroundImage.Source,
                            Stretch = Stretch.Uniform,
                            UseLayoutRounding = true
                        }
                    };

                    double width = printDialog.PrintableAreaWidth;
                    double height = printDialog.PrintableAreaHeight;

                    Size pageSize = new Size(width, height);
                    viewbox.Measure(pageSize);
                    viewbox.Arrange(new Rect(new Point(0, 0), pageSize));
                    viewbox.UpdateLayout();

                    XpsDocumentWriter writer = PrintQueue.CreateXpsDocumentWriter(printDialog.PrintQueue);
                    writer.Write(viewbox, printDialog.PrintTicket);
                }
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Common_Error"), ex.Message), ex);
            }
        }
    }
}
