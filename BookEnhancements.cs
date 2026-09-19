using Boxroom_Books;
using HarmonyLib;
using MelonLoader;
using SteamShelf.ControlHints;
using SteamShelf.Placeables;
using SteamShelf.PlayerTools;
using SteamShelf.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

namespace BR_BookSystem
{
    internal static class BookSdkIntegration
    {
        internal static bool UsesApiLifecycle => BR_MediaAPI.MediaApi.TryGet(Boxroom_Books.BookMedia.Type, out BR_MediaAPI.MediaTypeDefinition definition) && definition.UseGenericInteractionLifecycle;
    }

    /// <summary>
    /// Controls the bottom-right held-book model independently of the placement ghost.
    /// BOXROOM normally creates this presentation only for its built-in media types,
    /// so Books need a small lifecycle manager tied to the current held BookData.
    /// </summary>
    internal static class BookHandVisual
    {
        private static readonly Vector3 CarryPosition = new(0.24f, -0.19f, 0.36f);
        private static readonly Quaternion CarryRotation = Quaternion.Euler(5f, -10f, -5f);
        private static GameObject visual;
        private static BookData currentBook;

        internal static void Show(PlayerInteractionTool tool, BookData book)
        {
            if (tool == null || book == null || BookAssetBundle.BookDisplayPrefab == null) return;
            currentBook = book;

            Transform stage = AccessTools.Field(typeof(PlayerInteractionTool), "mediaStageHolder")?.GetValue(tool) as Transform;
            Component albumVisual = AccessTools.Field(typeof(PlayerInteractionTool), "inHandAlbumBox")?.GetValue(tool) as Component;
            Component gameVisual = AccessTools.Field(typeof(PlayerInteractionTool), "inHandGameBox")?.GetValue(tool) as Component;
            if (stage == null) return;

            if (albumVisual != null) AccessTools.Method(albumVisual.GetType(), "SetBoxShowing")?.Invoke(albumVisual, new object[] { false });
            if (gameVisual != null) AccessTools.Method(gameVisual.GetType(), "SetBoxShowing")?.Invoke(gameVisual, new object[] { false });
            stage.gameObject.SetActive(true);

            if (visual == null)
            {
                visual = BookAssetBundle.InstantiateDisplayPrefab();
                if (visual == null) return;
                visual.name = "InHandBook";
                visual.transform.SetParent(stage, false);
            }

            ApplyCarryPose(stage);
            visual.SetActive(true);
            BookVisual.Apply(visual, book);
        }

        internal static void ShowCurrent(PlayerInteractionTool tool)
        {
            if (currentBook != null) Show(tool, currentBook);
        }

        internal static void EnsureVisible(PlayerInteractionTool tool)
        {
            if (visual == null || currentBook == null || tool == null) return;
            Transform stage = AccessTools.Field(typeof(PlayerInteractionTool), "mediaStageHolder")?.GetValue(tool) as Transform;
            if (stage == null) return;
            stage.gameObject.SetActive(true);
            ApplyCarryPose(stage);
            visual.SetActive(true);
        }

        private static void ApplyCarryPose(Transform fallbackStage)
        {
            if (visual == null) return;
            Transform parent = Camera.main != null ? Camera.main.transform : fallbackStage;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = CarryPosition;
            visual.transform.localRotation = CarryRotation;
            visual.transform.localScale = Vector3.one * 0.8f;
        }

        internal static void Hide()
        {
            if (visual != null) visual.SetActive(false);
        }

        internal static void Clear()
        {
            currentBook = null;
            Hide();
        }

    }

    /// <summary>
    /// Applies book-type thickness, cover orientation, and transform corrections to
    /// any of the three book prefab contexts. Baselines are cached before scaling so
    /// repeated pickup/place/load cycles do not compound transforms.
    /// </summary>
    internal static class BookVisual
    {
        /// <summary>Original prefab transforms retained for idempotent visual updates.</summary>
        private sealed class Baseline
        {
            internal Quaternion VisualsRotation;
            internal Vector3 BodyScale;
            internal Vector3 CoverScale;
            internal Vector3 CoverPosition;
            internal Vector3 BackScale;
            internal Vector3 BackPosition;
            internal Vector3 SpineScale;
            internal Vector3 SpinePosition;
            internal Vector3 ColliderSize;
            internal Vector3 ColliderCenter;
        }

        private static readonly Dictionary<int, Baseline> Baselines = new();
        private const float DefaultCoverAspect = 0.1248f / 0.1782f;

        internal static void Apply(GameObject visual, BookData book)
        {
            if (visual == null || book == null) return;
            Transform cover = Find(visual.transform, "Cover");
            Renderer renderer = cover != null ? cover.GetComponent<Renderer>() : null;
            float coverAspect = DefaultCoverAspect;
            if (renderer != null)
            {
                BookCoverTextureLease lease =
                    visual.GetComponent<BookCoverTextureLease>() ??
                    visual.AddComponent<BookCoverTextureLease>();
                Texture2D texture = lease.Bind(book, BookCoverTextureCache.DetailMaxSize);
                if (texture != null)
                {
                    if (book.CoverAspectRatio > 0f)
                        coverAspect = book.CoverAspectRatio;
                    ApplyTexture(renderer, texture);
                }
            }
            ApplyShape(visual, book, coverAspect);
            RuntimeBookSpine.Apply(visual, book);
            RuntimeBookBack.Apply(visual, book);
        }

        internal static void ApplyTexture(Renderer renderer, Texture2D texture)
        {
            if (renderer == null || texture == null) return;
            Material material = renderer.material;
            material.mainTexture = texture;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            // The cover quad faces outward by rotating 180 degrees around Y;
            // compensate its mirrored U axis so printed titles read normally.
            material.mainTextureScale = new Vector2(-1f, 1f);
            material.mainTextureOffset = new Vector2(1f, 0f);
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTextureScale("_BaseMap", new Vector2(-1f, 1f));
                material.SetTextureOffset("_BaseMap", new Vector2(1f, 0f));
            }
            renderer.material = material;
        }

        internal static void ApplyThickness(GameObject visual, BookData book) =>
            ApplyShape(
                visual,
                book,
                book?.CoverAspectRatio > 0f
                    ? book.CoverAspectRatio
                    : DefaultCoverAspect);

        internal static void ApplyShelf(GameObject visual, BookData book)
        {
            if (visual == null || book == null) return;
            float aspect = book.CoverAspectRatio > 0f
                ? book.CoverAspectRatio
                : DefaultCoverAspect;
            ApplyShape(visual, book, aspect);
            RuntimeBookSpine.Apply(visual, book);
            RuntimeBookBack.Apply(visual, book);
        }

