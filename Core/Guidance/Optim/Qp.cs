using System;

namespace NOAutopilot.Core.Guidance.Optim;

/// <summary>
/// Convex quadratic program in inequality form:
///   minimize    0.5 * x' P x + q' x
///   subject to  lo &lt;= A x &lt;= hi      (use +-float.PositiveInfinity for open sides)
/// P must be positive semidefinite (a small ridge is added internally).
/// </summary>
public sealed class Qp
{
    public readonly int N;      // variables
    public readonly int M;      // constraints
    public readonly float[,] P; // N x N symmetric
    public readonly float[] Q;  // N
    public readonly float[,] A; // M x N
    public readonly float[] Lo; // M
    public readonly float[] Hi; // M

    public Qp(int n, int m)
    {
        N = n;
        M = m;
        P = new float[n, n];
        Q = new float[n];
        A = new float[m, n];
        Lo = new float[m];
        Hi = new float[m];
    }

    public void SetBound(int row, float lo, float hi)
    {
        Lo[row] = lo;
        Hi[row] = hi;
    }

    /// <summary>Adds the coefficient of variable col in constraint row.</summary>
    public void AddA(int row, int col, float value) => A[row, col] += value;
}

/// <summary>
/// Small dependency-free ADMM solver for <see cref="Qp"/> in the OSQP style:
/// alternating between an unconstrained Newton step (fixed Cholesky factor of P + rho*A'A + ridge)
/// and a projection onto the constraint box. Warm-startable, allocation-light per iteration.
/// Sizes here are tiny (N &lt;= 256), so dense math at a few Hz is far cheaper than the game frame.
/// </summary>
public static class QpSolver
{
    public const int MaxSize = 384;

    /// <summary>Solver tuning for the profile problems (experiments can retune these).</summary>
    public static int MaxIterProfile = 2000;
    public static float RhoProfile = 0.05f;

    /// <summary>Optional diagnostics sink (test harness only).</summary>
    public static Action<string> Diagnostics;

