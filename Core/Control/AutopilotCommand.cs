namespace NOAutopilot.Core.Control;

public enum VerticalMode
{
    None = 0,
    Altitude,
    VerticalSpeed,
    FlightPathAngle,
    PitchAttitude,
    LoadFactor,
    Acceleration
}

public enum LateralMode
{
    None = 0,
    Course,
    Bank,
    Acceleration
}

public enum SpeedMode
{
    None = 0,
    Airspeed,
    Throttle
}

public struct AutopilotCommand
{
    // vertical
    public VerticalMode Vertical;
    public float Altitude;
    public float VerticalSpeed;
    public float MaxClimbRate;
    public float MaxDescentRate;
    public float VerticalAccelUp, VerticalAccelDown;
    public float FlightPathAngle;
    public float PitchAttitude;
    public float LoadFactor;
    public float VerticalAccel;

    // lateral
    public LateralMode Lateral;
    public float Course;
    public float Bank;
    public float BankLimit;
    public float LateralAccel;

    public bool AggressiveRoll;
    public bool NoSkidAssist;

    // speed
    public SpeedMode Speed;
    public float Airspeed;
    public float Throttle;

    /// <summary>Allow afterburner / airbrake.</summary>
    public bool AllowExtremeThrottle;

    public float ThrottleMinOverride, ThrottleMaxOverride;

    /// <summary>
    /// speed priority: with the throttle saturated, climbs/descents only use the spare energy so the speed
    /// is held. Off = altitude priority.
    /// </summary>
    public bool SpeedPriority;

    /// <summary>
    /// Hold ground-relative speed instead of true airspeed.
    /// </summary>
    public bool SpeedIsInertial;

    /// <summary>Overrides of the load factor window (g)</summary>
    public float MaxG, MinG;

    /// <summary>Feed forward terms from guidance laws.</summary>
    public float VerticalSpeedFeedForward, CourseRateFeedForward;

    // direct inner loop commands
    public float RollRateOverride, PitchRateOverride, YawRateOverride, SideslipOverride;

    public float TouchdownBankLimit;

    /// <summary>
    /// Direct stick outputs.
    /// </summary>
    public float DirectPitch, DirectRoll, DirectYaw;

    public static AutopilotCommand Empty()
    {
        return new AutopilotCommand
        {
            MaxG = float.NaN,
            MinG = float.NaN,
            VerticalAccelUp = float.NaN,
            VerticalAccelDown = float.NaN,
            ThrottleMinOverride = float.NaN,
            ThrottleMaxOverride = float.NaN,
            RollRateOverride = float.NaN,
            PitchRateOverride = float.NaN,
            YawRateOverride = float.NaN,
            SideslipOverride = float.NaN,
            TouchdownBankLimit = float.NaN,
            DirectPitch = float.NaN,
            DirectRoll = float.NaN,
            DirectYaw = float.NaN,
            BankLimit = 0.6f,
            MaxClimbRate = 10f,
            MaxDescentRate = 10f
        };
    }

    public readonly bool PitchAxisActive =>
        Vertical != VerticalMode.None || !float.IsNaN(PitchRateOverride) || !float.IsNaN(DirectPitch);

    public readonly bool RollAxisActive =>
        Lateral != LateralMode.None || !float.IsNaN(RollRateOverride) || !float.IsNaN(DirectRoll);
}

/// <summary>Result of one controller tick.</summary>
public struct ControlOutput
{
    public bool PitchActive, RollActive, YawActive, ThrottleActive;
    public float Pitch, Roll, Yaw, Throttle;
}
