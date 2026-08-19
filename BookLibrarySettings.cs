using SteamShelf.Save;
using System.IO;

namespace BR_BookSystem
{
    /// <summary>
    /// Owns the persisted Books library path and exposes it through ModsPanel.
    /// Keeping persistence here means ModsPanel remains a presentation API: it
    /// never needs to understand BR-BookSystem's cache or save format.
    /// </summary>
    internal static class BookLibrarySettings
    {
        internal const string SettingId = "BoxroomPlusBooksLibraryRootPath";
        private const string PanelOwnerId = "Rusty.BR-BookSystem";

        internal static string SourceRoot
        {
            get
            {
                if (!Singleton<SaveManager>.HasInstance()) return string.Empty;
                return Singleton<SaveManager>.Instance.Settings.GetValue(
                    SettingId,
                    string.Empty);
            }
        }

        internal static void RegisterPanel()
        {
            global::ModsPanel.ModsPanelApi
                .RegisterSection(PanelOwnerId, "BR-BookSystem", 100)
                .Clear()
                .AddFolder(
                    "books-folder",
                    "Book Folder Location",
                    () => SourceRoot,
                    SetSourceRootAndReload,
                    GetLibraryStatus,
                    RefreshLibrary,
                    "Select books library folder")
                .AddButton(
                    "open-books-folder",
                    "Open the configured Books folder",
                    "Open Folder",
                    OpenFolder);
        }

        private static void SetSourceRootAndReload(string path)
        {
            if (!Singleton<SaveManager>.HasInstance()) return;

            Singleton<SaveManager>.Instance.Settings.SetValue(
                SettingId,
                path ?? string.Empty);
            Singleton<SaveManager>.Instance.Settings.Save();
            Boxroom_Books.BookLibrarySystem.LoadCache();
        }

        private static void RefreshLibrary()
        {
            Boxroom_Books.BookLibrarySystem.LoadCache();
        }

        private static string GetLibraryStatus()
        {
            if (string.IsNullOrWhiteSpace(SourceRoot))
                return "No books folder configured";

            return $"{Boxroom_Books.BookLibrarySystem.GetKnownBooks().Count} books found";
        }

        private static void OpenFolder()
        {
            if (!string.IsNullOrWhiteSpace(SourceRoot) && Directory.Exists(SourceRoot))
                FileExtensions.OpenDirectory(SourceRoot);
        }
    }
}
