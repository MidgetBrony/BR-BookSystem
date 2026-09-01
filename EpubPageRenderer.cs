using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using VersOne.Epub;

namespace BR_BookSystem
{
    internal sealed class EpubReaderPage
    {
        internal string[] Lines { get; set; }
        internal byte[] ImageBytes { get; set; }
        internal string ImageName { get; set; }
        internal bool IsImage => ImageBytes != null && ImageBytes.Length > 0;
    }

    internal static class EpubPageRenderer
    {
        internal const int PageWidth = 1600;
        internal const int PageHeight = 2200;
        private const float Margin = 130f;
        private const float FooterSpace = 100f;
        private static readonly Regex NonContent = new(@"<(script|style|head)\b[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex ImageTag = new("<img\\b[^>]*?\\bsrc\\s*=\\s*(['\\\"])(.*?)\\1[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex ImageMarker = new(@"\[\[BR_EPUB_IMAGE:(\d+)\]\]", RegexOptions.Compiled);
        private static readonly Regex BlockEnd = new(@"</?(?:p|div|h[1-6]|li|blockquote|section|article|br|hr)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Tags = new(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex HorizontalSpace = new(@"[ \t\f\v]+", RegexOptions.Compiled);
        private static readonly Regex ExcessLines = new(@"\n{3,}", RegexOptions.Compiled);

        internal static List<EpubReaderPage> ReadAndPaginate(string path)
        {
            EpubBook book = EpubReader.ReadBook(path);
            var result = new List<EpubReaderPage>();
            float fontSize = EpubReaderSettings.FontSize;
            float lineHeight = fontSize * 1.38f;
            int linesPerPage = Math.Max(8, (int)Math.Floor((PageHeight - Margin * 2f - FooterSpace) / lineHeight));
            using SKTypeface typeface = EpubReaderSettings.CreateTypeface();
            using var font = new SKFont(typeface, fontSize);
            using var paint = new SKPaint { IsAntialias = true };

            foreach (EpubLocalTextContentFile chapter in book.ReadingOrder)
            {
                var sources = new List<string>();
                string marked = MarkImages(chapter.Content, sources);
                foreach (string part in Regex.Split(marked, @"(\[\[BR_EPUB_IMAGE:\d+\]\])"))
                {
                    Match marker = ImageMarker.Match(part.Trim());
                    if (marker.Success)
                    {
                        int index = int.Parse(marker.Groups[1].Value);
                        if (index < sources.Count && TryResolveImage(book, chapter.FilePath, sources[index], out EpubLocalByteContentFile image) && IsSupportedRaster(image.Content))
                            result.Add(new EpubReaderPage { ImageBytes = image.Content, ImageName = image.FilePath });
                    }
                    else AddTextPages(part, font, paint, linesPerPage, result);
                }
            }
            if (result.Count == 0) throw new InvalidOperationException("The EPUB contains no readable text or supported images.");
            return result;
        }

        internal static SKBitmap Render(EpubReaderPage page, int pageIndex, int pageCount)
        {
            var bitmap = new SKBitmap(PageWidth, PageHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(new SKColor(248, 244, 230));
            if (page.IsImage) DrawImage(canvas, page.ImageBytes);
            else DrawText(canvas, page.Lines ?? Array.Empty<string>());
            DrawFooter(canvas, pageIndex, pageCount);
            canvas.Flush();
            return bitmap;
        }

        private static void AddTextPages(string html, SKFont font, SKPaint paint, int linesPerPage, ICollection<EpubReaderPage> result)
        {
            string text = ToPlainText(html);
            if (string.IsNullOrWhiteSpace(text)) return;
            var lines = new List<string>();
            foreach (string paragraph in text.Split('\n'))
            {
                string clean = paragraph.Trim();
                if (clean.Length == 0) { if (lines.Count > 0 && lines[^1].Length != 0) lines.Add(string.Empty); continue; }
                WrapParagraph(clean, font, paint, PageWidth - Margin * 2f, lines);
                lines.Add(string.Empty);
            }
            for (int offset = 0; offset < lines.Count; offset += linesPerPage)
                result.Add(new EpubReaderPage { Lines = lines.Skip(offset).Take(linesPerPage).ToArray() });
        }

        private static string MarkImages(string html, List<string> sources)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;
            string text = NonContent.Replace(html, string.Empty);
            return ImageTag.Replace(text, match => { int i = sources.Count; sources.Add(WebUtility.HtmlDecode(match.Groups[2].Value)); return $"\n[[BR_EPUB_IMAGE:{i}]]\n"; });
        }

        private static bool TryResolveImage(EpubBook book, string chapterPath, string source, out EpubLocalByteContentFile image)
        {
            image = null;
            if (string.IsNullOrWhiteSpace(source) || source.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return false;
            string clean = Uri.UnescapeDataString(source.Split('#', '?')[0]).Replace('\\', '/');
            string directory = (Path.GetDirectoryName(chapterPath) ?? string.Empty).Replace('\\', '/');
            var parts = new List<string>();
            foreach (string part in (directory + "/" + clean).Split('/'))
            {
                if (part.Length == 0 || part == ".") continue;
                if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
                else parts.Add(part);
            }
            string resolved = string.Join("/", parts);
            if (book.Content.Images.TryGetLocalFileByFilePath(resolved, out image)) return true;
            image = book.Content.Images.Local.FirstOrDefault(candidate => string.Equals(candidate.FilePath, resolved, StringComparison.OrdinalIgnoreCase));
            return image != null;
        }

        private static bool IsSupportedRaster(byte[] bytes)
        {
            try
            {
                using SKBitmap image = SKBitmap.Decode(bytes);
                return image != null && image.Width > 0 && image.Height > 0;
            }
            catch
            {
                return false;
            }
        }

        private static string ToPlainText(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;
            string text = BlockEnd.Replace(html, "\n");
            text = Tags.Replace(text, string.Empty);
            text = WebUtility.HtmlDecode(text).Replace("\r", string.Empty).Replace('\u00a0', ' ')
                .Replace("\u2060", string.Empty).Replace("\u200b", string.Empty).Replace("\ufeff", string.Empty).Replace("\u00ad", string.Empty);
            return ExcessLines.Replace(HorizontalSpace.Replace(text, " "), "\n\n").Trim();
        }

        private static void DrawText(SKCanvas canvas, IReadOnlyList<string> lines)
        {
            float size = EpubReaderSettings.FontSize;
            using SKTypeface typeface = EpubReaderSettings.CreateTypeface();
            using var font = new SKFont(typeface, size) { Subpixel = true };
            using var paint = new SKPaint { Color = new SKColor(32, 29, 24), IsAntialias = true };
            float y = Margin + size;
            foreach (string line in lines) { if (line.Length > 0) canvas.DrawText(line, Margin, y, SKTextAlign.Left, font, paint); y += size * 1.38f; }
        }

        private static void DrawImage(SKCanvas canvas, byte[] bytes)
        {
            using SKBitmap image = SKBitmap.Decode(bytes);
            if (image == null) return;
            float availableWidth = PageWidth - Margin * 2f;
            float availableHeight = PageHeight - Margin * 2f - FooterSpace;
            float scale = Math.Min(availableWidth / image.Width, availableHeight / image.Height);
            float width = image.Width * scale, height = image.Height * scale;
            float left = (PageWidth - width) * 0.5f, top = Margin + (availableHeight - height) * 0.5f;
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(image, new SKRect(left, top, left + width, top + height), paint);
        }

        private static void DrawFooter(SKCanvas canvas, int index, int count)
        {
            using var font = new SKFont(SKTypeface.Default, 30f);
            using var paint = new SKPaint { Color = new SKColor(100, 94, 82), IsAntialias = true };
            canvas.DrawText($"{index + 1} / {count}", PageWidth * 0.5f, PageHeight - 55f, SKTextAlign.Center, font, paint);
        }

        private static void WrapParagraph(string paragraph, SKFont font, SKPaint paint, float width, ICollection<string> output)
        {
            string line = string.Empty;
            foreach (string word in paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && font.MeasureText(candidate, paint) > width) { output.Add(line); line = word; } else line = candidate;
            }
            if (line.Length > 0) output.Add(line);
        }
    }
}
