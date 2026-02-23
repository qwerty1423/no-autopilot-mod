using UnityEngine;

namespace NOAutopilot.ACLS;

public class AlignmentCoordinateSystem
{
    public Vector3 Forward { get; private set; }

    public Vector3 Right { get; private set; }

    public Vector3 Up { get; private set; }

    public void UpdateFromAlignment(Vector3 alignmentVector)
    {
        //IL_0004: Unknown result type (might be due to invalid IL or missing references)
        //IL_0011: Unknown result type (might be due to invalid IL or missing references)
        //IL_0021: Unknown result type (might be due to invalid IL or missing references)
        //IL_002c: Unknown result type (might be due to invalid IL or missing references)
        //IL_0031: Unknown result type (might be due to invalid IL or missing references)
        //IL_0034: Unknown result type (might be due to invalid IL or missing references)
        //IL_0041: Unknown result type (might be due to invalid IL or missing references)
        //IL_0047: Unknown result type (might be due to invalid IL or missing references)
        //IL_004c: Unknown result type (might be due to invalid IL or missing references)
        Forward = alignmentVector.normalized;
        Vector3 val = new Vector3(Forward.z, 0f, 0f - Forward.x);
        Right = val.normalized;
        Up = Vector3.Cross(Forward, Right);
    }

    public (float yaw, float pitch, float roll) GetRelativeAngles(Vector3 direction, Quaternion rotation)
    {
        //IL_0003: Unknown result type (might be due to invalid IL or missing references)
        //IL_0008: Unknown result type (might be due to invalid IL or missing references)
        //IL_000a: Unknown result type (might be due to invalid IL or missing references)
        //IL_000c: Unknown result type (might be due to invalid IL or missing references)
        //IL_0011: Unknown result type (might be due to invalid IL or missing references)
        //IL_0016: Unknown result type (might be due to invalid IL or missing references)
        //IL_0018: Unknown result type (might be due to invalid IL or missing references)
        //IL_001d: Unknown result type (might be due to invalid IL or missing references)
        //IL_001f: Unknown result type (might be due to invalid IL or missing references)
        //IL_002a: Unknown result type (might be due to invalid IL or missing references)
        //IL_002b: Unknown result type (might be due to invalid IL or missing references)
        //IL_002d: Unknown result type (might be due to invalid IL or missing references)
        //IL_0038: Unknown result type (might be due to invalid IL or missing references)
        //IL_0039: Unknown result type (might be due to invalid IL or missing references)
        //IL_003e: Unknown result type (might be due to invalid IL or missing references)
        //IL_0043: Unknown result type (might be due to invalid IL or missing references)
        //IL_0044: Unknown result type (might be due to invalid IL or missing references)
        //IL_0046: Unknown result type (might be due to invalid IL or missing references)
        //IL_004b: Unknown result type (might be due to invalid IL or missing references)
        //IL_0050: Unknown result type (might be due to invalid IL or missing references)
        //IL_0052: Unknown result type (might be due to invalid IL or missing references)
        //IL_0055: Unknown result type (might be due to invalid IL or missing references)
        //IL_005b: Unknown result type (might be due to invalid IL or missing references)
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
