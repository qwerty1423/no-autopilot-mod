extern alias JetBrains;

using System;
using System.Globalization;
using System.Linq;
using System.Reflection;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

using HarmonyLib;

using JetBrains.Annotations;

using Mirage;

using NOAutopilot.Core.Config;
using NOAutopilot.Core.Flight;
using NOAutopilot.Core.HUD;
using NOAutopilot.Core.Map;
using NOAutopilot.Core.PID;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NOAutopilot.Core;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = MyPluginInfo.PLUGIN_GUID;
    public const string Name = MyPluginInfo.PLUGIN_NAME;
    public const string Version = MyPluginInfo.PLUGIN_VERSION;

    internal static new ManualLogSource Logger;

    public static bool IsBroken;

    private static string s_bufAlt = "";
    private static string s_bufClimb = "40";
    private static string s_bufRoll = "";
    private static string s_bufSpeed = "";
    private static string s_bufCourse = "";
    public static ConfigEntry<float> NavReachDistance, NavPassedDistance;
    public static ConfigEntry<string> ColorNav;
    public static ConfigEntry<bool> NavCycle;

    public static ConfigEntry<float> UI_PosX, UI_PosY;
    public static ConfigEntry<float> UI_Width, UI_Height;

    // Visuals
    public static ConfigEntry<string> ColorAPOn, ColorInfo, ColorGood;
    public static ConfigEntry<string> ColorWarn, ColorCrit, ColorRange;
    public static ConfigEntry<string> FuelFormatString;

    public static ConfigEntry<float> OverlayOffsetX,
        OverlayOffsetY,
        FuelSmoothing,
        FuelUpdateInterval,
        DisplayUpdateInterval;

    public static ConfigEntry<int> FuelWarnMinutes, FuelCritMinutes;
    public static ConfigEntry<bool> ShowExtraInfo;
    public static ConfigEntry<bool> ShowFuelOverlay, ShowAPOverlay;
    public static ConfigEntry<bool> ShowGCASOff, ShowGCASChevronOff, ShowOverride, ShowPlaceholders;
    public static ConfigEntry<bool> AltShowUnit;
    public static ConfigEntry<bool> DistShowUnit;
    public static ConfigEntry<bool> VertSpeedShowUnit;
    public static ConfigEntry<bool> SpeedShowUnit;
    public static ConfigEntry<bool> AngleShowUnit;

    // Settings
    public static ConfigEntry<float> StickTempThreshold, StickDisengageThreshold;
    public static ConfigEntry<float> DisengageDelay, ReengageDelay;
    public static ConfigEntry<bool> InvertRoll, InvertPitch, StickDisengageEnabled;
    public static ConfigEntry<bool> Conf_InvertCourseRoll, DisableATAPKey;
    public static ConfigEntry<bool> DisableATAPGCAS, DisableATAPGUI, DisableATAPStick;
    public static ConfigEntry<bool> DisableNavAPKey, DisableNavAPStick, EnableNavonWP;
    public static ConfigEntry<bool> KeepSetAltKey, KeepSetAltStick;
    public static ConfigEntry<bool> UnlockMapPan, UnlockMapZoom, SaveMapState, UnpatchIfBroken;
    public static ConfigEntry<bool> LockWingsSwept;

    // Auto Jammer
    public static ConfigEntry<bool> EnableAutoJammer;
    public static ConfigEntry<float> AutoJammerThreshold;
    public static ConfigEntry<bool> AutoJammerRandom;
    public static ConfigEntry<float> AutoJammerMinDelay, AutoJammerMaxDelay;
    public static ConfigEntry<float> AutoJammerReleaseMin, AutoJammerReleaseMax;

    // Controls
    public static ConfigEntry<KeyboardShortcut> MenuKey;
    public static ConfigEntry<KeyboardShortcut> ToggleKey, ToggleFBWKey, ToggleALSKey;
    public static ConfigEntry<KeyboardShortcut> AutoJammerKey, ToggleGCASKey, ClearKey;
    public static ConfigEntry<KeyboardShortcut> UpKey, DownKey, BigUpKey, BigDownKey;
    public static ConfigEntry<KeyboardShortcut> ClimbRateUpKey, ClimbRateDownKey;
    public static ConfigEntry<KeyboardShortcut> BankLeftKey, BankRightKey;
    public static ConfigEntry<KeyboardShortcut> SpeedHoldKey, SpeedUpKey, SpeedDownKey;
    public static ConfigEntry<KeyboardShortcut> ToggleMachKey, ToggleABKey;

    public static ConfigEntry<string> MenuRW;
    public static ConfigEntry<string> ToggleRW, ToggleFBWRW, ToggleALSRW;
    public static ConfigEntry<string> AutoJammerRW, ToggleGCASRW, ClearRW;
    public static ConfigEntry<string> UpRW, DownRW, BigUpRW, BigDownRW;
    public static ConfigEntry<string> ClimbRateUpRW, ClimbRateDownRW;
    public static ConfigEntry<string> BankLeftRW, BankRightRW;
    public static ConfigEntry<string> SpeedHoldRW, SpeedUpRW, SpeedDownRW;
    public static ConfigEntry<string> ToggleMachRW, ToggleABRW;

    // Flight Values
    public static ConfigEntry<float> AltStep, BigAltStep, ClimbRateStep, BankStep, SpeedStep, MinAltitude;

    // Limits
    public static ConfigEntry<float> DefaultMaxClimbRate, Conf_VS_MaxAngle, DefaultCRLimit;
    public static ConfigEntry<float> ThrottleMinLimit, ThrottleMaxLimit, MaxRollRate;

    // pid
    public static ConfigEntry<PIDTuning> ConfPidAlt;
    public static ConfigEntry<PIDTuning> ConfPidVs;
    public static ConfigEntry<PIDTuning> ConfPidAngle;
    public static ConfigEntry<PIDTuning> ConfPidRoll;
    public static ConfigEntry<PIDTuning> ConfPidRollRate;
    public static ConfigEntry<PIDTuning> ConfPidSpd;
    public static ConfigEntry<PIDTuning> ConfPidCrs;
    public static ConfigEntry<PIDTuning> ConfPidGcas;

    // pid logger
    public static ConfigEntry<PIDLogger.StepTarget> StepTestLoop;
    public static ConfigEntry<float> StepTestMagnitude;
    public static ConfigEntry<float> StepTestDuration;
    public static ConfigEntry<KeyboardShortcut> StepTestKey;

    // Auto GCAS
    public static ConfigEntry<bool> EnableGCAS, EnableGCASHelo, EnableGCASTiltwing;
    public static ConfigEntry<float> GcasMaxG, GcasWarnBuffer, GcasAutoBuffer, GcasDeadzone, GcasScanRadius, GcasMinAlt;

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
    private readonly float _jitterThreshold = 7.0f;
    private readonly GUIContent _measuringContent = new();
    private readonly float _buttonWidth = 40f;

    // controls table
    private readonly string _table =
        "<b>Toggle AP GUI:</b> F8\n" +
        "<b>Toggle Autopilot:</b> = (Equals)\n" +
        "<b>Toggle FBW:</b> Delete key | Singleplayer only\n" +
        "<b>Toggle AJ:</b> / (Slash)\n" +
        "<b>Toggle GCAS:</b> \\ (Backslash)\n" +
        "<b>Toggle ALS:</b> LCtrl + = (Equals) | Autoland\n" +
        "<b>Clear/Reset:</b> ' (Quote) | Roll>Nav>Crs>Roll>Alt>Roll\n\n" +
        "<b>Target Alt Small:</b> Up / Down Arrow | Small adjustment\n" +
        "<b>Target Alt Large:</b> Left / Right Arrow | Large adjustment\n" +
        "<b>Max Climb Rate:</b> PgUp / PgDn | Limit vertical speed\n" +
        "<b>Bank/Course L/R:</b> [ and ] | Adjust roll or heading\n\n" +
        "<b>Toggle Speed Hold:</b> ; (Semicolon) | Matches current speed\n" +
        "<b>Speed Up / Down:</b> LShift / LCtrl | Adjust target speed\n" +
        "<b>Mach/TAS Hold:</b> Home | Switch between Mach/TAS\n" +
        "<b>Toggle AB/Airbrake:</b> End | Toggle Afterburner/Airbrake\n";

    private RectEdge _activeEdge = RectEdge.None;
    private GUIContent _cachedExtraInfoContent;
    private GUIContent _cachedTableContent;

    private string _currentHoverTarget = "";

    private float _dynamicLabelWidth = 60f;
    private bool _firstWindowInit = true;
    private Harmony _harmony;
    private bool _isResizing;
    private bool _isTooltipVisible;
    private string _lastActiveTooltip = "";

    private Vector2 _scrollPos;
    private bool _showMenu;
    private Vector2 _stationaryPos;
    private float _stationaryTimer;
    private GUIStyle _styleButton;
    private GUIStyle _styleLabel;
    private GUIStyle _styleReadout;
    private bool _stylesInitialized;

    private GUIStyle _styleWindow;
    private bool _wasShownForThisTarget;

    // ap menu?
    private Rect _windowRect = new(50, 50, 227, 330);

    [UsedImplicitly]
    private void Awake()
    {
        Logger = base.Logger;

        TomlTypeConverter.AddConverter(typeof(PIDTuning), new TypeConverter
        {
            ConvertToString = (obj, _) => ((PIDTuning)obj).ToString(),
            ConvertToObject = (str, _) => PIDTuning.Parse(str)
        });

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
        DisplayUpdateInterval = Config.Bind("Visuals", "HUD overlay update interval", 0.02f, "seconds");
        ShowExtraInfo = Config.Bind("Visuals", "Show Fuel/AP Info", true, "Show extra info on Fuel Gauge");
        ShowFuelOverlay = Config.Bind("Visuals", "Show Fuel Overlay", true, "Draw fuel info text on the HUD.");
        ShowAPOverlay = Config.Bind("Visuals", "Show AP Overlay", true,
            "Draw AP status text on the HUD. Turn off if you want, there's a window now.");
        ShowGCASOff = Config.Bind("Visuals", "Show GCAS OFF", true, "Show GCAS- on HUD");
        ShowGCASChevronOff = Config.Bind("Visuals", "Show GCAS chevron while disabled", true,
            "Show chevron while GCAS disabled");
        ShowOverride = Config.Bind("Visuals", "Show Override Delay", true, "Show Override on HUD");
        ShowPlaceholders = Config.Bind("Visuals", "Show Overlay Placeholders", false,
            "Show the A, V, W, when values default/null");
        FuelFormatString = Config.Bind("Visuals", "Fuel time format string", "hh\\:mm",
            "e.g. hh\\:mm\\:ss, hh\\:mm, mm\\:ss, h\\h\\ m\\m\\ s\\s, etc.\nhttps://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-timespan-format-strings");
        AltShowUnit = Config.Bind("Visuals - Units", "1. Show unit for alt", false, "(example) on: 10m, off: 10");
        DistShowUnit = Config.Bind("Visuals - Units", "2. Show unit for dist", true, "(example) on: 10km, off: 10");
        VertSpeedShowUnit = Config.Bind("Visuals - Units", "3. Show unit for vertical speed", false,
            "(example) on: 10m/s, off: 10");
        SpeedShowUnit = Config.Bind("Visuals - Units", "4. Show unit for speed", false,
            "(example) on: 10km/h, off: 10 (unused right now, no autothrottle yet)");
        AngleShowUnit = Config.Bind("Visuals - Units", "5. Show unit for angle", false, "on: 10°, off: 10");

        UI_PosX = Config.Bind("Visuals - UI", "1. Window Position X", -1f,
            "-1 = Auto Bottom Right, otherwise pixel value");
        UI_PosY = Config.Bind("Visuals - UI", "2. Window Position Y", -1f,
            "-1 = Auto Bottom Right, otherwise pixel value");
        UI_Width = Config.Bind("Visuals - UI", "3. Window Width", 227f, "Saved Width");
        UI_Height = Config.Bind("Visuals - UI", "4. Window Height", 330f, "Saved Height");

        FuelSmoothing = Config.Bind("Calculations", "1. Fuel Flow Smoothing", 0.1f, "Alpha value");
        FuelUpdateInterval = Config.Bind("Calculations", "2. Fuel Update Interval", 1.0f, "Seconds");
        FuelWarnMinutes = Config.Bind("Calculations", "3. Fuel Warning Time", 15, "Minutes");
        FuelCritMinutes = Config.Bind("Calculations", "4. Fuel Critical Time", 5, "Minutes");

        // Settings
        StickTempThreshold = Config.Bind("Settings", "1. Temp disengage Stick Threshold", 0.01f,
            "for AP disengage via manual input");
        ReengageDelay = Config.Bind("Settings", "2. Reengage Delay (temp disengage)", 0.4f,
            "Seconds to wait after stick release before AP resumes control");
        DisengageDelay = Config.Bind("Settings", "3. Disengage Delay", 10f,
            "Seconds of continuous input before AP disengages (0 = off) (uses temp deadzone)");
        StickDisengageEnabled = Config.Bind("Settings", "4. Disengage on Large Input", true,
            "If true, moving the stick past a threshold turns AP OFF entirely.");
        StickDisengageThreshold = Config.Bind("Settings", "5. Large Input Disengage Threshold", 0.8f,
            "Stick input (0.0 to 1.0) required to disengage AP entirely");
        InvertRoll = Config.Bind("Settings", "6. Invert Roll", true, "Flip Roll");
        InvertPitch = Config.Bind("Settings", "7. Invert Pitch", true, "Flip Pitch");
        Conf_InvertCourseRoll =
            Config.Bind("Settings", "8. Invert Bank Direction", true, "Toggle if plane turns wrong way");
        DisableATAPGCAS = Config.Bind("Settings - Misc", "Disable autothrottle with AP (GCAS)", false,
            "Disable autothrottle when AP is disengaged by GCAS");
        DisableATAPGUI = Config.Bind("Settings - Misc", "Disable autothrottle with AP (GUI)", false,
            "Disable autothrottle when AP is disengaged by GUI");
        DisableATAPKey = Config.Bind("Settings - Misc", "Disable autothrottle with AP (key)", false,
            "Disable autothrottle when AP is disengaged by keyboard key");
        DisableATAPStick = Config.Bind("Settings - Misc", "Disable autothrottle with AP (stick)", false,
            "Disable autothrottle when AP is disengaged by stick input");
        DisableNavAPKey = Config.Bind("Settings - Misc", "Disable nav mode with AP (key)", false,
            "Disable nav mode when AP is disengaged by key");
        DisableNavAPStick = Config.Bind("Settings - Misc", "Disable nav mode with AP (stick)", false,
            "Disable nav mode when AP is disengaged by stick input");
        EnableNavonWP = Config.Bind("Settings - Misc", "Enable nav mode on WP creation", false,
            "Whether to enable nav mode on creation of new waypoint");
        KeepSetAltKey = Config.Bind("Settings - Misc", "Keep set altitude when AP engaged (key)", false,
            "AP will use previously set alt instead of current alt when engaged by keyboard key");
        KeepSetAltStick = Config.Bind("Settings - Misc", "Keep set altitude when stick inputs made", true,
            "AP will not reset alt to current alt when stick inputs are made");
        UnlockMapPan = Config.Bind("Settings - Misc", "Unlock Map Pan", true, "Requires restart to apply.");
        UnlockMapZoom = Config.Bind("Settings - Misc", "Unlock Map Zoom", true, "Requires restart to apply.");
        SaveMapState = Config.Bind("Settings - Misc", "Save Map State", false,
            "Prevent map from resetting position/zoom when reopened.");
        UnpatchIfBroken = Config.Bind("Settings - Misc", "Unpatch on error", true,
            "Unload this mod when it throws an error");

        LockWingsSwept = Config.Bind("Settings - Misc²", "Lock swing wings in swept position", false, "For when you want your AB-4 to always look like a triangle. Will move all swing wings to the swept position.");

        // nav
        NavReachDistance = Config.Bind("Settings - Navigation", "1. Reach Distance", 2500f,
            "Distance in meters to consider a waypoint reached.");
        NavPassedDistance = Config.Bind("Settings - Navigation", "2. Passed Distance", 10000f,
            "Distance in meters after waypoint is behind plane to consider it reached");
        NavCycle = Config.Bind("Settings - Navigation", "3. Cycle wp", true,
            "On: cycles to next wp upon reaching wp, Off: Deletes wp upon reaching wp");

        // Auto Jammer
        EnableAutoJammer = Config.Bind("Auto Jammer", "1. Enable Auto Jammer", true, "Allow the feature");
        AutoJammerThreshold = Config.Bind("Auto Jammer", "3. Energy Threshold", 0.99f, "Fire when energy > this %");
        AutoJammerRandom = Config.Bind("Auto Jammer", "4. Random Delay", false, "Add random delay");
        AutoJammerMinDelay = Config.Bind("Auto Jammer", "5. Delay Min", 0.02f, "Seconds");
        AutoJammerMaxDelay = Config.Bind("Auto Jammer", "6. Delay Max", 0.04f, "Seconds");
        AutoJammerReleaseMin = Config.Bind("Auto Jammer", "7. Release Delay Min", 0.02f, "Seconds");
        AutoJammerReleaseMax = Config.Bind("Auto Jammer", "8. Release Delay Max", 0.04f, "Seconds");

        // Controls
        MenuKey = Config.Bind("Controls", "1. Menu Key", new KeyboardShortcut(KeyCode.F8), "Open the Autopilot Menu");
        ToggleKey = Config.Bind("Controls", "2. Toggle AP Key", new KeyboardShortcut(KeyCode.Equals), "AP On/Off");
        ToggleFBWKey = Config.Bind("Controls", "3. Toggle FBW Key", new KeyboardShortcut(KeyCode.Delete),
            "works in singleplayer");
        AutoJammerKey = Config.Bind("Controls", "4. Auto Jammer Key", new KeyboardShortcut(KeyCode.Slash),
            "Key to toggle jamming");
        ToggleGCASKey = Config.Bind("Controls", "5. Toggle GCAS Key", new KeyboardShortcut(KeyCode.Backslash),
            "Turn Auto-GCAS on/off");
        ToggleALSKey = Config.Bind("Controls", "5.1 Toggle ALS Key",
            new KeyboardShortcut(KeyCode.Equals, KeyCode.LeftControl), "Turn autoland on/off");
        ClearKey = Config.Bind("Controls", "6. clear roll/nav/crs/roll/alt/roll", new KeyboardShortcut(KeyCode.Quote),
            "every press will clear/reset first thing it sees isn't clear from left to right");
        UpKey = Config.Bind("Controls - Altitude", "1. Altitude Up (Small)", new KeyboardShortcut(KeyCode.UpArrow),
            "small increase");
        DownKey = Config.Bind("Controls - Altitude", "2. Altitude Down (Small)",
            new KeyboardShortcut(KeyCode.DownArrow), "small decrease");
        BigUpKey = Config.Bind("Controls - Altitude", "3. Altitude Up (Big)", new KeyboardShortcut(KeyCode.LeftArrow),
            "large increase");
        BigDownKey = Config.Bind("Controls - Altitude", "4. Altitude Down (Big)",
            new KeyboardShortcut(KeyCode.RightArrow), "large decrease");
        ClimbRateUpKey = Config.Bind("Controls - Altitude", "5. Climb Rate Increase",
            new KeyboardShortcut(KeyCode.PageUp), "Increase Max VS");
        ClimbRateDownKey = Config.Bind("Controls - Altitude", "6. Climb Rate Decrease",
            new KeyboardShortcut(KeyCode.PageDown), "Decrease Max VS");
        BankLeftKey = Config.Bind("Controls - Bank", "1. Bank Left", new KeyboardShortcut(KeyCode.LeftBracket),
            "Roll/course Left");
        BankRightKey = Config.Bind("Controls - Bank", "2. Bank Right", new KeyboardShortcut(KeyCode.RightBracket),
            "Roll/course right");
        SpeedHoldKey = Config.Bind("Controls - Speed", "1. Speed Hold Toggle", new KeyboardShortcut(KeyCode.Semicolon),
            "speed hold/clear");
        SpeedUpKey = Config.Bind("Controls - Speed", "2. Target Speed Increase",
            new KeyboardShortcut(KeyCode.LeftShift), "Increase target speed");
        SpeedDownKey = Config.Bind("Controls - Speed", "3. Target Speed Decrease",
            new KeyboardShortcut(KeyCode.LeftControl), "Decrease target speed");
        ToggleMachKey = Config.Bind("Controls - Speed", "4. Toggle Mach/TAS", new KeyboardShortcut(KeyCode.Home),
            "Toggle between Mach and TAS hold");
        ToggleABKey = Config.Bind("Controls - Speed", "5. Toggle Afterburner/Airbrake",
            new KeyboardShortcut(KeyCode.End), "Toggle AB/Airbrake limits");

        // Controls (Rewired)
        MenuRW = RewiredConfigManager.BindRW(Config, "Controls (Rewired)", "1. Menu", "Open the Autopilot Menu");
        ToggleRW = RewiredConfigManager.BindRW(Config, "Controls (Rewired)", "2. Toggle AP", "AP On/Off");
        ToggleFBWRW =
            RewiredConfigManager.BindRW(Config, "Controls (Rewired)", "3. Toggle FBW", "works in singleplayer");
        AutoJammerRW = RewiredConfigManager.BindRW(Config, "Controls (Rewired)", "4. Toggle AJ",
            "Toggle auto jamming with jamming pods");
        ToggleGCASRW =
            RewiredConfigManager.BindRW(Config, "Controls (Rewired)", "5. Toggle GCAS", "Turn Auto-GCAS on/off");
        ToggleALSRW =
            RewiredConfigManager.BindRW(Config, "Controls (Rewired)", "5.1 Toggle ALS", "Turn autoland on/off");
        ClearRW = RewiredConfigManager.BindRW(Config, "Controls (Rewired)", "6. clear roll/nav/crs/roll/alt/roll",
            "every use will clear/reset first thing it sees isn't clear from left to right");
        UpRW = RewiredConfigManager.BindRW(Config, "Controls - Altitude (Rewired)", "1. Altitude Up (Small)",
            "small increase");
        DownRW = RewiredConfigManager.BindRW(Config, "Controls - Altitude (Rewired)", "2. Altitude Down (Small)",
            "small decrease");
        BigUpRW = RewiredConfigManager.BindRW(Config, "Controls - Altitude (Rewired)", "3. Altitude Up (Big)",
            "large increase");
        BigDownRW = RewiredConfigManager.BindRW(Config, "Controls - Altitude (Rewired)", "4. Altitude Down (Big)",
            "large decrease");
        ClimbRateUpRW = RewiredConfigManager.BindRW(Config, "Controls - Altitude (Rewired)", "5. Climb Rate Increase",
            "Increase Max VS");
        ClimbRateDownRW = RewiredConfigManager.BindRW(Config, "Controls - Altitude (Rewired)", "6. Climb Rate Decrease",
            "Decrease Max VS");
        BankLeftRW =
            RewiredConfigManager.BindRW(Config, "Controls - Bank (Rewired)", "1. Bank Left", "Roll/course Left");
        BankRightRW =
            RewiredConfigManager.BindRW(Config, "Controls - Bank (Rewired)", "2. Bank Right", "Roll/course right");
        SpeedHoldRW = RewiredConfigManager.BindRW(Config, "Controls - Speed (Rewired)", "1. Speed Hold Toggle",
            "speed hold/clear");
        SpeedUpRW = RewiredConfigManager.BindRW(Config, "Controls - Speed (Rewired)", "2. Target Speed Increase",
            "Increase target speed");
        SpeedDownRW = RewiredConfigManager.BindRW(Config, "Controls - Speed (Rewired)", "3. Target Speed Decrease",
            "Decrease target speed");
        ToggleMachRW = RewiredConfigManager.BindRW(Config, "Controls - Speed (Rewired)", "4. Toggle Mach/TAS",
            "Toggle between Mach and TAS hold");
        ToggleABRW = RewiredConfigManager.BindRW(Config, "Controls - Speed (Rewired)", "5. Toggle Afterburner/Airbrake",
            "Toggle AB/Airbrake limits");

        // control values
        AltStep = Config.Bind("Controls - Values", "17. Altitude Increment (Small)", 0.1f, "Meters per frame (60fps)");
        BigAltStep = Config.Bind("Controls - Values", "18. Altitude Increment (Big)", 100f, "Meters per frame (60fps)");
        ClimbRateStep = Config.Bind("Controls - Values", "19. Climb Rate Step", 0.5f, "m/s per frame (60fps)");
        BankStep = Config.Bind("Controls - Values", "20. Bank Step", 0.5f, "Degrees per frame (60fps)");
        SpeedStep = Config.Bind("Controls - Values", "21. Speed Step", 1.0f, "m/s per frame (60fps)");
        MinAltitude = Config.Bind("Controls - Values", "22. Minimum Target Altitude", 20f, "Safety floor");

        // Auto GCAS
        EnableGCAS = Config.Bind("Auto GCAS", "1. Enable GCAS on start", true,
            "GCAS off at start if disabled (anything that isn't a helo)");
        EnableGCASHelo = Config.Bind("Auto GCAS", "2. Enable GCAS on start (Helo)", false,
            "If disabled, GCAS starts off for helicopters.");
        EnableGCASTiltwing = Config.Bind("Auto GCAS", "3. Enable GCAS on start (Tiltwing)", true,
            "If disabled, gcas starts off for tiltwings.");
        GcasMaxG = Config.Bind("Auto GCAS", "4. Max G-Pull", 5.0f, "Assumed G-Force capability for calculation");
        GcasWarnBuffer =
            Config.Bind("Auto GCAS", "5. Warning Buffer", 20.0f, "GCAS warning indicator first appearance");
        GcasAutoBuffer = Config.Bind("Auto GCAS", "6. Auto-Pull Buffer", 0.5f, "Safety margin seconds");
        GcasDeadzone = Config.Bind("Auto GCAS", "7. GCAS Deadzone", 0.5f, "GCAS override deadzone (default 0.5 = 50%)");
        GcasScanRadius = Config.Bind("Auto GCAS", "8. Scan Radius", 2.0f, "Width of the spherecast (m)");
        GcasMinAlt = Config.Bind("Auto GCAS", "9. Minimum altitude", 2.0f, "Minimum altitude (m)");

        DefaultMaxClimbRate = Config.Bind("Limits", "1. Default Max Climb Rate", 10f, "Startup value");
        Conf_VS_MaxAngle = Config.Bind("Limits", "2. Max Pitch Angle", 85.0f, "angle from horizon limit");
        DefaultCRLimit = Config.Bind("Limits", "3. Default course roll limit", 10.0f,
            "default roll limit when turning in course/nav mode");
        ThrottleMinLimit = Config.Bind("Limits", "4. Safe Min Throttle", 0.01f,
            "Minimum throttle when limiter is active (prevents Airbrake)");
        ThrottleMaxLimit = Config.Bind("Limits", "5. Safe Max Throttle", 0.89f,
            "Maximum throttle when limiter is active (prevents Afterburner)");
        MaxRollRate = Config.Bind("Limits", "6. Max Roll Rate", 360f,
            "Maximum commanded roll rate in deg/s");

        // PID Loops
        ConfPidAlt = PIDTuningBinder.Bind(Config, "PID", "1. Altitude > VS",
            new PIDTuning(0.5, 0, 3), "Altitude > Vertical Speed");

        ConfPidVs = PIDTuningBinder.Bind(Config, "PID", "2. VS > Angle",
            new PIDTuning(2.44967771875362, 2.31068044043631, 0.549968619394224), "Vertical Speed > Pitch Angle");

        ConfPidAngle = PIDTuningBinder.Bind(Config, "PID", "3. Angle > Stick",
            new PIDTuning(0.0329026146189137, 5.7512084040881, 0.12329376291698), "Pitch Angle > Stick");

        ConfPidRoll = PIDTuningBinder.Bind(Config, "PID", "4. Roll > Roll Rate",
            new PIDTuning(2, 0, 0), "Roll > Roll rate");

        ConfPidRollRate = PIDTuningBinder.Bind(Config, "PID", "5. Roll Rate > Stick",
            new PIDTuning(0.00455294214079626, 0.402907365979166, 0.026685335805323), "Roll rate > Stick");

        ConfPidCrs = PIDTuningBinder.Bind(Config, "PID", "6. Course > Roll",
            new PIDTuning(1, 30, 0, clegg: true), "Course Error > Bank Angle");

        ConfPidSpd = PIDTuningBinder.Bind(Config, "PID", "7. Speed > Throttle",
            new PIDTuning(0.276635855846017, 4.55835278395057, 0.486418840935585, 5), "Speed Error > Throttle");

        ConfPidGcas = PIDTuningBinder.Bind(Config, "PID", "8. G-Force > Stick",
            new PIDTuning(0.448050807726941, 0.947761066338411, 0), "GCAS G Error > Stick");

        // PID logging
        StepTestLoop = Config.Bind("PID logging", "1. Target Loop", PIDLogger.StepTarget.None, "Which loop to run step response on");
        StepTestMagnitude = Config.Bind("PID logging", "2. Step Magnitude", 10.0f, "Amount to step the setpoint by (deg, m/s, etc)");
        StepTestDuration = Config.Bind("PID logging", "3. Record Duration", 5.0f, "How long to record data (seconds)");
        StepTestKey = Config.Bind("PID logging", "4. Start/Stop Key", new KeyboardShortcut(KeyCode.None), "Key to start/stop step logging");

        // Random
        RandomEnabled = Config.Bind("Settings - Random", "01. Random Enabled", false,
            "Add imperfections (needs some work i think)");
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
        Rand_RollRate_Inner = Config.Bind("Settings - Random", "13. Roll Rate Tolerance Inner", 1.0f,
            "Start Sleeping (deg/s)");
        Rand_RollRate_Outer =
            Config.Bind("Settings - Random", "14. Roll Rate Tolerance Outer", 20.0f, "Wake Up (deg/s)");
        Rand_RollSleepMin = Config.Bind("Settings - Random", "15. Roll Sleep Min", 1.5f, "Seconds");
        Rand_RollSleepMax = Config.Bind("Settings - Random", "16. Roll Sleep Max", 60.0f, "Seconds");
        Rand_Spd_Inner = Config.Bind("Settings - Random", "17. Speed Tolerance Inner", 0.5f,
            "Start Sleeping (m/s error)");
        Rand_Spd_Outer = Config.Bind("Settings - Random", "18. Speed Tolerance Outer", 2.0f, "Wake Up (m/s error)");
        Rand_Spd_SleepMin = Config.Bind("Settings - Random", "19. Speed Sleep Min", 2.0f, "Seconds");
        Rand_Spd_SleepMax = Config.Bind("Settings - Random", "20. Speed Sleep Max", 60.0f, "Seconds");
        Rand_Acc_Inner = Config.Bind("Settings - Random", "21. Accel Tolerance Inner", 0.05f,
            "Start Sleeping (m/s² acceleration)");
        Rand_Acc_Outer = Config.Bind("Settings - Random", "22. Accel Tolerance Outer", 0.5f,
            "Wake Up (m/s² acceleration)");

        _harmony = new Harmony(Guid);
        try
        {
            _harmony.PatchAll();
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            HudPatch.Initialize();
            Logger.LogInfo($"v{Version} loaded.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            IsBroken = true;
        }
    }

    [UsedImplicitly]
    private void Update()
    {
        try
        {
            if (IsBroken && UnpatchIfBroken.Value)
            {
                Logger.LogWarning("Unloading mod because it broke. You can disable this in Config - Misc.");
                _harmony?.UnpatchSelf();

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
                    foreach (GameObject obj in APData.NavVisuals)
                    {
                        if (obj != null)
                        {
                            Destroy(obj);
                        }
                    }
                }

                ClearAllStatics();

                _harmony = null;
                Logger = null;
                enabled = false;
            }

            RewiredConfigManager.Update();

            if (APData.PlayerRB != null)
            {
                float rawSpeed = APData.PlayerRB.velocity.magnitude;
                float alpha = 1.0f - Mathf.Exp(-Time.deltaTime * 2.0f);
                APData.SpeedEma = Mathf.Max(Mathf.Lerp(APData.SpeedEma, rawSpeed, alpha), 0.0001f);
            }

            if (CursorManager.GetFlag(CursorFlags.Chat))
            {
                return;
            }

            if (InputHelper.IsDown(MenuRW) || MenuKey.Value.IsDown())
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
                    InputHelper.IsPressed(UpRW) || UpKey.Value.IsPressed() ||
                    InputHelper.IsPressed(DownRW) || DownKey.Value.IsPressed() ||
                    InputHelper.IsPressed(BigUpRW) || BigUpKey.Value.IsPressed() ||
                    InputHelper.IsPressed(BigDownRW) || BigDownKey.Value.IsPressed() ||
                    InputHelper.IsPressed(ClimbRateUpRW) || ClimbRateUpKey.Value.IsPressed() ||
                    InputHelper.IsPressed(ClimbRateDownRW) || ClimbRateDownKey.Value.IsPressed() ||
                    InputHelper.IsPressed(BankLeftRW) || BankLeftKey.Value.IsPressed() ||
                    InputHelper.IsPressed(BankRightRW) || BankRightKey.Value.IsPressed() ||
                    InputHelper.IsPressed(ClearRW) || ClearKey.Value.IsPressed();

                if (isAdjusting)
                {
                    SyncMenuValues();
                }
            }

            if (InputHelper.IsDown(ToggleRW) || ToggleKey.Value.IsDown())
            {
                APData.Enabled = !APData.Enabled;
                if (!APData.Enabled)
                {
                    if (DisableNavAPKey.Value)
                    {
                        APData.NavEnabled = false;
                    }

                    if (DisableATAPKey.Value)
                    {
                        APData.TargetSpeed = -1f;
                    }
                }
                else if (!KeepSetAltKey.Value)
                {
                    APData.TargetAlt = APData.CurrentAlt;
                }

                SyncMenuValues();
            }

            if (EnableAutoJammer.Value && (InputHelper.IsDown(AutoJammerRW) || AutoJammerKey.Value.IsDown()))
            {
                APData.AutoJammerActive = !APData.AutoJammerActive;
            }

            if (InputHelper.IsDown(ToggleGCASRW) || ToggleGCASKey.Value.IsDown())
            {
                APData.GCASEnabled = !APData.GCASEnabled;
                if (!APData.GCASEnabled)
                {
                    APData.GCASActive = false;
                    APData.GCASWarning = false;
                }
            }

            if (InputHelper.IsDown(ToggleALSRW) || ToggleALSKey.Value.IsDown())
            {
                APData.ALSActive = !APData.ALSActive;
                if (!APData.ALSActive)
                {
                    APData.ALSStatusText = "";
                    APData.LocalPilot?.SwitchState(APData.LocalPilot.playerState);
                }
                else
                {
                    FactionHQ hq = APData.LocalAircraft?.NetworkHQ;
                    if (hq != null)
                    {
                        if (hq.GetAirbases().Any())
                        {
                            APData.LocalPilot?.SwitchState(new AIPilotLandingState());
                        }
                        else
                        {
                            APData.ALSStatusText = "ALS: NO AIRBASE";
                        }
                    }
                }
            }

            if (InputHelper.IsDown(SpeedHoldRW) || SpeedHoldKey.Value.IsDown())
            {
                if (APData.TargetSpeed >= 0)
                {
                    APData.TargetSpeed = -1f;
                    s_bufSpeed = "";
                }
                else if (APData.PlayerRB != null && APData.LocalAircraft != null)
                {
                    float currentTAS = APData.LocalAircraft != null
                        ? APData.LocalAircraft.speed
                        : APData.PlayerRB.velocity.magnitude;
                    if (APData.SpeedHoldIsMach)
                    {
                        float currentAlt = (APData.LocalAircraft?.GlobalPosition().y) ?? 0f;
                        float sos = LevelInfo.GetSpeedOfSound(currentAlt);
                        APData.TargetSpeed = currentTAS / sos;
                        s_bufSpeed = APData.TargetSpeed.ToString("F2");
                    }
                    else
                    {
                        APData.TargetSpeed = currentTAS;
                        s_bufSpeed = ModUtils.ConvertSpeed_ToDisplay(currentTAS).ToString("F0");
                    }
                }

                SyncMenuValues();
                GUI.FocusControl(null);
            }

            if (APData.TargetSpeed >= 0f)
            {
                bool speedUp = InputHelper.IsPressed(SpeedUpRW) || SpeedUpKey.Value.IsPressed();
                bool speedDown = InputHelper.IsPressed(SpeedDownRW) || SpeedDownKey.Value.IsPressed();
                if (speedUp || speedDown)
                {
                    if (APData.TargetSpeed < 0)
                    {
                        float currentTAS = APData.LocalAircraft != null
                            ? APData.LocalAircraft.speed
                            : APData.PlayerRB.velocity.magnitude;
                        if (APData.SpeedHoldIsMach)
                        {
                            float currentAlt = (APData.LocalAircraft?.GlobalPosition().y) ?? 0f;
                            APData.TargetSpeed = currentTAS / LevelInfo.GetSpeedOfSound(currentAlt);
                        }
                        else
                        {
                            APData.TargetSpeed = currentTAS;
                        }
                    }

                    float step = SpeedStep.Value * 60f * Time.deltaTime;
                    if (APData.SpeedHoldIsMach)
                    {
                        float currentAlt = (APData.LocalAircraft?.GlobalPosition().y) ?? 0f;
                        float sos = LevelInfo.GetSpeedOfSound(currentAlt);
                        step /= Mathf.Max(sos, 1f);
                    }

                    if (speedUp)
                    {
                        APData.TargetSpeed += step;
                    }

                    if (speedDown)
                    {
                        APData.TargetSpeed = Mathf.Max(0, APData.TargetSpeed - step);
                    }

                    SyncMenuValues();
                }
                else if (InputHelper.IsDown(ToggleMachRW) || ToggleMachKey.Value.IsDown())
                {
                    if (float.TryParse(s_bufSpeed, out float val))
                    {
                        float currentAlt = (APData.LocalAircraft?.GlobalPosition().y) ?? 0f;
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

                if (InputHelper.IsDown(ToggleABRW) || ToggleABKey.Value.IsDown())
                {
                    APData.AllowExtremeThrottle = !APData.AllowExtremeThrottle;
                }
            }

            if (InputHelper.IsDown(ToggleFBWRW) || ToggleFBWKey.Value.IsDown())
            {
                APData.NextMultiplayerCheck = 0f;
                APData.FBWDisabled = !IsMultiplayer() && !APData.FBWDisabled;

                UpdateFBWState();
            }

            // no need because plane respawns and the toggle above exists
            // not worth the lag spikes
            // if (APData.FBWDisabled && IsMultiplayer())
            // {
            //     APData.FBWDisabled = false;
            //     UpdateFBWState();
            // }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Update] Error: {ex}");
            IsBroken = true;
        }
    }

    [UsedImplicitly]
    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();

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
            foreach (GameObject obj in APData.NavVisuals)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
        }

        ClearAllStatics();

        _harmony = null;
        Logger = null;
    }

    [UsedImplicitly]
    // gui
    private void OnGUI()
    {
        if (_cachedTableContent == null)
        {
            _cachedTableContent = new GUIContent("(Hover for controls)", _table);
            _cachedExtraInfoContent = new GUIContent("(Hover above for some info)\n(Hover here for controls)", _table);
        }

        if (!_showMenu)
        {
            return;
        }

        float guiAlpha = !APData.IsConscious ? 0f : Mathf.Clamp01((APData.BloodPressure - 0.2f) / 0.4f);
        if (guiAlpha <= 0f)
        {
            _isResizing = false;
            return;
        }

        if (!_stylesInitialized)
        {
            InitStyles();
        }

        Color oldGuiColor = GUI.color;
        try
        {
            GUI.color = new Color(1, 1, 1, guiAlpha);

            if (_isResizing)
            {
                if (Event.current.type == EventType.MouseUp)
                {
                    _isResizing = false;
                    _activeEdge = RectEdge.None;
                }
                else if (Event.current.type == EventType.MouseDrag)
                {
                    Vector2 delta = Event.current.delta;
                    const float minW = 227f;
                    const float minH = 330f;

                    if (_activeEdge == RectEdge.Right || _activeEdge == RectEdge.TopRight ||
                        _activeEdge == RectEdge.BottomRight)
                    {
                        _windowRect.width = Mathf.Max(minW, _windowRect.width + delta.x);
                    }

                    if (_activeEdge == RectEdge.Bottom || _activeEdge == RectEdge.BottomLeft ||
                        _activeEdge == RectEdge.BottomRight)
                    {
                        _windowRect.height = Mathf.Max(minH, _windowRect.height + delta.y);
                    }

                    if (_activeEdge == RectEdge.Left || _activeEdge == RectEdge.TopLeft ||
                        _activeEdge == RectEdge.BottomLeft)
                    {
                        float oldX = _windowRect.x;
                        _windowRect.x = Mathf.Min(_windowRect.xMax - minW, _windowRect.x + delta.x);
                        _windowRect.width += oldX - _windowRect.x;
                    }

                    if (_activeEdge == RectEdge.Top || _activeEdge == RectEdge.TopLeft ||
                        _activeEdge == RectEdge.TopRight)
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

                if (x < 0)
                {
                    x = Screen.width - w - 20;
                }

                if (y < 0)
                {
                    y = Screen.height - h - 50;
                }

                _windowRect = new Rect(x, y, w, h);
                _firstWindowInit = false;
            }

            Vector2 mousePos = Event.current.mousePosition;
            const float thickness = 8f;

            if (Event.current.type == EventType.MouseDown && _showMenu)
            {
                bool withinVertical = mousePos.y >= _windowRect.y - thickness &&
                                      mousePos.y <= _windowRect.yMax + thickness;
                bool withinHorizontal = mousePos.x >= _windowRect.x - thickness &&
                                        mousePos.x <= _windowRect.xMax + thickness;

                bool closeLeft = Mathf.Abs(mousePos.x - _windowRect.x) < thickness && withinVertical;
                bool closeRight = Mathf.Abs(mousePos.x - _windowRect.xMax) < thickness && withinVertical;
                bool closeTop = Mathf.Abs(mousePos.y - _windowRect.y) < thickness && withinHorizontal;
                bool closeBottom = Mathf.Abs(mousePos.y - _windowRect.yMax) < thickness && withinHorizontal;

                if (closeLeft && closeTop)
                {
                    _activeEdge = RectEdge.TopLeft;
                }
                else if (closeRight && closeTop)
                {
                    _activeEdge = RectEdge.TopRight;
                }
                else if (closeLeft && closeBottom)
                {
                    _activeEdge = RectEdge.BottomLeft;
                }
                else if (closeRight && closeBottom)
                {
                    _activeEdge = RectEdge.BottomRight;
                }
                else if (closeLeft)
                {
                    _activeEdge = RectEdge.Left;
                }
                else if (closeRight)
                {
                    _activeEdge = RectEdge.Right;
                }
                else if (closeTop)
                {
                    _activeEdge = RectEdge.Top;
                }
                else if (closeBottom)
                {
                    _activeEdge = RectEdge.Bottom;
                }

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
        catch (Exception ex)
        {
            Logger.LogError($"[OnGUI] Error: {ex}");
            IsBroken = true;
        }
        finally
        {
            GUI.color = oldGuiColor;
        }
    }

    private void InitStyles()
    {
        _styleWindow = new GUIStyle(GUI.skin.window);

        _styleLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, richText = true };

        _styleReadout = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft,
            richText = true,
            padding = new RectOffset(3, 3, 3, 3)
        };

        _styleButton = new GUIStyle(GUI.skin.button);

        _stylesInitialized = true;
    }

    public static void SyncMenuValues()
    {
        s_bufAlt = APData.TargetAlt > 0
            ? ModUtils.ConvertAlt_ToDisplay(APData.TargetAlt).ToString("F0")
            : "";

        s_bufClimb = APData.CurrentMaxClimbRate > 0
            ? ModUtils.ConvertVS_ToDisplay(APData.CurrentMaxClimbRate).ToString("F0")
            : DefaultMaxClimbRate.Value.ToString(CultureInfo.CurrentCulture);

        s_bufRoll = APData.TargetRoll != -999f ? APData.TargetRoll.ToString("F0") : "";

        s_bufSpeed = APData.TargetSpeed < 0
            ? ""
            : APData.SpeedHoldIsMach
            ? APData.TargetSpeed.ToString("F2")
            : ModUtils.ConvertSpeed_ToDisplay(APData.TargetSpeed).ToString("F0");

        s_bufCourse = APData.TargetCourse >= 0 ? APData.TargetCourse.ToString("F0") : "";
    }

    // gui
    private void DrawAPWindow(int windowID)
    {
        GUI.DragWindow(new Rect(0, 0, 10000, 25));

        if (!APData.PlayerRB || !APData.LocalAircraft || !APData.LocalPilot)
        {
            GUILayout.Label("No player aircraft.");
            return;
        }

        _scrollPos = GUILayout.BeginScrollView(_scrollPos, false, false, GUIStyle.none, GUIStyle.none,
            GUILayout.Height(_windowRect.height - 30));

        GUILayout.BeginVertical();

        float currentVS = (APData.PlayerRB?.velocity.y) ?? 0f;

        float currentAlt = (APData.LocalAircraft?.GlobalPosition().y) ?? 0f;
        float sos = LevelInfo.GetSpeedOfSound(currentAlt);
        float currentSpeed = (APData.PlayerRB?.velocity.magnitude) ?? 0f;

        float currentCourse = 0f;
        if (APData.PlayerRB?.velocity.sqrMagnitude > 1f)
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
        GUI.color = APData.Enabled && APData.TargetAlt > 0 ? Color.green : Color.white;
        if (GUILayout.Button(new GUIContent($"{sAlt}", "Current altitude\nGreen if alt AP on\nClick to copy"),
                _styleReadout, GUILayout.Width(_dynamicLabelWidth)))
        {
            s_bufAlt = ModUtils.ConvertAlt_ToDisplay(APData.CurrentAlt).ToString("F0");
        }

        GUI.color = Color.white;
        s_bufAlt = GUILayout.TextField(s_bufAlt);
        GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Target altitude"));

        if (GUILayout.Button(new GUIContent("CLR", "disable alt hold"), _styleButton, GUILayout.Width(_buttonWidth)))
        {
            APData.TargetAlt = -1f;
            s_bufAlt = "";
            GUI.FocusControl(null);
        }

        GUILayout.EndHorizontal();

        // vertical speed
        GUILayout.BeginHorizontal();
        bool isDefaultVS = Mathf.Abs(APData.CurrentMaxClimbRate - DefaultMaxClimbRate.Value) < 0.1f;
        GUI.color = isDefaultVS ? Color.white : Color.cyan;
        if (GUILayout.Button(new GUIContent($"{sVS}", "Current climb/descent rate\nCyan if not default\nClick to copy"),
                _styleReadout, GUILayout.Width(_dynamicLabelWidth)))
        {
            s_bufClimb = ModUtils.ConvertVS_ToDisplay(Mathf.Abs(currentVS)).ToString("F0");
        }

        GUI.color = Color.white;
        s_bufClimb = GUILayout.TextField(s_bufClimb);
        GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Max vertical speed"));

        if (GUILayout.Button(new GUIContent("RST", "Reset to default"), _styleButton, GUILayout.Width(_buttonWidth)))
        {
            APData.CurrentMaxClimbRate = DefaultMaxClimbRate.Value;
            s_bufClimb = ModUtils.ConvertVS_ToDisplay(APData.CurrentMaxClimbRate).ToString("F0");
            GUI.FocusControl(null);
        }

        GUILayout.EndHorizontal();

        // bank angle
        GUILayout.BeginHorizontal();
        GUI.color = APData.NavEnabled && APData.Enabled
            ? Color.cyan
            : APData.Enabled && APData.TargetRoll != -999f
                ? Color.green : Color.white;

        if (GUILayout.Button(
                new GUIContent($"{sRoll}",
                    "Current bank angle\nCyan if Nav mode on\nGreen if roll AP on\nClick to copy"), _styleReadout,
                GUILayout.Width(_dynamicLabelWidth)))
        {
            s_bufRoll = APData.NavEnabled || APData.TargetCourse >= 0
                ? Mathf.Abs(APData.CurrentRoll).ToString("F0")
                : APData.CurrentRoll.ToString("F0");
        }

        GUI.color = Color.white;
        s_bufRoll = GUILayout.TextField(s_bufRoll);
        GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Target/limit bank angle"));

        if (GUILayout.Button(new GUIContent("CLR", "disable roll hold"), _styleButton, GUILayout.Width(_buttonWidth)))
        {
            APData.TargetRoll = -999f;
            s_bufRoll = "";
            GUI.FocusControl(null);
        }

        GUILayout.EndHorizontal();

        // speed
        GUILayout.BeginHorizontal();
        GUI.color = APData.TargetSpeed >= 0 ? Color.green : Color.white;
        if (GUILayout.Button(new GUIContent($"{sSpd}", "Current speed\nGreen if autothrottle on\nClick to copy"),
                _styleReadout, GUILayout.Width(_dynamicLabelWidth)))
        {
            if (APData.SpeedHoldIsMach)
            {
                float currentMach = currentSpeed / Mathf.Max(sos, 1f);
                s_bufSpeed = currentMach.ToString("F2");
            }
            else
            {
                s_bufSpeed = ModUtils.ConvertSpeed_ToDisplay(currentSpeed).ToString("F0");
            }
        }

        GUI.color = Color.white;
        s_bufSpeed = GUILayout.TextField(s_bufSpeed);
        GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Target speed"));

        // mach hold button
        string machText = APData.SpeedHoldIsMach ? "M" : "Spd";
        if (GUILayout.Button(new GUIContent(machText, "Mach Hold / TAS Hold"), _styleButton,
                GUILayout.Width(_buttonWidth)))
        {
            if (float.TryParse(s_bufSpeed, out float val))
            {
                if (!APData.SpeedHoldIsMach)
                {
                    float ms = ModUtils.ConvertSpeed_FromDisplay(val);
                    float mach = Mathf.Max(0, ms / sos);
                    s_bufSpeed = mach.ToString("F2");
                    if (APData.TargetSpeed >= 0)
                    {
                        APData.TargetSpeed = mach;
                    }
                }
                else
                {
                    float ms = Mathf.Max(0, val * sos);
                    float display = ModUtils.ConvertSpeed_ToDisplay(ms);
                    s_bufSpeed = display.ToString("F0");
                    if (APData.TargetSpeed >= 0)
                    {
                        APData.TargetSpeed = ms;
                    }
                }
            }

            APData.SpeedHoldIsMach = !APData.SpeedHoldIsMach;
            GUI.FocusControl(null);
        }

        Color oldCol = GUI.backgroundColor;
        if (APData.AllowExtremeThrottle)
        {
            GUI.backgroundColor = Color.red;
        }

        string limitText = APData.AllowExtremeThrottle ? "AB1" : "AB0";
        if (GUILayout.Button(new GUIContent(limitText, "Toggle afterburner/airbrake"), _styleButton,
                GUILayout.Width(_buttonWidth)))
        {
            APData.AllowExtremeThrottle = !APData.AllowExtremeThrottle;
            GUI.FocusControl(null);
        }

        GUI.backgroundColor = oldCol;
        GUILayout.EndHorizontal();

        // course
        GUILayout.BeginHorizontal();
        GUI.color = APData.NavEnabled && APData.Enabled
            ? Color.cyan
            : APData.Enabled && APData.TargetCourse >= 0
                ? Color.green : Color.white;

        if (GUILayout.Button(
                new GUIContent($"{sCrs}", "Current course\nCyan if Nav mode on\nGreen if Course AP on\nClick to copy"),
                _styleReadout, GUILayout.Width(_dynamicLabelWidth)))
        {
            s_bufCourse = currentCourse.ToString("F0");
        }

        GUI.color = Color.white;
        if (APData.NavEnabled && APData.TargetCourse >= 0)
        {
            s_bufCourse = APData.TargetCourse.ToString("F0");
        }

        s_bufCourse = GUILayout.TextField(s_bufCourse);
        GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Target course"));
        if (GUILayout.Button(new GUIContent("CLR", "Disable course hold/nav"), _styleButton,
                GUILayout.Width(_buttonWidth)))
        {
            APData.TargetCourse = -1f;
            s_bufCourse = "";
            APData.NavEnabled = false;
            APData.TargetRoll = 0;
            s_bufRoll = "0";
            GUI.FocusControl(null);
        }

        GUILayout.EndHorizontal();

        // set values
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Apply", "Applies typed values"), _styleButton))
        {
            APData.TargetAlt = float.TryParse(s_bufAlt, out float a)
                ? ModUtils.ConvertAlt_FromDisplay(a) : -1f;

            if (float.TryParse(s_bufClimb, out float c))
            {
                APData.CurrentMaxClimbRate = Mathf.Max(0.5f, ModUtils.ConvertVS_FromDisplay(c));
            }

            APData.TargetSpeed = float.TryParse(s_bufSpeed, out float s)
                ? APData.SpeedHoldIsMach
                    ? s : ModUtils.ConvertSpeed_FromDisplay(s)
                : -1f;

            APData.TargetCourse = float.TryParse(s_bufCourse, out float crs) ? crs : -1f;

            if (float.TryParse(s_bufRoll, out float r))
            {
                APData.TargetRoll = (APData.NavEnabled || APData.TargetCourse >= 0) &&
                                    APData.TargetRoll == 0
                    ? DefaultCRLimit.Value : r;
            }
            else if (APData.TargetCourse >= 0f || APData.NavEnabled)
            {
                APData.TargetRoll = DefaultCRLimit.Value;
                s_bufRoll = APData.TargetRoll.ToString("F0");
            }
            else
            {
                APData.TargetRoll = -999f;
                s_bufRoll = "";
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
                if (string.IsNullOrEmpty(s_bufAlt))
                {
                    APData.TargetAlt = APData.CurrentAlt;
                    s_bufAlt = ModUtils.ConvertAlt_ToDisplay(APData.TargetAlt).ToString("F0");
                }

                if (string.IsNullOrEmpty(s_bufCourse) && string.IsNullOrEmpty(s_bufRoll))
                {
                    if (APData.NavEnabled || APData.TargetCourse >= 0)
                    {
                        APData.TargetRoll = DefaultCRLimit.Value;
                        s_bufRoll = APData.TargetRoll.ToString("F0");
                    }
                    else
                    {
                        APData.TargetRoll = 0f;
                        s_bufRoll = "0";
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
        string ajText = APData.AutoJammerActive ? "AJ" : "AJ-";
        if (GUILayout.Button(new GUIContent(ajText, "Toggle Auto Jammer"), _styleButton))
        {
            APData.AutoJammerActive = !APData.AutoJammerActive;
            GUI.FocusControl(null);
        }

        GUI.backgroundColor = APData.GCASEnabled ? Color.green : Color.white;
        string gcasText = APData.GCASEnabled ? "GCAS" : "GCAS-";
        if (GUILayout.Button(new GUIContent(gcasText, "Toggle Auto-GCAS"), _styleButton))
        {
            APData.GCASEnabled = !APData.GCASEnabled;
            if (!APData.GCASEnabled)
            {
                APData.GCASActive = false;
            }

            GUI.FocusControl(null);
        }

        GUI.backgroundColor = APData.ALSActive ? Color.green : Color.white;
        if (GUILayout.Button(new GUIContent(APData.ALSActive ? "ALS" : "ALS-", "autoland"), _styleButton))
        {
            APData.ALSActive = !APData.ALSActive;
            if (!APData.ALSActive)
            {
                APData.ALSStatusText = "";
                APData.LocalPilot?.SwitchState(APData.LocalPilot.playerState);
            }
            else
            {
                FactionHQ hq = APData.LocalAircraft?.NetworkHQ;
                if (hq != null)
                {
                    if (hq.GetAirbases().Any())
                    {
                        APData.LocalPilot?.SwitchState(new AIPilotLandingState());
                    }
                    else
                    {
                        APData.ALSStatusText = "ALS: NO AIRBASE";
                    }
                }
            }

            GUI.FocusControl(null);
        }

        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        bool newSaveState = GUILayout.Toggle(APData.SaveMapState,
            new GUIContent("Lock", "Keep map zoom/pos when reopening."));
        if (newSaveState != APData.SaveMapState)
        {
            APData.SaveMapState = newSaveState;
            SaveMapState.Value = newSaveState;
        }

        if (GUILayout.Button(new GUIContent("Center", "Pan to the center of the map"), _styleButton))
        {
            DynamicMap map = SceneSingleton<DynamicMap>.i;
            if (map != null)
            {
                Vector2 stationary = map.stationaryOffset;
                map.positionOffset = -stationary;
                map.followingCamera = false;
            }

            GUI.FocusControl(null);
        }

        if (GUILayout.Button(new GUIContent("Aircraft", "Pan map to your aircraft"), _styleButton))
        {
            DynamicMap map = SceneSingleton<DynamicMap>.i;
            if (map != null)
            {
                map.positionOffset = Vector2.zero;
                map.CenterMap();
            }

            GUI.FocusControl(null);
        }

        GUILayout.EndHorizontal();

        // nav
        GUILayout.BeginHorizontal();
        bool newNavState =
            GUILayout.Toggle(APData.NavEnabled, new GUIContent("Nav mode", "switch for waypoint ap mode."));
        if (newNavState != APData.NavEnabled)
        {
            APData.NavEnabled = newNavState;
            if (APData.NavEnabled && (APData.TargetRoll == -999f || APData.TargetRoll == 0f))
            {
                APData.TargetRoll = DefaultCRLimit.Value;
            }

            SyncMenuValues();
        }

        NavCycle.Value = GUILayout.Toggle(NavCycle.Value,
            new GUIContent("Cycle wp", "On: cycles to next wp upon reaching wp, Off: Deletes upon reaching wp"));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        // Nav waypoint list
        if (APData.NavQueue.Count > 0)
        {
            Vector3 playerPos = APData.PlayerRB != null
                ? APData.PlayerRB.position.ToGlobalPosition().AsVector3()
                : Vector3.zero;
            float distNext = Vector2.Distance(new Vector2(playerPos.x, playerPos.z),
                new Vector2(APData.NavQueue[0].x, APData.NavQueue[0].z));

            // next wp row
            string nextDistStr =
                ModUtils.ProcessGameString(UnitConverter.DistanceReading(distNext), DistShowUnit.Value);
            GUILayout.Label(new GUIContent($"Next: {nextDistStr}", "Distance to next wp"), _styleLabel);

            float etaNext = distNext / APData.SpeedEma;
            string sEtaNext = etaNext > 3599
                ? TimeSpan.FromSeconds(etaNext).ToString(@"h\:mm\:ss")
                : TimeSpan.FromSeconds(etaNext).ToString(@"mm\:ss");
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

                string totalDistStr =
                    ModUtils.ProcessGameString(UnitConverter.DistanceReading(distTotal), DistShowUnit.Value);
                GUILayout.Label(new GUIContent($"Total: {totalDistStr}", "Total distance of flight plan"), _styleLabel);

                float etaTotal = distTotal / APData.SpeedEma;
                string sEtaTotal = etaTotal > 3599
                    ? TimeSpan.FromSeconds(etaTotal).ToString(@"h\:mm\:ss")
                    : TimeSpan.FromSeconds(etaTotal).ToString(@"mm\:ss");
                GUILayout.Label($" ETA: {sEtaTotal}", _styleLabel);
            }

            GUILayout.EndHorizontal();
            // nav control row
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Skip wp", "delete next point"), _styleButton))
            {
                APData.NavQueue.RemoveAt(0);
                if (APData.NavQueue.Count == 0)
                {
                    APData.NavEnabled = false;
                }

                RefreshNavVisuals();
            }

            if (GUILayout.Button(new GUIContent("Undo wp", "delete last point"), _styleButton))
            {
                if (APData.NavQueue.Count > 0)
                {
                    APData.NavQueue.RemoveAt(APData.NavQueue.Count - 1);
                    if (APData.NavQueue.Count == 0)
                    {
                        APData.NavEnabled = false;
                    }

                    RefreshNavVisuals();
                }
            }

            if (GUILayout.Button(new GUIContent("Clear all", "delete all points. (much self explanatory)"),
                    _styleButton))
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
            GUILayout.Label(
                new GUIContent("RMB the map to set wp.\nShift+RMB for multiple.",
                    "Here, RMB means Right Mouse Button click.\nShift + RMB means Shift key + Right Mouse Button.\nThis will only work on the map screen.\nIf nothing is happening after you drew a hundred lines on screen,\nthen you may have just forgotten to engage the autopilot with the equals key/set values button/engage button\n(tbh the original text was probably self explanatory)\n\nAlso if you see the last waypoint hovering around, just ignore it for now, afaik it's only a cosmetic defect.\n\nOh also, the tooltip logic is inspired by Firefox.\nIf you hover over something for some time on gui, it will show tooltip.\nIf you then your mouse away from the position you held your mouse in,\nthe tooltip will disappear and won't reappear until your mouse leaves the item."),
                _styleLabel);

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
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

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

            if (tooltipRect.xMax > Screen.width)
            {
                tooltipRect.x = _stationaryPos.x - size.x - 5;
            }

            if (tooltipRect.yMax > Screen.height)
            {
                tooltipRect.y = _stationaryPos.y - size.y - 5;
            }

            GUI.Box(tooltipRect, content, style);
        }

        if (Event.current.type == EventType.Repaint)
        {
            _lastActiveTooltip = "";
        }
    }

    public static void RefreshNavVisuals()
    {
        try
        {
            foreach (GameObject obj in APData.NavVisuals)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }

            APData.NavVisuals.Clear();

            DynamicMap map = SceneSingleton<DynamicMap>.i;
            if (APData.NavQueue.Count == 0 || map == null || APData.PlayerRB == null)
            {
                return;
            }

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

                float angle = (-Mathf.Atan2(end.x - start.x, end.y - start.y) * Mathf.Rad2Deg) + 180f;

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
                    if (marker.TryGetComponent(out Image mImg))
                    {
                        mImg.color = navCol;
                    }

                    APData.NavVisuals.Add(marker);
                }

                DrawLine(lastPoint, currentMap, i == 0 ? "AP_NavLine_Player" : "AP_NavLine");
                lastPoint = currentMap;
            }

            if (!NavCycle.Value || APData.NavQueue.Count <= 1)
            {
                return;
            }

            Vector3 firstMap = new(APData.NavQueue[0].x * factor, APData.NavQueue[0].z * factor, 0f);
            DrawLine(lastPoint, firstMap, "AP_NavLine_Loop", true);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[RefreshNavVisuals] Error: {ex}");
            IsBroken = true;
        }
    }

    public static void CleanUpFBW()
    {
        if (!APData.FBWDisabled)
        {
            return;
        }

        APData.FBWDisabled = false;
        UpdateFBWState();
    }

    public static void UpdateFBWState()
    {
        if (APData.LocalAircraft == null)
        {
            return;
        }

        bool shouldDisable = APData.FBWDisabled;
        if (APData.IsMultiplayerCached)
        {
            shouldDisable = false;
            APData.FBWDisabled = false;
        }

        try
        {
            ControlsFilter filterObj = APData.LocalAircraft.controlsFilter;
            if (filterObj == null)
            {
                return;
            }

            (bool, float[]) tupleResult = filterObj.GetFlyByWireParameters();
            float[] currentTuning = tupleResult.Item2;
            filterObj.SetFlyByWireParameters(!shouldDisable, currentTuning);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[UpdateFBWState] Error: {ex.Message}");
            IsBroken = true;
        }
    }

    private void OnSceneUnloaded(Scene scene)
    {
        try
        {
            if (string.Equals(scene.name, "GameWorld", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            APData.Reset();
            ControlOverridePatch.Reset();
            HUDVisualsPatch.Reset();
            CleanUpFBW();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[OnSceneUnloaded] Error: {ex}");
            IsBroken = true;
        }
    }

    public static bool IsMultiplayer()
    {
        if (Time.time < APData.NextMultiplayerCheck)
        {
            return APData.IsMultiplayerCached;
        }

        APData.NextMultiplayerCheck = Time.time + 2.0f;

        try
        {
            NetworkServer serverInstance = FindObjectOfType<NetworkServer>();
            if (serverInstance != null)
            {
                bool isServerActive = serverInstance.Active;
                if (isServerActive)
                {
                    if (serverInstance.AllPlayers is { } players &&
                        players.Count > 1)
                    {
                        APData.IsMultiplayerCached = true;
                        return true;
                    }
                }
            }

            NetworkClient clientInstance = FindObjectOfType<NetworkClient>();
            if (clientInstance != null)
            {
                bool isClientActive = clientInstance.Active;
                bool isHost = clientInstance.IsHost;

                if (isClientActive && !isHost)
                {
                    APData.IsMultiplayerCached = true;
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[IsMultiplayer] Error: {ex}");
            IsBroken = true;
        }

        APData.IsMultiplayerCached = false;
        return false;
    }

    private void ClearAllStatics()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        foreach (Type type in assembly.GetTypes())
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (FieldInfo field in fields)
            {
                if (field.IsLiteral || field.IsInitOnly)
                {
                    continue;
                }

                try
                {
                    object defaultValue =
                        field.FieldType.IsValueType ? Activator.CreateInstance(field.FieldType) : null;
                    field.SetValue(null, defaultValue);
                }
                catch
                {
                    // ignored
                }
            }
        }
    }

    private enum RectEdge { None, Top, Bottom, Left, Right, TopLeft, TopRight, BottomLeft, BottomRight }
}
