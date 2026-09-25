using System;
using System.Threading;

using UnityEngine;

namespace NOAutopilot.Core.Guidance.Optim;

/// <summary>
/// Inputs for one vertical-profile optimization: altitudes on a grid along the planned ground track with
/// slope and curvature limits, an optional desired profile and optional hard pins. Everything is convex,
/// so one QP solve gives the globally optimal smooth profile.
/// </summary>
public sealed class ProfileSpec
{
    public int N;                  // grid points (>= 4)
    public float Ds;               // grid spacing along track (m)
    public float[] Lower = null;   // per-point hard lower bound; null entries = none
    public float[] Desired = null; // per-point desired altitude; null entries = no tracking
    public float[] Pin = null;     // per-point hard altitude; NaN = no pin
    public float PinTolerance = 1f;

    public float MaxSlopeUp = 0.4f;    // dh/ds
    public float MaxSlopeDown = -0.5f; // negative
    public float CurvMax = 5e-4f;      // d2h/ds2 (pull-up limit)
    public float CurvMin = -5e-4f;     // push limit

    public float StartAlt = float.NaN;   // pin for h[0]
    public float StartSlope = float.NaN; // pin for (h1-h0)/ds
    public float StartWeight = 400f;
    public float TrackWeight = 1f;   // weight on (h - desired)
    public float TrackScale = 20f;   // metres of error that count as "1"
    public float SlopeWeight = 0f;   // weight on slope^2
    public float SlopeScale = 0.3f;
    public float CaptureLength = 0f; // >0: sets SlopeWeight for exponential error decay
    public float SmoothWeight = 2f;  // weight on curvature^2

    /// <summary>Curvature scale for normalizing the smoothness weight.</summary>
    public float CurvScale => Mathf.Max(Mathf.Abs(CurvMax), 1e-6f) * Ds * Ds;
}

