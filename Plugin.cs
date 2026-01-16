using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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

        // ap menu?
        public static ConfigEntry<KeyCode> MenuKey;
        private Rect _windowRect = new(50, 50, 227, 312);
        private bool _showMenu = false;

        private Vector2 _scrollPos;
        private bool _isResizing = false;
        private RectEdge _activeEdge = RectEdge.None;
        private enum RectEdge { None, Top, Bottom, Left, Right, TopLeft, TopRight, BottomLeft, BottomRight }

        private string _bufAlt = "";
        private string _bufClimb = "40";
        private string _bufRoll = "";
        private string _bufSpeed = "";
        private string _bufCourse = "";

        public static ConfigEntry<float> NavReachDistance, NavPassedDistance;
        public static ConfigEntry<string> ColorNav;
        public static ConfigEntry<bool> NavCycle;

        private GUIStyle _styleWindow;
        private GUIStyle _styleLabel;
        private GUIStyle _styleButton;
        private bool _stylesInitialized = false;

        public static ConfigEntry<float> UI_PosX, UI_PosY;
        public static ConfigEntry<float> UI_Width, UI_Height;
        private bool _firstWindowInit = true;

        private string _currentHoverTarget = "";
        private Vector2 _stationaryPos;
        private float _stationaryTimer = 0f;
        private bool _isTooltipVisible = false;
        private bool _wasShownForThisTarget = false;
        private readonly float _jitterThreshold = 5.0f;

        private float _dynamicLabelWidth = 60f;
        private readonly GUIContent _measuringContent = new();
        private readonly float buttonWidth = 40f;

        // Visuals
        public static ConfigEntry<string> ColorAPOn, ColorAPOff, ColorGood, ColorWarn, ColorCrit, ColorInfo;
        public static ConfigEntry<float> OverlayOffsetX, OverlayOffsetY, FuelSmoothing, FuelUpdateInterval;
        public static ConfigEntry<int> FuelWarnMinutes, FuelCritMinutes;
        public static ConfigEntry<bool> ShowExtraInfo;
        public static ConfigEntry<bool> ShowAPOverlay;
        public static ConfigEntry<bool> ShowGCASOff;
        public static ConfigEntry<bool> AltShowUnit;
        public static ConfigEntry<bool> DistShowUnit;
        public static ConfigEntry<bool> VertSpeedShowUnit;
        public static ConfigEntry<bool> SpeedShowUnit; // for future
        public static ConfigEntry<bool> AngleShowUnit;

        // Settings
        public static ConfigEntry<float> StickDeadzone;
        // public static ConfigEntry<float> DisengageDelay;
        public static ConfigEntry<float> ReengageDelay;
        public static ConfigEntry<bool> InvertRoll, InvertPitch;
        public static ConfigEntry<bool> Conf_InvertCourseRoll;

        // Auto Jammer
        public static ConfigEntry<bool> EnableAutoJammer;
        public static ConfigEntry<KeyCode> AutoJammerKey;
        public static ConfigEntry<float> AutoJammerThreshold;
        public static ConfigEntry<bool> AutoJammerRandom;
        public static ConfigEntry<float> AutoJammerMinDelay, AutoJammerMaxDelay;
        public static ConfigEntry<float> AutoJammerReleaseMin, AutoJammerReleaseMax;

        // Controls
        public static ConfigEntry<KeyCode> ToggleKey, UpKey, DownKey, BigUpKey, BigDownKey;
        public static ConfigEntry<KeyCode> ClimbRateUpKey, ClimbRateDownKey, BankLeftKey, BankRightKey, BankLevelKey, SpeedHoldKey;

        // Flight Values
        public static ConfigEntry<float> AltStep, BigAltStep, ClimbRateStep, BankStep, MinAltitude;

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
        public static ConfigEntry<KeyCode> ToggleGCASKey;
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

        internal static FieldInfo f_fuelLabel, f_fuelCapacity;
        internal static FieldInfo f_pilots, f_gearState, f_weaponManager; // f_radarAlt;

        internal static FieldInfo f_powerSupply, f_charge, f_maxCharge;

        internal static MethodInfo m_Fire, m_GetAccel;

        internal static Type t_JammingPod;

        private void Awake()
        {
            Logger = base.Logger;

            // Visuals
            ColorAPOn = Config.Bind("Visuals - Colors", "1. Color AP On", "#00FF00", "Green");
            ColorAPOff = Config.Bind("Visuals - Colors", "2. Color AP Off", "#ffffffff", "White");
            ColorGood = Config.Bind("Visuals - Colors", "3. Color Good", "#00FF00", "Green");
            ColorWarn = Config.Bind("Visuals - Colors", "4. Color Warning", "#FFFF00", "Yellow");
            ColorCrit = Config.Bind("Visuals - Colors", "5. Color Critical", "#FF0000", "Red");
            ColorInfo = Config.Bind("Visuals - Colors", "6. Color Info", "#00FFFF", "Cyan");
            ColorNav = Config.Bind("Visuals - Colors", "7. Navigation Color", "#ff00ffcc", "color for flight path lines.");
            OverlayOffsetX = Config.Bind("Visuals - Layout", "1. Stack Start X", 20f, "HUD Horizontal position");
            OverlayOffsetY = Config.Bind("Visuals - Layout", "2. Stack Start Y", -20f, "HUD Vertical position");
            ShowExtraInfo = Config.Bind("Visuals", "Show Fuel/AP Info", true, "Show extra info on Fuel Gauge");
            ShowAPOverlay = Config.Bind("Visuals", "Show AP Overlay", true, "Draw AP status text on the HUD. Turn off if you want, there's a window now.");
            ShowGCASOff = Config.Bind("Visuals", "Show GCAS OFF", true, "Show the GCAS OFF message");

            AltShowUnit = Config.Bind("Visuals - Units", "1. Show unit for alt", false, "(example) on: 10m, off: 10");
            DistShowUnit = Config.Bind("Visuals - Units", "2. Show unit for dist", true, "(example) on: 10km, off: 10");
            VertSpeedShowUnit = Config.Bind("Visuals - Units", "3. Show unit for vertical speed", false, "(example) on: 10m/s, off: 10");
            SpeedShowUnit = Config.Bind("Visuals - Units", "4. Show unit for speed", false, "(example) on: 10km/h, off: 10 (unused right now, no autothrottle yet)");
            AngleShowUnit = Config.Bind("Visuals - Units", "5. Show unit for angle", false, "on: 10°, off: 10");

            UI_PosX = Config.Bind("Visuals - UI", "1. Window Position X", -1f, "-1 = Auto Bottom Right, otherwise pixel value");
            UI_PosY = Config.Bind("Visuals - UI", "2. Window Position Y", -1f, "-1 = Auto Bottom Right, otherwise pixel value");
            UI_Width = Config.Bind("Visuals - UI", "3. Window Width", 227f, "Saved Width");
            UI_Height = Config.Bind("Visuals - UI", "4. Window Height", 312f, "Saved Height");

            FuelSmoothing = Config.Bind("Calculations", "1. Fuel Flow Smoothing", 0.1f, "Alpha value");
            FuelUpdateInterval = Config.Bind("Calculations", "2. Fuel Update Interval", 1.0f, "Seconds");
            FuelWarnMinutes = Config.Bind("Calculations", "3. Fuel Warning Time", 15, "Minutes");
            FuelCritMinutes = Config.Bind("Calculations", "4. Fuel Critical Time", 5, "Minutes");

            // Settings
            StickDeadzone = Config.Bind("Settings", "1. Stick Deadzone", 0.01f, "Threshold");
            // DisengageDelay = Config.Bind("Settings", "2. Disengage Delay", 10f, "Seconds of continuous input over deadzone before AP turns off");
            ReengageDelay = Config.Bind("Settings", "2. Reengage Delay", 0.4f, "Seconds to wait after stick release before AP resumes control");
            InvertRoll = Config.Bind("Settings", "3. Invert Roll", true, "Flip Roll");
            InvertPitch = Config.Bind("Settings", "4. Invert Pitch", true, "Flip Pitch");
            Conf_InvertCourseRoll = Config.Bind("Settings", "5. Invert Bank Direction", true, "Toggle if plane turns wrong way");

            // nav
            NavReachDistance = Config.Bind("Settings - Navigation", "1. Reach Distance", 2500f, "Distance in meters to consider a waypoint reached.");
            NavPassedDistance = Config.Bind("Settings - Navigation", "2. Passed Distance", 25000f, "Distance in meters after waypoint is behind plane to consider it reached");
            NavCycle = Config.Bind("Settings - Navigation", "3. Cycle wp", true, "On: cycles to next wp upon reaching wp, Off: Deletes wp upon reaching wp");

            // Auto Jammer
            EnableAutoJammer = Config.Bind("Auto Jammer", "1. Enable Auto Jammer", true, "Allow the feature");
            AutoJammerKey = Config.Bind("Auto Jammer", "2. Toggle Key", KeyCode.Slash, "Key to toggle jamming");
            AutoJammerThreshold = Config.Bind("Auto Jammer", "3. Energy Threshold", 0.99f, "Fire when energy > this %");
            AutoJammerRandom = Config.Bind("Auto Jammer", "4. Random Delay", true, "Add random delay");
            AutoJammerMinDelay = Config.Bind("Auto Jammer", "5. Delay Min", 0.02f, "Seconds");
            AutoJammerMaxDelay = Config.Bind("Auto Jammer", "6. Delay Max", 0.04f, "Seconds");
            AutoJammerReleaseMin = Config.Bind("Auto Jammer", "7. Release Delay Min", 0.02f, "Seconds");
            AutoJammerReleaseMax = Config.Bind("Auto Jammer", "8. Release Delay Max", 0.04f, "Seconds");

            // Controls
            ToggleKey = Config.Bind("Controls", "01. Toggle AP Key", KeyCode.Equals, "AP On/Off");
            UpKey = Config.Bind("Controls", "03. Altitude Up (Small)", KeyCode.UpArrow, "small increase");
            DownKey = Config.Bind("Controls", "04. Altitude Down (Small)", KeyCode.DownArrow, "small decrease");
            BigUpKey = Config.Bind("Controls", "05. Altitude Up (Big)", KeyCode.LeftArrow, "large increase");
            BigDownKey = Config.Bind("Controls", "06. Altitude Down (Big)", KeyCode.RightArrow, "large decrease");
            ClimbRateUpKey = Config.Bind("Controls", "07. Climb Rate Increase", KeyCode.PageUp, "Increase Max VS");
            ClimbRateDownKey = Config.Bind("Controls", "08. Climb Rate Decrease", KeyCode.PageDown, "Decrease Max VS");
            BankLeftKey = Config.Bind("Controls", "09. Bank Left", KeyCode.LeftBracket, "Roll/course Left");
            BankRightKey = Config.Bind("Controls", "10. Bank Right", KeyCode.RightBracket, "Roll/course right");
            BankLevelKey = Config.Bind("Controls", "11. Bank Level (Reset)", KeyCode.Quote, "Reset roll/clear course");
            SpeedHoldKey = Config.Bind("Controls", "12. Speed Hold Toggle", KeyCode.Semicolon, "speed hold/clear");

            // Flight Values
            AltStep = Config.Bind("Controls", "13. Altitude Increment (Small)", 0.1f, "Meters per tick");
            BigAltStep = Config.Bind("Controls", "14. Altitude Increment (Big)", 100f, "Meters per tick");
            ClimbRateStep = Config.Bind("Controls", "15. Climb Rate Step", 0.5f, "m/s per tick");
            BankStep = Config.Bind("Controls", "16. Bank Step", 0.5f, "Degrees per tick");
            MinAltitude = Config.Bind("Controls", "17. Minimum Target Altitude", 20f, "Safety floor");
            MenuKey = Config.Bind("Controls", "18. Menu Key", KeyCode.F8, "Open the Autopilot Menu");

            // Tuning
            DefaultMaxClimbRate = Config.Bind("Tuning - 0. Limits", "1. Default Max Climb Rate", 40f, "Startup value");
            Conf_VS_MaxAngle = Config.Bind("Tuning - 0. Limits", "2. Max Pitch Angle", 90.0f, "useless limit");
            DefaultCRLimit = Config.Bind("Tuning - 0. Limits", "3. Default course roll limit", 60.0f, "roll limit when turning in course/nav mode");

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
            ToggleGCASKey = Config.Bind("Auto GCAS", "2. Toggle GCAS Key", KeyCode.Backslash, "Turn Auto-GCAS on/off");
            GCAS_MaxG = Config.Bind("Auto GCAS", "3. Max G-Pull", 5.0f, "Assumed G-Force capability for calculation");
            GCAS_WarnBuffer = Config.Bind("Auto GCAS", "4. Warning Buffer", 20.0f, "Seconds warning before auto-pull");
            GCAS_AutoBuffer = Config.Bind("Auto GCAS", "5. Auto-Pull Buffer", 1.0f, "Safety margin seconds");
            GCAS_Deadzone = Config.Bind("Auto GCAS", "6. GCAS Deadzone", 0.5f, "GCAS override deadzone");
            GCAS_ScanRadius = Config.Bind("Auto GCAS", "7. Scan Radius", 2.0f, "Width of the spherecast.");
            GCAS_P = Config.Bind("GCAS PID", "1. GCAS P", 0.1f, "G Error -> Stick");
            GCAS_I = Config.Bind("GCAS PID", "2. GCAS I", 1.0f, "Builds pull over time");
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

                f_fuelCapacity = typeof(Aircraft).GetField("fuelCapacity", flags);
                f_pilots = typeof(Aircraft).GetField("pilots", flags);
                f_gearState = typeof(Aircraft).GetField("gearState", flags);
                f_weaponManager = typeof(Aircraft).GetField("weaponManager", flags);

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
            Logger.LogInfo("Unloading...");

            harmony?.UnpatchSelf();

            SceneManager.sceneUnloaded -= OnSceneUnloaded;

            APData.Reset();
            ControlOverridePatch.Reset();
            HUDVisualsPatch.Reset();
            HudPatch.Reset();
            MapInteractionPatch.Reset();
            MapWaypointPatch.Reset();

            Logger.LogInfo("Unloading complete.");
        }

        private void InitStyles()
        {
            _styleWindow = new GUIStyle(GUI.skin.window);

            _styleLabel = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft
            };

            _styleButton = new GUIStyle(GUI.skin.button);

            _stylesInitialized = true;
        }

        private void Update()
        {
            if (Input.GetKeyDown(MenuKey.Value))
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
                    Input.GetKey(UpKey.Value) || Input.GetKey(DownKey.Value) ||
                    Input.GetKey(BigUpKey.Value) || Input.GetKey(BigDownKey.Value) ||
                    Input.GetKey(ClimbRateUpKey.Value) || Input.GetKey(ClimbRateDownKey.Value) ||
                    Input.GetKey(BankLeftKey.Value) || Input.GetKey(BankRightKey.Value) || Input.GetKey(BankLevelKey.Value) || Input.GetKey(SpeedHoldKey.Value);

                if (isAdjusting)
                {
                    SyncMenuValues();
                }
            }
            if (Input.GetKeyDown(ToggleKey.Value))
            {
                APData.Enabled = !APData.Enabled;
                APData.TargetAlt = APData.CurrentAlt;
            }

            if (EnableAutoJammer.Value && Input.GetKeyDown(AutoJammerKey.Value))
            {
                APData.AutoJammerActive = !APData.AutoJammerActive;
            }

            if (Input.GetKeyDown(ToggleGCASKey.Value))
            {
                APData.GCASEnabled = !APData.GCASEnabled;
                if (!APData.GCASEnabled) { APData.GCASActive = false; APData.GCASWarning = false; }
            }
            if (Input.GetKeyDown(SpeedHoldKey.Value))
            {
                if (APData.TargetSpeed > 0)
                {
                    APData.TargetSpeed = -1f;
                    _bufSpeed = "";
                }
                else if (APData.PlayerRB != null)
                {
                    if (APData.SpeedHoldIsMach)
                    {
                        float currentAlt = (APData.LocalAircraft != null) ? APData.LocalAircraft.GlobalPosition().y : 0f;
                        float currentTAS = (APData.LocalAircraft != null) ? APData.LocalAircraft.speed : APData.PlayerRB.velocity.magnitude;
                        float sos = LevelInfo.GetSpeedofSound(currentAlt);

                        APData.TargetSpeed = currentTAS / sos;
                        _bufSpeed = APData.TargetSpeed.ToString("F2");
                    }
                    else
                    {
                        float currentTAS = (APData.LocalAircraft != null) ? APData.LocalAircraft.speed : APData.PlayerRB.velocity.magnitude;
                        APData.TargetSpeed = currentTAS;
                        _bufSpeed = ModUtils.ConvertSpeed_ToDisplay(currentTAS).ToString("F0");
                    }

                    APData.Enabled = true;
                }
                GUI.FocusControl(null);
            }
        }

        private void SyncMenuValues()
        {
            _bufAlt = (APData.TargetAlt > 0)
                ? ModUtils.ConvertAlt_ToDisplay(APData.TargetAlt).ToString("F0")
                : "";

            _bufClimb = (APData.CurrentMaxClimbRate > 0)
                ? ModUtils.ConvertVS_ToDisplay(APData.CurrentMaxClimbRate).ToString("F0")
                : DefaultMaxClimbRate.Value.ToString();

            _bufRoll = (APData.TargetRoll != -999f) ? APData.TargetRoll.ToString("F0") : "";

            if (APData.TargetSpeed > 0)
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
            if (!_showMenu) return;
            if (!_stylesInitialized) InitStyles();

            if (_isResizing)
            {
                if (Event.current.type == EventType.MouseUp) { _isResizing = false; _activeEdge = RectEdge.None; }
                else if (Event.current.type == EventType.MouseDrag)
                {
                    Vector2 delta = Event.current.delta;
                    float minW = 227f;
                    float minH = 312f;

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
                float h = Mathf.Max(312f, UI_Height.Value);

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

            _windowRect = GUI.Window(999, _windowRect, DrawAPWindow, "Autopilot controls", _styleWindow);

            float clampedX = Mathf.Clamp(_windowRect.x, 0, Screen.width - _windowRect.width);
            float clampedY = Mathf.Clamp(_windowRect.y, 0, Screen.height - _windowRect.height);

            if (clampedX != _windowRect.x || clampedY != _windowRect.y)
            {
                _windowRect.x = clampedX;
                _windowRect.y = clampedY;
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
            float sos = LevelInfo.GetSpeedofSound(currentAlt);
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

            float targetWidth = Mathf.Max(wAlt, wVS, wRoll, wSpd, wCrs);
            _dynamicLabelWidth = Mathf.Lerp(_dynamicLabelWidth, targetWidth, 0.15f);

            // float currentRollDefault = APData.TargetCourse == -1f ? 0f : DefaultCRLimit.Value;

            // altitude
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent($"{sAlt}", "Current altitude"), _styleLabel, GUILayout.Width(_dynamicLabelWidth));
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
            GUILayout.Label(new GUIContent($"{sVS}", "Current climb/descent rate"), _styleLabel, GUILayout.Width(_dynamicLabelWidth));
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
            GUILayout.Label(new GUIContent($"{sRoll}", "Current bank angle"), _styleLabel, GUILayout.Width(_dynamicLabelWidth));
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
            GUILayout.Label(new GUIContent($"{sSpd}", "Current speed"), _styleLabel, GUILayout.Width(_dynamicLabelWidth));
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
                        float mach = ms / sos;
                        _bufSpeed = mach.ToString("F2");
                        if (APData.TargetSpeed > 0) APData.TargetSpeed = mach;
                    }
                    else
                    {
                        float ms = val * sos;
                        float display = ModUtils.ConvertSpeed_ToDisplay(ms);
                        _bufSpeed = display.ToString("F0");
                        if (APData.TargetSpeed > 0) APData.TargetSpeed = ms;
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
            GUILayout.Label(new GUIContent($"{sCrs}", "Current course"), _styleLabel, GUILayout.Width(_dynamicLabelWidth));
            _bufCourse = GUILayout.TextField(_bufCourse);
            GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Target course"));
            if (GUILayout.Button(new GUIContent("CLR", "Disable course hold"), _styleButton, GUILayout.Width(buttonWidth)))
            {
                APData.TargetCourse = -1f; _bufCourse = "";
                APData.TargetRoll = 0;
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
                    APData.TargetRoll = r;
                }
                else
                {
                    if (APData.TargetCourse >= 0f)
                    {
                        APData.TargetRoll = DefaultCRLimit.Value;
                        _bufRoll = APData.TargetRoll.ToString("F0");
                    }
                    else
                    {
                        APData.TargetRoll = -999f;
                        _bufRoll = "";
                    }
                }

                APData.Enabled = true;
                APData.UseSetValues = true;
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
                        APData.TargetRoll = 0f;
                        _bufRoll = "0";
                    }

                    APData.UseSetValues = true;
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            // auto jam/gcas
            GUILayout.BeginHorizontal();
            string ajText = "AJ: " + (APData.AutoJammerActive ? "ON" : "OFF");
            if (GUILayout.Button(new GUIContent(ajText, "Toggle Auto Jammer"), _styleButton))
            {
                APData.AutoJammerActive = !APData.AutoJammerActive;
                GUI.FocusControl(null);
            }
            string gcasText = "GCAS: " + (APData.GCASEnabled ? "ON" : "OFF");
            if (GUILayout.Button(new GUIContent(gcasText, "Toggle Auto-GCAS"), _styleButton))
            {
                APData.GCASEnabled = !APData.GCASEnabled;
                if (!APData.GCASEnabled) APData.GCASActive = false;
                GUI.FocusControl(null);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // nav
            GUILayout.BeginHorizontal();
            APData.NavEnabled = GUILayout.Toggle(APData.NavEnabled, new GUIContent("Nav mode", "switch for waypoint ap mode. overrides course hold."));
            NavCycle.Value = GUILayout.Toggle(NavCycle.Value, new GUIContent("Cycle wp", "On: cycles to next wp upon reaching wp, Off: Deletes upon reaching wp"));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            // Nav waypoint list
            if (APData.NavQueue.Count > 0)
            {
                float rawSpeed = (APData.PlayerRB != null) ? APData.PlayerRB.velocity.magnitude : 0f;
                float alpha = 0.05f;
                APData.SpeedEma = (alpha * rawSpeed) + ((1f - alpha) * APData.SpeedEma);

                Vector3 playerPos = (APData.PlayerRB != null) ? APData.PlayerRB.position.ToGlobalPosition().AsVector3() : Vector3.zero;
                float distNext = Vector3.Distance(playerPos, APData.NavQueue[0]);

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

                    string totalDistStr = ModUtils.ProcessGameString(UnitConverter.DistanceReading(distTotal), Plugin.DistShowUnit.Value);
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
                    RefreshNavVisuals();
                }
                if (GUILayout.Button(new GUIContent("Clear all", "delete all points"), _styleButton))
                {
                    APData.NavQueue.Clear();
                    RefreshNavVisuals();
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("RMB the map to set wp.\nShift+RMB for multiple.", _styleLabel);
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();

            GUILayout.EndScrollView();

            if (Event.current.type != EventType.Repaint) return;

            string tooltipUnderMouse = GUI.tooltip;
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

                Rect tooltipRect = new(mousePos.x + 12, mousePos.y + 12, size.x, size.y);

                if (tooltipRect.xMax > _windowRect.width) tooltipRect.x = mousePos.x - size.x - 5;
                if (tooltipRect.yMax > _windowRect.height) tooltipRect.y = mousePos.y - size.y - 5;

                GUI.depth = 0;
                GUI.Box(tooltipRect, content, style);
            }
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
        public static float TargetAlt = -1f;
        public static float TargetRoll = -999f;
        public static float TargetSpeed = -1f;
        public static float TargetCourse = -1f;
        public static float CurrentAlt = 0f;
        public static float CurrentRoll = 0f;
        public static float CurrentMaxClimbRate = -1f;
        public static float SpeedEma = 0f;
        public static float LastPilotInputTime = -999f;
        public static object LocalPilot;
        public static List<Vector3> NavQueue = [];
        public static List<GameObject> NavVisuals = [];
        public static Transform PlayerTransform;
        public static Rigidbody PlayerRB;
        public static Aircraft LocalAircraft;
        public static WeaponManager LocalWeaponManager;

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
            TargetAlt = -1f;
            TargetRoll = -999f;
            TargetSpeed = -1f;
            TargetCourse = -1f;
            CurrentAlt = 0f;
            CurrentRoll = 0f;
            CurrentMaxClimbRate = -1f;
            SpeedEma = 0f;
            LastPilotInputTime = -999f;
            LocalPilot = null;
            PlayerTransform = null;
            PlayerRB = null;
            LocalAircraft = null;
            LocalWeaponManager = null;

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
                    APData.TargetAlt = altitude;
                    APData.TargetRoll = 0f;
                    APData.CurrentMaxClimbRate = -1f;
                    APData.LastPilotInputTime = -999f;
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
                }

                APData.CurrentRoll = APData.PlayerTransform.eulerAngles.z;
                if (APData.CurrentRoll > 180f) APData.CurrentRoll -= 360f;
            }
            catch (Exception ex) { Plugin.Logger.LogError($"[HudPatch] Error: {ex}"); }
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
        // private static float gcasNextScan = 0f;
        public static bool apStateBeforeGCAS = false;
        private static float currentAppliedThrottle = 0f;

        private static float jammerNextFireTime = 0f;
        private static float jammerNextReleaseTime = 0f;
        private static bool isJammerHoldingTrigger = false;

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
            // gcasNextScan = 0f;
            apStateBeforeGCAS = false;
            currentAppliedThrottle = 0f;

            jammerNextFireTime = 0f;
            jammerNextReleaseTime = 0f;
            isJammerHoldingTrigger = false;
        }

        private static void ResetIntegrators(float currentThrottle)
        {
            pidAlt.Reset();
            pidVS.Reset();
            pidAngle.Reset();
            pidRoll.Reset();
            pidGCAS.Reset();
            pidCrs.Reset();
            pidSpd.Reset(Mathf.Clamp01(currentThrottle));

            lastPitchOut = 0f;
            lastRollOut = 0f;
            lastThrottleOut = currentThrottle;
            lastBankReq = 0f;
            lastVSReq = 0f;
            lastAngleReq = 0f;

            isPitchSleeping = isRollSleeping = isSpdSleeping = false;
            pitchSleepUntil = rollSleepUntil = spdSleepUntil = 0f;
            currentAppliedThrottle = currentThrottle;
        }

        private static void Postfix(PilotPlayerState __instance)
        {
            if (APData.LocalAircraft == null || APData.PlayerRB == null || APData.PlayerTransform == null)
            {
                APData.Enabled = false;
                APData.GCASActive = false;
                return;
            }

            if (APData.CurrentMaxClimbRate < 0f) APData.CurrentMaxClimbRate = Plugin.DefaultMaxClimbRate.Value;

            if (Plugin.f_controlInputs == null || Plugin.f_pitch == null || Plugin.f_roll == null || Plugin.f_throttle == null) return;

            try
            {
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
                    }
                    wasEnabled = APData.Enabled;
                    APData.UseSetValues = false;
                }

                // can a plane have no pilot?
                if (APData.LocalPilot != null && Plugin.m_GetAccel != null)
                {
                    Vector3 pAccel = (Vector3)Plugin.m_GetAccel.Invoke(APData.LocalPilot, null);
                    currentG = Vector3.Dot(pAccel + Vector3.up, pUp);
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

                    if (Mathf.Abs(stickPitch) > Plugin.GCAS_Deadzone.Value || Mathf.Abs(stickRoll) > Plugin.GCAS_Deadzone.Value || gearDown)
                    {
                        if (APData.GCASActive)
                        {
                            APData.GCASActive = false;
                            APData.Enabled = false;
                        }
                    }
                    else
                    {
                        float speed = APData.PlayerRB.velocity.magnitude;
                        if (speed > 1f)
                        {
                            Vector3 velocity = APData.PlayerRB.velocity;
                            float descentRate = (velocity.y < 0) ? Mathf.Abs(velocity.y) : 0f;

                            float gAccel = Plugin.GCAS_MaxG.Value * 9.81f;
                            float turnRadius = speed * speed / gAccel;

                            float reactionTime = Plugin.GCAS_AutoBuffer.Value + (Time.deltaTime * 2.0f);
                            float reactionDist = speed * reactionTime;
                            float warnDist = speed * Plugin.GCAS_WarnBuffer.Value;

                            bool dangerImminent = false;
                            bool warningZone = false;
                            // bool isWallThreat = false;

                            // if (Time.time >= gcasNextScan)
                            // {
                            // gcasNextScan = Time.time + 0.02f;
                            Vector3 castStart = APData.PlayerRB.position + (velocity.normalized * 20f);
                            float scanRange = (turnRadius * 1.5f) + warnDist + 500f;

                            if (Physics.SphereCast(castStart, Plugin.GCAS_ScanRadius.Value, velocity.normalized, out RaycastHit hit, scanRange))
                            {
                                if (hit.transform.root != APData.PlayerTransform.root)
                                {
                                    float turnAngle = Mathf.Abs(Vector3.Angle(velocity, hit.normal) - 90f);
                                    float reqArc = turnRadius * (turnAngle * Mathf.Deg2Rad);

                                    if (hit.distance < (reqArc + reactionDist + 20f))
                                    {
                                        dangerImminent = true;
                                        // if (hit.normal.y < 0.7f) isWallThreat = true;
                                    }
                                    else if (hit.distance < (reqArc + reactionDist + warnDist))
                                    {
                                        warningZone = true;
                                    }
                                }
                            }

                            if (descentRate > 0.1f)
                            {
                                float diveAngle = Vector3.Angle(velocity, Vector3.ProjectOnPlane(velocity, Vector3.up));
                                float pullUpLoss = turnRadius * (1f - Mathf.Cos(diveAngle * Mathf.Deg2Rad));
                                float vertBuffer = descentRate * reactionTime;

                                if (APData.CurrentAlt < (pullUpLoss + vertBuffer)) dangerImminent = true;
                                else if (APData.CurrentAlt < (pullUpLoss + vertBuffer + (descentRate * Plugin.GCAS_WarnBuffer.Value))) warningZone = true;
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
                                    APData.Enabled = apStateBeforeGCAS;
                                }
                                else
                                {
                                    APData.GCASWarning = true;
                                    APData.TargetRoll = 0f;
                                    APData.TargetAlt = APData.CurrentAlt * 1.1f;
                                }
                            }
                            else if (dangerImminent)
                            {
                                apStateBeforeGCAS = APData.Enabled;
                                APData.Enabled = true;
                                APData.GCASActive = true;
                                APData.TargetRoll = 0f;
                            }
                            else if (warningZone)
                            {
                                APData.GCASWarning = true;
                            }
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

                bool pilotPitch = Mathf.Abs(stickPitch) > Plugin.StickDeadzone.Value;
                bool pilotRoll = Mathf.Abs(stickRoll) > Plugin.StickDeadzone.Value;

                if (pilotPitch || pilotRoll)
                {
                    APData.LastPilotInputTime = Time.time;
                }

                bool isWaitingToReengage = (Time.time - APData.LastPilotInputTime) < Plugin.ReengageDelay.Value;

                // autopilot
                if (APData.Enabled || APData.GCASActive)
                {
                    // keys
                    if (!pilotPitch && !pilotRoll && !APData.GCASActive)
                    {
                        if (Input.GetKey(Plugin.UpKey.Value)) APData.TargetAlt += Plugin.AltStep.Value;
                        if (Input.GetKey(Plugin.DownKey.Value)) APData.TargetAlt -= Plugin.AltStep.Value;
                        if (Input.GetKey(Plugin.BigUpKey.Value)) APData.TargetAlt += Plugin.BigAltStep.Value;
                        if (Input.GetKey(Plugin.BigDownKey.Value)) APData.TargetAlt = Mathf.Max(APData.TargetAlt - Plugin.BigAltStep.Value, Plugin.MinAltitude.Value);

                        if (Input.GetKey(Plugin.ClimbRateUpKey.Value)) APData.CurrentMaxClimbRate += Plugin.ClimbRateStep.Value;
                        if (Input.GetKey(Plugin.ClimbRateDownKey.Value)) APData.CurrentMaxClimbRate = Mathf.Max(0.5f, APData.CurrentMaxClimbRate - Plugin.ClimbRateStep.Value);

                        if (Input.GetKeyDown(Plugin.BankLevelKey.Value))
                        {
                            if (APData.TargetCourse >= 0f) { APData.TargetCourse = -1f; APData.TargetRoll = 0f; }
                            else { APData.TargetRoll = 0f; }
                        }

                        if (APData.TargetCourse >= 0f)
                        {
                            if (Input.GetKey(Plugin.BankLeftKey.Value)) APData.TargetCourse = Mathf.Repeat(APData.TargetCourse - Plugin.BankStep.Value, 360f);
                            if (Input.GetKey(Plugin.BankRightKey.Value)) APData.TargetCourse = Mathf.Repeat(APData.TargetCourse + Plugin.BankStep.Value, 360f);
                        }
                        else
                        {
                            if (Input.GetKey(Plugin.BankLeftKey.Value)) APData.TargetRoll = Mathf.Repeat(APData.TargetRoll + Plugin.BankStep.Value + 180f, 360f) - 180f;
                            if (Input.GetKey(Plugin.BankRightKey.Value)) APData.TargetRoll = Mathf.Repeat(APData.TargetRoll - Plugin.BankStep.Value + 180f, 360f) - 180f;
                        }
                    }

                    // throttle control
                    if (APData.TargetSpeed > 0f && Plugin.f_throttle != null)
                    {
                        float currentSpeed = (APData.LocalAircraft != null) ? APData.LocalAircraft.speed : APData.PlayerRB.velocity.magnitude;
                        float targetSpeedMS;

                        if (APData.SpeedHoldIsMach)
                        {
                            float currentAlt = APData.LocalAircraft.GlobalPosition().y;
                            float sos = LevelInfo.GetSpeedofSound(currentAlt);
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

                    if (APData.NavEnabled && APData.NavQueue.Count > 0)
                    {
                        Vector3 targetPos = APData.NavQueue[0];
                        Vector3 playerPos = APData.PlayerRB.position.ToGlobalPosition().AsVector3();
                        Vector3 diff = targetPos - playerPos;

                        float bearing = Mathf.Atan2(diff.x, diff.z) * Mathf.Rad2Deg;
                        APData.TargetCourse = (bearing + 360f) % 360f;

                        float distSq = new Vector2(diff.x, diff.z).sqrMagnitude;
                        bool passed = Vector3.Dot(pForward, diff.normalized) < 0;

                        float threshold = Plugin.NavReachDistance.Value;
                        float passedThreshold = Plugin.NavPassedDistance.Value;
                        if (distSq < (threshold * threshold) || (passed && distSq < passedThreshold * passedThreshold))
                        {
                            Vector3 reachedPoint = APData.NavQueue[0];
                            APData.NavQueue.RemoveAt(0);

                            if (Plugin.NavCycle.Value)
                            {
                                // Put the reached point at the back of the line
                                APData.NavQueue.Add(reachedPoint);
                            }

                            Plugin.RefreshNavVisuals();

                            // If we aren't cycling and ran out of points, turn off Nav
                            if (APData.NavQueue.Count == 0)
                            {
                                APData.NavEnabled = false;
                            }
                        }
                    }

                    // roll/course control
                    bool rollAxisActive = APData.GCASActive || APData.TargetCourse >= 0f || APData.TargetRoll != -999f;

                    if (rollAxisActive)
                    {
                        if (pilotRoll || isWaitingToReengage)
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
                        if (pilotPitch || isWaitingToReengage)
                        {
                            pidAlt.Reset();
                            pidVS.Reset();
                            pidAngle.Reset();
                            APData.TargetAlt = APData.CurrentAlt;
                        }
                        else
                        {
                            float pitchOut = 0f;

                            // gcas
                            if (APData.GCASActive)
                            {
                                float targetG = Plugin.GCAS_MaxG.Value;
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

                                    float possibleAccel = Plugin.GCAS_MaxG.Value;
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

        private static float lastFuelMass = 0f;
        private static float fuelFlowEma = 0f;
        private static float lastUpdateTime = 0f;

        private static FuelGauge _cachedFuelGauge;
        private static Text _cachedRefLabel;
        private static GameObject _lastVehicleChecked;

        public static void Reset()
        {
            if (infoOverlayObj != null)
            {
                UnityEngine.Object.Destroy(infoOverlayObj);
            }
            infoOverlayObj = null;
            overlayText = null;
            lastFuelMass = 0f;
            fuelFlowEma = 0f;
            lastUpdateTime = 0f;
            _cachedFuelGauge = null;
            _cachedRefLabel = null;
            _lastVehicleChecked = null;
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

                    if (_cachedFuelGauge != null)
                    {
                        _cachedRefLabel = (Text)Plugin.f_fuelLabel.GetValue(_cachedFuelGauge);
                    }
                }

                if (_cachedFuelGauge == null || _cachedRefLabel == null) return;

                if (!infoOverlayObj)
                {
                    infoOverlayObj = UnityEngine.Object.Instantiate(_cachedRefLabel.gameObject, __instance.transform);
                    infoOverlayObj.name = "AP_CombinedOverlay";

                    overlayText = infoOverlayObj.GetComponent<Text>();

                    overlayText.supportRichText = true;
                    overlayText.alignment = TextAnchor.UpperLeft;
                    overlayText.horizontalOverflow = HorizontalWrapMode.Overflow;
                    overlayText.verticalOverflow = VerticalWrapMode.Overflow;

                    infoOverlayObj.SetActive(true);
                }

                infoOverlayObj.transform.position = _cachedRefLabel.transform.position
                + (_cachedRefLabel.transform.right * Plugin.OverlayOffsetX.Value)
                + (_cachedRefLabel.transform.up * Plugin.OverlayOffsetY.Value);

                Aircraft aircraft = APData.LocalAircraft;
                if (aircraft == null) return;

                if (Plugin.f_fuelCapacity == null) return;
                float currentFuel = (float)Plugin.f_fuelCapacity.GetValue(aircraft) * aircraft.GetFuelLevel();
                float time = Time.time;
                if (lastUpdateTime != 0f && lastFuelMass > 0f)
                {
                    float dt = time - lastUpdateTime;
                    if (dt >= Plugin.FuelUpdateInterval.Value)
                    {
                        float burned = lastFuelMass - currentFuel;
                        float flow = Mathf.Max(0f, burned / dt);
                        fuelFlowEma = (Plugin.FuelSmoothing.Value * flow) + ((1f - Plugin.FuelSmoothing.Value) * fuelFlowEma);
                        lastUpdateTime = time;
                        lastFuelMass = currentFuel;
                    }
                }
                else { lastUpdateTime = time; lastFuelMass = currentFuel; }

                string content = "";

                if (currentFuel <= 1f)
                {
                    content += $"<color={Plugin.ColorCrit.Value}>TIME: 00:00\nRNG: -</color>\n";
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

                    content += $"<color={fuelCol}>{sTime}</color>\n";

                    float spd = (aircraft.rb != null) ? aircraft.rb.velocity.magnitude : 0f;
                    float distMeters = secs * spd;

                    if (distMeters > 99999000f) distMeters = 99999000f;

                    string sRange = ModUtils.ProcessGameString(UnitConverter.DistanceReading(distMeters), Plugin.DistShowUnit.Value);
                    content += $"<color={Plugin.ColorInfo.Value}>{sRange}</color>\n";
                }

                content += "\n";

                // (AP was on before GCAS) OR (AP is on and no GCAS)
                bool showAPInfo = (ControlOverridePatch.apStateBeforeGCAS && APData.GCASActive) || (APData.Enabled && !APData.GCASActive);

                if (showAPInfo && Plugin.ShowAPOverlay.Value)
                {
                    string altStr = (APData.TargetAlt > 0)
                        ? ModUtils.ProcessGameString(UnitConverter.AltitudeReading(APData.TargetAlt), Plugin.AltShowUnit.Value)
                        : "A-";
                    string climbStr = ModUtils.ProcessGameString(UnitConverter.ClimbRateReading(APData.CurrentMaxClimbRate), Plugin.VertSpeedShowUnit.Value);
                    string spdStr;
                    if (APData.TargetSpeed > 0)
                    {
                        if (APData.SpeedHoldIsMach)
                            spdStr = $"M{APData.TargetSpeed:F2}";
                        else
                            spdStr = ModUtils.ProcessGameString(UnitConverter.SpeedReading(APData.TargetSpeed), Plugin.SpeedShowUnit.Value);
                    }
                    else
                    {
                        spdStr = "S-";
                    }
                    string degUnit = Plugin.AngleShowUnit.Value ? "°" : "";
                    string degStr;

                    if (APData.TargetCourse >= 0)
                        degStr = $"C{APData.TargetCourse:F0}{degUnit}";
                    else if (APData.TargetRoll != -999f)
                        degStr = $"R{APData.TargetRoll:F0}{degUnit}";
                    else
                        degStr = "CR-";

                    string navStr = "";
                    if (APData.NavEnabled && APData.NavQueue.Count > 0)
                    {
                        float d = Vector3.Distance(APData.PlayerRB.position.ToGlobalPosition().AsVector3(), APData.NavQueue[0]);
                        navStr = "W>" + ModUtils.ProcessGameString(UnitConverter.DistanceReading(d), Plugin.DistShowUnit.Value);
                    }
                    else
                        navStr = "W-";

                    content += $"<color={Plugin.ColorAPOn.Value}>{altStr} {climbStr} {spdStr}\n{degStr} {navStr}</color>\n";
                }

                float overrideRemaining = Plugin.ReengageDelay.Value - (Time.time - APData.LastPilotInputTime);
                bool isOverridden = overrideRemaining > 0;
                if (APData.Enabled && isOverridden)
                {
                    content += $"<color={Plugin.ColorWarn.Value}>Override {overrideRemaining:F0}s</color>\n";
                }
                else if (APData.GCASActive)
                {
                    content += $"<color={Plugin.ColorCrit.Value}>A-GCAS</color>\n";
                }
                else if (APData.GCASWarning)
                {
                    content += $"<color={Plugin.ColorCrit.Value}>PULL UP</color>\n";
                }
                else if (!APData.GCASEnabled && Plugin.ShowGCASOff.Value)
                {
                    content += $"<color={Plugin.ColorWarn.Value}>GCAS-</color>\n";
                }

                if (APData.AutoJammerActive)
                {
                    content += $"<color={Plugin.ColorAPOn.Value}>AJ</color>";
                }

                overlayText.text = content;
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
            if (__instance.selectedIcons.Count > 0) return;

            if (DynamicMap.mapMaximized && Input.GetMouseButtonDown(1))
            {
                if (APData.LocalAircraft != null)
                {
                    if (!Input.GetKey(KeyCode.LeftShift)) APData.NavQueue.Clear();
                    APData.NavQueue.Add(__instance.GetCursorCoordinates().AsVector3());
                    APData.NavEnabled = true;
                    Plugin.RefreshNavVisuals();
                }
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
}
