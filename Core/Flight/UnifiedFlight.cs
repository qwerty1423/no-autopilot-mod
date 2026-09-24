using System;

using NOAutopilot.Core.Config;
using NOAutopilot.Core.Control;
using NOAutopilot.Core.Guidance;
using NOAutopilot.Core.PID;

using UnityEngine;

using Random = UnityEngine.Random;

namespace NOAutopilot.Core.Flight;

internal struct TickContext
{
    public float Dt;
    public float StickPitch, StickRoll, StickYaw;
    public bool PilotPitch, PilotRoll, PilotYaw;
    public bool WaitingToReengage;
    public bool UseRandom;
    public float NoiseT;
    public float CurrentG;
    public float GcasOverG;
}

internal static class UnifiedFlight
{
    private static bool s_pitchSleeping, s_rollSleeping, s_spdSleeping;
    private static readonly WaypointGuidance s_waypoints = new();
    private static readonly WaypointRoutePlan s_routePlan = new();
    private static float s_pitchSleepUntil, s_rollSleepUntil, s_spdSleepUntil;
    private static float s_holdPitch, s_holdRoll, s_holdThrottle;
    private static Vector3 s_legPrev, s_legTarget = new(float.NaN, 0f, 0f);

    public static float ThrottleOutput = float.NaN;
    public static float BrakeOutput = float.NaN;

    public static bool UseUnified
    {
        get
        {
            if (UnifiedConfig.ControllerType == null)
            {
                return false;
            }

            if (APData.ALSActive)
            {
                return false;
            }

            if (IsHelicopter && !UnifiedConfig.UnifiedHelicopters.Value)
            {
                return false;
            }

            return UnifiedConfig.ControllerType.Value == FlightControllerType.Indi ||
                (APData.NavEnabled && APData.NavQueue.Count > 0);
        }
    }

    private static bool IsHelicopter =>
        (APData.LocalPilot != null && APData.LocalPilot.pilotType == Pilot.PilotType.Helo) ||
        (APData.LocalAircraft != null && APData.LocalAircraft.GetControlsFilter() is HeloControlsFilter);

    public static void Reset()
    {
        s_pitchSleeping = s_rollSleeping = s_spdSleeping = false;
        s_pitchSleepUntil = s_rollSleepUntil = s_spdSleepUntil = 0f;
        ThrottleOutput = float.NaN;
        BrakeOutput = float.NaN;
        GameBridge.Reset();
    }

    private static float GamePitchToCtrl(float g) => Plugin.InvertPitch.Value ? -g : g;
    private static float CtrlPitchToGame(float c) => Plugin.InvertPitch.Value ? -c : c;
    private static float GameRollToCtrl(float g) => Plugin.InvertRoll.Value ? g : -g;
    private static float CtrlRollToGame(float c) => Plugin.InvertRoll.Value ? c : -c;

