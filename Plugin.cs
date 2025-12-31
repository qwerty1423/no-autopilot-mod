using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace AutopilotMod
{
    [BepInPlugin("com.qwerty1423.noautopilotmod", "NOAutopilotMod", "4.9.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal new static ManualLogSource Logger;

        // visuals
        public static ConfigEntry<string> ColorAPOn, ColorAPOff, ColorGood, ColorWarn, ColorCrit, ColorInfo;
        public static ConfigEntry<float> FuelOffsetY, FuelLineSpacing, FuelSmoothing, FuelUpdateInterval;
        public static ConfigEntry<int> FuelWarnMinutes, FuelCritMinutes;
        public static ConfigEntry<bool> ShowExtraInfo;

        // settings
        public static ConfigEntry<bool> EnableActionLogs, EnableDebugDump, ShowLogs, LogPIDData;
        public static ConfigEntry<int> LogRefreshRate;
        public static ConfigEntry<float> StickDeadzone;

        // auto jammer
        public static ConfigEntry<bool> EnableAutoJammer;
        public static ConfigEntry<KeyCode> AutoJammerKey;
        public static ConfigEntry<float> AutoJammerThreshold;
        public static ConfigEntry<bool> AutoJammerHumanize;
        public static ConfigEntry<float> AutoJammerMinDelay;
        public static ConfigEntry<float> AutoJammerMaxDelay;
        public static ConfigEntry<float> AutoJammerReleaseMin;
        public static ConfigEntry<float> AutoJammerReleaseMax;

        // controls
        public static ConfigEntry<KeyCode> ToggleKey, ToggleFBWKey, UpKey, DownKey, BigUpKey, BigDownKey;
        public static ConfigEntry<KeyCode> ClimbRateUpKey, ClimbRateDownKey, BankLeftKey, BankRightKey, BankLevelKey, DumpKey;

        // flight values
        public static ConfigEntry<float> AltStep, BigAltStep, ClimbRateStep, BankStep, MinAltitude;

        // tuning
        public static ConfigEntry<float> DefaultMaxClimbRate, Conf_VS_MaxAngle;
        
        // Loop 1 (Alt)
        public static ConfigEntry<float> Conf_Alt_P, Conf_Alt_I, Conf_Alt_D, Conf_Alt_ILimit;
        // Loop 2 (VS)
        public static ConfigEntry<float> Conf_VS_P, Conf_VS_I, Conf_VS_D, Conf_VS_ILimit;
        // Loop 3 (Angle)
        public static ConfigEntry<float> Conf_Angle_P, Conf_Angle_I, Conf_Angle_D, Conf_Angle_ILimit;
        // Roll
        public static ConfigEntry<float> RollP, RollI, RollD, RollMax, RollILimit;
        public static ConfigEntry<bool> InvertRoll, InvertPitch;

        // auto GCAS
        public static ConfigEntry<bool> EnableGCAS;
        public static ConfigEntry<float> GCAS_MaxG;
        public static ConfigEntry<float> GCAS_WarnBuffer;
        public static ConfigEntry<float> GCAS_AutoBuffer;
        public static ConfigEntry<float> GCAS_MinAlt;
        public static ConfigEntry<float> GCAS_Deadzone;
        public static ConfigEntry<float> GCAS_P, GCAS_I, GCAS_D, GCAS_ILimit;

        // humanise
        public static ConfigEntry<bool> HumanizeEnabled;
        public static ConfigEntry<float> HumanizeStrength, HumanizeSpeed;
        public static ConfigEntry<float> Hum_Alt_Inner, Hum_Alt_Outer, Hum_Alt_Scale;
        public static ConfigEntry<float> Hum_VS_Inner, Hum_VS_Outer;
        public static ConfigEntry<float> Hum_PitchSleepMin, Hum_PitchSleepMax;
        public static ConfigEntry<float> Hum_Roll_Inner, Hum_Roll_Outer, Hum_RollRate_Inner, Hum_RollRate_Outer;
        public static ConfigEntry<float> Hum_RollSleepMin, Hum_RollSleepMax;

        private void Awake()
        {
            Plugin.Logger = base.Logger;
            
            // Visuals
            ColorAPOn = Config.Bind("Visuals - Colors", "1. Color AP On", "#00FF00", "Green");
            ColorAPOff = Config.Bind("Visuals - Colors", "2. Color AP Off", "#FFFFFF80", "Transparent White");
            ColorGood = Config.Bind("Visuals - Colors", "3. Color Good", "#00FF00", "Green");
            ColorWarn = Config.Bind("Visuals - Colors", "4. Color Warning", "#FFFF00", "Yellow");
            ColorCrit = Config.Bind("Visuals - Colors", "5. Color Critical", "#FF0000", "Red");
            ColorInfo = Config.Bind("Visuals - Colors", "6. Color Info", "#00FFFF", "Cyan");

            FuelOffsetY = Config.Bind("Visuals - Layout", "1. Stack Start Y", -20f, "Vertical position");
            FuelLineSpacing = Config.Bind("Visuals - Layout", "2. Line Spacing", 20f, "Vertical gap");
            
            ShowExtraInfo = Config.Bind("Visuals", "Show Fuel/AP Info", true, "Show extra info on Fuel Gauge");

            FuelSmoothing = Config.Bind("Calculations", "1. Fuel Flow Smoothing", 0.1f, "Alpha value");
            FuelUpdateInterval = Config.Bind("Calculations", "2. Fuel Update Interval", 1.0f, "Seconds");
            FuelWarnMinutes = Config.Bind("Calculations", "3. Fuel Warning Time", 15, "Minutes");
            FuelCritMinutes = Config.Bind("Calculations", "4. Fuel Critical Time", 5, "Minutes");

            // Settings
            EnableActionLogs = Config.Bind("Settings", "1. Enable Action Logs", false, "Log On/Off events");
            EnableDebugDump = Config.Bind("Settings", "2. Enable Debug Dump", false, "Allow dumping aircraft variables");
            ShowLogs = Config.Bind("Settings", "3. Show Debug Logs", false, "Spam console");
            LogRefreshRate = Config.Bind("Settings", "4. Debug Log Rate", 60, "Frames");
            LogPIDData = Config.Bind("Settings", "5. Log PID CSV", false, "CSV output");
            StickDeadzone = Config.Bind("Settings", "6. Stick Deadzone", 0.5f, "Threshold");
            InvertRoll = Config.Bind("Settings", "7. Invert Roll", false, "Flip Roll");
            InvertPitch = Config.Bind("Settings", "8. Invert Pitch", true, "Flip Pitch");

            // Auto Jammer
            EnableAutoJammer = Config.Bind("Auto Jammer", "1. Enable Auto Jammer", true, "Allow the feature");
            AutoJammerKey = Config.Bind("Auto Jammer", "2. Toggle Key", KeyCode.Slash, "Key to toggle jamming");
            AutoJammerThreshold = Config.Bind("Auto Jammer", "3. Energy Threshold", 0.99f, "Fire when energy > this %");
            AutoJammerHumanize = Config.Bind("Auto Jammer", "4. Humanize Delay", true, "Add random delay to mimic human reaction");
            AutoJammerMinDelay = Config.Bind("Auto Jammer", "5. Delay Min", 0.05f, "Seconds");
            AutoJammerMaxDelay = Config.Bind("Auto Jammer", "6. Delay Max", 0.2f, "Seconds");
            AutoJammerReleaseMin = Config.Bind("Auto Jammer", "7. Release Delay Min", 0.05f, "Seconds to hold after energy drops");
            AutoJammerReleaseMax = Config.Bind("Auto Jammer", "8. Release Delay Max", 0.2f, "Seconds to hold after energy drops");

            // Controls
            ToggleKey = Config.Bind("Controls", "01. Toggle AP Key", KeyCode.Equals, "AP On/Off");
            ToggleFBWKey = Config.Bind("Controls", "02. Toggle FBW Key", KeyCode.Delete, "Toggle Stability Assist");
            UpKey = Config.Bind("Controls", "03. Altitude Up (Small)", KeyCode.UpArrow, "No Limits");
            DownKey = Config.Bind("Controls", "04. Altitude Down (Small)", KeyCode.DownArrow, "No Limits");
            BigUpKey = Config.Bind("Controls", "05. Altitude Up (Big)", KeyCode.LeftArrow, "Jump Up");
            BigDownKey = Config.Bind("Controls", "06. Altitude Down (Big)", KeyCode.RightArrow, "Jump Down");
            ClimbRateUpKey = Config.Bind("Controls", "07. Climb Rate Increase", KeyCode.PageUp, "Increase Max VS");
            ClimbRateDownKey = Config.Bind("Controls", "08. Climb Rate Decrease", KeyCode.PageDown, "Decrease Max VS");
            BankLeftKey = Config.Bind("Controls", "09. Bank Left", KeyCode.LeftBracket, "Roll Left");
            BankRightKey = Config.Bind("Controls", "10. Bank Right", KeyCode.RightBracket, "Roll Right");
            BankLevelKey = Config.Bind("Controls", "11. Bank Level (Reset)", KeyCode.Quote, "Reset Roll to 0");
            DumpKey = Config.Bind("Controls", "12. Debug Dump Key", KeyCode.Backslash, "Print Aircraft vars to console");

            // Flight Values
            AltStep = Config.Bind("Controls", "13. Altitude Increment (Small)", 0.1f, "Meters per tick");
            BigAltStep = Config.Bind("Controls", "14. Altitude Increment (Big)", 100f, "Meters per tick");
            ClimbRateStep = Config.Bind("Controls", "15. Climb Rate Step", 0.5f, "m/s per tick");
            BankStep = Config.Bind("Controls", "16. Bank Step", 0.5f, "Degrees per tick");
            MinAltitude = Config.Bind("Controls", "17. Minimum Target Altitude", 20f, "Safety floor");

            // Tuning
            DefaultMaxClimbRate = Config.Bind("Tuning - 0. Limits", "1. Default Max Climb Rate", 40f, "Startup value");
            Conf_VS_MaxAngle = Config.Bind("Tuning - 0. Limits", "2. Max Pitch Angle", 900.0f, "Safety Clamp");

            // Loop 1 (Alt)
            Conf_Alt_P = Config.Bind("Tuning - 1. Altitude", "1. Alt P", 0.5f, "Alt Error -> Target VS");
            Conf_Alt_I = Config.Bind("Tuning - 1. Altitude", "2. Alt I", 0.0f, "Accumulates Error");
            Conf_Alt_D = Config.Bind("Tuning - 1. Altitude", "3. Alt D", 1.5f, "Dampens Approach");
            Conf_Alt_ILimit = Config.Bind("Tuning - 1. Altitude", "4. Alt I Limit", 10.0f, "Max Integral (m/s)");

            // Loop 2 (VS)
            Conf_VS_P = Config.Bind("Tuning - 2. VertSpeed", "1. VS P", 0.5f, "VS Error -> Target Angle");
            Conf_VS_I = Config.Bind("Tuning - 2. VertSpeed", "2. VS I", 0.1f, "Trim Angle");
            Conf_VS_D = Config.Bind("Tuning - 2. VertSpeed", "3. VS D", 0.1f, "Dampens VS Change");
            Conf_VS_ILimit = Config.Bind("Tuning - 2. VertSpeed", "4. VS I Limit", 300.0f, "Max Trim (Deg)");

            // Loop 3 (Angle)
            Conf_Angle_P = Config.Bind("Tuning - 3. Angle", "1. Angle P", 0.01f, "Angle Error -> Stick");
            Conf_Angle_I = Config.Bind("Tuning - 3. Angle", "2. Angle I", 0.0f, "Holds Angle");
            Conf_Angle_D = Config.Bind("Tuning - 3. Angle", "3. Angle D", 0.0f, "Dampens Rotation");
            Conf_Angle_ILimit = Config.Bind("Tuning - 3. Angle", "4. Angle I Limit", 100.0f, "Max Integral (Stick)");

            // Roll
            RollP = Config.Bind("Tuning - Roll", "1. Roll P", 0.01f, "P");
            RollI = Config.Bind("Tuning - Roll", "2. Roll I", 0.002f, "I");
            RollD = Config.Bind("Tuning - Roll", "3. Roll D", 0.001f, "D");
            RollMax = Config.Bind("Tuning - Roll", "4. Roll Max Output", 1.0f, "Limit");
            RollILimit = Config.Bind("Tuning - Roll", "5. Roll I Limit", 50.0f, "Limit");

            // auto GCAS
            EnableGCAS = Config.Bind("Auto GCAS", "1. Enable GCAS", true, "Auto pull up logic");
            GCAS_MaxG = Config.Bind("Auto GCAS", "2. Max G-Pull", 5.0f, "Assumed G-Force capability for calculation");
            GCAS_WarnBuffer = Config.Bind("Auto GCAS", "3. Warning Buffer", 20.0f, "Seconds warning before auto-pull");
            GCAS_AutoBuffer = Config.Bind("Auto GCAS", "4. Auto-Pull Buffer", 2.0f, "Safety margin seconds");
            GCAS_MinAlt = Config.Bind("Auto GCAS", "5. Hard Floor", 0.0f, "Absolute min altitude");
            GCAS_Deadzone = Config.Bind("Auto GCAS", "6. GCAS Deadzone", 0.5f, "GCAS override deadzone");
            GCAS_P = Config.Bind("GCAS PID", "1. GCAS P", 0.1f, "G Error -> Stick");
            GCAS_I = Config.Bind("GCAS PID", "2. GCAS I", 1.0f, "Builds pull over time");
            GCAS_D = Config.Bind("GCAS PID", "3. GCAS D", 0.0f, "Dampens G overshoot");
            GCAS_ILimit = Config.Bind("GCAS PID", "4. GCAS I Limit", 1.0f, "Max stick influence");

            // Humanize
            HumanizeEnabled = Config.Bind("Settings - Humanize", "01. Humanize Enabled", true, "Add imperfections");
            HumanizeStrength = Config.Bind("Settings - Humanize", "02. Noise Strength", 0.01f, "Jitter amount");
            HumanizeSpeed = Config.Bind("Settings - Humanize", "03. Noise Speed", 1.0f, "Jitter freq");
            
            Hum_Alt_Inner = Config.Bind("Settings - Humanize", "04. Alt Tolerance Inner", 0.1f, "Start Sleeping (m)");
            Hum_Alt_Outer = Config.Bind("Settings - Humanize", "05. Alt Tolerance Outer", 1.0f, "Wake Up (m)");
            Hum_Alt_Scale = Config.Bind("Settings - Humanize", "06. Alt Scale", 0.01f, "Increase per meter alt");
            
            Hum_VS_Inner = Config.Bind("Settings - Humanize", "07. VS Tolerance Inner", 0.01f, "Start Sleeping (m/s)");
            Hum_VS_Outer = Config.Bind("Settings - Humanize", "08. VS Tolerance Outer", 5.0f, "Wake Up (m/s)");
            
            Hum_PitchSleepMin = Config.Bind("Settings - Humanize", "09. Pitch Sleep Min", 2.0f, "Seconds");
            Hum_PitchSleepMax = Config.Bind("Settings - Humanize", "10. Pitch Sleep Max", 60.0f, "Seconds");

            Hum_Roll_Inner = Config.Bind("Settings - Humanize", "11. Roll Tolerance Inner", 0.1f, "Start Sleeping (deg)");
            Hum_Roll_Outer = Config.Bind("Settings - Humanize", "12. Roll Tolerance Outer", 1.0f, "Wake Up (deg)");
            Hum_RollRate_Inner = Config.Bind("Settings - Humanize", "13. Roll Rate Tolerance Inner", 1.0f, "Start Sleeping (deg/s)");
            Hum_RollRate_Outer = Config.Bind("Settings - Humanize", "14. Roll Rate Tolerance Outer", 20.0f, "Wake Up (deg/s)");
            Hum_RollSleepMin = Config.Bind("Settings - Humanize", "15. Roll Sleep Min", 1.5f, "Seconds");
            Hum_RollSleepMax = Config.Bind("Settings - Humanize", "16. Roll Sleep Max", 60.0f, "Seconds");

            new Harmony("com.anon.autopilotmod").PatchAll();
        }
    }

    // --- SHARED DATA ---
    public static class APData
    {
        public static bool Enabled = false;
        public static bool AutoJammerActive = false;
        public static bool FBWDisabled = false;
        public static bool GCASActive = false; 
        public static bool GCASWarning = false;
        public static float TargetAlt = 0f;
        public static float TargetRoll = 0f;
        public static float CurrentAlt = 0f;
        public static float CurrentRoll = 0f;
        public static float CurrentMaxClimbRate = -1f; 
        public static Rigidbody PlayerRB;
    }

    // --- HELPERS ---
    public static class ModUtils
    {
        public static Color GetColor(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            if (!hex.StartsWith("#")) hex = "#" + hex;
            if (ColorUtility.TryParseHtmlString(hex, out Color c)) return c;
            return fallback;
        }
    }

    // --- PATCHES ---

    [HarmonyPatch(typeof(FlightHud), "SetHUDInfo")]
    internal class HudPatch
    {
        // Helper to track vehicle changes
        private static GameObject lastVehicleObj;

        private static void Postfix(object playerVehicle, float altitude)
        {
            try {
                APData.CurrentAlt = altitude;
                if (playerVehicle != null)
                {
                    Component v = (Component)playerVehicle;

                    // Detect if we switched planes or respawned
                    if (v.gameObject != lastVehicleObj)
                    {
                        lastVehicleObj = v.gameObject;
                        
                        // Force systems OFF so the new plane doesn't crash immediately
                        APData.Enabled = false;
                        APData.AutoJammerActive = false;
                        APData.FBWDisabled = false;

                        if (Plugin.EnableActionLogs.Value)
                            Plugin.Logger.LogInfo("New Vehicle Detected - Systems Reset");
                    }

                    APData.CurrentRoll = v.transform.eulerAngles.z;
                    if (APData.CurrentRoll > 180f) APData.CurrentRoll -= 360f;

                    // Update the Rigidbody reference
                    var rb = v.GetComponent<Rigidbody>();
                    if (APData.PlayerRB != rb) APData.PlayerRB = rb;
                }
            } catch (Exception ex) {
                Plugin.Logger.LogError($"[HudPatch] Error: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(FlightHud), "Update")]
    internal class InputHandlerPatch
    {
        private static void Postfix(FlightHud __instance)
        {
            try {
                if (Input.GetKeyDown(Plugin.ToggleKey.Value))
                {
                    APData.Enabled = !APData.Enabled;
                    if (Plugin.EnableActionLogs.Value)
                        Plugin.Logger.LogInfo("Autopilot " + (APData.Enabled ? "ENABLED" : "DISABLED"));
                }

                if (Plugin.EnableAutoJammer.Value && Input.GetKeyDown(Plugin.AutoJammerKey.Value))
                {
                    APData.AutoJammerActive = !APData.AutoJammerActive;
                    if (Plugin.EnableActionLogs.Value)
                        Plugin.Logger.LogInfo("Auto Jammer " + (APData.AutoJammerActive ? "ENABLED" : "DISABLED"));
                }

                if (Input.GetKeyDown(Plugin.ToggleFBWKey.Value))
                {
                    Aircraft ac = Traverse.Create(__instance).Field("playerVehicle").GetValue<Aircraft>();
                    if (ac != null) {
                        object cf = Traverse.Create(ac).Field("controlsFilter").GetValue();
                        if (cf != null) {
                            if (cf.GetType().Name.Contains("Helo")) return;
                            try
                            {
                                MethodInfo getM = cf.GetType().GetMethod("GetFlyByWireParameters");
                                MethodInfo setM = cf.GetType().GetMethod("SetFlyByWireParameters");
                                
                                if (getM != null && setM != null) {
                                    object result = getM.Invoke(cf, null);

                                    FieldInfo item1 = result.GetType().GetField("Item1", BindingFlags.Public | BindingFlags.Instance);
                                    FieldInfo item2 = result.GetType().GetField("Item2", BindingFlags.Public | BindingFlags.Instance);
                                    
                                    if (item1 != null && item2 != null)
                                    {
                                        bool isEnabled = (bool)item1.GetValue(result);
                                        float[] values = (float[])item2.GetValue(result);
                                        bool newState = !isEnabled;
                                        
                                        setM.Invoke(cf, [newState, values]);
                                        
                                        APData.FBWDisabled = !newState;
                                        if (Plugin.EnableActionLogs.Value)
                                            Plugin.Logger.LogInfo("Fly-By-Wire " + (newState ? "ENABLED" : "DISABLED"));
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Plugin.Logger.LogError($"Failed to toggle Fly-By-Wire via reflection. Is the game version incompatible? Error: {ex}");
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                Plugin.Logger.LogError($"[InputHandlerPatch] Error: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(FlightHud), "Update")]
    internal class DebugDumpPatch
    {
        private static void Postfix(FlightHud __instance)
        {
            if (!Plugin.EnableDebugDump.Value) return;
            try {
                if (Input.GetKeyDown(Plugin.DumpKey.Value))
                {
                    Aircraft ac = Traverse.Create(__instance).Field("playerVehicle").GetValue<Aircraft>();
                    if (ac != null)
                    {
                        Plugin.Logger.LogInfo($"=== DUMP START: {ac.name} ===");
                        foreach (FieldInfo f in typeof(Aircraft).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            try { Plugin.Logger.LogInfo($"[FIELD] {f.Name}: {f.GetValue(ac)}"); } catch { }
                        }
                        Plugin.Logger.LogInfo("=== DUMP END ===");
                    }
                }
            } catch (Exception ex) {
                 Plugin.Logger.LogError($"[DebugDumpPatch] Error: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(PilotPlayerState), "PlayerAxisControls")]
    internal class ControlOverridePatch
    {
        // pid integrals
        private static float altIntegral = 0f;
        private static float vsIntegral = 0f;
        private static float angleIntegral = 0f;
        private static float rollIntegral = 0f;
        
        // gcas integral
        private static float gcasIntegral = 0f;

        // derivatives
        private static float lastAltError = 0f;
        private static float lastVSError = 0f;
        private static float lastGError = 0f; // NEW
                
        // states
        private static int logTimer = 0;
        private static bool wasEnabled = false;
        
        // humanise state
        private static float pitchSleepUntil = 0f;
        private static float rollSleepUntil = 0f;
        private static bool isPitchSleeping = false;
        private static bool isRollSleeping = false;

        // jammer state
        private static float jammerNextFireTime = 0f;
        private static float jammerNextReleaseTime = 0f;
        private static bool isJammerHoldingTrigger = false;

        private static void ResetIntegrators()
        {
            altIntegral = 0f;
            vsIntegral = 0f;
            angleIntegral = 0f;
            rollIntegral = 0f;
            gcasIntegral = 0f;
            lastAltError = 0f;
            lastVSError = 0f;
            lastGError = 0f;
            pitchSleepUntil = 0f; 
            rollSleepUntil = 0f; 
            isPitchSleeping = false; 
            isRollSleeping = false;
        }

        private static void Postfix(object __instance)
        {
            if (APData.CurrentMaxClimbRate < 0f) APData.CurrentMaxClimbRate = Plugin.DefaultMaxClimbRate.Value;

            if (APData.Enabled != wasEnabled)
            {
                if (APData.Enabled)
                {
                    if (!APData.GCASActive) 
                    {
                        APData.TargetAlt = APData.CurrentAlt;
                        APData.TargetRoll = 0f;
                    }
                    ResetIntegrators();
                }
                wasEnabled = APData.Enabled;
            }

            bool inputsModified = false;

            try
            {
                Traverse tControlInputs = Traverse.Create(__instance).Field("controlInputs");
                object inputObj = tControlInputs.GetValue();
                Traverse tInputWrapper = Traverse.Create(inputObj);

                float stickPitch = tInputWrapper.Field("pitch").GetValue<float>();
                float stickRoll = tInputWrapper.Field("roll").GetValue<float>();

                APData.GCASWarning = false;

                float currentG = 1f;
                Aircraft acRef = null;
                if (APData.PlayerRB != null) {
                    acRef = APData.PlayerRB.GetComponent<Aircraft>();
                    if (acRef != null) {
                        try {
                            System.Collections.IList pilots = Traverse.Create(acRef).Field("pilots").GetValue<System.Collections.IList>();
                            if (pilots != null && pilots.Count > 0) {
                                object p0 = pilots[0];
                                Vector3 pAccel = (Vector3)Traverse.Create(p0).Method("GetAccel").GetValue();
                                currentG = Vector3.Dot(pAccel + Vector3.up, acRef.transform.up);
                            }
                        } catch {}
                    }
                }

                if (Plugin.EnableGCAS.Value && APData.PlayerRB != null)
                {
                    bool gearDown = false;
                    if (acRef != null) {
                        object gs = Traverse.Create(acRef).Field("gearState").GetValue();
                        if (gs != null && !gs.ToString().Contains("LockedRetracted")) {
                            gearDown = true;
                        }
                    }

                    bool pilotInput = Mathf.Abs(stickPitch) > Plugin.GCAS_Deadzone.Value || Mathf.Abs(stickRoll) > Plugin.GCAS_Deadzone.Value;
                    
                    if (pilotInput || gearDown)
                    {
                        if (APData.GCASActive)
                        {
                            APData.GCASActive = false;
                            APData.Enabled = false;
                            if (Plugin.EnableActionLogs.Value) Plugin.Logger.LogInfo("GCAS OVERRIDE (Pilot/Gear)");
                        }
                    }
                    else
                    {
                        float speed = APData.PlayerRB.velocity.magnitude;
                        if (speed > 15f)
                        {
                            Vector3 velocity = APData.PlayerRB.velocity;
                            Vector3 fwd = APData.PlayerRB.transform.forward;

                            float diveAngle = 0f;
                            if (velocity.y < -1f) 
                                diveAngle = Vector3.Angle(velocity, Vector3.ProjectOnPlane(velocity, Vector3.up));

                            float availAccel = Mathf.Max(1f, Plugin.GCAS_MaxG.Value - Mathf.Cos(diveAngle * Mathf.Deg2Rad)) * 9.81f;
                            float timeToLevel = speed * (diveAngle * Mathf.Deg2Rad) / availAccel;

                            float autoThresholdTime = timeToLevel + Plugin.GCAS_AutoBuffer.Value;
                            float warnThresholdTime = timeToLevel + Plugin.GCAS_WarnBuffer.Value;
                            float lookAheadDist = speed * (warnThresholdTime + 2.0f); 
                            
                            float timeToImpact = 999f;
                            if (Physics.SphereCast(APData.PlayerRB.position, 5f, velocity.normalized, out RaycastHit hit, lookAheadDist))
                            {
                                if (hit.collider.gameObject.isStatic || hit.collider.gameObject.layer == 0)
                                    timeToImpact = hit.distance / speed;
                            }
                            
                            float timeToFloor = 999f;
                            if (velocity.y < -5f) {
                                float distToFloor = APData.CurrentAlt - Plugin.GCAS_MinAlt.Value;
                                if (distToFloor > 0) timeToFloor = distToFloor / Mathf.Abs(velocity.y);
                                else timeToFloor = 0f;
                            }

                            float finalTTA = Mathf.Min(timeToImpact, timeToFloor);
                            
                            if (APData.GCASActive)
                            {
                                bool climbing = velocity.y > 5f;
                                bool noseUp = fwd.y > 0.1f;
                                bool pathClear = finalTTA > warnThresholdTime;

                                if (climbing && noseUp && pathClear) {
                                    APData.GCASActive = false;
                                    APData.Enabled = false;
                                    if (Plugin.EnableActionLogs.Value) Plugin.Logger.LogInfo("GCAS RECOVERED");
                                } else {
                                    APData.TargetRoll = 0f;
                                    APData.TargetAlt = APData.CurrentAlt + 500f; 
                                    APData.CurrentMaxClimbRate = 100f; 
                                    APData.GCASWarning = true; 
                                }
                            }
                            else
                            {
                                if (finalTTA < autoThresholdTime) {
                                    APData.Enabled = true;
                                    APData.GCASActive = true;
                                    APData.TargetRoll = 0f;
                                    APData.TargetAlt = APData.CurrentAlt + 500f;
                                    ResetIntegrators();
                                    if (Plugin.EnableActionLogs.Value) Plugin.Logger.LogWarning($"GCAS ENGAGE! TTA:{finalTTA:F1}s");
                                } else if (finalTTA < warnThresholdTime) {
                                    APData.GCASWarning = true;
                                }
                            }
                        }
                    }
                }

                // jammer logic
                if (APData.AutoJammerActive && APData.PlayerRB != null)
                {
                    Aircraft ac = APData.PlayerRB.GetComponent<Aircraft>();
                    if (ac != null && ac.name.Contains("EW1"))
                    {
                        object ps = Traverse.Create(ac).Field("powerSupply").GetValue();
                        if (ps != null) {
                            float energy = Traverse.Create(ps).Field("charge").GetValue<float>();
                            float maxE = Traverse.Create(ps).Field("maxCharge").GetValue<float>();
                            if (maxE <= 1f) maxE = 100f;
                            float pct = energy / maxE;

                            bool thresholdMet = (pct >= Plugin.AutoJammerThreshold.Value);

                            if (thresholdMet) {
                                jammerNextReleaseTime = 0f;
                                if (!isJammerHoldingTrigger) {
                                    if (jammerNextFireTime == 0f) {
                                        float delay = Plugin.AutoJammerHumanize.Value ? UnityEngine.Random.Range(Plugin.AutoJammerMinDelay.Value, Plugin.AutoJammerMaxDelay.Value) : 0f;
                                        jammerNextFireTime = Time.time + delay;
                                    }
                                    if (Time.time >= jammerNextFireTime) { isJammerHoldingTrigger = true; jammerNextFireTime = 0f; }
                                }
                            } else {
                                jammerNextFireTime = 0f;
                                if (isJammerHoldingTrigger) {
                                    if (jammerNextReleaseTime == 0f) {
                                        float delay = Plugin.AutoJammerHumanize.Value ? UnityEngine.Random.Range(Plugin.AutoJammerReleaseMin.Value, Plugin.AutoJammerReleaseMax.Value) : 0f;
                                        jammerNextReleaseTime = Time.time + delay;
                                    }
                                    if (Time.time >= jammerNextReleaseTime) { isJammerHoldingTrigger = false; jammerNextReleaseTime = 0f; }
                                }
                            }

                            if (isJammerHoldingTrigger) {
                                object wm = Traverse.Create(ac).Field("weaponManager").GetValue();
                                if (wm != null) Traverse.Create(wm).Method("Fire").GetValue();
                            }
                        }
                    }
                }

                // --- AUTOPILOT CORE ---
                if (APData.Enabled)
                {
                    if (Mathf.Abs(stickPitch) > Plugin.StickDeadzone.Value || Mathf.Abs(stickRoll) > Plugin.StickDeadzone.Value)
                    {
                        APData.Enabled = false;
                        APData.GCASActive = false;
                        if (Plugin.EnableActionLogs.Value) Plugin.Logger.LogInfo("AP OVERRIDE");
                        ResetIntegrators();
                    }
                    else
                    {
                        if (APData.PlayerRB != null) APData.PlayerRB.isKinematic = false;
                        
                        if (!APData.GCASActive)
                        {
                            if (Input.GetKey(Plugin.UpKey.Value)) APData.TargetAlt += Plugin.AltStep.Value;
                            if (Input.GetKey(Plugin.DownKey.Value)) APData.TargetAlt -= Plugin.AltStep.Value;
                            if (Input.GetKey(Plugin.BigUpKey.Value)) APData.TargetAlt += Plugin.BigAltStep.Value;
                            if (Input.GetKey(Plugin.BigDownKey.Value)) {
                                float n = APData.TargetAlt - Plugin.BigAltStep.Value;
                                APData.TargetAlt = Mathf.Max(n, Plugin.MinAltitude.Value);
                            }

                            if (Input.GetKey(Plugin.ClimbRateUpKey.Value)) APData.CurrentMaxClimbRate += Plugin.ClimbRateStep.Value;
                            if (Input.GetKey(Plugin.ClimbRateDownKey.Value)) APData.CurrentMaxClimbRate = Mathf.Max(0.5f, APData.CurrentMaxClimbRate - Plugin.ClimbRateStep.Value);
                            
                            if (Input.GetKey(Plugin.BankLevelKey.Value)) { APData.TargetRoll = 0f; }
                            else if (Input.GetKey(Plugin.BankLeftKey.Value)) { 
                                APData.TargetRoll = Mathf.Repeat(APData.TargetRoll + Plugin.BankStep.Value + 180f, 360f) - 180f;
                            }
                            else if (Input.GetKey(Plugin.BankRightKey.Value)) { 
                                APData.TargetRoll = Mathf.Repeat(APData.TargetRoll - Plugin.BankStep.Value + 180f, 360f) - 180f;
                            }
                        }

                        float currentVS = (APData.PlayerRB != null) ? APData.PlayerRB.velocity.y : 0f;
                        float noiseT = Time.time * Plugin.HumanizeSpeed.Value;
                        float pitchOut = 0f;
                        float rollOut = 0f;
                        float dt = Time.deltaTime; 
                        
                        Vector3 fwd = APData.PlayerRB.transform.forward;
                        Vector3 flatFwd = Vector3.ProjectOnPlane(fwd, Vector3.up).normalized;
                        float currentPitchDeg = Vector3.Angle(fwd, flatFwd);
                        if (fwd.y < 0) currentPitchDeg = -currentPitchDeg;
                        float pitchRate = APData.PlayerRB.transform.InverseTransformDirection(APData.PlayerRB.angularVelocity).x * Mathf.Rad2Deg;

                        bool useHumanize = Plugin.HumanizeEnabled.Value && !APData.GCASActive;

                        // Humanize Logic
                        if (useHumanize) {
                            float altErrorAbs = Mathf.Abs(APData.TargetAlt - APData.CurrentAlt);
                            float vsAbs = Mathf.Abs(currentVS);
                            float altScale = APData.CurrentAlt * Plugin.Hum_Alt_Scale.Value;
                            if (!isPitchSleeping) {
                                if (altErrorAbs < (Plugin.Hum_Alt_Inner.Value + altScale) && vsAbs < Plugin.Hum_VS_Inner.Value) {
                                    float dur = UnityEngine.Random.Range(Plugin.Hum_PitchSleepMin.Value, Plugin.Hum_PitchSleepMax.Value);
                                    pitchSleepUntil = Time.time + dur;
                                    isPitchSleeping = true;
                                }
                            } else {
                                if (altErrorAbs > (Plugin.Hum_Alt_Outer.Value + altScale) || vsAbs > Plugin.Hum_VS_Outer.Value || Time.time > pitchSleepUntil) 
                                    isPitchSleeping = false;
                            }
                        }

                        // gcas pid
                        if (APData.GCASActive)
                        {
                            float targetG = Plugin.GCAS_MaxG.Value;
                            float gError = targetG - currentG;

                            gcasIntegral = Mathf.Clamp(gcasIntegral + (gError * dt * Plugin.GCAS_I.Value), -Plugin.GCAS_ILimit.Value, Plugin.GCAS_ILimit.Value);
                            
                            float gD = (gError - lastGError) / dt;
                            lastGError = gError;

                            // Calculate stick directly from G error
                            float stickOut = (gError * Plugin.GCAS_P.Value) + gcasIntegral + (gD * Plugin.GCAS_D.Value);
                            
                            pitchOut = Mathf.Clamp(stickOut, -1f, 1f);

                            if (Plugin.InvertPitch.Value) pitchOut = -pitchOut;
                        }

                        // normal mode
                        else 
                        {
                            if (useHumanize && isPitchSleeping) {
                                pitchOut = (Mathf.PerlinNoise(noiseT, 0f) - 0.5f) * Plugin.HumanizeStrength.Value * 0.5f;
                            } 
                            else {
                                float altError = APData.TargetAlt - APData.CurrentAlt;
                                altIntegral = Mathf.Clamp(altIntegral + (altError * dt * Plugin.Conf_Alt_I.Value), -Plugin.Conf_Alt_ILimit.Value, Plugin.Conf_Alt_ILimit.Value);
                                float altD = (altError - lastAltError) / dt; lastAltError = altError;
                                
                                float targetVS = Mathf.Clamp((altError * Plugin.Conf_Alt_P.Value) + altIntegral + (altD * Plugin.Conf_Alt_D.Value), -APData.CurrentMaxClimbRate, APData.CurrentMaxClimbRate);
                                
                                float vsError = targetVS - currentVS;
                                vsIntegral = Mathf.Clamp(vsIntegral + (vsError * dt * Plugin.Conf_VS_I.Value), -Plugin.Conf_VS_ILimit.Value, Plugin.Conf_VS_ILimit.Value);
                                float vsD = (vsError - lastVSError) / dt; lastVSError = vsError;
                                
                                float targetPitchDeg = Mathf.Clamp((vsError * Plugin.Conf_VS_P.Value) + vsIntegral + (vsD * Plugin.Conf_VS_D.Value), -Plugin.Conf_VS_MaxAngle.Value, Plugin.Conf_VS_MaxAngle.Value);

                                float pitchError = targetPitchDeg - currentPitchDeg;
                                angleIntegral = Mathf.Clamp(angleIntegral + (pitchError * dt * Plugin.Conf_Angle_I.Value), -Plugin.Conf_Angle_ILimit.Value, Plugin.Conf_Angle_ILimit.Value);
                                
                                float stickRaw = (pitchError * Plugin.Conf_Angle_P.Value) + angleIntegral - (pitchRate * Plugin.Conf_Angle_D.Value);

                                pitchOut = Mathf.Clamp(stickRaw, -1f, 1f);

                                if (Plugin.InvertPitch.Value) pitchOut = -pitchOut;
                                if (useHumanize) pitchOut += (Mathf.PerlinNoise(noiseT, 0f) - 0.5f) * 2f * Plugin.HumanizeStrength.Value;
                            }
                        }

                        // Roll Logic
                        float rollError = Mathf.DeltaAngle(APData.CurrentRoll, APData.TargetRoll);
                        float rollRate = 0f;
                        if (APData.PlayerRB != null) rollRate = APData.PlayerRB.transform.InverseTransformDirection(APData.PlayerRB.angularVelocity).z * 57.29f;

                        if (useHumanize) {
                            float rErr = Mathf.Abs(rollError);
                            float rRate = Mathf.Abs(rollRate);
                            if (!isRollSleeping) {
                                if (rErr < Plugin.Hum_Roll_Inner.Value && rRate < Plugin.Hum_RollRate_Inner.Value) {
                                    float dur = UnityEngine.Random.Range(Plugin.Hum_RollSleepMin.Value, Plugin.Hum_RollSleepMax.Value);
                                    rollSleepUntil = Time.time + dur;
                                    isRollSleeping = true;
                                }
                            } else {
                                if (rErr > Plugin.Hum_Roll_Outer.Value || rRate > Plugin.Hum_RollRate_Outer.Value || Time.time > rollSleepUntil) 
                                    isRollSleeping = false;
                            }
                        }

                        if (useHumanize && isRollSleeping) {
                            rollOut = (Mathf.PerlinNoise(0f, noiseT) - 0.5f) * Plugin.HumanizeStrength.Value * 0.5f;
                        } else {
                            float rollP = rollError * Plugin.RollP.Value;
                            rollIntegral = Mathf.Clamp(rollIntegral + (rollError * dt * Plugin.RollI.Value), -Plugin.RollILimit.Value, Plugin.RollILimit.Value);
                            float rollD = (0f - rollRate) * Plugin.RollD.Value;
                            rollOut = Mathf.Clamp(rollP + rollIntegral + rollD, -Plugin.RollMax.Value, Plugin.RollMax.Value);
                            if (!Plugin.InvertRoll.Value) rollOut = -rollOut;
                            if (useHumanize) rollOut += (Mathf.PerlinNoise(0f, noiseT) - 0.5f) * 2f * Plugin.HumanizeStrength.Value;
                        }

                        tInputWrapper.Field("pitch").SetValue(Mathf.Clamp(pitchOut, -1f, 1f));
                        tInputWrapper.Field("roll").SetValue(Mathf.Clamp(rollOut, -1f, 1f));
                        inputsModified = true;
                        stickPitch = pitchOut;

                        if (Plugin.ShowLogs.Value) {
                            logTimer++;
                            if (logTimer > Plugin.LogRefreshRate.Value) {
                                logTimer = 0;
                                Plugin.Logger.LogInfo($"[AP] GCAS:{APData.GCASActive} G:{currentG:F1} Alt:{APData.CurrentAlt:F0}");
                            }
                        }
                    }
                }
                
                if (inputsModified) tControlInputs.SetValue(inputObj);
                if (Plugin.LogPIDData.Value) {
                    float cvs = (APData.PlayerRB != null) ? APData.PlayerRB.velocity.y : 0f;
                    Console.WriteLine($"{Time.time:F2}\t{stickPitch:F4}\t{cvs:F4}\t{APData.CurrentAlt:F1}");
                }
            }
            catch (Exception ex) { 
                Plugin.Logger.LogError($"[ControlOverridePatch] Error: {ex}");
                APData.Enabled = false; 
            }
        }
    }

    [HarmonyPatch(typeof(FlightHud), "Update")]
    internal class HUDVisualsPatch
    {
        private static GameObject timerObj, rangeObj, apObj, ajObj, fbwObj;
        private static float lastFuelMass = 0f;
        private static float fuelFlowEma = 0f;
        private static float lastUpdateTime = 0f;
        
        // Track the gauge to detect aircraft/HUD switches
        private static FuelGauge lastFuelGauge; 

        private static void Postfix(FlightHud __instance)
        {
            if (!Plugin.ShowExtraInfo.Value) return;

            try {
                Aircraft aircraft = Traverse.Create(__instance).Field("playerVehicle").GetValue<Aircraft>();
                if (aircraft == null) return;

                // 1. Find the FuelGauge, even if it is currently disabled (Landing Mode)
                FuelGauge fg = __instance.GetComponentInChildren<FuelGauge>(true);
                if (fg == null) return;

                // 2. Reset if we switched aircraft/gauges
                if (lastFuelGauge != fg) {
                    if (timerObj) UnityEngine.Object.Destroy(timerObj);
                    if (rangeObj) UnityEngine.Object.Destroy(rangeObj);
                    if (apObj) UnityEngine.Object.Destroy(apObj);
                    if (ajObj) UnityEngine.Object.Destroy(ajObj);
                    if (fbwObj) UnityEngine.Object.Destroy(fbwObj);
                    timerObj = null; rangeObj = null; apObj = null; ajObj = null; fbwObj = null;
                    
                    lastFuelGauge = fg;
                    fuelFlowEma = 0f;
                    lastUpdateTime = 0f;
                }

                // 3. Create Objects if missing
                if (!timerObj) {
                    Text template = Traverse.Create(fg).Field("fuelLabel").GetValue<Text>();
                    if (template == null) return;

                    // Helper to spawn text attached to the HUD root, not the gauge
                    GameObject Spawn(string name) {
                        GameObject go = UnityEngine.Object.Instantiate(template.gameObject, __instance.transform);
                        go.name = name;
                        go.SetActive(true); // Force visible even if template is hidden
                        return go;
                    }

                    timerObj = Spawn("AP_Timer");
                    rangeObj = Spawn("AP_Range");
                    apObj = Spawn("AP_Status");
                    ajObj = Spawn("AP_Jammer");
                    fbwObj = Spawn("AP_FBW");
                }

                // 4. Update Positions (Snap to the fuel gauge position)
                // We do this every frame in case the UI moves or sways
                Text refLabel = Traverse.Create(fg).Field("fuelLabel").GetValue<Text>();
                if (refLabel != null) {
                    Vector3 basePos = refLabel.transform.position;
                    float startY = Plugin.FuelOffsetY.Value;
                    float gap = Plugin.FuelLineSpacing.Value;
                    
                    // Small helper to position a line
                    void Place(GameObject obj, int index) {
                        if (!obj) return;
                        // Use transform.up for rotation safety, though usually just Y
                        obj.transform.position = basePos + (obj.transform.up * (startY - (gap * index)));
                        if (!obj.activeSelf) obj.SetActive(true); // Ensure it stays visible
                    }

                    Place(timerObj, 0);
                    Place(rangeObj, 1);
                    Place(apObj, 2);
                    Place(ajObj, 3);
                    Place(fbwObj, 4);
                }

                // 5. Calculate Fuel Logic (Same as before)
                float currentFuel = Traverse.Create(aircraft).Field("fuelCapacity").GetValue<float>() * aircraft.GetFuelLevel();
                float time = Time.time;
                if (lastUpdateTime != 0f) {
                    float dt = time - lastUpdateTime;
                    if (dt >= Plugin.FuelUpdateInterval.Value) {
                        float burned = lastFuelMass - currentFuel;
                        float flow = Mathf.Max(0f, burned / dt);
                        fuelFlowEma = (Plugin.FuelSmoothing.Value * flow) + ((1f - Plugin.FuelSmoothing.Value) * fuelFlowEma);
                        lastUpdateTime = time;
                        lastFuelMass = currentFuel;
                    }
                } else { lastUpdateTime = time; lastFuelMass = currentFuel; }

                // 6. Update Text Content
                Text tText = timerObj.GetComponent<Text>();
                Text rText = rangeObj.GetComponent<Text>();
                Text aText = apObj.GetComponent<Text>();
                Text jText = ajObj.GetComponent<Text>();
                Text fText = fbwObj.GetComponent<Text>();

                if (currentFuel <= 1f) {
                    tText.text = "00:00"; tText.color = ModUtils.GetColor(Plugin.ColorCrit.Value, Color.red);
                    rText.text = "--- km"; rText.color = ModUtils.GetColor(Plugin.ColorCrit.Value, Color.red);
                } else {
                    float calcFlow = Mathf.Max(fuelFlowEma, 0.0001f);
                    float secs = currentFuel / calcFlow;
                    int h = Mathf.FloorToInt(secs / 3600f);
                    int m = Mathf.FloorToInt(secs % 3600f / 60f);
                    if (h > 99) { h = 99; m = 59; }
                    
                    tText.text = $"{h:D2}:{m:D2}";
                    float mins = secs / 60f;
                    if (mins < Plugin.FuelCritMinutes.Value) tText.color = ModUtils.GetColor(Plugin.ColorCrit.Value, Color.red);
                    else if (mins < Plugin.FuelWarnMinutes.Value) tText.color = ModUtils.GetColor(Plugin.ColorWarn.Value, Color.yellow);
                    else tText.color = ModUtils.GetColor(Plugin.ColorGood.Value, Color.green);

                    float spd = (aircraft.rb != null) ? aircraft.rb.velocity.magnitude : 0f;
                    float rangeKm = secs * spd / 1000f;
                    if (rangeKm > 9999f) rangeKm = 9999f;
                    rText.text = $"{rangeKm:F0} km";
                    rText.color = ModUtils.GetColor(Plugin.ColorInfo.Value, Color.cyan);
                }

                if (APData.Enabled) {
                    if (APData.GCASActive) {
                        aText.text = "GCAS ENGAGED";
                        aText.color = ModUtils.GetColor(Plugin.ColorCrit.Value, Color.red);
                    } else if (APData.GCASWarning) {
                        aText.text = "PULL UP";
                        aText.color = ModUtils.GetColor(Plugin.ColorWarn.Value, Color.red);
                    } else {
                        aText.text = $"A: {APData.TargetAlt:F0} {APData.CurrentMaxClimbRate:F0} {APData.TargetRoll:F0}";
                        aText.color = ModUtils.GetColor(Plugin.ColorAPOn.Value, Color.green);
                    }
                } else {
                    if (APData.GCASWarning) {
                        aText.text = "PULL UP";
                        aText.color = ModUtils.GetColor(Plugin.ColorWarn.Value, Color.red);
                    } else { aText.text = ""; }
                }
                
                if (APData.AutoJammerActive) {
                    jText.text = "AJ: ON";
                    jText.color = ModUtils.GetColor(Plugin.ColorAPOn.Value, Color.green);
                } else { jText.text = ""; }
                
                if (APData.FBWDisabled) {
                    fText.text = "FBW: OFF";
                    fText.color = ModUtils.GetColor(Plugin.ColorCrit.Value, Color.red);
                } else { fText.text = ""; }

            } catch (Exception ex) {
                Plugin.Logger.LogError($"[HUDVisualsPatch] Error: {ex}");
            }
        }
    }
}
