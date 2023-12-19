using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker.GameModes;
using Kingmaker.UI.InputSystems;
using Kingmaker.UI.InputSystems.Enums;

using Owlcat.Runtime.Core;
using Owlcat.Runtime.Core.Logging;

using UnityEngine;

namespace MicroPatches.Patches
{
    [MicroPatch("Silence key bind conflict warnings")]
    [HarmonyPatch(typeof(KeyboardAccess), nameof(KeyboardAccess.RegisterBinding),
        [
            typeof(string),
            typeof(KeyCode),
            typeof(GameModeType),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(TriggerType),
            typeof(KeyboardAccess.ModificationSide),
            typeof(bool)
        ])]
    [HarmonyPatchCategory(Main.Category.Experimental)]
    internal static class SilenceKeyBindingConflict
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var ci in instructions)
            {
                if (ci.Calls(AccessTools.Method(typeof(LogChannel), nameof(LogChannel.Warning), [typeof(string), typeof(object[])])))
                {
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return new CodeInstruction(OpCodes.Pop);
                    continue;
                }

                yield return ci;
            }
        }
    }
}
