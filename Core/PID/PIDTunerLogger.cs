using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using BepInEx;

using UnityEngine;

namespace NOAutopilot.Core.PID;

public static class PIDTunerLogger
{
    public enum StepTarget { None, Roll, PitchAngle, Speed }

    public static bool IsTesting => _isTesting;
    public static StepTarget ActiveTarget => _activeTarget;

    private static bool _isTesting;
    private static StepTarget _activeTarget;
    private static StepTarget _pendingTarget;

    private static float _startTime;
    private static float _endTime;
    private static float _testSetpoint;
    private static readonly List<string> _data = new();

    public static void RequestTest(StepTarget target)
    {
        if (_isTesting)
        {
            StopTest();
            return;
        }
        _pendingTarget = target;
        Plugin.Logger.LogInfo($"PID Step Test requested for {target}. Waiting for control loop...");
    }

    public static bool IsPending(StepTarget target) => _pendingTarget == target;

    public static bool CheckAndStartTest(StepTarget target, float currentMeasurement, float stepMagnitude, float duration)
    {
        if (_pendingTarget == target && !_isTesting)
        {
            _isTesting = true;
            _activeTarget = target;
            _pendingTarget = StepTarget.None;

            _testSetpoint = currentMeasurement + stepMagnitude;
            _startTime = Time.time;
            _endTime = _startTime + duration;

            _data.Clear();
            // Columns: Time, Control Output (u), Process Variable (y), Setpoint (r)
            _data.Add("Time,Input_u,Output_y,Setpoint_r");

            Plugin.Logger.LogInfo($"Started PID Step Test for {target}. Base: {currentMeasurement:F2}, Step: {stepMagnitude:F2}, Target SP: {_testSetpoint:F2}");
            return true;
        }
        return false;
    }

    public static float GetTestSetpoint() => _testSetpoint;

    public static void LogData(float controlOutput_u, float processVariable_y, float setpoint_r)
    {
        if (!_isTesting) return;

        float t = Time.time - _startTime;
        var ci = CultureInfo.InvariantCulture;

        // Format: Time, u, y, r
        _data.Add($"{t.ToString("F4", ci)},{controlOutput_u.ToString("F4", ci)},{processVariable_y.ToString("F4", ci)},{setpoint_r.ToString("F4", ci)}");

        if (Time.time >= _endTime)
        {
            StopTest();
        }
    }

    public static void StopTest()
    {
        if (!_isTesting) return;
        _isTesting = false;

        string filename = $"PID_{_activeTarget}_Step_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        string path = Path.Combine(Paths.PluginPath, filename);
        File.WriteAllLines(path, _data);
        Plugin.Logger.LogInfo($"Saved PID Step Test data to {path}");

        _activeTarget = StepTarget.None;
    }
}
