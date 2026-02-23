using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace NOAutopilot.ACLS
{
    /// <summary>
    /// Auto-disengage ACLS when the player's tailhook catches an arresting wire.
    ///
    /// We hook TailHook.FixedUpdate and watch the private 'hooked' flag transition
    /// from false -> true. This is the same moment the game prints "Caught Xth Wire"
    /// via AircraftActionsReport.ReportText.
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
                        // Disengage ACLS control
                        ACLSPilotPlayerStatePatch.enableControl = false;
                        // Optional: small UI feedback (reuse existing autopilot text if present)
                        if (ThrottleGaugeRefreshPatch.autopilotText != null)
                        {
                            var txt = ThrottleGaugeRefreshPatch.autopilotText.GetComponent<UnityEngine.UI.Text>();
                            if (txt != null)
                            {
                                txt.text = "ALS: OFF";
                                ((UnityEngine.UI.Graphic)txt).color = Color.white;
                            }
                        }
                        Plugin.Logger?.LogInfo("[ACLS] Auto-disengaged after wire catch.");
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
