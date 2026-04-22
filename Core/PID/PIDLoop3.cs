/*
 * Copyright Lamont Granquist, Sebastien Gaggini and the MechJeb contributors
 * SPDX-License-Identifier: LicenseRef-PD-hp OR Unlicense OR CC0-1.0 OR 0BSD OR MIT-0 OR MIT OR LGPL-2.1+
 *
 * modified from PIDLoop2
 */

// using static MechJebLib.Utils.Statics;
using static System.Math;

// namespace MechJebLib.Control;
namespace NOAutopilot.Core.PID;

//
// 1. 2DOF PIDF controller with derivative filtering
// 2. trapezoidal discretization
// 3. standard and parallel form parameters
// 4. optional input and output deadbands
// 5. optional low pass filtering of input and output
// 6. optional Clegg integrator
// 7. optional conditional integration and tracking anti-windup
// 8. optional output rate limiting
// 9. optional tracking mode and manual mode with bumpless transfer
// 10. optional feedforward input
//
public class PIDLoop3 : IPIDLoop
{
    // internal nominal and saturated output state
    private double _xu = double.NaN;  // pre-saturation accumulated output
    // private double _xus = double.NaN;  // post-saturation output (for rate limiting)

    // internal state for previous P state (for incremental P)
    private double _p1 = double.NaN;

    // internal state for previous reference and measurement
    // private double _r1 = double.NaN;
    private double _y1 = double.NaN;

    // internal state for last error
    private double _ei1;
    private double _ed1;

    // internal state for derivative filter
    private double _d1;

    // internal state for previous feedforward (for increment)
    private double _uff1 = double.NaN;

    // internal state for output low pass filter
    private double _u1 = double.NaN;

    // internal state for PID filter
    public double PTerm { get; private set; }
    public double ITerm { get; private set; }
    public double DTerm { get; private set; }

    // standard form parameters
    public double K { get; set; } = 1.0;
    public double Ti { get; set; }
    public double Td { get; set; }
    public double N { get; set; } = 50;
    public double Ts { get; set; } = 0.02;

    // parallel form parameters
    public double Kp { set => K = value; get => K; } // TODO: rescale Ti and Td to keep Ki and Kd constant
    public double Ki { set => Ti = K / value; get => K / Ti; }
    public double Kd { set => Td = value / K; get => Td * K; }
    public double Tf { set => N = Td / value; get => Td / N; }

    // 2DOF PIDF parameters
    public double B { get; set; } = 1;
    public double C { get; set; } = 1;

    // optional extensions
    public double SmoothIn { get; set; } = 1.0;
    public double SmoothOut { get; set; } = 1.0;

    // deadbands
    public double ProportionalDeadband { get; set; }
    public double IntegralDeadband { get; set; } // probably the most useful deadband and necessary
    public double DerivativeDeadband { get; set; }
    public double OutputDeadband { get; set; }

    // output limits
    public double MinOutput { get; set; } = double.MinValue;
    public double MaxOutput { get; set; } = double.MaxValue;

    // output rate limits
    // public double MinRate { get; set; } = double.MinValue;
    // public double MaxRate { get; set; } = double.MaxValue;

    // anti-windup tracking time constant
    public double Tt { get; set; }

    public bool Clegg { get; set; } // not recommended if integrator required to zero the setpoint

    public bool TrackingMode { get; set; }
    public double TrackingValue { get; set; }

    public bool ManualMode { get; set; }
    public double ManualValue { get; set; }

    public double Feedforward { get; set; }

    public bool BumplessTransfer { get; set; } = true;

