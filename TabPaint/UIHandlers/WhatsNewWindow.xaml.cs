using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TabPaint.Windows
{
    public partial class WhatsNewWindow : Window
    {
        public WhatsNewWindow(string fromVersion, string toVersion)
        {
            InitializeComponent();

            string fromText = string.IsNullOrWhiteSpace(fromVersion) ? "-" : fromVersion;
            string toText = string.IsNullOrWhiteSpace(toVersion) ? AppConsts.ProgramVersion : toVersion;

            string format = LocalizationManager.GetString("L_WhatsNew_VersionRange_Format");
            if (string.IsNullOrWhiteSpace(format) || format == "L_WhatsNew_VersionRange_Format")
            {
                format = "{0}->v{1}";
            }

            VersionRangeText.Text = string.Format(format, fromText, toText);
            BuildCards(fromVersion, toVersion);
        }

        private static readonly string[] VersionOrder =
        {
            "0.9.5.5",
            "0.9.5.6"
        };

        private void BuildCards(string fromVersion, string toVersion)
        {
            if (FindName("CardsHost") is not StackPanel cardsHost)
            {
                return;
            }

            cardsHost.Children.Clear();

            Version? from = ParseVersionOrZero(fromVersion);
            Version to = ParseVersionOrZero(toVersion ?? AppConsts.ProgramVersion) ?? new Version(0, 0, 0, 0);

            var versionsToShow = VersionOrder
                .Where(v =>
                {
                    Version parsed = ParseVersionOrZero(v) ?? new Version(0, 0, 0, 0);
                    bool isAfterFrom = from == null || parsed > from;
                    return isAfterFrom && parsed <= to;
                })
                .OrderByDescending(v => ParseVersionOrZero(v) ?? new Version(0, 0, 0, 0))
                .ToList();

            // Fallback for edge cases where known versions are incomplete.
            if (versionsToShow.Count == 0)
            {
                string current = NormalizeVersion(toVersion);
                if (!string.IsNullOrWhiteSpace(current))
                {
                    versionsToShow.Add(current);
                }
            }

            foreach (string version in versionsToShow)
            {
                AddCard(cardsHost, version);
            }
        }

        private void AddCard(StackPanel cardsHost, string version)
        {
            string versionKey = ToVersionKey(version);
            string subtitle = GetResourceOrDefault($"L_WhatsNew_{versionKey}_Subtitle", $"Highlights in {version}");
            List<string> items = new List<string>();

            for (int i = 1; i <= 50; i++)
            {
                string item = GetResourceOrDefault($"L_WhatsNew_{versionKey}_Item{i}", string.Empty);
                if (string.IsNullOrWhiteSpace(item)) break;
                items.Add(item);
            }

            if (items.Count == 0) return;

            Border card = new Border
            {
                Background = TryGetBrush("ControlBackgroundBrush"),
                BorderBrush = TryGetBrush("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 10)
            };

            StackPanel content = new StackPanel { Margin = new Thickness(12, 10, 12, 10) };
            content.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            foreach (string item in items)
            {
                content.Children.Add(new TextBlock
                {
                    Text = $"•  {item}",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 8)
                });
            }

            card.Child = content;
            cardsHost.Children.Add(card);
        }

        private static Version? ParseVersionOrZero(string text)
        {
            string normalized = NormalizeVersion(text);
            return Version.TryParse(normalized, out var v) ? v : null;
        }

        private static string NormalizeVersion(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return text.Trim().TrimStart('v', 'V');
        }

        private static string ToVersionKey(string version)
        {
            return NormalizeVersion(version).Replace(".", string.Empty);
        }

        private static string GetResourceOrDefault(string key, string fallback)
        {
            string val = LocalizationManager.GetString(key);
            return val == key ? fallback : val;
        }

        private Brush? TryGetBrush(string key)
        {
            return TryFindResource(key) as Brush;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source)
            {
                DependencyObject current = source;
                while (current != null)
                {
                    if (current is System.Windows.Controls.Button)
                    {
                        return;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }

            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void WhatsNewWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }
    }
}
