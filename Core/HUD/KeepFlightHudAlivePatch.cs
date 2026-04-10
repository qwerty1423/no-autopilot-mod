extern alias JetBrains;

using System;

using HarmonyLib;

using JetBrains.Annotations;

namespace NOAutopilot.Core.HUD;

[HarmonyPatch(typeof(FlightHud), "EnableCanvas")]
internal static class KeepFlightHudAlivePatch
{
    [UsedImplicitly]
    private static bool Prefix(bool enable)
    {
        if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
        {
            return true;
        }

        try
        {
            if (!enable && APData.ALSActive)
            {
                CameraStateManager camManager = SceneSingleton<CameraStateManager>.i;
                if (camManager != null && camManager.currentState == camManager.cockpitState)
                {
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[KeepFlightHudAlive] Error: {ex}");
            Plugin.IsBroken = true;
        }

        return true;
    }
}
