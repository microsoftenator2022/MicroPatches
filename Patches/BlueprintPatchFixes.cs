using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Code.GameCore.Blueprints.BlueprintPatcher;

using HarmonyLib;

using Kingmaker.Blueprints;

using MicroUtils.Linq;
using MicroUtils.Transpiler;

using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

using Owlcat.Runtime.Core;


namespace MicroPatches.Patches.BlueprintPatchFixes
{
    class BlueprintPatchFixesGroup : MicroPatchGroup
    {
        public override string DisplayName => "OwlMod fixes: Blueprint patch fixes";
        public override bool Optional => true;
        public override bool Experimental => true;
    }

    [MicroPatchGroup(typeof(BlueprintPatchFixesGroup))]
    [HarmonyPatch]
    static class ListPatchElementTypeFix
    {
        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> TargetMethods() =>
            [
                AccessTools.Method(typeof(BlueprintPatchOperation), nameof(BlueprintPatchOperation.CheckTypeIsArrayOrListOfBlueprintReferences)),
                    AccessTools.Method(typeof(BlueprintSimpleArrayPatchOperation), nameof(BlueprintSimpleArrayPatchOperation.GetArrayElementType))
            ];

        static Type? GetListElementType(Type type)
        {
            var elementType = type.GetInterfaces()
                .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IList<object>).GetGenericTypeDefinition())
                ?.GetGenericArguments()[0];

