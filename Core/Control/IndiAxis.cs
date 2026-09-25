using UnityEngine;

namespace NOAutopilot.Core.Control;

/// <summary>
/// One channel of a sensor-based Incremental Nonlinear Dynamic Inversion (INDI) controller.
/// </summary>
internal sealed class IndiAxis
{
    private readonly LowPass2 _measFilter = new();
    private readonly LowPass2 _inputFilter = new();
    private readonly SampleHistory _history = new(8);

    private float _prevMeasF;
    private bool _initialized;

    // online estimation of the effectiveness multiplier (eta) and the state derivative term (F)
    private float _f;
    private float _p11 = 10f, _p12, _p22 = 10f;
    private float _prevU0F, _prevDerivF, _prevMeasFForRls;
    private int _oscillationCount;
    private float _prevIncrement;

    public float Command { get; private set; }
    public float Derivative { get; private set; }
    public float MeasurementFiltered { get; private set; }
    public float EffectivenessUsed { get; private set; }
    public float ActuatorModel { get; private set; }
    public float Eta { get; private set; } = 1f;

    /// <summary>Safety factor applied to the effectiveness.</summary>
    public float EffectivenessMargin { get; set; } = 1.3f;

    /// <summary>Resets the channel so the next step starts bumplessly from <paramref name="currentInput"/>.</summary>
    public void Reset(float currentInput, float measurement)
    {
        currentInput = ControlMath.Finite(currentInput);
        measurement = ControlMath.Finite(measurement);
        _history.Fill(currentInput);
        ActuatorModel = currentInput;
        _inputFilter.Reset(currentInput);
        _measFilter.Reset(measurement);
        _prevMeasF = measurement;
        _prevU0F = currentInput;
        _prevDerivF = 0f;
        _prevMeasFForRls = measurement;
        _prevIncrement = 0f;
        _oscillationCount = 0;
        Command = currentInput;
        Derivative = 0f;
        MeasurementFiltered = measurement;
        _initialized = true;
    }

    /// <summary>Forget the learned effectiveness (e.g. new aircraft).</summary>
    public void ResetEstimator()
    {
        Eta = 1f;
        _f = 0f;
        _p11 = 10f;
        _p12 = 0f;
        _p22 = 10f;
    }

    public void Track(float appliedInput, float measurement, float cutoff, float actuatorTau, float actuatorRate,
        int delayTicks, float dt)
    {
        appliedInput = ControlMath.Finite(appliedInput, Command);
        if (!_initialized)
        {
            Reset(appliedInput, measurement);
            return;
        }

        Configure(cutoff, dt);
        float measF = _measFilter.Step(ControlMath.Finite(measurement, MeasurementFiltered));
        Derivative = (measF - _prevMeasF) / dt;
        _prevMeasF = measF;
        MeasurementFiltered = measF;

        AdvanceActuator(delayTicks, actuatorTau, actuatorRate, dt);
        _inputFilter.Step(ActuatorModel);

        _history.Push(appliedInput);
        Command = appliedInput;
        _prevU0F = _inputFilter.Value;
        _prevDerivF = Derivative;
        _prevMeasFForRls = measF;
        _prevIncrement = 0f;
    }

