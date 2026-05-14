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
        string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        Dir = string.IsNullOrEmpty(assemblyDir) ? "" : Path.Combine(assemblyDir, "Profiles");
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
        catch
        {
            return null;
        }
    }

    public static bool Exists(string id)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(Dir))
        {
            return false;
        }

        return File.Exists(Path.Combine(Dir, $"{id}.json"));
    }
}
