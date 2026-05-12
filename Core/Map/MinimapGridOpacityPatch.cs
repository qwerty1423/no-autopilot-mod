using System;
using System.Collections.Generic;

using HarmonyLib;

using JetBrains.Annotations;

using UnityEngine;
using UnityEngine.UI;

namespace NOAutopilot.Core.Map;

internal static class MinimapGridOpacityPatch
{
    private static readonly List<Graphic> CachedGraphics = [];
    private static float s_lastAppliedOpacity = -1f;
    private static GridLabels s_lastInstance;

    public static void Reset()
    {
        CachedGraphics.Clear();
        s_lastAppliedOpacity = -1f;
        s_lastInstance = null;
    }

    private static void RebuildCache(
        GridLabels instance,
        GameObject majorParent,
        GameObject minorParent,
        GameObject[] gridImages,
        Text gridToolTip,
        Text gridAircraft)
    {
        CachedGraphics.Clear();
        s_lastInstance = instance;

        majorParent?.GetComponentsInChildren(true, CachedGraphics);

        if (minorParent != null)
        {
            List<Graphic> minorGraphics = [];
            minorParent.GetComponentsInChildren(true, minorGraphics);
            CachedGraphics.AddRange(minorGraphics);
        }

        if (gridImages != null)
        {
            foreach (GameObject gridObj in gridImages)
            {
                if (gridObj != null)
                {
                    Image img = gridObj.GetComponent<Image>();
                    if (img != null)
                    {
                        CachedGraphics.Add(img);
                    }
                }
            }
        }

        if (gridToolTip != null)
        {
            CachedGraphics.Add(gridToolTip);
        }

        if (gridAircraft != null)
        {
            CachedGraphics.Add(gridAircraft);
        }
    }

    private static void ApplyOpacity(float opacity)
    {
        opacity = Mathf.Clamp01(opacity);
        s_lastAppliedOpacity = opacity;

        for (int i = CachedGraphics.Count - 1; i >= 0; i--)
        {
            Graphic graphic = CachedGraphics[i];
            if (graphic == null)
            {
                CachedGraphics.RemoveAt(i);
                continue;
            }

            Color c = graphic.color;
            c.a = opacity;
            graphic.color = c;
        }
    }

    [HarmonyPatch(typeof(GridLabels), "SetupGrid")]
    internal static class InvalidateCacheOnSetup
    {
        [UsedImplicitly]
        private static void Postfix()
        {
            Reset();
        }
    }

    [HarmonyPatch(typeof(GridLabels), "LateUpdate")]
    internal static class ApplyOnLateUpdate
    {
        [UsedImplicitly]
        private static void Postfix(
            GridLabels __instance,
            GameObject ___majorParent,
            GameObject ___minorParent,
            GameObject[] ___gridImages,
            Text ___gridToolTip,
            Text ___gridAircraft)
        {
            if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
            {
                return;
            }

            try
            {
                // Only apply reduced opacity when minimized
                if (DynamicMap.mapMaximized)
                {
                    return;
                }

                if (__instance != s_lastInstance || CachedGraphics.Count == 0)
                {
                    RebuildCache(
                        __instance,
                        ___majorParent,
                        ___minorParent,
                        ___gridImages,
                        ___gridToolTip,
                        ___gridAircraft);
                }

                float opacity = Plugin.MinimapGridOpacity?.Value ?? 1f;

                if (!Mathf.Approximately(opacity, s_lastAppliedOpacity))
                {
                    ApplyOpacity(opacity);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MinimapGridOpacityPatch] LateUpdate error: {ex}");
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
                Plugin.Logger.LogError($"[MinimapGridOpacityPatch] Maximize error: {ex}");
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
                float opacity = Plugin.MinimapGridOpacity?.Value ?? 1f;
                ApplyOpacity(opacity);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MinimapGridOpacityPatch] Minimize error: {ex}");
                Plugin.IsBroken = true;
            }
        }
    }
}