    /// <summary>
    /// INDI
    /// </summary>
    /// <param name="measurement">Controlled variable (e.g. body rate).</param>
    /// <param name="nu">Desired derivative of the controlled variable (virtual control).</param>
    /// <param name="gPrior">A-priori control effectiveness d(y')/du (positive).</param>
    /// <param name="min">Lower input limit.</param>
    /// <param name="max">Upper input limit.</param>
    /// <param name="rateLimit">Max command change per second (0 = none).</param>
    /// <param name="delayTicks">Input to measurement delay in ticks.</param>
    /// <param name="cutoff">Synchronized filter cut-off (rad/s).</param>
    /// <param name="adapt">Allow the online estimator to update.</param>
    /// <param name="actuatorTau">Actuator time constant (s), 0 = instantaneous.</param>
    /// <param name="actuatorRate">Actuator rate limit (input units / s), 0 = none.</param>
    /// <param name="dt">Tick length.</param>
    public float Step(float measurement, float nu, float gPrior, float min, float max, float rateLimit,
        int delayTicks, float cutoff, bool adapt, float actuatorTau, float actuatorRate, float dt)
    {
        if (!_initialized)
        {
            Reset(Mathf.Clamp(0f, min, max), measurement);
        }

        Configure(cutoff, dt);

        float measF = _measFilter.Step(ControlMath.Finite(measurement, MeasurementFiltered));
        float deriv = (measF - _prevMeasF) / dt;
        _prevMeasF = measF;
        MeasurementFiltered = measF;
        Derivative = deriv;

        AdvanceActuator(delayTicks, actuatorTau, actuatorRate, dt);
        float u0 = _inputFilter.Step(ActuatorModel);

        gPrior = Mathf.Max(ControlMath.Finite(gPrior, 1f), 1e-4f);
        float g = gPrior * Eta * EffectivenessMargin;
        if (g < 1e-4f)
        {
            g = 1e-4f;
        }

        EffectivenessUsed = g;

        float increment = (ControlMath.Finite(nu) - deriv) / g;
        float u = u0 + increment;

        u = Mathf.Clamp(u, min, max);
        if (rateLimit > 0f)
        {
            float last = _history.Get(0);
            float step = rateLimit * dt;
            u = Mathf.Clamp(u, last - step, last + step);
        }

        if (!ControlMath.IsFinite(u))
        {
            u = _history.Get(0);
        }

        if (adapt)
        {
            UpdateEstimator(u0, deriv, measF, gPrior, min, max);
            MonitorOscillation(increment);
        }
        else
        {
            _prevU0F = u0;
            _prevDerivF = deriv;
            _prevMeasFForRls = measF;
        }

        _history.Push(u);
        Command = u;
        return u;
    }

    public float StepRateCommand(float measurement, float reference, float gainPrior, float min, float max,
        float rateLimit, int delayTicks, float compensationCutoff, float compensationGain, bool adapt,
        float responseTau, float dt)
    {
        if (!_initialized)
        {
            Reset(Mathf.Clamp(0f, min, max), measurement);
        }

        Configure(compensationCutoff, dt);
        float measF = _measFilter.Step(ControlMath.Finite(measurement, MeasurementFiltered));
        Derivative = (measF - _prevMeasF) / dt;
        _prevMeasF = measF;
        MeasurementFiltered = measF;

        AdvanceActuator(delayTicks, responseTau, 0f, dt);
        float u0 = _inputFilter.Step(ActuatorModel);

        gainPrior = ControlMath.Finite(gainPrior, 1f);
        if (Mathf.Abs(gainPrior) < 1e-4f)
        {
            gainPrior = 1e-4f;
        }

        float k = gainPrior * Eta * EffectivenessMargin;
        if (Mathf.Abs(k) < 1e-4f)
        {
            k = 1e-4f;
        }

        EffectivenessUsed = k;

        float kc = compensationGain;
        float increment = (ControlMath.Finite(reference) - (kc * measF)) / k;
        float u = Mathf.Clamp((kc * u0) + increment, min, max);
        if (rateLimit > 0f)
        {
            float last = _history.Get(0);
            float step = rateLimit * dt;
            u = Mathf.Clamp(u, last - step, last + step);
        }

        if (!ControlMath.IsFinite(u))
        {
            u = _history.Get(0);
        }

        if (adapt)
        {
            UpdateGainEstimate(u0, measF, gainPrior, min, max);
            MonitorOscillation(u - _history.Get(0));
        }
        else
        {
            _prevU0F = u0;
            _prevMeasFForRls = measF;
        }

        _history.Push(u);
        Command = u;
        return u;
    }

