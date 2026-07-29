using System;

using HarmonyLib;

using UnityEngine;

namespace NOAutopilot.Core.Flight;

[HarmonyPatch]
internal static class ALSLandingPatch
{
    private static float s_nextModeCheckTime;

    [HarmonyPatch(
     typeof(AIPilotLandingState),
     nameof(AIPilotLandingState.FixedUpdateState))]
    [HarmonyPrefix]
    private static void FixedUpdatePrefix(AIPilotLandingState __instance, Pilot pilot)
    {
        try
        {
            if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
            {
                return;
            }

            if (!APData.ALSActive ||
                pilot == null ||
                pilot != APData.LocalPilot ||
                Time.time < s_nextModeCheckTime)
            {
                return;
            }

            s_nextModeCheckTime = Time.time + 2f;
            __instance.LandingState_CheckMode();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[ALSLandingPatch] Error: {ex}");
            Plugin.IsBroken = true;
        }
    }
}
