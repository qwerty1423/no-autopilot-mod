using System.IO;
using System.Text.RegularExpressions;

using BepInEx.Configuration;

using NOAutopilot.Core.PID;

using UnityEngine;

namespace NOAutopilot.Core.Config;

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
        return !File.Exists(path)
            ? null
            : JsonUtility.FromJson<PidProfile>(File.ReadAllText(path));
    }

    public static void DeleteProfile(string id)
    {
        string path = Path.Combine(Dir, $"{id}.json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static void BeginTuning()
    {
        s_snapshot = CaptureConfig();
        IsTuning = true;
    }

    public static void SaveAndRestore(string id)
    {
        PidProfile tuned = CaptureConfig();
        PidProfile profile = new();
        bool diff = false;

        // Save only values that differ from defaults
        if (tuned.Alt != s_snapshot.Alt) { profile.Alt = tuned.Alt; diff = true; }
        if (tuned.Vs != s_snapshot.Vs) { profile.Vs = tuned.Vs; diff = true; }
        if (tuned.Pitch != s_snapshot.Pitch) { profile.Pitch = tuned.Pitch; diff = true; }
        if (tuned.Roll != s_snapshot.Roll) { profile.Roll = tuned.Roll; diff = true; }
        if (tuned.RollRate != s_snapshot.RollRate) { profile.RollRate = tuned.RollRate; diff = true; }
        if (tuned.Crs != s_snapshot.Crs) { profile.Crs = tuned.Crs; diff = true; }
        if (tuned.Spd != s_snapshot.Spd) { profile.Spd = tuned.Spd; diff = true; }
        if (tuned.Gcas != s_snapshot.Gcas) { profile.Gcas = tuned.Gcas; diff = true; }

        if (tuned.SchedPitch != s_snapshot.SchedPitch) { profile.SchedPitch = tuned.SchedPitch; diff = true; }
        if (tuned.SchedRollRate != s_snapshot.SchedRollRate) { profile.SchedRollRate = tuned.SchedRollRate; diff = true; }
        if (tuned.SchedVs != s_snapshot.SchedVs) { profile.SchedVs = tuned.SchedVs; diff = true; }
        if (tuned.SchedSpd != s_snapshot.SchedSpd) { profile.SchedSpd = tuned.SchedSpd; diff = true; }

        if (diff)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Path.Combine(Dir, $"{id}.json"), JsonUtility.ToJson(profile, true));
        }
        else
        {
            DeleteProfile(id); // If identical to defaults, erase override
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

        Plugin.SchedPidPitch.Value = GainSchedule.Parse(s_snapshot.SchedPitch);
        Plugin.SchedPidRollRate.Value = GainSchedule.Parse(s_snapshot.SchedRollRate);
        Plugin.SchedPidVs.Value = GainSchedule.Parse(s_snapshot.SchedVs);
        Plugin.SchedPidSpd.Value = GainSchedule.Parse(s_snapshot.SchedSpd);

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
        Gcas = Plugin.ConfPidGcas.Value.ToString(),
        SchedPitch = Plugin.SchedPidPitch.Value.ToString(),
        SchedRollRate = Plugin.SchedPidRollRate.Value.ToString(),
        SchedVs = Plugin.SchedPidVs.Value.ToString(),
        SchedSpd = Plugin.SchedPidSpd.Value.ToString()
    };

    public static void DrawConfigManagerControls(ConfigEntryBase entry)
    {
        string id = ActivePid.CurrentAircraftId;
        bool hasOverride = ActivePid.HasOverride;
        string displayId = string.IsNullOrEmpty(id) ? "None (Global Defaults Active)" : id;

        GUILayout.BeginVertical();
        GUILayout.Label($"<b>Target Aircraft:</b> {displayId}{(hasOverride ? " <color=#00FF00>[OVERRIDDEN]</color>" : "")}");

        GUILayout.BeginHorizontal();
        if (!IsTuning)
        {
            if (!string.IsNullOrEmpty(id))
            {
                if (GUILayout.Button("Tune Current Aircraft", GUILayout.ExpandWidth(true)))
                {
                    BeginTuning();
                }
                if (hasOverride && GUILayout.Button("Reset to Defaults", GUILayout.ExpandWidth(true)))
                {
                    DeleteProfile(id);
                    ActivePid.ApplyForAircraft(id);
                }
            }
            else
            {
                GUILayout.Label("<i>Enter an aircraft in-game to tune its specific profile.</i>");
            }
        }
        else
        {
            GUILayout.Label("<color=#FFFF00><b>Tuning Mode Active</b></color>", GUILayout.Width(130));
            if (GUILayout.Button("Save Overrides", GUILayout.ExpandWidth(true)))
            {
                SaveAndRestore(id);
            }
            if (GUILayout.Button("Cancel", GUILayout.ExpandWidth(true)))
            {
                Restore();
                ActivePid.ApplyForAircraft(id);
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }
}
