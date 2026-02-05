using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection.Emit;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Rewired;

namespace NOAutopilot
{
    [BepInPlugin(GUID, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = MyPluginInfo.PLUGIN_GUID;
        public const string Name = MyPluginInfo.PLUGIN_NAME;
        public const string Version = MyPluginInfo.PLUGIN_VERSION;

        internal new static ManualLogSource Logger;
        private Harmony harmony;

        // controls table
        private readonly string table =
        $"<b>Toggle AP GUI:</b> F8\n" +
        $"<b>Toggle Autopilot:</b> = (Equals)\n" +
        $"<b>Toggle AJ:</b> / (Slash)\n" +
        $"<b>Toggle GCAS:</b> \\ (Backslash)\n" +
        $"<b>Clear/Reset:</b> ' (Quote) | Roll>Nav>Crs>Roll>Alt>Roll><Roll\n\n" +

        $"<b>Target Alt Small:</b> Up / Down Arrow | Small adjustment\n" +
        $"<b>Target Alt Large:</b> Left / Right Arrow | Large adjustment\n" +
        $"<b>Max Climb Rate:</b> PgUp / PgDn | Limit vertical speed\n" +
        $"<b>Bank/Course L/R:</b> [ and ] | Adjust roll or heading\n\n" +

        $"<b>Toggle Speed Hold:</b> ; (Semicolon) | Matches current speed\n" +
        $"<b>Speed Up / Down:</b> LShift / LCtrl | Adjust target speed\n" +
        $"<b>Mach/TAS hold:</b> Home | Switch between Mach/TAS\n" +
        $"<b>Toggle AB:</b> End | Toggle Afterburner/Airbrake\n";

        // ap menu?
        private Rect _windowRect = new(50, 50, 227, 330);
        private bool _showMenu = false;

        private Vector2 _scrollPos;
        private bool _isResizing = false;
        private RectEdge _activeEdge = RectEdge.None;
        private enum RectEdge { None, Top, Bottom, Left, Right, TopLeft, TopRight, BottomLeft, BottomRight }

        private static string _bufAlt = "";
        private static string _bufClimb = "40";
        private static string _bufRoll = "";
        private static string _bufSpeed = "";
        private static string _bufCourse = "";

        public static ConfigEntry<float> NavReachDistance, NavPassedDistance;
        public static ConfigEntry<string> ColorNav;
        public static ConfigEntry<bool> NavCycle;

        private GUIStyle _styleWindow;
        private GUIStyle _styleLabel;
        private GUIStyle _styleReadout;
        private GUIStyle _styleButton;
        private bool _stylesInitialized = false;

        public static ConfigEntry<float> UI_PosX, UI_PosY;
        public static ConfigEntry<float> UI_Width, UI_Height;
        private bool _firstWindowInit = true;

        private string _currentHoverTarget = "";
        private string _lastActiveTooltip = "";
        private Vector2 _stationaryPos;
        private float _stationaryTimer = 0f;
        private bool _isTooltipVisible = false;
        private bool _wasShownForThisTarget = false;
        private readonly float _jitterThreshold = 7.0f;

        private float _dynamicLabelWidth = 60f;
        private readonly GUIContent _measuringContent = new();
        private readonly float buttonWidth = 40f;
        private GUIContent _cachedTableContent;
        private GUIContent _cachedExtraInfoContent;

        // Visuals
        public static ConfigEntry<string> ColorAPOn, ColorInfo, ColorGood, ColorWarn, ColorCrit, ColorRange;
        public static ConfigEntry<float> OverlayOffsetX, OverlayOffsetY, FuelSmoothing, FuelUpdateInterval, DisplayUpdateInterval;
        public static ConfigEntry<int> FuelWarnMinutes, FuelCritMinutes;
        public static ConfigEntry<bool> ShowExtraInfo;
        public static ConfigEntry<bool> ShowAPOverlay;
        public static ConfigEntry<bool> ShowGCASOff, ShowOverride, ShowPlaceholders;
        public static ConfigEntry<bool> AltShowUnit;
        public static ConfigEntry<bool> DistShowUnit;
        public static ConfigEntry<bool> VertSpeedShowUnit;
        public static ConfigEntry<bool> SpeedShowUnit; // for future
        public static ConfigEntry<bool> AngleShowUnit;

        // Settings
        public static ConfigEntry<float> StickTempThreshold, StickDisengageThreshold;
        public static ConfigEntry<float> DisengageDelay, ReengageDelay;
        public static ConfigEntry<bool> InvertRoll, InvertPitch, StickDisengageEnabled;
        public static ConfigEntry<bool> Conf_InvertCourseRoll, DisableATAPKey;
        public static ConfigEntry<bool> DisableATAPGCAS, DisableATAPGUI, DisableATAPStick,
        DisableNavAPKey, DisableNavAPStick, KeepSetAltKey, KeepSetAltStick;
        public static ConfigEntry<bool> UnlockMapPan, UnlockMapZoom, SaveMapState;

        // Auto Jammer
        public static ConfigEntry<bool> EnableAutoJammer;
        public static ConfigEntry<float> AutoJammerThreshold;
        public static ConfigEntry<bool> AutoJammerRandom;
        public static ConfigEntry<float> AutoJammerMinDelay, AutoJammerMaxDelay;
        public static ConfigEntry<float> AutoJammerReleaseMin, AutoJammerReleaseMax;

        // Controls
        public static ConfigEntry<KeyboardShortcut> MenuKey;
        public static ConfigEntry<KeyboardShortcut> ToggleKey, ToggleFBWKey;
        public static ConfigEntry<KeyboardShortcut> AutoJammerKey, ToggleGCASKey, ClearKey;
        public static ConfigEntry<KeyboardShortcut> UpKey, DownKey, BigUpKey, BigDownKey;
        public static ConfigEntry<KeyboardShortcut> ClimbRateUpKey, ClimbRateDownKey;
        public static ConfigEntry<KeyboardShortcut> BankLeftKey, BankRightKey;
        public static ConfigEntry<KeyboardShortcut> SpeedHoldKey, SpeedUpKey, SpeedDownKey;
        public static ConfigEntry<KeyboardShortcut> ToggleMachKey, ToggleABKey;

        public static ConfigEntry<string> MenuKeyRW;
        public static ConfigEntry<string> ToggleKeyRW, ToggleFBWKeyRW;
        public static ConfigEntry<string> AutoJammerKeyRW, ToggleGCASKeyRW, ClearKeyRW;
        public static ConfigEntry<string> UpKeyRW, DownKeyRW, BigUpKeyRW, BigDownKeyRW;
        public static ConfigEntry<string> ClimbRateUpKeyRW, ClimbRateDownKeyRW;
        public static ConfigEntry<string> BankLeftKeyRW, BankRightKeyRW;
        public static ConfigEntry<string> SpeedHoldKeyRW, SpeedUpKeyRW, SpeedDownKeyRW;
        public static ConfigEntry<string> ToggleMachKeyRW, ToggleABKeyRW;

        // Flight Values
        public static ConfigEntry<float> AltStep, BigAltStep, ClimbRateStep, BankStep, SpeedStep, MinAltitude;

        // Tuning
        public static ConfigEntry<float> DefaultMaxClimbRate, Conf_VS_MaxAngle, DefaultCRLimit;

        // pid
        public static ConfigEntry<float> Conf_Alt_P, Conf_Alt_I, Conf_Alt_D, Conf_Alt_ILimit;
        public static ConfigEntry<float> Conf_VS_P, Conf_VS_I, Conf_VS_D, Conf_VS_ILimit;
        public static ConfigEntry<float> Conf_Angle_P, Conf_Angle_I, Conf_Angle_D, Conf_Angle_ILimit;

        public static ConfigEntry<float> RollP, RollI, RollD, RollILimit;

        public static ConfigEntry<float> Conf_Spd_P, Conf_Spd_I, Conf_Spd_D, Conf_Spd_ILimit, Conf_Spd_C;
        public static ConfigEntry<float> ThrottleMinLimit, ThrottleMaxLimit, ThrottleSlewRate;

        public static ConfigEntry<float> Conf_Crs_P, Conf_Crs_I, Conf_Crs_D, Conf_Crs_ILimit;

        // Auto GCAS
        public static ConfigEntry<bool> EnableGCAS;
        public static ConfigEntry<float> GCAS_MaxG, GCAS_WarnBuffer, GCAS_AutoBuffer, GCAS_Deadzone, GCAS_ScanRadius;
        public static ConfigEntry<float> GCAS_P, GCAS_I, GCAS_D, GCAS_ILimit;

        // Random
        public static ConfigEntry<bool> RandomEnabled;
        public static ConfigEntry<float> RandomStrength, RandomSpeed;
        public static ConfigEntry<float> Rand_Alt_Inner, Rand_Alt_Outer, Rand_Alt_Scale;
        public static ConfigEntry<float> Rand_VS_Inner, Rand_VS_Outer;
        public static ConfigEntry<float> Rand_PitchSleepMin, Rand_PitchSleepMax;
        public static ConfigEntry<float> Rand_Roll_Inner, Rand_Roll_Outer, Rand_RollRate_Inner, Rand_RollRate_Outer;
        public static ConfigEntry<float> Rand_RollSleepMin, Rand_RollSleepMax;
        public static ConfigEntry<float> Rand_Spd_Inner, Rand_Spd_Outer;
        public static ConfigEntry<float> Rand_Spd_SleepMin, Rand_Spd_SleepMax;
        public static ConfigEntry<float> Rand_Acc_Inner, Rand_Acc_Outer;

        // reflection
        internal static FieldInfo f_playerVehicle;
        internal static FieldInfo f_controlInputs;
        internal static FieldInfo f_pitch, f_roll;
        internal static FieldInfo f_throttle;
        internal static FieldInfo f_targetList;
        internal static FieldInfo f_currentWeaponStation;
        internal static FieldInfo f_stationWeapons;

        internal static FieldInfo f_fuelLabel, f_fuelCapacity, f_controlsFilter;
        internal static FieldInfo f_pilots, f_gearState, f_weaponManager; // f_radarAlt;

        internal static FieldInfo f_powerSupply, f_charge, f_maxCharge;

        internal static MethodInfo m_Fire, m_GetAccel;

        internal static Type t_JammingPod;

        internal static MethodInfo m_GetFBWParams;
        internal static MethodInfo m_SetFBWParams;
        internal static FieldInfo f_fbw_item1_enabled;
        internal static FieldInfo f_fbw_item2_tuning;

        internal static Type t_NetworkServer;
        internal static PropertyInfo p_serverActive, p_serverAllPlayers;
        internal static Type t_NetworkClient;
        internal static PropertyInfo p_clientActive, p_clientIsHost;

        internal static FieldInfo f_mapPosOffset, f_mapStatOffset, f_mapFollow, f_onMapChanged;

        internal static Type t_GLOC;
        internal static FieldInfo f_bloodPressure;
        internal static FieldInfo f_conscious;

        private void Awake()
        {
            Logger = base.Logger;

            // Visuals
            ColorAPOn = Config.Bind("Visuals - Colors", "1. Color AP On", "#00FF00", "Green");
            ColorInfo = Config.Bind("Visuals - Colors", "2. Color Info", "#ffffff80", "color for override, gcas off");
            ColorGood = Config.Bind("Visuals - Colors", "3. Color Good", "#00FF00", "Green");
            ColorWarn = Config.Bind("Visuals - Colors", "4. Color Warning", "#FFFF00", "Yellow");
            ColorCrit = Config.Bind("Visuals - Colors", "5. Color Critical", "#FF0000", "Red");
            ColorRange = Config.Bind("Visuals - Colors", "6. Range display color", "#00FFFF", "color for range");
            ColorNav = Config.Bind("Visuals - Colors", "7. Navigation Color", "#ff00ffcc", "color for flight path lines.");
            OverlayOffsetX = Config.Bind("Visuals - Layout", "1. Stack Start X", -18f, "HUD Horizontal position");
            OverlayOffsetY = Config.Bind("Visuals - Layout", "2. Stack Start Y", -10f, "HUD Vertical position");
            ShowExtraInfo = Config.Bind("Visuals", "Show Fuel/AP Info", true, "Show extra info on Fuel Gauge");
            ShowAPOverlay = Config.Bind("Visuals", "Show AP Overlay", true, "Draw AP status text on the HUD. Turn off if you want, there's a window now.");
            ShowGCASOff = Config.Bind("Visuals", "Show GCAS OFF", true, "Show GCAS OFF on HUD");
            ShowOverride = Config.Bind("Visuals", "Show Override Delay", true, "Show Override on HUD");
            ShowPlaceholders = Config.Bind("Visuals", "Show Overlay Placeholders", false, "Show the A, V, W, when values default/null");
            DisplayUpdateInterval = Config.Bind("Visuals", "HUD overlay update interval", 0.02f, "seconds");
            AltShowUnit = Config.Bind("Visuals - Units", "1. Show unit for alt", false, "(example) on: 10m, off: 10");
            DistShowUnit = Config.Bind("Visuals - Units", "2. Show unit for dist", true, "(example) on: 10km, off: 10");
            VertSpeedShowUnit = Config.Bind("Visuals - Units", "3. Show unit for vertical speed", false, "(example) on: 10m/s, off: 10");
            SpeedShowUnit = Config.Bind("Visuals - Units", "4. Show unit for speed", false, "(example) on: 10km/h, off: 10 (unused right now, no autothrottle yet)");
            AngleShowUnit = Config.Bind("Visuals - Units", "5. Show unit for angle", false, "on: 10°, off: 10");

            UI_PosX = Config.Bind("Visuals - UI", "1. Window Position X", -1f, "-1 = Auto Bottom Right, otherwise pixel value");
            UI_PosY = Config.Bind("Visuals - UI", "2. Window Position Y", -1f, "-1 = Auto Bottom Right, otherwise pixel value");
            UI_Width = Config.Bind("Visuals - UI", "3. Window Width", 227f, "Saved Width");
            UI_Height = Config.Bind("Visuals - UI", "4. Window Height", 330f, "Saved Height");

            FuelSmoothing = Config.Bind("Calculations", "1. Fuel Flow Smoothing", 0.1f, "Alpha value");
            FuelUpdateInterval = Config.Bind("Calculations", "2. Fuel Update Interval", 1.0f, "Seconds");
            FuelWarnMinutes = Config.Bind("Calculations", "3. Fuel Warning Time", 15, "Minutes");
            FuelCritMinutes = Config.Bind("Calculations", "4. Fuel Critical Time", 5, "Minutes");

            // Settings
            StickTempThreshold = Config.Bind("Settings", "1. Temp disengage Stick Threshold", 0.01f, "for AP disengage via manual input");
            ReengageDelay = Config.Bind("Settings", "2. Reengage Delay (temp disengage)", 0.4f, "Seconds to wait after stick release before AP resumes control");
            DisengageDelay = Config.Bind("Settings", "3. Disengage Delay", 10f, "Seconds of continuous input before AP disengages (0 = off) (uses temp deadzone)");
            StickDisengageEnabled = Config.Bind("Settings", "4. Disengage on Large Input", true, "If true, moving the stick past a threshold turns AP OFF entirely.");
            StickDisengageThreshold = Config.Bind("Settings", "5. Large Input Disengage Threshold", 0.8f, "Stick input (0.0 to 1.0) required to disengage AP entirely");
            InvertRoll = Config.Bind("Settings", "6. Invert Roll", true, "Flip Roll");
            InvertPitch = Config.Bind("Settings", "7. Invert Pitch", true, "Flip Pitch");
            Conf_InvertCourseRoll = Config.Bind("Settings", "8. Invert Bank Direction", true, "Toggle if plane turns wrong way");
            DisableATAPGCAS = Config.Bind("Settings - Misc", "Disable autothrottle with AP (GCAS)", false, "Disable autothrottle when AP is disengaged by GCAS");
            DisableATAPGUI = Config.Bind("Settings - Misc", "Disable autothrottle with AP (GUI)", false, "Disable autothrottle when AP is disengaged by GUI");
            DisableATAPKey = Config.Bind("Settings - Misc", "Disable autothrottle with AP (key)", false, "Disable autothrottle when AP is disengaged by keyboard key");
            DisableATAPStick = Config.Bind("Settings - Misc", "Disable autothrottle with AP (stick)", false, "Disable autothrottle when AP is disengaged by stick input");
            DisableNavAPKey = Config.Bind("Settings - Misc", "Disable nav mode with AP (key)", false, "Disable nav mode when AP is disengaged by key");
            DisableNavAPStick = Config.Bind("Settings - Misc", "Disable nav mode with AP (stick)", false, "Disable nav mode when AP is disengaged by stick input");
            KeepSetAltKey = Config.Bind("Settings - Misc", "Keep set altitude when AP engaged (key)", false, "AP will use previously set alt instead of current alt when engaged by keyboard key");
            KeepSetAltStick = Config.Bind("Settings - Misc", "Keep set altitude when stick inputs made", true, "AP will not reset alt to current alt when stick inputs are made");
            UnlockMapPan = Config.Bind("Settings - Misc", "Unlock Map Pan", true, "Requires restart to apply.");
            UnlockMapZoom = Config.Bind("Settings - Misc", "Unlock Map Zoom", true, "Requires restart to apply.");
            SaveMapState = Config.Bind("Settings - Misc", "Save Map State", false, "Prevent map from resetting position/zoom when reopened.");
            APData.SaveMapState = SaveMapState.Value;

            // nav
            NavReachDistance = Config.Bind("Settings - Navigation", "1. Reach Distance", 2500f, "Distance in meters to consider a waypoint reached.");
            NavPassedDistance = Config.Bind("Settings - Navigation", "2. Passed Distance", 25000f, "Distance in meters after waypoint is behind plane to consider it reached");
            NavCycle = Config.Bind("Settings - Navigation", "3. Cycle wp", true, "On: cycles to next wp upon reaching wp, Off: Deletes wp upon reaching wp");

            // Auto Jammer
            EnableAutoJammer = Config.Bind("Auto Jammer", "1. Enable Auto Jammer", true, "Allow the feature");
            AutoJammerThreshold = Config.Bind("Auto Jammer", "3. Energy Threshold", 0.99f, "Fire when energy > this %");
            AutoJammerRandom = Config.Bind("Auto Jammer", "4. Random Delay", true, "Add random delay");
            AutoJammerMinDelay = Config.Bind("Auto Jammer", "5. Delay Min", 0.02f, "Seconds");
            AutoJammerMaxDelay = Config.Bind("Auto Jammer", "6. Delay Max", 0.04f, "Seconds");
            AutoJammerReleaseMin = Config.Bind("Auto Jammer", "7. Release Delay Min", 0.02f, "Seconds");
            AutoJammerReleaseMax = Config.Bind("Auto Jammer", "8. Release Delay Max", 0.04f, "Seconds");

            // Controls
            MenuKey = Config.Bind("Controls", "1. Menu Key", new KeyboardShortcut(KeyCode.F8), "Open the Autopilot Menu");
            ToggleKey = Config.Bind("Controls", "2. Toggle AP Key", new KeyboardShortcut(KeyCode.Equals), "AP On/Off");
            ToggleFBWKey = Config.Bind("Controls", "3. Toggle FBW Key", new KeyboardShortcut(KeyCode.Delete), "works in singleplayer");
            AutoJammerKey = Config.Bind("Controls", "4. Auto Jammer Key", new KeyboardShortcut(KeyCode.Slash), "Key to toggle jamming");
            ToggleGCASKey = Config.Bind("Controls", "5. Toggle GCAS Key", new KeyboardShortcut(KeyCode.Backslash), "Turn Auto-GCAS on/off");
            ClearKey = Config.Bind("Controls", "06. clear crs/roll/alt/roll", new KeyboardShortcut(KeyCode.Quote), "every click will clear/reset first thing it sees isn't clear from left to right");
            UpKey = Config.Bind("Controls - Altitude", "1. Altitude Up (Small)", new KeyboardShortcut(KeyCode.UpArrow), "small increase");
            DownKey = Config.Bind("Controls - Altitude", "2. Altitude Down (Small)", new KeyboardShortcut(KeyCode.DownArrow), "small decrease");
            BigUpKey = Config.Bind("Controls - Altitude", "3. Altitude Up (Big)", new KeyboardShortcut(KeyCode.LeftArrow), "large increase");
            BigDownKey = Config.Bind("Controls - Altitude", "4. Altitude Down (Big)", new KeyboardShortcut(KeyCode.RightArrow), "large decrease");
            ClimbRateUpKey = Config.Bind("Controls - Altitude", "5. Climb Rate Increase", new KeyboardShortcut(KeyCode.PageUp), "Increase Max VS");
            ClimbRateDownKey = Config.Bind("Controls - Altitude", "6. Climb Rate Decrease", new KeyboardShortcut(KeyCode.PageDown), "Decrease Max VS");
            BankLeftKey = Config.Bind("Controls - Bank", "1. Bank Left", new KeyboardShortcut(KeyCode.LeftBracket), "Roll/course Left");
            BankRightKey = Config.Bind("Controls - Bank", "2. Bank Right", new KeyboardShortcut(KeyCode.RightBracket), "Roll/course right");
            SpeedHoldKey = Config.Bind("Controls - Speed", "1. Speed Hold Toggle", new KeyboardShortcut(KeyCode.Semicolon), "speed hold/clear");
            SpeedUpKey = Config.Bind("Controls - Speed", "2. Target Speed Increase", new KeyboardShortcut(KeyCode.LeftShift), "Increase target speed");
            SpeedDownKey = Config.Bind("Controls - Speed", "3. Target Speed Decrease", new KeyboardShortcut(KeyCode.LeftControl), "Decrease target speed");
            ToggleMachKey = Config.Bind("Controls - Speed", "4. Toggle Mach/TAS", new KeyboardShortcut(KeyCode.Home), "Toggle between Mach and TAS hold");
            ToggleABKey = Config.Bind("Controls - Speed", "5. Toggle Afterburner/Airbrake", new KeyboardShortcut(KeyCode.End), "Toggle AB/Airbrake limits");

            // Controls (Rewired)
            MenuKeyRW = RewiredConfigManager.BindRW(Config, "Controls (Rewired)", "1. Menu Key", "Open the Autopilot Menu");
            ToggleKeyRW = RewiredConfigManager.BindRW(Config, "Controls (Rewired)", "2. Toggle AP Key", "AP On/Off");
            ToggleFBWKeyRW = RewiredConfigManager.BindRW(Config, "Controls (Rewired)", "3. Toggle FBW Key", "works in singleplayer");
            AutoJammerKeyRW = RewiredConfigManager.BindRW(Config, "Controls (Rewired)", "4. Toggle Key", "Key to toggle jamming");
            ToggleGCASKeyRW = RewiredConfigManager.BindRW(Config, "Controls (Rewired)", "5. Toggle GCAS Key", "Turn Auto-GCAS on/off");
            ClearKeyRW = RewiredConfigManager.BindRW(Config, "Controls (Rewired)", "6. clear crs/roll/alt/roll", "every click will clear/reset first thing it sees isn't clear from left to right");
            UpKeyRW = RewiredConfigManager.BindRW(Config, "Controls - Altitude (Rewired)", "1. Altitude Up (Small)", "small increase");
            DownKeyRW = RewiredConfigManager.BindRW(Config, "Controls - Altitude (Rewired)", "2. Altitude Down (Small)", "small decrease");
            BigUpKeyRW = RewiredConfigManager.BindRW(Config, "Controls - Altitude (Rewired)", "3. Altitude Up (Big)", "large increase");
            BigDownKeyRW = RewiredConfigManager.BindRW(Config, "Controls - Altitude (Rewired)", "4. Altitude Down (Big)", "large decrease");
            ClimbRateUpKeyRW = RewiredConfigManager.BindRW(Config, "Controls - Altitude (Rewired)", "5. Climb Rate Increase", "Increase Max VS");
            ClimbRateDownKeyRW = RewiredConfigManager.BindRW(Config, "Controls - Altitude (Rewired)", "6. Climb Rate Decrease", "Decrease Max VS");
            BankLeftKeyRW = RewiredConfigManager.BindRW(Config, "Controls - Bank (Rewired)", "1. Bank Left", "Roll/course Left");
            BankRightKeyRW = RewiredConfigManager.BindRW(Config, "Controls - Bank (Rewired)", "2. Bank Right", "Roll/course right");
            SpeedHoldKeyRW = RewiredConfigManager.BindRW(Config, "Controls - Speed (Rewired)", "1. Speed Hold Toggle", "speed hold/clear");
            SpeedUpKeyRW = RewiredConfigManager.BindRW(Config, "Controls - Speed (Rewired)", "2. Target Speed Increase", "Increase target speed");
            SpeedDownKeyRW = RewiredConfigManager.BindRW(Config, "Controls - Speed (Rewired)", "3. Target Speed Decrease", "Decrease target speed");
            ToggleMachKeyRW = RewiredConfigManager.BindRW(Config, "Controls - Speed (Rewired)", "4. Toggle Mach/TAS", "Toggle between Mach and TAS hold");
            ToggleABKeyRW = RewiredConfigManager.BindRW(Config, "Controls - Speed (Rewired)", "5. Toggle Afterburner/Airbrake", "Toggle AB/Airbrake limits");

            // control values
            AltStep = Config.Bind("Controls - Values", "17. Altitude Increment (Small)", 0.1f, "Meters per frame (60fps)");
            BigAltStep = Config.Bind("Controls - Values", "18. Altitude Increment (Big)", 100f, "Meters per frame (60fps)");
            ClimbRateStep = Config.Bind("Controls - Values", "19. Climb Rate Step", 0.5f, "m/s per frame (60fps)");
            BankStep = Config.Bind("Controls - Values", "20. Bank Step", 0.5f, "Degrees per frame (60fps)");
            SpeedStep = Config.Bind("Controls - Values", "21. Speed Step", 1.0f, "m/s per frame (60fps)");
            MinAltitude = Config.Bind("Controls - Values", "22. Minimum Target Altitude", 20f, "Safety floor");

            // Tuning
            DefaultMaxClimbRate = Config.Bind("Tuning - 0. Limits", "1. Default Max Climb Rate", 10f, "Startup value");
            Conf_VS_MaxAngle = Config.Bind("Tuning - 0. Limits", "2. Max Pitch Angle", 30.0f, "anti stall limit?");
            DefaultCRLimit = Config.Bind("Tuning - 0. Limits", "3. Default course roll limit", 30.0f, "roll limit when turning in course/nav mode");

            // Loops
            Conf_Alt_P = Config.Bind("Tuning - 1. Altitude", "1. Alt P", 0.5f, "Alt Error -> Target VS");
            Conf_Alt_I = Config.Bind("Tuning - 1. Altitude", "2. Alt I", 0.0f, "Accumulates Error");
            Conf_Alt_D = Config.Bind("Tuning - 1. Altitude", "3. Alt D", 1.5f, "Dampens Approach");
            Conf_Alt_ILimit = Config.Bind("Tuning - 1. Altitude", "4. Alt I Limit", 10.0f, "Max Integral (m/s)");
            Conf_VS_P = Config.Bind("Tuning - 2. VertSpeed", "1. VS P", 0.5f, "VS Error -> Target Angle");
            Conf_VS_I = Config.Bind("Tuning - 2. VertSpeed", "2. VS I", 0.1f, "Trim Angle");
            Conf_VS_D = Config.Bind("Tuning - 2. VertSpeed", "3. VS D", 0.1f, "Dampens VS Change");
            Conf_VS_ILimit = Config.Bind("Tuning - 2. VertSpeed", "4. VS I Limit", 90.0f, "Max Trim (Deg)");
            Conf_Angle_P = Config.Bind("Tuning - 3. Angle", "1. Angle P", 0.01f, "Angle Error -> Stick");
            Conf_Angle_I = Config.Bind("Tuning - 3. Angle", "2. Angle I", 0.0f, "Holds Angle");
            Conf_Angle_D = Config.Bind("Tuning - 3. Angle", "3. Angle D", 0.0f, "Dampens Rotation");
            Conf_Angle_ILimit = Config.Bind("Tuning - 3. Angle", "4. Angle I Limit", 90.0f, "Max Integral (Stick)");
            RollP = Config.Bind("Tuning - Roll", "1. Roll P", 0.01f, "P");
            RollI = Config.Bind("Tuning - Roll", "2. Roll I", 0.002f, "I");
            RollD = Config.Bind("Tuning - Roll", "3. Roll D", 0.001f, "D");
            RollILimit = Config.Bind("Tuning - Roll", "5. Roll I Limit", 1.0f, "Limit");
            Conf_Spd_P = Config.Bind("Tuning - 4. Speed", "1. Speed P", 0.05f, "Error -> Throttle");
            Conf_Spd_I = Config.Bind("Tuning - 4. Speed", "2. Speed I", 0.01f, "Hold speed");
            Conf_Spd_D = Config.Bind("Tuning - 4. Speed", "3. Speed D", 0.0f, "Dampen");
            Conf_Spd_ILimit = Config.Bind("Tuning - 4. Speed", "4. Speed I Limit", 1.0f, "Max Throttle Trim");
            Conf_Spd_C = Config.Bind("Tuning - 4. Speed", "3. Pitch compensation", 0.4f, "Multiplier for throttle pitch compensation");
            ThrottleMinLimit = Config.Bind("Tuning - 4. Speed", "6. Safe Min Throttle", 0.01f, "Minimum throttle when limiter is active (prevents Airbrake)");
            ThrottleMaxLimit = Config.Bind("Tuning - 4. Speed", "7. Safe Max Throttle", 0.89f, "Maximum throttle when limiter is active (prevents Afterburner)");
            ThrottleSlewRate = Config.Bind("Tuning - 4. Speed", "8. Throttle Slew Rate Limit", 0.2f, "in unit of throttle bars per second");
            Conf_Crs_P = Config.Bind("Tuning - 5. Course", "1. Course P", 0.5f, "Course Error -> Bank Angle");
            Conf_Crs_I = Config.Bind("Tuning - 5. Course", "2. Course I", 0.01f, "Correction");
            Conf_Crs_D = Config.Bind("Tuning - 5. Course", "3. Course D", 0.15f, "Dampen");
            Conf_Crs_ILimit = Config.Bind("Tuning - 5. Course", "4. Course I Limit", 70.0f, "Max Integral Bank");

            // Auto GCAS
            EnableGCAS = Config.Bind("Auto GCAS", "1. Enable GCAS on start", true, "GCAS off at start if disabled");
            GCAS_MaxG = Config.Bind("Auto GCAS", "3. Max G-Pull", 5.0f, "Assumed G-Force capability for calculation");
            GCAS_WarnBuffer = Config.Bind("Auto GCAS", "4. Warning Buffer", 20.0f, "GCAS warning indicator first appearance");
            GCAS_AutoBuffer = Config.Bind("Auto GCAS", "5. Auto-Pull Buffer", 1.0f, "Safety margin seconds");
            GCAS_Deadzone = Config.Bind("Auto GCAS", "6. GCAS Deadzone", 0.5f, "GCAS override deadzone");
            GCAS_ScanRadius = Config.Bind("Auto GCAS", "7. Scan Radius", 2.0f, "Width of the spherecast (m)");
            GCAS_P = Config.Bind("GCAS PID", "1. GCAS P", 0.1f, "G Error -> Stick");
            GCAS_I = Config.Bind("GCAS PID", "2. GCAS I", 0.5f, "Builds pull over time");
            GCAS_D = Config.Bind("GCAS PID", "3. GCAS D", 0.0f, "Dampens G overshoot");
            GCAS_ILimit = Config.Bind("GCAS PID", "4. GCAS I Limit", 1.0f, "Max stick influence");

            // Random
            RandomEnabled = Config.Bind("Settings - Random", "01. Random Enabled", false, "Add imperfections (needs some work i think)");
            RandomStrength = Config.Bind("Settings - Random", "02. Noise Strength", 0.01f, "Jitter amount");
            RandomSpeed = Config.Bind("Settings - Random", "03. Noise Speed", 1.0f, "Jitter freq");
            Rand_Alt_Inner = Config.Bind("Settings - Random", "04. Alt Tolerance Inner", 0.1f, "Start Sleeping (m)");
            Rand_Alt_Outer = Config.Bind("Settings - Random", "05. Alt Tolerance Outer", 1.0f, "Wake Up (m)");
            Rand_Alt_Scale = Config.Bind("Settings - Random", "06. Alt Scale", 0.01f, "Increase per meter alt");
            Rand_VS_Inner = Config.Bind("Settings - Random", "07. VS Tolerance Inner", 0.01f, "Start Sleeping (m/s)");
            Rand_VS_Outer = Config.Bind("Settings - Random", "08. VS Tolerance Outer", 5.0f, "Wake Up (m/s)");
            Rand_PitchSleepMin = Config.Bind("Settings - Random", "09. Pitch Sleep Min", 2.0f, "Seconds");
            Rand_PitchSleepMax = Config.Bind("Settings - Random", "10. Pitch Sleep Max", 60.0f, "Seconds");
            Rand_Roll_Inner = Config.Bind("Settings - Random", "11. Roll Tolerance Inner", 0.1f, "Start Sleeping (deg)");
            Rand_Roll_Outer = Config.Bind("Settings - Random", "12. Roll Tolerance Outer", 1.0f, "Wake Up (deg)");
            Rand_RollRate_Inner = Config.Bind("Settings - Random", "13. Roll Rate Tolerance Inner", 1.0f, "Start Sleeping (deg/s)");
            Rand_RollRate_Outer = Config.Bind("Settings - Random", "14. Roll Rate Tolerance Outer", 20.0f, "Wake Up (deg/s)");
            Rand_RollSleepMin = Config.Bind("Settings - Random", "15. Roll Sleep Min", 1.5f, "Seconds");
            Rand_RollSleepMax = Config.Bind("Settings - Random", "16. Roll Sleep Max", 60.0f, "Seconds");
            Rand_Spd_Inner = Config.Bind("Settings - Random", "17. Speed Tolerance Inner", 0.5f, "Start Sleeping (m/s error)");
            Rand_Spd_Outer = Config.Bind("Settings - Random", "18. Speed Tolerance Outer", 2.0f, "Wake Up (m/s error)");
            Rand_Spd_SleepMin = Config.Bind("Settings - Random", "19. Speed Sleep Min", 2.0f, "Seconds");
            Rand_Spd_SleepMax = Config.Bind("Settings - Random", "20. Speed Sleep Max", 60.0f, "Seconds");
            Rand_Acc_Inner = Config.Bind("Settings - Random", "21. Accel Tolerance Inner", 0.05f, "Start Sleeping (m/s² acceleration)");
            Rand_Acc_Outer = Config.Bind("Settings - Random", "22. Accel Tolerance Outer", 0.5f, "Wake Up (m/s² acceleration)");

            // reflection cache
            try
            {
                static void Check(object obj, string name)
                {
                    if (obj == null) throw new Exception($"[Reflection] missing: {name}");
                }

                BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                BindingFlags privateFlags = BindingFlags.NonPublic | BindingFlags.Instance;

                f_playerVehicle = typeof(FlightHud).GetField("playerVehicle", flags);
                Check(f_playerVehicle, "f_playerVehicle");

                f_controlInputs = typeof(PilotPlayerState).GetField("controlInputs", flags);
                if (f_controlInputs == null && typeof(PilotPlayerState).BaseType != null)
                {
                    f_controlInputs = typeof(PilotPlayerState).BaseType.GetField("controlInputs", flags);
                }
                Check(f_controlInputs, "f_controlInputs");

                Type inputType = f_controlInputs.FieldType;
                f_pitch = inputType.GetField("pitch", flags);
                f_roll = inputType.GetField("roll", flags);
                f_throttle = inputType.GetField("throttle", flags);

                Check(f_pitch, "f_pitch");
                Check(f_roll, "f_roll");
                Check(f_throttle, "f_throttle");

                f_controlsFilter = typeof(Aircraft).GetField("controlsFilter", flags);
                f_fuelCapacity = typeof(Aircraft).GetField("fuelCapacity", flags);
                f_pilots = typeof(Aircraft).GetField("pilots", flags);
                f_gearState = typeof(Aircraft).GetField("gearState", flags);
                f_weaponManager = typeof(Aircraft).GetField("weaponManager", flags);

                Check(f_fuelCapacity, "f_controlsFilter");
                Check(f_fuelCapacity, "f_fuelCapacity");
                Check(f_pilots, "f_pilots");
                Check(f_gearState, "f_gearState");
                Check(f_weaponManager, "f_weaponManager");

                Type psType = typeof(Aircraft).Assembly.GetType("PowerSupply");
                Check(psType, "PowerSupply Type");

                f_charge = psType.GetField("charge", flags);
                f_maxCharge = psType.GetField("maxCharge", flags);
                f_powerSupply = typeof(Aircraft).GetField("powerSupply", flags);

                Check(f_charge, "f_charge");
                Check(f_maxCharge, "f_maxCharge");
                Check(f_powerSupply, "f_powerSupply");

                Type t_Pilot = typeof(Aircraft).Assembly.GetType("Pilot");
                Check(t_Pilot, "t_Pilot");

                m_GetAccel = t_Pilot.GetMethod("GetAccel");
                Check(m_GetAccel, "m_GetAccel");

                Type t_WeaponManager = typeof(Aircraft).Assembly.GetType("WeaponManager");
                Check(t_WeaponManager, "t_WeaponManager");

                m_Fire = t_WeaponManager.GetMethod("Fire", flags, null, Type.EmptyTypes, null);
                f_targetList = t_WeaponManager.GetField("targetList", flags);
                f_currentWeaponStation = t_WeaponManager.GetField("currentWeaponStation", flags);

                Check(m_Fire, "m_Fire");
                Check(f_targetList, "f_targetList");
                Check(f_currentWeaponStation, "f_currentWeaponStation");

                Type t_WeaponStation = typeof(Aircraft).Assembly.GetType("WeaponStation");
                Check(t_WeaponStation, "t_WeaponStation");

                f_stationWeapons = t_WeaponStation.GetField("Weapons", flags);
                Check(f_stationWeapons, "f_stationWeapons");

                t_JammingPod = typeof(Aircraft).Assembly.GetType("JammingPod");
                Check(t_JammingPod, "t_JammingPod");

                Type t_FuelGauge = typeof(Aircraft).Assembly.GetType("FuelGauge");
                Check(t_FuelGauge, "t_FuelGauge");
                f_fuelLabel = t_FuelGauge.GetField("fuelLabel", flags);
                Check(f_fuelLabel, "f_fuelLabel");

                Type t_ControlsFilter = typeof(ControlsFilter);
                Check(t_ControlsFilter, "t_ControlsFilter");
                m_GetFBWParams = t_ControlsFilter.GetMethod("GetFlyByWireParameters", flags);
                m_SetFBWParams = t_ControlsFilter.GetMethod("SetFlyByWireParameters", flags);
                Check(m_GetFBWParams, "m_GetFBWParams");
                Check(m_SetFBWParams, "m_SetFBWParams");
                Type fbwTupleType = m_GetFBWParams.ReturnType;
                f_fbw_item1_enabled = fbwTupleType.GetField("Item1");
                Check(f_fbw_item1_enabled, "f_fbw_item1_enabled");
                f_fbw_item2_tuning = fbwTupleType.GetField("Item2");
                Check(f_fbw_item2_tuning, "f_fbw_item2_tuning");

                t_NetworkServer = AccessTools.TypeByName("Mirage.NetworkServer");
                Check(t_NetworkServer, "t_NetworkServer");
                p_serverActive = AccessTools.Property(t_NetworkServer, "Active");
                Check(p_serverActive, "p_serverActive");
                p_serverAllPlayers = AccessTools.Property(t_NetworkServer, "AllPlayers");
                Check(p_serverAllPlayers, "p_serverAllPlayers");
                t_NetworkClient = AccessTools.TypeByName("Mirage.NetworkClient");
                Check(t_NetworkClient, "t_NetworkClient");
                p_clientActive = AccessTools.Property(t_NetworkClient, "Active");
                Check(p_clientActive, "p_clientActive");
                p_clientIsHost = AccessTools.Property(t_NetworkClient, "IsHost");
                Check(p_clientIsHost, "p_clientIsHost");

                Type t_Map = typeof(DynamicMap);
                f_mapPosOffset = t_Map.GetField("positionOffset", flags);
                Check(f_mapPosOffset, "f_mapPosOffset");
                f_mapStatOffset = t_Map.GetField("stationaryOffset", flags);
                Check(f_mapStatOffset, "f_mapStatOffset");
                f_mapFollow = t_Map.GetField("followingCamera", flags);
                Check(f_mapFollow, "f_mapFollow");
                f_onMapChanged = t_Map.GetField("onMapChanged", flags | BindingFlags.Static);
                Check(f_onMapChanged, "f_onMapChanged");

                t_GLOC = typeof(Aircraft).Assembly.GetType("GLOC");
                Check(t_GLOC, "t_GLOC");
                f_bloodPressure = t_GLOC.GetField("bloodPressure", privateFlags);
                f_conscious = t_GLOC.GetField("conscious", privateFlags);
                Check(f_bloodPressure, "f_bloodPressure");
                Check(f_conscious, "f_conscious");
            }
            catch (Exception ex)
            {
                Logger.LogFatal("Failed to cache reflection fields! " + ex);
                Logger.LogFatal("The mod will now disable itself to prevent further issues.");
                enabled = false;
                return;
            }

            harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            Logger.LogInfo($"v{Version} loaded.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();

            SceneManager.sceneUnloaded -= OnSceneUnloaded;

            APData.Reset();
            ControlOverridePatch.Reset();
            HUDVisualsPatch.Reset();
            HudPatch.Reset();
            MapInteractionPatch.Reset();
            MapWaypointPatch.Reset();
            RewiredConfigManager.Reset();
            if (APData.NavVisuals != null)
            {
                foreach (var obj in APData.NavVisuals) if (obj != null) Destroy(obj);
            }

            ClearAllStatics();

            harmony = null;
            Logger = null;
        }

        private void InitStyles()
        {
            _styleWindow = new GUIStyle(GUI.skin.window);

            _styleLabel = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                richText = true
            };

            _styleReadout = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                richText = true,
                padding = new RectOffset(3, 3, 3, 3)
            };

            _styleButton = new GUIStyle(GUI.skin.button);

            _stylesInitialized = true;
        }

        private void Update()
        {
            RewiredConfigManager.Update();

            if (InputHelper.IsDown(MenuKeyRW) || MenuKey.Value.IsDown())
            {
                _showMenu = !_showMenu;
                if (_showMenu)
                {
                    SyncMenuValues();
                }
            }

            if (_showMenu && APData.Enabled)
            {
                bool isAdjusting =
                    InputHelper.IsPressed(UpKeyRW) || UpKey.Value.IsPressed() ||
                    InputHelper.IsPressed(DownKeyRW) || DownKey.Value.IsPressed() ||
                    InputHelper.IsPressed(BigUpKeyRW) || BigUpKey.Value.IsPressed() ||
                    InputHelper.IsPressed(BigDownKeyRW) || BigDownKey.Value.IsPressed() ||
                    InputHelper.IsPressed(ClimbRateUpKeyRW) || ClimbRateUpKey.Value.IsPressed() ||
                    InputHelper.IsPressed(ClimbRateDownKeyRW) || ClimbRateDownKey.Value.IsPressed() ||
                    InputHelper.IsPressed(BankLeftKeyRW) || BankLeftKey.Value.IsPressed() ||
                    InputHelper.IsPressed(BankRightKeyRW) || BankRightKey.Value.IsPressed() ||
                    InputHelper.IsPressed(ClearKeyRW) || ClearKey.Value.IsPressed();

                if (isAdjusting)
                {
                    SyncMenuValues();
                }
            }

            if (InputHelper.IsDown(ToggleKeyRW) || ToggleKey.Value.IsDown())
            {
                APData.Enabled = !APData.Enabled;
                if (!APData.Enabled)
                {
                    if (DisableNavAPKey.Value) APData.NavEnabled = false;
                    if (DisableATAPKey.Value) APData.TargetSpeed = -1f;
                }
                else if (!KeepSetAltKey.Value)
                {
                    APData.TargetAlt = APData.CurrentAlt;
                }
                SyncMenuValues();
            }

            if (EnableAutoJammer.Value && (InputHelper.IsDown(AutoJammerKeyRW) || AutoJammerKey.Value.IsDown()))
            {
                APData.AutoJammerActive = !APData.AutoJammerActive;
            }

            if (InputHelper.IsDown(ToggleGCASKeyRW) || ToggleGCASKey.Value.IsDown())
            {
                APData.GCASEnabled = !APData.GCASEnabled;
                if (!APData.GCASEnabled) { APData.GCASActive = false; APData.GCASWarning = false; }
            }

            if (InputHelper.IsDown(SpeedHoldKeyRW) || SpeedHoldKey.Value.IsDown())
            {
                if (APData.TargetSpeed >= 0)
                {
                    APData.TargetSpeed = -1f;
                    _bufSpeed = "";
                }
                else if (APData.PlayerRB != null)
                {
                    float currentTAS = (APData.LocalAircraft != null) ? APData.LocalAircraft.speed : APData.PlayerRB.velocity.magnitude;
                    if (APData.SpeedHoldIsMach)
                    {
                        float currentAlt = (APData.LocalAircraft != null) ? APData.LocalAircraft.GlobalPosition().y : 0f;
                        float sos = LevelInfo.GetSpeedOfSound(currentAlt);
                        APData.TargetSpeed = currentTAS / sos;
                        _bufSpeed = APData.TargetSpeed.ToString("F2");
                    }
                    else
                    {
                        APData.TargetSpeed = currentTAS;
                        _bufSpeed = ModUtils.ConvertSpeed_ToDisplay(currentTAS).ToString("F0");
                    }
                }
                SyncMenuValues();
                GUI.FocusControl(null);
            }

            if (APData.TargetSpeed >= 0f)
            {
                bool speedUp = InputHelper.IsPressed(SpeedUpKeyRW) || SpeedUpKey.Value.IsPressed();
                bool speedDown = InputHelper.IsPressed(SpeedDownKeyRW) || SpeedDownKey.Value.IsPressed();
                if (speedUp || speedDown)
                {
                    if (APData.TargetSpeed < 0)
                    {
                        float currentTAS = (APData.LocalAircraft != null) ? APData.LocalAircraft.speed : APData.PlayerRB.velocity.magnitude;
                        if (APData.SpeedHoldIsMach)
                        {
                            float currentAlt = (APData.LocalAircraft != null) ? APData.LocalAircraft.GlobalPosition().y : 0f;
                            APData.TargetSpeed = currentTAS / LevelInfo.GetSpeedOfSound(currentAlt);
                        }
                        else APData.TargetSpeed = currentTAS;
                    }

                    float step = SpeedStep.Value * 60f * Time.deltaTime;
                    if (APData.SpeedHoldIsMach)
                    {
                        float currentAlt = (APData.LocalAircraft != null) ? APData.LocalAircraft.GlobalPosition().y : 0f;
                        float sos = LevelInfo.GetSpeedOfSound(currentAlt);
                        step /= Mathf.Max(sos, 1f);
                    }

                    if (speedUp) APData.TargetSpeed += step;
                    if (speedDown) APData.TargetSpeed = Mathf.Max(0, APData.TargetSpeed - step);
                    SyncMenuValues();
                }
                else if (InputHelper.IsDown(ToggleMachKeyRW) || ToggleMachKey.Value.IsDown())
                {
                    if (float.TryParse(_bufSpeed, out float val))
                    {
                        float currentAlt = (APData.LocalAircraft != null) ? APData.LocalAircraft.GlobalPosition().y : 0f;
                        float sos = LevelInfo.GetSpeedOfSound(currentAlt);
                        if (!APData.SpeedHoldIsMach)
                        {
                            float ms = ModUtils.ConvertSpeed_FromDisplay(val);
                            APData.TargetSpeed = Mathf.Max(0, ms / sos);
                        }
                        else
                        {
                            float ms = val * sos;
                            APData.TargetSpeed = Mathf.Max(0, ms);
                        }
                    }
                    APData.SpeedHoldIsMach = !APData.SpeedHoldIsMach;
                    SyncMenuValues();
                }

                if (InputHelper.IsDown(ToggleABKeyRW) || ToggleABKey.Value.IsDown())
                {
                    APData.AllowExtremeThrottle = !APData.AllowExtremeThrottle;
                }
            }

            if (InputHelper.IsDown(ToggleFBWKeyRW) || ToggleFBWKey.Value.IsDown())
            {
                APData.NextMultiplayerCheck = 0f;
                if (IsMultiplayer())
                {
                    APData.FBWDisabled = false;
                }
                else
                {
                    APData.FBWDisabled = !APData.FBWDisabled;
                }
                UpdateFBWState();
            }

            // no need because plane respawns and the toggle above exists
            // not worth the lag spikes
            // if (APData.FBWDisabled && IsMultiplayer())
            // {
            //     APData.FBWDisabled = false;
            //     UpdateFBWState();
            // }

            if (APData.PlayerRB != null)
            {
                float rawSpeed = APData.PlayerRB.velocity.magnitude;
                float alpha = 1.0f - Mathf.Exp(-Time.deltaTime * 2.0f);
                APData.SpeedEma = Mathf.Lerp(APData.SpeedEma, rawSpeed, alpha);
            }
        }

        public static void SyncMenuValues()
        {
            _bufAlt = (APData.TargetAlt > 0)
                ? ModUtils.ConvertAlt_ToDisplay(APData.TargetAlt).ToString("F0")
                : "";

            _bufClimb = (APData.CurrentMaxClimbRate > 0)
                ? ModUtils.ConvertVS_ToDisplay(APData.CurrentMaxClimbRate).ToString("F0")
                : DefaultMaxClimbRate.Value.ToString();

            _bufRoll = (APData.TargetRoll != -999f) ? APData.TargetRoll.ToString("F0") : "";

            if (APData.TargetSpeed >= 0)
            {
                if (APData.SpeedHoldIsMach)
                    _bufSpeed = APData.TargetSpeed.ToString("F2");
                else
                    _bufSpeed = ModUtils.ConvertSpeed_ToDisplay(APData.TargetSpeed).ToString("F0");
            }
            else
            {
                _bufSpeed = "";
            }

            _bufCourse = (APData.TargetCourse >= 0) ? APData.TargetCourse.ToString("F0") : "";
        }

        // gui
        private void OnGUI()
        {
            if (_cachedTableContent == null)
            {
                _cachedTableContent = new GUIContent("(Hover for controls)", table);
                _cachedExtraInfoContent = new GUIContent("(Hover above for some info)\n(Hover here for controls)", table);
            }
            if (!_showMenu) return;

            float guiAlpha = 1f;
            if (!APData.IsConscious)
            {
                guiAlpha = 0f;
            }
            else
            {
                guiAlpha = Mathf.Clamp01((APData.BloodPressure - 0.2f) / 0.4f);
            }

            if (guiAlpha <= 0f)
            {
                _isResizing = false;
                return;
            }

            if (!_stylesInitialized) InitStyles();

            Color oldGuiColor = GUI.color;
            try
            {
                GUI.color = new Color(1, 1, 1, guiAlpha);

                if (_isResizing)
                {
                    if (Event.current.type == EventType.MouseUp) { _isResizing = false; _activeEdge = RectEdge.None; }
                    else if (Event.current.type == EventType.MouseDrag)
                    {
                        Vector2 delta = Event.current.delta;
                        float minW = 227f;
                        float minH = 330f;

                        if (_activeEdge == RectEdge.Right || _activeEdge == RectEdge.TopRight || _activeEdge == RectEdge.BottomRight)
                            _windowRect.width = Mathf.Max(minW, _windowRect.width + delta.x);

                        if (_activeEdge == RectEdge.Bottom || _activeEdge == RectEdge.BottomLeft || _activeEdge == RectEdge.BottomRight)
                            _windowRect.height = Mathf.Max(minH, _windowRect.height + delta.y);

                        if (_activeEdge == RectEdge.Left || _activeEdge == RectEdge.TopLeft || _activeEdge == RectEdge.BottomLeft)
                        {
                            float oldX = _windowRect.x;
                            _windowRect.x = Mathf.Min(_windowRect.xMax - minW, _windowRect.x + delta.x);
                            _windowRect.width += oldX - _windowRect.x;
                        }

                        if (_activeEdge == RectEdge.Top || _activeEdge == RectEdge.TopLeft || _activeEdge == RectEdge.TopRight)
                        {
                            float oldY = _windowRect.y;
                            _windowRect.y = Mathf.Min(_windowRect.yMax - minH, _windowRect.y + delta.y);
                            _windowRect.height += oldY - _windowRect.y;
                        }
                        Event.current.Use();
                    }
                }

                if (_firstWindowInit)
                {
                    float x = UI_PosX.Value;
                    float y = UI_PosY.Value;
                    float w = Mathf.Max(227f, UI_Width.Value);
                    float h = Mathf.Max(330f, UI_Height.Value);

                    if (x < 0) x = Screen.width - w - 20;
                    if (y < 0) y = Screen.height - h - 50;

                    _windowRect = new Rect(x, y, w, h);
                    _firstWindowInit = false;
                }

                Vector2 mousePos = Event.current.mousePosition;
                float thickness = 8f;

                if (Event.current.type == EventType.MouseDown && _showMenu)
                {
                    bool withinVertical = mousePos.y >= _windowRect.y - thickness && mousePos.y <= _windowRect.yMax + thickness;
                    bool withinHorizontal = mousePos.x >= _windowRect.x - thickness && mousePos.x <= _windowRect.xMax + thickness;

                    bool closeLeft = Mathf.Abs(mousePos.x - _windowRect.x) < thickness && withinVertical;
                    bool closeRight = Mathf.Abs(mousePos.x - _windowRect.xMax) < thickness && withinVertical;
                    bool closeTop = Mathf.Abs(mousePos.y - _windowRect.y) < thickness && withinHorizontal;
                    bool closeBottom = Mathf.Abs(mousePos.y - _windowRect.yMax) < thickness && withinHorizontal;

                    if (closeLeft && closeTop) _activeEdge = RectEdge.TopLeft;
                    else if (closeRight && closeTop) _activeEdge = RectEdge.TopRight;
                    else if (closeLeft && closeBottom) _activeEdge = RectEdge.BottomLeft;
                    else if (closeRight && closeBottom) _activeEdge = RectEdge.BottomRight;
                    else if (closeLeft) _activeEdge = RectEdge.Left;
                    else if (closeRight) _activeEdge = RectEdge.Right;
                    else if (closeTop) _activeEdge = RectEdge.Top;
                    else if (closeBottom) _activeEdge = RectEdge.Bottom;

                    if (_activeEdge != RectEdge.None)
                    {
                        _isResizing = true;
                        Event.current.Use();
                    }
                }

                if (Event.current.type == EventType.MouseUp && _showMenu)
                {
                    if (_windowRect.x != UI_PosX.Value || _windowRect.y != UI_PosY.Value ||
                        _windowRect.width != UI_Width.Value || _windowRect.height != UI_Height.Value)
                    {
                        UI_PosX.Value = _windowRect.x;
                        UI_PosY.Value = _windowRect.y;
                        UI_Width.Value = _windowRect.width;
                        UI_Height.Value = _windowRect.height;
                    }
                    _isResizing = false;
                    _activeEdge = RectEdge.None;
                }

                _windowRect.width = Mathf.Min(_windowRect.width, Screen.width);
                _windowRect.height = Mathf.Min(_windowRect.height, Screen.height);

                GUI.depth = 0;
                _windowRect = GUI.Window(999, _windowRect, DrawAPWindow, "Autopilot controls", _styleWindow);

                float clampedX = Mathf.Clamp(_windowRect.x, 0, Screen.width - _windowRect.width);
                float clampedY = Mathf.Clamp(_windowRect.y, 0, Screen.height - _windowRect.height);

                if (clampedX != _windowRect.x || clampedY != _windowRect.y)
                {
                    _windowRect.x = clampedX;
                    _windowRect.y = clampedY;
                }

                GUI.depth = -1;
                DrawCustomTooltip();
            }
            finally
            {
                GUI.color = oldGuiColor;
            }
        }

        // gui
        private void DrawAPWindow(int windowID)
        {
            GUI.DragWindow(new Rect(0, 0, 10000, 25));

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, false, false, GUIStyle.none, GUIStyle.none, GUILayout.Height(_windowRect.height - 30));

            GUILayout.BeginVertical();

            float currentVS = (APData.PlayerRB != null) ? APData.PlayerRB.velocity.y : 0f;

            float currentAlt = (APData.LocalAircraft != null) ? APData.LocalAircraft.GlobalPosition().y : 0f;
            float sos = LevelInfo.GetSpeedOfSound(currentAlt);
            float currentSpeed = (APData.PlayerRB != null) ? APData.PlayerRB.velocity.magnitude : 0f;

            float currentCourse = 0f;
            if (APData.PlayerRB != null && APData.PlayerRB.velocity.sqrMagnitude > 1f)
            {
                Vector3 flatVel = Vector3.ProjectOnPlane(APData.PlayerRB.velocity, Vector3.up);
                currentCourse = Quaternion.LookRotation(flatVel).eulerAngles.y;
            }

            string sAlt = ModUtils.ProcessGameString(UnitConverter.AltitudeReading(APData.CurrentAlt), true);
            string sVS = ModUtils.ProcessGameString(UnitConverter.ClimbRateReading(currentVS), true);
            string sRoll = $"{APData.CurrentRoll:F0}°";

            string sSpd;
            if (APData.SpeedHoldIsMach)
            {
                float currentMach = currentSpeed / Mathf.Max(sos, 1f);
                sSpd = $"M{currentMach:F2}";
            }
            else
            {
                sSpd = ModUtils.ProcessGameString(UnitConverter.SpeedReading(currentSpeed), true);
            }

            string sCrs = $"{currentCourse:F0}°";

            _measuringContent.text = sAlt;
            float wAlt = _styleLabel.CalcSize(_measuringContent).x;
            _measuringContent.text = sVS;
            float wVS = _styleLabel.CalcSize(_measuringContent).x;
            _measuringContent.text = sRoll;
            float wRoll = _styleLabel.CalcSize(_measuringContent).x;
            _measuringContent.text = sSpd;
            float wSpd = _styleLabel.CalcSize(_measuringContent).x;
            _measuringContent.text = sCrs;
            float wCrs = _styleLabel.CalcSize(_measuringContent).x;

            float targetWidth = Mathf.Max(wAlt, wVS, wRoll, wSpd, wCrs) + 6;
            _dynamicLabelWidth = Mathf.Lerp(_dynamicLabelWidth, targetWidth, 0.15f);

            // altitude
            GUILayout.BeginHorizontal();
            GUI.color = (APData.Enabled && APData.TargetAlt > 0) ? Color.green : Color.white;
            if (GUILayout.Button(new GUIContent($"{sAlt}", "Current altitude\nGreen if alt AP on\nClick to copy"), _styleReadout, GUILayout.Width(_dynamicLabelWidth)))
            {
                _bufAlt = ModUtils.ConvertAlt_ToDisplay(APData.CurrentAlt).ToString("F0");
            }
            GUI.color = Color.white;
            _bufAlt = GUILayout.TextField(_bufAlt);
            GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Target altitude"));

            if (GUILayout.Button(new GUIContent("CLR", "disable alt hold"), _styleButton, GUILayout.Width(buttonWidth)))
            {
                APData.TargetAlt = -1f; _bufAlt = "";
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            // vertical speed
            GUILayout.BeginHorizontal();
            bool isDefaultVS = Mathf.Abs(APData.CurrentMaxClimbRate - DefaultMaxClimbRate.Value) < 0.1f;
            GUI.color = isDefaultVS ? Color.white : Color.cyan;
            if (GUILayout.Button(new GUIContent($"{sVS}", "Current climb/descent rate\nCyan if not default\nClick to copy"), _styleReadout, GUILayout.Width(_dynamicLabelWidth)))
            {
                _bufClimb = ModUtils.ConvertVS_ToDisplay(Mathf.Abs(currentVS)).ToString("F0");
            }
            GUI.color = Color.white;
            _bufClimb = GUILayout.TextField(_bufClimb);
            GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Max vertical speed"));

            if (GUILayout.Button(new GUIContent("RST", "Reset to default"), _styleButton, GUILayout.Width(buttonWidth)))
            {
                APData.CurrentMaxClimbRate = DefaultMaxClimbRate.Value;
                _bufClimb = ModUtils.ConvertVS_ToDisplay(APData.CurrentMaxClimbRate).ToString("F0");
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            // bank angle
            GUILayout.BeginHorizontal();
            if (APData.NavEnabled && APData.Enabled) GUI.color = Color.cyan;
            else if (APData.Enabled && APData.TargetRoll != -999f) GUI.color = Color.green;
            else GUI.color = Color.white;
            if (GUILayout.Button(new GUIContent($"{sRoll}", "Current bank angle\nCyan if Nav mode on\nGreen if roll AP on\nClick to copy"), _styleReadout, GUILayout.Width(_dynamicLabelWidth)))
            {
                _bufRoll = (APData.NavEnabled || APData.TargetCourse >= 0)
                    ? Mathf.Abs(APData.CurrentRoll).ToString("F0")
                    : APData.CurrentRoll.ToString("F0");
            }
            GUI.color = Color.white;
            _bufRoll = GUILayout.TextField(_bufRoll);
            GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Target/limit bank angle"));

            if (GUILayout.Button(new GUIContent("CLR", "disable roll hold"), _styleButton, GUILayout.Width(buttonWidth)))
            {
                APData.TargetRoll = -999f; _bufRoll = "";
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            // speed
            GUILayout.BeginHorizontal();
            GUI.color = (APData.TargetSpeed >= 0) ? Color.green : Color.white;
            if (GUILayout.Button(new GUIContent($"{sSpd}", "Current speed\nGreen if autothrottle on\nClick to copy"), _styleReadout, GUILayout.Width(_dynamicLabelWidth)))
            {
                if (APData.SpeedHoldIsMach)
                {
                    float currentMach = currentSpeed / Mathf.Max(sos, 1f);
                    _bufSpeed = currentMach.ToString("F2");
                }
                else
                {
                    _bufSpeed = ModUtils.ConvertSpeed_ToDisplay(currentSpeed).ToString("F0");
                }
            }
            GUI.color = Color.white;
            _bufSpeed = GUILayout.TextField(_bufSpeed);
            GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Target speed"));

            // mach hold button
            string machText = APData.SpeedHoldIsMach ? "M" : "Spd";
            if (GUILayout.Button(new GUIContent(machText, "Mach Hold / TAS Hold"), _styleButton, GUILayout.Width(buttonWidth)))
            {
                if (float.TryParse(_bufSpeed, out float val))
                {
                    if (!APData.SpeedHoldIsMach)
                    {
                        float ms = ModUtils.ConvertSpeed_FromDisplay(val);
                        float mach = Mathf.Max(0, ms / sos);
                        _bufSpeed = mach.ToString("F2");
                        if (APData.TargetSpeed >= 0) APData.TargetSpeed = mach;
                    }
                    else
                    {
                        float ms = Mathf.Max(val * sos);
                        float display = ModUtils.ConvertSpeed_ToDisplay(ms);
                        _bufSpeed = display.ToString("F0");
                        if (APData.TargetSpeed >= 0) APData.TargetSpeed = ms;
                    }
                }
                APData.SpeedHoldIsMach = !APData.SpeedHoldIsMach;
                GUI.FocusControl(null);
            }
            Color oldCol = GUI.backgroundColor;
            if (APData.AllowExtremeThrottle) GUI.backgroundColor = Color.red;

            string limitText = APData.AllowExtremeThrottle ? "AB1" : "AB0";
            if (GUILayout.Button(new GUIContent(limitText, "Toggle afterburner/airbrake"), _styleButton, GUILayout.Width(buttonWidth)))
            {
                APData.AllowExtremeThrottle = !APData.AllowExtremeThrottle;
                GUI.FocusControl(null);
            }
            GUI.backgroundColor = oldCol;
            GUILayout.EndHorizontal();

            // course
            GUILayout.BeginHorizontal();
            if (APData.NavEnabled && APData.Enabled) GUI.color = Color.cyan;
            else if (APData.Enabled && APData.TargetCourse >= 0) GUI.color = Color.green;
            else GUI.color = Color.white;
            if (GUILayout.Button(new GUIContent($"{sCrs}", "Current course\nCyan if Nav mode on\nGreen if Course AP on\nClick to copy"), _styleReadout, GUILayout.Width(_dynamicLabelWidth)))
            {
                _bufCourse = currentCourse.ToString("F0");
            }
            GUI.color = Color.white;
            if (APData.NavEnabled && APData.TargetCourse >= 0)
            {
                _bufCourse = APData.TargetCourse.ToString("F0");
            }
            _bufCourse = GUILayout.TextField(_bufCourse);
            GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Target course"));
            if (GUILayout.Button(new GUIContent("CLR", "Disable course hold/nav"), _styleButton, GUILayout.Width(buttonWidth)))
            {
                APData.TargetCourse = -1f;
                _bufCourse = "";
                APData.NavEnabled = false;
                APData.TargetRoll = 0;
                _bufRoll = "0";
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            // set values
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Set Values", "Applies typed values"), _styleButton))
            {
                if (float.TryParse(_bufAlt, out float a))
                    APData.TargetAlt = ModUtils.ConvertAlt_FromDisplay(a);
                else
                    APData.TargetAlt = -1f;

                if (float.TryParse(_bufClimb, out float c))
                    APData.CurrentMaxClimbRate = Mathf.Max(0.5f, ModUtils.ConvertVS_FromDisplay(c));

                if (float.TryParse(_bufSpeed, out float s))
                {
                    APData.TargetSpeed = APData.SpeedHoldIsMach ? s : ModUtils.ConvertSpeed_FromDisplay(s);
                }
                else
                    APData.TargetSpeed = -1f;

                if (float.TryParse(_bufCourse, out float crs))
                    APData.TargetCourse = crs;
                else
                    APData.TargetCourse = -1f;

                if (float.TryParse(_bufRoll, out float r))
                {
                    if ((APData.NavEnabled || APData.TargetCourse >= 0) && APData.TargetRoll == 0)
                        APData.TargetRoll = DefaultCRLimit.Value;
                    else
                        APData.TargetRoll = r;
                }
                else if (APData.TargetCourse >= 0f || APData.NavEnabled)
                {
                    APData.TargetRoll = DefaultCRLimit.Value;
                    _bufRoll = APData.TargetRoll.ToString("F0");
                }
                else
                {
                    APData.TargetRoll = -999f;
                    _bufRoll = "";
                }

                APData.Enabled = true;
                APData.UseSetValues = true;
                SyncMenuValues();
                GUI.FocusControl(null);
            }

            // engage/disengage
            GUI.backgroundColor = APData.Enabled ? Color.green : Color.red;
            if (GUILayout.Button(new GUIContent(APData.Enabled ? "Disengage" : "Engage", "toggle AP"), _styleButton))
            {
                APData.Enabled = !APData.Enabled;
                if (APData.Enabled)
                {
                    if (string.IsNullOrEmpty(_bufAlt))
                    {
                        APData.TargetAlt = APData.CurrentAlt;
                        _bufAlt = ModUtils.ConvertAlt_ToDisplay(APData.TargetAlt).ToString("F0");
                    }

                    if (string.IsNullOrEmpty(_bufCourse) && string.IsNullOrEmpty(_bufRoll))
                    {
                        if (APData.NavEnabled || APData.TargetCourse >= 0)
                        {
                            APData.TargetRoll = DefaultCRLimit.Value;
                            _bufRoll = APData.TargetRoll.ToString("F0");
                        }
                        else
                        {
                            APData.TargetRoll = 0f;
                            _bufRoll = "0";
                        }
                    }
                    SyncMenuValues();
                    APData.UseSetValues = true;
                }
                else if (DisableATAPGUI.Value)
                {
                    APData.TargetSpeed = -1f;
                    SyncMenuValues();
                }
                GUI.FocusControl(null);
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            // auto jam/gcas
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = APData.AutoJammerActive ? Color.green : Color.white;
            string ajText = "AJ: " + (APData.AutoJammerActive ? "ON" : "OFF");
            if (GUILayout.Button(new GUIContent(ajText, "Toggle Auto Jammer"), _styleButton))
            {
                APData.AutoJammerActive = !APData.AutoJammerActive;
                GUI.FocusControl(null);
            }
            GUI.backgroundColor = APData.GCASEnabled ? Color.green : Color.white;
            string gcasText = "GCAS: " + (APData.GCASEnabled ? "ON" : "OFF");
            if (GUILayout.Button(new GUIContent(gcasText, "Toggle Auto-GCAS"), _styleButton))
            {
                APData.GCASEnabled = !APData.GCASEnabled;
                if (!APData.GCASEnabled) APData.GCASActive = false;
                GUI.FocusControl(null);
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            bool newSaveState = GUILayout.Toggle(APData.SaveMapState, new GUIContent("Lock", "Keep map zoom/pos when reopening."));
            if (newSaveState != APData.SaveMapState)
            {
                APData.SaveMapState = newSaveState;
                SaveMapState.Value = newSaveState;
            }

            if (GUILayout.Button(new GUIContent("Center", "Pan to the center of the map"), _styleButton))
            {
                var map = SceneSingleton<DynamicMap>.i;
                if (map != null && f_mapPosOffset != null && f_mapStatOffset != null)
                {
                    Vector2 stationary = (Vector2)f_mapStatOffset.GetValue(map);
                    f_mapPosOffset.SetValue(map, -stationary);
                    f_mapFollow?.SetValue(map, false);
                }
                GUI.FocusControl(null);
            }

            if (GUILayout.Button(new GUIContent("Aircraft", "Pan map to your aircraft"), _styleButton))
            {
                var map = SceneSingleton<DynamicMap>.i;
                if (map != null && f_mapPosOffset != null)
                {
                    f_mapPosOffset.SetValue(map, Vector2.zero);
                    map.CenterMap();
                }
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            // nav
            GUILayout.BeginHorizontal();
            bool newNavState = GUILayout.Toggle(APData.NavEnabled, new GUIContent("Nav mode", "switch for waypoint ap mode."));
            if (newNavState != APData.NavEnabled)
            {
                APData.NavEnabled = newNavState;
                if (APData.NavEnabled && (APData.TargetRoll == -999f || APData.TargetRoll == 0f))
                {
                    APData.TargetRoll = DefaultCRLimit.Value;
                }
                SyncMenuValues();
            }
            NavCycle.Value = GUILayout.Toggle(NavCycle.Value, new GUIContent("Cycle wp", "On: cycles to next wp upon reaching wp, Off: Deletes upon reaching wp"));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            // Nav waypoint list
            if (APData.NavQueue.Count > 0)
            {
                Vector3 playerPos = (APData.PlayerRB != null) ? APData.PlayerRB.position.ToGlobalPosition().AsVector3() : Vector3.zero;
                float distNext = Vector2.Distance(new Vector2(playerPos.x, playerPos.z), new Vector2(APData.NavQueue[0].x, APData.NavQueue[0].z));

                // next wp row
                string nextDistStr = ModUtils.ProcessGameString(UnitConverter.DistanceReading(distNext), Plugin.DistShowUnit.Value);
                GUILayout.Label(new GUIContent($"Next: {nextDistStr}", "Distance to next wp"), _styleLabel);

                if (APData.SpeedEma < 0.0001f) APData.SpeedEma = 0.0001f;

                float etaNext = distNext / APData.SpeedEma;
                string sEtaNext = (etaNext > 3599) ? TimeSpan.FromSeconds(etaNext).ToString(@"h\:mm\:ss") : TimeSpan.FromSeconds(etaNext).ToString(@"mm\:ss");
                GUILayout.Label($" ETA: {sEtaNext}", _styleLabel);

                GUILayout.EndHorizontal();

                // total row
                GUILayout.BeginHorizontal();
                if (APData.NavQueue.Count > 1)
                {
                    float distTotal = distNext;
                    for (int i = 0; i < APData.NavQueue.Count - 1; i++)
                    {
                        distTotal += Vector3.Distance(APData.NavQueue[i], APData.NavQueue[i + 1]);
                    }

                    string totalDistStr = ModUtils.ProcessGameString(UnitConverter.DistanceReading(distTotal), DistShowUnit.Value);
                    GUILayout.Label(new GUIContent($"Total: {totalDistStr}", "Total distance of flight plan"), _styleLabel);

                    float etaTotal = distTotal / APData.SpeedEma;
                    string sEtaTotal = (etaTotal > 3599) ? TimeSpan.FromSeconds(etaTotal).ToString(@"h\:mm\:ss") : TimeSpan.FromSeconds(etaTotal).ToString(@"mm\:ss");
                    GUILayout.Label($" ETA: {sEtaTotal}", _styleLabel);


                }
                GUILayout.EndHorizontal();
                // nav control row
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("Skip wp", "delete next point"), _styleButton))
                {
                    APData.NavQueue.RemoveAt(0);
                    if (APData.NavQueue.Count == 0) APData.NavEnabled = false;
                    RefreshNavVisuals();
                }
                if (GUILayout.Button(new GUIContent("Undo wp", "delete last point"), _styleButton))
                {
                    if (APData.NavQueue.Count > 0)
                    {
                        APData.NavQueue.RemoveAt(APData.NavQueue.Count - 1);
                        if (APData.NavQueue.Count == 0) APData.NavEnabled = false;
                        RefreshNavVisuals();
                    }
                }
                if (GUILayout.Button(new GUIContent("Clear all", "delete all points. (much self explanatory)"), _styleButton))
                {
                    APData.NavQueue.Clear();
                    APData.NavEnabled = false;
                    RefreshNavVisuals();
                }
                GUILayout.EndHorizontal();
                GUILayout.Label(_cachedTableContent, _styleLabel);
            }
            else
            {
                // display massive tooltips??
                GUILayout.EndHorizontal();
                GUILayout.Label(new GUIContent("RMB the map to set wp.\nShift+RMB for multiple.", "Here, RMB means Right Mouse Button click.\nShift + RMB means Shift key + Right Mouse Button.\nThis will only work on the map screen.\nIf nothing is happening after you drew a hundred lines on screen,\nthen you may have just forgotten to engage the autopilot with the equals key/set values button/engage button\n(tbh the original text was probably self explanatory)\n\nAlso if you see the last waypoint hovering around, just ignore it for now, afaik it's only a cosmetic defect.\n\nOh also, the tooltip logic is inspired by Firefox.\nIf you hover over something for some time on gui, it will show tooltip.\nIf you then your mouse away from the position you held your mouse in,\nthe tooltip will disappear and won't reappear until your mouse leaves the item."), _styleLabel);

                GUILayout.Label(_cachedExtraInfoContent, _styleLabel);
            }

            GUILayout.EndVertical();

            GUILayout.EndScrollView();

            if (Event.current.type == EventType.Repaint && !string.IsNullOrEmpty(GUI.tooltip))
            {
                _lastActiveTooltip = GUI.tooltip;
            }
        }

        private void DrawCustomTooltip()
        {
            if (Event.current.type != EventType.Repaint) return;

            string tooltipUnderMouse = _lastActiveTooltip;

            if (!_windowRect.Contains(Event.current.mousePosition))
            {
                tooltipUnderMouse = "";
            }

            Vector2 mousePos = Event.current.mousePosition;
            float now = Time.realtimeSinceStartup;

            if (tooltipUnderMouse != _currentHoverTarget)
            {
                _currentHoverTarget = tooltipUnderMouse;
                _stationaryPos = mousePos;
                _stationaryTimer = now;
                _isTooltipVisible = false;
                _wasShownForThisTarget = false;
            }

            float distFromStart = Vector2.Distance(mousePos, _stationaryPos);
            if (distFromStart > _jitterThreshold)
            {
                if (!_isTooltipVisible)
                {
                    _stationaryPos = mousePos;
                    _stationaryTimer = now;
                }
                else
                {
                    _isTooltipVisible = false;
                    _wasShownForThisTarget = true;
                }
            }

            if (!string.IsNullOrEmpty(_currentHoverTarget) && !_wasShownForThisTarget && !_isTooltipVisible)
            {
                if (now - _stationaryTimer >= 0.4f)
                {
                    _isTooltipVisible = true;
                }
            }

            if (_isTooltipVisible && !string.IsNullOrEmpty(_currentHoverTarget))
            {
                GUIContent content = new(_currentHoverTarget);
                GUIStyle style = GUI.skin.box;
                Vector2 size = style.CalcSize(content);

                Rect tooltipRect = new(_stationaryPos.x + 12, _stationaryPos.y + 12, size.x, size.y);

                if (tooltipRect.xMax > Screen.width) tooltipRect.x = _stationaryPos.x - size.x - 5;
                if (tooltipRect.yMax > Screen.height) tooltipRect.y = _stationaryPos.y - size.y - 5;

                GUI.Box(tooltipRect, content, style);
            }
            if (Event.current.type == EventType.Repaint) _lastActiveTooltip = "";
        }

        public static void RefreshNavVisuals()
        {
            foreach (var obj in APData.NavVisuals) if (obj != null) Destroy(obj);
            APData.NavVisuals.Clear();

            var map = SceneSingleton<DynamicMap>.i;
            if (APData.NavQueue.Count == 0 || map == null || APData.PlayerRB == null) return;

            float factor = 900f / map.mapDimension;
            float zoom = map.mapImage.transform.localScale.x;
            Color navCol = ModUtils.GetColor(ColorNav.Value, Color.cyan);

            Vector3 pPosG = APData.PlayerRB.position.ToGlobalPosition().AsVector3();
            Vector3 lastPoint = new(pPosG.x * factor, pPosG.z * factor, 0f);

            void DrawLine(Vector3 start, Vector3 end, string name, bool isLoop = false)
            {
                GameObject line = Instantiate(map.mapWaypointVector, map.mapImage.transform);
                line.name = name;

                line.transform.localPosition = end;

                float angle = -Mathf.Atan2(end.x - start.x, end.y - start.y) * Mathf.Rad2Deg + 180f;

                line.transform.localEulerAngles = new Vector3(0, 0, angle);

                line.transform.localScale = new Vector3(4f / zoom, Vector3.Distance(start, end), 4f / zoom);

                if (line.TryGetComponent(out Image img))
                {
                    img.color = isLoop ? new Color(navCol.r, navCol.g, navCol.b, navCol.a * 0.4f) : navCol;
                }
                APData.NavVisuals.Add(line);
            }

            for (int i = 0; i < APData.NavQueue.Count; i++)
            {
                Vector3 currentMap = new(APData.NavQueue[i].x * factor, APData.NavQueue[i].z * factor, 0f);

                if (i == APData.NavQueue.Count - 1)
                {
                    GameObject marker = Instantiate(map.mapWaypoint, map.mapImage.transform);
                    marker.name = "AP_NavMarker";
                    marker.transform.localPosition = currentMap;
                    marker.transform.localScale = Vector3.one * (1f / zoom);
                    if (marker.TryGetComponent(out Image mImg)) mImg.color = navCol;
                    APData.NavVisuals.Add(marker);
                }

                DrawLine(lastPoint, currentMap, (i == 0) ? "AP_NavLine_Player" : "AP_NavLine");
                lastPoint = currentMap;
            }

            if (NavCycle.Value && APData.NavQueue.Count > 1)
            {
                Vector3 firstMap = new(APData.NavQueue[0].x * factor, APData.NavQueue[0].z * factor, 0f);
                DrawLine(lastPoint, firstMap, "AP_NavLine_Loop", true);
            }
        }

        public static void CleanUpFBW()
        {
            if (APData.FBWDisabled)
            {
                APData.FBWDisabled = false;
                UpdateFBWState();
            }
        }

        public static void UpdateFBWState()
        {
            if (APData.LocalAircraft == null) return;

            bool shouldDisable = APData.FBWDisabled;
            if (APData.IsMultiplayerCached)
            {
                shouldDisable = false;
                APData.FBWDisabled = false;
            }

            try
            {
                object filterObj = f_controlsFilter.GetValue(APData.LocalAircraft);
                if (filterObj == null) return;
                object tupleResult = m_GetFBWParams.Invoke(filterObj, null);
                float[] currentTuning = (float[])f_fbw_item2_tuning.GetValue(tupleResult);
                m_SetFBWParams.Invoke(filterObj, [!shouldDisable, currentTuning]);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[UpdateFBWState] Error: {ex.Message}");
                APData.FBWDisabled = false;
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (!string.Equals(scene.name, "GameWorld", StringComparison.OrdinalIgnoreCase))
            {
                APData.Enabled = false;
                APData.GCASActive = false;
                APData.LocalAircraft = null;
                APData.PlayerRB = null;
                APData.PlayerTransform = null;
                APData.LocalPilot = null;
                APData.LocalWeaponManager = null;

                APData.NavQueue.Clear();
                foreach (var obj in APData.NavVisuals) if (obj != null) Destroy(obj);
                APData.NavVisuals.Clear();
                CleanUpFBW();
            }
        }

        public static bool IsMultiplayer()
        {
            if (Time.time < APData.NextMultiplayerCheck) return APData.IsMultiplayerCached;
            APData.NextMultiplayerCheck = Time.time + 2.0f;

            try
            {
                if (t_NetworkServer != null)
                {
                    UnityEngine.Object serverInstance = FindObjectOfType(t_NetworkServer);
                    if (serverInstance != null)
                    {
                        bool isServerActive = (bool)p_serverActive.GetValue(serverInstance);
                        if (isServerActive)
                        {
                            if (p_serverAllPlayers.GetValue(serverInstance) is IReadOnlyCollection<object> connections && connections.Count > 1)
                            {
                                APData.IsMultiplayerCached = true;
                                return true;
                            }
                        }
                    }
                }

                if (t_NetworkClient != null)
                {
                    UnityEngine.Object clientInstance = FindObjectOfType(t_NetworkClient);
                    if (clientInstance != null)
                    {
                        bool isClientActive = (bool)p_clientActive.GetValue(clientInstance);
                        bool isHost = (bool)p_clientIsHost.GetValue(clientInstance);

                        if (isClientActive && !isHost)
                        {
                            APData.IsMultiplayerCached = true;
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[IsMultiplayer] Error: {ex}");
            }

            APData.IsMultiplayerCached = false;
            return false;
        }

        private void ClearAllStatics()
        {
            var assembly = Assembly.GetExecutingAssembly();

            foreach (var type in assembly.GetTypes())
            {
                var fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                foreach (var field in fields)
                {
                    if (field.IsLiteral || field.IsInitOnly) continue;

                    try
                    {
                        object defaultValue = field.FieldType.IsValueType ? Activator.CreateInstance(field.FieldType) : null;
                        field.SetValue(null, defaultValue);
                    }
                    catch { }
                }
            }
        }
    }

    public static class ModUtils
    {
        private static readonly Regex _rxSpaces = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex _rxDecimals = new(@"[\.]\d+", RegexOptions.Compiled);
        private static readonly Regex _rxNumber = new(@"-?\d+", RegexOptions.Compiled);

        public static Color GetColor(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            if (!hex.StartsWith("#")) hex = "#" + hex;
            if (ColorUtility.TryParseHtmlString(hex, out Color c)) return c;
            return fallback;
        }

        public static float ConvertAlt_ToDisplay(float meters)
        {
            if (PlayerSettings.unitSystem == PlayerSettings.UnitSystem.Imperial)
                return meters * 3.28084f;
            return meters;
        }

        public static float ConvertAlt_FromDisplay(float displayVal)
        {
            if (PlayerSettings.unitSystem == PlayerSettings.UnitSystem.Imperial)
                return displayVal / 3.28084f;
            return displayVal;
        }

        public static float ConvertVS_ToDisplay(float metersPerSec)
        {
            if (PlayerSettings.unitSystem == PlayerSettings.UnitSystem.Imperial)
                return metersPerSec * 196.850394f;
            return metersPerSec;
        }

        public static float ConvertVS_FromDisplay(float displayVal)
        {
            if (PlayerSettings.unitSystem == PlayerSettings.UnitSystem.Imperial)
                return displayVal / 196.850394f;
            return displayVal;
        }

        public static float ConvertSpeed_ToDisplay(float ms)
        {
            return (PlayerSettings.unitSystem == PlayerSettings.UnitSystem.Imperial) ? ms * 1.94384f : ms * 3.6f;
        }
        public static float ConvertSpeed_FromDisplay(float val)
        {
            return (PlayerSettings.unitSystem == PlayerSettings.UnitSystem.Imperial) ? val / 1.94384f : val / 3.6f;
        }

        public static string ProcessGameString(string raw, bool keepUnit)
        {
            if (string.IsNullOrEmpty(raw)) return "";

            // remove spaces
            string clean = _rxSpaces.Replace(raw, "");

            clean = clean.Replace("+", "");

            // remove decimals
            clean = _rxDecimals.Replace(clean, "");

            if (keepUnit) return clean;

            // remove units
            var match = _rxNumber.Match(clean);
            return match.Success ? match.Value : clean;
        }

        public static Vector3 ParseGridToPos(string grid)
        {
            try
            {
                grid = grid.Trim();
                float x = 0, z = 0;
                float offX = 80000f; // offsetX from GridLabels class
                float offY = 80000f; // offsetY also from GL class

                if (grid.Length == 2)
                { // 1 letter, 1 number
                    int majY = char.ToUpper(grid[0]) - 'A';
                    int majX = int.Parse(grid[1].ToString());
                    x = (majX * 10000f) + 5000f - offX;
                    z = offY - ((majY * 10000f) + 5000f);
                }
                else if (grid.Length == 4)
                { // 2 letter, 2 num
                    int majY = char.ToUpper(grid[0]) - 'A';
                    int minY = char.ToLower(grid[1]) - 'a';
                    int majX = int.Parse(grid[2].ToString());
                    int minX = int.Parse(grid[3].ToString());
                    x = (majX * 10000f) + (minX * 1000f) + 500f - offX;
                    z = offY - ((majY * 10000f) + (minY * 1000f) + 500f);
                }
                return new Vector3(x, 0, z);
            }
            catch { return Vector3.zero; }
        }
    }

    public static class APData
    {
        public static bool Enabled = false;
        public static bool UseSetValues = false;
        public static bool GCASEnabled = true;
        public static bool AutoJammerActive = false;
        public static bool GCASActive = false;
        public static bool GCASWarning = false;
        public static bool AllowExtremeThrottle = false;
        public static bool SpeedHoldIsMach = false;
        public static bool NavEnabled = false;
        public static bool FBWDisabled = false;
        public static float TargetAlt = -1f;
        public static float TargetRoll = -999f;
        public static float TargetSpeed = -1f;
        public static float TargetCourse = -1f;
        public static float CurrentAlt = 0f;
        public static float CurrentRoll = 0f;
        public static float CurrentMaxClimbRate = -1f;
        public static float SpeedEma = 0f;
        public static float LastOverrideInputTime = -999f;
        public static object LocalPilot;
        public static List<Vector3> NavQueue = [];
        public static List<GameObject> NavVisuals = [];
        public static Transform PlayerTransform;
        public static Rigidbody PlayerRB;
        public static Aircraft LocalAircraft;
        public static WeaponManager LocalWeaponManager;
        public static float GCASConverge = 0f;
        public static bool IsMultiplayerCached = false;
        public static float NextMultiplayerCheck = 0f;
        public static bool SaveMapState = false;
        public static Vector2 SavedMapPos = Vector2.zero;
        public static float SavedMapZoom = 1f;
        public static bool SavedMapFollow = true;
        public static bool MapStateStored = false;
        public static float BloodPressure = 1f;
        public static bool IsConscious = true;

        public static void Reset()
        {
            Enabled = false;
            UseSetValues = false;
            GCASEnabled = true;
            AutoJammerActive = false;
            GCASActive = false;
            GCASWarning = false;
            AllowExtremeThrottle = false;
            SpeedHoldIsMach = false;
            NavEnabled = false;
            FBWDisabled = false;
            TargetAlt = -1f;
            TargetRoll = -999f;
            TargetSpeed = -1f;
            TargetCourse = -1f;
            CurrentAlt = 0f;
            CurrentRoll = 0f;
            CurrentMaxClimbRate = -1f;
            SpeedEma = 0f;
            LastOverrideInputTime = -999f;
            LocalPilot = null;
            PlayerTransform = null;
            PlayerRB = null;
            LocalAircraft = null;
            LocalWeaponManager = null;
            GCASConverge = 0f;
            IsMultiplayerCached = false;
            NextMultiplayerCheck = 0f;
            MapStateStored = false;
            SavedMapPos = Vector2.zero;
            SavedMapZoom = 1f;
            SavedMapFollow = true;
            BloodPressure = 1f;
            IsConscious = true;

            NavQueue.Clear();
            foreach (var obj in NavVisuals)
            {
                if (obj != null) UnityEngine.Object.Destroy(obj);
            }
            NavVisuals.Clear();
        }
    }

    [HarmonyPatch(typeof(FlightHud), "SetHUDInfo")]
    internal class HudPatch
    {
        private static GameObject lastVehicleObj;

        public static void Reset()
        {
            lastVehicleObj = null;
        }

        private static void Postfix(object playerVehicle, float altitude)
        {
            try
            {
                APData.CurrentAlt = altitude;
                if (playerVehicle == null || playerVehicle is not Component v) return;

                if (v.gameObject != lastVehicleObj)
                {
                    lastVehicleObj = v.gameObject;

                    APData.PlayerTransform = v.transform;
                    APData.PlayerRB = v.GetComponent<Rigidbody>();
                    APData.LocalAircraft = v.GetComponent<Aircraft>();

                    APData.Enabled = false;
                    APData.UseSetValues = false;
                    APData.GCASEnabled = Plugin.EnableGCAS.Value;
                    APData.AutoJammerActive = false;
                    APData.GCASActive = false;
                    APData.GCASWarning = false;
                    APData.FBWDisabled = false;
                    APData.TargetAlt = altitude;
                    APData.TargetRoll = 0f;
                    APData.TargetSpeed = -1f;
                    APData.TargetCourse = -1f;
                    APData.CurrentMaxClimbRate = -1f;
                    APData.LastOverrideInputTime = -999f;
                    APData.GCASConverge = 0f;
                    APData.LocalPilot = null;
                    APData.LocalWeaponManager = null;
                    if (APData.LocalAircraft != null)
                    {
                        if (Plugin.f_weaponManager != null)
                            APData.LocalWeaponManager = Plugin.f_weaponManager.GetValue(APData.LocalAircraft) as WeaponManager;

                        if (Plugin.f_pilots != null)
                        {
                            IList pilots = (IList)Plugin.f_pilots.GetValue(APData.LocalAircraft);
                            if (pilots != null && pilots.Count > 0) APData.LocalPilot = pilots[0];
                        }
                    }
                    APData.NavEnabled = false;
                    APData.NavQueue.Clear();
                    foreach (var obj in APData.NavVisuals) if (obj != null) UnityEngine.Object.Destroy(obj);
                    APData.NavVisuals.Clear();
                    Plugin.SyncMenuValues();
                    Plugin.CleanUpFBW();
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[HudPatch] Error: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(PilotPlayerState), "PlayerAxisControls")]
    internal class ControlOverridePatch
    {
        private static readonly PIDController pidAlt = new();
        private static readonly PIDController pidVS = new();
        private static readonly PIDController pidAngle = new();
        private static readonly PIDController pidRoll = new();
        private static readonly PIDController pidGCAS = new();
        private static readonly PIDController pidSpd = new();
        private static readonly PIDController pidCrs = new();

        private static float lastSpdMeasurement = 0f;

        private static float lastPitchOut = 0f;
        private static float lastRollOut = 0f;
        private static float lastThrottleOut = 0f;
        private static float lastBankReq = 0f;
        private static float lastVSReq = 0f;
        private static float lastAngleReq = 0f;

        private static bool wasEnabled = false;
        private static float pitchSleepUntil = 0f;
        private static float rollSleepUntil = 0f;
        private static float spdSleepUntil = 0f;
        private static bool isPitchSleeping = false;
        private static bool isRollSleeping = false;
        private static bool isSpdSleeping = false;
        private static float gcasNextScan = 0f;
        private static float overGFactor = 1.0f;
        private static bool dangerImminent = false;
        private static bool warningZone = false;
        public static bool apStateBeforeGCAS = false;
        private static float currentAppliedThrottle = 0f;

        private static float jammerNextFireTime = 0f;
        private static float jammerNextReleaseTime = 0f;
        private static bool isJammerHoldingTrigger = false;

        private static float _disengageTimer = 0f;

        public static void Reset()
        {
            pidAlt.Reset();
            pidVS.Reset();
            pidAngle.Reset();
            pidRoll.Reset();
            pidGCAS.Reset();
            pidSpd.Reset();
            pidCrs.Reset();

            lastSpdMeasurement = 0f;
            lastPitchOut = 0f;
            lastRollOut = 0f;
            lastThrottleOut = 0f;
            lastBankReq = 0f;
            lastVSReq = 0f;
            lastAngleReq = 0f;

            wasEnabled = false;
            pitchSleepUntil = 0f;
            rollSleepUntil = 0f;
            spdSleepUntil = 0f;
            isPitchSleeping = false;
            isRollSleeping = false;
            isSpdSleeping = false;
            gcasNextScan = 0f;
            overGFactor = 1.0f;
            dangerImminent = false;
            warningZone = false;
            apStateBeforeGCAS = false;
            currentAppliedThrottle = 0f;

            jammerNextFireTime = 0f;
            jammerNextReleaseTime = 0f;
            isJammerHoldingTrigger = false;

            _disengageTimer = 0f;
        }

        private static void ResetIntegrators(float inputThrottle)
        {
            pidAlt.Reset();
            pidVS.Reset();
            pidAngle.Reset();
            pidRoll.Reset();
            pidGCAS.Reset();
            pidCrs.Reset();
            if (APData.TargetSpeed < 0)
            {
                pidSpd.Reset(Mathf.Clamp01(inputThrottle));
                currentAppliedThrottle = inputThrottle;
                lastThrottleOut = inputThrottle;
            }

            lastPitchOut = 0f;
            lastRollOut = 0f;
            lastBankReq = 0f;
            lastVSReq = 0f;
            lastAngleReq = 0f;

            isPitchSleeping = isRollSleeping = isSpdSleeping = false;
            pitchSleepUntil = rollSleepUntil = spdSleepUntil = 0f;
        }

        private static void Postfix(PilotPlayerState __instance)
        {
            if (APData.LocalAircraft == null || APData.PlayerRB == null || APData.PlayerTransform == null)
            {
                APData.Enabled = false;
                APData.GCASActive = false;
                return;
            }

            if (Plugin.f_controlInputs == null || Plugin.f_pitch == null || Plugin.f_roll == null || Plugin.f_throttle == null) return;

            try
            {
                if (APData.CurrentMaxClimbRate < 0f) APData.CurrentMaxClimbRate = Plugin.DefaultMaxClimbRate.Value;
                APData.CurrentRoll = APData.PlayerTransform.eulerAngles.z;
                if (APData.CurrentRoll > 180f) APData.CurrentRoll -= 360f;
                object inputObj = Plugin.f_controlInputs.GetValue(__instance);
                if (inputObj == null) return;
                float stickPitch = 0f;
                float stickRoll = 0f;
                float currentThrottle = 0f;
                stickPitch = (float)Plugin.f_pitch.GetValue(inputObj);
                stickRoll = (float)Plugin.f_roll.GetValue(inputObj);
                currentThrottle = (float)Plugin.f_throttle.GetValue(inputObj);

                Vector3 pForward = APData.PlayerTransform.forward;
                Vector3 pUp = APData.PlayerTransform.up;
                Vector3 pEuler = APData.PlayerTransform.eulerAngles;
                Vector3 localAngVel = APData.PlayerTransform.InverseTransformDirection(APData.PlayerRB.angularVelocity);

                float dt = Mathf.Max(Time.deltaTime, 0.0001f);
                float noiseT = Time.time * Plugin.RandomSpeed.Value;
                bool useRandom = Plugin.RandomEnabled.Value && !APData.GCASActive;
                APData.GCASWarning = false;
                float currentG = 1f;

                if (APData.Enabled != wasEnabled)
                {
                    if (APData.Enabled)
                    {
                        ResetIntegrators(currentThrottle);
                        if (APData.FBWDisabled)
                        {
                            APData.FBWDisabled = false;
                            Plugin.UpdateFBWState();
                        }
                    }
                    wasEnabled = APData.Enabled;
                    APData.UseSetValues = false;
                }

                // waypoint deletion
                if (APData.NavQueue.Count > 0)
                {
                    Vector3 targetPos = APData.NavQueue[0];
                    Vector3 playerPos = APData.PlayerRB.position.ToGlobalPosition().AsVector3();
                    Vector3 diff = targetPos - playerPos;

                    float distSq = new Vector2(diff.x, diff.z).sqrMagnitude;
                    bool passed = Vector3.Dot(pForward, diff.normalized) < 0;

                    float threshold = Plugin.NavReachDistance.Value;
                    float passedThreshold = Plugin.NavPassedDistance.Value;

                    // if (close) or (behind and not too far away)
                    if (distSq < (threshold * threshold) || (passed && distSq < passedThreshold * passedThreshold))
                    {
                        Vector3 reachedPoint = APData.NavQueue[0];
                        APData.NavQueue.RemoveAt(0);
                        if (Plugin.NavCycle.Value && APData.NavQueue.Count >= 1)
                        {
                            APData.NavQueue.Add(reachedPoint);
                        }
                        Plugin.RefreshNavVisuals();
                        if (APData.NavQueue.Count == 0) APData.NavEnabled = false;
                    }

                    if (APData.Enabled && APData.NavEnabled && !APData.GCASActive)
                    {
                        float bearing = Mathf.Atan2(diff.x, diff.z) * Mathf.Rad2Deg;
                        APData.TargetCourse = (bearing + 360f) % 360f;
                    }
                }

                // can a plane have no pilot?
                if (APData.LocalPilot != null && Plugin.m_GetAccel != null)
                {
                    Vector3 pAccel = (Vector3)Plugin.m_GetAccel.Invoke(APData.LocalPilot, null);
                    currentG = Vector3.Dot(pAccel + Vector3.up, pUp);

                    Component pilotComp = APData.LocalPilot as Component;
                    if (pilotComp != null)
                    {
                        var gloc = pilotComp.GetComponent(Plugin.t_GLOC);
                        if (gloc != null)
                        {
                            APData.BloodPressure = (float)Plugin.f_bloodPressure.GetValue(gloc);
                            APData.IsConscious = (bool)Plugin.f_conscious.GetValue(gloc);
                        }
                    }
                }

                // gcas
                if (APData.GCASEnabled)
                {
                    bool gearDown = false;
                    Aircraft acRef = APData.LocalAircraft;
                    if (acRef != null && Plugin.f_gearState != null)
                    {
                        object gs = Plugin.f_gearState.GetValue(acRef);
                        if (gs != null && !gs.ToString().Contains("LockedRetracted")) gearDown = true;
                    }

                    bool pilotOverride = Mathf.Abs(stickPitch) > Plugin.GCAS_Deadzone.Value || Mathf.Abs(stickRoll) > Plugin.GCAS_Deadzone.Value || gearDown;

                    if (pilotOverride && APData.GCASActive)
                    {
                        APData.GCASActive = false;
                        APData.Enabled = false;
                    }

                    float speed = APData.PlayerRB.velocity.magnitude;
                    if (speed > 0f)
                    {
                        Vector3 velocity = APData.PlayerRB.velocity;
                        float descentRate = (velocity.y < 0) ? Mathf.Abs(velocity.y) : 0f;

                        float currentRollAbs = Mathf.Abs(APData.CurrentRoll);
                        float estimatedRollRate = 60f;
                        float timeToRollUpright = currentRollAbs / estimatedRollRate;

                        float gAccel = Plugin.GCAS_MaxG.Value * 9.81f;
                        float turnRadius = speed * speed / gAccel;

                        float reactionTime = Plugin.GCAS_AutoBuffer.Value + (Time.deltaTime * 2.0f) + timeToRollUpright;
                        float reactionDist = speed * reactionTime;
                        float warnDist = speed * Plugin.GCAS_WarnBuffer.Value;

                        overGFactor = 1.0f;

                        // bool isWallThreat = false;

                        if (Time.time >= gcasNextScan)
                        {
                            gcasNextScan = Time.time + 0.02f;

                            dangerImminent = false;
                            warningZone = false;

                            APData.GCASConverge = 0f;

                            Vector3 castStart = APData.PlayerRB.position + (velocity.normalized * 5f);
                            float scanRange = (turnRadius * 1.5f) + warnDist + 500f;

                            if (Physics.SphereCast(castStart, Plugin.GCAS_ScanRadius.Value, velocity.normalized, out RaycastHit hit, scanRange, 8256))
                            {
                                if (hit.transform.root != APData.PlayerTransform.root)
                                {
                                    float turnAngle = Mathf.Abs(Vector3.Angle(velocity, hit.normal) - 90f);
                                    float reqArc = turnRadius * (turnAngle * Mathf.Deg2Rad);

                                    if (hit.distance < (reqArc + reactionDist + 20f))
                                    {
                                        dangerImminent = true;

                                        float availableArcDist = hit.distance - reactionDist - speed * timeToRollUpright;

                                        if (availableArcDist < reqArc)
                                        {
                                            float neededRadius = availableArcDist / (turnAngle * Mathf.Deg2Rad);
                                            neededRadius = Mathf.Max(neededRadius, 1f);
                                            float gRequired = speed * speed / (neededRadius * 9.81f);

                                            overGFactor = Mathf.Max(overGFactor, gRequired / Plugin.GCAS_MaxG.Value);
                                        }
                                    }
                                    else if (hit.distance < (reqArc + reactionDist + warnDist))
                                    {
                                        warningZone = true;
                                        float distToTrigger = hit.distance - (reqArc + reactionDist + 20f);
                                        float totalWarnRange = warnDist - 20f;
                                        float fraction = 1f - (distToTrigger / Mathf.Max(totalWarnRange, 1f));
                                        APData.GCASConverge = Mathf.Clamp01(fraction);
                                    }
                                }
                            }
                        }

                        if (descentRate > 0f)
                        {
                            float diveAngle = Vector3.Angle(velocity, Vector3.ProjectOnPlane(velocity, Vector3.up));
                            float vertBuffer = descentRate * reactionTime;
                            float availablePullAlt = APData.CurrentAlt - vertBuffer;
                            float pullUpLoss = turnRadius * (1f - Mathf.Cos(diveAngle * Mathf.Deg2Rad));

                            if (availablePullAlt < pullUpLoss)
                            {
                                dangerImminent = true;

                                float availableRadius = availablePullAlt / (1f - Mathf.Cos(diveAngle * Mathf.Deg2Rad));
                                availableRadius = Mathf.Max(availableRadius, 1f);

                                float gReqFloor = speed * speed / (availableRadius * 9.81f);

                                overGFactor = Mathf.Max(overGFactor, gReqFloor / Plugin.GCAS_MaxG.Value);
                            }
                            else if (APData.CurrentAlt < (pullUpLoss + vertBuffer + (descentRate * Plugin.GCAS_WarnBuffer.Value)))
                            {
                                warningZone = true;
                                float triggerAlt = pullUpLoss + vertBuffer;
                                float warnRange = descentRate * Plugin.GCAS_WarnBuffer.Value;
                                float distToTrigger = APData.CurrentAlt - triggerAlt;
                                float fraction = 1f - (distToTrigger / Mathf.Max(warnRange, 1f));
                                APData.GCASConverge = Mathf.Max(APData.GCASConverge, Mathf.Clamp01(fraction));
                            }
                        }

                        if (APData.GCASActive)
                        {
                            bool safeToRelease = false;

                            if (!dangerImminent)
                            {
                                // if (isWallThreat || APData.CurrentAlt > 100f) safeToRelease = true;
                                // else if (velocity.y >= 0f) safeToRelease = true;

                                safeToRelease = true;
                            }

                            if (safeToRelease)
                            {
                                APData.GCASActive = false;
                                // APData.Enabled = apStateBeforeGCAS;
                                APData.Enabled = false;
                                pidGCAS.Reset();
                                pidAlt.Reset();
                                pidVS.Reset();
                                pidAngle.Reset();
                                if (Plugin.DisableATAPGCAS.Value)
                                {
                                    APData.TargetSpeed = -1f;
                                    Plugin.SyncMenuValues();
                                }
                            }
                            else
                            {
                                APData.GCASWarning = true;
                                APData.TargetRoll = 0f;
                                APData.GCASConverge = 1f;
                            }
                        }
                        else if (dangerImminent)
                        {
                            if (!pilotOverride)
                            {
                                apStateBeforeGCAS = APData.Enabled;
                                APData.Enabled = true;
                                APData.GCASActive = true;
                                APData.TargetRoll = 0f;
                                if (APData.FBWDisabled)
                                {
                                    APData.FBWDisabled = false;
                                    Plugin.UpdateFBWState();
                                }
                            }
                            APData.GCASWarning = true;
                            APData.GCASConverge = 1f;
                        }
                        else if (warningZone)
                        {
                            APData.GCASWarning = true;
                        }
                    }
                }

                // auto jam
                if (APData.LocalWeaponManager != null && APData.AutoJammerActive)
                {
                    object wm = APData.LocalWeaponManager;
                    if (wm != null)
                    {
                        bool fire = false;

                        if (Plugin.f_currentWeaponStation != null && Plugin.f_stationWeapons != null)
                        {
                            object currStation = Plugin.f_currentWeaponStation.GetValue(wm);
                            if (currStation != null)
                            {
                                if (Plugin.f_stationWeapons.GetValue(currStation) is IList wpnList)
                                {
                                    for (int i = 0; i < wpnList.Count; i++)
                                    {
                                        object w = wpnList[i];
                                        if (w != null && Plugin.t_JammingPod != null && Plugin.t_JammingPod.IsInstanceOfType(w))
                                        {
                                            fire = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        if (fire)
                        {
                            fire = false;
                            if (Plugin.f_targetList != null)
                            {
                                if (Plugin.f_targetList.GetValue(wm) is IList tList && tList.Count > 0) fire = true;
                            }
                        }

                        if (fire)
                        {
                            if (Plugin.f_charge != null && Plugin.f_powerSupply != null)
                            {
                                object ps = Plugin.f_powerSupply.GetValue(APData.LocalAircraft);
                                if (ps != null)
                                {
                                    float cur = (float)Plugin.f_charge.GetValue(ps);
                                    float max = (float)Plugin.f_maxCharge.GetValue(ps);
                                    if (max <= 1f) max = 100f;

                                    if ((cur / max) >= Plugin.AutoJammerThreshold.Value)
                                    {
                                        if (!isJammerHoldingTrigger)
                                        {
                                            if (jammerNextFireTime == 0f)
                                                jammerNextFireTime = Time.time + (Plugin.AutoJammerRandom.Value ? UnityEngine.Random.Range(Plugin.AutoJammerMinDelay.Value, Plugin.AutoJammerMaxDelay.Value) : 0f);

                                            if (Time.time >= jammerNextFireTime) { isJammerHoldingTrigger = true; jammerNextFireTime = 0f; }
                                        }
                                    }
                                    else
                                    {
                                        if (isJammerHoldingTrigger)
                                        {
                                            if (jammerNextReleaseTime == 0f)
                                                jammerNextReleaseTime = Time.time + (Plugin.AutoJammerRandom.Value ? UnityEngine.Random.Range(Plugin.AutoJammerReleaseMin.Value, Plugin.AutoJammerReleaseMax.Value) : 0f);

                                            if (Time.time >= jammerNextReleaseTime) { isJammerHoldingTrigger = false; jammerNextReleaseTime = 0f; }
                                        }
                                    }

                                    if (isJammerHoldingTrigger && Plugin.m_Fire != null)
                                        Plugin.m_Fire.Invoke(wm, null);
                                }
                            }
                        }
                        else
                        {
                            isJammerHoldingTrigger = false;
                            jammerNextFireTime = 0f;
                        }
                    }
                }

                bool pilotPitch = Mathf.Abs(stickPitch) > Plugin.StickTempThreshold.Value;
                bool pilotRoll = Mathf.Abs(stickRoll) > Plugin.StickTempThreshold.Value;

                if (pilotPitch || pilotRoll)
                {
                    APData.LastOverrideInputTime = Time.time;

                    if (APData.Enabled)
                    {
                        bool triggerDisengage = false;

                        if (Plugin.StickDisengageEnabled.Value)
                        {
                            if (Mathf.Abs(stickPitch) > Plugin.StickDisengageThreshold.Value ||
                                Mathf.Abs(stickRoll) > Plugin.StickDisengageThreshold.Value)
                            {
                                triggerDisengage = true;
                            }
                        }

                        if (Plugin.DisengageDelay.Value > 0)
                        {
                            _disengageTimer += dt;
                            if (_disengageTimer >= Plugin.DisengageDelay.Value)
                            {
                                triggerDisengage = true;
                            }
                        }

                        if (triggerDisengage)
                        {
                            APData.Enabled = false;
                            if (Plugin.DisableNavAPStick.Value)
                            {
                                APData.NavEnabled = false;
                            }
                            if (Plugin.DisableATAPStick.Value)
                            {
                                APData.TargetSpeed = -1f;
                                Plugin.SyncMenuValues();
                            }
                            _disengageTimer = 0f;
                        }
                    }
                }
                else
                {
                    _disengageTimer = 0f;
                }

                bool isWaitingToReengage = (Time.time - APData.LastOverrideInputTime) < Plugin.ReengageDelay.Value;

                // throttle control
                if (APData.TargetSpeed >= 0 && Plugin.f_throttle != null)
                {
                    float currentSpeed = (APData.LocalAircraft != null) ? APData.LocalAircraft.speed : APData.PlayerRB.velocity.magnitude;
                    float targetSpeedMS;

                    if (APData.SpeedHoldIsMach)
                    {
                        float currentAlt = APData.LocalAircraft.GlobalPosition().y;
                        float sos = LevelInfo.GetSpeedOfSound(currentAlt);
                        targetSpeedMS = APData.TargetSpeed * sos;
                    }
                    else
                    {
                        targetSpeedMS = APData.TargetSpeed;
                    }

                    float sErr = targetSpeedMS - currentSpeed;

                    float currentAccel = Mathf.Abs(currentSpeed - lastSpdMeasurement) / dt;
                    lastSpdMeasurement = currentSpeed;

                    if (useRandom)
                    {
                        float sErrAbs = Mathf.Abs(sErr);
                        if (!isSpdSleeping)
                        {
                            if (sErrAbs < Plugin.Rand_Spd_Inner.Value && currentAccel < Plugin.Rand_Acc_Inner.Value)
                            {
                                spdSleepUntil = Time.time + UnityEngine.Random.Range(Plugin.Rand_Spd_SleepMin.Value, Plugin.Rand_Spd_SleepMax.Value);
                                isSpdSleeping = true;
                            }
                        }
                        else if (sErrAbs > Plugin.Rand_Spd_Outer.Value || currentAccel > Plugin.Rand_Acc_Outer.Value || Time.time > spdSleepUntil)
                        {
                            isSpdSleeping = false;
                        }
                    }

                    float minT = APData.AllowExtremeThrottle ? 0f : Plugin.ThrottleMinLimit.Value;
                    float maxT = APData.AllowExtremeThrottle ? 1f : Plugin.ThrottleMaxLimit.Value;

                    float pidOutput = pidSpd.Evaluate(sErr, currentSpeed, dt,
                        Plugin.Conf_Spd_P.Value, Plugin.Conf_Spd_I.Value, Plugin.Conf_Spd_D.Value,
                        maxT, true, null,
                        lastThrottleOut, 0.95f);

                    lastThrottleOut = pidOutput;

                    float currentPitch = Mathf.Asin(pForward.y);
                    float pitchWorkload = Mathf.Sin(currentPitch) * Plugin.Conf_Spd_C.Value;

                    float desiredLeverPos = Mathf.Clamp((isSpdSleeping ? pidSpd.Integral : pidOutput) + pitchWorkload, minT, maxT);

                    currentAppliedThrottle = Mathf.MoveTowards(
                        currentAppliedThrottle,
                        desiredLeverPos,
                        Plugin.ThrottleSlewRate.Value * dt
                    );
                    Plugin.f_throttle.SetValue(inputObj, currentAppliedThrottle);
                }

                // autopilot
                if (APData.Enabled || APData.GCASActive)
                {
                    // keys
                    if (!pilotPitch && !pilotRoll && !APData.GCASActive)
                    {
                        float fpsRef = 60f;
                        float aStep = Plugin.AltStep.Value * fpsRef * dt;
                        float bStep = Plugin.BigAltStep.Value * fpsRef * dt;
                        float cStep = Plugin.ClimbRateStep.Value * fpsRef * dt;
                        float rStep = Plugin.BankStep.Value * fpsRef * dt;
                        if (InputHelper.IsPressed(Plugin.UpKeyRW) || Plugin.UpKey.Value.IsPressed()) APData.TargetAlt += aStep;
                        if (InputHelper.IsPressed(Plugin.DownKeyRW) || Plugin.DownKey.Value.IsPressed()) APData.TargetAlt -= aStep;
                        if (InputHelper.IsPressed(Plugin.BigUpKeyRW) || Plugin.BigUpKey.Value.IsPressed()) APData.TargetAlt += bStep;
                        if (InputHelper.IsPressed(Plugin.BigDownKeyRW) || Plugin.BigDownKey.Value.IsPressed()) APData.TargetAlt = Mathf.Max(APData.TargetAlt - bStep, Plugin.MinAltitude.Value);

                        if (InputHelper.IsPressed(Plugin.ClimbRateUpKeyRW) || Plugin.ClimbRateUpKey.Value.IsPressed()) APData.CurrentMaxClimbRate += cStep;
                        if (InputHelper.IsPressed(Plugin.ClimbRateDownKeyRW) || Plugin.ClimbRateDownKey.Value.IsPressed()) APData.CurrentMaxClimbRate = Mathf.Max(0.5f, APData.CurrentMaxClimbRate - cStep);

                        if (APData.NavEnabled)
                        {
                            bool bankLeft = InputHelper.IsPressed(Plugin.BankLeftKeyRW) || Plugin.BankLeftKey.Value.IsPressed();
                            bool bankRight = InputHelper.IsPressed(Plugin.BankRightKeyRW) || Plugin.BankRightKey.Value.IsPressed();
                            if (bankLeft || bankRight)
                            {
                                if (APData.TargetRoll == -999f) APData.TargetRoll = Plugin.DefaultCRLimit.Value;

                                if (bankLeft) APData.TargetRoll -= rStep;
                                if (bankRight) APData.TargetRoll += rStep;

                                APData.TargetRoll = Mathf.Clamp(APData.TargetRoll, 1f, 90f);
                            }
                        }
                        if (APData.TargetCourse >= 0f)
                        {
                            if (InputHelper.IsPressed(Plugin.BankLeftKeyRW) || Plugin.BankLeftKey.Value.IsPressed()) APData.TargetCourse = Mathf.Repeat(APData.TargetCourse - rStep, 360f);
                            if (InputHelper.IsPressed(Plugin.BankRightKeyRW) || Plugin.BankRightKey.Value.IsPressed()) APData.TargetCourse = Mathf.Repeat(APData.TargetCourse + rStep, 360f);
                        }
                        else
                        {
                            bool bankLeft = InputHelper.IsPressed(Plugin.BankLeftKeyRW) || Plugin.BankLeftKey.Value.IsPressed();
                            bool bankRight = InputHelper.IsPressed(Plugin.BankRightKeyRW) || Plugin.BankRightKey.Value.IsPressed();
                            if (bankLeft || bankRight)
                            {
                                if (APData.TargetRoll == -999f) APData.TargetRoll = APData.CurrentRoll;
                                if (bankLeft)
                                    APData.TargetRoll = Mathf.Repeat(APData.TargetRoll + rStep + 180f, 360f) - 180f;

                                if (bankRight)
                                    APData.TargetRoll = Mathf.Repeat(APData.TargetRoll - rStep + 180f, 360f) - 180f;
                            }
                        }

                        if (InputHelper.IsDown(Plugin.ClearKeyRW) || Plugin.ClearKey.Value.IsDown())
                        {
                            if (APData.NavEnabled)
                            {
                                float crlimit = Plugin.DefaultCRLimit.Value;
                                if (APData.TargetRoll != crlimit)
                                {
                                    APData.TargetRoll = crlimit;
                                }
                                else
                                {
                                    APData.NavEnabled = false;
                                }
                            }
                            else if (APData.TargetCourse != -1f)
                            {
                                APData.TargetCourse = -1f;
                            }
                            else if (APData.TargetRoll != 0f)
                            {
                                APData.TargetRoll = 0f;
                            }
                            else if (APData.TargetAlt != -1f)
                            {
                                APData.TargetAlt = -1f;
                            }
                            else
                            {
                                APData.TargetRoll = -999f;
                            }
                        }
                    }

                    // roll/course control
                    bool rollAxisActive = APData.GCASActive || APData.TargetCourse >= 0f || APData.TargetRoll != -999f;

                    if (rollAxisActive)
                    {
                        if ((pilotRoll || isWaitingToReengage) && !APData.GCASActive)
                        {
                            pidRoll.Reset();
                            pidCrs.Reset();
                        }
                        else
                        {
                            float activeTargetRoll = APData.TargetRoll;

                            if (APData.TargetCourse >= 0f && APData.PlayerRB.velocity.sqrMagnitude > 1f && !APData.GCASActive)
                            {
                                Vector3 flatVel = Vector3.ProjectOnPlane(APData.PlayerRB.velocity, Vector3.up);
                                if (flatVel.sqrMagnitude > 1f)
                                {
                                    float curCrs = Quaternion.LookRotation(flatVel).eulerAngles.y;
                                    float cErr = Mathf.DeltaAngle(curCrs, APData.TargetCourse);

                                    float desiredTurnRate = pidCrs.Evaluate(cErr, curCrs, dt,
                                        Plugin.Conf_Crs_P.Value, Plugin.Conf_Crs_I.Value, Plugin.Conf_Crs_D.Value,
                                        Plugin.Conf_Crs_ILimit.Value, true, null,
                                        lastBankReq, 20f);

                                    lastBankReq = desiredTurnRate;

                                    float gravity = 9.81f;
                                    float velocity = Mathf.Max(APData.PlayerRB.velocity.magnitude, 1f);
                                    float turnRateRad = desiredTurnRate * Mathf.Deg2Rad;
                                    float bankReq = Mathf.Atan(velocity * turnRateRad / gravity) * Mathf.Rad2Deg;

                                    if (Plugin.Conf_InvertCourseRoll.Value) bankReq = -bankReq;

                                    float safeMaxG = Mathf.Max(Plugin.GCAS_MaxG.Value, 1.01f);
                                    float gLimitBank = Mathf.Acos(1f / safeMaxG) * Mathf.Rad2Deg;

                                    float userLimit = (APData.TargetRoll != -999f && APData.TargetRoll != 0)
                                                      ? Mathf.Abs(APData.TargetRoll)
                                                      : Plugin.DefaultCRLimit.Value;

                                    float finalBankLimit = Mathf.Min(userLimit, gLimitBank);

                                    activeTargetRoll = Mathf.Clamp(bankReq, -finalBankLimit, finalBankLimit);
                                }
                            }
                            else if (APData.GCASActive)
                            {
                                activeTargetRoll = 0f;
                            }

                            float rollError = Mathf.DeltaAngle(APData.CurrentRoll, activeTargetRoll);
                            float rollRate = localAngVel.z * Mathf.Rad2Deg;

                            // Roll sleep
                            if (useRandom)
                            {
                                float rollErrAbs = Mathf.Abs(rollError);
                                float rollRateAbs = Mathf.Abs(rollRate);
                                if (!isRollSleeping)
                                {
                                    if (rollErrAbs < Plugin.Rand_Roll_Inner.Value && rollRateAbs < Plugin.Rand_RollRate_Inner.Value)
                                    {
                                        rollSleepUntil = Time.time + UnityEngine.Random.Range(Plugin.Rand_RollSleepMin.Value, Plugin.Rand_RollSleepMax.Value);
                                        isRollSleeping = true;
                                    }
                                }
                                else if (rollErrAbs > Plugin.Rand_Roll_Outer.Value || rollRateAbs > Plugin.Rand_RollRate_Outer.Value || Time.time > rollSleepUntil)
                                {
                                    isRollSleeping = false;
                                }
                            }

                            float rollOut = 0f;
                            if (useRandom && isRollSleeping)
                            {
                                pidRoll.Integral = Mathf.MoveTowards(pidRoll.Integral, 0f, dt * 5f);
                            }
                            else
                            {
                                rollOut = pidRoll.Evaluate(rollError, APData.CurrentRoll, dt,
                                    Plugin.RollP.Value, Plugin.RollI.Value, Plugin.RollD.Value,
                                    Plugin.RollILimit.Value, false, -rollRate,
                                    lastRollOut, 0.95f);

                                lastRollOut = rollOut;

                                if (Plugin.InvertRoll.Value) rollOut = -rollOut;
                                if (useRandom) rollOut += (Mathf.PerlinNoise(0f, noiseT) - 0.5f) * 2f * Plugin.RandomStrength.Value;
                            }

                            Plugin.f_roll?.SetValue(inputObj, Mathf.Clamp(rollOut, -1f, 1f));
                        }
                    }

                    // pitch control
                    bool pitchAxisActive = APData.GCASActive || APData.TargetAlt > 0f;

                    if (pitchAxisActive)
                    {
                        if ((pilotPitch || isWaitingToReengage) && !APData.GCASActive)
                        {
                            pidAlt.Reset();
                            pidVS.Reset();
                            pidAngle.Reset();
                            if (!Plugin.KeepSetAltStick.Value)
                            {
                                APData.TargetAlt = APData.CurrentAlt;
                            }
                        }
                        else
                        {
                            float pitchOut = 0f;

                            // gcas
                            if (APData.GCASActive)
                            {
                                float rollAngle = Mathf.Abs(APData.CurrentRoll);
                                float targetG;

                                if (rollAngle >= 90f)
                                {
                                    targetG = 0f;
                                }
                                else
                                {
                                    targetG = Plugin.GCAS_MaxG.Value * overGFactor;
                                }

                                float gError = targetG - currentG;
                                pitchOut = pidGCAS.Evaluate(gError, currentG, dt,
                                    Plugin.GCAS_P.Value, Plugin.GCAS_I.Value, Plugin.GCAS_D.Value,
                                    Plugin.GCAS_ILimit.Value, true);
                            }
                            // alt hold
                            else if (APData.TargetAlt > 0f)
                            {
                                float altError = APData.TargetAlt - APData.CurrentAlt;
                                float currentVS = APData.PlayerRB.velocity.y;

                                // pitch sleep
                                if (useRandom)
                                {
                                    float altErrAbs = Mathf.Abs(altError);
                                    float vsAbs = Mathf.Abs(currentVS);

                                    if (!isPitchSleeping)
                                    {
                                        // start sleep check
                                        if (altErrAbs < Plugin.Rand_Alt_Inner.Value && vsAbs < Plugin.Rand_VS_Inner.Value)
                                        {
                                            pitchSleepUntil = Time.time + UnityEngine.Random.Range(Plugin.Rand_PitchSleepMin.Value, Plugin.Rand_PitchSleepMax.Value);
                                            isPitchSleeping = true;
                                        }
                                    }
                                    else
                                    {
                                        // wake up check
                                        if (altErrAbs > Plugin.Rand_Alt_Outer.Value || vsAbs > Plugin.Rand_VS_Outer.Value || Time.time > pitchSleepUntil)
                                        {
                                            isPitchSleeping = false;
                                        }
                                    }
                                }

                                if (useRandom && isPitchSleeping)
                                {
                                    pidAlt.Integral = Mathf.MoveTowards(pidAlt.Integral, 0f, dt * 2f);
                                    pidVS.Integral = Mathf.MoveTowards(pidVS.Integral, 0f, dt * 10f);
                                    pidAngle.Integral = Mathf.MoveTowards(pidAngle.Integral, 0f, dt * 5f);
                                }
                                else
                                {
                                    float targetVS = pidAlt.Evaluate(altError, APData.CurrentAlt, dt,
                                        Plugin.Conf_Alt_P.Value, Plugin.Conf_Alt_I.Value, Plugin.Conf_Alt_D.Value,
                                        Plugin.Conf_Alt_ILimit.Value, false, null,
                                        lastVSReq, APData.CurrentMaxClimbRate * 0.95f);

                                    lastVSReq = targetVS;

                                    float possibleAccel = Plugin.GCAS_MaxG.Value * 9.81f;
                                    float maxSafeVS = Mathf.Sqrt(2f * possibleAccel * Mathf.Abs(altError));
                                    targetVS = Mathf.Clamp(targetVS, -maxSafeVS, maxSafeVS);

                                    targetVS = Mathf.Clamp(targetVS, -APData.CurrentMaxClimbRate, APData.CurrentMaxClimbRate);
                                    float vsError = targetVS - currentVS;

                                    float targetPitchDeg = pidVS.Evaluate(vsError, currentVS, dt,
                                        Plugin.Conf_VS_P.Value, Plugin.Conf_VS_I.Value, Plugin.Conf_VS_D.Value,
                                        Plugin.Conf_VS_ILimit.Value, true, null,
                                        lastAngleReq, Plugin.Conf_VS_MaxAngle.Value * 0.95f);

                                    lastAngleReq = targetPitchDeg;

                                    float currentPitch = Mathf.Asin(pForward.y) * Mathf.Rad2Deg;
                                    float angleError = targetPitchDeg - currentPitch;
                                    float pitchRate = localAngVel.x * Mathf.Rad2Deg;

                                    pitchOut = pidAngle.Evaluate(angleError, currentPitch, dt,
                                        Plugin.Conf_Angle_P.Value, Plugin.Conf_Angle_I.Value, Plugin.Conf_Angle_D.Value,
                                        Plugin.Conf_Angle_ILimit.Value, false, -pitchRate,
                                        lastPitchOut, 0.95f);

                                    lastPitchOut = pitchOut;

                                    if (useRandom) pitchOut += (Mathf.PerlinNoise(noiseT, 0f) - 0.5f) * 2f * Plugin.RandomStrength.Value;
                                }
                            }

                            if (Plugin.InvertPitch.Value) pitchOut = -pitchOut;
                            pitchOut = Mathf.Clamp(pitchOut, -1f, 1f);
                            Plugin.f_pitch?.SetValue(inputObj, pitchOut);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[ControlOverridePatch] Error: {ex}");
                APData.Enabled = false;
            }
        }
    }

    [HarmonyPatch(typeof(FlightHud), "Update")]
    internal class HUDVisualsPatch
    {
        private static GameObject infoOverlayObj;
        private static Text overlayText;

        private static GameObject gcasLeftObj;
        private static GameObject gcasRightObj;
        private static GameObject gcasTopObj;
        private static Text gcasLeftText;
        private static Text gcasRightText;
        private static Text gcasTopText;
        private static float smoothedConverge = 0f;

        private static float lastFuelMass = 0f;
        private static float fuelFlowEma = 0f;
        private static float lastUpdateTime = 0f;

        private static float _lastStringUpdate = 0f;
        private static FuelGauge _cachedFuelGauge;
        private static Text _cachedRefLabel;
        private static readonly StringBuilder _sbHud = new(1024);
        private static GameObject _lastVehicleChecked;

        public static void Reset()
        {
            if (infoOverlayObj != null)
            {
                UnityEngine.Object.Destroy(infoOverlayObj);
            }
            infoOverlayObj = null;
            overlayText = null;

            if (gcasLeftObj) UnityEngine.Object.Destroy(gcasLeftObj);
            if (gcasRightObj) UnityEngine.Object.Destroy(gcasRightObj);
            if (gcasTopObj) UnityEngine.Object.Destroy(gcasTopObj);
            gcasLeftObj = null;
            gcasRightObj = null;
            gcasTopObj = null;
            smoothedConverge = 0f;

            lastFuelMass = 0f;
            fuelFlowEma = 0f;
            lastUpdateTime = 0f;
            _lastStringUpdate = 0f;
            _cachedFuelGauge = null;
            _cachedRefLabel = null;
            _lastVehicleChecked = null;
            _sbHud.Clear();
        }

        private static void Postfix(FlightHud __instance)
        {
            if (!Plugin.ShowExtraInfo.Value) return;

            try
            {
                if (__instance == null || Plugin.f_playerVehicle == null) return;

                object vehicleRaw = Plugin.f_playerVehicle.GetValue(__instance);
                UnityEngine.Object unityObj = vehicleRaw as UnityEngine.Object;

                if (unityObj == null) return;
                if (vehicleRaw is not Component vehicleComponent) return;

                GameObject currentVehicleObj = vehicleComponent.gameObject;
                if (currentVehicleObj == null) return;

                if (_lastVehicleChecked != currentVehicleObj || _cachedFuelGauge == null)
                {
                    _lastVehicleChecked = currentVehicleObj;
                    lastFuelMass = 0f;
                    fuelFlowEma = 0f;
                    lastUpdateTime = 0f;
                    _cachedFuelGauge = __instance.GetComponentInChildren<FuelGauge>(true);

                    if (infoOverlayObj) UnityEngine.Object.Destroy(infoOverlayObj);
                    infoOverlayObj = null;

                    if (gcasLeftObj) UnityEngine.Object.Destroy(gcasLeftObj);
                    gcasLeftObj = null;

                    if (_cachedFuelGauge != null)
                    {
                        _cachedRefLabel = (Text)Plugin.f_fuelLabel.GetValue(_cachedFuelGauge);
                    }
                }

                if (_cachedFuelGauge == null || _cachedRefLabel == null) return;

                if (!infoOverlayObj)
                {
                    infoOverlayObj = UnityEngine.Object.Instantiate(_cachedRefLabel.gameObject, _cachedRefLabel.transform.parent);
                    infoOverlayObj.name = "AP_CombinedOverlay";
                    overlayText = infoOverlayObj.GetComponent<Text>();
                    overlayText.resizeTextForBestFit = false;
                    overlayText.supportRichText = true;
                    overlayText.alignment = TextAnchor.UpperLeft;
                    overlayText.horizontalOverflow = HorizontalWrapMode.Overflow;
                    overlayText.verticalOverflow = VerticalWrapMode.Overflow;
                    RectTransform rect = infoOverlayObj.GetComponent<RectTransform>();
                    rect.pivot = new Vector2(0, 1);
                    rect.anchorMin = new Vector2(0, 1);
                    rect.anchorMax = new Vector2(0, 1);
                    rect.localScale = _cachedRefLabel.transform.localScale;
                    rect.localRotation = Quaternion.identity;
                    infoOverlayObj.SetActive(true);
                }

                float currentSize = PlayerSettings.hudTextSize;
                float scaleRatio = currentSize / 40f;
                overlayText.fontSize = (int)currentSize;

                Vector3 refLocalPos = _cachedRefLabel.transform.localPosition;
                float finalX = Plugin.OverlayOffsetX.Value * scaleRatio;
                float finalY = Plugin.OverlayOffsetY.Value * scaleRatio;
                infoOverlayObj.transform.localPosition = refLocalPos + new Vector3(finalX, finalY, 0);

                Aircraft aircraft = APData.LocalAircraft;
                if (aircraft != null && Plugin.f_fuelCapacity != null)
                {
                    float currentFuel = (float)Plugin.f_fuelCapacity.GetValue(aircraft) * aircraft.GetFuelLevel();
                    float time = Time.time;
                    if (lastUpdateTime != 0f && lastFuelMass > 0f)
                    {
                        float dt = time - lastUpdateTime;
                        if (dt >= Plugin.FuelUpdateInterval.Value)
                        {
                            float burned = lastFuelMass - currentFuel;
                            float flow = Mathf.Max(0f, burned / dt);
                            fuelFlowEma = Mathf.Lerp(fuelFlowEma, flow, Plugin.FuelSmoothing.Value);
                            lastUpdateTime = time;
                            lastFuelMass = currentFuel;
                        }
                    }
                    else { lastUpdateTime = time; lastFuelMass = currentFuel; }

                    if (Time.time - _lastStringUpdate >= Plugin.DisplayUpdateInterval.Value)
                    {
                        _lastStringUpdate = Time.time;
                        _sbHud.Clear();

                        if (currentFuel <= 0f)
                        {
                            _sbHud.Append("<color=").Append(Plugin.ColorCrit.Value).Append(">00:00\n----</color>\n");
                        }
                        else
                        {
                            float calcFlow = Mathf.Max(fuelFlowEma, 0.0001f);
                            float secs = currentFuel / calcFlow;
                            string sTime = TimeSpan.FromSeconds(Mathf.Min(secs, 359999f)).ToString("hh\\:mm");
                            float mins = secs / 60f;

                            string fuelCol = Plugin.ColorGood.Value;
                            if (mins < Plugin.FuelCritMinutes.Value) fuelCol = Plugin.ColorCrit.Value;
                            else if (mins < Plugin.FuelWarnMinutes.Value) fuelCol = Plugin.ColorWarn.Value;

                            _sbHud.Append("<color=").Append(fuelCol).Append(">").Append(sTime).Append("</color>\n");

                            float spd = (aircraft.rb != null) ? aircraft.rb.velocity.magnitude : 0f;
                            float distMeters = secs * APData.SpeedEma;
                            if (distMeters > 99999000f) distMeters = 99999000f;

                            string sRange = ModUtils.ProcessGameString(UnitConverter.DistanceReading(distMeters), Plugin.DistShowUnit.Value);
                            _sbHud.Append("<color=").Append(Plugin.ColorRange.Value).Append(">").Append(sRange).Append("</color>\n\n");
                        }

                        // (AP was on before GCAS) or (AP is on and no GCAS)
                        bool apActive = (ControlOverridePatch.apStateBeforeGCAS && APData.GCASActive) || (APData.Enabled && !APData.GCASActive);
                        bool speedActive = APData.TargetSpeed >= 0f;

                        if ((apActive || speedActive) && Plugin.ShowAPOverlay.Value)
                        {
                            bool placeholders = Plugin.ShowPlaceholders.Value;

                            if (apActive || speedActive)
                                _sbHud.Append("<color=").Append(Plugin.ColorAPOn.Value).Append(">");

                            bool hasLine1 = false;
                            if (speedActive)
                            {
                                if (APData.SpeedHoldIsMach)
                                    _sbHud.Append("M").Append(APData.TargetSpeed.ToString("F2"));
                                else
                                    _sbHud.Append("S").Append(ModUtils.ProcessGameString(UnitConverter.SpeedReading(APData.TargetSpeed), Plugin.SpeedShowUnit.Value));
                                hasLine1 = true;
                            }
                            else if (placeholders) { _sbHud.Append("S"); hasLine1 = true; }

                            if (apActive)
                            {
                                string degUnit = Plugin.AngleShowUnit.Value ? "°" : "";
                                if (APData.TargetRoll != -999f)
                                {
                                    if (hasLine1) _sbHud.Append(" ");
                                    _sbHud.Append("R").Append(APData.TargetRoll.ToString("F0")).Append(degUnit);
                                    hasLine1 = true;
                                }
                                else if (placeholders)
                                {
                                    if (hasLine1) _sbHud.Append(" ");
                                    _sbHud.Append("R");
                                    hasLine1 = true;
                                }
                            }
                            if (hasLine1) _sbHud.Append("\n");

                            bool hasLine2 = false;
                            if (apActive)
                            {
                                if (APData.TargetAlt > 0)
                                {
                                    _sbHud.Append("A").Append(ModUtils.ProcessGameString(UnitConverter.AltitudeReading(APData.TargetAlt), Plugin.AltShowUnit.Value));
                                    hasLine2 = true;
                                }
                                else if (placeholders) { _sbHud.Append("A"); hasLine2 = true; }

                                if (APData.CurrentMaxClimbRate > 0 && APData.CurrentMaxClimbRate != Plugin.DefaultMaxClimbRate.Value)
                                {
                                    if (hasLine2) _sbHud.Append(" ");
                                    _sbHud.Append("V").Append(ModUtils.ProcessGameString(UnitConverter.ClimbRateReading(APData.CurrentMaxClimbRate), Plugin.VertSpeedShowUnit.Value));
                                    hasLine2 = true;
                                }
                                else if (placeholders) { if (hasLine2) _sbHud.Append(" "); _sbHud.Append("V"); hasLine2 = true; }
                            }
                            if (hasLine2) _sbHud.Append("\n");

                            bool hasLine3 = false;
                            if (apActive)
                            {
                                string degUnit = Plugin.AngleShowUnit.Value ? "°" : "";
                                if (APData.TargetCourse >= 0)
                                {
                                    _sbHud.Append("C").Append(APData.TargetCourse.ToString("F0")).Append(degUnit);
                                    hasLine3 = true;
                                }
                                else if (placeholders) { _sbHud.Append("C"); hasLine3 = true; }

                                if (APData.NavEnabled && APData.NavQueue.Count > 0)
                                {
                                    if (hasLine3) _sbHud.Append(" ");
                                    float d = Vector3.Distance(APData.PlayerRB.position.ToGlobalPosition().AsVector3(), APData.NavQueue[0]);
                                    _sbHud.Append("W>").Append(ModUtils.ProcessGameString(UnitConverter.DistanceReading(d), Plugin.DistShowUnit.Value));
                                    hasLine3 = true;
                                }
                                else if (placeholders) { if (hasLine3) _sbHud.Append(" "); _sbHud.Append("W"); hasLine3 = true; }
                            }
                            if (hasLine3) _sbHud.Append("\n");
                            if (apActive || speedActive) _sbHud.Append("</color>");
                        }

                        if (!APData.GCASEnabled && Plugin.ShowGCASOff.Value)
                        {
                            _sbHud.Append("<color=").Append(Plugin.ColorInfo.Value).Append(">GCAS-</color>\n");
                        }

                        if (Plugin.ShowOverride.Value && APData.Enabled && !APData.GCASActive)
                        {
                            float overrideRemaining = Plugin.ReengageDelay.Value - (Time.time - APData.LastOverrideInputTime);
                            if (overrideRemaining > 0)
                            {
                                _sbHud.Append("<color=").Append(Plugin.ColorInfo.Value).Append(">").Append(overrideRemaining.ToString("F1")).Append("s</color>\n");
                            }
                        }

                        if (APData.AutoJammerActive)
                        {
                            _sbHud.Append("<color=").Append(Plugin.ColorAPOn.Value).Append(">AJ\n</color>");
                        }

                        if (APData.FBWDisabled)
                        {
                            _sbHud.Append("<color=").Append(Plugin.ColorCrit.Value).Append(">FBW OFF</color>");
                        }
                        overlayText.text = _sbHud.ToString();
                    }
                }

                if (APData.GCASActive || APData.GCASWarning)
                {
                    if (gcasLeftObj == null)
                    {
                        Transform hudCenter = _cachedRefLabel.transform.parent;

                        GameObject CreateObj(string name, string txt)
                        {
                            GameObject obj = UnityEngine.Object.Instantiate(_cachedRefLabel.gameObject, hudCenter);
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

                        gcasLeftObj = CreateObj("GCAS_Left", ">");
                        gcasLeftText = gcasLeftObj.GetComponent<Text>();

                        gcasRightObj = CreateObj("GCAS_Right", "<");
                        gcasRightText = gcasRightObj.GetComponent<Text>();

                        gcasTopObj = CreateObj("GCAS_Top", "FLYUP");
                        gcasTopText = gcasTopObj.GetComponent<Text>();
                    }

                    float target = APData.GCASConverge;
                    smoothedConverge = Mathf.Lerp(smoothedConverge, target, Time.deltaTime * 10f);

                    gcasLeftObj.SetActive(true);
                    gcasRightObj.SetActive(true);
                    gcasTopObj.SetActive(APData.GCASActive);

                    Color gcasColor = ModUtils.GetColor(Plugin.ColorCrit.Value, Color.red);

                    int arrowSize = (int)currentSize;
                    int textSize = (int)(currentSize * 0.7);

                    gcasLeftText.fontSize = arrowSize;
                    gcasLeftText.color = gcasColor;
                    gcasRightText.fontSize = arrowSize;
                    gcasRightText.color = gcasColor;
                    gcasTopText.fontSize = textSize;
                    gcasTopText.color = gcasColor;

                    float alpha = smoothedConverge;
                    var c = gcasLeftText.color; c.a = alpha; gcasLeftText.color = c;
                    c = gcasRightText.color; c.a = alpha; gcasRightText.color = c;
                    c = gcasTopText.color; c.a = alpha; gcasTopText.color = c;

                    float offsetX = Mathf.Lerp(200f, 5f, smoothedConverge);

                    float yOffset = -(arrowSize * 0.25f);

                    gcasLeftObj.transform.localPosition = new Vector3(-offsetX, yOffset, 0);
                    gcasRightObj.transform.localPosition = new Vector3(offsetX, yOffset, 0);
                    gcasTopObj.transform.localPosition = new Vector3(0, 40, 0);
                }
                else
                {
                    if (gcasLeftObj && gcasLeftObj.activeSelf) gcasLeftObj.SetActive(false);
                    if (gcasRightObj && gcasRightObj.activeSelf) gcasRightObj.SetActive(false);
                    if (gcasTopObj && gcasTopObj.activeSelf) gcasTopObj.SetActive(false);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[HUDVisualsPatch] Error: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(DynamicMap), "MapControls")]
    internal class MapInteractionPatch
    {
        public static void Reset() { }
        static void Postfix(DynamicMap __instance)
        {
            if (__instance == null || !DynamicMap.mapMaximized || !Input.GetMouseButtonDown(1)) return;

            if (!__instance.TryGetCursorCoordinates(out var clickedGlobalPos)) return;

            if (__instance.selectedIcons != null && __instance.selectedIcons.Count > 0)
            {
                if (__instance.selectedIcons[0] is UnitMapIcon unitIcon)
                {
                    if (unitIcon.unit != null)
                    {
                        if (DynamicMap.GetFactionMode(unitIcon.unit.NetworkHQ, false) == FactionMode.Friendly
                            && unitIcon.unit is not Building)
                        {
                            return; // there was friendly unit selected as first unit
                        }
                    }
                }
            }

            if (APData.LocalAircraft != null)
            {
                if (!Input.GetKey(KeyCode.LeftShift))
                {
                    APData.NavQueue.Clear();
                }

                APData.NavQueue.Add(clickedGlobalPos.AsVector3());
                APData.NavEnabled = true;
                float currentTargetRoll = APData.TargetRoll;
                if (currentTargetRoll == -999f || currentTargetRoll == 0f)
                {
                    APData.TargetRoll = Plugin.DefaultCRLimit.Value;
                }
                Plugin.RefreshNavVisuals();
                Plugin.SyncMenuValues();
            }
        }
    }

    [HarmonyPatch(typeof(DynamicMap), "UpdateMap")]
    internal class MapWaypointPatch
    {
        public static void Reset() { }
        static void Postfix()
        {
            if (APData.NavVisuals.Count == 0 || APData.PlayerRB == null) return;

            var map = SceneSingleton<DynamicMap>.i;
            if (map == null || map.mapImage == null) return;

            float zoom = map.mapImage.transform.localScale.x;
            float invZoom = 1f / zoom;
            float factor = 900f / map.mapDimension;

            GameObject playerLine = null;

            foreach (var obj in APData.NavVisuals)
            {
                if (obj == null) continue;
                if (obj.name == "AP_NavMarker") obj.transform.localScale = Vector3.one * invZoom;
                else
                {
                    obj.transform.localScale = new Vector3(4f * invZoom, obj.transform.localScale.y, 4f * invZoom);
                    if (obj.name == "AP_NavLine_Player") playerLine = obj;
                }
            }

            if (playerLine != null && APData.NavQueue.Count > 0)
            {
                Vector3 pG = APData.PlayerRB.position.ToGlobalPosition().AsVector3();
                Vector3 pMap = new(pG.x * factor, pG.z * factor, 0f);
                Vector3 targetMap = new(APData.NavQueue[0].x * factor, APData.NavQueue[0].z * factor, 0f);

                playerLine.transform.localPosition = targetMap;

                float angle = -Mathf.Atan2(targetMap.x - pMap.x, targetMap.y - pMap.y) * Mathf.Rad2Deg + 180f;

                playerLine.transform.localEulerAngles = new Vector3(0, 0, angle);

                playerLine.transform.localScale = new Vector3(4f * invZoom, Vector3.Distance(pMap, targetMap), 4f * invZoom);
            }
        }
    }

    [HarmonyPatch(typeof(DynamicMap), "MapControls")]
    internal class UnlockMapPatch
    {
        static readonly MethodInfo ClampMethod = AccessTools.Method(typeof(Mathf), "Clamp", [typeof(float), typeof(float), typeof(float)]);

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var matcher = new CodeMatcher(instructions);

            if (Plugin.UnlockMapPan.Value)
            {
                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Ldc_R4),
                    new CodeMatch(i => i.opcode == OpCodes.Call && i.operand is MethodInfo { Name: "ClampPos" })
                );

                if (matcher.IsValid)
                {
                    matcher.SetOperandAndAdvance(float.MaxValue);
                }
                else
                {
                    Plugin.Logger.LogError("Could not find patch location for map pan.");
                }
            }

            if (Plugin.UnlockMapZoom.Value)
            {
                matcher.Start();
                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Ldc_R4),
                    new CodeMatch(OpCodes.Ldc_R4),
                    new CodeMatch(OpCodes.Call, ClampMethod)
                );

                if (matcher.IsValid)
                {
                    matcher.SetOperandAndAdvance(0.001f);
                    matcher.SetOperandAndAdvance(1000f);
                }
                else
                {
                    Plugin.Logger.LogError("Could not find patch location for map zoom.");
                }
            }
            return matcher.InstructionEnumeration();
        }
    }

    [HarmonyPatch(typeof(DynamicMap), "Minimize")]
    internal class MapSaveStatePatch
    {
        static void Prefix(DynamicMap __instance)
        {
            if (!APData.SaveMapState || __instance == null) return;

            try
            {
                APData.SavedMapPos = (Vector2)Plugin.f_mapPosOffset.GetValue(__instance);
                APData.SavedMapFollow = (bool)Plugin.f_mapFollow.GetValue(__instance);
                APData.SavedMapZoom = __instance.GetZoomLevel();
                APData.MapStateStored = true;
            }
            catch (Exception ex) { Plugin.Logger.LogError($"SaveMapState Error: {ex.Message}"); }
        }
    }

    [HarmonyPatch(typeof(DynamicMap), "Maximize")]
    internal class MapLoadStatePatch
    {
        static void Postfix(DynamicMap __instance)
        {
            if (!APData.SaveMapState || !APData.MapStateStored || __instance == null) return;

            try
            {
                Plugin.f_mapPosOffset.SetValue(__instance, APData.SavedMapPos);
                Plugin.f_mapFollow.SetValue(__instance, APData.SavedMapFollow);
                __instance.SetZoomLevel(APData.SavedMapZoom);
                if (Plugin.f_onMapChanged != null)
                {
                    var delegateObj = Plugin.f_onMapChanged.GetValue(null) as Action;
                    delegateObj?.Invoke();
                }
            }
            catch (Exception ex) { Plugin.Logger.LogError($"LoadMapState Error: {ex.Message}"); }
        }
    }

    public class PIDController
    {
        public float Integral;
        private float _lastError, _lastMeasurement;
        private bool _initialized;

        public void Reset(float currentIntegral = 0f)
        {
            Integral = currentIntegral;
            _lastError = 0; _lastMeasurement = 0;
            _initialized = false;
        }

        public float Evaluate(float error, float measurement, float dt, float kp, float ki, float kd, float iLimit, bool useErrorDeriv = false, float? manualDeriv = null, float currentOutput = 0f, float limitThreshold = 0.95f)
        {
            if (dt <= 0f) return 0f;
            if (!_initialized)
            {
                _lastError = error;
                _lastMeasurement = measurement;
                _initialized = true;
            }

            bool saturated = Mathf.Abs(currentOutput) >= limitThreshold;
            bool sameDirection = Mathf.Sign(error) == Mathf.Sign(currentOutput);

            if (!(saturated && sameDirection))
            {
                Integral += error * dt * ki;
            }

            Integral = Mathf.Clamp(Integral, -iLimit, iLimit);

            float derivative = manualDeriv ?? (useErrorDeriv ? (error - _lastError) / dt : -(measurement - _lastMeasurement) / dt);

            _lastError = error;
            _lastMeasurement = measurement;

            return (error * kp) + Integral + (derivative * kd);
        }
    }

    internal sealed class ConfigurationManagerAttributes
    {
        public bool? Browsable;
        // public bool? HideDefaultButton;
        // public int? Order;
        public Action<ConfigEntryBase> CustomDrawer;
        public object ControllerName;
        public object ButtonIndex;
    }

    internal static class RewiredConfigManager
    {
        private static bool _isListening = false;
        private static ConfigEntryBase _targetEntry, _targetController, _targetIndex;

        public static ConfigEntry<string> BindRW(ConfigFile config, string category, string keyName, string description)
        {
            var cName = config.Bind("Hidden", keyName + "_CN", "", new ConfigDescription("", null, new ConfigurationManagerAttributes { Browsable = false }));
            var bIdx = config.Bind("Hidden", keyName + "_IX", -1, new ConfigDescription("", null, new ConfigurationManagerAttributes { Browsable = false }));
            return config.Bind(category, keyName, "", new ConfigDescription(description, null, new ConfigurationManagerAttributes
            {
                CustomDrawer = RewiredButtonDrawer,
                ControllerName = cName,
                ButtonIndex = bIdx
            }));
        }

        public static void Update()
        {
            if (!_isListening || ReInput.controllers == null) return;
            foreach (var c in ReInput.controllers.Joysticks) if (CheckController(c)) return;
            if (Input.GetKeyDown(KeyCode.Escape)) _isListening = false;
        }

        private static bool CheckController(Controller c)
        {
            if (c == null || !c.GetAnyButtonDown()) return false;
            for (int i = 0; i < c.buttonCount; i++)
            {
                if (c.GetButtonDown(i))
                {
                    string cName = c.name.Trim();
                    string bName = "Button " + i;

                    var elements = Traverse.Create(c).Field("KHksquAJKcDEUkNfJQjMANjDEBFB").GetValue<IList>();
                    if (elements != null && i < elements.Count)
                    {
                        var id = Traverse.Create(elements[i]).Property("elementIdentifier").GetValue<ControllerElementIdentifier>();
                        if (id != null) bName = id.name;
                    }

                    _targetEntry.BoxedValue = $"{cName} | {bName} | {i}";
                    if (_targetController != null) _targetController.BoxedValue = cName;
                    if (_targetIndex != null) _targetIndex.BoxedValue = i;
                    _isListening = false;
                    return true;
                }
            }
            return false;
        }

        public static void RewiredButtonDrawer(ConfigEntryBase entry)
        {
            if (_isListening && _targetEntry == entry)
            {
                if (GUILayout.Button("Listening... (Esc to cancel)", GUILayout.ExpandWidth(true))) _isListening = false;
            }
            else
            {
                string val = (string)entry.BoxedValue;
                if (GUILayout.Button(string.IsNullOrEmpty(val) ? "None - Click to bind (Rewired)" : val, GUILayout.ExpandWidth(true)))
                {
                    _isListening = true;
                    _targetEntry = entry;
                    var attr = entry.Description.Tags?.OfType<ConfigurationManagerAttributes>().FirstOrDefault();
                    _targetController = attr?.ControllerName as ConfigEntryBase;
                    _targetIndex = attr?.ButtonIndex as ConfigEntryBase;
                }
            }
        }

        public static void Reset()
        {
            _isListening = false;
            _targetEntry = null;
            _targetController = null;
            _targetIndex = null;
        }
    }

    public static class InputHelper
    {
        public static bool IsDown(ConfigEntry<string> rw) => PollRewired(rw, true);
        public static bool IsPressed(ConfigEntry<string> rw) => PollRewired(rw, false);

        private static bool PollRewired(ConfigEntry<string> rw, bool checkDown)
        {
            string b = rw?.Value;
            if (string.IsNullOrEmpty(b) || !b.Contains("|") || ReInput.controllers == null) return false;
            string[] p = b.Split('|');
            if (p.Length < 3 || !int.TryParse(p[2].Trim(), out int idx)) return false;
            string cName = p[0].Trim();

            foreach (var c in ReInput.controllers.Joysticks) if (c.name.Trim() == cName) return checkDown ? c.GetButtonDown(idx) : c.GetButton(idx);
            var k = ReInput.controllers.Keyboard;
            if (k != null && k.name.Trim() == cName) return checkDown ? k.GetButtonDown(idx) : k.GetButton(idx);
            return false;
        }
    }
}
