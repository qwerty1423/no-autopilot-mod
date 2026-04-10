extern alias JetBrains;

using System;

using HarmonyLib;

using JetBrains.Annotations;

namespace NOAutopilot.Core.Flight;

[HarmonyPatch(typeof(Airbase), "RpcRegisterUsage")]
internal static class SuppressAirbaseRpcPatch
{
    [UsedImplicitly]
    private static bool Prefix(Airbase __instance, Aircraft aircraft, bool isUsing, byte? landingRunway)
    {
        if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
        {
            return true;
        }

        try
        {
            if (APData.ALSActive && aircraft == APData.LocalAircraft)
            {
                if (!__instance.IsClientOnly)
                {
                    return true;
                }

                // this is fine right?
                __instance.CmdRegisterUsage(aircraft, isUsing, landingRunway);
                return false;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[SuppressAirbaseRpcPatch] Error: {ex}");
            Plugin.IsBroken = true;
        }

        return true;
    }
}
