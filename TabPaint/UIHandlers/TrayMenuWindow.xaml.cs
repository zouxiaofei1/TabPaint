using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace TabPaint.UIHandlers
{
    public partial class TrayMenuWindow : Window
    {
        public event EventHandler? OpenRequested;
        public event EventHandler? ExitRequested;

        private bool _allowDirectClose;
        private Storyboard? _iconBreathingStoryboard;

        private FrameworkElement? MenuCardElement => FindName("MenuCard") as FrameworkElement;
        private System.Windows.Controls.Image? TopIconElement => FindName("TopIconImage") as System.Windows.Controls.Image;
        private System.Windows.Media.ScaleTransform? MenuScaleTransform => FindName("MenuScale") as System.Windows.Media.ScaleTransform;
        private System.Windows.Media.TranslateTransform? MenuTranslateTransform => FindName("MenuTranslate") as System.Windows.Media.TranslateTransform;
        private System.Windows.Media.ScaleTransform? IconScaleTransform => FindName("IconScale") as System.Windows.Media.ScaleTransform;
        private System.Windows.Media.RotateTransform? IconRotateTransform => FindName("IconRotate") as System.Windows.Media.RotateTransform;

        public TrayMenuWindow()
        {
            InitializeComponent();
            Deactivated += (_, _) => RequestClose();
            PreviewKeyDown += TrayMenuWindow_PreviewKeyDown;
        }

        public void ShowAtCursor()
        {
            // 1. ������Ļ����ʾ���� WPF ��ɲ��ֲ���
            Left = -10000;
            Top = -10000;
            Show();
            UpdateLayout();

            // 2. ���� ActualWidth/ActualHeight ��ֵ��
            GetCursorPos(out var p);

            // 3. DPI ת������Ļ�������� �� WPF �豸�޹ص�λ
            var source = PresentationSource.FromVisual(this);
            double dpiScaleX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
            double dpiScaleY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

            double cursorX = p.X * dpiScaleX;
            double cursorY = p.Y * dpiScaleY;

            // 4. ��λ��������Ϸ�
            Left = cursorX - ActualWidth;
            Top = cursorY - ActualHeight;

            // 5. �߽籣��
            if (Left < 0) Left = 8;
            if (Top < 0) Top = 8;

            // 6. ȷ����������Ļ����ȡ��������
            var workArea = SystemParameters.WorkArea;
            if (Left + ActualWidth > workArea.Right)
                Left = workArea.Right - ActualWidth - 8;
            if (Top + ActualHeight > workArea.Bottom)
                Top = workArea.Bottom - ActualHeight - 8;

            Activate();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadBestIcoFrame();
            BeginMenuEntranceAnimation();
            StartIconBreathingAnimation();
        }

        private void LoadBestIcoFrame()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/Resources/TabPaint.ico", UriKind.Absolute);
                var streamInfo = Application.GetResourceStream(uri);
                if (streamInfo?.Stream == null) return;

                using (streamInfo.Stream)
                {
                    var decoder = new IconBitmapDecoder(
                        streamInfo.Stream,
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.OnLoad);

                    var bestFrame = decoder.Frames
                        .OrderByDescending(f => f.PixelWidth * f.PixelHeight)
                        .FirstOrDefault();

                    if (bestFrame != null)
                    {
                        bestFrame.Freeze();
                        if (TopIconElement != null)
                        {
                            TopIconElement.Source = bestFrame;
                        }
                    }
                }
            }
            catch
            {
                // Keep default source if decoder fails.
            }
        }

        private void BeginMenuEntranceAnimation()
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var fade = new DoubleAnimation(0, 1, AnimationHelper.GetScaledTimeSpan(200))
            {
                EasingFunction = ease
            };
            MenuCardElement?.BeginAnimation(OpacityProperty, fade);

            var scaleX = new DoubleAnimation(0.94, 1.0, AnimationHelper.GetScaledTimeSpan(220))
            {
                EasingFunction = ease
            };
            var scaleY = new DoubleAnimation(0.94, 1.0, AnimationHelper.GetScaledTimeSpan(220))
            {
                EasingFunction = ease
            };
            var slide = new DoubleAnimation(8, 0, AnimationHelper.GetScaledTimeSpan(220))
            {
                EasingFunction = ease
            };

            MenuScaleTransform?.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleX);
            MenuScaleTransform?.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleY);
            MenuTranslateTransform?.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slide);
        }

        private void StartIconBreathingAnimation()
        {
            _iconBreathingStoryboard?.Stop();

            var breatheX = new DoubleAnimation(1.0, 1.04, AnimationHelper.GetScaledTimeSpan(1500))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            var breatheY = new DoubleAnimation(1.0, 1.04, AnimationHelper.GetScaledTimeSpan(1500))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            _iconBreathingStoryboard = new Storyboard();
            if (IconScaleTransform == null) return;

            Storyboard.SetTarget(breatheX, IconScaleTransform);
            Storyboard.SetTargetProperty(breatheX, new PropertyPath(System.Windows.Media.ScaleTransform.ScaleXProperty));
            Storyboard.SetTarget(breatheY, IconScaleTransform);
            Storyboard.SetTargetProperty(breatheY, new PropertyPath(System.Windows.Media.ScaleTransform.ScaleYProperty));

            _iconBreathingStoryboard.Children.Add(breatheX);
            _iconBreathingStoryboard.Children.Add(breatheY);
            _iconBreathingStoryboard.Begin();
        }

        private void TopIconImage_MouseEnter(object sender, MouseEventArgs e)
        {
            _iconBreathingStoryboard?.Stop();

            if (IconScaleTransform == null || IconRotateTransform == null) return;

            var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 };
            IconScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation(1.08, AnimationHelper.GetScaledTimeSpan(140)) { EasingFunction = ease });
            IconScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation(1.08, AnimationHelper.GetScaledTimeSpan(140)) { EasingFunction = ease });
            IconRotateTransform.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, new DoubleAnimation(6, AnimationHelper.GetScaledTimeSpan(140)) { EasingFunction = ease });
        }

        private void TopIconImage_MouseLeave(object sender, MouseEventArgs e)
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            IconRotateTransform?.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, new DoubleAnimation(0, AnimationHelper.GetScaledTimeSpan(150)) { EasingFunction = ease });
            IconScaleTransform?.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, AnimationHelper.GetScaledTimeSpan(180)) { EasingFunction = ease });
            IconScaleTransform?.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, AnimationHelper.GetScaledTimeSpan(180)) { EasingFunction = ease });
            StartIconBreathingAnimation();
        }

        private void TrayMenuWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                RequestClose();
            }
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            OpenRequested?.Invoke(this, EventArgs.Empty);
            RequestClose();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
            RequestClose();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_allowDirectClose)
            {
                e.Cancel = true;
                BeginMenuCloseAnimation();
                return;
            }

            base.OnClosing(e);
        }

        private void RequestClose()
        {
            if (_allowDirectClose) return;
            Close();
        }

        private void BeginMenuCloseAnimation()
        {
            _iconBreathingStoryboard?.Stop();

            var currentOpacity = MenuCardElement?.Opacity ?? 1.0;
            var fade = new DoubleAnimation(currentOpacity, 0, AnimationHelper.GetScaledTimeSpan(110));
            MenuCardElement?.BeginAnimation(OpacityProperty, fade);

            var currentY = MenuTranslateTransform?.Y ?? 0;
            var down = new DoubleAnimation(currentY, 6, AnimationHelper.GetScaledTimeSpan(110));
            MenuTranslateTransform?.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, down);

            fade.Completed += (_, _) =>
            {
                _allowDirectClose = true;
                Close();
            };
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }
    }
}