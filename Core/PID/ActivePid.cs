using System.Globalization;

using NOAutopilot.Core.Config;

using UnityEngine;

namespace NOAutopilot.Core.PID;

public static class ActivePid
{
    // active values
    public static PIDTuning Alt, Vs, Pitch, Roll, RollRate, Yaw, Trk, Spd, LvlChg, Gcas;
    public static GainSchedule SchedPitch, SchedRollRate, SchedVs, SchedSpd;

    public static float LvlChgCaptureDecelG;
    public static float LvlChgHandoffDistance;
    public static float LvlChgHandoffVS;

    // global default
    private static string s_gAlt, s_gVs, s_gPitch, s_gRoll, s_gRollRate, s_gYaw, s_gTrk, s_gSpd, s_gLvlChg, s_gGcas;
    private static string s_gSchedPitch, s_gSchedRollRate, s_gSchedVs, s_gSchedSpd;
    private static float s_gLvlChgCaptureDecelG, s_gLvlChgHandoffDistance, s_gLvlChgHandoffVS;

    public static string CurrentAircraftId { get; private set; } = "";
    public static bool IsUsingOverride { get; private set; }

    /// <summary>
    /// Capture the state of the BepInEx config entries as they were at startup.
    /// </summary>
    public static void CacheGlobalDefaults()
    {
        s_gAlt = Plugin.ConfPidAlt.Value.ToString();
        s_gVs = Plugin.ConfPidVs.Value.ToString();
        s_gPitch = Plugin.ConfPidPitch.Value.ToString();
        s_gRoll = Plugin.ConfPidRoll.Value.ToString();
        s_gRollRate = Plugin.ConfPidRollRate.Value.ToString();
        s_gYaw = Plugin.ConfPidYaw.Value.ToString();
        s_gTrk = Plugin.ConfPidTrk.Value.ToString();
        s_gSpd = Plugin.ConfPidSpd.Value.ToString();
        s_gLvlChg = Plugin.ConfPidLvlChg.Value.ToString();
        s_gGcas = Plugin.ConfPidGcas.Value.ToString();

        s_gLvlChgCaptureDecelG = Plugin.ConfLvlChgCaptureDecelG.Value;
        s_gLvlChgHandoffDistance = Plugin.ConfLvlChgHandoffDistance.Value;
        s_gLvlChgHandoffVS = Plugin.ConfLvlChgHandoffVS.Value;

        s_gSchedPitch = Plugin.SchedPidPitch.Value.ToString();
        s_gSchedRollRate = Plugin.SchedPidRollRate.Value.ToString();
        s_gSchedVs = Plugin.SchedPidVs.Value.ToString();
        s_gSchedSpd = Plugin.SchedPidSpd.Value.ToString();
    }

    /// <summary>
    /// Reset active values to the global defaults.
    /// </summary>
    public static void LoadGlobalDefaults()
    {
        LvlChgCaptureDecelG = s_gLvlChgCaptureDecelG;
        LvlChgHandoffDistance = s_gLvlChgHandoffDistance;
        LvlChgHandoffVS = s_gLvlChgHandoffVS;

        PidProfile defaults = DefaultProfiles.Load("global_defaults");
        if (defaults != null)
        {
            ApplyProfile(defaults);
        }
        else
        {
            Alt = PIDTuning.Parse(s_gAlt);
            Vs = PIDTuning.Parse(s_gVs);
            Pitch = PIDTuning.Parse(s_gPitch);
            Roll = PIDTuning.Parse(s_gRoll);
            RollRate = PIDTuning.Parse(s_gRollRate);
            Yaw = PIDTuning.Parse(s_gYaw);
            Trk = PIDTuning.Parse(s_gTrk);
            Spd = PIDTuning.Parse(s_gSpd);
            LvlChg = PIDTuning.Parse(s_gLvlChg);
            Gcas = PIDTuning.Parse(s_gGcas);

            SchedPitch = GainSchedule.Parse(s_gSchedPitch);
            SchedRollRate = GainSchedule.Parse(s_gSchedRollRate);
            SchedVs = GainSchedule.Parse(s_gSchedVs);
            SchedSpd = GainSchedule.Parse(s_gSchedSpd);
        }
        IsUsingOverride = false;
    }

