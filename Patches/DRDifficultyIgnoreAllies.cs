using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker.PubSubSystem.Core;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Damage;

using MicroUtils.Transpiler;

namespace MicroPatches.Patches
{
    [MicroPatch("Do not apply Damage Reduction modifiers from difficulty to friendly fire", Experimental = true)]
    [HarmonyPatch(typeof(RuleRollDamage), nameof(RuleRollDamage.ApplyDifficultyModifiers))]
    //[HarmonyPatchCategory(MicroPatch.Category.Experimental)]
    internal static class DRDifficultyIgnoreAllies
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var match = instructions.FindInstructionsIndexed(
            [
                ci => ci.Calls(AccessTools.PropertyGetter(typeof(IMechanicEntity), nameof(IMechanicEntity.IsPlayerFaction))),
                ci => ci.opcode == OpCodes.Brfalse_S || ci.opcode == OpCodes.Brfalse
            ]).ToArray();

            if (match.Length != 2)
            {
                throw new Exception("Could not find instructions to patch");
            }

            var iList = instructions.ToList();

            var branchTarget = (Label)match[1].instruction.operand;

            iList.InsertRange(match[1].index + 1,
            [
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RulebookTargetEvent), nameof(RulebookTargetEvent.Target))),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(IMechanicEntity), nameof(IMechanicEntity.IsPlayerFaction))),
                new CodeInstruction(OpCodes.Brtrue_S, branchTarget)
            ]);

            return iList;
        }
    }
}
