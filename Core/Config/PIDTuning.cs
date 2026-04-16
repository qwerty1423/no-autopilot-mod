using System.Globalization;

namespace NOAutopilot.Core.Config;

public struct PIDTuning(
    double kp, double ti, double td,
    double n = 50,
    double b = 1,
    double c = 0,
    double smoothIn = 1,
    double smoothOut = 1,
    double proportionalDeadband = 0,
    double integralDeadband = 0,
    double derivativeDeadband = 0,
    double outputDeadband = 0,
    bool clegg = false)
{
    // standard form
    public double Kp = kp, Ti = ti, Td = td;
    // filter
    public double N = n;
    // 2DOF
    public double B = b, C = c;
    // smoothing
    public double SmoothIn = smoothIn, SmoothOut = smoothOut;
    // deadbands
    public double ProportionalDeadband = proportionalDeadband, IntegralDeadband = integralDeadband, DerivativeDeadband = derivativeDeadband, OutputDeadband = outputDeadband;
    // optional
    public bool Clegg = clegg;

    public override readonly string ToString()
    {
        var ci = CultureInfo.InvariantCulture;
        return string.Join("|",
            Kp.ToString(ci),
            Ti.ToString(ci),
            Td.ToString(ci),
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

        double Get(int i, double def) =>
            p.Length > i && double.TryParse(p[i], NumberStyles.Float, ci, out double v) ? v : def;

        t.Kp = Get(0, 1);
        t.Ti = Get(1, 0);
        t.Td = Get(2, 0);
        t.N = Get(3, 50);
        t.B = Get(4, 1);
        t.C = Get(5, 0);
        t.SmoothIn = Get(6, 1);
        t.SmoothOut = Get(7, 1);
        t.ProportionalDeadband = Get(8, 0);
        t.IntegralDeadband = Get(9, 0);
        t.DerivativeDeadband = Get(10, 0);
        t.OutputDeadband = Get(11, 0);
        t.Clegg = p.Length > 12 && p[12] == "1";

        return t;
    }
}
