

//private async void OnCanvasDrop(object sender, System.Windows.DragEventArgs e)
//{
//    HideDragOverlay();
//    if (e.Data.GetDataPresent("TabPaintInternalDrag"))
//    {
//        e.Handled = true;
//        return;
//    }

//    if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
//    {
//        string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
//        if (files != null && files.Length > 0)
//        {
//            // --- 核心修改：逻辑分流 ---
//            if (files.Length > 1)
//            {
//                // 如果是多文件，走新建标签页逻辑
//                await OpenFilesAsNewTabs(files);
//            }
//            else
//            {
//                // 如果是单文件，走原有的“插入当前画布”逻辑
//                string filePath = files[0];
//                try
//                {
//                    BitmapImage bitmap = new BitmapImage();
//                    bitmap.BeginInit();
//                    bitmap.UriSource = new Uri(filePath);
//                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
//                    bitmap.EndInit();
//                    bitmap.Freeze(); // 保持你的建议，加上 Freeze

//                    _router.SetTool(_tools.Select);

//                    if (_tools.Select is SelectTool st)
//                    {
//                        st.InsertImageAsSelection(_ctx, bitmap);
//                    }
//                }
//                catch (Exception ex)
//                {
//                    System.Windows.MessageBox.Show("无法识别的图片格式: " + ex.Message);
//                }
//            }
//            e.Handled = true;
//        }
//    }
//}

//using System.Diagnostics;

//private async Task LoadAndDisplayImageInternalAsync(string filePath)
//{
//    try
//    {
//        OpenImageAndTabs(filePath);
//        //int newIndex = _imageFiles.IndexOf(filePath);
//        //if (newIndex < 0) return;
//        //_currentImageIndex = newIndex;

//        //foreach (var tab in FileTabs)
//        //    tab.IsSelected = false;

//        //FileTabItem current = FileTabs.FirstOrDefault(t => t.FilePath == filePath);
//        //current.IsSelected=true;
//        //// 3. 加载主图片
//        //await LoadImage(filePath); // 假设这是您加载大图的方法

//        //await RefreshTabPageAsync(_currentImageIndex);
//        //_currentTabItem = current;
//        ////a.s(_currentTabItem.FilePath);
//        //SetPreviewSlider(); 
//        //UpdateWindowTitle();
//    }
//    catch (Exception ex)
//    {
//        // 最好有异常处理
//        Debug.WriteLine($"Error loading image {filePath}: {ex.Message}");
//    }
//}



//using System.Globalization;
//using System.Windows.Data;

//namespace TabPaint.Converters
//{
//    // 将 Slider 的线性刻度转换为指数级增长的实际缩放值
//    public class LogarithmicScaleConverter : IValueConverter
//    {
//        // 目标真实倍率范围
//        private const double RealMin = 0.1;  // 10%
//        private const double RealMax = 16.0; // 1600%

//        // Slider 控件的逻辑范围 (XAML里要对应设置 Minimum=0 Maximum=100)
//        private const double SliderMin = 0.0;
//        private const double SliderMax = 100.0;

//        // 计算常数
//        private static readonly double LogRealMin = Math.Log(RealMin);
//        private static readonly double LogRealRange = Math.Log(RealMax) - Math.Log(RealMin);
//        private static readonly double SliderRange = SliderMax - SliderMin;

//        /// <summary>
//        /// ViewModel (真实倍率) -> View (Slider位置 0-100)
//        /// </summary>
//        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
//        {
//            if (value is double realZoom)
//            {
//                // 边界保护
//                if (realZoom <= RealMin) return SliderMin;
//                if (realZoom >= RealMax) return SliderMax;

//                // 公式: slider = (log(zoom) - log(min)) / (log(max) - log(min)) * 100
//                double sliderVal = ((Math.Log(realZoom) - LogRealMin) / LogRealRange) * SliderRange + SliderMin;
//                return sliderVal;
//            }
//            return SliderMin;
//        }

//        /// <summary>
//        /// View (Slider位置 0-100) -> ViewModel (真实倍率)
//        /// </summary>
//        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
//        {
//            if (value is double sliderVal)
//            {
//                // 边界保护
//                if (sliderVal <= SliderMin) return RealMin;
//                if (sliderVal >= SliderMax) return RealMax;

//                // 公式: zoom = exp( (slider/100) * (log(max)-log(min)) + log(min) )
//                double relativePos = (sliderVal - SliderMin) / SliderRange;
//                double realZoom = Math.Exp(relativePos * LogRealRange + LogRealMin);

//                return Math.Round(realZoom, 2); // 保留两位小数
//            }
//            return RealMin;
//        }
//    }
//}

//if (!tab.IsNew &&
//    !string.IsNullOrEmpty(_currentFilePath) &&
//    tab.FilePath == _currentFilePath)
//{
//    return;
//}
// s(1);



// 1. 清理底层画布
//Clean_bitmap(1200, 900);

//// 2. 重置窗口标题
//_currentFilePath = string.Empty;
//_currentFileName = "未命名";
//UpdateWindowTitle();

//var newTab = CreateNewUntitledTab();
//newTab.IsSelected = true; // 设为选中态
//FileTabs.Add(newTab);
//_currentTabItem = newTab;

//// 5. 重置撤销栈和脏状态追踪
//ResetDirtyTracker();

//// 6. 滚动视图归位
//MainImageBar.Scroller.ScrollToHorizontalOffset(0);