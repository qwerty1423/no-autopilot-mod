using System;

using NOAutopilot.Core.Control;

using UnityEngine;

namespace NOAutopilot.Core.Guidance;

public sealed class DubinsPath
{
    public enum SegmentType
    {
        Left,
        Straight,
        Right
    }

    public struct Segment
    {
        public SegmentType Type;
        public Vector2 Start;
        public float StartTheta;
        public float Length;     // metres
        public Vector2 Center;   // arcs only
    }

    public readonly Segment[] Segments = new Segment[3];
    public float Radius { get; private set; }
    public float Length { get; private set; }
    public bool Valid { get; private set; }
    public string Word { get; private set; } = "";

    public static float CourseToTheta(float course) => ControlMath.WrapPi((Mathf.PI * 0.5f) - course);

    public static float ThetaToCourse(float theta)
    {
        float c = (Mathf.PI * 0.5f) - theta;
        c %= ControlMath.TwoPi;
        return c < 0f ? c + ControlMath.TwoPi : c;
    }

    private static float Mod2Pi(float a)
    {
        a %= ControlMath.TwoPi;
        return a < 0f ? a + ControlMath.TwoPi : a;
    }

    /// <summary>Computes the shortest Dubins path. Returns false if the inputs are degenerate.</summary>
    public bool Compute(Vector2 p0, float theta0, Vector2 p1, float theta1, float radius)
    {
        Valid = false;
        if (radius < 1f || !ControlMath.IsFinite(p0.x) || !ControlMath.IsFinite(p1.x))
        {
            return false;
        }

        Radius = radius;
        float dx = p1.x - p0.x, dy = p1.y - p0.y;
        float dist = Mathf.Sqrt((dx * dx) + (dy * dy));
        float d = dist / radius;
        float th = d > 0f ? Mod2Pi(Mathf.Atan2(dy, dx)) : 0f;
        float alpha = Mod2Pi(theta0 - th);
        float beta = Mod2Pi(theta1 - th);

        float sa = Mathf.Sin(alpha), sb = Mathf.Sin(beta), ca = Mathf.Cos(alpha), cb = Mathf.Cos(beta);
        float cab = Mathf.Cos(alpha - beta);
        float dsq = d * d;

        float best = float.PositiveInfinity;
        int bestWord = -1;
        float bt = 0f, bp = 0f, bq = 0f;

        for (int w = 0; w < 6; w++)
        {
            if (!Solve(w, alpha, beta, d, dsq, sa, sb, ca, cb, cab, out float t, out float p, out float q))
            {
                continue;
            }

            float len = t + p + q;
            if (len < best)
            {
                best = len;
                bestWord = w;
                bt = t;
                bp = p;
                bq = q;
            }
        }

        if (bestWord < 0)
        {
            return false;
        }

        SegmentType[] types = Words[bestWord];
        Word = WordNames[bestWord];
        float[] lengths = [bt * radius, bp * radius, bq * radius];

        Vector2 pos = p0;
        float theta = theta0;
        for (int i = 0; i < 3; i++)
        {
            Segment s = new()
            {
                Type = types[i],
                Start = pos,
                StartTheta = theta,
                Length = lengths[i]
            };
            if (s.Type == SegmentType.Left)
            {
                s.Center = pos + (radius * new Vector2(-Mathf.Sin(theta), Mathf.Cos(theta)));
            }
            else if (s.Type == SegmentType.Right)
            {
                s.Center = pos + (radius * new Vector2(Mathf.Sin(theta), -Mathf.Cos(theta)));
            }

            Segments[i] = s;
            Advance(s, s.Length, radius, out pos, out theta);
        }

        Length = best * radius;
        Valid = true;
        return true;
    }

