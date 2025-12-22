
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

//
//SelectTool类的定义
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public partial class SelectTool : ToolBase
        {
            public override string Name => "Select";
            public override System.Windows.Input.Cursor Cursor => System.Windows.Input.Cursors.Cross;

            public bool _selecting = false;
            public bool _draggingSelection = false;

            private Point _startPixel;
            private Point _clickOffset;
            public Int32Rect _selectionRect;
            private Int32Rect _originalRect;
            public byte[]? _selectionData;
            private int _transformStep = 0; // 0 = 未操作，>0 = 已操作
            private byte[]? _clipboardData;
            private int _clipboardWidth;
            private int _clipboardHeight;


            private ResizeAnchor _currentAnchor = ResizeAnchor.None;
            private bool _resizing = false;
            private Point _startMouse;
            private double _startW, _startH, _startX, _startY;

            // 句柄尺寸
            private const double HandleSize = 6;

            public enum ResizeAnchor
            {
                None,
                TopLeft, TopMiddle, TopRight,
                LeftMiddle, RightMiddle,
                BottomLeft, BottomMiddle, BottomRight
            }

         

           
        }

    }
}