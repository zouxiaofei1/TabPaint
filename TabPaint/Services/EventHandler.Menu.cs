
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

//
//TabPaint事件处理cs
//menu及位于那一行的所有东西
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            // 打开设置窗口
            
            var settingsWindow = new SettingsWindow();
            settingsWindow.ProgramVersion = this.ProgramVersion;
            settingsWindow.Owner = this; // 设置主窗口为父窗口，实现模态
            settingsWindow.ShowDialog();
        }



        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            // 如果是空路径 OR 是虚拟路径，都视为"从未保存过"，走另存为
            if (string.IsNullOrEmpty(_currentFilePath) || IsVirtualPath(_currentFilePath))
            {
                OnSaveAsClick(sender, e);
            }
            else
            {
                SaveBitmap(_currentFilePath);
            }
        }


        private void OnSaveAsClick(object sender, RoutedEventArgs e)
        {
            // 1. 准备默认文件名
            // 如果是新建的，DisplayName 会返回 "未命名 1"，如果是已有的，会返回原文件名
            string defaultName = _currentTabItem?.DisplayName ?? "image";
            if (!defaultName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                defaultName += ".png";

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = PicFilterString,
                FileName = defaultName
            };

            // 2. 需求2：默认位置为打开的文件夹 (即 _currentFilePath 所在目录)
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
                SaveBitmap(newPath); // 实际保存文件

                // 3. 更新状态
                _currentFilePath = newPath;
                _currentFileName = System.IO.Path.GetFileName(newPath);

                if (_currentTabItem != null)
                {
                    // 这里会触发 FilePath 的 setter，进而自动触发 DisplayName 的通知
                    _currentTabItem.FilePath = newPath;

                    if (_currentTabItem.IsNew)
                    {
                        _currentTabItem.IsNew = false; // 也会触发 DisplayName 更新通知
                        if (!_imageFiles.Contains(newPath)) _imageFiles.Add(newPath);
                    }
                    else if (!_imageFiles.Contains(newPath))
                    {
                        _imageFiles.Add(newPath);
                    }

                    _currentImageIndex = _imageFiles.IndexOf(newPath);
                   // s(_currentImageIndex);
                }

                _isFileSaved = true;
                UpdateWindowTitle();
            }
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            // 确保 SelectTool 是当前工具
            if (_router.CurrentTool != _tools.Select)
                _router.SetTool(_tools.Select); // 切换到选择工具

            if (_router.CurrentTool is SelectTool selectTool)
                selectTool.CopySelection(_ctx);
        }

        private void OnCutClick(object sender, RoutedEventArgs e)
        {
            if (_router.CurrentTool != _tools.Select)
                _router.SetTool(_tools.Select); // 切换到选择工具

            if (_router.CurrentTool is SelectTool selectTool)
                selectTool.CutSelection(_ctx, true);
        }

        private void OnPasteClick(object sender, RoutedEventArgs e)
        {
            if (_router.CurrentTool != _tools.Select)
                _router.SetTool(_tools.Select); // 切换到选择工具

            if (_router.CurrentTool is SelectTool selectTool)
                selectTool.PasteSelection(_ctx, false);

        }

        private void OnUndoClick(object sender, RoutedEventArgs e) => Undo();
        private void OnRedoClick(object sender, RoutedEventArgs e) => Redo();
        private void OnBrightnessContrastExposureClick(object sender, RoutedEventArgs e)
        {
            if (_bitmap == null) return;// 1. (为Undo做准备) 保存当前图像的完整快照
            var fullRect = new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight);
            _undo.PushFullImageUndo(); // 2. 创建对话框，并传入主位图的一个克隆体用于预览
            var dialog = new AdjustBCEWindow(_bitmap, BackgroundImage);   // 3. 显示对话框并根据结果操作
            if (dialog.ShowDialog() == true)
            {// 4. 从对话框获取处理后的位图
                WriteableBitmap adjustedBitmap = dialog.FinalBitmap;   // 5. 将处理后的像素数据写回到主位图 (_bitmap) 中
                int stride = adjustedBitmap.BackBufferStride;
                int byteCount = adjustedBitmap.PixelHeight * stride;
                byte[] pixelData = new byte[byteCount];
                adjustedBitmap.CopyPixels(pixelData, stride, 0);
                _bitmap.WritePixels(fullRect, pixelData, stride, 0); 
                CheckDirtyState();
                SetUndoRedoButtonState();
            }
            else
            {  // 用户点击了 "取消" 或关闭了窗口
                _undo.Undo(); // 弹出刚刚压入的快照
                _undo.ClearRedo(); // 清空因此产生的Redo项
                SetUndoRedoButtonState();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            OnClosing();
            //Close();
        }



        private void CropMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 假设你的当前工具存储在一个属性 CurrentTool 中
            // 并且你的 SelectTool 实例是可访问的
            if (_router.CurrentTool is SelectTool selectTool)
            {
                // 创建或获取当前的 ToolContext
                // var toolContext = CreateToolContext(); // 你应该已经有类似的方法

                selectTool.CropToSelection(_ctx);
            }
        }

        private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            MaximizeWindowHandler();
        }


        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void ThicknessSlider_DragStarted(object sender, DragStartedEventArgs e)
        {
            ThicknessPreview.Visibility = Visibility.Visible;
            UpdateThicknessPreviewPosition(); // 初始定位

            ThicknessTip.Visibility = Visibility.Visible;
            SetThicknessSlider_Pos(ThicknessSlider.Value);
        }

        private void ThicknessSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            ThicknessPreview.Visibility = Visibility.Collapsed;

            ThicknessTip.Visibility = Visibility.Collapsed;
        }

        private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialLayoutComplete) return;

            // Check: 防止空引用
            if (ThicknessTip == null || ThicknessTipText == null || ThicknessSlider == null)
                return;

            // --- 【关键修改开始】 ---

            // e.NewValue 现在是 0 到 1 之间的进度值
            double t = e.NewValue;

            // 手动执行非线性公式，计算出真实的像素值 (1 到 400)
            // 公式必须与 Converter 中的 ConvertBack 保持一致： Min + (Max - Min) * t^2
            double realSize = 1.0 + (400.0 - 1.0) * (t * t);

            // 更新本地变量 (如果是双向绑定，这一步其实 ViewModel 已经更新了，但为了预览流畅最好手动赋值)
            PenThickness = realSize;

            // 1. 更新圆圈预览 (用真实像素值)
            UpdateThicknessPreviewPosition();

            // 2. 更新提示文字 (用真实像素值)
            ThicknessTipText.Text = $"{(int)realSize} 像素";

            // 3. 更新提示框位置 (用 Slider 的进度值 0-1)
            // 注意：这里传入原始的 e.NewValue (0-1)，因为位置是根据进度条比例算的
            SetThicknessSlider_Pos(e.NewValue);

            // --- 【关键修改结束】 ---

            // 确保可见
            ThicknessTip.Visibility = Visibility.Visible;
        }


        private void OnOpenClick(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = PicFilterString
            };
            if (dlg.ShowDialog() == true)
            {
                _currentFilePath = dlg.FileName;
                _currentImageIndex = -1;
                OpenImageAndTabs(_currentFilePath, true);
                UpdateImageBarSliderState();
            }
        }
        private void OnColorTempTintSaturationClick(object sender, RoutedEventArgs e)
        {
            if (_bitmap == null) return;
            _undo.PushFullImageUndo();// 1. (为Undo做准备) 保存当前图像的完整快照
            var dialog = new AdjustTTSWindow(_bitmap); // 2. 创建对话框，并传入主位图的一个克隆体用于预览
                                                       // 注意：这里我们传入的是 _bitmap 本身，因为 AdjustTTSWindow 内部会自己克隆一个原始副本


            if (dialog.ShowDialog() == true) // 更新撤销/重做按钮的状态
            {
                SetUndoRedoButtonState();
                CheckDirtyState();
            }
            else// 用户点击了 "取消"
            {
                _undo.Undo();
                _undo.ClearRedo();
                SetUndoRedoButtonState();
            }
        }
        private void OnConvertToBlackAndWhiteClick(object sender, RoutedEventArgs e)
        {

            if (_bitmap == null) return;  // 1. 检查图像是否存在
            _undo.PushFullImageUndo();
            ConvertToBlackAndWhite(_bitmap);
            CheckDirtyState();
            SetUndoRedoButtonState();
        }
        private void OnResizeCanvasClick(object sender, RoutedEventArgs e)
        {
            if (_surface?.Bitmap == null) return;
            var dialog = new ResizeCanvasDialog(// 1. 创建并配置对话框
                _surface.Bitmap.PixelWidth,
                _surface.Bitmap.PixelHeight
            );
            dialog.Owner = this; // 设置所有者，使对话框显示在主窗口中央
            if (dialog.ShowDialog() == true)  // 2. 显示对话框，并检查用户是否点击了“确定”
            {
                // 3. 如果用户点击了“确定”，获取新尺寸并调用缩放方法
                int newWidth = dialog.ImageWidth;
                int newHeight = dialog.ImageHeight;

                ResizeCanvas(newWidth, newHeight);
                CheckDirtyState();
            }
        }

        private void OnNewClick(object sender, RoutedEventArgs e)
        {
            CreateNewTab(true);
        }
    }
}