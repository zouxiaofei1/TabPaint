using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using XamlAnimatedGif; // 引用库
namespace TabPaint
{
    public class IndicatorItem : INotifyPropertyChanged
    {
        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }
    public class HelpPage
    {
        public Uri ImageUri { get; set; }
        public string DescriptionKey { get; set; }
    }
    public partial class HelpWindow : Window, INotifyPropertyChanged
    {
        private List<HelpPage> _pages;
        private int _currentIndex = 0;
        private DispatcherTimer _gifDelayTimer;
        private static readonly HttpClient _httpClient = new HttpClient();

        public event PropertyChangedEventHandler PropertyChanged;
        private Uri _displayUri; // 图片显示源
        public Uri DisplayUri
        {
            get => _displayUri;
            set { _displayUri = value; OnPropertyChanged(nameof(DisplayUri)); }
        }
        public string CurrentDescription => _pages.Count > 0 ? LocalizationManager.GetString(_pages[_currentIndex].DescriptionKey) : "";  // 当前描述
        private bool _isBusy; // UI 状态控制：是否显示遮罩层（加载或错误）
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(nameof(IsBusy)); }
        }
        private bool _hasError; // UI 状态控制：是否出错
        public bool HasError
        {
            get => _hasError;
            set { _hasError = value; OnPropertyChanged(nameof(HasError)); }
        }

        // 多语言文本绑定
        public string LoadingText => LocalizationManager.GetString("L_Loading");
        public string ErrorText => LocalizationManager.GetString("L_LoadFailed");

        public ObservableCollection<IndicatorItem> Indicators { get; set; } = new ObservableCollection<IndicatorItem>();

        public HelpWindow(List<HelpPage> pages)
        {
            InitializeComponent();
            this.SupportFocusHighlight();
            _pages = pages;
            this.DataContext = this;

            foreach (var page in _pages)
                Indicators.Add(new IndicatorItem { IsActive = false });

            _gifDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0) };
            _gifDelayTimer.Tick += GifDelayTimer_Tick;

            _ = LoadCurrentPageAsync();
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // 确保句柄已经准备好
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            bool isDark = ThemeManager.CurrentAppliedTheme == AppTheme.Dark;
            ThemeManager.SetWindowImmersiveDarkMode(this, isDark);

            MicaAcrylicManager.ApplyEffect(this);
            if (!MicaAcrylicManager.IsWin11())
            {
                var chromeLow = FindResource("ChromeLowBrush") as Brush;
            }
        }
        private async Task LoadCurrentPageAsync()
        {
            if (_pages == null || _pages.Count == 0) return;

            // 更新基本 UI
            OnPropertyChanged(nameof(CurrentDescription));
            // 刷新本地化文本
            OnPropertyChanged(nameof(LoadingText));
            OnPropertyChanged(nameof(ErrorText));

            for (int i = 0; i < Indicators.Count; i++)
                Indicators[i].IsActive = (i == _currentIndex);

            Uri rawUri = _pages[_currentIndex].ImageUri;

            if (rawUri.Scheme == Uri.UriSchemeHttp || rawUri.Scheme == Uri.UriSchemeHttps)
            {
                // 网络图片流程
                IsBusy = true;
                HasError = false;
                DisplayUri = null; // 清空旧图

                try
                {
                    string localPath = await DownloadImageAsync(rawUri.ToString());
                    DisplayUri = new Uri(localPath);
                    IsBusy = false; // 下载成功，隐藏遮罩
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"下载失败: {ex.Message}");
                    HasError = true; // 出错了，IsBusy 保持 true 以显示遮罩，但内容变为错误信息
                }
            }
            else
            {
                // 本地图片流程
                IsBusy = false;
                HasError = false;
                DisplayUri = rawUri;
            }
            if (!IsBusy && !HasError) ResetGifAnimation();
        }

        private async Task<string> DownloadImageAsync(string url)
        {
            string cacheDir = Path.Combine(Path.GetTempPath(), "TabPaintCache");
            if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);

            string fileName = Path.GetFileName(new Uri(url).LocalPath);
            if (string.IsNullOrEmpty(fileName)) fileName = "temp_" + url.GetHashCode() + ".gif";
            string localFilePath = Path.Combine(cacheDir, fileName);

            if (File.Exists(localFilePath))
                return localFilePath;

            // 添加超时控制
            using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                byte[] data = await _httpClient.GetByteArrayAsync(url, cts.Token);
                await File.WriteAllBytesAsync(localFilePath, data);
            }

            return localFilePath;
        }

        // 点击重试
        private void Retry_Click(object sender, MouseButtonEventArgs e)
        {
            _ = LoadCurrentPageAsync();
        }
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private void ResetGifAnimation()
        {
            _gifDelayTimer.Stop();
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                try
                {
                    var controller = AnimationBehavior.GetAnimator(MainImageDisplay);
                    if (controller != null)
                    {
                        controller.Rewind();
                        controller.Pause();
                        _gifDelayTimer.Start();
                    }
                }
                catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
            }));
        }

        private void GifDelayTimer_Tick(object sender, EventArgs e)
        {
            _gifDelayTimer.Stop();
            try
            {
                var controller = AnimationBehavior.GetAnimator(MainImageDisplay);
                if (controller != null) controller.Play();
            }
            catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
        }
        private void Next()
        {
            _currentIndex = (_currentIndex + 1) % _pages.Count;
            _ = LoadCurrentPageAsync();
        }

        private void Previous()
        {
            _currentIndex = (_currentIndex - 1 + _pages.Count) % _pages.Count;
            _ = LoadCurrentPageAsync();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) this.DragMove();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e) => Next();
        private void PrevButton_Click(object sender, RoutedEventArgs e) => Previous();
        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left) Previous();
            else if (e.Key == Key.Right) Next();
            else if (e.Key == Key.Escape) this.Close();
        }
    }


}
