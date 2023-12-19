using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker;
using Kingmaker.DLC;

using Owlcat.Runtime.Core.Logging;

namespace MicroPatches.Patches
{
    [MicroPatch("Quieter missing DLC warnings")]
    [HarmonyPatch]
    [HarmonyPatchCategory(Main.ExperimentalCategory)]
    internal static class ShorterMissingDlcMessage
    {
        [HarmonyTargetMethod]
        static MethodBase TargetMethod() => AccessTools.PropertyGetter(typeof(BlueprintDlc), nameof(BlueprintDlc.IsAvailable));

        static void LogMessage(LogChannel channel, string messageFormat) => channel.Log(messageFormat);

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var ci in instructions)
            {
                if (ci.Calls(AccessTools.Method(typeof(LogChannel), nameof(LogChannel.Error), [typeof(string)])))
                {
                    yield return CodeInstruction.Call((LogChannel channel, string messageFormat) => LogMessage(channel, messageFormat));
                    continue;
                }

                yield return ci;
            }
        }

    }
}
