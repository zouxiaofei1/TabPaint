using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TabPaint.Controls;
using static TabPaint.MainWindow;

//设置窗口


namespace TabPaint
{
    public partial class SettingsWindow : Window
    {
        public string ProgramVersion { get; set; } = "";
        private bool _isNavExpanded = true;
        private DispatcherTimer _toastTimer;
        private bool _isToastVisible = false;
        private bool MicaEnabled = false;
        private DispatcherTimer _updateToastTimer; private DispatcherTimer _conflictToastTimer;
        private string _latestVersionUrl = ""; // 用于存储点击跳转的地址
        private List<SearchItem> _searchItems;
        private bool _searchItemsBuilt;
        private static readonly string SearchCachePath = Path.Combine(AppConsts.CacheDir, "SettingsSearchCache.json");
        private static readonly int SearchCacheVersion = 2;
        public SettingsWindow()
        {
            InitializeComponent();
            SettingsSearchBar.SearchItemSelected += OnSearchBarItemSelected;
            this.DataContext = this;

            this.SupportFocusHighlight();
            this.Activated += SettingsWindow_Activated;
            if (SettingsManager.Instance.Current != null)
            {
                SettingsManager.Instance.Current.PropertyChanged += Settings_PropertyChanged;
            }

            this.Loaded += (s, e) =>
            {
                CheckUpdateOnLoad(); // <--- 调用自动检查
                UpdateSpecialNavItemsVisibility("General");
                EnsureSearchItemsBuilt();
                SettingsSearchBar.SearchItems = _searchItems;
                if (MainContent.Content == null)
                {
                    if (NavListBox.Items.Count > 0)
                    {
                        NavListBox.SelectedIndex = 0;
                        NavigateToPage("General");
                    }
                }
            };

            this.Unloaded += (s, e) =>
            {
                if (SettingsManager.Instance.Current != null)
                {
                    SettingsManager.Instance.Current.PropertyChanged -= Settings_PropertyChanged;
                }
            };

            this.SizeChanged += SettingsWindow_SizeChanged;
            _toastTimer = new DispatcherTimer();
            _toastTimer.Interval = TimeSpan.FromSeconds(1); // Toast 显示时长
            _toastTimer.Tick += (s, args) => HideToast();

            _updateToastTimer = new DispatcherTimer();
            _updateToastTimer.Interval = TimeSpan.FromSeconds(5);
            _updateToastTimer.Tick += (s, args) => HideUpdateToast();

            _conflictToastTimer = new DispatcherTimer();
            _conflictToastTimer.Interval = TimeSpan.FromSeconds(2.5);
            _conflictToastTimer.Tick += (s, args) => HideConflictToast();

        }
        public void ShowConflictToast(string featureName)
        {
            var conflictToast = this.FindName("ConflictToast") as Border;
            var txtConflict = this.FindName("TxtConflict") as TextBlock;
            if (conflictToast == null || txtConflict == null) return;

            _conflictToastTimer.Stop();
            string msg = LocalizationManager.GetString("L_Settings_Toast_Conflict");
            txtConflict.Text = string.Format(msg, featureName);
            if (conflictToast.Visibility != Visibility.Visible)
            {
                AnimateShow(conflictToast);
            }
            _conflictToastTimer.Start();
        }

        private void HideConflictToast()
        {
            _conflictToastTimer.Stop();
            var conflictToast = this.FindName("ConflictToast") as Border;
            if (conflictToast != null)
            {
                AnimateHide(conflictToast);
            }
        }

        public void ShowToast(string customMessage = null)
        {
            _toastTimer.Stop();

            var txtSavedToast = this.FindName("TxtSavedToast") as TextBlock;
            if (txtSavedToast != null)
            {
                txtSavedToast.Text = string.IsNullOrWhiteSpace(customMessage)
                    ? LocalizationManager.GetString("L_Settings_Toast_Saved")
                    : customMessage;
            }

            if (SavedToast.Visibility != Visibility.Visible)
            {
                AnimateShow(SavedToast);
                _isToastVisible = true;
            }

            _toastTimer.Start();
        }

