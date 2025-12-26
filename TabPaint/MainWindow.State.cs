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
        private string _programVersion = "v0.6.5 alpha"; // 可以从 Assembly 读取
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
        public enum BrushStyle { Round, Square, Brush, Spray, Pencil, Eraser, Watercolor, Crayon, Highlighter, Mosaic }
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
        private bool _maximized = false;
        private Rect _restoreBounds;
        private string _currentFilePath = string.Empty;
        private Point _dragStartPoint;
        private bool _draggingFromMaximized = false;
        public class PaintSession
        {
            public string LastViewedFile { get; set; } // 上次正在看的文件
            public List<SessionTabInfo> Tabs { get; set; } = new List<SessionTabInfo>();
        }


        public class SessionTabInfo
        {
            public string Id { get; set; }
            public string OriginalPath { get; set; }
            public string BackupPath { get; set; }
            public bool IsDirty { get; set; }
            public bool IsNew { get; set; }

            // [新增] 记录该标签页所属的工作目录
            public string WorkDirectory { get; set; }
        }

        private string _sessionPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TabPaint", "session.json");
        private readonly string _cacheDir = System.IO.Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "TabPaint", "Cache");
        private System.Windows.Threading.DispatcherTimer _autoSaveTimer;
        public CanvasResizeManager _canvasResizer;
        private int _savedUndoPoint = 0;
    }
}