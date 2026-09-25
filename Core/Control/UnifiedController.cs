using UnityEngine;

namespace NOAutopilot.Core.Control;

public struct AppliedInputs
{
    public float Pitch, Roll, Yaw, Throttle;
    public bool PitchOverride, RollOverride, YawOverride, ThrottleOverride;
}

public struct ControllerTelemetry
{
    public float VsCmd, AVertCmd, ALatCmd, NCmd, MuCmd, PCmd, QCmd, RCmd, VDotCmd, EnergyRateCmd;
    public float PEta, QEta, REta, EEta, NAlpha;
    public float PGain, QGain, RGain;
    public bool NSaturated, AlphaLimited;
}

public sealed class UnifiedController(ControllerSettings settings, AircraftModel model)
{
    public readonly ControllerSettings Settings = settings;
    public AircraftModel Model = model;

    private readonly IndiAxis _pAxis = new();
    private readonly IndiAxis _qAxis = new();
    private readonly IndiAxis _rAxis = new();
    private readonly IndiAxis _eAxis = new();
    private readonly IndiAxis _vzAxis = new();

    private readonly ResponseIdentifier _pId = new(0.25f, 3f);
    private readonly ResponseIdentifier _qId = new(0.35f, 0.5f);
    private readonly ResponseIdentifier _rId = new(0.4f, 0.5f);

    private readonly LowPass1 _vsCmdDotFilter = new();
    private readonly LowPass1 _hDotPathFilter = new();

    private float _vsCmdPrev;
    private bool _vsCmdPrevValid;
    private bool _vsIntFrozen;
    private float _hoverPsi;
    private float _hoverThrottle = float.NaN;

    public float VsInt { get; private set; }

    public bool HoverActive { get; private set; }

    public bool StallAssistActive { get; private set; }
    private float _nCmdPrev;
    private bool _nCmdPrevValid;
    private float _pCmdPrev, _qCmdPrev, _rCmdPrev;
    private bool _pWasActive, _qWasActive, _rWasActive, _tWasActive;
    private float _nAlpha = -1f;
    private bool _nAlphaInit;

    private float _heloSpeedHold = float.NaN;

    private float _thrSatHighTime, _thrSatLowTime, _energyRateMeasured;
    private int _energyLimited;
    private float _airbrakeTimer;

    public bool AirbrakeActive { get; private set; }

    public ControllerTelemetry Telemetry;

    public void Reset()
    {
        _pId.Reset(Settings.RollLag, 3f);
        _qId.Reset(Settings.PitchLag, 0.5f);
        _rId.Reset(Settings.YawLag, 0.5f);
        _pAxis.ResetEstimator();
        _qAxis.ResetEstimator();
        _rAxis.ResetEstimator();
        _eAxis.ResetEstimator();
        _vzAxis.ResetEstimator();
        _pWasActive = _qWasActive = _rWasActive = _tWasActive = false;
        _vsCmdPrevValid = false;
        _nAlpha = -1f;
        _nAlphaInit = false;
        HoverActive = false;
        StallAssistActive = false;
        _hoverThrottle = float.NaN;
        _heloSpeedHold = float.NaN;
        Telemetry = default;
    }

    public float LastPitch { get; private set; }
    public float LastRoll { get; private set; }
    public float LastYaw { get; private set; }
    public float LastThrottle { get; private set; }

    public ControlOutput Step(FlightState s, AutopilotCommand cmd, AppliedInputs applied)
    {
        return Model.IsHelicopter ? StepHelicopter(s, cmd, applied) : StepFixedWing(s, cmd, applied);
    }

