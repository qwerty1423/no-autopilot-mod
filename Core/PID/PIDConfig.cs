using NOAutopilot.Core.Config;

namespace NOAutopilot.Core.PID;

internal struct PIDConfig
{
    public static void Apply(PIDController pid,
        PIDTuning t, double ts, double minOutput, double maxOutput)
    {
        pid.K = t.Kp;
        pid.Ti = t.Ti;
        pid.Td = t.Td;
        pid.N = t.N;
        pid.Ts = ts;

        pid.B = t.B;
        pid.C = t.C;

        pid.SmoothIn = t.SmoothIn;
        pid.SmoothOut = t.SmoothOut;

        pid.ProportionalDeadband = t.ProportionalDeadband;
        pid.IntegralDeadband = t.IntegralDeadband;
        pid.DerivativeDeadband = t.DerivativeDeadband;
        pid.OutputDeadband = t.OutputDeadband;

        pid.Clegg = t.Clegg;
        pid.MinOutput = minOutput;
        pid.MaxOutput = maxOutput;
    }

    /// <summary>
    /// Same as Apply but scales the gains by dynamic pressure before writing.
    /// </summary>
    public static void ApplyScheduled(
        PIDController pid,
        PIDTuning baseTuning,
        GainSchedule schedule,
        float currentQ,
        double ts,
        double minOutput,
        double maxOutput)
    {
        PIDTuning scheduled = GainScheduler.Schedule(baseTuning, schedule, currentQ);
        Apply(pid, scheduled, ts, minOutput, maxOutput);
    }
}
