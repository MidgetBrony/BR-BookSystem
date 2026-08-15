using HarmonyLib;
using MelonLoader;
using SteamShelf;
using SteamShelf.Media;
using SteamShelf.Placeables;
using SteamShelf.Tweening;
using System.Reflection;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Boxroom_Books
{
    /// <summary>
    /// The shelf-controlled representation of a book.
    /// This is deliberately separate from PlacedBookProp: shelf containers own
    /// the transform and lifecycle of IShelfItem instances.
    /// </summary>
    /// <summary>
    /// Shelf-specific book representation. BOXROOM shelves manage IShelfItem objects
    /// rather than loose IPlaceable props, so this component owns slot attachment,
    /// selection, cover loading, and conversion back into held media.
    /// </summary>
    public sealed class ShelfBookItem : MonoBehaviour, IShelfItem
    {
        private BookData bookInfo;
        private Renderer coverRenderer;
        private TMP_Text[] titleTexts = System.Array.Empty<TMP_Text>();
        private Renderer[] displayRenderers = System.Array.Empty<Renderer>();
        private Texture2D loadedCoverTexture;
        private Texture2D nullCoverTexture;
        private Tweener placedTweener;
        private bool fullDisplayActive = true;

        public bool HasItemAndIsActive =>
            bookInfo != null &&
            fullDisplayActive &&
            gameObject.activeInHierarchy;

        public IMediaItem ItemInfo => bookInfo;

        public void Initialize(
            Renderer renderer,
            Tweener tweener)
        {
            coverRenderer = renderer;
            placedTweener = tweener;
            titleTexts = GetComponentsInChildren<TMP_Text>(true);
            displayRenderers = GetComponentsInChildren<Renderer>(true);

            if (coverRenderer != null &&
                coverRenderer.sharedMaterials != null &&
                coverRenderer.sharedMaterials.Length > 0)
            {
                nullCoverTexture =
                    coverRenderer.sharedMaterials[0].mainTexture as Texture2D;
            }

            ResetDisplay();
        }

        public void SetItem(IMediaItem item, bool playTween = false)
        {
            if (item is BookData book)
            {
                SetBook(book, playTween);
                return;
            }

            Clear();
        }

        public void SetItem(MediaRef mediaRef)
        {
            if (!mediaRef.IsValid() ||
                mediaRef.Type != BookMedia.Type)
            {
                Clear();
                return;
            }

            BookData book =
                BookLibrarySystem.GetBookSync(mediaRef.Id);

            if (book == null)
            {
                MelonLogger.Warning(
                    $"Shelf book '{mediaRef.Id}' was not found in the book library.");

                Clear();
                return;
            }

            SetBook(book);
        }

        public void Clear()
        {
            bookInfo = null;
            ResetDisplay();
        }

        public void HideCoverArt()
        {
            ApplyCoverTexture(nullCoverTexture);
        }

        public void ShowCoverArt()
        {
            if (bookInfo != null)
                ApplyBookCover(bookInfo);
        }

        public void SetFullDisplayActive(bool active)
        {
            fullDisplayActive = active;

            foreach (Renderer renderer in displayRenderers)
            {
                if (renderer != null)
                    renderer.enabled = active;
            }

            foreach (TMP_Text text in titleTexts)
            {
                if (text != null)
                    text.enabled = active;
            }
        }

        public void SetHoveredDisplayActive(bool active)
        {
            // BOXROOM's ShelfBox currently leaves this empty as well.
        }

        private void SetBook(
            BookData book,
            bool playTween = false)
        {
            bookInfo = book;

            string title = string.IsNullOrWhiteSpace(book.Title)
                ? book.Id
                : book.Title;

            foreach (TMP_Text text in titleTexts)
            {
                if (text != null)
                {
                    text.text = title;
                    text.gameObject.SetActive(true);
                }
            }

            ApplyBookCover(book);

            if (playTween && placedTweener != null)
                placedTweener.Play();
        }

        private void ApplyBookCover(BookData book)
        {
            ReleaseLoadedCover();

            if (book.CoverArtBytes != null &&
                book.CoverArtBytes.Length > 0)
            {
                loadedCoverTexture = new Texture2D(
                    2,
                    2,
                    TextureFormat.RGBA32,
                    mipChain: true);

                if (loadedCoverTexture.LoadImage(book.CoverArtBytes))
                {
                    ApplyCoverTexture(loadedCoverTexture);

                    foreach (TMP_Text text in titleTexts)
                    {
                        if (text != null)
                            text.gameObject.SetActive(false);
                    }

                    return;
                }

                ReleaseLoadedCover();
            }

            ApplyCoverTexture(nullCoverTexture);
        }

        private void ApplyCoverTexture(Texture2D texture)
        {
            if (coverRenderer == null || texture == null)
                return;

            // The MediaBox prefab uses material slot 0 for its cover.
            MaterialHelpers.SetTexture(
                coverRenderer,
                0,
                texture);
        }

        private void ResetDisplay()
        {
            ReleaseLoadedCover();
            ApplyCoverTexture(nullCoverTexture);

            foreach (TMP_Text text in titleTexts)
            {
                if (text != null)
                {
                    text.text = "";
                    text.gameObject.SetActive(true);
                }
            }
        }

        private void ReleaseLoadedCover()
        {
            if (loadedCoverTexture == null)
                return;

            Destroy(loadedCoverTexture);
            loadedCoverTexture = null;
        }

        private void OnDestroy()
        {
            ReleaseLoadedCover();
        }
    }

    /// <summary>
    /// Builds a ShelfBookItem from the dedicated MediaBoxShelf prefab and wires its
    /// runtime-only provider/renderers. A separate shelf prefab is required because
    /// vanilla shelf transforms differ from loose placement and inspection transforms.
    /// </summary>
    internal static class BookShelfItemFactory
    {
        private const string ShelfPrefabName = "MediaBoxShelf";

        private static readonly FieldInfo BundleField =
            AccessTools.Field(
                typeof(BookAssetBundle),
                "bundle");

        private static GameObject shelfPrefab;

        public static IShelfItem Create(Transform parent)
        {
            if (parent == null)
            {
                MelonLogger.Error(
                    "Cannot create a shelf book without a parent transform.");

                return null;
            }

            if (!BookAssetBundle.IsLoaded &&
                !BookAssetBundle.Load())
            {
                MelonLogger.Error(
                    "Cannot create a shelf book because the book AssetBundle is unavailable.");

                return null;
            }

            GameObject prefab = GetShelfPrefab();

            if (prefab == null)
            {
                MelonLogger.Error(
                    $"Cannot create a shelf book because prefab '{ShelfPrefabName}' " +
                    "was not found in the book AssetBundle.");

                return null;
            }

            GameObject instance =
                Object.Instantiate(prefab, parent);

            instance.name = "ShelfBook";
            instance.SetActive(true);

            Transform coverTransform =
                FindChildRecursive(instance.transform, "Cover");

            Renderer coverRenderer =
                coverTransform != null
                    ? coverTransform.GetComponent<Renderer>()
                    : null;

            if (coverRenderer == null)
            {
                MelonLogger.Error(
                    "ShelfBook could not find the MediaBox Cover renderer.");

                Object.Destroy(instance);
                return null;
            }

            ShelfBookItem shelfBook =
                instance.GetComponent<ShelfBookItem>();

            if (shelfBook == null)
                shelfBook = instance.AddComponent<ShelfBookItem>();

            Tweener tweener =
                instance.GetComponent<Tweener>();

            shelfBook.Initialize(
                coverRenderer,
                tweener);

            return shelfBook;
        }

        private static GameObject GetShelfPrefab()
        {
            if (shelfPrefab != null)
                return shelfPrefab;

            AssetBundle bundle =
                BundleField?.GetValue(null) as AssetBundle;

            if (bundle == null)
            {
                MelonLogger.Error(
                    "Could not access BookAssetBundle's loaded AssetBundle.");

                return null;
            }

            shelfPrefab =
                bundle.LoadAsset<GameObject>(ShelfPrefabName);

            if (shelfPrefab != null)
                return shelfPrefab;

            foreach (string assetName in bundle.GetAllAssetNames())
            {
                if (!assetName.EndsWith(
                        "/" + ShelfPrefabName + ".prefab",
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                shelfPrefab =
                    bundle.LoadAsset<GameObject>(assetName);

                break;
            }

            return shelfPrefab;
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
                        System.StringComparison.OrdinalIgnoreCase))
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
    }

    /// <summary>Intercepts shelf creation only for media type Books.</summary>
    [HarmonyPatch(
        typeof(ShelfItemFactory),
        nameof(ShelfItemFactory.Create))]
    internal static class ShelfItemFactoryBookPatch
    {
        private static bool Prefix(
            eMediaType type,
            Transform parent,
            ref IShelfItem __result)
        {
            if (type != BookMedia.Type)
                return true;

            __result = BookShelfItemFactory.Create(parent);

            // The book type is ours, so never enter BOXROOM's default switch.
            return false;
        }
    }

    /// <summary>
    /// BOXROOM's MediaAcceptFlags only knows its built-in media types. Without
    /// this patch, a shelf rejects BookMedia.Type before ShelfItemFactory.Create
    /// is ever called, causing the held book to follow the ordinary prop path.
    /// </summary>
    /// <summary>
    /// Adds Books to the shelf media whitelist. Without this narrow override the
    /// visual may exist, but BOXROOM refuses to commit the item to a shelf slot.
    /// </summary>
    [HarmonyPatch(
        typeof(PlaceableMediaContainer),
        nameof(PlaceableMediaContainer.CanAccept),
        new[] { typeof(eMediaType) })]
    internal static class ShelfAcceptsBookMediaPatch
    {
        private static bool Prefix(
            eMediaType type,
            ref bool __result)
        {
            if (type != BookMedia.Type)
                return true;

            __result = true;
            return false;
        }
    }
}
