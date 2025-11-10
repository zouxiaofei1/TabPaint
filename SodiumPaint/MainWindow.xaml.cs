using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

//
//SodiumPaint主程序
//

namespace SodiumPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {

        // MainWindow 类成员变量


        private CanvasSurface _surface;
        private UndoRedoManager _undo;
        public ToolContext _ctx;
        private InputRouter _router;
        private ToolRegistry _tools;
        private double zoomscale = 1;
        private byte[]? _preDrawSnapshot = null;

        private WriteableBitmap _bitmap;
        private int _bmpWidth, _bmpHeight;
        private Color _penColor = Colors.Black;
        private bool _isDrawing = false;
        private List<string> _imageFiles = new List<string>();
        private int _currentImageIndex = -1;
        private bool _isEdited = false; // 标记当前画布是否被修改
        private string _currentFileName = "未命名";
        private string _programVersion = "v0.5"; // 可以从 Assembly 读取
        private bool _isFileSaved = true; // 是否有未保存修改

        private string _mousePosition = "X:0, Y:0";
        public string MousePosition
        {
            get => _mousePosition;
            set { _mousePosition = value; OnPropertyChanged(); }
        }

        private string _imageSize = "0×0";
        public string ImageSize
        {
            get => _imageSize;
            set { _imageSize = value; OnPropertyChanged(); }
        }

