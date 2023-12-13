using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Components;

namespace MicroPatches.Patches
{
    [MicroPatch("Fix inverted CasterNamedProperty/TargetNamedProperty calculation")]
    [HarmonyPatch(typeof(ContextValue), nameof(ContextValue.Calculate))]
    [HarmonyPatchCategory("Experimental")]
    static class NamedCasterTargetSwap
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var switchInstruction = instructions.First(ci => ci.opcode == OpCodes.Switch);

            if (switchInstruction.operand is not Label[] jumpTable)
                throw new Exception();

            var casterNamed = (int)ContextValueType.CasterNamedProperty;
            var targetNamed = (int)ContextValueType.TargetNamedProperty;

            var tTarget = jumpTable[casterNamed];
            var cTarget = jumpTable[targetNamed];

            jumpTable[casterNamed] = cTarget;
            jumpTable[targetNamed] = tTarget;

            //var casterTargetLabel = jumpTable[casterNamed];
            //var targetTargetLabel = jumpTable[targetNamed];

            //var iArray = instructions.SkipWhile(ci => !ci.labels.Contains(targetTargetLabel)).ToArray();

            return instructions;
        }
    }
}
