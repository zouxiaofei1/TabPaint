using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public class SettingsManager
    {
        private static SettingsManager _instance;
        private static readonly object _lock = new object();

        // 设定存储路径: AppData/Local/TabPaint/settings.json
        private readonly string _folderPath;
        private readonly string _filePath;
        private const int MaxRecentFiles = 10;
        // 当前的设置实例
        public AppSettings Current { get; private set; }

        // 私有构造函数
        private SettingsManager()
        {
            _folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TabPaint");
            _filePath = Path.Combine(_folderPath, "settings.json");

            // 初始化时尝试加载，如果失败则创建默认
            Load();
        }

        // 单例访问点
        public static SettingsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new SettingsManager();
                        }
                    }
                }
                return _instance;
            }
        }

        // 加载设置
        public void Load()
        {
            if (!Directory.Exists(_folderPath))
            {
                Directory.CreateDirectory(_folderPath);
            }

            if (File.Exists(_filePath))
            {
                try
                {
                    string jsonString = File.ReadAllText(_filePath);
                    // 允许注释，格式化宽松
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };
                    Current = JsonSerializer.Deserialize<AppSettings>(jsonString, options);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Settings] Load failed: {ex.Message}. Using defaults.");
                    // 加载失败（如文件损坏），回滚到默认设置
                    Current = new AppSettings();
                }
            }
            else
            {
                // 文件不存在，使用默认值
                Current = new AppSettings();
                Save(); // 立即创建文件
            }
        }

        // 保存设置
        public void Save()
        {
            try
            {
                if (!Directory.Exists(_folderPath))
                {
                    Directory.CreateDirectory(_folderPath);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true // 格式化输出，方便人类阅读
                };
                string jsonString = JsonSerializer.Serialize(Current, options);
                File.WriteAllText(_filePath, jsonString);
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

            // 如果已存在，先移除（为了移到最前面）
            // 忽略大小写比较
            var existing = list.FirstOrDefault(f => f.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                list.Remove(existing);
            }

            // 插入到开头
            list.Insert(0, filePath);

            // 限制数量
            if (list.Count > MaxRecentFiles)
            {
                list = list.Take(MaxRecentFiles).ToList();
            }

            Current.RecentFiles = list;
           // Save(); // 立即保存
        }
        public void ClearRecentFiles()
        {
            Current.RecentFiles = new List<string>();
            Save();
        }


    }
}
