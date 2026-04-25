using System;

using HarmonyLib;

using JetBrains.Annotations;

namespace NOAutopilot.Core.Map;

[HarmonyPatch(typeof(DynamicMap), "Minimize")]
internal static class MapSaveStatePatch
{
    [UsedImplicitly]
    private static void Prefix(DynamicMap __instance)
    {
        if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
        {
            return;
        }

        if (__instance == null || (!APData.SaveMapPosition && !APData.SaveMapZoom))
        {
            return;
        }

        try
        {
            if (APData.SaveMapPosition)
            {
                APData.SavedMapPos = __instance.positionOffset;
                APData.SavedMapFollow = __instance.followingCamera;
                APData.MapPositionStored = true;
            }

            if (APData.SaveMapZoom)
            {
                APData.SavedMapZoom = __instance.GetZoomLevel();
                APData.MapZoomStored = true;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"SaveMapState Error: {ex.Message}");
            Plugin.IsBroken = true;
        }
    }
}
