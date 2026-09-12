using Boxroom_Books;
using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json.Linq;
using SteamShelf.Save;
using System;
using System.Collections.Generic;
using System.IO;

namespace BR_BookSystem
{
    /// <summary>
    /// Temporary pre-Movies migration for rooms saved while Books used media type 2.
    /// Remove this file and its csproj entry after users have had an opportunity to
    /// load and save their rooms with Books 1002. This deliberately does not register,
    /// route, or otherwise interpret media type 2 at runtime.
    /// </summary>
    internal static class LegacyBookMediaTypeMigration
    {
        private const int LegacyBookType = 2;
        private const int CurrentBookType = (int)BookMedia.Type;

        internal sealed class Result
        {
            internal int LooseBooks;
            internal int ShelfBooks;
            internal int UnrecognizedType2References;
            internal int InvalidCustomData;
            internal int Total => LooseBooks + ShelfBooks;
        }

        internal static Result Migrate(RoomStateSave room)
        {
            var result = new Result();
            if (room?.PlacedObjects == null)
                return result;

            HashSet<string> knownBookIds = GetKnownBookIds();

            // A loose object using the dedicated Book placeable is authoritative even
            // if its library folder is currently unavailable. Collect those IDs before
            // examining shelves so matching shelf copies can migrate as well.
            foreach (PlaceableSaveState state in room.PlacedObjects)
                CollectLooseBookId(state, knownBookIds);

            foreach (PlaceableSaveState state in room.PlacedObjects)
            {
                MigrateLooseBook(state, result);
                MigrateShelfSlots(state, knownBookIds, result);
            }

            return result;
        }

        internal static void RunAndLog(RoomStateSave room, string source)
        {
            Result result = Migrate(room);
            MelonLogger.Msg(
                $"[Books media migration] {source}: migrated {result.Total} legacy Book reference(s) " +
                $"from type {LegacyBookType} to {CurrentBookType} " +
                $"({result.LooseBooks} loose, {result.ShelfBooks} shelf). " +
                $"Unrecognized type-{LegacyBookType} references left unchanged: " +
                $"{result.UnrecognizedType2References}; invalid custom-data records: {result.InvalidCustomData}.");

            if (result.Total > 0)
                MelonLogger.Msg("[Books media migration] Save this room once to persist the migrated type 1002 references.");
        }

        private static HashSet<string> GetKnownBookIds()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (BookData book in BookLibrarySystem.GetKnownBooks())
                if (!string.IsNullOrWhiteSpace(book?.Id))
                    ids.Add(book.Id);

            string root = BookLibrarySettings.SourceRoot;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return ids;

            try
            {
                foreach (string metadataPath in Directory.GetFiles(root, "meta.json", SearchOption.AllDirectories))
                {
                    try
                    {
                        JObject metadata = JObject.Parse(File.ReadAllText(metadataPath));
                        string id = metadata.Value<string>("BookID");
                        if (!string.IsNullOrWhiteSpace(id))
                            ids.Add(id);
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[Books media migration] Could not read '{metadataPath}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Books media migration] Could not scan the configured Book library: {ex.Message}");
            }

            return ids;
        }

        private static void CollectLooseBookId(
            PlaceableSaveState state,
            HashSet<string> knownBookIds)
        {
            if (state?.ID != BookAssetBundle.PlaceableId || string.IsNullOrWhiteSpace(state.CustomData))
                return;

            if (!TryParse(state.CustomData, out JObject data, null))
                return;

            if (data.Value<int?>("mediaType") != LegacyBookType)
                return;

            string id = data.Value<string>("mediaId");
            if (!string.IsNullOrWhiteSpace(id))
                knownBookIds.Add(id);
        }

        private static void MigrateLooseBook(PlaceableSaveState state, Result result)
        {
            if (state?.ID != BookAssetBundle.PlaceableId || string.IsNullOrWhiteSpace(state.CustomData))
                return;

            if (!TryParse(state.CustomData, out JObject data, result))
                return;

            if (data.Value<int?>("mediaType") != LegacyBookType ||
                string.IsNullOrWhiteSpace(data.Value<string>("mediaId")))
                return;

            data["mediaType"] = CurrentBookType;
            state.CustomData = data.ToString(Newtonsoft.Json.Formatting.None);
            result.LooseBooks++;
        }

        private static void MigrateShelfSlots(
            PlaceableSaveState state,
            HashSet<string> knownBookIds,
            Result result)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.CustomData))
                return;

            if (!TryParse(state.CustomData, out JObject data, null))
                return;

            if (!(data["Slots"] is JArray slots))
                return;

            bool changed = false;
            foreach (JToken token in slots)
            {
                if (!(token is JObject slot) || slot.Value<int?>("mediaType") != LegacyBookType)
                    continue;

                string id = slot.Value<string>("mediaId");
                if (!string.IsNullOrWhiteSpace(id) && knownBookIds.Contains(id))
                {
                    slot["mediaType"] = CurrentBookType;
                    result.ShelfBooks++;
                    changed = true;
                }
                else
                {
                    result.UnrecognizedType2References++;
                }
            }

            if (changed)
                state.CustomData = data.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static bool TryParse(string json, out JObject data, Result result)
        {
            try
            {
                data = JObject.Parse(json);
                return true;
            }
            catch
            {
                data = null;
                if (result != null)
                    result.InvalidCustomData++;
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.LoadAllData))]
    internal static class LegacyBookMediaTypeLoadPatch
    {
        [HarmonyPriority(Priority.First)]
        private static void Postfix(SaveManager __instance)
        {
            LegacyBookMediaTypeMigration.RunAndLog(__instance?.RoomState, "save-slot load");
        }
    }

    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.LoadRoomFromPath))]
    internal static class LegacyBookMediaTypeImportedRoomPatch
    {
        [HarmonyPriority(Priority.First)]
        private static void Postfix(SaveManager __instance)
        {
            LegacyBookMediaTypeMigration.RunAndLog(__instance?.RoomState, "imported-room load");
        }
    }
}
