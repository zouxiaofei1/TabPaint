using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public partial class MainWindow 
    {
        private void SaveAppState()
        {
            var settings = TabPaint.SettingsManager.Instance.Current;

            if (_router?.CurrentTool != null)
            {
                settings.LastToolName = _router.CurrentTool.GetType().Name;
            }
            if (_ctx != null)
            {
                settings.LastBrushStyle = _ctx.PenStyle;
            }
            TabPaint.SettingsManager.Instance.Save();
        }

        /// <summary>
        /// 2. 恢复上次的应用状态
        /// </summary>
        private void RestoreAppState()
        {
            try
            { 

            var settings = TabPaint.SettingsManager.Instance.Current;

            // 1. 恢复笔刷大小
            if (_ctx != null)
            {
                _ctx.PenThickness = settings.PenThickness;
            }

            // 2. 恢复工具和样式
            ITool targetTool = null; // 默认
            BrushStyle targetStyle = settings.LastBrushStyle;

            switch (settings.LastToolName)
            {
                case "EyedropperTool": targetTool = _tools.Eyedropper; break;
                case "FillTool": targetTool = _tools.Fill; break;
                case "SelectTool": targetTool = _tools.Select; break;
                case "TextTool": targetTool = _tools.Text; break;
                case "ShapeTool": targetTool = _tools.Shape; break;
                case "PenTool":
                default:
                    targetTool = _tools.Pen;
                    break;
            }

            if (_ctx != null)
            {
                _ctx.PenStyle = targetStyle;
            }

            // 3. 应用工具切换
            // 注意：这里需要确保界面元素(MainToolBar)已经加载完毕，否则高亮更新可能会空引用
            Dispatcher.InvokeAsync(() =>
            {
                _router.SetTool(targetTool);

            }, System.Windows.Threading.DispatcherPriority.Loaded);
            }
            finally
            {

            }
        }
    }
    public class AppSettings : INotifyPropertyChanged
    {
        private double _penThickness = 5.0; // 默认值

        [JsonPropertyName("pen_thickness")]
        public double PenThickness
        {
            get => _penThickness;
            set
            {
                // 如果值没变，什么都不做
                if (Math.Abs(_penThickness - value) < 0.01) return;

                _penThickness = value;
                OnPropertyChanged();
            }
        }
        private double _penOpacity = 1.0; // 默认不透明 (0.0 到 1.0)
        [JsonPropertyName("pen_opacity")]
        public double PenOpacity
        {
            get => _penOpacity;
            set
            {
                if (_penOpacity != value)
                {
                    _penOpacity = value;
                    OnPropertyChanged(nameof(PenOpacity));
                }
            }
        }
        // 在 AppSettings 类中添加
        private bool _isFixedZoom = false;

        [JsonPropertyName("is_fixed_zoom")]
        public bool IsFixedZoom
        {
            get => _isFixedZoom;
            set
            {
                if (_isFixedZoom != value)
                {
                    _isFixedZoom = value;
                    OnPropertyChanged();
                }
            }
        }
        [JsonPropertyName("enable_clipboard_monitor")]
        public bool EnableClipboardMonitor
        {
            get => _enableClipboardMonitor;
            set
            {
                if (_enableClipboardMonitor != value)
                {
                    _enableClipboardMonitor = value;
                    OnPropertyChanged();
                    //SettingsManager.Instance.Save(); // 自动保存
                }
            }
        }
        private bool _enableClipboardMonitor = false; // 默认关闭



        [JsonPropertyName("last_tool_name")]
        public string LastToolName { get; set; } = "PenTool"; // 默认为笔

        [JsonPropertyName("last_brush_style")]
        public BrushStyle LastBrushStyle { get; set; } = BrushStyle.Pencil; // 默认为铅笔

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private List<string> _recentFiles = new List<string>();

        [JsonPropertyName("recent_files")]
        public List<string> RecentFiles
        {
            get => _recentFiles;
            set
            {
                if (_recentFiles != value)
                {
                    _recentFiles = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
