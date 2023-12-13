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
    [MicroPatch("Silence 'no binding' warnings")]
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
                ci => ci.Calls(AccessTools.PropertyGetter(typeof(PFLog), nameof(PFLog.Default))),
                ci => ci.opcode == OpCodes.Ldstr && ci.operand as string == "Bind: no binding named {0}"
            });

            if (match.Count() != 3)
                throw new KeyNotFoundException("Unable to find patch location");

            var (index, i) = match.First();

            var iList = instructions.ToList();

            iList[index] = new CodeInstruction(OpCodes.Br, i.operand);
            iList.Insert(index, new CodeInstruction(OpCodes.Pop));

            return iList;
        }
    }
}
