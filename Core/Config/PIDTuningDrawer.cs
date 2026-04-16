using BepInEx.Configuration;

using UnityEngine;

namespace NOAutopilot.Core.Config;

public static class PIDTuningDrawer
{
    // label width, field width
    private const float L = 26f;
    private const float F = 52f;

    public static void Draw(ConfigEntryBase entry)
    {
        var t = (PIDTuning)entry.BoxedValue;
        bool changed = false;

        GUILayout.BeginVertical();

        // Row 1 — matches PIDLoop2 order: Kp Ki Kd N
        GUILayout.BeginHorizontal();
        changed |= Field("Kp", ref t.Kp);
        changed |= Field("Ki", ref t.Ki);
        changed |= Field("Kd", ref t.Kd);
        changed |= Field("N", ref t.N);
        GUILayout.EndHorizontal();

        // Row 2 — 2DOF + smoothing: B C SmoothIn SmoothOut
        GUILayout.BeginHorizontal();
        changed |= Field("B", ref t.B);
        changed |= Field("C", ref t.C);
        changed |= Field("SmI", ref t.SmoothIn);
        changed |= Field("SmO", ref t.SmoothOut);
        GUILayout.EndHorizontal();

        // Row 3 — deadbands + Clegg: pDb iDb dDb oDb Clegg
        GUILayout.BeginHorizontal();
        changed |= Field("pDb", ref t.ProportionalDeadband);
        changed |= Field("iDb", ref t.IntegralDeadband);
        changed |= Field("dDb", ref t.DerivativeDeadband);
        changed |= Field("oDb", ref t.OutputDeadband);
        bool newClegg = GUILayout.Toggle(t.Clegg, "Clg", GUILayout.Width(36f));
        if (newClegg != t.Clegg) { t.Clegg = newClegg; changed = true; }
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        if (changed)
        {
            entry.BoxedValue = t;
        }
    }

    private static bool Field(string label, ref float value)
    {
        GUILayout.Label(label, GUILayout.Width(L));
        string prev = value.ToString("G5", System.Globalization.CultureInfo.InvariantCulture);
        string next = GUILayout.TextField(prev, GUILayout.Width(F));
        if (next != prev &&
            float.TryParse(next,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float v))
        {
            value = v;
            return true;
        }
        return false;
    }
}