            return elementType;
        }

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
        {
            var notNull = ilGen.DefineLabel();

            foreach (var i in instructions)
            {
                if (i.Calls(AccessTools.Method(typeof(Type), nameof(Type.GetElementType))))
                    yield return CodeInstruction.Call((Type type) => GetListElementType(type));
                else
                    yield return i;
            }
        }
    }

    static class ListPatchOperationFixes
    {
        static IList MaybeToList(IList array, BlueprintSimpleArrayPatchOperation patchOp)
        {
            if (!patchOp.CheckTypeIsList(patchOp.fieldType))
                return array;

            var list = (IList)typeof(Enumerable)
                    .GetMethod(nameof(Enumerable.ToList), BindingFlags.Public | BindingFlags.Static)
                    .MakeGenericMethod([array.GetType().GetElementType()])
                    .Invoke(null, [array]);

            return list;
        }

        [MicroPatchGroup(typeof(BlueprintPatchFixesGroup))]
        [HarmonyPatch(typeof(BlueprintSimpleArrayPatchOperation))]
        static class ListOperationFixes
        {
            [HarmonyTargetMethods]
            static IEnumerable<MethodBase> TargetMethods() =>
                [
                    AccessTools.Method(typeof(BlueprintSimpleArrayPatchOperation), nameof(BlueprintSimpleArrayPatchOperation.InsertElement)),
                        AccessTools.Method(typeof(BlueprintSimpleArrayPatchOperation), nameof(BlueprintSimpleArrayPatchOperation.ReplaceElement))
                ];

            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                foreach (var i in instructions)
                {
                    yield return i;

                    if (i.Calls(AccessTools.Method(typeof(Array), nameof(Array.CreateInstance), [typeof(Type), typeof(Int32)])))
                    {
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return CodeInstruction.Call((IList array, BlueprintSimpleArrayPatchOperation patchOp) => MaybeToList(array, patchOp));
                    }
                }
            }
        }

        [MicroPatchGroup(typeof(BlueprintPatchFixesGroup))]
        [HarmonyPatch(typeof(BlueprintSimpleArrayPatchOperation), nameof(BlueprintSimpleArrayPatchOperation.InsertElement))]
        static class InsertOperationFixes
        {
            static object? ToObject(string value, Type type)
            {
                var serializer = JsonSerializer.Create(BlueprintPatcher.Settings);

                return new JValue(value).ToObject(type, serializer);
            }

            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var match =
                    instructions.FindInstructionsIndexed(
                    [
                        ci => ci.opcode == OpCodes.Ldarg_0,
                        ci => ci.LoadsField(AccessTools.Field(typeof(BlueprintSimpleArrayPatchOperation), nameof(BlueprintSimpleArrayPatchOperation.Value))),
                        ci => ci.opcode == OpCodes.Castclass,
                        ci => ci.opcode == OpCodes.Ldloc_0,
                        ci => ci.Calls(AccessTools.Method(typeof(JToken), nameof(JToken.ToObject), [typeof(Type)]))
                    ]).ToArray();

                if (match.Length != 5)
                {
                    Main.PatchError(nameof(InsertOperationFixes), "Target instructions not found");
                    return instructions;
                }

                match[2].instruction.operand = typeof(string);
                match[4].instruction.operand = AccessTools.Method(typeof(InsertOperationFixes), nameof(InsertOperationFixes.ToObject));
                
                return instructions;
            }
        }

        [MicroPatchGroup(typeof(BlueprintPatchFixesGroup))]
        [HarmonyPatch(typeof(BlueprintSimpleArrayPatchOperation), nameof(BlueprintSimpleArrayPatchOperation.RemoveElement))]
        static class ListPatchRemoveItemFix
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                yield return new CodeInstruction(OpCodes.Ret);
            }

            [HarmonyPostfix]
            static void RemoveElement(BlueprintSimpleArrayPatchOperation __instance)
            {
                if (string.IsNullOrEmpty(__instance.TargetValueGuid))
                {
                    throw new Exception("Null target value guid given in patch operation");
                }

                Type arrayElementType = __instance.GetArrayElementType(__instance.field);

                IList list = (IList)__instance.field.GetValue(__instance.fieldHolder);

                //Main.Logger.Log($"fieldHolder is {(__instance.fieldHolder is null ? "null" : "not null")}");
                //Main.Logger.Log($"fieldHolder type: {__instance.fieldHolder?.GetType()}");
                //Main.Logger.Log($"source list has {list.Count} items");

                //foreach (var obj in list)
                //{
                //    Main.Logger.Log($"  {obj.GetType()}");
                //    if (obj is BlueprintReferenceBase brb)
                //        Main.Logger.Log($"    {brb.Guid}");
                //}

                IList list2 = MaybeToList(Array.CreateInstance(arrayElementType, list.Count - 1), __instance);

                var index =
                    list.Cast<object>()
                        .FindIndex(obj =>
                            obj is BlueprintReferenceBase brb &&
                            brb.Guid == __instance.TargetValueGuid);

                if (index < 0)
                    throw new Exception($"Target value guid {__instance.TargetValueGuid} not found");

                for (var i = 0; i < index; i++)
                {
                    list2[i] = list[i];
                }
                for (var i = index + 1; i < list.Count; i++)
                {
                    list2[i - 1] = list[i];
                }

                __instance.field.SetValue(__instance.fieldHolder, list2);
            }
        }
    }

    [MicroPatchGroup(typeof(BlueprintPatchFixesGroup))]
    [HarmonyPatch(typeof(BlueprintSimpleArrayPatchOperation), nameof(BlueprintSimpleArrayPatchOperation.CalculateReplaceIndex))]
    static class CalculateReplaceIndexFix
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            yield return new CodeInstruction(OpCodes.Ldc_I4_M1);
            yield return new CodeInstruction(OpCodes.Ret);
        }

        [HarmonyPostfix]
        static int CalculateReplaceIndex(int _, IList array, BlueprintSimpleArrayPatchOperation __instance)
        {
            bool predicate(object obj) =>
                (obj is BlueprintReferenceBase brb && brb.Guid == __instance.TargetValueGuid) ||
                (obj == __instance.TargetValue);

            var targetElements = array.Cast<object>().Indexed().Where(i => predicate(i.item));

            return __instance.OperationType switch
            {
                BlueprintPatchOperationType.InsertAtBeginning => 0,
                BlueprintPatchOperationType.InsertLast => array.Count,
                BlueprintPatchOperationType.InsertAfterElement => targetElements.Any() ? targetElements.Last().index : -1,
                BlueprintPatchOperationType.InsertBeforeElement => targetElements.Any() ? targetElements.First().index : -1,
                _ => throw new Exception($"Replace index cannot be calculated for operation type {__instance.OperationType}"),
            };
        }
    }

    
}
