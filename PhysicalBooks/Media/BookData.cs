using SteamShelf.Media;

namespace Boxroom_Books;

/// <summary>
/// Runtime representation of one Books_Cache entry. It implements BOXROOM's media
/// contract while retaining book-specific metadata used by inspect and PageFlip.
/// Spawn/hand flags prevent the same cache item appearing in multiple containers.
/// </summary>
public sealed class BookData : IMediaItem
{
    public readonly string Id;

    public string FolderPath { get; internal set; } = "";

    public string ContentPath { get; internal set; } = "";

    public string Title { get; internal set; } = "";

    public string Author { get; internal set; } = "";

    public string Series { get; internal set; } = "";

    public string Summary { get; internal set; } = "";

    public string Publisher { get; internal set; } = "";

    public string Isbn { get; internal set; } = "";

    public string Language { get; internal set; } = "";

    public string BookType { get; internal set; } = "";

    public string Volume { get; internal set; } = "";

    public string CoverArtPath { get; internal set; } = "";

    public float CoverAspectRatio { get; internal set; }

    public byte[] CoverArtBytes
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CoverArtPath) ||
                !System.IO.File.Exists(CoverArtPath))
            {
                return null;
            }

            try
            {
                // Compatibility path for IMediaItem consumers. Do not retain the
                // encoded file in every BookData instance.
                return System.IO.File.ReadAllBytes(CoverArtPath);
            }
            catch
            {
                return null;
            }
        }
    }

    public bool CoverArtLoaded =>
        !string.IsNullOrWhiteSpace(CoverArtPath) &&
        System.IO.File.Exists(CoverArtPath);

    public bool MetadataLoaded { get; internal set; }

    public DateTime LastScanned { get; internal set; }

    public long FileSize { get; internal set; }

    public string Extension { get; internal set; }
    public bool IsFullyLoaded => MetadataLoaded;

    public bool IsSpawned { get; set; }

    public bool IsInHand { get; set; }

    MediaRef IMediaItem.Ref => new MediaRef(BookMedia.Type, Id);

    string IMediaItem.DisplayName => Title;

    bool IMediaItem.CoverArtLoaded => CoverArtLoaded;

    byte[] IMediaItem.CoverArtBytes => CoverArtBytes;

    public BookData(string id)
    {
        Id = id;
    }
}

/// <summary>
/// PascalCase meta.json contract. Property names deliberately match existing book
/// folders and must not be casually renamed without a cache migration.
/// </summary>
public sealed class BookMetadata
{
    public int Version { get; set; }

    public string BookID { get; set; } = "";

    public string Title { get; set; } = "";

    public string Series { get; set; } = "";

    public string Volume { get; set; } = "";

    public string Summary { get; set; } = "";

    public string Author { get; set; } = "";

    public string Publisher { get; set; } = "";

    public string ISBN { get; set; } = "";

    public string Language { get; set; } = "";

    public string Type { get; set; } = "";
}
