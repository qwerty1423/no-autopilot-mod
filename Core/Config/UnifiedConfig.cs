using BepInEx.Configuration;

using NOAutopilot.Core.Control;
using NOAutopilot.Core.Flight;

using UnityEngine;

namespace NOAutopilot.Core.Config;

public static class UnifiedConfig
{
    public static ConfigEntry<FlightControllerType> ControllerType;
    public static ConfigEntry<float> ManeuverMaxG, ManeuverMinG, MaxRollRateDeg, AltitudeGain, VerticalSpeedGain, CourseGain, SkidAssist;
    public static ConfigEntry<bool> HoverEnabled;
    public static ConfigEntry<float> HoverSpeed;
    public static ConfigEntry<bool> StallAssist;
    public static ConfigEntry<float> SpeedGain, SpeedAccel, BankGain, LoadFactorGain, VerticalAccelG;
    public static ConfigEntry<bool> YawCoordination, SpeedPriority, OnlineIdentification, UnifiedHelicopters;

    // advanced
    public static ConfigEntry<float> PitchLag, RollLag, YawLag, CompensationCutoff, CompensationGain;
    public static ConfigEntry<float> EffectivenessMargin, StickRateLimit, YawAuthority, SideslipGain, AlphaLimitDeg;
    public static ConfigEntry<float> EnergyFeedForward, EngineSpoolRate, VerticalAccelShape;
    public static ConfigEntry<int> InputDelayTicks;

    // waypoints
    public static ConfigEntry<bool> ShowWaypointAlts;

    public static void Bind(ConfigFile cfg)
    {
        const string main = "INDI";
        ControllerType = cfg.Bind(main, "01. Controller", FlightControllerType.Indi,
            "The INDI is usually better. It is better if you are missing wings, or if you are flying some modded or strange aircraft like the tarantula. I feel like I am going to have to add per aircraft config for INDI at some point.");
        ManeuverMaxG = cfg.Bind(main, "02. Autopilot max G", 5f,
            "g limit");
        ManeuverMinG = cfg.Bind(main, "02b. Autopilot min G", -1.5f,
            "negative g limit");
        MaxRollRateDeg = cfg.Bind(main, "03. Max roll rate (deg/s)", 180f,
            "Roll rate used for bank changes (also capped by Limits > Max Roll Rate).");
        BankGain = cfg.Bind(main, "04. Bank gain (1/s)", 2.2f, "Bank angle -> roll rate gain.");
        LoadFactorGain = cfg.Bind(main, "05. Load factor gain (1/s)", 2.5f, "Load factor loop gain.");
        AltitudeGain = cfg.Bind(main, "06. Altitude gain (1/s)", 0.3f, "Altitude error -> vertical speed.");
        VerticalSpeedGain = cfg.Bind(main, "07. Vertical speed gain (1/s)", 1.0f, "Vertical speed error -> vertical acceleration.");
        CourseGain = cfg.Bind(main, "08. Course gain (1/s)", 0.6f, "Course error -> turn rate.");
        SpeedGain = cfg.Bind(main, "09. Speed gain (1/s)", 0.4f, "Speed error -> acceleration.");
        SpeedAccel = cfg.Bind(main, "10. Speed change acceleration (m/s^2)", 3f, "Acceleration used to shape speed changes.");
        YawCoordination = cfg.Bind(main, "11. Yaw coordination", true, "Use the rudder to keep turns coordinated?");
        SkidAssist = cfg.Bind(main, "11b. Rudder turn assist", 0.4f,
            "whatever that is");
        SpeedPriority = cfg.Bind(main, "12. Speed priority when throttle saturates", false,
            "when the throttle is at its limit, climbs and descents only use spare energy so the speed is held.");
        OnlineIdentification = cfg.Bind(main, "13. Online identification", true,
            "Identify the stick -> rate response of every axis in flight, also while you fly manually.");
        VerticalAccelG = cfg.Bind(main, "15. Climb/descent entry acceleration (g)", 1f,
            "probably something to do with gs and altitude changes");
        HoverEnabled = cfg.Bind(main, "13b. Hover / vertical flight", false,
            "for... hovering?");
        HoverSpeed = cfg.Bind(main, "13d. Hover regime boundary (m/s)", 35f,
            "example of why we need per aircraft config even with INDI???? AAAH");
        StallAssist = cfg.Bind(main, "13c. Stall assist", false,
            "anti stall????");
        UnifiedHelicopters = cfg.Bind(main, "14. Use INDI for helicopters", false,
            "broken");

        const string adv = "INDI - Advanced";
        PitchLag = cfg.Bind(adv, "01. Pitch response lag prior (s)", 0.12f, "Starting value before identification.");
        RollLag = cfg.Bind(adv, "02. Roll response lag prior (s)", 0.12f, "Starting value before identification.");
        YawLag = cfg.Bind(adv, "03. Yaw response lag prior (s)", 0.2f, "Starting value before identification.");
        CompensationCutoff = cfg.Bind(adv, "04. Inversion error compensation cut-off (rad/s)", 1.2f,
            "Hybrid INDI compensation filter. Lower = more robust, higher = faster disturbance rejection.");
        CompensationGain = cfg.Bind(adv, "05. Inversion error compensation gain", 1f, "1 = full integral action.");
        EffectivenessMargin = cfg.Bind(adv, "06. Effectiveness safety margin", 1.3f, "> 1 is more conservative.");
        StickRateLimit = cfg.Bind(adv, "07. Stick rate limit (1/s)", 1.0f, "Max stick movement per second.");
        YawAuthority = cfg.Bind(adv, "08. Yaw authority", 1.0f, "Max rudder the controller may use.");
        SideslipGain = cfg.Bind(adv, "09. Sideslip gain (1/s)", 1.2f, "Sideslip -> yaw rate.");
        AlphaLimitDeg = cfg.Bind(adv, "10. Angle of attack limit (deg)", 23f, "The controller stops pulling above this.");
        EnergyFeedForward = cfg.Bind(adv, "11. Climb power feed forward", 0.8f, "0..1, add power before the speed decays in climbs.");
        EngineSpoolRate = cfg.Bind(adv, "12. Engine spool rate (throttle/s)", 0.5f, "Used to synchronize the throttle loop.");
        VerticalAccelShape = cfg.Bind(adv, "13. Altitude capture acceleration (m/s^2)", 4f, "Smoothness of level-offs.");
        InputDelayTicks = cfg.Bind(adv, "14. Input delay (physics ticks)", 2, "Ticks between writing the stick and seeing the rate change.");

        ShowWaypointAlts = cfg.Bind("Waypoints", "01. Show waypoint altitudes", false,
            "Draw each waypoint's altitude under its node on the map.");

    }

