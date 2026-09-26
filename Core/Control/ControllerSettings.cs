using UnityEngine;

namespace NOAutopilot.Core.Control;

/// <summary>
/// Config of the unified controller. Units: rad, rad/s, m, m/s, s unless stated otherwise.
/// </summary>
public sealed class ControllerSettings
{
    // INDI angular rate core
    /// <summary>
    /// Inner loop formulation. RateCommand (default): behind the game's fly-by-wire the stick commands a body
    /// rate, so the incremental inversion is done on the rate itself (relative degree 0, no derivative of noisy
    /// gyro data). Acceleration: classic INDI on the angular acceleration (for experiments / FBW off).
    /// </summary>
    public bool AccelerationInnerLoop;

    /// <summary>
    /// Use one full 3x3 pitch/roll/yaw effectiveness matrix in the default rate-command inner loop.
    /// Disable only for comparison with the legacy three-channel SISO implementation.
    /// </summary>
    public bool MimoRateControl = true;

    /// <summary>Closed loop bandwidth of the roll / pitch / yaw rate loops (1/s), acceleration inner loop only.</summary>
    public float RollRateBandwidth = 5f, PitchRateBandwidth = 4f, YawRateBandwidth = 2.5f;

    /// <summary>
    /// Time constant (s) of the aircraft + fly-by-wire rate response. Used as the synchronization model of the
    /// input feedback. Err on the slow side: a model that is slower than reality only costs a bit of speed,
    /// a model that is faster than reality can make the loop ring.
    /// </summary>
    public float RollLag = 0.12f, PitchLag = 0.12f, YawLag = 0.2f;

    /// <summary>Cut-off of the synchronized INDI feedback filters (rad/s), acceleration inner loop and speed.</summary>
    public float FilterCutoff = 20f;

    /// <summary>
    /// Hybrid INDI inversion error compensation (Pollack 2024, eq. 4.6): cut-off of the compensation filter Hc
    /// (rad/s) and compensation gain Kc (1 = full integral action). A low cut-off keeps the sensor based part out
    /// of the frequency range where the fly-by-wire / servo dynamics are poorly known.
    /// </summary>
    public float CompensationCutoff = 1.2f, CompensationGain = 1f;

    /// <summary>Safety factor on the rate per stick priors (> 1 = conservative).</summary>
    public float RateEffectivenessMargin = 1.3f;

    /// <summary>Online identification of the stick -> rate response (gain and lag) of every axis.</summary>
    public bool ResponseIdentification = true;

    /// <summary>Physics ticks between writing a stick value and seeing its effect in the angular acceleration.</summary>
    public int InputDelayTicks = 2;

    /// <summary>Ticks between writing the throttle and seeing the thrust change (engines read it the same tick).</summary>
    public int ThrottleDelayTicks = 1;

    /// <summary>
    /// First order lag (s) of the stick -> control surface path used to synchronize the INDI input feedback
    /// (the game's servos move at 25..90 deg/s).
    /// </summary>
    public float ActuatorTau = 0.05f;

    /// <summary>Online estimation of the control effectiveness (adaptive INDI).</summary>
    public bool OnlineEstimation = true;

    /// <summary>Fraction of the rate command derivative that is fed forward (0..1).</summary>
    public float RateFeedForward = 0.7f;

    /// <summary>Maximum stick deflection the controller may use per axis.</summary>
    public float PitchAuthority = 1f, RollAuthority = 1f, YawAuthority = 1f;

    /// <summary>Maximum stick movement per second (smoothness); 0 = unlimited.</summary>
    public float StickRateLimit = 4f;

    /// <summary>Maximum rate of change of the load factor command (g/s) and of the vertical speed command.</summary>
    public float LoadFactorRateLimit = 4f;

    // ---------------- attitude / load factor -------------------------------------------------------------
    /// <summary>Bank (lift vector roll) loop gain (1/s).</summary>
    public float BankGain = 2.2f;

