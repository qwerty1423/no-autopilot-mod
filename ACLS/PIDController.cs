using UnityEngine;
namespace NOAutopilot.ACLS;

internal class PIDController(float targetState, float Kp, float Ki, float Kd, float iLimit = 0f, bool invert = false)
{
    public float targetState = targetState;
    public float Kp = Kp, Ki = Ki, Kd = Kd;
    public float lastError = 0f, lastOutput;
    public bool invert = invert;

    public float MinOutput = -1f;
    public float MaxOutput = 1f;

    public float ILimit = iLimit;

    private float integralSum = 0f;

    public float Update(float currentState)
    {
        float error = targetState - currentState;
        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) dt = 0.02f;

        integralSum += error * dt;

        if (ILimit > 0)
        {
            integralSum = Mathf.Clamp(integralSum, -ILimit, ILimit);
        }

        float derivative = (error - lastError) / dt;
        lastError = error;

        float output = (Kp * error) + (Ki * integralSum) + (Kd * derivative);
        lastOutput = Mathf.Clamp(output, MinOutput, MaxOutput);

        return invert ? -lastOutput : lastOutput;
    }

    public void Reset()
    {
        integralSum = 0f;
        lastError = 0f;
        lastOutput = 0f;
    }

    public static PIDController FromConfig(float targetState, PIDConfig config)
    {
        return new PIDController(targetState, config.Kp, config.Ki, config.Kd, config.ILimit, config.Invert);
    }
}
