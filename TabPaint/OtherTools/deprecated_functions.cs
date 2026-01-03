

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
//< DrawingImage x: Key = "rotate-left-svgrepo-com" >
//    < DrawingImage.Drawing >
//        < GeometryDrawing Brush = "#FF000000" >
//            < GeometryDrawing.Geometry >
//                < PathGeometry FillRule = "Nonzero" Figures = "M213.3330078125,746.6669921875C154.42300415039062,746.6669921875,106.66699981689453,794.4229736328125,106.66699981689453,853.3330078125L106.66699981689453,1706.6700439453125C106.66699981689453,1765.5799560546875,154.42300415039062,1813.3299560546875,213.3330078125,1813.3299560546875L1066.6700439453125,1813.3299560546875C1125.5799560546875,1813.3299560546875,1173.3299560546875,1765.5799560546875,1173.3299560546875,1706.6700439453125L1173.3299560546875,853.3330078125C1173.3299560546875,794.4229736328125,1125.5799560546875,746.6669921875,1066.6700439453125,746.6669921875L213.3330078125,746.6669921875z M213.3330078125,640L1066.6700439453125,640C1184.489990234375,640,1280,735.5130004882812,1280,853.3330078125L1280,1706.6700439453125C1280,1824.489990234375,1184.489990234375,1920,1066.6700439453125,1920L213.3330078125,1920C95.51300048828125,1920,0,1824.489990234375,-5.7220458984375E-06,1706.6700439453125L-5.7220458984375E-06,853.3330078125C0,735.5130004882812,95.51300048828125,640,213.3330078125,640z M1178.93994140625,0L1254.3699951171875,75.42500305175781 1120.030029296875,209.7659912109375 1280,209.7659912109375C1515.6400146484375,209.76600646972656,1706.6700439453125,400.7909851074219,1706.6700439453125,636.4320068359375L1600,636.4320068359375C1600,459.70098876953125,1456.72998046875,316.4320068359375,1280,316.4320068359375L1120,316.4320068359375 1251.449951171875,447.88299560546875 1176.030029296875,523.3070068359375 915.8330078125,263.1109924316406 1178.93994140625,0z" />
//            </ GeometryDrawing.Geometry >
//        </ GeometryDrawing >
//    </ DrawingImage.Drawing >
//</ DrawingImage >

//var frozenDrawingImage = (DrawingImage)image.Data; // 获取当前 UI 使用的绘图对象
//if (frozenDrawingImage == null) return;
//var modifiableDrawingImage = frozenDrawingImage.Clone();    // 克隆出可修改的副本
//if (modifiableDrawingImage.Drawing is GeometryDrawing geoDrawing)  // DrawingImage.Drawing 可能是 DrawingGroup 或 GeometryDrawing
//{
//    geoDrawing.Brush = isEnabled ? Brushes.Black : Brushes.Gray;
//}
//else if (modifiableDrawingImage.Drawing is DrawingGroup group)
//{
//    foreach (var child in group.Children)
//    {
//        if (child is GeometryDrawing childGeo)
//        {
//            childGeo.Brush = isEnabled ? Brushes.Black : Brushes.Gray;
//        }
//    }
//}

//var frozenDrawingImage = (DrawingImage)image.Data; // 获取当前 UI 使用的绘图对象
//if (frozenDrawingImage == null) return;
//var modifiableDrawingImage = frozenDrawingImage.Clone();    // 克隆出可修改的副本
//if (modifiableDrawingImage.Drawing is GeometryDrawing geoDrawing)  // DrawingImage.Drawing 可能是 DrawingGroup 或 GeometryDrawing
//{
//    geoDrawing.Brush = isEnabled ? Brushes.Black : Brushes.Gray;
//}
//else if (modifiableDrawingImage.Drawing is DrawingGroup group)
//{
//    foreach (var child in group.Children)
//    {
//        if (child is GeometryDrawing childGeo)
//        {
//            childGeo.Brush = isEnabled ? Brushes.Black : Brushes.Gray;
//        }
//    }
//}

//// 替换 Image.Source，让 UI 用新的对象
//image.Data = modifiableDrawingImage;

//using System.Windows;
//using TabPaint;
//using static TabPaint.MainWindow;

//private void SetPenResizeBarVisibility()
//{
//    if (((_router.CurrentTool is PenTool && _ctx.PenStyle != BrushStyle.Pencil) || _router.CurrentTool is ShapeTool) && !IsViewMode)
//        ((MainWindow)System.Windows.Application.Current.MainWindow).ThicknessPanel.Visibility = Visibility.Visible;
//    else
//    {
//        ((MainWindow)System.Windows.Application.Current.MainWindow).ThicknessPanel.Visibility = Visibility.Collapsed;

//    }

//}
//private void SetOpacityBarVisibility()
//{
//    if ((_router.CurrentTool is PenTool || _router.CurrentTool is TextTool || _router.CurrentTool is PenTool || _router.CurrentTool is ShapeTool) && !IsViewMode)
//        ((MainWindow)System.Windows.Application.Current.MainWindow).OpacityPanel.Visibility = Visibility.Visible;
//    else

//    {
//        ((MainWindow)System.Windows.Application.Current.MainWindow).OpacityPanel.Visibility = Visibility.Collapsed;
//    }

