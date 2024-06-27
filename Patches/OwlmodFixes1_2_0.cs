using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

using Code.GameCore.Blueprints.BlueprintPatcher;

using HarmonyLib;

using Kingmaker.Blueprints;

using MicroUtils.Linq;
using MicroUtils.Transpiler;

namespace MicroPatches.Patches;

[MicroPatch("1.2 OwlMod fixes", Optional = false)]
[HarmonyPatch]
internal static class OwlmodFixes1_2_0
{
    static bool IsSimple(object value)
    {
        var type = value.GetType();

        return type.IsPrimitive || type.IsEnum || (value is string s && !s.StartsWith("!bp_"));
    }

    [HarmonyPatch(typeof(BlueprintPatchObjectComparator), nameof(BlueprintPatchObjectComparator.ObjectsAreEqual))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> ReplaceIsSimple(IEnumerable<CodeInstruction> instructions)
    {
        var isSimpleCallIndices =
            instructions.Indexed()
                .Where(pair => pair.item.Calls(AccessTools.Method(typeof(BlueprintPatchObjectComparator), nameof(BlueprintPatchObjectComparator.IsSimple))))
                .Select(pair => pair.index);

        var iList = instructions.ToList();

        foreach (var index in isSimpleCallIndices)
        {
            iList[index - 1].opcode = OpCodes.Nop;
            iList[index - 1].operand = null;

            iList[index] = CodeInstruction.Call((object obj) => IsSimple(obj));
        }

        return iList;
    }

    static object TryReference(object obj)
    {
        if (obj is BlueprintReferenceBase)
        {
            //Main.PatchLog(nameof(OwlmodFixes1_2_0), $"{obj} is {nameof(BlueprintReferenceBase)}");
            return obj;
        }

        if (obj is string s)
        {
            //Main.PatchLog(nameof(OwlmodFixes1_2_0), $"{obj} is string. Create new reference");
            if (s.StartsWith("!bp_"))
                s = s.Remove(0, 4);

            return new BlueprintReferenceBase() { guid = s };
        }

        return obj;
    }

    [HarmonyPatch(typeof(BlueprintPatchObjectComparator), nameof(BlueprintPatchObjectComparator.ObjectsAreEqual))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> ReferenceCasts(IEnumerable<CodeInstruction> instructions)
    {
        var match = instructions.FindInstructionsIndexed(
        [
            ci => ci.opcode == OpCodes.Ldarg_0,
            ci => ci.opcode == OpCodes.Isinst,
            ci => ci.opcode == OpCodes.Stloc_0
        ]);

        var iList = instructions.ToList();

        iList.InsertRange(match.First().index,
        [
            new(OpCodes.Ldarg_0),
            CodeInstruction.Call((object obj) => TryReference(obj)),
            new(OpCodes.Starg_S, 0),
            new(OpCodes.Ldarg_1),
            CodeInstruction.Call((object obj) => TryReference(obj)),
            new(OpCodes.Starg_S, 1),
        ]);

        return iList;
    }
}
