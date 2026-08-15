using SteamShelf;
using SteamShelf.Media;
using SteamShelf.Placeables;
using SteamShelf.PlayerTools;
using SteamShelf.Save;
using System;
using UnityEngine;

namespace Boxroom_Books
{
    /// <summary>
    /// A loose, freely placeable book. It persists only a MediaRef in CustomData and
    /// rebuilds visual state from Books_Cache on load, avoiding duplicated metadata
    /// and cover bytes in RoomState. It also maintains BOXROOM's media reservation so
    /// a saved loose copy is not simultaneously offered by a shelf or Book Box.
    /// </summary>
    public class PlacedBookProp : MonoBehaviour, IPlaceable
    {
        [SerializeField]
        private string bookId = "";

        [SerializeField]
        private Renderer coverRenderer;

        [SerializeField]
        private BookDataProvider dataProvider;

        private PlaceableSaveState saveState;

        private MediaRef reservedRef;

        private bool providerEventsSubscribed;

        public PlaceableSaveState SaveState => saveState;

        private bool initialized;

        public BookData BookData
        {
            get
            {
                RestoreBookDataIfNeeded();

                return dataProvider != null
                    ? dataProvider.Data
                    : null;
            }
        }
        private bool RestoreBookDataIfNeeded()
        {
            if (dataProvider == null)
                return false;

            if (dataProvider.Data != null)
                return true;

            string id = bookId;

            // Fallback to the linked save state.
            if (string.IsNullOrWhiteSpace(id) &&
                saveState != null &&
                !string.IsNullOrWhiteSpace(saveState.CustomData))
            {
                MediaPropSaveData saveData =
                    JsonUtility.FromJson<MediaPropSaveData>(
                        saveState.CustomData);

                MediaRef mediaRef =
                    ResolveMediaRef(saveData);

                if (mediaRef.IsValid() &&
                    mediaRef.Type == BookMedia.Type)
                {
                    id = mediaRef.Id;
                    bookId = id;
                }
            }

            if (string.IsNullOrWhiteSpace(id))
                return false;

            dataProvider.SetBook(id);
            dataProvider.RequestCoverArt();

            return dataProvider.Data != null;
        }

        private Texture2D loadedCoverTexture;

        private void Awake()
        {
            SubscribeToProvider();

            PlayerInteractionTool.SpawnedMediaDemanded +=
                OnSpawnedMediaDemanded;
        }

        private void SubscribeToProvider()
        {
            if (dataProvider == null ||
                providerEventsSubscribed)
            {
                return;
            }

            dataProvider.OnMetadataReady +=
                HandleMetadataReady;

            dataProvider.OnCoverArtReady +=
                HandleCoverArtReady;

            providerEventsSubscribed = true;
        }

        private void UnsubscribeFromProvider()
        {
            if (dataProvider == null ||
                !providerEventsSubscribed)
            {
                return;
            }

            dataProvider.OnMetadataReady -=
                HandleMetadataReady;

            dataProvider.OnCoverArtReady -=
                HandleCoverArtReady;

            providerEventsSubscribed = false;
        }
        public void Initialize(
            BookDataProvider provider,
            Renderer renderer)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            if (renderer == null)
                throw new ArgumentNullException(nameof(renderer));

            if (initialized)
                return;

            UnsubscribeFromProvider();

            dataProvider = provider;
            coverRenderer = renderer;

            SubscribeToProvider();

            initialized = true;
        }

        private void OnDestroy()
        {
            UnsubscribeFromProvider();

            ClearReservation();

            PlayerInteractionTool.SpawnedMediaDemanded -=
                OnSpawnedMediaDemanded;
        }

