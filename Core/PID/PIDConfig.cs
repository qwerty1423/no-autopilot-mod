using NOAutopilot.Core.Config;

namespace NOAutopilot.Core.PID;

internal struct PIDConfig
{
    private float _kp, _ki, _kd;
    private float _n;
    private float _b, _c;
    private float _smoothIn, _smoothOut;
    private float _proportionalDeadband, _integralDeadband, _derivativeDeadband, _outputDeadband;
    private bool _clegg;
    private float _minOutput, _maxOutput;
    private float _ts;

    public static void Apply(ref PIDConfig cfg, PIDLoop2 pid,
        PIDTuning t,
        float minOutput, float maxOutput,
        float ts)
    {
        float kp = t.Kp, ki = t.Ki, kd = t.Kd;
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
            kp == cfg._kp && ki == cfg._ki && kd == cfg._kd &&
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

        cfg._kp = kp; cfg._ki = ki; cfg._kd = kd;
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

        // standard form: K, Ti, Td, N
        pid.K = kp;
        pid.Ti = ki > 1e-12f ? kp / ki : 1e12;
        pid.Td = kp > 1e-12f ? kd / kp : 0;
        pid.N = n;
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
        pid.Ts = ts;
    }
}
