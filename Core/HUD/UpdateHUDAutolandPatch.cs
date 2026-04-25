using System;

using HarmonyLib;

using UnityEngine;

namespace NOAutopilot.Core.HUD;

[HarmonyPatch]
internal static class UpdateHUDAutolandPatch
{
    [HarmonyPatch(typeof(AIPilotLandingState), nameof(AIPilotLandingState.FixedUpdateState))]
    [HarmonyPatch(typeof(AIPilotShortLandingState), nameof(AIPilotShortLandingState.FixedUpdateState))]
    [HarmonyPatch(typeof(AIPilotTaxiState), nameof(AIPilotTaxiState.FixedUpdateState))]
    private static void Postfix(PilotBaseState __instance)
    {
        if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
        {
            return;
        }

        if (!APData.ALSActive)
        {
            return;
        }

        if (__instance.pilot != APData.LocalPilot)
        {
            return;
        }

        try
        {
            string baseName = "UNKNOWN";
            bool searching = false;
            bool landed = false;
            bool isTaxi = false;

            if (__instance is AIPilotLandingState ls)
            {
                if (ls.runwayUsage.Runway == null)
                {
                    searching = true;
                }
                else
                {
                    baseName = ls.runwayUsage.Runway.airbase.name;
                }

                landed = ls.touchedDown;
            }
            else if (__instance is AIPilotShortLandingState sls)
            {
                if (sls.runwayUsage.Runway == null)
                {
                    searching = true;
                }
                else
                {
                    baseName = sls.runwayUsage.Runway.airbase.name;
                }

                landed = sls.touchedDown;
            }
            else if (__instance is AIPilotTaxiState ts)
            {
                if (ts.airbase == null)
                {
                    searching = true;
                }
                else
                {
                    baseName = ts.airbase.name;
                }

                landed = true;
                isTaxi = true;
            }

            if (searching)
            {
                APData.ALSStatusText = "ALS: SEARCH";
                APData.ALSStatusColor = ModUtils.GetColor(Plugin.ColorWarn.Value, Color.yellow);
            }
            else if (landed)
            {
                APData.ALSStatusText = isTaxi ? "ALS: TAXI" : "ALS: LANDED";
                APData.ALSStatusColor = ModUtils.GetColor(Plugin.ColorInfo.Value, Color.gray);
            }
            else
            {
                APData.ALSStatusText = $"ALS: {baseName.ToUpper()}";
                APData.ALSStatusColor = ModUtils.GetColor(Plugin.ColorAPOn.Value, Color.green);
            }

            Aircraft ac = __instance.pilot.aircraft;

            SceneSingleton<FlightHud>.i.SetAircraft(ac);

            if (SceneSingleton<CombatHUD>.i.aircraft == ac)
            {
                return;
            }

            SceneSingleton<CombatHUD>.i.SetAircraft(ac);
            FlightHud.EnableCanvas(true);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[UpdateHUDAutolandPatch] Error: {ex}");
            Plugin.IsBroken = true;
        }
    }
}
