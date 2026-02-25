//
//StatusBarControl.xaml.cs
//底部状态栏控件，显示图片尺寸、鼠标位置、缩放比例，并提供缩放控制和剪贴板监听开关。
//
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace TabPaint.Controls
{
    public class ZoomRoutedEventArgs : RoutedEventArgs
    {
        public double NewZoom { get; }
        public ZoomRoutedEventArgs(RoutedEvent routedEvent, double newZoom) : base(routedEvent)
        {
            NewZoom = newZoom;
        }
    }
    public partial class StatusBarControl : UserControl
    {
        private Storyboard? _ocrBusyStoryboard;

        public static readonly RoutedEvent ClipboardMonitorClickEvent = EventManager.RegisterRoutedEvent(
            "ClipboardMonitorClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(StatusBarControl));

        public static readonly RoutedEvent FitToWindowClickEvent = EventManager.RegisterRoutedEvent(
            "FitToWindowClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(StatusBarControl));

        public static readonly RoutedEvent ZoomOutClickEvent = EventManager.RegisterRoutedEvent(
            "ZoomOutClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(StatusBarControl));

        public static readonly RoutedEvent ZoomInClickEvent = EventManager.RegisterRoutedEvent(
            "ZoomInClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(StatusBarControl));

        public static readonly RoutedEvent ZoomSelectionChangedEvent = EventManager.RegisterRoutedEvent(
            "ZoomSelectionChanged", RoutingStrategy.Bubble, typeof(SelectionChangedEventHandler), typeof(StatusBarControl));
        public static readonly RoutedEvent FavoriteClickEvent = EventManager.RegisterRoutedEvent(
            "FavoriteClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(StatusBarControl));

        public event RoutedEventHandler ClipboardMonitorClick
        {
            add { AddHandler(ClipboardMonitorClickEvent, value); }
            remove { RemoveHandler(ClipboardMonitorClickEvent, value); }
        }
        public event RoutedEventHandler FitToWindowClick
        {
            add { AddHandler(FitToWindowClickEvent, value); }
            remove { RemoveHandler(FitToWindowClickEvent, value); }
        }
        public event RoutedEventHandler ZoomOutClick
        {
            add { AddHandler(ZoomOutClickEvent, value); }
            remove { RemoveHandler(ZoomOutClickEvent, value); }
        }
        public event RoutedEventHandler ZoomInClick
        {
            add { AddHandler(ZoomInClickEvent, value); }
            remove { RemoveHandler(ZoomInClickEvent, value); }
        }
        public event SelectionChangedEventHandler ZoomSelectionChanged
        {
            add { AddHandler(ZoomSelectionChangedEvent, value); }
            remove { RemoveHandler(ZoomSelectionChangedEvent, value); }
        }
        public event RoutedEventHandler FavoriteClick
        {
            add { AddHandler(FavoriteClickEvent, value); }
            remove { RemoveHandler(FavoriteClickEvent, value); }
        }

        public ComboBox ZoomComboBox => ZoomMenu;
        public ToggleButton ClipboardToggle => ClipboardMonitorToggle;
        public ToggleButton FavToggle => FavoriteToggle; 
        public Slider ZoomSliderControl => ZoomSlider;


        public StatusBarControl()
        {
            InitializeComponent();
        }

        public void SetOcrBusyEffect(bool isBusy)
        {
            _ocrBusyStoryboard ??= Resources["OcrBusyStoryboard"] as Storyboard;
            if (_ocrBusyStoryboard == null) return;

            var busyEffect = FindName("OcrBusyEffect") as FrameworkElement;
            if (busyEffect == null) return;

            if (isBusy)
            {
                busyEffect.Visibility = Visibility.Visible;
                _ocrBusyStoryboard.Begin(this, true);
            }
            else
            {
                _ocrBusyStoryboard.Stop(this);
                busyEffect.Visibility = Visibility.Collapsed;
            }
        }

        private void OnClipboardMonitorToggleClick(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ClipboardMonitorClickEvent));
        }

        private void OnFavoriteToggleClick(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(FavoriteClickEvent));
        }

        public void SetFavoriteToggleState(bool isChecked)
        {
            FavoriteToggle.IsChecked = isChecked;
        }
        private void OnFitToWindowClick(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(FitToWindowClickEvent));
        }

        private void OnZoomOutClick(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ZoomOutClickEvent));
        }

        private void OnZoomInClick(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ZoomInClickEvent));
        }

        private void OnZoomMenuSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ZoomMenu.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                if (double.TryParse(item.Tag.ToString(), out double val))
                {
                    RaiseZoomChanged(val);
                }
            }
        }
        public static readonly RoutedEvent ZoomChangedEvent = EventManager.RegisterRoutedEvent(
          "ZoomChanged", RoutingStrategy.Bubble, typeof(EventHandler<ZoomRoutedEventArgs>), typeof(StatusBarControl));
        public event EventHandler<ZoomRoutedEventArgs> ZoomChanged
        {
            add { AddHandler(ZoomChangedEvent, value); }
            remove { RemoveHandler(ZoomChangedEvent, value); }
        }
        private void OnZoomMenuPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TryApplyZoomText();

                e.Handled = true; // 标记已处理，防止发出系统提示音
            }
        }
        private void OnZoomMenuLostFocus(object sender, RoutedEventArgs e)
        {
            TryApplyZoomText();
        }
        private void OnStatusBarMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement element)
            {
                element.Focus();
            }
        }
        private void TryApplyZoomText()
        {
            string text = ZoomMenu.Text.Trim();

            // 去掉可能存在的 % 号
            text = text.Replace("%", "");

            if (double.TryParse(text, out double result))
            {
                double finalZoom = result / 100.0;

                RaiseZoomChanged(finalZoom);
            }
        }
        private void RaiseZoomChanged(double zoom)
        {
            RaiseEvent(new ZoomRoutedEventArgs(ZoomChangedEvent, zoom));
        }
        private void OnStatusBarSizeChanged(object sender, SizeChangedEventArgs e)
        {
            double w = e.NewSize.Width;

            Visibility fileVis = w > AppConsts.StatusBarThresholdFile ? Visibility.Visible : Visibility.Collapsed;
            StatusFileSize.Visibility = fileVis;
            SepFileSize.Visibility = fileVis;
            Visibility mouseVis = w > AppConsts.StatusBarThresholdMouse ? Visibility.Visible : Visibility.Collapsed;
            StatusMousePos.Visibility = mouseVis;
            SepMousePos.Visibility = mouseVis;
            Visibility selVis = w > AppConsts.StatusBarThresholdSelection ? Visibility.Visible : Visibility.Collapsed;
            StatusSelectionSize.Visibility = selVis;
            SepSelectionSize.Visibility = selVis;

            Visibility imgVis = w > AppConsts.StatusBarThresholdImage ? Visibility.Visible : Visibility.Collapsed;
            StatusImageSize.Visibility = imgVis;
            SepImageSize.Visibility = imgVis;

        }
    }
}