    public static void Step(ControlInputs inputs, TickContext ctx)
    {
        Aircraft aircraft = APData.LocalAircraft;
        GameBridge.EnsureAircraft(aircraft);
        UnifiedController ctl = GameBridge.Controller;
        if (ctl == null)
        {
            return;
        }

        FlightState s = GameBridge.BuildState(aircraft, APData.PlayerRB, APData.PlayerTransform, ctx.Dt);

        ControlOverridePatch.HandleKeys(ctx.Dt);

        AutopilotCommand cmd = AutopilotCommand.Empty();
        bool gcas = APData.GCASActive;
        bool apOn = APData.Enabled || gcas;

        BuildAutopilotCommand(s, ctx, ref cmd, apOn);

        if (APData.FBWDisabled && apOn)
        {
            APData.FBWDisabled = false;
            Plugin.UpdateFBWState();
        }

        if (gcas)
        {
            cmd.Lateral = LateralMode.Bank;
            cmd.Bank = 0f;
            cmd.BankLimit = Mathf.PI;
            cmd.AggressiveRoll = true;
            cmd.Vertical = VerticalMode.LoadFactor;
            float gTarget = Mathf.Min(Plugin.GcasMaxG.Value * Mathf.Max(ctx.GcasOverG, 1f), GameBridge.Model.GLimit);
            cmd.LoadFactor = gTarget;
            cmd.MaxG = gTarget;
            cmd.MinG = -1f;
            cmd.DirectPitch = cmd.DirectRoll = cmd.DirectYaw = float.NaN;
        }

        ApplyStepTests(s, ref cmd, apOn);

        AppliedInputs applied = new()
        {
            Pitch = GamePitchToCtrl(inputs.pitch),
            Roll = GameRollToCtrl(inputs.roll),
            Yaw = inputs.yaw,
            Throttle = inputs.throttle
        };

        bool overrideAll = ctx.WaitingToReengage && !gcas;
        applied.PitchOverride = (ctx.PilotPitch || overrideAll) && !gcas;
        applied.RollOverride = (ctx.PilotRoll || overrideAll) && !gcas;
        applied.YawOverride = (ctx.PilotYaw || overrideAll) && !gcas;

        bool hoverCapable = UnifiedConfig.HoverEnabled.Value && GameBridge.Model != null &&
            GameBridge.Model.MaxThrust > (1.05f * GameBridge.Model.Mass * ControlMath.G) &&
            GameBridge.Model.LandingSpeed < 45f;
        bool slowHandOff = !s.OnGround && s.V < 30f && !hoverCapable;
        if (!GameBridge.Model.IsHelicopter && !gcas && (aircraft.IsAutoHoverEnabled() || slowHandOff))
        {
            applied.PitchOverride = applied.RollOverride = applied.YawOverride = true;
        }

        if (applied.PitchOverride && cmd.Vertical == VerticalMode.Altitude &&
            !Plugin.KeepSetAltStick.Value)
        {
            APData.TargetAlt = APData.CurrentAlt;
        }

        Humanize(s, ctx, cmd, ref applied);

        ControlOutput o = ctl.Step(s, cmd, applied);

        // outputs
        if (o.PitchActive)
        {
            float p = o.Pitch;
            if (ctx.UseRandom && !s_pitchSleeping)
            {
                p += (Mathf.PerlinNoise(ctx.NoiseT, 0f) - 0.5f) * 2f * Plugin.RandomStrength.Value;
            }

            inputs.pitch = Mathf.Clamp(CtrlPitchToGame(p), -1f, 1f);
        }

        if (o.RollActive)
        {
            float r = o.Roll;
            if (ctx.UseRandom && !s_rollSleeping)
            {
                r += (Mathf.PerlinNoise(0f, ctx.NoiseT) - 0.5f) * 2f * Plugin.RandomStrength.Value;
            }

            inputs.roll = Mathf.Clamp(CtrlRollToGame(r), -1f, 1f);
        }

        if (o.YawActive)
        {
            inputs.yaw = Mathf.Clamp(o.Yaw, -1f, 1f);
        }

        if (o.ThrottleActive)
        {
            ThrottleOutput = Mathf.Clamp01(o.Throttle);
            inputs.throttle = ThrottleOutput;
        }
        else if (s_spdSleeping && cmd.Speed != SpeedMode.None)
        {
            ThrottleOutput = Mathf.Clamp01(s_holdThrottle);
            inputs.throttle = ThrottleOutput;
        }
        else
        {
            ThrottleOutput = float.NaN;
        }

        ApplyAuxOutputs(inputs);
        LogStepTests(s, ctl);
    }

