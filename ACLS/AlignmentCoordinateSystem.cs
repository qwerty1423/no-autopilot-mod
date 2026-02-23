using UnityEngine;

namespace NOAutopilot.ACLS;

public class AlignmentCoordinateSystem
{
    public Vector3 Forward { get; private set; }

    public Vector3 Right { get; private set; }

    public Vector3 Up { get; private set; }

    public void UpdateFromAlignment(Vector3 alignmentVector)
    {
        Forward = alignmentVector.normalized;
        Vector3 val = new(Forward.z, 0f, 0f - Forward.x);
        Right = val.normalized;
        Up = Vector3.Cross(Forward, Right);
    }

    public (float yaw, float pitch, float roll) GetRelativeAngles(Vector3 direction, Quaternion rotation)
    {
        direction = direction.normalized;
        Vector3 horizontalProj = Vector3.ProjectOnPlane(direction, Up);
        float yaw = Vector3.SignedAngle(Forward, horizontalProj, Up);
        float pitch = Vector3.SignedAngle(horizontalProj, direction, -Right);
        Vector3 aircraftUp = rotation * Vector3.up;
        Vector3 projectedUp = Vector3.ProjectOnPlane(aircraftUp, Forward);
        float roll = Vector3.SignedAngle(projectedUp, Up, Forward);

        return (yaw, pitch, roll);
    }
}
