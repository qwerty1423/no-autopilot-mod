using System;

using HarmonyLib;

using JetBrains.Annotations;

using UnityEngine;
using UnityEngine.UI;

namespace NOAutopilot.Core.Map;

[HarmonyPatch(typeof(DynamicMap), "CenterMinimizedMap")]
internal static class MinimapLayoutPatch
{
    private const float MapBorderPadding = 20f;

    private static RectTransform s_lowerLeftPanel;
    private static RectTransform s_hudMapAnchorRect;
    private static Image s_panelImage;
    private static Mask s_panelMask;
    private static RectMask2D s_panelRectMask;
    private static RectTransform s_canvasRect;

    private static Vector2? s_baseHudMapAnchorPos;

    public static void Reset()
    {
        s_lowerLeftPanel = null;
        s_hudMapAnchorRect = null;
        s_panelImage = null;
        s_panelMask = null;
        s_panelRectMask = null;
        s_canvasRect = null;
        s_baseHudMapAnchorPos = null;
    }

    [UsedImplicitly]
    private static void Postfix(
        DynamicMap __instance,
        GameObject ___hudMapAnchor,
        RectTransform ___mapRectTransform,
        RectTransform ___backgroundRectTransform)
    {
        if (Plugin.EnableMinimapLayoutPatch?.Value == false)
        {
            return;
        }

        if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
        {
            return;
        }

        try
        {
            if (DynamicMap.mapMaximized)
            {
                return;
            }

            if (__instance == null || ___hudMapAnchor == null || ___mapRectTransform == null || ___backgroundRectTransform == null)
            {
                return;
            }

            CombatHUD hud = SceneSingleton<CombatHUD>.i;
            if (hud == null || hud.aircraft?.disabled != false)
            {
                return;
            }

            if (!TryCacheRefs(___hudMapAnchor))
            {
                return;
            }

            ApplyPanelVisualSettings();
            ApplyMapSize(__instance, ___mapRectTransform, ___backgroundRectTransform);
            ApplyHudMapAnchorOffset();
            ClampMapToCanvas(___mapRectTransform, s_hudMapAnchorRect, s_canvasRect);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[MinimapLayoutPatch] Error: {ex}");
            Plugin.IsBroken = true;
        }
    }

    private static bool TryCacheRefs(GameObject hudMapAnchor)
    {
        s_hudMapAnchorRect ??= hudMapAnchor.GetComponent<RectTransform>();

        if (s_hudMapAnchorRect == null)
        {
            Plugin.Logger.LogWarning("[MinimapLayoutPatch] HUDMapAnchor RectTransform not found");
            return false;
        }

        if (s_baseHudMapAnchorPos == null)
        {
            s_baseHudMapAnchorPos = s_hudMapAnchorRect.anchoredPosition;
        }

        if (s_lowerLeftPanel == null)
        {
            Transform parent = hudMapAnchor.transform.parent;
            if (parent == null || parent.name != "LowerLeftPanel")
            {
                Plugin.Logger.LogWarning($"[MinimapLayoutPatch] Expected LowerLeftPanel, got '{parent?.name}'");
                return false;
            }

            s_lowerLeftPanel = parent as RectTransform;
            s_panelImage = parent.GetComponent<Image>();
            s_panelMask = parent.GetComponent<Mask>();
            s_panelRectMask = parent.GetComponent<RectMask2D>();
        }

        if (s_canvasRect == null)
        {
            Canvas canvas = hudMapAnchor.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                s_canvasRect = canvas.GetComponent<RectTransform>();
            }
        }

        return true;
    }

    private static void ApplyPanelVisualSettings()
    {
        bool hidePanel = Plugin.HideMinimapPanel?.Value ?? false;
        bool disableMask = Plugin.DisableMinimapPanelMask?.Value ?? hidePanel;

        if (s_panelImage != null)
        {
            s_panelImage.enabled = !hidePanel;
        }

        if (s_panelMask != null)
        {
            s_panelMask.enabled = !disableMask;
        }

        if (s_panelRectMask != null)
        {
            s_panelRectMask.enabled = !disableMask;
        }
    }

    private static void ApplyMapSize(
        DynamicMap map,
        RectTransform mapRectTransform,
        RectTransform backgroundRectTransform)
    {
        float size = Plugin.MinimapSize?.Value ?? 0f;
        if (size <= 0f)
        {
            return;
        }

        Vector2 mapSize = Vector2.one * size;
        mapRectTransform.sizeDelta = mapSize;
        backgroundRectTransform.sizeDelta = mapSize + new Vector2(MapBorderPadding, MapBorderPadding);

        map.mapScaleMinimized = size;
        map.mapScaleCurrent = size;
    }

    private static void ApplyHudMapAnchorOffset()
    {
        if (s_hudMapAnchorRect == null || s_baseHudMapAnchorPos == null)
        {
            return;
        }

        float offsetX = Plugin.MinimapOffsetX?.Value ?? 0f;
        float offsetY = Plugin.MinimapOffsetY?.Value ?? 0f;

        s_hudMapAnchorRect.anchoredPosition = s_baseHudMapAnchorPos.Value + new Vector2(offsetX, offsetY);
    }

    private static void ClampMapToCanvas(
    RectTransform mapRectTransform,
    RectTransform hudMapAnchorRect,
    RectTransform canvasRect)
    {
        if (!(Plugin.ClampMinimapToScreen?.Value ?? true))
        {
            return;
        }

        if (mapRectTransform == null || hudMapAnchorRect == null || canvasRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        // Get the center of the minimap in world space
        Vector3 mapCenter = mapRectTransform.position; // RectTransform.position is the pivot point (center by default)

        // Get canvas bounds
        Vector3[] canvasCorners = new Vector3[4];
        canvasRect.GetWorldCorners(canvasCorners);

        float canvasMinX = canvasCorners[0].x;
        float canvasMaxX = canvasCorners[2].x;
        float canvasMinY = canvasCorners[0].y;
        float canvasMaxY = canvasCorners[2].y;

        Vector3 delta = Vector3.zero;

        if (mapCenter.x < canvasMinX)
        {
            delta.x = canvasMinX - mapCenter.x;
        }
        else if (mapCenter.x > canvasMaxX)
        {
            delta.x = canvasMaxX - mapCenter.x;
        }

        if (mapCenter.y < canvasMinY)
        {
            delta.y = canvasMinY - mapCenter.y;
        }
        else if (mapCenter.y > canvasMaxY)
        {
            delta.y = canvasMaxY - mapCenter.y;
        }

        if (delta != Vector3.zero)
        {
            hudMapAnchorRect.position += delta;
        }
    }
}
