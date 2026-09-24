using NOAutopilot.Core.Control;

using UnityEngine;

namespace NOAutopilot.Core.Guidance;

/// <summary>
/// Waypoint following for the unified controller. Corners use the bank-limited turn circle tangent to
/// both legs; straights use pure pursuit. The output is a lateral acceleration demand.
/// </summary>
public sealed class WaypointGuidance
{
    /// <summary>Seconds of look-ahead for the pursuit point.</summary>
    public float LookAheadTime = 1.6f;

    /// <summary>How far the corner aim is pulled back from the exit tangent towards the waypoint.</summary>
    public float AimTightness = 0.35f;

    /// <summary>Lateral acceleration command (m/s², horizontal, positive right).</summary>
    /// <param name="prev">Previous waypoint or route start; the corner is built on the leg into the waypoint.</param>
    public float Step(FlightState s, Vector3 waypoint, bool hasNext, Vector3 next, Vector3 prev, float bankLimit,
        float gain, float rollRate)
    {
        const float g = ControlMath.G;
        float vg = Mathf.Max(s.GroundSpeed, 10f);

        float dx = waypoint.x - s.Position.x;
        float dz = waypoint.z - s.Position.z;
        float dist = Mathf.Sqrt((dx * dx) + (dz * dz));

        float ax = dx, az = dz;
        if (hasNext)
        {
            float p1x = waypoint.x - prev.x, p1z = waypoint.z - prev.z;
            float chi1 = (Mathf.Abs(p1x) + Mathf.Abs(p1z)) > 1f ? Mathf.Atan2(p1x, p1z) : Mathf.Atan2(dx, dz);
            float l2x = next.x - waypoint.x, l2z = next.z - waypoint.z;
            float chi2 = Mathf.Atan2(l2x, l2z);
            float dChi = ControlMath.WrapPi(chi2 - chi1);
            if (Mathf.Abs(dChi) > 0.02f)
            {
                float mtr = g * Mathf.Tan(Mathf.Min(bankLimit, 1.4f)) / vg;
                float radius = vg / Mathf.Max(mtr, 0.01f);
                float turnDist = radius / Mathf.Tan(Mathf.Abs(dChi) * 0.5f);
                float l2 = Mathf.Sqrt((l2x * l2x) + (l2z * l2z));
                if (l2 > 1f)
                {
                    float u2x = l2x / l2, u2z = l2z / l2;
                            float t2x = waypoint.x + (u2x * turnDist), t2z = waypoint.z + (u2z * turnDist);
                    float along2 = ((s.Position.x - t2x) * u2x) + ((s.Position.z - t2z) * u2z);
                    float cross2 = ((s.Position.x - t2x) * u2z) - ((s.Position.z - t2z) * u2x);
                    bool pastExit = along2 > 0f && Mathf.Abs(cross2) < Mathf.Max(300f, 0.6f * radius);
                    if (pastExit)
                    {
                        // through the corner: rejoin the next leg ahead of the projection
                        float proj = ((s.Position.x - waypoint.x) * u2x) + ((s.Position.z - waypoint.z) * u2z);
                        float lead = Mathf.Clamp(0.5f * dist, 150f, 0.5f * l2);
                        float along = Mathf.Max(proj + lead, 0f);
                        ax = waypoint.x + (u2x * along) - s.Position.x;
                        az = waypoint.z + (u2z * along) - s.Position.z;
                    }
                    else if (dist < turnDist)
                    {
                        // inside the corner: steer at the exit tangent point, pulled back to the waypoint
                        float aimX = Mathf.Lerp(t2x, waypoint.x, AimTightness);
                        float aimZ = Mathf.Lerp(t2z, waypoint.z, AimTightness);
                        ax = aimX - s.Position.x;
                        az = aimZ - s.Position.z;
                    }
                }
            }
        }

        if (Mathf.Abs(ax) < 1e-3f && Mathf.Abs(az) < 1e-3f)
        {
            return 0f;
        }

        float chiDes = Mathf.Atan2(ax, az);
        float chiErr = ControlMath.WrapPi(chiDes - s.Chi);
        if (Mathf.Abs(chiErr) > 2.6f)
        {
            // astern target: commit to the current bank's side instead of wagging at the wrap point
            chiErr = ((s.Phi + (0.2f * s.P)) >= 0f ? 1f : -1f) * Mathf.Abs(chiErr);
        }

        float maxTurnRate = g * Mathf.Tan(Mathf.Min(bankLimit, 1.4f)) / vg;
        float turnAccel = g * rollRate / vg;
        float chiDot = ControlMath.ShapedRate(chiErr, gain, turnAccel, maxTurnRate);
        return vg * chiDot;
    }
}
