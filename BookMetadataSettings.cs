using MelonLoader;
using ModsPanel;

namespace BR_BookSystem
{
    /// <summary>Controls whether Calibre metadata.opf takes priority over meta.json.</summary>
    internal static class BookMetadataSettings
    {
        private static MelonPreferences_Entry<bool> preferCalibreOpf;

        internal static bool PreferCalibreOpf => preferCalibreOpf?.Value ?? false;

        internal static void Initialize()
        {
            MelonPreferences_Category category = MelonPreferences.CreateCategory(
                "BRBookSystemMetadata",
                "BR-BookSystem Metadata");
            preferCalibreOpf = category.CreateEntry(
                "PreferCalibreOpf",
                false,
                "Prefer Calibre metadata.opf");

            ModsPanelApi.RegisterSection("Rusty.BR-BookSystem", "BR-BookSystem", 100)
                .AddLabel("metadata-heading", "Book Metadata")
                .AddToggle(
                    "prefer-calibre-opf",
                    "Use Calibre metadata.opf when available",
                    () => PreferCalibreOpf,
                    SetPreferCalibreOpf)
                .AddLabel(
                    "metadata-note",
                    "When enabled, Calibre supplies descriptive metadata. Existing meta.json BookID and Type are preserved for saved shelves and book behavior. Books without metadata.opf continue using meta.json.");
        }

        private static void SetPreferCalibreOpf(bool value)
        {
            preferCalibreOpf.Value = value;
            MelonPreferences.Save();
            Boxroom_Books.BookLibrarySystem.LoadCache();
        }
    }
}
