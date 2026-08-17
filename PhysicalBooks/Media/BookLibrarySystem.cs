using MelonLoader;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Boxroom_Books
{
    /// <summary>
    /// Owns the in-memory Books_Cache index. Each child folder is validated and
    /// translated from meta.json plus cover/archive files into BookData. Bad entries
    /// are isolated so one damaged book cannot prevent the rest of the library loading.
    /// </summary>
    public static class BookLibrarySystem
    {
        public static event Action<BookData> OnBookReady;

        public static event Action<IReadOnlyList<BookData>> OnLibraryReady;

        private static readonly Dictionary<string, BookData> books = new();

        public static IReadOnlyList<BookData> GetKnownBooks()
        {
            return books.Values.ToList();
        }

        public static BookData GetBookSync(string id)
        {
            books.TryGetValue(id, out var book);
            return book;
        }

        public static void Add(BookData book)
        {
            if (book == null ||
                string.IsNullOrWhiteSpace(book.Id))
            {
                return;
            }

            books[book.Id] = book;

            OnBookReady?.Invoke(book);

        }

        public static void Clear()
        {
            books.Clear();
        }

        public static void LoadCache()
        {
            books.Clear();

            string cacheRoot = BR_BookSystem.BookLibrarySettings.SourceRoot;

            if (string.IsNullOrWhiteSpace(cacheRoot))
            {
                OnLibraryReady?.Invoke(GetKnownBooks());
                return;
            }

            if (!Directory.Exists(cacheRoot))
            {
                MelonLogger.Warning($"Book cache not found: {cacheRoot}");
                return;
            }

            foreach (string folder in Directory.GetDirectories(
                cacheRoot,
                "*",
                SearchOption.AllDirectories))
            {
                LoadBook(folder);
            }

            OnLibraryReady?.Invoke(GetKnownBooks());
        }

        private static void LoadBook(string folder)
        {
            try
            {
                string metaFile = Path.Combine(folder, "meta.json");

                if (!File.Exists(metaFile))
                    return;

                BookMetadata meta =
                    JsonConvert.DeserializeObject<BookMetadata>(
                        File.ReadAllText(metaFile));

                if (meta == null)
                    return;

                BookData book = new(meta.BookID)
                {
                    FolderPath = folder,

                    Title = meta.Title,
                    Author = meta.Author,
                    Series = meta.Series,
                    Volume = meta.Volume,
                    Publisher = meta.Publisher,
                    Language = meta.Language,
                    BookType = meta.Type,

                    MetadataLoaded = true
                };

                string content = Directory.GetFiles(folder)
                    .FirstOrDefault(path => new[] { ".cbz", ".cbr", ".pdf", ".epub" }
                        .Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase));

                if (content != null)
                {
                    book.ContentPath = content;
                    book.Extension = Path.GetExtension(content).ToLowerInvariant();
                    book.FileSize = new FileInfo(content).Length;
                }

                string cover = Path.Combine(folder, "cover.jpg");

                if (File.Exists(cover))
                {
                    book.CoverArtBytes = File.ReadAllBytes(cover);
                    book.CoverArtLoaded = true;
                }

                Add(book);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed loading book folder '{folder}': {ex}");
            }
        }
    }
}
