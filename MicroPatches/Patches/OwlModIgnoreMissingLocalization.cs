using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker.Modding;

namespace MicroPatches.Patches
{
    [MicroPatch("OwlMod fixes: Ignore missing Localization", Optional = true)]
    [HarmonyPatch(typeof(OwlcatModification), nameof(OwlcatModification.LoadLocalizationPack))]
    static class OwlModIgnoreMissingLocalization
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
        {
            var fileExists = ilGen.DefineLabel();

            Label? endLabel = null;

            foreach (var i in instructions)
            {
                if (i.opcode == OpCodes.Brtrue_S)
                    endLabel = (Label)i.operand;

                yield return i;

                if (endLabel is { } notExists && i.Calls(AccessTools.Method(typeof(Path), nameof(Path.Combine), [typeof(string), typeof(string), typeof(string)])))
                {
                    yield return new(OpCodes.Dup);
                    yield return new(OpCodes.Call, AccessTools.Method(typeof(File), nameof(File.Exists)));
                    yield return new(OpCodes.Brtrue_S, fileExists);
                    yield return new(OpCodes.Pop);
                    yield return new(OpCodes.Br_S, notExists);
                    yield return new(OpCodes.Nop) { labels = [fileExists] };
                }
            }
        }
    }
}
