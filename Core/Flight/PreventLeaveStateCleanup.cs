extern alias JetBrains;

using System;

using HarmonyLib;

using JetBrains.Annotations;

namespace NOAutopilot.Core.Flight;

[HarmonyPatch(typeof(PilotPlayerState), "LeaveState")]
internal static class PreventLeaveStateCleanup
{
    [UsedImplicitly]
    private static bool Prefix(PilotPlayerState __instance)
    {
        if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
        {
            return true;
        }

        try
        {
            if (APData.ALSActive)
            {
                __instance.gloc?.ResetGLOC();
                return false;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[PreventLeaveStateCleanup] Error: {ex}");
            Plugin.IsBroken = true;
        }

        return true;
    }
}