    public static void ApplyTo(ControllerSettings s)
    {
        if (ControllerType == null)
        {
            return;
        }

        s.ManeuverMaxG = ManeuverMaxG.Value;
        s.ManeuverMinG = ManeuverMinG.Value;
        s.SkidAssist = SkidAssist.Value;
        s.HoverEnabled = HoverEnabled.Value;
        s.HoverSpeed = HoverSpeed.Value;
        s.StallAssist = StallAssist.Value;
        s.MaxRollRate = MaxRollRateDeg.Value * Mathf.Deg2Rad;
        s.MaxRollAccel = 2f * s.MaxRollRate;
        s.BankGain = BankGain.Value;
        s.LoadFactorGain = LoadFactorGain.Value;
        s.AltitudeGain = AltitudeGain.Value;
        s.VerticalSpeedGain = VerticalSpeedGain.Value;
        s.CourseGain = CourseGain.Value;
        s.SpeedGain = SpeedGain.Value;
        s.SpeedAccelShape = SpeedAccel.Value;
        s.YawCoordination = YawCoordination.Value;
        s.OnlineEstimation = OnlineIdentification.Value;
        s.ResponseIdentification = OnlineIdentification.Value;
        s.PitchLag = PitchLag.Value;
        s.RollLag = RollLag.Value;
        s.YawLag = YawLag.Value;
        s.CompensationCutoff = CompensationCutoff.Value;
        s.CompensationGain = CompensationGain.Value;
        s.RateEffectivenessMargin = EffectivenessMargin.Value;
        s.StickRateLimit = StickRateLimit.Value;
        s.YawAuthority = YawAuthority.Value;
        s.SideslipGain = SideslipGain.Value;
        s.AlphaLimit = AlphaLimitDeg.Value * Mathf.Deg2Rad;
        s.EnergyFeedForward = EnergyFeedForward.Value;
        s.EngineSpoolRate = EngineSpoolRate.Value;
        s.VerticalAccelShape = VerticalAccelShape.Value;
        s.VerticalAccelDefault = VerticalAccelG.Value * ControlMath.G;
        s.InputDelayTicks = InputDelayTicks.Value;
        if (Plugin.ThrottleMinLimit != null)
        {
            s.ThrottleMin = Plugin.ThrottleMinLimit.Value;
            s.ThrottleMax = Plugin.ThrottleMaxLimit.Value;
        }

        if (Plugin.Conf_VS_MaxAngle != null)
        {
            s.MaxFlightPathAngle = Plugin.Conf_VS_MaxAngle.Value * Mathf.Deg2Rad;
        }

    }
}
