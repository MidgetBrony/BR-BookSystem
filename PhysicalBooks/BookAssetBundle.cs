using MelonLoader;
using MelonLoader.Utils;
using SteamShelf.Placeables;
using System;
using System.IO;
using UnityEngine;
using SteamShelf.PlayerTools;

namespace Boxroom_Books
{
    /// <summary>
    /// Loads and validates the boxroomplus bundle, then exposes the three purpose-
    /// specific book prefabs: loose/held, shelf, and inspector/reader. Keeping prefab
    /// discovery here prevents hierarchy assumptions from leaking across gameplay code.
    /// </summary>
    public static class BookAssetBundle
    {
        private const string BundleFileName = "boxroomplus";
        private const string PrefabName = "MediaBox";
        internal const string PlaceableId = "BoxroomBooks_Book";
        private const string DisplayPrefabName = "BookBox";

        private static AssetBundle bundle;
        private static GameObject bookPrefab;
        private static GameObject bookDisplayPrefab;

        private static PlaceableData bookPlaceableData;

        public static bool IsLoaded =>
            bundle != null &&
            bookPrefab != null &&
            bookDisplayPrefab != null;

        public static GameObject BookPrefab => bookPrefab;
        public static GameObject BookDisplayPrefab => bookDisplayPrefab;

