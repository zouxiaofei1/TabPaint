
//
//AppConsts.cs
//项目全局常量定义，包含版本信息、Win32消息、UI参数、路径及AI模型配置等。
//
using System;
using System.IO;

namespace TabPaint
{
    public static class AppConsts
    {
        // --- 版本信息 ---
        public const string ProgramVersion = "v0.9.7.2";

        // --- Win32 消息常量 ---
        public const int WM_NCHITTEST = 0x0084;
        public const int WM_CLIPBOARDUPDATE = 0x031D;
        public const int WM_MOUSEHWHEEL = 0x020E;
        public const int WM_NCACTIVATE = 0x0086;

        // --- HitTest 区域常量 ---
        public const int HTCLIENT = 1;
        public const int HTLEFT = 10;
        public const int HTRIGHT = 11;
        public const int HTTOP = 12;
        public const int HTTOPLEFT = 13;
        public const int HTTOPRIGHT = 14;
        public const int HTBOTTOM = 15;
        public const int HTBOTTOMLEFT = 16;
        public const int HTBOTTOMRIGHT = 17;

        // --- 缩放与动画参数 ---
        public const double DefaultZoomStep = 0.1;
        public const double ZoomTimes = 1.1;
        public const double MinZoom = 0.05;
        public const double MaxZoom = 50.0;
        public const double ZoomSnapThreshold = 0.001;
        public const double ZoomLerpFactor = 1.0;
        public const double WheelScrollFactor = 0.4;

        // --- UI 与交互参数 ---
        public const int ToastDuration = 1500;
        public const int ToastFadeInMs = 200;
        public const int ToastFadeOutMs = 500;
        public const string InternalClipboardFormat = "TabPaint_Internal_Copy_Marker";

        // Timer intervals and delays
        public const int DeleteCommitTimerSeconds = 2;
        public const int DragTempCleanupDelaySeconds = 150;
        public const int NavGapLevel1Ms = 5000;
        public const int NavGapLevel2Ms = 10000;
        public const int ClipboardCooldownMs = 1000;
        public const double DefaultWindowWidth = 850;
        public const double DefaultWindowHeight = 700;
        public const double WindowMinSize = 200;
        public const int MaxThumbnailCacheCount = 300;
        public const int WindowCornerArea = 16;
        public const int WindowSideArea = 8;
        public const double DoubleClickTimeThreshold = 2.0;
        public const int HighPerformanceThreshold = 8;

        // --- 缩略图参数 ---
        public const int DefaultThumbnailWidth = 100;
        public const int DefaultThumbnailHeight = 60;

        // --- 目录与路径 ---
        public static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TabPaint");
        public static readonly string CacheDir = Path.Combine(AppDataFolder, "Cache");
        public static readonly string FavoriteDir = Path.Combine(CacheDir, "Favorite");
        public static readonly string PluginsDir = Path.Combine(CacheDir, "Plugins");
        public static readonly string DragTempDir = Path.Combine(CacheDir, "DragTemp");
        public static readonly string SessionPath = Path.Combine(AppDataFolder, "session.bin");
        public static readonly string LegacySessionPath = Path.Combine(AppDataFolder, "session.json");
        public static readonly string ClipboardCacheDir = Path.Combine(CacheDir, "Clipboard");
        public static readonly string PyOcrRootDir = Path.Combine(CacheDir, "pyocr");
        public static readonly string PyOcrRuntimeDir = Path.Combine(PyOcrRootDir, "python");
        public static readonly string PyOcrDownloadsDir = Path.Combine(PyOcrRootDir, "downloads");
        public static readonly string PyOcrModelsDir = Path.Combine(PyOcrRootDir, "models");
        public static readonly string PyOcrLogsDir = Path.Combine(PyOcrRootDir, "logs");

        // --- Python OCR 运行时配置 ---
        public const string PyOcrPythonVersion = "3.11.9";
        public const string PyOcrPythonEmbedZipName = "python-3.11.9-amd64.zip";
        public const string PyOcrPythonDownloadUrlTuna = "https://mirrors.tuna.tsinghua.edu.cn/python/3.11.9/python-3.11.9-amd64.zip";
        public const string PyOcrPythonDownloadUrlOfficial = "https://www.python.org/ftp/python/3.11.9/python-3.11.9-amd64.zip";
        public const string PyOcrGetPipUrl = "https://bootstrap.pypa.io/get-pip.py";
        public const string PyOcrPipMirror = "https://pypi.tuna.tsinghua.edu.cn/simple";
        public const int PyOcrProcessTimeoutSeconds = 180;
        public const int PyOcrRecognizeTimeoutSeconds = 60;

