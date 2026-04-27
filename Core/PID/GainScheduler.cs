using NOAutopilot.Core.Config;

using UnityEngine;

namespace NOAutopilot.Core.PID;

/// <summary>
/// Stores the scheduling parameters for one PID loop.
/// </summary>
/// <param name="refQ">dynamic pressure (Pa) at which the base gains were tuned.</param>
/// <param name="kpExp">exponent for Kp scaling.  Kp_actual = Kp_base * (RefQ/q)^KpExp</param>
/// <param name="tiExp">exponent for Ti scaling.  Ti_actual = Ti_base * (RefQ/q)^TiExp</param>
/// <param name="tdExp">exponent for Td scaling.  Td_actual = Td_base * (RefQ/q)^TdExp</param>
/// <param name="clampMin">minimum multiplier (safety floor, e.g. 0.1)</param>
/// <param name="clampMax">maximum multiplier (safety ceiling, e.g. 5.0)</param>
public struct GainSchedule(
    float refQ = 6000f,
    float kpExp = 1f,
    float tiExp = 0f,
    float tdExp = 0f,
    float clampMin = 0.1f,
    float clampMax = 5f)
{
    public float RefQ = refQ;
    public float KpExp = kpExp;
    public float TiExp = tiExp;
    public float TdExp = tdExp;
    public float ClampMin = clampMin;
    public float ClampMax = clampMax;

    /// <summary>
    /// serialization
    /// </summary>
    public override readonly string ToString()
    {
        return string.Join("|",
            RefQ.ToString(System.Globalization.CultureInfo.InvariantCulture),
            KpExp.ToString(System.Globalization.CultureInfo.InvariantCulture),
            TiExp.ToString(System.Globalization.CultureInfo.InvariantCulture),
            TdExp.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ClampMin.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ClampMax.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public static GainSchedule Parse(string s)
    {
        System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InvariantCulture;
        string[] p = s.Split('|');
        float Get(int i, float def)
        {
            return p.Length > i && float.TryParse(p[i],
                System.Globalization.NumberStyles.Float, ci, out float v) ? v : def;
        }

        return new GainSchedule(
            refQ: Get(0, 6000f),
            kpExp: Get(1, 1f),
            tiExp: Get(2, 0f),
            tdExp: Get(3, 0f),
            clampMin: Get(4, 0.1f),
            clampMax: Get(5, 5f));
    }
}

/// <summary>
/// Computes dynamic pressure from the current flight state.
/// </summary>
/// <returns>
/// A scheduled copy of a PIDTuning without mutating the config entry.
/// </returns>
internal static class GainScheduler
{
    // ISA atmosphere

    /// <summary>
    /// Returns air density (kg/m³) for a given geometric altitude (metres)
    /// using the two-layer ISA model that covers 0–20 km.
    /// </summary>
    private static float IsaDensity(float altitudeMetres)
    {
        const float rho0 = 1.225f;  // kg/m³    sea-level density
        const float t0 = 288.15f;   // K        sea-level temperature
        const float l = 0.0065f;    // K/m      lapse rate (troposphere)
        const float g = 9.80665f;   // m/s²
        const float r = 287.05f;    // J/(kg·K)
        const float hTrop = 11000f; // m        tropopause altitude

        altitudeMetres = Mathf.Max(altitudeMetres, 0f);

        if (altitudeMetres <= hTrop)
        {
            // Troposphere: temperature decreases linearly.
            float t = t0 - (l * altitudeMetres);
            const float exp = g / (r * l);
            return rho0 * Mathf.Pow(t / t0, exp - 1f);
        }
        else
        {
            // Lower stratosphere: isothermal at T_trop = 216.65 K.
            // First get density at tropopause, then apply isothermal layer.
            const float trop = t0 - (l * hTrop);
            float rho_trop = rho0 * Mathf.Pow(trop / t0, (g / (r * l)) - 1f);
            float dH = altitudeMetres - hTrop;
            return rho_trop * Mathf.Exp(-g * dH / (r * trop));
        }
    }

    /// <summary>
    /// Computes q = 0.5 * rho * v² (Pa).
    /// </summary>
    public static float DynamicPressure(float speedMs, float altitudeMetres)
    {
        float rho = IsaDensity(altitudeMetres);
        return 0.5f * rho * speedMs * speedMs;
    }

    /// <summary>
    /// <para>
    /// Returns a new PIDTuning with gains scaled for the current dynamic
    /// pressure.  The base gains in <paramref name="baseTuning"/> are treated
    /// as the calibration point at <see cref="GainSchedule.RefQ"/>.
    /// </para>
    /// <para>
    /// Scaling law (power-law, standard in aerospace):
    ///   Kp_actual = Kp_base * clamp( (RefQ / q)^KpExp, ClampMin, ClampMax )
    /// </para>
    /// <para>If scheduling is disabled (KpExp == 0) the function is a cheap copy.</para>
    /// </summary>
    public static PIDTuning Schedule(
        PIDTuning baseTuning,
        GainSchedule schedule,
        float currentQ)
    {
        // Guard: avoid division by zero or nonsensical q values.
        if (currentQ < 1f || schedule.RefQ < 1f)
        {
            return baseTuning;
        }

        float ratio = schedule.RefQ / currentQ;

        float kpMul = ScaleFactor(ratio, schedule.KpExp, schedule.ClampMin, schedule.ClampMax);
        float tiMul = ScaleFactor(ratio, schedule.TiExp, schedule.ClampMin, schedule.ClampMax);
        float tdMul = ScaleFactor(ratio, schedule.TdExp, schedule.ClampMin, schedule.ClampMax);

        PIDTuning t = baseTuning;
        t.Kp *= kpMul;
        t.Ti *= tiMul;
        t.Td *= tdMul;
        return t;
    }

    private static float ScaleFactor(float ratio, float exponent, float min, float max)
    {
        if (exponent == 0f)
        {
            return 1f; // no scaling requested
        }

        float factor = Mathf.Pow(ratio, exponent);
        return Mathf.Clamp(factor, min, max);
    }
}
