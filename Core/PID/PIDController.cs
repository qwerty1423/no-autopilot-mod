/*
 * Combined form 2DOF PIDF controller
 */

using static System.Math;

namespace NOAutopilot.Core.PID;

public class PIDController : IPIDLoop
{
    // internal state for PID filter
    public double PTerm { get; private set; }
    public double ITerm { get; private set; }
    public double DTerm { get; private set; }
    // standard form parameters 
    /// <summary>Proportional gain.</summary>
    public double K { get; set; } = 1.0;

    /// <summary>Integral time.</summary>
    public double Ti { get; set; }

    /// <summary>Derivative time.</summary>
    public double Td { get; set; }

    /// <summary>Derivative filter divisor.</summary>
    public double N { get; set; } = 50;

    /// <summary>Sample time.</summary>
    public double Ts { get; set; } = 0.02;

    // parallel form parameters
    public double Kp { get => K; set => K = value; }
    public double Ki { get => Ti == 0 ? 0 : K / Ti; set => Ti = value == 0 ? 0 : K / value; }
    public double Kd { get => Td * K; set => Td = K == 0 ? 0 : value / K; }
    public double Tf { get => N == 0 ? 0 : Td / N; set => N = value == 0 ? 0 : Td / value; }

    // 2DOF parameters
    /// <summary>Setpoint weight on proportional term.</summary>
    public double B { get; set; } = 1.0;

    /// <summary>Setpoint weight on derivative term.</summary>
    public double C { get; set; } = 1.0;

    /// <summary>Bias for position form.</summary>
    public double U0 { get; set; }

    //  Anti-windup 
    /// <summary>Anti-windup tracking time constant. If 0, defaults to sqrt(Ti*Td) or Ti.</summary>
    public double Tt { get; set; }

    //  Saturation and rate limits 
    public double MinOutput { get; set; } = double.MinValue;
    public double MaxOutput { get; set; } = double.MaxValue;

    /// <summary>Minimum rate of change of output per second.</summary>
    public double DuMin { get; set; } = double.NegativeInfinity;

    /// <summary>Maximum rate of change of output per second.</summary>
    public double DuMax { get; set; } = double.PositiveInfinity;

    //  Optional extensions 
    /// <summary>Input exponential smoothing factor.</summary>
    public double SmoothIn { get; set; } = 1.0;

    /// <summary>Output exponential smoothing factor.</summary>
    public double SmoothOut { get; set; } = 1.0;

    public double ProportionalDeadband { get; set; }
    public double IntegralDeadband { get; set; }
    public double DerivativeDeadband { get; set; }
    public double OutputDeadband { get; set; }

    /// <summary>Reset integrator when error crosses zero.</summary>
    public bool Clegg { get; set; }

    //  Second-order filter
    /// <summary>Time constant for the second-order signal filter.</summary>
    public double FilterTf { get; set; }

    //  Private state 

    // Previous signals
    private double _r1, _y1Ctrl;
    private double _dr1, _dy1;
    private double _uff1;

    // Incremental-form accumulated output (xu) and last saturated output (xus)
    private double _xu;
    private double _xus;

    // previous errors
    private double _ei1;
    private double _ed1;
    private double _dterm1;

    // Low-pass filter states
    private double _yIn1 = double.NaN;
    private double _uOut1 = double.NaN;

    // Second-order filter states
    private double _sf1 = double.NaN;
    private double _sf2 = double.NaN;

    private bool _initialized;

    // Initialization
    /// <summary>
    /// Initialize (or re-initialize) the controller state so that the first call
    /// to <see cref="Update"/> produces a bumpless start from the given operating
    /// point.
    /// </summary>
    /// <param name="r0">Initial setpoint.</param>
    /// <param name="y0">Initial process variable.</param>
    /// <param name="u0">Initial control output (null → <see cref="U0"/>).</param>
    /// <param name="uff0">Initial feedforward signal.</param>
    public void Init(double r0 = 0, double y0 = 0, double? u0 = null, double uff0 = 0)
    {
        double uInit = u0 ?? U0;

        _r1 = r0;
        _y1Ctrl = y0;
        _dr1 = 0;
        _dy1 = 0;
        _uff1 = uff0;

        _xu = uInit;
        _xus = uInit;

        PTerm = 0;
        ITerm = 0;
        DTerm = 0;
        _ed1 = (C * r0) - y0;
        _ei1 = 0;
        _dterm1 = 0;

        _yIn1 = y0;
        _uOut1 = uInit;

        _sf1 = y0;
        _sf2 = y0;

        _initialized = true;
    }

