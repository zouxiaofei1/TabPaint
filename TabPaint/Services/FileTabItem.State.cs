
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

//
//ImageBar图片选择框相关代码
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public partial class FileTabItem : INotifyPropertyChanged
        {
            private string _filePath;
            public string FilePath
            {
                get => _filePath;
                set
                {
                    _filePath = value;
                    OnPropertyChanged(nameof(FilePath));
                    // 🔥 关键：当路径变了，文件名和显示名自然也变了
                    OnPropertyChanged(nameof(FileName));
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
            private int _untitledNumber;
            public int UntitledNumber
            {
                get => _untitledNumber;
                set
                {
                    _untitledNumber = value;
                    OnPropertyChanged(nameof(UntitledNumber));
                    OnPropertyChanged(nameof(FileName));
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
            public string FileName
            {
                get
                {
                    if (!string.IsNullOrEmpty(FilePath))
                        return System.IO.Path.GetFileName(FilePath);
                    if (IsNew) // 如果是新建文件，显示 "未命名 X"
                        return $"未命名 {UntitledNumber}";
                    return "未命名";
                }
            }

            public string DisplayName// 🔄 修改：DisplayName (不带扩展名) 的显示逻辑同理
            {
                get
                {
                    if (!string.IsNullOrEmpty(FilePath))
                        return System.IO.Path.GetFileNameWithoutExtension(FilePath);

                    if (IsNew)
                        return $"未命名 {UntitledNumber}";

                    return "未命名";
                }
            }
            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
            }

            private bool _isLoading;
            public bool IsLoading
            {
                get => _isLoading;
                set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
            }

            // 🔴 状态：是否修改未保存
            private bool _isDirty;
            public bool IsDirty
            {
                get => _isDirty;
                set { _isDirty = value; OnPropertyChanged(nameof(IsDirty)); }
            }

            // 🔵 状态：是否是纯新建的内存文件
            private bool _isNew;
            public bool IsNew
            {
                get => _isNew;
                set
                {
                    _isNew = value;
                    OnPropertyChanged(nameof(IsNew));
                    // 🔥 关键：从“新建”变为“非新建”状态时，名字显示逻辑会切换
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(FileName));
                }
            }


            private BitmapSource? _thumbnail;
            public BitmapSource? Thumbnail
            {
                get => _thumbnail;
                set { _thumbnail = value; OnPropertyChanged(nameof(Thumbnail)); }
            }

            public ICommand CloseCommand { get; set; }
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string BackupPath { get; set; }
            public DateTime LastBackupTime { get; set; }
            public FileTabItem(string path)
            {
                FilePath = path;
            }

            // 在 MainWindow.cs -> FileTabItem 类内部

            public async Task LoadThumbnailAsync(int containerWidth, int containerHeight)
            {
                // 1. 确定要加载的路径：优先由 BackupPath (缓存)，其次是 FilePath (原图)
                string targetPath = null;

                if (!string.IsNullOrEmpty(BackupPath) && File.Exists(BackupPath))
                {
                    targetPath = BackupPath;
                }
                else if (!string.IsNullOrEmpty(FilePath) && File.Exists(FilePath))
                {
                    targetPath = FilePath;
                }

                if (targetPath == null) return;

                var thumbnail = await Task.Run(() =>
                {
                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(targetPath);

                        if (targetPath == FilePath)
                        {
                            bmp.DecodePixelWidth = 100;
                        }

                        bmp.CacheOption = BitmapCacheOption.OnLoad; // 关键：加载完立即释放文件锁
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    }
                    catch
                    {
                        return null;
                    }
                });

                if (thumbnail != null)
                {
                    // 回到 UI 线程设置属性
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        Thumbnail = thumbnail;
                    });
                }
            }
            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }


        private const int PageSize = 10; // 每页标签数量（可调整）

        public ObservableCollection<FileTabItem> FileTabs { get; }
            = new ObservableCollection<FileTabItem>();
        private bool _isProgrammaticScroll = false;
        // 文件总数绑定属性
        public int ImageFilesCount;
        private bool _isInitialLayoutComplete = false;
        // 这里的 1 表示下一个新建文件的编号
        private int _nextUntitledIndex = 1;
        // 存储当前会话中被用户手动关闭的图片路径，防止自动滚动时诈尸
        private HashSet<string> _explicitlyClosedFiles = new HashSet<string>();
        private long _currentCanvasVersion = 0;

        // 上次成功备份时的版本号
        private long _lastBackedUpVersion = -1;
    }
}