    private ControlOutput StepFixedWing(FlightState s, AutopilotCommand cmd, AppliedInputs applied)
    {
        ControllerSettings c = Settings;
        float dt = Mathf.Max(s.Dt, 1e-3f);
        float v = Mathf.Max(s.V, 5f);
        const float g = ControlMath.G;
        ControlOutput o = default;
        ControllerTelemetry t = default;

        UpdateLiftSlope(s);
        t.NAlpha = _nAlpha;

        float nMaxCfg = ControlMath.IsFinite(cmd.MaxG) ? cmd.MaxG : c.ManeuverMaxG;
        float nMinCfg = ControlMath.IsFinite(cmd.MinG) ? cmd.MinG : c.ManeuverMinG;
        float nMax = Model.AvailableLoadFactor(s.V, s.Rho, nMaxCfg);
        float nMin = Mathf.Min(nMinCfg, nMax - 0.1f);

        bool hoverCapable = c.HoverEnabled && !Model.IsHelicopter &&
            (Model.MaxThrust > (1.05f * Model.Mass * g)) && (Model.LandingSpeed < 45f);
        if (hoverCapable && !s.OnGround)
        {
            bool want = HoverActive ? s.V < (c.HoverSpeed + 8f) : s.V < c.HoverSpeed;
            if (want != HoverActive)
            {
                HoverActive = want;
                _hoverPsi = s.Psi;
                _hoverThrottle = float.NaN;
            }
        }
        else
        {
            HoverActive = false;
        }

        bool stallCapable = c.StallAssist && !Model.IsHelicopter && !s.OnGround && !HoverActive;
        if (stallCapable)
        {
            float aEng = Mathf.Clamp((Model.FbwAlphaLimiter - 2f) * Mathf.Deg2Rad, 0.24f, 0.55f);
            float aExit = aEng - 0.09f;
            bool want = StallAssistActive ? (s.Alpha > aExit) : (s.Alpha > aEng);
            if (want != StallAssistActive)
            {
                StallAssistActive = want;
                if (want)
                {
                    _hoverPsi = s.Psi;
                    _hoverThrottle = float.NaN;
                }
            }
        }
        else
        {
            StallAssistActive = false;
        }

        bool hover = HoverActive;
        bool stall = StallAssistActive;
        bool thrustVert = hover || stall;

        bool pitchActive = cmd.PitchAxisActive;
        bool haveAVert = false;
        float aVert = 0f;
        float nDes = float.NaN;
        float qCmd = float.NaN;
        float vsDes = s.VerticalSpeed;
        bool haveVsDes = false;

        switch (cmd.Vertical)
        {
            case VerticalMode.Altitude:
                {
                    float climbLimit = cmd.MaxClimbRate;
                    float descentLimit = cmd.MaxDescentRate;
                    float err = cmd.Altitude - s.Altitude;
                    float aShape = thrustVert
                        ? 0.4f * Mathf.Max((Model.MaxThrust / Mathf.Max(Model.Mass, 1f)) - g, 1f)
                        : VerticalShapeAccel(nMax, nMin, cmd);
                    vsDes = ControlMath.ShapedRate(err, c.AltitudeGain, aShape, 0f);
                    vsDes += cmd.VerticalSpeedFeedForward;
                    vsDes = Mathf.Clamp(vsDes, -descentLimit, climbLimit);
                    haveVsDes = true;
                    break;
                }
            case VerticalMode.VerticalSpeed:
                vsDes = cmd.VerticalSpeed;
                haveVsDes = true;
                break;
            case VerticalMode.FlightPathAngle:
                vsDes = s.Speed * Mathf.Sin(cmd.FlightPathAngle);
                haveVsDes = true;
                break;
            case VerticalMode.LoadFactor:
                nDes = cmd.LoadFactor;
                break;
            case VerticalMode.PitchAttitude:
                {
                    float thetaErr = ControlMath.WrapPi(cmd.PitchAttitude - s.Theta);
                    float cphi = Mathf.Cos(s.Phi);
                    float thetaDot = c.PitchAttitudeGain * thetaErr;
                    qCmd = (thetaDot * cphi) + ((g / v) * Mathf.Sin(s.Phi) * Mathf.Sin(s.Phi) * Mathf.Cos(s.Theta) / Mathf.Max(Mathf.Abs(cphi), 0.2f));
                    break;
                }
            case VerticalMode.Acceleration:
                aVert = cmd.VerticalAccel;
                haveAVert = true;
                break;
        }

        if (thrustVert && !haveAVert && ControlMath.IsFinite(nDes))
        {
            aVert = (nDes - 1f) * g;
            haveAVert = true;
            nDes = float.NaN;
        }

        if (haveVsDes)
        {
            if (!thrustVert)
            {
                float maxVs = s.Speed * Mathf.Sin(c.MaxFlightPathAngle);
                vsDes = Mathf.Clamp(vsDes, -maxVs, maxVs);
                vsDes = SpeedProtectVs(s, vsDes);
                vsDes = SpeedPriority(s, cmd, vsDes);
            }

            float vsDesDot = 0f;
            float aUp, aDown;
            if (thrustVert)
            {
                aUp = 0.6f * Mathf.Max((Model.MaxThrust / Mathf.Max(Model.Mass, 1f)) - g, 1f);
                aDown = 0.6f * g;
            }
            else
            {
                VerticalAccelLimits(nMax, nMin, cmd, out aUp, out aDown);
            }
            if (_vsCmdPrevValid)
            {
                vsDes = Mathf.Clamp(vsDes, _vsCmdPrev - (aDown * dt), _vsCmdPrev + (aUp * dt));
                vsDesDot = _vsCmdDotFilter.Step((vsDes - _vsCmdPrev) / dt, 0.3f, dt);
            }
            else
            {
                vsDes = Mathf.Clamp(vsDes, s.VerticalSpeed - (aDown * 0.5f), s.VerticalSpeed + (aUp * 0.5f));
                _vsCmdDotFilter.Reset(0f);
            }

            _vsCmdPrev = vsDes;
            _vsCmdPrevValid = true;

            float kvs = c.VerticalSpeedGain;
            float vsErr = vsDes - s.VerticalSpeed;

            if (Mathf.Abs(vsDesDot) < 0.5f)
            {
                if (!_vsIntFrozen || (VsInt * vsErr) < 0f)
                {
                    VsInt = Mathf.Clamp(VsInt + (0.25f * kvs * vsErr * dt), -0.3f * g, 0.3f * g);
                }
            }
            else
            {
                VsInt *= Mathf.Exp(-dt / 2f);
            }

            aVert = (kvs * vsErr) + vsDesDot + VsInt;
            haveAVert = true;
        }
        else
        {
            _vsCmdPrevValid = false;
            VsInt = 0f;
        }

        t.VsCmd = vsDes;
        t.AVertCmd = aVert;

        bool rollActive = cmd.RollAxisActive;
        float aLat = 0f;
        bool haveALat = false;
        float bankLimit = Mathf.Clamp(cmd.BankLimit, 0.02f, Mathf.PI);
        if (ControlMath.IsFinite(cmd.TouchdownBankLimit))
        {
            bankLimit = Mathf.Min(bankLimit, Mathf.Max(cmd.TouchdownBankLimit, 0f));
        }

        float muDes = float.NaN;
        float phiDes = float.NaN;

        switch (cmd.Lateral)
        {
            case LateralMode.Course:
                {
                    float vg = Mathf.Max(s.GroundSpeed, 5f);
                    float chiErr = ControlMath.WrapPi(cmd.Course - s.Chi);
                    float maxTurnRate = g * Mathf.Tan(Mathf.Min(bankLimit, 1.4f)) / vg;
                    float turnAccel = g * c.CourseRollRate / vg;
                    float chiDot = ControlMath.ShapedRate(chiErr, c.CourseGain, turnAccel, maxTurnRate);
                    chiDot += cmd.CourseRateFeedForward;
                    aLat = vg * chiDot;
                    haveALat = true;
                    break;
                }
            case LateralMode.Bank:
                phiDes = Mathf.Clamp(cmd.Bank, -bankLimit, bankLimit);
                break;
            case LateralMode.Acceleration:
                aLat = cmd.LateralAccel;
                haveALat = true;
                break;
        }

        t.ALatCmd = aLat;

        if (thrustVert)
        {
            phiDes = cmd.Lateral == LateralMode.Bank
                ? cmd.Bank
                : haveALat && cmd.Lateral != LateralMode.Course
                    ? Mathf.Atan2(aLat, Mathf.Max(aVert + g, 2f))
                    : 0f;

            muDes = float.NaN;
        }

        if (!thrustVert && (haveAVert || haveALat))
        {
            if (!haveAVert)
            {
                aVert = 0f;
            }

            Allocate(s, aVert, aLat, haveALat, phiDes, pitchActive, bankLimit, nMax, ref nDes, ref muDes);
        }

        float pCmd = float.NaN;
        if (ControlMath.IsFinite(cmd.RollRateOverride))
        {
            pCmd = cmd.RollRateOverride;
        }
        else if (rollActive)
        {
            float maxRate = cmd.AggressiveRoll ? 2f * c.MaxRollRate : c.MaxRollRate;
            float maxAccel = cmd.AggressiveRoll ? c.MaxRollAccel * 3f : c.MaxRollAccel;
            float err = ControlMath.IsFinite(phiDes)
                ? ControlMath.WrapPi(phiDes - s.Phi)
                : ControlMath.IsFinite(muDes)
                    ? ControlMath.WrapPi(muDes - s.Mu)
                    : ControlMath.WrapPi(-s.Phi);
            float kb = c.BankGain;
            float phiDot = ControlMath.ShapedRate(err, kb, maxAccel, maxRate);
            // Euler bank rate -> body roll rate: phi' = p + (q sin(phi) + r cos(phi)) tan(theta)
            float tanTheta = Mathf.Clamp(Mathf.Tan(s.Theta), -2f, 2f);
            pCmd = phiDot - (((s.Q * Mathf.Sin(s.Phi)) + (s.R * Mathf.Cos(s.Phi))) * tanTheta);
            pCmd = Mathf.Clamp(pCmd, -maxRate, maxRate);
            t.MuCmd = ControlMath.IsFinite(phiDes) ? phiDes : muDes;
        }

        if (ControlMath.IsFinite(cmd.PitchRateOverride))
        {
            qCmd = cmd.PitchRateOverride;
        }
        else if (thrustVert)
        {
            float thetaDes = 0f;
            if (stall)
            {
                float aHold = (Model.FbwAlphaLimiter - 2f) * Mathf.Deg2Rad;
                thetaDes = s.Gamma + aHold;
                qCmd = (1.5f * (thetaDes - s.Theta)) - (0.8f * s.Q);
            }
            else
            {
                if (cmd.Speed == SpeedMode.Airspeed && ControlMath.IsFinite(cmd.Airspeed))
                {
                    float uFwd = s.V * Mathf.Cos(s.Alpha) * Mathf.Cos(s.Beta);
                    thetaDes = 0.05f * (uFwd - cmd.Airspeed);
                }
                else if (!pitchActive)
                {
                    thetaDes = s.Theta;
                }

                qCmd = (2f * (thetaDes - s.Theta)) - (1f * s.Q);
            }

            t.NCmd = (aVert / g) + 1f;
        }
        else if (pitchActive && !ControlMath.IsFinite(qCmd))
        {
            if (!ControlMath.IsFinite(nDes))
            {
                nDes = Mathf.Cos(s.Gamma) * Mathf.Cos(s.Mu);
            }

            // unload - roll - pull: do not pull while the lift vector points the wrong way
            float bankTarget = ControlMath.IsFinite(phiDes) ? phiDes : muDes;
            if (ControlMath.IsFinite(bankTarget) && nDes > 0f)
            {
                float current = ControlMath.IsFinite(phiDes) ? s.Phi : s.Mu;
                float bankErr = Mathf.Abs(ControlMath.WrapPi(bankTarget - current));
                float fade = Mathf.Clamp01((1.57f - bankErr) / 0.52f);
                float nUnload = Mathf.Max(0.3f, nMin);
                nDes = Mathf.Lerp(Mathf.Min(nUnload, nDes), nDes, fade);
            }

            nDes = Mathf.Clamp(nDes, nMin, nMax);
            t.NSaturated = nDes >= nMax - 1e-3f || nDes <= nMin + 1e-3f;
            _vsIntFrozen = t.NSaturated;

            float nRate = c.LoadFactorRateLimit * (cmd.AggressiveRoll ? 2.5f : 1f);
            nDes = _nCmdPrevValid
                ? Mathf.Clamp(nDes, _nCmdPrev - (nRate * dt), _nCmdPrev + (nRate * dt))
                : Mathf.Clamp(nDes, s.NLift - (nRate * 0.2f), s.NLift + (nRate * 0.2f));

            _nCmdPrev = nDes;
            _nCmdPrevValid = true;
            t.NCmd = nDes;

            float kn = c.LoadFactorGain;
            float nuN = kn * (nDes - s.NLift);
            float qPathDes = (nDes - (Mathf.Cos(s.Gamma) * Mathf.Cos(s.Mu))) * ControlMath.G / v;
            float qPathFf = Mathf.Lerp(s.QPath, qPathDes, 0.5f);
            qCmd = qPathFf + (nuN / Mathf.Max(_nAlpha, 1f));
        }

        if (!pitchActive || ControlMath.IsFinite(cmd.PitchRateOverride) || cmd.Vertical == VerticalMode.PitchAttitude)
        {
            _nCmdPrevValid = false;
        }

        if (ControlMath.IsFinite(qCmd) && !thrustVert)
        {
            float alphaLim = c.AlphaLimit;
            float qMaxAlpha = s.QPath + (3f * (alphaLim - s.Alpha));
            float qMinAlpha = s.QPath + (3f * ((-0.5f * alphaLim) - s.Alpha));
            if (qCmd > qMaxAlpha)
            {
                qCmd = qMaxAlpha;
                t.AlphaLimited = true;
            }

            qCmd = Mathf.Max(qCmd, qMinAlpha);

        }

        float rCmd = float.NaN;
        if (ControlMath.IsFinite(cmd.YawRateOverride))
        {
            rCmd = cmd.YawRateOverride;
        }
        else if (thrustVert)
        {
            float psiDes = cmd.Lateral == LateralMode.Course && ControlMath.IsFinite(cmd.Course)
                ? cmd.Course
                : _hoverPsi;
            rCmd = (1.2f * ControlMath.WrapPi(psiDes - s.Psi)) - (0.5f * s.R);
        }
        else if (rollActive && c.YawCoordination && s.V > 30f && !s.OnGround)
        {
            float betaDes = ControlMath.IsFinite(cmd.SideslipOverride) ? cmd.SideslipOverride : 0f;
            if (!ControlMath.IsFinite(cmd.SideslipOverride) && haveALat && c.SkidAssist > 0f && !cmd.NoSkidAssist)
            {
                float vRef = Mathf.Max(Model.CornerSpeed, 40f);
                float lowSpeed = Mathf.Clamp01(((1.15f * vRef) - s.V) / (0.6f * vRef));
                betaDes -= c.SkidAssist * (aLat / g) * lowSpeed;
            }

            float rCoord = g / v * Mathf.Sin(s.Phi) * Mathf.Cos(s.Theta);
            rCmd = rCoord + (c.SideslipGain * (s.Beta - betaDes));
            rCmd = Mathf.Clamp(rCmd, -1f, 1f);
        }

        float cutoff = c.FilterCutoff;
        int delay = c.InputDelayTicks;
        bool adapt = c.OnlineEstimation;

        o.PitchActive = ControlMath.IsFinite(qCmd) && !applied.PitchOverride;
        o.RollActive = ControlMath.IsFinite(pCmd) && !applied.RollOverride;
        o.YawActive = ControlMath.IsFinite(rCmd) && !applied.YawOverride;

        float gP = PriorRoll(s), gQ = PriorPitch(s), gR = PriorYaw(s);

        ApplyDirect(cmd, applied, ref o, out AppliedInputs track);
        bool learn = !s.OnGround && s.V > 40f && s.RadarAltitude > 5f;
        float pitchMin = Model.IsHelicopter ? 0.15f : 0.5f;
        o.Pitch = RateChannel(_qAxis, _qId, ref _qWasActive, ref _qCmdPrev, o.PitchActive, s.Q, qCmd, track.Pitch,
            c.PitchRateBandwidth, gQ, c.PitchLag, pitchMin, c.PitchAuthority, cutoff, delay, adapt, learn, dt,
            out t.QGain);
        o.Roll = RateChannel(_pAxis, _pId, ref _pWasActive, ref _pCmdPrev, o.RollActive, s.P, pCmd, track.Roll,
            c.RollRateBandwidth, gP, c.RollLag, 0.1f, c.RollAuthority, cutoff, delay, adapt, learn, dt, out t.PGain);
        o.Yaw = RateChannel(_rAxis, _rId, ref _rWasActive, ref _rCmdPrev, o.YawActive, s.R, rCmd, track.Yaw,
            c.YawRateBandwidth, gR, c.YawLag, 0.1f, c.YawAuthority, cutoff, delay, adapt, learn, dt, out t.RGain);
        FinishDirect(cmd, applied, ref o);

        float hDotPath = haveVsDes && pitchActive ? vsDes : s.VerticalSpeed;
        if (thrustVert)
        {
            AutopilotCommand trk = cmd;
            trk.Speed = SpeedMode.None;
            EnergyChannel(s, trk, applied, hDotPath, ref t);   // keep the engine-spool filter synchronized
            float cosTilt = Mathf.Max(Mathf.Cos(s.Theta) * Mathf.Cos(s.Phi), 0.4f);
            float thrustDes = Model.Mass * (aVert + g) / cosTilt;
            float thCap = hover && cmd.AllowExtremeThrottle ? 1.15f : c.ThrottleMax;
            float thDes = Mathf.Clamp(thrustDes / Mathf.Max(Model.MaxThrust, 1f), 0f, thCap);
            _hoverThrottle = float.IsNaN(_hoverThrottle)
                ? applied.Throttle
                : Mathf.MoveTowards(_hoverThrottle, thDes, (stall ? 1.5f : 3f) * dt);
            o.Throttle = applied.ThrottleOverride ? applied.Throttle : _hoverThrottle;
            o.ThrottleActive = !applied.ThrottleOverride;
        }
        else
        {
            o.Throttle = EnergyChannel(s, cmd, applied, hDotPath, ref t);
            o.ThrottleActive = cmd.Speed != SpeedMode.None && !applied.ThrottleOverride;
        }

        t.PCmd = pCmd;
        t.QCmd = qCmd;
        t.RCmd = rCmd;
        t.PEta = _pAxis.Eta;
        t.QEta = _qAxis.Eta;
        t.REta = _rAxis.Eta;
        t.EEta = _eAxis.Eta;
        Telemetry = t;

        LastPitch = o.Pitch;
        LastRoll = o.Roll;
        LastYaw = o.Yaw;
        LastThrottle = o.Throttle;
        return o;
    }

