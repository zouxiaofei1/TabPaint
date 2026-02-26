using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace TabPaint.Controls 
{
    public class DelayedMenuItem : MenuItem
    {
        private DispatcherTimer _closeTimer;

        public bool InvokeClickOnHeaderClick
        {
            get { return (bool)GetValue(InvokeClickOnHeaderClickProperty); }
            set { SetValue(InvokeClickOnHeaderClickProperty, value); }
        }

        public static readonly DependencyProperty InvokeClickOnHeaderClickProperty =
            DependencyProperty.Register("InvokeClickOnHeaderClick", typeof(bool), typeof(DelayedMenuItem), new PropertyMetadata(false));

        public int CloseDelay
        {
            get { return (int)GetValue(CloseDelayProperty); }
            set { SetValue(CloseDelayProperty, value); }
        }

        public static readonly DependencyProperty CloseDelayProperty =
            DependencyProperty.Register("CloseDelay", typeof(int), typeof(DelayedMenuItem), new PropertyMetadata(200));

        public DelayedMenuItem()
        {
            // 初始化计时器
            _closeTimer = new DispatcherTimer();
            _closeTimer.Tick += CloseTimer_Tick;
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            if (IsSubmenuOpen)
            {
                _closeTimer.Interval = TimeSpan.FromMilliseconds(CloseDelay);
                _closeTimer.Stop(); 
                _closeTimer.Start();
            }
            else
            {
                base.OnMouseLeave(e);
            }
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            if (_closeTimer.IsEnabled)  _closeTimer.Stop();
            base.OnMouseEnter(e);
        }

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (InvokeClickOnHeaderClick && HasItems && IsMouseOver)
            {
                RaiseEvent(new RoutedEventArgs(ClickEvent, this));
                e.Handled = true;

                var parent = Parent as ContextMenu;
                if (parent != null)
                {
                    parent.IsOpen = false;
                }
                return;
            }

            base.OnPreviewMouseLeftButtonUp(e);
        }

        private void CloseTimer_Tick(object sender, EventArgs e)
        {
            _closeTimer.Stop();
            if (!this.IsMouseOver)
            {
                SetCurrentValue(IsSubmenuOpenProperty, false);
            }
        }
    }
}
