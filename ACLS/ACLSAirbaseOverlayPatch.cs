using System;
using HarmonyLib;
using UnityEngine;

namespace NOAutopilot.ACLS;

[HarmonyPatch(typeof(AirbaseOverlay), "DrawGlideslope")]
internal class ACLSAirbaseOverlayPatch
{
    public static Vector3 alignmentVector;

    public static AlignmentCoordinateSystem alignmentCoordinateSystem = new AlignmentCoordinateSystem();

    public static AlignmentCoordinateSystem runwayCoordinateSystem = new AlignmentCoordinateSystem();

    public static AlignmentCoordinateSystem glideslopeCoordinateSystem = new AlignmentCoordinateSystem();

    public static Vector3 glideslopeDirection;

    public static bool isActive = false;

    public static float runwayAltitude;

    public static float distanceToLand;

    public static Vector3 towardsRunway;

    private static void Postfix(AirbaseOverlay __instance, Aircraft aircraft)
    {
        //IL_003f: Unknown result type (might be due to invalid IL or missing references)
        //IL_0044: Unknown result type (might be due to invalid IL or missing references)
        //IL_0055: Unknown result type (might be due to invalid IL or missing references)
        //IL_005a: Unknown result type (might be due to invalid IL or missing references)
        //IL_005c: Unknown result type (might be due to invalid IL or missing references)
        //IL_005d: Unknown result type (might be due to invalid IL or missing references)
        //IL_005f: Unknown result type (might be due to invalid IL or missing references)
        //IL_0064: Unknown result type (might be due to invalid IL or missing references)
        //IL_0084: Unknown result type (might be due to invalid IL or missing references)
        //IL_0071: Unknown result type (might be due to invalid IL or missing references)
        //IL_0073: Unknown result type (might be due to invalid IL or missing references)
        //IL_0074: Unknown result type (might be due to invalid IL or missing references)
        //IL_0079: Unknown result type (might be due to invalid IL or missing references)
        //IL_0096: Unknown result type (might be due to invalid IL or missing references)
        //IL_0092: Unknown result type (might be due to invalid IL or missing references)
        //IL_0097: Unknown result type (might be due to invalid IL or missing references)
        //IL_009f: Unknown result type (might be due to invalid IL or missing references)
        //IL_00a4: Unknown result type (might be due to invalid IL or missing references)
        //IL_00bd: Unknown result type (might be due to invalid IL or missing references)
        //IL_00c2: Unknown result type (might be due to invalid IL or missing references)
        //IL_00ca: Unknown result type (might be due to invalid IL or missing references)
        //IL_00cf: Unknown result type (might be due to invalid IL or missing references)
        //IL_00d1: Unknown result type (might be due to invalid IL or missing references)
        //IL_00d6: Unknown result type (might be due to invalid IL or missing references)
        //IL_00de: Unknown result type (might be due to invalid IL or missing references)
        //IL_00e3: Unknown result type (might be due to invalid IL or missing references)
        //IL_00e8: Unknown result type (might be due to invalid IL or missing references)
        //IL_00ec: Unknown result type (might be due to invalid IL or missing references)
        //IL_013d: Unknown result type (might be due to invalid IL or missing references)
        //IL_0142: Unknown result type (might be due to invalid IL or missing references)
        //IL_014a: Unknown result type (might be due to invalid IL or missing references)
        //IL_014f: Unknown result type (might be due to invalid IL or missing references)
        //IL_0151: Unknown result type (might be due to invalid IL or missing references)
        //IL_0153: Unknown result type (might be due to invalid IL or missing references)
        //IL_0155: Unknown result type (might be due to invalid IL or missing references)
        //IL_015a: Unknown result type (might be due to invalid IL or missing references)
        //IL_015e: Unknown result type (might be due to invalid IL or missing references)
        //IL_0163: Unknown result type (might be due to invalid IL or missing references)
        //IL_016d: Unknown result type (might be due to invalid IL or missing references)
        //IL_0178: Unknown result type (might be due to invalid IL or missing references)
        //IL_017f: Unknown result type (might be due to invalid IL or missing references)
        //IL_0193: Unknown result type (might be due to invalid IL or missing references)
        //IL_0195: Unknown result type (might be due to invalid IL or missing references)
        //IL_0197: Unknown result type (might be due to invalid IL or missing references)
        //IL_019c: Unknown result type (might be due to invalid IL or missing references)
        //IL_01a0: Unknown result type (might be due to invalid IL or missing references)
        //IL_01a5: Unknown result type (might be due to invalid IL or missing references)
        //IL_01af: Unknown result type (might be due to invalid IL or missing references)
        Traverse val = Traverse.Create((object)__instance).Field("runwayUsage");
        bool value = val.Field("Reverse").GetValue<bool>();
        Traverse val2 = val.Field("Runway");
        Vector3 position = val2.Field("End").GetValue<Transform>().position;
        Vector3 position2 = val2.Field("Start").GetValue<Transform>().position;
        alignmentVector = position - position2;
        if (value)
        {
            alignmentVector = position2 - position;
        }
        alignmentCoordinateSystem.UpdateFromAlignment(alignmentVector);
        Vector3 val3 = (value ? position : position2);
        float num = FastMath.Distance(((Component)aircraft).transform.position, val3);
        Vector3 value2 = val2.Method("GetVelocity", Array.Empty<object>()).GetValue<Vector3>();
        Vector3 val4 = ((Unit)aircraft).rb.velocity - value2;
        Vector3 val5 = val3 - ((Component)aircraft).transform.position;
        float num2 = Vector3.Dot(val4, val5.normalized);
        float num3 = num / num2;
        Vector3 value3 = val2.Method("GetGlideslopeAimpoint", new object[4]
        {
            aircraft,
            num * 0.9f,
            value,
            num3 * 0.9f
        }).GetValue<Vector3>();
        Vector3 position3 = aircraft.CockpitRB().position;
        val5 = value3 - position3;
        glideslopeDirection = val5.normalized;
        glideslopeCoordinateSystem.UpdateFromAlignment(glideslopeDirection);
        runwayAltitude = position3.y - val3.y;
        distanceToLand = num;
        val5 = val3 - position3;
        towardsRunway = val5.normalized;
        runwayCoordinateSystem.UpdateFromAlignment(towardsRunway);
        isActive = true;
    }

