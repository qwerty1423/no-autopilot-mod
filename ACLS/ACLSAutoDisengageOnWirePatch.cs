using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace NOAutopilot.ACLS
{
    /// <summary>
    /// Auto-disengage ACLS when the player's tailhook catches an arresting wire.
    /// </summary>
    [HarmonyPatch(typeof(TailHook), "FixedUpdate")]
    internal class ACLSAutoDisengageOnWirePatch
    {
        private static readonly ConditionalWeakTable<TailHook, HookState> _state = [];

        private class HookState
        {
            public bool WasHooked;
        }

        private static void Prefix(TailHook __instance)
        {
            if (__instance == null) return;
            var st = _state.GetOrCreateValue(__instance);
            st.WasHooked = Traverse.Create(__instance).Field("hooked").GetValue<bool>();
        }

        private static void Postfix(TailHook __instance)
        {
            if (__instance == null) return;

            bool isHooked;
            try
            {
                isHooked = Traverse.Create(__instance).Field("hooked").GetValue<bool>();
            }
            catch
            {
                return;
            }

            var st = _state.GetOrCreateValue(__instance);
            if (!st.WasHooked && isHooked)
            {
                // Transition to hooked this frame
                try
                {
                    var aircraft = Traverse.Create(__instance).Field("aircraft").GetValue<Aircraft>();
                    if (aircraft != null && SceneSingleton<CombatHUD>.i != null && aircraft == SceneSingleton<CombatHUD>.i.aircraft)
                    {
                        APData.ACLSActive = false;
                        APData.ACLSStatusText = "ALS: OFF";
                        APData.ACLSStatusColor = Color.white;
                    }
                }
                catch
                {
                    // swallow any reflection/UI errors
                }
            }
        }
    }
}