    //  Reset

    /// <summary>Full reset of all internal state.</summary>
    public void Reset()
    {
        PTerm = ITerm = DTerm = 0;
        _r1 = _y1Ctrl = _dr1 = _dy1 = _uff1 = 0;
        _xu = _xus = 0;
        _ed1 = _dterm1 = 0;
        _yIn1 = _uOut1 = double.NaN;
        _sf1 = _sf2 = double.NaN;
        _initialized = false;
    }

    /// <summary>
    /// Soft reset of integrator-like states (derivative memory, feedforward
    /// memory) while preserving accumulated output to avoid bumps.
    /// </summary>
    public void ResetIntegratorLikeStates()
    {
        _dr1 = 0;
        _dy1 = 0;
        _uff1 = 0;
    }

    //  Main control update

    /// <summary>
    /// Compute control action in automatic mode.
    /// </summary>
    public double Update(double r, double y) =>
        Update(r, y, 0, 0, 0, Mode.Auto);

    /// <summary>
    /// Compute control action in the specified operating mode.
    /// </summary>
    /// <param name="r">Setpoint (reference).</param>
    /// <param name="y">Process variable (measurement), assumed pre-filtered if needed.</param>
    /// <param name="uff">Feedforward signal (0 if unused).</param>
    /// <param name="uman">Manual output (used when mode == MAN).</param>
    /// <param name="utrack">Tracking signal (used when mode == TRACK).</param>
    /// <param name="mode">Operating mode: Auto, Man, or Track.</param>
    /// <returns>Control output after saturation, rate limiting, and smoothing.</returns>
    public double Update(double r, double y, double uff, double uman, double utrack, Mode mode)
    {
        // Auto-init on first call 
        if (!_initialized)
        {
            Init(r, y, mode == Mode.Man ? uman : mode == Mode.Track ? utrack : U0, uff);
        }

        // Low-pass filter input 
        y = IsFinite(_yIn1) ? _yIn1 + (SmoothIn * (y - _yIn1)) : y;
        _yIn1 = y;

        // Rate limits relative to previous saturated output 
        double umin = Max(MinOutput, _xus + (Ts * DuMin));
        double umax = Min(MaxOutput, _xus + (Ts * DuMax));

        // Compute derivative approximations 
        double dr = (r - _r1) / Ts;
        double dy = (y - _y1Ctrl) / Ts;

        double u;

        if (mode == Mode.Track)
        {
            // Tracking mode
            _xu = utrack;
            u = utrack;
            _ei1 = ApplyDeadband(r - y, IntegralDeadband);
            _ed1 = ApplyDeadband((C * r) - y, DerivativeDeadband);
            _dterm1 = 0;
            DTerm = 0;
        }
        else if (mode == Mode.Man)
        {
            // Manual mode 
            u = uman;
            _xu = uman;
            _ei1 = ApplyDeadband(r - y, IntegralDeadband);
            _ed1 = ApplyDeadband((C * r) - y, DerivativeDeadband);
            _dterm1 = 0;
            DTerm = 0;
        }
        else if (Ti == 0)
        {
            // Positional form 
            double ep = ApplyDeadband((B * r) - y, ProportionalDeadband);
            double ed = ApplyDeadband((C * r) - y, DerivativeDeadband);

            PTerm = K * ep;

            // Trapezoidal derivative with filter
            double den = (2 * Td) + (N * Ts);
            DTerm = den == 0
                ? 0
                : (((2 * Td) - (N * Ts)) / den * DTerm) + (2 * N * K * Td / den * (ed - _ed1));

            if (!IsFinite(DTerm))
            {
                DTerm = 0;
            }

            _ed1 = ed;

            u = U0 + PTerm + DTerm + uff;
            _xu = u;
        }
        else
        {
            // Incremental form 
            double d_r = r - _r1;
            double d_y = y - _y1Ctrl;
            double d_dr = dr - _dr1;
            double d_dy = dy - _dy1;

            // ── Error signals with deadbands
            double ep = ApplyDeadband((B * r) - y, ProportionalDeadband);
            double ei = ApplyDeadband(r - y, IntegralDeadband);
            double ed = ApplyDeadband((C * r) - y, DerivativeDeadband);

            // Proportional increment
            double ep1 = ApplyDeadband((B * _r1) - _y1Ctrl, ProportionalDeadband);
            double du_p = K * (ep - ep1);

            // Integral increment
            if (Clegg && ei * _ei1 < 0)
            {
                _ei1 = 0;
            }

            double du_i = 0.5 * K / Ti * Ts * (ei + _ei1);

            // Derivative increment
            double den = (2 * Td) + (N * Ts);
            double dDTerm = den == 0
                ? 0
                : (((2 * Td) - (N * Ts)) / den * DTerm) + (2 * N * K * Td / den * (ed - _ed1));

            if (!IsFinite(dDTerm)) dDTerm = 0;
            DTerm = dDTerm;

            // Feedforward increment
            double du_ff = uff - _uff1;

            // Nominal control signal
            u = _xu + du_p + du_i + du_ff + (DTerm - _dterm1);

            // Anti-windup (back-calculation)
            double us = Clamp(u, umin, umax);
            double tr = GetTrackingTimeConstant();
            u -= Ts / tr * (u - us);

            _xu = u;
            _ei1 = ei;
            _ed1 = ed;
            _dterm1 = DTerm;
        }

        // Store pre-saturation signal
        // For TRACK and MAN modes, also align _xu so switching back to AUTO is bumpless.
        _xu = u;

        // Apply output deadband
        u = ApplyDeadband(u, OutputDeadband);

        // Saturate and rate-limit
        u = Clamp(u, umin, umax);
        _xus = u;

        // Low-pass filter the output
        _uOut1 = IsFinite(_uOut1) ? _uOut1 + (SmoothOut * (u - _uOut1)) : u;

        // Update stored states
        _r1 = r;
        _y1Ctrl = y;
        _dr1 = dr;
        _dy1 = dy;
        _uff1 = uff;

        return _uOut1;
    }

