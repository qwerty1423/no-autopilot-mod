using BepInEx.Configuration;

namespace NOAutopilot.Core.Config;

public static class PIDTuningBinder
{
    public static ConfigEntry<PIDTuning> Bind(
        ConfigFile config,
        string section,
        string key,
        PIDTuning defaultValue,
        string description)
    {
        return config.Bind(section, key, defaultValue,
            new ConfigDescription(description, null,
                new ConfigurationManagerAttributes
                {
                    CustomDrawer = PIDTuningDrawer.Draw
                }));
    }
}
