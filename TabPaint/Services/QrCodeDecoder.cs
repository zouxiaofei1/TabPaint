using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;

namespace TabPaint.Services
{
    /// <summary>
    /// 使用 ZXing.Net 实现的二维码/条码识别服务。
    /// 采用反射加载，支持插件式动态加载。
    /// </summary>
    public class QrCodeDecoder
    {
        private static bool? _isAvailable;
        private static Assembly? _zxingAssembly;

        public static bool IsZXingAvailable()
        {
            if (_isAvailable.HasValue) return _isAvailable.Value;
            try
            {
                // 尝试从插件目录加载或已加载的程序集中查找
                string dllPath = Path.Combine(AppConsts.PluginsDir, AppConsts.ZXing_DllName);
                if (File.Exists(dllPath))
                {
                    // 使用 File.ReadAllBytes 读取 DLL 内容，然后通过 Assembly.Load 加载。
                    // 这样可以避免 Assembly.LoadFrom 导致的磁盘文件锁定，从而允许在运行期间卸载或覆盖。
                    byte[] assemblyBytes = File.ReadAllBytes(dllPath);
                    _zxingAssembly = Assembly.Load(assemblyBytes);
                    _isAvailable = _zxingAssembly != null;
                }
                else
                {
                    _isAvailable = false;
                }
            }
            catch
            {
                _isAvailable = false;
            }
            return _isAvailable.Value;
        }

        public static void ResetAvailability()
        {
            _isAvailable = null;
            _zxingAssembly = null;
        }

        public static string Decode(string filePath)
        {
            if (!IsZXingAvailable()) return LocalizationManager.GetString("L_QrCode_Error_NotInstalled");

            BitmapSource? bitmap = LoadBitmap(filePath);
            if (bitmap == null) return LocalizationManager.GetString("L_QrCode_Error_LoadImageFailed");
            return Decode(bitmap);
        }

        public static string Decode(BitmapSource bitmap)
        {
            if (!IsZXingAvailable() || _zxingAssembly == null)
                return LocalizationManager.GetString("L_QrCode_Error_NotInstalled");

            try
            {
                // 使用动态/反射调用以避免编译时硬引用
                Type? readerType = _zxingAssembly.GetType("ZXing.BarcodeReaderGeneric");
                if (readerType == null) return string.Format(LocalizationManager.GetString("L_QrCode_Error_TypeNotFound"), "ZXing.BarcodeReaderGeneric");

                dynamic reader = Activator.CreateInstance(readerType)!;
                reader.AutoRotate = true;

                Type? decodingOptionsType = _zxingAssembly.GetType("ZXing.Common.DecodingOptions");
                if (decodingOptionsType != null)
                {
                    object options = Activator.CreateInstance(decodingOptionsType)!;

                    // 用反射设置 TryHarder = true
                    PropertyInfo? tryHarderProp = decodingOptionsType.GetProperty("TryHarder");
                    tryHarderProp?.SetValue(options, true);

                    Type? barcodeFormatType = _zxingAssembly.GetType("ZXing.BarcodeFormat");
                    if (barcodeFormatType != null)
                    {
                        // 创建 List<BarcodeFormat>
                        Type listType = typeof(List<>).MakeGenericType(barcodeFormatType);
                        object formatList = Activator.CreateInstance(listType)!;

                        // 通过 IList 接口添加元素
                        System.Collections.IList list = (System.Collections.IList)formatList;
                        list.Add(Enum.Parse(barcodeFormatType, "QR_CODE"));
                        list.Add(Enum.Parse(barcodeFormatType, "DATA_MATRIX"));
                        list.Add(Enum.Parse(barcodeFormatType, "AZTEC"));
                        list.Add(Enum.Parse(barcodeFormatType, "PDF_417"));
                        PropertyInfo? possibleFormatsProp = decodingOptionsType.GetProperty("PossibleFormats");
                        possibleFormatsProp?.SetValue(options, formatList);
                    }

                    PropertyInfo? optionsProp = readerType.GetProperty("Options");
                    optionsProp?.SetValue(reader, options);
                }


                var luminanceSourceWrapper = new BitmapSourceLuminanceSource(bitmap, _zxingAssembly);
                var result = reader.Decode(luminanceSourceWrapper.GetInternalSource());

                if (result != null)
                {
                    return result.Text;
                }
                else
                {
                    return LocalizationManager.GetString("L_QrCode_Error_NotFound");
                }
            }
            catch (TargetInvocationException tex)
            {
                return string.Format(LocalizationManager.GetString("L_QrCode_Error_Internal"), tex.InnerException?.Message ?? tex.Message);
            }
            catch (Exception ex)
            {
                return string.Format(LocalizationManager.GetString("L_QrCode_Error_General"), ex.Message);
            }

        }

        private static BitmapSource? LoadBitmap(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// 为 ZXing 提供 WPF BitmapSource 支持的 LuminanceSource 实现包装器。
    /// </summary>
    public class BitmapSourceLuminanceSource
    {
        private readonly dynamic _innerSource;

        public BitmapSourceLuminanceSource(BitmapSource bitmap, Assembly zxingAssembly)
        {
            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];

            if (bitmap.Format != System.Windows.Media.PixelFormats.Bgr32 &&
                bitmap.Format != System.Windows.Media.PixelFormats.Bgra32 &&
                bitmap.Format != System.Windows.Media.PixelFormats.Pbgra32)
            {
                bitmap = new FormatConvertedBitmap(bitmap, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            }

            bitmap.CopyPixels(pixels, stride, 0);

            // ✅ 转换为 RGB 格式（每像素3字节），ZXing.RGBLuminanceSource 期望此格式
            byte[] rgbPixels = new byte[width * height * 3];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int srcOffset = y * stride + x * 4;
                    int dstOffset = (y * width + x) * 3;
                    rgbPixels[dstOffset] = pixels[srcOffset + 2]; // R
                    rgbPixels[dstOffset + 1] = pixels[srcOffset + 1]; // G
                    rgbPixels[dstOffset + 2] = pixels[srcOffset];     // B
                }
            }

            Type? rgbLuminanceSourceType = zxingAssembly.GetType("ZXing.RGBLuminanceSource");
            if (rgbLuminanceSourceType == null)
                throw new Exception(string.Format(LocalizationManager.GetString("L_QrCode_Error_TypeNotFound"), "ZXing.RGBLuminanceSource"));

            // ✅ 构造函数签名: RGBLuminanceSource(byte[] rgbRawBytes, int width, int height)
            _innerSource = Activator.CreateInstance(
                rgbLuminanceSourceType,
                new object[] { rgbPixels, width, height }
            )!;
        }

        public dynamic GetInternalSource() => _innerSource;
    }

}
