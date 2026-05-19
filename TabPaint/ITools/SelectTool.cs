
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

//
//SelectTool类的定义
//

namespace TabPaint
{

    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {

        public partial class SelectTool : ToolBase
        {
            public void SetWandTolerance(int val)
            {
                _wandTolerance = val;
            }
            public int GetWandTolerance()
            {
                return _wandTolerance;
            }

            public SelectionType SelectionType { get; set; } = SelectionType.Rectangle;
            public bool IsPasted = false;
            public override string Name => "Select";
            private System.Windows.Input.Cursor _cachedCursor;
            public override System.Windows.Input.Cursor Cursor
            {
                get { return System.Windows.Input.Cursors.Cross; }
            }
            public Int32Rect SelectionRect => _selectionRect;
            public bool IsSelecting => _selecting;
            public bool HasRulerHighlight =>
                (_selectionData != null && _selectionRect.Width > 0 && _selectionRect.Height > 0)
                || (_selecting && _selectionRect.Width > 0 && _selectionRect.Height > 0);
            public bool _selecting = false;
            public bool _draggingSelection = false;
            private List<Point> _lassoPoints;
            public Geometry? _selectionGeometry;
            public byte[]? _selectionAlphaMap;
            private Point _startPixel;
            private Point _clickOffset;
            public Int32Rect _selectionRect;
            public Int32Rect _originalRect;
            public byte[]? _selectionData;
            internal byte[]? _preRotationSelectionData;
            private int _preRotationDataWidth, _preRotationDataHeight;
            internal Int32Rect _preRotationRect;
            private double _rotationAngle = 0;
            private int _transformStep = 0; // 0 = 未操作，>0 = 已操作
            private byte[]? _clipboardData;
            private int _clipboardWidth;
            private int _clipboardHeight;
            private ResizeAnchor _currentAnchor = ResizeAnchor.None;
            public bool _resizing = false;
            private Point _startMouse;
            private double _startW, _startH, _startX, _startY;
            public int lag = 0;
            // 句柄尺寸
            private DispatcherTimer _tabSwitchTimer;
            private FileTabItem _pendingTab;
            private int _wandTolerance = AppConsts.DefaultWandTolerance; // 当前容差
            private Point _wandStartPoint = new Point(-1, -1); // 点击的起始点
            private Color _wandStartColor; // 起始点的颜色
            private bool _isWandAdjusting = false; // 是否正在拖拽调整容差
            private bool[] _wandMaskBuffer; // 用于缓存全图的选中状态(bool)，避免重复申请内存
            private byte[] _wandAlphaBuffer; // 用于缓存选区的 AlphaMap
            private byte[] _wandPreviewBuffer; // 用于缓存预览遮罩的像素
            // 键盘移动
            private DispatcherTimer _keyMoveTimer;
            private readonly HashSet<Key> _pressedArrowKeys = new HashSet<Key>();
            private DateTime _arrowKeyPressTime;
            private const double KeyMoveBaseStep = 1.0;
            private const double KeyMoveAccelFactor = 0.005;
            private const double KeyMoveMaxStep = 10;
            public enum ResizeAnchor
            {
                None,
                TopLeft, TopMiddle, TopRight,
                LeftMiddle, RightMiddle,
                BottomLeft, BottomMiddle, BottomRight
            }
            public DateTime LastSelectionDeleteTime { get; private set; } = DateTime.MinValue;
            public override void SetCursor(ToolContext ctx)
            {
                System.Windows.Input.Mouse.OverrideCursor = null;
                if (ctx.ViewElement != null) ctx.ViewElement.Cursor = this.Cursor;
            }
        }

    }
}