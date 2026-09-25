using UnityEngine;

namespace NOAutopilot.Core.Control;

internal sealed class ResponseIdentifier(float tauPrior, float gainPrior)
{
    private static readonly float[] Taus = [0.06f, 0.09f, 0.13f, 0.19f, 0.27f, 0.38f, 0.54f, 0.75f, 1.05f, 1.5f];

    private readonly float[] _model = new float[Taus.Length];
    private readonly float[] _modelBp = new float[Taus.Length];
    private readonly float[] _modelLp = new float[Taus.Length];
    private readonly float[] _sya = new float[Taus.Length];
    private readonly float[] _saa = new float[Taus.Length];
    private readonly SampleHistory _uHist = new(8);
    private float _yLp, _yBp, _syy;
    private bool _init;
    private int _samples;

    public float Tau { get; private set; } = tauPrior;
    public float Gain { get; private set; } = gainPrior;
    public float Fit { get; private set; }

    public bool Confident => _samples > 200 && Fit > 0.6f;

    public void Reset(float tauPrior, float gainPrior)
    {
        Tau = tauPrior;
        Gain = gainPrior;
        _init = false;
        _samples = 0;
        Fit = 0f;
        _syy = 0f;
        for (int i = 0; i < Taus.Length; i++)
        {
            _sya[i] = _saa[i] = 0f;
        }
    }

    /// <summary>
    /// Compute gains?
    /// </summary>
    /// <param name="u">Input actually applied (stick, controller convention).</param>
    /// <param name="y">Measured rate.</param>
    /// <param name="delayTicks">Transport delay.</param>
    /// <param name="gainPrior">Prior gain (rate per unit input), also used to scale the excitation threshold.</param>
    /// <param name="learn">Allow updates (airborne, no limiter active, ...).</param>
    /// <param name="dt">Tick.</param>
    public void Update(float u, float y, int delayTicks, float gainPrior, bool learn, float dt)
    {
        if (!ControlMath.IsFinite(u) || !ControlMath.IsFinite(y))
        {
            return;
        }

        if (!_init)
        {
            _uHist.Fill(u);
            for (int i = 0; i < Taus.Length; i++)
            {
                _model[i] = u;
                _modelLp[i] = u * gainPrior;
            }

            _yLp = y;
            _init = true;
        }

        _uHist.Push(u);
        float ud = _uHist.Get(Mathf.Max(delayTicks - 1, 0));

        const float wl = 8f, wh = 0.3f;
        float al = 1f - Mathf.Exp(-wl * dt);
        float ah = Mathf.Exp(-wh * dt);

        float yPrevLp = _yLp;
        _yLp += al * (y - _yLp);
        _yBp = ah * (_yBp + (_yLp - yPrevLp));

        // forgetting: ~12 s memory
        float lambda = Mathf.Exp(-dt / 12f);
        bool update = learn;

        float gp = Mathf.Max(Mathf.Abs(gainPrior), 1e-3f);
        if (update)
        {
            _syy = (lambda * _syy) + (_yBp * _yBp);
        }

        int best = -1;
        float bestRes = float.MaxValue;
        for (int i = 0; i < Taus.Length; i++)
        {
            float a = 1f - Mathf.Exp(-dt / Taus[i]);
            _model[i] += a * (ud - _model[i]);
            float scaled = _model[i] * gp;
            float prevLp = _modelLp[i];
            _modelLp[i] += al * (scaled - _modelLp[i]);
            _modelBp[i] = ah * (_modelBp[i] + (_modelLp[i] - prevLp));

            if (!update)
            {
                continue;
            }

            _sya[i] = (lambda * _sya[i]) + (_yBp * _modelBp[i]);
            _saa[i] = (lambda * _saa[i]) + (_modelBp[i] * _modelBp[i]);

            if (_saa[i] > 1e-9f)
            {
                float res = _syy - (_sya[i] * _sya[i] / _saa[i]);
                if (res < bestRes && _sya[i] > 0f)
                {
                    bestRes = res;
                    best = i;
                }
            }
        }

        if (!update || best < 0)
        {
            return;
        }

        float excitation = _saa[best] / (1f / (1f - lambda));
        if (excitation < 1e-5f * gp * gp)
        {
            return;
        }

        _samples++;
        float fit = _syy > 1e-9f ? Mathf.Clamp01(1f - (bestRes / _syy)) : 0f;
        Fit = Mathf.Lerp(Fit, fit, 0.02f);
        if (fit < 0.5f)
        {
            return;
        }

        float tauBest = Taus[best];
        if (best > 0 && best < Taus.Length - 1)
        {
            float r0 = Residual(best - 1), r1 = bestRes, r2 = Residual(best + 1);
            float den = r0 - (2f * r1) + r2;
            if (den > 1e-9f)
            {
                float offset = Mathf.Clamp(0.5f * (r0 - r2) / den, -0.5f, 0.5f);
                float l0 = Mathf.Log(Taus[best - 1]), l1 = Mathf.Log(Taus[best]), l2 = Mathf.Log(Taus[best + 1]);
                float step = offset >= 0f ? l2 - l1 : l1 - l0;
                tauBest = Mathf.Exp(l1 + (offset * step));
            }
        }

        float k = _sya[best] / _saa[best] * gp;
        float w = 0.01f * fit;
        Tau = Mathf.Exp(Mathf.Lerp(Mathf.Log(Tau), Mathf.Log(tauBest), w));
        if (k > 0f && ControlMath.IsFinite(k))
        {
            Gain = Mathf.Exp(Mathf.Lerp(Mathf.Log(Mathf.Max(Gain, 1e-4f)), Mathf.Log(k), w));
        }
    }

    private float Residual(int i) => _saa[i] > 1e-9f ? _syy - (_sya[i] * _sya[i] / _saa[i]) : float.MaxValue;
}
