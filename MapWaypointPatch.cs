extern alias JetBrains;

using System;

using HarmonyLib;

using JetBrains.Annotations;

using UnityEngine;

namespace NOAutopilot;

[HarmonyPatch(typeof(DynamicMap), "UpdateMap")]
internal static class MapWaypointPatch
{
    public static void Reset() { }

    [UsedImplicitly]
    private static void Postfix()
    {
        if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
        {
            return;
        }

        try
        {
            if (APData.NavVisuals.Count == 0 || APData.PlayerRB == null)
            {
                return;
            }

            DynamicMap map = SceneSingleton<DynamicMap>.i;
            if (map == null || map.mapImage == null)
            {
                return;
            }

            float zoom = map.mapImage.transform.localScale.x;
            float invZoom = 1f / zoom;
            float factor = 900f / map.mapDimension;

            GameObject playerLine = null;

            foreach (GameObject obj in APData.NavVisuals)
            {
                if (obj == null)
                {
                    continue;
                }

                if (obj.name == "AP_NavMarker")
                {
                    obj.transform.localScale = Vector3.one * invZoom;
                }
                else
                {
                    obj.transform.localScale = new Vector3(4f * invZoom, obj.transform.localScale.y, 4f * invZoom);
                    if (obj.name == "AP_NavLine_Player")
                    {
                        playerLine = obj;
                    }
                }
            }

            if (playerLine == null || APData.NavQueue.Count == 0)
            {
                return;
            }

            Vector3 pG = APData.PlayerRB.position.ToGlobalPosition().AsVector3();
            Vector3 pMap = new(pG.x * factor, pG.z * factor, 0f);
            Vector3 targetMap = new(APData.NavQueue[0].x * factor, APData.NavQueue[0].z * factor, 0f);

            playerLine.transform.localPosition = targetMap;

            float angle = (-Mathf.Atan2(targetMap.x - pMap.x, targetMap.y - pMap.y) * Mathf.Rad2Deg) + 180f;

            playerLine.transform.localEulerAngles = new Vector3(0, 0, angle);

            playerLine.transform.localScale =
                new Vector3(4f * invZoom, Vector3.Distance(pMap, targetMap), 4f * invZoom);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[MapWaypointPatch] Error: {ex}");
            Plugin.IsBroken = true;
        }
    }
}