    private void UpdateGainEstimate(float u0, float measF, float gainPrior, float min, float max)
    {
        float phi = gainPrior * (u0 - _prevU0F);
        float y = measF - _prevMeasFForRls;
        _prevU0F = u0;
        _prevMeasFForRls = measF;

        float span = max - min;
        bool saturated = u0 <= min + (0.03f * span) || u0 >= max - (0.03f * span);
        if (saturated || Mathf.Abs(u0 - _prevU0F) > 0.2f * span || Mathf.Abs(phi) < 1e-4f * Mathf.Abs(gainPrior))
        {
            return;
        }

        const float lambda = 0.998f;
        float den = lambda + (phi * phi * _p11);
        float kk = _p11 * phi / den;
        float e = y - (Eta * phi);
        Eta += kk * e;
        _p11 = (_p11 - (kk * phi * _p11)) / lambda;
    }

    private void AdvanceActuator(int delayTicks, float tau, float rate, float dt)
    {
        // command that reaches the actuator this tick
        float target = _history.Get(Mathf.Max(delayTicks - 1, 0));
        float next = tau > 1e-4f ? ActuatorModel + ((target - ActuatorModel) * (1f - Mathf.Exp(-dt / tau))) : target;
        if (rate > 0f)
        {
            float step = rate * dt;
            next = Mathf.Clamp(next, ActuatorModel - step, ActuatorModel + step);
        }

        ActuatorModel = ControlMath.Finite(next, target);
    }

    private void Configure(float cutoff, float dt)
    {
        _measFilter.Configure(cutoff, dt);
        _inputFilter.Configure(cutoff, dt);
    }

    private void UpdateEstimator(float u0, float deriv, float measF, float gPrior, float min, float max)
    {
        float phi1 = gPrior * (u0 - _prevU0F);
        float phi2 = measF - _prevMeasFForRls;
        float y = deriv - _prevDerivF;

        _prevU0F = u0;
        _prevDerivF = deriv;
        _prevMeasFForRls = measF;

        float span = max - min;
        bool saturated = u0 <= min + (0.03f * span) || u0 >= max - (0.03f * span);
        if (Mathf.Abs(u0 - _prevU0F) > 0f && saturated)
        {
            return;
        }

        if (Mathf.Abs(phi1) < 2e-4f * gPrior * span || saturated)
        {
            return;
        }

        const float lambda = 0.998f;
        float pp1 = (_p11 * phi1) + (_p12 * phi2);
        float pp2 = (_p12 * phi1) + (_p22 * phi2);
        float den = lambda + (phi1 * pp1) + (phi2 * pp2);
        if (den < 1e-6f)
        {
            return;
        }

        float k1 = pp1 / den;
        float k2 = pp2 / den;
        float e = y - (Eta * phi1) - (_f * phi2);

        Eta += k1 * e;
        _f += k2 * e;

        _p11 = (_p11 - (k1 * pp1)) / lambda;
        _p12 = (_p12 - (k1 * pp2)) / lambda;
        _p22 = (_p22 - (k2 * pp2)) / lambda;

        float trace = _p11 + _p22;
        if (!ControlMath.IsFinite(trace) || trace > 100f)
        {
            _p11 = 10f;
            _p12 = 0f;
            _p22 = 10f;
        }
    }

    private void MonitorOscillation(float increment)
    {
        const float threshold = 0.004f;
        if (Mathf.Abs(increment) < threshold)
        {
            _oscillationCount = Mathf.Max(_oscillationCount - 1, 0);
            return;
        }

        bool flip = _prevIncrement != 0f && Mathf.Sign(increment) != Mathf.Sign(_prevIncrement);
        _prevIncrement = increment;
        if (flip)
        {
            _oscillationCount += 12;
        }
        else
        {
            _oscillationCount = Mathf.Max(_oscillationCount - 1, 0);
        }

        if (_oscillationCount > 120)
        {
            Eta *= 1.25f;
            _p11 = 10f;
            _p12 = 0f;
            _p22 = 10f;
            _oscillationCount = 0;
        }
    }
}
