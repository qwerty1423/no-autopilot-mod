using BepInEx.Configuration;

using NOAutopilot.Core.Control;
using NOAutopilot.Core.Flight;

using UnityEngine;

namespace NOAutopilot.Core.Config;

/// <summary>
/// configuration for the unified controller.
/// </summary>
public static class UnifiedConfig
{
    public static ConfigEntry<FlightControllerType> ControllerType;
    public static ConfigEntry<float> ManeuverMaxG, ManeuverMinG, AltitudeGain, VerticalSpeedGain, CourseGain;
    public static ConfigEntry<float> SpeedGain, BankGain, LoadFactorGain, SideslipGain;
    public static ConfigEntry<float> AltitudeCaptureLead, VerticalSpeedIntegralGain, VerticalSpeedIntegralLimit;
    public static ConfigEntry<bool> SpeedPriority, OnlineIdentification, MimoEnabled;
    public static ConfigEntry<float> EffectivenessMargin, StickRateLimit, YawAuthority, FilterCutoff;
    public static ConfigEntry<float> CompensationGain, PitchLag, RollLag, YawLag, PitchAuthority, RollAuthority;
    public static ConfigEntry<float> LoadFactorRateLimit, EnergyFeedForward;
    public static ConfigEntry<int> InputDelayTicks;
    public static ConfigEntry<bool> ShowWaypointAlts;

    public static void Bind(ConfigFile cfg)
    {
        const string main = "INDI";
        ControllerType = cfg.Bind(main, "01. Controller", FlightControllerType.Indi,
            "INDI: Incremental Nonlinear Dynamic Inversion, PID: Proportional Integral Derivative, something like that. INDI is better for strange or damaged aircraft, but may be worse for seaskimming currently.");
        ManeuverMaxG = cfg.Bind(main, "02. Maximum commanded load factor", 5f,
            "maximum G limit");
        ManeuverMinG = cfg.Bind(main, "03. Minimum commanded load factor", -1.5f,
            "minimum G limit");
        BankGain = cfg.Bind(main, "04. Bank response", 2.2f, "Bank error to roll rate gain.");
        LoadFactorGain = cfg.Bind(main, "05. Load-factor response", 2.5f, "Load-factor error response gain.");
        AltitudeGain = cfg.Bind(main, "06. Altitude response", 0.3f, "Altitude error to vertical-speed command.");
        VerticalSpeedGain = cfg.Bind(main, "07. Vertical-speed response", 1f, "Vertical-speed error response.");
        CourseGain = cfg.Bind(main, "08. Course response", 0.6f, "Course error response.");
        SpeedGain = cfg.Bind(main, "09. Speed response", 0.4f, "Airspeed error to energy-rate command.");
        SideslipGain = cfg.Bind(main, "10. Sideslip response", 1.2f, "Sideslip error to yaw-rate command.");
        SpeedPriority = cfg.Bind(main, "11. Speed priority at power limits", false,
            "Reduce climb/descent demand when throttle cannot maintain the requested speed.");

        const string adv = "INDI - Advanced";
        MimoEnabled = cfg.Bind(adv, "01. Enable MIMO allocation", true,
            "Keep enabled.");
        OnlineIdentification = cfg.Bind(adv, "02. Adapt effectiveness online", true,
            "Keep enabled.");
        FilterCutoff = cfg.Bind(adv, "03. Synchronized filter cutoff (rad/s)", 1.2f,
            "Applied equally to measured rates and delayed input feedback.");
        EffectivenessMargin = cfg.Bind(adv, "04. Effectiveness margin", 1.15f,
            "Values above one make allocation more conservative.");
        StickRateLimit = cfg.Bind(adv, "05. Stick rate limit (1/s)", 1f,
            "Max stick movement per second.");
        YawAuthority = cfg.Bind(adv, "06. Yaw authority", 1f, "Maximum yaw input.");
        InputDelayTicks = cfg.Bind(adv, "07. Input delay (physics ticks)", 2,
            "Delay used to synchronize applied-input and sensor feedback.");
        CompensationGain = cfg.Bind(adv, "08. INDI compensation gain", 1f,
            "0..1. Strength of measured low-frequency inversion-error correction.");
        PitchLag = cfg.Bind(adv, "09. Pitch response lag (s)", 0.12f,
            "Assumed pitch stick-to-rate response time.");
        RollLag = cfg.Bind(adv, "10. Roll response lag (s)", 0.12f,
            "Assumed roll stick-to-rate response time.");
        YawLag = cfg.Bind(adv, "11. Yaw response lag (s)", 0.2f,
            "Assumed yaw stick-to-rate response time.");
        PitchAuthority = cfg.Bind(adv, "12. Pitch authority", 1f, "Maximum absolute pitch input.");
        RollAuthority = cfg.Bind(adv, "13. Roll authority", 1f, "Maximum absolute roll input.");
        LoadFactorRateLimit = cfg.Bind(adv, "14. Load-factor command rate (g/s)", 4f,
            "Rate limit used between vertical guidance and the pitch-rate loop.");
        EnergyFeedForward = cfg.Bind(adv, "15. Climb power feedforward", 0.8f,
            "0..1. Anticipates the energy required by commanded climbs; does not affect pitch allocation.");

        const string capture = "INDI - Altitude capture";
        AltitudeCaptureLead = cfg.Bind(capture, "01. Capture look-ahead (s)", 1.2f,
            "Projects current vertical speed forward. Increase to reduce altitude overshoot.");
        VerticalSpeedIntegralGain = cfg.Bind(capture, "02. Vertical-speed integral gain", 0.1f,
            "Removes persistent vertical-speed error. Excessive values increase overshoot.");
        VerticalSpeedIntegralLimit = cfg.Bind(capture, "03. Vertical-speed integral limit (m/s^2)", 1.5f,
            "Maximum acceleration correction retained by the vertical-speed integrator.");

        ShowWaypointAlts = cfg.Bind("Waypoints", "01. Show waypoint altitudes", false,
            "Draw each waypoint's altitude under its map node.");
    }

