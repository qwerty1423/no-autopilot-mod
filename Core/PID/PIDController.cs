extern alias JetBrains;
using UnityEngine;

namespace NOAutopilot.Core.PID;

public class PIDController
{
    private bool _initialized;
    private float _lastError, _lastMeasurement;
    public float Integral;

    public void Reset(float currentIntegral = 0f)
    {
        Integral = currentIntegral;
        _lastError = 0;
        _lastMeasurement = 0;
        _initialized = false;
    }

    public float Evaluate(float error, float measurement, float dt, float kp, float ki, float kd, float iLimit,
        bool useErrorDerivative = false, float? manualDerivative = null, float currentOutput = 0f,
        float limitThreshold = 0.95f,
        bool isAngle = false)
    {
        if (dt <= 0f)
        {
            return 0f;
        }

        if (!_initialized)
        {
            _lastError = error;
            _lastMeasurement = measurement;
            _initialized = true;
        }

        bool saturated = Mathf.Abs(currentOutput) >= limitThreshold;
        bool sameDirection = Mathf.Sign(error) == Mathf.Sign(currentOutput);

        if (!(saturated && sameDirection))
        {
            Integral += error * dt * ki;
        }

        Integral = Mathf.Clamp(Integral, -iLimit, iLimit);

        float derivative = manualDerivative ?? (useErrorDerivative
            ? isAngle
                ? Mathf.DeltaAngle(_lastError, error) / dt
                : (error - _lastError) / dt
            : isAngle
                ? -Mathf.DeltaAngle(_lastMeasurement, measurement) / dt
                : -(measurement - _lastMeasurement) / dt);

        _lastError = error;
        _lastMeasurement = measurement;

        return (error * kp) + Integral + (derivative * kd);
    }
}
