using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace TabPaint.Services
{
    public class ShortcutProvider : INotifyPropertyChanged
    {
        private static ShortcutProvider _instance;
        public static ShortcutProvider Instance => _instance ??= new ShortcutProvider();

        public Dictionary<string, ShortcutItem> Shortcuts => SettingsManager.Instance.Current.Shortcuts;

        public void NotifyChanged()
        {
            OnPropertyChanged(nameof(Shortcuts));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public static class ShortcutService
    {
        private static readonly List<WeakReference<FrameworkElement>> _monitoredElements = new List<WeakReference<FrameworkElement>>();

        static ShortcutService()
        {
            // 监听全局快捷键变化
            ShortcutProvider.Instance.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ShortcutProvider.Shortcuts))
                {
                    RefreshAllToolTips();
                }
            };
        }
        private static void RefreshAllToolTips()
        {
            for (int i = _monitoredElements.Count - 1; i >= 0; i--)
            {
                if (_monitoredElements[i].TryGetTarget(out var element))
                {
                    UpdateToolTipBinding(element);
                }
                else
                {
                    _monitoredElements.RemoveAt(i);
                }
            }
        }

        // 绑定的快捷键 ID，例如 "Tool.SwitchToPen"
        public static readonly DependencyProperty ShortcutKeyProperty =
            DependencyProperty.RegisterAttached(
                "ShortcutKey",
                typeof(string),
                typeof(ShortcutService),
                new PropertyMetadata(null, OnShortcutKeyChanged));

        public static string GetShortcutKey(DependencyObject obj) => (string)obj.GetValue(ShortcutKeyProperty);
        public static void SetShortcutKey(DependencyObject obj, string value) => obj.SetValue(ShortcutKeyProperty, value);

        // 基础 ToolTip 文本或资源 Key
        public static readonly DependencyProperty BaseToolTipProperty =
            DependencyProperty.RegisterAttached(
                "BaseToolTip",
                typeof(object),
                typeof(ShortcutService),
                new PropertyMetadata(null, OnShortcutKeyChanged));

        public static object GetBaseToolTip(DependencyObject obj) => obj.GetValue(BaseToolTipProperty);
        public static void SetBaseToolTip(DependencyObject obj, object value) => obj.SetValue(BaseToolTipProperty, value);

        private static void OnShortcutKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                // 检查是否已经存在
                bool exists = false;
                foreach (var wr in _monitoredElements)
                {
                    if (wr.TryGetTarget(out var target) && target == element)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    _monitoredElements.Add(new WeakReference<FrameworkElement>(element));
                }

                UpdateToolTipBinding(element);
            }
        }

        private static void UpdateToolTipBinding(FrameworkElement element)
        {
            string key = GetShortcutKey(element);
            object baseContent = GetBaseToolTip(element);

            if (string.IsNullOrEmpty(key)) return;

            // 如果是 MenuItem，我们优先绑定 InputGestureText
            if (element is MenuItem menuItem)
            {
                var gestureBinding = new MultiBinding
                {
                    Converter = new MenuItemShortcutConverter()
                };
                gestureBinding.Bindings.Add(new Binding { Source = null }); // 占位
                gestureBinding.Bindings.Add(new Binding
                {
                    Source = ShortcutProvider.Instance,
                    Path = new PropertyPath("Shortcuts"),
                    Mode = BindingMode.OneWay
                });
                gestureBinding.ConverterParameter = key;
                menuItem.SetBinding(MenuItem.InputGestureTextProperty, gestureBinding);
            }
            else
            {
                var multiBinding = new MultiBinding
                {
                    Converter = new ShortcutToolTipConverter()
                };

                // 1. 绑定基础内容
                multiBinding.Bindings.Add(new Binding { Source = baseContent });

                // 2. 绑定到 Settings 中的 Shortcuts 字典
                multiBinding.Bindings.Add(new Binding
                {
                    Source = ShortcutProvider.Instance,
                    Path = new PropertyPath("Shortcuts"),
                    Mode = BindingMode.OneWay
                });
                multiBinding.ConverterParameter = key;
                element.SetBinding(FrameworkElement.ToolTipProperty, multiBinding);
            }
        }
    }

    public class MenuItemShortcutConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return "";
            var shortcuts = values[1] as Dictionary<string, ShortcutItem>;
            string shortcutKey = parameter as string;

            if (shortcuts != null && !string.IsNullOrEmpty(shortcutKey) && shortcuts.TryGetValue(shortcutKey, out var item))
            {
                string display = item.DisplayText;
                if (!string.IsNullOrEmpty(display) && display != LocalizationManager.GetString("L_Key_None"))
                {
                    return display;
                }
            }
            return "";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ShortcutToolTipConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return null;
            object baseContent = values[0];
            var shortcuts = values[1] as Dictionary<string, ShortcutItem>;
            string shortcutKey = parameter as string;

            string baseText = "";
            if (baseContent is string s && !string.IsNullOrEmpty(s))
            {
                // 尝试作为资源 Key 查找
                baseText = LocalizationManager.GetString(s);
            }
            else if (baseContent != null)
            {
                baseText = baseContent.ToString();
            }

            if (shortcuts != null && !string.IsNullOrEmpty(shortcutKey) && shortcuts.TryGetValue(shortcutKey, out var item))
            {
                string display = item.DisplayText;
                if (!string.IsNullOrEmpty(display) && display != LocalizationManager.GetString("L_Key_None"))
                {
                    return string.IsNullOrEmpty(baseText) ? display : $"{baseText} ({display})";
                }
            }

            return baseText;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
