using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;

namespace Boxroom_Books
{
    /// <summary>Reads the standard metadata.opf written by Calibre book folders.</summary>
    internal static class CalibreOpfReader
    {
        private const string OpfNamespace = "http://www.idpf.org/2007/opf";
        private const string DcNamespace = "http://purl.org/dc/elements/1.1/";

        internal static BookMetadata Read(string opfPath, string folder)
        {
            var document = new XmlDocument();
            document.Load(opfPath);

            var namespaces = new XmlNamespaceManager(document.NameTable);
            namespaces.AddNamespace("opf", OpfNamespace);
            namespaces.AddNamespace("dc", DcNamespace);

            XmlNode metadata = document.SelectSingleNode("//opf:metadata", namespaces);
            if (metadata == null)
                throw new InvalidDataException("The OPF file does not contain an OPF metadata element.");

            string[] authors = metadata.SelectNodes("dc:creator", namespaces)?
                .Cast<XmlNode>()
                .Select(Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<string>();

            string[] subjects = metadata.SelectNodes("dc:subject", namespaces)?
                .Cast<XmlNode>()
                .Select(Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray() ?? Array.Empty<string>();

            return new BookMetadata
            {
                Version = 2,
                BookID = ReadIdentifier(metadata, namespaces, folder),
                Title = Value(metadata.SelectSingleNode("dc:title", namespaces)),
                Series = ReadSeries(metadata, namespaces),
                Volume = ReadSeriesIndex(metadata, namespaces),
                Summary = PlainText(Value(metadata.SelectSingleNode("dc:description", namespaces))),
                Author = string.Join(", ", authors),
                Publisher = Value(metadata.SelectSingleNode("dc:publisher", namespaces)),
                ISBN = ReadIsbn(metadata, namespaces),
                Language = NormalizeLanguage(Value(metadata.SelectSingleNode("dc:language", namespaces))),
                Type = InferType(subjects, folder)
            };
        }

        private static string ReadIdentifier(XmlNode metadata, XmlNamespaceManager namespaces, string folder)
        {
            XmlNode[] identifiers = metadata.SelectNodes("dc:identifier", namespaces)?
                .Cast<XmlNode>()
                .Where(node => !string.IsNullOrWhiteSpace(Value(node)))
                .ToArray() ?? Array.Empty<XmlNode>();

            XmlNode preferred = identifiers.FirstOrDefault(node =>
                string.Equals(Attribute(node, OpfNamespace, "scheme"), "uuid", StringComparison.OrdinalIgnoreCase) ||
                node.Attributes?["id"]?.Value?.IndexOf("uuid", StringComparison.OrdinalIgnoreCase) >= 0);
            preferred ??= identifiers.FirstOrDefault(node =>
                !string.Equals(Attribute(node, OpfNamespace, "scheme"), "isbn", StringComparison.OrdinalIgnoreCase));
            preferred ??= identifiers.FirstOrDefault();

            string identifier = Value(preferred);
            if (identifier.StartsWith("urn:uuid:", StringComparison.OrdinalIgnoreCase))
                identifier = identifier.Substring("urn:uuid:".Length);
            return string.IsNullOrWhiteSpace(identifier)
                ? Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                : identifier;
        }

        private static string ReadSeries(XmlNode metadata, XmlNamespaceManager namespaces)
        {
            XmlNode calibre = metadata.SelectSingleNode("opf:meta[@name='calibre:series']", namespaces);
            string value = calibre?.Attributes?["content"]?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(value)) return value;

            XmlNode collection = FindSeriesCollection(metadata, namespaces);
            return Value(collection);
        }

        private static string ReadIsbn(XmlNode metadata, XmlNamespaceManager namespaces)
        {
            XmlNode[] identifiers = metadata.SelectNodes("dc:identifier", namespaces)?
                .Cast<XmlNode>()
                .ToArray() ?? Array.Empty<XmlNode>();

            foreach (XmlNode identifier in identifiers)
            {
                string scheme = Attribute(identifier, OpfNamespace, "scheme");
                string id = identifier.Attributes?["id"]?.Value ?? string.Empty;
                string value = Value(identifier);
                bool markedAsIsbn = scheme.IndexOf("isbn", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    id.IndexOf("isbn", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    value.StartsWith("urn:isbn:", StringComparison.OrdinalIgnoreCase);
                if (!markedAsIsbn && !string.IsNullOrWhiteSpace(id))
                {
                    XmlNode type = metadata.SelectSingleNode(
                        $"opf:meta[@refines='#{id}' and @property='identifier-type']",
                        namespaces);
                    string typeValue = Value(type);
                    markedAsIsbn = typeValue.IndexOf("isbn", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   typeValue == "02" || typeValue == "15";
                }
                if (!markedAsIsbn) continue;
                if (value.StartsWith("urn:isbn:", StringComparison.OrdinalIgnoreCase))
                    value = value.Substring("urn:isbn:".Length);
                return value.Trim();
            }

            return string.Empty;
        }

        private static string ReadSeriesIndex(XmlNode metadata, XmlNamespaceManager namespaces)
        {
            XmlNode calibre = metadata.SelectSingleNode("opf:meta[@name='calibre:series_index']", namespaces);
            string value = calibre?.Attributes?["content"]?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(value)) return value;

            XmlNode collection = FindSeriesCollection(metadata, namespaces);
            string id = collection?.Attributes?["id"]?.Value;
            XmlNode position = string.IsNullOrWhiteSpace(id)
                ? null
                : metadata.SelectSingleNode(
                    $"opf:meta[@refines='#{id}' and @property='group-position']",
                    namespaces);
            return Value(position);
        }

        private static XmlNode FindSeriesCollection(XmlNode metadata, XmlNamespaceManager namespaces)
        {
            XmlNode[] collections = metadata.SelectNodes(
                    "opf:meta[@property='belongs-to-collection']",
                    namespaces)?
                .Cast<XmlNode>()
                .ToArray() ?? Array.Empty<XmlNode>();

            foreach (XmlNode collection in collections)
            {
                string id = collection.Attributes?["id"]?.Value;
                if (string.IsNullOrWhiteSpace(id)) continue;
                XmlNode type = metadata.SelectSingleNode(
                    $"opf:meta[@refines='#{id}' and @property='collection-type']",
                    namespaces);
                if (string.Equals(Value(type), "series", StringComparison.OrdinalIgnoreCase))
                    return collection;
            }

            return collections.FirstOrDefault();
        }

        private static string InferType(IEnumerable<string> subjects, string folder)
        {
            string[] knownTypes = { "Manga", "Graphic Novel", "Comic", "Magazine", "Hardcover", "Paperback", "Book" };
            foreach (string subject in subjects)
            {
                string match = knownTypes.FirstOrDefault(type =>
                    string.Equals(subject.Trim(), type, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            string extension = Directory.GetFiles(folder)
                .Select(Path.GetExtension)
                .FirstOrDefault(value =>
                    string.Equals(value, ".cbz", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, ".cbr", StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(extension) ? "Book" : "Comic";
        }

        private static string PlainText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string text = Regex.Replace(value, @"(?i)<br\s*/?>", "\n");
            text = Regex.Replace(text, @"(?i)</(?:p|div|li|h[1-6])\s*>", "\n");
            text = Regex.Replace(text, @"<[^>]+>", string.Empty);
            text = WebUtility.HtmlDecode(text);
            text = Regex.Replace(text, @"[ \t]+", " ");
            text = Regex.Replace(text, @"(?:\r?\n\s*){3,}", "\n\n");
            return text.Trim();
        }

        private static string NormalizeLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language)) return string.Empty;
            return language.Trim().ToLowerInvariant() switch
            {
                "eng" => "en", "nor" => "no", "nob" => "no", "nno" => "no",
                "jpn" => "ja", "deu" => "de", "ger" => "de", "fra" => "fr",
                "fre" => "fr", "spa" => "es", "ita" => "it", "kor" => "ko",
                "zho" => "zh", "chi" => "zh", _ => language.Trim()
            };
        }

        private static string Attribute(XmlNode node, string namespaceUri, string name) =>
            node?.Attributes?[name, namespaceUri]?.Value ?? string.Empty;

        private static string Value(XmlNode node) => node?.InnerText?.Trim() ?? string.Empty;
    }
}
