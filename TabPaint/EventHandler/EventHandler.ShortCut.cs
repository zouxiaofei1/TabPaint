
//
//EventHandler.ShortCut.cs
//主窗口的事件处理部分，主要负责全局快捷键监听、模式切换以及各级菜单功能的逻辑分发。
//
using Microsoft.VisualBasic.FileIO;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;


namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {

        private bool HandleGlobalShortcuts(object sender, KeyEventArgs e)
        {
            if (IsShortcut("View.ToggleMode", e)) { TriggerModeChange(); e.Handled = true; return true; }
            if (IsShortcut("View.ToggleImageBar", e))
            {
                MainImageBar.IsCompactMode = !MainImageBar.IsCompactMode;
                e.Handled = true;
                return true;
            }
            if (IsShortcut("View.RotateLeft", e))
            {
                // 使用视觉旋转以避免大图卡顿
                _isVisualRotating = true;
                _visualRotationAngle -= 90;
                VisualRotateTransform.Angle = _visualRotationAngle;
                e.Handled = true;
                return true;
            }
            if (IsShortcut("View.RotateRight", e))
            {
                _isVisualRotating = true;
                _visualRotationAngle += 90;
                VisualRotateTransform.Angle = _visualRotationAngle;
                e.Handled = true;
                return true;
            }
            if (IsShortcut("View.VerticalFlip", e)) { OnFlipVerticalClick(sender, e); e.Handled = true; return true; }
            if (IsShortcut("View.HorizontalFlip", e)) { OnFlipHorizontalClick(sender, e); e.Handled = true; return true; }

            bool isNext = IsShortcut("View.NextImage", e);
            bool isPrev = IsShortcut("View.PrevImage", e);

            if (isNext || isPrev)
            {

                if (_router.CurrentTool is TextTool tx && tx._richTextBox != null) return false;
                if (!_isNavigating)
                {
                    _isNavigating = true;
                    _navKeyPressStartTime = DateTime.Now;
                }

                if (isNext) ShowNextImage();
                if (isPrev) ShowPrevImage();

                e.Handled = true;
                return true;
            }
            if (IsShortcut("View.FullScreen", e)) { MaximizeWindowHandler(); e.Handled = true; return true; }
            return false;
        }

        private void HandleViewModeShortcuts(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (IsShortcut("File.Print", e)) { OnPrintClick(sender, e); e.Handled = true; return; }

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.C:
                        if (_currentTabItem != null)
                        {
                            CopyTabToClipboard(_currentTabItem);
                            ShowToast("L_Toast_Copied");
                        }
                        e.Handled = true;
                        break;
                }
            }
        }

        private void HandlePaintModeShortcuts(object sender, KeyEventArgs e)
        {
            if (IsShortcut("Tool.SwitchToPen", e)) { SetBrushStyle(BrushStyle.Pencil); e.Handled = true; return; }
            if (IsShortcut("Tool.SwitchToPick", e)) { LastTool = _router.CurrentTool; _router.SetTool(_tools.Eyedropper); e.Handled = true; return; }
            if (IsShortcut("Tool.SwitchToEraser", e)) { SetBrushStyle(BrushStyle.Eraser); e.Handled = true; return; }
            if (IsShortcut("Tool.SwitchToSelect", e)) { _router.SetTool(_tools.Select); e.Handled = true; return; }
            if (IsShortcut("Tool.SwitchToFill", e)) { _router.SetTool(_tools.Fill); e.Handled = true; return; }
            if (IsShortcut("Tool.SwitchToText", e)) { _router.SetTool(_tools.Text); e.Handled = true; return; }
            if (IsShortcut("Tool.SwitchToBrush", e)) { SetBrushStyle(BrushStyle.Round); e.Handled = true; return; }
            if (IsShortcut("Tool.SwitchToShape", e)) { _router.SetTool(_tools.Shape); e.Handled = true; return; }
            if (IsShortcut("View.ToggleMinimize", e)) { if (this.WindowState != WindowState.Minimized) this.WindowState = WindowState.Minimized; e.Handled = true; return; }
            if (IsShortcut("Tool.ClipMonitor", e))
            {
                var settings = SettingsManager.Instance.Current;
                settings.EnableClipboardMonitor = !settings.EnableClipboardMonitor;
                e.Handled = true; return;
            }
            if (IsShortcut("Tool.RemoveBg", e)) { OnRemoveBackgroundClick(sender, e); return; }
            if (IsShortcut("Tool.ChromaKey", e)) { OnChromaKeyClick(sender, e); return; }
            if (IsShortcut("Tool.OCR", e)) { OnOcrClick(sender, e); return; }
            if (IsShortcut("Tool.ScreenPicker", e)) { OnScreenColorPickerClick(sender, e); return; }
            if (IsShortcut("Tool.CopyColorCode", e)) { OnCopyColorCodeClick(sender, e); return; }
            if (IsShortcut("Tool.AutoCrop", e)) { OnAutoCropClick(sender, e); return; }
            if (IsShortcut("Tool.AddBorder", e)) { OnAddBorderClick(sender, e); return; }

            // 文件高级
            if (IsShortcut("File.OpenWorkspace", e)) { OnOpenWorkspaceClick(sender, e); e.Handled = true; return; }
            if (IsShortcut("File.Print", e)) { OnPrintClick(sender, e); e.Handled = true; return; }
            if (IsShortcut("File.PasteNewTab", e)) { PasteClipboardAsNewTab(); e.Handled = true; return; }

            if (IsShortcut("Effect.Brightness", e)) { OnBrightnessContrastExposureClick(sender, e); e.Handled = true; return; } // Ctrl+Alt+Q
            if (IsShortcut("Effect.Temperature", e)) { OnColorTempTintSaturationClick(sender, e); e.Handled = true; return; } // Ctrl+Alt+W
            if (IsShortcut("Effect.Grayscale", e)) { OnConvertToBlackAndWhiteClick(sender, e); e.Handled = true; return; }   // Ctrl+Alt+E
            if (IsShortcut("Effect.Invert", e)) { OnInvertColorsClick(sender, e); e.Handled = true; return; }      // Ctrl+Alt+R
            if (IsShortcut("Effect.AutoLevels", e)) { OnAutoLevelsClick(sender, e); e.Handled = true; return; }  // Ctrl+Alt+T
            if (IsShortcut("Effect.Resize", e)) { OnResizeCanvasClick(sender, e); e.Handled = true; return; }      // Ctrl+Alt+Y

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (_router.CurrentTool is TextTool ttt && ttt._richTextBox != null && ttt._richTextBox.IsKeyboardFocused)
                {
                    switch (e.Key)
                    {// 复制粘贴剪切全选撤销重做
                        case Key.C:
                        case Key.V:
                        case Key.X:
                        case Key.A:
                        case Key.Z:
                        case Key.Y:
                            return;
                    }
                }
                switch (e.Key)
                {
                    case Key.Z:
                        if (_router.CurrentTool is TextTool textTool && textTool._richTextBox != null)// 只取消文本框，不撤销画布
                            textTool.GiveUpText(_ctx);
                        else Undo();
                        e.Handled = true;
                        break;

                    case Key.Y: Redo(); e.Handled = true; break;
                    case Key.S: OnSaveClick(sender, e); e.Handled = true; break;
                    case Key.N: OnNewClick(sender, e); e.Handled = true; break;
                    case Key.O: OnOpenClick(sender, e); e.Handled = true; break; // 普通打开
                    case Key.W:
                        var currentTab = FileTabs?.FirstOrDefault(t => t.IsSelected);
                        if (currentTab != null) CloseTab(currentTab);
                        e.Handled = true;
                        break;
                    case Key.V:
                        var dataObj = ClipboardHelper.GetDataObjectWithRetry();
                        if (dataObj == null) break;

                        if (dataObj.GetDataPresent(DataFormats.Rtf))
                        {
                            try
                            {
                                string rtfData = dataObj.GetData(DataFormats.Rtf) as string;
                                var styleInfo = TextFormatHelper.ParseRtf(rtfData);

                                if (styleInfo != null && !string.IsNullOrWhiteSpace(styleInfo.Text))
                                {

                                    if (!(_router.CurrentTool is TextTool)) _router.SetTool(_tools.Text);
                                    ApplyDetectedTextStyle(styleInfo);
                                    Point center = new Point(ActualWidth / 2, ActualHeight / 2);
                                    if (_router.CurrentTool is TextTool tt)
                                    {
                                        tt.SpawnTextBox(_ctx, center, styleInfo.Text);
                                        e.Handled = true;
                                        return; // 成功处理，退出
                                    }
                                }
                            }
                            catch { /* 解析失败则回退到纯文本 */ }
                        }
                        if (dataObj.GetDataPresent(DataFormats.Text))
                        {
                            string text = dataObj.GetData(DataFormats.Text) as string;
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                if (!(_router.CurrentTool is TextTool)) _router.SetTool(_tools.Text);
                                Point center = new Point(ActualWidth / 2, ActualHeight / 2);
                                if (_router.CurrentTool is TextTool texttool)
                                {
                                    texttool.SpawnTextBox(_ctx, center, text);
                                    e.Handled = true;
                                    break;
                                }
                            }
                        }
                        bool isMultiFilePaste = false;
                        if (dataObj.GetDataPresent(DataFormats.FileDrop))
                        {
                        }
                        if (!isMultiFilePaste)
                        {
                            _router.SetTool(_tools.Select);
                            if (_tools.Select is SelectTool st) st.PasteSelection(_ctx, true);
                        }
                        e.Handled = true;
                        break;
                    case Key.A:
                        if (_router.CurrentTool is TextTool tx && tx._richTextBox != null) break;
                        if (_router.CurrentTool != _tools.Select) break;
                        _router.SetTool(_tools.Select);
                        SelectTool stSelectAll = _router.GetSelectTool();
                        if (stSelectAll.HasActiveSelection) stSelectAll.CommitSelection(_ctx);
                        stSelectAll.Cleanup(_ctx);
                        stSelectAll.SelectAll(_ctx, false);
                        e.Handled = true;
                        break;
                }
            }
            else if (Keyboard.Modifiers == ModifierKeys.None)
            {
                switch (e.Key)
                {
                    case Key.Delete:
                        if (_tools.Select is SelectTool st)
                        {
                            if (st.HasActiveSelection)
                            {
                                st.DeleteSelection(_ctx);
                            }
                            else if (SettingsManager.Instance.Current.EnableFileDeleteInPaintMode)
                                if ((DateTime.Now - st.LastSelectionDeleteTime).TotalSeconds < AppConsts.DoubleClickTimeThreshold)
                                {
                                    st.ResetLastDeleteTime();
                                    ShowToast("L_Toast_PressDeleteAgain");
                                }
                                else
                                {
                                    HandleDeleteFileAction();
                                }
                        }
                        e.Handled = true;
                        break;
                }
            }
        }
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == 0)
            {
                _altToggleHandled = false;
            }

            if (_isVisualRotating)
            {
                // 如果释放了 L/R，或者释放了 Ctrl，都视为长按结束
                bool isKeyLOrR = (e.Key == Key.L || e.Key == Key.R);
                bool isCtrlReleased = (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl || e.Key == Key.System);

                if (isKeyLOrR || isCtrlReleased)
                {
                    // 提交旋转
                    if (_visualRotationAngle != 0)
                    {
                        int finalAngle = (int)_visualRotationAngle % 360;
                        if (finalAngle != 0)
                        {
                            RotateBitmap(finalAngle);
                        }
                    }

                    // 重置状态
                    _isVisualRotating = false;
                    _visualRotationAngle = 0;
                    VisualRotateTransform.Angle = 0;
                }
            }

            // 获取按键对应的功能名
            var settings = SettingsManager.Instance.Current;
            string? action = null;
            if (settings.Shortcuts != null)
            {
                foreach (var kvp in settings.Shortcuts)
                {
                    Key k = (e.Key == Key.System ? e.SystemKey : e.Key);
                    if (kvp.Value.Key == k)
                    {
                        action = kvp.Key;
                        break;
                    }
                }
            }

            if (action == "View.NextImage" || action == "View.PrevImage")
            {
                // 重置状态
                _isNavigating = false;
                _navKeyPressStartTime = DateTime.MinValue;
            }
        }

        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            Key actualKey = (e.Key == Key.System ? e.SystemKey : e.Key);

            bool isAltOnlyToggle =
                (actualKey == Key.LeftAlt || actualKey == Key.RightAlt) &&
                (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt &&
                (Keyboard.Modifiers & ~ModifierKeys.Alt) == 0;

            if (isAltOnlyToggle && !_altToggleHandled)
            {
                _isSelectToolFloatBarVisible = !_isSelectToolFloatBarVisible;
                _altToggleHandled = true;
                UpdateSelectionToolBarPosition(force: true);
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.None &&
                !IsEditingTextField() &&
                (actualKey == Key.Oem2 || actualKey == Key.Divide))
            {
                ApplyStatusCommandBarExpandedState(true, adjustWindowHeight: !_isStatusCommandBarExpanded);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                var ocrHolder = GetOcrFloatBarHolder();
                if (ocrHolder != null && ocrHolder.Visibility == Visibility.Visible)
                {
                    HideOcrOverlay();
                    e.Handled = true;
                    return;
                }
            }
            if (IsEditingTextField())
            {
                // 1. 允许基础光标导航和删除键穿透给输入框
                switch (e.Key)
                {
                    case Key.Left:
                    case Key.Right:
                    case Key.Up:
                    case Key.Down:
                    case Key.Home:
                    case Key.End:
                    case Key.Delete:
                    case Key.Back:
                    case Key.Tab:
                    case Key.Enter:
                        return;
                }
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    switch (e.Key)
                    {// 复制粘贴剪切全选撤销重做
                        case Key.C:
                        case Key.V:
                        case Key.X:
                        case Key.A:
                        case Key.Z:
                        case Key.Y:
                            return;
                    }
                }
            }
            if (HandleGlobalShortcuts(sender, e)) return;

            if (IsViewMode) HandleViewModeShortcuts(sender, e);
            else HandlePaintModeShortcuts(sender, e);
        }
        private bool IsShortcut(string actionName, KeyEventArgs e)
        {
            var settings = SettingsManager.Instance.Current;
            if (settings.Shortcuts == null || !settings.Shortcuts.ContainsKey(actionName))
                return false;

            var item = settings.Shortcuts[actionName];
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);
            return (key == item.Key && Keyboard.Modifiers == item.Modifiers);
        }
    }
}
