using System.Globalization;

using JetBrains.Annotations;

namespace NOAutopilot.Core.Config;

public struct PIDTuning(
    float kp, float ki, float kd,
    float n = 50f,
    float b = 1f,
    float c = 0f,
    float smoothIn = 1f,
    float smoothOut = 1f,
    float proportionalDeadband = 0f,
    float integralDeadband = 0f,
    float derivativeDeadband = 0f,
    float outputDeadband = 0f,
    bool clegg = false)
{
    // parallel form
    public float Kp = kp, Ki = ki, Kd = kd;
    // filter
    public float N = n;
    // 2DOF
    public float B = b, C = c;
    // smoothing
    public float SmoothIn = smoothIn, SmoothOut = smoothOut;
    // deadbands
    public float ProportionalDeadband = proportionalDeadband, IntegralDeadband = integralDeadband, DerivativeDeadband = derivativeDeadband, OutputDeadband = outputDeadband;
    // optional
    public bool Clegg = clegg;

    public override string ToString()
    {
        var ci = CultureInfo.InvariantCulture;
        return string.Join("|",
            Kp.ToString(ci),
            Ki.ToString(ci),
            Kd.ToString(ci),
            N.ToString(ci),
            B.ToString(ci),
            C.ToString(ci),
            SmoothIn.ToString(ci),
            SmoothOut.ToString(ci),
            ProportionalDeadband.ToString(ci),
            IntegralDeadband.ToString(ci),
            DerivativeDeadband.ToString(ci),
            OutputDeadband.ToString(ci),
            Clegg ? "1" : "0");
    }

    public static PIDTuning Parse(string s)
    {
        var ci = CultureInfo.InvariantCulture;
        var p = s.Split('|');
        var t = new PIDTuning();

        float Get(int i, float def) =>
            p.Length > i && float.TryParse(p[i], NumberStyles.Float, ci, out float v) ? v : def;

        t.Kp = Get(0, 1f);
        t.Ki = Get(1, 0f);
        t.Kd = Get(2, 0f);
        t.N = Get(3, 50f);
        t.B = Get(4, 1f);
        t.C = Get(5, 0f);
        t.SmoothIn = Get(6, 1f);
        t.SmoothOut = Get(7, 1f);
        t.ProportionalDeadband = Get(8, 0f);
        t.IntegralDeadband = Get(9, 0f);
        t.DerivativeDeadband = Get(10, 0f);
        t.OutputDeadband = Get(11, 0f);
        t.Clegg = p.Length > 12 && p[12] == "1";

        return t;
    }
}
