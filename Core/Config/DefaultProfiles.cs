using System;
using System.IO;
using System.Reflection;

using BepInEx;

using NOAutopilot.Core.PID;

using UnityEngine;

namespace NOAutopilot.Core.Config;

public static class DefaultProfiles
{
    private static readonly string Dir;

    static DefaultProfiles()
    {
        try
        {
            string location = Assembly.GetExecutingAssembly().Location;

            Dir = string.IsNullOrEmpty(location)
                ? Path.Combine(Paths.PluginPath, "NOAutopilot", "Profiles")
                : Path.Combine(Path.GetDirectoryName(location), "Profiles");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[DefaultProfiles] Failed to init: {ex.Message}");
            Dir = Path.Combine(Paths.PluginPath, "NOAutopilot", "Profiles");
        }
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
