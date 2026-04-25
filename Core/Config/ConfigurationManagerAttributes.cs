using System;

using BepInEx.Configuration;

namespace NOAutopilot.Core.Config;

internal sealed class ConfigurationManagerAttributes
{
    public bool? Browsable;
    public object ButtonIndex;

    public object ControllerName;

    // public bool? HideDefaultButton;
    // public int? Order;
    public Action<ConfigEntryBase> CustomDrawer;
}