public static class VerticalProfile
{
    /// <summary>
    /// Plans the profile into <paramref name="h"/>, which may hold a warm start.
    /// Returns true when the solver converged with small residuals.
    /// </summary>
    public static bool Plan(ProfileSpec spec, float[] h)
    {
        int n = spec.N;
        if (n < 4 || h.Length < n)
        {
            return false;
        }

        bool hasStart = !float.IsNaN(spec.StartAlt);
        bool hasSlope = !float.IsNaN(spec.StartSlope);
        bool hasTrack = spec.Desired != null;
        bool hasPin = spec.Pin != null;

        int pinCount = 0;
        if (hasPin)
        {
            for (int i = 0; i < n; i++)
            {
                if (!float.IsNaN(spec.Pin[i]))
                {
                    pinCount++;
                }
            }
        }

        // rows: slope, bounds, curvature, pins, start
        int m = (n - 1) + n + (n - 2) + pinCount + (hasStart ? 1 : 0) + (hasSlope ? 1 : 0);
        var qp = new Qp(n, m);
        float invDs = 1f / spec.Ds;

        // ---- cost: tracking + smoothness (+ slope energy) ----
        float ts = Mathf.Max(spec.TrackScale, 1f);
        float cs = spec.CurvScale;
        float ss = Mathf.Max(spec.SlopeScale, 1e-3f);
        for (int i = 0; i < n; i++)
        {
            if (hasTrack && !float.IsNaN(spec.Desired[i]))
            {
                float w = spec.TrackWeight / (ts * ts);
                qp.P[i, i] += w;
                qp.Q[i] += -w * spec.Desired[i];
            }
        }

        // smoothness: sum (h[i+1]-2h[i]+h[i-1])^2 / cs^2
        float wS = spec.SmoothWeight / (cs * cs);
        for (int i = 1; i < n - 1; i++)
        {
            qp.P[i - 1, i - 1] += wS;
            qp.P[i - 1, i] += -2f * wS;
            qp.P[i - 1, i + 1] += wS;
            qp.P[i, i - 1] += -2f * wS;
            qp.P[i, i] += 4f * wS;
            qp.P[i, i + 1] += -2f * wS;
            qp.P[i + 1, i - 1] += wS;
            qp.P[i + 1, i] += -2f * wS;
            qp.P[i + 1, i + 1] += wS;
        }

        // slope energy on the error slope: free while following the desired gradients, so captures stay exponential
        if (spec.SlopeWeight > 0f)
        {
            float wE = spec.SlopeWeight / (ss * ss) * invDs * invDs;
            for (int i = 0; i < n - 1; i++)
            {
                qp.P[i, i] += wE;
                qp.P[i, i + 1] += -wE;
                qp.P[i + 1, i] += -wE;
                qp.P[i + 1, i + 1] += wE;
                if (hasTrack && !float.IsNaN(spec.Desired[i]) && !float.IsNaN(spec.Desired[i + 1]))
                {
                    float dDes = spec.Desired[i + 1] - spec.Desired[i];
                    qp.Q[i] += wE * dDes;
                    qp.Q[i + 1] += -wE * dDes;
                }
            }
        }

        // start altitude and slope are HARD: the plan is a committed trajectory flown from the current
        // state - a soft start lets the plan begin mid-dive that the aircraft cannot follow, and the
        // resulting tracking error cascades into replan churn

        // ---- constraints ----
        int row = 0;
        for (int i = 0; i < n - 1; i++)
        {
            // slope limits
            qp.A[row, i] = -1f;
            qp.A[row, i + 1] = 1f;
            qp.Lo[row] = spec.MaxSlopeDown * spec.Ds;
            qp.Hi[row] = spec.MaxSlopeUp * spec.Ds;
            row++;

            // terrain lower bound: h[i+1] >= lower
            if (spec.Lower != null && !float.IsNaN(spec.Lower[i + 1]))
            {
                qp.A[row, i + 1] = 1f;
                qp.Lo[row] = spec.Lower[i + 1];
                qp.Hi[row] = float.PositiveInfinity;
                row++;
            }
        }

        // h[0] terrain bound
        if (spec.Lower != null && !float.IsNaN(spec.Lower[0]))
        {
            qp.A[row, 0] = 1f;
            qp.Lo[row] = spec.Lower[0];
            qp.Hi[row] = float.PositiveInfinity;
            row++;
        }

        // curvature limits
        for (int i = 1; i < n - 1; i++)
        {
            qp.A[row, i - 1] = 1f;
            qp.A[row, i] = -2f;
            qp.A[row, i + 1] = 1f;
            qp.Lo[row] = spec.CurvMin * spec.Ds * spec.Ds;
            qp.Hi[row] = spec.CurvMax * spec.Ds * spec.Ds;
            row++;
        }

        // hard start rows
        if (hasStart)
        {
            qp.A[row, 0] = 1f;
            qp.Lo[row] = spec.StartAlt - 2f;
            qp.Hi[row] = spec.StartAlt + 2f;
            row++;
        }
        if (hasSlope)
        {
            float t = spec.StartSlope * spec.Ds;
            qp.A[row, 0] = -1f;
            qp.A[row, 1] = 1f;
            qp.Lo[row] = t - 4f;
            qp.Hi[row] = t + 4f;
            row++;
        }

        // waypoint pins (hard, within tolerance)
        if (hasPin)
        {
            for (int i = 0; i < n; i++)
            {
                if (!float.IsNaN(spec.Pin[i]) && row < m)
                {
                    qp.A[row, i] = 1f;
                    qp.Lo[row] = spec.Pin[i] - spec.PinTolerance;
                    qp.Hi[row] = spec.Pin[i] + spec.PinTolerance;
                    row++;
                }
            }
        }

        // unused rows: open interval (ADMM ignores them)
        for (; row < m; row++)
        {
            qp.Lo[row] = float.NegativeInfinity;
            qp.Hi[row] = float.PositiveInfinity;
        }

        // variable scaling u = h / ts: puts P and the bounds in O(1..100) so the fixed-rho ADMM behaves
        for (int i = 0; i < n; i++)
        {
            qp.Q[i] *= ts;
            for (int j = 0; j < n; j++)
            {
                qp.P[i, j] *= ts * ts;
            }
            if (!float.IsNaN(h[i]))
            {
                h[i] /= ts;
            }
        }

        // h = ts*u  =>  A*h = (A*ts)*u, bounds stay in metres
        for (int r = 0; r < m; r++)
        {
            for (int j = 0; j < n; j++)
            {
                qp.A[r, j] *= ts;
            }
        }

        bool ok = QpSolver.Solve(qp, h, QpSolver.MaxIterProfile, 1e-3f, QpSolver.RhoProfile);
        for (int i = 0; i < n; i++)
        {
            h[i] *= ts;
        }
        return ok;
    }
}

/// <summary>
/// A solved vertical profile anchored to a ground position and course, so the guidance can interpolate it
/// as the aircraft travels along the track.
/// </summary>
public sealed class ProfilePlan
{
    public float[] H = new float[64];
    public int N;
    public float Ds;
    public float OriginX, OriginZ;
    public float OriginChi;   // course at the origin (rad)
    public float Age;         // seconds since the plan was made (advanced by the consumer)
    public bool Valid;

    /// <summary>Planned altitude at along-track distance d from the plan origin.</summary>
    public float AltAt(float d)
    {
        float f = d / Ds;
        int i0 = Mathf.FloorToInt(f);
        if (i0 < 0 || N < 2)
        {
            return H[0];
        }
        if (i0 >= N - 1)
        {
            return H[N - 1];
        }
        float frac = f - i0;
        return (H[i0] * (1f - frac)) + (H[i0 + 1] * frac);
    }

