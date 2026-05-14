using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace NOAutopilot.Core.Config;

internal static class ConfigBackup
{
    /// <summary>
    /// Bump this if necessary.
    /// </summary>
    public const int CurrentSchemaVersion = 2;
    public const string SchemaSection = "􏿽􏿽􏿽Internal􏿽􏿽􏿽";
    public const string SchemaKey = "ConfigSchemaVersion";

    private const string BackupSection = "Settings - Config Backup";

    // Defaults
    private const bool DefaultEnableBackups = true;
    private const bool DefaultBackupOnStartup = false;
    private const int DefaultMaxStartupBackups = 5;
    private const bool DefaultBackupOnSchemaChange = true;
    private const int DefaultMaxSchemaBackups = 100;
    private const bool DefaultAutoRegenerate = true;

    // Config entries (bound later so they show in ConfigManager)
    public static ConfigEntry<bool> EnableBackups;
    public static ConfigEntry<bool> BackupOnStartup;
    public static ConfigEntry<int> MaxStartupBackups;
    public static ConfigEntry<bool> BackupOnSchemaChange;
    public static ConfigEntry<int> MaxSchemaBackups;
    public static ConfigEntry<bool> AutoRegenerate;

    // Cached values read from raw file before BepInEx processes config
    private static bool s_enableBackups = DefaultEnableBackups;
    private static bool s_backupOnStartup = DefaultBackupOnStartup;
    private static int s_maxStartupBackups = DefaultMaxStartupBackups;
    private static bool s_backupOnSchemaChange = DefaultBackupOnSchemaChange;
    private static int s_maxSchemaBackups = DefaultMaxSchemaBackups;
    private static bool s_autoRegenerate = DefaultAutoRegenerate;
    public static string LastBackupPath { get; private set; }

