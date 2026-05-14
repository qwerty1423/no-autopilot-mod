using System;
using System.IO;
using System.Reflection;

using NOAutopilot.Core.PID;

using UnityEngine;

namespace NOAutopilot.Core.Config;

public static class DefaultProfiles
{
    private static readonly string Dir;

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

        // Search known locations
        string[] candidates =
        [
            // scriptengine debug
            Path.Combine(BepInEx.Paths.BepInExRootPath, "scripts", "Profiles"),

            // manual install
            Path.Combine(BepInEx.Paths.PluginPath, "no-autopilot-mod", "Profiles"),

            // nomm install
            Path.Combine(BepInEx.Paths.PluginPath, "no-autopilot-mod", "no-autopilot-mod", "Profiles"),
        ];

        foreach (string path in candidates)
        {
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        // Default fallback
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
            return JsonUtility.FromJson<PidProfile>(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[DefaultProfiles] Failed to load '{id}': {ex.Message}");
            return null;
        }
    }

    public static bool Exists(string id)
    {
        return !string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(Dir) && File.Exists(Path.Combine(Dir, $"{id}.json"));
    }
}
