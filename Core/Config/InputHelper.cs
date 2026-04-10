extern alias JetBrains;
using BepInEx.Configuration;

using Rewired;

namespace NOAutopilot.Core.Config;

public static class InputHelper
{
    public static bool IsDown(ConfigEntry<string> rw)
    {
        return PollRewired(rw, true);
    }

    public static bool IsPressed(ConfigEntry<string> rw)
    {
        return PollRewired(rw, false);
    }

    private static bool PollRewired(ConfigEntry<string> rw, bool checkDown)
    {
        string val = rw?.Value;
        if (string.IsNullOrEmpty(val) || !val.Contains("|") || ReInput.controllers == null)
        {
            return false;
        }

        string[] p = val.Split('|');
        if (p.Length < 3)
        {
            return false;
        }

        string cName = p[0].Trim();
        if (!int.TryParse(p[2].Trim(), out int id))
        {
            return false;
        }

        Controller target = null;
        foreach (Joystick j in ReInput.controllers.Joysticks)
        {
            if (j.name.Trim() == cName)
            {
                target = j;
                break;
            }
        }

        if (target == null && ReInput.controllers.Mouse.name.Trim() == cName)
        {
            target = ReInput.controllers.Mouse;
        }

        if (target == null && ReInput.controllers.Keyboard.name.Trim() == cName)
        {
            target = ReInput.controllers.Keyboard;
        }

        Controller.Element element = target?.GetElementById(id);

        if (element is not Controller.Button)
        {
            return false;
        }

        int idx = target.GetButtonIndexById(id);
        return idx >= 0 && (checkDown
            ? target.GetButtonDown(idx)
            : target.GetButton(idx));
    }
}
