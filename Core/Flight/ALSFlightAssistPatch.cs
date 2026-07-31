using System;

using HarmonyLib;

namespace NOAutopilot.Core.Flight;

[HarmonyPatch(typeof(Aircraft), nameof(Aircraft.SetFlightAssist))]
internal static class ALSFlightAssistPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        Aircraft __instance,
        bool enabled)
    {
        try
        {
            if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
            {
                return true;
            }

            if (!APData.ALSActive ||
                __instance == null ||
                __instance != APData.LocalAircraft)
            {
                return true;
            }

            if (__instance.flightAssist == enabled)
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[ALSFlightAssistPatch] Error: {ex}");
            Plugin.IsBroken = true;
        }

        return true;
    }
}
