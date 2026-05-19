using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Security.Cryptography;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace TabPaint.Controls
{
    public partial class FavoriteBarControl : UserControl
    {
        public event EventHandler CloseRequested;
        public event Action<string> ImageSelected;
        public event EventHandler FavoritesChanged;

        private string _currentPage = "Default";
        private const string ThumbnailDirName = ".thumbnails";
        private const string OrderFileName = ".order";
        private const int FavoriteThumbnailSize = 150;
        private const int WM_MOUSEHWHEEL = AppConsts.WM_MOUSEHWHEEL;
        private Point? _dragStartPoint = null;

        private bool _isLoading = false;

        private StackPanel GetFavoriteStack()
        {
            return this.FindName("FavoriteStackPanel") as StackPanel;
        }

        public FavoriteBarControl()
        {
            InitializeComponent();
            
            this.Loaded += (s, e) => {
                if (!string.IsNullOrEmpty(_currentPage))
                    LoadFavorites(_currentPage);
            };

            this.Loaded += FavoriteBarControl_Loaded;
            this.Unloaded += FavoriteBarControl_Unloaded;
            this.KeyDown += FavoriteBarControl_KeyDown;
            this.Focusable = true;
            this.AllowDrop = true;
        }

        private void FavoriteBarControl_Loaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
                source?.AddHook(WndProc);
            }
        }

        private void FavoriteBarControl_Unloaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                IntPtr handle = new WindowInteropHelper(window).Handle;
                if (handle != IntPtr.Zero)
                {
                    var source = HwndSource.FromHwnd(handle);
                    source?.RemoveHook(WndProc);
                }
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEHWHEEL)
            {
                var scroller = this.FindName("FavoriteScroller") as ScrollViewer;
                if (scroller != null && IsMouseOverControl(scroller))
                {
                    short tilt = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                    if (tilt != 0)
                    {
                        double scrollAmount = tilt * AppConsts.WheelScrollFactor;
                        scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset + scrollAmount);
                        handled = true;
                    }
                }
            }
            return IntPtr.Zero;
        }

        private bool IsMouseOverControl(UIElement control)
        {
            if (control == null || !control.IsVisible) return false;
            var mousePos = Mouse.GetPosition(control);
            var bounds = new Rect(0, 0, control.RenderSize.Width, control.RenderSize.Height);
            return bounds.Contains(mousePos);
        }

        private void ScrollViewer_ManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }

        private string ComputeFileHash(string filePath)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private string ComputeBitmapHash(BitmapSource bitmap)
        {
            using (var md5 = MD5.Create())
            using (var ms = new MemoryStream())
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(ms);
                ms.Position = 0;
                var hash = md5.ComputeHash(ms);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
        private bool IsDuplicateInPage(string sourceFilePath)
        {
            string pagePath = Path.Combine(AppConsts.FavoriteDir, _currentPage);
            if (!Directory.Exists(pagePath)) return false;

            string sourceHash = ComputeFileHash(sourceFilePath);

            var existingFiles = Directory.GetFiles(pagePath)
                                        .Where(f => AppConsts.IsSupportedImage(f));

            foreach (var existing in existingFiles)
            {
                try
                {
                    if (ComputeFileHash(existing) == sourceHash)
                        return true;
                }
                catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
            }
            return false;
        }

        private bool IsDuplicateBitmapInPage(BitmapSource bitmap)
        {
            string pagePath = Path.Combine(AppConsts.FavoriteDir, _currentPage);
            if (!Directory.Exists(pagePath)) return false;

            string sourceHash = ComputeBitmapHash(bitmap);

            var existingFiles = Directory.GetFiles(pagePath)
                                        .Where(f => AppConsts.IsSupportedImage(f));

            foreach (var existing in existingFiles)
            {
                try
                {
                    if (ComputeFileHash(existing) == sourceHash)
                        return true;
                }
                catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
            }
            return false;
        }

        private void FavoriteBarControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                var dataObj = ClipboardHelper.GetDataObjectWithRetry();
                if (dataObj == null) return;

                if (dataObj.GetDataPresent(DataFormats.Bitmap))
                {
                    var bitmap = dataObj.GetData(DataFormats.Bitmap) as BitmapSource;
                    if (bitmap != null) SaveAndAddImage(bitmap);
                }
                else if (dataObj.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = dataObj.GetData(DataFormats.FileDrop) as string[];
                    if (files != null)
                    {
                        foreach (var file in files)
                        {
                            AddFavoriteFile(file);
                        }
                    }
                }
            }
        }

        public void LoadFavorites(string pageName = "Default")
        {
            _currentPage = pageName;
            var stack = GetFavoriteStack();
            if (stack == null) return;

            _isLoading = true;
            try
            {
                stack.Children.Clear();

                string pagePath = Path.Combine(AppConsts.FavoriteDir, _currentPage);
                if (!Directory.Exists(pagePath))
                {
                    Directory.CreateDirectory(pagePath);
                }

                var allFiles = Directory.GetFiles(pagePath)
                                        .Where(f => AppConsts.IsSupportedImage(f))
                                        .ToList();

                // 加载自定义排序
                var orderedFiles = LoadOrderList(pagePath, allFiles);

                foreach (var file in orderedFiles)
                {
                    AddImageToUI(file);
                }

                AddPlusButton();
            }
            finally
            {
                _isLoading = false;
            }
        }

        private List<string> LoadOrderList(string pagePath, List<string> allFiles)
        {
            string orderFilePath = Path.Combine(pagePath, OrderFileName);
            List<string> result = new List<string>();

            if (File.Exists(orderFilePath))
            {
                try
                {
                    var lines = File.ReadAllLines(orderFilePath);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        string fullPath = Path.Combine(pagePath, line.Trim());
                        if (allFiles.Contains(fullPath))
                        {
                            result.Add(fullPath);
                            allFiles.Remove(fullPath);
                        }
                    }
                }
                catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
            }
            var remaining = allFiles.OrderByDescending(f => File.GetCreationTime(f)).ToList();
            result.AddRange(remaining);

            return result;
        }

        private void SaveOrder()
        {
            if (_isLoading) return;

            var stack = GetFavoriteStack();
            if (stack == null || string.IsNullOrEmpty(_currentPage)) return;

            string pagePath = Path.Combine(AppConsts.FavoriteDir, _currentPage);
            if (!Directory.Exists(pagePath)) return;

            string orderFilePath = Path.Combine(pagePath, OrderFileName);
            try
            {
                var files = new List<string>();
                foreach (var child in stack.Children)
                {
                    if (child is Grid grid && grid.Tag is string filePath)
                    {
                        files.Add(Path.GetFileName(filePath));
                    }
                }
                File.WriteAllLines(orderFilePath, files);
            }
            catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
        }

        private void AddPlusButton()
        {
            var stack = GetFavoriteStack();
            if (stack == null) return;

            var grid = new Grid
            {
                Width = 100,
                Height = 100,
                Margin = new Thickness(6),
                ToolTip = FindResource("L_Menu_File_Add"),
                Style = (Style)FindResource("PlusButtonStyle")  // ★ 应用 + 按钮动画样式
            };

            var rect = new System.Windows.Shapes.Rectangle
            {
                Stroke = (Brush)FindResource("BorderBrush"),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                RadiusX = 6,
                RadiusY = 6,
                Fill = (Brush)FindResource("GlassBackgroundLowBrush"),
                SnapsToDevicePixels = true
            };
            grid.Children.Add(rect);

            var iconPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M11,19V13H5V11H11V5H13V11H19V13H13V19H11Z"),
                Fill = (Brush)FindResource("IconFillBrush"),
                Width = 24,
                Height = 24,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(iconPath);

            grid.Cursor = Cursors.Hand;
            grid.MouseLeftButtonDown += (s, e) =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = string.Format(AppConsts.ImageFilterFormat, "Image Files", "PNG", "JPG", "WEBP", "BMP", "GIF", "TIF", "ICO", "SVG"),
                    Multiselect = true
                };
                if (dialog.ShowDialog() == true)
                {
                    foreach (var file in dialog.FileNames)
                    {
                        AddFavoriteFile(file);
                    }
                }
            };

            stack.Children.Add(grid);
        }


        private string GetThumbnailPath(string originalPath)
        {
            string dir = Path.GetDirectoryName(originalPath);
            string fileName = Path.GetFileName(originalPath);
            string thumbDir = Path.Combine(dir, ThumbnailDirName);
            return Path.Combine(thumbDir, fileName);
        }

        private string EnsureThumbnail(string originalPath)
        {
            try
            {
                string thumbPath = GetThumbnailPath(originalPath);
                if (File.Exists(thumbPath)) return thumbPath;

                string thumbDir = Path.GetDirectoryName(thumbPath);
                if (!Directory.Exists(thumbDir))
                {
                    var di = Directory.CreateDirectory(thumbDir);
                    di.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(originalPath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                double scale = Math.Min((double)FavoriteThumbnailSize / bitmap.PixelWidth, (double)FavoriteThumbnailSize / bitmap.PixelHeight);
                if (scale >= 1)
                {
                    File.Copy(originalPath, thumbPath, true);
                    return thumbPath;
                }

                var resized = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
                using (var fs = new FileStream(thumbPath, FileMode.Create))
                {
                    BitmapEncoder encoder;
                    string ext = Path.GetExtension(originalPath).ToLower();
                    if (ext == ".png" || ext == ".ico" || ext == ".svg") encoder = new PngBitmapEncoder();
                    else encoder = new JpegBitmapEncoder { QualityLevel = 80 };

                    encoder.Frames.Add(BitmapFrame.Create(resized));
                    encoder.Save(fs);
                }
                return thumbPath;
            }
            catch
            {
                return originalPath;
            }
        }

        private void AddImageToUI(string filePath)
        {
            var stack = GetFavoriteStack();
            if (stack == null) return;

            var grid = new Grid
            {
                Width = 100,
                Height = 100,
                Margin = new Thickness(6),
                Tag = filePath,
                Style = (Style)FindResource("FavoriteItemStyle") 
            };

            var border = new Border
            {
                BorderBrush = (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                ToolTip = Path.GetFileName(filePath),
                Background = (Brush)FindResource("GlassBackgroundLowBrush"),
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 10, ShadowDepth = 0, Opacity = 0.1 }
            };

            var img = new Image
            {
                Stretch = Stretch.Uniform,
                Margin = new Thickness(4)
            };

            try
            {
                string displayPath = EnsureThumbnail(filePath);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(displayPath);
                bitmap.DecodePixelWidth = FavoriteThumbnailSize;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                img.Source = bitmap;
            }
            catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }

            Action doDelete = () =>
            {
                var removeStoryboard = new System.Windows.Media.Animation.Storyboard();

                var scaleXAnim = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 0,
                    Duration = AnimationHelper.GetScaledTimeSpan(200),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
                };
                System.Windows.Media.Animation.Storyboard.SetTarget(scaleXAnim, grid);
                System.Windows.Media.Animation.Storyboard.SetTargetProperty(scaleXAnim,
                    new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));

                var scaleYAnim = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 0,
                    Duration = AnimationHelper.GetScaledTimeSpan(200),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
                };
                System.Windows.Media.Animation.Storyboard.SetTarget(scaleYAnim, grid);
                System.Windows.Media.Animation.Storyboard.SetTargetProperty(scaleYAnim,
                    new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));

                var opacityAnim = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 0,
                    Duration = AnimationHelper.GetScaledTimeSpan(200)
                };
                System.Windows.Media.Animation.Storyboard.SetTarget(opacityAnim, grid);
                System.Windows.Media.Animation.Storyboard.SetTargetProperty(opacityAnim,
                    new PropertyPath("Opacity"));

                removeStoryboard.Children.Add(scaleXAnim);
                removeStoryboard.Children.Add(scaleYAnim);
                removeStoryboard.Children.Add(opacityAnim);

                removeStoryboard.Completed += (s2, e2) =>
                {
                    try
                    {
                        File.Delete(filePath);
                        string thumbPath = GetThumbnailPath(filePath);
                        if (File.Exists(thumbPath)) File.Delete(thumbPath);
                        stack.Children.Remove(grid);
                        SaveOrder();
                        FavoritesChanged?.Invoke(this, EventArgs.Empty);
                    }
                    catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
                };

                removeStoryboard.Begin();
            };

            grid.MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Middle)
                {
                    doDelete();
                    e.Handled = true;
                }
            };

            border.Child = img;
            grid.Children.Add(border);
            var deleteBtn = new Button
            {
                Width = 20,
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 2, 0),
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Padding = new Thickness(0),
                Opacity = 0,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(0.7, 0.7)
            };
            try { deleteBtn.Style = (Style)FindResource("OtherCloseButtonStyle"); } catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }

            grid.Children.Add(deleteBtn);
            grid.MouseEnter += (s, e) =>
            {
                border.BorderBrush = (Brush)FindResource("SystemAccentHoverBrush");
                border.BorderThickness = new Thickness(1);
            };

            grid.MouseLeave += (s, e) =>
            {
                border.BorderBrush = (Brush)FindResource("BorderBrush");
                border.BorderThickness = new Thickness(1);
            };

            deleteBtn.Click += (s, e) =>
            {
                doDelete();
                e.Handled = true;
            };

            border.MouseMove += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed && _dragStartPoint.HasValue)
                {
                    Point current = e.GetPosition(border);
                    Vector diff = current - _dragStartPoint.Value;

                    if (Math.Abs(diff.X) > 20  ||
                        Math.Abs(diff.Y) > 20)
                    {
                        _dragStartPoint = null;
                        DataObject data = new DataObject();
                        data.SetData(DataFormats.FileDrop, new string[] { filePath });
                        var fileList = new System.Collections.Specialized.StringCollection { filePath };
                        data.SetFileDropList(fileList);
                        data.SetData("FavoriteItemReorder", filePath);
                        DragDrop.DoDragDrop(border, data, DragDropEffects.Copy | DragDropEffects.Move);
                    }
                }
            };
            border.MouseLeftButtonUp += (s, e) =>    {  _dragStartPoint = null; };
            border.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    ImageSelected?.Invoke(filePath);
                    _dragStartPoint = null;
                    return;
                }
                _dragStartPoint = e.GetPosition(border);
            };

            var menu = new ContextMenu { Style = (Style)FindResource("Win11ContextMenuStyle") };
            var openItem = new MenuItem { Header = FindResource("L_Menu_File_Open"), Style = (Style)FindResource("Win11MenuItemStyle") };
            openItem.Click += (s, e) => ImageSelected?.Invoke(filePath);

            var deleteItem = new MenuItem { Header = FindResource("L_Ctx_DeleteFile"), Style = (Style)FindResource("Win11MenuItemStyle"), Foreground = Brushes.Red };
            deleteItem.Click += (s, e) =>
            {
                doDelete();
            };

            var explorerItem = new MenuItem { Header = FindResource("L_Menu_File_OpenFolder"), Style = (Style)FindResource("Win11MenuItemStyle") };
            explorerItem.Click += (s, e) =>
            {
                try
                {
                    string argument = "/select,\"" + filePath + "\"";
                    System.Diagnostics.Process.Start("explorer.exe", argument);
                }
                catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
            };

            menu.Items.Add(openItem);
            menu.Items.Add(explorerItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(deleteItem);
            border.ContextMenu = menu;

            var plusBtn = stack.Children.Cast<FrameworkElement>().FirstOrDefault(c => c.Style == FindResource("PlusButtonStyle"));
            if (plusBtn != null)  stack.Children.Insert(stack.Children.IndexOf(plusBtn), grid);
            else stack.Children.Add(grid);
            SaveOrder();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) { CloseRequested?.Invoke(this, EventArgs.Empty); }

        private void OnWrapPanelDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("FavoriteItemReorder"))
            {
                e.Effects = DragDropEffects.Move;
                
                // 实时排序预览
                var stack = GetFavoriteStack();
                if (stack != null)
                {
                    Point pos = e.GetPosition(stack);
                    int index = CalculateInsertionIndex(stack, pos);
                    string draggedPath = (string)e.Data.GetData("FavoriteItemReorder");
                    
                    MoveItemPreview(stack, draggedPath, index);
                }
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else    e.Effects = DragDropEffects.None;
            
            e.Handled = true;
        }

        private int CalculateInsertionIndex(StackPanel stack, Point pos)
        {
            int index = 0;
            var plusStyle = FindResource("PlusButtonStyle") as Style;
            foreach (FrameworkElement child in stack.Children)
            {
                if (child.Style == plusStyle) break;
                
                Point childPos = child.TranslatePoint(new Point(0, 0), stack);
                if (pos.X < childPos.X + child.ActualWidth / 2) return index;
                index++;
            }
            return Math.Max(0, index); 
        }

        private void MoveItemPreview(StackPanel stack, string draggedPath, int targetIndex)
        {
            Grid draggedGrid = null;
            int currentIndex = -1;
            
            for (int i = 0; i < stack.Children.Count; i++)
            {
                if (stack.Children[i] is Grid g && g.Tag as string == draggedPath)
                {
                    draggedGrid = g;
                    currentIndex = i;
                    break;
                }
            }

            if (draggedGrid != null && currentIndex != targetIndex)
            {
                stack.Children.RemoveAt(currentIndex);
                // 确保不会插入到 PlusButton 之后
                int plusIndex = -1;
                var plusStyle = FindResource("PlusButtonStyle") as Style;
                for (int i = 0; i < stack.Children.Count; i++)
                {
                    if ((stack.Children[i] as FrameworkElement)?.Style == plusStyle){  plusIndex = i;  break; }
                }

                if (plusIndex != -1 && targetIndex > plusIndex) targetIndex = plusIndex;
                if (targetIndex >= stack.Children.Count)
                    stack.Children.Add(draggedGrid);
                else
                    stack.Children.Insert(targetIndex, draggedGrid);
            }
        }

        private void OnWrapPanelDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("FavoriteItemReorder"))
            {
                SaveOrder();
                e.Effects = DragDropEffects.Move;
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var file in files)
                {
                    AddFavoriteFile(file);
                }
            }
            e.Handled = true;
        }

        private void AddFavoriteFile(string filePath)
        {
            if (!AppConsts.IsSupportedImage(filePath)) return;
            try
            {
                if (IsDuplicateInPage(filePath)) return;

                string fileName = Path.GetFileName(filePath);
                string pagePath = Path.Combine(AppConsts.FavoriteDir, _currentPage);
                string destPath = Path.Combine(pagePath, fileName);

                int i = 1;
                while (File.Exists(destPath))
                {
                    destPath = Path.Combine(pagePath, Path.GetFileNameWithoutExtension(fileName) + $"_{i++}" + Path.GetExtension(fileName));
                }
                File.Copy(filePath, destPath);
                EnsureThumbnail(destPath);
                AddImageToUI(destPath);
                FavoritesChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer == null) return;

            if (e.Delta != 0)
            {
                double scrollAmount = e.Delta * AppConsts.WheelScrollFactor;
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - scrollAmount);
                e.Handled = true;
            }
        }

        private void SaveAndAddImage(BitmapSource bitmap)
        {
            try
            {
                if (IsDuplicateBitmapInPage(bitmap)) return;

                string fileName = string.Format("Pasted_{0:yyyyMMdd_HHmmss}.png", DateTime.Now);
                string pagePath = Path.Combine(AppConsts.FavoriteDir, _currentPage);
                string destPath = Path.Combine(pagePath, fileName);

                using (var fileStream = new FileStream(destPath, FileMode.Create))
                {
                    BitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(fileStream);
                }
                EnsureThumbnail(destPath);
                AddImageToUI(destPath);
                FavoritesChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
        }
    }
}
