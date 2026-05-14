using System.IO;
using System.Text.RegularExpressions;

using BepInEx.Configuration;

using NOAutopilot.Core.PID;

using UnityEngine;

namespace NOAutopilot.Core.Config;

public static class PidProfileManager
{
    private static readonly string UserDir = Path.Combine(BepInEx.Paths.ConfigPath, "NOAutopilot", "Profiles");

    private static PidProfile s_snapshot;
    private static string s_tuningAircraftId;
    private static readonly Regex Regex = new(@"\(Clone\)$", RegexOptions.IgnoreCase);
    public static bool IsTuning { get; private set; }

    public static string GetId(Aircraft a)
    {
        if (a == null)
        {
            return "";
        }

        string key = ((IHasJsonKey)a.definition).JsonKey;
        return Regex.Replace(key ?? a.name, "").Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Loads user profile first, then shipped default, then null.
    /// </summary>
    public static PidProfile LoadProfile(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        // User override takes priority
        string userPath = Path.Combine(UserDir, $"{id}.json");
        if (File.Exists(userPath))
        {
            return JsonUtility.FromJson<PidProfile>(File.ReadAllText(userPath));
        }

        // Fall back to shipped default
        return DefaultProfiles.Load(id);
    }

    /// <summary>
    /// Whether the user has a custom saved profile.
    /// </summary>
    public static bool HasUserProfile(string id)
    {
        return !string.IsNullOrEmpty(id) && File.Exists(Path.Combine(UserDir, $"{id}.json"));
    }

    /// <summary>
    /// Whether there is any profile (user or shipped).
    /// </summary>
    public static bool HasAnyProfile(string id)
    {
        return HasUserProfile(id) || DefaultProfiles.Exists(id);
    }

    public static void DeleteUserProfile(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        string path = Path.Combine(UserDir, $"{id}.json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static void BeginTuning()
    {
        string id = ActivePid.CurrentAircraftId;
        if (string.IsNullOrEmpty(id) || IsTuning)
        {
            return;
        }

        s_snapshot = CaptureConfig();
        s_tuningAircraftId = id;
        IsTuning = true;
    }

    public static void SaveAndRestore(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        PidProfile profile = CaptureConfig();

        Directory.CreateDirectory(UserDir);
        File.WriteAllText(Path.Combine(UserDir, $"{id}.json"), JsonUtility.ToJson(profile, true));

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
        s_tuningAircraftId = null;
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

    public static void DrawConfigManagerControls(ConfigEntryBase _)
    {
        if (!IsTuning)
        {
            // Display active values
            ActivePid.SyncToConfig();
        }
        else
        {
            // Apply config immediately
            ActivePid.SyncFromConfig();
        }

        string currentId = ActivePid.CurrentAircraftId;
        bool hasUser = HasUserProfile(currentId);
        bool hasShipped = DefaultProfiles.Exists(currentId);
        string displayId = string.IsNullOrEmpty(currentId) ? "None" : currentId;

        GUIStyle richLabel = new(GUI.skin.label) { richText = true };

        GUILayout.BeginVertical();

        // Status line
        string status = "";
        if (!string.IsNullOrEmpty(currentId))
        {
            if (hasUser)
            {
                status = " <color=#00FF00>[User Profile]</color>";
            }
            else if (hasShipped)
            {
                status = " <color=#88CCFF>[Default Profile]</color>";
            }
        }
        GUILayout.Label($"<b>Current aircraft:</b> {displayId}{status}", richLabel);

        GUILayout.BeginHorizontal();

        if (!IsTuning)
        {
            if (!string.IsNullOrEmpty(currentId))
            {
                if (GUILayout.Button("Tune current aircraft", GUILayout.ExpandWidth(true)))
                {
                    BeginTuning();
                }

                if (hasUser && GUILayout.Button("Reset to defaults", GUILayout.ExpandWidth(true)))
                {
                    DeleteUserProfile(currentId);
                    ActivePid.ApplyForAircraft(currentId);
                }
            }
            else
            {
                GUILayout.Label("Enter an aircraft to tune its profile.", richLabel);
            }
        }
        else
        {
            // Aircraft changed during tuning
            if (currentId != s_tuningAircraftId)
            {
                GUILayout.Label(
                    $"<color=#FFAA00>Aircraft changed. Return to <b>{s_tuningAircraftId}</b> or cancel.</color>",
                    richLabel);

                if (GUILayout.Button("Cancel", GUILayout.ExpandWidth(true)))
                {
                    Restore();
                    ActivePid.ApplyForAircraft(currentId);
                }
            }
            else
            {
                GUILayout.Label("<color=#FFFF00><b>Tuning mode</b></color>", richLabel, GUILayout.ExpandWidth(true));

                if (GUILayout.Button("Save Overrides", GUILayout.ExpandWidth(true)))
                {
                    SaveAndRestore(s_tuningAircraftId);
                }

                if (GUILayout.Button("Cancel", GUILayout.ExpandWidth(true)))
                {
                    Restore();
                    ActivePid.ApplyForAircraft(currentId);
                }
            }
        }

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }
}
