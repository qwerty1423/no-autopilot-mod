using System;

using HarmonyLib;

using JetBrains.Annotations;

using NOAutopilot.Core.Flight;

using UnityEngine;

namespace NOAutopilot.Core.Map;

[HarmonyPatch(typeof(DynamicMap), "MapControls")]
internal static class MapInteractionPatch
{
    private const float NodeHitRadius = 8f;
    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    public static void Reset()
    {
        WaypointAltDialog.Close();
    }

    [UsedImplicitly]
    private static void Postfix(DynamicMap __instance)
    {
        try
        {
            if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
            {
                return;
            }

            if (__instance == null || !DynamicMap.mapMaximized || WaypointAltDialog.IsOpen)
            {
                return;
            }

            bool rmb = Input.GetMouseButtonDown(1);
            bool lmb = Input.GetMouseButtonDown(0) && APData.WpAltEdit;
            if (!rmb && !lmb)
            {
                return;
            }

            if (!__instance.TryGetCursorCoordinates(out GlobalPosition clickedGlobalPos))
            {
                return;
            }

            if (__instance.selectedIcons is { Count: > 0 })
            {
                if (__instance.selectedIcons[0] is UnitMapIcon unitIcon)
                {
                    if (unitIcon.unit != null)
                    {
                        if (DynamicMap.GetFactionMode(unitIcon.unit.NetworkHQ) == FactionMode.Friendly
                            && unitIcon.unit is not Building)
                        {
                            return; // there was friendly unit selected as first unit
                        }
                    }
                }
            }

            if (APData.LocalAircraft == null)
            {
                return;
            }

            Vector3 wp = clickedGlobalPos.AsVector3();

            int hit = FindNode(__instance, wp.x, wp.z);
            if (hit >= 0)
            {
                WaypointAltDialog.Open(hit);
                return;
            }

            if (!rmb)
            {
                return;
            }

            wp.y = APData.WpAltEdit ? SuggestAlt(wp.x, wp.z, APData.NavQueue.Count) : 0f;
            APData.NavQueue.Add(wp);
            if (APData.WpAltEdit)
            {
                WaypointAltDialog.Open(APData.NavQueue.Count - 1);
            }
            else
            {
                UnifiedFlight.Announce("Waypoint set");
            }

            if (Plugin.EnableNavonWP.Value)
            {
                APData.NavEnabled = true;
                float currentTargetRoll = APData.TargetRoll;
                if (currentTargetRoll == -999f || currentTargetRoll == 0f)
                {
                    APData.TargetRoll = Plugin.DefaultCRLimit.Value;
                }

                Plugin.SyncMenuValues();
            }

            Plugin.RefreshNavVisuals();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[MapInteractionPatch] Error: {ex}");
            Plugin.IsBroken = true;
        }
    }

    private static int FindNode(DynamicMap map, float x, float z)
    {
        if (APData.NavQueue.Count == 0)
        {
            return -1;
        }

        float factor = 900f / map.mapDimension;
        float zoom = map.mapImage != null ? map.mapImage.transform.localScale.x : 1f;
        float radius = NodeHitRadius / Mathf.Max(zoom, 0.05f);
        float cx = x * factor, cz = z * factor;
        int best = -1;
        float bestD = radius;
        for (int i = 0; i < APData.NavQueue.Count; i++)
        {
            float dx = (APData.NavQueue[i].x * factor) - cx;
            float dz = (APData.NavQueue[i].z * factor) - cz;
            float d = Mathf.Sqrt((dx * dx) + (dz * dz));
            if (d <= bestD)
            {
                bestD = d;
                best = i;
            }
        }

        return best;
    }

    // initial altitude for a new 3D waypoint: terrain height, raised to the previous
    // pinned waypoint's altitude or to the aircraft's altitude when there is none
    internal static float SuggestAlt(float x, float z, int idx)
    {
        float alt = TerrainAt(x, z);
        for (int i = idx - 1; i >= 0; i--)
        {
            if (APData.NavQueue[i].y > 0f)
            {
                return Mathf.Max(alt, APData.NavQueue[i].y);
            }
        }

        return Mathf.Max(alt, APData.CurrentAlt);
    }

    internal static float TerrainAt(float x, float z)
    {
        Vector3 origin = Datum.originPosition;
        Vector3 top = new(x + origin.x, 12000f + origin.y, z + origin.z);
        int n = Physics.SphereCastNonAlloc(top, 30f, Vector3.down, Hits, 24000f,
            (1 << 6) | (1 << 11), QueryTriggerInteraction.Ignore);
        Transform own = APData.PlayerTransform?.root;
        float best = 0f;
        for (int i = 0; i < n; i++)
        {
            if (Hits[i].transform == null || Hits[i].distance <= 0f ||
                (own != null && Hits[i].transform.root == own))
            {
                continue;
            }

            best = Mathf.Max(best, Hits[i].point.y - origin.y);
        }

        return best;
    }
}