        public static bool Load()
        {
            if (IsLoaded)
                return true;

            string bundlePath = Path.Combine(
                MelonEnvironment.ModsDirectory,
                BundleFileName);

            if (!File.Exists(bundlePath))
            {
                MelonLogger.Error(
                    $"Book AssetBundle was not found: '{bundlePath}'");

                return false;
            }

            try
            {
                bundle = AssetBundle.LoadFromFile(bundlePath);

                if (bundle == null)
                {
                    MelonLogger.Error(
                        $"Failed to load AssetBundle: '{bundlePath}'");

                    return false;
                }

                bookPrefab = FindPrefab(bundle);

                if (bookPrefab == null)
                {
                    MelonLogger.Error(
                        $"Could not find prefab '{PrefabName}' " +
                        $"inside '{bundlePath}'.");

                    Unload();
                    return false;
                }

                bookDisplayPrefab = FindPrefab(bundle, DisplayPrefabName);
                if (bookDisplayPrefab == null)
                {
                    MelonLogger.Error($"Could not find display prefab '{DisplayPrefabName}' inside '{bundlePath}'.");
                    Unload();
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error(
                    $"Failed loading book AssetBundle: {ex}");

                Unload();
                return false;
            }
        }
        private static bool ConfigurePlaceable(GameObject instance)
        {
            if (instance == null)
                return false;

            GetOrCreatePlaceableData();

            PlacementTag placementTag =
                instance.GetComponent<PlacementTag>();

            if (placementTag == null)
            {
                placementTag =
                    instance.AddComponent<PlacementTag>();
            }

            placementTag.PlaceableData =
                bookPlaceableData;

            // Prefabs normally provide this collider. Keep a defensive fallback
            // so a malformed bundle fails gracefully instead of becoming inert.
            Collider[] colliders =
                instance.GetComponentsInChildren<Collider>(true);

            if (colliders.Length == 0)
            {
                MelonLogger.Warning(
                    "MediaBox has no collider. Adding a BoxCollider to its root.");

                instance.AddComponent<BoxCollider>();
            }

            int propLayer =
                LayerMask.NameToLayer("Prop");

            if (propLayer < 0)
            {
                MelonLogger.Error(
                    "Could not find BOXROOM's 'Prop' layer.");

                return false;
            }

            SetLayerRecursively(
                instance,
                propLayer);

            if (!instance.TryGetComponent<PlaceablePainter>(
                    out PlaceablePainter painter))
            {
                painter =
                    instance.AddComponent<PlaceablePainter>();

                painter.InitializeMaterials();
                painter.SetupAsForcedPlaceable();
            }

            return true;
        }

        internal static PlaceableData GetOrCreatePlaceableData()
        {
            if (bookPlaceableData != null) return bookPlaceableData;

            bookPlaceableData = ScriptableObject.CreateInstance<PlaceableData>();
            bookPlaceableData.ID = PlaceableId;
            bookPlaceableData.DisplayName = "Book";
            bookPlaceableData.PlacementType = PlacementType.Prop;
            bookPlaceableData.SetToolType(EToolType.Placeable);
            // InstantiatePlaceableAsync is intercepted below; this is not a
            // folder-based ModsPlaceableLoader object.
            bookPlaceableData.IsLoadedFromMod = false;
            return bookPlaceableData;
        }

        private static void SetLayerRecursively(
    GameObject root,
    int layer)
        {
            root.layer = layer;

            Transform[] children =
                root.GetComponentsInChildren<Transform>(
                    includeInactive: true);

            for (int i = 0; i < children.Length; i++)
            {
                children[i].gameObject.layer = layer;
            }
        }
        public static GameObject InstantiatePrefab()
        {
            if (!IsLoaded && !Load())
                return null;

            GameObject instance =
                UnityEngine.Object.Instantiate(bookPrefab);

            if (instance == null)
            {
                MelonLogger.Error(
                    "Unity failed to instantiate the MediaBox prefab.");

                return null;
            }

            instance.name = PrefabName;

            if (!ConfigureInstance(instance))
            {
                UnityEngine.Object.Destroy(instance);
                return null;
            }

            return instance;
        }

        public static GameObject InstantiateDisplayPrefab()
        {
            if (!IsLoaded && !Load())
                return null;

            GameObject instance = UnityEngine.Object.Instantiate(bookDisplayPrefab);
            if (instance != null)
                instance.name = DisplayPrefabName;
            return instance;
        }

        public static GameObject InstantiateBookReaderPrefab()
        {
            if (!IsLoaded && !Load()) return null;
            GameObject prefab = FindPrefab(bundle, "BookReader");
            if (prefab == null)
            {
                MelonLogger.Error("BookReader prefab was not found in boxroomplus.");
                return null;
            }
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            if (instance != null) instance.name = "BookReader";
            return instance;
        }

        public static Sprite LoadSprite(string assetName)
        {
            if (!IsLoaded && !Load()) return null;
            Sprite sprite = bundle.LoadAsset<Sprite>(assetName);
            if (sprite != null) return sprite;
            foreach (string path in bundle.GetAllAssetNames())
                if (path.EndsWith("/" + assetName + ".png", StringComparison.OrdinalIgnoreCase))
                    return bundle.LoadAsset<Sprite>(path);
            return null;
        }

        public static void Unload()
        {
            bookPrefab = null;
            bookDisplayPrefab = null;

            if (bundle != null)
            {
                // false keeps instantiated assets alive.
                bundle.Unload(false);
                bundle = null;
            }
        }

        private static GameObject FindPrefab(
            AssetBundle loadedBundle)
        {
            return FindPrefab(loadedBundle, PrefabName);
        }

        private static GameObject FindPrefab(
            AssetBundle loadedBundle,
            string prefabName)
        {
            GameObject prefab =
                loadedBundle.LoadAsset<GameObject>(prefabName);

            if (prefab != null)
                return prefab;

            foreach (string assetName in
                     loadedBundle.GetAllAssetNames())
            {
                if (!assetName.EndsWith(
                        "/" + prefabName + ".prefab",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return loadedBundle.LoadAsset<GameObject>(
                    assetName);
            }

            return null;
        }

        private static bool ConfigureInstance(GameObject instance)
        {
            if (instance == null)
                return false;

            Transform coverTransform = FindChildRecursive(
                instance.transform,
                "Cover");

            if (coverTransform == null)
            {
                MelonLogger.Error(
                    $"Could not find a child named 'Cover' beneath '{instance.name}'.");

                return false;
            }

            Renderer coverRenderer =
                coverTransform.GetComponent<Renderer>();

            if (coverRenderer == null)
            {
                MelonLogger.Error(
                    $"'{GetHierarchyPath(coverTransform)}' has no Renderer.");

                return false;
            }

            BookDataProvider provider =
                instance.GetComponent<BookDataProvider>();

            if (provider == null)
            {
                provider =
                    instance.AddComponent<BookDataProvider>();
            }

            PlacedBookProp placedBook =
                instance.GetComponent<PlacedBookProp>();

            if (placedBook == null)
            {
                MelonLogger.Warning(
                    "MediaBox did not contain PlacedBookProp; adding it at runtime.");

                placedBook =
                    instance.AddComponent<PlacedBookProp>();
            }

            if (placedBook == null)
            {
                MelonLogger.Error(
                    "Failed to add PlacedBookProp to MediaBox.");

                return false;
            }

            placedBook.Initialize(
                provider,
                coverRenderer);

            if (!ConfigurePlaceable(instance))
                return false;

            return true;
        }
        private static Transform FindChildRecursive(
    Transform parent,
    string childName)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);

                if (string.Equals(
                        child.name,
                        childName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }

                Transform result =
                    FindChildRecursive(child, childName);

                if (result != null)
                    return result;
            }

            return null;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            Transform current = transform.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}
