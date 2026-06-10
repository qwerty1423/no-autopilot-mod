using NOAutopilot.Core.Config;

namespace NOAutopilot.Core.PID;

public static class ActivePid
{
    // active values
    public static PIDTuning Alt, Vs, Pitch, Roll, RollRate, Crs, Spd, Gcas;
    public static GainSchedule SchedPitch, SchedRollRate, SchedVs, SchedSpd;

    // global default
    private static string s_gAlt, s_gVs, s_gPitch, s_gRoll, s_gRollRate, s_gCrs, s_gSpd, s_gGcas;
    private static string s_gSchedPitch, s_gSchedRollRate, s_gSchedVs, s_gSchedSpd;

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
        s_gCrs = Plugin.ConfPidCrs.Value.ToString();
        s_gSpd = Plugin.ConfPidSpd.Value.ToString();
        s_gGcas = Plugin.ConfPidGcas.Value.ToString();

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
            Crs = PIDTuning.Parse(s_gCrs);
            Spd = PIDTuning.Parse(s_gSpd);
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
        if (!string.IsNullOrEmpty(profile.Crs)) { Crs = PIDTuning.Parse(profile.Crs); }
        if (!string.IsNullOrEmpty(profile.Spd)) { Spd = PIDTuning.Parse(profile.Spd); }
        if (!string.IsNullOrEmpty(profile.Gcas)) { Gcas = PIDTuning.Parse(profile.Gcas); }

        if (!string.IsNullOrEmpty(profile.SchedPitch)) { SchedPitch = GainSchedule.Parse(profile.SchedPitch); }
        if (!string.IsNullOrEmpty(profile.SchedRollRate)) { SchedRollRate = GainSchedule.Parse(profile.SchedRollRate); }
        if (!string.IsNullOrEmpty(profile.SchedVs)) { SchedVs = GainSchedule.Parse(profile.SchedVs); }
        if (!string.IsNullOrEmpty(profile.SchedSpd)) { SchedSpd = GainSchedule.Parse(profile.SchedSpd); }
    }

    public static void SyncToConfig()
    {
        if (Plugin.Instance?.Config == null)
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
            Plugin.ConfPidCrs.Value = Crs;
            Plugin.ConfPidSpd.Value = Spd;
            Plugin.ConfPidGcas.Value = Gcas;

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
        Crs = Plugin.ConfPidCrs.Value;
        Spd = Plugin.ConfPidSpd.Value;
        Gcas = Plugin.ConfPidGcas.Value;

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
            Plugin.ConfPidCrs.Value.ToString() == s_gCrs &&
            Plugin.ConfPidSpd.Value.ToString() == s_gSpd &&
            Plugin.ConfPidGcas.Value.ToString() == s_gGcas &&
            Plugin.SchedPidPitch.Value.ToString() == s_gSchedPitch &&
            Plugin.SchedPidRollRate.Value.ToString() == s_gSchedRollRate &&
            Plugin.SchedPidVs.Value.ToString() == s_gSchedVs &&
            Plugin.SchedPidSpd.Value.ToString() == s_gSchedSpd;
    }

    public static void WriteGlobalDefaultsToConfig()
    {
        if (Plugin.Instance?.Config == null)
        {
            return;
        }

        bool prev = Plugin.Instance.Config.SaveOnConfigSet;
        Plugin.Instance.Config.SaveOnConfigSet = false;

        try
        {
            Plugin.ConfPidAlt.Value = PIDTuning.Parse(s_gAlt);
            Plugin.ConfPidVs.Value = PIDTuning.Parse(s_gVs);
            Plugin.ConfPidPitch.Value = PIDTuning.Parse(s_gPitch);
            Plugin.ConfPidRoll.Value = PIDTuning.Parse(s_gRoll);
            Plugin.ConfPidRollRate.Value = PIDTuning.Parse(s_gRollRate);
            Plugin.ConfPidCrs.Value = PIDTuning.Parse(s_gCrs);
            Plugin.ConfPidSpd.Value = PIDTuning.Parse(s_gSpd);
            Plugin.ConfPidGcas.Value = PIDTuning.Parse(s_gGcas);

            Plugin.SchedPidPitch.Value = GainSchedule.Parse(s_gSchedPitch);
            Plugin.SchedPidRollRate.Value = GainSchedule.Parse(s_gSchedRollRate);
            Plugin.SchedPidVs.Value = GainSchedule.Parse(s_gSchedVs);
            Plugin.SchedPidSpd.Value = GainSchedule.Parse(s_gSchedSpd);
        }
        finally
        {
            Plugin.Instance.Config.SaveOnConfigSet = prev;
        }
    }
}
