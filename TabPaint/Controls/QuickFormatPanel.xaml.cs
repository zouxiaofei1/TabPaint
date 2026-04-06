using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Animation;

namespace TabPaint.Controls
{
    public partial class QuickFormatPanel : UserControl
    {
        private readonly string[] defaultFormats = { "webp", "jpg", "png", "bmp", "ico" };
        private List<string> formats = new List<string>();
        private int selectedIndex = 0;
        private List<QuickFormatItem> items = new List<QuickFormatItem>();

        public event EventHandler<string> FormatSelected;

        public QuickFormatPanel()
        {
            InitializeComponent();
            // 初始化时先加载默认格式
            formats.AddRange(defaultFormats);
            CreateItems();
            UpdateHighlight();
            
            this.MouseMove += QuickFormatPanel_MouseMove;
        }

        private void CreateItems()
        {
            ItemsCanvas.Children.Clear();
            items.Clear();

            double centerX = 130;
            double centerY = 130;
            double innerRadius = 55;
            double outerRadius = 128;
            double sectorAngle = 360.0 / formats.Count;

            for (int i = 0; i < formats.Count; i++)
            {
                double startAngle = i * sectorAngle - 90 - sectorAngle / 2;
                var item = new QuickFormatItem(formats[i]);
                
                // 生成扇区几何形状
                item.SectorGeometry = CreateSectorGeometry(centerX, centerY, innerRadius, outerRadius, startAngle, startAngle + sectorAngle);
                
                // 计算文字中心位置
                double textAngle = startAngle + sectorAngle / 2;
                double textRad = textAngle * Math.PI / 180.0;
                double textRadius = (innerRadius + outerRadius) / 2;
                item.TextX = centerX + textRadius * Math.Cos(textRad);
                item.TextY = centerY + textRadius * Math.Sin(textRad);

                Canvas.SetLeft(item, 0);
                Canvas.SetTop(item, 0);
                
                int index = i;
                item.MouseDown += (s, e) => {
                    if (e.ClickCount == 2)
                    {
                        selectedIndex = index;
                        Confirm(true);
                    }
                    else if (!item.IsDisabled)
                    {
                        selectedIndex = index;
                        Confirm();
                    }
                };

                ItemsCanvas.Children.Add(item);
                items.Add(item);
            }
        }

        private Geometry CreateSectorGeometry(double centerX, double centerY, double innerRadius, double outerRadius, double startAngle, double endAngle)
        {
            double startRad = startAngle * Math.PI / 180.0;
            double endRad = endAngle * Math.PI / 180.0;

            Point p1 = new Point(centerX + outerRadius * Math.Cos(startRad), centerY + outerRadius * Math.Sin(startRad));
            Point p2 = new Point(centerX + outerRadius * Math.Cos(endRad), centerY + outerRadius * Math.Sin(endRad));
            Point p3 = new Point(centerX + innerRadius * Math.Cos(endRad), centerY + innerRadius * Math.Sin(endRad));
            Point p4 = new Point(centerX + innerRadius * Math.Cos(startRad), centerY + innerRadius * Math.Sin(startRad));

            bool isLargeArc = Math.Abs(endAngle - startAngle) > 180;

            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(p1, true, true);
                context.ArcTo(p2, new Size(outerRadius, outerRadius), 0, isLargeArc, SweepDirection.Clockwise, true, true);
                context.LineTo(p3, true, true);
                context.ArcTo(p4, new Size(innerRadius, innerRadius), 0, isLargeArc, SweepDirection.Counterclockwise, true, true);
            }
            geometry.Freeze();
            return geometry;
        }

        private void QuickFormatPanel_MouseMove(object sender, MouseEventArgs e)
        {
            Point p = e.GetPosition(ItemsCanvas);
            double dx = p.X - 130;
            double dy = p.Y - 130;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            // 视差效果：让中心图标随鼠标移动，增加空间感
            double limit = 8;
            double px = (dx / 130.0) * limit;
            double py = (dy / 130.0) * limit;
            
            CenterIconTransform.X = px;
            CenterIconTransform.Y = py;

            // if (dist > 40 && dist < 140) // 在圆环有效范围内
            {
                double angle = Math.Atan2(dy, dx) * 180 / Math.PI + 90 + (360.0 / formats.Count / 2);
                while (angle < 0) angle += 360;
                while (angle >= 360) angle -= 360;
                
                int index = (int)(angle / (360.0 / formats.Count)) % formats.Count;
                if (selectedIndex != index)
                {
                    selectedIndex = index;
                    UpdateHighlight();

                    // 切换时的轻微脉冲反馈
                    var pulse = new DoubleAnimation(1.0, 1.02, new Duration(TimeSpan.FromSeconds(0.05))) { AutoReverse = true };
                    PanelScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
                    PanelScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
                }
            }
        }

        public void Rotate()
        {
            int nextIndex = (selectedIndex + 1) % formats.Count;
            int startIdx = nextIndex;
            
            // 跳过禁用的格式
            while (items[nextIndex].IsDisabled)
            {
                nextIndex = (nextIndex + 1) % formats.Count;
                if (nextIndex == startIdx) break; // 防止死循环
            }

            selectedIndex = nextIndex;
            UpdateHighlight();
        }

