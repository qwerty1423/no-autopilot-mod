using System;
using System.IO;
using System.Text.RegularExpressions;

using BepInEx.Configuration;

using NOAutopilot.Core.PID;

using UnityEngine;

namespace NOAutopilot.Core.Config;

public static class PidProfileManager
{
    private static readonly string UserDir = Path.Combine(BepInEx.Paths.ConfigPath, "NOAutopilot", "Profiles");
    private static readonly string BackupDir = Path.Combine(UserDir, "Backups");

    private static PidProfile s_tuningSnapshot;
    private static string s_tuningAircraftId;
    private static bool? s_prevSaveOnConfigSet;
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

    public static PidProfile LoadUserProfile(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        string path = Path.Combine(UserDir, $"{id}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try { return JsonUtility.FromJson<PidProfile>(File.ReadAllText(path)); }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[PidProfileManager] Error loading user profile '{id}': {ex.Message}");
            return null;
        }
    }

    public static void MoveUserProfile(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        string path = Path.Combine(UserDir, $"{id}.json");
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            _ = Directory.CreateDirectory(BackupDir);
            string backupPath = Path.Combine(BackupDir, $"{id}_{DateTime.Now:yyyy-MM-ddTHH-mm-ss}.json");
            File.Move(path, backupPath);
            Plugin.Logger.LogInfo($"[PidProfileManager] Profile '{id}' moved to backup.");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[PidProfileManager] Failed to back up profile '{id}': {ex}");
        }
    }

    public static void BeginTuning()
    {
        string id = ActivePid.CurrentAircraftId;
        if (string.IsNullOrEmpty(id) || IsTuning)
        {
            return;
        }

        s_tuningSnapshot = CaptureCurrent();
        s_tuningAircraftId = id;
        SuppressConfigAutosaveForTuning();
        ActivePid.SyncToConfig();
        IsTuning = true;
    }
    private static void SuppressConfigAutosaveForTuning()
    {
        if (Plugin.Instance?.Config == null || s_prevSaveOnConfigSet.HasValue)
        {
            return;
        }

        s_prevSaveOnConfigSet = Plugin.Instance.Config.SaveOnConfigSet;
        Plugin.Instance.Config.SaveOnConfigSet = false;
    }

    public static void SaveAndExitTuning(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        ActivePid.LoadGlobalDefaults();
        ActivePid.ApplyProfile(DefaultProfiles.Load(id));
        PidProfile baseProfile = CaptureCurrent();

        ActivePid.SyncFromConfig();
        PidProfile tunedProfile = CaptureCurrent();

        PidProfile partial = new();
        if (tunedProfile.Alt != baseProfile.Alt) { partial.Alt = tunedProfile.Alt; }
        if (tunedProfile.Vs != baseProfile.Vs) { partial.Vs = tunedProfile.Vs; }
        if (tunedProfile.Pitch != baseProfile.Pitch) { partial.Pitch = tunedProfile.Pitch; }
        if (tunedProfile.Roll != baseProfile.Roll) { partial.Roll = tunedProfile.Roll; }
        if (tunedProfile.RollRate != baseProfile.RollRate) { partial.RollRate = tunedProfile.RollRate; }
        if (tunedProfile.Crs != baseProfile.Crs) { partial.Crs = tunedProfile.Crs; }
        if (tunedProfile.Spd != baseProfile.Spd) { partial.Spd = tunedProfile.Spd; }
        if (tunedProfile.Gcas != baseProfile.Gcas) { partial.Gcas = tunedProfile.Gcas; }
        if (tunedProfile.SchedPitch != baseProfile.SchedPitch) { partial.SchedPitch = tunedProfile.SchedPitch; }
        if (tunedProfile.SchedRollRate != baseProfile.SchedRollRate) { partial.SchedRollRate = tunedProfile.SchedRollRate; }
        if (tunedProfile.SchedVs != baseProfile.SchedVs) { partial.SchedVs = tunedProfile.SchedVs; }
        if (tunedProfile.SchedSpd != baseProfile.SchedSpd) { partial.SchedSpd = tunedProfile.SchedSpd; }

        if (!partial.IsEmpty())
        {
            _ = Directory.CreateDirectory(UserDir);
            File.WriteAllText(Path.Combine(UserDir, $"{id}.json"), JsonUtility.ToJson(partial, true));
        }
        else
        {
            string path = Path.Combine(UserDir, $"{id}.json");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        IsTuning = false;
        s_tuningAircraftId = null;
        s_tuningSnapshot = null;

        ActivePid.ApplyForAircraft(id);
        ActivePid.WriteGlobalDefaultsToConfig();
        RestoreConfigAutosaveAfterTuning();
    }
    private static void RestoreConfigAutosaveAfterTuning()
    {
        if (Plugin.Instance?.Config == null || !s_prevSaveOnConfigSet.HasValue)
        {
            return;
        }

        Plugin.Instance.Config.SaveOnConfigSet = s_prevSaveOnConfigSet.Value;
        s_prevSaveOnConfigSet = null;
    }

    public static void CancelTuning()
    {
        if (!IsTuning)
        {
            return;
        }

        IsTuning = false;

        ActivePid.Alt = PIDTuning.Parse(s_tuningSnapshot.Alt);
        ActivePid.Vs = PIDTuning.Parse(s_tuningSnapshot.Vs);
        ActivePid.Pitch = PIDTuning.Parse(s_tuningSnapshot.Pitch);
        ActivePid.Roll = PIDTuning.Parse(s_tuningSnapshot.Roll);
        ActivePid.RollRate = PIDTuning.Parse(s_tuningSnapshot.RollRate);
        ActivePid.Crs = PIDTuning.Parse(s_tuningSnapshot.Crs);
        ActivePid.Spd = PIDTuning.Parse(s_tuningSnapshot.Spd);
        ActivePid.Gcas = PIDTuning.Parse(s_tuningSnapshot.Gcas);
        ActivePid.SchedPitch = GainSchedule.Parse(s_tuningSnapshot.SchedPitch);
        ActivePid.SchedRollRate = GainSchedule.Parse(s_tuningSnapshot.SchedRollRate);
        ActivePid.SchedVs = GainSchedule.Parse(s_tuningSnapshot.SchedVs);
        ActivePid.SchedSpd = GainSchedule.Parse(s_tuningSnapshot.SchedSpd);

        s_tuningAircraftId = null;
        s_tuningSnapshot = null;

        ActivePid.WriteGlobalDefaultsToConfig();
        RestoreConfigAutosaveAfterTuning();
    }

    private static PidProfile CaptureCurrent()
    {
        return new()
        {
            Alt = ActivePid.Alt.ToString(),
            Vs = ActivePid.Vs.ToString(),
            Pitch = ActivePid.Pitch.ToString(),
            Roll = ActivePid.Roll.ToString(),
            RollRate = ActivePid.RollRate.ToString(),
            Crs = ActivePid.Crs.ToString(),
            Spd = ActivePid.Spd.ToString(),
            Gcas = ActivePid.Gcas.ToString(),
            SchedPitch = ActivePid.SchedPitch.ToString(),
            SchedRollRate = ActivePid.SchedRollRate.ToString(),
            SchedVs = ActivePid.SchedVs.ToString(),
            SchedSpd = ActivePid.SchedSpd.ToString()
        };
    }

    public static void DrawConfigManagerControls(ConfigEntryBase _)
    {
        if (IsTuning)
        {
            ActivePid.SyncFromConfig();
        }

        string currentId = ActivePid.CurrentAircraftId;
        bool hasUser = !string.IsNullOrEmpty(currentId) && File.Exists(Path.Combine(UserDir, $"{currentId}.json"));
        bool hasShipped = DefaultProfiles.Exists(currentId);
        string displayId = string.IsNullOrEmpty(currentId) ? "None" : currentId;

        GUIStyle richLabel = new(GUI.skin.label) { richText = true };
        GUILayout.BeginVertical();

        // status of active values
        string appliedStatus = string.IsNullOrEmpty(currentId)
            ? "<color=#AAAAAA>[No Aircraft]</color>"
            : hasUser
                ? "<color=#00FF00>[User Profile]</color>"
                : hasShipped
                    ? "<color=#88CCFF>[Default Profile]</color>"
                    : "<color=#AAAAAA>[Global Defaults]</color>";

        // status of config manager values
        string pidStatus = IsTuning
            ? "<color=#FFFF00>[Tuning]</color>"
            : "<color=#AAAAAA>[Global defaults]</color>";

        GUILayout.Label($"<b>Aircraft:</b> {displayId}", richLabel);
        GUILayout.Label($"<b>Active Profile:</b> {appliedStatus}", richLabel);
        GUILayout.Label($"<b>Displayed values:</b> {pidStatus}", richLabel);

        GUILayout.BeginHorizontal();

        if (!IsTuning)
        {
            if (!string.IsNullOrEmpty(currentId))
            {
                if (GUILayout.Button("Tune current aircraft"))
                {
                    BeginTuning();
                }

                if (hasUser && GUILayout.Button("Reset to defaults"))
                {
                    MoveUserProfile(currentId);
                    ActivePid.ApplyForAircraft(currentId);
                }
            }
            else
            {
                GUILayout.Label("Enter an aircraft to tune its profile.");
            }
        }
        else if (currentId != s_tuningAircraftId)
        {
            GUILayout.Label($"<color=#FFAA00>Aircraft changed. Return to <b>{s_tuningAircraftId}</b> or cancel.</color>", richLabel);
            if (GUILayout.Button("Cancel"))
            {
                CancelTuning();
            }
        }
        else
        {
            if (GUILayout.Button("Save Overrides"))
            {
                SaveAndExitTuning(s_tuningAircraftId);
            }

            if (GUILayout.Button("Cancel"))
            {
                CancelTuning();
            }
        }

        GUILayout.EndHorizontal();

        if (GUILayout.Button("Export current as global profile"))
        {
            PidProfile current = new()
            {
                Alt = ActivePid.Alt.ToString(),
                Vs = ActivePid.Vs.ToString(),
                Pitch = ActivePid.Pitch.ToString(),
                Roll = ActivePid.Roll.ToString(),
                RollRate = ActivePid.RollRate.ToString(),
                Crs = ActivePid.Crs.ToString(),
                Spd = ActivePid.Spd.ToString(),
                Gcas = ActivePid.Gcas.ToString(),
                SchedPitch = ActivePid.SchedPitch.ToString(),
                SchedRollRate = ActivePid.SchedRollRate.ToString(),
                SchedVs = ActivePid.SchedVs.ToString(),
                SchedSpd = ActivePid.SchedSpd.ToString()
            };
            DefaultProfiles.Save("exported_global_defaults", current);
        }

        GUILayout.EndVertical();
    }
}
