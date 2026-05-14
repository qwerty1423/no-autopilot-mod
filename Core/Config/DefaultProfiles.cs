using System.IO;
using System.Reflection;

using NOAutopilot.Core.PID;

using UnityEngine;

namespace NOAutopilot.Core.Config;

public static class DefaultProfiles
{
    private static readonly string Dir = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
        "Profiles"
    );

    public static PidProfile Load(string id)
    {
        string path = Path.Combine(Dir, $"{id}.json");
        return !File.Exists(path) ? null : JsonUtility.FromJson<PidProfile>(File.ReadAllText(path));
    }

    public static bool Exists(string id)
    {
        return File.Exists(Path.Combine(Dir, $"{id}.json"));
    }
}