    public double Update(double r, double y)
    {
        // low-pass filter the input
        y = IsFinite(_y1) ? _y1 + (SmoothIn * (y - _y1)) : y;


        // recover previous reference 
        // double rPrev = IsFinite(_r1) ? _r1 : r;
        double uffPrev = IsFinite(_uff1) ? _uff1 : Feedforward;

        if (ManualMode)
        {
            double um = Clamp(ManualValue, MinOutput, MaxOutput);

            if (BumplessTransfer)
            {
                // keep internal state tracking the manual output for bumpless exit
                _xu = um;
                // _xus = um;
                _p1 = K * ApplyDeadband((B * r) - y, ProportionalDeadband);
                _d1 = 0;
                PTerm = _p1;
                ITerm = 0;
                DTerm = 0;
                _y1 = y;
                // _r1 = r;
                _ei1 = ApplyDeadband(r - y, IntegralDeadband);
                _ed1 = ApplyDeadband((C * r) - y, DerivativeDeadband);
                _uff1 = Feedforward;
                _u1 = IsFinite(_u1) ? _u1 + (SmoothOut * (um - _u1)) : um;
            }
            return _u1;
        }

        if (TrackingMode)
        {
            _xu = TrackingValue;
            // _xus = TrackingValue;
        }

        // rate-limited saturation window
        // double usMin = double.MinValue;
        // double usMax = double.MaxValue;

        // double xus = IsFinite(_xus) ? _xus : 0;

        // if (MinRate > double.MinValue / 2)
        //     usMin = Max(usMin, xus + Ts * MinRate);
        // if (MaxRate < double.MaxValue / 2)
        //     usMax = Min(usMax, xus + Ts * MaxRate);

        // error signals with deadbands
        double ep = ApplyDeadband((B * r) - y, ProportionalDeadband);
        double ei = ApplyDeadband(r - y, IntegralDeadband);
        double ed = ApplyDeadband((C * r) - y, DerivativeDeadband);

        // first-run initialization
        bool firstRun = !IsFinite(_xu);
        if (firstRun)
        {
            _xu = 0;
            // _xus = 0;
            _p1 = K * ep;
            _ei1 = ei;
            _ed1 = ed;
            _d1 = 0;
        }

        if (Clegg && ei * ITerm < 0)
        {
            _xu -= ITerm;
            ITerm = 0;
        }

        // proportional increment
        double pNew = K * ep;
        double dup = pNew - _p1;

        // Trapezoidal/Tustin/Bilinear integrator term
        double k = K == 0 ? 1 : K;
        double dui = 0.5 * k * Ts * (ei + _ei1) / Ti;
        if (!IsFinite(dui))
        {
            dui = 0;
        }

        // Trapezoidal/Tustin/Bilinear derivative term
        double den = (2 * Td) + (N * Ts);
        double dNew2 = (((2 * Td) - (N * Ts)) / den * _d1) + (2 * N * K * Td / den * (ed - _ed1));
        if (!IsFinite(dNew2))
        {
            dNew2 = 0;
        }

        double dud = dNew2 - _d1;

        // feedforward increment 
        double duff = Feedforward - uffPrev;

        // nominal control signal 
        double u = _xu + dup + dui + dud + duff;

        // conditional integration anti-windup
        double us = Clamp(u, MinOutput, MaxOutput);
        if (Ti != 0 && dui * (u - us) > 0)
        {
            u -= Sign(u - us) * Min(Abs(dui), Abs(u - us));
            us = Clamp(u, MinOutput, MaxOutput);
        }

        // tracking anti-windup 
        if (Ti != 0)
        {
            double tr = Tt > 0 ? Tt : (Td == 0 ? Ti : Sqrt(Ti * Td));
            u -= Min(Ts / tr, 1.0) * (u - us);
        }

        // save pre-saturation nominal state 
        _xu = u;

        // output deadband and final saturation 
        u = ApplyDeadband(u, OutputDeadband);
        u = Clamp(u, MinOutput, MaxOutput);
        // _xus = u;

        // reconstruct PID components for telemetry 
        PTerm = pNew;
        _d1 = dNew2;
        DTerm = _d1;
        ITerm = _xu - PTerm - DTerm - Feedforward;
        if (!IsFinite(ITerm))
        {
            ITerm = 0;
        }

        // output low-pass filter 
        _u1 = IsFinite(_u1) ? _u1 + (SmoothOut * (u - _u1)) : u;

        // state update 
        _p1 = pNew;
        _y1 = y;
        // _r1 = r;
        _ei1 = ei;
        _ed1 = ed;
        _uff1 = Feedforward;

        return _u1;
    }

    private double ApplyDeadband(double v, double deadband)
    {
        return Abs(v) < deadband ? 0 : v - (Sign(v) * deadband);
    }

    public void Reset()
    {
        PTerm = ITerm = DTerm = 0;
        _ei1 = _ed1 = _d1 = 0;
        _p1 = double.NaN;
        _y1 = _u1 = _xu = _uff1 = double.NaN; // _r1 = _xus =
        TrackingMode = false;
        ManualMode = false;
    }

    // seeds the controller's nominal output state
    // use this for bumpless enable from a known output value
    public void SeedOutput(double value)
    {
        _xu = value;
        // _xus = value;
    }

    // seeds only the integrator component, leaving P and D state alone
    // only meaningful after at least one Update() call
    public void SeedIntegral(double value)
    {
        double pTerm = IsFinite(_p1) ? _p1 : 0;
        _xu = pTerm + value + DTerm + Feedforward;
        // _xus = Clamp(_xu, MinOutput, MaxOutput);
        ITerm = value;
    }

    // functions from MechJebLib.Utils.Statics to make it work
    private static bool IsFinite(double x) =>
        !double.IsNaN(x) && !double.IsInfinity(x);

    private static double Clamp(double x, double min, double max) =>
        x < min ? min : x > max ? max : x;
}
