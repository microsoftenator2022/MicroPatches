using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using Kingmaker.Code.UI.MVVM.VM.Space;

using UniRx;

using Warhammer.SpaceCombat.Blueprints;

namespace MicroPatches.Patches;

[MicroPatch("Fix Ship Rear Armor calculation", Experimental = true, Optional = true)]
[HarmonyPatch(typeof(ShipVM), nameof(ShipVM.UpdateStats))]
internal static class ShipRearArmorFix
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var valueSetter = typeof(ReactiveProperty<>).MakeGenericType(typeof(float)).GetProperty("Value").SetMethod;

        return new CodeMatcher(instructions)
            .MatchEndForward(
                new CodeMatch(ci => ci.LoadsField(AccessTools.Field(
                    typeof(BlueprintItemArmorPlating), nameof(BlueprintItemArmorPlating.ArmourAft)))),
                new CodeMatch(_ => true),
                new CodeMatch(_ => true),
                new CodeMatch(ci => ci.opcode == OpCodes.Callvirt && (MethodInfo)ci.operand == valueSetter))
            .SetAndAdvance(OpCodes.Nop, null)
            .Insert([new(OpCodes.Pop, null)])
            .Insert([new(OpCodes.Pop, null)])
            .InstructionEnumeration();
    }
}
