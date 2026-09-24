using UnityEngine;

namespace NOAutopilot.Core.Control;

/// <summary>
/// Measured aircraft state for one control tick.
/// </summary>
public struct FlightState
{
    public float Dt;

    // attitude (rad)
    public float Phi, Theta, Psi;

    // body rates (rad/s)
    public float P, Q, R;

    // air data
    public float V;          // true airspeed (m/s)
    public float Alpha;      // angle of attack (rad)
    public float Beta;       // sideslip (rad)
    public float Rho;        // air density (kg/m^3)
    public float Qbar;       // dynamic pressure (Pa)
    public Vector3 AirDir;   // unit vector of the air-relative velocity (world)

    // path (inertial, relative to the ground)
    public Vector3 Velocity;
    public float Speed;          // |Velocity|
    public float GroundSpeed;    // horizontal speed
    public float VerticalSpeed;  // m/s up
    public float Gamma;          // flight path angle (rad)
    public float Chi;            // course over ground (rad)

    // accelerations
    public Vector3 Accel;        // kinematic acceleration, world (m/s^2)
    public float Nx, Ny, Nz;     // specific force in body axes (g): forward, right, up
    public float NLift;          // specific force along the lift direction (perpendicular to airspeed) (g)
    public float Mu;             // bank of the lift vector about the air-relative velocity (rad)
    public float QPath;          // rotation rate of the velocity vector about the body pitch axis, nose up + (rad/s)
    public float RPath;          // rotation rate of the velocity vector about the body yaw axis, nose right + (rad/s)
    public float VDot;           // rate of change of airspeed (m/s^2)

    // position
    public Vector3 Position;     // world position (m); y = altitude above sea level
    public float Altitude;       // above sea level (m)
    public float RadarAltitude;  // above ground/ship (m)

    // body axes in world
    public Vector3 Forward, Up, Right;

    public bool OnGround;
    public bool GearDown;

    /// <summary>
    /// Builds the state from raw kinematics.
    /// </summary>
    /// <param name="position">World position, y = altitude above sea level.</param>
    /// <param name="forward">Body forward axis (world).</param>
    /// <param name="up">Body up axis (world).</param>
    /// <param name="right">Body right axis (world).</param>
    /// <param name="velocity">Inertial velocity (world).</param>
    /// <param name="angularVelocityWorld">Angular velocity (world, Unity convention).</param>
    /// <param name="accel">Kinematic acceleration (world, m/s^2).</param>
    /// <param name="wind">Wind velocity (world).</param>
    /// <param name="rho">Air density.</param>
    /// <param name="radarAltitude">Height above ground.</param>
    /// <param name="dt">Tick length.</param>
    public static FlightState Build(
        Vector3 position, Vector3 forward, Vector3 up, Vector3 right,
        Vector3 velocity, Vector3 angularVelocityWorld, Vector3 accel, Vector3 wind,
        float rho, float radarAltitude, float dt)
    {
        FlightState s = default;
        s.Dt = dt;
        s.Position = position;
        s.Altitude = position.y;
        s.RadarAltitude = radarAltitude;
        s.Forward = forward;
        s.Up = up;
        s.Right = right;
        s.Velocity = velocity;
        s.Accel = accel;
        s.Rho = Mathf.Max(rho, 1e-4f);

        // attitude
        s.Theta = ControlMath.SafeAsin(forward.y);
        s.Psi = Mathf.Atan2(forward.x, forward.z);
        if (s.Psi < 0f)
        {
            s.Psi += ControlMath.TwoPi;
        }

        s.Phi = Mathf.Atan2(-right.y, up.y);

        // body rates: Unity local x = right (positive = nose down), y = up (positive = nose right),
        // z = forward (positive = roll left)
        float wx = Vector3.Dot(angularVelocityWorld, right);
        float wy = Vector3.Dot(angularVelocityWorld, up);
        float wz = Vector3.Dot(angularVelocityWorld, forward);
        s.P = -wz;
        s.Q = -wx;
        s.R = wy;

        // inertial path
        s.Speed = velocity.magnitude;
        s.GroundSpeed = new Vector2(velocity.x, velocity.z).magnitude;
        s.VerticalSpeed = velocity.y;
        s.Gamma = s.Speed > 0.5f ? ControlMath.SafeAsin(velocity.y / s.Speed) : 0f;
        s.Chi = s.GroundSpeed > 0.5f ? Mathf.Atan2(velocity.x, velocity.z) : s.Psi;
        if (s.Chi < 0f)
        {
            s.Chi += ControlMath.TwoPi;
        }

        // air data
        Vector3 va = velocity - wind;
        s.V = va.magnitude;
        float u = Vector3.Dot(va, forward);
        float v = Vector3.Dot(va, right);
        float w = Vector3.Dot(va, up);
        s.Alpha = Mathf.Atan2(-w, Mathf.Max(u, 0.1f));
        s.Beta = Mathf.Atan2(v, Mathf.Max(u, 0.1f));
        s.Qbar = 0.5f * s.Rho * s.V * s.V;

        // specific force (g) = (a - g_vec)/g
        Vector3 f = (accel / ControlMath.G) + Vector3.up;
        s.Nx = Vector3.Dot(f, forward);
        s.Ny = Vector3.Dot(f, right);
        s.Nz = Vector3.Dot(f, up);

        Vector3 vHat = s.V > 1f ? va / s.V : forward;
        s.AirDir = vHat;

        // lift direction and lift bank angle
        Vector3 lift = ControlMath.SafeNormalize(Vector3.ProjectOnPlane(up, vHat), up);
        Vector3 vertRef = Vector3.ProjectOnPlane(Vector3.up, vHat);
        if (vertRef.sqrMagnitude < 1e-4f)
        {
            // flying vertically: use the body up axis as reference, bank is meaningless
            vertRef = lift;
        }

        vertRef.Normalize();
        Vector3 rightRef = Vector3.Cross(vertRef, vHat);
        s.Mu = Mathf.Atan2(Vector3.Dot(lift, rightRef), Vector3.Dot(lift, vertRef));
        s.NLift = Vector3.Dot(f, lift);

        // rotation of the velocity vector
        if (s.V > 1f)
        {
            Vector3 omegaV = Vector3.Cross(vHat, accel) / s.V;
            s.QPath = -Vector3.Dot(omegaV, right);
            s.RPath = Vector3.Dot(omegaV, up);
            s.VDot = Vector3.Dot(accel, vHat);
        }

        return s;
    }
}
