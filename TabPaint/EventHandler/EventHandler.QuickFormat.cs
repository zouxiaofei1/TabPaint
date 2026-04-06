
using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using TabPaint.Controls;
using System.Windows.Input;

namespace TabPaint
{
    public partial class MainWindow
    {
        private void ShowQuickFormatPanel()
        {
            if (_currentTabItem == null || string.IsNullOrEmpty(_currentTabItem.FilePath) || IsVirtualPath(_currentTabItem.FilePath))
            {
                ShowToast("L_QuickFormat_Error_NoFile");
                return;
            }

            _quickFormatModifiers = Keyboard.Modifiers;
            QuickFormatPopup.ShowPanel(_currentTabItem.FilePath);
        }

        private void OnQuickFormatSelected(object sender, string format)
        {
            if (_currentTabItem == null || string.IsNullOrEmpty(_currentTabItem.FilePath)) return;
            if (IsVirtualPath(_currentTabItem.FilePath)) return;

            string oldPath = _currentTabItem.FilePath;
            string extension = format.ToLower();
            if (extension == "jpg") extension = "jpg"; // 规范化

            string newPath = Path.ChangeExtension(oldPath, extension);

            // 如果文件名没变，不需要转换
            if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)) return;

            try
            {
                // 转换并保存
                SaveBitmap(newPath, allowIcoOptionsDialog: false);

                // 删除源文件
                if (File.Exists(oldPath))
                {
                    File.Delete(oldPath);
                }

                // 更新 UI 和数据结构
                _currentTabItem.FilePath = newPath;
                int index = _imageFiles.IndexOf(oldPath);
                if (index >= 0)
                {
                    _imageFiles[index] = newPath;
                }
                _currentFilePath = newPath;
                _currentFileName = Path.GetFileName(newPath);

                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_FormatConverted_Format"), format.ToUpper()));
                UpdateWindowTitle();
                QuickFormatPopup.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_SaveFailed_Prefix"), ex.Message), ex);
            }
            finally
            {
                _quickFormatModifiers = ModifierKeys.None;
            }
        }
    }
}
