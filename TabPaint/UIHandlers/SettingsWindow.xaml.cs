using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using System.Net.Http;

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
        public SettingsWindow()
        {
            InitializeComponent();
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
                try {  Process.Start(new ProcessStartInfo(_latestVersionUrl) { UseShellExecute = true });}catch { }
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
            else  element.Visibility = Visibility.Collapsed;
        }
        
       
        private bool IsNewerVersion(string currentRaw, string latestRaw)
        {
            try
            {
                var current = Version.Parse(currentRaw.TrimStart('v', 'V').Trim());
                var latest = Version.Parse(latestRaw.TrimStart('v', 'V').Trim());

                return latest > current;
            }
            catch {  return false; }
        }
        private void SettingsWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width < 750 && _isNavExpanded)   SetSidebarState(false);
            else if (e.NewSize.Width >= 750 && !_isNavExpanded)  SetSidebarState(true);
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
            if (TxtSystemReport != null) TxtSystemReport.Visibility = textVis;
            if (FindName("TxtAgreement") is TextBlock txtAgreement) txtAgreement.Visibility = textVis;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)return;
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
            if (source == NavListBox)  BottomListBox.SelectedIndex = -1; // 处理两个 ListBox 的互斥选中逻辑
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
                if (page != null)  _pages[tag] = page;
            }
            else page = _pages[tag];
            if (page != null)
            {
                MainContent.Content = page;
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
            NavListBox.SelectedItem = item;
            BottomListBox.SelectedIndex = -1;
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

            return null;
        }

        private void UpdateSpecialNavItemsVisibility(string tag)
        {
            var pluginsItem = FindNavItemByTag("Plugins");
            var systemReportItem = FindNavItemByTag("SystemReport");
            var agreementItem = FindNavItemByTag("Agreement");

            if (pluginsItem != null)
            {
                AnimateSpecialNavItemVisibility(
                    pluginsItem,
                    string.Equals(tag, "Plugins", StringComparison.Ordinal));
            }

            if (systemReportItem != null)
            {
                AnimateSpecialNavItemVisibility(
                    systemReportItem,
                    string.Equals(tag, "SystemReport", StringComparison.Ordinal));
            }

            if (agreementItem != null)
            {
                AnimateSpecialNavItemVisibility(
                    agreementItem,
                    string.Equals(tag, "Agreement", StringComparison.Ordinal));
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
    public class GridLengthAnimation : AnimationTimeline
    {
        public override Type TargetPropertyType => typeof(GridLength);
        protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

        public GridLength From { get; set; }
        public GridLength To { get; set; }
        public IEasingFunction EasingFunction { get; set; }

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
        {
            double fromVal = From.Value;
            double toVal = To.Value;

            if (fromVal > toVal)
                return new GridLength((1 - animationClock.CurrentProgress.Value) * (fromVal - toVal) + toVal, GridUnitType.Pixel);
            else
                return new GridLength(animationClock.CurrentProgress.Value * (toVal - fromVal) + fromVal, GridUnitType.Pixel);
        }
    }
}