    private static void BuildAutopilotCommand(FlightState s, TickContext ctx, ref AutopilotCommand cmd,
        bool apOn)
    {
        if (s.OnGround)
        {
            cmd.Vertical = VerticalMode.None;
            cmd.Lateral = LateralMode.Bank;
            cmd.Bank = 0f;
            cmd.Speed = SpeedMode.Throttle;
            cmd.Throttle = 0f;
            BrakeOutput = 1f;
            return;
        }

        bool heli = GameBridge.Model != null && GameBridge.Model.IsHelicopter;
        if (APData.TargetSpeed >= 0f && (!heli || apOn))
        {
            float target = APData.TargetSpeed;
            if (APData.SpeedHoldIsMach)
            {
                target *= LevelInfo.GetSpeedOfSound(s.Altitude);
            }

            cmd.Speed = SpeedMode.Airspeed;
            cmd.Airspeed = target;
            cmd.SpeedIsInertial = true;
            cmd.AllowExtremeThrottle = APData.AllowExtremeThrottle;
            cmd.SpeedPriority = UnifiedConfig.SpeedPriority.Value;
        }

        if (!apOn)
        {
            return;
        }

        if (APData.TargetAlt > 0f)
        {
            cmd.Vertical = VerticalMode.Altitude;
            cmd.Altitude = APData.TargetAlt;
            cmd.MaxClimbRate = Mathf.Max(APData.CurrentMaxClimbRate, 0.5f);
            cmd.MaxDescentRate = Mathf.Max(APData.CurrentMaxClimbRate, 0.5f);
        }

        float gLimitBank = Mathf.Acos(1f / Mathf.Max(UnifiedConfig.ManeuverMaxG.Value, 1.01f));
        if (!APData.NavEnabled || APData.NavQueue.Count == 0)
        {
            s_routePlan.Reset();
        }

        if (APData.NavEnabled && APData.NavQueue.Count > 0 && s.GroundSpeed > 1f)
        {
            ControllerSettings cs = GameBridge.Controller.Settings;
            float userLimit = APData.TargetRoll != -999f && APData.TargetRoll != 0f
                ? Mathf.Abs(APData.TargetRoll)
                : Plugin.DefaultCRLimit.Value;
            float bankLimit = Mathf.Min(userLimit * Mathf.Deg2Rad, gLimitBank);
            cmd.Lateral = LateralMode.Acceleration;

            if (float.IsNaN(s_legTarget.x) ||
                Mathf.Abs(s_legTarget.x - APData.NavQueue[0].x) > 1f ||
                Mathf.Abs(s_legTarget.z - APData.NavQueue[0].z) > 1f)
            {
                s_legPrev = float.IsNaN(s_legTarget.x) ? s.Position : s_legTarget;
                s_legTarget = APData.NavQueue[0];
            }
            else
            {
                s_legTarget.y = APData.NavQueue[0].y;
            }

            cmd.LateralAccel = s_waypoints.Step(s, APData.NavQueue[0], APData.NavQueue.Count > 1,
                APData.NavQueue.Count > 1 ? APData.NavQueue[1] : default, s_legPrev, bankLimit, cs.CourseGain,
                cs.CourseRollRate);
            cmd.BankLimit = bankLimit;

            if (s_routePlan.Update(s, APData.NavQueue, UnifiedConfig.ManeuverMaxG.Value,
                    out float routeAlt, out float routeSlope))
            {
                cmd.Vertical = VerticalMode.Altitude;
                cmd.Altitude = routeAlt;
                cmd.VerticalSpeedFeedForward = routeSlope * s.GroundSpeed;
                cmd.MaxClimbRate = Mathf.Max(APData.CurrentMaxClimbRate, 0.5f);
                cmd.MaxDescentRate = Mathf.Max(APData.CurrentMaxClimbRate, 0.5f);
                APData.TargetAlt = routeAlt;
            }
        }
        else if (APData.TargetCourse >= 0f && s.GroundSpeed > 1f)
        {
            float userLimit = APData.TargetRoll != -999f && APData.TargetRoll != 0f
                ? Mathf.Abs(APData.TargetRoll)
                : Plugin.DefaultCRLimit.Value;
            cmd.Lateral = LateralMode.Course;
            cmd.Course = APData.TargetCourse * Mathf.Deg2Rad;
            cmd.BankLimit = Mathf.Min(userLimit * Mathf.Deg2Rad, gLimitBank);
        }
        else if (APData.TargetRoll != -999f)
        {
            cmd.Lateral = LateralMode.Bank;
            cmd.Bank = -APData.TargetRoll * Mathf.Deg2Rad;
            cmd.BankLimit = Mathf.PI;
        }
    }

