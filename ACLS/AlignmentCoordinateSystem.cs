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
        Vector3 val = Vector3.ProjectOnPlane(direction, Up);
        float item = Vector3.SignedAngle(Forward, val, Up);
        float item2 = Vector3.SignedAngle(direction, val, Right);
        Vector3 val2 = rotation * Vector3.up;
        Vector3 val3 = Vector3.ProjectOnPlane(val2, Forward);
        float item3 = Vector3.SignedAngle(val3, Up, Forward);
        return (yaw: item, pitch: item2, roll: item3);
    }
}
