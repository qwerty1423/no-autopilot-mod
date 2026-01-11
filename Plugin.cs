using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace AutopilotMod
{
    [BepInPlugin("com.qwerty1423.noautopilotmod", "NOAutopilotMod", "4.12.2")]
    public class Plugin : BaseUnityPlugin
    {
        internal new static ManualLogSource Logger;

        // ap menu?
        public static ConfigEntry<KeyCode> MenuKey;
        private Rect _windowRect = new(50, 50, 210, 210);
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
        public static ConfigEntry<float> FuelOffsetY, FuelLineSpacing, FuelSmoothing, FuelUpdateInterval;
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

        // Auto Jammer
        public static ConfigEntry<bool> EnableAutoJammer;
        public static ConfigEntry<KeyCode> AutoJammerKey;
        public static ConfigEntry<float> AutoJammerThreshold;
        public static ConfigEntry<bool> AutoJammerHumanize;
        public static ConfigEntry<float> AutoJammerMinDelay, AutoJammerMaxDelay;
        public static ConfigEntry<float> AutoJammerReleaseMin, AutoJammerReleaseMax;

        // Controls
        public static ConfigEntry<KeyCode> ToggleKey, UpKey, DownKey, BigUpKey, BigDownKey;
        public static ConfigEntry<KeyCode> ClimbRateUpKey, ClimbRateDownKey, BankLeftKey, BankRightKey, BankLevelKey, SpeedHoldKey;

        // Flight Values
        public static ConfigEntry<float> AltStep, BigAltStep, ClimbRateStep, BankStep, MinAltitude;

        // Tuning
        public static ConfigEntry<float> DefaultMaxClimbRate, Conf_VS_MaxAngle, DefaultCRLimit;
        
        // Loop 1 (Alt)
        public static ConfigEntry<float> Conf_Alt_P, Conf_Alt_I, Conf_Alt_D, Conf_Alt_ILimit;
        // Loop 2 (VS)
        public static ConfigEntry<float> Conf_VS_P, Conf_VS_I, Conf_VS_D, Conf_VS_ILimit;
        // Loop 3 (Angle)
        public static ConfigEntry<float> Conf_Angle_P, Conf_Angle_I, Conf_Angle_D, Conf_Angle_ILimit;
        // Roll
        public static ConfigEntry<float> RollP, RollI, RollD, RollILimit;
        public static ConfigEntry<bool> InvertRoll, InvertPitch;

        public static ConfigEntry<float> Conf_Spd_P, Conf_Spd_I, Conf_Spd_D, Conf_Spd_ILimit;
        public static ConfigEntry<float> ThrottleMinLimit, ThrottleMaxLimit, ThrottleSlewRate;
        public static ConfigEntry<float> Conf_Crs_P, Conf_Crs_I, Conf_Crs_D, Conf_Crs_ILimit;
        public static ConfigEntry<bool> Conf_InvertCourseRoll;

        // Auto GCAS
        public static ConfigEntry<bool> EnableGCAS;
        public static ConfigEntry<KeyCode> ToggleGCASKey;
        public static ConfigEntry<float> GCAS_MaxG, GCAS_WarnBuffer, GCAS_AutoBuffer, GCAS_Deadzone, GCAS_ScanRadius;
        public static ConfigEntry<float> GCAS_P, GCAS_I, GCAS_D, GCAS_ILimit;

        // Humanize
        public static ConfigEntry<bool> HumanizeEnabled;
        public static ConfigEntry<float> HumanizeStrength, HumanizeSpeed;
        public static ConfigEntry<float> Hum_Alt_Inner, Hum_Alt_Outer, Hum_Alt_Scale;
        public static ConfigEntry<float> Hum_VS_Inner, Hum_VS_Outer;
        public static ConfigEntry<float> Hum_PitchSleepMin, Hum_PitchSleepMax;
        public static ConfigEntry<float> Hum_Roll_Inner, Hum_Roll_Outer, Hum_RollRate_Inner, Hum_RollRate_Outer;
        public static ConfigEntry<float> Hum_RollSleepMin, Hum_RollSleepMax;
        public static ConfigEntry<float> Hum_Spd_Inner, Hum_Spd_Outer;
        public static ConfigEntry<float> Hum_Spd_SleepMin, Hum_Spd_SleepMax;
        public static ConfigEntry<float> Hum_Acc_Inner, Hum_Acc_Outer;

        // --- CACHED REFLECTION FIELDS ---
        internal static FieldInfo f_playerVehicle;
        internal static FieldInfo f_controlInputs; 
        internal static FieldInfo f_pitch, f_roll; 
        internal static FieldInfo f_throttle;
        internal static FieldInfo f_targetList;
        internal static FieldInfo f_currentWeaponStation;
        internal static FieldInfo f_stationWeapons;
        
        internal static FieldInfo f_fuelCapacity, f_pilots, f_gearState, f_weaponManager; // f_radarAlt;
        
        internal static FieldInfo f_powerSupply, f_charge, f_maxCharge;
        internal static MethodInfo m_Fire, m_GetAccel;
        
        internal static Type t_JammingPod;

        private void Awake()
        {
            Plugin.Logger = base.Logger;
            
            // Visuals
            ColorAPOn = Config.Bind("Visuals - Colors", "1. Color AP On", "#00FF00", "Green");
            ColorAPOff = Config.Bind("Visuals - Colors", "2. Color AP Off", "#ffffffff", "White");
            ColorGood = Config.Bind("Visuals - Colors", "3. Color Good", "#00FF00", "Green");
            ColorWarn = Config.Bind("Visuals - Colors", "4. Color Warning", "#FFFF00", "Yellow");
            ColorCrit = Config.Bind("Visuals - Colors", "5. Color Critical", "#FF0000", "Red");
            ColorInfo = Config.Bind("Visuals - Colors", "6. Color Info", "#00FFFF", "Cyan");
            FuelOffsetY = Config.Bind("Visuals - Layout", "1. Stack Start Y", -20f, "Vertical position");
            FuelLineSpacing = Config.Bind("Visuals - Layout", "2. Line Spacing", 20f, "Vertical gap");
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
            UI_Width = Config.Bind("Visuals - UI", "3. Window Width", 210f, "Saved Width");
            UI_Height = Config.Bind("Visuals - UI", "4. Window Height", 210f, "Saved Height");

            FuelSmoothing = Config.Bind("Calculations", "1. Fuel Flow Smoothing", 0.1f, "Alpha value");
            FuelUpdateInterval = Config.Bind("Calculations", "2. Fuel Update Interval", 1.0f, "Seconds");
            FuelWarnMinutes = Config.Bind("Calculations", "3. Fuel Warning Time", 15, "Minutes");
            FuelCritMinutes = Config.Bind("Calculations", "4. Fuel Critical Time", 5, "Minutes");

            // Settings
            StickDeadzone = Config.Bind("Settings", "1. Stick Deadzone", 0.5f, "Threshold");
            InvertRoll = Config.Bind("Settings", "2. Invert Roll", true, "Flip Roll");
            InvertPitch = Config.Bind("Settings", "3. Invert Pitch", true, "Flip Pitch");

            // Auto Jammer
            EnableAutoJammer = Config.Bind("Auto Jammer", "1. Enable Auto Jammer", true, "Allow the feature");
            AutoJammerKey = Config.Bind("Auto Jammer", "2. Toggle Key", KeyCode.Slash, "Key to toggle jamming");
            AutoJammerThreshold = Config.Bind("Auto Jammer", "3. Energy Threshold", 0.99f, "Fire when energy > this %");
            AutoJammerHumanize = Config.Bind("Auto Jammer", "4. Humanize Delay", true, "Add random delay");
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
            DefaultCRLimit = Config.Bind("Tuning - 0. Limits", "3. Default course roll limit", 45.0f, "self explanatory?");

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
            ThrottleMinLimit = Config.Bind("Tuning - 4. Speed", "6. Safe Min Throttle", 0.01f, "Minimum throttle when limiter is active (prevents Airbrake)");
            ThrottleMaxLimit = Config.Bind("Tuning - 4. Speed", "7. Safe Max Throttle", 0.89f, "Maximum throttle when limiter is active (prevents Afterburner)");
            ThrottleSlewRate = Config.Bind("Tuning - 4. Speed", "8. Throttle Slew Rate Limit", 0.2f, "in unit of throttle bars per second");
            Conf_Crs_P = Config.Bind("Tuning - 5. Course", "1. Course P", 1.0f, "Course Error -> Bank Angle");
            Conf_Crs_I = Config.Bind("Tuning - 5. Course", "2. Course I", 1.0f, "Correction");
            Conf_Crs_D = Config.Bind("Tuning - 5. Course", "3. Course D", 0.1f, "Dampen");
            Conf_Crs_ILimit = Config.Bind("Tuning - 5. Course", "4. Course I Limit", 70.0f, "Max Integral Bank");
            Conf_InvertCourseRoll = Config.Bind("Tuning - 5. Course", "5. Invert Bank Direction", true, "Toggle this if the plane turns the wrong way");
            
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
            Hum_Spd_Inner = Config.Bind("Settings - Humanize", "17. Speed Tolerance Inner", 0.5f, "Start Sleeping (m/s error)");
            Hum_Spd_Outer = Config.Bind("Settings - Humanize", "18. Speed Tolerance Outer", 2.0f, "Wake Up (m/s error)");
            Hum_Spd_SleepMin = Config.Bind("Settings - Humanize", "19. Speed Sleep Min", 2.0f, "Seconds");
            Hum_Spd_SleepMax = Config.Bind("Settings - Humanize", "20. Speed Sleep Max", 60.0f, "Seconds");
            Hum_Acc_Inner = Config.Bind("Settings - Humanize", "21. Accel Tolerance Inner", 0.05f, "Start Sleeping (m/s² acceleration)");
            Hum_Acc_Outer = Config.Bind("Settings - Humanize", "22. Accel Tolerance Outer", 0.5f, "Wake Up (m/s² acceleration)");

            // reflection cache
            try {
                f_playerVehicle = typeof(FlightHud).GetField("playerVehicle", BindingFlags.NonPublic | BindingFlags.Instance);
                
                f_controlInputs = typeof(PilotPlayerState).GetField("controlInputs", BindingFlags.NonPublic | BindingFlags.Instance);
                if (f_controlInputs == null && typeof(PilotPlayerState).BaseType != null) {
                    f_controlInputs = typeof(PilotPlayerState).BaseType.GetField("controlInputs", BindingFlags.NonPublic | BindingFlags.Instance);
                }
                
                if (f_controlInputs != null) {
                    Type inputType = f_controlInputs.FieldType;
                    BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                    f_pitch = inputType.GetField("pitch", flags);
                    f_roll = inputType.GetField("roll", flags);
                    f_throttle = inputType.GetField("throttle", flags);
                }

                BindingFlags allFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                
                f_fuelCapacity = typeof(Aircraft).GetField("fuelCapacity", allFlags);
                f_pilots = typeof(Aircraft).GetField("pilots", allFlags);
                f_gearState = typeof(Aircraft).GetField("gearState", allFlags);
                f_weaponManager = typeof(Aircraft).GetField("weaponManager", allFlags);
                // f_radarAlt = typeof(Aircraft).GetField("radarAlt", allFlags); 

                Type psType = typeof(Aircraft).Assembly.GetType("PowerSupply");
                if (psType != null) {
                    f_charge = psType.GetField("charge", allFlags);
                    f_maxCharge = psType.GetField("maxCharge", allFlags);
                    f_powerSupply = typeof(Aircraft).GetField("powerSupply", allFlags);
                }

                Type pilotType = typeof(Aircraft).Assembly.GetType("Pilot");
                if (pilotType != null) {
                    m_GetAccel = pilotType.GetMethod("GetAccel");
                }

                Type wmType = typeof(Aircraft).Assembly.GetType("WeaponManager");
                if (wmType != null) {
                    m_Fire = wmType.GetMethod("Fire", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                    f_targetList = wmType.GetField("targetList", BindingFlags.NonPublic | BindingFlags.Instance);
                    f_currentWeaponStation = wmType.GetField("currentWeaponStation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                Type wsType = typeof(Aircraft).Assembly.GetType("WeaponStation");
                if (wsType != null) {
                    f_stationWeapons = wsType.GetField("Weapons", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                t_JammingPod = typeof(Aircraft).Assembly.GetType("JammingPod");

            } catch (Exception ex) {
                Logger.LogError("Failed to cache reflection fields! Mod might break. " + ex);
            }

            new Harmony("com.qwerty1423.noautopilotmod").PatchAll();
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
            if (Input.GetKeyDown(Plugin.ToggleKey.Value))
            {
                APData.Enabled = !APData.Enabled;
            }

            if (Plugin.EnableAutoJammer.Value && Input.GetKeyDown(Plugin.AutoJammerKey.Value))
            {
                APData.AutoJammerActive = !APData.AutoJammerActive;
            }
            
            if (Input.GetKeyDown(Plugin.ToggleGCASKey.Value))
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
                    float currentSpeedRaw = APData.PlayerRB.velocity.magnitude;
                    APData.TargetSpeed = currentSpeedRaw;
                    _bufSpeed = ModUtils.ConvertSpeed_ToDisplay(currentSpeedRaw).ToString("F0");
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

            _bufSpeed = (APData.TargetSpeed > 0) 
                ? ModUtils.ConvertSpeed_ToDisplay(APData.TargetSpeed).ToString("F0") 
                : "";

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
                    float minW = 100f;
                    float minH = 100f;

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
                float w = Mathf.Max(100f, UI_Width.Value);
                float h = Mathf.Max(100f, UI_Height.Value);

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
            float currentSpeed = (APData.PlayerRB != null) ? APData.PlayerRB.velocity.magnitude : 0f;
            float currentCourse = 0f;
            if (APData.PlayerRB != null && APData.PlayerRB.velocity.sqrMagnitude > 1f) {
                Vector3 flatVel = Vector3.ProjectOnPlane(APData.PlayerRB.velocity, Vector3.up);
                currentCourse = Quaternion.LookRotation(flatVel).eulerAngles.y;
            }

            string sAlt = ModUtils.ProcessGameString(UnitConverter.AltitudeReading(APData.CurrentAlt), true);
            string sVS = ModUtils.ProcessGameString(UnitConverter.ClimbRateReading(currentVS), true);
            string sRoll = $"{APData.CurrentRoll:F0}°";
            string sSpd = ModUtils.ProcessGameString(UnitConverter.SpeedReading(currentSpeed), true);
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
            
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{sAlt}", _styleLabel, GUILayout.Width(_dynamicLabelWidth));
            _bufAlt = GUILayout.TextField(_bufAlt);
            GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Target altitude"));
            
            if (GUILayout.Button(new GUIContent("CLR", "disable alt hold"), _styleButton, GUILayout.Width(buttonWidth)))
            {
                APData.TargetAlt = -1f; _bufAlt = "";
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{sVS}", _styleLabel, GUILayout.Width(_dynamicLabelWidth));
            _bufClimb = GUILayout.TextField(_bufClimb);
            GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Max vertical speed"));

            if (GUILayout.Button(new GUIContent("RST", "Reset to default"), _styleButton, GUILayout.Width(buttonWidth))) 
            {
                APData.CurrentMaxClimbRate = DefaultMaxClimbRate.Value;
                _bufClimb = ModUtils.ConvertVS_ToDisplay(APData.CurrentMaxClimbRate).ToString("F0");
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{sRoll}", _styleLabel, GUILayout.Width(_dynamicLabelWidth));
            _bufRoll = GUILayout.TextField(_bufRoll);
            GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Target bank angle"));

            if (GUILayout.Button(new GUIContent("CLR", "disable roll hold"), _styleButton, GUILayout.Width(buttonWidth))) 
            {
                APData.TargetRoll = -999f; _bufRoll = "";
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{sSpd}", _styleLabel, GUILayout.Width(_dynamicLabelWidth));
            _bufSpeed = GUILayout.TextField(_bufSpeed);
            GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Target speed"));
            Color oldCol = GUI.backgroundColor;
            if (APData.AllowExtremeThrottle) GUI.backgroundColor = Color.red; 

            string limitText = APData.AllowExtremeThrottle ? "AB1" : "AB0";
            if (GUILayout.Button(new GUIContent(limitText, "Toggle afterburner/airbrake"), _styleButton, GUILayout.Width(buttonWidth))) {
                APData.AllowExtremeThrottle = !APData.AllowExtremeThrottle;
                GUI.FocusControl(null);
            }
            GUI.backgroundColor = oldCol;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{currentCourse:F0}°", _styleLabel, GUILayout.Width(_dynamicLabelWidth));
            _bufCourse = GUILayout.TextField(_bufCourse);
            GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Target course"));
            if (GUILayout.Button(new GUIContent("CLR", "Disable course hold"), _styleButton, GUILayout.Width(buttonWidth))) {
                APData.TargetCourse = -1f; _bufCourse = "";
                APData.TargetRoll = 0;
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Set Values", "Applies typed values"), _styleButton))
            {
                if (float.TryParse(_bufSpeed, out float s)) 
                    APData.TargetSpeed = ModUtils.ConvertSpeed_FromDisplay(s);
                else 
                    APData.TargetSpeed = -1f;

                if (float.TryParse(_bufAlt, out float a)) 
                    APData.TargetAlt = ModUtils.ConvertAlt_FromDisplay(a);
                else 
                    APData.TargetAlt = -1f;

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
                    if (APData.TargetCourse >= 0f) {
                        APData.TargetRoll = DefaultCRLimit.Value; 
                        _bufRoll = APData.TargetRoll.ToString("F0");
                    } else {
                        APData.TargetRoll = -999f;
                        _bufRoll = "";
                    }
                }

                if (float.TryParse(_bufClimb, out float c)) 
                    APData.CurrentMaxClimbRate = Mathf.Max(0.5f, ModUtils.ConvertVS_FromDisplay(c));

                APData.Enabled = true;
                APData.UseSetValues = true;
                GUI.FocusControl(null);
            }

            GUI.backgroundColor = APData.Enabled ? Color.green : Color.red;
            if (GUILayout.Button(new GUIContent(APData.Enabled ? "Disengage" : "Engage", "toggle AP"), _styleButton))
            {
                APData.Enabled = !APData.Enabled;
                if (APData.Enabled)
                {
                    if (string.IsNullOrEmpty(_bufAlt)) {
                        APData.TargetAlt = APData.CurrentAlt;
                        _bufAlt = ModUtils.ConvertAlt_ToDisplay(APData.TargetAlt).ToString("F0");
                    }
                    
                    if (string.IsNullOrEmpty(_bufCourse) && string.IsNullOrEmpty(_bufRoll)) {
                        APData.TargetRoll = 0f;
                        _bufRoll = "0";
                    }
                    
                    APData.UseSetValues = true; 
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

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
    }

    // --- SHARED DATA ---
    public static class APData
    {
        public static bool Enabled = false;
        public static bool UseSetValues = false;
        public static bool GCASEnabled = true;
        public static bool AutoJammerActive = false;
        public static bool GCASActive = false; 
        public static bool GCASWarning = false;
        public static bool AllowExtremeThrottle = false;
        public static float TargetAlt = -1f;
        public static float TargetRoll = -999f;
        public static float TargetSpeed = -1f;
        public static float TargetCourse = -1f;
        public static float CurrentAlt = 0f;
        public static float CurrentRoll = 0f;
        public static float CurrentMaxClimbRate = -1f; 
        public static Rigidbody PlayerRB;
        public static Aircraft LocalAircraft;
        public static object LocalPilot;
        public static WeaponManager LocalWeaponManager;
    }

    // --- HELPERS ---
    public static class ModUtils
    {
        private static readonly Regex _rxSpaces = new(@"\s+");
        private static readonly Regex _rxDecimals = new(@"[\.]\d+");
        private static readonly Regex _rxNumber = new(@"-?\d+");

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

        public static float ConvertSpeed_ToDisplay(float ms) {
            return (PlayerSettings.unitSystem == PlayerSettings.UnitSystem.Imperial) ? ms * 1.94384f : ms * 3.6f;
        }
        public static float ConvertSpeed_FromDisplay(float val) {
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
    }

    // --- PATCHES ---

    [HarmonyPatch(typeof(FlightHud), "SetHUDInfo")]
    internal class HudPatch
    {
        private static GameObject lastVehicleObj;

        private static void Postfix(object playerVehicle, float altitude)
        {
            try {
                APData.CurrentAlt = altitude; 
                if (playerVehicle != null)
                {
                    if (playerVehicle is not Component v) return;
                    if (v.gameObject != lastVehicleObj)
                    {
                        lastVehicleObj = v.gameObject;
                        APData.Enabled = false;
                        APData.UseSetValues = false;
                        APData.GCASEnabled = Plugin.EnableGCAS.Value;
                        APData.AutoJammerActive = false;
                        APData.GCASActive = false;
                        APData.GCASWarning = false;
                        APData.TargetAlt = altitude;
                        APData.TargetRoll = 0f;
                        APData.CurrentMaxClimbRate = -1f;
                        APData.LocalAircraft = v.GetComponent<Aircraft>();
                        APData.LocalPilot = null;
                        APData.LocalWeaponManager = null;
                        if (APData.LocalAircraft != null && Plugin.f_weaponManager != null) {
                            APData.LocalWeaponManager = Plugin.f_weaponManager.GetValue(APData.LocalAircraft) as WeaponManager;
                        }
                        if (APData.LocalAircraft != null && Plugin.f_pilots != null)
                        {
                            IList pilots = (IList)Plugin.f_pilots.GetValue(APData.LocalAircraft);
                            if (pilots != null && pilots.Count > 0)
                            {
                                APData.LocalPilot = pilots[0];
                            }
                        }
                    }

                    APData.CurrentRoll = v.transform.eulerAngles.z;
                    if (APData.CurrentRoll > 180f) APData.CurrentRoll -= 360f;

                    var rb = v.GetComponent<Rigidbody>();
                    if (APData.PlayerRB != rb) APData.PlayerRB = rb;
                }
            } catch (Exception ex) {
                Plugin.Logger.LogError($"[HudPatch] Error: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(PilotPlayerState), "PlayerAxisControls")]
    internal class ControlOverridePatch
    {
        // PID States
        private static float altIntegral = 0f;
        private static float vsIntegral = 0f;
        private static float angleIntegral = 0f;
        private static float rollIntegral = 0f;
        private static float gcasIntegral = 0f;
        private static float spdIntegral = 0f;
        private static float crsIntegral = 0f;

        // Derivative States
        private static float lastAltMeasurement = 0f;
        private static float lastSpdMeasurement = 0f;
        private static float lastVSError = 0f;
        private static float lastGError = 0f;
        private static float lastSpdError = 0f;
        private static float lastCrsError = 0f;
                
        // other states
        private static bool wasEnabled = false;
        private static float pitchSleepUntil = 0f;
        private static float rollSleepUntil = 0f;
        private static float spdSleepUntil = 0f;
        private static bool isPitchSleeping = false;
        private static bool isRollSleeping = false;
        private static bool isSpdSleeping = false;
        // private static float gcasNextScan = 0f;
        private static float currentAppliedThrottle = 0f;

        // jammer
        private static float jammerNextFireTime = 0f;
        private static float jammerNextReleaseTime = 0f;
        private static bool isJammerHoldingTrigger = false;

        private static void ResetIntegrators(float currentThrottle)
        {
            altIntegral = vsIntegral = angleIntegral = rollIntegral = gcasIntegral = crsIntegral = 0f;
            spdIntegral = Mathf.Clamp01(currentThrottle);
            lastAltMeasurement = (APData.PlayerRB != null) ? APData.CurrentAlt : 0f;
            lastSpdMeasurement = (APData.PlayerRB != null) ? APData.PlayerRB.velocity.magnitude : 0f;
            lastSpdError = 0f;
            lastVSError = 0f;
            lastCrsError = 0f;
            isPitchSleeping = isRollSleeping = isSpdSleeping = false;
            spdSleepUntil = 0f;
            currentAppliedThrottle = currentThrottle;
        }

        private static void Postfix(PilotPlayerState __instance)
        {
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

                if (APData.Enabled != wasEnabled)
                {
                    if (APData.Enabled)
                    {
                        if (!APData.GCASActive && !APData.UseSetValues) 
                        {
                            APData.TargetAlt = APData.CurrentAlt;
                            APData.TargetRoll = 0f;
                        }
                        ResetIntegrators(currentThrottle);
                    }
                    wasEnabled = APData.Enabled;
                    APData.UseSetValues = false;
                }

                APData.GCASWarning = false;

                float currentG = 1f;
                Aircraft acRef = null;

                if (APData.PlayerRB != null) {
                    acRef = APData.LocalAircraft;
                    if (acRef != null) {
                        if (APData.LocalPilot != null && Plugin.m_GetAccel != null) {
                            Vector3 pAccel = (Vector3)Plugin.m_GetAccel.Invoke(APData.LocalPilot, null);
                            currentG = Vector3.Dot(pAccel + Vector3.up, acRef.transform.up);
                        }
                    }
                }

                // gcas
                if (APData.GCASEnabled && APData.PlayerRB != null)
                {
                    bool gearDown = false;
                    if (acRef != null && Plugin.f_gearState != null) {
                        object gs = Plugin.f_gearState.GetValue(acRef);
                        if (gs != null && !gs.ToString().Contains("LockedRetracted")) gearDown = true;
                    }

                    if (Mathf.Abs(stickPitch) > Plugin.GCAS_Deadzone.Value || Mathf.Abs(stickRoll) > Plugin.GCAS_Deadzone.Value || gearDown)
                    {
                        if (APData.GCASActive) {
                            APData.GCASActive = false;
                            APData.Enabled = false;
                        }
                    }
                    else
                    {
                        float speed = APData.PlayerRB.velocity.magnitude;
                        if (speed > 15f)
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
                            //     gcasNextScan = Time.time + 0.05f;
                            Vector3 castStart = APData.PlayerRB.position + (velocity.normalized * 20f); 
                            float scanRange = (turnRadius * 1.5f) + warnDist + 500f;

                            if (Physics.SphereCast(castStart, Plugin.GCAS_ScanRadius.Value, velocity.normalized, out RaycastHit hit, scanRange))
                            {
                                if (hit.transform.root != APData.PlayerRB.transform.root)
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
                            // }

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
                                    APData.Enabled = false;
                                }
                                else
                                {
                                    APData.GCASWarning = true;
                                    APData.TargetRoll = 0f;
                                }
                            }
                            else if (dangerImminent)
                            {
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
                if (APData.AutoJammerActive && APData.LocalAircraft != null)
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
                                                jammerNextFireTime = Time.time + (Plugin.AutoJammerHumanize.Value ? UnityEngine.Random.Range(Plugin.AutoJammerMinDelay.Value, Plugin.AutoJammerMaxDelay.Value) : 0f);
                                            
                                            if (Time.time >= jammerNextFireTime) { isJammerHoldingTrigger = true; jammerNextFireTime = 0f; }
                                        }
                                    } 
                                    else 
                                    {
                                        if (isJammerHoldingTrigger) 
                                        {
                                            if (jammerNextReleaseTime == 0f) 
                                                jammerNextReleaseTime = Time.time + (Plugin.AutoJammerHumanize.Value ? UnityEngine.Random.Range(Plugin.AutoJammerReleaseMin.Value, Plugin.AutoJammerReleaseMax.Value) : 0f);
                                            
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

                // autopilot
                if (APData.Enabled || APData.GCASActive)
                {
                    if (APData.PlayerRB == null)
                    {
                        APData.Enabled = false;
                        APData.GCASActive = false;
                        return;
                    }

                    bool pilotPitch = Mathf.Abs(stickPitch) > Plugin.StickDeadzone.Value;
                    bool pilotRoll = Mathf.Abs(stickRoll) > Plugin.StickDeadzone.Value;

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

                    float dt = Mathf.Max(Time.deltaTime, 0.0001f);
                    float noiseT = Time.time * Plugin.HumanizeSpeed.Value;
                    bool useHumanize = Plugin.HumanizeEnabled.Value && !APData.GCASActive;

                    // throttle control
                    if (APData.TargetSpeed > 0f && Plugin.f_throttle != null && !APData.GCASActive) 
                    {
                        float currentSpeed = APData.PlayerRB.velocity.magnitude;
                        float sErr = APData.TargetSpeed - currentSpeed;
                        
                        float currentAccel = Mathf.Abs(currentSpeed - lastSpdMeasurement) / dt;
                        lastSpdMeasurement = currentSpeed;

                        if (useHumanize) 
                        {
                            float sErrAbs = Mathf.Abs(sErr);
                            if (!isSpdSleeping) {
                                if (sErrAbs < Plugin.Hum_Spd_Inner.Value && currentAccel < Plugin.Hum_Acc_Inner.Value) {
                                    spdSleepUntil = Time.time + UnityEngine.Random.Range(Plugin.Hum_Spd_SleepMin.Value, Plugin.Hum_Spd_SleepMax.Value);
                                    isSpdSleeping = true;
                                }
                            } else if (sErrAbs > Plugin.Hum_Spd_Outer.Value || currentAccel > Plugin.Hum_Acc_Outer.Value || Time.time > spdSleepUntil) {
                                isSpdSleeping = false;
                            }
                        }

                        float desiredLeverPos;
                        if (isSpdSleeping) 
                        {
                            desiredLeverPos = spdIntegral; 
                        } 
                        else 
                        {
                            spdIntegral += sErr * dt * Plugin.Conf_Spd_I.Value;
                            
                            float minT = APData.AllowExtremeThrottle ? 0f : Plugin.ThrottleMinLimit.Value;
                            float maxT = APData.AllowExtremeThrottle ? 1f : Plugin.ThrottleMaxLimit.Value;
                            spdIntegral = Mathf.Clamp(spdIntegral, minT, maxT);

                            float pTerm = sErr * Plugin.Conf_Spd_P.Value;
                            float dTerm = (sErr - lastSpdError) / dt * Plugin.Conf_Spd_D.Value;
                            
                            desiredLeverPos = Mathf.Clamp(spdIntegral + pTerm + dTerm, minT, maxT);
                            lastSpdError = sErr;
                        }
                        currentAppliedThrottle = Mathf.MoveTowards(
                            currentAppliedThrottle, 
                            desiredLeverPos, 
                            Plugin.ThrottleSlewRate.Value * dt
                        );
                        Plugin.f_throttle.SetValue(inputObj, currentAppliedThrottle);
                    }

                    // roll/course control
                    bool rollAxisActive = APData.GCASActive || APData.TargetCourse >= 0f || APData.TargetRoll != -999f;

                    if (rollAxisActive)
                    {
                        if (pilotRoll && !APData.GCASActive)
                        {
                            if (APData.TargetRoll != -999f) APData.TargetRoll = APData.CurrentRoll;
                            APData.TargetCourse = -1f;
                            rollIntegral = 0f;
                        }
                        else
                        {
                            float activeTargetRoll = APData.TargetRoll;
                            
                            if (APData.TargetCourse >= 0f && APData.PlayerRB.velocity.sqrMagnitude > 25f && !APData.GCASActive)
                            {
                                Vector3 flatVel = Vector3.ProjectOnPlane(APData.PlayerRB.velocity, Vector3.up);
                                if (flatVel.sqrMagnitude > 1f)
                                {
                                    float curCrs = Quaternion.LookRotation(flatVel).eulerAngles.y;
                                    float cErr = Mathf.DeltaAngle(curCrs, APData.TargetCourse);
                                    
                                    float crsD = (cErr - lastCrsError) / dt;
                                    lastCrsError = cErr;

                                    float desiredYawRateDeg = (cErr * Plugin.Conf_Crs_P.Value) + (crsD * Plugin.Conf_Crs_D.Value);
                                    desiredYawRateDeg = Mathf.Clamp(desiredYawRateDeg, -8f, 8f);

                                    float speed = flatVel.magnitude;
                                    float yawRateRad = desiredYawRateDeg * Mathf.Deg2Rad;
                                    float bankRad = Mathf.Atan(yawRateRad * speed / 9.81f);
                                    float bankReq = bankRad * Mathf.Rad2Deg;

                                    crsIntegral = Mathf.Clamp(crsIntegral + (cErr * dt * Plugin.Conf_Crs_I.Value), -Plugin.Conf_Crs_ILimit.Value, Plugin.Conf_Crs_ILimit.Value);
                                    bankReq += crsIntegral;

                                    if (Plugin.Conf_InvertCourseRoll.Value) bankReq = -bankReq;

                                    float limit = Mathf.Abs(APData.TargetRoll);
                                    if (limit < 0.5f) limit = 0.5f;

                                    activeTargetRoll = Mathf.Clamp(bankReq, -limit, limit);
                                }
                            }
                            else if (APData.GCASActive)
                            {
                                activeTargetRoll = 0f;
                            }

                            float rollError = Mathf.DeltaAngle(APData.CurrentRoll, activeTargetRoll);
                            float rollRate = APData.PlayerRB.transform.InverseTransformDirection(APData.PlayerRB.angularVelocity).z * Mathf.Rad2Deg;

                            // Roll sleep
                            if (useHumanize) {
                                float rollErrAbs = Mathf.Abs(rollError);
                                float rollRateAbs = Mathf.Abs(rollRate);
                                if (!isRollSleeping) {
                                    if (rollErrAbs < Plugin.Hum_Roll_Inner.Value && rollRateAbs < Plugin.Hum_RollRate_Inner.Value) {
                                        rollSleepUntil = Time.time + UnityEngine.Random.Range(Plugin.Hum_RollSleepMin.Value, Plugin.Hum_RollSleepMax.Value);
                                        isRollSleeping = true;
                                    }
                                } else if (rollErrAbs > Plugin.Hum_Roll_Outer.Value || rollRateAbs > Plugin.Hum_RollRate_Outer.Value || Time.time > rollSleepUntil) {
                                    isRollSleeping = false;
                                }
                            }

                            float rollOut = 0f;
                            if (useHumanize && isRollSleeping) {
                                rollIntegral = Mathf.MoveTowards(rollIntegral, 0f, dt * 5f);
                            } else {
                                rollIntegral = Mathf.Clamp(rollIntegral + (rollError * dt * Plugin.RollI.Value), -Plugin.RollILimit.Value, Plugin.RollILimit.Value);
                                float rollD = (0f - rollRate) * Plugin.RollD.Value; 
                                rollOut = (rollError * Plugin.RollP.Value) + rollIntegral + rollD;

                                if (Plugin.InvertRoll.Value) rollOut = -rollOut;
                                if (useHumanize) rollOut += (Mathf.PerlinNoise(0f, noiseT) - 0.5f) * 2f * Plugin.HumanizeStrength.Value;
                            }

                            Plugin.f_roll?.SetValue(inputObj, Mathf.Clamp(rollOut, -1f, 1f));
                        }
                    }

                    // pitch control
                    bool pitchAxisActive = APData.GCASActive || APData.TargetAlt > 0f;

                    if (pitchAxisActive)
                    {
                        if (pilotPitch && !APData.GCASActive)
                        {
                            if (APData.TargetAlt > 0f) APData.TargetAlt = APData.CurrentAlt;
                            altIntegral = 0f;
                            vsIntegral = 0f;
                            angleIntegral = 0f;
                        }
                        else
                        {
                            float pitchOut = 0f;

                            // gcas
                            if (APData.GCASActive)
                            {
                                float targetG = Plugin.GCAS_MaxG.Value;
                                float gError = targetG - currentG;
                                gcasIntegral = Mathf.Clamp(gcasIntegral + (gError * dt * Plugin.GCAS_I.Value), -Plugin.GCAS_ILimit.Value, Plugin.GCAS_ILimit.Value);
                                
                                float gD = (gError - lastGError) / dt;
                                lastGError = gError;
                                
                                float stick = (gError * Plugin.GCAS_P.Value) + gcasIntegral + (gD * Plugin.GCAS_D.Value);
                                
                                pitchOut = Mathf.Clamp(stick, -1f, 1f);
                                if (Plugin.InvertPitch.Value) pitchOut = -pitchOut;
                            }
                            // alt hold
                            else if (APData.TargetAlt > 0f)
                            {
                                if (useHumanize && isPitchSleeping) {
                                    altIntegral = Mathf.MoveTowards(altIntegral, 0f, dt * 2f);
                                    vsIntegral = Mathf.MoveTowards(vsIntegral, 0f, dt * 10f);
                                    angleIntegral = Mathf.MoveTowards(angleIntegral, 0f, dt * 5f);
                                    lastAltMeasurement = APData.CurrentAlt;
                                    lastVSError = 0f;
                                } else {
                                    float altError = APData.TargetAlt - APData.CurrentAlt;
                                    altIntegral = Mathf.Clamp(altIntegral + (altError * dt * Plugin.Conf_Alt_I.Value), -Plugin.Conf_Alt_ILimit.Value, Plugin.Conf_Alt_ILimit.Value);

                                    float altD = -(APData.CurrentAlt - lastAltMeasurement) / dt;
                                    lastAltMeasurement = APData.CurrentAlt;
                                    
                                    float targetVS = Mathf.Clamp((altError * Plugin.Conf_Alt_P.Value) + altIntegral + (altD * Plugin.Conf_Alt_D.Value), -APData.CurrentMaxClimbRate, APData.CurrentMaxClimbRate);
                                    float vsError = targetVS - APData.PlayerRB.velocity.y;
                                    
                                    vsIntegral = Mathf.Clamp(vsIntegral + (vsError * dt * Plugin.Conf_VS_I.Value), -Plugin.Conf_VS_ILimit.Value, Plugin.Conf_VS_ILimit.Value);
                                    float vsD = (vsError - lastVSError) / dt; 
                                    lastVSError = vsError;
                                    
                                    float targetPitchDeg = Mathf.Clamp((vsError * Plugin.Conf_VS_P.Value) + vsIntegral + (vsD * Plugin.Conf_VS_D.Value), -Plugin.Conf_VS_MaxAngle.Value, Plugin.Conf_VS_MaxAngle.Value);

                                    float currentPitch = Mathf.Asin(APData.PlayerRB.transform.forward.y) * Mathf.Rad2Deg;

                                    // alt sleep
                                    if (useHumanize) {
                                        float altErrAbs = Mathf.Abs(altError);
                                        float vsAbs = Mathf.Abs(APData.PlayerRB.velocity.y);
                                        if (altErrAbs < Plugin.Hum_Alt_Inner.Value && vsAbs < Plugin.Hum_VS_Inner.Value) {
                                            pitchSleepUntil = Time.time + UnityEngine.Random.Range(Plugin.Hum_PitchSleepMin.Value, Plugin.Hum_PitchSleepMax.Value);
                                            isPitchSleeping = true;
                                        }
                                    }
                                    if (useHumanize && (Mathf.Abs(altError) > Plugin.Hum_Alt_Outer.Value || Time.time > pitchSleepUntil)) {
                                        isPitchSleeping = false;
                                    }

                                    float pitchRate = APData.PlayerRB.transform.InverseTransformDirection(APData.PlayerRB.angularVelocity).x * Mathf.Rad2Deg;

                                    float angleError = targetPitchDeg - currentPitch;
                                    angleIntegral = Mathf.Clamp(angleIntegral + (angleError * dt * Plugin.Conf_Angle_I.Value), -Plugin.Conf_Angle_ILimit.Value, Plugin.Conf_Angle_ILimit.Value);
                                    
                                    pitchOut = (angleError * Plugin.Conf_Angle_P.Value) + angleIntegral - (pitchRate * Plugin.Conf_Angle_D.Value);

                                    if (useHumanize) pitchOut += (Mathf.PerlinNoise(noiseT, 0f) - 0.5f) * 2f * Plugin.HumanizeStrength.Value;
                                    if (Plugin.InvertPitch.Value) pitchOut = -pitchOut;
                                }
                                pitchOut = Mathf.Clamp(pitchOut, -1f, 1f);
                            }

                            Plugin.f_pitch?.SetValue(inputObj, pitchOut);
                        }
                    }
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
        private static GameObject timerObj, rangeObj, apObj, ajObj;
        private static Text tText, rText, aText, jText;
        private static float lastFuelMass = 0f;
        private static float fuelFlowEma = 0f;
        private static float lastUpdateTime = 0f;

        private static FuelGauge _cachedFuelGauge;
        private static Text _cachedRefLabel;
        private static GameObject _lastVehicleChecked;

        private static void Postfix(FlightHud __instance)
        {
            if (!Plugin.ShowExtraInfo.Value) return;

            try {
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
                    
                    if (timerObj) UnityEngine.Object.Destroy(timerObj);
                    if (rangeObj) UnityEngine.Object.Destroy(rangeObj);
                    if (apObj) UnityEngine.Object.Destroy(apObj);
                    if (ajObj) UnityEngine.Object.Destroy(ajObj);
                    timerObj = null; rangeObj = null; apObj = null; ajObj = null;

                    if (_cachedFuelGauge != null) {
                        _cachedRefLabel = Traverse.Create(_cachedFuelGauge).Field("fuelLabel").GetValue<Text>();
                    }
                }

                if (_cachedFuelGauge == null || _cachedRefLabel == null) return;

                if (!timerObj) {
                    GameObject Spawn(string name) {
                        GameObject go = UnityEngine.Object.Instantiate(_cachedRefLabel.gameObject, __instance.transform); 
                        go.name = name;
                        go.SetActive(true); 
                        return go;
                    }
                    timerObj = Spawn("AP_Timer");
                    rangeObj = Spawn("AP_Range");
                    apObj = Spawn("AP_Status");
                    ajObj = Spawn("AP_Jammer");

                    tText = timerObj.GetComponent<Text>();
                    rText = rangeObj.GetComponent<Text>();
                    aText = apObj.GetComponent<Text>();
                    jText = ajObj.GetComponent<Text>();
                }

                float startY = Plugin.FuelOffsetY.Value;
                float gap = Plugin.FuelLineSpacing.Value;
                Vector3 basePos = _cachedRefLabel.transform.position;

                void Place(GameObject obj, int index) {
                    if (!obj) return;
                    obj.transform.position = basePos + (obj.transform.up * (startY - (gap * index)));
                    if (!obj.activeSelf) obj.SetActive(true); 
                }
                Place(timerObj, 0);
                Place(rangeObj, 1);
                Place(apObj, 3);
                Place(ajObj, 5);

                // Update Text
                Aircraft aircraft = APData.LocalAircraft;
                if (aircraft == null) return;
                if (Plugin.f_fuelCapacity == null) return; 

                float currentFuel = (float)Plugin.f_fuelCapacity.GetValue(aircraft) * aircraft.GetFuelLevel();
                float time = Time.time;
                if (lastUpdateTime != 0f && lastFuelMass > 0f) {
                    float dt = time - lastUpdateTime;
                    if (dt >= Plugin.FuelUpdateInterval.Value) {
                        float burned = lastFuelMass - currentFuel;
                        float flow = Mathf.Max(0f, burned / dt);
                        fuelFlowEma = (Plugin.FuelSmoothing.Value * flow) + ((1f - Plugin.FuelSmoothing.Value) * fuelFlowEma);
                        lastUpdateTime = time;
                        lastFuelMass = currentFuel;
                    }
                } else { lastUpdateTime = time; lastFuelMass = currentFuel; }

                if (currentFuel <= 1f) {
                    tText.text = "00:00"; tText.color = ModUtils.GetColor(Plugin.ColorCrit.Value, Color.red);
                    rText.text = "-"; rText.color = ModUtils.GetColor(Plugin.ColorCrit.Value, Color.red);
                } else {
                    float calcFlow = Mathf.Max(fuelFlowEma, 0.0001f);
                    float secs = currentFuel / calcFlow;
                    
                    string sTime = TimeSpan.FromSeconds(Mathf.Min(secs, 359999f)).ToString("hh\\:mm");
                    if (tText.text != sTime) tText.text = sTime;

                    float mins = secs / 60f;
                    if (mins < Plugin.FuelCritMinutes.Value) tText.color = ModUtils.GetColor(Plugin.ColorCrit.Value, Color.red);
                    else if (mins < Plugin.FuelWarnMinutes.Value) tText.color = ModUtils.GetColor(Plugin.ColorWarn.Value, Color.yellow);
                    else tText.color = ModUtils.GetColor(Plugin.ColorGood.Value, Color.green);

                    float spd = (aircraft.rb != null) ? aircraft.rb.velocity.magnitude : 0f;
                    float distMeters = secs * spd;
                    if (distMeters > 99999000f) distMeters = 99999000f;
                    
                    string sRange = ModUtils.ProcessGameString(UnitConverter.DistanceReading(distMeters), Plugin.DistShowUnit.Value);
                    if (rText.text != sRange) rText.text = sRange;
                    
                    rText.color = ModUtils.GetColor(Plugin.ColorInfo.Value, Color.cyan);
                }

                if (APData.GCASActive) {
                    aText.text = "AUTO-GCAS"; aText.color = ModUtils.GetColor(Plugin.ColorCrit.Value, Color.red);
                } else if (APData.GCASWarning) {
                    aText.text = "PULL UP"; aText.color = ModUtils.GetColor(Plugin.ColorCrit.Value, Color.red);
                } else if (APData.Enabled && Plugin.ShowAPOverlay.Value) {
                    string altStr = ModUtils.ProcessGameString(UnitConverter.AltitudeReading(Mathf.Round(APData.TargetAlt)), Plugin.AltShowUnit.Value);
                    string climbStr = ModUtils.ProcessGameString(UnitConverter.ClimbRateReading(Mathf.Round(APData.CurrentMaxClimbRate)), Plugin.VertSpeedShowUnit.Value);
                    
                    string spdStr = (APData.TargetSpeed > 0) 
                        ? $"{ModUtils.ProcessGameString(UnitConverter.SpeedReading(APData.TargetSpeed), Plugin.SpeedShowUnit.Value)}" 
                        : "null";

                    string degUnit = Plugin.AngleShowUnit.Value ? "°" : "";
                    string latStr = (APData.TargetCourse >= 0) 
                        ? $"C{APData.TargetCourse:F0}{degUnit}" 
                        : $"R{APData.TargetRoll:F0}{degUnit}";

                    string newText = $"{altStr} {climbStr}\n{spdStr} {latStr}";

                    if (aText.text != newText) aText.text = newText;
                    aText.color = ModUtils.GetColor(Plugin.ColorAPOn.Value, Color.green);
                } else if (!APData.GCASEnabled && Plugin.ShowGCASOff.Value) {
                    aText.text = "GCAS OFF"; aText.color = ModUtils.GetColor(Plugin.ColorWarn.Value, Color.yellow);
                } else {
                    aText.text = "";
                }
                
                if (APData.AutoJammerActive) {
                    jText.text = "AJ ON"; jText.color = ModUtils.GetColor(Plugin.ColorAPOn.Value, Color.green);
                } else { jText.text = ""; }
            } catch (Exception ex) {
                Plugin.Logger.LogError($"[HUDVisualsPatch] Error: {ex}");
            }
        }
    }
}

public class PIDController
{
    private float _integral;
    private float _lastError;
    private float _lastMeasurement; // For derivative on measurement to avoid kick
    private bool _initialized;

    public void Reset()
    {
        _integral = 0f;
        _lastError = 0f;
        _lastMeasurement = 0f;
        _initialized = false;
    }

    public void SetIntegral(float val) => _integral = val;

    public float Evaluate(float error, float currentMeasurement, float dt, float kp, float ki, float kd, float iLimit)
    {
        if (dt <= 0f) return 0f;
        
        if (!_initialized)
        {
            _lastMeasurement = currentMeasurement;
            _lastError = error;
            _initialized = true;
        }

        // Integral
        _integral += error * dt * ki;
        _integral = Mathf.Clamp(_integral, -iLimit, iLimit);

        // Derivative (using derivative of measurement to prevent derivative kick on setpoint change)
        float derivative = -(currentMeasurement - _lastMeasurement) / dt;
        _lastMeasurement = currentMeasurement;

        return (error * kp) + _integral + (derivative * kd);
    }
}
