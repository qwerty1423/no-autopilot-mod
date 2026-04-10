extern alias JetBrains;

using System;

using HarmonyLib;

namespace NOAutopilot.Core.Flight;

[HarmonyPatch]
internal static class ALSLandingPatch
{
    [HarmonyPatch(typeof(AIPilotLandingState), nameof(AIPilotLandingState.CheckApproachParameters))]
    [HarmonyPatch(typeof(AIPilotShortLandingState), nameof(AIPilotShortLandingState.CheckApproachParameters))]
    private static bool Prefix(PilotBaseState __instance)
    {
        if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
        {
            return true;
        }

        try
        {
            if (__instance.pilot == APData.LocalPilot)
            {
                if (__instance is AIPilotLandingState ls)
                {
                    ls.SearchBestAirbase();
                    if (ls.runwayUsage.Runway == null)
                    {
                        return false;
                    }
                }
                else if (__instance is AIPilotShortLandingState sls)
                {
                    sls.SearchBestAirbase();
                    if (sls.runwayUsage.Runway == null)
                    {
                        return false;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[ALSLandingPatch] Error: {ex}");
            Plugin.IsBroken = true;
        }

        return true;
    }
}
