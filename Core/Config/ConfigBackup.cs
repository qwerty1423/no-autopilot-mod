using System;
using System.IO;

using BepInEx;
using BepInEx.Logging;

namespace NOAutopilot.Core.Config;

internal static class ConfigBackup
{
    /// <summary>
    /// Bump this if necessary.
    /// </summary>
    public const int CurrentSchemaVersion = 1;
    public const string SchemaSection = "􏿽􏿽􏿽Internal􏿽􏿽􏿽";
    public const string SchemaKey = "ConfigSchemaVersion";

    /// <summary>
    /// Reads the cfg file to check the schema version, backs up and deletes if needed.
    /// Returns true if the config was regenerated.
    /// </summary>
    public static bool EnsureConfigValid(string pluginGuid, ManualLogSource logger)
    {
        string cfgPath = GetConfigPath(pluginGuid);

        // Always back up on startup
        BackupConfig(cfgPath, logger, "startup");
        PruneBackups(pluginGuid, logger, maxToKeep: 5);

        if (!File.Exists(cfgPath))
        {
            logger.LogInfo("[ConfigBackup] No config file found, will generate fresh.");
            return true;
        }

        int foundVersion = ReadSchemaVersion(cfgPath, logger);

        if (foundVersion == CurrentSchemaVersion)
        {
            logger.LogInfo($"[ConfigBackup] Config schema v{foundVersion} OK.");
            return false;
        }

        logger.LogWarning(
            $"[ConfigBackup] Schema mismatch: found v{foundVersion}, expected v{CurrentSchemaVersion}. " +
            "Backing up and regenerating config.");

        BackupConfig(cfgPath, logger, $"schema-v{foundVersion}");

        try
        {
            File.Delete(cfgPath);
            logger.LogInfo("[ConfigBackup] Old config deleted. Fresh config will be generated.");
        }
        catch (Exception ex)
        {
            logger.LogError($"[ConfigBackup] Failed to delete old config: {ex.Message}");
        }

        return true;
    }

    /// <summary>
    /// Writes the schema version into the config.
    /// </summary>
    public static void WriteSchemaVersion(BepInEx.Configuration.ConfigFile cfg)
    {
        BepInEx.Configuration.ConfigEntry<int> entry = cfg.Bind(
            SchemaSection,
            SchemaKey,
            CurrentSchemaVersion,
            "Do not edit. Used to detect config version mismatches."
        );

        // Force it to the current value in case an old value was loaded
        entry.Value = CurrentSchemaVersion;
        cfg.Save();
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
            Directory.CreateDirectory(backupDir);

            string fileName = Path.GetFileNameWithoutExtension(cfgPath);
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string backupPath = Path.Combine(backupDir, $"{fileName}.{tag}.{timestamp}.cfg.bak");

            File.Copy(cfgPath, backupPath, overwrite: true);
            logger.LogInfo($"[ConfigBackup] Backed up to: {backupPath}");
        }
        catch (Exception ex)
        {
            logger.LogError($"[ConfigBackup] Backup failed: {ex.Message}");
        }
    }

    private static void PruneBackups(string guid, ManualLogSource logger, int maxToKeep)
    {
        try
        {
            string backupDir = GetBackupDir();
            if (!Directory.Exists(backupDir))
            {
                return;
            }

            // Only prune startup backups, leave schema-mismatch ones alone
            string pattern = $"{guid}.startup.*.cfg.bak";
            string[] files = Directory.GetFiles(backupDir, pattern);

            if (files.Length <= maxToKeep)
            {
                return;
            }

            // Sort oldest first
            Array.Sort(files, StringComparer.Ordinal);

            int toDelete = files.Length - maxToKeep;
            for (int i = 0; i < toDelete; i++)
            {
                File.Delete(files[i]);
                logger.LogInfo($"[ConfigBackup] Pruned old backup: {Path.GetFileName(files[i])}");
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
            bool inSection = false;

            foreach (string line in File.ReadLines(cfgPath))
            {
                string trimmed = line.Trim();

                // BepInEx cfg section header
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    string section = trimmed[1..^1].Trim();
                    inSection = string.Equals(section, SchemaSection, StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!inSection)
                {
                    continue;
                }

                if (!trimmed.StartsWith(SchemaKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int eqIdx = trimmed.IndexOf('=');
                if (eqIdx < 0)
                {
                    continue;
                }

                string valStr = trimmed[(eqIdx + 1)..].Trim();
                if (int.TryParse(valStr, out int version))
                {
                    return version;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"[ConfigBackup] Failed to read schema version: {ex.Message}");
        }

        return 0;
    }
}
