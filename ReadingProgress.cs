using MelonLoader;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace BR_BookSystem
{
    /// <summary>Persists the current PageFlip spread for each stable BookID.</summary>
    internal static class ReadingProgress
    {
        private static MelonPreferences_Entry<string> savedPages;
        private static Dictionary<string, int> pages = new(StringComparer.Ordinal);

        internal static void Initialize()
        {
            MelonPreferences_Category category = MelonPreferences.CreateCategory("BRBookSystemReader", "BR-BookSystem Reader");
            savedPages = category.CreateEntry("LastPages", "{}", "Last page read by BookID");
            try
            {
                pages = JsonConvert.DeserializeObject<Dictionary<string, int>>(savedPages.Value)
                    ?? new Dictionary<string, int>(StringComparer.Ordinal);
            }
            catch (Exception ex)
            {
                pages = new Dictionary<string, int>(StringComparer.Ordinal);
                MelonLogger.Warning($"Could not read saved book progress; starting fresh: {ex.Message}");
            }
        }

        internal static int Get(string bookId, int pageCount, bool rightToLeft)
        {
            if (string.IsNullOrWhiteSpace(bookId) || pageCount <= 0 || !pages.TryGetValue(bookId, out int page))
                return rightToLeft ? pageCount - 1 : 0;
            int logicalLastSpread = rightToLeft
                ? ((pageCount - 1) / 2) * 2
                : (pageCount % 2 == 0 ? pageCount : pageCount - 1);
            page = Math.Max(0, Math.Min(logicalLastSpread, page - page % 2));
            return rightToLeft ? pageCount - 1 - page : page;
        }

        internal static void Save(string bookId, int readerPage, int pageCount, bool rightToLeft)
        {
            if (string.IsNullOrWhiteSpace(bookId)) return;
            int page = rightToLeft ? pageCount - 1 - readerPage : readerPage;
            page = Math.Max(0, page - page % 2);
            if (pages.TryGetValue(bookId, out int existing) && existing == page) return;
            pages[bookId] = page;
            savedPages.Value = JsonConvert.SerializeObject(pages);
            MelonPreferences.Save();
        }
    }
}