    /// <summary>Limits used to shape roll rate commands.</summary>
    public float MaxRollRate = 3.14f, MaxRollAccel = 6.28f;   // 180 deg/s

    /// <summary>
    /// Load factor loop gain (1/s). Automatically capped to keep a time scale separation with the pitch rate
    /// response (at most 0.45 / pitch lag).
    /// </summary>
    public float LoadFactorGain = 2.5f;

    /// <summary>Sideslip loop gain (1/s). The yaw axis keeps the turn coordinated.</summary>
    public float SideslipGain = 1.2f;
    public float SkidAssist = 0.4f;          // rudder share of lateral force below corner speed (0 = pure bank)
    public bool HoverEnabled = true;         // thrust-vertical regime for aircraft with TWR > 1
    public float HoverSpeed = 35f;           // m/s, slow-hover regime boundary (enters below; leaves 8 m/s above)
    public bool StallAssist = true;          // thrust-vertical regime past the alpha limit (any fixed wing)

    public bool YawCoordination = true;

    /// <summary>Pitch attitude loop gain (1/s), used by the pitch attitude mode.</summary>
    public float PitchAttitudeGain = 1.5f;

    // path
    /// <summary>Altitude to vertical speed gain (1/s).</summary>
    public float AltitudeGain = 0.3f;

    /// <summary>
    /// Altitude-capture look-ahead (s).
    /// </summary>
    public float AltitudeCaptureLead = 1.2f;

    /// <summary>Vertical speed to vertical acceleration gain (1/s).</summary>
    public float VerticalSpeedGain = 1.0f;

    /// <summary>Vertical-speed integral gain multiplier and acceleration limit.</summary>
    public float VerticalSpeedIntegralGain = 0.1f, VerticalSpeedIntegralLimit = 1f;

    /// <summary>Vertical acceleration used to shape altitude captures (m/s^2).</summary>
    public float VerticalAccelShape = 4f;

    /// <summary>
    /// Default vertical acceleration for starting / stopping climbs and descents (m/s^2). 0.5 g gives a load factor
    /// between 0.5 and 1.5 g in normal autopilot manoeuvres.
    /// </summary>
    public float VerticalAccelDefault = 9.81f;

    /// <summary>Course to course rate gain (1/s).</summary>
    public float CourseGain = 0.6f;

    /// <summary>Roll rate (rad/s) assumed when shaping course captures (smooth roll in / roll out).</summary>
    public float CourseRollRate = 0.35f;

    /// <summary>Load factor window for autopilot manoeuvres (g).</summary>
    public float ManeuverMaxG = 5f, ManeuverMinG = -1.5f;

    /// <summary>Maximum flight path angle the vertical modes may command (rad).</summary>
    public float MaxFlightPathAngle = 1.2f;

    // speed
    /// <summary>Speed error to acceleration gain (1/s) and shaping acceleration (m/s^2).</summary>
    public float SpeedGain = 0.4f, SpeedAccelShape = 3f;

    /// <summary>Engine spool rate as throttle fraction per second (rate limited spool model).</summary>
    public float EngineSpoolRate = 0.5f;

    /// <summary>Scale of the estimated throttle effectiveness (lower = more aggressive).</summary>
    public float ThrottleEffectivenessScale = 1f;

    /// <summary>Filter cut-off for the speed channel (rad/s).</summary>
    public float SpeedFilterCutoff = 4f;

    /// <summary>
    /// Feed forward of flight path changes into the throttle (0..1): climbing needs power before speed is lost.
    /// </summary>
    public float EnergyFeedForward = 0.8f;

    public float ThrottleMin = 0.01f, ThrottleMax = 0.89f;

    // protections
    /// <summary>Angle of attack (rad) above which the controller stops pulling more.</summary>
    public float AlphaLimit = 0.4f;

    /// <summary>Climbing is progressively limited below this multiple of the landing speed.</summary>
    public float SpeedProtectionFactor = 1.25f;

}
