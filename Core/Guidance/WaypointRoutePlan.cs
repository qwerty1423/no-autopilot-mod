using System.Collections.Generic;

using NOAutopilot.Core.Control;
using NOAutopilot.Core.Guidance.Optim;

using UnityEngine;

namespace NOAutopilot.Core.Guidance;

/// <summary>
/// Vertical profile for a 3D waypoint route: waypoints with an altitude (y &gt; 0) are pinned and the
/// altitude along the route is planned as one constrained quadratic program, solved asynchronously at
/// ~1 Hz. 2D waypoints carry no altitude and are invisible to the plan; a route without any altitude
/// holds the autopilot's target altitude.
/// </summary>
public sealed class WaypointRoutePlan
{
    public bool Async = true;
    public float ReplanPeriod = 1f;
    public float GridSpacing = 150f;

    private ProfilePlannerWorker _worker;
    private ProfilePlan _plan;
    private float _trav;
    private float _time;
    private float _replanAt;
    private int _lastCount = -1;
    private Vector3 _lastWp = new(float.NaN, 0f, 0f);
    private float _slopeCmd = float.NaN;

    public void Reset()
    {
        _plan = null;
        _trav = 0f;
        _replanAt = 0f;
        _lastCount = -1;
        _slopeCmd = float.NaN;
    }

    /// <summary>
    /// Advances the plan, replanning asynchronously when due or when the route changed.
    /// Returns true with the altitude to hold and the slope feed-forward while a plan is active.
    /// </summary>
    public bool Update(FlightState s, IReadOnlyList<Vector3> wps, float maxG, out float alt, out float slope)
    {
        alt = float.NaN;
        slope = 0f;
        if (wps == null || wps.Count == 0)
        {
            return false;
        }

        bool anyPin = false;
        for (int i = 0; i < wps.Count; i++)
        {
            if (wps[i].y > 0f)
            {
                anyPin = true;
                break;
            }
        }

        if (!anyPin)
        {
            return false;
        }

        _time += s.Dt;
        if (_worker == null || _worker.Async != Async)
        {
            _worker?.Dispose();
            _worker = new ProfilePlannerWorker(Async);
        }

        ProfilePlan fresh = _worker.TakePlan();
        if (fresh != null)
        {
            _plan = fresh;
            _trav = 0f;
        }

        bool routeChanged = wps.Count != _lastCount ||
            (float.IsNaN(_lastWp.x) || Vector3.Distance(wps[0], _lastWp) > 1f);
        if (_plan == null || routeChanged || _time >= _replanAt || _plan.N < 8 || _trav > (_plan.N - 4) * _plan.Ds)
        {
            _replanAt = _time + ReplanPeriod;
            _lastCount = wps.Count;
            _lastWp = wps[0];
            Submit(s, wps, maxG);
        }

        if (_plan == null)
        {
            return false;
        }

        _trav += s.GroundSpeed * s.Dt;
        alt = _plan.AltAt(_trav);
        // rate-limited feed-forward slope: a fresh plan's opening slope must not jerk the load factor
        float slopeNow = s.VerticalSpeed / Mathf.Max(s.GroundSpeed, 1f);
        if (float.IsNaN(_slopeCmd))
        {
            _slopeCmd = slopeNow;
        }

        float slopeRate = (2.5f * ControlMath.G) / Mathf.Max(s.GroundSpeed, 1f);
        _slopeCmd = Mathf.MoveTowards(_slopeCmd, _plan.SlopeAt(_trav), slopeRate * s.Dt);
        slope = _slopeCmd;
        return true;
    }

    private void Submit(FlightState s, IReadOnlyList<Vector3> wps, float maxG)
    {
        var pts = new List<Vector2> { new(s.Position.x, s.Position.z) };
        var dCum = new List<float> { 0f };
        for (int i = 0; i < wps.Count; i++)
        {
            pts.Add(new Vector2(wps[i].x, wps[i].z));
            dCum.Add(dCum[^1] + Vector2.Distance(pts[^2], pts[^1]));
        }

        float total = dCum[^1];
        float ds = Mathf.Max(GridSpacing, 20f);
        int n = Mathf.Clamp(Mathf.CeilToInt(total / ds) + 1, 8, 128);
        ds = total / (n - 1);
        float v = Mathf.Max(s.Speed, 30f);

        var spec = new ProfileSpec
        {
            N = n,
            Ds = ds,
            Lower = new float[n],
            Desired = new float[n],
            Pin = new float[n],
            MaxSlopeUp = 0.35f,
            MaxSlopeDown = -0.5f,
            CurvMax = 0.7f * (Mathf.Max(maxG, 1.5f) - 1f) * ControlMath.G / (v * v),
            CurvMin = 0.7f * -1.6f * ControlMath.G / (v * v),
            StartAlt = s.Altitude,
            StartSlope = s.VerticalSpeed / Mathf.Max(s.GroundSpeed, 1f),
            TrackScale = 150f,
            SlopeScale = 0.15f,
            SmoothWeight = 2f,
            PinTolerance = 25f,
        };
        float capL = Mathf.Clamp(v * 2f, 300f, 800f);
        spec.SlopeWeight = spec.TrackWeight * capL * capL * spec.SlopeScale * spec.SlopeScale /
            (spec.TrackScale * spec.TrackScale);

        var pinD = new List<float>();
        var pinA = new List<float>();
        for (int i = 0; i < wps.Count; i++)
        {
            if (wps[i].y > 0f)
            {
                pinD.Add(dCum[i + 1]);
                pinA.Add(wps[i].y);
            }
        }

        for (int i = 0; i < n; i++)
        {
            spec.Pin[i] = float.NaN;
            spec.Lower[i] = float.NaN;
            float d = i * ds;
            float d0 = 0f, a0 = s.Altitude;
            for (int k = 0; k < pinA.Count; k++)
            {
                if (d <= pinD[k] || k == pinA.Count - 1)
                {
                    float span = Mathf.Max(pinD[k] - d0, 1e-3f);
                    spec.Desired[i] = Mathf.Lerp(a0, pinA[k], Mathf.Clamp01((d - d0) / span));
                    break;
                }

                d0 = pinD[k];
                a0 = pinA[k];
            }
        }

        for (int i = 0; i < pinA.Count; i++)
        {
            int idx = Mathf.Clamp(Mathf.RoundToInt(pinD[i] / ds), 0, n - 1);
            spec.Pin[idx] = pinA[i];
        }

        _worker.Submit(spec, s.Position.x, s.Position.z, s.Chi);
    }
}
