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
//
//TabPaint主程序
//

namespace TabPaint
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
        private string _programVersion = "v0.6.1 alpha"; // 可以从 Assembly 读取
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


        private void SetBrushStyle(BrushStyle style)
        {//设置画笔样式，所有画笔都是pen工具
            _router.SetTool(_tools.Pen);
            _ctx.PenStyle = style;
            UpdateToolSelectionHighlight();
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
            SetCropButtonState();
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;

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

        private void OnResizeDragDelta(object sender, DragDeltaEventArgs e)
        {
            var thumb = sender as Thumb;
            var tag = thumb.Tag.ToString();

            // 获取当前的缩放倍数
            double zoom = ZoomTransform.ScaleX;

            // 获取当前 UI 尺寸
            double oldWidth = double.IsNaN(BackgroundImage.Width) ? BackgroundImage.ActualWidth : BackgroundImage.Width;
            double oldHeight = double.IsNaN(BackgroundImage.Height) ? BackgroundImage.ActualHeight : BackgroundImage.Height;

            double newWidth = oldWidth;
            double newHeight = oldHeight;

            // --- 水平方向处理 ---
            if (tag.Contains("Left"))
            {
                // 拉左边：宽度增加，且滚动条向右偏移以抵消视觉位移
                double deltaX = e.HorizontalChange;
                double widthChange = -deltaX;

                if (oldWidth + widthChange > 10) // 最小宽度限制
                {
                    newWidth = oldWidth + widthChange;
                    // 补偿位移：由于 Grid 居中，增加宽度会向两边扩张，我们需要补偿滚动位置
                    // 公式：新的偏移 = 当前偏移 + (变化量 * 缩放 / 2) 
                    // 注意：如果是 Left，deltaX 是负数，这里逻辑需要根据你的具体布局微调
                    ScrollContainer.ScrollToHorizontalOffset(ScrollContainer.HorizontalOffset + (deltaX * zoom));
                }
            }
            else if (tag.Contains("Right"))
            {
                // 拉右边：简单增加宽度
                newWidth = Math.Max(10, oldWidth + e.HorizontalChange);
            }

            // --- 垂直方向处理 ---
            if (tag.Contains("Top"))
            {
                double deltaY = e.VerticalChange;
                double heightChange = -deltaY;

                if (oldHeight + heightChange > 10)
                {
                    newHeight = oldHeight + heightChange;
                    ScrollContainer.ScrollToVerticalOffset(ScrollContainer.VerticalOffset + (deltaY * zoom));
                }
            }
            else if (tag.Contains("Bottom"))
            {
                newHeight = Math.Max(10, oldHeight + e.VerticalChange);
            }

            // 更新 UI 尺寸
            BackgroundImage.Width = newWidth;
            BackgroundImage.Height = newHeight;

            // 手动强制同步手柄层 Canvas 的尺寸，如果没用绑定的话
            ResizeHandleCanvas.Width = newWidth;
            ResizeHandleCanvas.Height = newHeight;
        }
        private void OnResizeDragCompleted(object sender, DragCompletedEventArgs e)
        {
            // 1. 获取 UI 最终拉伸后的尺寸
            int finalWidth = (int)BackgroundImage.Width;
            int finalHeight = (int)BackgroundImage.Height;

            // 2. 创建一个新的 WriteableBitmap
            // WriteableBitmap newBmp = new WriteableBitmap(finalWidth, finalHeight, 96, 96, ...);

            // 3. 将旧位图绘制到新位图上
            // 如果是拉右/下：旧图画在 (0,0)
            // 如果是拉左/上：旧图画在 (offset, offset)

            // 4. 更新核心数据源，重置 Image 控件的拉伸
            // BackgroundImage.Source = newBmp;
            // BackgroundImage.Width = double.NaN; // 恢复自动宽度
            // BackgroundImage.Height = double.NaN;
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
