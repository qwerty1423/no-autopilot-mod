using UnityEngine;

namespace NOAutopilot.Core.Control;

/// <summary>
/// MIMO incremental nonlinear dynamic inversion
/// Rows are body rates (q,p,r), columns are virtual (pitch,roll,yaw) inputs.
/// </summary>
internal sealed class MimoIndiController
{
    private const int N = 3;
    private readonly LowPass2[] _rateFilter = [new(), new(), new()];
    private readonly LowPass2[] _inputFilter = [new(), new(), new()];
    private readonly SampleHistory[] _history = [new(16), new(16), new(16)];
    private readonly float[] _actuator = new float[N];
    private readonly float[] _lastRate = new float[N];
    private readonly float[] _lastInput = new float[N];
    private readonly float[,] _b = new float[N, N];
    private readonly float[,] _p = new float[N, N];
    private readonly float[] _prior = new float[N];
    private float _excitation;
    private float _residualLp;
    private int _faultTicks;
    private bool _initialized;

    public float Condition { get; private set; }
    public float Residual { get; private set; }
    public float Confidence { get; private set; }
    public int RawRank { get; private set; } = N;
    public int Rank { get; private set; } = N;
    public bool FaultSuspected { get; private set; }
    public bool Degraded => Rank < N || FaultSuspected;
    public bool CrossAxisAllocationActive { get; private set; }

    public float GetEffectiveness(int output, int input) => _b[output, input];

    public void Clear() => _initialized = false;

    public void Reset(float pitch, float roll, float yaw, float q, float pRate, float r,
        float pitchGain, float rollGain, float yawGain)
    {
        float[] u = [pitch, roll, yaw];
        float[] y = [q, pRate, r];
        float[] g = [pitchGain, rollGain, yawGain];
        for (int i = 0; i < N; i++)
        {
            _history[i].Fill(u[i]);
            _actuator[i] = u[i];
            _inputFilter[i].Reset(u[i]);
            _rateFilter[i].Reset(y[i]);
            _lastInput[i] = u[i];
            _lastRate[i] = y[i];
            _prior[i] = Mathf.Max(Mathf.Abs(g[i]), 1e-3f);
            for (int j = 0; j < N; j++)
            {
                _b[i, j] = i == j ? g[i] : 0f;
                _p[i, j] = i == j ? 20f : 0f;
            }
        }

        _excitation = _residualLp = 0f;
        _faultTicks = 0;
        Confidence = 0f;
        FaultSuspected = false;
        UpdateRank();
        Residual = 0f;
        _initialized = true;
    }

    public void Track(float pitch, float roll, float yaw, float q, float pRate, float r,
        float pitchGain, float rollGain, float yawGain, float cutoff,
        float pitchTau, float rollTau, float yawTau, int delayTicks, float dt)
    {
        if (!_initialized)
        {
            Reset(pitch, roll, yaw, q, pRate, r, pitchGain, rollGain, yawGain);
            return;
        }

        float[] u = [pitch, roll, yaw];
        float[] y = [q, pRate, r];
        float[] tau = [pitchTau, rollTau, yawTau];
        Configure(cutoff, dt);
        for (int i = 0; i < N; i++)
        {
            AdvanceActuator(i, delayTicks, tau[i], dt);
            _lastInput[i] = _inputFilter[i].Step(_actuator[i]);
            _lastRate[i] = _rateFilter[i].Step(y[i]);
            _history[i].Push(u[i]);
        }
    }

