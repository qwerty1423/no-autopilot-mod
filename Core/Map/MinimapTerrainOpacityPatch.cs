using System;

using HarmonyLib;

using JetBrains.Annotations;

using UnityEngine;
using UnityEngine.UI;

namespace NOAutopilot.Core.Map;

internal static class MinimapTerrainOpacityPatch
{
    private static Image s_terrainImage;

    public static void Reset()
    {
        s_terrainImage = null;
    }

    private static void SetAlpha(float alpha)
    {
        if (s_terrainImage == null)
        {
            DynamicMap map = SceneSingleton<DynamicMap>.i;
            if (map == null || map.mapImage == null)
            {
                return;
            }

            s_terrainImage = map.mapImage.GetComponent<Image>();
            if (s_terrainImage == null)
            {
                return;
            }
        }

        float clamped = Mathf.Clamp01(alpha);
        Color c = s_terrainImage.color;
        if (Mathf.Approximately(c.a, clamped))
        {
            return;
        }

        c.a = clamped;
        s_terrainImage.color = c;
    }

    [HarmonyPatch(typeof(DynamicMap), "LoadMapImage")]
    internal static class InvalidateOnLoad
    {
        [UsedImplicitly]
        private static void Postfix()
        {
            s_terrainImage = null;
        }
    }

    [HarmonyPatch(typeof(DynamicMap), "Update")]
    internal static class ApplyOnUpdate
    {
        [UsedImplicitly]
        private static void Postfix()
        {
            if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
            {
                return;
            }

            try
            {
                float target = DynamicMap.mapMaximized
                    ? 1f
                    : (Plugin.MinimapTerrainOpacity?.Value ?? 1f);

                SetAlpha(target);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MinimapTerrainOpacityPatch] Update error: {ex}");
                Plugin.IsBroken = true;
            }
        }
    }
}
