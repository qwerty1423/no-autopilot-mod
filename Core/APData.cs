extern alias JetBrains;
using System.Collections.Generic;

using UnityEngine;

using Object = UnityEngine.Object;

namespace NOAutopilot.Core;

public static class APData
{
    public static bool Enabled;
    public static bool UseSetValues;
    public static bool GCASEnabled = true;
    public static bool AutoJammerActive;
    public static bool GCASActive;
    public static bool GCASWarning;
    public static bool AllowExtremeThrottle;
    public static bool SpeedHoldIsMach;
    public static bool NavEnabled;
    public static bool FBWDisabled;
    public static float TargetAlt = -1f;
    public static float TargetRoll = -999f;
    public static float TargetSpeed = -1f;
    public static float TargetCourse = -1f;
    public static float CurrentAlt;
    public static float CurrentRoll;
    public static float CurrentMaxClimbRate = -1f;
    public static float SpeedEma;
    public static float LastOverrideInputTime = -999f;
    public static Pilot LocalPilot;
    public static List<Vector3> NavQueue = [];
    public static List<GameObject> NavVisuals = [];
    public static Transform PlayerTransform;
    public static Rigidbody PlayerRB;
    public static Aircraft LocalAircraft;
    public static WeaponManager LocalWeaponManager;
    public static float GCASConverge;
    public static bool IsMultiplayerCached;
    public static float NextMultiplayerCheck;
    public static bool SaveMapState;
    public static Vector2 SavedMapPos = Vector2.zero;
    public static float SavedMapZoom = 1f;
    public static bool SavedMapFollow = true;
    public static bool MapStateStored;
    public static float BloodPressure = 1f;

    public static bool IsConscious = true;

    // public static List<Component> LocalLandingGears = [];
    public static bool IsOnGround;

    // ALS
    public static bool ALSActive;
    public static string ALSStatusText = "";
    public static Color ALSStatusColor = Color.white;

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
        // LocalLandingGears.Clear();
        IsOnGround = false;
        ALSActive = false;
        ALSStatusText = "";
        ALSStatusColor = Color.white;

        NavQueue.Clear();
        foreach (GameObject obj in NavVisuals)
        {
            if (obj != null)
            {
                Object.Destroy(obj);
            }
        }

        NavVisuals.Clear();
    }
}
