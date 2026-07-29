using System;

using HarmonyLib;

namespace NOAutopilot.Core.Flight;

[HarmonyPatch(typeof(ControlsFilter), nameof(ControlsFilter.SetAutoHover))]
internal static class ALSAutoHoverPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        ControlsFilter __instance,
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
                __instance.aircraft == null ||
                __instance.aircraft != APData.LocalAircraft)
            {
                return true;
            }

            bool effectiveEnabled = enabled;

            if (__instance.aircraft.radarAlt < 1f)
            {
                effectiveEnabled = false;
            }

            if (__instance.autoHover.Enabled == effectiveEnabled)
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[ALSDeduplicateAutoHoverPatch] Error: {ex}");

            Plugin.IsBroken = true;
        }

        return true;
    }
}