        // --- AI 模型配置 (RMBG - 背景移除) ---
        public const string BgRem14_ModelUrl_HF = "https://huggingface.co/briaai/RMBG-1.4/resolve/main/onnx/model.onnx";
        public const string BgRem14_ModelUrl_MS = "https://modelscope.cn/models/AI-ModelScope/RMBG-1.4/resolve/master/onnx/model.onnx";
        public const string BgRem14_ModelName = "rmbg-1.4.onnx";
        public const string BgRem14_ExpectedMD5 = "8bb9b16ff49cda31e7784852873cfd0d";

        public const string BgRem20_ModelUrl_HF = "https://huggingface.co/briaai/RMBG-2.0/resolve/main/onnx/model_fp16.onnx";
        public const string BgRem20_ModelUrl_MS = "https://modelscope.cn/models/AI-ModelScope/RMBG-2.0/resolve/master/onnx/model_fp16.onnx";
        public const string BgRem20_ModelName = "rmbg-2.0.onnx";
        public const string BgRem20_ExpectedMD5 = "3ecf310c9e7d0ac9c2ab7574ef52d747";

        // --- AI 模型配置 (Real-ESRGAN - 超分辨率) ---
        public const string Sr_ModelUrl_HF = "https://modelscope.cn/models/AXERA-TECH/Real-ESRGAN/resolve/master/onnx/realesrgan-x4-256.onnx";
        public const string Sr_ModelUrl_Mirror = "https://modelscope.cn/models/AXERA-TECH/Real-ESRGAN/resolve/master/onnx/realesrgan-x4-256.onnx";
        public const string Sr_ModelName = "realesrgan-x4plus.onnx";
        public const string Sr_ExpectedMD5 = "25C354305A32B59300A610BCD7846977";
        public const int Sr_TileSize = 256;
        public const int Sr_ScaleFactor = 4;

        // --- AI 模型配置 (LaMa - 智能填补) ---
        public const string Inpaint_ModelUrl = "https://huggingface.co/Carve/LaMa-ONNX/resolve/main/lama_fp32.onnx";
        public const string Inpaint_ModelUrl_Mirror = "https://modelscope.cn/models/codetrend/LaMa_Inpainting_Model_ONNX/resolve/master/lama_fp32.onnx";
        public const string Inpaint_ModelName = "lama_fp32.onnx";
        public const string Inpaint_ExpectedMD5 = "2777748DC5275B27DAFC63C5D4F1F730";

        // --- 二维码识别插件 (ZXing.Net) ---
        public const string ZXing_Version = "0.16.9";
        public const string ZXing_DllName = "zxing.dll";
        public const string ZXing_DownloadUrl = "https://www.nuget.org/api/v2/package/ZXing.Net/0.16.9";
        public const string ZXing_DownloadUrl_Mirror = "https://repo.huaweicloud.com/repository/nuget/v3/zxing.net/0.16.9/zxing.net.0.16.9.nupkg";

        // --- PSD 读取插件 (Magick.NET) ---
        public const string Magick_Version = "14.11.1";
        public const string Magick_DllName = "Magick.NET-Q8-AnyCPU.dll";
        public const string Magick_DownloadUrl = "https://www.nuget.org/api/v2/package/Magick.NET-Q8-AnyCPU/14.11.1";
        public const string Magick_DownloadUrl_Mirror = "https://repo.huaweicloud.com/repository/nuget/v3/magick.net-q8-anycpu/14.11.1/magick.net-q8-anycpu.14.11.1.nupkg";

        public const string MagickCore_DllName = "Magick.NET.Core.dll";
        public const string MagickCore_DownloadUrl = "https://www.nuget.org/api/v2/package/Magick.NET.Core/14.11.1";
        public const string MagickCore_DownloadUrl_Mirror = "https://repo.huaweicloud.com/repository/nuget/v3/magick.net.core/14.11.1/magick.net.core.14.11.1.nupkg";

        public const string MagickNative_DllName = "Magick.Native-Q8-x64.dll";

        // --- 绘图工具参数 ---
        public const double DefaultPenThickness = 5.0;
        public const double DefaultPenOpacity = 1.0;
        public const int PenCursorZIndex = 9999;
        public const byte HighlighterAlpha = 50;
        public const byte AiEraserCursorAlpha = 100;
        public const byte GaussianBlurCursorAlpha = 100;
        public const double PenLowOpacityThreshold = 0.3;
        public const byte PenLowOpacityStrokeAlpha = 100;
        public const double PenLowOpacityStrokeThickness = 0.5;
        public const double PenDefaultStrokeThickness = 1.0;
        public const double AiEraserMaskOpacity = 0.6;

