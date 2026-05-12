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
    private static GridLabels s_lastInstance;
    private static bool s_outdated = true;

    public static void Reset()
    {
        CachedGraphics.Clear();
        s_lastInstance = null;
        s_outdated = true;
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
        s_outdated = false;

        majorParent?.GetComponentsInChildren(true, CachedGraphics);

        if (minorParent != null)
        {
            List<Graphic> minor = [];
            minorParent.GetComponentsInChildren(true, minor);
            CachedGraphics.AddRange(minor);
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

    private static bool NeedsUpdate(float targetOpacity)
    {
        for (int i = CachedGraphics.Count - 1; i >= 0; i--)
        {
            Graphic graphic = CachedGraphics[i];
            if (graphic == null)
            {
                CachedGraphics.RemoveAt(i);
                continue;
            }

            return !Mathf.Approximately(graphic.color.a, targetOpacity);
        }

        return false;
    }

    private static void ApplyOpacity(float opacity)
    {
        float clamped = Mathf.Clamp01(opacity);

        foreach (Graphic graphic in CachedGraphics)
        {
            if (graphic == null)
            {
                continue;
            }

            Color c = graphic.color;
            c.a = clamped;
            graphic.color = c;
        }
    }

    [HarmonyPatch(typeof(GridLabels), "SetupGrid")]
    internal static class InvalidateOnSetup
    {
        [UsedImplicitly]
        private static void Postfix()
        {
            s_outdated = true;
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
                if (s_outdated || __instance != s_lastInstance || CachedGraphics.Count == 0)
                {
                    RebuildCache(
                        __instance,
                        ___majorParent,
                        ___minorParent,
                        ___gridImages,
                        ___gridToolTip,
                        ___gridAircraft);
                }

                float target = DynamicMap.mapMaximized
                    ? 1f
                    : (Plugin.MinimapGridOpacity?.Value ?? 1f);

                if (NeedsUpdate(target))
                {
                    ApplyOpacity(target);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MinimapGridOpacityPatch] LateUpdate error: {ex}");
                Plugin.IsBroken = true;
            }
        }
    }
}
