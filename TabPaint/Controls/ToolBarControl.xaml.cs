using System.Windows;
using System.Windows.Controls;

namespace TabPaint.Controls
{
    public partial class ToolBarControl : UserControl
    {
        // ================= 定义路由事件 =================

        // 基础工具
        public static readonly RoutedEvent PenClickEvent = RegisterEvent("PenClick");
        public static readonly RoutedEvent PickColorClickEvent = RegisterEvent("PickColorClick");
        public static readonly RoutedEvent EraserClickEvent = RegisterEvent("EraserClick");
        public static readonly RoutedEvent SelectClickEvent = RegisterEvent("SelectClick");
        public static readonly RoutedEvent FillClickEvent = RegisterEvent("FillClick");
        public static readonly RoutedEvent TextClickEvent = RegisterEvent("TextClick");

        // 样式点击 (画刷/形状) - 需要传递 Tag 或 Source
        public static readonly RoutedEvent BrushStyleClickEvent = RegisterEvent("BrushStyleClick");
        public static readonly RoutedEvent ShapeStyleClickEvent = RegisterEvent("ShapeStyleClick");

        // 编辑操作
        public static readonly RoutedEvent CropClickEvent = RegisterEvent("CropClick");
        public static readonly RoutedEvent RotateLeftClickEvent = RegisterEvent("RotateLeftClick");
        public static readonly RoutedEvent RotateRightClickEvent = RegisterEvent("RotateRightClick");
        public static readonly RoutedEvent Rotate180ClickEvent = RegisterEvent("Rotate180Click");
        public static readonly RoutedEvent FlipVerticalClickEvent = RegisterEvent("FlipVerticalClick");
        public static readonly RoutedEvent FlipHorizontalClickEvent = RegisterEvent("FlipHorizontalClick");

        // 颜色操作
        public static readonly RoutedEvent CustomColorClickEvent = RegisterEvent("CustomColorClick");
        public static readonly RoutedEvent ColorOneClickEvent = RegisterEvent("ColorOneClick");
        public static readonly RoutedEvent ColorTwoClickEvent = RegisterEvent("ColorTwoClick");
        public static readonly RoutedEvent ColorButtonClickEvent = RegisterEvent("ColorButtonClick");

        // ================= 事件包装器 =================

        public event RoutedEventHandler PenClick { add => AddHandler(PenClickEvent, value); remove => RemoveHandler(PenClickEvent, value); }
        public event RoutedEventHandler PickColorClick { add => AddHandler(PickColorClickEvent, value); remove => RemoveHandler(PickColorClickEvent, value); }
        public event RoutedEventHandler EraserClick { add => AddHandler(EraserClickEvent, value); remove => RemoveHandler(EraserClickEvent, value); }
        public event RoutedEventHandler SelectClick { add => AddHandler(SelectClickEvent, value); remove => RemoveHandler(SelectClickEvent, value); }
        public event RoutedEventHandler FillClick { add => AddHandler(FillClickEvent, value); remove => RemoveHandler(FillClickEvent, value); }
        public event RoutedEventHandler TextClick { add => AddHandler(TextClickEvent, value); remove => RemoveHandler(TextClickEvent, value); }

        public event RoutedEventHandler BrushStyleClick { add => AddHandler(BrushStyleClickEvent, value); remove => RemoveHandler(BrushStyleClickEvent, value); }
        public event RoutedEventHandler ShapeStyleClick { add => AddHandler(ShapeStyleClickEvent, value); remove => RemoveHandler(ShapeStyleClickEvent, value); }

        public event RoutedEventHandler CropClick { add => AddHandler(CropClickEvent, value); remove => RemoveHandler(CropClickEvent, value); }
        public event RoutedEventHandler RotateLeftClick { add => AddHandler(RotateLeftClickEvent, value); remove => RemoveHandler(RotateLeftClickEvent, value); }
        public event RoutedEventHandler RotateRightClick { add => AddHandler(RotateRightClickEvent, value); remove => RemoveHandler(RotateRightClickEvent, value); }
        public event RoutedEventHandler Rotate180Click { add => AddHandler(Rotate180ClickEvent, value); remove => RemoveHandler(Rotate180ClickEvent, value); }
        public event RoutedEventHandler FlipVerticalClick { add => AddHandler(FlipVerticalClickEvent, value); remove => RemoveHandler(FlipVerticalClickEvent, value); }
        public event RoutedEventHandler FlipHorizontalClick { add => AddHandler(FlipHorizontalClickEvent, value); remove => RemoveHandler(FlipHorizontalClickEvent, value); }

