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
    public static ConfigEntry<bool> SpeedPriority, OnlineIdentification, MimoEnabled, MimoCrossAxisNormal;
    public static ConfigEntry<float> EffectivenessMargin, StickRateLimit, YawAuthority, FilterCutoff;
    public static ConfigEntry<float> CompensationGain, PitchLag, RollLag, YawLag, PitchAuthority, RollAuthority;
    public static ConfigEntry<float> LoadFactorRateLimit, LoadFactorUnloadRateLimit, EnergyFeedForward;
    public static ConfigEntry<int> InputDelayTicks;
    public static ConfigEntry<bool> ShowWaypointAlts;

    public static void Bind(ConfigFile cfg)
    {
        const string main = "INDI (experimental)";
        ControllerType = cfg.Bind(main, "01. Controller", FlightControllerType.Pid,
            "INDI: Incremental Nonlinear Dynamic Inversion, PID: Proportional Integral Derivative, something like that. INDI is better for strange aircraft, damaged aircraft, and aircraft without pid profiles, but is quite unreliable currently.");
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
        FilterCutoff = cfg.Bind(adv, "03. Synchronized INDI filter cutoff (rad/s)", 50f,
            "Applied equally to measured motion and retained-input feedback.");
        EffectivenessMargin = cfg.Bind(adv, "04. Effectiveness margin", 1.15f,
            "Values above one make allocation more conservative.");
        StickRateLimit = cfg.Bind(adv, "05. Stick rate limit (1/s)", 1f,
            "Max stick movement per second.");
        YawAuthority = cfg.Bind(adv, "06. Yaw authority", 1f, "Maximum yaw input.");
        InputDelayTicks = cfg.Bind(adv, "07. Input delay (physics ticks)", 2,
            "Delay used to synchronize applied-input and sensor feedback.");
        MimoCrossAxisNormal = cfg.Bind(adv, "07b. Cross-axis allocation in normal flight", false,
            "Experimental. Off keeps nominal pitch/roll/yaw allocation diagonal; full MIMO activates automatically after rank loss or a suspected effectiveness fault.");
        CompensationGain = cfg.Bind(adv, "08. INDI compensation gain", 1f,
            "0..1. Strength of measured low-frequency inversion-error correction.");
        PitchLag = cfg.Bind(adv, "09. Pitch response lag (s)", 0.5f,
            "Assumed pitch stick-to-rate response time.");
        RollLag = cfg.Bind(adv, "10. Roll response lag (s)", 0.5f,
            "Assumed roll stick-to-rate response time.");
        YawLag = cfg.Bind(adv, "11. Yaw response lag (s)", 0.5f,
            "Assumed yaw stick-to-rate response time.");
        PitchAuthority = cfg.Bind(adv, "12. Pitch authority", 1f, "Maximum absolute pitch input.");
        RollAuthority = cfg.Bind(adv, "13. Roll authority", 1f, "Maximum absolute roll input.");
        LoadFactorRateLimit = cfg.Bind(adv, "14. Load-factor pull rate (g/s)", 12f,
            "Maximum increase of commanded load factor.");
        LoadFactorUnloadRateLimit = cfg.Bind(adv, "15. Load-factor unload rate (g/s)", 12f,
            "Maximum reduction of commanded load factor.");
        EnergyFeedForward = cfg.Bind(adv, "16. Climb power feedforward", 0.8f,
            "0..1. Anticipates the energy required by commanded climbs; does not affect pitch allocation.");

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
        s.MimoCrossAxisInNormalFlight = MimoCrossAxisNormal.Value;
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
        s.LoadFactorUnloadRateLimit = Mathf.Max(LoadFactorUnloadRateLimit.Value, s.LoadFactorRateLimit);
        s.EnergyFeedForward = Mathf.Clamp01(EnergyFeedForward.Value);
        if (Plugin.ThrottleMinLimit != null)
        {
            s.ThrottleMin = Plugin.ThrottleMinLimit.Value;
            s.ThrottleMax = Plugin.ThrottleMaxLimit.Value;
        }
    }
}
