using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>
    /// Load either the legacy single-profile config or the newer multi-profile config set.
    /// </summary>
    public static void LoadSingleton()
    {
        string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string path = Path.Combine(pluginDir, "acls_config.json");

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        // Try to load as config-set first (new format)
        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                using JsonDocument doc = JsonDocument.Parse(json);
                // Check if the root has a "Profiles" property to distinguish 
                // between the new Set format and the legacy single Profile format
                if (doc.RootElement.TryGetProperty("Profiles", out _))
                {
                    setSingleton = JsonSerializer.Deserialize<ACLSConfigSet>(json, options);
                }
                else
                {
                    ACLSConfig legacy = JsonSerializer.Deserialize<ACLSConfig>(json, options);
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
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            string contents = JsonSerializer.Serialize(set, options);
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
                Kp = 0.025f,
                Ki = 0f,
                Kd = 0f,
                BufferDuration = 0f,
                Invert = false
            },
            YawController = new PIDConfig
            {
                Kp = 0.1f,
                Ki = 0f,
                Kd = 0.3f,
                BufferDuration = 0f,
                Invert = false
            },
            PitchController = new PIDConfig
            {
                Kp = 0.1f,
                Ki = 0.01f,
                Kd = 0.05f,
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
            TerminalPhaseHeight = 15f,
            MaxControlAngle = 45f,
            CruisingSpeed = 138f,
            LandingSpeed = 72f,
            SpeedTransitionDistance = 6000f,
            TerminalPitchAngle = 10f,
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
    public Dictionary<string, ACLSConfig> Profiles { get; set; } = new Dictionary<string, ACLSConfig>();

    /// <summary>
    /// Map by Aircraft.definition.jsonKey (mission/unit key, e.g. "Multirole1").
    /// </summary>
    public Dictionary<string, string> AircraftJsonKeyToProfile { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Map by Unit.unitName (runtime unit name).
    /// </summary>
    public Dictionary<string, string> AircraftUnitNameToProfile { get; set; } = [];

    /// <summary>
    /// Map by Aircraft.definition.unitName (display/unit name).
    /// </summary>
    public Dictionary<string, string> AircraftDefinitionNameToProfile { get; set; } = [];

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
                // If your aircraft keys differ, edit this mapping.
                ["Multirole1"] = "ifrit",
                ["SmallFighter1"] = "compass",
                ["Fighter1"] = "compass"
            },
            AircraftUnitNameToProfile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Optional: runtime unitName matching (if you see these in logs).
                ["Ifrit"] = "ifrit",
                ["Compass"] = "compass",
                ["KR-67 Ifrit"] = "ifrit"
            },
            AircraftDefinitionNameToProfile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Optional: ScriptableObject UnitDefinition.unitName matching.
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
        if (legacy == null)
        {
            return CreateDefault();
        }

        var set = new ACLSConfigSet
        {
            DefaultProfile = "ifrit",
            Profiles = new Dictionary<string, ACLSConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["ifrit"] = legacy,
                ["compass"] = CloneProfile(legacy)
            },
            AircraftJsonKeyToProfile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Multirole1"] = "ifrit",
                ["SmallFighter1"] = "compass",
                ["Fighter1"] = "compass"
            }
        };

        set.NormalizeComparers();
        return set;
    }

    public ACLSConfig GetProfileOrDefault(string name)
    {
        NormalizeComparers();
        if (!string.IsNullOrWhiteSpace(name) && Profiles != null && Profiles.TryGetValue(name, out var cfg) && cfg != null)
        {
            return cfg;
        }

        // Fall back: ifrit -> first profile -> default profile values
        if (Profiles != null && Profiles.TryGetValue("ifrit", out var ifrit) && ifrit != null) return ifrit;
        if (Profiles != null)
        {
            foreach (var kv in Profiles)
            {
                if (kv.Value != null) return kv.Value;
            }
        }
        return ACLSConfig.GetDefaultProfileIfrit();
    }

    public string ResolveProfileForAircraft(Aircraft aircraft)
    {
        NormalizeComparers();
        if ((UnityEngine.Object)(object)aircraft == null) return DefaultProfile;

        try
        {
            // Best signal: mission key (UnitDefinition.jsonKey) - often things like "Multirole1"
            string jsonKey = aircraft.definition?.jsonKey;
            if (!string.IsNullOrWhiteSpace(jsonKey) && AircraftJsonKeyToProfile != null && AircraftJsonKeyToProfile.TryGetValue(jsonKey, out var p1))
                return p1;

            // Next: ScriptableObject unitName
            string defName = aircraft.definition?.unitName;
            if (!string.IsNullOrWhiteSpace(defName) && AircraftDefinitionNameToProfile != null && AircraftDefinitionNameToProfile.TryGetValue(defName, out var p2))
                return p2;

            // Next: runtime Unit.unitName
            string unitName = aircraft.unitName;
            if (!string.IsNullOrWhiteSpace(unitName) && AircraftUnitNameToProfile != null && AircraftUnitNameToProfile.TryGetValue(unitName, out var p3))
                return p3;

            // Last chance: substring match for common names
            string hay = $"{jsonKey} {defName} {unitName}".ToLowerInvariant();
            if (hay.Contains("ifrit")) return "ifrit";
            if (hay.Contains("compass")) return "compass";
        }
        catch
        {
            // ignore and fall back
        }

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
        string json = JsonSerializer.Serialize(src);
        return JsonSerializer.Deserialize<ACLSConfig>(json);
    }
}
