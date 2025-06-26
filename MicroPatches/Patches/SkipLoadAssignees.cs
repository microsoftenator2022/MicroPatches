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
    //[MicroPatch("Skip LoadAssigneesAsync", Experimental = true)]
    //[HarmonyPatch]
    //internal static class SkipLoadAssignees
    //{
    //    [HarmonyTargetMethod]
    //    static MethodBase TargetMethod() =>
    //        typeof(ReportingUtils)
    //            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
    //            .Single(mi =>
    //            {
    //                var ps = mi.GetParameters();

    //                if (ps.Length == 1 && ps[0].ParameterType == typeof(Task))
    //                {
    //                    #if DEBUG
    //                    Main.PatchLog(nameof(SkipLoadAssignees), $"Found method {mi}");
    //                    #endif
    //                    return true;
    //                }

    //                return false;
    //            });

    //    [HarmonyTranspiler]
    //    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> _)
    //    {
    //        yield return new CodeInstruction(OpCodes.Ret);
    //    }
    //}
}
