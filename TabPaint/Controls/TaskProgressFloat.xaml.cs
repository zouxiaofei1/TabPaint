using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TabPaint.Controls
{
    public partial class TaskProgressFloat : UserControl
    {
        private bool _isDragging = false;
        private Point _startPoint;
        public event EventHandler CancelRequested;
        public bool IsDraggable { get; set; } = true;
        public TaskProgressFloat()
        {
            InitializeComponent();
            try
            {
                TaskIconPath.Data = Geometry.Parse(AppConsts.PathTaskProgress);
            }
            catch
            {
                // fallback: leave icon empty
            }
        }
        public void SetIcon(string iconPath)
        {
            if (TaskIconPath != null && !string.IsNullOrEmpty(iconPath))
            {
                try
                {
                    TaskIconPath.Data = Geometry.Parse(iconPath);
                }
                catch { }
            }
        }
        public void UpdateProgress(AiDownloadStatus status, string taskName = null)
        {
            var bottomGrid = this.FindName("BottomInfoGrid") as FrameworkElement;
            if (bottomGrid != null) bottomGrid.Visibility = Visibility.Visible;

            if (this.Visibility != Visibility.Visible)
            {
                this.Visibility = Visibility.Visible;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                this.BeginAnimation(OpacityProperty, fadeIn);
            }
            double p = Math.Max(0, Math.Min(100, status.Percentage));
            var percentageText = this.FindName("PercentageText") as TextBlock;
            if (percentageText != null) percentageText.Text = $"{p:F1}%";

            double totalWidth = this.ActualWidth > 0 ? this.ActualWidth - 26 : 274;
            var progressBarFill = this.FindName("ProgressBarFill") as FrameworkElement;
            if (progressBarFill != null) progressBarFill.Width = (p / 100.0) * totalWidth;

            if (!string.IsNullOrEmpty(taskName))
            {
                var taskNameText = this.FindName("TaskNameText") as TextBlock;
                if (taskNameText != null) taskNameText.Text = taskName;
            }

            
            string currentSize = FormatFileSize(status.BytesReceived);// 更新大小信息
            string totalSize = status.TotalBytes > 0 ? FormatFileSize(status.TotalBytes) : "Unknown";
            var sizeInfoText = this.FindName("SizeInfoText") as TextBlock;
            if (sizeInfoText != null) sizeInfoText.Text = $"{currentSize} / {totalSize}";

            var speedText = this.FindName("SpeedText") as TextBlock;// 速度
            if (speedText != null) speedText.Text = $"{FormatFileSize((long)status.SpeedBytesPerSecond)}/s";
        }
        public void UpdateProgress(double percentage, string taskName = null, string leftText = null, string rightText = null)
        {
            var bottomGrid = this.FindName("BottomInfoGrid") as FrameworkElement;
            if (bottomGrid != null)
            {
                if (string.IsNullOrEmpty(leftText) && string.IsNullOrEmpty(rightText))
                    bottomGrid.Visibility = Visibility.Collapsed;
                else
                    bottomGrid.Visibility = Visibility.Visible;
            }

            if (this.Visibility != Visibility.Visible)
            {
                this.Visibility = Visibility.Visible;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                this.BeginAnimation(OpacityProperty, fadeIn);
            }

            double p = Math.Max(0, Math.Min(100, percentage));
            var percentageText = this.FindName("PercentageText") as TextBlock;
            if (percentageText != null) percentageText.Text = $"{p:F1}%";

            double totalWidth = this.ActualWidth > 0 ? this.ActualWidth - 26 : 274;
            var progressBarFill = this.FindName("ProgressBarFill") as FrameworkElement;
            if (progressBarFill != null) progressBarFill.Width = (p / 100.0) * totalWidth;

            if (!string.IsNullOrEmpty(taskName))
            {
                var taskNameText = this.FindName("TaskNameText") as TextBlock;
                if (taskNameText != null) taskNameText.Text = taskName;
            }
            if (leftText != null)
            {
                var sizeInfoText = this.FindName("SizeInfoText") as TextBlock;
                if (sizeInfoText != null) sizeInfoText.Text = leftText;
            }
            if (rightText != null)
            {
                var speedText = this.FindName("SpeedText") as TextBlock;
                if (speedText != null) speedText.Text = rightText;
            }
        }
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
        public void Finish()
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500));
            fadeOut.Completed += (s, e) => { this.Visibility = Visibility.Collapsed; };
            this.BeginAnimation(OpacityProperty, fadeOut);
        }
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
            this.Visibility = Visibility.Collapsed;
        }
        private void UserControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsDraggable) return;
            if (e.OriginalSource is DependencyObject obj && IsDescendantOf(obj, typeof(Button)))
                return;

            _isDragging = true;
            _startPoint = e.GetPosition(this.Parent as UIElement);
            this.CaptureMouse();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!IsDraggable) { base.OnMouseMove(e); return; }
            if (_isDragging)
            {
                var currentPoint = e.GetPosition(this.Parent as UIElement);
                var diff = currentPoint - _startPoint;
                if (!(this.RenderTransform is TranslateTransform))
                {
                    this.RenderTransform = new TranslateTransform();
                }

                var transform = (TranslateTransform)this.RenderTransform;
                transform.X += diff.X;
                transform.Y += diff.Y;

                _startPoint = currentPoint;
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            _isDragging = false;
            this.ReleaseMouseCapture();
            base.OnMouseUp(e);
        }
        private bool IsDescendantOf(DependencyObject node, Type type)
        {
            while (node != null)
            {
                if (node.GetType() == type) return true;
                node = System.Windows.Media.VisualTreeHelper.GetParent(node);
            }
            return false;
        }
    }
}
