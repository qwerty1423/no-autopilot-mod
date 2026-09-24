using UnityEngine;

namespace NOAutopilot.Core.Control;

public sealed class AircraftModel
{
    public bool IsHelicopter;
    public float Mass = 10000f;             // kg
    public float MaxWeight = 15000f;        // kg
    public float GLimit = 9f;
    public float CornerSpeed = 180f;        // m/s (equivalent)
    public float LandingSpeed = 70f;        // m/s at max weight
    public float TakeoffSpeed = 60f;        // m/s
    public float MaxThrust;                 // N, total
    public float CruiseThrottle = 0.9f;
    public float GearHeight = 1.5f;         // m
    public bool HasTailHook;

    // fixed wing fbw
    public bool FbwEnabled;
    public float FbwGLimit = 9f;
    public float FbwCornerSpeed = 160f;
    public float FbwMaxPitchAngularVel = 1f;
    public float FbwMaxRollAngularVel = 6f;
    public float FbwMaxRollSpeed = 300f;
    public float FbwRollTightness = 0.5f;
    public float FbwYawTightness = 1f;
    public float FbwAlphaLimiter = 25f;         // deg
    public float FbwLimitFactor = 1f;

    // helicopter fbw
    public float HeloGLimit = 3f;
    public Vector3 HeloMaxAngularVel = new(0.8f, 0.8f, 1.2f);   // pitch, yaw, roll (rad/s per unit stick)

    /// <summary>
    /// Structural / maneuver load factor available at the given equivalent airspeed.
    /// </summary>
    public float AvailableLoadFactor(float trueAirspeed, float rho, float configuredMax)
    {
        float nMax = Mathf.Min(configuredMax, GLimit);
        float veas = trueAirspeed * Mathf.Sqrt(Mathf.Max(rho, 1e-3f) / 1.225f);
        float corner = Mathf.Max(CornerSpeed, 30f);
        float liftLimited = Mathf.Max(GLimit * (veas * veas) / (corner * corner), 1.05f);
        return Mathf.Max(Mathf.Min(nMax, liftLimited), 1.05f);
    }

    /// <summary>Landing speed scaled for the current weight.</summary>
    public float AdjustedLandingSpeed()
    {
        float ratio = MaxWeight > 1f ? Mathf.Clamp(Mass / MaxWeight, 0.2f, 1.5f) : 1f;
        return Mathf.Max(Mathf.Sqrt(ratio) * LandingSpeed, 30f);
    }

    /// <summary>Estimated change of airspeed rate per unit of throttle (m/s^2).</summary>
    public float ThrottleEffectiveness(float rho)
    {
        float t = MaxThrust > 1f ? MaxThrust : 0.4f * Mass * ControlMath.G;
        // thrust drops with density; keep a floor so the estimate never gets tiny
        float densityFactor = Mathf.Clamp(Mathf.Pow(Mathf.Max(rho, 0.05f) / 1.225f, 0.7f), 0.25f, 1f);
        return Mathf.Clamp(t * densityFactor / Mathf.Max(Mass, 100f), 1f, 40f);
    }

    /// <summary>
    /// Pitch rate the fixed wing FBW commands per unit of stick.
    /// </summary>
    public float FbwPitchRatePerStick(float speed, float rho)
    {
        float qRatio = QRatio(speed, rho);
        float k = FbwGLimit * ControlMath.G / Mathf.Max(speed, FbwCornerSpeed * 0.75f);
        if (qRatio < 1f)
        {
            k *= Mathf.Clamp(qRatio, 0.3f, 1f);
        }

        return Mathf.Max(Mathf.Lerp(FbwMaxPitchAngularVel, k, Mathf.Clamp01(FbwLimitFactor)), 0.02f);
    }

    /// <summary>Upper bound of the roll rate the FBW produces per unit stick.</summary>
    public float FbwRollRatePerStick(float speed, float rho)
    {
        float qRatio = QRatio(speed, rho);
        float k = FbwMaxRollAngularVel * Mathf.Clamp(qRatio / Mathf.Max(FbwMaxRollSpeed, 1e-3f), 0.5f, 1f);
        return Mathf.Max(k, 0.1f);
    }

    public float FbwYawRatePerStick() => 1f;

    /// <summary>Dynamic pressure ratio the FBW uses.</summary>
    public float QRatio(float speed, float rho)
    {
        float q0 = FbwCornerSpeed * FbwCornerSpeed * 1.225f;
        return speed * speed * rho / Mathf.Max(q0, 1f);
    }
}
