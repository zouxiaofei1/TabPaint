using System;
using System.IO;
using System.Reflection;
using System.Windows;

namespace TabPaint.Services
{
    /// <summary>
    /// PSD 读取插件助手，负责 Magick.NET 的动态加载与状态检查。
    /// </summary>
    public static class PsdPluginHelper
    {
        private static bool? _isAvailable;
        private static Assembly? _magickAssembly;
        private static Type? _magickImageType;
        private static Type? _magickImageInfoType;
        private static Type? _magickFormatType;
        private static Type? _pixelMappingType;

        public static bool IsPsdPluginAvailable()
        {
            if (_isAvailable.HasValue) return _isAvailable.Value;

            try
            {
                string dllPath = Path.Combine(AppConsts.PluginsDir, AppConsts.Magick_DllName);
                if (File.Exists(dllPath))
                {
                    // 加载程序集
                    _magickAssembly = Assembly.LoadFrom(dllPath);
                    if (_magickAssembly != null)
                    {
                        _magickImageType = _magickAssembly.GetType("ImageMagick.MagickImage");
                        _magickImageInfoType = _magickAssembly.GetType("ImageMagick.MagickImageInfo");
                        _magickFormatType = _magickAssembly.GetType("ImageMagick.MagickFormat");
                        _pixelMappingType = _magickAssembly.GetType("ImageMagick.PixelMapping");

                        // 设置 Native 目录（如果有的话）
                        var magickNetType = _magickAssembly.GetType("ImageMagick.MagickNET");
                        var setNativeDirectoryMethod = magickNetType?.GetMethod("SetNativeDirectory", new[] { typeof(string) });
                        if (setNativeDirectoryMethod != null)
                        {
                            setNativeDirectoryMethod.Invoke(null, new object[] { AppConsts.PluginsDir });
                        }

                        _isAvailable = _magickImageType != null;
                    }
                    else
                    {
                        _isAvailable = false;
                    }
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
            _magickAssembly = null;
            _magickImageType = null;
            _magickImageInfoType = null;
            _magickFormatType = null;
            _pixelMappingType = null;
        }

        public static Assembly? GetAssembly() => _magickAssembly;
        public static Type? GetMagickImageType() => _magickImageType;
        public static Type? GetMagickImageInfoType() => _magickImageInfoType;
        public static Type? GetMagickFormatType() => _magickFormatType;
        public static Type? GetPixelMappingType() => _pixelMappingType;
    }
}
