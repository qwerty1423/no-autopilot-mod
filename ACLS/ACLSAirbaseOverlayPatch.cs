using System;
using HarmonyLib;
using UnityEngine;

namespace NOAutopilot.ACLS;

[HarmonyPatch(typeof(AirbaseOverlay), "DrawGlideslope")]
internal class ACLSAirbaseOverlayPatch
{
    public static Vector3 alignmentVector;

    public static AlignmentCoordinateSystem alignmentCoordinateSystem = new();

    public static AlignmentCoordinateSystem runwayCoordinateSystem = new();

    public static AlignmentCoordinateSystem glideslopeCoordinateSystem = new();

    public static Vector3 glideslopeDirection;

    public static bool isActive = false;

    public static float runwayAltitude;

    public static float distanceToLand;

    public static Vector3 towardsRunway;

    private static void Postfix(AirbaseOverlay __instance, Aircraft aircraft)
    {
        Traverse val = Traverse.Create(__instance).Field("runwayUsage");
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
        Vector3 val3 = value ? position : position2;
        float num = FastMath.Distance(aircraft.transform.position, val3);
        Vector3 value2 = val2.Method("GetVelocity", Array.Empty<object>()).GetValue<Vector3>();
        Vector3 val4 = aircraft.rb.velocity - value2;
        Vector3 val5 = val3 - aircraft.transform.position;
        float num2 = Vector3.Dot(val4, val5.normalized);
        float num3 = num / num2;
        Vector3 value3 = val2.Method("GetGlideslopeAimpoint",
        [
            aircraft,
            num * 0.9f,
            value,
            num3 * 0.9f
        ]).GetValue<Vector3>();
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
        Traverse val = Traverse.Create(__instance).Field("runwayUsage");
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
