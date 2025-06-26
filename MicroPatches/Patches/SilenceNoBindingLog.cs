using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker;
using Kingmaker.UI.InputSystems;

using MicroUtils.Transpiler;
using MicroUtils.Linq;

using Owlcat.Runtime.Core.Logging;
using System.Reflection;

namespace MicroPatches.Patches
{
    [MicroPatch("Silence 'no binding' warnings", Optional = true)]
    [HarmonyPatch]
    internal static class SilenceNoBindingLog
    {
        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> Methods() =>
        [
            AccessTools.Method(typeof(KeyboardAccess), nameof(KeyboardAccess.Bind)),
            AccessTools.Method(typeof(KeyboardAccess), nameof(KeyboardAccess.DoUnbind))
        ];

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Patch_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var match = instructions.FindInstructionsIndexed(new Func<CodeInstruction, bool>[]
            {
                ci => ci.opcode == OpCodes.Brfalse_S,
                _ => true,
                ci => ci.opcode == OpCodes.Ldstr && ci.operand as string == "Bind: no binding named {0}",
                _ => true,
                _ => true,
                _ => true,
                _ => true,
                _ => true,
                _ => true,
                _ => true,
                ci => ci.Calls(AccessTools.Method(typeof(LogChannel), nameof(LogChannel.Warning), [typeof(string), typeof(object[])]))
            });

            if (match.Count() != 11)
                throw new KeyNotFoundException("Unable to find patch location");

            foreach (var (_, i) in match.Skip(1))
            {
                i.opcode = OpCodes.Nop;
                i.operand = null;
            }

            return instructions;
        }
    }
}
