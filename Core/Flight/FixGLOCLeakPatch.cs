extern alias JetBrains;

using System;

using HarmonyLib;

using JetBrains.Annotations;

using Object = UnityEngine.Object;

namespace NOAutopilot.Core.Flight;

/// <summary>
/// Fixes a GLOC component leak in <see cref="PilotPlayerState.EnterState"/> from autoland.
/// <para>
/// The base game adds a new GLOC component every time the player
/// enters the pilot state without checking for an existing instance, and
/// <see cref="PilotPlayerState.LeaveState"/> never destroys the component.
/// This causes GLOC components to stack on the pilot GameObject each time
/// autoland is triggered without this patch.
/// </para>
/// </summary>
[HarmonyPatch(typeof(PilotPlayerState), "EnterState")]
internal static class FixGLOCLeakPatch
{
    [UsedImplicitly]
    private static void Prefix(Pilot pilot)
    {
        if (Plugin.IsBroken && Plugin.UnpatchIfBroken.Value)
        {
            return;
        }

        try
        {
            GLOC existingGloc = pilot.gameObject.GetComponent<GLOC>();
            if (existingGloc != null)
            {
                Object.Destroy(existingGloc);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[FixGLOCLeakPatch] Error: {ex}");
            Plugin.IsBroken = true;
        }
    }
}