        private string _selectionSize = "0×0";
        public string SelectionSize
        {
            get => _selectionSize;
            set { _selectionSize = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private double _penThickness = 5;
        public double PenThickness
        {
            get => _penThickness;
            set
            {
                if (_penThickness != value)
                {
                    _penThickness = value;
                    OnPropertyChanged(nameof(PenThickness));
                    if (_ctx != null) _ctx.PenThickness = value;
                }
            }
        }
        public enum BrushStyle { Round, Square, Brush, Spray, Pencil, Eraser, Watercolor, Crayon }
        public enum UndoActionType
        {
            Draw,         // 普通绘图
            Transform,    // 旋转/翻转
            CanvasResize, // 画布拉伸或缩放
            ReplaceImage  // 整图替换（打开新图）
        }
        public SelectTool Select;
        public SolidColorBrush ForegroundBrush { get; set; } = new SolidColorBrush(Colors.Black);
        public SolidColorBrush BackgroundBrush { get; set; } = new SolidColorBrush(Colors.White);
        // 当前画笔颜色属性，可供工具使用
        public SolidColorBrush SelectedBrush { get; set; } = new SolidColorBrush(Colors.Black);

        // 绑定到 ItemsControl 的预设颜色集合
        public ObservableCollection<SolidColorBrush> ColorItems { get; set; }
            = new ObservableCollection<SolidColorBrush>
            {
                new SolidColorBrush(Colors.Black),
                new SolidColorBrush(Colors.Gray),
                new SolidColorBrush(Colors.Red),
                new SolidColorBrush(Colors.Orange),
                new SolidColorBrush(Colors.Yellow),
                new SolidColorBrush(Colors.Green),
                new SolidColorBrush(Colors.Cyan),
                new SolidColorBrush(Colors.Blue),
                new SolidColorBrush(Colors.Purple),
                new SolidColorBrush(Colors.Brown),
                new SolidColorBrush(Colors.Pink),
                new SolidColorBrush(Colors.White)
            };

        private double _zoomScale = 1.0;
        private string _zoomLevel = "100%";
        public string ZoomLevel
        {
            get => _zoomLevel;
            set { _zoomLevel = value; OnPropertyChanged(); }
        }
        private System.Windows.Controls.TextBox? _activeTextBox;
        private Stack<UndoAction> _undoStack = new Stack<UndoAction>();
        private List<Int32Rect> _currentDrawRegions = new List<Int32Rect>(); // 当前笔的区域记录
        private Stack<UndoAction> _redoStack = new Stack<UndoAction>();
        String PicFilterString = "图像文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp";
        ITool LastTool;





        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // 各种ITool + InputRouter
        //已经被拆分到Itools文件夹中

        //MainWindow 类通用过程


        private void OnForegroundColorClick(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.ColorDialog();
            dlg.Color = System.Drawing.Color.FromArgb(ForegroundBrush.Color.A, ForegroundBrush.Color.R, ForegroundBrush.Color.G, ForegroundBrush.Color.B);
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ForegroundBrush = new SolidColorBrush(
                    Color.FromArgb(255, dlg.Color.R, dlg.Color.G, dlg.Color.B));
                DataContext = this; // 刷新绑定

                _ctx.PenColor = ForegroundBrush.Color;
                UpdateForegroundButtonColor(ForegroundBrush.Color);
            }
        }

        private void OnBackgroundColorClick(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.ColorDialog();
            dlg.Color = System.Drawing.Color.FromArgb(BackgroundBrush.Color.A, BackgroundBrush.Color.R, BackgroundBrush.Color.G, BackgroundBrush.Color.B);
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BackgroundBrush = new SolidColorBrush(
                    Color.FromArgb(255, dlg.Color.R, dlg.Color.G, dlg.Color.B));
                DataContext = this; // 刷新绑定
                UpdateBackgroundButtonColor(BackgroundBrush.Color);
            }
        }
        private void OnColorButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Background is SolidColorBrush brush)
            {
                SelectedBrush = new SolidColorBrush(brush.Color);

                // 如果你有 ToolContext，可同步笔颜色，例如：
                _ctx.PenColor = brush.Color;
                UpdateForegroundButtonColor(_ctx.PenColor);
            }
        }
        public void UpdateForegroundButtonColor(Color color) // 更新前景色按钮颜色
        {
            ForegroundBrush = new SolidColorBrush(color);
            OnPropertyChanged(nameof(ForegroundBrush)); // 通知绑定刷新
        }
        public void UpdateBackgroundButtonColor(Color color)    // 更新背景色按钮颜色（可选）
        {
            BackgroundBrush = new SolidColorBrush(color);
            OnPropertyChanged(nameof(BackgroundBrush)); // 通知绑定刷新
        }

        private void OnCustomColorClick(object sender, RoutedEventArgs e)// 点击彩虹按钮自定义颜色
        {
            var dlg = new ColorDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var color = Color.FromArgb(255, dlg.Color.R, dlg.Color.G, dlg.Color.B);
                var brush = new SolidColorBrush(color);
                SelectedBrush = brush;
                //HighlightSelectedButton(null);

                // 同步到绘图上下文
                _ctx.PenColor = color;
                UpdateForegroundButtonColor(color);
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {  // 工具函数 - 查找所有子元素
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
        private void OnBrushStyleClick(object sender, RoutedEventArgs e)
        {
            //  _currentTool = ToolMode.Pen;
            if (sender is System.Windows.Controls.MenuItem menuItem
                && menuItem.Tag is string tagString
                && Enum.TryParse(tagString, out BrushStyle style))
            {
                _router.SetTool(_tools.Pen);
                _ctx.PenStyle = style; // 你的画笔样式枚举
            }

            // 点击后关闭下拉按钮
            BrushToggle.IsChecked = false;
        }

        private void SetBrushStyle(BrushStyle style)
        {//设置画笔样式，所有画笔都是pen工具
            _router.SetTool(_tools.Pen);
            _ctx.PenStyle = style;
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

        private void SetThicknessSlider_Pos(double newValue)
        {
            // 根据 Slider 高度和当前值，计算提示位置
            Rect rect = new Rect(
             ThicknessSlider.TransformToAncestor(this).Transform(new Point(0, 0)),
             new Size(ThicknessSlider.ActualWidth, ThicknessSlider.ActualHeight));
            double trackHeight = ThicknessSlider.ActualHeight;
            double relativeValue = (ThicknessSlider.Maximum - newValue) / (ThicknessSlider.Maximum - ThicknessSlider.Minimum);
            double offsetY = relativeValue * trackHeight;

            ThicknessTip.Margin = new Thickness(80, offsetY + rect.Top - 10, 0, 0);
        }

        private void UpdateThicknessPreviewPosition()
        {
            if (ThicknessPreview == null)return;

            // 图像缩放比例
            double zoom = ZoomTransform.ScaleX;   // or ScaleY，通常两者相等
            double size = PenThickness * 2 * zoom; // 半径→界面直径 * 缩放

            ThicknessPreview.Width = size;
            ThicknessPreview.Height = size;

            ThicknessPreview.Fill = Brushes.Transparent;
            ThicknessPreview.StrokeThickness = 2;
        }
        private void OnRotateLeftClick(object sender, RoutedEventArgs e)
        {
            RotateBitmap(-90); RotateFlipMenuToggle.IsChecked = false;
        }

        private void OnRotateRightClick(object sender, RoutedEventArgs e)
        {
            RotateBitmap(90); RotateFlipMenuToggle.IsChecked = false;
        }

        private void OnRotate180Click(object sender, RoutedEventArgs e)
        {
            RotateBitmap(180); RotateFlipMenuToggle.IsChecked = false;
        }


        private void OnFlipVerticalClick(object sender, RoutedEventArgs e)
        {
            FlipBitmap(flipVertical: true); RotateFlipMenuToggle.IsChecked = false;
        }

        private void OnFlipHorizontalClick(object sender, RoutedEventArgs e)
        {
            FlipBitmap(flipVertical: false); RotateFlipMenuToggle.IsChecked = false;
        }


        private void ApplyTransform(System.Windows.Media.Transform transform)
        {
            if (BackgroundImage.Source is not BitmapSource src || _surface?.Bitmap == null)
                return;

           
            var undoRect = new Int32Rect(0, 0, _surface.Bitmap.PixelWidth, _surface.Bitmap.PixelHeight); // --- 1. 捕获变换前的状态 (for UNDO) ---
            var undoPixels = _surface.ExtractRegion(undoRect);
            if (undoPixels == null) return; // 如果提取失败则中止
            var transformedBmp = new TransformedBitmap(src, transform); // --- 2. 计算并生成变换后的新位图 (这是 REDO 的目标状态) ---
            var newBitmap = new WriteableBitmap(transformedBmp);

            var redoRect = new Int32Rect(0, 0, newBitmap.PixelWidth, newBitmap.PixelHeight);  // --- 3. 捕获变换后的状态 (for REDO) ---
            int redoStride = newBitmap.BackBufferStride;
            var redoPixels = new byte[redoStride * redoRect.Height];
            newBitmap.CopyPixels(redoPixels, redoStride, 0);

            _bitmap = newBitmap;
            BackgroundImage.Source = _bitmap;
            _surface.Attach(_bitmap);
            _surface.ReplaceBitmap(_bitmap);
         
            _undo.PushTransformAction(undoRect, undoPixels, redoRect, redoPixels);   // --- 5. 将完整的变换信息作为一个原子操作压入 Undo 栈 ---

            SetUndoRedoButtonState();
        }

        private void RotateBitmap(int angle)
        {
            ApplyTransform(new RotateTransform(angle));
        }

        private void FlipBitmap(bool flipVertical)
        {
            double cx = _bitmap.PixelWidth / 2.0;
            double cy = _bitmap.PixelHeight / 2.0;
            ApplyTransform(flipVertical ? new ScaleTransform(1, -1, cx, cy) : new ScaleTransform(-1, 1, cx, cy));
        }
        private void UpdateWindowTitle()
        {
            string dirtyMark = _isFileSaved ? "" : "*";
            string newTitle = $"{dirtyMark}{_currentFileName} ({_currentImageIndex + 1}/{_imageFiles.Count}) - SodiumPaint {_programVersion}";

            // 更新窗口系统标题栏（任务栏显示）
            this.Title = newTitle;

            // 同步更新自定义标题栏显示内容
            TitleTextBlock.Text = newTitle;
        }


        public void ShowTextToolbarFor(System.Windows.Controls.TextBox tb)
        {
            _activeTextBox = tb;
            TextEditBar.Visibility = Visibility.Visible;

            FontFamilyBox.SelectedItem = tb.FontFamily;
            FontSizeBox.Text = tb.FontSize.ToString(CultureInfo.InvariantCulture);
            BoldBtn.IsChecked = tb.FontWeight == FontWeights.Bold;
            ItalicBtn.IsChecked = tb.FontStyle == FontStyles.Italic;
            UnderlineBtn.IsChecked = tb.TextDecorations == TextDecorations.Underline;
        }

        public void HideTextToolbar()
        {
            TextEditBar.Visibility = Visibility.Collapsed;
            _activeTextBox = null;
        }

        private void FontSettingChanged(object? sender, RoutedEventArgs e)
        {
            if (_activeTextBox == null) return;

            if (FontFamilyBox.SelectedItem is FontFamily family)
                _activeTextBox.FontFamily = family;
            if (double.TryParse((FontSizeBox.SelectedItem as ComboBoxItem)?.Content?.ToString(), out double size))
                _activeTextBox.FontSize = size;

            _activeTextBox.FontWeight = BoldBtn.IsChecked == true ? FontWeights.Bold : FontWeights.Normal;
            _activeTextBox.FontStyle = ItalicBtn.IsChecked == true ? FontStyles.Italic : FontStyles.Normal;
            _activeTextBox.TextDecorations = UnderlineBtn.IsChecked == true ? TextDecorations.Underline : null;



            if (_tools.Text is TextTool st) // 强转成 SelectTool
            {
                _activeTextBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    st.DrawTextboxOverlay(_ctx);
                }), DispatcherPriority.Background);
            }
        }

        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }




  
        private void MainWindow_Deactivated(object sender, EventArgs e)
        {
            // When the window loses focus, tell the current tool to stop its action.
            _router.CurrentTool?.StopAction(_ctx);
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        private Point _dragStartPoint;
        private bool _draggingFromMaximized = false;

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

            if (e.ClickCount == 2) // 双击标题栏切换最大化/还原
            {
                MaximizeRestore_Click(sender, null);
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (_maximized)
                {
                    // 记录按下位置，准备看是否拖动
                    _dragStartPoint = e.GetPosition(this);
                    _draggingFromMaximized = true;
                    MouseMove += Border_MouseMoveFromMaximized;
                }
                else
                {
                    DragMove(); // 普通拖动
                }
            }
        }

        private void Border_MouseMoveFromMaximized(object sender, System.Windows.Input.MouseEventArgs e)
        {

            if (_draggingFromMaximized && e.LeftButton == MouseButtonState.Pressed)
            {
                // 鼠标移动的阈值，比如 5px
                var currentPos = e.GetPosition(this);
                if (Math.Abs(currentPos.X - _dragStartPoint.X) > 5 ||
                    Math.Abs(currentPos.Y - _dragStartPoint.Y) > 5)
                {
                    // 超过阈值，恢复窗口大小，并开始拖动
                    _draggingFromMaximized = false;
                    MouseMove -= Border_MouseMoveFromMaximized;

                    _maximized = false;

                    var percentX = _dragStartPoint.X / ActualWidth;

                    Left = e.GetPosition(this).X - _restoreBounds.Width * percentX;
                    Top = e.GetPosition(this).Y;
                    Width = _restoreBounds.Width;
                    Height = _restoreBounds.Height;
                    SetMaximizeIcon();
                    DragMove();
                }
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            hwndSource.AddHook(WndProc);
        }


        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_NCHITTEST)
            {
                if (_maximized)
                {
                    handled = true;
                    return (IntPtr)1; // HTCLIENT
                }
                // 获得鼠标相对于窗口的位置
                var mousePos = PointFromScreen(new Point(
                    (short)(lParam.ToInt32() & 0xFFFF),
                    (short)((lParam.ToInt32() >> 16) & 0xFFFF)));

                double width = ActualWidth;
                double height = ActualHeight;
                int resizeBorder = 12; // 可拖动边框宽度

                handled = true;

                // 判断边缘区域
                if (mousePos.Y <= resizeBorder)
                {
                    if (mousePos.X <= resizeBorder) return (IntPtr)HTTOPLEFT;
                    if (mousePos.X >= width - resizeBorder) return (IntPtr)HTTOPRIGHT;
                    return (IntPtr)HTTOP;
                }
                else if (mousePos.Y >= height - resizeBorder)
                {
                    if (mousePos.X <= resizeBorder) return (IntPtr)HTBOTTOMLEFT;
                    if (mousePos.X >= width - resizeBorder) return (IntPtr)HTBOTTOMRIGHT;
                    return (IntPtr)HTBOTTOM;
                }
                else
                {
                    if (mousePos.X <= resizeBorder) return (IntPtr)HTLEFT;
                    if (mousePos.X >= width - resizeBorder) return (IntPtr)HTRIGHT;
                }

                // 否则返回客户区
                return (IntPtr)1; // HTCLIENT
            }
            return IntPtr.Zero;
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

        private void SetRestoreIcon()
        {
            MaxRestoreButton.Content = new Image
            {
                Source = (DrawingImage)FindResource("Restore_Image"),
                Width = 12,
                Height = 12
            };
        }
        private void SetMaximizeIcon()
        {
            MaxRestoreButton.Content = new Viewbox
            {
                Width = 10,
                Height = 10,
                Child = new Rectangle
                {
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Width = 12,
                    Height = 12
                }
            };
        }
        private bool _maximized = false;
        private Rect _restoreBounds;
        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                _restoreBounds = new Rect(Left, Top, Width, Height);
                _maximized = true;

                var workArea = SystemParameters.WorkArea;
                Left = workArea.Left;
                Top = workArea.Top;
                Width = workArea.Width;
                Height = workArea.Height;

                // 切换到还原图标
                SetRestoreIcon();
                WindowState = WindowState.Normal;
            }

        }

        private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Get the position relative to the scaled CanvasWrapper
            Point pos = e.GetPosition(CanvasWrapper);
            _router.ViewElement_MouseDown(pos, e);
        }

        private void OnCanvasMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Point pos = e.GetPosition(CanvasWrapper);
            _router.ViewElement_MouseMove(pos, e);
        }

        private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(CanvasWrapper);
            _router.ViewElement_MouseUp(pos, e);
        }

        private void OnCanvasMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _router.CurrentTool?.StopAction(_ctx);
        }


        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
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
        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            // 让 WPF 的合成目标透明，否则会被清成黑色
            var src = (HwndSource)PresentationSource.FromVisual(this)!;
            src.CompositionTarget.BackgroundColor = Colors.Transparent;
        }





        
        public MainWindow(string startFilePath)///////////////////////////////////////////////////主窗口初始化
        {

            _currentFilePath = startFilePath;
            InitializeComponent();


            Loaded += (s, e) =>
            {
                MicaAcrylicManager.ApplyEffect(this);
            };
            Loaded += MainWindow_Loaded;
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
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;

            this.Focusable = true;
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Focus();
            Task.Run(async () => // 在后台线程运行，不阻塞UI线程
            {
                await OpenImageAndTabs(_currentFilePath, true);
                System.Windows.Application.Current.Dispatcher.Invoke(() =>// 如果你需要在完成后通知UI，要切回UI线程
                {
                    _isInitialLayoutComplete = true;
                });
            });
        }

        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.Z:
                        Undo();
                        e.Handled = true;
                        break;
                    case Key.Y:
                        Redo();
                        e.Handled = true;
                        break;
                    case Key.S:
                        OnSaveClick(sender, e);
                        e.Handled = true;
                        break;
                    case Key.N:
                        OnNewClick(sender, e);
                        e.Handled = true;
                        break;
                    case Key.O:
                        OnOpenClick(sender, e);
                        e.Handled = true;
                        break;
                    case Key.A:
                        if (Keyboard.Modifiers == ModifierKeys.Control)
                        {
                            _router.SetTool(_tools.Select); // 切换到选择工具

                            if (_tools.Select is SelectTool st) // 强转成 SelectTool
                            {
                                st.SelectAll(_ctx); // 调用选择工具的特有方法
                            }
                            e.Handled = true;
                        }
                        break;
                }
            }
            else
            {
                switch (e.Key)
                {
                    case Key.Left:
                        ShowPrevImage();
                        e.Handled = true; // 防止焦点导航
                        break;
                    case Key.Right:
                        ShowNextImage();
                        e.Handled = true;
                        break;
                }
            }
        }
        private byte[] ExtractRegionFromSnapshot(byte[] fullData, Int32Rect rect, int stride)
        {
            int bytesPerPixel = 4;
            byte[] region = new byte[rect.Height * rect.Width * bytesPerPixel];

            for (int row = 0; row < rect.Height; row++)
            {
                int srcOffset = (rect.Y + row) * stride + rect.X * bytesPerPixel;
                int dstOffset = row * rect.Width * bytesPerPixel;
                Buffer.BlockCopy(fullData, srcOffset, region, dstOffset, rect.Width * bytesPerPixel);
            }
            return region;
        }
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
        private void OnUndoClick(object sender, RoutedEventArgs e) => Undo();
        private void OnRedoClick(object sender, RoutedEventArgs e) => Redo();
        private void EmptyClick(object sender, RoutedEventArgs e)
        {
            RotateFlipMenuToggle.IsChecked = false;
            BrushToggle.IsChecked = false;
        }
        private static Int32Rect ClampRect(Int32Rect rect, int maxWidth, int maxHeight)
        {
            // Clamp 左上角坐标到合法范围
            int x = Math.Max(0, Math.Min(rect.X, maxWidth));
            int y = Math.Max(0, Math.Min(rect.Y, maxHeight));

            // 计算允许的最大宽高
            int w = Math.Max(0, Math.Min(rect.Width, maxWidth - x));
            int h = Math.Max(0, Math.Min(rect.Height, maxHeight - y));

            return new Int32Rect(x, y, w, h);
        }
        public void SetUndoRedoButtonState()
        {
            UpdateBrushAndButton(UndoButton, UndoIcon, _undo.CanUndo);
            UpdateBrushAndButton(RedoButton, RedoIcon, _undo.CanRedo);

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
        private void UpdateBrushAndButton(System.Windows.Controls.Button button, Image image, bool isEnabled)
        {
            button.IsEnabled = isEnabled;

           
            var frozenDrawingImage = (DrawingImage)image.Source; // 获取当前 UI 使用的绘图对象
            var modifiableDrawingImage = frozenDrawingImage.Clone();    // 克隆出可修改的副本
            if (modifiableDrawingImage.Drawing is GeometryDrawing geoDrawing)  // DrawingImage.Drawing 可能是 DrawingGroup 或 GeometryDrawing
            {
                geoDrawing.Brush = isEnabled ? Brushes.Black : Brushes.Gray;
            }
            else if (modifiableDrawingImage.Drawing is DrawingGroup group)
            {
                foreach (var child in group.Children)
                {
                    if (child is GeometryDrawing childGeo)
                    {
                        childGeo.Brush = isEnabled ? Brushes.Black : Brushes.Gray;
                    }
                }
            }

            // 替换 Image.Source，让 UI 用新的对象
            image.Source = modifiableDrawingImage;
        }
        private void OnTextClick(object sender, RoutedEventArgs e)
        {
            _router.SetTool(_tools.Text);
        }
        private void Undo()
        {

            _undo.Undo(); _ctx.IsDirty = true;
            SetUndoRedoButtonState();
        }
        private void Redo()
        {
            _undo.Redo(); _ctx.IsDirty = true;
            SetUndoRedoButtonState();
        }

        private byte[] ExtractRegionFromBitmap(WriteableBitmap bmp, Int32Rect rect)
        {
            int stride = bmp.BackBufferStride;
            byte[] region = new byte[rect.Width * rect.Height * 4];

            bmp.Lock();
            for (int row = 0; row < rect.Height; row++)
            {
                IntPtr src = bmp.BackBuffer + (rect.Y + row) * stride + rect.X * 4;
                System.Runtime.InteropServices.Marshal.Copy(src, region, row * rect.Width * 4, rect.Width * 4);
            }
            bmp.Unlock();
            return region;
        }
        private void SaveBitmap(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Create))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(_bitmap));
                encoder.Save(fs);
            }
        }
        private Color GetPixelColor(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _bmpWidth || y >= _bmpHeight) return Colors.Transparent;

            _bitmap.Lock();
            unsafe
            {
                IntPtr pBackBuffer = _bitmap.BackBuffer;
                int stride = _bitmap.BackBufferStride;
                byte* p = (byte*)pBackBuffer + y * stride + x * 4;
                byte b = p[0];
                byte g = p[1];
                byte r = p[2];
                byte a = p[3];
                _bitmap.Unlock();
                return Color.FromArgb(a, r, g, b);
            }
        }
        private void DrawPixel(int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= _bmpWidth || y >= _bmpHeight) return;

            _bitmap.Lock();
            unsafe
            {
                IntPtr pBackBuffer = _bitmap.BackBuffer;
                int stride = _bitmap.BackBufferStride;
                byte* p = (byte*)pBackBuffer + y * stride + x * 4;
                p[0] = color.B;
                p[1] = color.G;
                p[2] = color.R;
                p[3] = color.A;
            }
            _bitmap.AddDirtyRect(new Int32Rect(x, y, 1, 1));
            _bitmap.Unlock();
        }

       
        // Ctrl + 滚轮 缩放事件
        private void FitToWindow()
        {
            if (BackgroundImage.Source != null)
            {
                double imgWidth = BackgroundImage.Source.Width;
                double imgHeight = BackgroundImage.Source.Height;

                double viewWidth = ScrollContainer.ViewportWidth;
                double viewHeight = ScrollContainer.ViewportHeight;

                double scaleX = viewWidth / imgWidth;
                double scaleY = viewHeight / imgHeight;

                double fitScale = Math.Min(scaleX, scaleY); // 保持纵横比适应
                zoomscale = fitScale;

                ZoomTransform.ScaleX = ZoomTransform.ScaleY = zoomscale;
                UpdateSliderBarValue(zoomscale);
            }
        }
        private void FitToWindow_Click(object sender, RoutedEventArgs e)
        {
            FitToWindow();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            double newScale = zoomscale / ZoomTimes;
            zoomscale = Math.Clamp(newScale, MinZoom, MaxZoom);
            ZoomTransform.ScaleX = ZoomTransform.ScaleY = zoomscale;
            UpdateSliderBarValue(zoomscale);
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            double newScale = zoomscale * ZoomTimes;
            zoomscale = Math.Clamp(newScale, MinZoom, MaxZoom);
            ZoomTransform.ScaleX = ZoomTransform.ScaleY = zoomscale;
            UpdateSliderBarValue(zoomscale);
        }

        private void ZoomMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox combo && combo.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                double selectedScale = Convert.ToDouble(item.Tag);
                zoomscale = Math.Clamp(selectedScale, MinZoom, MaxZoom);
                ZoomTransform.ScaleX = ZoomTransform.ScaleY = zoomscale;
                // s(zoomscale);
                UpdateSliderBarValue(zoomscale);
            }
        }
        private void UpdateSliderBarValue(double newScale)
        {
            ZoomSlider.Value = newScale;
            ZoomLevel = newScale.ToString("P0");
            ZoomMenu.Text = newScale.ToString("P0");
        }
        private void OnMouseWheelZoom(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;

            e.Handled = true; // 阻止默认滚动

            double oldScale = zoomscale;
            double newScale = oldScale * (e.Delta > 0 ? ZoomTimes : 1 / ZoomTimes);
            newScale = Math.Clamp(newScale, MinZoom, MaxZoom);
            zoomscale = newScale;
            ZoomTransform.ScaleX = ZoomTransform.ScaleY = newScale;


            Point mouseInScroll = e.GetPosition(ScrollContainer);

            double offsetX = ScrollContainer.HorizontalOffset;
            double offsetY = ScrollContainer.VerticalOffset;
            UpdateSliderBarValue(zoomscale);
            // 维持鼠标相对画布位置不变的平移公式
            double newOffsetX = (offsetX + mouseInScroll.X) * (newScale / oldScale) - mouseInScroll.X;
            double newOffsetY = (offsetY + mouseInScroll.Y) * (newScale / oldScale) - mouseInScroll.Y;
            ScrollContainer.ScrollToHorizontalOffset(newOffsetX);
            ScrollContainer.ScrollToVerticalOffset(newOffsetY);
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
        private void ShowNextImage()
        {
            if (_imageFiles.Count == 0 || _currentImageIndex < 0) return;
            _currentImageIndex++;
            if (_currentImageIndex >= _imageFiles.Count)
                _currentImageIndex = 0; // 循环到第一张

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
                _currentImageIndex = _imageFiles.Count - 1; // 循环到最后一张

            RequestImageLoad(_imageFiles[_currentImageIndex]);
        }
        private void ClearRect(ToolContext ctx, Int32Rect rect, Color color)
        {
            ctx.Surface.Bitmap.Lock();
            unsafe
            {
                byte* basePtr = (byte*)ctx.Surface.Bitmap.BackBuffer;
                int stride = ctx.Surface.Bitmap.BackBufferStride;
                for (int y = rect.Y; y < rect.Y + rect.Height; y++)
                {
                    byte* rowPtr = basePtr + y * stride + rect.X * 4;
                    for (int x = 0; x < rect.Width; x++)
                    {
                        rowPtr[0] = color.B;
                        rowPtr[1] = color.G;
                        rowPtr[2] = color.R;
                        rowPtr[3] = color.A;
                        rowPtr += 4;
                    }
                }
            }
            ctx.Surface.Bitmap.AddDirtyRect(rect);
            ctx.Surface.Bitmap.Unlock();
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
          
            if (_bitmap == null)return;  // 1. 检查图像是否存在
            _undo.PushFullImageUndo();
            ConvertToBlackAndWhite(_bitmap);
            SetUndoRedoButtonState();
        }
        private void ConvertToBlackAndWhite(WriteableBitmap bmp)
        {
            bmp.Lock();
            unsafe
            {
                byte* basePtr = (byte*)bmp.BackBuffer;
                int stride = bmp.BackBufferStride;
                int height = bmp.PixelHeight;
                int width = bmp.PixelWidth;
                Parallel.For(0, height, y =>// 使用并行处理来加速计算，每个CPU核心处理一部分行
                {
                    byte* row = basePtr + y * stride;
                    // 像素格式为 BGRA (4 bytes per pixel)
                    for (int x = 0; x < width; x++)
                    {
                        // 获取当前像素的 B, G, R 值
                        byte b = row[x * 4];
                        byte g = row[x * 4 + 1];
                        byte r = row[x * 4 + 2];
                        // 使用亮度公式计算灰度值
                        // 这个公式比简单的 (R+G+B)/3 效果更符合人眼感知
                        byte gray = (byte)(r * 0.2126 + g * 0.7152 + b * 0.0722); // 将计算出的灰度值写回所有三个颜色通道
                        row[x * 4] = gray; // Blue
                        row[x * 4 + 1] = gray; // Green
                        row[x * 4 + 2] = gray; // Red
                                               // Alpha 通道 (row[x * 4 + 3]) 保持不变
                    }
                });
            }
            // 标记整个图像区域已更新
            bmp.AddDirtyRect(new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight));
            bmp.Unlock();
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
        private void ResizeCanvas(int newWidth, int newHeight)
        {
            var oldBitmap = _surface.Bitmap;
            if (oldBitmap == null) return; // 如果尺寸没有变化，则不执行任何操作
            if (oldBitmap.PixelWidth == newWidth && oldBitmap.PixelHeight == newHeight)return;

            var undoRect = new Int32Rect(0, 0, oldBitmap.PixelWidth, oldBitmap.PixelHeight);  // --- 1. 捕获变换前的完整状态 (for UNDO) ---
            var undoPixels = new byte[oldBitmap.PixelHeight * oldBitmap.BackBufferStride];
            // 从旧位图复制像素
            oldBitmap.CopyPixels(undoRect, undoPixels, oldBitmap.BackBufferStride, 0);

            var transform = new ScaleTransform(// --- 2. 创建新的、缩放后的位图 ---
                (double)newWidth / oldBitmap.PixelWidth, // 创建一个变换，指定缩放比例
                (double)newHeight / oldBitmap.PixelHeight
            );

            var transformedBitmap = new TransformedBitmap(oldBitmap, transform);    // 应用变换
            RenderOptions.SetBitmapScalingMode(transformedBitmap, BitmapScalingMode.NearestNeighbor);

            // 将结果转换为一个新的 WriteableBitmap
            var newFormatedBitmap = new FormatConvertedBitmap(transformedBitmap, PixelFormats.Bgra32, null, 0);
            var newBitmap = new WriteableBitmap(newFormatedBitmap);

            var redoRect = new Int32Rect(0, 0, newBitmap.PixelWidth, newBitmap.PixelHeight);    // --- 3. 捕获变换后的完整状态 (for REDO) ---
            var redoPixels = new byte[newBitmap.PixelHeight * newBitmap.BackBufferStride];
            // 从新创建的位图复制像素
            newBitmap.CopyPixels(redoRect, redoPixels, newBitmap.BackBufferStride, 0);
            _surface.ReplaceBitmap(newBitmap);  // --- 4. 执行变换：用新的位图替换旧的画布 ---
            _ctx.Undo.PushTransformAction(undoRect, undoPixels, redoRect, redoPixels);   // --- 5. 将完整的变换信息压入 Undo 栈 ---
            SetUndoRedoButtonState();
        }


        public class FileTabItem : INotifyPropertyChanged
        {
            public string FilePath { get; }
            public string FileName => System.IO.Path.GetFileName(FilePath);
            public string DisplayName => System.IO.Path.GetFileNameWithoutExtension(FilePath);
            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
            }
            private bool _isLoading;
            public bool IsLoading
            {
                get => _isLoading;
                set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
            }

            private BitmapSource? _thumbnail;
            public BitmapSource? Thumbnail
            {
                get => _thumbnail;
                set
                {
                    _thumbnail = value;
                    OnPropertyChanged(nameof(Thumbnail));
                }
            }

            public FileTabItem(string path) {FilePath = path;}
            public async Task LoadThumbnailAsync(int containerWidth, int containerHeight)
            {  // 异步加载缩略图（调用时自动更新UI）
            // 需要 using System.Drawing; 
            // 和 using System.IO;
                var thumbnail = await Task.Run(() =>
                {
                    try
                    {
                        // 步骤 1: 使用 System.Drawing.Image 获取原始尺寸
                        double originalWidth, originalHeight;
                        using (var img = System.Drawing.Image.FromFile(FilePath))
                        {
                            originalWidth = img.Width;
                            originalHeight = img.Height;
                        }

                        // 步骤 2, 3, 4 与方案一完全相同
                        double ratioX = containerWidth / originalWidth;
                        double ratioY = containerHeight / originalHeight;
                        double finalRatio = Math.Min(ratioX, ratioY);

                        if (finalRatio > 1.0)
                        {
                            finalRatio = 1.0;
                        }

                        int decodeWidth = (int)(originalWidth * finalRatio);
                        if (decodeWidth < 1) decodeWidth = 1;

                        // 步骤 5: 创建并加载BitmapImage
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(FilePath);
                        bmp.DecodePixelWidth = decodeWidth;
                        bmp.EndInit();
                        bmp.Freeze();
                        //  s(bmp.PixelWidth.ToString() + " " + bmp.PixelHeight.ToString());
                        return bmp;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to load thumbnail for {FilePath}: {ex.Message}");
                        return null;
                    }
                });
                if (thumbnail != null)Thumbnail = thumbnail;
            }
            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged(string name)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private const int PageSize = 10; // 每页标签数量（可调整）

        public ObservableCollection<FileTabItem> FileTabs { get; }
            = new ObservableCollection<FileTabItem>();
        // 加载当前页 + 前后页文件到显示区
        private async Task LoadTabPageAsync(int centerIndex)
        {//全部清空并重新加载!!!
            if (_imageFiles == null || _imageFiles.Count == 0) return;


            FileTabs.Clear();
            int start = Math.Max(0, centerIndex - PageSize);
            int end = Math.Min(_imageFiles.Count - 1, centerIndex + PageSize);
            //s(centerIndex);
            foreach (var path in _imageFiles.Skip(start).Take(end - start + 1))
                FileTabs.Add(new FileTabItem(path));

            foreach (var tab in FileTabs)
                if (tab.Thumbnail == null && !tab.IsLoading)
                {
                    tab.IsLoading = true;
                    _ = tab.LoadThumbnailAsync(100, 60);
                }
        }
        private async Task RefreshTabPageAsync(int centerIndex, bool refresh = false)
        {

            if (_imageFiles == null || _imageFiles.Count == 0)return;

            if (refresh)
                await LoadTabPageAsync(centerIndex);

            // 计算当前选中图片在 FileTabs 中的索引
            var currentTab = FileTabs.FirstOrDefault(t => t.FilePath == _imageFiles[centerIndex]);
            if (currentTab == null)return;

            int selectedIndex = FileTabs.IndexOf(currentTab);
            if (selectedIndex < 0)return;

            double itemWidth = 124;                   // 与 Button 实际宽度一致
            double viewportWidth = FileTabsScroller.ViewportWidth;
            double targetOffset = selectedIndex * itemWidth - viewportWidth / 2 + itemWidth / 2;

            targetOffset = Math.Max(0, targetOffset); // 防止负数偏移
            double maxOffset = Math.Max(0, FileTabs.Count * itemWidth - viewportWidth);
            targetOffset = Math.Min(targetOffset, maxOffset); // 防止超出范围

            FileTabsScroller.ScrollToHorizontalOffset(targetOffset);
        }

        // 文件总数绑定属性
        public int ImageFilesCount;
        private bool _isInitialLayoutComplete = false;
        private async void OnFileTabsScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!_isInitialLayoutComplete)return;
           
            double itemWidth = 124;
            int firstIndex = (int)(FileTabsScroller.HorizontalOffset / itemWidth);
            int visibleCount = (int)(FileTabsScroller.ViewportWidth / itemWidth) + 2;
            int lastIndex = firstIndex + visibleCount;
            PreviewSlider.Value = firstIndex;
            bool needload = false;

            // 尾部加载
            if (lastIndex >= FileTabs.Count - 10 && FileTabs.Count < _imageFiles.Count)
            {
                int currentFirstIndex = _imageFiles.IndexOf(FileTabs[FileTabs.Count - 1].FilePath);
                if (currentFirstIndex > 0)
                {
                    int start = Math.Min(_imageFiles.Count - 1, currentFirstIndex);
                    foreach (var path in _imageFiles.Skip(start).Take(PageSize))
                        FileTabs.Add(new FileTabItem(path));
                    needload = true;
                }
            }

            // 前端加载
            if (FileTabs.Count > 0 && firstIndex < 10 && FileTabs[0].FilePath != _imageFiles[0])
            {
                int currentFirstIndex = _imageFiles.IndexOf(FileTabs[0].FilePath);
                if (currentFirstIndex > 0)
                {
                    int start = Math.Min(0, currentFirstIndex - PageSize);
                    double offsetBefore = FileTabsScroller.HorizontalOffset;

                    var prevPaths = _imageFiles.Skip(start).Take(currentFirstIndex - start);

                    foreach (var path in prevPaths.Reverse())
                        FileTabs.Insert(0, new FileTabItem(path));
                    FileTabsScroller.ScrollToHorizontalOffset(offsetBefore + prevPaths.Count() * itemWidth);
                    needload = true;
                }
            }
            if (needload || e.HorizontalChange != 0 || e.ExtentWidthChange != 0)  // 懒加载缩略图，仅当有新增或明显滚动时触发
            {
                int end = Math.Min(lastIndex, FileTabs.Count);
                for (int i = firstIndex; i < end; i++)
                {
                    var tab = FileTabs[i];
                    if (tab.Thumbnail == null && !tab.IsLoading)
                    {
                        tab.IsLoading = true;
                        _ = tab.LoadThumbnailAsync(100, 60);
                    }
                }
            }
        }
        private async void OnFileTabClick(object sender, RoutedEventArgs e)// 点击标签打开图片
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is FileTabItem item) await OpenImageAndTabs(item.FilePath);
        }

        // 鼠标滚轮横向滑动标签栏
        private void OnFileTabsWheelScroll(object sender, MouseWheelEventArgs e)
        {
            double offset = FileTabsScroller.HorizontalOffset - e.Delta / 2;
            FileTabsScroller.ScrollToHorizontalOffset(offset);
            e.Handled = true;
        }

        private bool _isDragging = false;
        private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 如果点击的目标是 Thumb 本身或其子元素，则不作任何处理。
            // 让 Slider 的默认 Thumb 拖动逻辑去工作。
            if (IsMouseOverThumb(e))return;

            // 如果点击的是轨道部分
            _isDragging = true;
            var slider = (Slider)sender;

            // 捕获鼠标，这样即使鼠标移出 Slider 范围，我们也能继续收到 MouseMove 事件
            slider.CaptureMouse();

            // 更新 Slider 的值到当前点击的位置
            UpdateSliderValueFromPoint(slider, e.GetPosition(slider));

            // 标记事件已处理，防止其他控件响应
            e.Handled = true;
        }

        private void Slider_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // 仅当我们通过点击轨道开始拖动时，才处理 MouseMove 事件
            if (_isDragging)
            {
                var slider = (Slider)sender;
                // 持续更新 Slider 的值
                UpdateSliderValueFromPoint(slider, e.GetPosition(slider));
            }
        }

        private void Slider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 如果我们正在拖动
            if (_isDragging)
            {
                _isDragging = false;
                var slider = (Slider)sender;
                // 释放鼠标捕获
                slider.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void Slider_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var slider = (Slider)sender;

            // 根据滚轮方向调整值
            double change = slider.LargeChange; // 使用 LargeChange 作为滚动步长
            if (e.Delta < 0)
            {
                change = -change;
            }

            slider.Value += change;
            e.Handled = true;
        }
        private async void UpdateSliderValueFromPoint(Slider slider, Point position)
        {
            double ratio = position.Y / slider.ActualHeight; // 计算点击位置在总高度中的比例

            // 将比例转换为滑块的值范围
            double value = slider.Minimum + (slider.Maximum - slider.Minimum) * (1 - ratio);
           
            value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, value)); // 确保值在有效范围内

            slider.Value = value;
            await OpenImageAndTabs(_imageFiles[(int)value], true);
        }
        private bool IsMouseOverThumb(MouseButtonEventArgs e)/// 检查鼠标事件的原始源是否是 Thumb 或其内部的任何元素。
        {
            var slider = (Slider)e.Source;
            var track = slider.Template.FindName("PART_Track", slider) as Track;
            if (track == null) return false;

            return track.Thumb.IsMouseOver;
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
  

        private void SetPreviewSlider()
        {
            if (_imageFiles == null || _imageFiles.Count == 0) return;
            PreviewSlider.Minimum = 0;
            PreviewSlider.Maximum = _imageFiles.Count - 1;
            PreviewSlider.Value = _currentImageIndex;
            //if(_imageFiles.Count < 30)
            //    PreviewSlider.Visibility= Visibility.Collapsed;
            //else
            //    PreviewSlider.Visibility = Visibility.Visible;
        }

      
        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                OnSaveAsClick(sender, e); // 如果没有当前路径，就走另存为
            }
            else SaveBitmap(_currentFilePath);
        }
        private string _currentFilePath = string.Empty;

        private void OnSaveAsClick(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = PicFilterString,
                FileName = "image.png"
            };
            if (dlg.ShowDialog() == true)
            {
                _currentFilePath = dlg.FileName;
                SaveBitmap(_currentFilePath);
            }
        }
    
        private void OnPickColorClick(object s, RoutedEventArgs e)
        {
            LastTool = ((MainWindow)System.Windows.Application.Current.MainWindow)._router.CurrentTool;
            _router.SetTool(_tools.Eyedropper);
        }

        private void OnEraserClick(object s, RoutedEventArgs e)
        {
            SetBrushStyle(BrushStyle.Eraser);
        }
        private void OnFillClick(object s, RoutedEventArgs e) => _router.SetTool(_tools.Fill);
        private void OnSelectClick(object s, RoutedEventArgs e) => _router.SetTool(_tools.Select);

        private void OnEffectButtonClick(object sender, RoutedEventArgs e)
        {
            var btn = (System.Windows.Controls.Button)sender;
            btn.ContextMenu.IsOpen = true;
        }
        private void Clean_bitmap(int _bmpWidth, int _bmpHeight)
        {
            _bitmap = new WriteableBitmap(_bmpWidth, _bmpHeight, 96, 96, PixelFormats.Bgra32, null);
            BackgroundImage.Source = _bitmap;

            // 填充白色背景
            _bitmap.Lock();

            if (_undo != null)
            {
                _undo.ClearUndo();
                _undo.ClearRedo();
            }

            if (_surface == null)
                _surface = new CanvasSurface(_bitmap);
            else
                _surface.Attach(_bitmap);
            unsafe
            {
                IntPtr pBackBuffer = _bitmap.BackBuffer;
                int stride = _bitmap.BackBufferStride;
                for (int y = 0; y < _bmpHeight; y++)
                {
                    byte* row = (byte*)pBackBuffer + y * stride;
                    for (int x = 0; x < _bmpWidth; x++)
                    {
                        row[x * 4 + 0] = 255; // B
                        row[x * 4 + 1] = 255; // G
                        row[x * 4 + 2] = 255; // R
                        row[x * 4 + 3] = 255; // A
                    }
                }
            }
            _bitmap.AddDirtyRect(new Int32Rect(0, 0, _bmpWidth, _bmpHeight));
            _bitmap.Unlock();

            // 调整窗口和画布大小
            double imgWidth = _bitmap.Width;
            double imgHeight = _bitmap.Height;

            BackgroundImage.Width = imgWidth;
            BackgroundImage.Height = imgHeight;

            _imageSize = $"{_surface.Width}×{_surface.Height}";
            OnPropertyChanged(nameof(ImageSize));
            UpdateWindowTitle();

            FitToWindow();
            SetBrushStyle(BrushStyle.Round);
        }

        private void OnNewClick(object sender, RoutedEventArgs e)
        {
            _bmpWidth = 1200; // 可以弹出对话框让用户输入宽高，也可以用默认尺寸
            _bmpHeight = 900;
            _currentFilePath = string.Empty; // 新建后没有路径
            _currentFileName = "未命名";
            Clean_bitmap(_bmpWidth, _bmpHeight);

            UpdateWindowTitle();
        }

    }

}