    public static Vector3 AlignmentVector(AirbaseOverlay __instance)
    {
        //IL_003f: Unknown result type (might be due to invalid IL or missing references)
        //IL_0044: Unknown result type (might be due to invalid IL or missing references)
        //IL_0055: Unknown result type (might be due to invalid IL or missing references)
        //IL_005a: Unknown result type (might be due to invalid IL or missing references)
        //IL_005c: Unknown result type (might be due to invalid IL or missing references)
        //IL_005d: Unknown result type (might be due to invalid IL or missing references)
        //IL_005f: Unknown result type (might be due to invalid IL or missing references)
        //IL_0064: Unknown result type (might be due to invalid IL or missing references)
        //IL_007b: Unknown result type (might be due to invalid IL or missing references)
        //IL_0080: Unknown result type (might be due to invalid IL or missing references)
        //IL_006e: Unknown result type (might be due to invalid IL or missing references)
        //IL_0070: Unknown result type (might be due to invalid IL or missing references)
        //IL_0071: Unknown result type (might be due to invalid IL or missing references)
        //IL_0076: Unknown result type (might be due to invalid IL or missing references)
        //IL_0084: Unknown result type (might be due to invalid IL or missing references)
        Traverse val = Traverse.Create((object)__instance).Field("runwayUsage");
        bool value = val.Field("Reverse").GetValue<bool>();
        Traverse val2 = val.Field("Runway");
        Vector3 position = val2.Field("End").GetValue<Transform>().position;
        Vector3 position2 = val2.Field("Start").GetValue<Transform>().position;
        Vector3 val3 = position - position2;
        if (value)
        {
            val3 = position2 - position;
        }
        return val3.normalized;
    }
}
