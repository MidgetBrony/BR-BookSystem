using HarmonyLib;
using SFB;
using SteamShelf.Save;
using SteamShelf.Settings;
using SteamShelf.UI;
using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BR_BookSystem
{
    /// <summary>
    /// Adds a Books Library folder panel beside BOXROOM's Music Library setting.
    /// The panel is cloned from the native UI so it retains the game's layout and
    /// controller navigation; Harmony redirects only the cloned panel's actions.
    /// </summary>
    internal static class BookLibrarySettings
    {
        internal const string SettingId = "BoxroomPlusBooksLibraryRootPath";
        private const string PanelName = "BoxroomPlus Books Library Path";

        internal static string SourceRoot
        {
            get
            {
                if (!Singleton<SaveManager>.HasInstance()) return string.Empty;
                return Singleton<SaveManager>.Instance.Settings.GetValue(SettingId, string.Empty);
            }
        }

        internal static void SetSourceRoot(string path)
        {
            if (!Singleton<SaveManager>.HasInstance()) return;
            Singleton<SaveManager>.Instance.Settings.SetValue(SettingId, path ?? string.Empty);
            Singleton<SaveManager>.Instance.Settings.Save();
        }

        internal static void InstallPanels()
        {
            foreach (UI_AlbumLibraryPathDisplay musicPanel in
                     Resources.FindObjectsOfTypeAll<UI_AlbumLibraryPathDisplay>())
            {
                if (!musicPanel.gameObject.scene.IsValid() ||
                    musicPanel.GetComponent<BookLibrarySettingsPanel>() != null ||
                    musicPanel.transform.parent == null ||
                    musicPanel.transform.parent.Find(PanelName) != null)
                {
                    continue;
                }

                GameObject clone = UnityEngine.Object.Instantiate(
                    musicPanel.gameObject,
                    musicPanel.transform.parent);

                clone.name = PanelName;
                clone.transform.SetSiblingIndex(musicPanel.transform.GetSiblingIndex() + 1);
                ReserveVerticalSpace(
                    (RectTransform)musicPanel.transform,
                    (RectTransform)clone.transform);

                UI_AlbumLibraryPathDisplay clonedDisplay =
                    clone.GetComponent<UI_AlbumLibraryPathDisplay>();

                CDAlbumPathSetting pathSetting =
                    ScriptableObject.CreateInstance<CDAlbumPathSetting>();

                pathSetting.SettingID = SettingId;
                pathSetting.DefaultValue = string.Empty;

                AccessTools.Field(typeof(UI_AlbumLibraryPathDisplay), "pathSetting")
                    .SetValue(clonedDisplay, pathSetting);

                UIFolderBrowser browser = GetBrowser(clonedDisplay);
                AccessTools.Field(typeof(UIFolderBrowser), "folderPanelTitle")
                    .SetValue(browser, "Select books library folder");

                clone.AddComponent<BookLibrarySettingsPanel>();
                RenameLabels(clone);
            }
        }

        /// <summary>
        /// BOXROOM's Gameplay settings card positions its rows manually. Cloning the
        /// Music row therefore copies its coordinates exactly. Move the Books row
        /// below it and enlarge the card so the surrounding scroll layout reserves
        /// the new space instead of drawing both controls on top of each other.
        /// </summary>
        private static void ReserveVerticalSpace(
            RectTransform source,
            RectTransform clone)
        {
            // The settings canvas is rendered at roughly half scale, so this leaves
            // a visible ~24 px gap between the Music and Books rows on screen.
            const float spacing = 48f;
            float addedHeight = source.rect.height + spacing;

            clone.anchoredPosition = source.anchoredPosition +
                                     Vector2.down * addedHeight;

            if (source.parent is RectTransform card)
            {
                card.sizeDelta = new Vector2(
                    card.sizeDelta.x,
                    card.sizeDelta.y + addedHeight);

                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.MarkLayoutForRebuild(card);

                if (card.parent is RectTransform settingsContent)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(settingsContent);
            }
        }

        internal static bool IsBookPanel(UI_AlbumLibraryPathDisplay display) =>
            display != null && display.GetComponent<BookLibrarySettingsPanel>() != null;

        internal static UIFolderBrowser GetBrowser(UI_AlbumLibraryPathDisplay display) =>
            (UIFolderBrowser)AccessTools.Field(typeof(UI_AlbumLibraryPathDisplay), "folderBrowser")
                .GetValue(display);

        internal static Button GetApplyButton(UI_AlbumLibraryPathDisplay display) =>
            (Button)AccessTools.Field(typeof(UI_AlbumLibraryPathDisplay), "applyButton")
                .GetValue(display);

        internal static TMP_Text GetStatusLabel(UI_AlbumLibraryPathDisplay display) =>
            (TMP_Text)AccessTools.Field(typeof(UI_AlbumLibraryPathDisplay), "statusLabel")
                .GetValue(display);

        internal static void Refresh(UI_AlbumLibraryPathDisplay display)
        {
            UIFolderBrowser browser = GetBrowser(display);
            browser.SetPath(SourceRoot);
            GetApplyButton(display).interactable = false;
            UpdateStatus(display);
        }

        internal static void Apply(UI_AlbumLibraryPathDisplay display)
        {
            UIFolderBrowser browser = GetBrowser(display);
            if (!browser.HasSelectedPath) return;

            SetSourceRoot(browser.CurrentSelectedPath);
            GetApplyButton(display).interactable = false;
            Boxroom_Books.BookLibrarySystem.LoadCache();
            UpdateStatus(display);
        }

        internal static void Rescan(UI_AlbumLibraryPathDisplay display)
        {
            Boxroom_Books.BookLibrarySystem.LoadCache();
            UpdateStatus(display);
        }

        internal static void OpenFolder()
        {
            if (!string.IsNullOrWhiteSpace(SourceRoot) && Directory.Exists(SourceRoot))
                FileExtensions.OpenDirectory(SourceRoot);
        }

        private static void UpdateStatus(UI_AlbumLibraryPathDisplay display)
        {
            TMP_Text status = GetStatusLabel(display);
            if (status == null) return;

            status.text = string.IsNullOrWhiteSpace(SourceRoot)
                ? "No books folder configured"
                : $"{Boxroom_Books.BookLibrarySystem.GetKnownBooks().Count} books found";
        }

        private static void RenameLabels(GameObject panel)
        {
            foreach (TMP_Text label in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                label.text = label.text
                    .Replace("Music", "Books")
                    .Replace("music", "books")
                    .Replace("Albums", "Books")
                    .Replace("albums", "books")
                    .Replace("Album", "Book")
                    .Replace("album", "book");

                if (label.text.Contains("CD Book Location", StringComparison.OrdinalIgnoreCase))
                    label.text = "Book Folder Location:";
            }
        }
    }

    /// <summary>Marker used to distinguish the cloned Books panel from Music.</summary>
    internal sealed class BookLibrarySettingsPanel : MonoBehaviour { }

    [HarmonyPatch(typeof(UI_AlbumLibraryPathDisplay), nameof(UI_AlbumLibraryPathDisplay.RefreshValue))]
    internal static class RefreshBookLibrarySettingPatch
    {
        private static bool Prefix(UI_AlbumLibraryPathDisplay __instance)
        {
            if (!BookLibrarySettings.IsBookPanel(__instance)) return true;
            BookLibrarySettings.Refresh(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(UI_AlbumLibraryPathDisplay), nameof(UI_AlbumLibraryPathDisplay.OnApplyPressed))]
    internal static class ApplyBookLibrarySettingPatch
    {
        private static bool Prefix(UI_AlbumLibraryPathDisplay __instance)
        {
            if (!BookLibrarySettings.IsBookPanel(__instance)) return true;
            BookLibrarySettings.Apply(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(UI_AlbumLibraryPathDisplay), nameof(UI_AlbumLibraryPathDisplay.OnRescanPressed))]
    internal static class RescanBookLibrarySettingPatch
    {
        private static bool Prefix(UI_AlbumLibraryPathDisplay __instance)
        {
            if (!BookLibrarySettings.IsBookPanel(__instance)) return true;
            BookLibrarySettings.Rescan(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(UI_AlbumLibraryPathDisplay), nameof(UI_AlbumLibraryPathDisplay.OnOpenFolderPressed))]
    internal static class OpenBookLibraryFolderPatch
    {
        private static bool Prefix(UI_AlbumLibraryPathDisplay __instance)
        {
            if (!BookLibrarySettings.IsBookPanel(__instance)) return true;
            BookLibrarySettings.OpenFolder();
            return false;
        }
    }
}
