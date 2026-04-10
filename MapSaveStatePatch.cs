extern alias JetBrains;

using System;

using HarmonyLib;

using JetBrains.Annotations;

namespace NOAutopilot;

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

        if (!APData.SaveMapState || __instance == null)
        {
            return;
        }

        try
        {
            APData.SavedMapPos = __instance.positionOffset;
            APData.SavedMapFollow = __instance.followingCamera;
            APData.SavedMapZoom = __instance.GetZoomLevel();
            APData.MapStateStored = true;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"SaveMapState Error: {ex.Message}");
            Plugin.IsBroken = true;
        }
    }
}
