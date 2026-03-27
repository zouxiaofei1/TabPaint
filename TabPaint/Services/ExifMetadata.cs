
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

//
//ExifMetadata
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private string BuildBasicMetadataInfo(string filePath, long byteLength, BitmapSource bitmap)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(LocalizationManager.GetString("L_Exif_Section_File"));
            if (IsVirtualPath(filePath)) sb.AppendLine(LocalizationManager.GetString("L_Exif_Path_Memory"));
            else sb.AppendLine(string.Format(LocalizationManager.GetString("L_Exif_Path_Format"), filePath));
            sb.AppendLine(string.Format(LocalizationManager.GetString("L_Exif_Size_MB_Format"), byteLength / 1024.0 / 1024.0));
            sb.AppendLine();

            sb.AppendLine(LocalizationManager.GetString("L_Exif_Section_Canvas"));
            sb.AppendLine(string.Format(LocalizationManager.GetString("L_Exif_Dim_Format"), bitmap.PixelWidth, bitmap.PixelHeight));
            sb.AppendLine(string.Format(LocalizationManager.GetString("L_Exif_BitDepth_Format"), bitmap.Format.BitsPerPixel));
            sb.AppendLine(string.Format(LocalizationManager.GetString("L_Exif_Dpi_Format"), bitmap.DpiX, bitmap.DpiY));
            sb.AppendLine(string.Format(LocalizationManager.GetString("L_Exif_PixelFormat_Format"), bitmap.Format));

            return sb.ToString().TrimEnd();
        }

        private Task<string> GetImageMetadataInfoAsync(string filePath, long byteLength, BitmapSource bitmap)
        {
            return Task.Run(() =>
            {
                try
                {
                    StringBuilder sb = new StringBuilder(BuildBasicMetadataInfo(filePath, byteLength, bitmap));

                    // --- 2. 尝试读取 EXIF 元数据 ---
                    if (IsVirtualPath(filePath) || !File.Exists(filePath)) return sb.ToString().TrimEnd();

                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);

                    if (decoder.Frames.Count > 0 && decoder.Frames[0].Metadata is BitmapMetadata metadata)
                    {
                        StringBuilder exifSb = new StringBuilder();

                        // 设备信息
                        string device = "";
                        if (!string.IsNullOrEmpty(metadata.CameraManufacturer)) device += metadata.CameraManufacturer + " ";
                        if (!string.IsNullOrEmpty(metadata.CameraModel)) device += metadata.CameraModel;
                        if (!string.IsNullOrEmpty(device)) exifSb.AppendLine(string.Format(LocalizationManager.GetString("L_Exif_Device_Format"), device.Trim()));

                        var expVal = TryGetQuery(metadata, AppConsts.ExifTagExposureTime);
                        if (expVal != null)
                        {
                            double seconds = ParseUnsignedRational(expVal);
                            if (seconds > 0)
                            {
                                if (seconds < 1.0)
                                    exifSb.AppendLine(string.Format(LocalizationManager.GetString("L_Exif_ExpTime_Fraction_Format"), Math.Round(1.0 / seconds)));
                                else
                                    exifSb.AppendLine(string.Format(LocalizationManager.GetString("L_Exif_ExpTime_Sec_Format"), seconds));
                            }
                        }

                        var fVal = TryGetQuery(metadata, AppConsts.ExifTagFNumber);
                        if (fVal != null)
                        {
                            double fNum = ParseUnsignedRational(fVal);
                            if (fNum > 0) exifSb.AppendLine(string.Format(LocalizationManager.GetString("L_Exif_FNumber_Format"), fNum));
                        }

                        var iso = TryGetQuery(metadata, AppConsts.ExifTagIsoSpeed);
                        if (iso != null) exifSb.AppendLine(string.Format(LocalizationManager.GetString("L_Exif_ISO_Format"), iso));

                        var biasVal = TryGetQuery(metadata, AppConsts.ExifTagExposureBias);
                        if (biasVal != null)
                        {
                            double bias = ParseSignedRational(biasVal);
                            // 格式化为 +0.3 / -1.0 / 0
                            string biasStr = bias == 0 ? "0" : (bias > 0 ? $"+{bias:0.##}" : $"{bias:0.##}");
                            exifSb.AppendLine(string.Format(LocalizationManager.GetString("L_Exif_Bias_Format"), biasStr));
                        }
                        var focalVal = TryGetQuery(metadata, AppConsts.ExifTagFocalLength);
                        if (focalVal != null)
                        {
                            double focal = ParseUnsignedRational(focalVal);
                            exifSb.AppendLine(string.Format(LocalizationManager.GetString("L_Exif_Focal_Format"), focal));
                        }

                        var focal35 = TryGetQuery(metadata, AppConsts.ExifTagFocalLength35mm);
                        if (focal35 != null) exifSb.AppendLine(string.Format(LocalizationManager.GetString("L_Exif_Focal35_Format"), focal35));


                        // 测光模式
                        var meter = TryGetQuery(metadata, AppConsts.ExifTagMeteringMode);
                        if (meter != null) exifSb.AppendLine(string.Format(LocalizationManager.GetString("L_Exif_Metering_Format"), MapMeteringMode(meter)));


                        // 闪光灯
                        var flash = TryGetQuery(metadata, AppConsts.ExifTagFlash);
                        if (flash != null)
                        {
                            bool isFlashOn = (Convert.ToInt32(flash) & 1) == 1;
                            string flashStatus = isFlashOn ? LocalizationManager.GetString("L_Exif_Flash_On") : LocalizationManager.GetString("L_Exif_Flash_Off");
                            // [Localized] Flash: {0}
                            exifSb.AppendLine(string.Format(LocalizationManager.GetString("L_Exif_Flash_Format"), flashStatus));
                        }

                        // 软件/后期
                        if (!string.IsNullOrEmpty(metadata.ApplicationName))
                            exifSb.AppendLine(string.Format(LocalizationManager.GetString("L_Exif_Software_Format"), metadata.ApplicationName));

                        if (!string.IsNullOrEmpty(metadata.DateTaken))
                            exifSb.AppendLine(string.Format(LocalizationManager.GetString("L_Exif_Date_Format"), metadata.DateTaken));

                        // 镜头信息
                        var lens = TryGetQuery(metadata, AppConsts.ExifTagLensModel);
                        if (lens != null) exifSb.AppendLine(string.Format(LocalizationManager.GetString("L_Exif_Lens_Format"), lens));

                        // 合并
                        if (exifSb.Length > 0)
                        {
                            sb.AppendLine();
                            sb.AppendLine(LocalizationManager.GetString("L_Exif_Section_Photo"));
                            sb.Append(exifSb.ToString());
                        }
                    }
                    return sb.ToString().TrimEnd();
                }
                catch (Exception ex)
                {
                    return LocalizationManager.GetString("L_Exif_Error_Parse") + ex.Message;
                }
            });
        }
        private object TryGetQuery(BitmapMetadata metadata, string query)
        {
            try { if (metadata.ContainsQuery(query)) return metadata.GetQuery(query); } catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }
            return null;
        }
        private double ParseUnsignedRational(object value)    // 解析无符号分数 (RATIONAL)
        {
            if (value == null) return 0;

            if (value is ulong raw)
            {
                uint numerator = (uint)(raw & 0xFFFFFFFF); // 低32位是分子
                uint denominator = (uint)(raw >> 32);      // 高32位是分母
                return denominator == 0 ? 0 : (double)numerator / denominator;
            }
            if (value is long rawLong)
            {
                uint numerator = (uint)(rawLong & 0xFFFFFFFF);
                uint denominator = (uint)(rawLong >> 32);
                return denominator == 0 ? 0 : (double)numerator / denominator;
            }
            if (value is double d) return d;

            return 0;
        }

        // 解析有符号分数 (SRATIONAL) - 用于曝光补偿
        private double ParseSignedRational(object value)
        {
            if (value == null) return 0;

            // 逻辑与上面类似，但是要处理符号
            if (value is long raw)
            {
                int numerator = (int)(raw & 0xFFFFFFFF);
                int denominator = (int)(raw >> 32);
                return denominator == 0 ? 0 : (double)numerator / denominator;
            }
            if (value is ulong rawU)
            {
                // 强转回 int
                int numerator = (int)(rawU & 0xFFFFFFFF);
                int denominator = (int)(rawU >> 32);
                return denominator == 0 ? 0 : (double)numerator / denominator;
            }
            return 0;
        }

        private string MapMeteringMode(object val)
        {
            int code = Convert.ToInt32(val);
            return code switch
            {
                0 => LocalizationManager.GetString("L_Exif_Meter_Unknown"),
                1 => LocalizationManager.GetString("L_Exif_Meter_Average"),
                2 => LocalizationManager.GetString("L_Exif_Meter_Center"),
                3 => LocalizationManager.GetString("L_Exif_Meter_Spot"),
                4 => LocalizationManager.GetString("L_Exif_Meter_MultiSpot"),
                5 => LocalizationManager.GetString("L_Exif_Meter_Pattern"),
                6 => LocalizationManager.GetString("L_Exif_Meter_Partial"),
                255 => LocalizationManager.GetString("L_Exif_Meter_Other"),
                _ => LocalizationManager.GetString("L_Exif_Meter_Unknown")
            };
        }

    }
}