        private void UpdateHighlight()
        {
            for (int i = 0; i < items.Count; i++)
            {
                items[i].IsSelected = (i == selectedIndex);
            }
        }

        public void CheckFiles(string currentPath)
        {
            if (string.IsNullOrEmpty(currentPath)) return;
            string dir = System.IO.Path.GetDirectoryName(currentPath);
            string fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(currentPath);

            for (int i = 0; i < formats.Count; i++)
            {
                string ext = formats[i].ToLower();
                string targetPath = System.IO.Path.Combine(dir, fileNameWithoutExt + "." + ext);

                // 如果目标文件已存在，且不是当前文件，则禁用
                if (File.Exists(targetPath) && !string.Equals(targetPath, currentPath, StringComparison.OrdinalIgnoreCase))
                {
                    items[i].IsDisabled = true;
                }
                //else if (string.Equals(System.IO.Path.GetExtension(currentPath).TrimStart('.'), ext, StringComparison.OrdinalIgnoreCase))
                //{
                //    items[i].IsDisabled = true; // 当前格式也禁用
                //}
                else
                {
                    items[i].IsDisabled = false;
                }
            }
        }

        public void ShowPanel(string currentPath)
        {
            if (string.IsNullOrEmpty(currentPath)) return;
            
            // 重新初始化格式列表
            formats.Clear();
            formats.AddRange(defaultFormats);
            
            string currentExt = System.IO.Path.GetExtension(currentPath).TrimStart('.').ToLower();
            
            // 检查当前格式是否在预设中
            int existingIndex = formats.FindIndex(f => string.Equals(f, currentExt, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                selectedIndex = existingIndex;
            }
            else
            {
                // 不在预设中，添加为第 6 个，并选中
                formats.Add(currentExt);
                selectedIndex = formats.Count - 1;
            }

            // 重新生成 UI 以匹配格式数量
            CreateItems();
            
            CheckFiles(currentPath);

            // 如果当前选中的项（本应是当前格式）被禁用了，尝试自动切换到一个可用的项
            if (items[selectedIndex].IsDisabled)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (!items[i].IsDisabled)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }
            UpdateHighlight();

            this.Visibility = Visibility.Visible;
            this.Focus();
            if (this.FindResource("ShowAnim") is Storyboard sb)
            {
                sb.Begin();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
            {
                this.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
            else if (e.Key == Key.Left || e.Key == Key.Up)
            {
                selectedIndex = (selectedIndex - 1 + formats.Count) % formats.Count;
                UpdateHighlight();
                e.Handled = true;
            }
            else if (e.Key == Key.Right || e.Key == Key.Down || e.Key == Key.Tab)
            {
                selectedIndex = (selectedIndex + 1) % formats.Count;
                UpdateHighlight();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                Confirm();
                e.Handled = true;
            }
        }

        public void Confirm(bool force = false)
        {
            if (!force && items[selectedIndex].IsDisabled)
            {
                this.Visibility = Visibility.Collapsed;
                return;
            }

            FormatSelected?.Invoke(this, formats[selectedIndex]);
            this.Visibility = Visibility.Collapsed;
        }

        private void Background_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;
        }
    }

    public class QuickFormatItem : Control
    {
        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register("IsSelected", typeof(bool), typeof(QuickFormatItem), 
                new PropertyMetadata(false));

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        public static readonly DependencyProperty FormatProperty =
            DependencyProperty.Register("Format", typeof(string), typeof(QuickFormatItem), 
                new PropertyMetadata(string.Empty));

        public string Format
        {
            get => (string)GetValue(FormatProperty);
            set => SetValue(FormatProperty, value);
        }

        public static readonly DependencyProperty SectorGeometryProperty =
            DependencyProperty.Register("SectorGeometry", typeof(Geometry), typeof(QuickFormatItem), 
                new PropertyMetadata(null));

        public Geometry SectorGeometry
        {
            get => (Geometry)GetValue(SectorGeometryProperty);
            set => SetValue(SectorGeometryProperty, value);
        }

        public static readonly DependencyProperty TextXProperty =
            DependencyProperty.Register("TextX", typeof(double), typeof(QuickFormatItem), 
                new PropertyMetadata(0.0));

        public double TextX
        {
            get => (double)GetValue(TextXProperty);
            set => SetValue(TextXProperty, value);
        }

        public static readonly DependencyProperty TextYProperty =
            DependencyProperty.Register("TextY", typeof(double), typeof(QuickFormatItem), 
                new PropertyMetadata(0.0));

        public double TextY
        {
            get => (double)GetValue(TextYProperty);
            set => SetValue(TextYProperty, value);
        }

        static QuickFormatItem()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(QuickFormatItem), new FrameworkPropertyMetadata(typeof(QuickFormatItem)));
        }

        public static readonly DependencyProperty IsDisabledProperty =
            DependencyProperty.Register("IsDisabled", typeof(bool), typeof(QuickFormatItem), 
                new PropertyMetadata(false));

        public bool IsDisabled
        {
            get => (bool)GetValue(IsDisabledProperty);
            set => SetValue(IsDisabledProperty, value);
        }

        public QuickFormatItem(string format)
        {
            Format = format.ToUpper();
        }
    }
}
