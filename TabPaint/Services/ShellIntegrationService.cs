using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace TabPaint
{
    public static class ShellIntegrationService
    {
        private const string ClassesRootPath = @"Software\Classes";
        private const string AppExeName = "TabPaint.exe";
        private const string AppProgId = "TabPaint.Image";
        private const string ContextMenuVerb = "TabPaint.Open";

        public static bool IsShellIntegrationEnabled()
        {
            try
            {
                using var classesRoot = Registry.CurrentUser.OpenSubKey(ClassesRootPath, false);
                if (classesRoot == null) return false;

                string cmdA = classesRoot.OpenSubKey(@"Applications\TabPaint.exe\shell\open\command")?.GetValue("") as string;
                string cmdB = classesRoot.OpenSubKey(@"SystemFileAssociations\image\shell\TabPaint.Open\command")?.GetValue("") as string;

                return !string.IsNullOrWhiteSpace(cmdA) && !string.IsNullOrWhiteSpace(cmdB);
            }
            catch
            {
                return false;
            }
        }

        public static void EnableShellIntegration()
        {
            string exePath = GetExecutablePath();
            string command = $"\"{exePath}\" \"%1\"";

            using var classesRoot = Registry.CurrentUser.CreateSubKey(ClassesRootPath);
            if (classesRoot == null) throw new InvalidOperationException("无法打开 HKCU\\Software\\Classes");

            // 1) 应用程序入口（用于“打开方式”）
            using (var cmd = classesRoot.CreateSubKey(@"Applications\TabPaint.exe\shell\open\command"))
            {
                cmd?.SetValue("", command, RegistryValueKind.String);
            }
            using (var app = classesRoot.CreateSubKey(@"Applications\TabPaint.exe"))
            {
                app?.SetValue("FriendlyAppName", "TabPaint", RegistryValueKind.String);
            }

            // 2) 程序 ProgID
            using (var prog = classesRoot.CreateSubKey(AppProgId))
            {
                prog?.SetValue("", "TabPaint Image", RegistryValueKind.String);
                prog?.SetValue("FriendlyTypeName", "TabPaint Image", RegistryValueKind.String);
            }
            using (var icon = classesRoot.CreateSubKey($@"{AppProgId}\DefaultIcon"))
            {
                icon?.SetValue("", $"\"{exePath}\",0", RegistryValueKind.String);
            }
            using (var progCmd = classesRoot.CreateSubKey($@"{AppProgId}\shell\open\command"))
            {
                progCmd?.SetValue("", command, RegistryValueKind.String);
            }

            // 3) 右键菜单：按“所有图片”统一 + 每个扩展补充
            using (var imageMenu = classesRoot.CreateSubKey(@"SystemFileAssociations\image\shell\TabPaint.Open"))
            {
                imageMenu?.SetValue("", "用TabPaint打开", RegistryValueKind.String);
                imageMenu?.SetValue("Icon", exePath, RegistryValueKind.String);
            }
            using (var imageCmd = classesRoot.CreateSubKey(@"SystemFileAssociations\image\shell\TabPaint.Open\command"))
            {
                imageCmd?.SetValue("", command, RegistryValueKind.String);
            }

            foreach (var ext in AppConsts.ImageExtensions)
            {
                // 打开方式
                using (var openWithList = classesRoot.CreateSubKey($@"{ext}\OpenWithList\{AppExeName}"))
                {
                    openWithList?.SetValue("", "", RegistryValueKind.String);
                }
                using (var openWithProgIds = classesRoot.CreateSubKey($@"{ext}\OpenWithProgids"))
                {
                    openWithProgIds?.SetValue(AppProgId, string.Empty, RegistryValueKind.String);
                }

                // 每个扩展都补一份右键菜单
                using (var extMenu = classesRoot.CreateSubKey($@"SystemFileAssociations\{ext}\shell\{ContextMenuVerb}"))
                {
                    extMenu?.SetValue("", "用TabPaint打开", RegistryValueKind.String);
                    extMenu?.SetValue("Icon", exePath, RegistryValueKind.String);
                }
                using (var extCmd = classesRoot.CreateSubKey($@"SystemFileAssociations\{ext}\shell\{ContextMenuVerb}\command"))
                {
                    extCmd?.SetValue("", command, RegistryValueKind.String);
                }
            }
        }

        public static void DisableShellIntegration()
        {
            using var classesRoot = Registry.CurrentUser.OpenSubKey(ClassesRootPath, true);
            if (classesRoot == null) return;

            SafeDeleteSubKeyTree(classesRoot, @"Applications\TabPaint.exe");
            SafeDeleteSubKeyTree(classesRoot, AppProgId);
            SafeDeleteSubKeyTree(classesRoot, @"SystemFileAssociations\image\shell\TabPaint.Open");

            foreach (var ext in AppConsts.ImageExtensions)
            {
                SafeDeleteSubKeyTree(classesRoot, $@"SystemFileAssociations\{ext}\shell\{ContextMenuVerb}");
                SafeDeleteSubKeyTree(classesRoot, $@"{ext}\OpenWithList\{AppExeName}");
                SafeDeleteValue(classesRoot, $@"{ext}\OpenWithProgids", AppProgId);
            }
        }

        private static string GetExecutablePath()
        {
            string? processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath)) return processPath;

            processPath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath)) return processPath;

            throw new InvalidOperationException("无法定位程序可执行文件路径。");
        }

        private static void SafeDeleteSubKeyTree(RegistryKey root, string subKeyPath)
        {
            try
            {
                root.DeleteSubKeyTree(subKeyPath, false);
            }
            catch
            {
                // 忽略
            }
        }

        private static void SafeDeleteValue(RegistryKey root, string subKeyPath, string valueName)
        {
            try
            {
                using var key = root.OpenSubKey(subKeyPath, true);
                key?.DeleteValue(valueName, false);
            }
            catch
            {
                // 忽略
            }
        }
    }
}