        private void HideToast()
        {
            _toastTimer.Stop();
            _isToastVisible = false;
            AnimateHide(SavedToast);
        }
        private void ShowUpdateToast(string versionTag, string url)
        {
            _latestVersionUrl = url;

            string title = LocalizationManager.GetString("L_Update_Found_Title") ?? "New Update Available";
            TxtUpdateVer.Text = $"{versionTag} ready to download";
            UpdateToast.IsHitTestVisible = true;

            _updateToastTimer.Stop();

            if (UpdateToast.Visibility != Visibility.Visible)
            {
                AnimateShow(UpdateToast);
            }

            _updateToastTimer.Start();
        }

        private void HideUpdateToast()
        {
            _updateToastTimer.Stop();
            UpdateToast.IsHitTestVisible = false;
            AnimateHide(UpdateToast);
        }

        // 点击 Toast 的事件处理
        private void UpdateToast_Click(object sender, MouseButtonEventArgs e)
        {
            if (!string.IsNullOrEmpty(_latestVersionUrl))
            {
                try { Process.Start(new ProcessStartInfo(_latestVersionUrl) { UseShellExecute = true }); } catch { }
            }
            HideUpdateToast();
        }
        private static bool _hasCheckedUpdateThisSession = false;

        private async void CheckUpdateOnLoad()
        {
            if (_hasCheckedUpdateThisSession) return;

            await CheckForUpdatesAsync(isManual: false);
            _hasCheckedUpdateThisSession = true;
        }

        public async void CheckForUpdatesManually()
        {
            await CheckForUpdatesAsync(isManual: true);
        }

        private async Task CheckForUpdatesAsync(bool isManual)
        {
            try
            {
                string owner = "zouxiaofei1";
                string repo = "TabPaint";
                string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
                string releaseUrl = $"https://github.com/{owner}/{repo}/releases/latest"; // 默认跳转地址

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("TabPaint-Client");
                    client.Timeout = TimeSpan.FromSeconds(5);

                    string jsonResponse = await client.GetStringAsync(apiUrl);
                    var match = Regex.Match(jsonResponse, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");

                    if (match.Success)
                    {
                        string latestVersionTag = match.Groups[1].Value;

                        if (IsNewerVersion(ProgramVersion, latestVersionTag))
                        {
                            ShowUpdateToast(latestVersionTag, releaseUrl);
                        }
                        else if (isManual)
                        {
                            FluentMessageBox.Show(
                                LocalizationManager.GetString("L_Update_Latest_Desc") ?? "You are using the latest version.",
                                LocalizationManager.GetString("L_Update_Latest_Title") ?? "Up to date",
                                MessageBoxButton.OK);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (isManual)
                {
                    FluentMessageBox.Show($"Check update failed: {ex.Message}", "Error", MessageBoxButton.OK);
                }
                Debug.WriteLine("Update check failed: " + ex.Message);
            }
        }
        private void AnimateShow(UIElement element)
        {
            if (element.Visibility == Visibility.Visible) return; // 已经在显示了

            element.Visibility = Visibility.Visible;
            element.Opacity = 0;

            // 1. 透明度淡入
            var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            element.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            if (element is FrameworkElement fe && fe.RenderTransform is TranslateTransform trans)
            {
                trans.X = 50; // 初始位置在右侧
                var slideIn = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                trans.BeginAnimation(TranslateTransform.XProperty, slideIn);
            }
        }

        // 通用隐藏动画
        private void AnimateHide(UIElement element)
        {
            if (element.Visibility == Visibility.Collapsed) return;
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
            element.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            if (element is FrameworkElement fe && fe.RenderTransform is TranslateTransform trans)
            {
                var slideOut = new DoubleAnimation(50, TimeSpan.FromMilliseconds(200));
                slideOut.Completed += (s, e) =>
                {
                    element.Visibility = Visibility.Collapsed;
                };

                trans.BeginAnimation(TranslateTransform.XProperty, slideOut);
            }
            else element.Visibility = Visibility.Collapsed;
        }


        private bool IsNewerVersion(string currentRaw, string latestRaw)
        {
            try
            {
                var current = Version.Parse(currentRaw.TrimStart('v', 'V').Trim());
                var latest = Version.Parse(latestRaw.TrimStart('v', 'V').Trim());

                return latest > current;
            }
            catch { return false; }
        }
        private void SettingsWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width < 750 && _isNavExpanded) SetSidebarState(false);
            else if (e.NewSize.Width >= 750 && !_isNavExpanded) SetSidebarState(true);
        }
        private void SetSidebarState(bool expand)
        {


            if (_isNavExpanded == expand) return;
            _isNavExpanded = expand;

            double targetWidth = expand ? AppConsts.NavExpandedWidth : AppConsts.NavCollapsedWidth;
            DoubleAnimation anim = new DoubleAnimation();
            anim.From = SidebarBorder.ActualWidth;
            anim.To = targetWidth;
            anim.Duration = TimeSpan.FromMilliseconds(200);
            anim.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            SidebarBorder.BeginAnimation(Border.WidthProperty, anim);
            Visibility textVis = expand ? Visibility.Visible : Visibility.Collapsed;
            if (TxtGeneral != null) TxtGeneral.Visibility = textVis;
            if (TxtPaint != null) TxtPaint.Visibility = textVis;
            if (TxtView != null) TxtView.Visibility = textVis;
            if (TxtShortcuts != null) TxtShortcuts.Visibility = textVis;
            if (TxtAdvanced != null) TxtAdvanced.Visibility = textVis;
            if (TxtAbout != null) TxtAbout.Visibility = textVis;
            if (TxtPlugins != null) TxtPlugins.Visibility = textVis;
            if (TxtDevTools != null) TxtDevTools.Visibility = textVis;
            if (TxtSystemReport != null) TxtSystemReport.Visibility = textVis;
            if (FindName("TxtAgreement") is TextBlock txtAgreement) txtAgreement.Visibility = textVis;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            bool isDark = ThemeManager.CurrentAppliedTheme == AppTheme.Dark;
            ThemeManager.SetWindowImmersiveDarkMode(this, isDark);

            MicaAcrylicManager.ApplyEffect(this);
            if (!MicaAcrylicManager.IsWin11())
            {
                var chromeLow = FindResource("ChromeLowBrush") as Brush;
                SidebarBorder.Background = chromeLow;
            }
        }
        private void SettingsWindow_Activated(object sender, EventArgs e)
        {
        }

        private static ResourceDictionary _cachedEnResources;

        private static string GetEnglishString(string key)
        {
            try
            {
                if (_cachedEnResources == null)
                {
                    _cachedEnResources = new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/Resources/Lang.en-US.xaml", UriKind.Absolute)
                    };
                }
                if (_cachedEnResources.Contains(key))
                    return _cachedEnResources[key] as string ?? key;
            }
            catch { }
            return key;
        }


