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
    public static ConfigEntry<bool> SpeedPriority, OnlineIdentification, MimoEnabled;
    public static ConfigEntry<float> EffectivenessMargin, StickRateLimit, YawAuthority, FilterCutoff;
    public static ConfigEntry<int> InputDelayTicks;
    public static ConfigEntry<bool> ShowWaypointAlts;

    public static void Bind(ConfigFile cfg)
    {
        const string main = "INDI";
        ControllerType = cfg.Bind(main, "01. Controller", FlightControllerType.Indi,
            "Incremental Nonlinear Dynamic Inversion, and Proportional Integral Derivative, something like that.");
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
        EffectivenessMargin = cfg.Bind(adv, "04. Effectiveness margin", 1.3f,
            "Values above one make allocation more conservative.");
        StickRateLimit = cfg.Bind(adv, "05. Stick rate limit (1/s)", 1f,
            "Max stick movement per second.");
        YawAuthority = cfg.Bind(adv, "06. Yaw authority", 1f, "Maximum yaw input.");
        InputDelayTicks = cfg.Bind(adv, "07. Input delay (physics ticks)", 2,
            "Delay used to synchronize applied-input and sensor feedback.");

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
        s.YawAuthority = YawAuthority.Value;
        s.InputDelayTicks = InputDelayTicks.Value;
        if (Plugin.ThrottleMinLimit != null)
        {
            s.ThrottleMin = Plugin.ThrottleMinLimit.Value;
            s.ThrottleMax = Plugin.ThrottleMaxLimit.Value;
        }
    }
}
