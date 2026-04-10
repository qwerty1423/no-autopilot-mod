extern alias JetBrains;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using NOAutopilot.Core;

using UnityEngine;

namespace NOAutopilot.Core.Map;

[HarmonyPatch(typeof(DynamicMap), "MapControls")]
internal static class UnlockMapPatch
{
    private static readonly MethodInfo ClampMethod =
        AccessTools.Method(typeof(Mathf), "Clamp", [typeof(float), typeof(float), typeof(float)]);

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher matcher = new(instructions);

        if (Plugin.UnlockMapPan.Value)
        {
            matcher.MatchForward(false,
                new CodeMatch(OpCodes.Ldc_R4),
                new CodeMatch(i => i.opcode == OpCodes.Call && i.operand is MethodInfo { Name: "ClampPos" })
            );

            if (matcher.IsValid)
            {
                matcher.SetOperandAndAdvance(float.MaxValue);
            }
            else
            {
                Plugin.Logger.LogError("Could not find patch location for map pan.");
            }
        }

        if (!Plugin.UnlockMapZoom.Value)
        {
            return matcher.InstructionEnumeration();
        }

        matcher.Start();
        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Ldc_R4),
            new CodeMatch(OpCodes.Ldc_R4),
            new CodeMatch(OpCodes.Call, ClampMethod)
        );

        if (matcher.IsValid)
        {
            matcher.SetOperandAndAdvance(0.001f);
            matcher.SetOperandAndAdvance(1000f);
        }
        else
        {
            Plugin.Logger.LogError("Could not find patch location for map zoom.");
        }

        return matcher.InstructionEnumeration();
    }
}
