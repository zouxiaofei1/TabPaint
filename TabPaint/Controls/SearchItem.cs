using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace TabPaint.Controls
{
    public class SearchItem
    {
        public string Tag { get; }
        public string ParentTag { get; }
        public string DisplayName { get; }
        public string EnglishName { get; }
        public List<string> SearchTerms { get; }
        public string IconKey { get; }
        public Geometry IconPath { get; }

        public SearchItem(string tag, string displayName, string iconKey)
            : this(tag, tag, displayName, null, iconKey) { }

        public SearchItem(string tag, string parentTag, string displayName, string englishName, string iconKey)
        {
            Tag = tag;
            ParentTag = parentTag ?? tag;
            DisplayName = displayName;
            EnglishName = englishName;
            SearchTerms = new List<string> { displayName };
            if (!string.IsNullOrEmpty(englishName) &&
                !string.Equals(displayName, englishName, System.StringComparison.OrdinalIgnoreCase))
            {
                SearchTerms.Add(englishName);
            }

            IconKey = iconKey;
            IconPath = Application.Current?.TryFindResource(iconKey) as Geometry;
        }
    }
}