    /// <summary>
    /// Second-order signal filter.
    /// Uses <see cref="FilterTf"/> and <see cref="Ts"/>.
    /// </summary>
    /// <param name="y">Raw signal.</param>
    /// <returns>Filtered signal.</returns>
    public double Filter(double y)
    {
        if (!IsFinite(_sf1))
        {
            _sf1 = y;
            _sf2 = y;
            return y;
        }

        double a = Ts / (FilterTf + (0.5 * Ts));
        _sf1 += a * (y - _sf1);
        _sf2 += a * (_sf1 - _sf2);
        return _sf2;
    }

    //  Private helpers

    /// <summary>
    /// Compute the anti-windup tracking time constant.
    /// If Tt is explicitly set (> 0), use it. Otherwise default to
    /// sqrt(Ti * Td) when derivative is active, or Ti otherwise.
    /// </summary>
    private double GetTrackingTimeConstant()
    {
        if (Tt > 0)
        {
            return Tt;
        }

        if (Td > 0 && Ti > 0)
        {
            return Sqrt(Ti * Td);
        }

        if (Ti > 0)
        {
            return Ti;
        }

        return Ts; // fallback to avoid division by zero
    }

    private static double ApplyDeadband(double v, double deadband)
    {
        return Abs(v) < deadband ? 0 : v - (Sign(v) * deadband);
    }

    private static bool IsFinite(double x) =>
        !double.IsNaN(x) && !double.IsInfinity(x);

    private static double Clamp(double x, double min, double max) =>
        x < min ? min : x > max ? max : x;
}

/// <summary>
/// Operating mode for the PID controller.
/// </summary>
public enum Mode
{
    /// <summary>Automatic mode: normal operation.</summary>
    Auto,

    /// <summary>Manual mode: output follows uman.</summary>
    Man,

    /// <summary>Tracking mode: output tracks utrack.</summary>
    Track
}