    /// <summary>Pose after travelling <paramref name="dist"/> along a segment.</summary>
    public static void Advance(Segment s, float dist, float radius, out Vector2 pos, out float theta)
    {
        switch (s.Type)
        {
            case SegmentType.Straight:
                theta = s.StartTheta;
                pos = s.Start + (dist * new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)));
                break;
            case SegmentType.Left:
                {
                    float phi0 = s.StartTheta - (Mathf.PI * 0.5f);
                    float phi = phi0 + (dist / radius);
                    pos = s.Center + (radius * new Vector2(Mathf.Cos(phi), Mathf.Sin(phi)));
                    theta = s.StartTheta + (dist / radius);
                    break;
                }
            default:
                {
                    float phi0 = s.StartTheta + (Mathf.PI * 0.5f);
                    float phi = phi0 - (dist / radius);
                    pos = s.Center + (radius * new Vector2(Mathf.Cos(phi), Mathf.Sin(phi)));
                    theta = s.StartTheta - (dist / radius);
                    break;
                }
        }
    }

    private static readonly SegmentType[][] Words =
    [
        [SegmentType.Left, SegmentType.Straight, SegmentType.Left],
        [SegmentType.Right, SegmentType.Straight, SegmentType.Right],
        [SegmentType.Left, SegmentType.Straight, SegmentType.Right],
        [SegmentType.Right, SegmentType.Straight, SegmentType.Left],
        [SegmentType.Right, SegmentType.Left, SegmentType.Right],
        [SegmentType.Left, SegmentType.Right, SegmentType.Left]
    ];

    private static readonly string[] WordNames = ["LSL", "RSR", "LSR", "RSL", "RLR", "LRL"];

    private static bool Solve(int word, float alpha, float beta, float d, float dsq, float sa, float sb, float ca,
        float cb, float cab, out float t, out float p, out float q)
    {
        t = p = q = 0f;
        switch (word)
        {
            case 0: // LSL
                {
                    float tmp0 = d + sa - sb;
                    float psq = 2f + dsq - (2f * cab) + (2f * d * (sa - sb));
                    if (psq < 0f)
                    {
                        return false;
                    }

                    float tmp1 = Mathf.Atan2(cb - ca, tmp0);
                    t = Mod2Pi(tmp1 - alpha);
                    p = Mathf.Sqrt(psq);
                    q = Mod2Pi(beta - tmp1);
                    return true;
                }
            case 1: // RSR
                {
                    float tmp0 = d - sa + sb;
                    float psq = 2f + dsq - (2f * cab) + (2f * d * (sb - sa));
                    if (psq < 0f)
                    {
                        return false;
                    }

                    float tmp1 = Mathf.Atan2(ca - cb, tmp0);
                    t = Mod2Pi(alpha - tmp1);
                    p = Mathf.Sqrt(psq);
                    q = Mod2Pi(tmp1 - beta);
                    return true;
                }
            case 2: // LSR
                {
                    float psq = -2f + dsq + (2f * cab) + (2f * d * (sa + sb));
                    if (psq < 0f)
                    {
                        return false;
                    }

                    p = Mathf.Sqrt(psq);
                    float tmp0 = Mathf.Atan2(-ca - cb, d + sa + sb) - Mathf.Atan2(-2f, p);
                    t = Mod2Pi(tmp0 - alpha);
                    q = Mod2Pi(tmp0 - Mod2Pi(beta));
                    return true;
                }
            case 3: // RSL
                {
                    float psq = -2f + dsq + (2f * cab) - (2f * d * (sa + sb));
                    if (psq < 0f)
                    {
                        return false;
                    }

                    p = Mathf.Sqrt(psq);
                    float tmp0 = Mathf.Atan2(ca + cb, d - sa - sb) - Mathf.Atan2(2f, p);
                    t = Mod2Pi(alpha - tmp0);
                    q = Mod2Pi(beta - tmp0);
                    return true;
                }
            case 4: // RLR
                {
                    float tmp0 = (6f - dsq + (2f * cab) + (2f * d * (sa - sb))) / 8f;
                    if (Mathf.Abs(tmp0) > 1f)
                    {
                        return false;
                    }

                    float phi = Mathf.Atan2(ca - cb, d - sa + sb);
                    p = Mod2Pi(ControlMath.TwoPi - Mathf.Acos(tmp0));
                    t = Mod2Pi(alpha - phi + Mod2Pi(p * 0.5f));
                    q = Mod2Pi(alpha - beta - t + Mod2Pi(p));
                    return true;
                }
            case 5: // LRL
                {
                    float tmp0 = (6f - dsq + (2f * cab) + (2f * d * (sb - sa))) / 8f;
                    if (Mathf.Abs(tmp0) > 1f)
                    {
                        return false;
                    }

                    float phi = Mathf.Atan2(ca - cb, d + sa - sb);
                    p = Mod2Pi(ControlMath.TwoPi - Mathf.Acos(tmp0));
                    t = Mod2Pi(-alpha - phi + (p * 0.5f));
                    q = Mod2Pi(Mod2Pi(beta) - alpha - t + Mod2Pi(p));
                    return true;
                }
        }

        return false;
    }
}

