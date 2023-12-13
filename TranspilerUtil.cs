using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using MicroUtils.Linq;

namespace MicroUtils.Transpiler
{
    public static class TranspilerUtil
    {
        public static IEnumerable<(int index, CodeInstruction instruction)> FindInstructionsIndexed(
            this IEnumerable<CodeInstruction> instructions,
            IEnumerable<Func<CodeInstruction, bool>> matchFuncs)
        {
            var matched = instructions
                .Indexed()
                .FindSequence(matchFuncs
                    .Select<Func<CodeInstruction, bool>, Func<(int index, CodeInstruction item), bool>>(f =>
                        i => f(i.item)));

            if (!matched.Any()) return Enumerable.Empty<(int, CodeInstruction)>();

            return matched;
        }

        public static IEnumerable<CodeInstruction> ReplaceInstructions(
            IEnumerable<CodeInstruction> source,
            IEnumerable<CodeInstruction> match,
            IEnumerable<CodeInstruction> replaceWith)
        {
            var matchIndexed = match.Select<CodeInstruction, Func<(int, CodeInstruction), bool>>(m =>
                ((int, CodeInstruction instruction) ici) =>
                    m.opcode == ici.instruction.opcode &&
                    (m.operand is null || m.operand == ici.instruction.operand));

            (int index, CodeInstruction i)[] matchedInstructions = source.Indexed().FindSequence(matchIndexed).ToArray();

            if (!matchedInstructions.Any())
            {
                return Enumerable.Empty<CodeInstruction>();
            }

            var index = matchedInstructions.First().index;

            var iList = source.ToList();

            iList.RemoveRange(index, matchedInstructions.Length);
            iList.InsertRange(index, replaceWith);


            return iList;
        }
    }
}