    public void Step(float q, float pRate, float r, float qRef, float pRef, float rRef,
        float pitchApplied, float rollApplied, float yawApplied,
        bool pitchAvailable, bool rollAvailable, bool yawAvailable,
        bool qControlled, bool pControlled, bool rControlled,
        float pitchGain, float rollGain, float yawGain,
        float pitchAuthority, float rollAuthority, float yawAuthority,
        float rateLimit, int delayTicks, float cutoff, float pitchTau, float rollTau, float yawTau,
        bool crossAxisInNormalFlight, bool adapt, float margin, float dt,
        out float pitch, out float roll, out float yaw)
    {
        if (!_initialized)
        {
            Reset(pitchApplied, rollApplied, yawApplied, q, pRate, r, pitchGain, rollGain, yawGain);
        }

        Configure(cutoff, dt);
        float[] measured = [q, pRate, r];
        float[] reference = [qRef, pRef, rRef];
        bool[] controlled = [qControlled, pControlled, rControlled];
        bool[] available = [pitchAvailable, rollAvailable, yawAvailable];
        float[] applied = [pitchApplied, rollApplied, yawApplied];
        float[] authority = [pitchAuthority, rollAuthority, yawAuthority];
        float[] tau = [pitchTau, rollTau, yawTau];
        float[] u0 = new float[N];
        float[] yf = new float[N];

        for (int i = 0; i < N; i++)
        {
            AdvanceActuator(i, delayTicks, tau[i], dt);
            u0[i] = _inputFilter[i].Step(_actuator[i]);
            yf[i] = _rateFilter[i].Step(ControlMath.Finite(measured[i], _lastRate[i]));
        }

        if (adapt)
        {
            Adapt(u0, yf, dt);
        }
        else
        {
            UpdateRank();
        }
        UpdateRank(available);
        CrossAxisAllocationActive = crossAxisInNormalFlight || Rank < N || FaultSuspected;

        float[] error = new float[N];
        float[] weight = new float[N];
        for (int i = 0; i < N; i++)
        {
            error[i] = (controlled[i] ? ControlMath.Finite(reference[i], yf[i]) : 0f) - yf[i];
            weight[i] = controlled[i] ? 1f : 0.25f;
        }

        // When rank is lost, preserve pitch/roll stabilization before yaw/coordination.  This is a reduced-output
        // selection rather than pretending all three objectives remain independently attainable.
        if (Rank < N)
        {
            weight[2] *= 0.08f;
            if (Rank < 2)
            {
                weight[1] *= 0.25f;
            }
        }

        float[] lo = new float[N];
        float[] hi = new float[N];
        for (int i = 0; i < N; i++)
        {
            if (!available[i])
            {
                lo[i] = hi[i] = 0f;
                continue;
            }

            lo[i] = -authority[i] - u0[i];
            hi[i] = authority[i] - u0[i];
            if (rateLimit > 0f)
            {
                float last = _history[i].Get(0);
                float step = rateLimit * dt;
                lo[i] = Mathf.Max(lo[i], last - step - u0[i]);
                hi[i] = Mathf.Min(hi[i], last + step - u0[i]);
            }
        }

        float[] du = SolveBounded(error, weight, lo, hi, Mathf.Max(margin, 1f));
        float[] command = new float[N];
        for (int i = 0; i < N; i++)
        {
            command[i] = available[i] ? u0[i] + du[i] : applied[i];
            if (!ControlMath.IsFinite(command[i]))
            {
                command[i] = applied[i];
            }

            _history[i].Push(command[i]);
            _lastInput[i] = u0[i];
            _lastRate[i] = yf[i];
        }

        Residual = ResidualNorm(error, du, weight, margin);
        pitch = command[0];
        roll = command[1];
        yaw = command[2];
    }

    /// <summary>
    /// Exact bounded least-squares for three inputs.  Every active-set combination (lower/free/upper) is
    /// enumerated; free variables are solved from the damped normal equations.  This is deterministic and avoids
    /// the post-allocation clipping error of the previous coordinate-descent implementation.
    /// </summary>
    private float[] SolveBounded(float[] target, float[] weight, float[] lo, float[] hi, float margin)
    {
        float bestCost = float.PositiveInfinity;
        float[] best = new float[N];
        for (int code = 0; code < 27; code++)
        {
            int n = code;
            int[] state = new int[N]; // 0 lower, 1 free, 2 upper
            float[] x = new float[N];
            bool valid = true;
            for (int j = 0; j < N; j++)
            {
                state[j] = n % 3;
                n /= 3;
                if (lo[j] > hi[j])
                {
                    valid = false;
                }

                if (state[j] == 0)
                {
                    x[j] = lo[j];
                }
                else if (state[j] == 2)
                {
                    x[j] = hi[j];
                }
            }
            if (!valid)
            {
                continue;
            }

            int[] free = new int[N];
            int nf = 0;
            for (int j = 0; j < N; j++)
            {
                if (state[j] == 1)
                {
                    free[nf++] = j;
                }
            }

            if (nf > 0 && !SolveFree(target, weight, margin, state, free, nf, x))
            {
                continue;
            }

            for (int j = 0; j < N; j++)
            {
                if (x[j] < lo[j] - 1e-5f || x[j] > hi[j] + 1e-5f || !ControlMath.IsFinite(x[j]))
                {
                    valid = false;
                    break;
                }
            }
            if (!valid)
            {
                continue;
            }

            float cost = Objective(target, weight, x, margin);
            if (cost < bestCost)
            {
                bestCost = cost;
                for (int j = 0; j < N; j++)
                {
                    best[j] = Mathf.Clamp(x[j], lo[j], hi[j]);
                }
            }
        }
        return best;
    }

    private float AllocationEffectiveness(int output, int input) =>
        CrossAxisAllocationActive || output == input ? _b[output, input] : 0f;

