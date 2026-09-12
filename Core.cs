using MelonLoader;
using UnityEngine;
using BR_MediaAPI;

[assembly: MelonInfo(typeof(BR_BookSystem.Core), "BR-BookSystem", "2.0.1", "Rusty", null)]
[assembly: MelonGame("NestedLoop", "BOXROOM")]
[assembly: MelonAdditionalDependencies("BR_MediaAPI")]

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
            ReadingProgress.Initialize();
            if (!Boxroom_Books.BookAssetBundle.Load()) LoggerInstance.Warning("Original book asset bundle could not be loaded.");
            MediaApi.Register(new MediaTypeDefinition
            {
                Id = (int)Boxroom_Books.BookMedia.Type,
                Key = "Rusty.BR-BookSystem",
                DisplayName = "Books",
                ModelType = typeof(Boxroom_Books.BookData),
                Library = new Boxroom_Books.BookMediaLibrary(),
                AllowOnShelves = true,
                ShelfFactory = Boxroom_Books.BookShelfItemFactory.Create,
                PlaceableId = Boxroom_Books.BookAssetBundle.PlaceableId,
                PlaceableDataFactory = Boxroom_Books.BookAssetBundle.GetOrCreatePlaceableData,
                LoosePrefabFactory = Boxroom_Books.BookAssetBundle.InstantiatePrefab,
                HeldPrefabFactory = Boxroom_Books.BookAssetBundle.InstantiateDisplayPrefab,
                CreateUnplacedMediaBox = true,
                UnplacedMediaBoxId = "BoxroomPlus_BookBox",
                UnplacedMediaBoxName = "Book Box",
                UnplacedMediaBoxDescription = "A container for all of your unplaced books",
                UseGenericInteractionLifecycle = true,
                Visuals = new MediaVisualDefinition
                {
                    OnHeld = context => BookVisual.Apply(context.Visual, (Boxroom_Books.BookData)context.Item),
                    OnInspect = context => BookVisual.Apply(context.Visual, (Boxroom_Books.BookData)context.Item)
                },
                Inspect = new MediaInspectDefinition
                {
                    PrimaryActionLabel = "Read",
                    PrefabFactory = _ => Boxroom_Books.BookAssetBundle.InstantiateDisplayPrefab(),
                    OnPrimaryAction = context => OpenBook((Boxroom_Books.BookData)context.Item)
                },
                LibraryFolder = new MediaLibraryFolderOptions
                {
                    SettingId = BookLibrarySettings.SettingId,
                    Label = "Book Folder Location",
                    PanelTitle = "BR-BookSystem",
                    PanelOrder = 100,
                    Reload = Boxroom_Books.BookLibrarySystem.LoadCache,
                    GetStatus = () => $"{Boxroom_Books.BookLibrarySystem.GetKnownBooks().Count} books found"
                }
            });
            EpubReaderSettings.Initialize();
        }

        public override void OnDeinitializeMelon()
        {
            Boxroom_Books.BookCoverTextureCache.EvictAll();
            Boxroom_Books.BookAssetBundle.Unload();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            EnsureInspectRuntime();
        }

        internal static void OpenBook(Boxroom_Books.BookData book)
        {
            BookInspectRuntime runtime = EnsureInspectRuntime();
            if (runtime == null)
            {
                MelonLogger.Error("Could not create the Books reader runtime.");
                return;
            }

            runtime.Open(book);
        }

        internal static BookInspectRuntime EnsureInspectRuntime()
        {
            if (BookInspectRuntime.Instance != null) return BookInspectRuntime.Instance;

            var host = new GameObject("BR-BookInspect");
            UnityEngine.Object.DontDestroyOnLoad(host);
            return host.AddComponent<BookInspectRuntime>();
        }
    }
}
