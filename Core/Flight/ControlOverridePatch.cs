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
    private static readonly PIDController PidAlt = new();
    private static readonly PIDController PidVS = new();
    private static readonly PIDController PidPitch = new();
    private static readonly PIDController PidRoll = new();
    private static readonly PIDController PidRollRate = new();
    private static readonly PIDController PidCrs = new();
    private static readonly PIDController PidCrsRate = new();
    private static readonly PIDController PidGCAS = new();
    private static readonly PIDController PidSpd = new();
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
    private static float s_jammerNextFireTime;
    private static float s_jammerNextReleaseTime;
    private static bool s_isJammerHoldingTrigger;

    private static float s_disengageTimer;

    public static float ThrottleOutput;

    private static void ConfigurePID(
    PIDController pid, ConfigEntry<PIDTuning> tuning,
        float dt, float minOutput, float maxOutput)
    {
        PIDConfig.Apply(pid, tuning.Value,
            Mathf.Max(dt, 0.0001f), minOutput, maxOutput);
    }

    public static void Reset()
    {
        PidAlt.Reset();
        PidVS.Reset();
        PidPitch.Reset();
        PidRoll.Reset();
        PidRollRate.Reset();
        PidCrs.Reset();
        PidCrsRate.Reset();
        PidGCAS.Reset();
        PidSpd.Reset();

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

        s_jammerNextFireTime = 0f;
        s_jammerNextReleaseTime = 0f;
        s_isJammerHoldingTrigger = false;

        s_disengageTimer = 0f;
    }

    private static void ResetIntegrators()
    {
        PidAlt.Reset();
        PidVS.Reset();
        PidPitch.Reset();
        PidRoll.Reset();
        PidRollRate.Reset();
        PidCrs.Reset();
        PidCrsRate.Reset();
        PidGCAS.Reset();

        if (APData.TargetSpeed < 0)
        {
            PidSpd.Reset();
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
            float rollRate = localAngVel.z * Mathf.Rad2Deg;
            Vector3 flatVel = Vector3.ProjectOnPlane(APData.PlayerRB.velocity, Vector3.up);

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
                    ResetIntegrators();
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

                bool pilotOverride = Mathf.Abs(stickPitch) > Plugin.GcasDeadzone.Value ||
                    Mathf.Abs(stickRoll) > Plugin.GcasDeadzone.Value || gearDown;

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

                    float gAccel = Plugin.GcasMaxG.Value * 9.81f;
                    float turnRadius = speed * speed / gAccel;

                    float reactionTime = Plugin.GcasAutoBuffer.Value + (Time.deltaTime * 2.0f) + timeToRollUpright;
                    float reactionDist = speed * reactionTime;
                    float warnDist = speed * Plugin.GcasWarnBuffer.Value;

                    s_overGFactor = 1.0f;

                    if (Time.time >= s_gcasNextScan)
                    {
                        s_gcasNextScan = Time.time + 0.02f;

                        s_dangerImminent = false;
                        s_warningZone = false;

                        APData.GCASConverge = 0f;

                        Vector3 castStart = APData.PlayerRB.position + (velocity.normalized * 5f);
                        float scanRange = (turnRadius * 1.5f) + warnDist + 500f;

                        if (Physics.SphereCast(castStart, Plugin.GcasScanRadius.Value, velocity.normalized,
                                out RaycastHit hit, scanRange, 8256) && hit.transform.root != APData.PlayerTransform.root)
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
                                        Mathf.Max(s_overGFactor, gRequired / Plugin.GcasMaxG.Value);
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

                    if (descentRate > 0f)
                    {
                        float diveAngle = Vector3.Angle(velocity, Vector3.ProjectOnPlane(velocity, Vector3.up));
                        float vertBuffer = descentRate * reactionTime;
                        float availablePullAlt = APData.CurrentAlt - vertBuffer - Plugin.GcasMinAlt.Value;
                        float pullUpLoss = turnRadius * (1f - Mathf.Cos(diveAngle * Mathf.Deg2Rad));

                        if (availablePullAlt < pullUpLoss)
                        {
                            s_dangerImminent = true;

                            float availableRadius =
                                availablePullAlt / (1f - Mathf.Cos(diveAngle * Mathf.Deg2Rad));
                            availableRadius = Mathf.Max(availableRadius, 1f);

                            float gReqFloor = speed * speed / (availableRadius * 9.81f);

                            s_overGFactor = Mathf.Max(s_overGFactor, gReqFloor / Plugin.GcasMaxG.Value);
                        }
                        else if (APData.CurrentAlt <
                                 pullUpLoss + vertBuffer + (descentRate * Plugin.GcasWarnBuffer.Value))
                        {
                            s_warningZone = true;
                            float triggerAlt = pullUpLoss + vertBuffer;
                            float warnRange = descentRate * Plugin.GcasWarnBuffer.Value;
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
                            PidAlt.Reset();
                            PidVS.Reset();
                            PidPitch.Reset();
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

                ConfigurePID(PidSpd, Plugin.ConfPidSpd, dt, minT, maxT);

                float pidOutput = s_isSpdSleeping
                    ? (float)PidSpd.ITerm
                    : (float)PidSpd.Update(targetSpeedMS, currentSpeed);

                float desiredThrottle = Mathf.Clamp(pidOutput, minT, maxT);
                PIDLogger.Log(PIDLogger.StepTarget.Spd, desiredThrottle, currentSpeed, targetSpeedMS);

                ThrottleOutput = desiredThrottle;
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
                        // Outer loop: track current angle as target
                        PidRoll.Update(APData.CurrentRoll, APData.CurrentRoll,
                            0, 0, 0, Mode.Track);
                        // Inner loop: track pilot's stick input  
                        PidRollRate.Update(rollRate, rollRate,
                            0, stickRoll, stickRoll, Mode.Track);
                    }
                    else
                    {
                        float activeTargetRoll = APData.TargetRoll;

                        if ((APData.TargetCourse >= 0f ||
                            PIDLogger.IsTesting(PIDLogger.StepTarget.Crs)) &&
                            APData.PlayerRB.velocity.sqrMagnitude > 1f &&
                            !APData.GCASActive && flatVel.sqrMagnitude > 1f)
                        {
                            float curCrs = Quaternion.LookRotation(flatVel).eulerAngles.y;
                            float baseTargetCrs = APData.TargetCourse >= 0f ? APData.TargetCourse : curCrs;

                            float targetCrs = PIDLogger.GetSetpoint(PIDLogger.StepTarget.Crs, baseTargetCrs, curCrs);
                            float cErr = Mathf.DeltaAngle(curCrs, targetCrs);
                            float unwrappedTargetCrs = curCrs + cErr;
                            ConfigurePID(PidCrs, Plugin.ConfPidCrs, dt, -90f, 90f);
                            float desiredTurnRate = (float)PidCrs.Update(unwrappedTargetCrs, curCrs);

                            PIDLogger.Log(PIDLogger.StepTarget.Crs, desiredTurnRate, curCrs, targetCrs);

                            const float gravity = 9.81f;
                            float velocity = Mathf.Max(APData.PlayerRB.velocity.magnitude, 1f);
                            float turnRateRad = desiredTurnRate * Mathf.Deg2Rad;
                            float bankReqFf = Mathf.Atan(velocity * turnRateRad / gravity) * Mathf.Rad2Deg;

                            float safeMaxG = Mathf.Max(Plugin.GcasMaxG.Value, 1.01f);
                            float gLimitBank = Mathf.Acos(1f / safeMaxG) * Mathf.Rad2Deg;

                            float userLimit = APData.TargetRoll != -999f && APData.TargetRoll != 0
                                ? Mathf.Abs(APData.TargetRoll)
                                : Plugin.DefaultCRLimit.Value;

                            float finalBankLimit = Mathf.Min(userLimit, gLimitBank);

                            float curTurnRate = APData.PlayerRB.angularVelocity.y * Mathf.Rad2Deg;
                            float targetTurnRate = PIDLogger.GetSetpoint(PIDLogger.StepTarget.CrsRate, desiredTurnRate, curTurnRate);
                            ConfigurePID(PidCrsRate, Plugin.ConfPidCrsRate, dt, -finalBankLimit, finalBankLimit);
                            float bankReq = (float)PidCrsRate.Update(targetTurnRate, curTurnRate, bankReqFf, 0, 0, Mode.Auto);
                            PIDLogger.Log(PIDLogger.StepTarget.CrsRate, bankReq, curTurnRate, targetTurnRate);

                            if (Plugin.Conf_InvertCourseRoll.Value)
                            {
                                bankReq = -bankReq;
                            }

                            activeTargetRoll = bankReq;
                        }

                        activeTargetRoll = PIDLogger.GetSetpoint(PIDLogger.StepTarget.Roll, activeTargetRoll, APData.CurrentRoll);
                        float rollError = Mathf.DeltaAngle(APData.CurrentRoll, activeTargetRoll);
                        float unwrappedTargetRoll = APData.CurrentRoll + rollError;

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
                            // PidRoll.SeedIntegral(Mathf.MoveTowards((float)PidRoll.ITerm, 0f, dt * 5f));
                        }
                        else
                        {
                            // Roll > Roll rate
                            ConfigurePID(PidRoll, Plugin.ConfPidRoll, dt,
                                -Plugin.MaxRollRate.Value, Plugin.MaxRollRate.Value);

                            float rollRateOut = (float)PidRoll.Update(unwrappedTargetRoll, APData.CurrentRoll);
                            PIDLogger.Log(PIDLogger.StepTarget.Roll, rollRateOut,
                                APData.CurrentRoll, activeTargetRoll);

                            rollRateOut = PIDLogger.GetSetpoint(PIDLogger.StepTarget.RollRate,
                                rollRateOut, rollRate);
                            ConfigurePID(PidRollRate, Plugin.ConfPidRollRate, dt, -1f, 1f);

                            rollOut = (float)PidRollRate.Update(rollRateOut, rollRate);
                            PIDLogger.Log(PIDLogger.StepTarget.RollRate, rollOut,
                                rollRate, rollRateOut);

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
                                 PIDLogger.IsTesting(PIDLogger.StepTarget.Pitch) ||
                                 PIDLogger.IsTesting(PIDLogger.StepTarget.GCAS);

                bool pitchAxisActive = APData.GCASActive || APData.TargetAlt > 0f || pitchTest;

                if (!pitchAxisActive)
                {
                    return;
                }

                if ((pilotPitch || isWaitingToReengage) && !APData.GCASActive)
                {
                    float currentVS = APData.PlayerRB.velocity.y;
                    float currentPitch = Mathf.Asin(pForward.y) * Mathf.Rad2Deg;

                    PidAlt.Update(APData.CurrentAlt, APData.CurrentAlt,
                        0, 0, 0, Mode.Track);
                    PidVS.Update(currentVS, currentVS,
                        0, 0, 0, Mode.Track);
                    PidPitch.Update(currentPitch, currentPitch,
                        0, stickPitch, stickPitch, Mode.Track);

                    if (!Plugin.KeepSetAltStick.Value)
                    {
                        APData.TargetAlt = APData.CurrentAlt;
                    }
                }
                else
                {
                    float pitchOut = 0f;

                    // gcas
                    if (APData.GCASActive || PIDLogger.IsTesting(PIDLogger.StepTarget.GCAS))
                    {
                        float rollAngle = Mathf.Abs(APData.CurrentRoll);
                        float targetG = rollAngle >= 90f ? currentG : Plugin.GcasMaxG.Value * s_overGFactor;

                        ConfigurePID(PidGCAS, Plugin.ConfPidGcas, dt, -1f, 1f);

                        float activeTargetG = PIDLogger.GetSetpoint(PIDLogger.StepTarget.GCAS, targetG, currentG);
                        pitchOut = (float)PidGCAS.Update(activeTargetG, currentG);
                        PIDLogger.Log(PIDLogger.StepTarget.GCAS, pitchOut, currentG, activeTargetG);
                    }
                    // Altitude hold
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
                            // PidAlt.SeedIntegral(Mathf.MoveTowards((float)PidAlt.ITerm, 0f, dt * 2f));
                            // PidVS.SeedIntegral(Mathf.MoveTowards((float)PidVS.ITerm, 0f, dt * 10f));
                            // PidAngle.SeedIntegral(Mathf.MoveTowards((float)PidAngle.ITerm, 0f, dt * 5f));
                        }
                        else
                        {
                            // Altitude > vertical speed
                            ConfigurePID(PidAlt, Plugin.ConfPidAlt, dt,
                                -APData.CurrentMaxClimbRate, APData.CurrentMaxClimbRate);

                            float baseTargetAlt = APData.TargetAlt > 0f ? APData.TargetAlt : APData.CurrentAlt;
                            float altTarget = PIDLogger.GetSetpoint(PIDLogger.StepTarget.Alt, baseTargetAlt, APData.CurrentAlt);
                            float targetVS = (float)PidAlt.Update(altTarget, APData.CurrentAlt);
                            PIDLogger.Log(PIDLogger.StepTarget.Alt, targetVS, APData.CurrentAlt, altTarget);

                            float possibleAccel = Plugin.GcasMaxG.Value * 9.81f;
                            float altErr2 = Mathf.Abs(APData.TargetAlt - APData.CurrentAlt);
                            float maxSafeVS = Mathf.Sqrt(2f * possibleAccel * altErr2);
                            targetVS = Mathf.Clamp(targetVS, -maxSafeVS, maxSafeVS);
                            targetVS = Mathf.Clamp(targetVS,
                                -APData.CurrentMaxClimbRate, APData.CurrentMaxClimbRate);

                            // VS -> desired pitch angle
                            ConfigurePID(PidVS, Plugin.ConfPidVs, dt,
                                -Plugin.Conf_VS_MaxAngle.Value, Plugin.Conf_VS_MaxAngle.Value);

                            targetVS = PIDLogger.GetSetpoint(PIDLogger.StepTarget.VS, targetVS, currentVS);

                            float airspeed = Mathf.Max(flatVel.magnitude, 1f);
                            float vsRatio = Mathf.Clamp(targetVS / airspeed, -1f, 1f);
                            float vsFf = Mathf.Asin(vsRatio) * Mathf.Rad2Deg;

                            float targetPitchDeg = (float)PidVS.Update(targetVS, currentVS, vsFf, 0, 0, Mode.Auto);
                            PIDLogger.Log(PIDLogger.StepTarget.VS, targetPitchDeg, currentVS, targetVS);

                            // Pitch angle -> stick
                            float currentPitch = Mathf.Asin(pForward.y) * Mathf.Rad2Deg;

                            ConfigurePID(PidPitch, Plugin.ConfPidPitch, dt, -1f, 1f);

                            targetPitchDeg = PIDLogger.GetSetpoint(PIDLogger.StepTarget.Pitch, targetPitchDeg, currentPitch);
                            pitchOut = (float)PidPitch.Update(targetPitchDeg, currentPitch);
                            PIDLogger.Log(PIDLogger.StepTarget.Pitch, pitchOut, currentPitch, targetPitchDeg);

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
