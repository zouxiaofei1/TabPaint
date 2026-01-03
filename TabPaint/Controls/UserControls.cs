using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using TabPaint.Controls;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
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
        private void InitializeLazyControls()
        {
            MainImageBar = new ImageBarControl();

            var viewModeBinding = new Binding("IsViewMode") { Source = this, Mode = BindingMode.OneWay };
            BindingOperations.SetBinding(MainImageBar, ImageBarControl.IsViewModeProperty, viewModeBinding);

            // 2. 恢复事件订阅 (从你提供的 XAML 逐一搬运)
            MainImageBar.SaveAllClick += OnSaveAllClick;
            MainImageBar.SaveAllDoubleClick += OnSaveAllDoubleClick;
            MainImageBar.ClearUneditedClick += OnClearUneditedClick;
            MainImageBar.DiscardAllClick += OnDiscardAllClick;
            MainImageBar.PrependTabClick += OnPrependTabClick;
            MainImageBar.NewTabClick += OnNewTabClick;

            MainImageBar.FileTabClick += OnFileTabClick;
            MainImageBar.FileTabCloseClick += OnFileTabCloseClick;

            MainImageBar.FileTabPreviewMouseDown += OnFileTabPreviewMouseDown;
            MainImageBar.FileTabPreviewMouseMove += OnFileTabPreviewMouseMove;
            MainImageBar.FileTabDrop += OnFileTabDrop;
            MainImageBar.FileTabLeave += OnFileTabLeave;
            MainImageBar.FileTabReorderDragOver += OnFileTabReorderDragOver;

            MainImageBar.FileTabsWheelScroll += OnFileTabsWheelScroll;
            MainImageBar.FileTabsScrollChanged += OnFileTabsScrollChanged;
            MainImageBar.PreviewSliderValueChanged += PreviewSlider_ValueChanged;
            MainImageBar.SliderPreviewMouseWheel += Slider_PreviewMouseWheel;

            MainImageBar.TabCopyClick += OnTabCopyClick;
            MainImageBar.TabCutClick += OnTabCutClick;
            MainImageBar.TabPasteClick += OnTabPasteClick;
            MainImageBar.TabOpenFolderClick += OnTabOpenFolderClick;
            MainImageBar.TabDeleteClick += OnTabDeleteClick;
            MainImageBar.TabFileDeleteClick += OnTabFileDeleteClick;

            // 3. 注入到界面
            ImageBarHolder.Content = MainImageBar;

            MainMenu = new MenuBarControl();


            // 事件订阅
            MainMenu.NewClick += OnNewClick;
            MainMenu.OpenClick += OnOpenClick;
            MainMenu.SaveClick += OnSaveClick;
            MainMenu.SaveAsClick += OnSaveAsClick;
            MainMenu.ExitClick += OnExitClick;

            MainMenu.CopyClick += OnCopyClick;
            MainMenu.CutClick += OnCutClick;
            MainMenu.PasteClick += OnPasteClick;
            MainMenu.ResizeCanvasClick += OnResizeCanvasClick;

            MainMenu.BCEClick += OnBrightnessContrastExposureClick;
            MainMenu.TTSClick += OnColorTempTintSaturationClick;
            MainMenu.BlackWhiteClick += OnConvertToBlackAndWhiteClick;

            MainMenu.UndoClick += OnUndoClick;
            MainMenu.RedoClick += OnRedoClick;
            MainMenu.SettingsClick += OnSettingsClick;

            // 注入到界面
            MenuBarHolder.Content = MainMenu;

            MainToolBar = new ToolBarControl();

            // 事件订阅
            MainToolBar.PenClick += OnPenClick;
            MainToolBar.PickColorClick += OnPickColorClick;
            MainToolBar.EraserClick += OnEraserClick;
            MainToolBar.SelectClick += OnSelectClick;
            MainToolBar.FillClick += OnFillClick;
            MainToolBar.TextClick += OnTextClick;

            MainToolBar.BrushStyleClick += OnBrushStyleClick;
            MainToolBar.ShapeStyleClick += OnShapeStyleClick;

            MainToolBar.CropClick += CropMenuItem_Click;
            MainToolBar.RotateLeftClick += OnRotateLeftClick;
            MainToolBar.RotateRightClick += OnRotateRightClick;
            MainToolBar.Rotate180Click += OnRotate180Click;
            MainToolBar.FlipVerticalClick += OnFlipVerticalClick;
            MainToolBar.FlipHorizontalClick += OnFlipHorizontalClick;

            MainToolBar.CustomColorClick += OnCustomColorClick;
            MainToolBar.ColorOneClick += OnColorOneClick;
            MainToolBar.ColorTwoClick += OnColorTwoClick;
            MainToolBar.ColorButtonClick += OnColorButtonClick;

            // 注入到界面
            ToolBarHolder.Content = MainToolBar;


            MyStatusBar = new StatusBarControl();

            // 事件订阅
            MyStatusBar.ClipboardMonitorClick += ClipboardMonitorToggle_Click;
            MyStatusBar.FitToWindowClick += FitToWindow_Click;
            MyStatusBar.ZoomOutClick += ZoomOut_Click;
            MyStatusBar.ZoomInClick += ZoomIn_Click;
            MyStatusBar.ZoomSelectionChanged += ZoomMenu_SelectionChanged;

            // 注入到界面
            StatusBarHolder.Content = MyStatusBar;

            _dragWatchdog = new DispatcherTimer();
            _dragWatchdog.Interval = TimeSpan.FromMilliseconds(200); // 200ms 检查一次，性能消耗可忽略
            _dragWatchdog.Tick += DragWatchdog_Tick;
        }
    }
}