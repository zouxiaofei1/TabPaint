using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TabPaint.Services;

namespace TabPaint.Controls
{
    public partial class SearchBarControl : UserControl
    {
        private const double DefaultWidth = 150;
        private const double ExpandedWidth = 240;
        private const double AnimationDurationMs = 200;

        private DispatcherTimer _searchFocusTimer;

        public static readonly DependencyProperty SearchItemsProperty =
            DependencyProperty.Register(nameof(SearchItems),
                typeof(IEnumerable<SearchItem>),
                typeof(SearchBarControl),
                new PropertyMetadata(null));

        public IEnumerable<SearchItem> SearchItems
        {
            get => (IEnumerable<SearchItem>)GetValue(SearchItemsProperty);
            set => SetValue(SearchItemsProperty, value);
        }

        public static readonly RoutedEvent SearchItemSelectedEvent =
            EventManager.RegisterRoutedEvent("SearchItemSelected",
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(SearchBarControl));

        public event RoutedEventHandler SearchItemSelected
        {
            add { AddHandler(SearchItemSelectedEvent, value); }
            remove { RemoveHandler(SearchItemSelectedEvent, value); }
        }

        public SearchBarControl()
        {
            InitializeComponent();

            Width = DefaultWidth;

            _searchFocusTimer = new DispatcherTimer();
            _searchFocusTimer.Interval = TimeSpan.FromMilliseconds(150);
            _searchFocusTimer.Tick += SearchFocusTimer_Tick;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                parentWindow.LocationChanged += OnParentWindowLocationChanged;
            }
        }

        private void OnParentWindowLocationChanged(object sender, EventArgs e)
        {
            if (SearchPopup.IsOpen)
            {
                var offset = SearchPopup.HorizontalOffset;
                SearchPopup.HorizontalOffset = offset + 0.1;
                Dispatcher.BeginInvoke(DispatcherPriority.Background,
                    new Action(() => SearchPopup.HorizontalOffset = offset));
            }
        }

        public void FocusSearch()
        {
            AnimateWidth(ExpandedWidth);
            SearchTextBox?.Focus();
            if (!string.IsNullOrEmpty(SearchTextBox?.Text))
            {
                SearchTextBox.SelectAll();
            }
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePlaceholderVisibility();
            UpdateClearButtonVisibility();
            UpdateSearchResults();
        }

        private void OnSearchGotFocus(object sender, RoutedEventArgs e)
        {
            UpdatePlaceholderVisibility();
            if (!string.IsNullOrEmpty(SearchTextBox.Text))
            {
                UpdateSearchResults();
            }
            FocusBorder.Visibility = Visibility.Visible;
            SearchBorder.BorderBrush = TryFindResource("SystemAccentBrush") as System.Windows.Media.Brush
                                       ?? SearchBorder.BorderBrush;
            AnimateWidth(ExpandedWidth);
        }

        private void OnSearchLostFocus(object sender, RoutedEventArgs e)
        {
            UpdatePlaceholderVisibility();
            _searchFocusTimer.Start();
            FocusBorder.Visibility = Visibility.Collapsed;
            SearchBorder.BorderBrush = TryFindResource("BorderBrush") as System.Windows.Media.Brush
                                       ?? SearchBorder.BorderBrush;
            if (string.IsNullOrEmpty(SearchTextBox.Text))
            {
                AnimateWidth(DefaultWidth);
            }
        }

        private void SearchFocusTimer_Tick(object sender, EventArgs e)
        {
            _searchFocusTimer.Stop();
            SearchPopup.IsOpen = false;
        }

        private void OnSearchPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (SearchPopup.IsOpen)
                {
                    SearchPopup.IsOpen = false;
                }
                else
                {
                    SearchTextBox.Text = "";
                    FocusBorder.Visibility = Visibility.Collapsed;
                    SearchBorder.BorderBrush = TryFindResource("BorderBrush") as System.Windows.Media.Brush
                                               ?? SearchBorder.BorderBrush;
                    AnimateWidth(DefaultWidth);
                    Keyboard.ClearFocus();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (SearchPopup.IsOpen && SearchResultList.Items.Count > 0)
                {
                    if (SearchResultList.SelectedItem is SearchItem item)
                    {
                        NavigateToSearchItem(item);
                    }
                    else
                    {
                        SearchResultList.SelectedIndex = 0;
                        if (SearchResultList.SelectedItem is SearchItem first)
                            NavigateToSearchItem(first);
                    }
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Down)
            {
                if (SearchPopup.IsOpen && SearchResultList.Items.Count > 0)
                {
                    int idx = SearchResultList.SelectedIndex;
                    if (idx < SearchResultList.Items.Count - 1)
                    {
                        SearchResultList.SelectedIndex = idx + 1;
                        if (SearchResultList.SelectedItem != null)
                            SearchResultList.ScrollIntoView(SearchResultList.SelectedItem);
                    }
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Up)
            {
                if (SearchPopup.IsOpen && SearchResultList.Items.Count > 0)
                {
                    int idx = SearchResultList.SelectedIndex;
                    if (idx > 0)
                    {
                        SearchResultList.SelectedIndex = idx - 1;
                        if (SearchResultList.SelectedItem != null)
                            SearchResultList.ScrollIntoView(SearchResultList.SelectedItem);
                    }
                    e.Handled = true;
                }
            }
        }

        private void OnClearButtonClick(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "";
            SearchTextBox.Focus();
        }

        private void OnResultListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void OnResultListPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox?.SelectedItem is SearchItem item)
            {
                NavigateToSearchItem(item);
            }
        }

        private void NavigateToSearchItem(SearchItem item)
        {
            SearchPopup.IsOpen = false;
            SearchTextBox.Text = "";
            Keyboard.ClearFocus();
            RaiseEvent(new RoutedEventArgs(SearchItemSelectedEvent, item));
        }

        private void UpdatePlaceholderVisibility()
        {
            PlaceholderGrid.Visibility = string.IsNullOrEmpty(SearchTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateClearButtonVisibility()
        {
            ClearButton.Visibility = string.IsNullOrEmpty(SearchTextBox.Text)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void UpdateSearchResults()
        {
            string query = SearchTextBox.Text?.Trim() ?? "";
            var items = SearchItems;
            if (string.IsNullOrEmpty(query) || items == null)
            {
                SearchPopup.IsOpen = false;
                return;
            }

            var results = FuzzyMatcher.Match(items, query, maxResults: 6);

            const double minScore = 0.15;
            var filtered = results
                .Where(r => r.Score >= minScore)
                .Select(r => r.Item)
                .ToList();

            SearchResultList.ItemsSource = filtered;
            SearchResultList.SelectedIndex = -1;
            SearchPopup.IsOpen = filtered.Count > 0;
        }

        private void AnimateWidth(double targetWidth)
        {
            var animation = new DoubleAnimation
            {
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(AnimationDurationMs),
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            BeginAnimation(WidthProperty, animation);
        }
    }
}