    private void Allocate(FlightState s, float aVert, float aLat, bool haveALat, float phiDes, bool pitchActive,
        float bankLimit, float nMax, ref float nDes, ref float muDes)
    {
        const float g = ControlMath.G;
        float cosGamma = Mathf.Max(Mathf.Cos(s.Gamma), 0.2f);
        float sinGamma = Mathf.Sin(s.Gamma);

        float aEv = (aVert - (sinGamma * s.VDot)) / cosGamma;
        float fEv = pitchActive ? aEv + (g * Mathf.Cos(s.Gamma)) : s.NLift * g * Mathf.Cos(s.Mu);
        float fEl = haveALat ? aLat : 0f;

        if (!haveALat)
        {
            muDes = ControlMath.IsFinite(phiDes) ? phiDes : 0f;
            if (pitchActive)
            {
                float cmu = Mathf.Cos(muDes);
                if (Mathf.Abs(cmu) > 0.12f)
                {
                    nDes = fEv / (g * cmu);
                }
            }

            return;
        }

        float upright = Mathf.Clamp01((fEv / g) - 0.05f) / 0.2f;
        float mu = Mathf.Atan2(fEl, Mathf.Max(fEv, 0.05f * g));
        mu = Mathf.Clamp(mu, -bankLimit, bankLimit) * Mathf.Clamp01(upright);
        muDes = mu;

        if (pitchActive)
        {
            float cmu = Mathf.Max(Mathf.Cos(mu), 0.1f);
            nDes = fEv / (g * cmu);

            // if vertical is fine but n is capped, the bank has to give
            if (nDes > nMax && fEv > 0f)
            {
                float cmuMax = Mathf.Clamp01(fEv / (g * nMax));
                float muMax = ControlMath.SafeAcos(cmuMax);
                muDes = Mathf.Clamp(mu, -muMax, muMax);
                nDes = nMax;
            }
        }
    }

