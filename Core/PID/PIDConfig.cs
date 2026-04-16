using NOAutopilot.Core.Config;

namespace NOAutopilot.Core.PID;

internal struct PIDConfig
{
    private float _kp, _ti, _td;
    private float _n;
    private float _b, _c;
    private float _smoothIn, _smoothOut;
    private float _proportionalDeadband, _integralDeadband, _derivativeDeadband, _outputDeadband;
    private bool _clegg;
    private float _minOutput, _maxOutput;
    private float _ts;

    public static void Apply(ref PIDConfig cfg, PIDLoop2 pid,
        PIDTuning t,
        float ts,
        float minOutput, float maxOutput)
    {
        float kp = t.Kp, ti = t.Ti, td = t.Td;
        float n = t.N;
        float b = t.B, c = t.C;
        float smoothIn = t.SmoothIn;
        float smoothOut = t.SmoothOut;
        float pDb = t.ProportionalDeadband;
        float iDb = t.IntegralDeadband;
        float dDb = t.DerivativeDeadband;
        float oDb = t.OutputDeadband;
        bool clegg = t.Clegg;

        bool tsChanged = System.Math.Abs(ts - cfg._ts) > 1e-6f;

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
        // optional
        pid.Clegg = clegg;
        pid.MinOutput = minOutput;
        pid.MaxOutput = maxOutput;
    }
}
