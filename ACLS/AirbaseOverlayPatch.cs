using System;
using UnityEngine;
namespace NOAutopilot.ACLS;
internal class AirbaseOverlayManager
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

    public static void Reset()
    {
        isActive = false;
        alignmentCoordinateSystem = new AlignmentCoordinateSystem();
        runwayCoordinateSystem = new AlignmentCoordinateSystem();
        glideslopeCoordinateSystem = new AlignmentCoordinateSystem();
    }

    public static void UpdateACLSData(AirbaseOverlay overlay, Aircraft aircraft)
    {
        try
        {
            if (overlay == null || !overlay.runwayUsage.HasValue || aircraft == null)
            {
                isActive = false;
                return;
            }
            var usage = overlay.runwayUsage.Value;
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
            float timeToLand = dist / Mathf.Max(dot, 1f);
            Vector3 aimpoint = runway.GetGlideslopeAimpoint(
                aircraft,
                dist,
                isReverse,
                timeToLand
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
            Plugin.Logger.LogError($"[UpdateACLSData] Error: {ex}");
            Plugin.IsBroken = true;
        }
    }
}
