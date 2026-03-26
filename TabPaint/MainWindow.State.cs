//
//MainWindow.State.cs
//MainWindow类的状态部分，包含各种字段、属性定义以及属性变更通知逻辑。
//
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
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


namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private bool _fontsLoaded = false;
        private const int WM_NCHITTEST = AppConsts.WM_NCHITTEST;
        private const int HTLEFT = AppConsts.HTLEFT;
        private const int HTRIGHT = AppConsts.HTRIGHT;
        private const int HTTOP = AppConsts.HTTOP;
        private const int HTTOPLEFT = AppConsts.HTTOPLEFT;
        private const int HTTOPRIGHT = AppConsts.HTTOPRIGHT;
        private const int HTBOTTOM = AppConsts.HTBOTTOM;
        private const int HTBOTTOMLEFT = AppConsts.HTBOTTOMLEFT;
        private const int HTBOTTOMRIGHT = AppConsts.HTBOTTOMRIGHT;


        private const double ZoomStep = AppConsts.DefaultZoomStep; // 每次滚轮缩放步进
        private const double ZoomTimes = AppConsts.ZoomTimes;
        private const double MinZoom = AppConsts.MinZoom;
        private const double MaxZoom = AppConsts.MaxZoom;
        private const int WM_CLIPBOARDUPDATE = AppConsts.WM_CLIPBOARDUPDATE;
        private const int WM_MOUSEHWHEEL = AppConsts.WM_MOUSEHWHEEL;
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        private HwndSource _hwndSource;
        private static MainWindow _activeMonitorInstance;
        private bool _isMonitoringClipboard = false;
        private string _fileSize = "0 KB";
        public string FileSize
        {
            get => _fileSize;
            set
            {
                if (_fileSize != value)
                {
                    _fileSize = value;
                    OnPropertyChanged(nameof(FileSize));
                }
            }
        }
        public CanvasSurface _surface;
        public UndoRedoManager _undo;
        public ToolContext _ctx;
        public InputRouter _router;
        public ToolRegistry _tools;
        public double zoomscale = 1;
        private byte[]? _preDrawSnapshot = null;

        private WriteableBitmap _bitmap;
        private int _bmpWidth, _bmpHeight;
        private Color _penColor = Colors.Black;
        private bool _isDrawing = false;
        public List<string> _imageFiles = new List<string>();
        private int _currentImageIndex = -1;
        private bool _isEdited = false; // 标记当前画布是否被修改
        private string _currentFileName = LocalizationManager.GetString("L_Common_Untitled");
        public string ProgramVersion { get; set; } = AppConsts.ProgramVersion;

        private bool _isFileSaved = true; // 是否有未保存修改

        private string _mousePosition = "0,0" + LocalizationManager.GetString("L_Main_Unit_Pixel");
        public string MousePosition
        {
            get => _mousePosition;
            set { _mousePosition = value; OnPropertyChanged(); }
        }
        private bool _isPanning = false;
        private Point _lastMousePosition;
        private string _imageSize = "0×0" + LocalizationManager.GetString("L_Main_Unit_Pixel");
        public string ImageSize
        {
            get => _imageSize;
            set { _imageSize = value; OnPropertyChanged(); }
        }

        private string _selectionSize = "0×0" + LocalizationManager.GetString("L_Main_Unit_Pixel");
        public string SelectionSize
        {
            get => _selectionSize;
            set { _selectionSize = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private Dictionary<string, ToolSettingsModel> _localPerToolSettings = SettingsManager.Instance.Current.PerToolSettings.ToDictionary(k => k.Key, v => v.Value.Clone());
        public Dictionary<string, ToolSettingsModel> LocalPerToolSettings
        {
            get => _localPerToolSettings;
            set { _localPerToolSettings = value; OnPropertyChanged(); }
        }

        private string _currentToolKey = SettingsManager.Instance.Current.CurrentToolKey;
        public string CurrentToolKey
        {
            get => _currentToolKey;
            set
            {
                if (_currentToolKey != value)
                {
                    _currentToolKey = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PenThickness));
                    OnPropertyChanged(nameof(PenOpacity));
                }
            }
        }

        public double PenThickness
        {
            get
            {
                if (_currentToolKey == "Pen_Pencil") return 1.0;
                if (LocalPerToolSettings != null && LocalPerToolSettings.TryGetValue(_currentToolKey, out var settings))
                {
                    return settings.Thickness;
                }
                return 25.0; // Fallback
            }
            set
            {
                if (_currentToolKey == "Pen_Pencil") return;
                if (LocalPerToolSettings == null)
                {
                    _localPerToolSettings = SettingsManager.Instance.Current.PerToolSettings.ToDictionary(k => k.Key, v => v.Value.Clone());
                }

                if (!LocalPerToolSettings.ContainsKey(_currentToolKey))
                {
                    LocalPerToolSettings[_currentToolKey] = new ToolSettingsModel();
                }

                // 只有当值确实改变，或者 _ctx 中的值不一致时才处理
                bool valueChanged = Math.Abs(LocalPerToolSettings[_currentToolKey].Thickness - value) > 0.001;
                bool ctxOutOfSync = _ctx != null && Math.Abs(_ctx.PenThickness - value) > 0.001;

        if (valueChanged || ctxOutOfSync)
        {
            LocalPerToolSettings[_currentToolKey].Thickness = value;
            if (_ctx != null)
            {
                _ctx.PenThickness = value;
                // 实时同步 ShapeTool 预览
                if (_router?.CurrentTool is ShapeTool shapeTool)
                {
                    shapeTool.RefreshPreview(_ctx);
                }
            }
            OnPropertyChanged();
        }
            }
        }

        public double PenOpacity
        {
            get
            {
                if (LocalPerToolSettings != null && LocalPerToolSettings.TryGetValue(_currentToolKey, out var settings))
                {
                    return settings.Opacity;
                }
                return 1.0;
            }
            set
            {
                if (LocalPerToolSettings == null)
                {
                    _localPerToolSettings = SettingsManager.Instance.Current.PerToolSettings.ToDictionary(k => k.Key, v => v.Value.Clone());
                }
                if (!LocalPerToolSettings.ContainsKey(_currentToolKey))
                {
                    LocalPerToolSettings[_currentToolKey] = new ToolSettingsModel();
                }

                bool valueChanged = Math.Abs(LocalPerToolSettings[_currentToolKey].Opacity - value) > 0.0001;
                bool ctxOutOfSync = _ctx != null && Math.Abs(_ctx.PenOpacity - value) > 0.0001;

                if (valueChanged || ctxOutOfSync)
                {
                    LocalPerToolSettings[_currentToolKey].Opacity = value;
                    if (_ctx != null)
                    {
                        _ctx.PenOpacity = value;
                        // 实时同步 ShapeTool 预览
                        if (_router?.CurrentTool is ShapeTool shapeTool)
                        {
                            shapeTool.RefreshPreview(_ctx);
                        }
                    }
                    OnPropertyChanged();
                }
            }
        }
 
        public enum BrushStyle { Round, Square, Brush, Spray, Pencil, Eraser, Watercolor, Crayon, Highlighter, Mosaic, Calligraphy, AiEraser, GaussianBlur, Gradient }
        public enum UndoActionType
        {
            Draw,         // 普通绘图
            Transform,    // 旋转/翻转
            Selection,
            FileDelete
        }
        private UndoAction _pendingDeleteUndo = null;
        public SelectTool Select;
        public SolidColorBrush ForegroundBrush { get; set; } = new SolidColorBrush(Colors.Black);
        public SolidColorBrush BackgroundBrush { get; set; } = new SolidColorBrush(Colors.White);
        // 当前画笔颜色属性，可供工具使用
        public Color BackgroundColor= Colors.White;
        public Color ForegroundColor= Colors.Black;
        public SolidColorBrush SelectedBrush { get; set; } = new SolidColorBrush(Colors.Black);


        private double _zoomScale = 1.0;
        private bool _isInternalZoomUpdate =false;
        private string _zoomLevel = "100%";
        public string ZoomLevel
        {
            get => _zoomLevel;
            set { _zoomLevel = value; OnPropertyChanged(); }
        }
        private System.Windows.Controls.RichTextBox? _activeTextBox;
        private Stack<UndoAction> _undoStack = new Stack<UndoAction>();
        private List<Int32Rect> _currentDrawRegions = new List<Int32Rect>(); // 当前笔的区域记录
        private Stack<UndoAction> _redoStack = new Stack<UndoAction>();
        string PicFilterString => string.Format(AppConsts.ImageFilterFormat,
            LocalizationManager.GetString("L_Main_Filter_AllImages"),
            LocalizationManager.GetString("L_Main_Filter_PNG"),
            LocalizationManager.GetString("L_Main_Filter_JPEG"),
            LocalizationManager.GetString("L_Main_Filter_WebP"),
            LocalizationManager.GetString("L_Main_Filter_BMP"),
            LocalizationManager.GetString("L_Main_Filter_GIF"),
            LocalizationManager.GetString("L_Main_Filter_TIFF"),
            LocalizationManager.GetString("L_Main_Filter_ICO"),
            LocalizationManager.GetString("L_Main_Filter_SVG"));
        ITool LastTool;
        public bool useSecondColor = false;//是否使用备用颜色
        private bool _maximized = false;
        private Rect _restoreBounds;
        private string _currentFilePath = string.Empty;
        private Point _dragStartPoint;
        private bool _draggingFromMaximized = false;
        private bool _isWindowViewDragging = false;
        private Point _viewDragMouseStartScreen;
        private Point _viewDragWindowStart;
        private const double ViewModeDragMinVisibleWidth = 120;
        private const double ViewModeDragMinVisibleHeight = 80;
        public class PaintSession
        {
            private const int CurrentVersion = 1;
            public string LastViewedFile { get; set; } // 上次正在看的文件
            public List<SessionTabInfo> Tabs { get; set; } = new List<SessionTabInfo>();
            public int ActiveTabIndex { get; set; }

            public void Write(BinaryWriter writer)
            {
                writer.Write(CurrentVersion);
                writer.Write(LastViewedFile ?? "");
                writer.Write(ActiveTabIndex);
                writer.Write(Tabs.Count);
                foreach (var tab in Tabs)
                {
                    tab.Write(writer);
                }
            }

            public static PaintSession Read(BinaryReader reader)
            {
                int version = reader.ReadInt32();
                if (version != 1) throw new InvalidDataException("Unsupported session version");

                var session = new PaintSession();
                session.LastViewedFile = reader.ReadString();
                session.ActiveTabIndex = reader.ReadInt32();
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    session.Tabs.Add(SessionTabInfo.Read(reader, version));
                }
                return session;
            }
        }
        public bool _startupFinished = false;

        public class SessionTabInfo
        {
            public string Id { get; set; }
            public string OriginalPath { get; set; }
            public string BackupPath { get; set; }
            public bool IsDirty { get; set; }
            public bool IsNew { get; set; }
            public int UntitledNumber { get; set; }
            public bool IsCleanDiskFile { get; set; }
            // [新增] 记录该标签页所属的工作目录
            public string WorkDirectory { get; set; }

            public void Write(BinaryWriter writer)
            {
                writer.Write(Id ?? "");
                writer.Write(OriginalPath ?? "");
                writer.Write(BackupPath ?? "");
                writer.Write(IsDirty);
                writer.Write(IsNew);
                writer.Write(UntitledNumber);
                writer.Write(IsCleanDiskFile);
                writer.Write(WorkDirectory ?? "");
            }

            public static SessionTabInfo Read(BinaryReader reader, int version)
            {
                return new SessionTabInfo
                {
                    Id = reader.ReadString(),
                    OriginalPath = reader.ReadString(),
                    BackupPath = reader.ReadString(),
                    IsDirty = reader.ReadBoolean(),
                    IsNew = reader.ReadBoolean(),
                    UntitledNumber = reader.ReadInt32(),
                    IsCleanDiskFile = reader.ReadBoolean(),
                    WorkDirectory = reader.ReadString()
                };
            }
        }
        public string _dragTempDir = AppConsts.DragTempDir;

        private string _sessionPath = AppConsts.SessionPath;
        public readonly string _cacheDir = AppConsts.CacheDir;
        private System.Windows.Threading.DispatcherTimer _autoSaveTimer;
        public CanvasResizeManager _canvasResizer;
        private int _savedUndoPoint = 0;
        private string _currentImageFullInfo;
        public string CurrentImageFullInfo
        {
            get => _currentImageFullInfo;
            set { _currentImageFullInfo = value; OnPropertyChanged(nameof(CurrentImageFullInfo)); }
        }
        private double _originalDpiX = AppConsts.StandardDpi;
        private double _originalDpiY = AppConsts.StandardDpi;
        private bool _isFixedZoom = false;
        public bool IsFixedZoom
        {
            get => _isFixedZoom;
            set
            {
                if (_isFixedZoom != value)
                {
                    _isFixedZoom = value;
                    OnPropertyChanged();
                }
            }
        }
        private bool _isPaintingMode = true;//画图模式
        public bool MicaEnabled = false;
        private bool _isLoadingImage = true;//是否正在加载图像,false时不能画图
        private bool _programClosed = false;
        private string _workingPath;
        public const string InternalClipboardFormat = AppConsts.InternalClipboardFormat;
        public bool _firstFittoWindowdone = false;
        public int PerformanceScore;
        public static readonly DependencyProperty IsViewModeProperty =
     DependencyProperty.Register("IsViewMode", typeof(bool), typeof(MainWindow),
         new PropertyMetadata(false, OnIsViewModeChanged));
        private bool _showRulers = false; // 默认为 false 或从配置读取
        public bool ShowRulers
        {
            get => _showRulers;
            set
            {
                if (_showRulers != value)
                {
                    _showRulers = value;
                    OnPropertyChanged(nameof(ShowRulers));
                    // 切换显示时强制刷新一次位置
                    if (value) UpdateRulerPositions(force: true);
                }
            }
        }
        public bool IsViewMode
        {
            get { return (bool)GetValue(IsViewModeProperty); }
            set { SetValue(IsViewModeProperty, value); }
        }

        public bool CanNavigateImages => ImageFilesCount > 1;

        private int _imageFilesCount;
        public int ImageFilesCount
        {
            get => _imageFilesCount;
            set
            {
                if (_imageFilesCount != value)
                {
                    _imageFilesCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanNavigateImages));
                }
            }
        }

        public Controls.MenuBarControl MainMenu;
        public Controls.ToolBarControl MainToolBar;
        public Controls.ImageBarControl MainImageBar;
        public Controls.StatusBarControl MyStatusBar;
        private const double StatusCommandBarHeightDelta = 46;
        private bool _isStatusCommandBarExpanded = false;

        private DispatcherTimer _toastTimer;
        private DispatcherTimer? _viewModeNavHoldTimer;
        private int _viewModeNavHoldDirection = 0; // -1: prev, 1: next
        private bool _viewModeNavHoldStartedRepeating = false;
        private bool _suppressViewModeNavClickOnce = false;

        public bool BlanketMode = false;
        private bool _isCurrentFileGif = false; // 标记当前文件是否为GIF
        private System.Windows.Threading.DispatcherTimer _deleteCommitTimer;
        private List<FileTabItem> _pendingDeletionTabs = new List<FileTabItem>(); // 待删除列表
        private FileTabItem _lastDeletedTabForUndo = null; // 专门用于 Ctrl+Z 的引用
        private int _lastDeletedTabIndex = -1;
        private bool _isDraggingBirdEye = false;
        private Brush _originalGridBrush; // 用于存储启动时 XAML 里定义的那个格子画刷
        private readonly SolidColorBrush _darkBackgroundBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(AppConsts.DarkBackgroundHex));
        public bool _currentFileExists = true; // 标记当前文件是否存在于磁盘
        private bool _hasUserManuallyZoomed = false;
        private long _lastRulerUpdateTick = long.MinValue;
        private long _lastSelectionToolbarUpdateTick = long.MinValue;

        // 缩放动画参考状态
        private double _zoomStartScale;
        private double _zoomStartScrollH;
        private double _zoomStartScrollV;

        public static ThumbnailCache GlobalThumbnailCache = new ThumbnailCache(AppConsts.MaxThumbnailCacheCount);
        private bool _isUpdatingToolSettings = false;
        public static SemaphoreSlim _thumbnailSemaphore = new SemaphoreSlim(Environment.ProcessorCount);
        private double _visualRotationAngle = 0;
        private bool _isVisualRotating = false;
    }
}
