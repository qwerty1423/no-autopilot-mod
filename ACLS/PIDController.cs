using UnityEngine;
namespace NOAutopilot.ACLS;

internal class PIDController(float targetState, float Kp, float Ki, float Kd, bool invert = false)
{
    public float targetState = targetState;
    public float Kp = Kp;
    public float Ki = Ki;
    public float Kd = Kd;
    public float lastError = 0f;
    public float lastOutput;
    public bool invert = invert;

    public float MinOutput = -1f;
    public float MaxOutput = 1f;

    public float IntegralLimit = 1000f;

    private float integralSum = 0f;

    public float Update(float currentState)
    {
        float error = targetState - currentState;
        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) dt = 0.02f;

        integralSum += error * dt;

        if (IntegralLimit > 0)
        {
            integralSum = Mathf.Clamp(integralSum, -IntegralLimit, IntegralLimit);
        }

        float derivative = (error - lastError) / dt;
        lastError = error;

        float output = Kp * error + Ki * integralSum + Kd * derivative;

        lastOutput = Mathf.Clamp(output, MinOutput, MaxOutput);

        if (invert)
        {
            lastOutput = -lastOutput;
        }
        return lastOutput;
    }

    public void Reset()
    {
        integralSum = 0f;
        lastError = 0f;
        lastOutput = 0f;
    }

    public void LogState()
    {
        Plugin.Logger.LogInfo($"Target: {targetState:0.00}, Integral: {integralSum:0.00}, LastError: {lastError:0.00}, LastOutput: {lastOutput:0.00}");
    }

    public static PIDController FromConfig(float targetState, PIDConfig config)
    {
        return new PIDController(targetState, config.Kp, config.Ki, config.Kd, config.BufferDuration, config.Invert);
    }
}
