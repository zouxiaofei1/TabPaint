
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TabPaint.Controls;
using XamlAnimatedGif; // 添加这一行
using static TabPaint.MainWindow;

//
//看图模式
//

namespace TabPaint
{
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)  return b ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v) return v != Visibility.Visible;
            return false;
        }
    }
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private long _lastModeSwitchTick = 0;
        private const long ModeSwitchCooldown = AppConsts.ModeSwitchCooldownTicks;

        private void TriggerModeChange()
        {
            long currentTick = DateTime.Now.Ticks;
            if (currentTick - _lastModeSwitchTick < ModeSwitchCooldown)return;
            _lastModeSwitchTick = currentTick;
            IsViewMode = !IsViewMode;
            OnModeChanged(IsViewMode);
        }
        private Cursor _cachedCursorOpen;
        private Cursor _cachedCursorClosed;

        public Cursor CursorOpenHand
        {
            get
            {
                if (_cachedCursorOpen == null)
                {
                    try
                    {
                        var uri = new Uri("pack://application:,,,/Resources/Cursors/Openhand.cur");
                        var resourceInfo = Application.GetResourceStream(uri);
                        if (resourceInfo != null)   _cachedCursorOpen = new Cursor(resourceInfo.Stream);
                    }
                    catch { _cachedCursorOpen = Cursors.ScrollAll; }
                }
                return _cachedCursorOpen;
            }
        }

        public Cursor CursorClosedHand
        {
            get
            {
                if (_cachedCursorClosed == null)
                {
                    try
                    {
                        var uri = new Uri("pack://application:,,,/Resources/Cursors/Closedhand.cur");
                        var resourceInfo = Application.GetResourceStream(uri);
                        if (resourceInfo != null)   _cachedCursorClosed = new Cursor(resourceInfo.Stream);
                    }
                    catch {  _cachedCursorClosed = Cursors.SizeAll;    }
                }
                return _cachedCursorClosed;
            }
        }
        private void SetViewCursor(bool isPressed = false)
        {
            if (!IsViewMode) return;
            if (isPressed)   Mouse.OverrideCursor = CursorClosedHand;
            else
            {
                // 松开时清除强制覆盖，并设置当前控件光标
                Mouse.OverrideCursor = null;
                CanvasWrapper.Cursor = CursorOpenHand;
                CanvasWrapper.ForceCursor = true;
                ScrollContainer.Cursor = CursorOpenHand;
            }
        }
    

        private void OnModeChanged(bool isView, bool isSilent = false)
        {
            var settings = SettingsManager.Instance.Current;

            if (!isSilent) ShowToast(isView ? "L_Toast_Mode_View" : "L_Toast_Mode_Paint");
            AutoSetFloatBarVisibility();
            CheckBirdEyeVisibility();
            if (isView)
            {
                SetViewCursor(false);
                if (AppTitleBar != null)
                {
                    AppTitleBar.IsLogoMenuEnabled = true;
                    AppTitleBar.TryShowLogoMenuHintOnce();
                }
                _router.CleanUpSelectionandShape();
                if (_router.CurrentTool is TextTool textTool) textTool.Cleanup(_ctx);
                if (_router.CurrentTool is PenTool penTool) penTool.StopDrawing(_ctx);
                if (_isCurrentFileGif)
                {
                    BackgroundImage.Visibility = Visibility.Collapsed; // 隐藏静态图
                    GifPlayerImage.Visibility = Visibility.Visible;    // 显示动态图

                    if (!string.IsNullOrEmpty(_currentFilePath))
                    {
                        AnimationBehavior.SetSourceUri(GifPlayerImage, new Uri(_currentFilePath));
                    }

                    var controller = AnimationBehavior.GetAnimator(GifPlayerImage);
                    controller?.Play();
                }
                if (settings.ViewUseDarkCanvasBackground)
                {
                    // 只有当前不是 Dark 时才切换，避免重复刷新
                    if (ThemeManager.CurrentAppliedTheme != AppTheme.Dark)  ThemeManager.ApplyTheme(AppTheme.Dark);
                }
                RootWindow.MinHeight= 100;
                RootWindow.MinWidth = 150;
                CanvasResizeOverlay.Visibility = Visibility.Collapsed;
                SetViewCursor();
            }
            else
            {
                if (AppTitleBar != null) AppTitleBar.IsLogoMenuEnabled = false;
                if (_isCurrentFileGif)
                {
                    var controller = AnimationBehavior.GetAnimator(GifPlayerImage);
                    controller?.Pause();

                    GifPlayerImage.Visibility = Visibility.Collapsed; // 隐藏动态图
                    BackgroundImage.Visibility = Visibility.Visible;  // 显示静态图 (WriteableBitmap)
                }
                if (ThemeManager.CurrentAppliedTheme != settings.ThemeMode)
                {
                    ThemeManager.ApplyTheme(settings.ThemeMode);
                }
                CanvasResizeOverlay.Visibility = Visibility.Visible;
                RootWindow.MinHeight = 345;
                RootWindow.MinWidth = 430;
                _router.CurrentTool.SetCursor(_ctx);
                System.Windows.Input.Mouse.OverrideCursor = null; ScrollContainer.Cursor = Cursors.Arrow;
                CanvasWrapper.Cursor = null;
                CanvasWrapper.ForceCursor = false;
            }
            UpdateCanvasVisuals();
            UpdateDwmBorderColor();
            if (AppTitleBar != null) AppTitleBar.UpdateModeIcon(IsViewMode);
            AutoUpdateMaximizeIcon();
     
                if (_canvasResizer != null) _canvasResizer.UpdateUI();
            if (!_hasUserManuallyZoomed && _bitmap != null)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    FitToWindow();
                }, DispatcherPriority.Loaded);
            }

        }
        private void OnTitleBarModeSwitch(object sender, RoutedEventArgs e)
        {
    TriggerModeChange();
        }

        private static void OnIsViewModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var window = (MainWindow)d;
            bool isView = (bool)e.NewValue;
            if (isView)
            {
                window.ApplyStatusCommandBarExpandedState(false, adjustWindowHeight: true);
            }
        }

    }
}