public sealed class PathFollower
{
    private float _alongSegment;

    // continuous (unwrapped) progress along the current arc
    private float _arcRawPrev;
    private float _arcUnwrapped;
    private bool _arcInit;

    public DubinsPath Path { get; private set; }
    public int SegmentIndex { get; private set; }

    /// <summary>Remaining path length from the current projection.</summary>
    public float Remaining { get; private set; }

    /// <summary>Signed cross track error (m, right of the path positive).</summary>
    public float CrossTrack { get; private set; }

    public bool Finished { get; private set; }

    public void SetPath(DubinsPath path)
    {
        Path = path;
        SegmentIndex = 0;
        _alongSegment = 0f;
        _arcInit = false;
        Finished = path?.Valid != true;
        Remaining = path?.Length ?? 0f;
        // skip zero length segments
        SkipEmpty();
    }

    private void SkipEmpty()
    {
        while (Path != null && SegmentIndex < 3 && Path.Segments[SegmentIndex].Length < 1f)
        {
            SegmentIndex++;
            _arcInit = false;
        }

        if (SegmentIndex >= 3)
        {
            Finished = true;
        }
    }

    /// <summary>
    /// Desired course (rad, clockwise from north) and course rate feed forward (rad/s) for position p (x east, y north).
    /// </summary>
    public bool Guide(Vector2 p, float groundSpeed, float fieldLength, out float course, out float courseRateFf)
    {
        course = 0f;
        courseRateFf = 0f;
        if (Path?.Valid != true)
        {
            return false;
        }

        float r = Path.Radius;
        float k = 1f / Mathf.Max(fieldLength, 1f);

        // advance through finished segments
        for (int guard = 0; guard < 3 && SegmentIndex < 3; guard++)
        {
            DubinsPath.Segment seg = Path.Segments[SegmentIndex];
            float along = Progress(seg, p, r);
            if (along < seg.Length - 0.5f || SegmentIndex == 2)
            {
                break;
            }

            SegmentIndex++;
            _arcInit = false;
            SkipEmpty();
        }

        if (SegmentIndex >= 3)
        {
            SegmentIndex = 2;
            Finished = true;
        }

        DubinsPath.Segment s = Path.Segments[SegmentIndex];
        _alongSegment = Mathf.Clamp(Progress(s, p, r), 0f, s.Length);

        float rem = s.Length - _alongSegment;
        for (int i = SegmentIndex + 1; i < 3; i++)
        {
            rem += Path.Segments[i].Length;
        }

        Remaining = rem;
        if (SegmentIndex == 2 && _alongSegment >= s.Length - 0.5f)
        {
            Finished = true;
        }

        float thetaDes;
        float thetaRate = 0f;
        switch (s.Type)
        {
            case DubinsPath.SegmentType.Straight:
                {
                    float th = s.StartTheta;
                    Vector2 rel = p - s.Start;
                    // left of the path positive (maths convention)
                    float e = (-Mathf.Sin(th) * rel.x) + (Mathf.Cos(th) * rel.y);
                    CrossTrack = -e;
                    thetaDes = th - Mathf.Atan(k * e);
                    break;
                }
            case DubinsPath.SegmentType.Left:
                {
                    Vector2 rel = p - s.Center;
                    float phi = Mathf.Atan2(rel.y, rel.x);
                    float er = rel.magnitude - r;
                    CrossTrack = -er; // outside of a left turn = right of the path
                    thetaDes = phi + (Mathf.PI * 0.5f) + Mathf.Atan(k * er);
                    thetaRate = groundSpeed / r;
                    break;
                }
            default:
                {
                    Vector2 rel = p - s.Center;
                    float phi = Mathf.Atan2(rel.y, rel.x);
                    float er = rel.magnitude - r;
                    CrossTrack = er; // outside of a right turn = left of the path
                    thetaDes = phi - (Mathf.PI * 0.5f) - Mathf.Atan(k * er);
                    thetaRate = -groundSpeed / r;
                    break;
                }
        }

        course = DubinsPath.ThetaToCourse(thetaDes);
        courseRateFf = -thetaRate;
        return true;
    }

