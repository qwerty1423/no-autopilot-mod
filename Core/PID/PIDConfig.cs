namespace NOAutopilot.Core.PID;

internal struct PIDConfig
{
    private float _kp, _ki, _kd, _n;
    private float _minOutput, _maxOutput;
    private float _b, _c;
    private float _smoothIn, _smoothOut;
    private float _integralDeadband;
    private float _ts;

    public static void Apply(ref PIDConfig cfg, PIDLoop2 pid,
        float kp, float ki, float kd, float n,
        float minOutput, float maxOutput,
        float b, float c,
        float smoothIn, float smoothOut,
        float integralDeadband,
        float ts)
    {
        bool tsChanged = System.Math.Abs(ts - cfg._ts) > 1e-6f;

        if (!tsChanged &&
            kp == cfg._kp && ki == cfg._ki && kd == cfg._kd && n == cfg._n &&
            minOutput == cfg._minOutput && maxOutput == cfg._maxOutput &&
            b == cfg._b && c == cfg._c &&
            smoothIn == cfg._smoothIn && smoothOut == cfg._smoothOut &&
            integralDeadband == cfg._integralDeadband)
        {
            return;
        }

        cfg._kp = kp; cfg._ki = ki; cfg._kd = kd; cfg._n = n;
        cfg._minOutput = minOutput; cfg._maxOutput = maxOutput;
        cfg._b = b; cfg._c = c;
        cfg._smoothIn = smoothIn; cfg._smoothOut = smoothOut;
        cfg._integralDeadband = integralDeadband;
        cfg._ts = ts;

        pid.Kp = kp;
        pid.Ti = ki > 1e-8f ? kp / ki : 1e12;
        pid.Td = kp > 1e-8f ? kd / kp : 0;
        pid.N = n;
        pid.MinOutput = minOutput;
        pid.MaxOutput = maxOutput;
        pid.B = b;
        pid.C = c;
        pid.SmoothIn = smoothIn;
        pid.SmoothOut = smoothOut;
        pid.ProportionalDeadband = 0;
        pid.IntegralDeadband = integralDeadband;
        pid.DerivativeDeadband = 0;
        pid.OutputDeadband = 0;
        pid.Ts = ts;
    }
}
