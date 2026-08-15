using SteamShelf.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Boxroom_Books
{
    /// <summary>
    /// Exposes the loaded book collection through BOXROOM's IMediaLibrary API. This
    /// is the bridge that lets shelves, held media, and inspectors resolve a MediaRef
    /// back to the authoritative BookData instance.
    /// </summary>
    public sealed class BookMediaLibrary : IMediaLibrary
    {
        public eMediaType HandledType => BookMedia.Type;

        public event Action<IMediaItem> OnItemReady;

        public event Action<IReadOnlyList<IMediaItem>> OnLibraryReady;

        public BookMediaLibrary()
        {
            BookLibrarySystem.OnBookReady += HandleBookReady;
            BookLibrarySystem.OnLibraryReady += HandleLibraryReady;
        }

        public IReadOnlyList<IMediaItem> GetKnownItems()
        {
            return BookLibrarySystem
                .GetKnownBooks()
                .Cast<IMediaItem>()
                .ToList();
        }

        public IMediaItem GetItemSync(MediaRef mediaRef)
        {
            if (mediaRef.Type != BookMedia.Type)
                return null;

            return BookLibrarySystem.GetBookSync(mediaRef.Id);
        }

        public Task<IMediaItem> GetItemAsync(MediaRef mediaRef)
        {
            return Task.FromResult(GetItemSync(mediaRef));
        }

        private void HandleBookReady(BookData book)
        {
            OnItemReady?.Invoke(book);
        }

        private void HandleLibraryReady(
            IReadOnlyList<BookData> books)
        {
            OnLibraryReady?.Invoke(
                books.Cast<IMediaItem>().ToList());
        }
    }
}
