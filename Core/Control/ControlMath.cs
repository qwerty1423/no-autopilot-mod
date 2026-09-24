using UnityEngine;

namespace NOAutopilot.Core.Control;

/// <summary>
/// Small math helpers used by the unified controller.
/// </summary>
internal static class ControlMath
{
    public const float G = 9.81f;
    public const float TwoPi = Mathf.PI * 2f;

    /// <summary>Wraps an angle in radians to (-pi, pi].</summary>
    public static float WrapPi(float a)
    {
        if (float.IsNaN(a) || float.IsInfinity(a))
        {
            return 0f;
        }

        a %= TwoPi;
        if (a > Mathf.PI)
        {
            a -= TwoPi;
        }
        else if (a <= -Mathf.PI)
        {
            a += TwoPi;
        }

        return a;
    }

    public static float SafeAsin(float x) => Mathf.Asin(Mathf.Clamp(x, -1f, 1f));

    public static float SafeAcos(float x) => Mathf.Acos(Mathf.Clamp(x, -1f, 1f));

    public static bool IsFinite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);

    public static float Finite(float x, float fallback = 0f) => IsFinite(x) ? x : fallback;

    /// <summary>Conjugate (= inverse for unit quaternions). Managed replacement for Quaternion.Inverse.</summary>
    public static Quaternion Conjugate(Quaternion q) => new(-q.x, -q.y, -q.z, q.w);

    /// <summary>
    /// Proportional law with a square-root shaped region, i.e. the rate command that brings
    /// the error to zero with a bounded "acceleration" and without overshoot:
    /// rate = sign(e) * min(k|e|, sqrt(2 a |e|), max).
    /// </summary>
    public static float ShapedRate(float error, float gain, float accel, float max)
    {
        float abs = Mathf.Abs(error);
        float linear = gain * abs;
        float sqrt = accel > 0f ? Mathf.Sqrt(2f * accel * abs) : linear;
        float r = Mathf.Min(linear, sqrt);
        if (max > 0f)
        {
            r = Mathf.Min(r, max);
        }

        return error >= 0f ? r : -r;
    }

    /// <summary>Normalizes a vector, returning <paramref name="fallback"/> if it is too short.</summary>
    public static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
    {
        float m = v.magnitude;
        return m > 1e-5f ? v / m : fallback;
    }
}
