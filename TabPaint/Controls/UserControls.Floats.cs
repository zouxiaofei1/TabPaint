//浮动面板控件延迟加载
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using TabPaint.Controls;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private TaskProgressFloat _taskProgressPopup;
        private SelectionToolBar _selectionToolBar;
        private SelectionRotateFloat _selectionRotatePopup;
        private OcrFloatBar _ocrFloatBar;
        public TaskProgressFloat TaskProgressPopup
        {
            get
            {
                if (_taskProgressPopup == null)
                {
                    _taskProgressPopup = new TaskProgressFloat();
                    _taskProgressPopup.HorizontalAlignment = HorizontalAlignment.Center;
                    _taskProgressPopup.VerticalAlignment = VerticalAlignment.Bottom;
                    _taskProgressPopup.CancelRequested += OnDownloadCancelRequested;
                    TaskProgressHolder.Content = _taskProgressPopup;
                }
                return _taskProgressPopup;
            }
        }
        public SelectionToolBar SelectionToolBar
        {
            get
            {
                if (_selectionToolBar == null)
                {
                    _selectionToolBar = new SelectionToolBar();
                    _selectionToolBar.HorizontalAlignment = HorizontalAlignment.Left;
                    _selectionToolBar.VerticalAlignment = VerticalAlignment.Top;
                    _selectionToolBar.CopyClick += SelectionToolBar_CopyClick;
                    _selectionToolBar.AiRemoveBgClick += SelectionToolBar_AiRemoveBgClick;
                    _selectionToolBar.OcrClick += SelectionToolBar_OcrClick;
                    _selectionToolBar.RotateClick += SelectionToolBar_RotateClick;
                    SelectionToolHolder.Content = _selectionToolBar;
                }
                return _selectionToolBar;
            }
        }

        public SelectionRotateFloat SelectionRotatePopup
        {
            get
            {
                if (_selectionRotatePopup == null)
                {
                    _selectionRotatePopup = new SelectionRotateFloat();
                    _selectionRotatePopup.HorizontalAlignment = HorizontalAlignment.Center;
                    _selectionRotatePopup.VerticalAlignment = VerticalAlignment.Bottom;
                    _selectionRotatePopup.AngleChanged += SelectionRotatePopup_AngleChanged;
                    SelectionRotateHolder.Content = _selectionRotatePopup;
                }
                return _selectionRotatePopup;
            }
        }

        public OcrFloatBar OcrFloatBar
        {
            get
            {
                if (_ocrFloatBar == null)
                {
                    _ocrFloatBar = new OcrFloatBar();
                    _ocrFloatBar.HorizontalAlignment = HorizontalAlignment.Left;
                    _ocrFloatBar.VerticalAlignment = VerticalAlignment.Top;
                    _ocrFloatBar.CopyAllClick += OcrFloatBar_CopyAllClick;
                    _ocrFloatBar.ConfirmClick += OcrFloatBar_ConfirmClick;
                    _ocrFloatBar.BarMouseDown += OcrFloatBar_BarMouseDown;
                    _ocrFloatBar.BarMouseMove += OcrFloatBar_BarMouseMove;
                    _ocrFloatBar.BarMouseUp += OcrFloatBar_BarMouseUp;
                    OcrFloatBarHolder.Content = _ocrFloatBar;
                }
                return _ocrFloatBar;
            }
        }

        public TextToolControl TextMenu { get; private set; }
        private void EnsureTextToolLoaded()
        {

            if (TextMenu != null) return;
            TextMenu = new TextToolControl();

            TextMenu.FontFamilyCombo.SelectionChanged += FontSettingChanged;
            TextMenu.FontFamilyCombo.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                new TextChangedEventHandler(FontSettingChanged));

            TextMenu.FontSizeCombo.SelectionChanged += FontSettingChanged;
            TextMenu.FontSizeCombo.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                new TextChangedEventHandler(FontSettingChanged));
            TextMenu.FontFamilyBox.SelectionChanged += FontSettingChanged;
            TextMenu.FontSizeBox.SelectionChanged += FontSettingChanged;
            TextMenu.TextEditBar.MouseDown += TextEditBar_MouseDown; // 绑定回 MainWindow 的旧方法
            TextMenu.TextEditBar.MouseMove += TextEditBar_MouseMove;
            TextMenu.TextEditBar.MouseUp += TextEditBar_MouseUp;
            TextMenu.BoldButton.Click += FontSettingChanged;
            TextMenu.ItalicButton.Click += FontSettingChanged;
            TextMenu.UnderlineButton.Click += FontSettingChanged;
            TextMenu.StrikeButton.Click += FontSettingChanged;

            TextMenu.TextBackgroundButton.Click += FontSettingChanged;
            TextMenu.SubscriptButton.Click += FontSettingChanged;
            TextMenu.SuperscriptButton.Click += FontSettingChanged;
            TextMenu.HighlightButton.Click += FontSettingChanged;
            TextMenu.ShadowButton.Click += FontSettingChanged;
            TextMenu.AlignLeftButton.Click += TextAlign_Click;
            TextMenu.AlignCenterButton.Click += TextAlign_Click;
            TextMenu.AlignRightButton.Click += TextAlign_Click;
            TextMenu.InsertTableButton.Click += InsertTable_Click;
            TextToolHolder.Content = TextMenu;
        }
        private void InitializeDragWatchdog()
        {
            _dragWatchdog = new DispatcherTimer();
            _dragWatchdog.Interval = TimeSpan.FromMilliseconds(200);
            _dragWatchdog.Tick += DragWatchdog_Tick;
        }
        private void OnAppTitleBarIconDragRequest(object sender, MouseButtonEventArgs e)
        {
            if (MainImageBar.IsSingleTabMode && _currentTabItem != null)
            {
                try
                {
                    string dragFilePath = PrepareDragFilePath(_currentTabItem);

                    if (!string.IsNullOrEmpty(dragFilePath) && File.Exists(dragFilePath))
                    {
                        var dataObject = new DataObject(DataFormats.FileDrop, new string[] { dragFilePath });
                        DragDrop.DoDragDrop(AppTitleBar, dataObject, DragDropEffects.Copy | DragDropEffects.Move);
                    }
                }
                catch (Exception ex)
                {
                    ShowToast(string.Format(LocalizationManager.GetString("L_Toast_DragFailed"), ex.Message), ex);
                }
            }
        }
    }
}