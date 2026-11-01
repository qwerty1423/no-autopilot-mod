using System;

using HarmonyLib;

using JetBrains.Annotations;

using RoadPathfinding;

namespace NOAutopilot.Core.Flight;

[HarmonyPatch(typeof(Airbase), nameof(Airbase.GetTaxiNetwork))]
internal static class TaxiNetworkPatch
{
    [UsedImplicitly]
    private static void Postfix(RoadNetwork __result)
    {
        if (__result == null || __result.roads.Count == 0 || __result.nodes.Count > 0)
        {
            return;
        }

        try
        {
            __result.RegenerateNetwork();
            Plugin.Logger.LogInfo("[TaxiNetworkPatch] Regenerated client taxi network nodes.");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[TaxiNetworkPatch] Failed to regenerate taxi network: {ex}");
            Plugin.IsBroken = true;
        }
    }
}

[HarmonyPatch(typeof(PathfindingAgent), nameof(PathfindingAgent.Pathfind))]
internal static class TaxiPathfindingGuardPatch
{
    [UsedImplicitly]
    private static void Prefix(ref RoadNetwork network)
    {
        if (network == null || network.roads.Count == 0)
        {
            return;
        }

        bool missingNodes = network.nodes.Count == 0;
        if (!missingNodes)
        {
            foreach (Road road in network.roads)
            {
                if (road.startNode == null || road.endNode == null)
                {
                    missingNodes = true;
                    break;
                }
            }
        }

        if (!missingNodes)
        {
            return;
        }

        try
        {
            network.RegenerateNetwork();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[TaxiNetworkPatch] Could not rebuild taxi network: {ex}");
            Plugin.IsBroken = true;
            network = null;
        }
    }
}

[HarmonyPatch(typeof(RoadPathfinder), nameof(RoadPathfinder.TryPathfind))]
internal static class TaxiRoadPathfindGuardPatch
{
    [UsedImplicitly]
    private static bool Prefix(RoadNetwork network, GlobalPosition startPos, GlobalPosition targetPos,
        ref RoadPathfinder.PathfindResult result)
    {
        if (network == null || network.roads.Count == 0)
        {
            result = RoadPathfinder.PathfindResult.NoNetwork;
            return false;
        }

        bool hasRoad = network.TryGetNearestRoad(startPos, out Road startRoad);
        if (hasRoad && (startRoad.startNode == null || startRoad.endNode == null))
        {
            Plugin.Logger.LogError("[TaxiNetworkPatch] Taxi network has a road without nodes.");
            Plugin.IsBroken = true;
            result = RoadPathfinder.PathfindResult.NoConnection;
            return false;
        }

        if (!hasRoad && !RoadPathfinder.TryFindNearestNode(network, startPos, out _))
        {
            result = RoadPathfinder.PathfindResult.NoConnection;
            return false;
        }

        bool hasTargetRoad = network.TryGetNearestRoad(targetPos, out Road targetRoad);
        if (hasTargetRoad && (targetRoad.startNode == null || targetRoad.endNode == null))
        {
            Plugin.Logger.LogError("[TaxiNetworkPatch] Taxi target road has no nodes.");
            Plugin.IsBroken = true;
            result = RoadPathfinder.PathfindResult.NoConnection;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(AIPilotTaxiState), "FixedUpdateState")]
internal static class TaxiStateFailurePatch
{
    [UsedImplicitly]
    private static Exception Finalizer(Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        Plugin.Logger.LogError($"[TaxiNetworkPatch] Taxi state failed: {__exception}");
        Plugin.IsBroken = true;
        return null;
    }
}