        public void PopulateFromLoad(PlaceableSaveState state)
        {
            saveState = state;

            if (state == null || string.IsNullOrEmpty(state.CustomData))
            {
                Debug.LogError("PlacedBookProp has loaded with invalid custom data.");
                return;
            }

            MediaPropSaveData saveData =
                JsonUtility.FromJson<MediaPropSaveData>(state.CustomData);

            if (saveData == null)
            {
                Debug.LogError("PlacedBookProp - Save data is null.");
                return;
            }

            MediaRef id = ResolveMediaRef(saveData);

            if (!id.IsValid() || id.Type != BookMedia.Type)
            {
                Debug.LogError(
                    $"PlacedBookProp - Invalid book MediaRef: {state.CustomData}");

                return;
            }

            bookId = id.Id;

            reservedRef = id;
            PlaceableMediaContainer.ReserveMedia(id);

            dataProvider.SetBook(bookId);
            dataProvider.RequestCoverArt();

            dataProvider.SetBook(id.Id);
        }

        public void ApplyData(BookData book)
        {
            if (saveState == null)
            {
                saveState = new PlaceableSaveState(
                    "",
                    transform.position,
                    transform.rotation);
            }

            if (book != null)
            {
                bookId = book.Id;

                book.IsSpawned = true;
                book.IsInHand = true;

                saveState.CustomData = JsonUtility.ToJson(
                    new MediaPropSaveData
                    {
                        mediaType = (int)BookMedia.Type,
                        mediaId = book.Id
                    });
            }

            dataProvider.SetBook(book);
            dataProvider.RequestCoverArt();
        }

        public void LinkSaveState(PlaceableSaveState state)
        {
            saveState = state;
        }

        public void RefreshSaveStateReference(PlaceableSaveState newState)
        {
            saveState = newState;
        }


        public void OnPlaced()
        {
            if (!RestoreBookDataIfNeeded())
            {
                Debug.LogWarning(
                    $"PlacedBookProp could not restore book '{bookId}'.");

                return;
            }

            BookData.IsSpawned = true;
            BookData.IsInHand = false;
        }

        public void OnPickedUp()
        {
            if (BookData != null)
                BookData.IsInHand = true;
        }

        public void OnDeleted()
        {
            if (BookData != null)
            {
                BookData.IsSpawned = false;
                BookData.IsInHand = false;
            }
        }

        private void HandleMetadataReady(BookData data)
        {
            data.IsSpawned = true;

            ClearReservation();
        }

        private void HandleCoverArtReady(BookData data)
        {
            if (coverRenderer == null)
            {
                Debug.LogError(
                    "PlacedBookProp has no cover renderer.");

                return;
            }

            if (loadedCoverTexture != null)
            {
                Destroy(loadedCoverTexture);
                loadedCoverTexture = null;
            }

            if (data.CoverArtBytes != null &&
                data.CoverArtBytes.Length > 0)
            {
                loadedCoverTexture = new Texture2D(
                    2,
                    2,
                    TextureFormat.RGBA32,
                    mipChain: true);

                if (loadedCoverTexture.LoadImage(
                        data.CoverArtBytes))
                {
                    MaterialHelpers.SetTexture(
                        coverRenderer,
                        0,
                        loadedCoverTexture);

                    return;
                }

                Destroy(loadedCoverTexture);
                loadedCoverTexture = null;
            }
        }


        private void OnSpawnedMediaDemanded(
            MediaRef mediaRef)
        {
            if (BookData != null &&
                mediaRef.Type == BookMedia.Type &&
                BookData.Id == mediaRef.Id)
            {
                BookData.IsSpawned = false;
                BookData.IsInHand = true;

                Destroy(gameObject);
            }
        }
        private void ClearReservation()
        {
            if (reservedRef.IsValid())
            {
                PlaceableMediaContainer.UnreserveMedia(reservedRef);
                reservedRef = default;
            }
        }

        private static MediaRef ResolveMediaRef(MediaPropSaveData saveData)
        {
            if (!string.IsNullOrEmpty(saveData.mediaId))
            {
                return new MediaRef(
                    (eMediaType)saveData.mediaType,
                    saveData.mediaId);
            }

            return default;
        }
    }
}
