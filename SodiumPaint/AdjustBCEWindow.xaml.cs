
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;


namespace SodiumPaint
{
	public partial class AdjustBCEWindow : Window
	{
		private WriteableBitmap _originalBitmap;
		private WriteableBitmap _previewBitmap;
		private Image _targetImage; // 主窗口的图片控件引用

		public double Brightness => BrightnessSlider.Value;
		public double Contrast => ContrastSlider.Value;
		public double Exposure => ExposureSlider.Value;

		public AdjustBCEWindow(WriteableBitmap bitmap, Image targetImage)
		{
			InitializeComponent();
			_originalBitmap = bitmap.Clone(); // 保存原始
			_targetImage = targetImage;
			_previewBitmap = bitmap;
		}

		private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			ApplyPreview();
		}

		private void ApplyPreview()
		{
			// 复制原图再做预览处理
			var temp = _originalBitmap.Clone();

			// 调整亮度/对比度/曝光
			AdjustImage(temp, Brightness, Contrast, Exposure);

			// 显示到目标 Image
			_targetImage.Source = temp;
			_previewBitmap = temp;
		}

		private void Ok_Click(object sender, RoutedEventArgs e)
		{
			// 应用最终调整到原图
			AdjustImage(_previewBitmap, Brightness, Contrast, Exposure);
			_targetImage.Source = _previewBitmap;
			DialogResult = true;
			Close();
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			// 恢复原图
			_targetImage.Source = _originalBitmap;
			DialogResult = false;
			Close();
		}

		/// <summary>
		/// 核心调整函数：亮度、对比度、曝光
		/// </summary>
		private void AdjustImage(WriteableBitmap bmp, double brightness, double contrast, double exposure)
		{
			bmp.Lock();
			unsafe
			{
				int stride = bmp.BackBufferStride;
				byte* basePtr = (byte*)bmp.BackBuffer;
				for (int y = 0; y < bmp.PixelHeight; y++)
				{
					byte* row = basePtr + y * stride;
					for (int x = 0; x < bmp.PixelWidth; x++)
					{
						byte b = row[x * 4];
						byte g = row[x * 4 + 1];
						byte r = row[x * 4 + 2];

						// brightness [-100, 100]: 转 0-255
						double brAdj = brightness / 100.0 * 255.0;

						// contrast [-100, 100] -> 系数
						double ctAdj = (100.0 + contrast) / 100.0;
						ctAdj *= ctAdj;

						// 曝光 [-2, 2] 系数
						double expAdj = Math.Pow(2, exposure);

						// 先加亮度
						double nr = r + brAdj;
						double ng = g + brAdj;
						double nb = b + brAdj;

						// 再调对比度
						nr = ((((nr / 255.0) - 0.5) * ctAdj) + 0.5) * 255.0;
						ng = ((((ng / 255.0) - 0.5) * ctAdj) + 0.5) * 255.0;
						nb = ((((nb / 255.0) - 0.5) * ctAdj) + 0.5) * 255.0;

						// 最后调整曝光
						nr *= expAdj;
						ng *= expAdj;
						nb *= expAdj;

						// 限制范围
						row[x * 4 + 2] = (byte)Math.Max(0, Math.Min(255, nr));
						row[x * 4 + 1] = (byte)Math.Max(0, Math.Min(255, ng));
						row[x * 4] = (byte)Math.Max(0, Math.Min(255, nb));
					}
				}
			}
			bmp.AddDirtyRect(new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight));
			bmp.Unlock();
		}
	}
}