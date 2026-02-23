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
        Right = Vector3.Cross(Vector3.up, Forward).normalized;
        Up = Vector3.Cross(Forward, Right).normalized;
    }

    public (float yaw, float pitch, float roll) GetRelativeAngles(Vector3 direction, Quaternion rotation)
    {
        Vector3 localDir = Quaternion.Inverse(Quaternion.LookRotation(Forward, Up)) * direction.normalized;
        float yaw = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Atan2(localDir.y, localDir.z) * Mathf.Rad2Deg;
        Vector3 aircraftUp = rotation * Vector3.up;
        Vector3 localUp = Quaternion.Inverse(Quaternion.LookRotation(Forward, Up)) * aircraftUp;
        float roll = Mathf.Atan2(localUp.x, localUp.y) * Mathf.Rad2Deg;
        return (yaw, pitch, roll);
    }
}
