extern alias JetBrains;

using System;
using System.Collections;

using BepInEx.Configuration;

using HarmonyLib;

using JetBrains.Annotations;

using NOAutopilot.Core.Config;
using NOAutopilot.Core.PID;

using UnityEngine;

using Random = UnityEngine.Random;

namespace NOAutopilot.Core.Flight;

[HarmonyPatch(typeof(PilotPlayerState), "PlayerAxisControls")]
internal static class ControlOverridePatch
{
    private static readonly PIDLoop3 PidAlt = new();
    private static readonly PIDLoop3 PidVS = new();
    private static readonly PIDLoop3 PidAngle = new();
    private static readonly PIDLoop3 PidRoll = new();
    private static readonly PIDLoop3 PidGCAS = new();
    private static readonly PIDLoop3 PidSpd = new();
    private static readonly PIDLoop3 PidCrs = new();

    private static PIDConfig s_cfgAlt;
    private static PIDConfig s_cfgVS;
    private static PIDConfig s_cfgAngle;
    private static PIDConfig s_cfgRoll;
    private static PIDConfig s_cfgGCAS;
    private static PIDConfig s_cfgSpd;
    private static PIDConfig s_cfgCrs;
    private static bool s_wasEnabled;
    private static float s_pitchSleepUntil;
    private static float s_rollSleepUntil;
    private static float s_spdSleepUntil;
    private static bool s_isPitchSleeping;
    private static bool s_isRollSleeping;
    private static bool s_isSpdSleeping;
    private static float s_gcasNextScan;
    private static float s_overGFactor = 1.0f;
    private static bool s_dangerImminent;
    private static bool s_warningZone;
    public static bool ApStateBeforeGCAS;
    private static float s_currentAppliedThrottle;

    private static float s_jammerNextFireTime;
    private static float s_jammerNextReleaseTime;
    private static bool s_isJammerHoldingTrigger;

    private static float s_disengageTimer;

    public static float ThrottleOutput;

    private static void ConfigurePID(
    PIDLoop3 pid, ref PIDConfig cfg,
    ConfigEntry<PIDTuning> tuning,
    float dt,
    float minOutput, float maxOutput)
    {
        PIDConfig.Apply(ref cfg, pid,
            tuning.Value,
            Mathf.Max(dt, 0.0001f),
            minOutput, maxOutput);
    }

    public static void Reset()
    {
        PidAlt.Reset();
        PidVS.Reset();
        PidAngle.Reset();
        PidRoll.Reset();
        PidGCAS.Reset();
        PidSpd.Reset();
        PidCrs.Reset();

        s_cfgAlt = default;
        s_cfgVS = default;
        s_cfgAngle = default;
        s_cfgRoll = default;
        s_cfgGCAS = default;
        s_cfgSpd = default;
        s_cfgCrs = default;

        s_wasEnabled = false;
        s_pitchSleepUntil = 0f;
        s_rollSleepUntil = 0f;
        s_spdSleepUntil = 0f;
        s_isPitchSleeping = false;
        s_isRollSleeping = false;
        s_isSpdSleeping = false;
        s_gcasNextScan = 0f;
        s_overGFactor = 1.0f;
        s_dangerImminent = false;
        s_warningZone = false;
        ApStateBeforeGCAS = false;
        s_currentAppliedThrottle = 0f;

        s_jammerNextFireTime = 0f;
        s_jammerNextReleaseTime = 0f;
        s_isJammerHoldingTrigger = false;

        s_disengageTimer = 0f;
    }

    private static void ResetIntegrators(float inputThrottle)
    {
        PidAlt.Reset();
        PidVS.Reset();
        PidAngle.Reset();
        PidRoll.Reset();
        PidGCAS.Reset();
        PidCrs.Reset();

        PidAlt.Feedforward = 0;
        PidVS.Feedforward = 0;
        PidAngle.Feedforward = 0;
        PidRoll.Feedforward = 0;
        PidGCAS.Feedforward = 0;
        PidSpd.Feedforward = 0;
        PidCrs.Feedforward = 0;

        if (APData.TargetSpeed < 0)
        {
            PidSpd.Reset();
            // start throttle from current position
            PidSpd.SeedOutput(Mathf.Clamp01(inputThrottle));
            s_currentAppliedThrottle = inputThrottle;
        }

        s_isPitchSleeping = s_isRollSleeping = s_isSpdSleeping = false;
        s_pitchSleepUntil = s_rollSleepUntil = s_spdSleepUntil = 0f;
    }

