using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace AutopilotMod
{
    [BepInPlugin("com.qwerty1423.noautopilotmod", "NOAutopilotMod", "4.11.1")]
    public class Plugin : BaseUnityPlugin
    {
        internal new static ManualLogSource Logger;

        // ap menu?
        public static ConfigEntry<KeyCode> MenuKey;
        private Rect _windowRect = new Rect(50, 50, 250, 450);
        private bool _showMenu = false;
        
        private Vector2 _scrollPos;
        private bool _isResizing = false;
        private Rect _resizeRect = new Rect(0, 0, 0, 0);
        
        private string _bufAlt = "0";
        private string _bufClimb = "40";
        private string _bufRoll = "0";
        
        private GUIStyle _styleWindow;
        private GUIStyle _styleLabel;
        private GUIStyle _styleButton;
        private bool _stylesInitialized = false;

        public static ConfigEntry<float> UI_PosX, UI_PosY;
        public static ConfigEntry<float> UI_Width, UI_Height;
        private bool _firstWindowInit = true;

        // Visuals
        public static ConfigEntry<string> ColorAPOn, ColorAPOff, ColorGood, ColorWarn, ColorCrit, ColorInfo;
        public static ConfigEntry<float> FuelOffsetY, FuelLineSpacing, FuelSmoothing, FuelUpdateInterval;
        public static ConfigEntry<int> FuelWarnMinutes, FuelCritMinutes;
        public static ConfigEntry<bool> ShowExtraInfo;
        public static ConfigEntry<bool> ShowAPOverlay;
        public static ConfigEntry<bool> ShowGCASOff;

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
        public static ConfigEntry<KeyCode> ToggleKey, ToggleFBWKey, UpKey, DownKey, BigUpKey, BigDownKey;
        public static ConfigEntry<KeyCode> ClimbRateUpKey, ClimbRateDownKey, BankLeftKey, BankRightKey, BankLevelKey;

        // Flight Values
        public static ConfigEntry<float> AltStep, BigAltStep, ClimbRateStep, BankStep, MinAltitude;

        // Tuning
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

        // --- CACHED REFLECTION FIELDS ---
        internal static FieldInfo f_playerVehicle;
        internal static FieldInfo f_controlInputs; 
        internal static FieldInfo f_pitch, f_roll; 
        
        // Aircraft Specifics
        internal static FieldInfo f_controlsFilter, f_fuelCapacity, f_pilots, f_gearState, f_weaponManager, f_radarAlt;
        
        // Weapon/Jammer Specifics
        internal static FieldInfo f_powerSupply, f_charge, f_maxCharge;
        internal static MethodInfo m_Fire, m_GetAccel;

        // FBW Methods
        internal static MethodInfo m_GetFBW, m_SetFBW;

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

            UI_PosX = Config.Bind("Visuals - UI", "1. Window Position X", -1f, "-1 = Auto Bottom Right, otherwise pixel value");
            UI_PosY = Config.Bind("Visuals - UI", "2. Window Position Y", -1f, "-1 = Auto Bottom Right, otherwise pixel value");
            UI_Width = Config.Bind("Visuals - UI", "3. Window Width", 300f, "Saved Width");
            UI_Height = Config.Bind("Visuals - UI", "4. Window Height", 450f, "Saved Height");

            FuelSmoothing = Config.Bind("Calculations", "1. Fuel Flow Smoothing", 0.1f, "Alpha value");
            FuelUpdateInterval = Config.Bind("Calculations", "2. Fuel Update Interval", 1.0f, "Seconds");
            FuelWarnMinutes = Config.Bind("Calculations", "3. Fuel Warning Time", 15, "Minutes");
            FuelCritMinutes = Config.Bind("Calculations", "4. Fuel Critical Time", 5, "Minutes");

            // Settings
            StickDeadzone = Config.Bind("Settings", "1. Stick Deadzone", 0.5f, "Threshold");
            InvertRoll = Config.Bind("Settings", "2. Invert Roll", false, "Flip Roll");
            InvertPitch = Config.Bind("Settings", "3. Invert Pitch", true, "Flip Pitch");

            // Auto Jammer
            EnableAutoJammer = Config.Bind("Auto Jammer", "1. Enable Auto Jammer", true, "Allow the feature");
            AutoJammerKey = Config.Bind("Auto Jammer", "2. Toggle Key", KeyCode.Slash, "Key to toggle jamming");
            AutoJammerThreshold = Config.Bind("Auto Jammer", "3. Energy Threshold", 0.99f, "Fire when energy > this %");
            AutoJammerHumanize = Config.Bind("Auto Jammer", "4. Humanize Delay", true, "Add random delay");
            AutoJammerMinDelay = Config.Bind("Auto Jammer", "5. Delay Min", 0.05f, "Seconds");
            AutoJammerMaxDelay = Config.Bind("Auto Jammer", "6. Delay Max", 0.15f, "Seconds");
            AutoJammerReleaseMin = Config.Bind("Auto Jammer", "7. Release Delay Min", 0.05f, "Seconds");
            AutoJammerReleaseMax = Config.Bind("Auto Jammer", "8. Release Delay Max", 0.15f, "Seconds");

            // Controls
            ToggleKey = Config.Bind("Controls", "01. Toggle AP Key", KeyCode.Equals, "AP On/Off");
            ToggleFBWKey = Config.Bind("Controls", "02. Toggle FBW Key", KeyCode.Delete, "Toggle Stability Assist");
            UpKey = Config.Bind("Controls", "03. Altitude Up (Small)", KeyCode.UpArrow, "small increase");
            DownKey = Config.Bind("Controls", "04. Altitude Down (Small)", KeyCode.DownArrow, "small decrease");
            BigUpKey = Config.Bind("Controls", "05. Altitude Up (Big)", KeyCode.LeftArrow, "large increase");
            BigDownKey = Config.Bind("Controls", "06. Altitude Down (Big)", KeyCode.RightArrow, "large decrease");
            ClimbRateUpKey = Config.Bind("Controls", "07. Climb Rate Increase", KeyCode.PageUp, "Increase Max VS");
            ClimbRateDownKey = Config.Bind("Controls", "08. Climb Rate Decrease", KeyCode.PageDown, "Decrease Max VS");
            BankLeftKey = Config.Bind("Controls", "09. Bank Left", KeyCode.LeftBracket, "Roll Left");
            BankRightKey = Config.Bind("Controls", "10. Bank Right", KeyCode.RightBracket, "Roll Right");
            BankLevelKey = Config.Bind("Controls", "11. Bank Level (Reset)", KeyCode.Quote, "Reset Roll to 0");

            // Flight Values
            AltStep = Config.Bind("Controls", "13. Altitude Increment (Small)", 0.1f, "Meters per tick");
            BigAltStep = Config.Bind("Controls", "14. Altitude Increment (Big)", 100f, "Meters per tick");
            ClimbRateStep = Config.Bind("Controls", "15. Climb Rate Step", 0.5f, "m/s per tick");
            BankStep = Config.Bind("Controls", "16. Bank Step", 0.5f, "Degrees per tick");
            MinAltitude = Config.Bind("Controls", "17. Minimum Target Altitude", 20f, "Safety floor");
            MenuKey = Config.Bind("Controls", "18. Menu Key", KeyCode.F8, "Open the Autopilot Menu");

            // Tuning
            DefaultMaxClimbRate = Config.Bind("Tuning - 0. Limits", "1. Default Max Climb Rate", 40f, "Startup value");
            Conf_VS_MaxAngle = Config.Bind("Tuning - 0. Limits", "2. Max Pitch Angle", 900.0f, "Safety Clamp");

            // Loops
            Conf_Alt_P = Config.Bind("Tuning - 1. Altitude", "1. Alt P", 0.5f, "Alt Error -> Target VS");
            Conf_Alt_I = Config.Bind("Tuning - 1. Altitude", "2. Alt I", 0.0f, "Accumulates Error");
            Conf_Alt_D = Config.Bind("Tuning - 1. Altitude", "3. Alt D", 1.5f, "Dampens Approach");
            Conf_Alt_ILimit = Config.Bind("Tuning - 1. Altitude", "4. Alt I Limit", 10.0f, "Max Integral (m/s)");
            Conf_VS_P = Config.Bind("Tuning - 2. VertSpeed", "1. VS P", 0.5f, "VS Error -> Target Angle");
            Conf_VS_I = Config.Bind("Tuning - 2. VertSpeed", "2. VS I", 0.1f, "Trim Angle");
            Conf_VS_D = Config.Bind("Tuning - 2. VertSpeed", "3. VS D", 0.1f, "Dampens VS Change");
            Conf_VS_ILimit = Config.Bind("Tuning - 2. VertSpeed", "4. VS I Limit", 300.0f, "Max Trim (Deg)");
            Conf_Angle_P = Config.Bind("Tuning - 3. Angle", "1. Angle P", 0.01f, "Angle Error -> Stick");
            Conf_Angle_I = Config.Bind("Tuning - 3. Angle", "2. Angle I", 0.0f, "Holds Angle");
            Conf_Angle_D = Config.Bind("Tuning - 3. Angle", "3. Angle D", 0.0f, "Dampens Rotation");
            Conf_Angle_ILimit = Config.Bind("Tuning - 3. Angle", "4. Angle I Limit", 100.0f, "Max Integral (Stick)");
            RollP = Config.Bind("Tuning - Roll", "1. Roll P", 0.01f, "P");
            RollI = Config.Bind("Tuning - Roll", "2. Roll I", 0.002f, "I");
            RollD = Config.Bind("Tuning - Roll", "3. Roll D", 0.001f, "D");
            RollMax = Config.Bind("Tuning - Roll", "4. Roll Max Output", 1.0f, "Limit");
            RollILimit = Config.Bind("Tuning - Roll", "5. Roll I Limit", 50.0f, "Limit");

            // Auto GCAS
            EnableGCAS = Config.Bind("Auto GCAS", "1. Enable GCAS on start", true, "GCAS off at start if disabled");
            ToggleGCASKey = Config.Bind("Auto GCAS", "2. Toggle GCAS Key", KeyCode.Backslash, "Turn Auto-GCAS on/off");
            GCAS_MaxG = Config.Bind("Auto GCAS", "3. Max G-Pull", 5.0f, "Assumed G-Force capability for calculation");
            GCAS_WarnBuffer = Config.Bind("Auto GCAS", "4. Warning Buffer", 20.0f, "Seconds warning before auto-pull");
            GCAS_AutoBuffer = Config.Bind("Auto GCAS", "5. Auto-Pull Buffer", 1.0f, "Safety margin seconds");
            GCAS_Deadzone = Config.Bind("Auto GCAS", "6. GCAS Deadzone", 0.5f, "GCAS override deadzone");
            GCAS_ScanRadius = Config.Bind("Auto GCAS", "7. Scan Radius", 2.0f, "Width of the collision tunnel. Bigger = safer for wings.");
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

            // --- REFLECTION CACHING ---
            try {
                // 1. Get Player Vehicle
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
                }

                BindingFlags allFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                
                f_controlsFilter = typeof(Aircraft).GetField("controlsFilter", allFlags);
                f_fuelCapacity = typeof(Aircraft).GetField("fuelCapacity", allFlags);
                f_pilots = typeof(Aircraft).GetField("pilots", allFlags);
                f_gearState = typeof(Aircraft).GetField("gearState", allFlags);
                f_weaponManager = typeof(Aircraft).GetField("weaponManager", allFlags);
                f_radarAlt = typeof(Aircraft).GetField("radarAlt", allFlags); 

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
                    m_Fire = wmType.GetMethod("Fire");
                }

            } catch (Exception ex) {
                Logger.LogError("Failed to cache reflection fields! Mod might break. " + ex);
            }

            new Harmony("com.qwerty1423.noautopilotmod").PatchAll();
        }

        private void InitStyles()
        {
            _styleWindow = new GUIStyle(GUI.skin.window);
            
            _styleLabel = new GUIStyle(GUI.skin.label);
            _styleLabel.alignment = TextAnchor.MiddleLeft;

            _styleButton = new GUIStyle(GUI.skin.button);
            _styleButton.fixedHeight = 25;

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
                    Input.GetKey(BankLeftKey.Value) || Input.GetKey(BankRightKey.Value) || Input.GetKey(BankLevelKey.Value);

                if (isAdjusting)
                {
                    SyncMenuValues();
                }
            }
        }

        private void SyncMenuValues()
        {
            float currentAlt_Raw = APData.TargetAlt > 0 
                ? APData.TargetAlt
                : 0.0f;

            _bufAlt = ModUtils.ConvertAlt_ToDisplay(currentAlt_Raw).ToString("F0");
            
            float currentVS_Raw = APData.CurrentMaxClimbRate > 0 
                ? APData.CurrentMaxClimbRate 
                : DefaultMaxClimbRate.Value;

            _bufClimb = ModUtils.ConvertVS_ToDisplay(currentAlt_Raw).ToString("F0");

            _bufRoll = APData.TargetRoll.ToString("F0");
        }

        private void OnGUI()
        {
            if (!_showMenu) return;
            if (!_stylesInitialized) InitStyles();

            if (_isResizing)
            {
                // If we let go of the mouse anywhere on screen, stop resizing
                if (Event.current.type == EventType.MouseUp)
                {
                    _isResizing = false;
                }
                // If we drag the mouse anywhere on screen, update window size
                else if (Event.current.type == EventType.MouseDrag)
                {
                    // Add the mouse movement (delta) to the width/height
                    _windowRect.width = Mathf.Max(250f, _windowRect.width + Event.current.delta.x);
                    _windowRect.height = Mathf.Max(200f, _windowRect.height + Event.current.delta.y);
                    
                    // Repaint immediately so the UI feels responsive
                    Event.current.Use();
                }
            }

            if (_firstWindowInit)
            {
                // Load positions and sizes from Config
                float x = UI_PosX.Value;
                float y = UI_PosY.Value;
                float w = Mathf.Max(250f, UI_Width.Value);
                float h = Mathf.Max(200f, UI_Height.Value);

                if (x < 0) x = Screen.width - w - 20;
                if (y < 0) y = Screen.height - h - 50;

                _windowRect = new Rect(x, y, w, h);
                _firstWindowInit = false;
            }

            // Draw the window
            _windowRect = GUI.Window(999, _windowRect, DrawAPWindow, "AUTOPILOT CONTROLS", _styleWindow);
        }

        private void DrawAPWindow(int windowID)
        {
            // detect click
            // The handle is a 20x20 square in the bottom right corner
            Rect resizeHandleRect = new Rect(_windowRect.width - 20, _windowRect.height - 20, 20, 20);

            if (Event.current.type == EventType.MouseDown && resizeHandleRect.Contains(Event.current.mousePosition))
            {
                _isResizing = true;
                Event.current.Use(); // Consume the click so we don't click buttons underneath
            }

            GUI.DragWindow(new Rect(0, 0, 10000, 25));

            // Save Config
            if (Event.current.type == EventType.Repaint)
            {
                if (_windowRect.x != UI_PosX.Value || _windowRect.y != UI_PosY.Value)
                {
                    UI_PosX.Value = _windowRect.x;
                    UI_PosY.Value = _windowRect.y;
                }
                if (_windowRect.width != UI_Width.Value || _windowRect.height != UI_Height.Value)
                {
                    UI_Width.Value = _windowRect.width;
                    UI_Height.Value = _windowRect.height;
                }
            }

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(_windowRect.height - 40));

            GUILayout.BeginVertical();

            string cOn = ColorAPOn.Value.StartsWith("#") ? ColorAPOn.Value : "#" + ColorAPOn.Value;
            string cOff = ColorAPOff.Value.StartsWith("#") ? ColorAPOff.Value : "#" + ColorAPOff.Value;
            string statusColor = APData.Enabled ? cOn : cOff;
            string statusText = APData.Enabled ? "ENGAGED" : "DISENGAGED";
            
            GUILayout.Label($"STATUS: <color={statusColor}>{statusText}</color>", _styleLabel);

            float currentVS = 0f;
            if (APData.PlayerRB != null) currentVS = APData.PlayerRB.velocity.y;
            
            string sAlt = UnitConverter.AltitudeReading(APData.CurrentAlt);
            string sRoll = $"{APData.CurrentRoll:F0}°";
            string sVS = UnitConverter.ClimbRateReading(currentVS);

            GUILayout.Label($"Alt: {sAlt}", _styleLabel);
            GUILayout.Label($"VS: {sVS} | Roll: {sRoll}", _styleLabel);

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Target Alt:", _styleLabel, GUILayout.Width(80));
            _bufAlt = GUILayout.TextField(_bufAlt);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Max V.Spd:", _styleLabel, GUILayout.Width(80));
            _bufClimb = GUILayout.TextField(_bufClimb);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Bank Angle:", _styleLabel, GUILayout.Width(80));
            _bufRoll = GUILayout.TextField(_bufRoll);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (GUILayout.Button("SET VALUES", _styleButton))
            {
                if (float.TryParse(_bufAlt, out float a)) 
                    APData.TargetAlt = ModUtils.ConvertAlt_FromDisplay(a);
                
                if (float.TryParse(_bufClimb, out float c)) 
                    APData.CurrentMaxClimbRate = Mathf.Max(0.5f, ModUtils.ConvertVS_FromDisplay(c));
                
                if (float.TryParse(_bufRoll, out float r)) APData.TargetRoll = r;
            }

            GUILayout.Space(5);

            GUI.backgroundColor = APData.Enabled ? Color.green : Color.red;
            if (GUILayout.Button(APData.Enabled ? "DISENGAGE" : "ENGAGE", _styleButton))
            {
                APData.Enabled = !APData.Enabled;
                if (APData.Enabled)
                {
                    if (float.TryParse(_bufAlt, out float a)) 
                        APData.TargetAlt = ModUtils.ConvertAlt_FromDisplay(a);
                    
                    if (float.TryParse(_bufClimb, out float c)) 
                        APData.CurrentMaxClimbRate = Mathf.Max(0.5f, ModUtils.ConvertVS_FromDisplay(c));
                        
                    if (float.TryParse(_bufRoll, out float r)) APData.TargetRoll = r;
                }
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(15);

            if (GUILayout.Button($"Auto Jammer: {(APData.AutoJammerActive ? "ON" : "OFF")}", _styleButton))
                APData.AutoJammerActive = !APData.AutoJammerActive;

            if (GUILayout.Button($"Auto GCAS: {(APData.GCASEnabled ? "ON" : "OFF")}", _styleButton))
            {
                APData.GCASEnabled = !APData.GCASEnabled;
                if (!APData.GCASEnabled) APData.GCASActive = false;
            }

            if (GUILayout.Button($"FBW Stability: {(APData.FBWDisabled ? "OFF" : "ON")}", _styleButton))
                ToggleFBW_Action();

            if (GUILayout.Button("Reset Bank (Level)", _styleButton))
            {
                APData.TargetRoll = 0f;
                _bufRoll = "0";
            }

            GUILayout.EndVertical();

            GUILayout.EndScrollView();

            GUI.Label(resizeHandleRect, "↘");
        }

        public static void ToggleFBW_Action()
        {
            if (APData.LocalAircraft == null) return;

            try {
                if (f_controlsFilter == null) return;

                object cf = f_controlsFilter.GetValue(APData.LocalAircraft);
                
                if (cf != null && !cf.GetType().Name.Contains("Helo")) {
                    if (m_GetFBW == null) m_GetFBW = cf.GetType().GetMethod("GetFlyByWireParameters");
                    if (m_SetFBW == null) m_SetFBW = cf.GetType().GetMethod("SetFlyByWireParameters");
                    
                    if (m_GetFBW != null && m_SetFBW != null) {
                        object result = m_GetFBW.Invoke(cf, null);
                        
                        bool isEnabled = (bool)result.GetType().GetField("Item1").GetValue(result);
                        float[] values = (float[])result.GetType().GetField("Item2").GetValue(result);
                        
                        m_SetFBW.Invoke(cf, new object[] { !isEnabled, values });
                        
                        APData.FBWDisabled = isEnabled;
                    }
                }
            } catch (Exception ex) {
                Logger.LogError("Error toggling FBW: " + ex);
            }
        }
    }

    public enum AltitudeUnit
    {
        Meters,
        Feet
    }

    public enum DistanceUnit
    {
        Kilometers,
        NauticalMiles,
        Miles
    }

    public enum VertSpeedUnit
    {
        MetersPerSec,
        FeetPerSec,
        FeetPerMin,
        Knots
    }

    public enum SpeedUnit
    {
        KilometersPerHour,
        MilesPerHour,
        Knots,
        MetersPerSec
    }

    // --- SHARED DATA ---
    public static class APData
    {
        public static bool Enabled = false;
        public static bool WasAPOn = false;
        public static bool GCASEnabled = Plugin.EnableGCAS.Value;
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
        public static Aircraft LocalAircraft;
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
                return metersPerSec * 3.28084f;
            return metersPerSec;
        }
        public static float ConvertVS_FromDisplay(float displayVal)
        {
            if (PlayerSettings.unitSystem == PlayerSettings.UnitSystem.Imperial)
                return displayVal / 196.850394f;
            return displayVal;
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
                    Component v = (Component)playerVehicle;
                    if (v.gameObject != lastVehicleObj)
                    {
                        lastVehicleObj = v.gameObject;
                        APData.Enabled = false;
                        APData.WasAPOn = false;
                        APData.GCASEnabled = Plugin.EnableGCAS.Value;
                        APData.AutoJammerActive = false;
                        APData.FBWDisabled = false;
                        APData.GCASActive = false;
                        APData.GCASWarning = false;
                        APData.TargetAlt = altitude;
                        APData.TargetRoll = 0f;
                        APData.CurrentMaxClimbRate = -1f;
                        APData.LocalAircraft = v.GetComponent<Aircraft>();
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

    [HarmonyPatch(typeof(FlightHud), "Update")]
    internal class InputHandlerPatch
    {
        private static void Postfix(FlightHud __instance)
        {
            try {
                if (Input.GetKeyDown(Plugin.ToggleKey.Value))
                {
                    APData.Enabled = !APData.Enabled;
                }

                if (Plugin.EnableAutoJammer.Value && Input.GetKeyDown(Plugin.AutoJammerKey.Value))
                {
                    APData.AutoJammerActive = !APData.AutoJammerActive;
                }

                if (Input.GetKeyDown(Plugin.ToggleFBWKey.Value))
                {
                    Plugin.ToggleFBW_Action();
                }
                
                if (Input.GetKeyDown(Plugin.ToggleGCASKey.Value))
                {
                    APData.GCASEnabled = !APData.GCASEnabled;
                    if (!APData.GCASEnabled) { APData.GCASActive = false; APData.GCASWarning = false; }
                }
            } catch (Exception ex) {
                Plugin.Logger.LogError($"[InputHandlerPatch] Error: {ex}");
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

        // Derivative States
        private static float lastAltError = 0f;
        private static float lastVSError = 0f;
        private static float lastGError = 0f;
        private static float lastAltMeasurement = 0f;
                
        // Timers & Logic
        private static bool wasEnabled = false;
        private static float pitchSleepUntil = 0f;
        private static float rollSleepUntil = 0f;
        private static bool isPitchSleeping = false;
        private static bool isRollSleeping = false;

        // Jammer
        private static float jammerNextFireTime = 0f;
        private static float jammerNextReleaseTime = 0f;
        private static bool isJammerHoldingTrigger = false;

        private static void ResetIntegrators()
        {
            altIntegral = vsIntegral = angleIntegral = rollIntegral = gcasIntegral = 0f;
            lastAltError = lastVSError = lastGError = 0f;
            isPitchSleeping = isRollSleeping = false;
        }

        private static void Postfix(PilotPlayerState __instance)
        {
            if (APData.CurrentMaxClimbRate < 0f) APData.CurrentMaxClimbRate = Plugin.DefaultMaxClimbRate.Value;

            if (Plugin.f_controlInputs == null || Plugin.f_pitch == null || Plugin.f_roll == null) return;

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

            try
            {
                // Access input via cached field
                object inputObj = Plugin.f_controlInputs.GetValue(__instance);

                if (inputObj == null) return;
                
                float stickPitch = 0f;
                float stickRoll = 0f;

                if (Plugin.f_pitch == null && Plugin.f_roll == null) return;

                stickPitch = (float)Plugin.f_pitch.GetValue(inputObj);
                stickRoll = (float)Plugin.f_roll.GetValue(inputObj);

                APData.GCASWarning = false;

                float currentG = 1f;
                Aircraft acRef = null;

                if (APData.PlayerRB != null) {
                    acRef = APData.LocalAircraft;
                    if (acRef != null) {
                        // Safe G-Force
                        if (Plugin.f_pilots != null) {
                            IList pilots = (IList)Plugin.f_pilots.GetValue(acRef);
                            if (pilots != null && pilots.Count > 0 && Plugin.m_GetAccel != null) {
                                Vector3 pAccel = (Vector3)Plugin.m_GetAccel.Invoke(pilots[0], null);
                                currentG = Vector3.Dot(pAccel + Vector3.up, acRef.transform.up);
                            }
                        }
                    }
                }

                // gcas
                if (APData.GCASEnabled && APData.PlayerRB != null)
                {
                    bool gearDown = false;
                    // Safe Gear Check
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
                        if (speed > 15f && (!APData.Enabled || APData.GCASActive))
                        {
                            Vector3 velocity = APData.PlayerRB.velocity;
                            float descentRate = (velocity.y < 0) ? Mathf.Abs(velocity.y) : 0f;

                            float gAccel = Plugin.GCAS_MaxG.Value * 9.81f; 
                            float turnRadius = (speed * speed) / gAccel;
                            
                            // Reverted to 4.9.0 buffer logic
                            float reactionTime = Plugin.GCAS_AutoBuffer.Value;
                            float reactionDist = speed * reactionTime;
                            float warnDist = speed * Plugin.GCAS_WarnBuffer.Value;

                            bool dangerImminent = false;
                            bool warningZone = false;
                            // bool isWallThreat = false;

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

                                // Reverted to 4.9.0 Release Logic
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
                if (APData.AutoJammerActive && acRef != null && acRef.name.Contains("EW1"))
                {
                    if (Plugin.f_charge != null && Plugin.f_powerSupply != null) {
                        object ps = Plugin.f_powerSupply.GetValue(acRef);
                        if (ps != null) {
                            float cur = (float)Plugin.f_charge.GetValue(ps);
                            float max = (float)Plugin.f_maxCharge.GetValue(ps);
                            if (max <= 1f) max = 100f;
                            float pct = cur / max;

                            if (pct >= Plugin.AutoJammerThreshold.Value) {
                                if (!isJammerHoldingTrigger) {
                                    if (jammerNextFireTime == 0f) jammerNextFireTime = Time.time + (Plugin.AutoJammerHumanize.Value ? UnityEngine.Random.Range(Plugin.AutoJammerMinDelay.Value, Plugin.AutoJammerMaxDelay.Value) : 0f);
                                    if (Time.time >= jammerNextFireTime) { isJammerHoldingTrigger = true; jammerNextFireTime = 0f; }
                                }
                            } else {
                                if (isJammerHoldingTrigger) {
                                    if (jammerNextReleaseTime == 0f) jammerNextReleaseTime = Time.time + (Plugin.AutoJammerHumanize.Value ? UnityEngine.Random.Range(Plugin.AutoJammerReleaseMin.Value, Plugin.AutoJammerReleaseMax.Value) : 0f);
                                    if (Time.time >= jammerNextReleaseTime) { isJammerHoldingTrigger = false; jammerNextReleaseTime = 0f; }
                                }
                            }

                            if (isJammerHoldingTrigger && Plugin.f_weaponManager != null) {
                                object wm = Plugin.f_weaponManager.GetValue(acRef);
                                if (wm != null) {
                                    if (Plugin.m_Fire != null) Plugin.m_Fire.Invoke(wm, null);
                                    else Traverse.Create(wm).Method("Fire").GetValue();
                                }
                            }
                        }
                    }
                }

                // autopilot
                if (APData.Enabled)
                {
                    if (Mathf.Abs(stickPitch) > Plugin.StickDeadzone.Value || Mathf.Abs(stickRoll) > Plugin.StickDeadzone.Value)
                    {
                        APData.Enabled = false;
                        APData.GCASActive = false;
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
                            if (Input.GetKey(Plugin.BigDownKey.Value)) APData.TargetAlt = Mathf.Max(APData.TargetAlt - Plugin.BigAltStep.Value, Plugin.MinAltitude.Value);
                            
                            if (Input.GetKey(Plugin.ClimbRateUpKey.Value)) APData.CurrentMaxClimbRate += Plugin.ClimbRateStep.Value;
                            if (Input.GetKey(Plugin.ClimbRateDownKey.Value)) APData.CurrentMaxClimbRate = Mathf.Max(0.5f, APData.CurrentMaxClimbRate - Plugin.ClimbRateStep.Value);

                            if (Input.GetKey(Plugin.BankLevelKey.Value)) APData.TargetRoll = 0f;
                            if (Input.GetKey(Plugin.BankLeftKey.Value)) APData.TargetRoll = Mathf.Repeat(APData.TargetRoll + Plugin.BankStep.Value + 180f, 360f) - 180f;
                            if (Input.GetKey(Plugin.BankRightKey.Value)) APData.TargetRoll = Mathf.Repeat(APData.TargetRoll - Plugin.BankStep.Value + 180f, 360f) - 180f;
                        }

                        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
                        float pitchOut = 0f;
                        float rollOut = 0f;
                        float noiseT = Time.time * Plugin.HumanizeSpeed.Value;
                        bool useHumanize = Plugin.HumanizeEnabled.Value && !APData.GCASActive;

                        // Humanize Logic
                        if (useHumanize) {
                            float altErrAbs = Mathf.Abs(APData.TargetAlt - APData.CurrentAlt);
                            float vsAbs = (APData.PlayerRB != null) ? Mathf.Abs(APData.PlayerRB.velocity.y) : 0f;
                            if (!isPitchSleeping) {
                                if (altErrAbs < Plugin.Hum_Alt_Inner.Value && vsAbs < Plugin.Hum_VS_Inner.Value) {
                                    pitchSleepUntil = Time.time + UnityEngine.Random.Range(Plugin.Hum_PitchSleepMin.Value, Plugin.Hum_PitchSleepMax.Value);
                                    isPitchSleeping = true;
                                }
                            } else if (altErrAbs > Plugin.Hum_Alt_Outer.Value || vsAbs > Plugin.Hum_VS_Outer.Value || Time.time > pitchSleepUntil) {
                                isPitchSleeping = false;
                            }

                            float rollErrAbs = Mathf.Abs(Mathf.DeltaAngle(APData.CurrentRoll, APData.TargetRoll));
                            float rollRateAbs = (APData.PlayerRB != null) ? Mathf.Abs(APData.PlayerRB.transform.InverseTransformDirection(APData.PlayerRB.angularVelocity).z * 57.29f) : 0f;
                            if (!isRollSleeping) {
                                if (rollErrAbs < Plugin.Hum_Roll_Inner.Value && rollRateAbs < Plugin.Hum_RollRate_Inner.Value) {
                                    rollSleepUntil = Time.time + UnityEngine.Random.Range(Plugin.Hum_RollSleepMin.Value, Plugin.Hum_RollSleepMax.Value);
                                    isRollSleeping = true;
                                }
                            } else if (rollErrAbs > Plugin.Hum_Roll_Outer.Value || rollRateAbs > Plugin.Hum_RollRate_Outer.Value || Time.time > rollSleepUntil) {
                                isRollSleeping = false;
                            }
                        }

                        // Pitch Control
                        float pInv = Plugin.InvertPitch.Value ? -1f : 1f;

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
                        else
                        {
                            if (useHumanize && isPitchSleeping) {
                                pitchOut = (Mathf.PerlinNoise(noiseT, 0f) - 0.5f) * Plugin.HumanizeStrength.Value * 0.5f;
                            } else {
                                float altError = APData.TargetAlt - APData.CurrentAlt;
                                altIntegral = Mathf.Clamp(altIntegral + (altError * dt * Plugin.Conf_Alt_I.Value), -Plugin.Conf_Alt_ILimit.Value, Plugin.Conf_Alt_ILimit.Value);

                                float altD = -(APData.CurrentAlt - lastAltMeasurement) / dt;
                                lastAltMeasurement = APData.CurrentAlt;
                                
                                float targetVS = Mathf.Clamp((altError * Plugin.Conf_Alt_P.Value) + altIntegral + (altD * Plugin.Conf_Alt_D.Value), -APData.CurrentMaxClimbRate, APData.CurrentMaxClimbRate);
                                float vsError = targetVS - APData.PlayerRB.velocity.y;
                                vsIntegral = Mathf.Clamp(vsIntegral + (vsError * dt * Plugin.Conf_VS_I.Value), -Plugin.Conf_VS_ILimit.Value, Plugin.Conf_VS_ILimit.Value);
                                float vsD = (vsError - lastVSError) / dt; lastVSError = vsError;
                                
                                float targetPitchDeg = Mathf.Clamp((vsError * Plugin.Conf_VS_P.Value) + vsIntegral + (vsD * Plugin.Conf_VS_D.Value), -Plugin.Conf_VS_MaxAngle.Value, Plugin.Conf_VS_MaxAngle.Value);

                                Vector3 fwd = APData.PlayerRB.transform.forward;
                                Vector3 flatFwd = Vector3.ProjectOnPlane(fwd, Vector3.up).normalized;
                                float currentPitch = Vector3.Angle(fwd, flatFwd);
                                if (fwd.y < 0) currentPitch = -currentPitch;
                                float pitchRate = APData.PlayerRB.transform.InverseTransformDirection(APData.PlayerRB.angularVelocity).x * Mathf.Rad2Deg;

                                float angleError = targetPitchDeg - currentPitch;
                                angleIntegral = Mathf.Clamp(angleIntegral + (angleError * dt * Plugin.Conf_Angle_I.Value), -Plugin.Conf_Angle_ILimit.Value, Plugin.Conf_Angle_ILimit.Value);
                                
                                float stickRaw = (angleError * Plugin.Conf_Angle_P.Value) + angleIntegral - (pitchRate * Plugin.Conf_Angle_D.Value);
                                
                                pitchOut = Mathf.Clamp(stickRaw, -1f, 1f);
                                if (Plugin.InvertPitch.Value) pitchOut = -pitchOut;

                                if (useHumanize) pitchOut += (Mathf.PerlinNoise(noiseT, 0f) - 0.5f) * 2f * Plugin.HumanizeStrength.Value;
                            }
                        }

                        // Roll Control
                        float rInv = Plugin.InvertRoll.Value ? -1f : 1f;
                        float rollError = Mathf.DeltaAngle(APData.CurrentRoll, APData.TargetRoll);
                        float rollRate = APData.PlayerRB.transform.InverseTransformDirection(APData.PlayerRB.angularVelocity).z * 57.29f;

                        if (useHumanize && isRollSleeping) {
                            rollOut = (Mathf.PerlinNoise(0f, noiseT) - 0.5f) * Plugin.HumanizeStrength.Value * 0.5f;
                        } else {
                            rollIntegral = Mathf.Clamp(rollIntegral + (rollError * dt * Plugin.RollI.Value), -Plugin.RollILimit.Value, Plugin.RollILimit.Value);
                            float rollD = (0f - rollRate) * Plugin.RollD.Value;
                            float stickRaw = (rollError * Plugin.RollP.Value) + rollIntegral + rollD;
                            rollOut = Mathf.Clamp(stickRaw, -Plugin.RollMax.Value, Plugin.RollMax.Value);
                            
                            if (!Plugin.InvertRoll.Value) rollOut = -rollOut;

                            if (useHumanize) rollOut += (Mathf.PerlinNoise(0f, noiseT) - 0.5f) * 2f * Plugin.HumanizeStrength.Value;
                        }

                        // Apply to inputs
                        pitchOut = Mathf.Clamp(pitchOut, -1f, 1f);
                        rollOut = Mathf.Clamp(rollOut, -1f, 1f);

                        // Apply to inputs
                        if (Plugin.f_pitch != null && Plugin.f_roll != null) {
                            Plugin.f_pitch.SetValue(inputObj, pitchOut);
                            Plugin.f_roll.SetValue(inputObj, rollOut);
                        } else {
                            Traverse.Create(inputObj).Field("pitch").SetValue(pitchOut);
                            Traverse.Create(inputObj).Field("roll").SetValue(rollOut);
                        }
                        
                        stickPitch = pitchOut;
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
        private static GameObject timerObj, rangeObj, apObj, ajObj, fbwObj;
        private static Text tText, rText, aText, jText, fText;
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
                GameObject currentVehicleObj = ((Component)Plugin.f_playerVehicle.GetValue(__instance))?.gameObject;
                
                if (currentVehicleObj == null) return;

                if (_lastVehicleChecked != currentVehicleObj || _cachedFuelGauge == null)
                {
                    _lastVehicleChecked = currentVehicleObj;
                    _cachedFuelGauge = __instance.GetComponentInChildren<FuelGauge>(true);
                    
                    if (timerObj) UnityEngine.Object.Destroy(timerObj);
                    if (rangeObj) UnityEngine.Object.Destroy(rangeObj);
                    if (apObj) UnityEngine.Object.Destroy(apObj);
                    if (ajObj) UnityEngine.Object.Destroy(ajObj);
                    if (fbwObj) UnityEngine.Object.Destroy(fbwObj);
                    timerObj = null; rangeObj = null; apObj = null; ajObj = null; fbwObj = null;

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
                    fbwObj = Spawn("AP_FBW");

                    tText = timerObj.GetComponent<Text>();
                    rText = rangeObj.GetComponent<Text>();
                    aText = apObj.GetComponent<Text>();
                    jText = ajObj.GetComponent<Text>();
                    fText = fbwObj.GetComponent<Text>();
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
                Place(apObj, 2);
                Place(ajObj, 3);
                Place(fbwObj, 4);

                // Update Text
                Aircraft aircraft = APData.LocalAircraft;
                if (aircraft == null) return;

                float currentFuel = (float)Plugin.f_fuelCapacity.GetValue(aircraft) * aircraft.GetFuelLevel();
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

                if (currentFuel <= 1f) {
                    tText.text = "00:00"; tText.color = ModUtils.GetColor(Plugin.ColorCrit.Value, Color.red);
                    rText.text = "-"; rText.color = ModUtils.GetColor(Plugin.ColorCrit.Value, Color.red);
                } else {
                    float calcFlow = Mathf.Max(fuelFlowEma, 0.0001f);
                    float secs = currentFuel / calcFlow;
                    
                    tText.text = TimeSpan.FromSeconds(Mathf.Min(secs, 359999f)).ToString("hh\\:mm");
                    float mins = secs / 60f;
                    if (mins < Plugin.FuelCritMinutes.Value) tText.color = ModUtils.GetColor(Plugin.ColorCrit.Value, Color.red);
                    else if (mins < Plugin.FuelWarnMinutes.Value) tText.color = ModUtils.GetColor(Plugin.ColorWarn.Value, Color.yellow);
                    else tText.color = ModUtils.GetColor(Plugin.ColorGood.Value, Color.green);

                    float spd = (aircraft.rb != null) ? aircraft.rb.velocity.magnitude : 0f;
                    float distMeters = secs * spd;
                    if (distMeters > 99999000f) distMeters = 99999000f;
                    rText.text = UnitConverter.DistanceReading(distMeters);
                    rText.color = ModUtils.GetColor(Plugin.ColorInfo.Value, Color.cyan);
                }

                if (APData.GCASActive) {
                    aText.text = "AUTO-GCAS"; aText.color = ModUtils.GetColor(Plugin.ColorCrit.Value, Color.red);
                } else if (APData.GCASWarning) {
                    aText.text = "PULL UP"; aText.color = ModUtils.GetColor(Plugin.ColorCrit.Value, Color.red);
                } else if (APData.Enabled && Plugin.ShowAPOverlay.Value) {
                    string altStr = UnitConverter.AltitudeReading(APData.TargetAlt);
                    string climbStr = UnitConverter.ClimbRateReading(APData.CurrentMaxClimbRate);
                    string rollStr = $"{APData.TargetRoll}";
                    string newText = $"{altStr} {climbStr} {rollStr}";
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
                
                if (APData.FBWDisabled) {
                    fText.text = "FBW OFF"; fText.color = ModUtils.GetColor(Plugin.ColorCrit.Value, Color.red);
                } else { fText.text = ""; }

            } catch (Exception ex) {
                Plugin.Logger.LogError($"[HUDVisualsPatch] Error: {ex}");
            }
        }
    }
}
