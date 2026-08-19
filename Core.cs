using HarmonyLib;
using MelonLoader;
using SteamShelf;
using SteamShelf.Media;
using SteamShelf.Placeables;
using System.Threading.Tasks;
using UnityEngine;

[assembly: MelonInfo(typeof(BR_BookSystem.Core), "BR-BookSystem", "1.1.0", "Rusty", null)]
[assembly: MelonGame("NestedLoop", "BOXROOM")]

namespace BR_BookSystem
{
    /// <summary>
    /// MelonLoader entry point. Loads the shared prefab bundle, registers the
    /// catalogue item, and creates the persistent runtime used by book inspect/read.
    /// Game-specific integration is kept in Harmony patches below so startup stays
    /// small and failures can be traced to the BOXROOM system being extended.
    /// </summary>
    public sealed class Core : MelonMod
    {
        public override void OnInitializeMelon()
        {
            if (!Boxroom_Books.BookAssetBundle.Load()) LoggerInstance.Warning("Original book asset bundle could not be loaded.");
            BookLibrarySettings.RegisterPanel();
            SteamShelf.Placeables.PlaceableManager.PlaceableDataLoaded += BookCatalogueBox.Register;
        }

        public override void OnDeinitializeMelon()
        {
            SteamShelf.Placeables.PlaceableManager.PlaceableDataLoaded -= BookCatalogueBox.Register;
            Boxroom_Books.BookAssetBundle.Unload();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (BookInspectRuntime.Instance == null)
            {
                var host = new GameObject("BR-BookInspect");
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<BookInspectRuntime>();
            }
        }

    }

    /// <summary>
    /// Registers Books after BOXROOM creates its media bootstrap. Registering any
    /// earlier is unreliable because the game's media-library collection does not
    /// exist yet.
    /// </summary>
    [HarmonyPatch(typeof(MediaBootstrap), "Initialize")]
    internal static class RegisterOriginalBookLibraryPatch
    {
        private static void Postfix()
        {
            MediaLibraryRouter.UnRegister(Boxroom_Books.BookMedia.Type);
            MediaLibraryRouter.Register(new Boxroom_Books.BookMediaLibrary());
        }
    }

    /// <summary>
    /// Scans Books_Cache once BOXROOM has configured its library systems. This
    /// mirrors the point at which the built-in media libraries become usable.
    /// </summary>
    [HarmonyPatch(typeof(SteamLibrarySystem), "Configure")]
    internal static class LoadOriginalBookCachePatch
    {
        private static void Postfix() => Boxroom_Books.BookLibrarySystem.LoadCache();
    }

    // Free-placed books save with their own PlaceableData ID. The data is kept
    // out of the object catalogue, but must resolve while RoomDataManager is
    // rebuilding saved objects.
    /// <summary>
    /// Lets RoomState restore the hidden loose-book placeable. It is intentionally
    /// absent from the catalogue, so the vanilla ID lookup needs this fallback.
    /// </summary>
    [HarmonyPatch(typeof(PlaceableManager), nameof(PlaceableManager.GetDataByID))]
    internal static class ResolveSavedBookPlaceablePatch
    {
        private static void Postfix(string id, ref PlaceableData __result)
        {
            if (__result == null && id == Boxroom_Books.BookAssetBundle.PlaceableId)
                __result = Boxroom_Books.BookAssetBundle.GetOrCreatePlaceableData();
        }
    }

    /// <summary>
    /// Creates a saved loose book from the custom prefab path. BOXROOM cannot
    /// instantiate this private placeable through its normal catalogue pipeline.
    /// </summary>
    [HarmonyPatch(typeof(PlaceableManager), nameof(PlaceableManager.InstantiatePlaceableAsync))]
    internal static class InstantiateSavedBookPlaceablePatch
    {
        private static bool Prefix(PlaceableData data, ref Task<GameObject> __result)
        {
            if (data == null || data.ID != Boxroom_Books.BookAssetBundle.PlaceableId) return true;
            __result = Task.FromResult(Boxroom_Books.BookAssetBundle.InstantiatePrefab());
            return false;
        }
    }
}