    private bool SolveFree(float[] target, float[] weight, float margin, int[] state, int[] free, int nf, float[] x)
    {
        const float damping = 0.01f;
        float[,] a = new float[N, N];
        float[] rhs = new float[N];
        for (int row = 0; row < nf; row++)
        {
            int j = free[row];
            rhs[row] = 0f;
            for (int i = 0; i < N; i++)
            {
                float fixedPrediction = 0f;
                for (int k = 0; k < N; k++)
                {
                    if (state[k] != 1)
                    {
                        fixedPrediction += AllocationEffectiveness(i, k) * x[k] * margin;
                    }
                }

                float bij = AllocationEffectiveness(i, j) * margin;
                rhs[row] += weight[i] * bij * (target[i] - fixedPrediction);
                for (int col = 0; col < nf; col++)
                {
                    int k = free[col];
                    a[row, col] += weight[i] * bij * AllocationEffectiveness(i, k) * margin;
                }
            }
            a[row, row] += damping;
        }

        // Gaussian elimination with partial pivoting (maximum 3x3).
        for (int col = 0; col < nf; col++)
        {
            int pivot = col;
            for (int row = col + 1; row < nf; row++)
            {
                if (Mathf.Abs(a[row, col]) > Mathf.Abs(a[pivot, col]))
                {
                    pivot = row;
                }
            }

            if (Mathf.Abs(a[pivot, col]) < 1e-7f)
            {
                return false;
            }

            if (pivot != col)
            {
                for (int k = col; k < nf; k++)
                {
                    (a[col, k], a[pivot, k]) = (a[pivot, k], a[col, k]);
                }

                (rhs[col], rhs[pivot]) = (rhs[pivot], rhs[col]);
            }
            float d = a[col, col];
            for (int k = col; k < nf; k++)
            {
                a[col, k] /= d;
            }

            rhs[col] /= d;
            for (int row = 0; row < nf; row++)
            {
                if (row == col)
                {
                    continue;
                }

                float f = a[row, col];
                for (int k = col; k < nf; k++)
                {
                    a[row, k] -= f * a[col, k];
                }

                rhs[row] -= f * rhs[col];
            }
        }
        for (int i = 0; i < nf; i++)
        {
            x[free[i]] = rhs[i];
        }

        return true;
    }

    private float Objective(float[] target, float[] weight, float[] x, float margin)
    {
        float cost = 0.01f * ((x[0] * x[0]) + (x[1] * x[1]) + (x[2] * x[2]));
        for (int i = 0; i < N; i++)
        {
            float y = 0f;
            for (int j = 0; j < N; j++)
            {
                y += AllocationEffectiveness(i, j) * x[j] * margin;
            }

            float e = target[i] - y;
            cost += weight[i] * e * e;
        }
        return cost;
    }

    private void Adapt(float[] u, float[] y, float dt)
    {
        float[] phi = new float[N];
        float phi2 = 0f;
        for (int j = 0; j < N; j++)
        {
            phi[j] = u[j] - _lastInput[j];
            phi2 += phi[j] * phi[j];
        }

        _excitation = Mathf.Lerp(_excitation, phi2, 1f - Mathf.Exp(-dt / 2f));
        if (phi2 < 2e-6f)
        {
            Confidence = Mathf.Clamp01(_excitation / 2e-4f);
            UpdateRank();
            return;
        }

        float[] pPhi = new float[N];
        float den = 0.997f;
        for (int i = 0; i < N; i++)
        {
            for (int j = 0; j < N; j++)
            {
                pPhi[i] += _p[i, j] * phi[j];
            }

            den += phi[i] * pPhi[i];
        }
        float[] gain = new float[N];
        for (int i = 0; i < N; i++)
        {
            gain[i] = pPhi[i] / Mathf.Max(den, 1e-6f);
        }

        float residualSq = 0f;
        for (int output = 0; output < N; output++)
        {
            float dy = y[output] - _lastRate[output];
            float predicted = 0f;
            for (int j = 0; j < N; j++)
            {
                predicted += _b[output, j] * phi[j];
            }

            float innovation = Mathf.Clamp(dy - predicted, -0.75f, 0.75f);
            residualSq += innovation * innovation;
            for (int j = 0; j < N; j++)
            {
                float limit = output == j ? 4f * _prior[output] : 2f * Mathf.Max(_prior[output], _prior[j]);
                _b[output, j] = Mathf.Clamp(_b[output, j] + (gain[j] * innovation), -limit, limit);
            }
        }

        for (int i = 0; i < N; i++)
        {
            for (int j = 0; j < N; j++)
            {
                _p[i, j] = (_p[i, j] - (gain[i] * pPhi[j])) / 0.997f;
            }
        }

        float sampleResidual = Mathf.Sqrt(residualSq);
        _residualLp = Mathf.Lerp(_residualLp, sampleResidual, 1f - Mathf.Exp(-dt / 0.2f));
        bool inconsistent = _residualLp > 0.18f && phi2 > 2e-5f;
        _faultTicks = inconsistent ? _faultTicks + 1 : Mathf.Max(_faultTicks - 1, 0);
        FaultSuspected = _faultTicks > 8;
        if (FaultSuspected)
        {
            // Covariance inflation makes abrupt effectiveness changes converge faster.  It does not pick a failed
            // actuator by fiat; the measured multivariable response determines the new signed column.
            for (int i = 0; i < N; i++)
            {
                _p[i, i] = Mathf.Min(_p[i, i] + 0.15f, 60f);
            }
        }

        Confidence = Mathf.Clamp01(_excitation / 2e-4f) * Mathf.Clamp01(1f - (_residualLp / 0.5f));
        UpdateRank();
    }