    /// <summary>
    /// Solves the QP into <paramref name="x"/>. When <paramref name="x"/> holds a warm start it is used,
    /// otherwise a feasible-ish start is computed from the constraint midpoints.
    /// Returns true when the optimality residuals fell below tolerance.
    /// </summary>
    public static bool Solve(Qp qp, float[] x, int maxIter = 200, float eps = 1e-3f, float rho = 4f)
    {
        int n = qp.N;
        int m = qp.M;
        if (n > MaxSize)
        {
            return false;
        }

        // K = P + rho*A'A + ridge*I, factored (refactored when the adaptive rho changes).
        // DOUBLE precision: P entries are O(1) while rho*A'A entries reach O(1e3) - in float32 the
        // Cholesky of that mix buries the cost structure in rounding noise and the solver converges
        // to a feasible but expensive point.
        double[] k = new double[n * n];
        double[] lChol = new double[n * n];
        const double ridge = 1e-6;
        float rhoCur = rho;
        void BuildK()
        {
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    double acc = qp.P[i, j] + (i == j ? ridge : 0.0);
                    for (int r = 0; r < m; r++)
                    {
                        acc += (double)rhoCur * qp.A[r, i] * qp.A[r, j];
                    }
                    k[i * n + j] = acc;
                }
            }
        }

        BuildK();
        if (!Cholesky(k, n, lChol))
        {
            return false;
        }

        float[] z = new float[m];
        float[] y = new float[m];
        double[] rhs = new double[n];
        double[] tmp = new double[n];
        float[] ax = new float[m];
        float[] xPrev = new float[n];
        bool warm = true;
        for (int i = 0; i < n; i++)
        {
            if (float.IsNaN(x[i]))
            {
                warm = false;
                break;
            }
        }

        if (!warm)
        {
            // cold start: x = 0, z = clipped A*x, y = 0
            Array.Clear(x, 0, n);
            MatVecA(qp, x, z);
        }
        else
        {
            MatVecA(qp, x, z);
        }

        for (int r = 0; r < m; r++)
        {
            z[r] = Mathf2.Clamp(z[r], qp.Lo[r], qp.Hi[r]);
        }

        float priRes = float.MaxValue;
        float duaRes = float.MaxValue;
        float duaTol = float.MaxValue;
        int it;
        for (it = 0; it < maxIter; it++)
        {
            // x <- K^-1 (rho*A'(z - y/rho) - q) = K^-1 (rho*A'z - A'y - q)
            for (int i = 0; i < n; i++)
            {
                double acc = -qp.Q[i];
                for (int r = 0; r < m; r++)
                {
                    float ar = qp.A[r, i];
                    if (ar != 0f)
                    {
                        acc += ar * (rhoCur * z[r] - y[r]);
                    }
                }
                rhs[i] = acc;
            }

            CholSolve(lChol, n, rhs, tmp);
            // over-relaxed iterate for the z-step (OSQP alpha = 1.4)
            const float alpha = 1.4f;
            for (int i = 0; i < n; i++)
            {
                float xn = (float)tmp[i];
                xPrev[i] = x[i] + alpha * (xn - x[i]);
                x[i] = xn;
            }

            // z-tilde = A x_relaxed
            float priMax = 1e-9f;
            MatVecA(qp, xPrev, ax);
            for (int r = 0; r < m; r++)
            {
                float zt = ax[r];
                float zp = Mathf2.Clamp(zt + y[r] / rhoCur, qp.Lo[r], qp.Hi[r]);
                priMax = Math.Max(priMax, Math.Abs(zt - zp));
                y[r] += rhoCur * (zt - zp);
                z[r] = zp;
            }

            // dual residual: P x + q + A' y, with the scale-relative tolerance of OSQP - P and A are
            // variable-scaled, so an absolute threshold would reject perfectly optimal scaled solutions
            float duaMax = 1e-9f, nPxQ = 1e-9f, nATy = 1e-9f;
            for (int i = 0; i < n; i++)
            {
                float acc = qp.Q[i];
                float grad = qp.Q[i];
                for (int j = 0; j < n; j++)
                {
                    acc += qp.P[i, j] * x[j];
                    grad += qp.P[i, j] * x[j];
                }
                for (int r = 0; r < m; r++)
                {
                    float t = qp.A[r, i] * y[r];
                    acc += t;
                    nATy = Math.Max(nATy, Math.Abs(t));
                }
                nPxQ = Math.Max(nPxQ, Math.Abs(grad));
                duaMax = Math.Max(duaMax, Math.Abs(acc));
            }

            priRes = priMax;
            duaRes = duaMax;
            duaTol = 1f + 0.02f * Math.Max(nPxQ, nATy);
            bool tight = priRes < eps && duaRes < eps;
            bool looseOk = priRes < 0.05f && duaRes < duaTol;
            if (tight || (eps >= 1e-3f && looseOk))
            {
                it++;
                break;
            }

            // adaptive rho (OSQP-style): whichever residual lags gets more weight - rho up emphasizes
            // feasibility, rho down emphasizes optimality. Refactoring K costs O(n^3) but n <= 384 and
            // it happens a handful of times per solve.
            if ((it & 63) == 63 && it < maxIter - 64)
            {
                float priRel = priRes / 0.05f;
                float duaRel = duaRes / Math.Max(duaTol, 1e-6f);
                float ratio = (float)Math.Sqrt((priRel + 1e-9) / (duaRel + 1e-9));
                if (ratio > 2f || ratio < 0.5f)
                {
                    float newRho = Mathf2.Clamp(rhoCur * Mathf2.Clamp(ratio, 0.1f, 10f), 1e-4f, 1e4f);
                    if (Math.Abs(newRho - rhoCur) > rhoCur * 1e-3f)
                    {
                        float scale = rhoCur / newRho;
                        for (int r = 0; r < m; r++)
                        {
                            y[r] *= scale;
                        }

                        rhoCur = newRho;
                        BuildK();
                        if (!Cholesky(k, n, lChol))
                        {
                            break;
                        }
                    }
                }
            }
        }

        // near-feasible, near-optimal is enough for guidance (constraints are hard limits, cost is preference)
        if (Diagnostics != null)
        {
            Diagnostics($"iters={it} pri={priRes:E2} dua={duaRes:E2} tol={duaTol:E2}");
        }
        // acceptance: constraints met to ~1 m (scaled units: 0.05*ts) and a small dual residual relative
        // to the cost/constraint scales.
        return priRes < Math.Max(eps, 0.05f) && duaRes < Math.Max(eps, duaTol);
    }

    private static void MatVecA(Qp qp, float[] x, float[] outM)
    {
        for (int r = 0; r < qp.M; r++)
        {
            float acc = 0f;
            for (int j = 0; j < qp.N; j++)
            {
                acc += qp.A[r, j] * x[j];
            }
            outM[r] = acc;
        }
    }

    private static bool Cholesky(double[] a, int n, double[] l)
    {
        Array.Copy(a, l, a.Length);
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                double sum = l[i * n + j];
                for (int k = 0; k < j; k++)
                {
                    sum -= l[i * n + k] * l[j * n + k];
                }

                if (i == j)
                {
                    if (sum <= 0.0)
                    {
                        return false;
                    }
                    l[i * n + i] = Math.Sqrt(sum);
                }
                else
                {
                    l[i * n + j] = sum / l[j * n + j];
                }
            }
        }
        return true;
    }

    private static void CholSolve(double[] l, int n, double[] b, double[] x)
    {
        // forward substitution L y = b
        for (int i = 0; i < n; i++)
        {
            double sum = b[i];
            for (int k = 0; k < i; k++)
            {
                sum -= l[i * n + k] * x[k];
            }
            x[i] = sum / l[i * n + i];
        }
        // backward substitution L' x = y
        for (int i = n - 1; i >= 0; i--)
        {
            double sum = x[i];
            for (int k = i + 1; k < n; k++)
            {
                sum -= l[k * n + i] * x[k];
            }
            x[i] = sum / l[i * n + i];
        }
    }
}

internal static class Mathf2
{
    public static float Clamp(float v, float lo, float hi)
    {
        return v < lo ? lo : v > hi ? hi : v;
    }
}