        private List<SearchItem> BuildSearchItems()
        {
            var items = new List<SearchItem>();

            // Page-level navigation items
            items.Add(new SearchItem("General", LocalizationManager.GetString("L_Settings_Nav_General"), "Settings_General_Image"));
            items.Add(new SearchItem("Paint", LocalizationManager.GetString("L_Settings_Nav_Paint"), "Paint_Mode_Image"));
            items.Add(new SearchItem("View", LocalizationManager.GetString("L_Settings_Nav_View"), "View_Mode_Image"));
            items.Add(new SearchItem("Shortcuts", LocalizationManager.GetString("L_Settings_Nav_Shortcuts"), "Keyboard_Image"));
            items.Add(new SearchItem("Advanced", LocalizationManager.GetString("L_Settings_Nav_Advanced"), "Settings_Advanced_Image"));
            items.Add(new SearchItem("Plugins", LocalizationManager.GetString("L_Settings_Nav_Plugins"), "Plugins_Image"));
            items.Add(new SearchItem("DevTools", LocalizationManager.GetString("L_Settings_Nav_DevTools"), "DevTools_Image"));
            items.Add(new SearchItem("SystemReport", LocalizationManager.GetString("L_Settings_Advanced_CollectReport_Title"), "Report_Image"));
            items.Add(new SearchItem("Agreement", LocalizationManager.GetString("L_Settings_Nav_Agreement"), "Agreement_Image"));
            items.Add(new SearchItem("About", LocalizationManager.GetString("L_Settings_Nav_About"), "Info_Image"));

            // Individual settings within pages
            AddSettingsToSearch(items, "General", "Settings_General_Image",
                ("L_Settings_General_AutoLoadFolderImages_Title", "AutoLoadFolderImages"),
                ("L_Settings_General_StartInViewMode_Title", "StartInViewMode"),
                ("L_Settings_General_ShellIntegration_Title", "ShellIntegration"),
                ("L_Settings_General_MouseWheel_Title", "MouseWheel"),
                ("L_Settings_General_Theme_Title", "Theme"),
                ("L_Settings_General_Accent_Title", "Accent"),
                ("L_Settings_General_Language_Title", "Language")
            );

            AddSettingsToSearch(items, "View", "View_Mode_Image",
                ("L_Settings_View_MouseWheel_Title", "ViewMouseWheel"),
                ("L_Settings_View_DarkBg_Title", "ViewDarkBg"),
                ("L_Settings_View_TransparentGrid_Title", "ViewTransparentGrid")
            );

            AddSettingsToSearch(items, "Paint", "Paint_Mode_Image",
                ("L_Settings_Paint_AutoPopupClipboard_Title", "AutoPopupClipboard"),
                ("L_Settings_Paint_SelectionClear_Title", "SelectionClear"),
                ("L_Settings_Paint_OcrResultAction_Title", "OcrResultAction"),
                ("L_Settings_Paint_EnableFileDelete_Title", "EnableFileDelete"),
                ("L_Settings_Paint_ProfessionalMode_Title", "ProfessionalMode"),
                ("L_Settings_Paint_BirdEye_Title", "BirdEye")
            );

            AddSettingsToSearch(items, "Advanced", "Settings_Advanced_Image",
                ("L_Settings_Advanced_Resampling_Title", "AdvancedResampling"),
                ("L_Settings_Advanced_NewCanvasSize_Title", "NewCanvasSize"),
                ("L_Settings_Advanced_SvgDecodeSize_Title", "SvgDecodeSize"),
                ("L_Settings_Advanced_ICC_Title", "ICC"),
                ("L_Settings_Advanced_PixelThreshold_Title", "PixelThreshold"),
                ("L_Settings_MaxGlobalUndoSteps", "MaxGlobalUndoSteps"),
                ("L_Settings_MaxUndoMemory", "MaxUndoMemory"),
                ("L_Settings_Advanced_SkipResetConfirm_Title", "SkipResetConfirm"),
                ("L_Settings_Advanced_DiscardAllOnExit_Title", "DiscardAllOnExit"),
                ("L_Settings_Advanced_EnablePdfSavePage_Title", "EnablePdfSavePage"),
                ("L_Settings_Advanced_AlwaysShowTabClose_Title", "AlwaysShowTabClose"),
                ("L_Settings_Advanced_MaxRecentFiles_Title", "MaxRecentFiles")
            );

            AddSettingsToSearch(items, "Plugins", "Plugins_Image",
                ("L_Settings_Plugins_RmbgModel_Title", "RmbgModel"),
                ("L_Settings_Plugins_ModelDir_Title", "ModelDir"),
                ("L_Settings_General_AiImage_ApiBaseUrl_Title", "AiImageApiBaseUrl"),
                ("L_Settings_General_AiImage_ApiKey_Title", "AiImageApiKey"),
                ("L_Settings_General_AiImage_Model_Title", "AiImageModel")
            );

            return items;
        }