        public event RoutedEventHandler CustomColorClick { add => AddHandler(CustomColorClickEvent, value); remove => RemoveHandler(CustomColorClickEvent, value); }
        public event RoutedEventHandler ColorOneClick { add => AddHandler(ColorOneClickEvent, value); remove => RemoveHandler(ColorOneClickEvent, value); }
        public event RoutedEventHandler ColorTwoClick { add => AddHandler(ColorTwoClickEvent, value); remove => RemoveHandler(ColorTwoClickEvent, value); }
        public event RoutedEventHandler ColorButtonClick { add => AddHandler(ColorButtonClickEvent, value); remove => RemoveHandler(ColorButtonClickEvent, value); }


        public ToolBarControl()
        {
            InitializeComponent();
        }

        // 辅助注册方法
        private static RoutedEvent RegisterEvent(string name)
        {
            return EventManager.RegisterRoutedEvent(name, RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToolBarControl));
        }

        // ================= 内部转发方法 (XAML Click 指向这里) =================

        private void OnPenClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(PenClickEvent));
        private void OnPickColorClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(PickColorClickEvent));
        private void OnEraserClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(EraserClickEvent));
        private void OnSelectClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SelectClickEvent));
        private void OnFillClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(FillClickEvent));
        private void OnTextClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(TextClickEvent));

        // 注意：MenuItem 的 Tag 和 Source 需要保留，所以直接传 e，或者把 Source 设为触发的 MenuItem
        private void OnBrushStyleClick_Forward(object sender, RoutedEventArgs e)
        {
            // 关闭 Popup
            BrushToggle.IsChecked = false;
            // 转发事件，保持 Source 为被点击的 MenuItem，这样 MainWindow 可以读取 Tag
            RaiseEvent(new RoutedEventArgs(BrushStyleClickEvent, sender));
        }

        private void OnShapeStyleClick_Forward(object sender, RoutedEventArgs e)
        {
            ShapeToggle.IsChecked = false;
            RaiseEvent(new RoutedEventArgs(ShapeStyleClickEvent, sender));
        }

        private void CropMenuItem_Click_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(CropClickEvent));
        private void OnRotateLeftClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(RotateLeftClickEvent));
        private void OnRotateRightClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(RotateRightClickEvent));

        private void OnRotate180Click_Forward(object sender, RoutedEventArgs e)
        {
            RotateFlipMenuToggle.IsChecked = false;
            RaiseEvent(new RoutedEventArgs(Rotate180ClickEvent));
        }
        private void OnFlipVerticalClick_Forward(object sender, RoutedEventArgs e)
        {
            RotateFlipMenuToggle.IsChecked = false;
            RaiseEvent(new RoutedEventArgs(FlipVerticalClickEvent));
        }
        private void OnFlipHorizontalClick_Forward(object sender, RoutedEventArgs e)
        {
            RotateFlipMenuToggle.IsChecked = false;
            RaiseEvent(new RoutedEventArgs(FlipHorizontalClickEvent));
        }

        private void OnCustomColorClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(CustomColorClickEvent));
        private void OnColorOneClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ColorOneClickEvent, sender)); // 需要 sender 获取 Tag
        private void OnColorTwoClick_Forward(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ColorTwoClickEvent, sender));

        private void OnColorButtonClick_Forward(object sender, RoutedEventArgs e)
        {
            // 列表中的颜色块，sender是Button，DataContext是颜色Brush
            RaiseEvent(new RoutedEventArgs(ColorButtonClickEvent, sender));
        }
    }
}