    private static void ApplyDirect(AutopilotCommand cmd, AppliedInputs applied, ref ControlOutput o,
        out AppliedInputs track)
    {
        track = applied;
        if (ControlMath.IsFinite(cmd.DirectPitch) && !applied.PitchOverride)
        {
            o.PitchActive = false;
            track.Pitch = cmd.DirectPitch;
        }

        if (ControlMath.IsFinite(cmd.DirectRoll) && !applied.RollOverride)
        {
            o.RollActive = false;
            track.Roll = cmd.DirectRoll;
        }

        if (ControlMath.IsFinite(cmd.DirectYaw) && !applied.YawOverride)
        {
            o.YawActive = false;
            track.Yaw = cmd.DirectYaw;
        }
    }

    private static void FinishDirect(AutopilotCommand cmd, AppliedInputs applied, ref ControlOutput o)
    {
        if (ControlMath.IsFinite(cmd.DirectPitch) && !applied.PitchOverride)
        {
            o.PitchActive = true;
            o.Pitch = Mathf.Clamp(cmd.DirectPitch, -1f, 1f);
        }

        if (ControlMath.IsFinite(cmd.DirectRoll) && !applied.RollOverride)
        {
            o.RollActive = true;
            o.Roll = Mathf.Clamp(cmd.DirectRoll, -1f, 1f);
        }

        if (ControlMath.IsFinite(cmd.DirectYaw) && !applied.YawOverride)
        {
            o.YawActive = true;
            o.Yaw = Mathf.Clamp(cmd.DirectYaw, -1f, 1f);
        }
    }

