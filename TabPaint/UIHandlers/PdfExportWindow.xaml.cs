using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TabPaint.Services;

namespace TabPaint.Windows
{
    public class PdfPageItem : INotifyPropertyChanged
    {
        public string PageTitle { get; set; }
        public string PageInfo { get; set; }

        private string _pageNumberText;
        public string PageNumberText
        {
            get => _pageNumberText;
            set
            {
                _pageNumberText = value;
                OnPropertyChanged(nameof(PageNumberText));
            }
        }

        private bool _isDragOver;
        public bool IsDragOver
        {
            get => _isDragOver;
            set
            {
                _isDragOver = value;
                OnPropertyChanged(nameof(IsDragOver));
            }
        }

        private bool _canDelete = true;
        public bool CanDelete
        {
            get => _canDelete;
            set
            {
                _canDelete = value;
                OnPropertyChanged(nameof(CanDelete));
            }
        }

        private ImageSource _previewImage;
        public ImageSource PreviewImage
        {
            get => _previewImage;
            set
            {
                _previewImage = value;
                OnPropertyChanged(nameof(PreviewImage));
            }
        }

        private ImageSource _fullImage;
        public ImageSource FullImage
        {
            get
            {
                if (_fullImage == null)
                {
                    _ = LoadFullImageAsync();
                    return PreviewImage;
                }
                return _fullImage;
            }
            set
            {
                _fullImage = value;
                OnPropertyChanged(nameof(FullImage));
            }
        }

        private bool _isLoadingFull = false;
        public MainWindow.FileTabItem TabItem { get; set; }
        public MainWindow MainWindow { get; set; }

        public async Task LoadFullImageAsync()
        {
            if (_isLoadingFull || _fullImage != null || MainWindow == null || TabItem == null) return;
            _isLoadingFull = true;

            try
            {
                var bitmap = await Task.Run(() =>
                {
                    using (var stream = MainWindow.GetImageStreamForTab(TabItem))
                    {
                        if (stream == null) return null;
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource = stream;
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp as ImageSource;
                    }
                });

                if (bitmap != null)
                {
                    FullImage = bitmap;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lazy load full image error: {ex.Message}");
            }
            finally
            {
                _isLoadingFull = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class PdfExportWindow : Window
    {
        public ObservableCollection<PdfPageItem> PageItems { get; set; } = new ObservableCollection<PdfPageItem>();
        public bool IsConfirmed { get; private set; } = false;
        private MainWindow _mainWindow;

        public PdfExportWindow(List<MainWindow.FileTabItem> tabs, MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            this.SupportFocusHighlight();
            
            PageListBox.ItemsSource = PageItems;
            PreviewListBox.ItemsSource = PageItems;

            string initialPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "Combined_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".pdf");
            SavePathTextBox.Text = initialPath;

            int index = 1;
            bool canDelete = tabs.Count > 1;
            foreach (var tab in tabs)
            {
                var item = new PdfPageItem
                {
                    PageTitle = tab.DisplayName,
                    TabItem = tab,
                    MainWindow = mainWindow,
                    PageNumberText = $"{LocalizationManager.GetString("L_PdfExport_Page") ?? "Page"} {index}",
                    CanDelete = canDelete,
                    PageInfo = $"{tab.PixelWidth} x {tab.PixelHeight}"
                };

                // 只快速加载缩略图作为初始预览
                try
                {
                    using (var stream = mainWindow.GetImageStreamForTab(tab))
                    {
                        if (stream != null)
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = stream;
                            bitmap.DecodePixelWidth = 120; // 限制缩略图宽度以节省显存
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            item.PreviewImage = bitmap;
                        }
                    }
                }
                catch (Exception ex)
                {
                    item.PageInfo = "Error loading image";
                    System.Diagnostics.Debug.WriteLine($"PDF Preview load error: {ex.Message}");
                }

                PageItems.Add(item);
                index++;
            }

            this.Loaded += PdfExportWindow_Loaded;
        }

        private void PdfExportWindow_Loaded(object sender, RoutedEventArgs e)
        {
            bool isDark = ThemeManager.CurrentAppliedTheme == AppTheme.Dark;
            ThemeManager.SetWindowImmersiveDarkMode(this, isDark);

            // 启动入场动画
            if (this.Resources["WindowEntranceAnimation"] is System.Windows.Media.Animation.Storyboard storyboard)
            {
                storyboard.Begin();
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            MicaAcrylicManager.ApplyEffect(this);
            if (!MicaAcrylicManager.IsWin11())
            {
                var bgBrush = Application.Current.TryFindResource("WindowBackgroundBrush") as Brush
                              ?? Application.Current.TryFindResource("ControlBackgroundBrush") as Brush;
                RootBorder.Background = bgBrush ?? Brushes.White;
            }
            else
            {
                RootBorder.Background = Brushes.Transparent;
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) { IsConfirmed = false; Close(); }
        private void OnCancelClick(object sender, RoutedEventArgs e) { IsConfirmed = false; Close(); }

        private void OnBrowseClick(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = Path.GetFileName(SavePathTextBox.Text),
                DefaultExt = ".pdf"
            };

            if (dlg.ShowDialog() == true)
            {
                SavePathTextBox.Text = dlg.FileName;
            }
        }

        private void OnDeletePageClick(object sender, RoutedEventArgs e)
        {
            if (PageItems.Count <= 1) return;

            if (sender is Button btn && btn.DataContext is PdfPageItem item)
            {
                PageItems.Remove(item);
                UpdatePageItemsState();
            }
        }

        private void UpdatePageItemsState()
        {
            string pageText = LocalizationManager.GetString("L_PdfExport_Page") ?? "Page";
            bool canDelete = PageItems.Count > 1;
            for (int i = 0; i < PageItems.Count; i++)
            {
                PageItems[i].PageNumberText = $"{pageText} {i + 1}";
                PageItems[i].CanDelete = canDelete;
            }
        }

        private void UpdatePageNumbers() => UpdatePageItemsState();

        #region Drag and Drop

        private Point _dragStartPoint;

        private void Item_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("PdfPageItem"))
            {
                if (sender is FrameworkElement fe && fe.DataContext is PdfPageItem item)
                {
                    item.IsDragOver = true;
                }
            }
        }

        private void Item_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is PdfPageItem item) item.IsDragOver = false;
        }

