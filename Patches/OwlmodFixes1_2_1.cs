using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

using Code.GameCore.Blueprints.BlueprintPatcher;

using HarmonyLib;

using Kingmaker.Blueprints;
using Kingmaker.Modding;

using Newtonsoft.Json.Linq;

namespace MicroPatches.Patches;

[MicroPatch("1.2.1 OwlMod fixes", Optional = false)]
[HarmonyPatch]
internal static class OwlmodFixes1_2_1
{
    static string? GetGuidFromReferenceOrString(object obj)
    {
        if (obj is string s)
            return s;

        if (obj is BlueprintReferenceBase brb)
            return $"!bp_{brb.Guid}";

        return null;
    }

    [HarmonyPatch(typeof(BlueprintPatchObjectComparator), nameof(BlueprintPatchObjectComparator.ObjectsAreEqual))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> ReferenceCasts(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var i in instructions)
        {
            if (i.opcode == OpCodes.Castclass && ((Type)i.operand) == typeof(string))
            {
                yield return CodeInstruction.Call((object o) => GetGuidFromReferenceOrString(o))
                    .WithBlocks(i.blocks)
                    .WithLabels(i.labels);
                continue;
            }

            yield return i;
        }
    }

    [HarmonyPatch(typeof(BlueprintPatchOperation), nameof(BlueprintPatchOperation.Apply))]
    [HarmonyPostfix]
    static void FixBlueprintPatchOperation(BlueprintPatchOperation __instance)
    {
        if (__instance is BlueprintSimpleArrayPatchOperation ap)
        {
            if (ap.Value == null)
                return;

            var elementType = ap.GetArrayElementType(ap.field);

            if (ap.Value.GetType() != elementType && ap.Value is JObject jobj)
            {
                ap.Value = jobj.ToObject(elementType);
            }

            return;
        }

        if (__instance is BlueprintFieldOverrideOperation fp)
        {
            if (fp.FieldValue == null)
                return;
            
            if (fp.FieldValue.GetType() != fp.fieldType && fp.FieldValue is JObject jobj)
            {
                fp.FieldValue = jobj.ToObject(fp.fieldType);
            }

            return;
        }
    }
}
