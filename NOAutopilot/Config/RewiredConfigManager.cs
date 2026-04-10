extern alias JetBrains;
using System.Linq;

using BepInEx.Configuration;

using Rewired;

using UnityEngine;

namespace NOAutopilot;

internal static class RewiredConfigManager
{
    private static bool s_isListening;
    private static ConfigEntryBase s_targetEntry, s_targetController, s_targetIndex;

    public static ConfigEntry<string> BindRW(ConfigFile config, string category, string keyName, string description)
    {
        ConfigEntry<string> cName = config.Bind("Hidden", keyName + "_CN", "",
            new ConfigDescription("", null, new ConfigurationManagerAttributes { Browsable = false }));
        ConfigEntry<int> bIdx = config.Bind("Hidden", keyName + "_IX", -1,
            new ConfigDescription("", null, new ConfigurationManagerAttributes { Browsable = false }));
        return config.Bind(category, keyName, "",
            new ConfigDescription(description, null,
                new ConfigurationManagerAttributes
                {
                    CustomDrawer = RewiredButtonDrawer,
                    ControllerName = cName,
                    ButtonIndex = bIdx
                }));
    }

    public static void Update()
    {
        if (!s_isListening || ReInput.controllers == null)
        {
            return;
        }

        foreach (Joystick j in ReInput.controllers.Joysticks)
        {
            if (CheckController(j))
            {
                return;
            }
        }

        if (CheckController(ReInput.controllers.Mouse))
        {
            return;
        }

        CheckController(ReInput.controllers.Keyboard);
    }

    private static bool CheckController(Controller c)
    {
        if (c == null)
        {
            return false;
        }

        for (int i = 0; i < c.buttonCount; i++)
        {
            if (c.GetButtonDown(i))
            {
                if (c.type == ControllerType.Mouse && i <= 2)
                {
                    continue;
                }

                Controller.Button btn = c.Buttons[i];
                SaveBind(c, btn.elementIdentifier.name, btn.elementIdentifier.id, "B");
                return true;
            }
        }

        return false;
    }

    private static void SaveBind(Controller c, string elemName, int id, string typeTag)
    {
        string cName = c.name.Trim();
        s_targetEntry.BoxedValue = $"{cName} | {elemName} | {id} | {typeTag}";
        if (s_targetController != null)
        {
            s_targetController.BoxedValue = cName;
        }

        if (s_targetIndex != null)
        {
            s_targetIndex.BoxedValue = id;
        }

        s_isListening = false;
    }

    public static void RewiredButtonDrawer(ConfigEntryBase entry)
    {
        if (s_isListening && s_targetEntry == entry)
        {
            if (GUILayout.Button("Listening... (Click to cancel)", GUILayout.ExpandWidth(true)))
            {
                s_isListening = false;
            }
        }
        else
        {
            string val = (string)entry.BoxedValue;
            if (!GUILayout.Button(string.IsNullOrEmpty(val) ? "None - Click to bind (Rewired)" : val,
                    GUILayout.ExpandWidth(true)))
            {
                return;
            }

            s_isListening = true;
            s_targetEntry = entry;
            ConfigurationManagerAttributes attr = entry.Description.Tags?.OfType<ConfigurationManagerAttributes>()
                .FirstOrDefault();
            s_targetController = attr?.ControllerName as ConfigEntryBase;
            s_targetIndex = attr?.ButtonIndex as ConfigEntryBase;
        }
    }

    public static void Reset()
    {
        s_isListening = false;
        s_targetEntry = null;
        s_targetController = null;
        s_targetIndex = null;
    }
}
