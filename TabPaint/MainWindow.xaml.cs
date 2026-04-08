
//
//MainWindow.xaml.cs
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using TabPaint.Controls;
using TabPaint.Services;
using TabPaint.UIHandlers;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public static readonly RoutedEvent FavoriteClickEvent = EventManager.RegisterRoutedEvent(
            "FavoriteClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MainWindow));

        public event RoutedEventHandler FavoriteClick
        {
            add { AddHandler(FavoriteClickEvent, value); }
            remove { RemoveHandler(FavoriteClickEvent, value); }
        }

        public static MainWindow GetCurrentInstance()
        {
            foreach (Window w in Application.Current.Windows)
            {
                if (w is MainWindow mw && mw.IsActive) return mw;
            }
            if (_lastFocusedInstance != null) return _lastFocusedInstance;

            // 兜底：返回第一个找到的 MainWindow
            return Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
        }
        public static (MainWindow? Window, FileTabItem? Tab) FindWindowHostingFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || filePath.StartsWith(AppConsts.VirtualFilePrefix)) return (null, null);

            foreach (Window w in Application.Current.Windows)
            {
                if (w is MainWindow mw)
                {
                    var tab = mw.FileTabs.FirstOrDefault(t => string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
                    if (tab != null) return (mw, tab);
                }
            }
            return (null, null);
        }
        public void FocusAndSelectTab(FileTabItem tab)
        {
            RestoreWindow(this);
            SwitchToTab(tab);
            ScrollToTabCenter(tab);
            UpdateImageBarSliderState();
        }

        // 记录最后获得焦点的实例
        private static MainWindow? _lastFocusedInstance;

        private UIHandlers.DropZoneWindow? _dropZone;

        private bool _shouldLoadSession = true;
        private bool _startupTraceFlushed = false;

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            _lastFocusedInstance = this;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (_lastFocusedInstance == this)
                _lastFocusedInstance = null;

            TrayIconService.UpdateVisibility();
        }


        public bool IsWin11 => MicaAcrylicManager.IsWin11();

        public WriteableBitmap CurrentBitmap => _bitmap;

        /// <summary>
        /// 检查当前位图是否含有透明像素 (Alpha < 255)
        /// </summary>
        public bool HasTransparency()
        {
            var bmp = _bitmap;
            if (bmp == null) return false;

            bmp.Lock();
            try
            {
                unsafe
                {
                    int width = bmp.PixelWidth;
                    int height = bmp.PixelHeight;
                    int* p = (int*)bmp.BackBuffer;
                    int count = width * height;

                    for (int i = 0; i < count; i++)
                    {
                        // BGRA32 格式，Alpha 在最高字节 (0xAARRGGBB in little endian is BB GG RR AA)
                        // 但 int* 指针访问时，位移 24 位正好是 Alpha
                        if (((*p >> 24) & 0xFF) < 255)
                        {
                            return true;
                        }
                        p++;
                    }
                }
            }
            finally
            {
                bmp.Unlock();
            }
            return false;
        }

        public MainWindow(string path, bool? fileExists = null, FileTabItem? initialTab = null, bool loadSession = true)
        {
            using var __perfCtor = StartupPerformanceTracer.Measure("MainWindow.Ctor");
            if (!MicaAcrylicManager.IsWin11())
            {
                this.AllowsTransparency = true;
            }
            
            _shouldLoadSession = loadSession;
            _workingPath = path;
            _currentFilePath = path;  
            if (fileExists.HasValue) _currentFileExists = fileExists.Value;//<0.1ms
            else CheckFilePathAvailibility(_currentFilePath);

            if (initialTab != null)
            {
                FileTabs.Add(initialTab);
                _currentTabItem = initialTab;
                _imageFiles.Add(initialTab.FilePath);
                ImageFilesCount = _imageFiles.Count;
                _currentFilePath = initialTab.FilePath;
                _currentFileName = initialTab.FileName;
                _currentImageIndex = 0;
            }
            InitializeLazyControls();
            StartupPerformanceTracer.Point("MainWindow.BeforeInitializeComponent");
            InitializeComponent();  //220ms
            StartupPerformanceTracer.Point("MainWindow.AfterInitializeComponent");
          
            RestoreWindowBounds();//0.3ms

            if (SettingsManager.Instance.Current.StartInViewMode && _currentFileExists)
            {
                IsViewMode = true;
                ThicknessPanel.Visibility = Visibility.Collapsed;
                OpacityPanel.Visibility = Visibility.Collapsed;
            }
            this.ContentRendered += MainWindow_ContentRendered;
            DataContext = this;

            InitDebounceTimer();//0.3ms
            InitWheelLockTimer(); //0.4ms
            Loaded += MainWindow_Loaded;
            Loaded += (_, _) => TrayIconService.UpdateVisibility();
            IsVisibleChanged += (_, _) => TrayIconService.UpdateVisibility();
            Activated += MainWindow_Activated;

            this.Focusable = true;

            FileTabs.CollectionChanged += (s, e) =>
            {
                ImageFilesCount = _imageFiles.Count;
                OnPropertyChanged(nameof(CanNavigateImages));
            };

            this.Loaded += (s, e) =>
            {
                ImageFilesCount = _imageFiles.Count;
                OnPropertyChanged(nameof(CanNavigateImages));
            };

            QuickFormatPopup.FormatSelected += OnQuickFormatSelected;
        }


        private async void MainWindow_ContentRendered(object sender, EventArgs e)
        {
            using var __perfContentRendered = StartupPerformanceTracer.Measure("MainWindow.ContentRendered");
            InitializeAutoSave();
        
            this.SupportFocusHighlight();
            UpdateImageBarSliderState();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                ThemeManager.LoadLazyIcons();
            }), DispatcherPriority.Background);
            
            _canvasResizer = new CanvasResizeManager(this);//0.2ms
            OnModeChanged(IsViewMode, isSilent: true);


            if (_shouldLoadSession)
            {
                _ = Dispatcher.InvokeAsync(async () =>
                {
                    using var __perfLoadSession = StartupPerformanceTracer.Measure("MainWindow.ContentRendered.LoadSessionAsync");
                    await LoadSessionAsync();
                }, DispatcherPriority.Background);
            }

           if (!string.IsNullOrEmpty(_currentFilePath) && Directory.Exists(_currentFilePath))//0.2ms
            {
                _currentFilePath = FindFirstImageInDirectory(_currentFilePath);
            }


            if (!string.IsNullOrEmpty(_currentFilePath) && (File.Exists(_currentFilePath) || IsVirtualPath(_currentFilePath)))
            {
                if (FileTabs.Contains(_currentTabItem) && _currentTabItem != null)
                {
                    // 如果已经有了通过构造函数传入的标签，直接加载它
                  OpenImageAndTabs(_currentTabItem.FilePath, true);
                    
                    // 还原撤销栈
                    _undo.ClearUndo();
                    _undo.ClearRedo();
                    if (_currentTabItem.UndoStack != null)
                    {
                        foreach (var action in _currentTabItem.UndoStack) _undo._undo.Push(action);
                    }
                    if (_currentTabItem.RedoStack != null)
                    {
                        foreach (var action in _currentTabItem.RedoStack) _undo._redo.Push(action);
                    }
                    _savedUndoPoint = _currentTabItem.SavedUndoPoint;
                    SetUndoRedoButtonState();
                }
                else
                {
                    OpenImageAndTabs(_currentFilePath, true);
                }
            }
            else
            {//无路径启动
                {
                    if (FileTabs.Count == 0)
                    {
                        CreateNewTab(TabInsertPosition.AfterCurrent, true);
                    }
                    else
                    {
                        SwitchToTab(FileTabs[0]);
                    }
                }
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                RestoreAppState();
            }), DispatcherPriority.Loaded);
            this.Dispatcher.InvokeAsync(() =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _startupFinished = true;
                }), DispatcherPriority.ApplicationIdle);
                Dispatcher.InvokeAsync(() => {
                    CenterImage();
                }, DispatcherPriority.Render);
            }, DispatcherPriority.ApplicationIdle);

            InitializeScrollPosition();

            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_startupTraceFlushed) return;
                _startupTraceFlushed = true;
                string tracePath = StartupPerformanceTracer.Flush("MainWindow.ContentRendered.ApplicationIdle");
                if (!string.IsNullOrWhiteSpace(tracePath))
                {
                    Logger.Info($"[StartupTrace] {tracePath}");
                }
            }), DispatcherPriority.ApplicationIdle);
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            using var __perfOnSourceInitialized = StartupPerformanceTracer.Measure("MainWindow.OnSourceInitialized");
            //Dispatcher.BeginInvoke(new Action(() =>
            //{
                base.OnSourceInitialized(e); //17ms

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!MicaEnabled)
                {
                    MicaAcrylicManager.ApplyEffect(this);
                    MicaEnabled = true;
                }
            }), DispatcherPriority.Background);

            var currentSettings = SettingsManager.Instance.Current;//共0.7ms
            bool isDark = (ThemeManager.CurrentAppliedTheme == AppTheme.Dark) || (currentSettings.StartInViewMode && currentSettings.ViewUseDarkCanvasBackground && _currentFileExists);
            ThemeManager.SetWindowImmersiveDarkMode(this, isDark); 


            Dispatcher.BeginInvoke(new Action(() =>
            {
                InitializeClipboardMonitor();
            }), DispatcherPriority.ApplicationIdle);

            var src = (HwndSource)PresentationSource.FromVisual(this);
            if (src != null)
            {
                src.CompositionTarget.BackgroundColor = Colors.Transparent;
            }
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
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {//共5ms
            using var __perfLoaded = StartupPerformanceTracer.Measure("MainWindow.Loaded");
            try
            {
                var bar = this.FindName("FavoriteBar") as FavoriteBarControl;
                var popup = this.FindName("FavoritePopup") as Popup;
                if (bar != null && popup != null)
                {
                    bar.CloseRequested += (s, ev) => { if (popup != null) popup.IsOpen = false; };
                    bar.ImageSelected += async (path) =>
                    {
                        if (AppConsts.IsSupportedImage(path))
                        {
                            await OpenFilesAsNewTabs(new string[] { path });
                            if (popup != null) popup.IsOpen = false;
                        }
                    };
                }
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _ = Task.Run(() =>
                    {
                        int score = QuickBenchmark.EstimatePerformanceScore();
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            PerformanceScore = score;
                        }));
                    });
                }), DispatcherPriority.ApplicationIdle);

                this.Focus(); 
                _deleteCommitTimer = new System.Windows.Threading.DispatcherTimer();
                _deleteCommitTimer.Interval = TimeSpan.FromSeconds(AppConsts.DeleteCommitTimerSeconds); // 2秒
                _deleteCommitTimer.Tick += (s, e) => CommitPendingDeletions();
                StateChanged += MainWindow_StateChanged;
                Select = new SelectTool();
                this.Deactivated += MainWindow_Deactivated;

                // 魔棒容差悬浮窗初始化
                WandTolerancePopup.ToleranceChanged += (s, val) =>
                {
                    if (_router?.CurrentTool is SelectTool selectTool)
                    {
                        selectTool.SetWandTolerance(val);
                        selectTool.RefreshWandPreview(_ctx);
                    }
                };

                // 选区旋转悬浮窗初始化
                SelectionRotatePopup.AngleChanged += SelectionRotatePopup_AngleChanged;

                // Canvas 事件
                CanvasWrapper.MouseDown += OnCanvasMouseDown;
                CanvasWrapper.MouseMove += OnCanvasMouseMove;
                CanvasWrapper.MouseUp += OnCanvasMouseUp;
                CanvasWrapper.MouseLeave += OnCanvasMouseLeave;

                // 初始化工具
                _surface = new CanvasSurface(_bitmap);
                _undo = new UndoRedoManager(_surface);
                _ctx = new ToolContext(this, _surface, _undo, BackgroundImage, SelectionPreview, SelectionOverlayCanvas, EditorOverlayCanvas, CanvasWrapper);
                _tools = new ToolRegistry();
                _ctx.ViewElement.Cursor = _tools.Pen.Cursor;
                _router = new InputRouter(this, _ctx, _tools.Pen);
                _originalGridBrush = CanvasWrapper.Background;
                SettingsManager.Instance.Current.PropertyChanged += OnSettingsPropertyChanged;
                UpdateCanvasVisuals();
                this.PreviewKeyDown += (s, e) =>
                {
                    MainWindow_PreviewKeyDown(s, e);
                    _router.OnPreviewKeyDown(s, e);
                };

                _ = Task.Delay(TimeSpan.FromSeconds(AppConsts.DragTempCleanupDelaySeconds)).ContinueWith(async _ =>
                    {
                        await CheckAndCleanDragTempAsync();
                    }, TaskScheduler.Default); 
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_LoadFailed_Prefix"), ex.Message), ex);
            }
            finally
            {
                _isInitialLayoutComplete = true;
                if (FileTabs.Count > 0 && MainImageBar != null && MainImageBar.Scroller != null)
                { // 模拟触发一次滚动检查
                    OnFileTabsScrollChanged(MainImageBar.Scroller, null);
                }
             
            }
        }
        private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppSettings.Language))
            {
                // 刷新依赖于语言的硬编码属性
                OnPropertyChanged(nameof(PicFilterString));
            }

            if (e.PropertyName == nameof(TabPaint.SettingsManager.Instance.Current.ViewUseDarkCanvasBackground) ||
                e.PropertyName == nameof(TabPaint.SettingsManager.Instance.Current.ViewShowTransparentGrid))
            {
                UpdateCanvasVisuals();
            }

            if (e.PropertyName == nameof(AppSettings.EnableClipboardMonitor))
            {
                var settings = SettingsManager.Instance.Current;
                if (settings.EnableClipboardMonitor)
                {
                    if (_activeMonitorInstance == null) _activeMonitorInstance = this;
                }
                else
                {
                    if (_activeMonitorInstance == this) _activeMonitorInstance = null;
                }
            }

            if (e.PropertyName == nameof(AppSettings.IsWindowTopmost))
            {
                this.Topmost = SettingsManager.Instance.Current.IsWindowTopmost;
                SettingsManager.Instance.Save();
            }

            if (e.PropertyName == nameof(AppSettings.ShowBirdEyeInViewMode))
            {
                CheckBirdEyeVisibility();
                UpdateBirdEyeView();
            }
        }
        private void UpdateCanvasVisuals()
        {
            var settings = SettingsManager.Instance.Current;

            if (IsViewMode)
            {
                if (settings.ViewUseDarkCanvasBackground)ScrollContainer.Background = _darkBackgroundBrush;
                else ScrollContainer.Background = Brushes.Transparent;
                if (settings.ViewShowTransparentGrid)CanvasWrapper.Background = _originalGridBrush;
                else CanvasWrapper.Background = Brushes.White;
            }
            else
            {
                ScrollContainer.Background = Brushes.Transparent;
                CanvasWrapper.Background = _originalGridBrush;
            }
        }


        private string FindFirstImageInDirectory(string folderPath)
        {
            try
            {
                var allFiles = Directory.GetFiles(folderPath);

                var firstImage = allFiles
                    .Where(f => IsImageFile(f))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase) //自然语言顺序
                    .FirstOrDefault();

                return firstImage; // 如果没找到，返回 null
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_ReadFolderFailed_Prefix"), ex.Message), ex);
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
            string ext = System.IO.Path.GetExtension(path)?.ToLower();
            return AppConsts.ImageExtensions.Contains(ext);
        }

        public async Task OpenFilesAsNewTabs(string[] files)
        {
            if (files == null || files.Length == 0) return;

            bool showProgress = files.Length > 5;
            if (showProgress)
            {
                TaskProgressPopup.SetIcon(AppConsts.PathTaskProgress);
                TaskProgressPopup.UpdateProgress(0, LocalizationManager.GetString("L_Toast_BatchOpen_Title") ?? "Opening images...", $"0 / {files.Length}", "");
            }

            int insertIndex = _imageFiles.Count;
            int uiInsertIndex = FileTabs.Count;

            if (_currentTabItem != null && !_currentTabItem.IsNew)
            {
                int currentIndexInFiles = _imageFiles.IndexOf(_currentTabItem.FilePath);
                if (currentIndexInFiles >= 0) insertIndex = currentIndexInFiles + 1;

                int currentIndexInTabs = FileTabs.IndexOf(_currentTabItem);
                if (currentIndexInTabs >= 0) uiInsertIndex = currentIndexInTabs + 1;
            }

            FileTabItem? firstNewTab = null;
            int addedCount = 0;

            foreach (string file in files)
            {
                if (IsImageFile(file))
                {
                    // 1. 跨窗口互斥：检查文件是否在其他窗口打开
                    var (existingWindow, existingTab) = FindWindowHostingFile(file);
                    if (existingWindow != null && existingTab != null)
                    {
                        existingWindow.FocusAndSelectTab(existingTab);
                        continue;
                    }
                    if (_imageFiles.Contains(file))
                    {
                        var tab = FileTabs.FirstOrDefault(t => string.Equals(t.FilePath, file, StringComparison.OrdinalIgnoreCase));
                        if (tab != null) firstNewTab = tab;
                        continue;
                    }

                    _imageFiles.Insert(insertIndex + addedCount, file);
                    var newTab = new FileTabItem(file) { IsLoading = true };

                    if (uiInsertIndex + addedCount <= FileTabs.Count)
                        FileTabs.Insert(uiInsertIndex + addedCount, newTab);
                    else
                        FileTabs.Add(newTab);

                    _ = newTab.LoadThumbnailAsync(AppConsts.DefaultThumbnailWidth, AppConsts.DefaultThumbnailHeight);
                    if (firstNewTab == null) firstNewTab = newTab;
                    addedCount++;

                    if (showProgress)
                    {
                        double p = (double)addedCount / files.Length * 100;
                        TaskProgressPopup.UpdateProgress(p, null, $"{addedCount} / {files.Length}", "");
                    }
                }
            }

            if (showProgress) TaskProgressPopup.Finish();

            if (firstNewTab != null)
            {
                ImageFilesCount = _imageFiles.Count;
                SetPreviewSlider();
                _router.CleanUpSelectionandShape();
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


        private void FitToWindow(double addscale = 1,bool needcanvasUpdateUI=true,double viewWidthoffset=0,double viewHeightoffset = 0)
        {
            if (SettingsManager.Instance.Current.IsFixedZoom && _firstFittoWindowdone) return;
            if (BackgroundImage.Source != null)
            {
             
                double imgWidth = BackgroundImage.Source.Width;
                double imgHeight = BackgroundImage.Source.Height;
                double viewWidth = ScrollContainer.ViewportWidth+ viewWidthoffset;
                double viewHeight = ScrollContainer.ViewportHeight+ viewHeightoffset;
                double scaleX = viewWidth / imgWidth;
                double scaleY = viewHeight / imgHeight;

                double fitScale = Math.Min(scaleX, scaleY); // 保持纵横比适应
                zoomscale = fitScale * addscale * AppConsts.FitToWindowMarginFactor;
                ZoomTransform.ScaleX = ZoomTransform.ScaleY = zoomscale;
                UpdateSliderBarValue(zoomscale);

                if(needcanvasUpdateUI)_canvasResizer.UpdateUI();//关掉可以节省2-5ms
                _firstFittoWindowdone = true;
                    CenterImage();
            }
        }
        private async void PasteClipboardAsNewTab()
        {
            List<string> filesToProcess = new List<string>();

            try
            {
                var dataObj = ClipboardHelper.GetDataObjectWithRetry();
                if (dataObj == null) return;

                if (dataObj.GetDataPresent(DataFormats.FileDrop))
                {
                    var dropList = dataObj.GetData(DataFormats.FileDrop) as string[];
                    if (dropList != null)
                    {
                        foreach (string file in dropList)
                        {
                            if (IsImageFile(file)) filesToProcess.Add(file);
                        }
                    }
                }
                else if (TryExtractBitmapFromDataObject(dataObj, out var bitmapSource))
                {
                    if (bitmapSource != null)
                    {
                        string cacheDir = AppConsts.CacheDir;
                        if (!System.IO.Directory.Exists(cacheDir)) System.IO.Directory.CreateDirectory(cacheDir);

                        string fileName = $"Paste_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                        string filePath = System.IO.Path.Combine(cacheDir, fileName);

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
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_ClipboardReadFailed_Prefix"), ex.Message), ex);
                return;
            }

            if (filesToProcess.Count == 0) return;
            int insertIndex = _imageFiles.Count; // 默认插到最后
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

            foreach (string file in filesToProcess)
            {
                // 1. 跨窗口互斥检查
                var (existingWindow, existingTab) = FindWindowHostingFile(file);
                if (existingWindow != null && existingTab != null)
                {
                    existingWindow.FocusAndSelectTab(existingTab);
                    continue;
                }

                if (_imageFiles.Contains(file)) continue;
                _imageFiles.Insert(insertIndex + addedCount, file);

                var newTab = new FileTabItem(file);
                newTab.IsLoading = true;

                if (uiInsertIndex + addedCount <= FileTabs.Count)FileTabs.Insert(uiInsertIndex + addedCount, newTab);
                else FileTabs.Add(newTab);
                _ = newTab.LoadThumbnailAsync(AppConsts.DefaultThumbnailWidth, AppConsts.DefaultThumbnailHeight); // 异步加载缩略图
                if (firstNewTab == null) firstNewTab = newTab; // 记录第一张新图，用于稍后跳转

                addedCount++;
            }

            if (addedCount > 0)
            {
                ImageFilesCount = _imageFiles.Count;
                SetPreviewSlider();

                if (_tools.Select is SelectTool st && st.HasActiveSelection)  st.CommitSelection(_ctx);

                if (firstNewTab != null)
                {
                    if (_currentTabItem != null) _currentTabItem.IsSelected = false;
                    firstNewTab.IsSelected = true; // 选中新图
                    _currentTabItem = firstNewTab;
                    await OpenImageAndTabs(firstNewTab.FilePath);
                   
                    MainImageBar.Scroller.ScrollToHorizontalOffset(MainImageBar.Scroller.HorizontalOffset + 1); // 确保新加的图片在视野内
                }
            }
        }
        private async Task CheckAndCleanDragTempAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(_dragTempDir)) return;

                    var dirInfo = new DirectoryInfo(_dragTempDir);
                    var files = dirInfo.GetFiles();
                    int maxFileCount = 100; // 设定阈值：超过100个文件触发清理
                    int targetCount = 20;   // 清理目标：清理到只剩20个
                    if (files.Length > maxFileCount)
                    {
                        var sortedFiles = files.OrderBy(f => f.CreationTime).ToList();// 按创建时间升序排列（最旧的在前）
                        int deleteCount = files.Length - targetCount;

                        int deleted = 0;
                        foreach (var file in sortedFiles)
                        {
                            if (deleted >= deleteCount) break;
                            try
                            {
                                file.Delete(); // 尝试删除
                                deleted++;
                            }
                            catch (IOException) { /* 文件可能被占用，跳过 */ }
                            catch (UnauthorizedAccessException) { /* 无权限，跳过 */ }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TabPaint] DragTemp cleanup failed: {ex.Message}");
                }
            });
        }


        private string GetPixelFormatString(System.Windows.Media.PixelFormat format)
        {
            return format.ToString().Replace("Rgb", "RGB").Replace("Bgr", "BGR");
        }
        private void CenterImage()
        {
            if (_bitmap == null || BackgroundImage == null || ScrollContainer == null)
                return;
            BackgroundImage.Width = BackgroundImage.Source.Width;
            BackgroundImage.Height = BackgroundImage.Source.Height;
            ScrollContainer.UpdateLayout();
            double targetOffsetH = (ScrollContainer.ExtentWidth - ScrollContainer.ViewportWidth) / 2;
            double targetOffsetV = (ScrollContainer.ExtentHeight - ScrollContainer.ViewportHeight) / 2;

            ScrollContainer.ScrollToHorizontalOffset(targetOffsetH);
            ScrollContainer.ScrollToVerticalOffset(targetOffsetV);

            BackgroundImage.VerticalAlignment = VerticalAlignment.Center;
        }

        private void InitializeToastTimer()
        {
            _toastTimer = new DispatcherTimer();
            _toastTimer.Interval = TimeSpan.FromMilliseconds(AppConsts.ToastDuration);
            _toastTimer.Tick += (s, e) => HideToast(); // 计时结束触发淡出
        }

        public void ShowToast(string messageOrKey, Exception ex = null)
        {
            if (_toastTimer == null) InitializeToastTimer();
            _toastTimer.Stop();
            string message = LocalizationManager.GetString(messageOrKey);

            if (ex != null)
            {
                Logger.Error($"[ToastError] {message} (Key: {messageOrKey})", ex);
            }
            else
            {
                // 即使没有 Exception，如果是报错类的 Key，也按 Error 记录
                bool isError = messageOrKey.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                               messageOrKey.Contains("Failed", StringComparison.OrdinalIgnoreCase) ||
                               messageOrKey.StartsWith("L_Error_", StringComparison.OrdinalIgnoreCase) ||
                               messageOrKey.Contains("Exception", StringComparison.OrdinalIgnoreCase);

                if (isError) Logger.Error($"[ToastError] {message} (Key: {messageOrKey})");
                else Logger.Info($"[ToastInfo] {message} (Key: {messageOrKey})");
            }

            InfoToastText.Text = message;

            if (InfoToast.Opacity < 1.0)
            {
                InfoToast.BeginAnimation(OpacityProperty, null);

                DoubleAnimation fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(AppConsts.ToastFadeInMs));
                fadeIn.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
                InfoToast.BeginAnimation(OpacityProperty, fadeIn);
            }
            _toastTimer.Start();
        }
        private void HideToast()
        {
            _toastTimer.Stop(); // 停止计时器

            DoubleAnimation fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(AppConsts.ToastFadeOutMs));
            fadeOut.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn };

            InfoToast.BeginAnimation(OpacityProperty, fadeOut);
        }

        private DateTime _navKeyPressStartTime = DateTime.MinValue;
        private bool _isNavigating = false;
        private const int ViewModeNavHoldInitialDelayMs = 400;
        private const int ViewModeNavHoldRepeatIntervalMs = 85;

        private void EnsureViewModeNavHoldTimer()
        {
            if (_viewModeNavHoldTimer != null) return;

            _viewModeNavHoldTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ViewModeNavHoldInitialDelayMs)
            };
            _viewModeNavHoldTimer.Tick += OnViewModeNavHoldTimerTick;
        }

        private static int GetViewModeNavDirectionFromSender(object sender)
        {
            if (sender is Button button)
            {
                if (button.Name == "PrevImageButton") return -1;
                if (button.Name == "NextImageButton") return 1;
            }
            return 0;
        }

        private void StartViewModeNavHold(int direction)
        {
            if (direction == 0) return;

            EnsureViewModeNavHoldTimer();
            _viewModeNavHoldDirection = direction;
            _viewModeNavHoldStartedRepeating = false;
            _viewModeNavHoldTimer!.Interval = TimeSpan.FromMilliseconds(ViewModeNavHoldInitialDelayMs);
            _viewModeNavHoldTimer.Start();
        }

        private void StopViewModeNavHold()
        {
            if (_viewModeNavHoldTimer != null) _viewModeNavHoldTimer.Stop();
            _viewModeNavHoldDirection = 0;
            _viewModeNavHoldStartedRepeating = false;
        }

        private void OnViewModeNavHoldTimerTick(object? sender, EventArgs e)
        {
            if (_viewModeNavHoldDirection == 0)
            {
                StopViewModeNavHold();
                return;
            }

            MoveImageIndex(_viewModeNavHoldDirection);

            if (!_viewModeNavHoldStartedRepeating && _viewModeNavHoldTimer != null)
            {
                _viewModeNavHoldStartedRepeating = true;
                _viewModeNavHoldTimer.Interval = TimeSpan.FromMilliseconds(ViewModeNavHoldRepeatIntervalMs);
            }
        }

        private void OnViewModeNavButtonPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            int direction = GetViewModeNavDirectionFromSender(sender);
            if (direction == 0) return;

            // 按下先切一张，随后进入长按连发。
            MoveImageIndex(direction);
            _suppressViewModeNavClickOnce = true;
            StartViewModeNavHold(direction);
        }

        private void OnViewModeNavButtonPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            StopViewModeNavHold();
        }

        private void OnViewModeNavButtonMouseLeave(object sender, MouseEventArgs e)
        {
            StopViewModeNavHold();
        }

        private void OnViewModeNavButtonLostMouseCapture(object sender, MouseEventArgs e)
        {
            StopViewModeNavHold();
        }

        private int CalculateNavigationGap()
        {
            if (_navKeyPressStartTime == DateTime.MinValue) return 1;

            var duration = (DateTime.Now - _navKeyPressStartTime).TotalMilliseconds;

            if (duration < AppConsts.NavGapLevel1Ms) return 1;
            if (duration < AppConsts.NavGapLevel2Ms) return 2;
            if (PerformanceScore > AppConsts.HighPerformanceThreshold) return 5; else return 3;
        }


        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
                return string.Format(LocalizationManager.GetString("L_Format_Size_B"), bytes);
            if (bytes < 1024 * 1024)
                return string.Format(LocalizationManager.GetString("L_Format_Size_KB"), bytes / 1024.0);
            return string.Format(LocalizationManager.GetString("L_Format_Size_MB"), bytes / 1024.0 / 1024.0);
        }

        private void OnViewModePrevImageClick(object sender, RoutedEventArgs e)
        {
            if (_suppressViewModeNavClickOnce)
            {
                _suppressViewModeNavClickOnce = false;
                e.Handled = true;
                return;
            }
            ShowPrevImage();
            e.Handled = true;
        }

        private void OnViewModeNextImageClick(object sender, RoutedEventArgs e)
        {
            if (_suppressViewModeNavClickOnce)
            {
                _suppressViewModeNavClickOnce = false;
                e.Handled = true;
                return;
            }
            ShowNextImage();
            e.Handled = true;
        }

        private void ShowNextImage() { MoveImageIndex(1); }
        private void ShowPrevImage() { MoveImageIndex(-1);  }
        private void MoveImageIndex(int direction) // direction: 1 or -1
        {
            if (_imageFiles.Count == 0 || _currentImageIndex < 0 || FileTabs == null) return;
            if (FileTabs.Count < 2) return;
            _router.CleanUpSelectionandShape();
            if (_isEdited && !string.IsNullOrEmpty(_currentFilePath))
            {

                SaveBitmap(_currentFilePath, allowIcoOptionsDialog: false);
                _isEdited = false;
            }

            int gap = CalculateNavigationGap();
            int actualStep = gap * direction;
            int newIndex = _currentImageIndex + actualStep;

            if (newIndex >= _imageFiles.Count)
            {
                newIndex = newIndex % _imageFiles.Count; // 循环回到开头附近
                if (gap == 1)
                {
                    ShowToast("L_Toast_FirstImage");
                    TriggerNavButtonAnimation(NextImageButton);
                }
            }
            else if (newIndex < 0)
            {
                newIndex = (_imageFiles.Count + (newIndex % _imageFiles.Count)) % _imageFiles.Count;
                if (gap == 1)
                {
                    ShowToast("L_Toast_LastImage");
                    TriggerNavButtonAnimation(PrevImageButton);
                }
            }

            _currentImageIndex = newIndex;

            RequestImageLoad(_imageFiles[_currentImageIndex]);
            ScrollToTabCenter(_currentTabItem ?? FileTabs.FirstOrDefault(t => t.FilePath == _imageFiles[newIndex]));
        }


        private string SaveClipboardImageToCache(BitmapSource source)
        {
            try
            {
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
            catch   { return null;   }
        }

        private async Task InsertImagesToTabs(string[] files)
        {
            if (files == null || files.Length == 0) return;

            int insertIndex = _imageFiles.Count; // 默认插到最后
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
                // 1. 跨窗口互斥检查
                var (existingWindow, existingTab) = FindWindowHostingFile(file);
                if (existingWindow != null && existingTab != null)
                {
                    existingWindow.FocusAndSelectTab(existingTab);
                    continue;
                }

                // 2. 去重检查
                if (_imageFiles.Contains(file)) continue;
                _imageFiles.Insert(insertIndex + addedCount, file);

                var newTab = new FileTabItem(file);
                newTab.IsLoading = true;

                if (uiInsertIndex + addedCount <= FileTabs.Count)
                    FileTabs.Insert(uiInsertIndex + addedCount, newTab);
                else
                    FileTabs.Add(newTab);
                _ = newTab.LoadThumbnailAsync(AppConsts.DefaultThumbnailWidth, AppConsts.DefaultThumbnailHeight); // 异步加载缩略图
                if (firstNewTab == null) firstNewTab = newTab;
                addedCount++;
            }

            if (addedCount > 0)
            {
                // 更新 Slider 范围
                ImageFilesCount = _imageFiles.Count;
                SetPreviewSlider();

                if (firstNewTab != null)
                {
                    if (_currentTabItem != null) _currentTabItem.IsSelected = false;

                    firstNewTab.IsSelected = true;
                    _currentTabItem = firstNewTab;
                    await OpenImageAndTabs(firstNewTab.FilePath);

                    // 滚动 ImageBar
                    MainImageBar.Scroller.ScrollToHorizontalOffset(MainImageBar.Scroller.HorizontalOffset + 1);
                }
            }
        }
        private void ScrollContainer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            CheckBirdEyeVisibility();
            UpdateBirdEyeView();
            UpdateRulerPositions(); ClampScrollBarThumbSize();
        }
        private void ClampScrollBarThumbSize()
        {
            const double minThumbRatio = 0.06;

            // 获取 ScrollViewer 内部的 ScrollBar
            var vBar = ScrollContainer.Template.FindName("PART_VerticalScrollBar", ScrollContainer) as System.Windows.Controls.Primitives.ScrollBar;
            var hBar = ScrollContainer.Template.FindName("PART_HorizontalScrollBar", ScrollContainer) as System.Windows.Controls.Primitives.ScrollBar;

            if (vBar != null && vBar.Maximum > 0)
            {
                double minViewport = vBar.Maximum * minThumbRatio / (1.0 - minThumbRatio);
                if (vBar.ViewportSize < minViewport)
                    vBar.ViewportSize = minViewport;
            }

            if (hBar != null && hBar.Maximum > 0)
            {
                double minViewport = hBar.Maximum * minThumbRatio / (1.0 - minThumbRatio);
                if (hBar.ViewportSize < minViewport)
                    hBar.ViewportSize = minViewport;
            }
        }

        private long GetOverlayRefreshIntervalTicks()
        {
            int score = Math.Max(1, PerformanceScore);
            int targetFps = score <= 3 ? 20 : (score <= 6 ? 30 : 60);
            return Math.Max(1, Stopwatch.Frequency / targetFps);
        }

        private int GetPerformanceParallelism(bool preferResponsive = true)
        {
            int score = Math.Max(1, PerformanceScore);
            int cpu = Math.Max(1, Environment.ProcessorCount);

            int degree = score <= 3
                ? Math.Max(1, cpu / 2)
                : (score <= 6 ? Math.Max(1, cpu - 1) : cpu);

            if (preferResponsive && degree > 1) degree--;
            return Math.Max(1, degree);
        }

        public ParallelOptions CreatePerformanceParallelOptions(bool preferResponsive = true)
            => new ParallelOptions { MaxDegreeOfParallelism = GetPerformanceParallelism(preferResponsive) };

        public void UpdateRulerPositions(bool force = false)
        {
            if (!SettingsManager.Instance.Current.ShowRulers || BackgroundImage == null || BackgroundImage.Source == null) return;

            // 获取图像原始大小
            double w = BackgroundImage.Source.Width;
            double h = BackgroundImage.Source.Height;

            // 获取 CanvasWrapper 在 ScrollContainer 坐标系中的变换。
            // 包含缩放和旋转。
            var transform = CanvasWrapper.TransformToAncestor(ScrollContainer);

            // 计算图像四个角在 ScrollContainer 中的位置
            Point p0 = transform.Transform(new Point(0, 0));
            Point p1 = transform.Transform(new Point(w, 0));
            Point p2 = transform.Transform(new Point(w, h));
            Point p3 = transform.Transform(new Point(0, h));

            // 取最小坐标作为标尺原点，确保图像始终在正数区域（视觉上）
            double originX = Math.Min(Math.Min(p0.X, p1.X), Math.Min(p2.X, p3.X));
            double originY = Math.Min(Math.Min(p0.Y, p1.Y), Math.Min(p2.Y, p3.Y));

            double currentZoom = ZoomTransform.ScaleX;

            RulerTop.OriginOffset = originX;
            RulerTop.ZoomFactor = currentZoom;
            RulerTop.InvalidateVisual();

            RulerLeft.OriginOffset = originY;
            RulerLeft.ZoomFactor = currentZoom;
            RulerLeft.InvalidateVisual();
        }
        public static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T) return (T)child;
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
        private void RefreshBitmapScalingMode()
        {
            if (BackgroundImage == null) return; // 防止空引用

            var settings = TabPaint.SettingsManager.Instance.Current;
            double threshold = (IsViewMode ? settings.ViewInterpolationThreshold : settings.PaintInterpolationThreshold) / 100.0;
            if (zoomscale >= threshold)
            {
                if (RenderOptions.GetBitmapScalingMode(BackgroundImage) != BitmapScalingMode.NearestNeighbor) RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.NearestNeighbor);
            }
            else
            {
                if (RenderOptions.GetBitmapScalingMode(BackgroundImage) != BitmapScalingMode.Linear) RenderOptions.SetBitmapScalingMode(BackgroundImage, BitmapScalingMode.Linear);
            }
        }
        public void UpdateDwmBorderColor()
        {
            DwmBorderHelper.UpdateWindowBorder(this);
        }
        private void OnScrollContainerMouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                Point currentPos = e.GetPosition(ScrollContainer);
                double deltaX = _lastMousePosition.X - currentPos.X;   // 计算偏移量
                double deltaY = _lastMousePosition.Y - currentPos.Y;

                ScrollContainer.ScrollToHorizontalOffset(ScrollContainer.HorizontalOffset + deltaX);
                ScrollContainer.ScrollToVerticalOffset(ScrollContainer.VerticalOffset + deltaY);
                _lastMousePosition = currentPos;
            }
            if (SettingsManager.Instance.Current.ShowRulers)
            {
                Point pos = e.GetPosition(ScrollContainer);
                RulerTop.MouseMarker = pos.X;
                RulerLeft.MouseMarker = pos.Y;
            }
        }
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateRulerPositions();
        }
        private void OnScrollContainerMouseUp(object sender, MouseButtonEventArgs e)
        {
            // 1. 处理右键菜单加载
            if (e.ChangedButton == MouseButton.Right)
            {
                if (IsViewMode) return;

                // 如果菜单还没加载过，进行加载
                if (ScrollContainer.ContextMenu == null) LoadCanvasContextMenu();// 再次检查（因为加载可能失败），如果成功则打开
                if (ScrollContainer.ContextMenu != null)
                {
                    _lastRightClickPosition = Mouse.GetPosition(BackgroundImage);
                    ScrollContainer.ContextMenu.PlacementTarget = ScrollContainer; // 确保定位准确
                    ScrollContainer.ContextMenu.IsOpen = true;
                }
            }
            if (_isPanning)
            {
                _isPanning = false;
                ScrollContainer.ReleaseMouseCapture();
                SetViewCursor(false);
            }

            if (_isLoadingImage) return;
            if (!IsViewMode)
            {
                if (Mouse.OverrideCursor != null) Mouse.OverrideCursor = null;
                Point pos = e.GetPosition(CanvasWrapper);
                _router.ViewElement_MouseUp(pos, e);
            }
        }
        private void LoadCanvasContextMenu()
        {
            try
            {
                var resourceUri = new Uri("pack://application:,,,/Controls/ContextMenus/CanvasMenu.xaml");
                var dictionary = new ResourceDictionary { Source = resourceUri };
                var menu = dictionary["MainImageCtxMenu"] as ContextMenu;

                if (menu != null)
                {
                    // 确保资源字典被加入到菜单的资源中，这样 DynamicResource 才能在语言切换时正常工作
                    menu.Resources.MergedDictionaries.Add(dictionary);

                    foreach (var item in menu.Items)
                    {
                        BindCanvasMenuEvents(item);
                    }
                    ScrollContainer.ContextMenu = menu;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"主菜单加载失败: {ex.Message}");
            }
        }
        public void UpdateRulerSelection()
        {
            if (RulerTop == null || RulerLeft == null) return;

            var selectTool = _router?.CurrentTool as SelectTool;
            if (selectTool == null)
                selectTool = _tools?.Select as SelectTool;

            if (selectTool != null && selectTool.HasRulerHighlight)
            {
                var rect = selectTool.SelectionRect;

                if (rect.Width > 0 && rect.Height > 0)
                {
                    // 将原始坐标系的选区矩形转换为视觉坐标系（考虑旋转）
                    // 我们需要得到在视觉上相对于“视觉左上角”的坐标
                    
                    // 获取视觉包围盒
                    var visualBounds = CanvasWrapper.LayoutTransform.TransformBounds(new Rect(rect.X, rect.Y, rect.Width, rect.Height));
                    
                    // 注意：标尺的 0 刻度现在已经通过 UpdateRulerPositions 修正到了视觉左上角
                    // 所以这里的逻辑坐标也需要映射到视觉包围盒相对于图像视觉起始点的位置。
                    
                    // 计算整个图像的视觉包围盒
                    double w = BackgroundImage.Source.Width;
                    double h = BackgroundImage.Source.Height;
                    var imageVisualBounds = CanvasWrapper.LayoutTransform.TransformBounds(new Rect(0, 0, w, h));

                    // 转换后的坐标是相对于 imageVisualBounds.TopLeft 的
                    double visualX = visualBounds.X - imageVisualBounds.X;
                    double visualY = visualBounds.Y - imageVisualBounds.Y;

                    double zoom = ZoomTransform.ScaleX;
                    RulerTop.SelectionStart = visualX / zoom;
                    RulerTop.SelectionEnd = (visualX + visualBounds.Width) / zoom;

                    RulerLeft.SelectionStart = visualY / zoom;
                    RulerLeft.SelectionEnd = (visualY + visualBounds.Height) / zoom;
                    return;
                }
            }
            ClearRulerSelection();
        }

        public void ClearRulerSelection()
        {
            if (RulerTop != null)
            {
                RulerTop.SelectionStart = -1;
                RulerTop.SelectionEnd = -1;
            }
            if (RulerLeft != null)
            {
                RulerLeft.SelectionStart = -1;
                RulerLeft.SelectionEnd = -1;
            }
        }
        private void BindCanvasMenuEvents(object item)
        {
            if (item is MenuItem menuItem)
            {
                menuItem.Click -= OnCanvasMenuClickDispatcher;
                if (menuItem.Tag != null)
                {
                    switch (menuItem.Tag.ToString())
                    {
                        case "Copy": menuItem.Click += OnCopyClick; break;
                        case "Cut": menuItem.Click += OnCutClick; break;
                        case "Paste": menuItem.Click += OnPasteClick; break;
                        case "RemoveBackground": menuItem.Click += OnRemoveBackgroundClick; break;
                        case "ChromaKey": menuItem.Click += OnChromaKeyClick; break;
                        case "Ocr": menuItem.Click += OnOcrClick; break;
                        case "ScreenColorPicker": menuItem.Click += OnScreenColorPickerClick; break;
                        case "CopyColorCode": menuItem.Click += OnCopyColorCodeClick; break;
                        case "AutoCrop": menuItem.Click += OnAutoCropClick; break;
                        case "AddBorder": menuItem.Click += OnAddBorderClick; break;
                        case "AiUpscale": menuItem.Click += OnAiUpscaleClick; break;
                        case "AiOcr": menuItem.Click += OnAiOcrClick; break;
                    }
                }
                if (menuItem.Items.Count > 0)
                {
                    foreach (var subItem in menuItem.Items)
                    {
                        BindCanvasMenuEvents(subItem);
                    }
                }
            }
            else if (item is TabPaint.Controls.DelayedMenuItem delayedItem)
            {
                foreach (var subItem in delayedItem.Items)
                {
                    BindCanvasMenuEvents(subItem);
                }
            }
        }
        private void OnCanvasMenuClickDispatcher(object sender, RoutedEventArgs e){ }
        private bool _isDragOverlayVisible = false;

        public void UpdateSelectionScalingMode()
        {

            if (SelectionPreview == null) return;

            double currentZoomPercent = ZoomTransform.ScaleX * 100.0;
            double threshold = SettingsManager.Instance.Current.PaintInterpolationThreshold;

            var mode = (currentZoomPercent >= threshold)
                ? BitmapScalingMode.NearestNeighbor
                : BitmapScalingMode.Linear;

            if (RenderOptions.GetBitmapScalingMode(SelectionPreview) != mode)    RenderOptions.SetBitmapScalingMode(SelectionPreview, mode);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            StopViewModeNavHold();
            if (!_programClosed)
            {
                int otherVisibleWindows = Application.Current.Windows.OfType<MainWindow>().Count(w => w != this && w.IsVisible);
                if (otherVisibleWindows == 0)
                {
                    bool hasVisibleSticky = Application.Current.Windows.OfType<TabPaint.Windows.StickyWindow>().Any(w => w.IsVisible);
                    if (hasVisibleSticky)
                    {
                        OnClosing();
                    }
                    else
                    {
                        App.GlobalExit();
                    }
                }
                else
                {
                    OnClosing();
                }
            }
        }
        private void OnTitleBarCloseClick(object sender, RoutedEventArgs e)
        {
            int mainWindowCount = Application.Current.Windows
        .OfType<MainWindow>()
        .Count(w => w != this); // 排除自己

            if (mainWindowCount == 0)
            {
                bool hasVisibleSticky = Application.Current.Windows.OfType<TabPaint.Windows.StickyWindow>().Any(w => w.IsVisible);
                if (hasVisibleSticky)
                {
                    this.Close();
                }
                else
                {
                    App.GlobalExit();
                }
            }
            else
                this.Close();
        }

        private void OnTitleBarMinimizeClick(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void OnTitleBarMaximizeRestoreClick(object sender, RoutedEventArgs e)
        {
            MaximizeWindowHandler();
        }
        public bool IsTransferringSelection { get; private set; } = false;

        // 暂存传输的选区数据
        private byte[] _transferSelectionData;
        private int _transferWidth;
        private int _transferHeight;

        public async void TransferSelectionToTab(FileTabItem targetTab, byte[] selectionData, int width, int height)
        {
            if (targetTab == null || targetTab == _currentTabItem) return;

            IsTransferringSelection = true;
            _transferSelectionData = selectionData;
            _transferWidth = width;
            _transferHeight = height;
            try
            {
                SwitchToTab(targetTab);
            }
            finally
            {
            }
        }
        public void RestoreTransferredSelection()
        {
            if (!IsTransferringSelection || _transferSelectionData == null || _surface == null) return;

            try
            {
                // 1. 构造 BitmapSource
                var bmp = BitmapSource.Create(_transferWidth, _transferHeight,
                    _surface.Bitmap.DpiX, _surface.Bitmap.DpiY,
                    PixelFormats.Bgra32, null, _transferSelectionData, _transferWidth * 4);

                SelectTool st = _router.GetSelectTool();
                if (_router.CurrentTool != st)  _router.SetTool(st);// 确保切换到 Select 工具
                st._selectionData = null;
                st._selectionRect = new Int32Rect(0, 0, 0, 0);
                if (_ctx.SelectionPreview != null)
                {
                    _ctx.SelectionPreview.Visibility = Visibility.Collapsed;
                    _ctx.SelectionPreview.Source = null;
                }
                st.InsertImageAsSelection(_ctx, bmp, expandCanvas: true);

                st.ForceDragState(this);

                NotifyCanvasChanged();
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_RestoreSelectionFailed_Prefix"), ex.Message), ex);
            }
            finally
            {
                IsTransferringSelection = false;
                _transferSelectionData = null;
            }
        }
        private void SelectionToolBar_CopyClick(object sender, RoutedEventArgs e)
        {
            if (_router.CurrentTool is SelectTool selectTool)
            {
                selectTool.CopySelection(_ctx);
                ShowToast("L_Main_Ctx_Copy");
            }
        }

        private void SelectionToolBar_AiRemoveBgClick(object sender, RoutedEventArgs e)
        {
            OnRemoveBackgroundClick(sender, e); 
            if (SelectionToolHolder != null) SelectionToolHolder.Visibility = Visibility.Collapsed;
        }

        private void SelectionToolBar_OcrClick(object sender, RoutedEventArgs e)
        {
            OnOcrClick(sender, e);
            if (SelectionToolHolder != null) SelectionToolHolder.Visibility = Visibility.Collapsed;
        }

        private void SelectionToolBar_RotateClick(object sender, RoutedEventArgs e)
        {
            var settings = SettingsManager.Instance.Current;
            settings.IsSelectionRotateEnabled = !settings.IsSelectionRotateEnabled;

            if (settings.IsSelectionRotateEnabled)
            {
                SelectionRotatePopup.SetValue(0);
                SelectionRotatePopup.Visibility = Visibility.Visible;
                if (_router?.CurrentTool is SelectTool st)
                {
                    st.PrepareRotation(_ctx);
                }
            }
            else
            {
                SelectionRotatePopup.Visibility = Visibility.Collapsed;
            }
            SettingsManager.Instance.Save();
            UpdateSelectionToolBarPosition();
        }

        private void SelectionRotatePopup_AngleChanged(object sender, RoutedEventArgs e)
        {
            if (SelectionRotatePopup != null)
            {
                int angle = SelectionRotatePopup.CurrentAngle;
                if (_router?.CurrentTool is SelectTool st)
                {
                    st.UpdateRotation(_ctx, angle, false);
                }
            }
        }

        public void ToggleProfessionalMode()
        {
            var settings = SettingsManager.Instance.Current;
            settings.IsProfessionalMode = !settings.IsProfessionalMode;

            string toastKey = settings.IsProfessionalMode ? "L_Toast_ProMode_On" : "L_Toast_ProMode_Off";
            ShowToast(toastKey);

            // 统一刷新所有工具的专业模式 UI
            UpdateSelectionToolBarPosition(force: true);
            
            // 无论当前是什么工具，都尝试刷新可能存在的覆盖层
            if (_router?.CurrentTool is TextTool textTool)
            {
                textTool.DrawTextboxOverlay(_ctx);
            }
            else if (_router?.CurrentTool is ShapeTool shapeTool)
            {
                shapeTool.RefreshPreview(_ctx);
            }

            SettingsManager.Instance.Save();
        }

        public void UpdateSelectionToolBarPosition(bool force = false)
        {
            // 如果还没初始化且当前没有选区，直接返回，避免不必要的实例化
            var selectTool = _router?.CurrentTool as SelectTool; if (selectTool == null) return;
            var holder = this.FindName("SelectionToolHolder") as ContentControl;
            if (holder == null) return;
            var rotateHolder = this.FindName("SelectionRotateHolder") as ContentControl;

            var settings = SettingsManager.Instance.Current;

            double viewportArea = ScrollContainer.ViewportWidth * ScrollContainer.ViewportHeight;
            double selectionScreenArea = (selectTool._selectionRect.Width * zoomscale) * (selectTool._selectionRect.Height * zoomscale);
            bool shouldShow = !IsViewMode
                              && settings.IsProfessionalMode
                              && selectTool.HasActiveSelection
                              && (viewportArea > 0 && (selectionScreenArea / viewportArea) > 0.015);

            if (shouldShow)
            {
                // 确保工具栏已加载
                if (holder.Content == null) { var bar = this.SelectionToolBar; }
                this.SelectionToolBar.IsRotateChecked = settings.IsSelectionRotateEnabled;
                if (settings.IsSelectionRotateEnabled && SelectionRotatePopup.Visibility != Visibility.Visible)
                {
                    SelectionRotatePopup.SetValue(0);
                    SelectionRotatePopup.Visibility = Visibility.Visible;
                    selectTool.PrepareRotation(_ctx);
                }

                Int32Rect rect = selectTool._selectionRect;
                // 旋转过程中使用稳定的原始矩形作为定位基准，避免工具栏随包围盒跳动
                if (selectTool._preRotationSelectionData != null) rect = selectTool._preRotationRect;

                if (rect.Width <= 0 || rect.Height <= 0)
                {
                    holder.Visibility = Visibility.Collapsed;
                    if (rotateHolder != null) rotateHolder.Visibility = Visibility.Collapsed;
                    return;
                }

                Point p1 = _ctx.FromPixel(new Point(rect.X, rect.Y));
                Point p2 = _ctx.FromPixel(new Point(rect.X + rect.Width, rect.Y + rect.Height));

                Point rootPos = CanvasWrapper.TranslatePoint(p1, (UIElement)this.Content);
                Point rootPosEnd = CanvasWrapper.TranslatePoint(p2, (UIElement)this.Content);

                double selTop = rootPos.Y;
                double selLeft = rootPos.X;
                double selWidth = rootPosEnd.X - rootPos.X;
                double toolbarHeight = 45;
                double toolbarWidth = 160;

                double top = selTop - toolbarHeight - 10;
                double left = selLeft + (selWidth - toolbarWidth) / 2;

                if (top < 40) top = rootPosEnd.Y + 10;
                if (top + toolbarHeight > this.ActualHeight - 20) top = this.ActualHeight - toolbarHeight - 20;

                if (left < 10) left = 10;
                if (left + toolbarWidth > this.ActualWidth - 10) left = this.ActualWidth - toolbarWidth - 10;
                holder.Margin = new Thickness(left, top, 0, 0);
                holder.Visibility = Visibility.Visible;

                // 旋转控制面板跟随
                if (rotateHolder != null && _selectionRotatePopup != null && _selectionRotatePopup.Visibility == Visibility.Visible)
                {
                    double rotateHeight = 40;
                    double rotateWidth = 280;

                    // 默认放在选区底部下方
                    double rTop = rootPosEnd.Y + 10;

                    // 如果工具栏已经因为上方空间不足而移到了选区下方，则旋转面板顺延到工具栏下方
                    if (top >= rootPosEnd.Y)
                    {
                        rTop = top + toolbarHeight + 5;
                    }

                    double rLeft = selLeft + (selWidth - rotateWidth) / 2;

                    // 底部边界检查：如果下方放不下，则翻转到上方
                    if (rTop + rotateHeight > this.ActualHeight - 20)
                    {
                        if (top < selTop) // 工具栏在上方
                        {
                            rTop = top - rotateHeight - 5; // 放在工具栏上方
                        }
                        else
                        {
                            rTop = selTop - rotateHeight - 10; // 放在选区上方
                        }
                    }

                    if (rLeft < 10) rLeft = 10;
                    if (rLeft + rotateWidth > this.ActualWidth - 10) rLeft = this.ActualWidth - rotateWidth - 10;

                    rotateHolder.Margin = new Thickness(rLeft, rTop, 0, 0);
                    rotateHolder.Visibility = Visibility.Visible;
                }
                else if (rotateHolder != null)
                {
                    rotateHolder.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                holder.Visibility = Visibility.Collapsed;
                if (rotateHolder != null) rotateHolder.Visibility = Visibility.Collapsed;
            }
        }

        public void SyncTextToolbarState(RichTextBox rtb)

        {
            var selection = rtb.Selection;
            var weight = selection.GetPropertyValue(TextElement.FontWeightProperty);// 字体粗细
            TextMenu.BoldBtn.IsChecked = (weight != DependencyProperty.UnsetValue) && ((FontWeight)weight == FontWeights.Bold);
            var style = selection.GetPropertyValue(TextElement.FontStyleProperty);// 斜体
            TextMenu.ItalicBtn.IsChecked = (style != DependencyProperty.UnsetValue) && ((FontStyle)style == FontStyles.Italic);

            // 下划线/删除线
            var decor = selection.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;
            TextMenu.UnderlineBtn.IsChecked = false;
            TextMenu.StrikeBtn.IsChecked = false;
            if (decor != null)
            {
                // 简单判断，实际可能需要遍历
                foreach (var d in decor)
                {
                    if (d.Location == TextDecorationLocation.Underline) TextMenu.UnderlineBtn.IsChecked = true;
                    if (d.Location == TextDecorationLocation.Strikethrough) TextMenu.StrikeBtn.IsChecked = true;
                }
            }
            var baseline = selection.GetPropertyValue(Inline.BaselineAlignmentProperty);// 上下标
            TextMenu.SubscriptBtn.IsChecked = false;
            TextMenu.SuperscriptBtn.IsChecked = false;
            if (baseline != DependencyProperty.UnsetValue)
            {
                var bl = (BaselineAlignment)baseline;
                if (bl == BaselineAlignment.Subscript) TextMenu.SubscriptBtn.IsChecked = true;
                if (bl == BaselineAlignment.Superscript) TextMenu.SuperscriptBtn.IsChecked = true;
            }
            var bg = selection.GetPropertyValue(TextElement.BackgroundProperty);// 高亮
            TextMenu.HighlightBtn.IsChecked = (bg != null && bg != DependencyProperty.UnsetValue && bg != Brushes.Transparent);
            TextMenu.ShadowBtn.IsChecked = (rtb.Effect is System.Windows.Media.Effects.DropShadowEffect);// 阴影
        }

        private void UpdateImageBarVisibilityState()
        {
            if (MainImageBar == null || FileTabs == null) return;


            bool isSingle = FileTabs.Count <= 1;
            if (MainImageBar.IsSingleTabMode != isSingle)
            {
                MainImageBar.IsSingleTabMode = isSingle;
                CheckFittoWindow();
            }
        }
        private UIHandlers.FavoriteWindow _favoriteWindow;
        public void ToggleFavoriteWindow()
        {
            if (_favoriteWindow == null)
            {
                _favoriteWindow = new UIHandlers.FavoriteWindow();
                _favoriteWindow.FavoriteContent.ImageSelected += async (path) =>
                {
                    if (AppConsts.IsSupportedImage(path))
                    {
                        await OpenFilesAsNewTabs(new string[] { path });
                    }
                };
                _favoriteWindow.IsVisibleChanged += (s, e) =>
                {
                    if (MyStatusBar != null)
                    {
                        MyStatusBar.SetFavoriteToggleState(_favoriteWindow.IsVisible);
                    }
                };
            }
            FavoriteWindowManager.Toggle(this);
        }

        private void TriggerNavButtonAnimation(Button button)
        {
            if (button == null) return;
            var sb = FindResource("CyclePulseAnimation") as Storyboard;
            if (sb != null)
            {
                sb.Begin(button);
            }
        }
    }
}
