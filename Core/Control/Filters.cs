using System;

using UnityEngine;

namespace NOAutopilot.Core.Control;

/// <summary>
/// Second order Butterworth low-pass filter, discretized with a pre-warped bilinear transform.
/// Used in pairs with identical coefficients to keep the INDI feedback signals synchronized.
/// </summary>
internal sealed class LowPass2
{
    private float _b0, _b1, _b2, _a1, _a2;
    private float _z1, _z2;
    private float _cutoff = -1f, _dt = -1f;
    private bool _initialized;

    public float Value { get; private set; }

    public void Configure(float cutoffRadPerSec, float dt)
    {
        dt = Mathf.Max(dt, 1e-4f);
        // stay well below Nyquist
        cutoffRadPerSec = Mathf.Clamp(cutoffRadPerSec, 0.05f, 0.8f * Mathf.PI / dt);

        if (Mathf.Approximately(cutoffRadPerSec, _cutoff) && Mathf.Approximately(dt, _dt))
        {
            return;
        }

        _cutoff = cutoffRadPerSec;
        _dt = dt;

        float k = Mathf.Tan(cutoffRadPerSec * dt * 0.5f);
        float k2 = k * k;
        const float sqrt2 = 1.41421356f;
        float norm = 1f / (1f + (sqrt2 * k) + k2);
        _b0 = k2 * norm;
        _b1 = 2f * _b0;
        _b2 = _b0;
        _a1 = 2f * (k2 - 1f) * norm;
        _a2 = (1f - (sqrt2 * k) + k2) * norm;

        if (_initialized)
        {
            // keep the output continuous when coefficients change
            Reset(Value);
        }
    }

    public void Reset(float value)
    {
        Value = value;
        _z1 = value * (1f - _b0);
        _z2 = value * (_b2 - _a2);
        _initialized = true;
    }

    public float Step(float x)
    {
        if (!ControlMath.IsFinite(x))
        {
            x = Value;
        }

        if (!_initialized)
        {
            Reset(x);
            return x;
        }

        float y = (_b0 * x) + _z1;
        _z1 = (_b1 * x) - (_a1 * y) + _z2;
        _z2 = (_b2 * x) - (_a2 * y);

        if (!ControlMath.IsFinite(y))
        {
            Reset(x);
            return x;
        }

        Value = y;
        return y;
    }
}

/// <summary>First order low-pass (exponential smoothing with a time constant).</summary>
internal sealed class LowPass1
{
    private bool _initialized;
    public float Value { get; private set; }

    public void Reset(float value)
    {
        Value = value;
        _initialized = true;
    }

    public float Step(float x, float timeConstant, float dt)
    {
        if (!ControlMath.IsFinite(x))
        {
            return Value;
        }

        if (!_initialized || timeConstant <= 0f)
        {
            Reset(x);
            return x;
        }

        float a = 1f - Mathf.Exp(-dt / timeConstant);
        Value += a * (x - Value);
        return Value;
    }
}

/// <summary>Fixed length history of past samples (index 0 = most recent).</summary>
internal sealed class SampleHistory
{
    private readonly float[] _buffer;
    private int _head;

    public SampleHistory(int capacity)
    {
        _buffer = new float[Math.Max(capacity, 1)];
    }

    public int Capacity => _buffer.Length;

    public void Fill(float value)
    {
        for (int i = 0; i < _buffer.Length; i++)
        {
            _buffer[i] = value;
        }
    }

    public void Push(float value)
    {
        _head = (_head + 1) % _buffer.Length;
        _buffer[_head] = value;
    }

    /// <summary>Returns the sample pushed <paramref name="age"/> pushes ago (0 = last pushed).</summary>
    public float Get(int age)
    {
        age = Mathf.Clamp(age, 0, _buffer.Length - 1);
        int idx = (_head - age) % _buffer.Length;
        if (idx < 0)
        {
            idx += _buffer.Length;
        }

        return _buffer[idx];
    }
}
