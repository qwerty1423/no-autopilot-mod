extern alias JetBrains;

using System;

using HarmonyLib;

using JetBrains.Annotations;

using NOAutopilot.Core.Flight;

using UnityEngine;

namespace NOAutopilot.Core.HUD;

[HarmonyPatch(typeof(FlightHud), "SetAircraft")]
internal static class HudPatch
{
    private static Aircraft s_lastAircraft;

    public static void Reset() => s_lastAircraft = null;

    public static void Initialize()
    {
        if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
        {
            return;
        }

        try
        {
            FlightHud hud = SceneSingleton<FlightHud>.i;
            if (hud == null || hud.aircraft == null)
            {
                return;
            }

            Postfix(hud.aircraft);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[HudPatch] Error: {ex}");
            Plugin.IsBroken = true;
        }
    }

    [UsedImplicitly]
    private static void Postfix(Aircraft aircraft)
    {
        if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
        {
            return;
        }

        if (aircraft == s_lastAircraft || !aircraft)
        {
            return;
        }

        try
        {
            s_lastAircraft = aircraft;
            APData.Reset();
            ControlOverridePatch.Reset();
            HUDVisualsPatch.Reset();

            APData.LocalAircraft = aircraft;
            APData.PlayerTransform = aircraft.transform;
            APData.PlayerRB = aircraft.cockpit?.rb ?? aircraft.GetComponent<Rigidbody>();
            APData.LocalWeaponManager = aircraft.weaponManager;

            APData.TargetAlt = aircraft.transform.position.GlobalY();

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

            if (Plugin.LockWingsSwept.Value)
            {
                SwingWingController swing = APData.LocalAircraft.GetComponent<SwingWingController>();
                if (swing != null)
                {
                    swing.forwardMach = float.MaxValue;
                    swing.sweptMach = 0f;
                }
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