    private static void Humanize(FlightState s, TickContext ctx, AutopilotCommand cmd,
        ref AppliedInputs applied)
    {
        UnifiedController ctl = GameBridge.Controller;
        if (!ctx.UseRandom)
        {
            s_pitchSleeping = s_rollSleeping = s_spdSleeping = false;
            s_holdThrottle = ctl.LastThrottle;
            return;
        }

        float now = Time.time;

        // pitch
        if (cmd.Vertical == VerticalMode.Altitude)
        {
            float altErr = Mathf.Abs(cmd.Altitude - s.Altitude);
            float vs = Mathf.Abs(s.VerticalSpeed);
            if (!s_pitchSleeping)
            {
                if (altErr < Plugin.Rand_Alt_Inner.Value && vs < Plugin.Rand_VS_Inner.Value)
                {
                    s_pitchSleepUntil = now + Random.Range(Plugin.Rand_PitchSleepMin.Value, Plugin.Rand_PitchSleepMax.Value);
                    s_pitchSleeping = true;
                    s_holdPitch = 0f;
                }
            }
            else if (altErr > Plugin.Rand_Alt_Outer.Value || vs > Plugin.Rand_VS_Outer.Value || now > s_pitchSleepUntil)
            {
                s_pitchSleeping = false;
            }
        }
        else
        {
            s_pitchSleeping = false;
        }

        // roll
        if (cmd.Lateral == LateralMode.Bank)
        {
            float rollErr = Mathf.Abs(ControlMath.WrapPi(cmd.Bank - s.Phi)) * Mathf.Rad2Deg;
            float rate = Mathf.Abs(s.P) * Mathf.Rad2Deg;
            if (!s_rollSleeping)
            {
                if (rollErr < Plugin.Rand_Roll_Inner.Value && rate < Plugin.Rand_RollRate_Inner.Value)
                {
                    s_rollSleepUntil = now + Random.Range(Plugin.Rand_RollSleepMin.Value, Plugin.Rand_RollSleepMax.Value);
                    s_rollSleeping = true;
                    s_holdRoll = 0f;
                }
            }
            else if (rollErr > Plugin.Rand_Roll_Outer.Value || rate > Plugin.Rand_RollRate_Outer.Value ||
                     now > s_rollSleepUntil)
            {
                s_rollSleeping = false;
            }
        }
        else
        {
            s_rollSleeping = false;
        }

        // speed
        if (cmd.Speed == SpeedMode.Airspeed)
        {
            float err = Mathf.Abs(cmd.Airspeed - s.Speed);
            float acc = Mathf.Abs(s.VDot);
            if (!s_spdSleeping)
            {
                if (err < Plugin.Rand_Spd_Inner.Value && acc < Plugin.Rand_Acc_Inner.Value)
                {
                    s_spdSleepUntil = now + Random.Range(Plugin.Rand_Spd_SleepMin.Value, Plugin.Rand_Spd_SleepMax.Value);
                    s_spdSleeping = true;
                    s_holdThrottle = ctl.LastThrottle;
                }
            }
            else if (err > Plugin.Rand_Spd_Outer.Value || acc > Plugin.Rand_Acc_Outer.Value || now > s_spdSleepUntil)
            {
                s_spdSleeping = false;
            }
        }
        else
        {
            s_spdSleeping = false;
        }

        if (s_pitchSleeping && !applied.PitchOverride)
        {
            applied.PitchOverride = true;
            applied.Pitch = s_holdPitch;
        }

        if (s_rollSleeping && !applied.RollOverride)
        {
            applied.RollOverride = true;
            applied.Roll = s_holdRoll;
        }

        if (s_spdSleeping && !applied.ThrottleOverride)
        {
            applied.ThrottleOverride = true;
            applied.Throttle = s_holdThrottle;
        }
    }

