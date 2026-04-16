using BepInEx.Configuration;

using UnityEngine;

namespace NOAutopilot.Core.Config;

public static class PIDTuningDrawer
{
    private const float F = 52f;
    private const int Cols = 3;

    private static readonly (string Label, string Tooltip, string Field)[] Cells =
    [
        ("Kp", "Proportional gain",
        nameof(PIDTuning.Kp)),
        ("Ti", "Integral time",
        nameof(PIDTuning.Ti)),
        ("Td", "Derivative time",
        nameof(PIDTuning.Td)),
        ("N", "First-order derivative filter divisor",
        nameof(PIDTuning.N)),
        ("B", "Setpoint weighting on P term",
        nameof(PIDTuning.B)),
        ("C", "Setpoint weighting on D term",
        nameof(PIDTuning.C)),
        ("smI",  "Input smoothing (0-1), lower = more smoothing",
        nameof(PIDTuning.SmoothIn)),
        ("smO", "Output smoothing (0-1), lower = more smoothing",
        nameof(PIDTuning.SmoothOut)),
        ("pDb", "Proportional deadband",
        nameof(PIDTuning.ProportionalDeadband)),
        ("iDb", "Integral deadband (they say this is the most useful?)",
        nameof(PIDTuning.IntegralDeadband)),
        ("dDb", "Derivative deadband",
        nameof(PIDTuning.DerivativeDeadband)),
        ("oDb", "Output deadband",
        nameof(PIDTuning.OutputDeadband)),
    ];

    // Per-column label widths
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
                var content = new GUIContent(Cells[i].Label);
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
        var t = (PIDTuning)entry.BoxedValue;
        bool changed = false;

        // Force recalc if skin changes (e.g. first frame)
        if (s_colWidths == null)
        {
            _ = ColWidths;
        }

        GUILayout.BeginVertical();

        int i = 0;
        while (i < Cells.Length)
        {
            GUILayout.BeginHorizontal();
            for (int col = 0; col < Cols && i < Cells.Length; col++, i++)
            {
                var (label, tooltip, fieldName) = Cells[i];
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

        GUILayout.BeginHorizontal();
        var cleggContent = new GUIContent("Clegg",
            "Clegg integrator — resets integral on every zero-crossing of error");
        bool newClegg = GUILayout.Toggle(t.Clegg, cleggContent);
        if (newClegg != t.Clegg) { t.Clegg = newClegg; changed = true; }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

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

    private static float GetField(ref PIDTuning t, string name) => name switch
    {
        nameof(PIDTuning.Kp) => t.Kp,
        nameof(PIDTuning.Ti) => t.Ti,
        nameof(PIDTuning.Td) => t.Td,
        nameof(PIDTuning.N) => t.N,
        nameof(PIDTuning.B) => t.B,
        nameof(PIDTuning.C) => t.C,
        nameof(PIDTuning.SmoothIn) => t.SmoothIn,
        nameof(PIDTuning.SmoothOut) => t.SmoothOut,
        nameof(PIDTuning.ProportionalDeadband) => t.ProportionalDeadband,
        nameof(PIDTuning.IntegralDeadband) => t.IntegralDeadband,
        nameof(PIDTuning.DerivativeDeadband) => t.DerivativeDeadband,
        nameof(PIDTuning.OutputDeadband) => t.OutputDeadband,
        _ => 0f,
    };

    private static void SetField(ref PIDTuning t, string name, float v)
    {
        switch (name)
        {
            case nameof(PIDTuning.Kp): t.Kp = v; break;
            case nameof(PIDTuning.Ti): t.Ti = v; break;
            case nameof(PIDTuning.Td): t.Td = v; break;
            case nameof(PIDTuning.N): t.N = v; break;
            case nameof(PIDTuning.B): t.B = v; break;
            case nameof(PIDTuning.C): t.C = v; break;
            case nameof(PIDTuning.SmoothIn): t.SmoothIn = v; break;
            case nameof(PIDTuning.SmoothOut): t.SmoothOut = v; break;
            case nameof(PIDTuning.ProportionalDeadband): t.ProportionalDeadband = v; break;
            case nameof(PIDTuning.IntegralDeadband): t.IntegralDeadband = v; break;
            case nameof(PIDTuning.DerivativeDeadband): t.DerivativeDeadband = v; break;
            case nameof(PIDTuning.OutputDeadband): t.OutputDeadband = v; break;
        }
    }
}
