//
//AppSettings.cs
//应用程序设置模型，包含语言、主题、快捷键、画笔参数及各种UI配置项的持久化结构。
//
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public enum AppResamplingMode
    {
        Auto,
        Bilinear,    // 双线性
        Fant,        // 高质量幻像 (WPF HighQualityBicubic 类似)
        HighQuality  // 通用高质量
    }
    public enum SelectionClearMode
    {
        Transparent,    // 透明底 (RGBA: 0,0,0,0)
        White,          // 白底 (RGBA: 255,255,255,255)
        PreserveAlpha   // 不改变Alpha (只修改RGB，保留原透明度)
    }
    public enum MouseWheelMode
    {
        Zoom,          // 缩放 (默认)
        SwitchImage,   // 切图
        VerticalScroll // 上下滚动
    }
    public enum AppLanguage
    {
        ChineseSimplified,
        English
    }
    public enum AppTheme
    {
        Light,
        Dark,
        System // 跟随系统
    }
    public enum OcrResultAction
    {
        EditText,
        DirectCopy
    }
    public class ToolSettingsModel
    {
        public double Thickness { get; set; } = AppConsts.DefaultPenThickness;
        public double Opacity { get; set; } = AppConsts.DefaultPenOpacity;

        public ToolSettingsModel Clone()
        {
            return new ToolSettingsModel
            {
                Thickness = this.Thickness,
                Opacity = this.Opacity
            };
        }
    }

    public class ShortcutItem : INotifyPropertyChanged
    {
        private Key _key;
        private ModifierKeys _modifiers;

        public Key Key
        {
            get => _key;
            set
            {
                if (_key != value)
                {
                    _key = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        public ModifierKeys Modifiers
        {
            get => _modifiers;
            set
            {
                if (_modifiers != value)
                {
                    _modifiers = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            // 当快捷键的 Key 或 Modifiers 改变时，通知全局 Provider
            if (propertyName == nameof(Key) || propertyName == nameof(Modifiers))
            {
                TabPaint.Services.ShortcutProvider.Instance.NotifyChanged();
            }
        }

        public string DisplayText
        {
            get
            {
                if (Key == Key.None) return LocalizationManager.GetString("L_Key_None");

                StringBuilder sb = new StringBuilder();
                if ((Modifiers & ModifierKeys.Control) == ModifierKeys.Control) sb.Append("Ctrl + "); // 1. 处理修饰键 (Ctrl, Alt, Shift)
                if ((Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) sb.Append("Shift + ");
                if ((Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) sb.Append("Alt + ");
                if ((Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) sb.Append("Win + ");
                sb.Append(GetKeyDisplayName(Key));// 2. 处理按键显示的转换逻辑
                return sb.ToString();
            }
        }

        private string GetKeyDisplayName(Key key)
        {
            // 处理主键盘区的数字键 D0 - D9
            if (key >= Key.D0 && key <= Key.D9) return ((int)key - (int)Key.D0).ToString();

            // 处理小键盘区的数字键 NumPad0 - NumPad9
            if (key >= Key.NumPad0 && key <= Key.NumPad9) return "Num " + ((int)key - (int)Key.NumPad0).ToString();
            switch (key)
            {
                case Key.OemPlus: return "+";
                case Key.OemMinus: return "-";
                case Key.OemComma: return ",";
                case Key.OemPeriod: return ".";
                case Key.OemQuestion: return "?";
                case Key.OemOpenBrackets: return "[";
                case Key.OemCloseBrackets: return "]";
                case Key.OemPipe: return "|";
                case Key.OemTilde: return "~";
                case Key.Return: return "Enter";
                case Key.Next: return "PageDown"; // WPF里 PageDown叫 Next
                case Key.Capital: return "CapsLock";
                case Key.Back: return "Backspace";
                default:
                    return key.ToString(); // 其他键保持默认
            }
        }
    }
    public partial class MainWindow
    {
        private void SaveAppState()
        {
            var settings = TabPaint.SettingsManager.Instance.Current;

            int mainWindowCount = Application.Current.Windows.OfType<MainWindow>().Count();
            // 如果是最后一个窗口，或者程序正在退出过程中，则保存画笔参数到全局设置
            if (mainWindowCount <= 1 || _programClosed)
            {
                // 只有最后一个窗口关闭时才保存画笔参数到设置
                if (this.LocalPerToolSettings != null)
                {
                    settings.PerToolSettings = this.LocalPerToolSettings.ToDictionary(k => k.Key, v => v.Value.Clone());
                }
                settings.CurrentToolKey = this.CurrentToolKey;

                if (_router?.CurrentTool != null) settings.LastToolName = _router.CurrentTool.GetType().Name;
                if (_ctx != null) settings.LastBrushStyle = _ctx.PenStyle;
            }

            if (this.WindowState == System.Windows.WindowState.Maximized)
            {
                // 最大化时，保存还原后的坐标和尺寸
                settings.WindowTop = this.RestoreBounds.Top;
                settings.WindowLeft = this.RestoreBounds.Left;
                settings.WindowHeight = this.RestoreBounds.Height;
                settings.WindowWidth = this.RestoreBounds.Width;
                settings.WindowState = (int)System.Windows.WindowState.Maximized;
            }
            else if (this.WindowState == System.Windows.WindowState.Normal)
            {
                // 正常模式，直接保存
                settings.WindowTop = this.Top;
                settings.WindowLeft = this.Left;
                settings.WindowHeight = this.Height;
                settings.WindowWidth = this.Width;
                settings.WindowState = (int)System.Windows.WindowState.Normal;
            }

            if (MainImageBar != null)
            {
                settings.IsImageBarCompact = MainImageBar.IsCompactMode;
            }
            TabPaint.SettingsManager.Instance.Save();
        }

        private void RestoreWindowBounds()
        {
            var settings = TabPaint.SettingsManager.Instance.Current;
            var workArea = SystemParameters.WorkArea;

            // 1. 确定初始尺寸
            double winWidth = Math.Max(settings.WindowWidth, AppConsts.WindowMinSize);
            double winHeight = Math.Max(settings.WindowHeight, AppConsts.WindowMinSize);
            if (winWidth > workArea.Width) winWidth = workArea.Width;
            if (winHeight > workArea.Height) winHeight = workArea.Height;

            this.Width = winWidth;
            this.Height = winHeight;

            // 2. 查找参照窗口
            var existingWindows = Application.Current.Windows.OfType<MainWindow>()
                .Where(w => w != this && w.IsVisible).ToList();

            if (existingWindows.Count > 0)
            {
                // 层叠创建逻辑
                var refWindow = existingWindows.Last();
                double offset = 30;
                double newLeft, newTop;

                if (refWindow.WindowState == WindowState.Maximized)
                {
                    newLeft = refWindow.RestoreBounds.Left + offset;
                    newTop = refWindow.RestoreBounds.Top + offset;
                }
                else
                {
                    newLeft = refWindow.Left + offset;
                    newTop = refWindow.Top + offset;
                }

                // 回绕处理：超出屏幕边界则返回左上角区域
                if (newLeft + winWidth > workArea.Right || newTop + winHeight > workArea.Bottom) { newLeft = workArea.Left + offset; newTop = workArea.Top + offset; }
                if (newLeft < workArea.Left) newLeft = workArea.Left;
                if (newTop < workArea.Top) newTop = workArea.Top;

                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Left = newLeft;
                this.Top = newTop;
                this.WindowState = WindowState.Normal;
            }
            else
            {
                // 首个窗口逻辑：恢复上次位置或居中
                if (settings.WindowLeft != AppConsts.UninitializedWindowPosition &&
                    settings.WindowTop != AppConsts.UninitializedWindowPosition)
                {
                    this.WindowStartupLocation = WindowStartupLocation.Manual;
                    this.Left = settings.WindowLeft;
                    this.Top = settings.WindowTop;
                    
                    // 额外检查：确保恢复的位置在当前可见区域内（防止多显示器断开后的问题）
                    if (this.Left + 100 > workArea.Right || this.Top + 100 > workArea.Bottom ||
                        this.Left + this.Width < workArea.Left || this.Top < workArea.Top)
                    {
                        this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    }
                }
                else
                {
                    this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                if (settings.WindowState == (int)WindowState.Maximized)
                {
                    this.WindowState = WindowState.Maximized;
                }
            }
        }

        private void RestoreAppState()
        {
            try
            {
                var settings = TabPaint.SettingsManager.Instance.Current;
                this.Topmost = settings.IsWindowTopmost;

                // 初始化本地画笔参数
                var existingWindows = Application.Current.Windows.OfType<MainWindow>()
                    .Where(w => w != this && w.IsVisible).ToList();

                ITool targetTool = null;
                BrushStyle targetStyle = settings.LastBrushStyle;
                string lastToolName = settings.LastToolName;

                // 寻找参照窗口：优先选最后获得焦点的，其次选其他窗口中第一个
                MainWindow refWindow = null;
                if (_lastFocusedInstance != null && _lastFocusedInstance != this && _lastFocusedInstance.IsVisible)
                {
                    refWindow = _lastFocusedInstance;
                }
                else
                {
                    refWindow = existingWindows.FirstOrDefault();
                }

                if (refWindow != null)
                {
                    // 1. 继承画笔参数
                    if (refWindow.LocalPerToolSettings != null)
                    {
                        this.LocalPerToolSettings = refWindow.LocalPerToolSettings.ToDictionary(
                            k => k.Key, v => v.Value.Clone());
                        this.CurrentToolKey = refWindow.CurrentToolKey;
                    }

                    // 2. 继承源窗口当前的工具和样式
                    if (refWindow._router?.CurrentTool != null)
                    {
                        lastToolName = refWindow._router.CurrentTool.GetType().Name;
                    }
                    if (refWindow._ctx != null)
                    {
                        targetStyle = refWindow._ctx.PenStyle;
                    }
                }
                else
                {
                    // 首个窗口：从全局设置读取
                    if (settings.PerToolSettings != null)
                    {
                        this.LocalPerToolSettings = settings.PerToolSettings.ToDictionary(
                            k => k.Key, v => v.Value.Clone());
                    }
                    this.CurrentToolKey = settings.CurrentToolKey;
                }

                // 同步笔刷大小和透明度到当前上下文
                if (_ctx != null)
                {
                    _ctx.PenThickness = this.PenThickness;
                    _ctx.PenOpacity = this.PenOpacity;
                }
                
                // 通知 UI 更新
                OnPropertyChanged(nameof(PenThickness));
                OnPropertyChanged(nameof(PenOpacity));

                // 准备对应的工具实例
                switch (lastToolName)
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

                if (MainImageBar != null)
                {
                    MainImageBar.IsCompactMode = settings.IsImageBarCompact;
                }

                // 最终应用工具
                if (_ctx != null) _ctx.PenStyle = targetStyle;
                if (_router != null)
                {
                    if (lastToolName == "PenTool")
                    {
                        // 如果是画笔工具，通过 SetBrushStyle 激活对应的子样式（如橡皮擦）
                        SetBrushStyle(targetStyle);
                    }
                    else if (targetTool != null)
                    {
                        _router.SetTool(targetTool);
                    }
                }

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RestoreAppState Error: {ex.Message}");
            }
        }

    }
  
}
