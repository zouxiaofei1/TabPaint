
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

//
//SelectTool类的定义
//

namespace TabPaint
{

    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {

        public partial class SelectTool : ToolBase
        {
            private void EnsureTimer()
            {
                if (_tabSwitchTimer == null)
                {
                    _tabSwitchTimer = new DispatcherTimer();
                    _tabSwitchTimer.Interval = TimeSpan.FromMilliseconds(AppConsts.TabSwitchCheckIntervalMs); // 每50ms检查一次
                    _tabSwitchTimer.Tick += OnTabSwitchTimerTick;
                }
            }
            private void OnTabSwitchTimerTick(object sender, EventArgs e)
            {
                if (_pendingTab == null || !_draggingSelection)
                {
                    ResetSwitchTimer();
                    return;
                }
                bool isCleanSelection = ctxForTimer.Undo.UndoCount <= 1 && ctxForTimer.Undo._redo.Count == 0;
                double targetDelay = isCleanSelection ? AppConsts.QuickDragDelayMs : AppConsts.SlowDragDelayMs;
                double elapsed = (DateTime.Now - _hoverStartTime).TotalMilliseconds;
                var mw = ctxForTimer.ParentWindow;
                if (elapsed > AppConsts.HoverStartTimeThresholdMs)
                {
                    // 1. 光标反馈
                    Mouse.OverrideCursor = Cursors.AppStarting;

                    // 2. 状态栏文字倒计时
                    double remaining = (targetDelay - elapsed) / 1000.0;
                    string cleanStateText = isCleanSelection ? LocalizationManager.GetString("L_Selection_Drag_Quick") : "";

                    string statusText = string.Format(
            LocalizationManager.GetString("L_Selection_Drag_Jump"),
            _pendingTab.FileName,
            cleanStateText,
            remaining
        );
                    mw.SelectionSize = statusText;

                    if (ctxForTimer != null)
                    {
                        double scaleFactor = 1.0 - (elapsed / (targetDelay * 2));
                        if (scaleFactor < 0.8) scaleFactor = 0.8;
                        ctxForTimer.SelectionPreview.Opacity = 0.5 + (Math.Sin(elapsed / 100.0) * 0.2);
                    }
                }
                if (elapsed > targetDelay)
                {
                    if (ctxForTimer != null) ctxForTimer.SelectionPreview.Opacity = 0.7; // 恢复默认拖拽透明度
                    Mouse.OverrideCursor = null;

                    _draggingSelection = false;

                    byte[] dataClone = null;
                    int w, h;
                    double? angle = null;

                    if (_preRotationSelectionData != null && Math.Abs(_rotationAngle) > 0.01)
                    {
                        w = _preRotationDataWidth;
                        h = _preRotationDataHeight;
                        angle = _rotationAngle;
                        dataClone = (byte[])_preRotationSelectionData.Clone();
                    }
                    else
                    {
                        EnsureRotationBaked(ctxForTimer);
                        w = _originalRect.Width > 0 ? _originalRect.Width : _selectionRect.Width;
                        h = _originalRect.Height > 0 ? _originalRect.Height : _selectionRect.Height;
                        if (_selectionData != null)
                        {
                            dataClone = (byte[])_selectionData.Clone();
                        }
                    }

                    mw.TransferSelectionToTab(_pendingTab, dataClone, w, h, angle);

                    ResetSwitchTimer();
                }
            }
            private void ResetSwitchTimer()
            {
                if (_tabSwitchTimer != null && _tabSwitchTimer.IsEnabled)
                {
                    _tabSwitchTimer.Stop();
                }
                _pendingTab = null;
                Mouse.OverrideCursor = null;

                // 恢复预览图状态
                if (ctxForTimer != null) ctxForTimer.SelectionPreview.Opacity = 1.0;

                var mw = ctxForTimer?.ParentWindow;
                if (mw != null) mw.UpdateSelectionScalingMode();
            }
            private ToolContext ctxForTimer;
        }
    }
}