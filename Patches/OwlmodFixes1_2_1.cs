using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

using Code.GameCore.Blueprints.BlueprintPatcher;

using HarmonyLib;

using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints.Loot;
using Kingmaker.Blueprints.Root;
using Kingmaker.Globalmap.Blueprints.Colonization;
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

    [HarmonyPatch(typeof(OwlcatModification), nameof(OwlcatModification.GetBlueprintPatch))]
    [HarmonyPrefix]
    static void GetBlueprintPatch_Prefix(object resource, out bool __state)
    {
        __state = false;

        if (resource is SimpleBlueprint blueprint && Json.BlueprintBeingRead is null)
        {
            Json.BlueprintBeingRead = new(blueprint);
            __state = true;
        }
    }

    [HarmonyPatch(typeof(OwlcatModification), nameof(OwlcatModification.GetBlueprintPatch))]
    [HarmonyPostfix]
    static void GetBlueprintPatch_Postfix(bool __state)
    {
        if (__state)
            Json.BlueprintBeingRead = null;
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

    [HarmonyPatch(typeof(BlueprintPatchObjectComparator), nameof(BlueprintPatchObjectComparator.ObjectsAreEqual))]
    [HarmonyPrefix]
    static bool CompareSoulMarkToFact(object? protoItem, object? targetItem, ref bool __result)
    {
        if (protoItem is not SoulMarkToFact && targetItem is not SoulMarkToFact)
            return true;

        var proto = protoItem as SoulMarkToFact;
        var target = targetItem as SoulMarkToFact;

        Main.PatchLog(nameof(CompareSoulMarkToFact), 
            $"Compare soulmarks ({proto?.m_SoulMarkBlueprint}, {proto?.SoulMarkDirection}) to ({target?.m_SoulMarkBlueprint}, {target?.SoulMarkDirection})");

        if (proto is null || target is null)
        {
            __result = false;
            return false;
        }

        __result = proto.m_SoulMarkBlueprint.Equals(target.m_SoulMarkBlueprint) && (proto.SoulMarkDirection == target.SoulMarkDirection);

        return false;
    }

    [HarmonyPatch(typeof(BlueprintPatchObjectComparator), nameof(BlueprintPatchObjectComparator.ObjectsAreEqual))]
    [HarmonyPrefix]
    static bool CompareLootEntry(object? protoItem, object? targetItem, ref bool __result)
    {
        var proto = protoItem as LootEntry;
        var target = targetItem as LootEntry;

        if (proto is null && target is null)
            return true;

        if (proto is null || target is null)
        {
            __result = false;
            return false;
        }

        __result = proto.IsDuplicate(target);

        return false;
    }

    [HarmonyPatch(typeof(BlueprintPatchObjectComparator), nameof(BlueprintPatchObjectComparator.ObjectsAreEqual))]
    [HarmonyPrefix]
    static bool CompareResourceData(object? protoItem, object? targetItem, ref bool __result)
    {
        var proto = protoItem as ResourceData;
        var target = targetItem as ResourceData;

        if (proto is null && target is null)
            return true;

        if (proto is null || target is null)
        {
            __result = false;
            return false;
        }

        __result = proto.Resource == target.Resource && proto.Count == target.Count;

        return false;
    }

    static object? MaybeFixJObject(object? obj, Type expectedType)
    {
        if (obj is not JObject jObject)
            return obj;

        return jObject.ToObject(expectedType, Json.Serializer);
    }

    [HarmonyPatch(typeof(BlueprintPatchOperation), nameof(BlueprintPatchOperation.Apply))]
    [HarmonyPostfix]
    static void FixBlueprintPatchOperation(BlueprintPatchOperation __instance)
    {
        if (__instance is BlueprintSimpleArrayPatchOperation ap)
        {
            var elementType = ap.GetArrayElementType(ap.field);

            ap.Value = MaybeFixJObject(ap.Value, elementType);
            ap.TargetValue =  MaybeFixJObject(ap.TargetValue, elementType);

            return;
        }

        if (__instance is BlueprintFieldOverrideOperation fp)
        {
            fp.FieldValue = MaybeFixJObject(fp.FieldValue, fp.FieldType);

            return;
        }
    }
}
