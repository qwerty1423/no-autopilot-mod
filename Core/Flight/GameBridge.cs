using System;
using System.Collections.Generic;

using NOAutopilot.Core.Control;

using UnityEngine;

namespace NOAutopilot.Core.Flight;

public enum FlightControllerType
{
    Indi = 0,
    Pid = 1
}

internal static class GameBridge
{
    public static readonly ControllerSettings Settings = new();

    public static UnifiedController Controller;
    public static AircraftModel Model;

    private static Aircraft s_aircraft;
    private static Vector3 s_prevVelocity;
    private static bool s_prevValid;
    private static float s_nextModelRefresh;

    public static bool Ready => Controller != null && s_aircraft != null;

    public static void Reset()
    {
        Controller = null;
        Model = null;
        s_aircraft = null;
        s_prevValid = false;
        s_nextModelRefresh = 0f;
    }

    /// <summary>Called every physics tick before the controller runs.</summary>
    public static void EnsureAircraft(Aircraft aircraft)
    {
        if (aircraft == null)
        {
            Reset();
            return;
        }

        Config.UnifiedConfig.ApplyTo(Settings);

        if (aircraft != s_aircraft || Controller == null)
        {
            s_aircraft = aircraft;
            Model = BuildModel(aircraft);
            Controller = new UnifiedController(Settings, Model);
            s_prevValid = false;
            s_nextModelRefresh = Time.time + 1f;
            Plugin.Logger?.LogInfo(
                $"[UnifiedController] {aircraft.definition?.unitName}: FBW={Model.FbwEnabled} heli={Model.IsHelicopter} " +
                $"mass={Model.Mass:F0} gLimit={Model.GLimit} corner={Model.CornerSpeed} landing={Model.LandingSpeed} " +
                $"thrust={Model.MaxThrust:F0} fbwG={Model.FbwGLimit} fbwCorner={Model.FbwCornerSpeed} " +
                $"fbwPitchVel={Model.FbwMaxPitchAngularVel} fbwRollVel={Model.FbwMaxRollAngularVel}");
            return;
        }

        if (Time.time >= s_nextModelRefresh)
        {
            s_nextModelRefresh = Time.time + 1f;
            RefreshModel(aircraft, Model);
        }
    }

    private static AircraftModel BuildModel(Aircraft aircraft)
    {
        AircraftModel m = new();
        RefreshModel(aircraft, m);
        return m;
    }

    private static void RefreshModel(Aircraft aircraft, AircraftModel m)
    {
        try
        {
            AircraftParameters p = aircraft.GetAircraftParameters();
            if (p != null)
            {
                m.GLimit = p.aircraftGLimit > 0f ? p.aircraftGLimit : 6f;
                m.CornerSpeed = p.cornerSpeed > 1f ? p.cornerSpeed : 150f;
                m.LandingSpeed = p.landingSpeed > 1f ? p.landingSpeed : 60f;
                m.TakeoffSpeed = p.takeoffSpeed;
                m.CruiseThrottle = p.cruiseThrottle;
            }

            m.Mass = Mathf.Max(aircraft.GetMass(), 100f);
            m.MaxWeight = Mathf.Max(aircraft.definition?.aircraftInfo?.maxWeight ?? m.Mass, 1f);
            m.GearHeight = aircraft.definition != null ? aircraft.definition.spawnOffset.y : 1.5f;
            m.HasTailHook = aircraft.weaponManager != null && aircraft.weaponManager.HasTailHook();
            m.MaxThrust = aircraft.GetMaxThrust(out float thrust) ? thrust : 0f;

            ControlsFilter filter = aircraft.GetControlsFilter();
            m.IsHelicopter = filter is HeloControlsFilter ||
                             (APData.LocalPilot != null && APData.LocalPilot.pilotType == Pilot.PilotType.Helo);

            if (filter is HeloControlsFilter helo && helo.heloFlyByWire != null)
            {
                m.FbwEnabled = helo.heloFlyByWire.Enabled;
                m.HeloGLimit = helo.heloFlyByWire.gLimit;
                m.HeloMaxAngularVel = helo.heloFlyByWire.maxAngularVel;
            }
            else if (filter != null && filter.flyByWire != null)
            {
                ControlsFilter.FlyByWire f = filter.flyByWire;
                m.FbwEnabled = f.Enabled;
                m.FbwGLimit = f.gLimitPositive;
                m.FbwCornerSpeed = Mathf.Max(f.cornerSpeed, 10f);
                m.FbwMaxPitchAngularVel = f.maxPitchAngularVel;
                m.FbwMaxRollAngularVel = f.maxRollAngularVel;
                m.FbwMaxRollSpeed = Mathf.Max(f.maxRollSpeed, 1f);
                m.FbwRollTightness = f.rollTightness;
                m.FbwYawTightness = f.yawTightness;
                m.FbwAlphaLimiter = f.alphaLimiter;
                m.FbwLimitFactor = f.limitFactorSmoothed;
            }
            else
            {
                m.FbwEnabled = false;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning($"[UnifiedController] model refresh failed: {ex.Message}");
        }
    }

    public static FlightState BuildState(Aircraft aircraft, Rigidbody rb, Transform tf, float dt)
    {
        Vector3 vel = rb.velocity;
        Vector3 accel = s_prevValid ? (vel - s_prevVelocity) / Mathf.Max(dt, 1e-4f) : Vector3.zero;
        s_prevVelocity = vel;
        s_prevValid = true;

        if (APData.LocalPilot != null)
        {
            Vector3 pilotAccelG = APData.LocalPilot.GetAccel();
            if (pilotAccelG.sqrMagnitude > 0f)
            {
                accel = pilotAccelG * ControlMath.G;
            }
        }

        Vector3 wind = Vector3.zero;
        LevelInfo level = NetworkSceneSingleton<LevelInfo>.i;
        if (level != null)
        {
            wind = level.GetWind(aircraft.GlobalPosition());
        }

        Vector3 pos = tf.position - Datum.originPosition;
        float rho = aircraft.airDensity > 0f ? aircraft.airDensity : 1.225f;

        FlightState s = FlightState.Build(pos, tf.forward, tf.up, tf.right, vel, rb.angularVelocity, accel, wind, rho,
            Mathf.Max(aircraft.radarAlt, 0f), dt);
        s.GearDown = aircraft.gearState != LandingGear.GearState.LockedRetracted;
        s.OnGround = s.GearDown && aircraft.radarAlt < 0.15f;
        if (Model != null)
        {
            ControlsFilter filter = aircraft.GetControlsFilter();
            if (filter != null && filter.flyByWire != null && filter is not HeloControlsFilter)
            {
                Model.FbwLimitFactor = filter.flyByWire.limitFactorSmoothed;
            }
        }

        return s;
    }
}
