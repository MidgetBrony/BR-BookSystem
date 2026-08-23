namespace BR_BookSystem
{
    /// <summary>Books' stable setting key; BR-MediaAPI owns persistence and ModsPanel presentation.</summary>
    internal static class BookLibrarySettings
    {
        internal const string SettingId = "BoxroomPlusBooksLibraryRootPath";
        internal static string SourceRoot => BR_MediaAPI.MediaApi.GetLibraryFolder(Boxroom_Books.BookMedia.Type);
    }
}
