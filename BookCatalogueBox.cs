using Boxroom_Books;
using HarmonyLib;
using MelonLoader;
using SteamShelf;
using SteamShelf.Items;
using SteamShelf.Media;
using SteamShelf.Placeables;
using SteamShelf.PlayerTools;
using SteamShelf.Save;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace BR_BookSystem
{
    /// <summary>
    /// Registers the Book Box as the first mod-catalogue item. Its catalogue data
    /// borrows the vanilla CD Album Box behaviour while its prefab and contents are
    /// supplied by this mod.
    /// </summary>
    internal static class BookCatalogueBox
    {
        internal const string Id = "BoxroomPlus_BookBox";
        internal const string AlbumBoxId = "Placeable_UnplacedAlbumBox";
        internal static PlaceableData Data { get; private set; }
        internal static PlaceableData AlbumTemplate { get; private set; }

        internal static void Register()
        {
            if (!Singleton<PlaceableManager>.HasInstance()) return;
            PlaceableManager manager = Singleton<PlaceableManager>.Instance;
            if (Data != null || manager.GetDataByID(Id) != null) return;

            AlbumTemplate = manager.GetDataByID(AlbumBoxId);
            if (AlbumTemplate == null)
            {
                MelonLogger.Error("Cannot register Book Box: vanilla CD Album Box data was not found.");
                return;
            }

            Data = ScriptableObject.CreateInstance<PlaceableData>();
            Data.ID = Id;
            Data.DisplayName = "Book Box";
            Data.DisplayDescription = "A container for all of your unplaced books";
            Data.PlacementType = AlbumTemplate.PlacementType;
            Data.SetToolType(EToolType.Placeable);
            Data.IsLoadedFromMod = true;
            Data.ModScale = 1f;

            var modsField = AccessTools.Field(typeof(PlaceableManager), "modsPlaceables");
            var mods = modsField?.GetValue(manager) as List<PlaceableData>;
            if (mods == null)
            {
                MelonLogger.Error("Cannot register Book Box: mod catalogue list was unavailable.");
                return;
            }

            mods.Insert(0, Data);
            manager.AllPlaceables.Insert(0, Data);
        }

        internal static async Task<GameObject> Instantiate()
        {
            if (AlbumTemplate == null) return null;
            GameObject instance = await Singleton<PlaceableManager>.Instance.InstantiatePlaceableAsync(AlbumTemplate);
            if (instance == null) return null;

            foreach (UnplacedAlbumsBox albums in instance.GetComponentsInChildren<UnplacedAlbumsBox>(true))
                UnityEngine.Object.DestroyImmediate(albums);
            instance.AddComponent<UnplacedBooksBox>();
            instance.name = "Book Box";
            PlacementTag tag = instance.GetComponent<PlacementTag>();
            if (tag != null) tag.PlaceableData = Data;
            return instance;
        }

        internal static Task<Sprite> GetSprite() => AlbumTemplate != null
            ? Singleton<PlaceableManager>.Instance.GetSpriteAsync(AlbumTemplate)
            : Task.FromResult<Sprite>(null);
    }

    /// <summary>
    /// Saveable catalogue prop that acts as a source of unplaced books. Selecting a
    /// book delegates to BOXROOM's normal held-media flow, which preserves placement
    /// controls and avoids maintaining a second inventory implementation.
    /// </summary>
    public sealed class UnplacedBooksBox : MonoBehaviour, IPlaceable
    {
        private PlaceableSaveState saveState;
        private PlaceableMediaContainer[] containers;
        private bool initialized;
        private bool canAddBooks;

        public PlaceableSaveState SaveState => saveState;

        private void Awake() => containers = GetComponentsInChildren<PlaceableMediaContainer>(true);

        private void OnDestroy()
        {
            Shelf.OnShelfPutAway = (Action)Delegate.Remove(Shelf.OnShelfPutAway, new Action(FillAllWithUnplaced));
        }

        public void OnPlaced()
        {
            Initialize();
            Shelf.OnShelfPutAway = (Action)Delegate.Remove(Shelf.OnShelfPutAway, new Action(FillAllWithUnplaced));
            Shelf.OnShelfPutAway = (Action)Delegate.Combine(Shelf.OnShelfPutAway, new Action(FillAllWithUnplaced));
        }

        public void OnPickedUp()
        {
            Shelf.OnShelfPutAway = (Action)Delegate.Remove(Shelf.OnShelfPutAway, new Action(FillAllWithUnplaced));
            ClearContainers();
        }

        public void OnDeleted() => ClearContainers();

        public void PopulateFromLoad(PlaceableSaveState state)
        {
            saveState = state;
            Initialize();
            Shelf.OnShelfPutAway = (Action)Delegate.Remove(Shelf.OnShelfPutAway, new Action(FillAllWithUnplaced));
            Shelf.OnShelfPutAway = (Action)Delegate.Combine(Shelf.OnShelfPutAway, new Action(FillAllWithUnplaced));
        }

        public void LinkSaveState(PlaceableSaveState state) => saveState = state;
        public void RefreshSaveStateReference(PlaceableSaveState state) => saveState = state;

        private void Initialize()
        {
            if (initialized) return;
            containers = GetComponentsInChildren<PlaceableMediaContainer>(true);
            foreach (PlaceableMediaContainer container in containers)
            {
                container.Initialise();
                container.OnItemPickedFromSlot = (Action<PlaceableMediaContainer, int>)Delegate.Combine(
                    container.OnItemPickedFromSlot, new Action<PlaceableMediaContainer, int>(OnBookPicked));
            }
            initialized = true;
            StartCoroutine(WaitForRoomThenFill());
        }

        private IEnumerator WaitForRoomThenFill()
        {
            while (Singleton<RoomDataManager>.Instance.IsSpawningRoom) yield return null;
            canAddBooks = true;
            FillAllWithUnplaced();
        }

        private void FillAllWithUnplaced()
        {
            if (!canAddBooks) return;
            foreach (BookData book in BookLibrarySystem.GetKnownBooks().OrderBy(b => b.Title, StringComparer.OrdinalIgnoreCase))
            {
                if (!CanAdd(book)) continue;
                bool placed = false;
                foreach (PlaceableMediaContainer container in containers)
                {
                    for (int slot = 0; slot < container.MaxGameCount; slot++)
                    {
                        if (container.IsItemInSlot(slot)) continue;
                        container.PlaceItem(book, slot, playPlacedTween: false);
                        placed = true;
                        break;
                    }
                    if (placed) break;
                }
            }
        }

        private static bool CanAdd(BookData book)
        {
            if (book == null || book.IsSpawned || !book.IsFullyLoaded || book.IsInHand) return false;
            MediaRef mediaRef = ((IMediaItem)book).Ref;
            return !PlaceableMediaContainer.IsMediaReserved(mediaRef);
        }

        private void OnBookPicked(PlaceableMediaContainer container, int slot) => FillAllWithUnplaced();

        private void ClearContainers()
        {
            if (containers == null) return;
            foreach (PlaceableMediaContainer container in containers) container.ClearAll();
        }
    }

    /// <summary>Routes the custom Book Box ID to the bundled prefab.</summary>
    [HarmonyPatch(typeof(PlaceableManager), nameof(PlaceableManager.InstantiatePlaceableAsync))]
    internal static class InstantiateBookBoxPatch
    {
        private static bool Prefix(PlaceableData data, ref Task<GameObject> __result)
        {
            if (data == null || data.ID != BookCatalogueBox.Id) return true;
            __result = BookCatalogueBox.Instantiate();
            return false;
        }
    }

    /// <summary>Provides the Book Box cover image to the mod catalogue.</summary>
    [HarmonyPatch(typeof(PlaceableManager), nameof(PlaceableManager.GetSpriteAsync))]
    internal static class BookBoxSpritePatch
    {
        private static bool Prefix(PlaceableData data, ref Task<Sprite> __result)
        {
            if (data == null || data.ID != BookCatalogueBox.Id) return true;
            __result = BookCatalogueBox.GetSprite();
            return false;
        }
    }
}
