using HarmonyLib;
using MelonLoader;
using SteamShelf.Media;
using SteamShelf.Placeables;
using SteamShelf.PlayerTools;
using SteamShelf.Save;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Boxroom_Books
{
    /// <summary>
    /// Recreates BOXROOM's GameBox/CD shelf-pickup path for Books. The vanilla method
    /// switches on known media types and otherwise removes the shelf item without
    /// putting anything in hand. This prefix handles Books fully, then skips that
    /// incompatible branch while leaving all other media untouched.
    /// </summary>
    [HarmonyPatch]
    internal static class PlayerInteractionBookPickupPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(PlayerInteractionTool),
                "PickupItem",
                new[] { typeof(PlacementTag) });
        }

        private static bool Prefix(
            PlayerInteractionTool __instance,
            PlacementTag placeable,
            ref GameObject ___heldPrefab,
            ref PlaceableSaveState ___heldSaveState,
            ref PlacementTag ___currentLookingAtPlaceable,
            ref IMediaItem ___currentHeldMediaItem)
        {
            if (placeable == null)
                return true;

            if (!placeable.TryGetComponent(
                    out PlacedBookProp bookProp))
            {
                // Not a book: let BOXROOM handle games and albums.
                return true;
            }

            BookData book = bookProp.BookData;

            if (book == null)
            {
                MelonLogger.Warning(
                    "Cannot pick up book because its BookData is null.");

                return false;
            }

            ___heldSaveState = bookProp.SaveState;

            placeable.TryClearMeFromItemBelow();

            bookProp.OnPickedUp();

            if (placeable.TryGetComponent(
                    out StackableSurface stackable))
            {
                stackable.TriggerStackCollapse();
            }

            book.IsInHand = true;

            ___currentHeldMediaItem = book;
            ___heldPrefab = placeable.gameObject;

            // Match BOXROOM's normal media pickup sequence.
            ___heldPrefab.SetActive(false);

            if (!TryBeginPlacement(
                    __instance,
                    placeable,
                    ___heldPrefab))
            {
                MelonLogger.Error(
                    "Could not start placement for the picked-up book.");

                ___heldPrefab.SetActive(true);
                ___heldPrefab = null;
                ___heldSaveState = null;
                ___currentHeldMediaItem = null;

                book.IsInHand = false;

                return false;
            }

            ___currentLookingAtPlaceable = null;

            // Skip BOXROOM's game/album-only implementation.
            return false;
        }

        private static bool TryBeginPlacement(
            PlayerInteractionTool tool,
            PlacementTag placementTag,
            GameObject heldObject)
        {
            FieldInfo controllerField =
                AccessTools.Field(
                    typeof(PlayerTool),
                    "controller");

            if (controllerField == null)
            {
                MelonLogger.Error(
                    "Could not find PlayerTool.controller.");

                return false;
            }

            object controller =
                controllerField.GetValue(tool);

            if (controller == null)
            {
                MelonLogger.Error(
                    "PlayerInteractionTool controller is null.");

                return false;
            }

            object registry =
                GetMemberValue(
                    controller,
                    "Registry");

            object objectPlacer =
                GetMemberValue(
                    controller,
                    "ObjectPlacer");

            if (registry == null || objectPlacer == null)
            {
                MelonLogger.Error(
                    "Could not access the placement registry or ObjectPlacer.");

                return false;
            }

            MethodInfo getSettingsMethod =
                AccessTools.Method(
                    registry.GetType(),
                    "Get",
                    new[] { typeof(PlacementType) });

            if (getSettingsMethod == null)
            {
                MelonLogger.Error(
                    "Could not find PlacementToolRegistry.Get.");

                return false;
            }

            object settings =
                getSettingsMethod.Invoke(
                    registry,
                    new object[]
                    {
                    placementTag.PlacementType
                    });

            if (settings == null)
            {
                MelonLogger.Error(
                    $"No placement settings exist for " +
                    $"{placementTag.PlacementType}.");

                return false;
            }

            FieldInfo validMaterialField =
                AccessTools.Field(
                    typeof(PlayerInteractionTool),
                    "validPreviewMaterial");

            FieldInfo invalidMaterialField =
                AccessTools.Field(
                    typeof(PlayerInteractionTool),
                    "invalidPreviewMaterial");

            Material validMaterial =
                validMaterialField?.GetValue(tool) as Material;

            Material invalidMaterial =
                invalidMaterialField?.GetValue(tool) as Material;

            MethodInfo beginMethod =
                AccessTools.Method(
                    objectPlacer.GetType(),
                    "Begin");

            if (beginMethod == null)
            {
                MelonLogger.Error(
                    "Could not find ObjectPlacer.Begin.");

                return false;
            }

            beginMethod.Invoke(
                objectPlacer,
                new[]
                {
                heldObject,
                settings,
                validMaterial,
                invalidMaterial
                });

            return true;
        }

        private static object GetMemberValue(
            object instance,
            string memberName)
        {
            Type type = instance.GetType();

            PropertyInfo property =
                AccessTools.Property(
                    type,
                    memberName);

            if (property != null)
                return property.GetValue(instance);

            FieldInfo field =
                AccessTools.Field(
                    type,
                    memberName);

            return field?.GetValue(instance);
        }
    }
}
