using System;

using HarmonyLib;

using JetBrains.Annotations;

using UnityEngine;
using UnityEngine.UI;

namespace NOAutopilot.Core.Map;

[HarmonyPatch(typeof(DynamicMap), "CenterMinimizedMap")]
internal static class MinimapTerrainOpacityPatch
{
    private static Image s_terrainImage;

    public static void Reset()
    {
        s_terrainImage = null;
    }

    [UsedImplicitly]
    private static void Postfix(
        DynamicMap __instance,
        GameObject ___mapImage)
    {
        if (Plugin.EnableMinimapLayoutPatch?.Value == false)
        {
            return;
        }

        if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
        {
            return;
        }

        try
        {
            if (DynamicMap.mapMaximized)
            {
                return;
            }

            if (__instance == null || ___mapImage == null)
            {
                return;
            }

            CombatHUD hud = SceneSingleton<CombatHUD>.i;
            if (hud == null || hud.aircraft?.disabled != false)
            {
                return;
            }

            if (!TryCacheTerrainImage(___mapImage))
            {
                return;
            }

            ApplyTerrainOpacity();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[MinimapTerrainOpacityPatch] Error: {ex}");
            Plugin.IsBroken = true;
        }
    }

    private static bool TryCacheTerrainImage(GameObject mapImage)
    {
        if (s_terrainImage != null)
        {
            return true;
        }

        s_terrainImage = mapImage.GetComponent<Image>();

        if (s_terrainImage == null)
        {
            Plugin.Logger.LogWarning("[MinimapTerrainOpacityPatch] Could not find Image on mapImage GameObject");
            return false;
        }

        return true;
    }

    private static void ApplyTerrainOpacity()
    {
        if (s_terrainImage == null)
        {
            return;
        }

        float opacity = Plugin.MinimapTerrainOpacity?.Value ?? 0.1f;
        opacity = Mathf.Clamp01(opacity);

        Color c = s_terrainImage.color;
        c.a = opacity;
        s_terrainImage.color = c;
    }
}