    public static void ApplyTo(ControllerSettings s)
    {
        if (ControllerType == null)
        {
            return;
        }

        s.ManeuverMaxG = ManeuverMaxG.Value;
        s.ManeuverMinG = ManeuverMinG.Value;
        s.BankGain = BankGain.Value;
        s.LoadFactorGain = LoadFactorGain.Value;
        s.AltitudeGain = AltitudeGain.Value;
        s.VerticalSpeedGain = VerticalSpeedGain.Value;
        s.CourseGain = CourseGain.Value;
        s.SpeedGain = SpeedGain.Value;
        s.SideslipGain = SideslipGain.Value;
        s.MimoRateControl = MimoEnabled.Value;
        s.OnlineEstimation = OnlineIdentification.Value;
        s.CompensationCutoff = FilterCutoff.Value;
        s.RateEffectivenessMargin = EffectivenessMargin.Value;
        s.StickRateLimit = StickRateLimit.Value;
        s.YawAuthority = Mathf.Clamp01(YawAuthority.Value);
        s.InputDelayTicks = Mathf.Max(InputDelayTicks.Value, 0);
        s.CompensationGain = Mathf.Clamp01(CompensationGain.Value);
        s.PitchLag = Mathf.Max(PitchLag.Value, 0.01f);
        s.RollLag = Mathf.Max(RollLag.Value, 0.01f);
        s.YawLag = Mathf.Max(YawLag.Value, 0.01f);
        s.PitchAuthority = Mathf.Clamp01(PitchAuthority.Value);
        s.RollAuthority = Mathf.Clamp01(RollAuthority.Value);
        s.LoadFactorRateLimit = Mathf.Max(LoadFactorRateLimit.Value, 0.1f);
        s.EnergyFeedForward = Mathf.Clamp01(EnergyFeedForward.Value);
        s.AltitudeCaptureLead = Mathf.Max(AltitudeCaptureLead.Value, 0f);
        s.VerticalSpeedIntegralGain = Mathf.Max(VerticalSpeedIntegralGain.Value, 0f);
        s.VerticalSpeedIntegralLimit = Mathf.Max(VerticalSpeedIntegralLimit.Value, 0f);
        if (Plugin.ThrottleMinLimit != null)
        {
            s.ThrottleMin = Plugin.ThrottleMinLimit.Value;
            s.ThrottleMax = Plugin.ThrottleMaxLimit.Value;
        }
    }
}
