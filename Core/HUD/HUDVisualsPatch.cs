extern alias JetBrains;

using System;
using System.Text;

using HarmonyLib;

using JetBrains.Annotations;

using NOAutopilot.Core.Flight;

using UnityEngine;
using UnityEngine.UI;

using Object = UnityEngine.Object;

namespace NOAutopilot.Core.HUD;

[HarmonyPatch(typeof(FlightHud), "Update")]
internal static class HUDVisualsPatch
{
    private static GameObject s_infoOverlayObj;
    private static Text s_overlayText;

    private static GameObject s_gcasLeftObj;
    private static GameObject s_gcasRightObj;
    private static GameObject s_gcasTopObj;
    private static Text s_gcasLeftText;
    private static Text s_gcasRightText;
    private static Text s_gcasTopText;
    private static float s_smoothedConverge;

    private static float s_lastFuelMass;
    private static float s_fuelFlowEma;
    private static float s_lastUpdateTime;

    private static float s_lastStringUpdate;
    private static FuelGauge s_cachedFuelGauge;
    private static Text s_cachedRefLabel;
    private static Vector3 s_fuelLabelPosOffset;
    private static readonly StringBuilder SbHud = new(1024);
    private static GameObject s_lastVehicleChecked;

    public static void Reset()
    {
        if (s_infoOverlayObj != null)
        {
            Object.Destroy(s_infoOverlayObj);
        }

        s_infoOverlayObj = null;
        s_overlayText = null;

        if (s_gcasLeftObj)
        {
            Object.Destroy(s_gcasLeftObj);
        }

        if (s_gcasRightObj)
        {
            Object.Destroy(s_gcasRightObj);
        }

        if (s_gcasTopObj)
        {
            Object.Destroy(s_gcasTopObj);
        }

        s_gcasLeftObj = null;
        s_gcasRightObj = null;
        s_gcasTopObj = null;
        s_smoothedConverge = 0f;

        s_lastFuelMass = 0f;
        s_fuelFlowEma = 0f;
        s_lastUpdateTime = 0f;
        s_lastStringUpdate = 0f;
        s_cachedFuelGauge = null;
        s_cachedRefLabel = null;
        s_lastVehicleChecked = null;
        SbHud.Clear();
    }