    /// <summary>Planned slope at along-track distance d.</summary>
    public float SlopeAt(float d)
    {
        int i0 = Mathf.Clamp(Mathf.FloorToInt(d / Ds), 0, N - 2);
        return (H[i0 + 1] - H[i0]) / Ds;
    }

    /// <summary>Along-track distance of the position from the plan origin.</summary>
    public float DistOf(float x, float z)
    {
        float dx = x - OriginX;
        float dz = z - OriginZ;
        return (dx * Mathf.Sin(OriginChi)) + (dz * Mathf.Cos(OriginChi));
    }
}

/// <summary>
/// Runs the profile optimization on a background thread at a low rate (a few Hz) and hands the latest plan
/// to the guidance thread - the solver never runs in the control tick. With <see cref="Async"/> = false
/// (simulator, deterministic replay) the same job runs inline when requested.
/// </summary>
public sealed class ProfilePlannerWorker : IDisposable
{
    private readonly Thread _thread;
    private readonly AutoResetEvent _wake = new(false);
    private readonly object _lock = new();
    private ProfileSpec _pending;
    private ProfilePlan _ready;
    private ProfilePlan _current = new();
    private readonly float[] _warm;
    private volatile bool _stop;

    public bool Async { get; }

    /// <summary>Seconds between replans (consumer-driven; the worker only solves when asked).</summary>
    public float ReplanPeriod = 0.5f;

    /// <summary>Consecutive solves that produced no plan (infeasible problem). Resets on success;
    /// consumers use this to distinguish 'still computing' from 'there is no feasible profile'.</summary>
    public int ConsecutiveFailures { get; private set; }

    public ProfilePlannerWorker(bool async)
    {
        Async = async;
        _warm = new float[QpSolver.MaxSize];
        for (int i = 0; i < _warm.Length; i++)
        {
            _warm[i] = float.NaN;
        }
        if (async)
        {
            _thread = new Thread(Loop) { IsBackground = true, Name = "NOA-ProfilePlanner" };
            _thread.Start();
        }
    }

    public void Submit(ProfileSpec spec, float originX, float originZ, float originChi)
    {
        lock (_lock)
        {
            _pending = spec;
            _pendingOriginX = originX;
            _pendingOriginZ = originZ;
            _pendingOriginChi = originChi;
        }

        if (Async)
        {
            _wake.Set();
        }
        else
        {
            SolveOne();
        }
    }

    private float _pendingOriginX, _pendingOriginZ, _pendingOriginChi;

    private void Loop()
    {
        while (!_stop)
        {
            _wake.WaitOne(100);
            SolveOne();
        }
    }

    private void SolveOne()
    {
        ProfileSpec spec;
        float ox, oz, oc;
        lock (_lock)
        {
            spec = _pending;
            ox = _pendingOriginX;
            oz = _pendingOriginZ;
            oc = _pendingOriginChi;
            _pending = null;
        }

        if (spec == null || spec.N < 4 || spec.N > QpSolver.MaxSize)
        {
            return;
        }

        // warm start: shift the previous solution one step along the track
        for (int i = 0; i < spec.N; i++)
        {
            _warm[i] = i + 1 < _current.N ? _current.H[i + 1] : (_current.N > 0 ? _current.H[_current.N - 1] : float.NaN);
        }

        bool ok = VerticalProfile.Plan(spec, _warm);
        if (!ok && !float.IsNaN(spec.StartSlope))
        {
            // committed slope is the usual infeasibility source: free it and keep the altitude
            // anchored. The failed iterates stay in as a warm start.
            spec.StartSlope = float.NaN;
            ok = VerticalProfile.Plan(spec, _warm);
        }
        if (!ok && !float.IsNaN(spec.StartAlt))
        {
            // hard start can still be infeasible: retry fully soft
            spec.StartAlt = float.NaN;
            ok = VerticalProfile.Plan(spec, _warm);
        }
        if (!ok)
        {
            ConsecutiveFailures++;
            return;
        }

        ConsecutiveFailures = 0;

        var plan = new ProfilePlan
        {
            H = new float[Math.Max(spec.N, 64)],
            N = spec.N,
            Ds = spec.Ds,
            OriginX = ox,
            OriginZ = oz,
            OriginChi = oc,
            Valid = true,
        };
        Array.Copy(_warm, plan.H, spec.N);
        lock (_lock)
        {
            _ready = plan;
            _current = plan;   // warm-start source for the next solve
        }
    }

    public ProfilePlan TakePlan()
    {
        lock (_lock)
        {
            ProfilePlan p = _ready;
            _ready = null;
            return p;
        }
    }

    public void Dispose()
    {
        _stop = true;
        _wake.Set();
    }
}