    private void VerticalAccelLimits(float nMax, float nMin, AutopilotCommand cmd, out float up, out float down)
    {
        const float g = ControlMath.G;
        float upPhys = 0.8f * g * Mathf.Max(nMax - 1f, 0.2f);
        float downPhys = 0.8f * g * Mathf.Max(1f - nMin, 0.2f);
        float upReq = ControlMath.IsFinite(cmd.VerticalAccelUp) && cmd.VerticalAccelUp > 0f
            ? cmd.VerticalAccelUp
            : Settings.VerticalAccelDefault;
        float downReq = ControlMath.IsFinite(cmd.VerticalAccelDown) && cmd.VerticalAccelDown > 0f
            ? cmd.VerticalAccelDown
            : Settings.VerticalAccelDefault;
        up = Mathf.Max(Mathf.Min(upReq, upPhys), 0.5f);
        down = Mathf.Max(Mathf.Min(downReq, downPhys), 0.5f);
    }

    private float VerticalShapeAccel(float nMax, float nMin, AutopilotCommand cmd)
    {
        VerticalAccelLimits(nMax, nMin, cmd, out float up, out float down);
        return Mathf.Min(Settings.VerticalAccelShape, 0.5f * Mathf.Min(up, down));
    }

    private float SpeedPriority(FlightState s, AutopilotCommand cmd, float vsDes)
    {
        if (cmd.Speed != SpeedMode.Airspeed || _energyLimited == 0 || !cmd.SpeedPriority)
        {
            return vsDes;
        }

        const float g = ControlMath.G;
        float vMeas = cmd.SpeedIsInertial ? s.Speed : s.V;
        float vErr = cmd.Airspeed - vMeas;
        float vDotDes = Mathf.Clamp(0.3f * vErr, -3f, 3f);
        float available = _energyRateMeasured - (vMeas * vDotDes / g);

        if (_energyLimited > 0 && vErr > 3f)
        {
            return Mathf.Min(vsDes, Mathf.Max(available, 0f));
        }
        else if (_energyLimited < 0 && vErr < -3f)
        {
            return Mathf.Max(vsDes, Mathf.Min(available, 0f));
        }
        else
        {
            return vsDes;
        }
    }

    private float SpeedProtectVs(FlightState s, float vsDes)
    {
        if (vsDes <= 0f)
        {
            return vsDes;
        }

        float vProt = Model.AdjustedLandingSpeed() * Settings.SpeedProtectionFactor;
        float vMin = Model.AdjustedLandingSpeed() * 1.05f;
        float veas = s.V * Mathf.Sqrt(s.Rho / 1.225f);
        float f = Mathf.Clamp01((veas - vMin) / Mathf.Max(vProt - vMin, 1f));
        return vsDes * f;
    }

    private float RateChannel(IndiAxis axis, ResponseIdentifier id, ref bool wasActive, ref float cmdPrev,
        bool active, float rate, float rateCmd, float appliedInput, float bandwidth, float gainPrior, float lagPrior,
        float gainMinFactor, float authority, float cutoff, int delay, bool adapt, bool learn, float dt,
        out float gainUsed)
    {
        ControllerSettings c = Settings;

        id.Update(axis.Command, rate, delay, gainPrior, learn && c.ResponseIdentification, dt);
        float gain = gainPrior, lag = lagPrior;
        if (c.ResponseIdentification && id.Confident)
        {
            gain = Mathf.Clamp(id.Gain * 1.15f, gainMinFactor * gainPrior, 2f * gainPrior);
            lag = Mathf.Clamp(id.Tau, 0.4f * lagPrior, 3f * lagPrior);
        }

        gainUsed = gain;

        float tau = c.AccelerationInnerLoop ? c.ActuatorTau : lag;
        axis.EffectivenessMargin = c.AccelerationInnerLoop ? 1.3f : c.RateEffectivenessMargin;
        if (!active)
        {
            axis.Track(appliedInput, rate, c.AccelerationInnerLoop ? cutoff : c.CompensationCutoff, tau, 0f, delay, dt);
            wasActive = false;
            return appliedInput;
        }

        if (!wasActive)
        {
            cmdPrev = rateCmd;
            wasActive = true;
        }

        float cmdDot = (rateCmd - cmdPrev) / dt;
        cmdPrev = rateCmd;

        if (!c.AccelerationInnerLoop)
        {
            return axis.StepRateCommand(rate, rateCmd, gain, -authority, authority, c.StickRateLimit, delay,
                c.CompensationCutoff, c.CompensationGain, adapt && !c.ResponseIdentification, tau, dt);
        }

        float nu = (bandwidth * (rateCmd - rate)) + (c.RateFeedForward * cmdDot);
        return axis.Step(rate, nu, gain / lag, -authority, authority, c.StickRateLimit, delay, cutoff, adapt,
            tau, 0f, dt);
    }