    public static void ApplyForAircraft(string id)
    {
        CurrentAircraftId = id ?? "";
        LoadGlobalDefaults();

        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        PidProfile shipped = DefaultProfiles.Load(id);
        PidProfile user = PidProfileManager.LoadUserProfile(id);

        if (shipped != null)
        {
            ApplyProfile(shipped);
        }

        if (user != null)
        {
            ApplyProfile(user);
        }

        IsUsingOverride = shipped != null || user != null;
    }

    public static void ApplyProfile(PidProfile profile)
    {
        if (profile == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(profile.Alt)) { Alt = PIDTuning.Parse(profile.Alt); }
        if (!string.IsNullOrEmpty(profile.Vs)) { Vs = PIDTuning.Parse(profile.Vs); }
        if (!string.IsNullOrEmpty(profile.Pitch)) { Pitch = PIDTuning.Parse(profile.Pitch); }
        if (!string.IsNullOrEmpty(profile.Roll)) { Roll = PIDTuning.Parse(profile.Roll); }
        if (!string.IsNullOrEmpty(profile.RollRate)) { RollRate = PIDTuning.Parse(profile.RollRate); }
        if (!string.IsNullOrEmpty(profile.Yaw)) { Yaw = PIDTuning.Parse(profile.Yaw); }
        if (!string.IsNullOrEmpty(profile.Trk)) { Trk = PIDTuning.Parse(profile.Trk); }
        if (!string.IsNullOrEmpty(profile.Spd)) { Spd = PIDTuning.Parse(profile.Spd); }
        if (!string.IsNullOrEmpty(profile.LvlChg)) { LvlChg = PIDTuning.Parse(profile.LvlChg); }
        LvlChgCaptureDecelG = ParseCaptureFloat(profile.LvlChgCaptureDecelG, LvlChgCaptureDecelG, 0.01f, 1f);
        LvlChgHandoffDistance = ParseCaptureFloat(profile.LvlChgHandoffDistance, LvlChgHandoffDistance, 0.1f, 100f);
        LvlChgHandoffVS = ParseCaptureFloat(profile.LvlChgHandoffVS, LvlChgHandoffVS, 0.05f, 10f);
        if (!string.IsNullOrEmpty(profile.Gcas)) { Gcas = PIDTuning.Parse(profile.Gcas); }

        if (!string.IsNullOrEmpty(profile.SchedPitch)) { SchedPitch = GainSchedule.Parse(profile.SchedPitch); }
        if (!string.IsNullOrEmpty(profile.SchedRollRate)) { SchedRollRate = GainSchedule.Parse(profile.SchedRollRate); }
        if (!string.IsNullOrEmpty(profile.SchedVs)) { SchedVs = GainSchedule.Parse(profile.SchedVs); }
        if (!string.IsNullOrEmpty(profile.SchedSpd)) { SchedSpd = GainSchedule.Parse(profile.SchedSpd); }
    }

    private static float ParseCaptureFloat(string text, float fallback, float min, float max)
    {
        return string.IsNullOrEmpty(text) ||
            !float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? fallback
            : Mathf.Clamp(value, min, max);
    }

    public static void SyncToConfig()
    {
        if (Plugin.Instance == null || Plugin.Instance.Config == null)
        {
            return;
        }

        bool prev = Plugin.Instance.Config.SaveOnConfigSet;
        Plugin.Instance.Config.SaveOnConfigSet = false;

        try
        {
            Plugin.ConfPidAlt.Value = Alt;
            Plugin.ConfPidVs.Value = Vs;
            Plugin.ConfPidPitch.Value = Pitch;
            Plugin.ConfPidRoll.Value = Roll;
            Plugin.ConfPidRollRate.Value = RollRate;
            Plugin.ConfPidYaw.Value = Yaw;
            Plugin.ConfPidTrk.Value = Trk;
            Plugin.ConfPidSpd.Value = Spd;
            Plugin.ConfPidLvlChg.Value = LvlChg;
            Plugin.ConfPidGcas.Value = Gcas;
            Plugin.ConfLvlChgCaptureDecelG.Value = LvlChgCaptureDecelG;
            Plugin.ConfLvlChgHandoffDistance.Value = LvlChgHandoffDistance;
            Plugin.ConfLvlChgHandoffVS.Value = LvlChgHandoffVS;

            Plugin.SchedPidPitch.Value = SchedPitch;
            Plugin.SchedPidRollRate.Value = SchedRollRate;
            Plugin.SchedPidVs.Value = SchedVs;
            Plugin.SchedPidSpd.Value = SchedSpd;
        }
        finally
        {
            Plugin.Instance.Config.SaveOnConfigSet = prev;
        }
    }

