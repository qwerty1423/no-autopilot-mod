using System;

using HarmonyLib;

using JetBrains.Annotations;

namespace NOAutopilot.Core.Map;

[HarmonyPatch(typeof(DynamicMap), "Maximize")]
internal static class MapLoadStatePatch
{
    [UsedImplicitly]
    private static void Postfix(DynamicMap __instance)
    {
        if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
        {
            return;
        }

        if (__instance == null)
        {
            return;
        }

        bool shouldRestorePos = APData.SaveMapPosition && APData.MapPositionStored;
        bool shouldRestoreZoom = APData.SaveMapZoom && APData.MapZoomStored;

        if (!shouldRestorePos && !shouldRestoreZoom)
        {
            return;
        }

        try
        {
            bool changed = false;

            if (shouldRestorePos)
            {
                __instance.positionOffset = APData.SavedMapPos;
                __instance.followingCamera = APData.SavedMapFollow;
                changed = true;
            }

            if (shouldRestoreZoom)
            {
                __instance.SetZoomLevel(APData.SavedMapZoom);
                changed = true;
            }

            if (changed)
            {
                __instance.UpdateMap();
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"LoadMapState Error: {ex.Message}");
            Plugin.IsBroken = true;
        }
    }
}
