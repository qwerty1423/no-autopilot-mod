using System.Collections.Generic;

using BepInEx.Configuration;

using Rewired;

using UnityEngine;

namespace NOAutopilot.Core.Config;

public static class InputHelper
{
    private sealed class CachedBinding
    {
        public string RawValue;
        public Controller Controller;
        public int ButtonIndex = -1;
        public float NextRetryTime;
    }

    private static readonly Dictionary<ConfigEntryBase, CachedBinding> Cache = [];

    public static void Reset()
    {
        Cache.Clear();
    }

    public static bool IsDown(ConfigEntry<string> rw)
    {
        return Poll(rw, true);
    }

    public static bool IsPressed(ConfigEntry<string> rw)
    {
        return Poll(rw, false);
    }

    private static bool Poll(ConfigEntry<string> rw, bool checkDown)
    {
        if (rw == null || ReInput.controllers == null)
        {
            return false;
        }

        if (!Cache.TryGetValue(rw, out CachedBinding cached))
        {
            cached = new CachedBinding();
            Cache[rw] = cached;
        }

        string val = rw.Value;

        if (cached.RawValue != val)
        {
            Resolve(cached, val);
        }

        Controller c = cached.Controller;

        if (c == null || (c.type == ControllerType.Joystick && !c.isConnected))
        {
            if (cached.ButtonIndex < 0 && c == null)
            {
                if (string.IsNullOrEmpty(val) || !val.Contains("|"))
                {
                    return false;
                }
            }

            if (Time.unscaledTime < cached.NextRetryTime)
            {
                return false;
            }

            cached.NextRetryTime = Time.unscaledTime + 1f;
            Resolve(cached, val);
            c = cached.Controller;

            if (c == null)
            {
                return false;
            }
        }

        int idx = cached.ButtonIndex;
        return idx >= 0 && (checkDown ? c.GetButtonDown(idx) : c.GetButton(idx));
    }

    private static void Resolve(CachedBinding cached, string val)
    {
        cached.RawValue = val;
        cached.Controller = null;
        cached.ButtonIndex = -1;

        if (string.IsNullOrEmpty(val) || !val.Contains("|"))
        {
            return;
        }

        string[] p = val.Split('|');
        if (p.Length < 3)
        {
            return;
        }

        string cName = p[0].Trim();
        if (!int.TryParse(p[2].Trim(), out int id))
        {
            return;
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

        if (target?.GetElementById(id) is not Controller.Button)
        {
            return;
        }

        int idx = target.GetButtonIndexById(id);
        if (idx < 0)
        {
            return;
        }

        cached.Controller = target;
        cached.ButtonIndex = idx;
    }
}
