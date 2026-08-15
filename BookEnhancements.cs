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
            internal Vector3 BodyScale;
            internal Vector3 CoverPosition;
            internal Vector3 BackPosition;
            internal Vector3 SpineScale;
            internal Vector3 SpinePosition;
        }

        private static readonly Dictionary<int, Baseline> Baselines = new();

        internal static void Apply(GameObject visual, BookData book)
        {
            if (visual == null || book == null) return;
            ApplyThickness(visual, book);
            Transform cover = Find(visual.transform, "Cover");
            Renderer renderer = cover != null ? cover.GetComponent<Renderer>() : null;
            if (renderer != null && book.CoverArtBytes != null)
            {
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (ImageConversion.LoadImage(texture, book.CoverArtBytes))
                    ApplyTexture(renderer, texture);
                else
                    UnityEngine.Object.Destroy(texture);
            }
            RuntimeBookSpine.Apply(visual, book);
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

        internal static void ApplyThickness(GameObject visual, BookData book)
        {
            if (visual == null || book == null) return;
            Transform body = Find(visual.transform, "Body");
            Transform cover = Find(visual.transform, "Cover");
            Transform back = Find(visual.transform, "Back");
            Transform spine = Find(visual.transform, "Spine");
            if (body == null) return;

            int id = visual.GetInstanceID();
            if (!Baselines.TryGetValue(id, out Baseline baseline))
            {
                baseline = new Baseline
                {
                    BodyScale = body.localScale,
                    CoverPosition = cover != null ? cover.localPosition : Vector3.zero,
                    BackPosition = back != null ? back.localPosition : Vector3.zero,
                    SpineScale = spine != null ? spine.localScale : Vector3.one,
                    SpinePosition = spine != null ? spine.localPosition : Vector3.zero
                };
                Baselines[id] = baseline;
            }

            float thickness = ThicknessFor(book.BookType);
            Vector3 scale = baseline.BodyScale;
            scale.z *= thickness;
            body.localScale = scale;
            if (cover != null) cover.localPosition = new Vector3(baseline.CoverPosition.x, baseline.CoverPosition.y, baseline.CoverPosition.z * thickness);
            if (back != null) back.localPosition = new Vector3(baseline.BackPosition.x, baseline.BackPosition.y, baseline.BackPosition.z * thickness);
            if (spine != null)
            {
                Vector3 spineScale = baseline.SpineScale;
                spineScale.z *= thickness;
                spine.localScale = spineScale;
                spine.localPosition = baseline.SpinePosition;
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

                Transform visuals = Find(visual.transform, "BookVisuals");
                if (visuals != null)
                    visuals.localPosition = new Vector3(0.00186443f, anchoredCenter, -0.00131416f);

                BoxCollider collider = visual.GetComponent<BoxCollider>();
                if (collider != null)
                {
                    Vector3 size = collider.size;
                    size.y = placedThickness;
                    collider.size = size;
                    collider.center = new Vector3(0.00186443f, anchoredCenter, -0.00131416f);
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
            rect.localPosition = new Vector3(-0.0638f, 0f, 0f);
            // BOXROOM's stock spine labels read from the top of the case down.
            // Face the printed side of the spine. The previous -90 Y showed
            // TextMeshPro from behind, mirroring every glyph.
            rect.localRotation = Quaternion.Euler(0f, 90f, -90f);
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

    /// <summary>
    /// Reapplies visuals after RoomState reconstruction. Loaded transforms include
    /// placement scale, so this is intentionally separate from the new-book path.
    /// </summary>
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

            // Loaded books bypass ApplyData, so restore the exact same cover,
            // type thickness, bottom anchor, collider and spine configuration.
            BookVisual.Apply(__instance.gameObject, book);
        }
    }

    /// <summary>Updates shelf thickness and spine after a reusable slot changes book.</summary>
    [HarmonyPatch(typeof(ShelfBookItem), nameof(ShelfBookItem.SetItem), new[] { typeof(SteamShelf.Media.IMediaItem), typeof(bool) })]
    internal static class ShelfBookThicknessPatch
    {
        private static void Postfix(ShelfBookItem __instance, SteamShelf.Media.IMediaItem item)
        {
            if (item is not BookData book) return;
            BookVisual.Apply(__instance.gameObject, book);
            RuntimeBookSpine.Apply(__instance.gameObject, book);
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
            if (book == null || visual == null || !visual.activeInHierarchy) return false;

            Camera camera = Camera.main;
            InspectUIAnchors anchors = visual.GetComponentInChildren<InspectUIAnchors>(true);
            Transform target = anchors?.Open;
            if (camera == null || target == null) return false;

            Vector3 point = camera.WorldToScreenPoint(target.position);
            if (point.z <= 0f) return false;
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
            if (HeldMedia.GetValue(__instance) is BookData book) BookHandVisual.Show(__instance, book);
        }
    }

    /// <summary>Prevents the held model remaining visible in another player tool.</summary>
    [HarmonyPatch(typeof(PlayerInteractionTool), nameof(PlayerInteractionTool.OnToolDeactivated))]
    internal static class HideBookHandVisualPatch
    {
        private static void Postfix() => BookHandVisual.Hide();
    }

    /// <summary>Clears the custom visual alongside BOXROOM's held-media state.</summary>
    [HarmonyPatch(typeof(PlayerInteractionTool), "ClearMediaInHand")]
    internal static class ClearBookHandVisualPatch
    {
        private static void Postfix(PlayerInteractionTool __instance)
        {
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
        private static void Postfix() => BookInspectorVisual.Hide();
    }

    /// <summary>Positions and sizes the book model after the stock inspector activates.</summary>
    [HarmonyPatch(typeof(BoxInspector), nameof(BoxInspector.OnToolActivated))]
    internal static class PositionBookInspectorPatch
    {
        private static void Postfix(BoxInspector __instance)
        {
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
                BookInspectRuntime.Instance?.Open(book);
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
            // Book inspection has one stable panel: metadata + Read. Reset it
            // every time because Menu_Inspect retains its previous box state.
            if (OpenPanel.GetValue(menu) is GameObject openPanel) openPanel.SetActive(true);
            if (InsidePanel.GetValue(menu) is GameObject insidePanel) insidePanel.SetActive(false);
            if (PostcardPanel.GetValue(menu) is GameObject postcardPanel) postcardPanel.SetActive(false);
            EnsureReadTracker(menu);

            if (GameText.GetValue(menu) is TextMeshProUGUI info)
            {
                info.text = $"Title: {book.Title}\nAuthor: {Value(book.Author)}\nSeries: {Value(book.Series)}" +
                            (book.Volume > 0 ? $"\nVolume: {book.Volume}" : string.Empty) +
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
        private readonly List<byte[]> pages = new();
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
            if (IsExternalDocument(selected))
            {
                OpenExternalDocument(selected);
                return;
            }

            if (PageFlipReaderController.Open(selected))
            {
                BookHandVisual.Hide();
                return;
            }

            MelonLogger.Warning("PageFlip reader was unavailable; using the temporary image reader.");
            pages.Clear();
            string archivePath = ComicArchive.Find(selected.FolderPath);
            if (archivePath == null) return;
            foreach (ComicPage page in ComicArchive.ReadPages(archivePath)) pages.Add(page.Bytes);
            if (pages.Count == 0) return;
            book = selected;
            SetPage(0);
            BookHandVisual.Hide();
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        /// <summary>
        /// PDF and EPUB rendering is delegated to the user's chosen OS application.
        /// This avoids embedding heavyweight document engines and respects existing
        /// accessibility, DRM, annotation, and reader preferences.
        /// </summary>
        private static bool IsExternalDocument(BookData selected) =>
            selected != null &&
            (string.Equals(selected.Extension, ".pdf", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(selected.Extension, ".epub", StringComparison.OrdinalIgnoreCase));

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
            GUILayout.Label($"{book.Title}   {pageIndex + 1} / {pages.Count}", GUI.skin.box, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Next") && pageIndex + 1 < pages.Count) SetPage(pageIndex + 1);
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
            pageIndex = Mathf.Clamp(index, 0, pages.Count - 1);
            if (pageTexture != null) Destroy(pageTexture);
            pageTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            ImageConversion.LoadImage(pageTexture, pages[pageIndex], false);
        }

        private void Close()
        {
            book = null;
            pages.Clear();
            if (pageTexture != null) Destroy(pageTexture);
            pageTexture = null;
            PlayerInteractionTool tool = UnityEngine.Object.FindFirstObjectByType<PlayerInteractionTool>();
            BookHandVisual.ShowCurrent(tool);
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