        // --- 画布限制 ---
        public const double MaxCanvasSize = 16384.0;
        public const double StandardDpi = 96.0;
        public const int BytesPerPixel = 4;
        public const int DefaultBlankCanvasWidth = 1600;
        public const int DefaultBlankCanvasHeight = 1200;

        // --- 文本工具参数 ---
        public const double DefaultFontSize = 24.0;
        public const double DefaultTextBoxWidth = 500.0;
        public const double MinTextBoxHeight = 50.0;
        public const double MaxTextBoxWidth = 1000.0;
        public const int EditorOverlayZIndex = 999;
        public const double TextToolOutlineThickness = 1.5;
        public const double TextToolHandleHitTestSize = 16.0;
        public const double TextToolBorderThicknessMin = 6.0;
        public const double TextToolBorderThicknessMax = 14.0;
        public const double TextToolPadding = 5.0;

        // --- 形状工具参数 ---
        public const double ShapeToolRoundedRectRadius = 20.0;
        public const double ShapeToolStarInnerRadiusRatio = 0.4;
        public const double ShapeToolBubbleRadiusRatio = 0.15;
        public const double ShapeToolBubbleTailHeightRatio = 0.2;
        public const double ShapeToolBubbleTailStartRatio = 0.7;
        public const double ShapeToolBubbleTailEndRatio = 0.5;

        // --- 选择工具参数 ---
        public const double SelectToolHandleSize = 6.0;
        public const double SelectToolOutlineThickness = 1.5;
        public const double SelectToolDashLength = 4.0;
        public const double SelectToolAnimationDurationSeconds = 1.0;
        public const double SelectToolAnimationTo = 8.0;
        public const double CanvasResizeHandleSize = 8.0;

        // --- 图片栏参数 ---
        public const double ImageBarItemWidth = 124.0;
        public const double ImageBarAddButtonWidthFallback = 46.0;

        // --- 导航栏参数 ---
        public const double NavExpandedWidth = 170;
        public const double NavCollapsedWidth = 48;

        // --- 选择与拖拽定时 (ms) ---
        public const int TabSwitchCheckIntervalMs = 50;
        public const int HoverStartTimeThresholdMs = 200;
        public const int QuickDragDelayMs = 500;
        public const int SlowDragDelayMs = 1000;
        public const int TempFileCleanupDelayMs = 5000;

        // --- 拖拽与布局阈值 ---
        public const double DragTitleBarThresholdY = 100.0;
        public const double DragImageBarThresholdY = 210.0;
        public const double DragGlobalPadding = 5.0;

        // --- 状态栏布局阈值 ---
        public const double StatusBarThresholdFile = 950.0;
        public const double StatusBarThresholdMouse = 800.0;
        public const double StatusBarThresholdSelection = 650.0;
        public const double StatusBarThresholdImage = 500.0;

        // --- AI 修复参数 ---
        public const int AiInpaintSize = 512;
        public const int AiInferenceSizeDefault = 1024;
        public const int AiSrTileOverlap = 16;
        public const int AiDownloadTimeoutMinutes = 20;
        public const int AiDownloadBufferSize = 8192;
        public const string AiImageDefaultApiBaseUrl = "https://api.openai.com/v1";
        public const string AiImageDefaultModel = "gpt-image-1";
        public static readonly string AiModelDefaultSaveDir = CacheDir;

        // --- 布局参数 ---
        public const double FitToWindowMarginFactor = 0.9;
        public const double CanvasMargin = 200.0;

        // --- 加载与性能参数 ---
        public const int PreviewDecodeWidth = 480;
        public const long HugeImagePixelThreshold = 10_000_000;
        public const long PerformanceScorePixelThreshold = 2_000_000;
        public const long MemoryLimitForAggressiveRelease = 1024L * 1024 * 1024;
        public const int FilterBrownRAddition = 35;
        public const int FilterBrownGAddition = 8;
        public const int FilterBrownBAddition = -20;
        public const int AutoSaveDefaultDelaySeconds = 3;
        public const int AutoSaveBaseDelayMs = 2000;
        public const int AutoSavePerformanceFactor = 200;
        public const double CanvasResizeEdgePadding = 50.0;
        public const int DefaultWandTolerance = 50;

        public const int ImageLoadDelayHugeMs = 100;
        public const int ImageLoadDelayLazyMs = 50;

