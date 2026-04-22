using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using System.Windows.Threading;
using static TabPaint.MainWindow;
using System.Text.Json.Serialization;
namespace TabPaint
{
    [JsonSerializable(typeof(AppSettings))]
    [JsonSerializable(typeof(List<string>))]
    [JsonSerializable(typeof(Dictionary<string, ToolSettingsModel>))]
    [JsonSerializable(typeof(Dictionary<string, ShortcutItem>))]
    public partial class AppSettingsJsonContext : JsonSerializerContext
    {
    }
    public class SettingsManager
    {
        private static SettingsManager _instance;
        private static readonly object _lock = new object();
        private readonly string _folderPath;        // 设定存储路径: AppData/Local/TabPaint/settings.json
        private readonly string _filePath;
        private readonly string _binPath;
        // 当前的设置实例
        public AppSettings Current { get; private set; }
        private SettingsManager()
        {
            _folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TabPaint");
            _filePath = Path.Combine(_folderPath, "settings.json");
            _binPath = Path.Combine(_folderPath, "settings.bin");
            Load(); // 初始化时尝试加载，如果失败则创建默认
        }
        public static SettingsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null) _instance = new SettingsManager();
                    }
                }
                return _instance;
            }
        }
        public async Task InitializeAsync()
        {
            await Task.Run(() =>
            {
                Load(); // 在后台线程执行繁重的 I/O 和 JSON 解析
            });
        }

        // 加载设置
        public void Load()
        {
            Directory.CreateDirectory(_folderPath);

            bool loaded = false;

            // 1. 尝试从二进制文件加载 (性能优化：< 3ms)
            if (File.Exists(_binPath))
            {
                bool useBin = true;
                if (File.Exists(_filePath))
                {
                    var binInfo = new FileInfo(_binPath);
                    var jsonInfo = new FileInfo(_filePath);
                    // 如果 JSON 比 BIN 新，说明用户可能手动编辑了 JSON，此时不使用 BIN
                    if (jsonInfo.LastWriteTime > binInfo.LastWriteTime) useBin = false;
                }

                if (useBin && LoadBinary())
                {
                    loaded = true;
                }
            }
            if (!loaded)
            {
                if (File.Exists(_filePath))
                {
                    try
                    {
                        using (FileStream stream = File.OpenRead(_filePath))
                        {
                            Current = JsonSerializer.Deserialize(stream, AppSettingsJsonContext.Default.AppSettings);
                        }
                        loaded = true;
                        SaveBinary();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Settings] JSON Load failed: {ex.Message}. Using defaults.");
                    }
                }
            }
            if (!loaded)
            {
                Current = new AppSettings();
                Save(); // 初始化时同时创建 .json 和 .bin
            }
        }
        public void Save()// 保存设置
        {
            try
            {
                if (!Directory.Exists(_folderPath)) Directory.CreateDirectory(_folderPath);
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(Current, options);
                File.WriteAllText(_filePath, jsonString);
                SaveBinary();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] Save failed: {ex.Message}");
            }
        }
        public void AddRecentFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

            var list = Current.RecentFiles ?? new List<string>();
            var existing = list.FirstOrDefault(f => f.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                list.Remove(existing);
            }

            // 插入到开头
            list.Insert(0, filePath);

            // 限制数量
            if (list.Count > Current.MaxRecentFiles) list = list.Take(Current.MaxRecentFiles).ToList();

            Current.RecentFiles = list;
        }
        public void ClearRecentFiles()
        {
            Current.RecentFiles = new List<string>();
            Save();
        }

        private void SaveBinary()
        {

            try
            {
                using (var stream = File.Create(_binPath))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(AppConsts.AppSettingsBinaryVersion);

                    // 基础属性
                    writer.Write((int)Current.Language);
                    writer.Write((int)Current.SelectionClearMode);
                    writer.Write(Current.IsFirstRun);
                    writer.Write(Current.LastLaunchedVersion ?? "");
                    writer.Write(Current.NewestInstalledVersion ?? "");
                    writer.Write(Current.IsImageBarCompact);
                    writer.Write(Current.IsStatusCommandBarExpanded);
                    writer.Write(Current.AlwaysShowTabCloseButton);
                    writer.Write(Current.StartInViewMode);
                    writer.Write((int)Current.ViewMouseWheelMode);
                    writer.Write(Current.IsTextToolbarExpanded);
                    writer.Write(Current.EnableIccColorCorrection);
                    writer.Write((int)Current.ThemeMode);
                    writer.Write(Current.IsFixedZoom);
                    writer.Write(Current.IsWindowTopmost);
                    writer.Write(Current.EnableClipboardMonitor);
                    writer.Write(Current.LastToolName ?? "");
                    writer.Write((int)Current.LastBrushStyle);
                    writer.Write(Current.IsSelectionRotateEnabled);
                    writer.Write(Current.ShowRulers);
                    writer.Write((int)Current.ResamplingMode);
                    writer.Write(Current.ViewInterpolationThreshold);
                    writer.Write(Current.PaintInterpolationThreshold);
                    writer.Write(Current.WindowWidth);
                    writer.Write(Current.WindowHeight);
                    writer.Write(Current.WindowLeft);
                    writer.Write(Current.WindowTop);
                    writer.Write(Current.WindowState);
                    writer.Write(Current.AutoLoadFolderImages);
                    writer.Write(Current.ViewShowTransparentGrid);
                    writer.Write(Current.SkipResetConfirmation);
                    writer.Write(Current.DiscardAllOnExit);
                    writer.Write(Current.AutoPopupOnClipboardImage);
                    writer.Write(Current.EnableFileDeleteInPaintMode);
                    writer.Write(Current.AiOcrPromptShown);
                    writer.Write(Current.EnableAiOcr);
                    writer.Write(Current.AiImageApiBaseUrl ?? AppConsts.AiImageDefaultApiBaseUrl);
                    writer.Write(Current.AiImageApiKey ?? string.Empty);
                    writer.Write(Current.AiImageModel ?? AppConsts.AiImageDefaultModel);
                    writer.Write(Current.AiModelDefaultSaveDir ?? AppConsts.AiModelDefaultSaveDir);
                    writer.Write((int)Current.OcrResultAction);
                    writer.Write(Current.IsShapeToolProMode);
                    writer.Write(Current.IsTextToolProMode);
                    writer.Write(Current.ViewUseDarkCanvasBackground);
                    writer.Write(Current.ShowBirdEyeInViewMode);
                    writer.Write(Current.ViewLogoMenuHintShown);
                    writer.Write(Current.ThemeAccentColor ?? "");
                    writer.Write(Current.PerformanceScore);
                    writer.Write(Current.LastBenchmarkDate.Ticks);
                    writer.Write(Current.MaxUndoMemoryMB);
                    writer.Write(Current.MaxGlobalUndoSteps);
                    writer.Write(Current.DefaultBlankCanvasWidth);
                    writer.Write(Current.DefaultBlankCanvasHeight);
                    writer.Write((int)Current.RmbgModel);
                    writer.Write(Current.UseWin10StyleOnWin11);
                    writer.Write(Current.MaxRecentFiles);
                    writer.Write(Current.SvgDecodeSize);
                    writer.Write(Current.EnablePdfSavePage);

                    // PinnedSettingsTags
                    var pinnedTags = Current.PinnedSettingsTags ?? new List<string>();
                    writer.Write(pinnedTags.Count);
                    foreach (var tag in pinnedTags) writer.Write(tag ?? "");

                    // RecentFiles
                    var recentFiles = Current.RecentFiles ?? new List<string>();
                    writer.Write(recentFiles.Count);
                    foreach (var file in recentFiles) writer.Write(file ?? "");

                    // CustomColors
                    var customColors = Current.CustomColors ?? new List<string>();
                    writer.Write(customColors.Count);
                    foreach (var color in customColors) writer.Write(color ?? "");

                    // PerToolSettings
                    var perToolSettings = Current.PerToolSettings ?? new Dictionary<string, ToolSettingsModel>();
                    writer.Write(perToolSettings.Count);
                    foreach (var kvp in perToolSettings)
                    {
                        writer.Write(kvp.Key);
                        writer.Write(kvp.Value.Thickness);
                        writer.Write(kvp.Value.Opacity);
                    }

                    // Shortcuts
                    var shortcuts = Current.Shortcuts ?? new Dictionary<string, ShortcutItem>();
                    writer.Write(shortcuts.Count);
                    foreach (var kvp in shortcuts)
                    {
                        writer.Write(kvp.Key);
                        writer.Write((int)kvp.Value.Key);
                        writer.Write((int)kvp.Value.Modifiers);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] SaveBinary failed: {ex.Message}");
            }
        }

        private bool LoadBinary()
        {
            if (!File.Exists(_binPath)) return false;

            try
            {

                using (var stream = File.OpenRead(_binPath))
                using (var reader = new BinaryReader(stream))
                {
                    int dataVersion = reader.ReadInt32();
                    if (dataVersion < 7 || dataVersion > AppConsts.AppSettingsBinaryVersion) return false;

                    var settings = new AppSettings();
                    settings.Language = (AppLanguage)reader.ReadInt32();
                    settings.SelectionClearMode = (SelectionClearMode)reader.ReadInt32();
                    settings.IsFirstRun = reader.ReadBoolean();
                    settings.LastLaunchedVersion = reader.ReadString();
                    settings.NewestInstalledVersion = dataVersion >= 8 ? reader.ReadString() : settings.LastLaunchedVersion;
                    settings.IsImageBarCompact = reader.ReadBoolean();
                    settings.IsStatusCommandBarExpanded = dataVersion >= 12 && reader.ReadBoolean();
                    settings.AlwaysShowTabCloseButton = reader.ReadBoolean();
                    settings.StartInViewMode = reader.ReadBoolean();
                    settings.ViewMouseWheelMode = (MouseWheelMode)reader.ReadInt32();
                    settings.IsTextToolbarExpanded = reader.ReadBoolean();
                    settings.EnableIccColorCorrection = reader.ReadBoolean();
                    settings.ThemeMode = (AppTheme)reader.ReadInt32();
                    settings.IsFixedZoom = reader.ReadBoolean();
                    settings.IsWindowTopmost = reader.ReadBoolean();
                    settings.EnableClipboardMonitor = reader.ReadBoolean();
                    settings.LastToolName = reader.ReadString();
                    settings.LastBrushStyle = (BrushStyle)reader.ReadInt32();
                    settings.IsSelectionRotateEnabled = reader.ReadBoolean();
                    settings.ShowRulers = reader.ReadBoolean();
                    settings.ResamplingMode = (AppResamplingMode)reader.ReadInt32();
                    settings.ViewInterpolationThreshold = reader.ReadDouble();
                    settings.PaintInterpolationThreshold = reader.ReadDouble();
                    settings.WindowWidth = reader.ReadDouble();
                    settings.WindowHeight = reader.ReadDouble();
                    settings.WindowLeft = reader.ReadDouble();
                    settings.WindowTop = reader.ReadDouble();
                    settings.WindowState = reader.ReadInt32();
                    settings.AutoLoadFolderImages = reader.ReadBoolean();
                    settings.ViewShowTransparentGrid = reader.ReadBoolean();
                    settings.SkipResetConfirmation = reader.ReadBoolean();
                    settings.DiscardAllOnExit = reader.ReadBoolean();
                    settings.AutoPopupOnClipboardImage = reader.ReadBoolean();
                    settings.EnableFileDeleteInPaintMode = reader.ReadBoolean();
                    settings.AiOcrPromptShown = reader.ReadBoolean();
                    settings.EnableAiOcr = reader.ReadBoolean();
                    settings.AiImageApiBaseUrl = dataVersion >= 16 ? reader.ReadString() : AppConsts.AiImageDefaultApiBaseUrl;
                    settings.AiImageApiKey = dataVersion >= 16 ? reader.ReadString() : string.Empty;
                    settings.AiImageModel = dataVersion >= 16 ? reader.ReadString() : AppConsts.AiImageDefaultModel;
                    settings.AiModelDefaultSaveDir = dataVersion >= 18 ? reader.ReadString() : AppConsts.AiModelDefaultSaveDir;
                    settings.OcrResultAction = dataVersion >= 10
                        ? (OcrResultAction)reader.ReadInt32()
                        : OcrResultAction.EditText;
                    settings.IsShapeToolProMode = reader.ReadBoolean();
                    settings.IsTextToolProMode = dataVersion >= 20 ? reader.ReadBoolean() : false;
                    settings.ViewUseDarkCanvasBackground = reader.ReadBoolean();
                    settings.ShowBirdEyeInViewMode = dataVersion >= 11
                        ? reader.ReadBoolean()
                        : true;
                    settings.ViewLogoMenuHintShown = dataVersion >= 17
                        ? reader.ReadBoolean()
                        : false;
                    settings.ThemeAccentColor = reader.ReadString();
                    settings.PerformanceScore = reader.ReadInt32();
                    settings.LastBenchmarkDate = new DateTime(reader.ReadInt64());
                    settings.MaxUndoMemoryMB = reader.ReadInt32();
                    settings.MaxGlobalUndoSteps = reader.ReadInt32();
                    if (dataVersion >= 19)
                    {
                        settings.DefaultBlankCanvasWidth = reader.ReadInt32();
                        settings.DefaultBlankCanvasHeight = reader.ReadInt32();
                        settings.RmbgModel = (RmbgModelType)reader.ReadInt32();
                    }
                    if (dataVersion >= 21)
                    {
                        settings.UseWin10StyleOnWin11 = reader.ReadBoolean();
                    }
                    if (dataVersion >= 22)
                    {
                        settings.MaxRecentFiles = reader.ReadInt32();
                    }
                    if (dataVersion >= 24)
                    {
                        settings.SvgDecodeSize = reader.ReadInt32();
                    }
                    if (dataVersion >= 25)
                    {
                        settings.EnablePdfSavePage = reader.ReadBoolean();
                    }
                    if (dataVersion >= 23)
                    {
                        int pinnedCount = reader.ReadInt32();
                        var pinnedTags = new List<string>(pinnedCount);
                        for (int i = 0; i < pinnedCount; i++) pinnedTags.Add(reader.ReadString());
                        settings.PinnedSettingsTags = pinnedTags;
                    }
                    // RecentFiles
                    int recentCount = reader.ReadInt32();
                    var recentFiles = new List<string>(recentCount);
                    for (int i = 0; i < recentCount; i++) recentFiles.Add(reader.ReadString());
                    settings.RecentFiles = recentFiles;

                    // CustomColors
                    int colorCount = reader.ReadInt32();
                    var customColors = new List<string>(colorCount);
                    for (int i = 0; i < colorCount; i++) customColors.Add(reader.ReadString());
                    settings.CustomColors = customColors;

                    // PerToolSettings
                    int toolCount = reader.ReadInt32();
                    var perToolSettings = new Dictionary<string, ToolSettingsModel>(toolCount);
                    for (int i = 0; i < toolCount; i++)
                    {
                        var key = reader.ReadString();
                        perToolSettings[key] = new ToolSettingsModel
                        {
                            Thickness = reader.ReadDouble(),
                            Opacity = reader.ReadDouble()
                        };
                    }
                    settings.PerToolSettings = perToolSettings;

                    // Shortcuts
                    int shortcutCount = reader.ReadInt32();
                    var shortcuts = new Dictionary<string, ShortcutItem>(shortcutCount);
                    for (int i = 0; i < shortcutCount; i++)
                    {
                        var key = reader.ReadString();
                        shortcuts[key] = new ShortcutItem
                        {
                            Key = (System.Windows.Input.Key)reader.ReadInt32(),
                            Modifiers = (System.Windows.Input.ModifierKeys)reader.ReadInt32()
                        };
                    }
                    settings.Shortcuts = shortcuts;

                    Current = settings;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] LoadBinary failed: {ex.Message}");
                return false;
            }
        }
    }
}
