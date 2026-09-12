using SteamShelf.Media;
using System;
using System.Collections.Generic;
using System.Text;

namespace Boxroom_Books
{
    /// <summary>
    /// Shared identity for the Books media type. Custom media IDs live at 1000+
    /// so BOXROOM can use its built-in IDs without colliding with this mod.
    /// Every save, shelf, and inspect check must use the same value.
    /// </summary>
    public static class BookMedia
    {
        public const eMediaType Type = (eMediaType)1002;
    }
}