        // --- 滤镜参数 ---
        public const double SepiaR1 = 0.393;
        public const double SepiaR2 = 0.769;
        public const double SepiaR3 = 0.189;
        public const double SepiaG1 = 0.349;
        public const double SepiaG2 = 0.686;
        public const double SepiaG3 = 0.168;
        public const double SepiaB1 = 0.272;
        public const double SepiaB2 = 0.534;
        public const double SepiaB3 = 0.131;

        // --- EXIF Tags ---
        public const string ExifTagExposureTime = "/app1/ifd/exif/{uint=33434}";
        public const string ExifTagFNumber = "/app1/ifd/exif/{uint=33437}";
        public const string ExifTagIsoSpeed = "/app1/ifd/exif/{uint=34855}";
        public const string ExifTagExposureBias = "/app1/ifd/exif/{uint=37380}";
        public const string ExifTagFocalLength = "/app1/ifd/exif/{uint=37386}";
        public const string ExifTagFocalLength35mm = "/app1/ifd/exif/{uint=41989}";
        public const string ExifTagMeteringMode = "/app1/ifd/exif/{uint=37383}";
        public const string ExifTagFlash = "/app1/ifd/exif/{uint=37385}";
        public const string ExifTagLensModel = "/app1/ifd/exif/{uint=42036}";

        // --- 灰度转换权重 ---
        public const double GrayWeightR = 0.2126;
        public const double GrayWeightG = 0.7152;
        public const double GrayWeightB = 0.0722;

        // --- 样式与颜色 ---
        public const string DarkBackgroundHex = "#333";
        public const string DefaultThemeAccentColor = "#0078D4";

        // --- 系统与环境 ---
        public const int Windows11BuildThreshold = 22000;
        public const int DwmCornerPreferenceRounded = 2;
        public const double UninitializedWindowPosition = -10000.0;

        // --- 渲染阈值 ---
        public const double DefaultViewInterpolationThreshold = 160.0;
        public const double DefaultPaintInterpolationThreshold = 200.0;

        // --- 标尺参数 ---
        public const double RulerSegmentHeight = 5;
        public const double RulerSegmentHeightMid = 10;
        public const double RulerSegmentHeightLong = 15;

        // --- 消息框图标路径 (SVG) ---
        public const string PathInfo = "M12,2C6.48,2 2,6.48 2,12C2,17.52 6.48,22 12,22C17.52,22 22,17.52 22,12C22,6.48 17.52,2 12,2M13,17H11V11H13V17M13,9H11V7H13V9Z";
        public const string PathQuestion = "M12,2C17.52,2 22,6.48 22,12C22,17.52 17.52,22 12,22C6.48,22 2,17.52 2,12C2,6.48 6.48,2 12,2M11,19H13V17H11M12,5C10.6,5 9.27,5.57 8.5,6.5C7.94,7.2 7.7,8.08 7.75,9H9.72C9.72,8.65 10,8 11.2,7.7C12.4,7.4 13.5,8 13.7,9C13.88,10 13.25,10.5 12.6,11C11.66,11.73 11,12.5 11,14.5H13C13,13.29 13.55,12.7 14.4,12C15.5,11.16 16.5,10.2 16.27,8.2C16.06,6.42 14.5,5 12,5Z";
        public const string PathWarning = "M12,2L1,21H23M12,6L19.53,19H4.47M11,10V14H13V10M11,16V18H13V16";
        public const string PathError = "M12,2C17.53,2 22,6.47 22,12C22,17.53 17.53,22 12,22C6.47,22 2,17.53 2,12C2,6.47 6.47,2 12,2M15.59,7L12,10.59L8.41,7L7,8.41L10.59,12L7,15.59L8.41,17L12,13.41L15.59,17L17,15.59L13.41,12L17,8.41L15.59,7Z";
        public const string PathTaskProgress = "F1 M10.027,13.832 L2,21.611 2,27.503 30,27.503 30,25.653 C29.973,25.632 29.945,25.613 29.92,25.588 L23.333,18.826 19.681,22.132 C19.25,22.458 18.643,22.511 18.302,22.097 L10.027,13.832z M23,8.514 C24.103,8.514 24.995,9.408 24.995,10.509 24.995,11.61 24.102,12.504 23,12.504 21.897,12.504 21.005,11.61 21.005,10.509 21.005,9.408 21.897,8.514 23,8.514z M1.998,4.496 L1.998,18.818 9.307,11.706 C9.509,11.516 9.781,11.426 10.056,11.436 10.333,11.455 10.588,11.585 10.765,11.799 L19.045,19.989 22.813,16.707 C23.209,16.406 23.767,16.441 24.124,16.793 L29.998,22.804 29.998,22.805 30,22.806 29.998,22.804 29.998,4.496 1.998,4.496z M2,2.497 L30,2.497 C31.099,2.497 32,3.398 32,4.497 L32,27.503 C32,28.602 31.099,29.503 30,29.503 L2,29.503 C0.9,29.503 0,28.602 0,27.503 L0,4.497 C0,3.398 0.901,2.497 2,2.497z";

