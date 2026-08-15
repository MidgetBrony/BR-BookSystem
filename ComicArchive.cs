using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace BR_BookSystem
{
    /// <summary>One decoded comic page and its archive-relative name.</summary>
    internal sealed class ComicPage
    {
        internal string Name { get; set; }
        internal byte[] Bytes { get; set; }
    }

    /// <summary>
    /// Finds and reads CBZ/CBR files through SharpCompress. Page names are sorted
    /// naturally (2 before 10), matching the reading order expected by comic files.
    /// Archive data is returned as bytes so Unity textures can be created on demand.
    /// </summary>
    internal static class ComicArchive
    {
        internal static string Find(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return null;
            return Directory.EnumerateFiles(folder)
                .Where(path => IsComicArchive(path))
                .OrderBy(path => path, NaturalComparer.Instance)
                .FirstOrDefault();
        }

        internal static List<ComicPage> ReadPages(string archivePath)
        {
            if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
                throw new FileNotFoundException("Comic archive was not found.", archivePath);

            var pages = new List<ComicPage>();
            using IArchive archive = ArchiveFactory.OpenArchive(archivePath);
            foreach (IArchiveEntry entry in archive.Entries
                .Where(entry => !entry.IsDirectory && IsImage(entry.Key))
                .OrderBy(entry => entry.Key, NaturalComparer.Instance))
            {
                using Stream input = entry.OpenEntryStream();
                using var output = new MemoryStream();
                input.CopyTo(output);
                pages.Add(new ComicPage
                {
                    Name = Path.GetFileNameWithoutExtension(entry.Key),
                    Bytes = output.ToArray()
                });
            }
            return pages;
        }

        private static bool IsComicArchive(string path)
        {
            string extension = Path.GetExtension(path);
            return extension.Equals(".cbz", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".cbr", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsImage(string path) => new[] { ".jpg", ".jpeg", ".png", ".webp" }
            .Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

        /// <summary>Compares embedded page paths by their numeric segments.</summary>
        private sealed class NaturalComparer : IComparer<string>
        {
            internal static readonly NaturalComparer Instance = new();
            public int Compare(string x, string y) => StrCmpLogicalW(x, y);
            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
            private static extern int StrCmpLogicalW(string x, string y);
        }
    }
}