    // private float PitchLagNow() =>
    //     Settings.ResponseIdentification && _qId.Confident ? _qId.Tau : Settings.PitchLag;

    // private float RollLagNow() =>
    //     Settings.ResponseIdentification && _pId.Confident ? _pId.Tau : Settings.RollLag;

    private float EnergyChannel(FlightState s, AutopilotCommand cmd, AppliedInputs applied, float hDotPath,
        ref ControllerTelemetry t)
    {
        ControllerSettings c = Settings;
        float dt = Mathf.Max(s.Dt, 1e-3f);
        const float g = ControlMath.G;
        float vMeas = cmd.SpeedIsInertial ? s.Speed : s.V;
        float v = Mathf.Max(vMeas, 5f);
        float energyHeight = s.Altitude + (vMeas * vMeas / (2f * g));
        float spool = c.EngineSpoolRate;
        int tDelay = c.ThrottleDelayTicks;

        const float engineTau = 0.1f;
        bool active = cmd.Speed != SpeedMode.None && !applied.ThrottleOverride;
        if (!active)
        {
            _eAxis.Track(applied.Throttle, energyHeight, c.SpeedFilterCutoff, engineTau, spool, tDelay, dt);
            _hDotPathFilter.Reset(s.VerticalSpeed);
            _tWasActive = false;
            return applied.Throttle;
        }

        float tMin = cmd.AllowExtremeThrottle ? 0f : c.ThrottleMin;
        float tMax = cmd.AllowExtremeThrottle ? 1f : c.ThrottleMax;
        if (ControlMath.IsFinite(cmd.ThrottleMinOverride))
        {
            tMin = Mathf.Clamp01(cmd.ThrottleMinOverride);
        }

        if (ControlMath.IsFinite(cmd.ThrottleMaxOverride))
        {
            tMax = Mathf.Clamp(cmd.ThrottleMaxOverride, tMin, 1f);
        }

        if (cmd.Speed == SpeedMode.Throttle)
        {
            float direct = Mathf.Clamp(cmd.Throttle, 0f, 1f);
            _eAxis.Track(direct, energyHeight, c.SpeedFilterCutoff, engineTau, spool, tDelay, dt);
            _tWasActive = false;
            return direct;
        }

        if (!_tWasActive)
        {
            _hDotPathFilter.Reset(s.VerticalSpeed);
            _tWasActive = true;
        }

        float vErr = cmd.Airspeed - vMeas;
        float vDotDes = ControlMath.ShapedRate(vErr, c.SpeedGain, c.SpeedAccelShape, 2f * c.SpeedAccelShape);

        // intent of the path channel: climbing needs power now, not after the speed has decayed
        float hDotIntent = Mathf.Lerp(s.VerticalSpeed, hDotPath, c.EnergyFeedForward);
        float hDot = _hDotPathFilter.Step(hDotIntent, 0.5f, dt);

        float nu = (v * vDotDes / g) + hDot;
        float gE = v * Model.ThrottleEffectiveness(s.Rho) / (g * c.ThrottleEffectivenessScale);

        bool airbrakeAllowed = tMin <= 1e-4f;
        if (airbrakeAllowed && AirbrakeActive)
        {
            if (vErr > -1.5f)
            {
                AirbrakeActive = false;
            }
            else
            {
                _eAxis.Track(0f, energyHeight, c.SpeedFilterCutoff, engineTau, spool, tDelay, dt);
                t.VDotCmd = vDotDes;
                t.EnergyRateCmd = nu;
                _energyLimited = -1;
                _energyRateMeasured = _eAxis.Derivative;
                return 0f;
            }
        }
        else if (!airbrakeAllowed)
        {
            AirbrakeActive = false;
        }

        float tMinIndi = airbrakeAllowed ? 0.004f : tMin;
        float thr = _eAxis.Step(energyHeight, nu, gE, tMinIndi, tMax, 0f, tDelay, c.SpeedFilterCutoff,
            c.OnlineEstimation, engineTau, spool, dt);

        if (airbrakeAllowed && thr <= tMinIndi + 1e-4f && vErr < -6f)
        {
            _airbrakeTimer += dt;
            if (_airbrakeTimer > 0.3f)
            {
                AirbrakeActive = true;
                _airbrakeTimer = 0f;
                thr = 0f;
            }
        }
        else
        {
            _airbrakeTimer = 0f;
        }

        t.VDotCmd = vDotDes;
        t.EnergyRateCmd = nu;

        const float satTime = 0.5f;
        _thrSatHighTime = thr >= tMax - 0.005f ? _thrSatHighTime + dt : 0f;
        _thrSatLowTime = thr <= tMinIndi + 0.005f ? _thrSatLowTime + dt : 0f;
        _energyLimited = _thrSatHighTime > satTime ? 1 : _thrSatLowTime > satTime ? -1 : 0;
        _energyRateMeasured = _eAxis.Derivative;
        return thr;
    }

    private float PriorPitch(FlightState s)
    {
        if (Model.IsHelicopter)
        {
            return Model.HeloMaxAngularVel.x;
        }

        if (Model.FbwEnabled && s.V > 25f)
        {
            return Model.FbwPitchRatePerStick(s.V, s.Rho);
        }

        return DirectPrior(s, 0.6f);
    }

