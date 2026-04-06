using System;
using System.Collections.Generic;
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
        private readonly string[] formats = { "webp", "jpg", "png", "bmp", "ico" };
        private int selectedIndex = 0;
        private List<QuickFormatItem> items = new List<QuickFormatItem>();

        public event EventHandler<string> FormatSelected;

        public QuickFormatPanel()
        {
            InitializeComponent();
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
            double radius = 90;

            for (int i = 0; i < formats.Length; i++)
            {
                double angle = i * (360.0 / formats.Length) - 90;
                double rad = angle * Math.PI / 180.0;
                double x = centerX + radius * Math.Cos(rad);
                double y = centerY + radius * Math.Sin(rad);

                var item = new QuickFormatItem(formats[i]);
                Canvas.SetLeft(item, x - 35);
                Canvas.SetTop(item, y - 35);
                
                int index = i;
                item.MouseDown += (s, e) => {
                    selectedIndex = index;
                    Confirm();
                };

                ItemsCanvas.Children.Add(item);
                items.Add(item);
            }
        }

        private void QuickFormatPanel_MouseMove(object sender, MouseEventArgs e)
        {
            Point p = e.GetPosition(ItemsCanvas);
            double dx = p.X - 130;
            double dy = p.Y - 130;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist > 50) // 不在中心盲区
            {
                double angle = Math.Atan2(dy, dx) * 180 / Math.PI + 90;
                if (angle < 0) angle += 360;
                
                int index = (int)Math.Round(angle / (360.0 / formats.Length)) % formats.Length;
                if (selectedIndex != index)
                {
                    selectedIndex = index;
                    UpdateHighlight();
                }
            }
        }

        public void Rotate()
        {
            selectedIndex = (selectedIndex + 1) % formats.Length;
            UpdateHighlight();
        }

        private void UpdateHighlight()
        {
            for (int i = 0; i < items.Count; i++)
            {
                items[i].IsSelected = (i == selectedIndex);
            }
        }

        public void ShowPanel()
        {
            this.Visibility = Visibility.Visible;
            this.Focus();
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
                selectedIndex = (selectedIndex - 1 + formats.Length) % formats.Length;
                UpdateHighlight();
                e.Handled = true;
            }
            else if (e.Key == Key.Right || e.Key == Key.Down || e.Key == Key.Tab)
            {
                selectedIndex = (selectedIndex + 1) % formats.Length;
                UpdateHighlight();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                Confirm();
                e.Handled = true;
            }
        }

        public void Confirm()
        {
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

        static QuickFormatItem()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(QuickFormatItem), new FrameworkPropertyMetadata(typeof(QuickFormatItem)));
        }

        public QuickFormatItem(string format)
        {
            Format = format.ToUpper();
        }
    }
}
