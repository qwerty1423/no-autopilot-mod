using System.IO;
using System.Text.RegularExpressions;

using NOAutopilot.Core.Config;

using UnityEngine;

namespace NOAutopilot.Core.PID;

public static class PidProfileManager
{
    private static readonly string Dir = Path.Combine(BepInEx.Paths.ConfigPath, "NOAutopilot", "Profiles");
    private static PidProfile s_snapshot;
    public static bool IsTuning { get; private set; }

    public static string GetId(Aircraft a)
    {
        if (a == null)
        {
            return "";
        }

        string key = ((IHasJsonKey)a.definition).JsonKey;
        return Regex.Replace(key ?? a.name, @"\(Clone\)$", "", RegexOptions.IgnoreCase).Trim().ToLowerInvariant();
    }

    public static PidProfile LoadProfile(string id)
    {
        string path = Path.Combine(Dir, $"{id}.json");
        return !File.Exists(path) ? null : JsonUtility.FromJson<PidProfile>(File.ReadAllText(path));
    }

    public static void BeginTuning()
    {
        s_snapshot = CaptureConfig();
        IsTuning = true;
    }

    public static void SaveAndRestore(string id)
    {
        var tuned = CaptureConfig();
        var profile = new PidProfile();
        bool diff = false;

        // Diff against snapshot
        if (tuned.Alt != s_snapshot.Alt) { profile.Alt = tuned.Alt; diff = true; }
        if (tuned.Vs != s_snapshot.Vs) { profile.Vs = tuned.Vs; diff = true; }
        if (tuned.Pitch != s_snapshot.Pitch) { profile.Pitch = tuned.Pitch; diff = true; }
        if (tuned.Roll != s_snapshot.Roll) { profile.Roll = tuned.Roll; diff = true; }
        if (tuned.RollRate != s_snapshot.RollRate) { profile.RollRate = tuned.RollRate; diff = true; }
        if (tuned.Crs != s_snapshot.Crs) { profile.Crs = tuned.Crs; diff = true; }
        if (tuned.Spd != s_snapshot.Spd) { profile.Spd = tuned.Spd; diff = true; }
        if (tuned.Gcas != s_snapshot.Gcas) { profile.Gcas = tuned.Gcas; diff = true; }

        if (diff)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Path.Combine(Dir, $"{id}.json"), JsonUtility.ToJson(profile, true));
        }

        Restore();
        ActivePid.ApplyForAircraft(id);
    }

    public static void Restore()
    {
        if (s_snapshot == null)
        {
            return;
        }

        Plugin.ConfPidAlt.Value = PIDTuning.Parse(s_snapshot.Alt);
        Plugin.ConfPidVs.Value = PIDTuning.Parse(s_snapshot.Vs);
        Plugin.ConfPidPitch.Value = PIDTuning.Parse(s_snapshot.Pitch);
        Plugin.ConfPidRoll.Value = PIDTuning.Parse(s_snapshot.Roll);
        Plugin.ConfPidRollRate.Value = PIDTuning.Parse(s_snapshot.RollRate);
        Plugin.ConfPidCrs.Value = PIDTuning.Parse(s_snapshot.Crs);
        Plugin.ConfPidSpd.Value = PIDTuning.Parse(s_snapshot.Spd);
        Plugin.ConfPidGcas.Value = PIDTuning.Parse(s_snapshot.Gcas);
        IsTuning = false;
    }

    private static PidProfile CaptureConfig() => new()
    {
        Alt = Plugin.ConfPidAlt.Value.ToString(),
        Vs = Plugin.ConfPidVs.Value.ToString(),
        Pitch = Plugin.ConfPidPitch.Value.ToString(),
        Roll = Plugin.ConfPidRoll.Value.ToString(),
        RollRate = Plugin.ConfPidRollRate.Value.ToString(),
        Crs = Plugin.ConfPidCrs.Value.ToString(),
        Spd = Plugin.ConfPidSpd.Value.ToString(),
        Gcas = Plugin.ConfPidGcas.Value.ToString()
    };
}
