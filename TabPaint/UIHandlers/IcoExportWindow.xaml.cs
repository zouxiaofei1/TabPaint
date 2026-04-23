using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TabPaint.Core; // 引用 IcoEncoder

namespace TabPaint.Windows
{
    // 用于列表绑定的简单模型
    public class IcoSizeItem
    {
        public int Size { get; set; }
        public string DisplayText => $"{Size} x {Size}";
        public ImageSource Image { get; set; } // 缓存的小图
    }

    public partial class IcoExportWindow : Window
    {
        private BitmapSource _sourceImage;
        public ObservableCollection<IcoSizeItem> SizeItems { get; set; } = new ObservableCollection<IcoSizeItem>();
        public bool IsConfirmed { get; private set; } = false;
        public List<int> ResultSizes { get; private set; } = new List<int>();

        public IcoExportWindow(BitmapSource source)
        {
            InitializeComponent();
            this.SupportFocusHighlight();
            _sourceImage = source;
            SizeListBox.ItemsSource = SizeItems;
            AddSize(256);  AddSize(64);AddSize(48);AddSize(32); AddSize(16);// 默认尺寸

            // 默认选中第一个
            if (SizeItems.Count > 0) SizeListBox.SelectedIndex = 0;
            this.Loaded += IcoExportWindow_Loaded;
        }
        private void IcoExportWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 适配暗黑模式的边框颜色等（如果需要）
            bool isDark = ThemeManager.CurrentAppliedTheme == AppTheme.Dark;
            ThemeManager.SetWindowImmersiveDarkMode(this, isDark);
        }

        private void LargePreviewImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (LargePreviewImage.Source == null)
            {
                ImageBackgroundBorder.Visibility = Visibility.Collapsed;
                return;
            }

            // 获取图片源的原始尺寸
            var bmp = LargePreviewImage.Source as BitmapSource;
            if (bmp == null) return;
            double containerWidth = LargePreviewImage.ActualWidth;
            double containerHeight = LargePreviewImage.ActualHeight;

            if (containerWidth == 0 || containerHeight == 0) return;

            double imageRatio = bmp.PixelWidth / (double)bmp.PixelHeight;
            double containerRatio = containerWidth / containerHeight;

            double renderWidth, renderHeight;

            if (imageRatio >= containerRatio)
            {
                renderWidth = containerWidth;
                renderHeight = containerWidth / imageRatio;
            }
            else
            {
                renderWidth = containerHeight * imageRatio;
                renderHeight = containerHeight;
            }

            // 设置背景 Border 的大小
            ImageBackgroundBorder.Width = renderWidth;
            ImageBackgroundBorder.Height = renderHeight;
            ImageBackgroundBorder.Visibility = Visibility.Visible;
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
        private void AddSize(int size)
        {
            if (SizeItems.Any(x => x.Size == size)) return;
            var preview = CreateIcoPreview(_sourceImage, size);

            var item = new IcoSizeItem { Size = size, Image = preview };
            int insertIndex = 0;
            while (insertIndex < SizeItems.Count && SizeItems[insertIndex].Size > size)
                insertIndex++;

            SizeItems.Insert(insertIndex, item);
        }
        private BitmapSource CreateIcoPreview(BitmapSource source, int targetSize)
        {
            var drawingVisual = new DrawingVisual();
            using (var dc = drawingVisual.RenderOpen())
            {
                double ratio = Math.Min((double)targetSize / source.PixelWidth, (double)targetSize / source.PixelHeight);
                double newWidth = source.PixelWidth * ratio;
                double newHeight = source.PixelHeight * ratio;

                double x = (targetSize - newWidth) / 2;
                double y = (targetSize - newHeight) / 2;

                dc.DrawImage(source, new Rect(x, y, newWidth, newHeight));
            }

            var renderBitmap = new RenderTargetBitmap(targetSize, targetSize, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(drawingVisual);
            if (renderBitmap.CanFreeze) renderBitmap.Freeze();
            return renderBitmap;
        }
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void OnAddSizeClick(object sender, RoutedEventArgs e)
        {
            if (SizeComboBox.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Content.ToString().Split(' ')[0], out int size))
            {
                AddSize(size);
            }
        }

        private void OnDeleteSizeClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is IcoSizeItem item)
            {
                SizeItems.Remove(item);
                if (SizeItems.Count == 0) LargePreviewImage.Source = null;
            }
        }

        private void SizeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SizeListBox.SelectedItem is IcoSizeItem item)
            {
                LargePreviewImage.Source = item.Image;
                PreviewHintText.Visibility = Visibility.Collapsed;
            }
            else
            {
                LargePreviewImage.Source = null;
                PreviewHintText.Visibility = Visibility.Visible;
            }
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (SizeItems.Count == 0)
            {
                FluentMessageBox.Show(LocalizationManager.GetString("L_Ico_Error_NoSize"), "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ResultSizes = SizeItems.Select(x => x.Size).ToList();
            IsConfirmed = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) {  IsConfirmed = false; Close();  }

        private void OnCloseClick(object sender, RoutedEventArgs e) { IsConfirmed = false; Close();  }
    }
}
