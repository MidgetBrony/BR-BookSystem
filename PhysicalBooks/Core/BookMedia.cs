using SteamShelf.Media;
using System;
using System.Collections.Generic;
using System.Text;

namespace Boxroom_Books
{
    /// <summary>
    /// Shared identity for the Books media type. Value 2 is the unused BOXROOM media
    /// slot selected by the original implementation; every save, shelf, and inspect
    /// check must use the same value for compatibility with existing rooms.
    /// </summary>
    public static class BookMedia
    {
        public const eMediaType Type = (eMediaType)2;
    }
}
