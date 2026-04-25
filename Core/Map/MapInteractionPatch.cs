using System;

using HarmonyLib;

using JetBrains.Annotations;

using UnityEngine;

namespace NOAutopilot.Core.Map;

[HarmonyPatch(typeof(DynamicMap), "MapControls")]
internal static class MapInteractionPatch
{
    public static void Reset() { }

    [UsedImplicitly]
    private static void Postfix(DynamicMap __instance)
    {
        try
        {
            if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
            {
                return;
            }

            if (__instance == null || !DynamicMap.mapMaximized || !Input.GetMouseButtonDown(1))
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

            if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
            {
                APData.NavQueue.Clear();
            }

            APData.NavQueue.Add(clickedGlobalPos.AsVector3());
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
}
