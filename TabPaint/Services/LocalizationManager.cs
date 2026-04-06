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
        private static readonly Uri ZhTwDict = new Uri("pack://application:,,,/Resources/Lang.zh-TW.xaml", UriKind.Absolute);
        private static readonly Uri EnUsDict = new Uri("pack://application:,,,/Resources/Lang.en-US.xaml", UriKind.Absolute);
        private static readonly Uri JaJpDict = new Uri("pack://application:,,,/Resources/Lang.ja-JP.xaml", UriKind.Absolute);
        private static readonly Uri WhatsNewZhCnDict = new Uri("pack://application:,,,/Resources/WhatsNew.zh_cn.xaml", UriKind.Absolute);
        private static readonly Uri WhatsNewEnUsDict = new Uri("pack://application:,,,/Resources/WhatsNew.en_us.xaml", UriKind.Absolute);

        private static readonly System.Collections.Generic.Dictionary<string, (string Title, string Message)> FallbackTranslations = new()
        {
            ["en"] = ("Language Support", "It seems TabPaint doesn't have a full translation for your language yet. You are welcome to help us translate it or open an issue on GitHub!"),
            ["ko"] = ("언어 지원", "TabPaint에 아직 해당 언어에 대한 전체 번역이 없는 것 같습니다. 번역을 도와주시거나 GitHub에 이슈를 남겨주세요!"),
            ["fr"] = ("Support linguistique", "Il semble que TabPaint n'ait pas encore de traduction complète pour votre langue. Vous êtes invité à nous aider à traduire ou à ouvrir un ticket sur GitHub !"),
            ["de"] = ("Sprachunterstützung", "Es scheint, dass TabPaint noch keine vollständige Übersetzung für Ihre Sprache hat. Sie können uns gerne bei der Übersetzung helfen oder ein Problem auf GitHub melden!"),
            ["es"] = ("Soporte de idioma", "Parece que TabPaint aún no tiene una traducción completa para su idioma. ¡Le invitamos a ayudarnos a traducir o abrir un problema en GitHub!"),
            ["ru"] = ("Поддержка языка", "Похоже, что в TabPaint еще нет полного перевода на ваш язык. Вы можете помочь нам с переводом или создать тикет на GitHub!"),
            ["it"] = ("Supporto linguistico", "Sembra che TabPaint non abbia ancora una traduzione completa per la tua lingua. Sei il benvenuto ad aiutarci a tradurre o ad aprire una segnalazione su GitHub!"),
            ["pt"] = ("Suporte de idioma", "Parece que o TabPaint ainda não possui uma tradução completa para o seu idioma. Você é bem-vindo para nos ajudar a traduzir ou abrir um problema no GitHub!"),
            ["vi"] = ("Hỗ trợ ngôn ngữ", "Có vẻ như TabPaint vẫn chưa có bản dịch đầy đủ cho ngôn ngữ của bạn. Bạn có thể giúp chúng tôi dịch hoặc mở yêu cầu trên GitHub!"),
            ["th"] = ("การสนับสนุนด้านภาษา", "ดูเหมือนว่า TabPaint จะยังไม่มีการแปลภาษาของคุณอย่างสมบูรณ์ คุณสามารถช่วยเราแปลหรือแจ้งปัญหาบน GitHub ได้!"),
            ["ar"] = ("دعم اللغة", "يبدو أن TabPaint لا يحتوي على ترجمة كاملة للغتك بعد. نرحب بمساعدتك في الترجمة أو فتح تذكرة على GitHub!"),
            ["hi"] = ("भाषा समर्थन", "ऐसा लगता है कि TabPaint के पास अभी तक आपकी भाषा के लिए पूर्ण अनुवाद नहीं है। आप अनुवाद करने में हमारी सहायता कर सकते हैं या GitHub पर समस्या दर्ज कर सकते हैं!"),
            ["tr"] = ("Dil Desteği", "Görünüşe göre TabPaint'in henüz diliniz için tam bir çevirisi yok. Çevirmemize yardımcı olabilir veya GitHub'da bir sorun bildirebilirsiniz!"),
            ["nl"] = ("Taalondersteuning", "Het lijkt erop dat TabPaint nog geen volledige vertaling voor uw taal heeft. U bent van harte welkom om ons te helpen vertalen of een probleem te melden op GitHub!"),
            ["pl"] = ("Wsparcie językowe", "Wygląda na to, że TabPaint nie ma jeszcze pełnego tłumaczenia na Twój język. Zapraszamy do pomocy w tłumaczeniu lub zgłoszenia problemu na GitHubie!")
        };

        public static bool IsLanguageSupported(string cultureName)
        {
            if (string.IsNullOrEmpty(cultureName)) return false;
            // 检查当前已有的 XAML 资源
            return cultureName.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ||
                   cultureName.StartsWith("en", StringComparison.OrdinalIgnoreCase) ||
                   cultureName.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        }

        public static (string Title, string Message) GetMultilingualToast(string cultureName)
        {
            string key = cultureName.Split('-')[0].ToLowerInvariant();
            if (FallbackTranslations.TryGetValue(key, out var translation))
            {
                return translation;
            }
            return FallbackTranslations["en"];
        }

        public static void ApplyLanguage(AppLanguage language)
        {
            try
            {
                CultureInfo ci = language switch
                {
                    AppLanguage.ChineseTraditional => new CultureInfo("zh-TW"),
                    AppLanguage.English => new CultureInfo("en-US"),
                    AppLanguage.Japanese => new CultureInfo("ja-JP"),
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

            var target = language switch
            {
                AppLanguage.English => EnUsDict,
                AppLanguage.Japanese => JaJpDict,
                AppLanguage.ChineseTraditional => ZhTwDict,
                _ => ZhCnDict
            };

            // 当前仅有中/英 What's New 资源，繁中复用中文，日语复用英文
            var whatsNewTarget = language switch
            {
                AppLanguage.English => WhatsNewEnUsDict,
                AppLanguage.Japanese => WhatsNewEnUsDict,
                _ => WhatsNewZhCnDict
            };

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
