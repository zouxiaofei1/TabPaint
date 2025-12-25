
using System.ComponentModel;
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
        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                OnSaveAsClick(sender, e); // 如果没有当前路径，就走另存为
            }
            else SaveBitmap(_currentFilePath);
        }

        private void OnSaveAsClick(object sender, RoutedEventArgs e)
        {
            // 1. 【修复默认文件名】
            // 逻辑：如果是已存在的文件，默认显示原名；如果是新建文件，显示"未命名"
            string defaultName = "image.png";

            if (_currentTabItem != null && !_currentTabItem.IsNew && !string.IsNullOrEmpty(_currentTabItem.FilePath))
            {
                // 这里的 Path.GetFileName 确保只获取文件名（如 photo.jpg），而不是全路径
                defaultName = System.IO.Path.GetFileName(_currentTabItem.FilePath);
            }
            else
            {
                defaultName = "未命名.png";
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = PicFilterString,
                FileName = defaultName // 设置计算好的默认名
            };

            if (dlg.ShowDialog() == true)
            {
                string newPath = dlg.FileName;

                // 执行实际保存
                SaveBitmap(newPath);

                // 2. 【修复图片数量更新】核心逻辑
                // 更新全局路径变量
                _currentFilePath = newPath;
                _currentFileName = System.IO.Path.GetFileName(newPath);

                if (_currentTabItem != null)
                {
                    // 如果这个 Tab 之前是“新建”状态，或者另存为了一个新路径
                    bool isNewFileAdded = false;

                    // 更新 Tab 对象的核心数据
                    _currentTabItem.FilePath = newPath; // 将 Tab 指向新路径

                    // 如果之前是 IsNew，现在变成了正式文件
                    if (_currentTabItem.IsNew)
                    {
                        _currentTabItem.IsNew = false;
                        // 此时需要把新路径加入到文件夹列表里，否则 (x/y) 数量不对
                        if (!_imageFiles.Contains(newPath))
                        {
                            _imageFiles.Add(newPath);
                            // 可选：如果你希望按文件名自动排序，可以在这里 Sort 一下
                            // _imageFiles.Sort(); 
                            isNewFileAdded = true;
                        }
                    }
                    // 如果是老文件另存为新名字
                    else if (!_imageFiles.Contains(newPath))
                    {
                        _imageFiles.Add(newPath);
                        isNewFileAdded = true;
                    }

                    // 3. 重新计算索引 (非常重要，否则 (x/y) 显示错误)
                    if (isNewFileAdded || _currentImageIndex == -1)
                    {
                        _currentImageIndex = _imageFiles.IndexOf(newPath);
                    }
                }

                // 4. 标记已保存并刷新标题
                _isFileSaved = true;
                UpdateWindowTitle(); // 这里现在会正确显示 (x/y) 而不是 [新画板]
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
            if (!_maximized)
            {
                _restoreBounds = new Rect(Left, Top, Width, Height);
                _maximized = true;

                var workArea = SystemParameters.WorkArea;
                //s((SystemParameters.BorderWidth));
                Left = workArea.Left - (SystemParameters.BorderWidth) * 2;
                Top = workArea.Top - (SystemParameters.BorderWidth) * 2;
                Width = workArea.Width + (SystemParameters.BorderWidth * 4);
                Height = workArea.Height + (SystemParameters.BorderWidth * 4);

                SetRestoreIcon();  // 切换到还原图标
            }
            else
            {
                _maximized = false;
                Left = _restoreBounds.Left;
                Top = _restoreBounds.Top;
                Width = _restoreBounds.Width;
                Height = _restoreBounds.Height;
                WindowState = WindowState.Normal;

                // 切换到最大化矩形图标
                SetMaximizeIcon();
            }
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
            SetThicknessSlider_Pos(0);
        }

        private void ThicknessSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            ThicknessPreview.Visibility = Visibility.Collapsed;

            ThicknessTip.Visibility = Visibility.Collapsed;
        }

        private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialLayoutComplete) return;
            PenThickness = e.NewValue;
            UpdateThicknessPreviewPosition();

            if (ThicknessTip == null || ThicknessTipText == null || ThicknessSlider == null)
                return;

            PenThickness = e.NewValue;
            ThicknessTipText.Text = $"{(int)PenThickness} 像素";

            // 让提示显示出来
            ThicknessTip.Visibility = Visibility.Visible;
            SetThicknessSlider_Pos(e.NewValue);

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
            }
        }
        private void OnColorTempTintSaturationClick(object sender, RoutedEventArgs e)
        {
            if (_bitmap == null) return;
            _undo.PushFullImageUndo();// 1. (为Undo做准备) 保存当前图像的完整快照
            var dialog = new AdjustTTSWindow(_bitmap); // 2. 创建对话框，并传入主位图的一个克隆体用于预览
                                                       // 注意：这里我们传入的是 _bitmap 本身，因为 AdjustTTSWindow 内部会自己克隆一个原始副本


            if (dialog.ShowDialog() == true) // 更新撤销/重做按钮的状态
                SetUndoRedoButtonState();
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
            }
        }

        private void OnNewClick(object sender, RoutedEventArgs e)
        {
            CreateNewTab(true);
        }
    }
}