extern alias JetBrains;

using System;

using HarmonyLib;

using JetBrains.Annotations;

using NOAutopilot.Core.Flight;

using UnityEngine;

namespace NOAutopilot.Core.HUD;

[HarmonyPatch(typeof(FlightHud), "SetHUDInfo")]
internal static class HudPatch
{
    private static GameObject s_lastVehicleObj;

    public static void Reset()
    {
        s_lastVehicleObj = null;
    }

    [UsedImplicitly]
    private static void Postfix(object playerVehicle, float altitude)
    {
        if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
        {
            return;
        }

        try
        {
            APData.CurrentAlt = altitude;
            if (playerVehicle is not Component v)
            {
                s_lastVehicleObj = null;
                APData.LocalAircraft = null;
                APData.PlayerRB = null;
                return;
            }

            Aircraft foundAircraft = v.GetComponent<Aircraft>();

            if (foundAircraft == null || (v.gameObject == s_lastVehicleObj && APData.LocalAircraft != null))
            {
                return;
            }

            s_lastVehicleObj = v.gameObject;
            APData.Reset();
            ControlOverridePatch.Reset();
            HUDVisualsPatch.Reset();

            APData.LocalAircraft = foundAircraft;
            APData.PlayerTransform = v.transform;
            APData.PlayerRB = v.GetComponent<Rigidbody>();

            APData.TargetAlt = altitude;
            APData.TargetRoll = 0f;
            APData.LocalWeaponManager = APData.LocalAircraft.weaponManager;
            APData.SaveMapState = Plugin.SaveMapState.Value;

            Pilot[] pilots = APData.LocalAircraft.pilots;
            if (pilots?.Length > 0)
            {
                APData.LocalPilot = pilots[0];
                APData.GCASEnabled = APData.LocalPilot.pilotType switch
                {
                    Pilot.PilotType.Helo => Plugin.EnableGCASHelo.Value,
                    Pilot.PilotType.Tiltwing => Plugin.EnableGCASTiltwing.Value,
                    _ => Plugin.EnableGCAS.Value
                };
            }

            Plugin.SyncMenuValues();
            Plugin.CleanUpFBW();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[HudPatch] Error: {ex}");
            Plugin.IsBroken = true;
        }
    }
}
