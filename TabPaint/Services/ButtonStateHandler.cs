using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using static TabPaint.MainWindow;

//
//更新按钮状态的相关代码
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public void UpdateCurrentColor(Color color, bool secondColor = false) // 更新前景色按钮颜色
        {
            if (secondColor)
            {
                BackgroundBrush = new SolidColorBrush(color);
                OnPropertyChanged(nameof(BackgroundBrush)); // 通知绑定刷新
                _ctx.PenColor = color;
                BackgroundColor = color;
            }
            else
            {
                ForegroundBrush = new SolidColorBrush(color);
                OnPropertyChanged(nameof(ForegroundBrush)); // 通知绑定刷新
                ForegroundColor = color;
            }

        }

        private void SetBrushStyle(BrushStyle style)
        {//设置画笔样式，所有画笔都是pen工具
            _router.SetTool(_tools.Pen);
            _ctx.PenStyle = style;
            UpdateToolSelectionHighlight();

            SetPenResizeBarVisibility(_ctx.PenStyle != BrushStyle.Pencil);
        }

        private void SetThicknessSlider_Pos(double newValue)
        {
            // 根据 Slider 高度和当前值，计算提示位置
            Rect rect = new Rect(
             ThicknessSlider.TransformToAncestor(this).Transform(new Point(0, 0)),
             new Size(ThicknessSlider.ActualWidth, ThicknessSlider.ActualHeight));
            double trackHeight = ThicknessSlider.ActualHeight;
            double relativeValue = (ThicknessSlider.Maximum - newValue) / (ThicknessSlider.Maximum - ThicknessSlider.Minimum);
            double offsetY = relativeValue * trackHeight;

            ThicknessTip.Margin = new Thickness(80, offsetY + rect.Top - 10, 0, 0);
        }

        private void UpdateThicknessPreviewPosition()
        {
            if (ThicknessPreview == null) return;

            // 图像缩放比例
            double zoom = ZoomTransform.ScaleX;   // or ScaleY，通常两者相等
            double size = PenThickness * 2 * zoom; // 半径→界面直径 * 缩放

            ThicknessPreview.Width = size;
            ThicknessPreview.Height = size;

            ThicknessPreview.Fill = Brushes.Transparent;
            ThicknessPreview.StrokeThickness = 2;
        }


        private void UpdateWindowTitle()
        {
            // 0. 防御性编程：如果没有选中项，显示默认标题
            if (_currentTabItem == null)
            {
                this.Title = $"TabPaint {_programVersion}";
                if (TitleTextBlock != null) TitleTextBlock.Text = this.Title;
                return;
            }

            string dirtyMark = _currentTabItem.IsDirty ? "*" : "";
            string displayFileName = _currentTabItem.FileName;

            string countInfo = "";
            if (_currentTabItem.IsNew)
            {
                countInfo = ""; // 新建文件不显示进度
            }
            else
            {
                int total = _imageFiles.Count;
                int currentIndex = FileTabs.IndexOf(_currentTabItem);
                if (currentIndex == -1 && !string.IsNullOrEmpty(_currentTabItem.FilePath))
                {
                    currentIndex = _imageFiles.IndexOf(_currentTabItem.FilePath);
                }

                if (currentIndex >= 0 && total > 0)
                {
                    countInfo = $"({currentIndex + 1}/{total})";
                }
            }

            // 4. 拼接标题
            string newTitle = $"{dirtyMark}{displayFileName} {countInfo} - TabPaint {_programVersion}";

            // 5. 更新 UI
            this.Title = newTitle;
            if (TitleTextBlock != null) TitleTextBlock.Text = newTitle;
        }


        public void ShowTextToolbarFor(System.Windows.Controls.TextBox tb)
        {
            _activeTextBox = tb;
            TextEditBar.Visibility = Visibility.Visible;

            FontFamilyBox.SelectedItem = tb.FontFamily;
            FontSizeBox.Text = tb.FontSize.ToString(CultureInfo.InvariantCulture);
            BoldBtn.IsChecked = tb.FontWeight == FontWeights.Bold;
            ItalicBtn.IsChecked = tb.FontStyle == FontStyles.Italic;
            UnderlineBtn.IsChecked = tb.TextDecorations == TextDecorations.Underline;
        }

        public void HideTextToolbar()
        {
            TextEditBar.Visibility = Visibility.Collapsed;
            _activeTextBox = null;
        }
        private void SetRestoreIcon()
        {
            MaxRestoreButton.Content = new Image
            {
                Source = (DrawingImage)FindResource("Restore_Image"),
                Width = 12,
                Height = 12
            };
        }
        private void SetMaximizeIcon()
        {
            MaxRestoreButton.Content = new Viewbox
            {
                Width = 10,
                Height = 10,
                Child = new Rectangle
                {
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Width = 12,
                    Height = 12
                }
            };
        }
        private void UpdateColorHighlight()
        {

            // 假设你的两个颜色按钮在 XAML 里设置了 Name="ColorBtn1" 和 Name="ColorBtn2"
            ColorBtn1.Tag = !useSecondColor ? "True" : "False"; // 如果不是色2，那就是色1选中
            ColorBtn2.Tag = useSecondColor ? "True" : "False";
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            var src = (HwndSource)PresentationSource.FromVisual(this)!;// 让 WPF 的合成目标透明，否则会被清成黑色
            src.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        private void SetPenResizeBarVisibility(bool vis)
        {
            ((MainWindow)System.Windows.Application.Current.MainWindow).ThicknessPanel.Visibility = vis ? Visibility.Visible : Visibility.Collapsed;
        }

        public void SetUndoRedoButtonState()
        {
            UpdateBrushAndButton(UndoButton, UndoIcon, _undo.CanUndo);
            UpdateBrushAndButton(RedoButton, RedoIcon, _undo.CanRedo);

        }

        public void SetCropButtonState()
        {
            UpdateBrushAndButton(CutImage, CutImageIcon, _tools.Select is SelectTool st && _ctx.SelectionOverlay.Visibility != Visibility.Collapsed);
        }

        private void UpdateBrushAndButton(System.Windows.Controls.Button button, Image image, bool isEnabled)
        {
            button.IsEnabled = isEnabled;


            var frozenDrawingImage = (DrawingImage)image.Source; // 获取当前 UI 使用的绘图对象
            var modifiableDrawingImage = frozenDrawingImage.Clone();    // 克隆出可修改的副本
            if (modifiableDrawingImage.Drawing is GeometryDrawing geoDrawing)  // DrawingImage.Drawing 可能是 DrawingGroup 或 GeometryDrawing
            {
                geoDrawing.Brush = isEnabled ? Brushes.Black : Brushes.Gray;
            }
            else if (modifiableDrawingImage.Drawing is DrawingGroup group)
            {
                foreach (var child in group.Children)
                {
                    if (child is GeometryDrawing childGeo)
                    {
                        childGeo.Brush = isEnabled ? Brushes.Black : Brushes.Gray;
                    }
                }
            }

            // 替换 Image.Source，让 UI 用新的对象
            image.Source = modifiableDrawingImage;
        }
        private byte[] ExtractRegionFromBitmap(WriteableBitmap bmp, Int32Rect rect)
        {
            int stride = bmp.BackBufferStride;
            byte[] region = new byte[rect.Width * rect.Height * 4];

            bmp.Lock();
            for (int row = 0; row < rect.Height; row++)
            {
                IntPtr src = bmp.BackBuffer + (rect.Y + row) * stride + rect.X * 4;
                System.Runtime.InteropServices.Marshal.Copy(src, region, row * rect.Width * 4, rect.Width * 4);
            }
            bmp.Unlock();
            return region;
        }
        private void SaveBitmap(string path)
        {
            // 1. 原有的保存逻辑
            using (FileStream fs = new FileStream(path, FileMode.Create))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(_bitmap));
                encoder.Save(fs);
            }
            MarkAsSaved();
            // 2. 新增：更新对应标签页的缩略图
            UpdateTabThumbnail(path);
        }

        private Color GetPixelColor(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _bmpWidth || y >= _bmpHeight) return Colors.Transparent;

            _bitmap.Lock();
            unsafe
            {
                IntPtr pBackBuffer = _bitmap.BackBuffer;
                int stride = _bitmap.BackBufferStride;
                byte* p = (byte*)pBackBuffer + y * stride + x * 4;
                byte b = p[0];
                byte g = p[1];
                byte r = p[2];
                byte a = p[3];
                _bitmap.Unlock();
                return Color.FromArgb(a, r, g, b);
            }
        }
        private void DrawPixel(int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= _bmpWidth || y >= _bmpHeight) return;

            _bitmap.Lock();
            unsafe
            {
                IntPtr pBackBuffer = _bitmap.BackBuffer;
                int stride = _bitmap.BackBufferStride;
                byte* p = (byte*)pBackBuffer + y * stride + x * 4;
                p[0] = color.B;
                p[1] = color.G;
                p[2] = color.R;
                p[3] = color.A;
            }
            _bitmap.AddDirtyRect(new Int32Rect(x, y, 1, 1));
            _bitmap.Unlock();
        }
        private void UpdateSliderBarValue(double newScale)
        {
            ZoomSlider.Value = newScale;
            ZoomLevel = newScale.ToString("P0");
            ZoomMenu.Text = newScale.ToString("P0");
        }
    }
}