    [UsedImplicitly]
    private static void Postfix(PilotPlayerState __instance)
    {
        if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
        {
            return;
        }

        if (__instance == null || APData.LocalAircraft == null || APData.PlayerRB == null ||
            APData.PlayerTransform == null)
        {
            APData.Enabled = false;
            APData.GCASActive = false;
            return;
        }

        try
        {
            APData.CurrentAlt = APData.LocalAircraft.transform.position.GlobalY();

            if (APData.CurrentMaxClimbRate < 0f)
            {
                APData.CurrentMaxClimbRate = Plugin.DefaultMaxClimbRate.Value;
            }

            APData.CurrentRoll = APData.PlayerTransform.eulerAngles.z;
            if (APData.CurrentRoll > 180f)
            {
                APData.CurrentRoll -= 360f;
            }

            ControlInputs inputObj = __instance.controlInputs;
            if (inputObj == null)
            {
                return;
            }

            float stickPitch = inputObj.pitch;
            float stickRoll = inputObj.roll;
            float currentThrottle = inputObj.throttle;
            bool pilotPitch = Mathf.Abs(stickPitch) > Plugin.StickTempThreshold.Value;
            bool pilotRoll = Mathf.Abs(stickRoll) > Plugin.StickTempThreshold.Value;

            Vector3 pForward = APData.PlayerTransform.forward;
            Vector3 pUp = APData.PlayerTransform.up;
            Vector3 localAngVel = APData.PlayerTransform.InverseTransformDirection(APData.PlayerRB.angularVelocity);

            float dt = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            float noiseT = Time.time * Plugin.RandomSpeed.Value;

            if (Plugin.StepTestKey.Value.IsDown())
            {
                PIDLogger.RequestTest(Plugin.StepTestLoop.Value);
            }

            if (!APData.Enabled && !APData.GCASActive && PIDLogger.IsTestActive)
            {
                PIDLogger.StopTest();
            }

            bool useRandom = Plugin.RandomEnabled.Value &&
            !APData.GCASActive && !PIDLogger.IsTestActive;

            APData.GCASWarning = false;
            float currentG = 1f;

            if (APData.Enabled != s_wasEnabled)
            {
                if (APData.Enabled)
                {
                    ResetIntegrators(currentThrottle);
                    if (APData.FBWDisabled)
                    {
                        APData.FBWDisabled = false;
                        Plugin.UpdateFBWState();
                    }
                }

                s_wasEnabled = APData.Enabled;
                APData.UseSetValues = false;
            }

            // waypoint deletion
            if (APData.NavQueue.Count > 0)
            {
                Vector3 targetPos = APData.NavQueue[0];
                Vector3 playerPos = APData.PlayerRB.position.ToGlobalPosition().AsVector3();
                Vector3 diff = targetPos - playerPos;

                float distSq = new Vector2(diff.x, diff.z).sqrMagnitude;
                bool passed = Vector3.Dot(pForward, diff.normalized) < 0;

                float threshold = Plugin.NavReachDistance.Value;
                float passedThreshold = Plugin.NavPassedDistance.Value;

                // if (close) or (behind and not too far away)
                if (distSq < threshold * threshold || (passed && distSq < passedThreshold * passedThreshold))
                {
                    Vector3 reachedPoint = APData.NavQueue[0];
                    APData.NavQueue.RemoveAt(0);
                    if (Plugin.NavCycle.Value && APData.NavQueue.Count >= 1)
                    {
                        APData.NavQueue.Add(reachedPoint);
                    }

                    Plugin.RefreshNavVisuals();
                    if (APData.NavQueue.Count == 0)
                    {
                        APData.NavEnabled = false;
                    }
                }

                if (APData.Enabled && APData.NavEnabled && !APData.GCASActive)
                {
                    float bearing = Mathf.Atan2(diff.x, diff.z) * Mathf.Rad2Deg;
                    APData.TargetCourse = (bearing + 360f) % 360f;
                }
            }

            // can a plane have no pilot? need to measure g
            Vector3 pAccel = default;
            if (APData.LocalPilot != null)
            {
                pAccel = APData.LocalPilot.GetAccel();
                currentG = Vector3.Dot(pAccel + Vector3.up, pUp);

                Component pilotComp = APData.LocalPilot;
                if (pilotComp != null)
                {
                    GLOC gloc = pilotComp.GetComponent<GLOC>();
                    if (gloc != null)
                    {
                        APData.BloodPressure = gloc.bloodPressure;
                        APData.IsConscious = gloc.conscious;
                    }
                }
            }

            // gcas
            if (APData.GCASEnabled || Plugin.ShowGCASChevronOff.Value)
            {
                bool gearDown = false;
                Aircraft acRef = APData.LocalAircraft;
                if (acRef.gearState != LandingGear.GearState.LockedRetracted)
                {
                    gearDown = true;
                }

                APData.IsOnGround = false;
                float currentRadarAlt = APData.LocalAircraft.radarAlt;
                if (gearDown && currentRadarAlt < 0.1f)
                {
                    APData.IsOnGround = true;
                }

                bool pilotOverride = Mathf.Abs(stickPitch) > Plugin.GCAS_Deadzone.Value ||
                                     Mathf.Abs(stickRoll) > Plugin.GCAS_Deadzone.Value || gearDown;

                if (pilotOverride && APData.GCASActive)
                {
                    APData.GCASActive = false;
                    APData.Enabled = false;
                }

                float speed = APData.PlayerRB.velocity.magnitude;
                if (speed > 0f)
                {
                    Vector3 velocity = APData.PlayerRB.velocity;
                    float descentRate = velocity.y < 0 ? Mathf.Abs(velocity.y) : 0f;

                    float currentRollAbs = Mathf.Abs(APData.CurrentRoll);
                    const float estimatedRollRate = 60f;
                    float timeToRollUpright = currentRollAbs / estimatedRollRate;

                    float gAccel = Plugin.GCAS_MaxG.Value * 9.81f;
                    float turnRadius = speed * speed / gAccel;

                    float reactionTime = Plugin.GCAS_AutoBuffer.Value + (Time.deltaTime * 2.0f) + timeToRollUpright;
                    float reactionDist = speed * reactionTime;
                    float warnDist = speed * Plugin.GCAS_WarnBuffer.Value;

                    s_overGFactor = 1.0f;

                    if (Time.time >= s_gcasNextScan)
                    {
                        s_gcasNextScan = Time.time + 0.02f;

                        s_dangerImminent = false;
                        s_warningZone = false;

                        APData.GCASConverge = 0f;

                        Vector3 castStart = APData.PlayerRB.position + (velocity.normalized * 5f);
                        float scanRange = (turnRadius * 1.5f) + warnDist + 500f;

                        if (Physics.SphereCast(castStart, Plugin.GCAS_ScanRadius.Value, velocity.normalized,
                                out RaycastHit hit, scanRange, 8256))
                        {
                            if (hit.transform.root != APData.PlayerTransform.root)
                            {
                                float turnAngle = Mathf.Abs(Vector3.Angle(velocity, hit.normal) - 90f);
                                float reqArc = turnRadius * (turnAngle * Mathf.Deg2Rad);

                                if (hit.distance < reqArc + reactionDist + 20f)
                                {
                                    s_dangerImminent = true;

                                    float availableArcDist =
                                        hit.distance - reactionDist - (speed * timeToRollUpright);

                                    if (availableArcDist < reqArc)
                                    {
                                        float neededRadius = availableArcDist / (turnAngle * Mathf.Deg2Rad);
                                        neededRadius = Mathf.Max(neededRadius, 1f);
                                        float gRequired = speed * speed / (neededRadius * 9.81f);

                                        s_overGFactor =
                                            Mathf.Max(s_overGFactor, gRequired / Plugin.GCAS_MaxG.Value);
                                    }
                                }
                                else if (hit.distance < reqArc + reactionDist + warnDist)
                                {
                                    s_warningZone = true;
                                    float distToTrigger = hit.distance - (reqArc + reactionDist + 20f);
                                    float totalWarnRange = warnDist - 20f;
                                    float fraction = 1f - (distToTrigger / Mathf.Max(totalWarnRange, 1f));
                                    APData.GCASConverge = Mathf.Clamp01(fraction);
                                }
                            }
                        }
                    }

                    if (descentRate > 0f)
                    {
                        float diveAngle = Vector3.Angle(velocity, Vector3.ProjectOnPlane(velocity, Vector3.up));
                        float vertBuffer = descentRate * reactionTime;
                        float availablePullAlt = APData.CurrentAlt - vertBuffer;
                        float pullUpLoss = turnRadius * (1f - Mathf.Cos(diveAngle * Mathf.Deg2Rad));

                        if (availablePullAlt < pullUpLoss)
                        {
                            s_dangerImminent = true;

                            float availableRadius =
                                availablePullAlt / (1f - Mathf.Cos(diveAngle * Mathf.Deg2Rad));
                            availableRadius = Mathf.Max(availableRadius, 1f);

                            float gReqFloor = speed * speed / (availableRadius * 9.81f);

                            s_overGFactor = Mathf.Max(s_overGFactor, gReqFloor / Plugin.GCAS_MaxG.Value);
                        }
                        else if (APData.CurrentAlt <
                                 pullUpLoss + vertBuffer + (descentRate * Plugin.GCAS_WarnBuffer.Value))
                        {
                            s_warningZone = true;
                            float triggerAlt = pullUpLoss + vertBuffer;
                            float warnRange = descentRate * Plugin.GCAS_WarnBuffer.Value;
                            float distToTrigger = APData.CurrentAlt - triggerAlt;
                            float fraction = 1f - (distToTrigger / Mathf.Max(warnRange, 1f));
                            APData.GCASConverge = Mathf.Max(APData.GCASConverge, Mathf.Clamp01(fraction));
                        }
                    }

                    if (APData.GCASActive)
                    {
                        bool safeToRelease = false;

                        if (!s_dangerImminent)
                        {
                            if (velocity.y >= 0f || pilotPitch || pilotRoll)
                            {
                                safeToRelease = true;
                            }
                        }

                        if (safeToRelease)
                        {
                            APData.GCASActive = false;
                            APData.Enabled = false;
                            PidGCAS.Reset();
                            PidAlt.Reset();
                            PidVS.Reset();
                            PidAngle.Reset();
                            if (Plugin.DisableATAPGCAS.Value)
                            {
                                APData.TargetSpeed = -1f;
                                Plugin.SyncMenuValues();
                            }
                        }
                        else
                        {
                            APData.GCASWarning = true;
                            APData.TargetRoll = 0f;
                            APData.GCASConverge = 1f;
                        }
                    }
                    else if (s_dangerImminent)
                    {
                        if (!pilotOverride && APData.GCASEnabled)
                        {
                            ApStateBeforeGCAS = APData.Enabled;
                            APData.Enabled = true;
                            APData.GCASActive = true;
                            APData.ALSActive = false;
                            APData.TargetRoll = 0f;
                            if (APData.FBWDisabled)
                            {
                                APData.FBWDisabled = false;
                                Plugin.UpdateFBWState();
                            }
                        }

                        APData.GCASWarning = true;
                        APData.GCASConverge = 1f;
                    }
                    else if (s_warningZone)
                    {
                        APData.GCASWarning = true;
                    }
                }
            }

            // auto jam
            if (APData.LocalWeaponManager != null && APData.AutoJammerActive)
            {
                WeaponManager wm = APData.LocalWeaponManager;
                if (wm != null)
                {
                    bool fire = false;
                    WeaponStation currStation = wm.currentWeaponStation;
                    if (currStation?.Weapons is IList wpnList)
                    {
                        for (int i = 0; i < wpnList.Count; i++)
                        {
                            if (wpnList[i] is JammingPod)
                            {
                                fire = true;
                                break;
                            }
                        }
                    }

                    if (fire)
                    {
                        fire = wm.targetList is IList { Count: > 0 };
                    }

                    if (fire)
                    {
                        PowerSupply ps = APData.LocalAircraft.powerSupply;
                        if (ps != null)
                        {
                            float cur = ps.charge;
                            float max = ps.maxCharge;
                            if (max <= 1f)
                            {
                                max = 100f;
                            }

                            if (cur / max >= Plugin.AutoJammerThreshold.Value)
                            {
                                if (!s_isJammerHoldingTrigger)
                                {
                                    if (s_jammerNextFireTime == 0f)
                                    {
                                        s_jammerNextFireTime = Time.time + (Plugin.AutoJammerRandom.Value
                                            ? Random.Range(Plugin.AutoJammerMinDelay.Value,
                                                Plugin.AutoJammerMaxDelay.Value)
                                            : 0f);
                                    }

                                    if (Time.time >= s_jammerNextFireTime)
                                    {
                                        s_isJammerHoldingTrigger = true;
                                        s_jammerNextFireTime = 0f;
                                    }
                                }
                            }
                            else
                            {
                                if (s_isJammerHoldingTrigger)
                                {
                                    if (s_jammerNextReleaseTime == 0f)
                                    {
                                        s_jammerNextReleaseTime = Time.time + (Plugin.AutoJammerRandom.Value
                                            ? Random.Range(Plugin.AutoJammerReleaseMin.Value,
                                                Plugin.AutoJammerReleaseMax.Value)
                                            : 0f);
                                    }

                                    if (Time.time >= s_jammerNextReleaseTime)
                                    {
                                        s_isJammerHoldingTrigger = false;
                                        s_jammerNextReleaseTime = 0f;
                                    }
                                }
                            }

                            if (s_isJammerHoldingTrigger)
                            {
                                wm.Fire();
                            }
                        }
                    }
                    else
                    {
                        s_isJammerHoldingTrigger = false;
                        s_jammerNextFireTime = 0f;
                    }
                }
            }

            // stick disengage
            if (pilotPitch || pilotRoll)
            {
                APData.LastOverrideInputTime = Time.time;

                if (APData.Enabled)
                {
                    bool triggerDisengage = false;

                    if (Plugin.StickDisengageEnabled.Value)
                    {
                        if (Mathf.Abs(stickPitch) > Plugin.StickDisengageThreshold.Value ||
                            Mathf.Abs(stickRoll) > Plugin.StickDisengageThreshold.Value)
                        {
                            triggerDisengage = true;
                        }
                    }

                    if (Plugin.DisengageDelay.Value > 0)
                    {
                        s_disengageTimer += dt;
                        if (s_disengageTimer >= Plugin.DisengageDelay.Value)
                        {
                            triggerDisengage = true;
                        }
                    }

                    if (triggerDisengage)
                    {
                        APData.Enabled = false;
                        if (Plugin.DisableNavAPStick.Value)
                        {
                            APData.NavEnabled = false;
                        }

                        if (Plugin.DisableATAPStick.Value)
                        {
                            APData.TargetSpeed = -1f;
                            Plugin.SyncMenuValues();
                        }

                        s_disengageTimer = 0f;
                    }
                }
            }
            else
            {
                s_disengageTimer = 0f;
            }

            bool isWaitingToReengage = Time.time - APData.LastOverrideInputTime < Plugin.ReengageDelay.Value;

            // throttle control
            if (APData.TargetSpeed >= 0 || PIDLogger.IsTesting(PIDLogger.StepTarget.Spd))
            {
                float currentSpeed = APData.LocalAircraft != null
                    ? APData.LocalAircraft.speed
                    : APData.PlayerRB.velocity.magnitude;
                float targetSpeedMS;

                if (APData.SpeedHoldIsMach)
                {
                    float currentAlt = APData.LocalAircraft.GlobalPosition().y;
                    float sos = LevelInfo.GetSpeedOfSound(currentAlt);
                    targetSpeedMS = APData.TargetSpeed * sos;
                }
                else
                {
                    targetSpeedMS = APData.TargetSpeed;
                }

                targetSpeedMS = PIDLogger.GetSetpoint(PIDLogger.StepTarget.Spd, targetSpeedMS, currentSpeed);
                float sErr = targetSpeedMS - currentSpeed;
                float forwardAccel = Vector3.Dot(pAccel, pForward);

                if (useRandom)
                {
                    float sErrAbs = Mathf.Abs(sErr);
                    if (!s_isSpdSleeping)
                    {
                        if (sErrAbs < Plugin.Rand_Spd_Inner.Value && forwardAccel < Plugin.Rand_Acc_Inner.Value)
                        {
                            s_spdSleepUntil = Time.time + Random.Range(Plugin.Rand_Spd_SleepMin.Value,
                                Plugin.Rand_Spd_SleepMax.Value);
                            s_isSpdSleeping = true;
                        }
                    }
                    else if (sErrAbs > Plugin.Rand_Spd_Outer.Value || forwardAccel > Plugin.Rand_Acc_Outer.Value ||
                             Time.time > s_spdSleepUntil)
                    {
                        s_isSpdSleeping = false;
                    }
                }

                float minT = APData.AllowExtremeThrottle ? 0f : Plugin.ThrottleMinLimit.Value;
                float maxT = APData.AllowExtremeThrottle ? 1f : Plugin.ThrottleMaxLimit.Value;

                // Configure speed PID: setpoint = targetSpeed, measurement = currentSpeed
                ConfigurePID(PidSpd, ref s_cfgSpd, Plugin.PID_Spd, dt, minT, maxT);

                float pidOutput = s_isSpdSleeping
                    ? (float)PidSpd.ITerm
                    : (float)PidSpd.Update(targetSpeedMS, currentSpeed);

                float desiredThrottle = Mathf.Clamp(pidOutput, minT, maxT);
                PIDLogger.Log(PIDLogger.StepTarget.Spd, desiredThrottle, currentSpeed, targetSpeedMS);

                ThrottleOutput = s_currentAppliedThrottle;
            }

            // autopilot
            if (!APData.Enabled && !APData.GCASActive)
            {
                return;
            }

            {
                // keys
                if (!APData.ALSActive && !CursorManager.GetFlag(CursorFlags.Chat))
                {
                    const float fpsRef = 60f;
                    float aStep = Plugin.AltStep.Value * fpsRef * dt;
                    float bStep = Plugin.BigAltStep.Value * fpsRef * dt;
                    float cStep = Plugin.ClimbRateStep.Value * fpsRef * dt;
                    float rStep = Plugin.BankStep.Value * fpsRef * dt;
                    if (InputHelper.IsPressed(Plugin.UpRW) || Plugin.UpKey.Value.IsPressed())
                    {
                        APData.TargetAlt += aStep;
                    }

                    if (InputHelper.IsPressed(Plugin.DownRW) || Plugin.DownKey.Value.IsPressed())
                    {
                        APData.TargetAlt -= aStep;
                    }

                    if (InputHelper.IsPressed(Plugin.BigUpRW) || Plugin.BigUpKey.Value.IsPressed())
                    {
                        APData.TargetAlt += bStep;
                    }

                    if (InputHelper.IsPressed(Plugin.BigDownRW) || Plugin.BigDownKey.Value.IsPressed())
                    {
                        APData.TargetAlt = Mathf.Max(APData.TargetAlt - bStep, Plugin.MinAltitude.Value);
                    }

                    if (InputHelper.IsPressed(Plugin.ClimbRateUpRW) || Plugin.ClimbRateUpKey.Value.IsPressed())
                    {
                        APData.CurrentMaxClimbRate += cStep;
                    }

                    if (InputHelper.IsPressed(Plugin.ClimbRateDownRW) || Plugin.ClimbRateDownKey.Value.IsPressed())
                    {
                        APData.CurrentMaxClimbRate = Mathf.Max(0.5f, APData.CurrentMaxClimbRate - cStep);
                    }

                    if (APData.NavEnabled)
                    {
                        bool bankLeft = InputHelper.IsPressed(Plugin.BankLeftRW) ||
                                        Plugin.BankLeftKey.Value.IsPressed();
                        bool bankRight = InputHelper.IsPressed(Plugin.BankRightRW) ||
                                         Plugin.BankRightKey.Value.IsPressed();
                        if (bankLeft || bankRight)
                        {
                            if (APData.TargetRoll == -999f)
                            {
                                APData.TargetRoll = Plugin.DefaultCRLimit.Value;
                            }

                            if (bankLeft)
                            {
                                APData.TargetRoll -= rStep;
                            }

                            if (bankRight)
                            {
                                APData.TargetRoll += rStep;
                            }

                            APData.TargetRoll = Mathf.Clamp(APData.TargetRoll, 1f, 90f);
                        }
                    }

                    if (APData.TargetCourse >= 0f)
                    {
                        if (InputHelper.IsPressed(Plugin.BankLeftRW) || Plugin.BankLeftKey.Value.IsPressed())
                        {
                            APData.TargetCourse = Mathf.Repeat(APData.TargetCourse - rStep, 360f);
                        }

                        if (InputHelper.IsPressed(Plugin.BankRightRW) || Plugin.BankRightKey.Value.IsPressed())
                        {
                            APData.TargetCourse = Mathf.Repeat(APData.TargetCourse + rStep, 360f);
                        }
                    }
                    else
                    {
                        bool bankLeft = InputHelper.IsPressed(Plugin.BankLeftRW) ||
                                        Plugin.BankLeftKey.Value.IsPressed();
                        bool bankRight = InputHelper.IsPressed(Plugin.BankRightRW) ||
                                         Plugin.BankRightKey.Value.IsPressed();
                        if (bankLeft || bankRight)
                        {
                            if (APData.TargetRoll == -999f)
                            {
                                APData.TargetRoll = APData.CurrentRoll;
                            }

                            if (bankLeft)
                            {
                                APData.TargetRoll = Mathf.Repeat(APData.TargetRoll + rStep + 180f, 360f) - 180f;
                            }

                            if (bankRight)
                            {
                                APData.TargetRoll = Mathf.Repeat(APData.TargetRoll - rStep + 180f, 360f) - 180f;
                            }
                        }
                    }

                    if (InputHelper.IsDown(Plugin.ClearRW) || Plugin.ClearKey.Value.IsDown())
                    {
                        if (APData.NavEnabled)
                        {
                            float crlimit = Plugin.DefaultCRLimit.Value;
                            if (APData.TargetRoll != crlimit)
                            {
                                APData.TargetRoll = crlimit;
                            }
                            else
                            {
                                APData.NavEnabled = false;
                            }
                        }
                        else if (APData.TargetCourse != -1f)
                        {
                            APData.TargetCourse = -1f;
                        }
                        else if (APData.TargetRoll != 0f)
                        {
                            APData.TargetRoll = 0f;
                        }
                        else if (APData.TargetAlt != -1f)
                        {
                            APData.TargetAlt = -1f;
                        }
                        else
                        {
                            APData.TargetRoll = -999f;
                        }
                    }
                }

                // roll/course control
                bool rollTest = PIDLogger.IsTesting(PIDLogger.StepTarget.Roll) ||
                    PIDLogger.IsTesting(PIDLogger.StepTarget.Crs);
                bool rollAxisActive = APData.GCASActive ||
                    APData.TargetCourse >= 0f ||
                    APData.TargetRoll != -999f ||
                    rollTest;

                if (rollAxisActive)
                {
                    if ((pilotRoll || isWaitingToReengage) && !APData.GCASActive)
                    {
                        PidCrs.Reset();
                        PidRoll.ManualMode = true;
                        PidAngle.ManualValue = stickRoll;
                    }
                    else
                    {
                        PidRoll.ManualMode = false;
                        float activeTargetRoll = APData.TargetRoll;

                        if ((APData.TargetCourse >= 0f ||
                            PIDLogger.IsTesting(PIDLogger.StepTarget.Crs)) &&
                            APData.PlayerRB.velocity.sqrMagnitude > 1f &&
                            !APData.GCASActive)
                        {
                            Vector3 flatVel = Vector3.ProjectOnPlane(APData.PlayerRB.velocity, Vector3.up);
                            if (flatVel.sqrMagnitude > 1f)
                            {
                                float curCrs = Quaternion.LookRotation(flatVel).eulerAngles.y;
                                float baseTargetCrs = APData.TargetCourse >= 0f ? APData.TargetCourse : curCrs;

                                float targetCrs = PIDLogger.GetSetpoint(PIDLogger.StepTarget.Crs, baseTargetCrs, curCrs);
                                float cErr = Mathf.DeltaAngle(curCrs, targetCrs);

                                ConfigurePID(PidCrs, ref s_cfgCrs, Plugin.PID_Crs, dt, -90f, 90f);

                                float desiredTurnRate = (float)PidCrs.Update(cErr, 0);
                                PIDLogger.Log(PIDLogger.StepTarget.Crs, desiredTurnRate, curCrs, targetCrs);

                                const float gravity = 9.81f;
                                float velocity = Mathf.Max(APData.PlayerRB.velocity.magnitude, 1f);
                                float turnRateRad = desiredTurnRate * Mathf.Deg2Rad;
                                float bankReq = Mathf.Atan(velocity * turnRateRad / gravity) * Mathf.Rad2Deg;

                                if (Plugin.Conf_InvertCourseRoll.Value)
                                {
                                    bankReq = -bankReq;
                                }

                                float safeMaxG = Mathf.Max(Plugin.GCAS_MaxG.Value, 1.01f);
                                float gLimitBank = Mathf.Acos(1f / safeMaxG) * Mathf.Rad2Deg;

                                float userLimit = APData.TargetRoll != -999f && APData.TargetRoll != 0
                                    ? Mathf.Abs(APData.TargetRoll)
                                    : Plugin.DefaultCRLimit.Value;

                                float finalBankLimit = Mathf.Min(userLimit, gLimitBank);

                                activeTargetRoll = Mathf.Clamp(bankReq, -finalBankLimit, finalBankLimit);
                            }
                        }

                        activeTargetRoll = PIDLogger.GetSetpoint(PIDLogger.StepTarget.Roll, activeTargetRoll, APData.CurrentRoll);
                        float rollError = Mathf.DeltaAngle(APData.CurrentRoll, activeTargetRoll);
                        float rollRate = localAngVel.z * Mathf.Rad2Deg;

                        // Roll sleep
                        if (useRandom)
                        {
                            float rollErrAbs = Mathf.Abs(rollError);
                            float rollRateAbs = Mathf.Abs(rollRate);
                            if (!s_isRollSleeping)
                            {
                                if (rollErrAbs < Plugin.Rand_Roll_Inner.Value &&
                                    rollRateAbs < Plugin.Rand_RollRate_Inner.Value)
                                {
                                    s_rollSleepUntil = Time.time + Random.Range(Plugin.Rand_RollSleepMin.Value,
                                        Plugin.Rand_RollSleepMax.Value);
                                    s_isRollSleeping = true;
                                }
                            }
                            else if (rollErrAbs > Plugin.Rand_Roll_Outer.Value ||
                                     rollRateAbs > Plugin.Rand_RollRate_Outer.Value ||
                                     Time.time > s_rollSleepUntil)
                            {
                                s_isRollSleeping = false;
                            }
                        }

                        float rollOut = 0f;
                        if (useRandom && s_isRollSleeping)
                        {
                            PidRoll.SeedIntegral(Mathf.MoveTowards((float)PidRoll.ITerm, 0f, dt * 5f));
                        }
                        else
                        {
                            ConfigurePID(PidRoll, ref s_cfgRoll, Plugin.PID_Roll, dt, -1f, 1f);

                            rollOut = (float)PidRoll.Update(rollError, 0);
                            PIDLogger.Log(PIDLogger.StepTarget.Roll, rollOut, APData.CurrentRoll, activeTargetRoll);

                            if (Plugin.InvertRoll.Value)
                            {
                                rollOut = -rollOut;
                            }

                            if (useRandom)
                            {
                                rollOut += (Mathf.PerlinNoise(0f, noiseT) - 0.5f) * 2f *
                                           Plugin.RandomStrength.Value;
                            }
                        }

                        inputObj.roll = Mathf.Clamp(rollOut, -1f, 1f);
                    }
                }

                // pitch control
                bool pitchTest = PIDLogger.IsTesting(PIDLogger.StepTarget.Alt) ||
                                 PIDLogger.IsTesting(PIDLogger.StepTarget.VS) ||
                                 PIDLogger.IsTesting(PIDLogger.StepTarget.Angle) ||
                                 PIDLogger.IsTesting(PIDLogger.StepTarget.GCAS);

                bool pitchAxisActive = APData.GCASActive || APData.TargetAlt > 0f || pitchTest;

                if (!pitchAxisActive)
                {
                    return;
                }

                if ((pilotPitch || isWaitingToReengage) && !APData.GCASActive)
                {
                    PidAlt.Reset();
                    PidVS.Reset();
                    PidAngle.ManualMode = true;
                    PidAngle.ManualValue = stickPitch;
                    if (!Plugin.KeepSetAltStick.Value)
                    {
                        APData.TargetAlt = APData.CurrentAlt;
                    }
                }
                else
                {
                    PidAngle.ManualMode = false;
                    float pitchOut = 0f;

                    // gcas
                    if (APData.GCASActive || PIDLogger.IsTesting(PIDLogger.StepTarget.GCAS))
                    {
                        float rollAngle = Mathf.Abs(APData.CurrentRoll);
                        float targetG = rollAngle >= 90f ? 0f : Plugin.GCAS_MaxG.Value * s_overGFactor;

                        // GCAS PID: setpoint = targetG, measurement = currentG
                        ConfigurePID(PidGCAS, ref s_cfgGCAS, Plugin.PID_GCAS, dt, -1f, 1f);

                        float activeTargetG = PIDLogger.GetSetpoint(PIDLogger.StepTarget.GCAS, targetG, currentG);
                        pitchOut = (float)PidGCAS.Update(activeTargetG, currentG);
                        PIDLogger.Log(PIDLogger.StepTarget.GCAS, pitchOut, currentG, activeTargetG);
                    }
                    // Altitude hold (cascaded: Alt -> VS -> Angle -> Stick)
                    else if (APData.TargetAlt > 0f || pitchTest)
                    {
                        float currentVS = APData.PlayerRB.velocity.y;

                        // pitch sleep
                        if (useRandom)
                        {
                            float altError = APData.TargetAlt - APData.CurrentAlt;
                            float altErrAbs = Mathf.Abs(altError);
                            float vsAbs = Mathf.Abs(currentVS);

                            if (!s_isPitchSleeping)
                            {
                                if (altErrAbs < Plugin.Rand_Alt_Inner.Value &&
                                    vsAbs < Plugin.Rand_VS_Inner.Value)
                                {
                                    s_pitchSleepUntil = Time.time + Random.Range(
                                        Plugin.Rand_PitchSleepMin.Value,
                                        Plugin.Rand_PitchSleepMax.Value);
                                    s_isPitchSleeping = true;
                                }
                            }
                            else
                            {
                                if (altErrAbs > Plugin.Rand_Alt_Outer.Value ||
                                    vsAbs > Plugin.Rand_VS_Outer.Value ||
                                    Time.time > s_pitchSleepUntil)
                                {
                                    s_isPitchSleeping = false;
                                }
                            }
                        }

                        if (useRandom && s_isPitchSleeping)
                        {
                            PidAlt.SeedIntegral(Mathf.MoveTowards((float)PidAlt.ITerm, 0f, dt * 2f));
                            PidVS.SeedIntegral(Mathf.MoveTowards((float)PidVS.ITerm, 0f, dt * 10f));
                            PidAngle.SeedIntegral(Mathf.MoveTowards((float)PidAngle.ITerm, 0f, dt * 5f));
                        }
                        else
                        {
                            // Altitude > vertical speed
                            ConfigurePID(PidAlt, ref s_cfgAlt, Plugin.PID_Alt, dt,
                                -APData.CurrentMaxClimbRate, APData.CurrentMaxClimbRate);

                            float baseTargetAlt = APData.TargetAlt > 0f ? APData.TargetAlt : APData.CurrentAlt;
                            float altTarget = PIDLogger.GetSetpoint(PIDLogger.StepTarget.Alt, baseTargetAlt, APData.CurrentAlt);
                            float targetVS = (float)PidAlt.Update(altTarget, APData.CurrentAlt);
                            PIDLogger.Log(PIDLogger.StepTarget.Alt, targetVS, APData.CurrentAlt, altTarget);

                            float possibleAccel = Plugin.GCAS_MaxG.Value * 9.81f;
                            float altErr2 = Mathf.Abs(APData.TargetAlt - APData.CurrentAlt);
                            float maxSafeVS = Mathf.Sqrt(2f * possibleAccel * altErr2);
                            targetVS = Mathf.Clamp(targetVS, -maxSafeVS, maxSafeVS);
                            targetVS = Mathf.Clamp(targetVS,
                                -APData.CurrentMaxClimbRate, APData.CurrentMaxClimbRate);

                            // VS -> desired pitch angle
                            ConfigurePID(PidVS, ref s_cfgVS, Plugin.PID_VS, dt,
                                -Plugin.Conf_VS_MaxAngle.Value, Plugin.Conf_VS_MaxAngle.Value);

                            targetVS = PIDLogger.GetSetpoint(PIDLogger.StepTarget.VS, targetVS, currentVS);

                            float airspeed = Mathf.Max(APData.PlayerRB.velocity.magnitude, 1f);
                            float vsRatio = Mathf.Clamp(targetVS / airspeed, -1f, 1f);
                            PidVS.Feedforward = Mathf.Asin(vsRatio) * Mathf.Rad2Deg;

                            float targetPitchDeg = (float)PidVS.Update(targetVS, currentVS);
                            PIDLogger.Log(PIDLogger.StepTarget.VS, targetPitchDeg, currentVS, targetVS);

                            // Pitch angle -> stick
                            float currentPitch = Mathf.Asin(pForward.y) * Mathf.Rad2Deg;

                            ConfigurePID(PidAngle, ref s_cfgAngle, Plugin.PID_Angle, dt, -1f, 1f);

                            targetPitchDeg = PIDLogger.GetSetpoint(PIDLogger.StepTarget.Angle, targetPitchDeg, currentPitch);
                            pitchOut = (float)PidAngle.Update(targetPitchDeg, currentPitch);
                            PIDLogger.Log(PIDLogger.StepTarget.Angle, pitchOut, currentPitch, targetPitchDeg);

                            if (useRandom)
                            {
                                pitchOut += (Mathf.PerlinNoise(noiseT, 0f) - 0.5f) * 2f *
                                            Plugin.RandomStrength.Value;
                            }
                        }
                    }

                    if (Plugin.InvertPitch.Value)
                    {
                        pitchOut = -pitchOut;
                    }

                    pitchOut = Mathf.Clamp(pitchOut, -1f, 1f);
                    inputObj.pitch = pitchOut;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[ControlOverridePatch] Error: {ex}");
            Plugin.IsBroken = true;
        }
    }
}

[HarmonyPatch(typeof(PilotPlayerState), "PlayerThrottleAxis1Controls")]
internal static class ThrottleOverridePatch
{
    [UsedImplicitly]
    private static void Postfix(PilotPlayerState __instance)
    {
        if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
        {
            return;
        }

        if (__instance == null || APData.LocalAircraft == null || APData.PlayerRB == null)
        {
            return;
        }

        if (APData.TargetSpeed < 0 && !PIDLogger.IsTesting(PIDLogger.StepTarget.Spd))
        {
            return;
        }

        try
        {
            ControlInputs inputObj = __instance.controlInputs;
            if (inputObj == null)
            {
                return;
            }

            if (APData.TargetSpeed >= 0 || PIDLogger.IsTesting(PIDLogger.StepTarget.Spd))
            {
                inputObj.throttle = ControlOverridePatch.ThrottleOutput;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[ThrottleOverridePatch] Error: {ex}");
            Plugin.IsBroken = true;
        }
    }
}
