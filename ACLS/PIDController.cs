using System.Collections.Generic;
using UnityEngine;

namespace NOAutopilot.ACLS;

internal class PIDController
{
    public float targetState;

    public float Kp;

    public float Ki;

    public float Kd;

    public float lastError;

    public float lastOutput;

    public bool invert;

    private readonly Queue<float> errorBuffer;

    private readonly float bufferDuration;

    private float integralSum;

    public PIDController(float targetState, float Kp, float Ki, float Kd, float bufferDuration = 1f, bool invert = false)
    {
        this.targetState = targetState;
        this.Kp = Kp;
        this.Ki = Ki;
        this.Kd = Kd;
        lastError = 0f;
        this.invert = invert;
        this.bufferDuration = bufferDuration;
        int capacity = Mathf.CeilToInt(bufferDuration / Time.fixedDeltaTime);
        errorBuffer = new Queue<float>(capacity);
        integralSum = 0f;
    }

    public float Update(float currentState)
    {
        float num = targetState - currentState;
        errorBuffer.Enqueue(num * Time.fixedDeltaTime);
        integralSum += num * Time.fixedDeltaTime;
        if (errorBuffer.Count > Mathf.CeilToInt(bufferDuration / Time.fixedDeltaTime))
        {
            integralSum -= errorBuffer.Dequeue();
        }
        float num2 = (num - lastError) / Time.fixedDeltaTime;
        lastError = num;
        float num3 = Kp * num + Ki * integralSum + Kd * num2;
        lastOutput = Mathf.Clamp(num3, -1f, 1f);
        if (invert)
        {
            lastOutput = 0f - lastOutput;
        }
        return lastOutput;
    }

    public void Reset()
    {
        errorBuffer.Clear();
        integralSum = 0f;
        lastError = 0f;
        lastOutput = 0f;
    }

    public void LogState()
    {
        Plugin.Logger.LogInfo($"Target: {targetState:0.00}," + $"Integral: {integralSum:0.00}, LastError: {lastError:0.00}, LastOutput: {lastOutput:0.00}");
    }

    public static PIDController FromConfig(float targetState, PIDConfig config)
    {
        return new PIDController(targetState, config.Kp, config.Ki, config.Kd, config.BufferDuration, config.Invert);
    }
}
