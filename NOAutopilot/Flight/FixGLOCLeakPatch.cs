extern alias JetBrains;

using System;

using HarmonyLib;

using JetBrains.Annotations;

using Object = UnityEngine.Object;

namespace NOAutopilot;

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
                Object.DestroyImmediate(existingGloc);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"[FixGLOCLeakPatch] Error: {ex}");
            Plugin.IsBroken = true;
        }
    }
}
