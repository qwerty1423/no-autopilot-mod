using BepInEx.Configuration;

using NOAutopilot.Core.PID;

namespace NOAutopilot.Core.Config;

public static class GainScheduleBinder
{
    public static ConfigEntry<GainSchedule> Bind(
        ConfigFile config,
        string section,
        string key,
        GainSchedule defaultValue,
        string description)
    {
        ConfigDescription desc = new(description, null,
            new ConfigurationManagerAttributes
            {
                CustomDrawer = GainScheduleDrawer.Draw,
            });

        return config.Bind(section, key, defaultValue, desc);
    }
}
