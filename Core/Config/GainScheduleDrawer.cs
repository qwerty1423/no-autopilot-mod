using BepInEx.Configuration;

using NOAutopilot.Core.PID;

using UnityEngine;

namespace NOAutopilot.Core.Config;

public static class GainScheduleDrawer
{
    private const float F = 52f;
    private const int Cols = 2;

    private static readonly (string Label, string Tooltip, string Field)[] Cells =
    [
        ("RefQ", "Dynamic pressure (Pa) where gains were tuned.", nameof(GainSchedule.RefQ)),
        ("KpExp", "Proportional scaling", nameof(GainSchedule.KpExp)),
        ("TiExp", "Integral scaling", nameof(GainSchedule.TiExp)),
        ("TdExp", "Derivative scaling", nameof(GainSchedule.TdExp)),
        ("MinX", "Safety floor multiplier", nameof(GainSchedule.ClampMin)),
        ("MaxX", "Safety ceiling multiplier", nameof(GainSchedule.ClampMax)),
    ];

    private static float[] s_colWidths;

    private static float[] ColWidths
    {
        get
        {
            if (s_colWidths != null)
            {
                return s_colWidths;
            }

            s_colWidths = new float[Cols];
            for (int i = 0; i < Cells.Length; i++)
            {
                int col = i % Cols;
                GUIContent content = new(Cells[i].Label);
                float w = GUI.skin.label.CalcSize(content).x + 4f;
                if (w > s_colWidths[col])
                {
                    s_colWidths[col] = w;
                }
            }
            return s_colWidths;
        }
    }

    public static void Draw(ConfigEntryBase entry)
    {
        GainSchedule t = (GainSchedule)entry.BoxedValue;
        bool changed = false;
        if (s_colWidths == null)
        {
            _ = ColWidths;
        }

        GUILayout.BeginVertical();

        // Draw live Q if we are in flight
        if (APData.PlayerRB != null)
        {
            float speed = APData.PlayerRB.velocity.magnitude;
            float currentQ = GainScheduler.DynamicPressure(speed, APData.CurrentAlt);
            GUILayout.Label($"<color=#00FFFF>Live Q: {currentQ:F0} Pa</color> " +
                            $"(Alt: {APData.CurrentAlt:F0}m, Spd: {speed:F0}m/s)",
                            new GUIStyle(GUI.skin.label) { richText = true });
        }
        else
        {
            GUILayout.Label("<color=#888888>Live Q: (Spawn in to see)</color>",
                            new GUIStyle(GUI.skin.label) { richText = true });
        }

        int i = 0;
        while (i < Cells.Length)
        {
            GUILayout.BeginHorizontal();
            for (int col = 0; col < Cols && i < Cells.Length; col++, i++)
            {
                (string label, string tooltip, string fieldName) = Cells[i];
                float val = GetField(ref t, fieldName);
                if (Field(label, tooltip, ColWidths[col], ref val))
                {
                    SetField(ref t, fieldName, val);
                    changed = true;
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();

        if (changed)
        {
            entry.BoxedValue = t;
        }
    }

    private static bool Field(string label, string tooltip, float labelWidth, ref float value)
    {
        GUILayout.Label(new GUIContent(label, tooltip), GUILayout.Width(labelWidth));
        string prev = value.ToString("G5", System.Globalization.CultureInfo.InvariantCulture);
        string next = GUILayout.TextField(prev, GUILayout.Width(F));

        if (next != prev && float.TryParse(next,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float v))
        {
            value = v;
            return true;
        }
        return false;
    }

    private static float GetField(ref GainSchedule t, string name) => name switch
    {
        nameof(GainSchedule.RefQ) => t.RefQ,
        nameof(GainSchedule.KpExp) => t.KpExp,
        nameof(GainSchedule.TiExp) => t.TiExp,
        nameof(GainSchedule.TdExp) => t.TdExp,
        nameof(GainSchedule.ClampMin) => t.ClampMin,
        nameof(GainSchedule.ClampMax) => t.ClampMax,
        _ => 0f,
    };

    private static void SetField(ref GainSchedule t, string name, float v)
    {
        switch (name)
        {
            case nameof(GainSchedule.RefQ): t.RefQ = v; break;
            case nameof(GainSchedule.KpExp): t.KpExp = v; break;
            case nameof(GainSchedule.TiExp): t.TiExp = v; break;
            case nameof(GainSchedule.TdExp): t.TdExp = v; break;
            case nameof(GainSchedule.ClampMin): t.ClampMin = v; break;
            case nameof(GainSchedule.ClampMax): t.ClampMax = v; break;
        }
    }
}
