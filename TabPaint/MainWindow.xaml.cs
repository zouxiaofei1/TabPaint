using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
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

        public MainWindow(string startFilePath)///////////////////////////////////////////////////主窗口初始化
        {

            _currentFilePath = startFilePath;
            InitializeComponent();


            Loaded += (s, e) =>
            {
              
                LoadSession();
                MicaAcrylicManager.ApplyEffect(this);
            };
            Loaded += MainWindow_Loaded;
            InitializeAutoSave();
            this.Show();

            DataContext = this;
            // LoadAllFilePaths(basepath);

            StateChanged += MainWindow_StateChanged; // 监听系统状态变化
            Select = new SelectTool();
            this.Deactivated += MainWindow_Deactivated;
            // 初始化字体大小事件
            FontFamilyBox.SelectionChanged += FontSettingChanged;
            FontSizeBox.SelectionChanged += FontSettingChanged;
            BoldBtn.Checked += FontSettingChanged;
            BoldBtn.Unchecked += FontSettingChanged;
            ItalicBtn.Checked += FontSettingChanged;
            ItalicBtn.Unchecked += FontSettingChanged;
            UnderlineBtn.Checked += FontSettingChanged;
            UnderlineBtn.Unchecked += FontSettingChanged;

            SourceInitialized += OnSourceInitialized;

            ZoomSlider.ValueChanged += (s, e) =>
            {
                UpdateSliderBarValue(ZoomSlider.Value);
                //ZoomScale = ZoomSlider.Value; // 更新属性而不是直接访问 zoomscale
            };

            CanvasWrapper.MouseDown += OnCanvasMouseDown;
            CanvasWrapper.MouseMove += OnCanvasMouseMove;
            CanvasWrapper.MouseUp += OnCanvasMouseUp;

            // 3. (Failsafe) Handle the mouse leaving the element
            CanvasWrapper.MouseLeave += OnCanvasMouseLeave;
            _surface = new CanvasSurface(_bitmap);
            _undo = new UndoRedoManager(_surface);
            _ctx = new ToolContext(_surface, _undo, BackgroundImage, SelectionPreview, SelectionOverlayCanvas, EditorOverlayCanvas, CanvasWrapper);
            _tools = new ToolRegistry();
            _ctx.ViewElement.Cursor = _tools.Pen.Cursor;
            _router = new InputRouter(_ctx, _tools.Pen); // 默认画笔
            this.PreviewKeyDown += (s, e) =>
            {
                // 保持原有 Ctrl+Z/Y/S/N/O 与方向键导航逻辑
                MainWindow_PreviewKeyDown(s, e);
                // 再路由给当前工具（例如文本工具用键盘输入）
                _router.OnPreviewKeyDown(s, e);
            };
            SetBrushStyle(BrushStyle.Round);
            SetCropButtonState();
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;


            _canvasResizer = new CanvasResizeManager(this);

            this.Focusable = true;
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
            string[] validExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".ico", ".tiff" };
            string ext = System.IO.Path.GetExtension(path)?.ToLower();
            return validExtensions.Contains(ext);
        }
       


        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Focus();

            InitializeScrollPosition();

            Task.Run(async () => // 在后台线程运行，不阻塞UI线程
            {
                // await 
                await OpenImageAndTabs(_currentFilePath, true);
                System.Windows.Application.Current.Dispatcher.Invoke(() =>// 如果你需要在完成后通知UI，要切回UI线程
                {
                    InitializeScrollPosition();
                    _isInitialLayoutComplete = true;
                });
            });
        }
     


        private void FitToWindow(double addscale = 1)
        {
            if (BackgroundImage.Source != null)
            {
                double imgWidth = BackgroundImage.Source.Width;
                double imgHeight = BackgroundImage.Source.Height;
                //s(ScrollContainer.ViewportWidth);
                double viewWidth = ScrollContainer.ViewportWidth;
                double viewHeight = ScrollContainer.ViewportHeight;

                double scaleX = viewWidth / imgWidth;
                double scaleY = viewHeight / imgHeight;

                double fitScale = Math.Min(scaleX, scaleY); // 保持纵横比适应
                zoomscale = fitScale * addscale;
                // s(fitScale);
                ZoomTransform.ScaleX = ZoomTransform.ScaleY = zoomscale;
                UpdateSliderBarValue(zoomscale);
            }
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


        private void ShowToast(string message)
        {
            InfoToastText.Text = message;

            // 创建渐变动画
            DoubleAnimation fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(200));
            DoubleAnimation fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(500));
            fadeOut.BeginTime = TimeSpan.FromSeconds(1); // 停留1.5秒后开始消失

            // 播放动画
            InfoToast.BeginAnimation(OpacityProperty, null); // 清除之前的动画
            Storyboard sb = new Storyboard();
            Storyboard.SetTarget(fadeIn, InfoToast);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(OpacityProperty));

            Storyboard.SetTarget(fadeOut, InfoToast);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(OpacityProperty));

            sb.Children.Add(fadeIn);
            sb.Children.Add(fadeOut);
            sb.Begin();
        }


        private void ShowNextImage()
        {
            if (_imageFiles.Count == 0 || _currentImageIndex < 0) return;
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
       
    }

}