    [UsedImplicitly]
    private static void Postfix(FlightHud __instance)
    {
        if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
        {
            return;
        }

        if (!Plugin.ShowExtraInfo.Value)
        {
            return;
        }

        try
        {
            if (__instance == null)
            {
                return;
            }

            Aircraft vehicleRaw = __instance.aircraft;
            Object unityObj = vehicleRaw;

            if (unityObj == null)
            {
                return;
            }

            if (vehicleRaw is not Component vehicleComponent)
            {
                return;
            }

            GameObject currentVehicleObj = vehicleComponent.gameObject;
            if (currentVehicleObj == null)
            {
                return;
            }

            if (s_lastVehicleChecked != currentVehicleObj || s_cachedFuelGauge == null)
            {
                s_lastVehicleChecked = currentVehicleObj;
                s_cachedFuelGauge = __instance.GetComponentInChildren<FuelGauge>(true);

                if (s_cachedFuelGauge != null)
                {
                    s_cachedRefLabel = s_cachedFuelGauge.fuelLabel;
                    s_fuelLabelPosOffset = __instance.GetHUDCenter()
                        .InverseTransformPoint(s_cachedRefLabel.transform.position);
                }
            }

            if (s_cachedFuelGauge == null || s_cachedRefLabel == null)
            {
                return;
            }

            if (!s_infoOverlayObj)
            {
                s_infoOverlayObj = Object.Instantiate(s_cachedRefLabel.gameObject, __instance.GetHUDCenter());
                s_infoOverlayObj.name = "AP_CombinedOverlay";
                s_infoOverlayObj.transform.localPosition = s_fuelLabelPosOffset;
                s_overlayText = s_infoOverlayObj.GetComponent<Text>();
                s_overlayText.resizeTextForBestFit = false;
                s_overlayText.supportRichText = true;
                s_overlayText.alignment = TextAnchor.UpperLeft;
                s_overlayText.horizontalOverflow = HorizontalWrapMode.Overflow;
                s_overlayText.verticalOverflow = VerticalWrapMode.Overflow;
                RectTransform rect = s_infoOverlayObj.GetComponent<RectTransform>();
                rect.pivot = new Vector2(0, 1);
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(0, 1);
                rect.localScale = s_cachedRefLabel.transform.localScale;
                rect.localRotation = Quaternion.identity;
                s_infoOverlayObj.SetActive(true);
            }

            float currentSize = PlayerSettings.hudTextSize;
            float scaleRatio = currentSize / 40f;
            s_overlayText.fontSize = (int)currentSize;

            // Vector3 refLocalPos = _cachedRefLabel.transform.localPosition;
            float finalX = Plugin.OverlayOffsetX.Value * scaleRatio;
            float finalY = Plugin.OverlayOffsetY.Value * scaleRatio;
            s_infoOverlayObj.transform.localPosition = s_fuelLabelPosOffset + new Vector3(finalX, finalY, 0);

            Aircraft aircraft = APData.LocalAircraft;
            if (aircraft != null)
            {
                float currentFuel = aircraft.fuelCapacity * aircraft.GetFuelLevel();
                float time = Time.time;
                if (s_lastUpdateTime != 0f && s_lastFuelMass > 0f)
                {
                    float dt = time - s_lastUpdateTime;
                    if (dt >= Plugin.FuelUpdateInterval.Value)
                    {
                        float burned = s_lastFuelMass - currentFuel;
                        float flow = Mathf.Max(0f, burned / dt);
                        s_fuelFlowEma = Mathf.Lerp(s_fuelFlowEma, flow, Plugin.FuelSmoothing.Value);
                        s_lastUpdateTime = time;
                        s_lastFuelMass = currentFuel;
                    }
                }
                else
                {
                    s_lastUpdateTime = time;
                    s_lastFuelMass = currentFuel;
                }

                if (Time.time - s_lastStringUpdate >= Plugin.DisplayUpdateInterval.Value)
                {
                    s_lastStringUpdate = Time.time;
                    SbHud.Clear();

                    if (Plugin.ShowFuelOverlay.Value)
                    {
                        if (currentFuel <= 0f)
                        {
                            string sTime = "";
                            try
                            {
                                sTime = TimeSpan.FromSeconds(0).ToString(Plugin.FuelFormatString.Value);
                            }
                            catch (FormatException) { }

                            SbHud.Append("<color=").Append(Plugin.ColorCrit.Value).Append(">").Append(sTime)
                                .Append("\n----</color>\n");
                        }
                        else
                        {
                            float calcFlow = Mathf.Max(s_fuelFlowEma, 0.0001f);
                            float secs = currentFuel / calcFlow;
                            string sTime = "";
                            try
                            {
                                sTime = TimeSpan.FromSeconds(Mathf.Min(secs, 359999f))
                                    .ToString(Plugin.FuelFormatString.Value);
                            }
                            catch (FormatException) { }

                            float mins = secs / 60f;

                            string fuelCol = Plugin.ColorGood.Value;
                            if (mins < Plugin.FuelCritMinutes.Value)
                            {
                                fuelCol = Plugin.ColorCrit.Value;
                            }
                            else if (mins < Plugin.FuelWarnMinutes.Value)
                            {
                                fuelCol = Plugin.ColorWarn.Value;
                            }

                            SbHud.Append("<color=").Append(fuelCol).Append(">").Append(sTime).Append("</color>\n");

                            // float spd = aircraft.rb != null ? aircraft.rb.velocity.magnitude : 0f;
                            float distMeters = secs * APData.SpeedEma;
                            if (distMeters > 99999000f)
                            {
                                distMeters = 99999000f;
                            }

                            string sRange = ModUtils.ProcessGameString(UnitConverter.DistanceReading(distMeters),
                                Plugin.DistShowUnit.Value);
                            SbHud.Append("<color=").Append(Plugin.ColorRange.Value).Append(">").Append(sRange)
                                .Append("</color>\n\n");
                        }
                    }

                    // (AP was on before GCAS) or (AP is on and no GCAS)
                    bool apActive = (ControlOverridePatch.ApStateBeforeGCAS && APData.GCASActive) ||
                                    (APData.Enabled && !APData.GCASActive);
                    bool speedActive = APData.TargetSpeed >= 0f;

                    if ((apActive || speedActive) && Plugin.ShowAPOverlay.Value)
                    {
                        bool placeholders = Plugin.ShowPlaceholders.Value;

                        if (apActive || speedActive)
                        {
                            SbHud.Append("<color=").Append(Plugin.ColorAPOn.Value).Append(">");
                        }

                        bool hasLine1 = false;
                        if (speedActive)
                        {
                            if (APData.SpeedHoldIsMach)
                            {
                                SbHud.Append("M").Append(APData.TargetSpeed.ToString("F2"));
                            }
                            else
                            {
                                SbHud.Append("S").Append(ModUtils.ProcessGameString(
                                    UnitConverter.SpeedReading(APData.TargetSpeed), Plugin.SpeedShowUnit.Value));
                            }

                            hasLine1 = true;
                        }
                        else if (placeholders)
                        {
                            SbHud.Append("S");
                            hasLine1 = true;
                        }

                        if (apActive)
                        {
                            string degUnit = Plugin.AngleShowUnit.Value ? "°" : "";
                            if (APData.TargetRoll != -999f)
                            {
                                if (hasLine1)
                                {
                                    SbHud.Append(" ");
                                }

                                SbHud.Append("R").Append(APData.TargetRoll.ToString("F0")).Append(degUnit);
                                hasLine1 = true;
                            }
                            else if (placeholders)
                            {
                                if (hasLine1)
                                {
                                    SbHud.Append(" ");
                                }

                                SbHud.Append("R");
                                hasLine1 = true;
                            }
                        }

                        if (hasLine1)
                        {
                            SbHud.Append("\n");
                        }

                        bool hasLine2 = false;
                        if (apActive)
                        {
                            if (APData.TargetAlt > 0)
                            {
                                SbHud.Append("A").Append(ModUtils.ProcessGameString(
                                    UnitConverter.AltitudeReading(APData.TargetAlt), Plugin.AltShowUnit.Value));
                                hasLine2 = true;
                            }
                            else if (placeholders)
                            {
                                SbHud.Append("A");
                                hasLine2 = true;
                            }

                            if (APData.CurrentMaxClimbRate > 0 &&
                                APData.CurrentMaxClimbRate != Plugin.DefaultMaxClimbRate.Value)
                            {
                                if (hasLine2)
                                {
                                    SbHud.Append(" ");
                                }

                                SbHud.Append("V").Append(ModUtils.ProcessGameString(
                                    UnitConverter.ClimbRateReading(APData.CurrentMaxClimbRate),
                                    Plugin.VertSpeedShowUnit.Value));
                                hasLine2 = true;
                            }
                            else if (placeholders)
                            {
                                if (hasLine2)
                                {
                                    SbHud.Append(" ");
                                }

                                SbHud.Append("V");
                                hasLine2 = true;
                            }
                        }

                        if (hasLine2)
                        {
                            SbHud.Append("\n");
                        }

                        bool hasLine3 = false;
                        if (apActive)
                        {
                            string degUnit = Plugin.AngleShowUnit.Value ? "°" : "";
                            if (APData.TargetCourse >= 0)
                            {
                                SbHud.Append("C").Append(APData.TargetCourse.ToString("F0")).Append(degUnit);
                                hasLine3 = true;
                            }
                            else if (placeholders)
                            {
                                SbHud.Append("C");
                                hasLine3 = true;
                            }

                            if (APData.NavEnabled && APData.NavQueue.Count > 0)
                            {
                                if (hasLine3)
                                {
                                    SbHud.Append(" ");
                                }

                                float d = Vector3.Distance(APData.PlayerRB.position.ToGlobalPosition().AsVector3(),
                                    APData.NavQueue[0]);
                                SbHud.Append("W>").Append(ModUtils.ProcessGameString(UnitConverter.DistanceReading(d),
                                    Plugin.DistShowUnit.Value));
                                hasLine3 = true;
                            }
                            else if (placeholders)
                            {
                                if (hasLine3)
                                {
                                    SbHud.Append(" ");
                                }

                                SbHud.Append("W");
                                hasLine3 = true;
                            }
                        }

                        if (hasLine3)
                        {
                            SbHud.Append("\n");
                        }

                        if (apActive || speedActive)
                        {
                            SbHud.Append("</color>");
                        }
                    }

                    if (!APData.GCASEnabled && Plugin.ShowGCASOff.Value)
                    {
                        SbHud.Append("<color=").Append(Plugin.ColorInfo.Value).Append(">GCAS-</color>\n");
                    }

                    if (Plugin.ShowOverride.Value && APData.Enabled && !APData.GCASActive)
                    {
                        float overrideRemaining =
                            Plugin.ReengageDelay.Value - (Time.time - APData.LastOverrideInputTime);
                        if (overrideRemaining > 0)
                        {
                            SbHud.Append("<color=").Append(Plugin.ColorInfo.Value).Append(">")
                                .Append(overrideRemaining.ToString("F1")).Append("s</color>\n");
                        }
                    }

                    if (APData.AutoJammerActive)
                    {
                        SbHud.Append("<color=").Append(Plugin.ColorAPOn.Value).Append(">AJ\n</color>");
                    }

                    if (APData.FBWDisabled)
                    {
                        SbHud.Append("<color=").Append(Plugin.ColorCrit.Value).Append(">FBW OFF</color>");
                    }

                    if (APData.ALSActive || !string.IsNullOrEmpty(APData.ALSStatusText))
                    {
                        string hexColor = ColorUtility.ToHtmlStringRGBA(APData.ALSStatusColor);
                        SbHud.Append($"\n<color=#{hexColor}>{APData.ALSStatusText}</color>");
                    }

                    s_overlayText.text = SbHud.ToString();
                }
            }

            if (!APData.ALSActive && (APData.GCASActive || (APData.GCASWarning && !APData.IsOnGround)))
            {
                if (s_gcasLeftObj == null)
                {
                    Transform hudCenter = __instance.GetHUDCenter();

                    GameObject CreateObj(string name, string txt)
                    {
                        GameObject obj = Object.Instantiate(s_cachedRefLabel.gameObject, hudCenter);
                        obj.name = name;
                        Text t = obj.GetComponent<Text>();
                        t.fontStyle = FontStyle.Normal;
                        t.text = txt;
                        t.alignment = TextAnchor.MiddleCenter;
                        t.horizontalOverflow = HorizontalWrapMode.Overflow;
                        t.verticalOverflow = VerticalWrapMode.Overflow;
                        t.resizeTextForBestFit = false;

                        obj.transform.localRotation = Quaternion.identity;
                        obj.transform.localScale = Vector3.one;

                        RectTransform rt = obj.GetComponent<RectTransform>();
                        rt.pivot = new Vector2(0.5f, 0.5f);
                        rt.anchorMin = new Vector2(0.5f, 0.5f);
                        rt.anchorMax = new Vector2(0.5f, 0.5f);
                        rt.sizeDelta = new Vector2(200, 100);

                        return obj;
                    }

                    s_gcasLeftObj = CreateObj("GCAS_Left", ">");
                    s_gcasLeftText = s_gcasLeftObj.GetComponent<Text>();

                    s_gcasRightObj = CreateObj("GCAS_Right", "<");
                    s_gcasRightText = s_gcasRightObj.GetComponent<Text>();

                    s_gcasTopObj = CreateObj("GCAS_Top", "FLYUP");
                    s_gcasTopText = s_gcasTopObj.GetComponent<Text>();
                }

                float target = APData.GCASConverge;
                s_smoothedConverge = Mathf.Lerp(s_smoothedConverge, target, Time.deltaTime * 10f);

                s_gcasLeftObj.SetActive(true);
                s_gcasRightObj.SetActive(true);
                s_gcasTopObj.SetActive(target >= 1);

                Color gcasColor = ModUtils.GetColor(Plugin.ColorCrit.Value, Color.red);

                int arrowSize = (int)currentSize;
                int textSize = (int)(currentSize * 0.7);

                s_gcasLeftText.fontSize = arrowSize;
                s_gcasLeftText.color = gcasColor;
                s_gcasRightText.fontSize = arrowSize;
                s_gcasRightText.color = gcasColor;
                s_gcasTopText.fontSize = textSize;
                s_gcasTopText.color = gcasColor;

                float alpha = s_smoothedConverge;
                Color c = s_gcasLeftText.color;
                c.a = alpha;
                s_gcasLeftText.color = c;
                c = s_gcasRightText.color;
                c.a = alpha;
                s_gcasRightText.color = c;
                c = s_gcasTopText.color;
                c.a = alpha;
                s_gcasTopText.color = c;

                float offsetX = Mathf.Lerp(200f, 5f, s_smoothedConverge);

                float yOffset = -(arrowSize * 0.25f);

                s_gcasLeftObj.transform.localPosition = new Vector3(-offsetX, yOffset, 0);
                s_gcasRightObj.transform.localPosition = new Vector3(offsetX, yOffset, 0);
                s_gcasTopObj.transform.localPosition = new Vector3(0, 40, 0);
            }
            else
            {
                if (s_gcasLeftObj && s_gcasLeftObj.activeSelf)
                {
                    s_gcasLeftObj.SetActive(false);
                }

                if (s_gcasRightObj && s_gcasRightObj.activeSelf)
                {
                    s_gcasRightObj.SetActive(false);
                }

                if (s_gcasTopObj && s_gcasTopObj.activeSelf)
                {
                    s_gcasTopObj.SetActive(false);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[HUDVisualsPatch] Error: {ex}");
            Plugin.IsBroken = true;
        }
    }
}
