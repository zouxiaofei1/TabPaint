//
//ShortcutRecorder.xaml.cs
//快捷键录制控件，用于在设置界面中捕获用户按下的键盘组合键并实时显示。
//
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TabPaint.Services;

namespace TabPaint.Controls
{
    public partial class ShortcutRecorder : UserControl
    {
        public static readonly DependencyProperty DisplayTextProperty =
            DependencyProperty.Register(nameof(DisplayText), typeof(string), typeof(ShortcutRecorder),
                new PropertyMetadata(string.Empty));

        // 定义依赖属性，以便在 XAML 中绑定
        public static readonly DependencyProperty CurrentItemProperty =
            DependencyProperty.Register("CurrentItem", typeof(ShortcutItem), typeof(ShortcutRecorder),
                new PropertyMetadata(null, OnCurrentItemChanged));

        private Window _hostWindow;
        private bool _isRecording;
        private Key _originalKey = Key.None;
        private ModifierKeys _originalModifiers = ModifierKeys.None;

        public string DisplayText
        {
            get { return (string)GetValue(DisplayTextProperty); }
            set { SetValue(DisplayTextProperty, value); }
        }

        public ShortcutItem CurrentItem
        {
            get { return (ShortcutItem)GetValue(CurrentItemProperty); }
            set { SetValue(CurrentItemProperty, value); }
        }

        public ShortcutRecorder()
        {
            InitializeComponent();
            Loaded += (_, _) => RefreshDisplayText();
            Unloaded += ShortcutRecorder_Unloaded;
        }

        private static void OnCurrentItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ShortcutRecorder recorder) return;

            if (e.OldValue is ShortcutItem oldItem)
            {
                oldItem.PropertyChanged -= recorder.CurrentItem_PropertyChanged;
            }

            if (e.NewValue is ShortcutItem newItem)
            {
                newItem.PropertyChanged += recorder.CurrentItem_PropertyChanged;
            }

