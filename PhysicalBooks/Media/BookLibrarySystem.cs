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
                BookMetadata meta = LoadMetadata(folder);

                if (meta == null)
                    return;

                BookData book = new(meta.BookID)
                {
                    FolderPath = folder,

                    Title = meta.Title,
                    Author = meta.Author,
                    Series = meta.Series,
                    Summary = meta.Summary,
                    Volume = meta.Volume,
                    Publisher = meta.Publisher,
                    Isbn = meta.ISBN,
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
                    // Keep only the path in the library index. Cover bytes and the
                    // decoded texture are loaded on demand for exposed books.
                    book.CoverArtPath = cover;
                    book.CoverAspectRatio = CoverImageInfo.ReadAspectRatio(cover);
                }

                Add(book);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed loading book folder '{folder}': {ex}");
            }
        }

        private static BookMetadata LoadMetadata(string folder)
        {
            string metaFile = Path.Combine(folder, "meta.json");
            string opfFile = Path.Combine(folder, "metadata.opf");
            BookMetadata existing = File.Exists(metaFile)
                ? JsonConvert.DeserializeObject<BookMetadata>(File.ReadAllText(metaFile))
                : null;

            if (BR_BookSystem.BookMetadataSettings.PreferCalibreOpf && File.Exists(opfFile))
            {
                try
                {
                    BookMetadata calibre = CalibreOpfReader.Read(opfFile, folder);

                    // Shelf and loose-prop saves resolve media by BookID. Keep the
                    // established BR-BookSystem identity when a converted metadata
                    // file already exists, while allowing OPF to own descriptive data.
                    if (!string.IsNullOrWhiteSpace(existing?.BookID))
                        calibre.BookID = existing.BookID;

                    // Type is BR-BookSystem behavior (not standard bibliographic
                    // metadata) and controls thickness plus Manga reading direction.
                    if (!string.IsNullOrWhiteSpace(existing?.Type))
                        calibre.Type = existing.Type;

                    return calibre;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Could not read Calibre metadata '{opfFile}'; falling back to meta.json: {ex.Message}");
                }
            }

            return existing;
        }
    }
}
