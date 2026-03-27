using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TabPaint.Controls;

namespace TabPaint.UIHandlers
{
    public partial class FavoriteWindow : Window
    {
        private Window _owner;
        private WindowSnapManager _snapManager;

        private List<string> _pages = new List<string>();
        private string _activePage = "Default";

        private static readonly Color[] PageColorPalette = new[]
        {
            Color.FromRgb(0x3B, 0x82, 0xF6), // 蓝
            Color.FromRgb(0x10, 0xB9, 0x81), // 绿
            Color.FromRgb(0xF5, 0x9E, 0x0B), // 橙
            Color.FromRgb(0xEF, 0x44, 0x44), // 红
            Color.FromRgb(0x8B, 0x5C, 0xF6), // 紫
            Color.FromRgb(0x06, 0xB6, 0xD4), // 青
            Color.FromRgb(0xEC, 0x48, 0x99), // 粉
            Color.FromRgb(0x84, 0xCC, 0x16), // 青绿
            Color.FromRgb(0xF9, 0x73, 0x16), // 深橙
            Color.FromRgb(0x14, 0xB8, 0xA6), // 湖绿
            Color.FromRgb(0xA8, 0x55, 0xF7), // 亮紫
            Color.FromRgb(0x0E, 0xAE, 0xE9), // 天蓝
        };

        private StackPanel GetPagesStack() => this.FindName("PagesStackPanel") as StackPanel;
        private FavoriteBarControl GetFavoriteContent() => this.FindName("FavoriteContent") as FavoriteBarControl;

        public FavoriteWindow()
        {
            InitializeComponent();
            this.SupportFocusHighlight();
            this.Loaded += (s, e) => LoadPages();
            this.SourceInitialized += OnSourceInitialized; ThemeManager.ThemeChanged += OnThemeChanged;
        }
        private void OnThemeChanged()
        {
          
            Dispatcher.Invoke(() =>
            {
                // 更新窗口暗色模式
                ThemeManager.SetWindowImmersiveDarkMode(this, ThemeManager.CurrentAppliedTheme == AppTheme.Dark);
                MicaAcrylicManager.ApplyEffect(this);

                // 重新加载内容（代码创建的控件会重新取色）
                RefreshContent();
            });
        }
        private void OnSourceInitialized(object sender, EventArgs e)
        {
            ThemeManager.SetWindowImmersiveDarkMode(this, ThemeManager.CurrentAppliedTheme == AppTheme.Dark);
            MicaAcrylicManager.ApplyEffect(this);
        }

        protected override void OnClosed(EventArgs e)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            if (_snapManager!=null) _snapManager.Detach();
            base.OnClosed(e);
        }
        public void AttachToAnchor(Window newAnchor)
        {
            if (_snapManager == null)
            {
                _snapManager = new WindowSnapManager(this, newAnchor, SnapEdge.Bottom);
                _snapManager.SnapStateChanged += OnSnapStateChanged;

                if (this.IsLoaded)
                {
                    _snapManager.Attach();
                }
                else
                {
                    void handler(object s, RoutedEventArgs ev)
                    {
                        _snapManager.Attach();
                        this.Loaded -= handler;
                    }
                    this.Loaded += handler;
                }
            }
            else
            {
                _snapManager.SwitchAnchor(newAnchor);
            }

            // 吸附状态下立即绑定 Owner
            if (_snapManager.IsSnapped)
            {
                SetOwnerSafe(newAnchor);
            }
        }

        public void RefreshContent()
        {
            GetFavoriteContent()?.LoadFavorites(_activePage);
            RefreshPageTabs();
        }
        private void ClearOwner()
        {
            try
            {
                if (this.Owner != null)
                {
                    this.Owner = null;
                }
            }
            catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
        }
        private void SetOwnerSafe(Window newOwner)
        {
            try
            {
                if (this.Owner == newOwner) return;
                // 必须先清除旧 Owner，否则跨窗口切换会抛异常
                this.Owner = null;
                if (newOwner != null && newOwner.IsLoaded && newOwner.IsVisible)
                {
                    this.Owner = newOwner;
                }
            }
            catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
        }

        private void OnSnapStateChanged(SnapEdge edge, bool isSnapped)
        {
            if (isSnapped)
            {
                SetOwnerSafe(_snapManager.AnchorWindow);
            }
            else
            {
                ClearOwner();
            }
        }
        #region 拖动（转发给 SnapManager）

