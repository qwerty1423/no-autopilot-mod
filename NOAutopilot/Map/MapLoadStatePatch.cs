extern alias JetBrains;

using System;

using HarmonyLib;

using JetBrains.Annotations;

namespace NOAutopilot;

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

        if (!APData.SaveMapState || !APData.MapStateStored || __instance == null)
        {
            return;
        }

        try
        {
            __instance.positionOffset = APData.SavedMapPos;
            __instance.followingCamera = APData.SavedMapFollow;
            __instance.SetZoomLevel(APData.SavedMapZoom);
            __instance.UpdateMap();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"LoadMapState Error: {ex.Message}");
            Plugin.IsBroken = true;
        }
    }
}