        private static void ApplyShape(GameObject visual, BookData book, float coverAspect)
        {
            if (visual == null || book == null) return;
            Transform body = Find(visual.transform, "Body");
            Transform cover = Find(visual.transform, "Cover");
            Transform back = Find(visual.transform, "Back");
            Transform spine = Find(visual.transform, "Spine");
            Transform visuals = Find(visual.transform, "BookVisuals");
            if (body == null) return;
            BoxCollider collider = visual.GetComponent<BoxCollider>();

            int id = visual.GetInstanceID();
            if (!Baselines.TryGetValue(id, out Baseline baseline))
            {
                baseline = new Baseline
                {
                    VisualsRotation = visuals != null ? visuals.localRotation : Quaternion.identity,
                    BodyScale = body.localScale,
                    CoverScale = cover != null ? cover.localScale : Vector3.one,
                    CoverPosition = cover != null ? cover.localPosition : Vector3.zero,
                    BackScale = back != null ? back.localScale : Vector3.one,
                    BackPosition = back != null ? back.localPosition : Vector3.zero,
                    SpineScale = spine != null ? spine.localScale : Vector3.one,
                    SpinePosition = spine != null ? spine.localPosition : Vector3.zero,
                    ColliderSize = collider != null ? collider.size : Vector3.zero,
                    ColliderCenter = collider != null ? collider.center : Vector3.zero
                };
                Baselines[id] = baseline;
            }

            bool rightBound = IsRightBound(book);
            bool shelfVisual = visual.GetComponent<ShelfBookItem>() != null;
            if (visuals != null)
            {
                // Shelf Manga presents its right-side binding toward the room.
                // From left to right this leaves front cover, spine, then back/summary.
                // The shelf root/pivot and saved slot never move.
                visuals.localRotation = shelfVisual && rightBound
                    ? baseline.VisualsRotation * Quaternion.Euler(0f, 180f, 0f)
                    : baseline.VisualsRotation;
            }

            float thickness = ThicknessFor(book.BookType);
            float widthScale = Mathf.Clamp(coverAspect / DefaultCoverAspect, 0.65f, 3.0f);
            Vector3 scale = baseline.BodyScale;
            scale.x *= widthScale;
            scale.z *= thickness;
            body.localScale = scale;
            if (cover != null)
            {
                Vector3 coverScale = baseline.CoverScale;
                coverScale.x *= widthScale;
                cover.localScale = coverScale;
            }
            if (cover != null) cover.localPosition = new Vector3(baseline.CoverPosition.x, baseline.CoverPosition.y, baseline.CoverPosition.z * thickness);
            if (back != null)
            {
                Vector3 backScale = baseline.BackScale;
                backScale.x *= widthScale;
                back.localScale = backScale;
            }
            if (back != null) back.localPosition = new Vector3(baseline.BackPosition.x, baseline.BackPosition.y, baseline.BackPosition.z * thickness);
            if (spine != null)
            {
                Vector3 spineScale = baseline.SpineScale;
                spineScale.z *= thickness;
                spine.localScale = spineScale;
                float spineX = rightBound
                    ? Mathf.Abs(baseline.SpinePosition.x)
                    : baseline.SpinePosition.x;
                spine.localPosition = new Vector3(
                    spineX * widthScale,
                    baseline.SpinePosition.y,
                    baseline.SpinePosition.z);
            }

            // A thin comic must shrink downward toward the supporting surface,
            // not equally around its centre. Keep the official GameBox bottom
            // at the same height and move only its top face.
            if (visual.GetComponent<PlacedBookProp>() != null)
            {
                const float fullThickness = 0.03278351f;
                const float fixedBottom = 0.001794635f;
                float placedThickness = fullThickness * thickness;
                float anchoredCenter = fixedBottom + placedThickness * 0.5f;

                if (visuals != null)
                    visuals.localPosition = new Vector3(0.00186443f, anchoredCenter, -0.00131416f);

                if (collider != null)
                {
                    Vector3 size = baseline.ColliderSize;
                    size.x *= widthScale;
                    size.y = placedThickness;
                    collider.size = size;
                    collider.center = new Vector3(
                        baseline.ColliderCenter.x,
                        anchoredCenter,
                        baseline.ColliderCenter.z);
                }
            }

        }