    public static void SyncFromConfig()
    {
        Alt = Plugin.ConfPidAlt.Value;
        Vs = Plugin.ConfPidVs.Value;
        Pitch = Plugin.ConfPidPitch.Value;
        Roll = Plugin.ConfPidRoll.Value;
        RollRate = Plugin.ConfPidRollRate.Value;
        Yaw = Plugin.ConfPidYaw.Value;
        Trk = Plugin.ConfPidTrk.Value;
        Spd = Plugin.ConfPidSpd.Value;
        LvlChg = Plugin.ConfPidLvlChg.Value;
        Gcas = Plugin.ConfPidGcas.Value;
        LvlChgCaptureDecelG = Plugin.ConfLvlChgCaptureDecelG.Value;
        LvlChgHandoffDistance = Plugin.ConfLvlChgHandoffDistance.Value;
        LvlChgHandoffVS = Plugin.ConfLvlChgHandoffVS.Value;

        SchedPitch = Plugin.SchedPidPitch.Value;
        SchedRollRate = Plugin.SchedPidRollRate.Value;
        SchedVs = Plugin.SchedPidVs.Value;
        SchedSpd = Plugin.SchedPidSpd.Value;
    }

    public static bool ConfigEntriesMatchGlobalDefaults()
    {
        return
            Plugin.ConfPidAlt.Value.ToString() == s_gAlt &&
            Plugin.ConfPidVs.Value.ToString() == s_gVs &&
            Plugin.ConfPidPitch.Value.ToString() == s_gPitch &&
            Plugin.ConfPidRoll.Value.ToString() == s_gRoll &&
            Plugin.ConfPidRollRate.Value.ToString() == s_gRollRate &&
            Plugin.ConfPidYaw.Value.ToString() == s_gYaw &&
            Plugin.ConfPidTrk.Value.ToString() == s_gTrk &&
            Plugin.ConfPidSpd.Value.ToString() == s_gSpd &&
            Plugin.ConfPidLvlChg.Value.ToString() == s_gLvlChg &&
            Plugin.ConfPidGcas.Value.ToString() == s_gGcas &&
            Mathf.Approximately(Plugin.ConfLvlChgCaptureDecelG.Value, s_gLvlChgCaptureDecelG) &&
            Mathf.Approximately(Plugin.ConfLvlChgHandoffDistance.Value, s_gLvlChgHandoffDistance) &&
            Mathf.Approximately(Plugin.ConfLvlChgHandoffVS.Value, s_gLvlChgHandoffVS) &&
            Plugin.SchedPidPitch.Value.ToString() == s_gSchedPitch &&
            Plugin.SchedPidRollRate.Value.ToString() == s_gSchedRollRate &&
            Plugin.SchedPidVs.Value.ToString() == s_gSchedVs &&
            Plugin.SchedPidSpd.Value.ToString() == s_gSchedSpd;
    }

    public static void WriteGlobalDefaultsToConfig()
    {
        if (Plugin.Instance == null || Plugin.Instance.Config == null)
        {
            return;
        }

        bool prev = Plugin.Instance.Config.SaveOnConfigSet;
        Plugin.Instance.Config.SaveOnConfigSet = false;

        try
        {
            Plugin.ConfPidAlt.Value = Alt;
            Plugin.ConfPidVs.Value = Vs;
            Plugin.ConfPidPitch.Value = Pitch;
            Plugin.ConfPidRoll.Value = Roll;
            Plugin.ConfPidRollRate.Value = RollRate;
            Plugin.ConfPidYaw.Value = Yaw;
            Plugin.ConfPidTrk.Value = Trk;
            Plugin.ConfPidSpd.Value = Spd;
            Plugin.ConfPidLvlChg.Value = LvlChg;
            Plugin.ConfPidGcas.Value = Gcas;
            Plugin.ConfLvlChgCaptureDecelG.Value = LvlChgCaptureDecelG;
            Plugin.ConfLvlChgHandoffDistance.Value = LvlChgHandoffDistance;
            Plugin.ConfLvlChgHandoffVS.Value = LvlChgHandoffVS;

            Plugin.SchedPidPitch.Value = SchedPitch;
            Plugin.SchedPidRollRate.Value = SchedRollRate;
            Plugin.SchedPidVs.Value = SchedVs;
            Plugin.SchedPidSpd.Value = SchedSpd;
        }
        finally
        {
            Plugin.Instance.Config.SaveOnConfigSet = prev;
        }
    }
}
