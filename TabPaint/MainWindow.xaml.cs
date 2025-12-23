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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using static TabPaint.MainWindow;
//
//TabPaint主程序
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {

        // MainWindow 类成员变量


        private CanvasSurface _surface;
        public UndoRedoManager _undo;
        public ToolContext _ctx;
        private InputRouter _router;
        private ToolRegistry _tools;
        public double zoomscale = 1;
        private byte[]? _preDrawSnapshot = null;

        private WriteableBitmap _bitmap;
        private int _bmpWidth, _bmpHeight;
        private Color _penColor = Colors.Black;
        private bool _isDrawing = false;
        private List<string> _imageFiles = new List<string>();
        private int _currentImageIndex = -1;
        private bool _isEdited = false; // 标记当前画布是否被修改
        private string _currentFileName = "未命名";
        private string _programVersion = "v0.6.2 alpha"; // 可以从 Assembly 读取
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
        public enum BrushStyle { Round, Square, Brush, Spray, Pencil, Eraser, Watercolor, Crayon,Highlighter,Mosaic }
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
        public Color BackgroundColor;
        public Color ForegroundColor;
        public SolidColorBrush SelectedBrush { get; set; } = new SolidColorBrush(Colors.Black);

        // 绑定到 ItemsControl 的预设颜色集合
        public ObservableCollection<SolidColorBrush> ColorItems { get; set; }
            = new ObservableCollection<SolidColorBrush>
            {
                new SolidColorBrush(Colors.Black),
                new SolidColorBrush(Colors.Gray),
                new SolidColorBrush(Colors.Brown),
                new SolidColorBrush(Colors.Red),
                new SolidColorBrush(Colors.Orange),
                new SolidColorBrush(Colors.Yellow),
                new SolidColorBrush(Colors.Green),
                 new SolidColorBrush( (Color)ColorConverter.ConvertFromString("#B5E61D")),
                new SolidColorBrush(Colors.Cyan),
                new SolidColorBrush(Colors.Blue),
                new SolidColorBrush(Colors.Purple),
                new SolidColorBrush(Colors.Pink),
                new SolidColorBrush(Colors.BlueViolet),
                 new SolidColorBrush(Colors.CornflowerBlue),
                 new SolidColorBrush( (Color)ColorConverter.ConvertFromString("#C8BFE7")),
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
        private bool useSecondColor = false;//是否使用备用颜色




        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // 各种ITool + InputRouter + EventHandler 相关过程
        //已经被拆分到Itools文件夹中

        //MainWindow 类通用过程,很多都是找不到归属的



        public void UpdateCurrentColor(Color color, bool secondColor = false) // 更新前景色按钮颜色
        {
            if (secondColor)
            {
                BackgroundBrush = new SolidColorBrush(color);
                OnPropertyChanged(nameof(BackgroundBrush)); // 通知绑定刷新
                _ctx.PenColor = color;
                BackgroundColor = color;
            }
            else
            {
                ForegroundBrush = new SolidColorBrush(color);
                OnPropertyChanged(nameof(ForegroundBrush)); // 通知绑定刷新
                ForegroundColor = color;
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
        private void SetPenResizeBarVisibility(bool vis)
        {
            ((MainWindow)System.Windows.Application.Current.MainWindow).ThicknessPanel.Visibility = vis ? Visibility.Visible : Visibility.Collapsed;

        }

        private void SetBrushStyle(BrushStyle style)
        {//设置画笔样式，所有画笔都是pen工具
            _router.SetTool(_tools.Pen);
            _ctx.PenStyle = style;
            UpdateToolSelectionHighlight();
            
            SetPenResizeBarVisibility( _ctx.PenStyle != BrushStyle.Pencil);
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
            if (ThicknessPreview == null) return;

            // 图像缩放比例
            double zoom = ZoomTransform.ScaleX;   // or ScaleY，通常两者相等
            double size = PenThickness * 2 * zoom; // 半径→界面直径 * 缩放

            ThicknessPreview.Width = size;
            ThicknessPreview.Height = size;

            ThicknessPreview.Fill = Brushes.Transparent;
            ThicknessPreview.StrokeThickness = 2;
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
            string newTitle = $"{dirtyMark}{_currentFileName} ({_currentImageIndex + 1}/{_imageFiles.Count}) - TabPaint {_programVersion}";

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
        private void UpdateColorHighlight()
        {

            // 假设你的两个颜色按钮在 XAML 里设置了 Name="ColorBtn1" 和 Name="ColorBtn2"
            ColorBtn1.Tag = !useSecondColor ? "True" : "False"; // 如果不是色2，那就是色1选中
            ColorBtn2.Tag = useSecondColor ? "True" : "False";
        }
        private void FontSizeBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(FontSizeBox.Text, out _))
            {
                FontSizeBox.Text = _activeTextBox.FontSize.ToString(); // 还原为当前有效字号
            }
        }
        private void Control_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // 1. 强制让当前的 ComboBox 失去焦点并应用更改
                DependencyObject focusScope = FocusManager.GetFocusScope((System.Windows.Controls.Control)sender);
                FocusManager.SetFocusedElement(focusScope, _activeTextBox);

                // 2. 将焦点还给画布上的文本框，让用户可以继续打字
                if (_activeTextBox != null)
                {
                    _activeTextBox.Focus();
                    // 将光标移到文字末尾
                    _activeTextBox.SelectionStart = _activeTextBox.Text.Length;
                }

                e.Handled = true; // 阻止回车产生额外的换行或响铃
            }
        }
        private void TextEditBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 如果点击的是工具栏背景本身，而不是子控件
            if (e.OriginalSource is Border || e.OriginalSource is StackPanel)
            {
                if (_activeTextBox != null)
                {
                    _activeTextBox.Focus();
                }
                e.Handled = true;
            }
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            // 让 WPF 的合成目标透明，否则会被清成黑色
            var src = (HwndSource)PresentationSource.FromVisual(this)!;
            src.CompositionTarget.BackgroundColor = Colors.Transparent;
        }




        public CanvasResizeManager _canvasResizer;

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
            SetCropButtonState();
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;


            _canvasResizer = new CanvasResizeManager(this);
          
            this.Focusable = true;
        }

        // 在 MainWindow 类中处理 Drop 事件
        private void OnCanvasDrop(object sender, System.Windows.DragEventArgs e)
        {
            //s(1);
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    string filePath = files[0];
                    try
                    {
                        // 1. 加载图片文件
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(filePath);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad; // 必须 Load，否则文件会被占用
                        bitmap.EndInit();

                        // 2. 切换到选择工具 (假设你有一个切换工具的方法)
                        // this.CurrentTool = this.Select; 
                        _router.SetTool(_tools.Select);
                        // 3. 调用我们刚才提取的逻辑
                        // 注意：这里需要传入你的 ToolContext
                       
                        if (_tools.Select is SelectTool st) // 强转成 SelectTool
                        {
                            st.InsertImageAsSelection(_ctx, bitmap);
                        }
                        e.Handled = true;
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show("无法识别的图片格式: " + ex.Message);
                    }
                }
            }
        }
        private void OnCanvasDragOver(object sender, System.Windows.DragEventArgs e)
        {
            // 检查拖动的是否是文件
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                // 设置鼠标效果为“复制”图标，告知系统和用户这里可以放下
                e.Effects = System.Windows.DragDropEffects.Copy;
                e.Handled = true; // 标记事件已处理
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
        }



        private bool _isSyncingSlider = false; // 防止死循环

        // 当滑块拖动时触发
        private async void PreviewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSyncingSlider) return;
            if (_imageFiles == null || _imageFiles.Count == 0) return;

            // 只有当变化量足够大（或者是拖动结束）时才加载图片，防止滑动太快卡顿
            // 这里做一个简单的去抖动处理，或者直接加载
            int index = (int)e.NewValue;

            // 边界检查
            if (index < 0) index = 0;
            if (index >= _imageFiles.Count) index = _imageFiles.Count - 1;

            // 调用你的加载逻辑，并不触发滚动条反向更新 Slider
            _isSyncingSlider = true;
            await OpenImageAndTabs(_imageFiles[index],true);
            _isSyncingSlider = false;
        }

        // 同时，当你通过点击列表切换图片时，也要更新 Slider 的位置
        // 在 OpenImageAndTabs 方法内部：
        /*
            if (!fromSlider) 
            {
                _isSyncingSlider = true;
                PreviewSlider.Value = _currentImageIndex;
                _isSyncingSlider = false;
            }
        */

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
                    case Key.V:
                        if (Keyboard.Modifiers == ModifierKeys.Control)
                        {
                            _router.SetTool(_tools.Select); // 切换到选择工具

                            if (_tools.Select is SelectTool st) // 强转成 SelectTool
                            {
                                st.PasteSelection(_ctx, true);
                            }
                            e.Handled = true;
                        }
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

        public void SetCropButtonState()
        {
           // s(1);
            UpdateBrushAndButton(CutImage, CutImageIcon, _tools.Select is SelectTool st && _ctx.SelectionOverlay.Visibility!=Visibility.Collapsed);
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

        private void Undo()
        {

            _undo.Undo(); _ctx.IsDirty = true;
            SetUndoRedoButtonState();
            _canvasResizer.UpdateUI();

        }
        private void Redo()
        {
            _undo.Redo(); _ctx.IsDirty = true;
            SetUndoRedoButtonState();
            _canvasResizer.UpdateUI();
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
            // 1. 原有的保存逻辑
            using (FileStream fs = new FileStream(path, FileMode.Create))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(_bitmap));
                encoder.Save(fs);
            }

            // 2. 新增：更新对应标签页的缩略图
            UpdateTabThumbnail(path);
        }
        private void UpdateTabThumbnail(string path)
        {
            // 在 ObservableCollection 中找到对应的 Tab
            var tab = FileTabs.FirstOrDefault(t => t.FilePath == path);
            if (tab == null) return;

            // 从当前的 _bitmap (WriteableBitmap) 创建一个缩小的版本
            // 假设缩略图宽度为 100 (和你 LoadThumbnailAsync 保持一致)
            double targetWidth = 100;
            double scale = targetWidth / _bitmap.PixelWidth;

            // 使用 TransformedBitmap 进行快速缩放
            var transformedBitmap = new TransformedBitmap(_bitmap, new ScaleTransform(scale, scale));

            // 转为 RenderTargetBitmap 或直接转回 WriteableBitmap 以便冻结
            // 冻结 (Freeze) 是为了确保它可以在 UI 线程安全显示
            var newThumb = new WriteableBitmap(transformedBitmap);
            newThumb.Freeze();

            // 触发 UI 更新
            tab.Thumbnail = newThumb;
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
                //UpdateImagePosition();
            }
        }
        // Ctrl + 滚轮 缩放事件
        // 改造 FitToWindow，使其接收像素尺寸作为参数
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




        private void UpdateSliderBarValue(double newScale)
        {
            ZoomSlider.Value = newScale;
            ZoomLevel = newScale.ToString("P0");
            ZoomMenu.Text = newScale.ToString("P0");
        }
        private void OnMouseWheelZoom(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;

            e.Handled = false; // 阻止默认滚动
                               // s();
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
            if (_tools.Select is SelectTool st)
            {
                // 假设你的 ctx 实例在 MainWindow 中可以访问
                // 或者你把 ctx 传给这个方法
                st.RefreshOverlay(_ctx);
            }
            if (_tools.Text is TextTool tx)
            {
                tx.DrawTextboxOverlay(_ctx);
            }
                _canvasResizer.UpdateUI();
        }

        // 1. 实现 Ctrl+N 或点击 (+) 按钮
        private void OnNewTabClick(object sender, RoutedEventArgs e)
        {
            CreateNewTab();
        }

        private void CreateNewTab()
        {
            // 创建一个新的 ViewModel
            var newTab = new FileTabItem(null)
            {
                IsNew = true,
                IsSelected = true
                // 可以在这里设置一个默认的白板缩略图
            };

            // 取消其他选中
            foreach (var tab in FileTabs) tab.IsSelected = false;

            // 添加到列表末尾
            FileTabs.Add(newTab);

            // 滚动到最后
            FileTabsScroller.ScrollToRightEnd();

            // TODO: 这里需要调用你加载 Canvas 空白画布的逻辑
            // OpenCanvas(null); 
        }

        // 2. 实现关闭逻辑
        private void OnFileTabCloseClick(object sender, RoutedEventArgs e)
        {
            // 阻止事件冒泡到 Item 的点击事件
            e.Handled = true;

            if (sender is System.Windows.Controls.Button btn && btn.Tag is FileTabItem item)
            {
                CloseTab(item);
            }
        }

        private void CloseTab(FileTabItem item)
        {
            if (item.IsDirty)
            {
                var result = System.Windows.MessageBox.Show($"图片 {item.FileName} 尚未保存，是否保存？", "保存提示", MessageBoxButton.YesNoCancel);
                if (result == MessageBoxResult.Cancel) return;
                if (result == MessageBoxResult.Yes)
                {
                    // TODO: 调用保存逻辑
                    // SaveFile(item);
                }
            }

            // 从 UI 集合移除
            FileTabs.Remove(item);

            // 如果移除的是当前选中的，需要选中另一个
            if (item.IsSelected && FileTabs.Count > 0)
            {
                // 简单策略：选中最后一个，或者选中相邻的
                var last = FileTabs.Last();
                last.IsSelected = true;
                // OpenImageAndTabs(last.FilePath);
            }
        }

        // 3. 键盘快捷键监听 (建议放在 Window_PreviewKeyDown)
        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.N && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                CreateNewTab();
                e.Handled = true;
            }
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

        private void ResizeCanvas(int newWidth, int newHeight)
        {
            var oldBitmap = _surface.Bitmap;
            if (oldBitmap == null) return; // 如果尺寸没有变化，则不执行任何操作
            if (oldBitmap.PixelWidth == newWidth && oldBitmap.PixelHeight == newHeight) return;

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

        private void OnPenClick(object sender, RoutedEventArgs e)
        {
            SetBrushStyle(BrushStyle.Pencil);
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



    }
    public class HalfValueConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double d) return d / 2.0;
            return 0.0;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }
}