        // --- 注册表与系统常量 ---
        public const string RegistryKeyPathThemes = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        public const string RegistryValueNameLightTheme = "AppsUseLightTheme";
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_BORDER_COLOR = 34;

        // --- 应用逻辑与限制 ---
        public const string VirtualFilePrefix = "::TABPAINT_NEW::";
        public const string AppUniqueId = "TabPaint_App_Mutex_UUID_91823091";
        public const long ModeSwitchCooldownTicks = 200 * 10000;
        public const int ColorPickerZoomPixelSize = 15;
        public const int FileTabPageSize = 10;
        public const int AiUpscaleMaxLongSide = 4096;
        public const int DefaultBorderThickness = 2;
        public const int DefaultChromaKeyTolerance = 45;

        // --- 加载与进度模拟参数 ---
        public const float SvgMinSide = 512f;
        public const int FallbackImageWidth = 800;
        public const int FallbackImageHeight = 600;
        public const double ProgressStartPercent = 5.0;
        public const double ProgressMaxPercent = 95.0;
        public const double ProgressLimitPercent = 99.0;
        public const int ProgressIntervalMs = 50;
        public const int ProgressMinDurationMs = 300;
        public const int DragMoveThreshold = 50;

        // --- 转换器与缩放参数 ---
        public const double ConverterMaxDataValue = 5000.0;
        public const double ConverterMaxSliderValue = 100.0;
        public const double DynamicRangeMinSize = 1.0;
        public const double DynamicRangeShapeMaxSize = 24.0;
        public const double DynamicRangePenPencilMaxSize = 10.0;
        public const double DynamicRangeDefaultMaxSize = 1200.0;
        public const double ZoomSliderMinReal = 0.1;
        public const double ZoomSliderMaxReal = 16.0;

        // --- 交互安全边距与图形参数 ---
        public const double DragSafetyMargin = 50.0;
        public const double ArrowHeadRatio = 0.2;
        public const double ArrowAngleDegrees = 35.0;

        // --- 撤销管理参数 ---
        public const int UndoHotZoneSize = 3;
        public const int UndoCompressThreshold = 64 * 1024;
        public const int PencilTimedUndoIntervalMs = 3000;
        public const double PencilTimedUndoMinZoom = 8.0;

        // --- 笔刷特定逻辑参数 ---
        public const double CalligraphyMaxSpeed = 60.0;
        public const double CalligraphyMinPressure = 0.1;
        public const byte ColorComponentMax = 255;
        public const double MinCustomCursorSize = 4.0;
        public const float PenVelocitySmoothFactor = 0.2f;
        public const float CalligraphyMinSpeed = 50f;
        public const float CalligraphyMaxSpeedPx = 2000f;
        public const float CalligraphyMinPressureVal = 0.15f;
        public const int BlurParallelThreshold = 16;
        public const int OilPaintBrightnessVariation = 40;

        // --- 服务与性能配置 ---
        public const int MaxDiskConcurrency = 4;
        public const int AppSettingsBinaryVersion = 23;

        public static bool IsSupportedImage(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path).ToLower();
            foreach (var s in ImageExtensions)
            {
                if (s == ext) return true;
            }
            return false;
        }
        // --- 文件过滤器与扩展名 ---
        public const string ImageFilterFormat = "{0}|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.avif;*.ico;*.heic;*.jfif;*.exif;*.jpe;*.jxl;*.heif;*.hif;*.dib;*.wdp;*.wmp;*.jxr;*.svg;*.psd|{1} (*.png)|*.png|{2} (*.jpg;*.jpeg)|*.jpg;*.jpeg|{3} (*.webp)|*.webp|{4} (*.bmp)|*.bmp|{5} (*.gif)|*.gif|{6} (*.tif;*.tiff)|*.tif;*.tiff|{7} (*.ico)|*.ico|PSD (*.psd)|*.psd";
        public static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".avif", ".ico", ".tiff", ".heic", ".tif", ".jfif", ".exif", ".jpe", ".jxl", ".heif", ".hif", ".dib", ".wdp", ".wmp", ".jxr", ".svg", ".psd" };
    }
}