        private void ListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void ListBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _dragStartPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    ListBox listBox = sender as ListBox;
                    ListBoxItem listBoxItem = FindAnchestor<ListBoxItem>((DependencyObject)e.OriginalSource);

                    if (listBoxItem == null) return;

                    PdfPageItem item = (PdfPageItem)listBox.ItemContainerGenerator.ItemFromContainer(listBoxItem);

                    DataObject dragData = new DataObject("PdfPageItem", item);
                    DragDrop.DoDragDrop(listBoxItem, dragData, DragDropEffects.Move);
                }
            }
        }

        private void ListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("PdfPageItem"))
            {
                PdfPageItem droppedData = e.Data.GetData("PdfPageItem") as PdfPageItem;
                PdfPageItem target = ((FrameworkElement)e.OriginalSource).DataContext as PdfPageItem;

                // Reset all drag over states
                foreach (var item in PageItems) item.IsDragOver = false;

                if (droppedData != null && target != null && droppedData != target)
                {
                    int oldIndex = PageItems.IndexOf(droppedData);
                    int newIndex = PageItems.IndexOf(target);

                    if (oldIndex != -1 && newIndex != -1)
                    {
                        PageItems.Move(oldIndex, newIndex);
                        UpdatePageNumbers();
                    }
                }
            }
        }

        private static T FindAnchestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T) return (T)current;
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }

        #endregion

        private async void OnSaveClick(object sender, RoutedEventArgs e)
        {
            string targetPath = SavePathTextBox.Text;
            if (string.IsNullOrEmpty(targetPath)) return;

            if (PageItems.Count == 0) return;

            IsConfirmed = true;
            var items = PageItems.Select(p => p.TabItem).ToList();
            await _mainWindow.ExportTabsToPdfAsync(items, targetPath);
            this.Close();
        }
    }
}
