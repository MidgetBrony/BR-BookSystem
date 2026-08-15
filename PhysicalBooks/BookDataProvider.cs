using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Boxroom_Books
{
    /// <summary>
    /// Unity-facing adapter between prefab components and BookLibrarySystem. Prefabs
    /// can bind once to its events while save restoration or shelf reuse changes the
    /// selected BookData underneath them.
    /// </summary>
    public class BookDataProvider : MonoBehaviour
    {
        public string BookId { get; private set; } = "";

        public BookData Data { get; private set; }

        public bool MetadataReady { get; private set; }

        public bool CoverArtReady { get; private set; }

        public event Action<BookData> OnMetadataReady;

        public event Action<BookData> OnCoverArtReady;

        public void SetBook(string id)
        {
            Clear();

            if (string.IsNullOrWhiteSpace(id))
                return;

            BookId = id;
            Data = BookLibrarySystem.GetBookSync(id);

            if (Data == null)
                return;

            MetadataReady = true;
            CoverArtReady = Data.CoverArtLoaded;

            OnMetadataReady?.Invoke(Data);
        }

        public void SetBook(BookData book)
        {
            Clear();

            if (book == null)
                return;

            BookId = book.Id;
            Data = book;

            MetadataReady = true;
            CoverArtReady = book.CoverArtLoaded;

            OnMetadataReady?.Invoke(book);
        }

        public void RequestCoverArt()
        {
            if (Data == null)
                return;

            CoverArtReady =
                Data.CoverArtLoaded &&
                Data.CoverArtBytes != null &&
                Data.CoverArtBytes.Length > 0;

            if (CoverArtReady)
                OnCoverArtReady?.Invoke(Data);
        }

        public void Clear()
        {
            BookId = "";
            Data = null;
            MetadataReady = false;
            CoverArtReady = false;
        }
    }
}
