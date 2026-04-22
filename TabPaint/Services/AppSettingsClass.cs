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
    public enum RmbgModelType
    {
        Rmbg14,
        Rmbg20
    }

    public class AppSettings : INotifyPropertyChanged
    {
        private AppLanguage _language = GetDefaultLanguage();

        private static AppLanguage GetDefaultLanguage()
        {
            try
            {
                string cultureName = CultureInfo.CurrentUICulture.Name;
                if (cultureName.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
                {
                    return AppLanguage.Japanese;
                }

                if (cultureName.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                {
                    if (cultureName.Contains("Hant", StringComparison.OrdinalIgnoreCase)
                        || cultureName.Contains("TW", StringComparison.OrdinalIgnoreCase)
                        || cultureName.Contains("HK", StringComparison.OrdinalIgnoreCase)
                        || cultureName.Contains("MO", StringComparison.OrdinalIgnoreCase))
                    {
                        return AppLanguage.ChineseTraditional;
                    }

                    return AppLanguage.ChineseSimplified;
                }
            }
            catch
            {
                // 忽略异常，默认返回英文或中文
            }
            return AppLanguage.English;
        }

        [JsonPropertyName("language")]
        public AppLanguage Language
        {
            get => _language;
            set
            {
                if (_language != value)
                {
                    _language = value;
                    OnPropertyChanged();
                }
            }
        }
        private SelectionClearMode _selectionClearMode = SelectionClearMode.White; // 默认白底
        [JsonPropertyName("selection_clear_mode")]
        public SelectionClearMode SelectionClearMode
        {
            get => _selectionClearMode;
            set
            {
                if (_selectionClearMode != value)
                {
                    _selectionClearMode = value;
                    OnPropertyChanged();
                }
            }
        }
        private bool _isFirstRun = true; // 默认为 true
        [JsonPropertyName("is_first_run")]
        public bool IsFirstRun
        {
            get => _isFirstRun;
            set
            {
                if (_isFirstRun != value)
                {
                    _isFirstRun = value;
                    OnPropertyChanged(nameof(IsFirstRun));
                }
            }
        }

        private string _lastLaunchedVersion = string.Empty;
        [JsonPropertyName("last_launched_version")]
        public string LastLaunchedVersion
        {
            get => _lastLaunchedVersion;
            set
            {
                if (_lastLaunchedVersion != value)
                {
                    _lastLaunchedVersion = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _newestInstalledVersion = string.Empty;
        [JsonPropertyName("newest_installed_version")]
        public string NewestInstalledVersion
        {
            get => _newestInstalledVersion;
            set
            {
                if (_newestInstalledVersion != value)
                {
                    _newestInstalledVersion = value;
                    OnPropertyChanged();
                }
            }
        }
        private bool _isImageBarCompact = true; // 默认为 false (展开状态)

        [JsonPropertyName("is_image_bar_compact")]
        public bool IsImageBarCompact
        {
            get => _isImageBarCompact;
            set
            {
                if (_isImageBarCompact != value)
                {
                    _isImageBarCompact = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isStatusCommandBarExpanded = false;

        [JsonPropertyName("is_status_command_bar_expanded")]
        public bool IsStatusCommandBarExpanded
        {
            get => _isStatusCommandBarExpanded;
            set
            {
                if (_isStatusCommandBarExpanded != value)
                {
                    _isStatusCommandBarExpanded = value;
                    OnPropertyChanged();
                }
            }
        }
        private bool _alwaysShowTabCloseButton = false;

        [JsonPropertyName("always_show_tab_close_button")]
        public bool AlwaysShowTabCloseButton
        {
            get => _alwaysShowTabCloseButton;
            set
            {
                if (_alwaysShowTabCloseButton != value)
                {
                    _alwaysShowTabCloseButton = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _startInViewMode = false; // 默认关闭，即启动为画图模式

        [JsonPropertyName("start_in_view_mode")]
        public bool StartInViewMode
        {
            get => _startInViewMode;
            set
            {
                if (_startInViewMode != value)
                {
                    _startInViewMode = value;
                    OnPropertyChanged();
                }
            }
        }
        private MouseWheelMode _viewMouseWheelMode = MouseWheelMode.Zoom;

        [JsonPropertyName("view_mouse_wheel_mode")]
        public MouseWheelMode ViewMouseWheelMode
        {
            get => _viewMouseWheelMode;
            set
            {
                if (_viewMouseWheelMode != value)
                {
                    _viewMouseWheelMode = value;
                    OnPropertyChanged();
                }
            }
        }
        private Dictionary<string, ToolSettingsModel> _perToolSettings;

        [JsonPropertyName("per_tool_settings")]
        public Dictionary<string, ToolSettingsModel> PerToolSettings
        {
            get
            {
                if (_perToolSettings == null) _perToolSettings = GetDefaultToolSettings();
                return _perToolSettings;
            }
            set
            {
                _perToolSettings = value;
                OnPropertyChanged();
            }
        }
        private bool _isTextToolbarExpanded = false;

        [JsonPropertyName("is_text_toolbar_expanded")]
        public bool IsTextToolbarExpanded
        {
            get => _isTextToolbarExpanded;
            set
            {
                if (_isTextToolbarExpanded != value)
                {
                    _isTextToolbarExpanded = value;
                    OnPropertyChanged();
                }
            }
        }
        private bool _enableIccColorCorrection = false;

        [JsonPropertyName("enable_icc_color_correction")]
        public bool EnableIccColorCorrection
        {
            get => _enableIccColorCorrection;
            set
            {
                if (_enableIccColorCorrection != value)
                {
                    _enableIccColorCorrection = value;
                    OnPropertyChanged();
                }
            }
        }
        private string _currentToolKey = "Pen_Round";
        [JsonIgnore]
        public string CurrentToolKey
        {
            get => _currentToolKey;
            set
            {
                if (_currentToolKey != value)
                {
                    _currentToolKey = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PenThickness));
                    OnPropertyChanged(nameof(PenOpacity));
                }
            }
        }
        [JsonIgnore] // 不再直接序列化这个值，而是序列化字典
        public double PenThickness
        {
            get
            {
                if (_currentToolKey == "Pen_Pencil") return 1.0;

                if (PerToolSettings.TryGetValue(_currentToolKey, out var settings))
                {
                    return settings.Thickness;
                }
                return 25.0; // Fallback
            }
            set
            {
                // 特殊处理：铅笔不允许修改粗细
                if (_currentToolKey == "Pen_Pencil") return;

                if (!PerToolSettings.ContainsKey(_currentToolKey))
                {
                    PerToolSettings[_currentToolKey] = new ToolSettingsModel();
                }

                if (Math.Abs(PerToolSettings[_currentToolKey].Thickness - value) > 0.01)
                {
                    PerToolSettings[_currentToolKey].Thickness = value;
                    OnPropertyChanged();
                }
            }
        }
        [JsonIgnore]
        public double PenOpacity
        {
            get
            {
                if (PerToolSettings.TryGetValue(_currentToolKey, out var settings))
                {
                    return settings.Opacity;
                }
                return 1.0;
            }
            set
            {
                if (!PerToolSettings.ContainsKey(_currentToolKey))
                {
                    PerToolSettings[_currentToolKey] = new ToolSettingsModel();
                }

                if (Math.Abs(PerToolSettings[_currentToolKey].Opacity - value) > 0.001)
                {
                    PerToolSettings[_currentToolKey].Opacity = value;
                    OnPropertyChanged();
                }
            }
        }
        private Dictionary<string, ToolSettingsModel> GetDefaultToolSettings()
        {
            var dict = new Dictionary<string, ToolSettingsModel>();

            // 为每种笔刷样式预设值
            dict["Pen_Pencil"] = new ToolSettingsModel { Thickness = 1.0, Opacity = 1.0 };
            dict["Pen_Round"] = new ToolSettingsModel { Thickness = 25.0, Opacity = 1.0 };
            dict["Pen_Square"] = new ToolSettingsModel { Thickness = 25.0, Opacity = 1.0 };
            dict["Pen_Highlighter"] = new ToolSettingsModel { Thickness = 20.0, Opacity = 0.5 }; // 荧光笔默认半透明
            dict["Pen_Eraser"] = new ToolSettingsModel { Thickness = 25.0, Opacity = 1.0 };
            dict["Pen_Watercolor"] = new ToolSettingsModel { Thickness = 30.0, Opacity = 0.8 };
            dict["Pen_Crayon"] = new ToolSettingsModel { Thickness = 25.0, Opacity = 1.0 };
            dict["Pen_Spray"] = new ToolSettingsModel { Thickness = 40.0, Opacity = 0.8 };
            dict["Pen_Mosaic"] = new ToolSettingsModel { Thickness = 20.0, Opacity = 1.0 };
            dict["Pen_Brush"] = new ToolSettingsModel { Thickness = 8.0, Opacity = 1.0 };

            // 形状工具预留
            dict["Shape"] = new ToolSettingsModel { Thickness = 3.0, Opacity = 1.0 };
            return dict;
        }
        private AppTheme _themeMode = AppTheme.System; // 默认跟随系统

        [JsonPropertyName("theme_mode")]
        public AppTheme ThemeMode
        {
            get => _themeMode;
            set
            {
                if (_themeMode != value)
                {
                    _themeMode = value;
                    OnPropertyChanged();
                }
            }
        }
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
        private bool _isWindowTopmost = false;

        [JsonPropertyName("is_window_topmost")]
        public bool IsWindowTopmost
        {
            get => _isWindowTopmost;
            set
            {
                if (_isWindowTopmost != value)
                {
                    _isWindowTopmost = value;
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
                }
            }
        }
        private bool _enableClipboardMonitor = false; // 默认关闭
        [JsonPropertyName("last_tool_name")]
        public string LastToolName { get; set; } = "PenTool"; // 默认为笔

        [JsonPropertyName("last_brush_style")]
        public BrushStyle LastBrushStyle { get; set; } = BrushStyle.Round;

        private bool _isSelectionRotateEnabled = false;
        [JsonPropertyName("is_selection_rotate_enabled")]
        public bool IsSelectionRotateEnabled
        {
            get => _isSelectionRotateEnabled;
            set
            {
                if (_isSelectionRotateEnabled != value)
                {
                    _isSelectionRotateEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        private List<string> _pinnedSettingsTags = new List<string>();
        [JsonPropertyName("pinned_settings_tags")]
        public List<string> PinnedSettingsTags
        {
            get => _pinnedSettingsTags ??= new List<string>();
            set
            {
                if (_pinnedSettingsTags != value)
                {
                    _pinnedSettingsTags = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private int _maxRecentFiles = 10;
        [JsonPropertyName("max_recent_files")]
        public int MaxRecentFiles
        {
            get => _maxRecentFiles;
            set
            {
                if (_maxRecentFiles != value)
                {
                    _maxRecentFiles = value;
                    OnPropertyChanged();
                    if (RecentFiles != null && RecentFiles.Count > _maxRecentFiles)
                    {
                        RecentFiles = RecentFiles.Take(_maxRecentFiles).ToList();
                    }
                }
            }
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
        private Dictionary<string, ShortcutItem> _shortcuts;

        [JsonPropertyName("shortcuts")]
        public Dictionary<string, ShortcutItem> Shortcuts
        {
            get
            {
                if (_shortcuts == null) _shortcuts = GetDefaultShortcuts();
                return _shortcuts;
            }
            set
            {
                _shortcuts = value;
                OnPropertyChanged();
                TabPaint.Services.ShortcutProvider.Instance.NotifyChanged();
            }
        }
        private bool _showRulers = true; // 默认不显示

        [JsonPropertyName("show_rulers")]
        public bool ShowRulers
        {
            get => _showRulers;
            set
            {
                if (_showRulers != value)
                {
                    _showRulers = value;
                    OnPropertyChanged();
                }
            }
        }
        private AppResamplingMode _resamplingMode = AppResamplingMode.Auto;
        [JsonPropertyName("resampling_mode")]
        public AppResamplingMode ResamplingMode
        {
            get => _resamplingMode;
            set
            {
                if (_resamplingMode != value)
                {
                    _resamplingMode = value;
                    OnPropertyChanged();
                }
            }
        }
        private double _viewInterpolationThreshold = AppConsts.DefaultViewInterpolationThreshold;
        [JsonPropertyName("view_interpolation_threshold")]
        public double ViewInterpolationThreshold
        {
            get => _viewInterpolationThreshold;
            set
            {
                if (Math.Abs(_viewInterpolationThreshold - value) > 0.1)
                {
                    _viewInterpolationThreshold = value;
                    OnPropertyChanged();
                }
            }
        }
        private List<string> _customColors = new List<string>();

        [JsonPropertyName("custom_colors")]
        public List<string> CustomColors
        {
            get => _customColors;
            set
            {
                if (_customColors != value)
                {
                    _customColors = value;
                    OnPropertyChanged();
                }
            }
        }
        private double _paintInterpolationThreshold = AppConsts.DefaultPaintInterpolationThreshold;
        [JsonPropertyName("paint_interpolation_threshold")]
        public double PaintInterpolationThreshold
        {
            get => _paintInterpolationThreshold;
            set
            {
                if (Math.Abs(_paintInterpolationThreshold - value) > 0.1)
                {
                    _paintInterpolationThreshold = value;
                    OnPropertyChanged();
                }
            }
        }
        [JsonPropertyName("window_width")]
        public double WindowWidth { get; set; } = AppConsts.DefaultWindowWidth; // 默认宽

        [JsonPropertyName("window_height")]
        public double WindowHeight { get; set; } = AppConsts.DefaultWindowHeight; // 默认高

        [JsonPropertyName("window_left")]
        public double WindowLeft { get; set; } = AppConsts.UninitializedWindowPosition; // 默认无效值，用于判断是否是首次启动

        [JsonPropertyName("window_top")]
        public double WindowTop { get; set; } = AppConsts.UninitializedWindowPosition;
        [JsonPropertyName("window_state")] // 0: Normal, 1: Minimized, 2: Maximized
        public int WindowState { get; set; } = 0;

        private bool _autoLoadFolderImages = false;

        [JsonPropertyName("auto_load_folder_images")]
        public bool AutoLoadFolderImages
        {
            get => _autoLoadFolderImages;
            set
            {
                if (_autoLoadFolderImages != value)
                {
                    _autoLoadFolderImages = value;
                    OnPropertyChanged();
                }
            }
        }
        private bool _viewShowTransparentGrid = false; // 默认隐藏透明灰白格子(即显示白底)

        [JsonPropertyName("view_show_transparent_grid")]
        public bool ViewShowTransparentGrid
        {
            get => _viewShowTransparentGrid;
            set
            {
                if (_viewShowTransparentGrid != value)
                {
                    _viewShowTransparentGrid = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _skipResetConfirmation = false; // 默认需要确认

        [JsonPropertyName("skip_reset_confirmation")]
        public bool SkipResetConfirmation
        {
            get => _skipResetConfirmation;
            set
            {
                if (_skipResetConfirmation != value)
                {
                    _skipResetConfirmation = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _discardAllOnExit = false; // 默认关闭

        [JsonPropertyName("discard_all_on_exit")]
        public bool DiscardAllOnExit
        {
            get => _discardAllOnExit;
            set
            {
                if (_discardAllOnExit != value)
                {
                    _discardAllOnExit = value;
                    OnPropertyChanged();
                }
            }
        }
        private bool _autoPopupOnClipboardImage = false; // 默认不弹出

        [JsonPropertyName("auto_popup_on_clipboard_image")]
        public bool AutoPopupOnClipboardImage
        {
            get => _autoPopupOnClipboardImage;
            set
            {
                if (_autoPopupOnClipboardImage != value)
                {
                    _autoPopupOnClipboardImage = value;
                    OnPropertyChanged();
                }
            }
        }
        private bool _enableFileDeleteInPaintMode = false; // 默认关闭

        [JsonPropertyName("enable_file_delete_in_paint_mode")]
        public bool EnableFileDeleteInPaintMode
        {
            get => _enableFileDeleteInPaintMode;
            set
            {
                if (_enableFileDeleteInPaintMode != value)
                {
                    _enableFileDeleteInPaintMode = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _aiOcrPromptShown = false;
        [JsonPropertyName("ai_ocr_prompt_shown")]
        public bool AiOcrPromptShown
        {
            get => _aiOcrPromptShown;
            set
            {
                if (_aiOcrPromptShown != value)
                {
                    _aiOcrPromptShown = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _enableAiOcr = true;
        [JsonPropertyName("enable_ai_ocr")]
        public bool EnableAiOcr
        {
            get => _enableAiOcr;
            set
            {
                if (_enableAiOcr != value)
                {
                    _enableAiOcr = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _aiImageApiBaseUrl = AppConsts.AiImageDefaultApiBaseUrl;
        [JsonPropertyName("ai_image_api_base_url")]
        public string AiImageApiBaseUrl
        {
            get => _aiImageApiBaseUrl;
            set
            {
                if (_aiImageApiBaseUrl != value)
                {
                    _aiImageApiBaseUrl = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _aiImageApiKey = string.Empty;
        [JsonPropertyName("ai_image_api_key")]
        public string AiImageApiKey
        {
            get => _aiImageApiKey;
            set
            {
                if (_aiImageApiKey != value)
                {
                    _aiImageApiKey = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _aiImageModel = AppConsts.AiImageDefaultModel;
        [JsonPropertyName("ai_image_model")]
        public string AiImageModel
        {
            get => _aiImageModel;
            set
            {
                if (_aiImageModel != value)
                {
                    _aiImageModel = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _aiModelDefaultSaveDir = AppConsts.AiModelDefaultSaveDir;
        [JsonPropertyName("ai_model_default_save_dir")]
        public string AiModelDefaultSaveDir
        {
            get => _aiModelDefaultSaveDir;
            set
            {
                if (_aiModelDefaultSaveDir != value)
                {
                    _aiModelDefaultSaveDir = value;
                    OnPropertyChanged();
                }
            }
        }

        private OcrResultAction _ocrResultAction = OcrResultAction.DirectCopy;
        [JsonPropertyName("ocr_result_action")]
        public OcrResultAction OcrResultAction
        {
            get => _ocrResultAction;
            set
            {
                if (_ocrResultAction != value)
                {
                    _ocrResultAction = value;
                    OnPropertyChanged();
                }
            }
        }

        private RmbgModelType _rmbgModel = RmbgModelType.Rmbg14;
        [JsonPropertyName("rmbg_model")]
        public RmbgModelType RmbgModel
        {
            get => _rmbgModel;
            set
            {
                if (_rmbgModel != value)
                {
                    _rmbgModel = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isProfessionalMode = false;

        [JsonPropertyName("is_professional_mode")]
        public bool IsProfessionalMode
        {
            get => _isProfessionalMode;
            set
            {
                if (_isProfessionalMode != value)
                {
                    _isProfessionalMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsTextToolProMode));
                    OnPropertyChanged(nameof(IsShapeToolProMode));
                }
            }
        }

        [JsonIgnore]
        public bool IsTextToolProMode
        {
            get => IsProfessionalMode;
            set => IsProfessionalMode = value;
        }

        [JsonIgnore]
        public bool IsShapeToolProMode
        {
            get => IsProfessionalMode;
            set => IsProfessionalMode = value;
        }

        private bool _viewUseDarkCanvasBackground = true; // 默认开启深灰色背景(#1A1A1A)

        [JsonPropertyName("view_use_dark_canvas_background")]
        public bool ViewUseDarkCanvasBackground
        {
            get => _viewUseDarkCanvasBackground;
            set
            {
                if (_viewUseDarkCanvasBackground != value)
                {
                    _viewUseDarkCanvasBackground = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _showBirdEyeInViewMode = true;

        [JsonPropertyName("show_bird_eye_in_view_mode")]
        public bool ShowBirdEyeInViewMode
        {
            get => _showBirdEyeInViewMode;
            set
            {
                if (_showBirdEyeInViewMode != value)
                {
                    _showBirdEyeInViewMode = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _viewLogoMenuHintShown = false;
        [JsonPropertyName("view_logo_menu_hint_shown")]
        public bool ViewLogoMenuHintShown
        {
            get => _viewLogoMenuHintShown;
            set
            {
                if (_viewLogoMenuHintShown != value)
                {
                    _viewLogoMenuHintShown = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _themeAccentColor = AppConsts.DefaultThemeAccentColor; // 默认蓝色

        [JsonPropertyName("theme_accent_color")]
        public string ThemeAccentColor
        {
            get => _themeAccentColor;
            set
            {
                if (_themeAccentColor != value)
                {
                    _themeAccentColor = value;
                    OnPropertyChanged();
                }
            }
        }
        private int _maxGlobalUndoSteps = 1000;
        [JsonPropertyName("max_global_undo_steps")]
        public int MaxGlobalUndoSteps
        {
            get => _maxGlobalUndoSteps;
            set
            {
                if (_maxGlobalUndoSteps != value)
                {
                    _maxGlobalUndoSteps = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _useWin10StyleOnWin11 = false;
        [JsonPropertyName("use_win10_style_on_win11")]
        public bool UseWin10StyleOnWin11
        {
            get => _useWin10StyleOnWin11;
            set
            {
                if (_useWin10StyleOnWin11 != value)
                {
                    _useWin10StyleOnWin11 = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DevTools_UseWin10StyleOnWin11));
                    NotifyWin10StyleChanged();
                }
            }
        }

        private bool _useNewStyle = false;
        [JsonIgnore]
        public bool UseNewStyle
        {
            get => _useNewStyle;
            set
            {
                if (_useNewStyle != value)
                {
                    _useNewStyle = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public bool DevTools_UseWin10StyleOnWin11
        {
            get => UseWin10StyleOnWin11;
            set => UseWin10StyleOnWin11 = value;
        }

        private void NotifyWin10StyleChanged()
        {
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                foreach (Window window in Application.Current.Windows)
                {
                    MicaAcrylicManager.ApplyEffect(window);
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private int _maxUndoMemoryMB = 2048;
        [JsonPropertyName("max_undo_memory_mb")]
        public int MaxUndoMemoryMB
        {
            get => _maxUndoMemoryMB;
            set
            {
                if (_maxUndoMemoryMB != value)
                {
                    _maxUndoMemoryMB = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _defaultBlankCanvasWidth = AppConsts.DefaultBlankCanvasWidth;
        [JsonPropertyName("default_blank_canvas_width")]
        public int DefaultBlankCanvasWidth
        {
            get => _defaultBlankCanvasWidth;
            set
            {
                if (_defaultBlankCanvasWidth != value)
                {
                    _defaultBlankCanvasWidth = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _defaultBlankCanvasHeight = AppConsts.DefaultBlankCanvasHeight;
        [JsonPropertyName("default_blank_canvas_height")]
        public int DefaultBlankCanvasHeight
        {
            get => _defaultBlankCanvasHeight;
            set
            {
                if (_defaultBlankCanvasHeight != value)
                {
                    _defaultBlankCanvasHeight = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isCompactColorPicker = false;
        [JsonPropertyName("is_compact_color_picker")]
        public bool IsCompactColorPicker
        {
            get => _isCompactColorPicker;
            set
            {
                if (_isCompactColorPicker != value)
                {
                    _isCompactColorPicker = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _performanceScore = -1;
        [JsonPropertyName("performance_score")]
        public int PerformanceScore
        {
            get => _performanceScore;
            set
            {
                if (_performanceScore != value)
                {
                    _performanceScore = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _svgDecodeSize = 2048;
        [JsonPropertyName("svg_decode_size")]
        public int SvgDecodeSize
        {
            get => _svgDecodeSize;
            set
            {
                if (_svgDecodeSize != value)
                {
                    _svgDecodeSize = value;
                    OnPropertyChanged();
                }
            }
        }

        private DateTime _lastBenchmarkDate = DateTime.MinValue;
        [JsonPropertyName("last_benchmark_date")]
        public DateTime LastBenchmarkDate
        {
            get => _lastBenchmarkDate;
            set
            {
                if (_lastBenchmarkDate != value)
                {
                    _lastBenchmarkDate = value;
                    OnPropertyChanged();
                }
            }
        }
        private Dictionary<string, ShortcutItem> GetDefaultShortcuts()
        {

            var defaults = new Dictionary<string, ShortcutItem>
    {
        // 0. 基础文件
        { "File.New",            new ShortcutItem { Key = Key.N, Modifiers = ModifierKeys.Control } },
        { "File.Open",           new ShortcutItem { Key = Key.O, Modifiers = ModifierKeys.Control } },
        { "File.Save",           new ShortcutItem { Key = Key.S, Modifiers = ModifierKeys.Control } },
        { "File.SaveAs",         new ShortcutItem { Key = Key.S, Modifiers = ModifierKeys.Control | ModifierKeys.Shift } },
        { "File.Print",          new ShortcutItem { Key = Key.P, Modifiers = ModifierKeys.Control } },


        // 1. 全局/视图功能
        { "View.PrevImage",      new ShortcutItem { Key = Key.Left, Modifiers = ModifierKeys.None } },
        { "View.NextImage",      new ShortcutItem { Key = Key.Right, Modifiers = ModifierKeys.None } },
        { "View.RotateLeft",     new ShortcutItem { Key = Key.L, Modifiers = ModifierKeys.Control } },
        { "View.RotateRight",    new ShortcutItem { Key = Key.R, Modifiers = ModifierKeys.Control } },
        { "View.FullScreen",     new ShortcutItem { Key = Key.F11, Modifiers = ModifierKeys.None } },
        { "View.VerticalFlip",   new ShortcutItem { Key = Key.V, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 自动色阶
        { "View.HorizontalFlip",       new ShortcutItem { Key = Key.H, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 调整大小
        { "View.ZoomIn",         new ShortcutItem { Key = Key.OemPlus, Modifiers = ModifierKeys.Control } },
        { "View.ZoomOut",        new ShortcutItem { Key = Key.OemMinus, Modifiers = ModifierKeys.Control } },
        { "View.ToggleMode",           new ShortcutItem { Key = Key.Tab, Modifiers = ModifierKeys.None } },
        { "View.ToggleImageBar",       new ShortcutItem { Key = Key.Tab, Modifiers = ModifierKeys.Shift } },
        // 2. 高级工具 (Ctrl + Alt 系列)
        { "Tool.ClipMonitor",    new ShortcutItem { Key = Key.P, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 剪贴板监听开关
        { "Tool.RemoveBg",       new ShortcutItem { Key = Key.D1, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 抠图
        { "Tool.ChromaKey",      new ShortcutItem { Key = Key.D2, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } },
        { "Tool.OCR",            new ShortcutItem { Key = Key.D3, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } },
        { "Tool.ScreenPicker",   new ShortcutItem { Key = Key.D4, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 屏幕取色
        { "Tool.CopyColorCode",  new ShortcutItem { Key = Key.D5, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } },
        { "Tool.AutoCrop",       new ShortcutItem { Key = Key.D6, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } },
        { "Tool.AddBorder",      new ShortcutItem { Key = Key.D7, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } },
        { "Tool.AiUpscale",      new ShortcutItem { Key = Key.D8, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } },
        { "Tool.AiOcr",          new ShortcutItem { Key = Key.D9, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } },
    

        // 3. 特殊操作 (Ctrl + Shift 系列)
        { "File.OpenWorkspace",  new ShortcutItem { Key = Key.O, Modifiers = ModifierKeys.Control | ModifierKeys.Shift } }, // 打开工作区
        { "File.PasteNewTab",    new ShortcutItem { Key = Key.V, Modifiers = ModifierKeys.Control | ModifierKeys.Shift } }, // 粘贴为新标签
        
        // 4. 编辑
        { "Edit.Undo",           new ShortcutItem { Key = Key.Z, Modifiers = ModifierKeys.Control } },
        { "Edit.Redo",           new ShortcutItem { Key = Key.Y, Modifiers = ModifierKeys.Control } },
        { "Edit.Copy",           new ShortcutItem { Key = Key.C, Modifiers = ModifierKeys.Control } },
        { "Edit.Cut",            new ShortcutItem { Key = Key.X, Modifiers = ModifierKeys.Control } },
        { "Edit.Paste",          new ShortcutItem { Key = Key.V, Modifiers = ModifierKeys.Control } },
        { "Edit.Bold",           new ShortcutItem { Key = Key.B, Modifiers = ModifierKeys.Control } },
        { "Edit.Italic",         new ShortcutItem { Key = Key.I, Modifiers = ModifierKeys.Control } },
        { "Edit.Underline",      new ShortcutItem { Key = Key.U, Modifiers = ModifierKeys.Control } },
        { "Edit.Strikethrough",  new ShortcutItem { Key = Key.T, Modifiers = ModifierKeys.Control | ModifierKeys.Shift } },

          // === 5. 基础绘图工具  ===
        { "Tool.SwitchToSelect", new ShortcutItem { Key = Key.D1, Modifiers = ModifierKeys.Control } }, // 选择
        { "Tool.SwitchToPen",    new ShortcutItem { Key = Key.D2, Modifiers = ModifierKeys.Control } }, // 铅笔/画笔
        { "Tool.SwitchToPick",   new ShortcutItem { Key = Key.D3, Modifiers = ModifierKeys.Control } }, // 取色
        { "Tool.SwitchToEraser", new ShortcutItem { Key = Key.D4, Modifiers = ModifierKeys.Control } }, // 橡皮
        { "Tool.SwitchToFill",   new ShortcutItem { Key = Key.D5, Modifiers = ModifierKeys.Control } }, // 填充
        { "Tool.SwitchToText",   new ShortcutItem { Key = Key.D6, Modifiers = ModifierKeys.Control } }, // 文字
        { "Tool.SwitchToBrush",  new ShortcutItem { Key = Key.D7, Modifiers = ModifierKeys.Control } }, // 画刷菜单
        { "Tool.SwitchToShape",  new ShortcutItem { Key = Key.D8, Modifiers = ModifierKeys.Control } }, // 形状菜单(通常切到默认形
        { "Tool.QuickFormat",    new ShortcutItem { Key = Key.Q, Modifiers = ModifierKeys.Control } },

          // === 6. 效果菜单 (Effect) ===
        { "Effect.Brightness",   new ShortcutItem { Key = Key.R, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 亮度/对比度
        { "Effect.Temperature",  new ShortcutItem { Key = Key.T, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 色温/色调
        { "Effect.Grayscale",    new ShortcutItem { Key = Key.Y, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 黑白
        { "Effect.Invert",       new ShortcutItem { Key = Key.U, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 反色
        { "Effect.AutoLevels",   new ShortcutItem { Key = Key.I, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 自动色阶
        { "Effect.Resize",       new ShortcutItem { Key = Key.O, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 调整大小
    };
            return defaults;
        }
        public void ResetShortcutsToDefault()
        {
            var defaults = GetDefaultShortcuts();
            Shortcuts = defaults;
            OnPropertyChanged(nameof(Shortcuts));
        }
    }
}