//}
//< DrawingImage x: Key = "Screenshot_Image" >
//    < DrawingImage.Drawing >
//        < GeometryDrawing Brush = "#FF000000" >
//            < GeometryDrawing.Geometry >
//                < PathGeometry FillRule = "Nonzero" Figures = "M209.00267028808594,275.3066711425781L149.33267211914062,339.0886535644531 149.3333282470703,362.6666564941406 362.6666564941406,362.6666564941406 362.6666564941406,351.39166259765625 317.0986633300781,305.83465576171875 278.3260803222656,344.62628173828125 209.00267028808594,275.3066711425781z M288,170.6666717529297C305.673095703125,170.6666717529297 320,184.99356079101562 320,202.6666717529297 320,220.33978271484375 305.673095703125,234.6666717529297 288,234.6666717529297 270.3268737792969,234.6666717529297 256,220.33978271484375 256,202.6666717529297 256,184.99356079101562 270.3268737792969,170.6666717529297 288,170.6666717529297z M149.3333282470703,149.3333282470703L149.33267211914062,276.6486511230469 207.9995574951172,213.9600067138672 278.3146667480469,284.2879943847656 317.11395263671875,245.49864196777344 362.6666564941406,291.0516662597656 362.6666564941406,149.3333282470703 149.3333282470703,149.3333282470703z M106.66667175292969,42.66666793823242L149.3333282470703,42.66666793823242 149.3333282470703,106.66667175292969 405.33331298828125,106.66667175292969 405.33331298828125,362.6666564941406 469.33331298828125,362.6666564941406 469.33331298828125,405.33331298828125 405.33331298828125,405.33331298828125 405.33331298828125,469.33331298828125 362.6666564941406,469.33331298828125 362.6666564941406,405.33331298828125 106.66667175292969,405.33331298828125 106.66667175292969,149.3333282470703 42.66666793823242,149.3333282470703 42.66666793823242,106.66667175292969 106.66667175292969,106.66667175292969 106.66667175292969,42.66666793823242z" />
//            </ GeometryDrawing.Geometry >
//        </ GeometryDrawing >
//    </ DrawingImage.Drawing >
//</ DrawingImage >

 //< DrawingImage x: Key = "Brush_Round_Image" >
 //       < DrawingImage.Drawing >
 //           < DrawingGroup >
 //               < DrawingGroup.Children >
 //                   < GeometryDrawing Brush = "#FF000000" >
 //                       < GeometryDrawing.Geometry >
 //                           < PathGeometry FillRule = "Nonzero" Figures = "M229.806,376.797L171.64100000000002,335.821 170.51300000000003,335.709C143.62400000000002,332.968 117.26600000000003,344.957 101.72300000000003,367.019 86.98000000000003,387.947 81.62200000000003,410.762 76.44100000000003,432.831 72.91300000000003,447.89500000000004 69.26000000000003,463.471 62.63600000000003,478.444 57.153000000000034,490.826 53.48000000000003,495.24600000000004 53.46700000000003,495.266L49.68300000000003,499.54900000000004 54.83100000000003,502.028C78.78900000000003,513.57 111.14100000000003,515.171 143.59700000000004,506.422 177.68700000000004,497.24 206.23600000000005,478.31300000000005 223.96900000000005,453.14200000000005 239.51200000000006,431.08000000000004 241.93200000000004,402.22300000000007 230.29100000000005,377.826L229.806,376.797z M208.721,442.4C204.55,448.315 199.573,453.883 193.92600000000002,459.005 193.03400000000002,459.602 192.116,460.264 191.15200000000002,461.012 180.495,469.394 166.604,465.787 175.05100000000002,448.788 183.49800000000002,431.776 168.61100000000002,426.332 156.51700000000002,437.502 141.342,451.524 134.21900000000002,440.32800000000003 137.026,432.589 139.82600000000002,424.851 149.907,414.298 141.472,407.478 136.39600000000002,403.366 129.844,409.373 119.39000000000001,417.519 113.40200000000002,422.181 99.61700000000002,420.66700000000003 105.20400000000001,399.969 108.227,392.276 111.97200000000001,384.859 116.97000000000001,377.763 127.81700000000001,362.351 146.214,353.301 165.07500000000002,354.022L214.88500000000002,389.109C221.923,406.631,219.575,426.988,208.721,442.4z" />
 //                       </ GeometryDrawing.Geometry >
 //                   </ GeometryDrawing >
 //                   < GeometryDrawing Brush = "#FF000000" >
 //                       < GeometryDrawing.Geometry >
 //                           < PathGeometry FillRule = "Nonzero" Figures = "M191.519,277.032C185.281,284.97499999999997 182.58,295.12699999999995 184.035,305.12199999999996 185.505,315.116 191.007,324.068 199.264,329.88599999999997L226.094,348.78599999999994C234.351,354.60299999999995 244.641,356.77399999999994 254.54399999999998,354.79299999999995 264.447,352.79999999999995 273.09799999999996,346.83099999999996 278.48199999999997,338.29299999999995L302.83899999999994,299.55899999999997 219.79199999999994,241.05299999999997 191.519,277.032z" />
 //                       </ GeometryDrawing.Geometry >
 //                   </ GeometryDrawing >
 //                   < GeometryDrawing Brush = "#FF000000" >
 //                       < GeometryDrawing.Geometry >
 //                           < PathGeometry FillRule = "Nonzero" Figures = "M447.22,6.635L447.016,6.497C431.53200000000004,-4.41,410.22400000000005,-1.2809999999999997,398.524,13.606L229.839,228.265 311.497,285.788 456.847,54.687C466.934,38.658,462.697,17.541,447.22,6.635z" />
 //                       </ GeometryDrawing.Geometry >
 //                   </ GeometryDrawing >
 //               </ DrawingGroup.Children >
 //           </ DrawingGroup >
 //       </ DrawingImage.Drawing >
 //   </ DrawingImage >