    private float PriorRoll(FlightState s)
    {
        if (Model.IsHelicopter)
        {
            return Model.HeloMaxAngularVel.z;
        }

        if (Model.FbwEnabled && s.V > 25f)
        {
            return Model.FbwRollRatePerStick(s.V, s.Rho);
        }

        return DirectPrior(s, 2f);
    }

    private float PriorYaw(FlightState s)
    {
        if (Model.IsHelicopter)
        {
            return Model.HeloMaxAngularVel.y;
        }

        if (Model.FbwEnabled && s.V > 25f)
        {
            return Model.FbwYawRatePerStick();
        }

        return DirectPrior(s, 0.3f);
    }

    private float DirectPrior(FlightState s, float atCorner)
    {
        float qRatio = Model.QRatio(s.V, s.Rho);
        return atCorner * Mathf.Clamp(qRatio, 0.2f, 3f);
    }

    private void UpdateLiftSlope(FlightState s)
    {
        float veas = s.V * Mathf.Sqrt(s.Rho / 1.225f);
        float corner = Mathf.Max(Model.CornerSpeed, 1f);
        float r2 = veas * veas / (corner * corner);
        float alphaMax = Mathf.Max(Model.FbwAlphaLimiter * Mathf.Deg2Rad, 0.05f);
        float prior = Model.GLimit / alphaMax * r2;

        float stat = float.NaN;
        if (s.Alpha > 0.015f && s.NLift > 0.3f && !s.OnGround)
        {
            stat = Mathf.Clamp(s.NLift / s.Alpha, 1f, 300f);
        }

        float dt = Mathf.Max(s.Dt, 1e-3f);
        if (!_nAlphaInit)
        {
            _aLp = s.Alpha;
            _nLp = s.NLift;
            _aBp = _nBp = 0f;
            _sAn = _sAa = 0f;
            _nAlpha = float.IsNaN(stat) ? prior : stat;
            _nAlphaInit = true;
        }

        float al = 1f - Mathf.Exp(-6f * dt);
        float ah = Mathf.Exp(-0.2f * dt);
        float aPrev = _aLp, nPrev = _nLp;
        _aLp += al * (s.Alpha - _aLp);
        _nLp += al * (s.NLift - _nLp);
        _aBp = ah * (_aBp + (_aLp - aPrev));
        _nBp = ah * (_nBp + (_nLp - nPrev));

        float lambda = Mathf.Exp(-dt / 6f);
        bool learn = Settings.OnlineEstimation && !s.OnGround;
        float qn = Mathf.Max(r2, 0.02f);
        if (learn)
        {
            _sAn = (lambda * _sAn) + (_aBp * _nBp / qn);
            _sAa = (lambda * _sAa) + (_aBp * _aBp);
        }

        float baseline = float.IsNaN(stat) ? prior : stat;
        float target = baseline;
        float excitation = _sAa * (1f - lambda);
        if (excitation > 2e-6f && _sAn > 0f)
        {
            target = _sAn / _sAa * qn;
        }

        _nAlpha = Mathf.Lerp(_nAlpha, target, 1f - Mathf.Exp(-dt / 1.5f));
    }

    private float _aLp, _nLp, _aBp, _nBp, _sAn, _sAa;

