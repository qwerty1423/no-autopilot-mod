using System;
using System.Collections.Generic;

using HarmonyLib;

using JetBrains.Annotations;

using UnityEngine;
using UnityEngine.UI;

namespace NOAutopilot.Core.Map;

[HarmonyPatch(typeof(GridLabels), "LateUpdate")]
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
            float opacity = Mathf.Clamp01(Plugin.MinimapGridOpacity?.Value ?? 1f);

            // Re-cache if instance changed or cache is empty
            if (__instance != s_lastInstance || CachedGraphics.Count == 0)
            {
                RebuildCache(
                    __instance,
                    ___majorParent,
                    ___minorParent,
                    ___gridImages,
                    ___gridToolTip,
                    ___gridAircraft);
                s_lastAppliedOpacity = -1f; // force re-apply
            }

            // Only update colors when the config value actually changed
            if (Mathf.Approximately(opacity, s_lastAppliedOpacity))
            {
                return;
            }

            s_lastAppliedOpacity = opacity;
            ApplyOpacity(opacity);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[MinimapGridOpacityPatch] Error: {ex}");
            Plugin.IsBroken = true;
        }
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

        // Collect all Text components from major/minor label parents
        majorParent?.GetComponentsInChildren(true, CachedGraphics);

        if (minorParent != null)
        {
            List<Graphic> minorGraphics = [];
            minorParent.GetComponentsInChildren(true, minorGraphics);
            CachedGraphics.AddRange(minorGraphics);
        }

        // Collect grid line images
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

        // Include tooltip and aircraft grid text
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
}
