using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace NOAutopilot.ACLS;

/// <summary>
/// Auto-disengage ACLS when the player's tailhook catches an arresting wire.
/// </summary>
[HarmonyPatch(typeof(TailHook), "FixedUpdate")]
internal class ACLSAutoDisengageOnWirePatch
{
    private static readonly ConditionalWeakTable<TailHook, HookState> _state = new();

    private class HookState
    {
        public bool WasHooked;
    }

    private static void Prefix(TailHook __instance)
    {
        if (__instance == null) return;
        var st = _state.GetOrCreateValue(__instance);
        st.WasHooked = __instance.hooked;
    }

    private static void Postfix(TailHook __instance)
    {
        if (__instance == null) return;
        bool isHooked = __instance.hooked;

        var st = _state.GetOrCreateValue(__instance);
        if (!st.WasHooked && isHooked)
        {
            var aircraft = __instance.aircraft;

            if (aircraft != null && SceneSingleton<CombatHUD>.i != null && aircraft == SceneSingleton<CombatHUD>.i.aircraft)
            {
                APData.ACLSActive = false;
                APData.ACLSStatusText = "ALS: OFF";
                APData.ACLSStatusColor = Color.white;
            }
        }
    }
}
