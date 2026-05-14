using NOAutopilot.Core.Config;

namespace NOAutopilot.Core.PID;

public static class ActivePid
{
    public static PIDTuning Alt, Vs, Pitch, Roll, RollRate, Crs, Spd, Gcas;
    public static GainSchedule SchedPitch, SchedRollRate, SchedVs, SchedSpd;

    public static string CurrentAircraftId { get; private set; } = "";
    public static bool HasOverride { get; private set; }

    public static void LoadDefaults()
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
        HasOverride = false;
    }

    public static void ApplyForAircraft(string id)
    {
        LoadDefaults();
        CurrentAircraftId = id ?? "";
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        PidProfile profile = PidProfileManager.LoadProfile(id);
        if (profile == null)
        {
            return;
        }

        HasOverride = true;
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
}