        internal static Transform Find(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return child;
                Transform nested = Find(child, name);
                if (nested != null) return nested;
            }
            return null;
        }

        private static float ThicknessFor(string type)
        {
            return (type ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "magazine" => 0.35f,
                "comic" => 0.55f,
                "paperback" => 0.8f,
                "hardcover" => 1.25f,
                "graphic novel" => 1.0f,
                _ => 1.0f
            };
        }

        internal static bool IsRightBound(BookData book) =>
            string.Equals(book?.BookType?.Trim(), "Manga", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates the spine title at runtime using BOXROOM's already-loaded font. This
    /// avoids shipping a duplicate TMP font asset and automatically sizes text to the
    /// thickness selected for Comic, Manga, or other book types.
    /// </summary>
    internal static class RuntimeBookSpine
    {
        private const string LabelName = "RuntimeBookSpineTitle";
        private static TMP_FontAsset cachedFont;
        private static Material cachedMaterial;

        internal static void Apply(GameObject physicalBook, BookData book)
        {
            if (physicalBook == null || book == null) return;
            Transform spine = BookVisual.Find(physicalBook.transform, "Spine");
            if (spine == null || !TryGetBoxroomFont()) return;
            Transform labelParent = spine.parent;
            if (labelParent == null) return;

            Transform existing = labelParent.Find(LabelName);
            TextMeshPro label;
            if (existing == null)
            {
                GameObject labelObject = new GameObject(LabelName, typeof(RectTransform));
                labelObject.layer = spine.gameObject.layer;
                labelObject.transform.SetParent(labelParent, false);
                label = labelObject.AddComponent<TextMeshPro>();
            }
            else
            {
                label = existing.GetComponent<TextMeshPro>();
                if (label == null) label = existing.gameObject.AddComponent<TextMeshPro>();
            }

            RectTransform rect = label.rectTransform;
            float thickness = 0.0328f * ThicknessScale(book.BookType);
            bool rightBound = BookVisual.IsRightBound(book);
            // The label is a sibling of Spine, so follow the reshaped spine's
            // actual position instead of the original portrait-only X value.
            // Move it just beyond the outside face to avoid z-fighting.
            rect.localPosition = new Vector3(
                spine.localPosition.x + (rightBound ? 1f : -1f) * Mathf.Abs(spine.localScale.x) * 0.625f,
                spine.localPosition.y,
                spine.localPosition.z);
            // BOXROOM's stock spine labels read from the top of the case down.
            // Face the outside edge of the binding. Manga uses the opposite
            // edge, so its label must face +X instead of being seen from behind.
            rect.localRotation = Quaternion.Euler(0f, rightBound ? -90f : 90f, -90f);
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(0.158f, Mathf.Max(0.007f, thickness * 0.78f));

            label.font = cachedFont;
            label.fontSharedMaterial = cachedMaterial;
            label.text = string.IsNullOrWhiteSpace(book.Title) ? "Untitled Book" : book.Title;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Truncate;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.05f;
            label.fontSizeMax = MaxFontSize(book.BookType);
            label.characterSpacing = IsThin(book.BookType) ? -3f : 0f;
            label.color = Color.white;
            label.raycastTarget = false;
            label.ForceMeshUpdate(true, true);
        }

        private static bool TryGetBoxroomFont()
        {
            if (cachedFont != null && cachedMaterial != null) return true;

            TMP_Text template = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>()
                .FirstOrDefault(text => text != null && text.font != null && text.fontSharedMaterial != null);
            template ??= Resources.FindObjectsOfTypeAll<TMP_Text>()
                .FirstOrDefault(text => text != null && text.font != null && text.fontSharedMaterial != null &&
                                        !text.name.Equals(LabelName, StringComparison.Ordinal));
            if (template == null) return false;

            cachedFont = template.font;
            cachedMaterial = template.fontSharedMaterial;
            return cachedFont != null && cachedMaterial != null;
        }

        private static bool IsThin(string type) =>
            string.Equals(type?.Trim(), "Comic", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type?.Trim(), "Magazine", StringComparison.OrdinalIgnoreCase);

        private static float ThicknessScale(string type)
        {
            return (type ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "magazine" => 0.35f,
                "comic" => 0.55f,
                "paperback" => 0.8f,
                "hardcover" => 1.25f,
                "graphic novel" => 1.0f,
                _ => 1.0f
            };
        }

        private static float MaxFontSize(string type)
        {
            return (type ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "magazine" => 0.24f,
                "comic" => 0.30f,
                "paperback" => 0.42f,
                "hardcover" => 0.58f,
                "graphic novel" => 0.50f,
                _ => 0.46f
            };
        }
    }

    /// <summary>
    /// Typesets the metadata summary directly onto the physical back cover. The
    /// label follows the reshaped back quad, so portrait and landscape books keep
    /// the same proportional margins without needing another authored prefab.
    /// </summary>
    internal static class RuntimeBookBack
    {
        private const string SummaryLabelName = "RuntimeBookBackSummary";
        private const string CreditLabelName = "RuntimeBookBackCredit";
        private const string IsbnLabelName = "RuntimeBookBackIsbn";
        private static TMP_FontAsset cachedFont;
        private static Material cachedMaterial;
        private static bool triedSystemSerif;

        internal static void Apply(GameObject physicalBook, BookData book)
        {
            if (physicalBook == null || book == null) return;
            Transform back = BookVisual.Find(physicalBook.transform, "Back");
            if (back == null || back.parent == null) return;

            Transform existing = back.parent.Find(SummaryLabelName);
            if (string.IsNullOrWhiteSpace(book.Summary))
            {
                if (existing != null) existing.gameObject.SetActive(false);
                Transform existingCredit = back.parent.Find(CreditLabelName);
                if (existingCredit != null) existingCredit.gameObject.SetActive(false);
                Transform existingIsbn = back.parent.Find(IsbnLabelName);
                if (existingIsbn != null) existingIsbn.gameObject.SetActive(false);
                return;
            }

            if (!TryGetBookFont()) return;

            float width = Mathf.Abs(back.localScale.x);
            float height = Mathf.Abs(back.localScale.y);
            TextMeshPro summary = GetOrCreateLabel(back, SummaryLabelName);
            Place(summary.rectTransform, back, height * 0.08f, width * 0.82f, height * 0.66f);
            Configure(summary);
            summary.text = book.Summary.Trim();
            summary.alignment = TextAlignmentOptions.TopLeft;
            summary.fontSizeMin = 0.040f;
            summary.fontSizeMax = 0.062f;
            summary.lineSpacing = 5f;
            summary.ForceMeshUpdate(true, true);

            string credit = string.Join("\n", new[] { book.Author, book.Publisher }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim()));
            TextMeshPro credits = GetOrCreateLabel(back, CreditLabelName);
            if (string.IsNullOrWhiteSpace(credit))
            {
                credits.gameObject.SetActive(false);
            }
            else
            {
                Place(credits.rectTransform, back, height * -0.36f, width * 0.82f, height * 0.10f);
                Configure(credits);
                credits.text = credit;
                credits.alignment = TextAlignmentOptions.Bottom;
                credits.fontSizeMin = 0.028f;
                credits.fontSizeMax = 0.040f;
                credits.lineSpacing = 2f;
                credits.ForceMeshUpdate(true, true);
            }

            TextMeshPro isbn = GetOrCreateLabel(back, IsbnLabelName);
            if (string.IsNullOrWhiteSpace(book.Isbn))
            {
                isbn.gameObject.SetActive(false);
                return;
            }

            Place(isbn.rectTransform, back, height * -0.46f, width * 0.82f, height * 0.055f);
            Configure(isbn);
            isbn.text = $"ISBN {book.Isbn.Trim()}";
            isbn.alignment = TextAlignmentOptions.Bottom;
            isbn.fontSizeMin = 0.024f;
            isbn.fontSizeMax = 0.032f;
            isbn.ForceMeshUpdate(true, true);
        }

        private static TextMeshPro GetOrCreateLabel(Transform back, string name)
        {
            Transform existing = back.parent.Find(name);
            TextMeshPro label;
            if (existing == null)
            {
                GameObject labelObject = new GameObject(name, typeof(RectTransform));
                labelObject.layer = back.gameObject.layer;
                labelObject.transform.SetParent(back.parent, false);
                label = labelObject.AddComponent<TextMeshPro>();
            }
            else
            {
                existing.gameObject.SetActive(true);
                label = existing.GetComponent<TextMeshPro>();
                if (label == null) label = existing.gameObject.AddComponent<TextMeshPro>();
            }
            return label;
        }

        private static void Place(
            RectTransform rect,
            Transform back,
            float yOffset,
            float width,
            float height,
            float xOffset = 0f)
        {
            rect.localPosition = new Vector3(
                back.localPosition.x + xOffset,
                back.localPosition.y + yOffset,
                back.localPosition.z + 0.0002f);
            // TextMeshPro's printed face points toward -Z. Rotate it to face the
            // outside (+Z) face of the back cover.
            rect.localRotation = Quaternion.Euler(0f, 180f, 0f);
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Configure(TextMeshPro label)
        {
            label.font = cachedFont;
            label.fontSharedMaterial = cachedMaterial;
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.enableAutoSizing = true;
            label.color = new Color(0.94f, 0.92f, 0.86f, 1f);
            label.raycastTarget = false;
        }

        private static bool TryGetBookFont()
        {
            if (cachedFont != null && cachedMaterial != null) return true;

            if (!triedSystemSerif)
            {
                triedSystemSerif = true;
                foreach (string fontName in new[] { "Georgia", "Times New Roman", "Liberation Serif", "DejaVu Serif" })
                {
                    Font systemFont = Font.CreateDynamicFontFromOSFont(fontName, 48);
                    if (systemFont == null) continue;
                    TMP_FontAsset bookFont = TMP_FontAsset.CreateFontAsset(systemFont);
                    if (bookFont == null || bookFont.material == null) continue;
                    bookFont.name = $"BR-BookSystem {fontName}";
                    cachedFont = bookFont;
                    cachedMaterial = bookFont.material;
                    return true;
                }
            }

            // Platforms without one of the common serif faces still get readable
            // text using a font already loaded by BOXROOM.
            TMP_Text template = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>()
                .FirstOrDefault(text => text != null && text.font != null && text.fontSharedMaterial != null);
            template ??= Resources.FindObjectsOfTypeAll<TMP_Text>()
                .FirstOrDefault(text => text != null && text.font != null && text.fontSharedMaterial != null &&
                                        !text.name.Equals(SummaryLabelName, StringComparison.Ordinal) &&
                                        !text.name.Equals(CreditLabelName, StringComparison.Ordinal) &&
                                        !text.name.Equals(IsbnLabelName, StringComparison.Ordinal));
            if (template == null) return false;

            cachedFont = template.font;
            cachedMaterial = template.fontSharedMaterial;
            return cachedFont != null && cachedMaterial != null;
        }
    }

    /// <summary>
    /// Temporarily removes BOXROOM's native inspect overlay while a book reader is
    /// active. The same state owner is shared by PageFlip and the fallback reader
    /// so metadata, colour controls, and action prompts cannot remain underneath.
    /// </summary>
    internal static class BookInspectUiVisibility
    {
        private static GameObject suspendedMenu;
        private static bool wasActive;

        internal static void Suspend()
        {
            if (suspendedMenu != null) return;
            Menu_Inspect menu = UnityEngine.Object.FindFirstObjectByType<Menu_Inspect>();
            if (menu == null) return;
            suspendedMenu = menu.gameObject;
            wasActive = suspendedMenu.activeSelf;
            suspendedMenu.SetActive(false);
        }

        internal static void Restore()
        {
            if (suspendedMenu != null && wasActive)
                suspendedMenu.SetActive(true);
            suspendedMenu = null;
            wasActive = false;
        }
    }

    /// <summary>Applies visual metadata when a new loose book receives its data.</summary>
    [HarmonyPatch(typeof(PlacedBookProp), nameof(PlacedBookProp.ApplyData))]
    internal static class PlacedBookThicknessPatch
    {
        private static void Postfix(PlacedBookProp __instance, BookData book)
        {
            BookVisual.Apply(__instance.gameObject, book);
            RuntimeBookSpine.Apply(__instance.gameObject, book);
        }
    }

    /// <summary>Reapplies visuals after RoomState reconstructs a loose book.</summary>
    [HarmonyPatch(typeof(PlacedBookProp), nameof(PlacedBookProp.PopulateFromLoad))]
    internal static class LoadedPlacedBookVisualPatch
    {
        private static void Postfix(PlacedBookProp __instance)
        {
            BookData book = __instance.BookData;
            if (book == null)
            {
                MelonLogger.Warning("A restored free book had no BookData when applying its visuals.");
                return;
            }

            BookVisual.Apply(__instance.gameObject, book);
        }
    }

    /// <summary>
    /// Owns the dedicated large inspection model. Reusing the held or shelf object
    /// produced incorrect scale/orientation, so inspection gets its own prefab while
    /// the stock BoxInspector continues to manage camera and menu state.
    /// </summary>
    internal static class BookInspectorVisual
    {
        private static GameObject visual;
        private static HintHandle readHint;
        private static BookData currentBook;
        internal static bool IsVisible => visual != null && visual.activeInHierarchy && currentBook != null;

        internal static bool TryGetReadPrompt(out BookData book, out Vector2 screenPosition)
        {
            book = currentBook;
            screenPosition = default;
            bool mirrorApiPrompt = false;

            GameObject promptVisual = visual;
            if (book == null || promptVisual == null || !promptVisual.activeInHierarchy)
            {
                // BR-MediaAPI owns the inspection model when its generic lifecycle
                // is active, so the legacy BookInspectorVisual fields are empty.
                // Resolve the same book and action anchor from BOXROOM's active
                // inspector instead of dropping the visible Read prompt.
                BoxInspector inspector = UnityEngine.Object.FindFirstObjectByType<BoxInspector>();
                book = inspector?.heldMediaInfo as BookData;
                if (!BookInspectMenuPatch.IsBookInspectActive || book == null || inspector?.BoxHolder == null || !inspector.gameObject.activeInHierarchy)
                    return false;
                promptVisual = inspector.BoxHolder.gameObject;
                mirrorApiPrompt = true;
            }

            Camera camera = Camera.main;
            InspectUIAnchors anchors = promptVisual.GetComponentInChildren<InspectUIAnchors>(true);
            Transform target = anchors?.Open;
            if (camera == null || target == null) return false;

            Vector3 point = camera.WorldToScreenPoint(target.position);
            if (point.z <= 0f) return false;
            if (mirrorApiPrompt)
            {
                Renderer[] renderers = promptVisual.GetComponentsInChildren<Renderer>(false);
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    Vector3 center = camera.WorldToScreenPoint(bounds.center);
                    if (center.z > 0f) point.x = center.x * 2f - point.x;
                }
            }
            screenPosition = new Vector2(point.x, Screen.height - point.y);
            return true;
        }

        internal static void Show(BoxInspector inspector, BookData book)
        {
            if (inspector == null || inspector.BoxHolder == null || book == null) return;
            inspector.Box?.SetBoxShowing(false);
            Component album = AccessTools.Field(typeof(BoxInspector), "albumBox")?.GetValue(inspector) as Component;
            if (album != null) AccessTools.Method(album.GetType(), "SetBoxShowing")?.Invoke(album, new object[] { false });

            if (visual == null)
            {
                visual = BookAssetBundle.InstantiateDisplayPrefab();
                if (visual == null) return;
                visual.name = "InspectedBook";
            }
            // BookBox is authored like BOXROOM's inspector-only media objects:
            // it lives at BoxHolder origin and inherits the holder's pose.
            visual.transform.SetParent(inspector.BoxHolder, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            // The inspector GameBox is substantially larger on screen than its
            // placed/shelf counterpart. BookBox is a dedicated display prefab.
            visual.transform.localScale = Vector3.one * 1.25f;
            visual.SetActive(true);
            currentBook = book;
            BookVisual.Apply(visual, book);
            InspectUIAnchors anchors = visual.GetComponentInChildren<InspectUIAnchors>(true);
            AccessTools.Property(typeof(BoxInspector), nameof(BoxInspector.ActiveUIAnchors))?.SetValue(inspector, anchors);
            EnsureReadHint();
        }

        internal static void Hide()
        {
            if (visual != null) visual.SetActive(false);
            currentBook = null;
            readHint?.Dispose();
            readHint = null;
        }

        private static void EnsureReadHint()
        {
            if (readHint != null) return;
            readHint = new HintHandle("ReadInspectedBook", new ControlHint
            {
                actionLabel = "Read",
                bindingKeys = new[] { "Primary" },
                priority = 20
            });
        }

        internal static void PlaceInFront(BoxInspector inspector)
        {
            Camera camera = Camera.main;
            if (inspector?.BoxHolder == null || camera == null) return;
            // OnToolActivated has now completed, so BOXROOM cannot overwrite
            // this camera-relative inspection pose afterward.
            inspector.BoxHolder.position = camera.transform.TransformPoint(new Vector3(0f, 0f, 0.50f));
            inspector.BoxHolder.rotation = camera.transform.rotation;
        }

    }

    // BOXROOM's stock callback only converts SteamGameData and AlbumData into
    // held props. Books need the equivalent branch so taking one from a shelf
    // produces the original MediaBox/PlacedBookProp in hand.
    /// <summary>
    /// Converts a shelf-selected BookData into the loose book placeable expected by
    /// PlayerInteractionTool, matching the built-in GameBox/Album pickup behaviour.
    /// </summary>
    [HarmonyPatch(typeof(PlayerInteractionTool), "OnMediaInHandChanged")]
    internal static class BookShelfPickupToHandPatch
    {
        private static bool Prefix(PlayerInteractionTool __instance, SteamShelf.Media.IMediaItem item)
        {
            if (BookSdkIntegration.UsesApiLifecycle) return true;
            if (item is not BookData book) return true;

            GameObject looseBook = BookAssetBundle.InstantiatePrefab();
            if (looseBook == null) return false;

            PlacedBookProp prop = looseBook.GetComponent<PlacedBookProp>();
            PlacementTag tag = looseBook.GetComponent<PlacementTag>();
            if (prop == null || tag == null)
            {
                DestroySafely(looseBook);
                return false;
            }

            prop.ApplyData(book);

            // This invokes the original PickupMediaProp -> PickupItem flow.
            // HarmonyPatchBig then handles PlacedBookProp exactly as it does
            // for a free-placed original book.
            AccessTools.Method(typeof(PlayerInteractionTool), "PickupMediaProp")
                ?.Invoke(__instance, new object[] { tag, looseBook });

            BookHandVisual.Show(__instance, book);

            return false;
        }

        private static void DestroySafely(GameObject instance)
        {
            if (instance != null) UnityEngine.Object.Destroy(instance);
        }
    }

    /// <summary>Shows the held visual when an already-loose book is picked up.</summary>
    [HarmonyPatch(typeof(PlayerInteractionTool), "PickupItem", new[] { typeof(PlacementTag) })]
    internal static class LooseBookHandVisualPatch
    {
        private static void Postfix(PlayerInteractionTool __instance, PlacementTag placeable)
        {
            if (BookSdkIntegration.UsesApiLifecycle) return;
            if (placeable != null && placeable.TryGetComponent(out PlacedBookProp prop) && prop.BookData != null)
                BookHandVisual.Show(__instance, prop.BookData);
        }
    }

    /// <summary>Restores the held visual when returning to the placement tool.</summary>
    [HarmonyPatch(typeof(PlayerInteractionTool), nameof(PlayerInteractionTool.OnToolActivated))]
    internal static class RestoreBookHandVisualPatch
    {
        private static readonly System.Reflection.FieldInfo HeldMedia = AccessTools.Field(typeof(PlayerInteractionTool), "currentHeldMediaItem");
        private static void Postfix(PlayerInteractionTool __instance)
        {
            if (BookSdkIntegration.UsesApiLifecycle) return;
            if (HeldMedia.GetValue(__instance) is BookData book) BookHandVisual.Show(__instance, book);
        }
    }

    /// <summary>Prevents the held model remaining visible in another player tool.</summary>
    [HarmonyPatch(typeof(PlayerInteractionTool), nameof(PlayerInteractionTool.OnToolDeactivated))]
    internal static class HideBookHandVisualPatch
    {
        private static void Postfix() { if (!BookSdkIntegration.UsesApiLifecycle) BookHandVisual.Hide(); }
    }

    /// <summary>Clears the custom visual alongside BOXROOM's held-media state.</summary>
    [HarmonyPatch(typeof(PlayerInteractionTool), "ClearMediaInHand")]
    internal static class ClearBookHandVisualPatch
    {
        private static void Postfix(PlayerInteractionTool __instance)
        {
            if (BookSdkIntegration.UsesApiLifecycle) return;
            BookHandVisual.Clear();
            BookReadHintPatch.Clear(__instance);
        }
    }

    /// <summary>Supplies the book-specific model when BoxInspector receives a BookData.</summary>
    [HarmonyPatch(typeof(BoxInspector), nameof(BoxInspector.SetHeldMedia))]
    internal static class BookInspectorMediaPatch
    {
        private static void Postfix(BoxInspector __instance, SteamShelf.Media.IMediaItem item)
        {
            if (BookSdkIntegration.UsesApiLifecycle) return;
            if (item is BookData book)
                BookInspectorVisual.Show(__instance, book);
            else
                BookInspectorVisual.Hide();
        }
    }

    /// <summary>Destroys the custom inspect visual when inspection ends.</summary>
    [HarmonyPatch(typeof(BoxInspector), nameof(BoxInspector.OnToolDeactivated))]
    internal static class HideBookInspectorVisualPatch
    {
        private static void Postfix() { if (!BookSdkIntegration.UsesApiLifecycle) BookInspectorVisual.Hide(); }
    }

    /// <summary>Positions and sizes the book model after the stock inspector activates.</summary>
    [HarmonyPatch(typeof(BoxInspector), nameof(BoxInspector.OnToolActivated))]
    internal static class PositionBookInspectorPatch
    {
        private static void Postfix(BoxInspector __instance)
        {
            if (BookSdkIntegration.UsesApiLifecycle) return;
            // Run after BOXROOM's ViewInternal has finished configuring and
            // positioning BoxHolder. SetHeldMedia happens too early and its
            // result is overwritten by the remainder of OnToolActivated.
            if (__instance.heldMediaInfo is BookData book)
            {
                BookHandVisual.Hide();
                BookInspectorVisual.Show(__instance, book);
                BookInspectorVisual.PlaceInFront(__instance);
            }
        }
    }

    /// <summary>
    /// Replaces the inspector's built-in Play/Open action only while a Book is held,
    /// routing the same input to Read without changing other media behaviour.
    /// </summary>
    [HarmonyPatch(typeof(BoxInspector), nameof(BoxInspector.OnUpdate))]
    internal static class BookInspectorReadInputPatch
    {
        private static bool Prefix(BoxInspector __instance, SteamShelf.Input.PlayerInputContext inputContext)
        {
            if (__instance.heldMediaInfo is not BookData book) return true;
            if (BookInspectRuntime.Instance != null && BookInspectRuntime.Instance.IsOpen) return false;
            if (inputContext.PrimaryPressedThisFrame)
            {
                BR_BookSystem.Core.OpenBook(book);
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Populates the stock inspect menu with book metadata and relabels its action to
    /// Read. Reusing the game UI preserves controller navigation and visual styling.
    /// </summary>
    [HarmonyPatch(typeof(Menu_Inspect), "OnPreShow")]
    internal static class BookInspectMenuPatch
    {
        internal static bool IsBookInspectActive { get; private set; }
        private static readonly System.Reflection.FieldInfo GameText = AccessTools.Field(typeof(Menu_Inspect), "m_GameText");
        private static readonly System.Reflection.FieldInfo LaunchButton = AccessTools.Field(typeof(Menu_Inspect), "m_LaunchGameButton");
        private static readonly System.Reflection.FieldInfo PostcardsButton = AccessTools.Field(typeof(Menu_Inspect), "m_ViewPostcardsButton");
        private static readonly System.Reflection.FieldInfo OpenPanel = AccessTools.Field(typeof(Menu_Inspect), "m_OpenPanel");
        private static readonly System.Reflection.FieldInfo InsidePanel = AccessTools.Field(typeof(Menu_Inspect), "m_InsidePanel");
        private static readonly System.Reflection.FieldInfo PostcardPanel = AccessTools.Field(typeof(Menu_Inspect), "m_PostcardPanel");
        private static readonly System.Reflection.FieldInfo OpenButtonTracker = AccessTools.Field(typeof(Menu_Inspect), "m_OpenButtonTracker");
        private static readonly Dictionary<TMP_Text, string> OriginalTmpLabels = new();
        private static readonly Dictionary<Text, string> OriginalLegacyLabels = new();
        private static readonly HashSet<GameObject> HiddenStockButtons = new();
        private static UI_RelativeScreenPosition stockOpenTracker;

        private static void Postfix(Menu_Inspect __instance)
        {
            PlayerInteractionTool tool = UnityEngine.Object.FindFirstObjectByType<PlayerInteractionTool>();
            if (tool?.CurrentHeldMediaItem is not BookData book)
            {
                RestoreStockMenu();
                return;
            }

            ApplyBookMenu(__instance, book);
        }

        internal static void RefreshIfNeeded()
        {
            PlayerInteractionTool tool = UnityEngine.Object.FindFirstObjectByType<PlayerInteractionTool>();
            Menu_Inspect menu = UnityEngine.Object.FindFirstObjectByType<Menu_Inspect>();
            if (tool?.CurrentHeldMediaItem is not BookData book)
            {
                RestoreStockMenu();
                return;
            }
            if (menu != null) ApplyBookMenu(menu, book);
        }

        private static void ApplyBookMenu(Menu_Inspect menu, BookData book)
        {
            IsBookInspectActive = true;
            // Book inspection has one stable panel: metadata + Read. Reset it
            // every time because Menu_Inspect retains its previous box state.
            if (OpenPanel.GetValue(menu) is GameObject openPanel) openPanel.SetActive(true);
            if (InsidePanel.GetValue(menu) is GameObject insidePanel) insidePanel.SetActive(false);
            if (PostcardPanel.GetValue(menu) is GameObject postcardPanel) postcardPanel.SetActive(false);
            EnsureReadTracker(menu);

            if (GameText.GetValue(menu) is TextMeshProUGUI info)
            {
                info.text = $"Title: {book.Title}\nAuthor: {Value(book.Author)}\nSeries: {Value(book.Series)}" +
                            (!string.IsNullOrWhiteSpace(book.Volume) ? $"\nVolume: {book.Volume}" : string.Empty) +
                            $"\nPublisher: {Value(book.Publisher)}\nLanguage: {Value(book.Language)}\nType: {Value(book.BookType)}";
            }

            if (LaunchButton.GetValue(menu) is GameObject button)
            {
                // Books read from the first (normally Open) action. The
                // separate game/album Play action does not apply.
                button.SetActive(false);
                foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
                    SetLabel(label, "Read");
                foreach (Text label in button.GetComponentsInChildren<Text>(true))
                    SetLabel(label, "Read");
            }
            if (PostcardsButton.GetValue(menu) is GameObject postcards)
                postcards.SetActive(false);

            // The first inspector action is a separate Open button, not the
            // Play/Launch button. Books read directly from that first action.
            foreach (TMP_Text label in menu.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.text == "Open" || label.text == "Play") SetLabel(label, "Read");
                if (label.text == "Change Art" || label.text == "Add Screenshots")
                {
                    Button stockButton = label.GetComponentInParent<Button>();
                    if (stockButton != null)
                    {
                        HiddenStockButtons.Add(stockButton.gameObject);
                        stockButton.gameObject.SetActive(false);
                    }
                }
            }
            foreach (Text label in menu.GetComponentsInChildren<Text>(true))
            {
                if (label.text == "Open" || label.text == "Play") SetLabel(label, "Read");
                if (label.text == "Change Art" || label.text == "Add Screenshots")
                {
                    Button stockButton = label.GetComponentInParent<Button>();
                    if (stockButton != null)
                    {
                        HiddenStockButtons.Add(stockButton.gameObject);
                        stockButton.gameObject.SetActive(false);
                    }
                }
            }
        }

        private static void SetLabel(TMP_Text label, string value)
        {
            if (!OriginalTmpLabels.ContainsKey(label)) OriginalTmpLabels[label] = label.text;
            label.text = value;
        }

        private static void SetLabel(Text label, string value)
        {
            if (!OriginalLegacyLabels.ContainsKey(label)) OriginalLegacyLabels[label] = label.text;
            label.text = value;
        }

        internal static void RestoreStockMenu()
        {
            IsBookInspectActive = false;
            foreach (var pair in OriginalTmpLabels)
                if (pair.Key != null) pair.Key.text = pair.Value;
            foreach (var pair in OriginalLegacyLabels)
                if (pair.Key != null) pair.Key.text = pair.Value;
            foreach (GameObject button in HiddenStockButtons)
                if (button != null) button.SetActive(true);
            OriginalTmpLabels.Clear();
            OriginalLegacyLabels.Clear();
            HiddenStockButtons.Clear();
            if (stockOpenTracker != null) stockOpenTracker.gameObject.SetActive(true);
        }

        private static void EnsureReadTracker(Menu_Inspect menu)
        {
            stockOpenTracker = OpenButtonTracker.GetValue(menu) as UI_RelativeScreenPosition;
            if (stockOpenTracker == null) return;
            // The stock Open button also owns stateful UI_ControlIcon and
            // controller-trigger components. They can remain visually hidden
            // after the first use even though their Button still fires. Books
            // draw a small dedicated action at the same world anchor instead.
            stockOpenTracker.gameObject.SetActive(false);
        }

        private static string Value(string value) => string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
    }

    // The held Book remains assigned when the Inspect menu closes, so polling
    // CurrentHeldMediaItem cannot detect the end of an inspect session. Hook
    // the menu lifecycle itself and discard the per-session Read tracker.
    /// <summary>Restores altered stock menu controls so the next non-book is unaffected.</summary>
    [HarmonyPatch(typeof(Menu_Inspect), "OnPreHide")]
    internal static class BookInspectMenuHidePatch
    {
        private static void Postfix() => BookInspectMenuPatch.RestoreStockMenu();
    }

    /// <summary>Redirects the stock Play callback to the reader for Books only.</summary>
    [HarmonyPatch(typeof(Menu_Inspect), nameof(Menu_Inspect.OnClick_LaunchGame))]
    internal static class BookReadButtonPatch
    {
        private static bool Prefix()
        {
            PlayerInteractionTool tool = UnityEngine.Object.FindFirstObjectByType<PlayerInteractionTool>();
            if (tool?.CurrentHeldMediaItem is not BookData book) return true;
            if (BookSdkIntegration.UsesApiLifecycle) return true;
            BookInspectRuntime.Instance?.Open(book);
            return false;
        }
    }

    /// <summary>Redirects the stock Open callback to Read for Books only.</summary>
    [HarmonyPatch(typeof(Menu_Inspect), nameof(Menu_Inspect.OnClick_OpenButton))]
    internal static class BookReadFromOpenButtonPatch
    {
        private static bool Prefix()
        {
            PlayerInteractionTool tool = UnityEngine.Object.FindFirstObjectByType<PlayerInteractionTool>();
            if (tool?.CurrentHeldMediaItem is not BookData book) return true;
            if (BookSdkIntegration.UsesApiLifecycle) return true;
            BookInspectRuntime.Instance?.Open(book);
            return false;
        }
    }

    /// <summary>Maintains the contextual Read hint while carrying a Book.</summary>
    [HarmonyPatch(typeof(PlayerInteractionTool), nameof(PlayerInteractionTool.OnUpdate))]
    internal static class BookReadHintPatch
    {
        private static readonly System.Reflection.FieldInfo HeldMedia = AccessTools.Field(typeof(PlayerInteractionTool), "currentHeldMediaItem");
        private static readonly System.Reflection.FieldInfo OpenHint = AccessTools.Field(typeof(PlayerInteractionTool), "openBoxHint");
        private static HintHandle bookHint;

        private static void Postfix(PlayerInteractionTool __instance)
        {
            if (BookSdkIntegration.UsesApiLifecycle) return;
            if (HeldMedia.GetValue(__instance) is BookData)
            {
                if (!BookInspectorVisual.IsVisible && !(BookInspectRuntime.Instance?.IsOpen ?? false))
                    BookHandVisual.EnsureVisible(__instance);
                HintHandle stockHint = OpenHint.GetValue(__instance) as HintHandle;
                if (stockHint != null && stockHint != bookHint) stockHint.Dispose();
                if (bookHint == null)
                {
                    bookHint = new HintHandle("ReadBook", new ControlHint
                    {
                        actionLabel = "Read Book",
                        bindingKeys = new[] { "Secondary" },
                        priority = 1
                    });
                }
                OpenHint.SetValue(__instance, bookHint);
            }
            else if (bookHint != null)
            {
                bookHint.Dispose();
                if (OpenHint.GetValue(__instance) == bookHint) OpenHint.SetValue(__instance, null);
                bookHint = null;
            }
        }

        internal static void Clear(PlayerInteractionTool tool)
        {
            bookHint?.Dispose();
            if (tool != null && OpenHint.GetValue(tool) == bookHint)
                OpenHint.SetValue(tool, null);
            bookHint = null;
        }
    }

    // The inspect/read feature is intentionally isolated from the original
    // physical book, shelf, placement, pickup, and rendering implementation.
    /// <summary>
    /// Enters BOXROOM's normal inspector from a held Book. This remains separate from
    /// Read so the player can first inspect metadata/model, then choose to open pages.
    /// </summary>
    [HarmonyPatch(typeof(PlayerInteractionTool), nameof(PlayerInteractionTool.OnUpdate))]
    internal static class BookInspectInputPatch
    {
        private static readonly System.Reflection.FieldInfo HeldMedia = AccessTools.Field(typeof(PlayerInteractionTool), "currentHeldMediaItem");
        private static readonly System.Reflection.FieldInfo Controller = AccessTools.Field(typeof(PlayerTool), "controller");

        private static bool Prefix(PlayerInteractionTool __instance, SteamShelf.Input.PlayerInputContext inputContext)
        {
            if (BookSdkIntegration.UsesApiLifecycle) return true;
            if (BookInspectRuntime.Instance != null && BookInspectRuntime.Instance.IsOpen) return false;
            if (inputContext.SecondaryPressedThisFrame && HeldMedia.GetValue(__instance) is BookData book)
            {
                PlayerToolController controller = Controller?.GetValue(__instance) as PlayerToolController;
                if (controller == null)
                {
                    MelonLogger.Error("Book inspect could not access PlayerToolController.");
                    return false;
                }

                BoxInspector inspector = controller.GetToolClass<BoxInspector>();
                if (inspector == null)
                {
                    MelonLogger.Error("Book inspect could not find BOXROOM's BoxInspector tool.");
                    return false;
                }

                inspector.SetHeldMedia(book);
                controller.SetActiveTool(EToolType.None, force: true);
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Persistent coordinator for opening and closing book readers. PageFlip is the
    /// preferred UI; the simple image viewer remains a safe fallback when the bundle
    /// or archive reader fails, ensuring Read never leaves the player stuck in a tool.
    /// </summary>
    public sealed class BookInspectRuntime : MonoBehaviour
    {
        private ComicArchiveReader fallbackArchive;
        private int fallbackPageCount;
        private Texture2D pageTexture;
        private BookData book;
        private int pageIndex;
        public static BookInspectRuntime Instance { get; private set; }
        public bool IsOpen => book != null || (PageFlipReaderController.Instance?.IsOpen ?? false);

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        internal void Open(BookData selected)
        {
            if (PageFlipReaderController.Open(selected))
            {
                BookInspectUiVisibility.Suspend();
                BookHandVisual.Hide();
                return;
            }

            if (string.Equals(selected.Extension, ".pdf", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(selected.Extension, ".epub", StringComparison.OrdinalIgnoreCase))
            {
                MelonLogger.Warning("The in-game document reader was unavailable; opening the book with the operating system instead.");
                OpenExternalDocument(selected);
                return;
            }

            MelonLogger.Warning("PageFlip reader was unavailable; using the temporary image reader.");
            string archivePath = ComicArchive.Find(selected.FolderPath);
            if (archivePath == null) return;
            fallbackArchive?.Dispose();
            fallbackArchive = ComicArchive.Open(archivePath);
            fallbackPageCount = fallbackArchive.Count;
            if (fallbackPageCount == 0)
            {
                fallbackArchive.Dispose();
                fallbackArchive = null;
                return;
            }
            book = selected;
            SetPage(0);
            BookInspectUiVisibility.Suspend();
            BookHandVisual.Hide();
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        /// <summary>
        /// EPUB rendering, and PDF fallback when PDFium cannot initialize, is
        /// delegated to the user's chosen OS application.
        /// </summary>
        private static void OpenExternalDocument(BookData selected)
        {
            if (string.IsNullOrWhiteSpace(selected.ContentPath) ||
                !File.Exists(selected.ContentPath))
            {
                MelonLogger.Error($"Book document was not found: {selected?.ContentPath}");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = selected.ContentPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Error(
                    $"Could not open '{selected.ContentPath}' with the OS default reader: {ex}");
            }
        }

        private void Update()
        {
            if (!IsOpen || Keyboard.current == null) return;
            // PageFlip owns its own navigation and close lifecycle. Do not also
            // run the fallback reader controls against an empty page list.
            if (PageFlipReaderController.Instance?.IsOpen == true) return;
            if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.bKey.wasPressedThisFrame) Close();
            else if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame) SetPage(pageIndex + 1);
            else if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame) SetPage(pageIndex - 1);
        }

        private void OnGUI()
        {
            if (!IsOpen)
            {
                DrawInspectReadControl();
                return;
            }
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            Rect pageRect = new Rect(Screen.width * .10f, 40, Screen.width * .80f, Screen.height - 115);
            if (pageTexture != null) GUI.DrawTexture(pageRect, pageTexture, ScaleMode.ScaleToFit, true);
            GUILayout.BeginArea(new Rect(Screen.width * .10f, Screen.height - 68, Screen.width * .80f, 58));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Previous") && pageIndex > 0) SetPage(pageIndex - 1);
            GUILayout.Label($"{book.Title}   {pageIndex + 1} / {fallbackPageCount}", GUI.skin.box, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Next") && pageIndex + 1 < fallbackPageCount) SetPage(pageIndex + 1);
            if (GUILayout.Button("Close")) Close();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawInspectReadControl()
        {
            if (!BookInspectorVisual.TryGetReadPrompt(out BookData inspectedBook, out Vector2 point)) return;

            const float width = 150f;
            const float height = 112f;
            Rect hitArea = new Rect(point.x - width * 0.5f, point.y - height * 0.5f, width, height);
            GUIStyle style = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 30,
                fontStyle = FontStyle.Normal,
                wordWrap = false
            };
            style.normal.background = null;
            style.hover.background = null;
            style.active.background = null;
            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;

            if (GUI.Button(hitArea, "◯\nRead", style))
                Open(inspectedBook);
        }

        private void SetPage(int index)
        {
            if (fallbackArchive == null || fallbackPageCount <= 0) return;
            pageIndex = Mathf.Clamp(index, 0, fallbackPageCount - 1);
            if (pageTexture != null) Destroy(pageTexture);
            pageTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            ComicPage page = fallbackArchive.ReadPage(pageIndex);
            ImageConversion.LoadImage(pageTexture, page.Bytes, false);
        }

        private void Close()
        {
            book = null;
            fallbackArchive?.Dispose();
            fallbackArchive = null;
            fallbackPageCount = 0;
            if (pageTexture != null) Destroy(pageTexture);
            pageTexture = null;
            BookInspectUiVisibility.Restore();
            PlayerInteractionTool tool = UnityEngine.Object.FindFirstObjectByType<PlayerInteractionTool>();
            BookHandVisual.ShowCurrent(tool);
        }

        private void OnDestroy()
        {
            fallbackArchive?.Dispose();
            fallbackArchive = null;
        }

        private static bool IsImage(string path) => new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

        /// <summary>Natural filename ordering for fallback loose-image books.</summary>
        private sealed class NaturalComparer : IComparer<string>
        {
            internal static readonly NaturalComparer Instance = new();
            public int Compare(string x, string y) => StrCmpLogicalW(x, y);
            [System.Runtime.InteropServices.DllImport("shlwapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            private static extern int StrCmpLogicalW(string x, string y);
        }
    }
}
