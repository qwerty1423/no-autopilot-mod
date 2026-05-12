using System;

using HarmonyLib;

using JetBrains.Annotations;

using UnityEngine;

namespace NOAutopilot.Core.Map;

[HarmonyPatch(typeof(DynamicMap), nameof(DynamicMap.Minimize))]
internal static class MinimapZoomPatch
{
    private const float VanillaScale = 2f;

    [UsedImplicitly]
    private static void Postfix(GameObject ___mapImage, Transform ___mapScaleCenter)
    {
        if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
        {
            return;
        }

        try
        {
            if (___mapImage == null || ___mapScaleCenter == null)
            {
                return;
            }

            float zoom = Plugin.MinimapDefaultZoom?.Value ?? 1f;
            float scale = VanillaScale * zoom;

            ___mapScaleCenter.localScale = Vector3.one * scale;
            ___mapImage.transform.localScale = Vector3.one * scale;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[MinimapZoomPatch] Minimize error: {ex}");
            Plugin.IsBroken = true;
        }
    }
}

[HarmonyPatch(typeof(DynamicMap), nameof(DynamicMap.Maximize))]
internal static class MinimapZoomRestorePatch
{
    private const float VanillaScale = 2f;

    [UsedImplicitly]
    private static void Prefix(GameObject ___mapImage, Transform ___mapScaleCenter)
    {
        if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
        {
            return;
        }

        try
        {
            if (___mapImage == null || ___mapScaleCenter == null)
            {
                return;
            }

            ___mapScaleCenter.localScale = Vector3.one * VanillaScale;
            ___mapImage.transform.localScale = Vector3.one * VanillaScale;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[MinimapZoomPatch] Maximize error: {ex}");
            Plugin.IsBroken = true;
        }
    }
}
