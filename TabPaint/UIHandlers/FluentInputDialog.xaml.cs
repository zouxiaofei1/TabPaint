using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TabPaint.UIHandlers
{
    public partial class FluentInputDialog : Window
    {
        public string InputResult { get; private set; }
        public string SelectedSuffix { get; private set; }
        public bool IsConfirmed { get; private set; }

        private FluentInputDialog()
        {
            InitializeComponent();
            this.SupportFocusHighlight();
            if (!MicaAcrylicManager.IsWin11())
            {
                RootBorder.CornerRadius = new CornerRadius(0);
                SecondBorder.CornerRadius = new CornerRadius(0);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            bool isDark = ThemeManager.CurrentAppliedTheme == AppTheme.Dark;
            ThemeManager.SetWindowImmersiveDarkMode(this, isDark);
            MicaAcrylicManager.ApplyEffect(this);
            if (!MicaAcrylicManager.IsWin11())
            {
                this.Background = FindResource("ControlBackgroundBrush") as Brush;
            }
            
            InputTextBox.Focus();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source && FindAncestor<System.Windows.Controls.Button>(source) == null)
            {
                this.DragMove();
            }
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match) return match;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        public static (bool confirmed, string result) Show(string message, string title = "TabPaint", string defaultInput = "", Window owner = null, string suffix = "")
        {
            return Show(message, title, defaultInput, owner, new string[] { suffix });
        }

        public static (bool confirmed, string result) Show(string message, string title, string defaultInput, Window owner, string[] suffixes)
        {
            var res = ShowWithSuffix(message, title, defaultInput, owner, suffixes);
            return (res.confirmed, res.result);
        }

        public static (bool confirmed, string result, string suffix) ShowWithSuffix(string message, string title = "TabPaint", string defaultInput = "", Window owner = null, string[] suffixes = null)
        {
            var dialog = new FluentInputDialog();
            dialog.TxtTitle.Text = title;
            dialog.TxtMessage.Text = message;
            dialog.InputTextBox.Text = defaultInput;

            if (suffixes != null && suffixes.Length > 0 && !string.IsNullOrEmpty(suffixes[0]))
            {
                dialog.SuffixComboBox.ItemsSource = suffixes;
                // 尝试匹配传入的第一个后缀（通常是当前的）
                dialog.SuffixComboBox.SelectedIndex = 0;
                dialog.SuffixComboBox.Visibility = Visibility.Visible;
            }

            if (!string.IsNullOrEmpty(defaultInput))
            {
                dialog.InputTextBox.SelectAll();
            }

            if (owner != null)
            {
                dialog.Owner = owner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            dialog.ShowDialog();
            return (dialog.IsConfirmed, dialog.InputResult, dialog.SelectedSuffix);
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Confirm();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Cancel();
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Confirm();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Cancel();
                e.Handled = true;
            }
        }

        private void Confirm()
        {
            IsConfirmed = true;
            InputResult = InputTextBox.Text;
            SelectedSuffix = SuffixComboBox.SelectedItem as string;
            this.Close();
        }

        private void Cancel()
        {
            IsConfirmed = false;
            this.Close();
        }
    }
}