    private ControlOutput StepHelicopter(FlightState s, AutopilotCommand cmd, AppliedInputs applied)
    {
        ControllerSettings c = Settings;
        float dt = Mathf.Max(s.Dt, 1e-3f);
        const float g = ControlMath.G;
        ControlOutput o = default;
        ControllerTelemetry t = default;

        bool vertActive = cmd.Vertical != VerticalMode.None;
        float aVert = 0f;
        float vsDes = s.VerticalSpeed;
        switch (cmd.Vertical)
        {
            case VerticalMode.Altitude:
                {
                    float err = cmd.Altitude - s.Altitude;
                    vsDes = ControlMath.ShapedRate(err, c.AltitudeGain, 2f, 0f) + cmd.VerticalSpeedFeedForward;
                    vsDes = Mathf.Clamp(vsDes, -Mathf.Max(cmd.MaxDescentRate, 0.5f), Mathf.Max(cmd.MaxClimbRate, 0.5f));
                    aVert = c.VerticalSpeedGain * (vsDes - s.VerticalSpeed);
                    break;
                }
            case VerticalMode.VerticalSpeed:
            case VerticalMode.FlightPathAngle:
                vsDes = cmd.Vertical == VerticalMode.VerticalSpeed ? cmd.VerticalSpeed : s.Speed * Mathf.Sin(cmd.FlightPathAngle);
                aVert = c.VerticalSpeedGain * (vsDes - s.VerticalSpeed);
                break;
            case VerticalMode.LoadFactor:
                aVert = (cmd.LoadFactor - 1f) * g;
                break;
            case VerticalMode.Acceleration:
                aVert = cmd.VerticalAccel;
                break;
            case VerticalMode.PitchAttitude:
                vertActive = false;
                break;
        }

        aVert = Mathf.Clamp(aVert, -0.5f * g, Mathf.Max(Model.HeloGLimit - 1f, 0.5f) * g);
        t.VsCmd = vsDes;
        t.AVertCmd = aVert;

        bool collectiveActive = vertActive && !applied.ThrottleOverride;
        float collective;
        if (collectiveActive)
        {
            float gC = 2f * g / c.ThrottleEffectivenessScale;
            collective = _vzAxis.Step(s.VerticalSpeed, aVert, gC, 0f, 1f, 0f, c.InputDelayTicks,
                c.SpeedFilterCutoff * 2f, c.OnlineEstimation, 0.2f, 2f, dt);
        }
        else
        {
            _vzAxis.Track(applied.Throttle, s.VerticalSpeed, c.SpeedFilterCutoff * 2f, 0.2f, 2f, c.InputDelayTicks, dt);
            collective = applied.Throttle;
        }

        float fwdSpeed = Vector3.Dot(s.Velocity, Vector3.ProjectOnPlane(s.Forward, Vector3.up).normalized);
        bool pitchActive = cmd.PitchAxisActive || cmd.Speed == SpeedMode.Airspeed;
        if (cmd.Speed == SpeedMode.Airspeed)
        {
            _heloSpeedHold = cmd.Airspeed;
        }
        else if (!ControlMath.IsFinite(_heloSpeedHold) || !pitchActive)
        {
            _heloSpeedHold = fwdSpeed;
        }

        float aFwd = Mathf.Clamp(c.SpeedGain * (_heloSpeedHold - fwdSpeed), -0.3f * g, 0.3f * g);
        float thetaDes = -Mathf.Atan(aFwd / g);
        thetaDes = Mathf.Clamp(thetaDes, -0.35f, 0.3f);
        t.VDotCmd = aFwd;

        float qCmd = float.NaN;
        if (ControlMath.IsFinite(cmd.PitchRateOverride))
        {
            qCmd = cmd.PitchRateOverride;
        }
        else if (cmd.Vertical == VerticalMode.PitchAttitude)
        {
            qCmd = c.PitchAttitudeGain * ControlMath.WrapPi(cmd.PitchAttitude - s.Theta);
        }
        else if (pitchActive)
        {
            qCmd = c.PitchAttitudeGain * ControlMath.WrapPi(thetaDes - s.Theta);
        }

        float pCmd = float.NaN, rCmd = float.NaN;
        float bankLimit = Mathf.Clamp(cmd.BankLimit, 0.02f, 0.6f);
        bool fast = s.GroundSpeed > 25f;
        if (ControlMath.IsFinite(cmd.RollRateOverride))
        {
            pCmd = cmd.RollRateOverride;
        }
        else if (cmd.RollAxisActive)
        {
            float phiDes;
            switch (cmd.Lateral)
            {
                case LateralMode.Bank:
                    phiDes = Mathf.Clamp(cmd.Bank, -bankLimit, bankLimit);
                    break;
                case LateralMode.Course when fast:
                    {
                        float chiErr = ControlMath.WrapPi(cmd.Course - s.Chi);
                        float chiDot = ControlMath.ShapedRate(chiErr, c.CourseGain, g * c.CourseRollRate / s.GroundSpeed,
                            g * Mathf.Tan(bankLimit) / s.GroundSpeed);
                        phiDes = Mathf.Atan(s.GroundSpeed * chiDot / g);
                        break;
                    }
                case LateralMode.Acceleration:
                    phiDes = Mathf.Clamp(Mathf.Atan(cmd.LateralAccel / g), -bankLimit, bankLimit);
                    break;
                default:
                    {
                        // slow: kill sideways drift with bank
                        float sideSpeed = Vector3.Dot(s.Velocity, s.Right);
                        phiDes = Mathf.Clamp(-0.05f * sideSpeed, -0.25f, 0.25f);
                        break;
                    }
            }

            pCmd = ControlMath.ShapedRate(ControlMath.WrapPi(phiDes - s.Phi), c.BankGain, c.MaxRollAccel, 1f);
            t.MuCmd = phiDes;

            if (cmd.Lateral == LateralMode.Course && !fast)
            {
                float psiErr = ControlMath.WrapPi(cmd.Course - s.Psi);
                rCmd = ControlMath.ShapedRate(psiErr, 0.8f, 0.5f, 0.5f);
            }
            else
            {
                rCmd = c.YawCoordination && fast
                    ? (g / Mathf.Max(s.V, 10f) * Mathf.Sin(s.Phi)) + (c.SideslipGain * s.Beta)
                    : 0f;
            }
        }

        if (ControlMath.IsFinite(cmd.YawRateOverride))
        {
            rCmd = cmd.YawRateOverride;
        }

        float cutoff = c.FilterCutoff;
        int delay = c.InputDelayTicks;
        bool adapt = c.OnlineEstimation;
        o.PitchActive = ControlMath.IsFinite(qCmd) && !applied.PitchOverride;
        o.RollActive = ControlMath.IsFinite(pCmd) && !applied.RollOverride;
        o.YawActive = ControlMath.IsFinite(rCmd) && !applied.YawOverride;

        float gP = PriorRoll(s), gQ = PriorPitch(s), gR = PriorYaw(s);
        ApplyDirect(cmd, applied, ref o, out AppliedInputs track);
        bool learn = !s.OnGround && s.V > 40f && s.RadarAltitude > 5f;
        float pitchMin = Model.IsHelicopter ? 0.15f : 0.5f;
        o.Pitch = RateChannel(_qAxis, _qId, ref _qWasActive, ref _qCmdPrev, o.PitchActive, s.Q, qCmd, track.Pitch,
            c.PitchRateBandwidth, gQ, c.PitchLag, pitchMin, c.PitchAuthority, cutoff, delay, adapt, learn, dt,
            out t.QGain);
        o.Roll = RateChannel(_pAxis, _pId, ref _pWasActive, ref _pCmdPrev, o.RollActive, s.P, pCmd, track.Roll,
            c.RollRateBandwidth, gP, c.RollLag, 0.1f, c.RollAuthority, cutoff, delay, adapt, learn, dt, out t.PGain);
        o.Yaw = RateChannel(_rAxis, _rId, ref _rWasActive, ref _rCmdPrev, o.YawActive, s.R, rCmd, track.Yaw,
            c.YawRateBandwidth, gR, c.YawLag, 0.1f, c.YawAuthority, cutoff, delay, adapt, learn, dt, out t.RGain);
        FinishDirect(cmd, applied, ref o);

        o.Throttle = collective;
        o.ThrottleActive = collectiveActive;

        t.PCmd = pCmd;
        t.QCmd = qCmd;
        t.RCmd = rCmd;
        t.PEta = _pAxis.Eta;
        t.QEta = _qAxis.Eta;
        t.REta = _rAxis.Eta;
        t.EEta = _vzAxis.Eta;
        Telemetry = t;

        LastPitch = o.Pitch;
        LastRoll = o.Roll;
        LastYaw = o.Yaw;
        LastThrottle = o.Throttle;
        return o;
    }
}
