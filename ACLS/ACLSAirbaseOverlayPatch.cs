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
        try
        {
            if (!__instance.runwayUsage.HasValue) return;

            var usage = __instance.runwayUsage.Value;
            bool isReverse = usage.Reverse;
            var runway = usage.Runway;

            Vector3 posEnd = runway.End.position;
            Vector3 posStart = runway.Start.position;

            alignmentVector = posEnd - posStart;
            if (isReverse)
            {
                alignmentVector = posStart - posEnd;
            }

            alignmentCoordinateSystem.UpdateFromAlignment(alignmentVector);

            Vector3 targetPos = isReverse ? posEnd : posStart;
            float dist = FastMath.Distance(aircraft.transform.position, targetPos);

            Vector3 runwayVel = runway.GetVelocity();
            Vector3 relativeVel = aircraft.rb.velocity - runwayVel;
            Vector3 toRunway = targetPos - aircraft.transform.position;

            float dot = Vector3.Dot(relativeVel, toRunway.normalized);
            float timeToLand = dist / dot;

            Vector3 aimpoint = runway.GetGlideslopeAimpoint(
                aircraft,
                dist * 0.9f,
                isReverse,
                timeToLand * 0.9f
            );

            Vector3 cockpitPos = aircraft.CockpitRB().position;
            Vector3 aimDir = aimpoint - cockpitPos;

            glideslopeDirection = aimDir.normalized;
            glideslopeCoordinateSystem.UpdateFromAlignment(glideslopeDirection);

            runwayAltitude = cockpitPos.y - targetPos.y;
            distanceToLand = dist;
            towardsRunway = (targetPos - cockpitPos).normalized;
            runwayCoordinateSystem.UpdateFromAlignment(towardsRunway);

            isActive = true;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[ACLSAirbaseOverlayPatch] Error: {ex}");
            Plugin.IsBroken = true;
        }
    }

    public static Vector3 AlignmentVector(AirbaseOverlay __instance)
    {
        if (!__instance.runwayUsage.HasValue) return Vector3.forward;

        var usage = __instance.runwayUsage.Value;
        var runway = usage.Runway;

        Vector3 vec = runway.End.position - runway.Start.position;
        if (usage.Reverse)
        {
            vec = runway.Start.position - runway.End.position;
        }
        return vec.normalized;
    }
}