    private static void ApplyStepTests(FlightState s, ref AutopilotCommand cmd, bool apOn)
    {
        if (!PIDLogger.IsTestActive && Plugin.StepTestLoop.Value == PIDLogger.StepTarget.None)
        {
            return;
        }

        if (PIDLogger.IsTesting(PIDLogger.StepTarget.Spd))
        {
            cmd.Speed = SpeedMode.Airspeed;
            cmd.SpeedIsInertial = true;
            float baseSpd = cmd.Airspeed > 0f ? cmd.Airspeed : s.Speed;
            cmd.Airspeed = PIDLogger.GetSetpoint(PIDLogger.StepTarget.Spd, baseSpd, s.Speed);
        }

        if (!apOn)
        {
            return;
        }

        float currentRollMod = -s.Phi * Mathf.Rad2Deg;
        float currentPitch = s.Theta * Mathf.Rad2Deg;

        if (PIDLogger.IsTesting(PIDLogger.StepTarget.Alt))
        {
            cmd.Vertical = VerticalMode.Altitude;
            float baseAlt = cmd.Altitude > 0f ? cmd.Altitude : s.Altitude;
            cmd.Altitude = PIDLogger.GetSetpoint(PIDLogger.StepTarget.Alt, baseAlt, s.Altitude);
            cmd.MaxClimbRate = cmd.MaxDescentRate = Mathf.Max(APData.CurrentMaxClimbRate, 0.5f);
        }
        else if (PIDLogger.IsTesting(PIDLogger.StepTarget.VS))
        {
            cmd.Vertical = VerticalMode.VerticalSpeed;
            cmd.VerticalSpeed = PIDLogger.GetSetpoint(PIDLogger.StepTarget.VS, s.VerticalSpeed, s.VerticalSpeed);
        }
        else if (PIDLogger.IsTesting(PIDLogger.StepTarget.Pitch))
        {
            cmd.Vertical = VerticalMode.PitchAttitude;
            cmd.PitchAttitude = PIDLogger.GetSetpoint(PIDLogger.StepTarget.Pitch, currentPitch, currentPitch) *
                                Mathf.Deg2Rad;
        }
        else if (PIDLogger.IsTesting(PIDLogger.StepTarget.GCAS))
        {
            cmd.Vertical = VerticalMode.LoadFactor;
            cmd.LoadFactor = PIDLogger.GetSetpoint(PIDLogger.StepTarget.GCAS, s.NLift, s.NLift);
            cmd.MaxG = Mathf.Max(cmd.LoadFactor, 1.5f);
        }

        if (PIDLogger.IsTesting(PIDLogger.StepTarget.Roll))
        {
            cmd.Lateral = LateralMode.Bank;
            cmd.Bank = -PIDLogger.GetSetpoint(PIDLogger.StepTarget.Roll, currentRollMod, currentRollMod) *
                       Mathf.Deg2Rad;
            cmd.BankLimit = Mathf.PI;
        }
        else if (PIDLogger.IsTesting(PIDLogger.StepTarget.RollRate))
        {
            float rateMod = -s.P * Mathf.Rad2Deg;
            cmd.RollRateOverride = -PIDLogger.GetSetpoint(PIDLogger.StepTarget.RollRate, 0f, rateMod) * Mathf.Deg2Rad;
        }
        else if (PIDLogger.IsTesting(PIDLogger.StepTarget.Crs))
        {
            float crs = s.Chi * Mathf.Rad2Deg;
            float baseCrs = cmd.Lateral == LateralMode.Course ? cmd.Course * Mathf.Rad2Deg : crs;
            cmd.Lateral = LateralMode.Course;
            cmd.Course = PIDLogger.GetSetpoint(PIDLogger.StepTarget.Crs, baseCrs, crs) * Mathf.Deg2Rad;
            cmd.BankLimit = Mathf.Max(cmd.BankLimit, Plugin.DefaultCRLimit.Value * Mathf.Deg2Rad);
        }
        else if (PIDLogger.IsTesting(PIDLogger.StepTarget.Yaw))
        {
            float beta = s.Beta * Mathf.Rad2Deg;
            cmd.SideslipOverride = PIDLogger.GetSetpoint(PIDLogger.StepTarget.Yaw, 0f, beta) * Mathf.Deg2Rad;
            if (cmd.Lateral == LateralMode.None)
            {
                cmd.Lateral = LateralMode.Bank;
                cmd.Bank = s.Phi;
            }
        }

    }

    private static void LogStepTests(FlightState s, UnifiedController ctl)
    {
        if (!PIDLogger.IsTestActive)
        {
            return;
        }

        ControllerTelemetry t = ctl.Telemetry;
        PIDLogger.Log(PIDLogger.StepTarget.Alt, t.VsCmd, s.Altitude);
        PIDLogger.Log(PIDLogger.StepTarget.VS, t.NCmd, s.VerticalSpeed);
        PIDLogger.Log(PIDLogger.StepTarget.Pitch, t.QCmd * Mathf.Rad2Deg, s.Theta * Mathf.Rad2Deg);
        PIDLogger.Log(PIDLogger.StepTarget.Roll, -t.PCmd * Mathf.Rad2Deg, -s.Phi * Mathf.Rad2Deg);
        PIDLogger.Log(PIDLogger.StepTarget.RollRate, ctl.LastRoll, -s.P * Mathf.Rad2Deg);
        PIDLogger.Log(PIDLogger.StepTarget.Yaw, ctl.LastYaw, s.Beta * Mathf.Rad2Deg);
        PIDLogger.Log(PIDLogger.StepTarget.Crs, t.ALatCmd, s.Chi * Mathf.Rad2Deg);
        PIDLogger.Log(PIDLogger.StepTarget.Spd, ctl.LastThrottle, s.Speed);
        PIDLogger.Log(PIDLogger.StepTarget.GCAS, ctl.LastPitch, s.NLift);
    }

    private static void ApplyAuxOutputs(ControlInputs inputs)
    {
        if (!float.IsNaN(BrakeOutput) && APData.PlayerRB != null && APData.PlayerRB.velocity.magnitude > 0.5f)
        {
            inputs.brake = Mathf.Clamp01(BrakeOutput);
        }
        else
        {
            BrakeOutput = float.NaN;
        }
    }

    public static void Announce(string text)
    {
        try
        {
            AircraftActionsReport report = SceneSingleton<AircraftActionsReport>.i;
            report?.ReportText(text, 4f);
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogDebug($"[UnifiedFlight] announce failed: {ex.Message}");
        }
    }
}
