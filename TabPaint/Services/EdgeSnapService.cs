using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace TabPaint.Services
{
    public class EdgeSnapService : IDisposable
    {
        private const double SnapGripSize = 8.0;
        private const double ExpandThreshold = 16.0;
        private const double CollapseThreshold = 32.0;
        private const double DragSnapThreshold = 32.0;
        private const double CollapseDelayMs = 600;
        private const double AnimationDurationMs = 200;
        private const double MonitorIntervalMs = 50;

        // === ������س��� ===
        private const double UnsnapDeadZone = 32.0;

        // ������Ϣ����
        private const int WM_ENTERSIZEMOVE = 0x0231;
        private const int WM_EXITSIZEMOVE = 0x0232;
        private const int WM_MOVING = 0x0216;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        private readonly Window _window;
        private IntPtr _hwnd;
        private HwndSource _hwndSource;
        private bool _isEnabled;
        private bool _isSnapped;
        private bool _isDragging;

        public enum EdgeSnapSide { Left, Right, Top, Bottom }
        private EdgeSnapSide _snapEdge;
        private bool _isExpanded;
        private bool _isAnimating;
        private bool _isDisposed;

        private double _preSnapLeft;
        private double _preSnapTop;

        // === ����״̬�ֶ� ===
        /// <summary>��ק��ʼʱ�Ƿ�������״̬����Ҫ�����߼���</summary>
        private bool _wasSnappedOnDragStart;
        /// <summary>�����Ƿ��Ѿ���ͻ��</summary>
        private bool _deadZoneBreached;
        /// <summary>��ק��ʼʱ����������Ļ���꣨���أ�</summary>
        private POINT _dragStartCursorRaw;
        /// <summary>��ק��ʼʱ���ڵ�������ĻRECT�����أ�</summary>
        private RECT _dragStartWindowRect;
        /// <summary>����ͻ��˲������������Ļ���꣨���أ�</summary>
        private POINT _breachCursorRaw;

        private readonly DispatcherTimer _monitorTimer;
        private readonly DispatcherTimer _collapseTimer;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;
                _isEnabled = value;
                if (_isEnabled)
                {
                    EnsureHook();
                    StartMonitoring();
                }
                else
                {
                    StopMonitoring();
                    if (_isSnapped) RestoreWindowPosition();
                    _isSnapped = false;
                    _isExpanded = false;
                }
                EnabledChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool IsSnapped => _isSnapped;
        public EdgeSnapSide CurrentEdge => _snapEdge;

        public event EventHandler EnabledChanged;

        public EdgeSnapService(Window window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));

            _monitorTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(MonitorIntervalMs)
            };
            _monitorTimer.Tick += OnMonitorTick;

            _collapseTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(CollapseDelayMs)
            };
            _collapseTimer.Tick += OnCollapseTimeout;

            if (_window.IsLoaded) EnsureHook();
            else _window.Loaded += OnWindowLoaded;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            _window.Loaded -= OnWindowLoaded;
            EnsureHook();
        }

        private void EnsureHook()
        {
            if (_hwndSource != null) return;
            _hwnd = new WindowInteropHelper(_window).Handle;
            if (_hwnd == IntPtr.Zero) return;
            _hwndSource = HwndSource.FromHwnd(_hwnd);
            _hwndSource?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_ENTERSIZEMOVE:
                    OnDragStart();
                    break;
                case WM_EXITSIZEMOVE:
                    OnDragEnd();
                    break;
                case WM_MOVING:
                    if (_isDragging && _wasSnappedOnDragStart)
                    {
                        handled = HandleMovingInDeadZone(lParam);
                    }
                    break;
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// ������������WM_MOVING��ǿ�ƴ��ڲ�����
        /// ͻ���������������������ۻ�ƫ�ơ�
        /// </summary>
        private bool HandleMovingInDeadZone(IntPtr lParam)
        {
            // lParam ָ�� RECT�����ڼ����ƶ�����λ�ã��������أ�
            RECT proposedRect = Marshal.PtrToStructure<RECT>(lParam);

            GetCursorPos(out POINT currentCursor);

            if (!_deadZoneBreached)
            {
                // ����������ק��ʼ�����ڵ���������λ��
                double dx = currentCursor.X - _dragStartCursorRaw.X;
                double dy = currentCursor.Y - _dragStartCursorRaw.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance < UnsnapDeadZone)
                {
                    // ���������� �� ǿ�ƴ��ڱ���ԭλ
                    Marshal.StructureToPtr(_dragStartWindowRect, lParam, false);
                    return true; // handled = true����ֹϵͳ�ƶ�����
                }
                else
                {
                    // ͻ����������¼ͻ��˲������λ��
                    _deadZoneBreached = true;
                    _breachCursorRaw = currentCursor;
                }
            }

            // ������ͻ�� �� �������꣺����λ�� = ԭʼλ�� + (��ǰ��� - ͻ��˲�����)
            // �������ڴ�ԭλ��ʼƽ�����棬û����Ծ
            int offsetX = currentCursor.X - _breachCursorRaw.X;
            int offsetY = currentCursor.Y - _breachCursorRaw.Y;

            int width = _dragStartWindowRect.Right - _dragStartWindowRect.Left;
            int height = _dragStartWindowRect.Bottom - _dragStartWindowRect.Top;

            RECT correctedRect = new RECT
            {
                Left = _dragStartWindowRect.Left + offsetX,
                Top = _dragStartWindowRect.Top + offsetY,
                Right = _dragStartWindowRect.Left + offsetX + width,
                Bottom = _dragStartWindowRect.Top + offsetY + height
            };

            Marshal.StructureToPtr(correctedRect, lParam, false);
            return true; // ʼ�սӹ��ƶ�
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        private void OnDragStart()
        {
            _isDragging = true;
            _isAnimating = false;

            // �ж���ק��ʼʱ�Ƿ�������״̬
            _wasSnappedOnDragStart = _isSnapped;
            _deadZoneBreached = false;

            if (_wasSnappedOnDragStart)
            {
                // ��¼��ק��ʼʱ���������λ��
                GetCursorPos(out _dragStartCursorRaw);

                // ��¼��ק��ʼʱ���ڵ�����λ�ã�����RECT��
                GetWindowRect(_hwnd, out _dragStartWindowRect);
            }

            // 1. ��ȡ��ǰ�Ӿ�����
            double currentLeft = _window.Left;
            double currentTop = _window.Top;

            // 2. ������ж�����
            _window.BeginAnimation(Window.LeftProperty, null);
            _window.BeginAnimation(Window.TopProperty, null);

            // 3. ������ֵ��Ϊ��ǰλ��
            _window.Left = currentLeft;
            _window.Top = currentTop;

            // ֹͣ�۵�����ʱ������״̬
            _collapseTimer.Stop();
            _isSnapped = false;
            _isExpanded = false;
        }

        private void OnDragEnd()
        {
            _isDragging = false;
            _wasSnappedOnDragStart = false;
            _deadZoneBreached = false;

            if (!_isEnabled || _isDisposed || _window.WindowState == WindowState.Maximized) return;

            CheckAndApplySnapOnDragEnd();
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            StopMonitoring();
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource = null;
            _collapseTimer.Tick -= OnCollapseTimeout;
            _monitorTimer.Tick -= OnMonitorTick;
            _window.Loaded -= OnWindowLoaded;
        }

        private Rect GetMonitorWorkArea()
        {
            IntPtr monitor = _hwnd != IntPtr.Zero
                ? MonitorFromWindow(_hwnd, MONITOR_DEFAULTTONEAREST)
                : MonitorFromPoint(new POINT { X = (int)(_window.Left + _window.ActualWidth / 2), Y = (int)(_window.Top + _window.ActualHeight / 2) }, MONITOR_DEFAULTTONEAREST);

            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref mi)) return SystemParameters.WorkArea;

            var src = PresentationSource.FromVisual(_window);
            if (src?.CompositionTarget != null)
            {
                var t = src.CompositionTarget.TransformFromDevice;
                return new Rect(
                    t.Transform(new Point(mi.rcWork.Left, mi.rcWork.Top)),
                    t.Transform(new Point(mi.rcWork.Right, mi.rcWork.Bottom)));
            }
            return new Rect(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Right - mi.rcWork.Left, mi.rcWork.Bottom - mi.rcWork.Top);
        }

        private void StartMonitoring()
        {
            if (_isDisposed) return;
            _monitorTimer.Start();
        }

        private void StopMonitoring()
        {
            _monitorTimer.Stop();
            _collapseTimer.Stop();
            _isExpanded = false;
        }

        private void OnMonitorTick(object sender, EventArgs e)
        {
            if (_isDisposed || !_isEnabled || !_isSnapped || _isAnimating || _isDragging || _window.WindowState == WindowState.Minimized) return;

            GetCursorPos(out POINT rawPt);
            var src = PresentationSource.FromVisual(_window);
            Point cursor = src?.CompositionTarget != null
                ? src.CompositionTarget.TransformFromDevice.Transform(new Point(rawPt.X, rawPt.Y))
                : new Point(rawPt.X, rawPt.Y);

            var workArea = GetMonitorWorkArea();
            double dist = double.MaxValue;

            switch (_snapEdge)
            {
                case EdgeSnapSide.Left: dist = cursor.X - workArea.Left; break;
                case EdgeSnapSide.Right: dist = workArea.Right - cursor.X; break;
                case EdgeSnapSide.Top: dist = cursor.Y - workArea.Top; break;
                case EdgeSnapSide.Bottom: dist = workArea.Bottom - cursor.Y; break;
            }

            if (!_isExpanded)
            {
                if (dist <= ExpandThreshold) ExpandWindow();
            }
            else
            {
                if (dist > CollapseThreshold)
                {
                    if (!_collapseTimer.IsEnabled) _collapseTimer.Start();
                }
                else
                {
                    if (_collapseTimer.IsEnabled) _collapseTimer.Stop();
                }
            }
        }

        private void OnCollapseTimeout(object sender, EventArgs e)
        {
            _collapseTimer.Stop();
            if (!_isDisposed && _isSnapped && _isExpanded && !_isAnimating && !_isDragging)
            {
                CollapseWindow();
            }
        }

        private void CheckAndApplySnapOnDragEnd()
        {
            var workArea = GetMonitorWorkArea();
            double w = _window.ActualWidth;
            double h = _window.ActualHeight;

            double leftDist = Math.Abs(_window.Left - workArea.Left);
            double rightDist = Math.Abs((_window.Left + w) - workArea.Right);
            double topDist = Math.Abs(_window.Top - workArea.Top);
            double bottomDist = Math.Abs((_window.Top + h) - workArea.Bottom);

            double min = Math.Min(Math.Min(leftDist, rightDist), Math.Min(topDist, bottomDist));

            if (min <= DragSnapThreshold)
            {
                if (min == leftDist) _snapEdge = EdgeSnapSide.Left;
                else if (min == rightDist) _snapEdge = EdgeSnapSide.Right;
                else if (min == topDist) _snapEdge = EdgeSnapSide.Top;
                else _snapEdge = EdgeSnapSide.Bottom;

                _preSnapLeft = _window.Left;
                _preSnapTop = _window.Top;

                _isSnapped = true;
                _isExpanded = true;
                SnapWindow();
            }
            else
            {
                _isSnapped = false;
                _isExpanded = false;
                _collapseTimer.Stop();
            }
        }

        private void SnapWindow()
        {
            _isAnimating = true;
            var workArea = GetMonitorWorkArea();
            double w = _window.ActualWidth;
            double h = _window.ActualHeight;
            double targetLeft = _window.Left, targetTop = _window.Top;

            switch (_snapEdge)
            {
                case EdgeSnapSide.Left:
                    targetLeft = workArea.Left;
                    targetTop = Math.Clamp(_window.Top, workArea.Top, workArea.Bottom - h);
                    break;
                case EdgeSnapSide.Right:
                    targetLeft = workArea.Right - w;
                    targetTop = Math.Clamp(_window.Top, workArea.Top, workArea.Bottom - h);
                    break;
                case EdgeSnapSide.Top:
                    targetLeft = Math.Clamp(_window.Left, workArea.Left, workArea.Right - w);
                    targetTop = workArea.Top;
                    break;
                case EdgeSnapSide.Bottom:
                    targetLeft = Math.Clamp(_window.Left, workArea.Left, workArea.Right - w);
                    targetTop = workArea.Bottom - h;
                    break;
            }
            AnimatePosition(targetLeft, targetTop, () => _isAnimating = false);
        }

        private void ExpandWindow()
        {
            _isExpanded = true;
            _isAnimating = true;
            var workArea = GetMonitorWorkArea();
            double w = _window.ActualWidth;
            double h = _window.ActualHeight;

            double targetLeft = _window.Left, targetTop = _window.Top;
            switch (_snapEdge)
            {
                case EdgeSnapSide.Left: targetLeft = workArea.Left; break;
                case EdgeSnapSide.Right: targetLeft = workArea.Right - w; break;
                case EdgeSnapSide.Top: targetTop = workArea.Top; break;
                case EdgeSnapSide.Bottom: targetTop = workArea.Bottom - h; break;
            }
            AnimatePosition(targetLeft, targetTop, () => _isAnimating = false);
        }

        private void CollapseWindow()
        {
            _isExpanded = false;
            _isAnimating = true;
            var workArea = GetMonitorWorkArea();
            double w = _window.ActualWidth;
            double h = _window.ActualHeight;

            double targetLeft = _window.Left, targetTop = _window.Top;
            switch (_snapEdge)
            {
                case EdgeSnapSide.Left: targetLeft = workArea.Left - w + SnapGripSize; break;
                case EdgeSnapSide.Right: targetLeft = workArea.Right - SnapGripSize; break;
                case EdgeSnapSide.Top: targetTop = workArea.Top - h + SnapGripSize; break;
                case EdgeSnapSide.Bottom: targetTop = workArea.Bottom - SnapGripSize; break;
            }
            AnimatePosition(targetLeft, targetTop, () => _isAnimating = false);
        }

        private void RestoreWindowPosition()
        {
            _isAnimating = true;
            var workArea = GetMonitorWorkArea();
            double targetLeft = Math.Clamp(_preSnapLeft, workArea.Left, workArea.Right - 200);
            double targetTop = Math.Clamp(_preSnapTop, workArea.Top, workArea.Bottom - 100);
            AnimatePosition(targetLeft, targetTop, () => _isAnimating = false);
        }

        private void AnimatePosition(double targetLeft, double targetTop, Action onCompleted)
        {
            double fromLeft = _window.Left;
            double fromTop = _window.Top;

            _window.BeginAnimation(Window.LeftProperty, null);
            _window.BeginAnimation(Window.TopProperty, null);

            _window.Left = targetLeft;
            _window.Top = targetTop;

            bool animLeftNeeded = Math.Abs(targetLeft - fromLeft) > 0.5;
            bool animTopNeeded = Math.Abs(targetTop - fromTop) > 0.5;

            if (!animLeftNeeded && !animTopNeeded)
            {
                onCompleted?.Invoke();
                return;
            }

            var duration = AnimationHelper.GetScaledTimeSpan(AnimationDurationMs);
            var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            DoubleAnimation animLeft = null;
            DoubleAnimation animTop = null;

            if (animLeftNeeded)
            {
                animLeft = new DoubleAnimation { From = fromLeft, To = targetLeft, Duration = duration, EasingFunction = easing, FillBehavior = FillBehavior.Stop };
            }
            if (animTopNeeded)
            {
                animTop = new DoubleAnimation { From = fromTop, To = targetTop, Duration = duration, EasingFunction = easing, FillBehavior = FillBehavior.Stop };
            }

            EventHandler completionHandler = null;
            completionHandler = (s, e) =>
            {
                if (animLeft != null) animLeft.Completed -= completionHandler;
                else if (animTop != null) animTop.Completed -= completionHandler;
                onCompleted?.Invoke();
            };

            if (animLeft != null)
            {
                animLeft.Completed += completionHandler;
                _window.BeginAnimation(Window.LeftProperty, animLeft);
            }

            if (animTop != null)
            {
                if (animLeft == null) animTop.Completed += completionHandler;
                _window.BeginAnimation(Window.TopProperty, animTop);
            }
        }
    }
}
