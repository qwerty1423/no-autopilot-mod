using NOAutopilot.Core.Config;

namespace NOAutopilot.Core.PID;

internal struct PIDConfig
{
    private double _kp, _ti, _td;
    private double _n;
    private double _b, _c;
    private double _smoothIn, _smoothOut;
    private double _proportionalDeadband, _integralDeadband, _derivativeDeadband, _outputDeadband;
    private double _minRate, _maxRate;
    private double _tt;
    private bool _clegg;
    private double _minOutput, _maxOutput;
    private double _ts;

    public static void Apply(ref PIDConfig cfg, PIDLoop3 pid,
        PIDTuning t,
        double ts,
        double minOutput, double maxOutput)
    {
        double kp = t.Kp, ti = t.Ti, td = t.Td;
        double n = t.N;
        double b = t.B, c = t.C;
        double smoothIn = t.SmoothIn;
        double smoothOut = t.SmoothOut;
        double pDb = t.ProportionalDeadband;
        double iDb = t.IntegralDeadband;
        double dDb = t.DerivativeDeadband;
        double oDb = t.OutputDeadband;
        double minR = t.MinRate;
        double maxR = t.MaxRate;
        double tt = t.Tt;
        bool clegg = t.Clegg;

        bool tsChanged = System.Math.Abs(ts - cfg._ts) > 1e-6;

        if (!tsChanged &&
            kp == cfg._kp && ti == cfg._ti && td == cfg._td &&
            n == cfg._n &&
            minOutput == cfg._minOutput && maxOutput == cfg._maxOutput &&
            b == cfg._b && c == cfg._c &&
            smoothIn == cfg._smoothIn && smoothOut == cfg._smoothOut &&
            pDb == cfg._proportionalDeadband &&
            iDb == cfg._integralDeadband &&
            dDb == cfg._derivativeDeadband &&
            oDb == cfg._outputDeadband &&
            minR == cfg._minRate &&
            maxR == cfg._maxRate &&
            tt == cfg._tt &&
            clegg == cfg._clegg)
        {
            return;
        }

        cfg._kp = kp; cfg._ti = ti; cfg._td = td;
        cfg._n = n;
        cfg._minOutput = minOutput; cfg._maxOutput = maxOutput;
        cfg._b = b; cfg._c = c;
        cfg._smoothIn = smoothIn; cfg._smoothOut = smoothOut;
        cfg._proportionalDeadband = pDb;
        cfg._integralDeadband = iDb;
        cfg._derivativeDeadband = dDb;
        cfg._outputDeadband = oDb;
        cfg._minRate = minR;
        cfg._maxRate = maxR;
        cfg._tt = tt;
        cfg._clegg = clegg;
        cfg._ts = ts;

        // standard form
        pid.K = kp;
        pid.Ti = ti;
        pid.Td = td;
        pid.N = n;
        pid.Ts = ts;
        // 2DOF
        pid.B = b;
        pid.C = c;
        // smoothing
        pid.SmoothIn = smoothIn;
        pid.SmoothOut = smoothOut;
        // deadbands
        pid.ProportionalDeadband = pDb;
        pid.IntegralDeadband = iDb;
        pid.DerivativeDeadband = dDb;
        pid.OutputDeadband = oDb;
        pid.MinRate = minR;
        pid.MaxRate = maxR;
        // optional
        pid.Tt = tt;
        pid.Clegg = clegg;
        pid.MinOutput = minOutput;
        pid.MaxOutput = maxOutput;
    }
}
