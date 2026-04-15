//
//UserControls.cs
//负责在主窗口中延迟初始化并注入各个自定义控件（ImageBar、MenuBar、ToolBar等），并绑定相关事件。
//
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
using static TabPaint.MainWindow;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {

        private void InitializeLazyControls()
        {
            Dispatcher.BeginInvoke(new Action(InitializeImageBar), DispatcherPriority.Render);
            Dispatcher.BeginInvoke(new Action(InitializeToolBar), DispatcherPriority.Loaded);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                InitializeMenuBar();
                InitializeStatusBar();
                if (MyStatusBar != null)
                {
                    MyStatusBar.ZoomSliderControl.ValueChanged += (s, e) =>
                    {
                        if (_isInternalZoomUpdate|| !_startupFinished) return;
                        double sliderVal = MyStatusBar.ZoomSliderControl.Value;
                        double targetScale = SliderToZoom(sliderVal);
SetZoom(targetScale, slient: true);
                    };
                }
                SetCropButtonState();
            }), DispatcherPriority.Background);
            Dispatcher.BeginInvoke(new Action(InitializeDragWatchdog), DispatcherPriority.ApplicationIdle);
        }

        private void InitializeImageBar()
        {
            MainImageBar = new ImageBarControl();
            var viewModeBinding = new Binding("IsViewMode") { Source = this, Mode = BindingMode.OneWay };
            BindingOperations.SetBinding(MainImageBar, ImageBarControl.IsViewModeProperty, viewModeBinding);
            MainImageBar.SaveAllClick += OnSaveAllClick;
            MainImageBar.SaveAllDoubleClick += OnSaveAllDoubleClick;
            MainImageBar.ClearUneditedClick += OnClearUneditedClick;
            MainImageBar.DiscardAllClick += OnDiscardAllClick;
            MainImageBar.PrependTabClick += OnPrependTabClick;
            MainImageBar.NewTabClick += OnNewTabClick;
            MainImageBar.TabStickImageClick += OnTabStickImageClick;
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
            MainImageBar.BackgroundMouseDown += BackgroundMouseDown_Handler;
            MainImageBar.TabRenameClick += OnTabRenameClick;
            MainImageBar.TabCopyClick += OnTabCopyClick;
            MainImageBar.TabCutClick += OnTabCutClick;
            MainImageBar.TabPasteClick += OnTabPasteClick;
            MainImageBar.TabOpenFolderClick += OnTabOpenFolderClick;
            MainImageBar.TabDeleteClick += OnTabDeleteClick;
            MainImageBar.TabCloseOthersClick += OnTabCloseOthersClick;
            MainImageBar.TabMoveToNewWindowClick += OnTabMoveToNewWindowClick;
            MainImageBar.TabNewTabRightClick += OnTabNewTabRightClick;
            MainImageBar.TabFileDeleteClick += OnTabFileDeleteClick;
            ImageBarHolder.Content = MainImageBar;
            if (MainImageBar != null)
                MainImageBar.IsCompactMode = SettingsManager.Instance.Current.IsImageBarCompact;
        }

        private void InitializeMenuBar()
        {
            MainMenu = new MenuBarControl();
            MainMenu.NewClick += OnNewClick;
            MainMenu.OpenClick += OnOpenClick;
            MainMenu.OpenWorkspaceClick += OnOpenWorkspaceClick;
            MainMenu.PrintClick += OnPrintClick;
            MainMenu.SaveClick += OnSaveClick;
            MainMenu.SaveAsClick += OnSaveAsClick;
            MainMenu.SaveAsPdfClick += OnSaveAsPdfClick;
            MainMenu.NewWindowClick += OnNewWindowClick;
            MainMenu.ExitClick += OnExitClick;
            MainMenu.RecentFileClick += OnRecentFileClick;
            MainMenu.RecycleBinClick += OnRecycleBinClick;
            MainMenu.ClearRecentFilesClick += OnClearRecentFilesClick;
            MainMenu.WatermarkClick += OnWatermarkClick;
            MainMenu.CopyClick += OnCopyClick;
            MainMenu.CutClick += OnCutClick;
            MainMenu.PasteClick += OnPasteClick;
            MainMenu.ResizeCanvasClick += OnResizeCanvasClick;
            MainMenu.BCEClick += OnBrightnessContrastExposureClick;
            MainMenu.TTSClick += OnColorTempTintSaturationClick;
            MainMenu.BlackWhiteClick += OnConvertToBlackAndWhiteClick;
            MainMenu.InvertClick += OnInvertColorsClick;
            MainMenu.AutoLevelsClick += OnAutoLevelsClick;
            MainMenu.UndoClick += OnUndoClick;
            MainMenu.RedoClick += OnRedoClick;
            MainMenu.SettingsClick += OnSettingsClick;
            MainMenu.NewTabClick += OnNewTabClick;
            MainMenu.DiscardAllClick += OnDiscardAllClick;
            MainMenu.DiscardImageClick += OnDiscardImageClick;
            MainMenu.SepiaClick += OnSepiaClick;
            MainMenu.OilPaintingClick += OnOilPaintingClick;
            MainMenu.VignetteClick += OnVignetteClick;
            MainMenu.GlowClick += OnGlowClick;
            MainMenu.SharpenClick += OnSharpenClick;
            MainMenu.BrownClick += OnBrownClick;
            MainMenu.MosaicClick += OnMosaicClick;
            MainMenu.GaussianBlurClick += OnGaussianBlurClick;
            MainMenu.RedEyeClick += OnRedEyeClick;
            MainMenu.SketchClick += OnSketchClick;
            MainMenu.EdgeClick += OnEdgeClick;
            MenuBarHolder.Content = MainMenu;

            SetUndoRedoButtonState();
        }

        private void InitializeToolBar()
        {
            MainToolBar = new ToolBarControl();
            MainToolBar.PenClick += OnPenClick;
            MainToolBar.PickColorClick += OnPickColorClick;
            MainToolBar.EraserClick += OnEraserClick;
            MainToolBar.SelectClick += OnSelectClick;
            MainToolBar.FillClick += OnFillClick;
            MainToolBar.TextClick += OnTextClick;
            MainToolBar.BrushStyleClick += OnBrushStyleClick;
            MainToolBar.ShapeStyleClick += OnShapeStyleClick;
            MainToolBar.BrushMainClick += OnBrushMainClick;
            MainToolBar.ShapeMainClick += OnShapeMainClick;
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
            MainToolBar.BackgroundMouseDown += BackgroundMouseDown_Handler;
            MainToolBar.SelectMainClick += OnSelectMainClick;
            MainToolBar.SelectStyleClick += OnSelectStyleClick;
            ToolBarHolder.Content = MainToolBar;
            UpdateToolSelectionHighlight();
            UpdateColorHighlight();
            SetCropButtonState();
        }

        private void InitializeStatusBar()
        {
            MyStatusBar = new StatusBarControl();
            MyStatusBar.ClipboardMonitorClick += ClipboardMonitorToggle_Click;
            MyStatusBar.FavoriteClick += (s, e) => ToggleFavoriteWindow();
            MyStatusBar.CommandBarToggleRequested += OnStatusCommandBarToggleRequested;
            MyStatusBar.FitToWindowClick += FitToWindow_Click;
            MyStatusBar.ZoomOutClick += ZoomOut_Click;
            MyStatusBar.ZoomInClick += ZoomIn_Click;
            MyStatusBar.ZoomChanged += OnStatusBarZoomChanged;
            AppTitleBar.NewClick += OnNewClick;
            AppTitleBar.OpenClick += OnOpenClick;
            AppTitleBar.OpenWorkspaceClick += OnOpenWorkspaceClick;
            AppTitleBar.SaveClick += OnSaveClick;
            AppTitleBar.SaveAsClick += OnSaveAsClick;
            AppTitleBar.SettingsClick += OnSettingsClick;
            AppTitleBar.ExitClick += OnExitClick;
            AppTitleBar.IconDragRequest += OnAppTitleBarIconDragRequest;
            AppTitleBar.LogoMiddleClick += OnAppTitleBarLogoMiddleClick;
            AppTitleBar.HelpClick += OnHelpClick;
            AppTitleBar.CloseClick += OnTitleBarCloseClick;
            AppTitleBar.MinimizeClick += OnTitleBarMinimizeClick;
            AppTitleBar.MaximizeRestoreClick += OnTitleBarMaximizeRestoreClick;

            StatusBarHolder.Content = MyStatusBar;
            ApplyStatusCommandBarExpandedState(SettingsManager.Instance.Current.IsStatusCommandBarExpanded, adjustWindowHeight: false);

            UpdateUIStatus(zoomscale);
        }



    }
}