using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//namespace SodiumPaint.Resources
//{
//    class deprecated_functions
//    {
//    }
//}


// Note: This file is intentionally left empty as a placeholder for deprecated functions.



//public void SetZoomAndOffset(double scaleFactor, double offsetX, double offsetY)
//{
//    double oldScale = zoomscale;
//    zoomscale = scaleFactor;
//    UpdateSliderBarValue(zoomscale);
//    // 更新缩放变换
//    ZoomTransform.ScaleX = ZoomTransform.ScaleY = scaleFactor;

//    // 计算新的滚动偏移
//    // 偏移要按当前比例转换：ScrollViewer 的偏移单位是可视区域像素，而非原图像素。
//    double newOffsetX = offsetX * scaleFactor;
//    double newOffsetY = offsetY * scaleFactor;

//    // 应用到滚动容器
//    ScrollContainer.ScrollToHorizontalOffset(newOffsetX);
//    ScrollContainer.ScrollToVerticalOffset(newOffsetY);
//}