        private void StarIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (IsInteractiveElement(e.OriginalSource as DependencyObject)) return;

            _snapManager.BeginDrag(_snapManager.GetCursorPosDIU());

            var element = sender as UIElement;
            if (element != null)
            {
                element.CaptureMouse(); element.MouseMove += StarIcon_MouseMove;
                element.MouseLeftButtonUp += StarIcon_MouseLeftButtonUp;
            }
            e.Handled = true;
        }
        public void SwitchAnchorKeepPosition(Window newAnchor)
        {
            _snapManager?.SwitchAnchorKeepPosition(newAnchor); if (_snapManager?.IsSnapped == true)
            {
                SetOwnerSafe(newAnchor);
            }
        }
        public bool IsSnapped => _snapManager?.IsSnapped ?? false;
        private void StarIcon_MouseMove(object sender, MouseEventArgs e)
        {
            var cursorPos = _snapManager.GetCursorPosDIU();
            _snapManager.UpdateDrag(cursorPos);
            if (!_snapManager.IsSnapped)
            {
                ClearOwner();
                var nearest = FavoriteWindowManager.FindNearestMainWindow(cursorPos);
                if (nearest != null && nearest != FavoriteWindowManager.CurrentAnchor)
                {
                    // 切换锚点但保持自由拖动状态
                    FavoriteWindowManager.SwitchAnchorDuringDrag(nearest);
                }
            }
        }

        private void StarIcon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _snapManager.EndDrag();

            // ★ 松手后确认最终吸附的窗口
            if (_snapManager.IsSnapped)
            {
                var cursorPos = _snapManager.GetCursorPosDIU();
                var nearest = FavoriteWindowManager.FindNearestMainWindow(cursorPos);
                if (nearest != null)
                {
                    FavoriteWindowManager.SwitchAnchor(nearest); SetOwnerSafe(nearest);
                }
            }
            else
            {
                ClearOwner();
            }

            var element = sender as UIElement;
            if (element != null)
            {
                element.ReleaseMouseCapture();
                element.MouseMove -= StarIcon_MouseMove;
                element.MouseLeftButtonUp -= StarIcon_MouseLeftButtonUp;
            }
            e.Handled = true;
        }

        private bool IsInteractiveElement(DependencyObject source)
        {
            DependencyObject current = source;
            while (current != null && current != this)
            {
                if (current is System.Windows.Controls.Primitives.ButtonBase
                    || current is TextBox
                    || current is System.Windows.Controls.Primitives.TextBoxBase
                    || current is ComboBox
                    || current is System.Windows.Controls.Primitives.ScrollBar
                    || current is System.Windows.Controls.Primitives.Thumb
                    || current is MenuItem
                    || current is ListBoxItem
                    || current is System.Windows.Controls.Primitives.Selector) return true;

                if (current is FrameworkElement fe && fe.Cursor == Cursors.Hand)
                    return true;

                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private Brush GetPageIndicatorBrush(string pageName)
        {
            if (string.IsNullOrEmpty(pageName))
                return new SolidColorBrush(PageColorPalette[0]);

            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in pageName)
                {
                    hash ^= c;
                    hash *= 16777619;
                }

                int index = (int)(hash % (uint)PageColorPalette.Length);
                var brush = new SolidColorBrush(PageColorPalette[index]);
                brush.Freeze();
                return brush;
            }
        }

        #endregion

        #region 页面管理（保持不变）

        private void LoadPages()
        {
            if (!Directory.Exists(AppConsts.FavoriteDir))
                Directory.CreateDirectory(AppConsts.FavoriteDir);

            _pages = Directory.GetDirectories(AppConsts.FavoriteDir)
                              .Select(Path.GetFileName)
                              .ToList();

            if (!_pages.Contains("Default"))
            {
                _pages.Insert(0, "Default");
                string defaultPath = Path.Combine(AppConsts.FavoriteDir, "Default");
                if (!Directory.Exists(defaultPath))
                    Directory.CreateDirectory(defaultPath);
            }

            RefreshPageTabs();
        }

        private void RefreshPageTabs()
        {
            var stack = GetPagesStack();
            if (stack == null) return;
            stack.Children.Clear();

            var reversedPages = new List<string>(_pages);
            reversedPages.Reverse();

            foreach (var page in reversedPages)
            {
                var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };

                var btn = new Button
                {
                    Content = page.Length > 2 ? page.Substring(0, 2) : page,
                    Style = (Style)FindResource("RoundedMenuButtonStyle"),
                    Width = 36,
                    Height = 36,
                    ToolTip = page,
                    Tag = page
                };

                if (page == _activePage)
                {
                    btn.Background = (Brush)FindResource("SystemAccentBrush");
                    var normalBrush = (Brush)FindResource("TextInverseBrush");   // 浅色=白, 深色=黑
                    var hoverBrush = (Brush)FindResource("TextPrimaryBrush");    // 浅色=黑, 深色=白

                    btn.Foreground = normalBrush;

                    btn.MouseEnter += (s, e) => ((Button)s).Foreground = hoverBrush;
                    btn.MouseLeave += (s, e) => ((Button)s).Foreground = normalBrush;
                }

                btn.Click += (s, e) =>
                {
                    _activePage = (string)((Button)s).Tag;
                    GetFavoriteContent()?.LoadFavorites(_activePage);
                    RefreshPageTabs();
                };

                grid.Children.Add(btn);

                var indicator = new Border
                {
                    Width = 8,
                    Height = 8,
                    CornerRadius = new CornerRadius(4),
                    Background = GetPageIndicatorBrush(page),
                    BorderBrush = (Brush)FindResource("BorderBrush"),
                    BorderThickness = new Thickness(1),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 3, 3),
                    IsHitTestVisible = false
                };
                grid.Children.Add(indicator);

                if (page != "Default")
                {
                    string pagePath = Path.Combine(AppConsts.FavoriteDir, page);
                    bool isEmpty = !Directory.Exists(pagePath) || !Directory.GetFiles(pagePath).Any();

                    if (isEmpty)
                    {
                        var delBtn = new Button
                        {
                            Style = (Style)FindResource("OtherCloseButtonStyle"),
                            Width = 14,
                            Height = 14,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            VerticalAlignment = VerticalAlignment.Top,
                            Margin = new Thickness(0, -2, -2, 0),
                            Content = "×",
                            FontSize = 10,
                            Padding = new Thickness(0),
                            Tag = page
                        };
                        delBtn.Click += (s, e) =>
                        {
                            try
                            {
                                if (Directory.Exists(pagePath)) Directory.Delete(pagePath, true);
                                _pages.Remove(page);
                                if (_activePage == page) _activePage = "Default";
                                GetFavoriteContent()?.LoadFavorites(_activePage);
                                RefreshPageTabs();
                            }
                            catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
                        };
                        grid.Children.Add(delBtn);
                    }

                    var menu = new ContextMenu();
                    var deleteItem = new MenuItem { Header = FindResource("L_Ctx_DeleteFile") };
                    deleteItem.Click += (s2, e2) =>
                    {
                        if (MessageBox.Show("Delete page and all its images?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            try
                            {
                                Directory.Delete(Path.Combine(AppConsts.FavoriteDir, page), true);
                                _pages.Remove(page);
                                if (_activePage == page) _activePage = "Default";
                                GetFavoriteContent()?.LoadFavorites(_activePage);
                                RefreshPageTabs();
                            }
                            catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
                        }
                    };
                    menu.Items.Add(deleteItem);
                    btn.ContextMenu = menu;
                }

                stack.Children.Add(grid);
            }
        }

        private void AddPage_Click(object sender, RoutedEventArgs e)
        {
            string newPageName = "Page " + (_pages.Count);
            int i = _pages.Count;
            while (_pages.Contains(newPageName)) newPageName = "Page " + (++i);

            try
            {
                Directory.CreateDirectory(Path.Combine(AppConsts.FavoriteDir, newPageName));
                _pages.Add(newPageName);
                _activePage = newPageName;
                GetFavoriteContent()?.LoadFavorites(_activePage);
                RefreshPageTabs();
            }
            catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Favorite window is now a detachable panel. You can drag it to any MainWindow and it will remember its position. To close it, just click the star icon again or use the system close button.", "Info", MessageBoxButton.OK);
            this.Close();
        }



        #endregion
    }
}
