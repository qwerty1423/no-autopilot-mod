using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using NOAutopilot.Core.PID;

using UnityEngine;

namespace NOAutopilot.Core.Config;

public static class DefaultProfiles
{
    private static readonly string Dir;
    private static readonly HashSet<string> ErrorLogged = [];

    static DefaultProfiles()
    {
        Dir = FindProfilesDir();
        Plugin.Logger.LogInfo($"[DefaultProfiles] Using profiles dir: '{Dir}'");
    }

    private static string FindProfilesDir()
    {
        try
        {
            string location = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(location))
            {
                string assemblyDir = Path.GetDirectoryName(location);
                if (!string.IsNullOrEmpty(assemblyDir))
                {
                    string path = Path.Combine(assemblyDir, "Profiles");
                    if (Directory.Exists(path))
                    {
                        return path;
                    }
                }
            }
        }
        catch
        {
            // Ignore
        }

        // Fallback for ScriptEngine or nomm installs
        string[] candidates =
        [
            Path.Combine(BepInEx.Paths.BepInExRootPath, "scripts", "Profiles"),
            Path.Combine(BepInEx.Paths.PluginPath, "no-autopilot-mod", "Profiles"),
            Path.Combine(BepInEx.Paths.PluginPath, "no-autopilot-mod", "no-autopilot-mod", "Profiles"),
            Path.Combine(BepInEx.Paths.PluginPath, "Profiles")
        ];

        foreach (string path in candidates)
        {
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        // Return first candidate as a default path to avoid null
        return candidates[0];
    }

    public static PidProfile Load(string id)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(Dir))
        {
            return null;
        }

        string path = Path.Combine(Dir, $"{id}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<PidProfile>(json);
        }
        catch (Exception ex)
        {
            if (ErrorLogged.Add(id))
            {
                Plugin.Logger.LogError($"[DefaultProfiles] Failed to load '{id}': {ex.Message}");
            }
            return null;
        }
    }

    public static bool Exists(string id)
    {
        return !string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(Dir) && File.Exists(Path.Combine(Dir, $"{id}.json"));
    }

    public static void Save(string id, PidProfile profile)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(Dir))
        {
            return;
        }

        try
        {
            string path = Path.Combine(Dir, $"{id}.json");
            File.WriteAllText(path, JsonUtility.ToJson(profile, true));
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[DefaultProfiles] Failed to save '{id}': {ex.Message}");
        }
    }
}
