using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;

namespace TabPaint
{
    public static class LocalizationManager
    {
        private static readonly Uri ZhCnDict = new Uri("pack://application:,,,/Resources/Lang.zh-CN.xaml", UriKind.Absolute);
        private static readonly Uri EnUsDict = new Uri("pack://application:,,,/Resources/Lang.en-US.xaml", UriKind.Absolute);
        private static readonly Uri WhatsNewZhCnDict = new Uri("pack://application:,,,/Resources/WhatsNew.zh_cn.xaml", UriKind.Absolute);
        private static readonly Uri WhatsNewEnUsDict = new Uri("pack://application:,,,/Resources/WhatsNew.en_us.xaml", UriKind.Absolute);

        public static void ApplyLanguage(AppLanguage language)
        {
            try
            {
                CultureInfo ci = language switch
                {
                    AppLanguage.English => new CultureInfo("en-US"),
                    _ => new CultureInfo("zh-CN")
                };

                CultureInfo.DefaultThreadCurrentCulture = ci;
                CultureInfo.DefaultThreadCurrentUICulture = ci;
                Thread.CurrentThread.CurrentCulture = ci;
                Thread.CurrentThread.CurrentUICulture = ci;
            }
            catch (global::System.Exception ex) { global::System.Diagnostics.Debug.WriteLine(ex); }

            var app = Application.Current;
            if (app == null) return;

            var target = language == AppLanguage.English ? EnUsDict : ZhCnDict;
            var whatsNewTarget = language == AppLanguage.English ? WhatsNewEnUsDict : WhatsNewZhCnDict;

            // remove existing language dictionaries
            var existing = app.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && IsLanguageDictionary(d.Source));

            var existingWhatsNew = app.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && IsWhatsNewDictionary(d.Source));

            bool sameMain = existing != null && existing.Source == target;
            bool sameWhatsNew = existingWhatsNew != null && existingWhatsNew.Source == whatsNewTarget;

            if (sameMain && sameWhatsNew) return;

            if (existing != null)
            {
                app.Resources.MergedDictionaries.Remove(existing);
            }

            if (existingWhatsNew != null)
            {
                app.Resources.MergedDictionaries.Remove(existingWhatsNew);
            }

            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = target });
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = whatsNewTarget });
        }

        private static bool IsLanguageDictionary(Uri source)
        {
            var s = source.OriginalString;
            return s.Contains("/Resources/Lang.", StringComparison.OrdinalIgnoreCase)
                   && s.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWhatsNewDictionary(Uri source)
        {
            var s = source.OriginalString;
            return s.Contains("/Resources/WhatsNew.", StringComparison.OrdinalIgnoreCase)
                   && s.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetString(string key)
        {
            var app = Application.Current;
            if (app != null && app.TryFindResource(key) is string val)
            {
                return val;
            }
            return key;
        }
    }
}
