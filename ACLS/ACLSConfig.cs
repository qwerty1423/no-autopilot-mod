using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NOAutopilot.ACLS;

/// <summary>
/// Represents a single aircraft "profile" of ACLS tuning parameters.
/// </summary>
public class ACLSConfig
{
    /// <summary>
    /// The currently active profile that the mod uses at runtime.
    /// </summary>
    public static ACLSConfig singleton;

    /// <summary>
    /// The loaded config set (multiple profiles + mappings). May be null if legacy config was loaded.
    /// </summary>
    public static ACLSConfigSet setSingleton;

    /// <summary>
    /// Name of the currently active profile (when using ACLSConfigSet).
    /// </summary>
    public static string activeProfileName = "ifrit";

    public PIDConfig RollController { get; set; }
    public PIDConfig YawController { get; set; }
    public PIDConfig PitchController { get; set; }
    public PIDConfig ThrottleController { get; set; }
    public float TerminalPhaseHeight { get; set; }
    public float MaxControlAngle { get; set; }
    public float CruisingSpeed { get; set; }
    public float LandingSpeed { get; set; }
    public float SpeedTransitionDistance { get; set; }
    public float TerminalPitchAngle { get; set; }
    public string KeyName { get; set; }

    private static string _assetsDir = "";
    /// <summary>
    /// Load either the legacy single-profile config or the newer multi-profile config set.
    /// </summary>
    public static void LoadSingleton(string assetsDir = null)
    {
        if (!string.IsNullOrEmpty(assetsDir)) _assetsDir = assetsDir;
        string path = Path.Combine(_assetsDir, "acls_config.json");

        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                JObject doc = JObject.Parse(json);

                // Check if it's the new format or legacy
                if (doc.ContainsKey("Profiles"))
                {
                    setSingleton = JsonConvert.DeserializeObject<ACLSConfigSet>(json);
                }
                else
                {
                    ACLSConfig legacy = JsonConvert.DeserializeObject<ACLSConfig>(json);
                    setSingleton = ACLSConfigSet.FromLegacy(legacy);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error loading ACLS JSON: {ex.Message}");
            }
        }

        if (setSingleton == null)
        {
            Plugin.Logger.LogWarning("ACLS config missing or unreadable; creating default config set.");
            setSingleton = ACLSConfigSet.CreateDefault();
            TrySaveConfigSet(path, setSingleton);
        }

        activeProfileName = string.IsNullOrWhiteSpace(setSingleton.DefaultProfile) ? "ifrit" : setSingleton.DefaultProfile;
        singleton = setSingleton.GetProfileOrDefault(activeProfileName);
    }

    /// <summary>
    /// Select the best matching profile for the given aircraft, and switch the active config if needed.
    /// Call this when the player's aircraft changes.
    /// </summary>
    public static bool SelectForAircraft(Aircraft aircraft)
    {
        if (setSingleton == null)
        {
            // Legacy load path. Nothing to select.
            return false;
        }

        string profile = setSingleton.ResolveProfileForAircraft(aircraft);
        if (string.IsNullOrWhiteSpace(profile))
        {
            profile = setSingleton.DefaultProfile;
        }
        if (string.IsNullOrWhiteSpace(profile))
        {
            profile = "ifrit";
        }

        if (!string.Equals(profile, activeProfileName, StringComparison.OrdinalIgnoreCase))
        {
            activeProfileName = profile;
            singleton = setSingleton.GetProfileOrDefault(activeProfileName);
            return true;
        }
        return false;
    }

    private static string DescribeAircraft(Aircraft a)
    {
        if ((UnityEngine.Object)(object)a == null) return "null aircraft";
        try
        {
            string unitName = a.unitName ?? "";
            string defUnitName = a.definition != null ? a.definition.unitName : "";
            string jsonKey = a.definition != null ? a.definition.jsonKey : "";
            string code = a.definition != null ? a.definition.code : "";
            return $"unitName='{unitName}', def.unitName='{defUnitName}', jsonKey='{jsonKey}', code='{code}'";
        }
        catch
        {
            return "aircraft (describe failed)";
        }
    }

    private static void TrySaveConfigSet(string path, ACLSConfigSet set)
    {
        try
        {
            string contents = JsonConvert.SerializeObject(set, Formatting.Indented);
            File.WriteAllText(path, contents);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError("Error saving default ACLS config set: " + ex.Message);
        }
    }

    /// <summary>
    /// Default single-profile values (currently tuned for KR-67 Ifrit per user).
    /// </summary>
    internal static ACLSConfig GetDefaultProfileIfrit()
    {
        return new ACLSConfig
        {
            RollController = new PIDConfig
            {
                Kp = 0.015f,
                Ki = 0f,
                Kd = 0f,
                BufferDuration = 0f,
                Invert = false
            },
            YawController = new PIDConfig
            {
                Kp = 0.07f,
                Ki = 0f,
                Kd = 0.07f,
                BufferDuration = 0f,
                Invert = false
            },
            PitchController = new PIDConfig
            {
                Kp = 0.08f,
                Ki = 0.01f,
                Kd = 0.01f,
                BufferDuration = 1f,
                Invert = true
            },
            ThrottleController = new PIDConfig
            {
                Kp = 0.2f,
                Ki = 0f,
                Kd = 0f,
                BufferDuration = 2f,
                Invert = false
            },
            TerminalPhaseHeight = 11f,
            MaxControlAngle = 45f,
            CruisingSpeed = 150f,
            LandingSpeed = 72f,
            SpeedTransitionDistance = 6000f,
            TerminalPitchAngle = 7f,
            KeyName = "Equals"
        };
    }
}

