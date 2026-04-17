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

    public static bool IsTestActive => _testActive;
    private static bool _testActive;
    private static bool _testPending;
    private static StepTarget _targetLoop;

    private static float _testSetpoint;
    private static float _startTime;
    private static readonly List<string> _data = new();

    private static float _preStepDuration = 2.0f;
    private static bool _stepFired;

    public static void RequestTest(StepTarget target)
    {
        if (_testActive || _testPending)
        {
            StopTest();
            return;
        }

        if (target == StepTarget.None) return;

        _targetLoop = target;
        _testPending = true;
        Plugin.Logger.LogInfo($"Step test pending for {target}. Waiting for control loop execution...");
    }

    // Used by patches to ensure dormant axes wake up when tested
    public static bool IsTesting(StepTarget target) => (_testActive || _testPending) && _targetLoop == target;

    // Injects the test setpoint if this loop is active
    public static float GetSetpoint(StepTarget target, float normalSetpoint, float currentMeasurement)
    {
        if (_targetLoop != target) return normalSetpoint;

        if (_testPending)
        {
            _testPending = false;
            _testActive = true;
            _stepFired = false;
            _startTime = Time.time;

            _testSetpoint = currentMeasurement + Plugin.StepTestMagnitude.Value;

            _data.Clear();
            _data.Add("Time,Input_u,Output_y,Setpoint_r");
            Plugin.Logger.LogInfo($"Starting recording for {target}. Step input in {_preStepDuration}s...");
        }

        if (_testActive)
        {
            float elapsed = Time.time - _startTime;

            if (elapsed < _preStepDuration)
            {
                return currentMeasurement;
            }

            if (!_stepFired)
            {
                _stepFired = true;
                Plugin.Logger.LogInfo("applying step input...");
            }
            return _testSetpoint;
        }

        return normalSetpoint;
    }

    public static void Log(StepTarget target, double u, double y, double r)
    {
        if (_testActive && _targetLoop == target)
        {
            float t = Time.time - _startTime;
            var ci = CultureInfo.InvariantCulture;

            _data.Add($"{t.ToString("F4", ci)},{u.ToString("F4", ci)},{y.ToString("F4", ci)},{r.ToString("F4", ci)}");

            // Adjust total duration to include the pre-step
            if (t >= Plugin.StepTestDuration.Value + _preStepDuration)
            {
                StopTest();
            }
        }
    }

    public static void StopTest()
    {
        if (!_testActive && !_testPending) return;

        _testPending = false;
        if (_testActive)
        {
            _testActive = false;
            string filename = $"PID_{_targetLoop}_Step_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string path = Path.Combine(Paths.PluginPath, filename);
            try
            {
                File.WriteAllLines(path, _data);
                Plugin.Logger.LogInfo($"Saved PID Step Test data to {path}");
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"Failed to save PID test data: {e.Message}");
            }
        }
        _targetLoop = StepTarget.None;
    }
}
