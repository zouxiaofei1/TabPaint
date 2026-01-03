using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using TabPaint.Controls;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static TabPaint.MainWindow;

//
//TabPaint主程序
// 各种ITool + InputRouter + EventHandler + CanvasSurface 相关过程
//已经被拆分到Itools文件夹中
//MainWindow 类通用过程,很多都是找不到归属的,也有的是新加的测试功能
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {

        public MainWindow(string path)
        {
            _workingPath = path;
            _currentFilePath = path;
        

            InitializeComponent(); 
            this.ContentRendered += MainWindow_ContentRendered;
            DataContext = this;
            InitDebounceTimer(); 
            InitWheelLockTimer();
            Loaded += MainWindow_Loaded;

            InitializeAutoSave();


            this.Focusable = true; 
        }

     
        private async void MainWindow_ContentRendered(object sender, EventArgs e)
        {
           InitializeLazyControls();
            
            
            MyStatusBar.ZoomSliderControl.ValueChanged += (s, e) =>
            {
                if (_isInternalZoomUpdate)
                {
                    return;
                }
                double sliderVal = MyStatusBar.ZoomSliderControl.Value;

                // 2. 通过算法算出真实的缩放倍率 (例如滑块50 -> 倍率1.26)
                double targetScale = SliderToZoom(sliderVal);

                // 3. 应用缩放 (注意：不要在这里直接设置 Slider.Value，SetZoom 会去做的)
                SetZoom(targetScale);
            };

         
            SetBrushStyle(BrushStyle.Round);
            SetCropButtonState();   
           _canvasResizer = new CanvasResizeManager(this);;
            // 1. 先加载上次会话 (Tabs结构)
            LoadSession();
            if (!string.IsNullOrEmpty(_currentFilePath) && Directory.Exists(_currentFilePath))
            {
                _currentFilePath = FindFirstImageInDirectory(_currentFilePath);
            }
            
           
            if (!string.IsNullOrEmpty(_currentFilePath) && (File.Exists(_currentFilePath)))
            {
                // 直接 await，不要用 Task.Run，否则无法操作 UI 集合
                OpenImageAndTabs(_currentFilePath, true);
            }
            else
            {

                {
                    if (FileTabs.Count == 0)
                        CreateNewTab(TabInsertPosition.AfterCurrent, true);
                    else SwitchToTab(FileTabs[0]);
                }
            }
            await Task.Run(() =>
            {
                // 注意：创建 ResourceDictionary 必须在 UI 线程，但我们可以通过 Dispatcher 插入低优先级任务
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var iconsDict = new ResourceDictionary();
                    // 注意这里要用 pack URI 格式
                    iconsDict.Source = new Uri("pack://application:,,,/Resources/Icons/Icons.xaml");

                    // 把图标合并到全局资源中
                    Application.Current.Resources.MergedDictionaries.Add(iconsDict);
                }, System.Windows.Threading.DispatcherPriority.Background);
            });

            RestoreAppState();
            InitializeScrollPosition(); 

        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e); // 建议保留 base 调用

            MicaAcrylicManager.ApplyEffect(this); 
            MicaEnabled = true;
            //this.Show();
            InitializeClipboardMonitor();

            var src = (HwndSource)PresentationSource.FromVisual(this);
            if (src != null)
            {
                src.CompositionTarget.BackgroundColor = Colors.Transparent;
            }
           
            // 初始化 Mica

        }
        private void MainWindow_Activated(object sender, EventArgs e)
        {
            // 为了性能和避免闪烁，可以加个判断，如果已经是 Mica 则不重复设置
            if (!MicaEnabled)
            {
                MicaAcrylicManager.ApplyEffect(this);
                MicaEnabled = true;
            }
        }
        // 修改为 async void，以便使用 await
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
          
            await Task.Yield();  
            
            this.Focus();


            try
            {
                StateChanged += MainWindow_StateChanged;
                Select = new SelectTool();
                this.Deactivated += MainWindow_Deactivated;
                // 字体事件
                FontFamilyBox.SelectionChanged += FontSettingChanged;
                FontSizeBox.SelectionChanged += FontSettingChanged;
                BoldBtn.Checked += FontSettingChanged;
                BoldBtn.Unchecked += FontSettingChanged;
                ItalicBtn.Checked += FontSettingChanged;
                ItalicBtn.Unchecked += FontSettingChanged;
                UnderlineBtn.Checked += FontSettingChanged;
                UnderlineBtn.Unchecked += FontSettingChanged;

                //   SourceInitialized += OnSourceInitialized;



                // Canvas 事件
                CanvasWrapper.MouseDown += OnCanvasMouseDown;
                CanvasWrapper.MouseMove += OnCanvasMouseMove;
                CanvasWrapper.MouseUp += OnCanvasMouseUp;
                CanvasWrapper.MouseLeave += OnCanvasMouseLeave;

                // 初始化工具
                _surface = new CanvasSurface(_bitmap);
                _undo = new UndoRedoManager(_surface);
                _ctx = new ToolContext(_surface, _undo, BackgroundImage, SelectionPreview, SelectionOverlayCanvas, EditorOverlayCanvas, CanvasWrapper);
                _tools = new ToolRegistry();
                _ctx.ViewElement.Cursor = _tools.Pen.Cursor;
                _router = new InputRouter(_ctx, _tools.Pen);

                this.PreviewKeyDown += (s, e) =>
                {
                    MainWindow_PreviewKeyDown(s, e);
                    _router.OnPreviewKeyDown(s, e);
                };

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"启动加载失败: {ex.Message}");
                // 可以在这里弹窗提示用户
            }
            finally
            {
               
                _isInitialLayoutComplete = true;
                if (FileTabs.Count > 0)
                {
                    // 模拟触发一次滚动检查
                    OnFileTabsScrollChanged(MainImageBar.Scroller, null);
                }
              //  TimeRecorder t = new TimeRecorder(); t.Reset(); t.Toggle(); t.Toggle();
            }
        }
        private string FindFirstImageInDirectory(string folderPath)
        {
            try
            {
                // 获取所有文件
                var allFiles = Directory.GetFiles(folderPath);

                // 使用你现有的 IsImageFile 方法进行过滤，并按名称排序取第一个
                var firstImage = allFiles
                    .Where(f => IsImageFile(f))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase) // 确保按文件名顺序（如 1.jpg, 2.jpg）
                    .FirstOrDefault();

                return firstImage; // 如果没找到，返回 null
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取文件夹失败: {ex.Message}");
                return null;
            }
        }
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {  // 工具函数 - 查找所有子元素
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T) yield return (T)child;

                    foreach (T childOfChild in FindVisualChildren<T>(child)) yield return childOfChild;
                }
            }
        }
        private bool IsImageFile(string path)
        {
            string[] validExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".ico", ".tiff",".gif" ,".heic", ".tif" };
            string ext = System.IO.Path.GetExtension(path)?.ToLower();
            return validExtensions.Contains(ext);
        }

        private async Task OpenFilesAsNewTabs(string[] files)
        {
            if (files == null || files.Length == 0) return;

            // 1. 确定插入位置
            int insertIndex = _imageFiles.Count;
            int uiInsertIndex = FileTabs.Count;

            if (_currentTabItem != null && !_currentTabItem.IsNew)
            {
                int currentIndexInFiles = _imageFiles.IndexOf(_currentTabItem.FilePath);
                if (currentIndexInFiles >= 0) insertIndex = currentIndexInFiles + 1;

                int currentIndexInTabs = FileTabs.IndexOf(_currentTabItem);
                if (currentIndexInTabs >= 0) uiInsertIndex = currentIndexInTabs + 1;
            }

            FileTabItem firstNewTab = null;
            int addedCount = 0;

            foreach (string file in files)
            {
                if (IsImageFile(file))
                {
                    if (_imageFiles.Contains(file)) continue; // 去重

                    _imageFiles.Insert(insertIndex + addedCount, file);
                    var newTab = new FileTabItem(file) { IsLoading = true };

                    if (uiInsertIndex + addedCount <= FileTabs.Count)
                        FileTabs.Insert(uiInsertIndex + addedCount, newTab);
                    else
                        FileTabs.Add(newTab);

                    _ = newTab.LoadThumbnailAsync(100, 60);
                    if (firstNewTab == null) firstNewTab = newTab;
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                ImageFilesCount = _imageFiles.Count;
                SetPreviewSlider();

                if (firstNewTab != null)
                {
                    if (_currentTabItem != null) _currentTabItem.IsSelected = false;
                    firstNewTab.IsSelected = true;
                    _currentTabItem = firstNewTab;
                    await OpenImageAndTabs(firstNewTab.FilePath);
                    MainImageBar.Scroller.ScrollToHorizontalOffset(MainImageBar.Scroller.HorizontalOffset + 1);
                }
            }
        }


        private void FitToWindow(double addscale = 1)
        {
            if (SettingsManager.Instance.Current.IsFixedZoom&& _firstFittoWindowdone) return;
            if (BackgroundImage.Source != null)
            {
               
                //  s(1);
                double imgWidth = BackgroundImage.Source.Width;
                double imgHeight = BackgroundImage.Source.Height;
                //s(ScrollContainer.ViewportWidth);
                double viewWidth = ScrollContainer.ViewportWidth;
                double viewHeight = ScrollContainer.ViewportHeight;

                double scaleX = viewWidth / imgWidth;
                double scaleY = viewHeight / imgHeight;

                double fitScale = Math.Min(scaleX, scaleY); // 保持纵横比适应
                zoomscale = fitScale * addscale * 0.98;
                // s(fitScale);

                ZoomTransform.ScaleX = ZoomTransform.ScaleY = zoomscale;
                UpdateSliderBarValue(zoomscale);
                _canvasResizer.UpdateUI();
                _firstFittoWindowdone=true;
            }
        }
        private async void PasteClipboardAsNewTab()
        {
            // 准备一个列表来存放待处理的文件路径
            List<string> filesToProcess = new List<string>();

            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    var dropList = Clipboard.GetFileDropList();
                    if (dropList != null)
                    {
                        foreach (string file in dropList)
                        {
                            // 复用你现有的 IsImageFile 方法检查是否为支持的图片
                            if (IsImageFile(file))
                            {
                                filesToProcess.Add(file);
                            }
                        }
                    }
                }
                else if (Clipboard.ContainsImage())
                {
                    var bitmapSource = Clipboard.GetImage();
                    if (bitmapSource != null)
                    {
                        // 生成临时文件路径
                        string cacheDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TabPaint_Cache");
                        if (!System.IO.Directory.Exists(cacheDir)) System.IO.Directory.CreateDirectory(cacheDir);

                        string fileName = $"Paste_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                        string filePath = System.IO.Path.Combine(cacheDir, fileName);

                        // 保存为本地 PNG
                        using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
                        {
                            System.Windows.Media.Imaging.BitmapEncoder encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmapSource));
                            encoder.Save(fileStream);
                        }

                        filesToProcess.Add(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowToast($"读取剪切板数据失败: {ex.Message}");
                return;
            }

            if (filesToProcess.Count == 0) return;
            int insertIndex = _imageFiles.Count; // 默认插到最后
            int uiInsertIndex = FileTabs.Count;

            // 如果当前有选中的 Tab，且不是新建的空文件，则插在它后面
            if (_currentTabItem != null && !_currentTabItem.IsNew)
            {
                int currentIndexInFiles = _imageFiles.IndexOf(_currentTabItem.FilePath);
                if (currentIndexInFiles >= 0)
                {
                    insertIndex = currentIndexInFiles + 1;
                }

                int currentIndexInTabs = FileTabs.IndexOf(_currentTabItem);
                if (currentIndexInTabs >= 0)
                {
                    uiInsertIndex = currentIndexInTabs + 1;
                }
            }

            FileTabItem firstNewTab = null;
            int addedCount = 0;

            foreach (string file in filesToProcess)
            {
                // 去重检查（可选）
                if (_imageFiles.Contains(file)) continue;

                // 2. 插入到底层数据源
                _imageFiles.Insert(insertIndex + addedCount, file);

                // 3. 插入到 UI 列表
                var newTab = new FileTabItem(file);
                newTab.IsLoading = true;
                // 如果是剪切板生成的临时文件，最好标记一下，方便后续处理保存逻辑
                // if (file.Contains("TabPaint_Cache")) newTab.IsTemp = true; 

                if (uiInsertIndex + addedCount <= FileTabs.Count)
                {
                    FileTabs.Insert(uiInsertIndex + addedCount, newTab);
                }
                else
                {
                    FileTabs.Add(newTab);
                }

                // 异步加载缩略图
                _ = newTab.LoadThumbnailAsync(100, 60);

                // 记录第一张新图，用于稍后跳转
                if (firstNewTab == null) firstNewTab = newTab;

                addedCount++;
            }

            if (addedCount > 0)
            {
                // 更新 Slider 范围
                ImageFilesCount = _imageFiles.Count;
                SetPreviewSlider();

                // 4. 自动切换到第一张新加入的图片
                if (firstNewTab != null)
                {
                    // 取消当前选中状态
                    if (_currentTabItem != null) _currentTabItem.IsSelected = false;

                    // 选中新图
                    firstNewTab.IsSelected = true;
                    _currentTabItem = firstNewTab;

                    await OpenImageAndTabs(firstNewTab.FilePath);

                    // 确保新加的图片在视野内
                    MainImageBar.Scroller.ScrollToHorizontalOffset(MainImageBar.Scroller.HorizontalOffset + 1);
                }
            }
        }


        private string GetPixelFormatString(System.Windows.Media.PixelFormat format)
        {
            // 简单映射常见格式名
            return format.ToString().Replace("Rgb", "RGB").Replace("Bgr", "BGR");
        }
        private void CenterImage()
        {
            if (_bitmap == null || BackgroundImage == null)
                return;

            BackgroundImage.Width = BackgroundImage.Source.Width;
            BackgroundImage.Height = BackgroundImage.Source.Height;

            // 如果在 ScrollViewer 中，自动滚到中心
            if (ScrollContainer != null)
            {
                ScrollContainer.ScrollToHorizontalOffset(
                    (BackgroundImage.Width - ScrollContainer.ViewportWidth) / 2);
                ScrollContainer.ScrollToVerticalOffset(
                    (BackgroundImage.Height - ScrollContainer.ViewportHeight) / 2);
            }

            BackgroundImage.VerticalAlignment = VerticalAlignment.Center;
        }

    private void InitializeToastTimer()
    {
        _toastTimer = new DispatcherTimer();
        _toastTimer.Interval = TimeSpan.FromMilliseconds(ToastDuration);
        _toastTimer.Tick += (s, e) => HideToast(); // 计时结束触发淡出
    }

    private void ShowToast(string message)
    {
        if (_toastTimer == null) InitializeToastTimer();
        _toastTimer.Stop();
        InfoToastText.Text = message;

        if (InfoToast.Opacity < 1.0)
        {
            InfoToast.BeginAnimation(OpacityProperty, null);

            DoubleAnimation fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(200));
            fadeIn.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            InfoToast.BeginAnimation(OpacityProperty, fadeIn);
        }
        else
        {
        }

        // 4. 重新开始倒计时（重置停留时间）
        _toastTimer.Start();
    }

    // 独立的淡出方法
    private void HideToast()
    {
        _toastTimer.Stop(); // 停止计时器

        DoubleAnimation fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(500));
        // 缓动效果（可选）
        fadeOut.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn };

        InfoToast.BeginAnimation(OpacityProperty, fadeOut);
    }

    // 在 MainWindow 类中添加

    // 统一处理文字对齐点击
  

        // 修改原有的 FontSettingChanged，让它调用 TextTool 的更新
        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"大小: {bytes} B";
            if (bytes < 1024 * 1024) return $"大小: {bytes / 1024.0:F1} KB";
            return $"大小: {bytes / 1024.0 / 1024.0:F2} MB";
        }

        private void ShowNextImage()
        {
            if (_imageFiles.Count == 0 || _currentImageIndex < 0) return;
            _router.CleanUpSelectionandShape();
            _currentImageIndex++;
            if (_currentImageIndex >= _imageFiles.Count)
            {
                _currentImageIndex = 0; // 循环到第一张
                ShowToast("已回到第一张图片"); // 提示逻辑
            }

            RequestImageLoad(_imageFiles[_currentImageIndex]);
        }

        private void ShowPrevImage()
        {
            if (_imageFiles.Count == 0 || _currentImageIndex < 0) return;
            _router.CleanUpSelectionandShape();
            // 自动保存已编辑图片
            if (_isEdited && !string.IsNullOrEmpty(_currentFilePath))
            {
                SaveBitmap(_currentFilePath);
                _isEdited = false;
            }
            _currentImageIndex--;
            if (_currentImageIndex < 0)
            {
                _currentImageIndex = _imageFiles.Count - 1; // 循环到最后一张
                ShowToast("这是最后一张图片");
            }

            RequestImageLoad(_imageFiles[_currentImageIndex]);
        }
        private string SaveClipboardImageToCache(BitmapSource source)
        {
            try
            {
                // 确保缓存目录存在 (建议放在 TabPaint 的缓存目录中)
                string cacheDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache", "Clipboard");
                if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);

                string fileName = $"Paste_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
                string filePath = System.IO.Path.Combine(cacheDir, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    BitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(source));
                    encoder.Save(fileStream);
                }
                return filePath;
            }
            catch
            {
                return null;
            }
        }

        private async Task InsertImagesToTabs(string[] files)
        {
            if (files == null || files.Length == 0) return;

            // 1. 确定插入位置
            int insertIndex = _imageFiles.Count; // 默认插到最后
            int uiInsertIndex = FileTabs.Count;

            // 如果当前有选中的 Tab，且不是新建的空文件，则插在它后面
            if (_currentTabItem != null && !_currentTabItem.IsNew)
            {
                int currentIndexInFiles = _imageFiles.IndexOf(_currentTabItem.FilePath);
                if (currentIndexInFiles >= 0) insertIndex = currentIndexInFiles + 1;

                int currentIndexInTabs = FileTabs.IndexOf(_currentTabItem);
                if (currentIndexInTabs >= 0) uiInsertIndex = currentIndexInTabs + 1;
            }

            FileTabItem firstNewTab = null;
            int addedCount = 0;

            foreach (string file in files)
            {
                // 去重检查
                if (_imageFiles.Contains(file)) continue;

                // 2. 插入到底层数据源 _imageFiles
                _imageFiles.Insert(insertIndex + addedCount, file);

                // 3. 插入到 UI 列表 FileTabs
                var newTab = new FileTabItem(file);
                newTab.IsLoading = true;

                if (uiInsertIndex + addedCount <= FileTabs.Count)
                    FileTabs.Insert(uiInsertIndex + addedCount, newTab);
                else
                    FileTabs.Add(newTab);

                // 异步加载缩略图
                _ = newTab.LoadThumbnailAsync(100, 60);

                if (firstNewTab == null) firstNewTab = newTab;
                addedCount++;
            }

            if (addedCount > 0)
            {
                // 更新 Slider 范围
                ImageFilesCount = _imageFiles.Count;
                SetPreviewSlider(); // 假设你有这个方法更新 ImageBar Slider

                // 4. 自动切换到第一张新加入的图片
                if (firstNewTab != null)
                {
                    if (_currentTabItem != null) _currentTabItem.IsSelected = false;

                    firstNewTab.IsSelected = true;
                    _currentTabItem = firstNewTab;

                    // 调用你原本的打开逻辑
                    await OpenImageAndTabs(firstNewTab.FilePath);

                    // 滚动 ImageBar
                    MainImageBar.Scroller.ScrollToHorizontalOffset(MainImageBar.Scroller.HorizontalOffset + 1);
                }
            }
        }
        public static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject    // 这是一个通用的辅助方法，用于在可视化树中查找特定类型的子控件
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T)
                {
                    return (T)child;
                }
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }
            return null;
        }

        private void OnScrollContainerMouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                Point currentPos = e.GetPosition(ScrollContainer);

                // 计算偏移量：鼠标向左移，视口应该向右移，所以是 上次位置 - 当前位置
                double deltaX = _lastMousePosition.X - currentPos.X;
                double deltaY = _lastMousePosition.Y - currentPos.Y;

                ScrollContainer.ScrollToHorizontalOffset(ScrollContainer.HorizontalOffset + deltaX);
                ScrollContainer.ScrollToVerticalOffset(ScrollContainer.VerticalOffset + deltaY);
                _lastMousePosition = currentPos;
            }
        }
        private void OnScrollContainerMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                ScrollContainer.ReleaseMouseCapture();
                Mouse.OverrideCursor = null; // 恢复光标
            }
        }
        private bool _isDragOverlayVisible = false;

        // 更新显示方法


        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_programClosed) OnClosing();
        }
    }
}