/// <summary>
/// Multi-profile config format. Allows mapping aircraft -> profile.
/// </summary>
public class ACLSConfigSet
{
    /// <summary>
    /// Default profile to use when no mapping matches.
    /// </summary>
    public string DefaultProfile { get; set; } = "ifrit";

    /// <summary>
    /// Profiles by name (e.g. "ifrit", "compass").
    /// </summary>
    public Dictionary<string, ACLSConfig> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Map by Aircraft.definition.jsonKey (mission/unit key, e.g. "Multirole1").
    /// </summary>
    public Dictionary<string, string> AircraftJsonKeyToProfile { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Map by Unit.unitName (runtime unit name).
    /// </summary>
    public Dictionary<string, string> AircraftUnitNameToProfile { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Map by Aircraft.definition.unitName (display/unit name).
    /// </summary>
    public Dictionary<string, string> AircraftDefinitionNameToProfile { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Create default config set with two profiles and conservative mappings.
    /// NOTE: "compass" profile is cloned from "ifrit" by default; tune its values in acls_config.json.
    /// </summary>
    public static ACLSConfigSet CreateDefault()
    {
        var ifrit = ACLSConfig.GetDefaultProfileIfrit();

        // By default we clone ifrit so the mod stays usable even before tuning compass.
        var compass = CloneProfile(ifrit);
        var set = new ACLSConfigSet
        {
            DefaultProfile = "ifrit",
            Profiles = new Dictionary<string, ACLSConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["ifrit"] = ifrit,
                ["compass"] = compass
            },
            AircraftJsonKeyToProfile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // These keys come from unit jsonKey / mission unit key.
                ["Multirole1"] = "ifrit",
                ["SmallFighter1"] = "compass",
                ["Fighter1"] = "compass"
            },
            AircraftUnitNameToProfile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Ifrit"] = "ifrit",
                ["Compass"] = "compass",
                ["KR-67 Ifrit"] = "ifrit"
            },
            AircraftDefinitionNameToProfile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Ifrit"] = "ifrit",
                ["Compass"] = "compass",
                ["KR-67 Ifrit"] = "ifrit"
            }
        };

        // Normalize dictionaries after JSON load/save (to keep case-insensitive behavior).
        set.NormalizeComparers();
        return set;
    }

    public static ACLSConfigSet FromLegacy(ACLSConfig legacy)
    {
        if (legacy == null) return CreateDefault();
        var set = new ACLSConfigSet
        {
            DefaultProfile = "ifrit",
            Profiles = { ["ifrit"] = legacy, ["compass"] = CloneProfile(legacy) },
            AircraftJsonKeyToProfile = { ["Multirole1"] = "ifrit", ["SmallFighter1"] = "compass", ["Fighter1"] = "compass" }
        };
        set.NormalizeComparers();
        return set;
    }

    public ACLSConfig GetProfileOrDefault(string name)
    {
        if (!string.IsNullOrWhiteSpace(name) && Profiles.TryGetValue(name, out var cfg)) return cfg;
        if (Profiles.TryGetValue("ifrit", out var ifrit)) return ifrit;
        return ACLSConfig.GetDefaultProfileIfrit();
    }

    public string ResolveProfileForAircraft(Aircraft aircraft)
    {
        if (aircraft == null) return DefaultProfile;
        string jsonKey = aircraft.definition?.jsonKey;
        if (!string.IsNullOrWhiteSpace(jsonKey) && AircraftJsonKeyToProfile.TryGetValue(jsonKey, out var p1)) return p1;

        string defName = aircraft.definition?.unitName;
        if (!string.IsNullOrWhiteSpace(defName) && AircraftDefinitionNameToProfile.TryGetValue(defName, out var p2)) return p2;

        string unitName = aircraft.unitName;
        if (!string.IsNullOrWhiteSpace(unitName) && AircraftUnitNameToProfile.TryGetValue(unitName, out var p3)) return p3;

        return DefaultProfile;
    }

    /// <summary>
    /// Ensure dictionaries are case-insensitive even after JSON deserialization.
    /// </summary>
    public void NormalizeComparers()
    {
        Profiles = EnsureComparer(Profiles);
        AircraftJsonKeyToProfile = EnsureComparer(AircraftJsonKeyToProfile);
        AircraftUnitNameToProfile = EnsureComparer(AircraftUnitNameToProfile);
        AircraftDefinitionNameToProfile = EnsureComparer(AircraftDefinitionNameToProfile);
    }

    private static Dictionary<string, T> EnsureComparer<T>(Dictionary<string, T> dict)
    {
        if (dict == null) return new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        if (dict.Comparer == StringComparer.OrdinalIgnoreCase) return dict;

        var fixedDict = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in dict) fixedDict[kv.Key] = kv.Value;
        return fixedDict;
    }

    private static ACLSConfig CloneProfile(ACLSConfig src)
    {
        if (src == null) return ACLSConfig.GetDefaultProfileIfrit();
        return JsonConvert.DeserializeObject<ACLSConfig>(JsonConvert.SerializeObject(src));
    }
}
