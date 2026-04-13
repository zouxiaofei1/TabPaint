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
        private static bool? _isInstalled;
        private static Assembly? _magickAssembly;
        private static Type? _magickImageType;
        private static Type? _magickImageInfoType;
        private static Type? _magickFormatType;
        private static Type? _pixelMappingType;
        // ★ 文件名常量（使用 AppConsts 定义）
        private static string ManagedDllName => AppConsts.Magick_DllName;
        private static string CoreDllName => AppConsts.MagickCore_DllName;
        private static string NativeDllName => AppConsts.MagickNative_DllName;

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);
        public static bool IsPsdPluginInstalled()
        {
            if (_isInstalled.HasValue) return _isInstalled.Value;
            string managedPath = Path.Combine(AppConsts.PluginsDir, ManagedDllName);
            string corePath = Path.Combine(AppConsts.PluginsDir, CoreDllName);
            string nativePath = Path.Combine(AppConsts.PluginsDir, NativeDllName);
            // ★ 所有文件都必须存在
            _isInstalled = File.Exists(managedPath) && File.Exists(corePath) && File.Exists(nativePath);
            return _isInstalled.Value;
        }
        public static bool IsPsdPluginAvailable()
        {
            if (_isAvailable.HasValue) return _isAvailable.Value;
            if (!IsPsdPluginInstalled())
            {
                _isAvailable = false;
                return false;
            }
            try
            {
                string managedPath = Path.Combine(AppConsts.PluginsDir, ManagedDllName);
                string nativePath = Path.Combine(AppConsts.PluginsDir, NativeDllName);
                // ★ 第一步：设置非托管 DLL 搜索路径，让系统能找到 Native DLL
                SetDllDirectory(AppConsts.PluginsDir);
                // ★ 第二步：加载托管程序集（不是 Native！）
                _magickAssembly = Assembly.LoadFrom(managedPath);
                if (_magickAssembly == null)
                {
                    Logger.Error("PSD Plugin: Failed to load managed assembly.");
                    _isAvailable = false;
                    return false;
                }
                // ★ 第三步：注册 Native DLL 解析器（.NET 5+/6+/8+ 推荐方式）
                System.Runtime.InteropServices.NativeLibrary.SetDllImportResolver(
                    _magickAssembly,
                    (libraryName, assembly, searchPath) =>
                    {
                        // Magick.NET 内部会请求加载 "Magick.Native-Q8-x64"
                        if (libraryName.Contains("Magick.Native"))
                        {
                            string fullPath = Path.Combine(AppConsts.PluginsDir, libraryName);
                            if (!fullPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                                fullPath += ".dll";

                            if (File.Exists(fullPath))
                            {
                                Logger.Info($"PSD Plugin: Loading native DLL from {fullPath}");
                                return System.Runtime.InteropServices.NativeLibrary.Load(fullPath);
                            }
                        }
                        return IntPtr.Zero;
                    });
                // ★ 第四步：先调 SetNativeDirectory（Magick.NET 自身的机制）
                var magickNetType = _magickAssembly.GetType("ImageMagick.MagickNET");
                var setNativeDir = magickNetType?.GetMethod("SetNativeDirectory",
                    BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                if (setNativeDir != null)
                {
                    setNativeDir.Invoke(null, new object[] { AppConsts.PluginsDir });
                    Logger.Info($"PSD Plugin: SetNativeDirectory → {AppConsts.PluginsDir}");
                }
                // ★ 第五步：获取所需类型
                _magickImageType = _magickAssembly.GetType("ImageMagick.MagickImage");
                _magickImageInfoType = _magickAssembly.GetType("ImageMagick.MagickImageInfo");
                _magickFormatType = _magickAssembly.GetType("ImageMagick.MagickFormatInfo");
                _pixelMappingType = _magickAssembly.GetType("ImageMagick.PixelChannel");
                //try
                //{
                //    // 从主程序集和 Core 程序集都搜索
                //    var allAssemblies = new List<Assembly> { _magickAssembly };
                //    string corePath = Path.Combine(AppConsts.PluginsDir, "Magick.NET.Core.dll");
                //    if (File.Exists(corePath))
                //    {
                //        var coreAsm = Assembly.LoadFrom(corePath);
                //        if (coreAsm != null) allAssemblies.Add(coreAsm);
                //    }
                //    foreach (var asm in allAssemblies)
                //    {
                //        try
                //        {
                //            foreach (var type in asm.GetExportedTypes())
                //            {
                //                if (type.FullName.Contains("MagickFormat") ||
                //                    type.FullName.Contains("PixelMapping") ||
                //                    type.FullName.Contains("PixelChannel") ||
                //                    type.FullName.Contains("Mapp"))
                //                {
                //                    Console.WriteLine($"PSD Plugin: Found type: {type.FullName} (IsEnum={type.IsEnum}) in {asm.GetName().Name}");
                //                }
                //            }
                //        }
                //        catch (ReflectionTypeLoadException ex)
                //        {
                //            foreach (var le in ex.LoaderExceptions)
                //                Logger.Error($"  LoaderException: {le?.Message}");
                //        }
                //    }
                //}
                //catch (Exception ex)
                //{
                //    Logger.Error("PSD Plugin: Diagnostic scan failed", ex);
                //}

                // ★ 第六步：验证 Native 是否真正可用
                if (_magickImageType != null)
                {
                    try
                    {
                        var versionProp = magickNetType?.GetProperty("Version",
                            BindingFlags.Public | BindingFlags.Static);
                        var version = versionProp?.GetValue(null);
                        Logger.Info($"PSD Plugin: Loaded successfully, version = {version}");
                        _isAvailable = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("PSD Plugin: Native binding failed (native DLL issue)", ex);
                        _isAvailable = false;
                    }
                }
                else
                {
                    Logger.Error("PSD Plugin: MagickImage type not found.");
                    _isAvailable = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("PSD Plugin: Exception during availability check", ex);
                _isAvailable = false;
            }
            return _isAvailable.Value;
        }

        public static void ResetAvailability()
        {
            _isAvailable = null;
            _isInstalled = null;
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
