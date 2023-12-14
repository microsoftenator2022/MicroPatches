using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker.Utility;

using MicroUtils.Transpiler;

namespace MicroPatches.Patches
{
    [MicroPatch("Skip LoadAssigneesAsync")]
    [HarmonyPatch]
    [HarmonyPatchCategory(Main.ExperimentalCategory)]
    internal static class SkipLoadAssignees
    {
        [HarmonyTargetMethod]
        static MethodBase TargetMethod() =>
            typeof(ReportingUtils)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .First(mi =>
                {
                    var ps = mi.GetParameters();

#if DEBUG
                    if (
#else
                    return
#endif
                        ps.Length == 1 && ps[0].ParameterType == typeof(Task)
#if DEBUG
                    )
                    {
                        Main.PatchLog(nameof(SkipLoadAssignees), $"Found method {mi}");
                        return true;
                    }

                    return false
#endif
                    ;
                });

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> _)
        {
            yield return new CodeInstruction(OpCodes.Ret);
        }
    }
}