    private void UpdateRank(bool[] available = null)
    {
        // Singular values are sqrt(eigenvalues(B*B^T)).  When availability is supplied, omitted actuator columns
        // are excluded so supervision sees the authority that can actually be allocated this tick.
        float[,] a = new float[N, N];
        for (int i = 0; i < N; i++)
        {
            for (int j = 0; j < N; j++)
            {
                for (int k = 0; k < N; k++)
                {
                    if (available?[k] != false)
                    {
                        a[i, j] += _b[i, k] * _b[j, k];
                    }
                }
            }
        }

        for (int sweep = 0; sweep < 8; sweep++)
        {
            Jacobi(a, 0, 1); Jacobi(a, 0, 2); Jacobi(a, 1, 2);
        }
        float s0 = Mathf.Sqrt(Mathf.Max(a[0, 0], 0f));
        float s1 = Mathf.Sqrt(Mathf.Max(a[1, 1], 0f));
        float s2 = Mathf.Sqrt(Mathf.Max(a[2, 2], 0f));
        float max = Mathf.Max(s0, Mathf.Max(s1, s2));
        float threshold = Mathf.Max(0.04f * max, 1e-3f);
        int rank = (s0 > threshold ? 1 : 0) + (s1 > threshold ? 1 : 0) + (s2 > threshold ? 1 : 0);
        if (available == null)
        {
            RawRank = rank;
        }
        Rank = rank;
        float min = Mathf.Min(s0, Mathf.Min(s1, s2));
        Condition = max > 1e-6f ? min / max : 0f;
    }

    private static void Jacobi(float[,] a, int p, int q)
    {
        if (Mathf.Abs(a[p, q]) < 1e-8f)
        {
            return;
        }

        float angle = 0.5f * Mathf.Atan2(2f * a[p, q], a[q, q] - a[p, p]);
        float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
        float app = (c * c * a[p, p]) - (2f * s * c * a[p, q]) + (s * s * a[q, q]);
        float aqq = (s * s * a[p, p]) + (2f * s * c * a[p, q]) + (c * c * a[q, q]);
        for (int k = 0; k < N; k++)
        {
            if (k == p || k == q)
            {
                continue;
            }

            float akp = (c * a[k, p]) - (s * a[k, q]);
            float akq = (s * a[k, p]) + (c * a[k, q]);
            a[k, p] = a[p, k] = akp;
            a[k, q] = a[q, k] = akq;
        }
        a[p, p] = app; a[q, q] = aqq; a[p, q] = a[q, p] = 0f;
    }

    private float ResidualNorm(float[] error, float[] du, float[] weight, float margin)
    {
        float sum = 0f;
        for (int i = 0; i < N; i++)
        {
            float achieved = 0f;
            for (int j = 0; j < N; j++)
            {
                achieved += AllocationEffectiveness(i, j) * du[j] * margin;
            }

            float e = weight[i] * (error[i] - achieved);
            sum += e * e;
        }
        return Mathf.Sqrt(sum);
    }

    private void Configure(float cutoff, float dt)
    {
        for (int i = 0; i < N; i++)
        {
            _rateFilter[i].Configure(cutoff, dt);
            _inputFilter[i].Configure(cutoff, dt);
        }
    }

    private void AdvanceActuator(int axis, int delayTicks, float tau, float dt)
    {
        float target = _history[axis].Get(Mathf.Max(delayTicks - 1, 0));
        _actuator[axis] = tau > 1e-4f
            ? _actuator[axis] + ((target - _actuator[axis]) * (1f - Mathf.Exp(-dt / tau)))
            : target;
        _actuator[axis] = ControlMath.Finite(_actuator[axis], target);
    }
}