    /// <summary>
    /// Reads the cfg file to check the schema version, backs up and deletes if needed.
    /// Pass base.Config so we can clear its in-memory cache when regenerating.
    /// Returns true if the config was regenerated.
    /// Must be called before Config.Bind calls.
    /// </summary>
    public static bool EnsureConfigValid(string pluginGuid, ManualLogSource logger, ConfigFile liveConfig)
    {
        string cfgPath = GetConfigPath(pluginGuid);

        // Read backup settings from raw cfg before BepInEx touches it
        ReadBackupSettingsRaw(cfgPath, logger);

        if (!s_enableBackups)
        {
            logger.LogInfo("[ConfigBackup] Backups disabled by config.");
        }

        // Startup backup
        if (s_enableBackups && s_backupOnStartup)
        {
            BackupConfig(cfgPath, logger, "startup");
            PruneBackups(pluginGuid, logger, "startup", s_maxStartupBackups);
        }

        if (!File.Exists(cfgPath))
        {
            logger.LogInfo("[ConfigBackup] No config file found, will regenerate.");
            return true;
        }

        int foundVersion = ReadSchemaVersion(cfgPath, logger);

        if (foundVersion == CurrentSchemaVersion)
        {
            logger.LogInfo($"[ConfigBackup] Config schema v{foundVersion} OK.");
            return false;
        }

        logger.LogWarning(
            $"[ConfigBackup] Schema mismatch: found v{foundVersion}, expected v{CurrentSchemaVersion}. ");

        // Schema mismatch backup
        if (s_enableBackups && s_backupOnSchemaChange)
        {
            BackupConfig(cfgPath, logger, $"schema-v{foundVersion}");
            PruneBackups(pluginGuid, logger, "schema", s_maxSchemaBackups);
        }

        if (!s_autoRegenerate)
        {
            logger.LogWarning("[ConfigBackup] Auto-regen is disabled. Keeping old config.");
            return false;
        }

        try
        {
            // Write an empty file so Reload() doesn't throw FileNotFoundException
            File.WriteAllText(cfgPath, string.Empty);
            liveConfig.Reload();
            logger.LogInfo("[ConfigBackup] Config cache cleared.");
        }
        catch (Exception ex)
        {
            logger.LogError($"[ConfigBackup] Failed to reset config cache: {ex.Message}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Binds backup settings into ConfigManager so they appear in the UI.
    /// </summary>
    public static void BindBackupSettings(ConfigFile cfg)
    {
        EnableBackups = cfg.Bind(BackupSection, "01. Enable Backups", DefaultEnableBackups,
            "Toggle for all config backups.");

        BackupOnStartup = cfg.Bind(BackupSection, "02. Backup On Startup", DefaultBackupOnStartup,
            "Create a backup every time the game starts.");

        MaxStartupBackups = cfg.Bind(BackupSection, "03. Max Startup Backups", DefaultMaxStartupBackups,
            "How many startup backups to keep. Oldest are pruned first. 0 = keep all.");

        BackupOnSchemaChange = cfg.Bind(BackupSection, "04. Backup On Schema Change", DefaultBackupOnSchemaChange,
            "Create a backup when a config version mismatch is detected.");

        MaxSchemaBackups = cfg.Bind(BackupSection, "05. Max Schema Backups", DefaultMaxSchemaBackups,
            "How many schema-mismatch backups to keep. 0 = keep all.");

        AutoRegenerate = cfg.Bind(BackupSection, "06. Auto Regenerate On Mismatch", DefaultAutoRegenerate,
            "Deletes and regenerates config when schema version doesn't match.");
    }

    /// <summary>
    /// Writes the schema version into the config.
    /// </summary>
    public static void WriteSchemaVersion(ConfigFile cfg)
    {
        ConfigEntry<int> entry = cfg.Bind(
            SchemaSection,
            SchemaKey,
            CurrentSchemaVersion,
            "Do not edit. Used to detect config version mismatches."
        );

        entry.Value = CurrentSchemaVersion;
        cfg.Save();
    }

    /// <summary>
    /// Reads backup-related settings directly from the raw .cfg text.
    /// This runs before BepInEx binds anything, so we can decide whether to
    /// back up or delete the file before it's loaded.
    /// </summary>
    private static void ReadBackupSettingsRaw(string cfgPath, ManualLogSource logger)
    {
        if (!File.Exists(cfgPath))
        {
            return;
        }

        try
        {
            Dictionary<string, string> values = ReadSectionValues(cfgPath, BackupSection);

            s_enableBackups = ReadBool(values, "01. Enable Backups", DefaultEnableBackups);
            s_backupOnStartup = ReadBool(values, "02. Backup On Startup", DefaultBackupOnStartup);
            s_maxStartupBackups = ReadInt(values, "03. Max Startup Backups", DefaultMaxStartupBackups);
            s_backupOnSchemaChange = ReadBool(values, "04. Backup On Schema Change", DefaultBackupOnSchemaChange);
            s_maxSchemaBackups = ReadInt(values, "05. Max Schema Backups", DefaultMaxSchemaBackups);
            s_autoRegenerate = ReadBool(values, "06. Auto Regenerate On Mismatch", DefaultAutoRegenerate);

            logger.LogInfo(
                $"[ConfigBackup] Raw settings: enable={s_enableBackups}, startup={s_backupOnStartup}(max {s_maxStartupBackups}), " +
                $"schema={s_backupOnSchemaChange}(max {s_maxSchemaBackups}), regen={s_autoRegenerate}");
        }
        catch (Exception ex)
        {
            logger.LogWarning($"[ConfigBackup] Failed to read raw backup settings, using defaults: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads all key=value pairs from a specific [Section] in a BepInEx .cfg file.
    /// </summary>
    private static Dictionary<string, string> ReadSectionValues(string cfgPath, string targetSection)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        bool inSection = false;

        foreach (string line in File.ReadLines(cfgPath))
        {
            string trimmed = line.Trim();

            // Skip comments and empty lines
            if (trimmed.Length == 0 || trimmed[0] == '#' || trimmed[0] == ';')
            {
                continue;
            }

            // Section header
            if (trimmed[0] == '[' && trimmed[^1] == ']')
            {
                string section = trimmed[1..^1].Trim();
                inSection = string.Equals(section, targetSection, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inSection)
            {
                continue;
            }

            int eqIdx = trimmed.IndexOf('=');
            if (eqIdx < 0)
            {
                continue;
            }

            string key = trimmed[..eqIdx].Trim();
            result[key] = trimmed[(eqIdx + 1)..].Trim();
        }

        return result;
    }

    private static bool ReadBool(Dictionary<string, string> values, string key, bool fallback)
    {
        return values.TryGetValue(key, out string val) && bool.TryParse(val, out bool parsed)
            ? parsed
            : fallback;
    }

    private static int ReadInt(Dictionary<string, string> values, string key, int fallback)
    {
        return values.TryGetValue(key, out string val) && int.TryParse(val, out int parsed)
            ? Math.Max(0, parsed)
            : fallback;
    }

    private static string GetConfigPath(string guid)
    {
        return Path.Combine(Paths.ConfigPath, $"{guid}.cfg");
    }

    private static string GetBackupDir()
    {
        return Path.Combine(Paths.ConfigPath, "no-autopilot-backups");
    }

    private static void BackupConfig(string cfgPath, ManualLogSource logger, string tag)
    {
        if (!File.Exists(cfgPath))
        {
            return;
        }

        try
        {
            string backupDir = GetBackupDir();
            _ = Directory.CreateDirectory(backupDir);

            string fileName = Path.GetFileNameWithoutExtension(cfgPath);
            string timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss");
            string backupPath = Path.Combine(backupDir, $"{fileName}.{tag}.{timestamp}.cfg");

            File.Copy(cfgPath, backupPath, overwrite: true);
            LastBackupPath = backupPath;
            logger.LogInfo($"[ConfigBackup] Backed up to: {backupPath}");
        }
        catch (Exception ex)
        {
            logger.LogError($"[ConfigBackup] Backup failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Prunes backups matching a tag prefix, keeping only the newest N.
    /// </summary>
    private static void PruneBackups(string guid, ManualLogSource logger, string tagPrefix, int maxToKeep)
    {
        if (maxToKeep <= 0)
        {
            return; // keep all
        }

        try
        {
            string backupDir = GetBackupDir();
            if (!Directory.Exists(backupDir))
            {
                return;
            }

            string pattern = $"{guid}.{tagPrefix}*.cfg";

            List<FileInfo> sortedFiles = [.. new DirectoryInfo(backupDir)
                .GetFiles(pattern)
                .OrderBy(static f => f.LastWriteTimeUtc)];

            if (sortedFiles.Count <= maxToKeep)
            {
                return;
            }

            int toDelete = sortedFiles.Count - maxToKeep;
            for (int i = 0; i < toDelete; i++)
            {
                sortedFiles[i].Delete();
                logger.LogInfo($"[ConfigBackup] Pruned old backup: {sortedFiles[i].Name}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"[ConfigBackup] Pruning failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads the .cfg file looking for the schema version line.
    /// </summary>
    private static int ReadSchemaVersion(string cfgPath, ManualLogSource logger)
    {
        try
        {
            Dictionary<string, string> values = ReadSectionValues(cfgPath, SchemaSection);

            if (values.TryGetValue(SchemaKey, out string val) &&
                int.TryParse(val, out int version))
            {
                return version;
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"[ConfigBackup] Failed to read schema version: {ex.Message}");
        }

        return 0;
    }
}
