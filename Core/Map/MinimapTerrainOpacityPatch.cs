using System;

using HarmonyLib;

using JetBrains.Annotations;

using UnityEngine;
using UnityEngine.UI;

namespace NOAutopilot.Core.Map;

internal static class MinimapTerrainOpacityPatch
{
    private static Image s_terrainImage;
    private static float s_lastAppliedOpacity = -1f;

    public static void Reset()
    {
        s_terrainImage = null;
        s_lastAppliedOpacity = -1f;
    }

    private static void ApplyOpacity(float targetOpacity)
    {
        if (s_terrainImage == null)
        {
            DynamicMap map = SceneSingleton<DynamicMap>.i;
            if (map?.mapImage != null)
            {
                s_terrainImage = map.mapImage.GetComponent<Image>();
            }
        }

        if (s_terrainImage == null)
        {
            return;
        }

        float clamped = Mathf.Clamp01(targetOpacity);

        if (Mathf.Approximately(s_terrainImage.color.a, clamped))
        {
            s_lastAppliedOpacity = clamped;
            return;
        }

        Color c = s_terrainImage.color;
        c.a = clamped;
        s_terrainImage.color = c;
        s_lastAppliedOpacity = clamped;
    }

    [HarmonyPatch(typeof(DynamicMap), "LoadMapImage")]
    internal static class ApplyOnLoadMapImage
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
                Reset();

                if (!DynamicMap.mapMaximized)
                {
                    ApplyOpacity(Plugin.MinimapTerrainOpacity?.Value ?? 1f);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MinimapTerrainOpacityPatch] LoadMapImage error: {ex}");
                Plugin.IsBroken = true;
            }
        }
    }

    [HarmonyPatch(typeof(DynamicMap), "Update")]
    internal static class ApplyOnConfigChange
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
                if (DynamicMap.mapMaximized)
                {
                    return;
                }

                float configOp = Plugin.MinimapTerrainOpacity?.Value ?? 1f;
                if (!Mathf.Approximately(configOp, s_lastAppliedOpacity))
                {
                    ApplyOpacity(configOp);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MinimapTerrainOpacityPatch] Update error: {ex}");
                Plugin.IsBroken = true;
            }
        }
    }

    [HarmonyPatch(typeof(DynamicMap), "Maximize")]
    internal static class RestoreOnMaximize
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
                ApplyOpacity(1f);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MinimapTerrainOpacityPatch] Maximize error: {ex}");
                Plugin.IsBroken = true;
            }
        }
    }

    [HarmonyPatch(typeof(DynamicMap), "Minimize")]
    internal static class ApplyOnMinimize
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
                ApplyOpacity(Plugin.MinimapTerrainOpacity?.Value ?? 1f);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MinimapTerrainOpacityPatch] Minimize error: {ex}");
                Plugin.IsBroken = true;
            }
        }
    }
}