    /// <summary>
    /// Distance travelled along the segment. For arcs the swept angle is unwrapped continuously, so arcs of
    /// 180 degrees and more are tracked correctly.
    /// </summary>
    private float Progress(DubinsPath.Segment s, Vector2 p, float r)
    {
        if (s.Type == DubinsPath.SegmentType.Straight)
        {
            return AlongSegment(s, p, r);
        }

        Vector2 rel = p - s.Center;
        float phi = Mathf.Atan2(rel.y, rel.x);
        float phi0 = s.Type == DubinsPath.SegmentType.Left
            ? s.StartTheta - (Mathf.PI * 0.5f)
            : s.StartTheta + (Mathf.PI * 0.5f);
        float raw = s.Type == DubinsPath.SegmentType.Left ? phi - phi0 : phi0 - phi;
        if (!_arcInit)
        {
            _arcRawPrev = raw;
            _arcUnwrapped = ControlMath.WrapPi(raw);
            _arcInit = true;
        }
        else
        {
            _arcUnwrapped += ControlMath.WrapPi(raw - _arcRawPrev);
            _arcRawPrev = raw;
        }

        return _arcUnwrapped * r;
    }

    private static float AlongSegment(DubinsPath.Segment s, Vector2 p, float r)
    {
        switch (s.Type)
        {
            case DubinsPath.SegmentType.Straight:
                {
                    Vector2 dir = new(Mathf.Cos(s.StartTheta), Mathf.Sin(s.StartTheta));
                    return Vector2.Dot(p - s.Start, dir);
                }
            case DubinsPath.SegmentType.Left:
                {
                    Vector2 rel = p - s.Center;
                    float phi = Mathf.Atan2(rel.y, rel.x);
                    float phi0 = s.StartTheta - (Mathf.PI * 0.5f);
                    float swept = ControlMath.WrapPi(phi - phi0);
                    // allow up to a full turn: unwrap using the segment length as a hint
                    float total = s.Length / r;
                    if (swept < -0.2f && total > Mathf.PI)
                    {
                        swept += ControlMath.TwoPi;
                    }

                    return swept * r;
                }
            default:
                {
                    Vector2 rel = p - s.Center;
                    float phi = Mathf.Atan2(rel.y, rel.x);
                    float phi0 = s.StartTheta + (Mathf.PI * 0.5f);
                    float swept = ControlMath.WrapPi(phi0 - phi);
                    float total = s.Length / r;
                    if (swept < -0.2f && total > Mathf.PI)
                    {
                        swept += ControlMath.TwoPi;
                    }

                    return swept * r;
                }
        }
    }
}
