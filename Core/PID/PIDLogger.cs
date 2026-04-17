using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using BepInEx;

using UnityEngine;

namespace NOAutopilot.Core.PID;

public static class PIDLogger
{
    public enum StepTarget { None, Alt, VS, Angle, Roll, Crs, Spd, GCAS }

    public static bool IsTestActive { get; private set; }

    private static bool s_testPending;
    private static StepTarget s_targetLoop;

    private static float s_testSetpoint;
    private static float s_startTime;
    private static readonly List<string> Data = [];

    private const float PreStepDuration = 2.0f;
    private static bool s_stepFired;

    public static void RequestTest(StepTarget target)
    {
        if (IsTestActive || s_testPending)
        {
            StopTest();
            return;
        }

        if (target == StepTarget.None)
        {
            return;
        }

        s_targetLoop = target;
        s_testPending = true;
        Plugin.Logger.LogInfo($"Step test pending for {target}. Waiting for control loop execution...");
    }

    // Used by patches to ensure dormant axes wake up when tested
    public static bool IsTesting(StepTarget target) => (IsTestActive || s_testPending) && s_targetLoop == target;

    // Injects the test setpoint if this loop is active
    public static float GetSetpoint(StepTarget target, float normalSetpoint, float currentMeasurement)
    {
        if (s_targetLoop != target)
        {
            return normalSetpoint;
        }

        if (s_testPending)
        {
            s_testPending = false;
            IsTestActive = true;
            s_stepFired = false;
            s_startTime = Time.time;

            s_testSetpoint = currentMeasurement + Plugin.StepTestMagnitude.Value;

            Data.Clear();
            Data.Add("Time,Input_u,Output_y,Setpoint_r");
            Plugin.Logger.LogInfo($"Starting recording for {target}. Step input in {PreStepDuration}s...");
        }

        if (IsTestActive)
        {
            float elapsed = Time.time - s_startTime;

            if (elapsed < PreStepDuration)
            {
                return currentMeasurement;
            }

            if (!s_stepFired)
            {
                s_stepFired = true;
                Plugin.Logger.LogInfo("applying step input...");
            }
            return s_testSetpoint;
        }

        return normalSetpoint;
    }

    public static void Log(StepTarget target, double u, double y, double r)
    {
        if (IsTestActive && s_targetLoop == target)
        {
            float t = Time.time - s_startTime;
            var ci = CultureInfo.InvariantCulture;

            Data.Add($"{t.ToString("F4", ci)},{u.ToString("F4", ci)},{y.ToString("F4", ci)},{r.ToString("F4", ci)}");

            // Adjust total duration to include the pre-step
            if (t >= Plugin.StepTestDuration.Value + PreStepDuration)
            {
                StopTest();
            }
        }
    }

    public static void StopTest()
    {
        if (!IsTestActive && !s_testPending)
        {
            return;
        }

        s_testPending = false;
        if (IsTestActive)
        {
            IsTestActive = false;
            string filename = $"PID_{s_targetLoop}_Step_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string path = Path.Combine(Paths.PluginPath, filename);
            try
            {
                File.WriteAllLines(path, Data);
                Plugin.Logger.LogInfo($"Saved PID Step Test data to {path}");
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"Failed to save PID test data: {e.Message}");
            }
        }
        s_targetLoop = StepTarget.None;
    }
}