            recorder.RefreshDisplayText();
        }

        private void ShortcutRecorder_Unloaded(object sender, RoutedEventArgs e)
        {
            DetachOutsideClickHandler();
            if (CurrentItem != null)
            {
                CurrentItem.PropertyChanged -= CurrentItem_PropertyChanged;
            }
        }

        private void CurrentItem_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ShortcutItem.Key) ||
                e.PropertyName == nameof(ShortcutItem.Modifiers) ||
                e.PropertyName == nameof(ShortcutItem.DisplayText))
            {
                RefreshDisplayText();
            }
        }

        private void UserControl_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            BeginRecording();
        }

        private void UserControl_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!_isRecording) return;
            if (IsElementWithinControl(e.NewFocus as DependencyObject)) return;

            CancelRecording();
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            BeginRecording();

            if (e.Key == Key.ImeProcessed)
            {
                e.Handled = true;
                return;
            }

            e.Handled = true;
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            if (key == Key.Escape)
            {
                CancelRecording();
                Keyboard.ClearFocus();
                return;
            }

            if (IsModifierKey(key))
            {
                UpdateRecordingDisplay(GetCurrentModifiers(key));
                return;
            }

            ModifierKeys modifiers = Keyboard.Modifiers;

            // 冲突检查逻辑
            var settings = SettingsManager.Instance.Current;
            if (settings != null && settings.Shortcuts != null)
            {
                var conflict = settings.Shortcuts.FirstOrDefault(kvp => 
                    kvp.Value != CurrentItem && // 排除自己
                    key != Key.None && // 忽略 None 键的冲突
                    kvp.Value.Key == key && 
                    kvp.Value.Modifiers == modifiers);

                if (conflict.Value != null)
                {
                    conflict.Value.Key = Key.None;
                    conflict.Value.Modifiers = ModifierKeys.None;
                    var window = Window.GetWindow(this) as SettingsWindow;
                    if (window != null)
                    {
                        string featureName = GetFriendlyName(conflict.Key);
                        window.ShowConflictToast(featureName);
                    }
                }
            }
            if (CurrentItem == null) CurrentItem = new ShortcutItem();

            CurrentItem.Key = key;
            CurrentItem.Modifiers = modifiers;

            EndRecording();
            Keyboard.ClearFocus();
        }

        private void UserControl_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (!_isRecording) return;

            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);
            if (!IsModifierKey(key)) return;

            e.Handled = true;
            UpdateRecordingDisplay(Keyboard.Modifiers);
        }

        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source)
            {
                var parent = System.Windows.Media.VisualTreeHelper.GetParent(source);
                while (parent != null && parent != this)
                {
                    if (parent is Button) return; // 如果是按钮，直接返回，不做额外处理
                    parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                }
            }

            Focus();
            e.Handled = true;
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentItem != null)
            {
                CurrentItem.Key = Key.None;
                CurrentItem.Modifiers = ModifierKeys.None;
            }

            EndRecording();
            Keyboard.ClearFocus();
        }

        private void BeginRecording()
        {
            if (_isRecording) return;

            _isRecording = true;
            _originalKey = CurrentItem?.Key ?? Key.None;
            _originalModifiers = CurrentItem?.Modifiers ?? ModifierKeys.None;
            DisplayText = LocalizationManager.GetString("L_Settings_Shortcuts_Waiting");
            AttachOutsideClickHandler();
        }

        private void EndRecording()
        {
            _isRecording = false;
            DetachOutsideClickHandler();
            RefreshDisplayText();
        }

        private void CancelRecording()
        {
            if (!_isRecording) return;

            if (CurrentItem != null)
            {
                CurrentItem.Key = _originalKey;
                CurrentItem.Modifiers = _originalModifiers;
            }

            EndRecording();
        }

        private void RefreshDisplayText()
        {
            if (_isRecording) return;

            DisplayText = CurrentItem?.DisplayText ?? LocalizationManager.GetString("L_Key_None");
        }

        private void UpdateRecordingDisplay(ModifierKeys modifiers)
        {
            string text = BuildDisplayText(Key.None, modifiers, true);
            DisplayText = string.IsNullOrWhiteSpace(text)
                ? LocalizationManager.GetString("L_Settings_Shortcuts_Waiting")
                : text;
        }

        private void AttachOutsideClickHandler()
        {
            _hostWindow = Window.GetWindow(this);
            if (_hostWindow != null)
            {
                _hostWindow.PreviewMouseDown -= HostWindow_PreviewMouseDown;
                _hostWindow.PreviewMouseDown += HostWindow_PreviewMouseDown;
            }
        }

        private void DetachOutsideClickHandler()
        {
            if (_hostWindow != null)
            {
                _hostWindow.PreviewMouseDown -= HostWindow_PreviewMouseDown;
                _hostWindow = null;
            }
        }

        private void HostWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isRecording) return;
            if (IsElementWithinControl(e.OriginalSource as DependencyObject)) return;

            CancelRecording();
            Keyboard.ClearFocus();
        }

        private bool IsElementWithinControl(DependencyObject element)
        {
            while (element != null)
            {
                if (ReferenceEquals(element, this)) return true;
                element = System.Windows.Media.VisualTreeHelper.GetParent(element);
            }

            return false;
        }

        private static bool IsModifierKey(Key key)
        {
            return key == Key.LeftCtrl || key == Key.RightCtrl ||
                   key == Key.LeftAlt || key == Key.RightAlt ||
                   key == Key.LeftShift || key == Key.RightShift ||
                   key == Key.LWin || key == Key.RWin ||
                   key == Key.System;
        }

        private static ModifierKeys GetCurrentModifiers(Key currentKey)
        {
            ModifierKeys modifiers = Keyboard.Modifiers;

            switch (currentKey)
            {
                case Key.LeftCtrl:
                case Key.RightCtrl:
                    modifiers |= ModifierKeys.Control;
                    break;
                case Key.LeftShift:
                case Key.RightShift:
                    modifiers |= ModifierKeys.Shift;
                    break;
                case Key.LeftAlt:
                case Key.RightAlt:
                case Key.System:
                    modifiers |= ModifierKeys.Alt;
                    break;
                case Key.LWin:
                case Key.RWin:
                    modifiers |= ModifierKeys.Windows;
                    break;
            }

            return modifiers;
        }

        private static string BuildDisplayText(Key key, ModifierKeys modifiers, bool allowModifierOnly = false)
        {
            if (key == Key.None && modifiers == ModifierKeys.None)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control) sb.Append("Ctrl + ");
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) sb.Append("Shift + ");
            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) sb.Append("Alt + ");
            if ((modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) sb.Append("Win + ");

            if (key != Key.None)
            {
                sb.Append(GetKeyDisplayName(key));
            }
            else if (!allowModifierOnly && sb.Length >= 3)
            {
                sb.Length -= 3;
            }

            return sb.ToString();
        }

        private static string GetKeyDisplayName(Key key)
        {
            if (key >= Key.D0 && key <= Key.D9) return ((int)key - (int)Key.D0).ToString();
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
                case Key.Next: return "PageDown";
                case Key.Capital: return "CapsLock";
                case Key.Back: return "Backspace";
                default:
                    return key.ToString();
            }
        }

        private string GetFriendlyName(string shortcutId)
        {
            string resourceKey = "L_Settings_Shortcuts_" + shortcutId.Replace(".", "_");
            string name = LocalizationManager.GetString(resourceKey);
            if (name != resourceKey) return name;

            resourceKey = "L_Settings_Shortcuts_" + (shortcutId.Contains(".") ? shortcutId.Split('.')[1] : shortcutId);
            name = LocalizationManager.GetString(resourceKey);
            if (name != resourceKey) return name;
            resourceKey = "L_ToolBar_" + shortcutId.Replace("Tool.SwitchTo", "");
            name = LocalizationManager.GetString(resourceKey);
            if (name != resourceKey) return name;
            resourceKey = "L_Menu_" + shortcutId.Replace(".", "_");
            name = LocalizationManager.GetString(resourceKey);
            if (name != resourceKey) return name;

            return shortcutId;
        }
    }
}