        private void EnsureSearchItemsBuilt()
        {
            if (_searchItemsBuilt) return;
            _searchItems = LoadCachedSearchItems();
            if (_searchItems == null)
            {
                _searchItems = BuildSearchItems();
                SaveSearchCache(_searchItems);
            }
            _searchItemsBuilt = true;
        }

        private List<SearchItem> LoadCachedSearchItems()
        {
            try
            {
                if (!File.Exists(SearchCachePath)) return null;
                string json = File.ReadAllText(SearchCachePath);
                var data = JsonSerializer.Deserialize<SearchItemCacheData>(json);
                if (data == null || data.Version != SearchCacheVersion) return null;
                string currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
                if (data.AppVersion != currentVersion) return null;
                return data.Items.Select(dto => new SearchItem(
                    dto.Tag, dto.ParentTag, dto.DisplayName, dto.EnglishName,
                    dto.IconKey)).ToList();
            }
            catch
            {
                return null;
            }
        }

        private void SaveSearchCache(List<SearchItem> items)
        {
            try
            {
                string dir = Path.GetDirectoryName(SearchCachePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var data = new SearchItemCacheData
                {
                    Version = SearchCacheVersion,
                    AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "",
                    Items = items.Select(i => new SearchItemDto
                    {
                        Tag = i.Tag,
                        ParentTag = i.ParentTag,
                        DisplayName = i.DisplayName,
                        EnglishName = i.EnglishName,
                        IconKey = i.IconKey
                    }).ToList()
                };
                string json = JsonSerializer.Serialize(data);
                File.WriteAllText(SearchCachePath, json);
            }
            catch { }
        }

        private class SearchItemCacheData
        {
            public int Version { get; set; }
            public string AppVersion { get; set; }
            public List<SearchItemDto> Items { get; set; }
        }

        private class SearchItemDto
        {
            public string Tag { get; set; }
            public string ParentTag { get; set; }
            public string DisplayName { get; set; }
            public string EnglishName { get; set; }
            public string IconKey { get; set; }
            }

        private void AddSettingsToSearch(List<SearchItem> items, string parentTag, string iconKey,
            params (string Key, string SubTag)[] settings)
        {
            foreach (var (key, subTag) in settings)
            {
                string displayName = LocalizationManager.GetString(key);
                string englishName = GetEnglishString(key);
                items.Add(new SearchItem(subTag, parentTag, displayName, englishName, iconKey));
            }
        }

        private void OnSearchBarItemSelected(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is SearchItem item)
            {
                string navTag = item.ParentTag ?? item.Tag;
                bool success = NavigateToTag(navTag);
                if (success)
                {
                    HighlightNavItem(navTag);
                }

                if (!string.IsNullOrEmpty(item.Tag))
                {
                    string targetName = item.Tag + "Setting";
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var page = MainContent.Content as UserControl;
                        if (page == null) return;
                        var target = page.FindName(targetName) as FrameworkElement;
                        if (target != null)
                        {
                            target.BringIntoView();
                            FlashElement(target);
                        }
                    }), DispatcherPriority.Loaded);
                }
            }
        }

        private void HighlightNavItem(string tag)
        {
            var navItem = FindNavItemByTag(tag);
            if (navItem == null) return;

            var accentBrush = TryFindResource("SystemAccentPressedBrush") as SolidColorBrush;
            if (accentBrush == null) return;

            var animBrush = new SolidColorBrush(accentBrush.Color);
            navItem.Background = animBrush;

            var colorAnim = new ColorAnimation
            {
                To = Colors.Transparent,
                Duration = TimeSpan.FromMilliseconds(800),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            colorAnim.Completed += (_, __) =>
            {
                navItem.ClearValue(ListBoxItem.BackgroundProperty);
            };

            animBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
        }

        private void FlashElement(FrameworkElement target)
        {
            var accentBrush = TryFindResource("SystemAccentPressedBrush") as SolidColorBrush;
            if (accentBrush == null) return;

            var originalBg = target.GetValue(Panel.BackgroundProperty);
            var flashBrush = new SolidColorBrush(accentBrush.Color) { Opacity = 0.3 };
            target.SetValue(Panel.BackgroundProperty, flashBrush);

            var colorAnim = new ColorAnimation
            {
                To = Colors.Transparent,
                Duration = TimeSpan.FromMilliseconds(1500),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            colorAnim.Completed += (_, __) =>
            {
                target.SetValue(Panel.BackgroundProperty, originalBg);
            };

            flashBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
        }

        #region Navigation & Window Logic

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnMaximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void HamburgerBtn_Click(object sender, RoutedEventArgs e)
        {
            // 切换状态
            SetSidebarState(!_isNavExpanded);
        }

        private bool _isInternalChange = false;
        private Dictionary<string, UserControl> _pages = new Dictionary<string, UserControl>();

        private void NavListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NavListBox == null || BottomListBox == null) return;

            if (_isInternalChange) return;

            System.Windows.Controls.ListBox source = sender as System.Windows.Controls.ListBox;
            if (source == null || source.SelectedIndex == -1) return;

            _isInternalChange = true;
            if (source == NavListBox) BottomListBox.SelectedIndex = -1; // 处理两个 ListBox 的互斥选中逻辑
            else NavListBox.SelectedIndex = -1;
            string tag = "";
            if (source.SelectedItem is ListBoxItem item && item.Tag != null) tag = item.Tag.ToString();
            NavigateToPage(tag);

            _isInternalChange = false;
        }


        private void NavigateToPage(string tag)
        {
            UpdateSpecialNavItemsVisibility(tag);
            UserControl page = null;

            // 懒加载：如果缓存里没有，就创建新的
            if (!_pages.ContainsKey(tag))
            {
                switch (tag)
                {
                    case "General": page = new Pages.GeneralPage(); break;
                    case "Paint": page = new Pages.PaintPage(); break;
                    case "View": page = new Pages.ViewPage(); break;
                    case "Shortcuts": page = new Pages.ShortcutsPage(); break;
                    case "Advanced":
                        page = new Pages.AdvancedPage();
                        break;
                    case "Plugins":
                        page = new Pages.PluginPage();
                        break;
                    case "DevTools":
                        page = new Pages.DevToolsPage();
                        break;
                    case "SystemReport":
                        page = new Pages.SystemReportPage();
                        break;
                    case "Agreement":
                        page = new Pages.AgreementPage();
                        break;
                    case "About":
                        page = new Pages.AboutPage();
                        break;
                    default:
                        break;
                }
                if (page != null) _pages[tag] = page;
            }
            else page = _pages[tag];
            if (page != null)
            {
                AnimatePageTransition(page);
            }
        }

        private void AnimatePageTransition(UserControl newPage)
        {
            var oldPage = MainContent.Content as UIElement;

            // 如果目标页面已经是当前页面，跳过动画
            if (oldPage == newPage) return;

            // 新页面初始状态：透明，向右偏移30，轻微放大1.05
            newPage.Opacity = 0;
            var newPageTransform = new TranslateTransform(30, 0);
            var newPageScale = new ScaleTransform(1.0, 1.0);
            newPage.RenderTransform = new TransformGroup
            {
                Children = new TransformCollection { newPageTransform, newPageScale }
            };

            // 先显示新页面
            MainContent.Content = newPage;

            const int durationMs = 70;
            var duration = TimeSpan.FromMilliseconds(durationMs);
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };

            // 新页面进入动画：从右侧滑入，淡入，缩放
            var fadeIn = new DoubleAnimation(1, duration) { EasingFunction = easeOut };
            var slideIn = new DoubleAnimation(0, duration) { EasingFunction = easeOut };
            var scaleIn = new DoubleAnimation(1, duration) { EasingFunction = easeOut };

            newPage.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            newPageTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);
            newPageScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
            newPageScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn);

            // 旧页面退出动画：左移，淡出，缩小
            if (oldPage != null)
            {
                var oldTransform = oldPage.RenderTransform as TranslateTransform;
                if (oldTransform == null)
                {
                    oldTransform = new TranslateTransform(0, 0);
                    oldPage.RenderTransform = oldTransform;
                }

                var fadeOut = new DoubleAnimation(0, duration);
                var slideOut = new DoubleAnimation(-30, duration);
                var scaleOut = new DoubleAnimation(0.95, duration);

                oldPage.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                oldTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);

                var oldScale = oldPage.RenderTransform as ScaleTransform;
                if (oldScale == null)
                {
                    var group = oldPage.RenderTransform as TransformGroup;
                    if (group != null)
                    {
                        foreach (var child in group.Children)
                        {
                            if (child is ScaleTransform st)
                            {
                                oldScale = st;
                                break;
                            }
                        }
                    }
                }
                if (oldScale != null)
                {
                    oldScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleOut);
                    oldScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleOut);
                }
            }
        }

        public bool NavigateToTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return false;

            var item = FindNavItemByTag(tag);
            if (item == null)
            {
                NavigateToPage(tag);
                return MainContent.Content != null;
            }

            _isInternalChange = true;
            bool inNavList = false;
            foreach (var obj in NavListBox.Items)
            {
                if (obj == item) { inNavList = true; break; }
            }
            if (inNavList)
            {
                NavListBox.SelectedItem = item;
                BottomListBox.SelectedIndex = -1;
            }
            else
            {
                BottomListBox.SelectedItem = item;
                NavListBox.SelectedIndex = -1;
            }
            _isInternalChange = false;

            NavigateToPage(tag);
            return true;
        }

        private ListBoxItem? FindNavItemByTag(string tag)
        {
            foreach (var obj in NavListBox.Items)
            {
                if (obj is ListBoxItem item && string.Equals(item.Tag?.ToString(), tag, StringComparison.Ordinal))
                {
                    return item;
                }
            }
            foreach (var obj in BottomListBox.Items)
            {
                if (obj is ListBoxItem item && string.Equals(item.Tag?.ToString(), tag, StringComparison.Ordinal))
                {
                    return item;
                }
            }
            return null;
        }

        public void TogglePinPage(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return;
            var settings = SettingsManager.Instance.Current;
            if (settings == null) return;

            if (settings.PinnedSettingsTags.Contains(tag))
            {
                settings.PinnedSettingsTags.Remove(tag);
            }
            else
            {
                settings.PinnedSettingsTags.Add(tag);
            }

            SettingsManager.Instance.Save();
            UpdateSpecialNavItemsVisibility(tag); // tag 是当前正在操作的页，可能需要重新评估显示
        }

        public bool IsPagePinned(string tag)
        {
            return SettingsManager.Instance.Current?.PinnedSettingsTags.Contains(tag) ?? false;
        }

        private void UpdateSpecialNavItemsVisibility(string tag)
        {
            var pinnedTags = SettingsManager.Instance.Current?.PinnedSettingsTags ?? new List<string>();

            var pluginsItem = FindNavItemByTag("Plugins");
            var devToolsItem = FindNavItemByTag("DevTools");
            var systemReportItem = FindNavItemByTag("SystemReport");
            var agreementItem = FindNavItemByTag("Agreement");

            if (pluginsItem != null)
            {
                AnimateSpecialNavItemVisibility(
                    pluginsItem,
                    string.Equals(tag, "Plugins", StringComparison.Ordinal) || pinnedTags.Contains("Plugins"));
            }

            if (devToolsItem != null)
            {
                AnimateSpecialNavItemVisibility(
                    devToolsItem,
                    string.Equals(tag, "DevTools", StringComparison.Ordinal) || pinnedTags.Contains("DevTools"));
            }

            if (systemReportItem != null)
            {
                AnimateSpecialNavItemVisibility(
                    systemReportItem,
                    string.Equals(tag, "SystemReport", StringComparison.Ordinal) || pinnedTags.Contains("SystemReport"));
            }

            if (agreementItem != null)
            {
                AnimateSpecialNavItemVisibility(
                    agreementItem,
                    string.Equals(tag, "Agreement", StringComparison.Ordinal) || pinnedTags.Contains("Agreement"));
            }
        }

        private void AnimateSpecialNavItemVisibility(ListBoxItem item, bool show)
        {
            const double itemHeight = 40;
            var duration = TimeSpan.FromMilliseconds(140);

            item.BeginAnimation(OpacityProperty, null);
            item.BeginAnimation(FrameworkElement.MaxHeightProperty, null);

            if (show)
            {
                if (item.Visibility == Visibility.Visible && item.Opacity >= 0.99 && item.MaxHeight >= itemHeight)
                {
                    return;
                }

                item.Visibility = Visibility.Visible;
                item.Opacity = 0;
                item.MaxHeight = 0;

                var fadeIn = new DoubleAnimation(1, duration)
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var expand = new DoubleAnimation(itemHeight, duration)
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                item.BeginAnimation(OpacityProperty, fadeIn);
                item.BeginAnimation(FrameworkElement.MaxHeightProperty, expand);
                return;
            }

            if (item.Visibility == Visibility.Collapsed)
            {
                return;
            }

            var fadeOut = new DoubleAnimation(0, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            var collapse = new DoubleAnimation(0, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (_, __) =>
            {
                item.Visibility = Visibility.Collapsed;
                item.MaxHeight = itemHeight;
            };

            item.BeginAnimation(OpacityProperty, fadeOut);
            item.BeginAnimation(FrameworkElement.MaxHeightProperty, collapse);
        }

        #endregion

        #region Toast Logic

        private DateTime _lastToastTime = DateTime.MinValue;
        private bool _isToasting = false;

        private async void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            TabPaint.SettingsManager.Instance.Save();
            if (_isToasting) return;
            _lastToastTime = DateTime.Now;
            _isToasting = true;

            ShowToast();
            await Task.Delay(1000);
            if ((DateTime.Now - _lastToastTime).TotalMilliseconds >= 1000)
            {
                HideToast();
                _isToasting = false;
            }
        }
        #endregion
    }
}
