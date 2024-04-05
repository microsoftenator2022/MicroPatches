using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Code.GameCore.Blueprints.BlueprintPatcher;

using HarmonyLib;

using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Items.Weapons;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints.JsonSystem.BinaryFormat;
using Kingmaker.Blueprints.JsonSystem.Converters;
using Kingmaker.Blueprints.JsonSystem.Helpers;
using Kingmaker.Blueprints.Root;
using Kingmaker.Code.UI.MVVM.VM.Retrain;
using Kingmaker.Code.UI.MVVM.VM.SystemMap;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Entities.Base;
using Kingmaker.Localization;
using Kingmaker.Modding;
using Kingmaker.UI.Canvases;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Blueprints;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.UnitLogic.Progression.Features;
using Kingmaker.View;

using MicroPatches.Patches;
using MicroPatches.UGUI;

using MicroUtils.Transpiler;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Owlcat.Runtime.Core;
using Owlcat.Runtime.Core.Logging;

using RogueTrader.SharedTypes;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace MicroPatches
{
    internal partial class Main
    {
#if DEBUG

        [MicroPatch("Hidden Failure Test Patch", Description = "Test\nTest\nTest\nTest\nTest\nTest\nTest", Hidden = true, Optional = true)]
        [HarmonyPatch(typeof(Main), nameof(Main.Load))]
        static class TestHiddenFailPatch
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
                throw new Exception($"{nameof(TestHiddenFailPatch)}");
        }

        //[HarmonyPatch]
        static class FreeMercs
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() =>
                typeof(CreateCustomCompanion)
                .GetNestedTypes(AccessTools.all)
                .First()
                .GetMethods(AccessTools.all)
                .First(mi => mi.GetParameters()[0].ParameterType == typeof(BaseUnitEntity));

            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                bool patched = false;

                foreach (var i in instructions)
                {
                    if (!patched)
                    {
                        if (i.opcode == OpCodes.Brtrue_S)
                        {
                            patched = true;

                            yield return new CodeInstruction(OpCodes.Pop);

                            i.opcode = OpCodes.Br_S;
                        }
                    }
                    
                    yield return i;
                }
            }
        }

        //[HarmonyPatch]
        static class RemoveRespecPFPenalty
        {
            [HarmonyPatch(typeof(RespecVM), MethodType.Constructor, typeof(List<BaseUnitEntity>), typeof(Action<BaseUnitEntity>), typeof(Action))]
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                foreach (var i in instructions)
                {
                    if (i.Calls(AccessTools.Method(typeof(SystemMapSpaceResourcesVM), nameof(SystemMapSpaceResourcesVM.SetAdditionalProfitFactor))))
                    {
                        i.opcode = OpCodes.Pop;
                        i.operand = null;
                        
                        yield return i;
                    }

                    yield return i;
                }
            }
        }

        //static IEnumerable<FieldInfo> GetAllFields(Type type)
        //{
        //    if (type is null)
        //        yield break;

        //    if (type != typeof(object))
        //    {
        //        foreach (var f in GetAllFields(type.BaseType))
        //            yield return f;
                        
        //        foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic))
        //            yield return f;
        //    }
        //}

        //static FieldInfo? GetField(Type type, string fieldName) => GetAllFields(type).FirstOrDefault(f => f.Name == fieldName);

        //[HarmonyPatch]
        //static class BlueprintPatchFieldPatch
        //{
        //    [HarmonyPatch(typeof(BlueprintPatchOperation), nameof(BlueprintPatchOperation.Apply))]
        //    [HarmonyTranspiler]
        //    static IEnumerable<CodeInstruction> BlueprintPatchOperation_Apply_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
        //    {
        //        var iList = instructions.ToList();

        //        var notNullLabel = ilGen.DefineLabel();

        //        var loopEnd = iList.FindIndex(i => i.opcode == OpCodes.Blt) + 1;
        //        var loopEndLabel = ilGen.DefineLabel();
        //        iList[loopEnd].labels.Add(loopEndLabel);

        //        for (var i = 0; i < iList.Count; i++)
        //        {
        //            // Get fields in parent types too
        //            if (iList[i].Calls(AccessTools.Method(typeof(Type), nameof(Type.GetField), [typeof(string), typeof(BindingFlags)])))
        //            {
        //                iList[i].opcode = OpCodes.Call;
        //                iList[i].operand = AccessTools.Method(typeof(Main), nameof(GetField));

        //                iList.Insert(i, new(OpCodes.Pop));
        //                i++;
        //            }

        //            // Check if field is null
        //            if (iList[i].Calls(AccessTools.Method(typeof(FieldInfo), nameof(FieldInfo.GetValue))))
        //            {
        //                i++;
        //                var toInsert = new CodeInstruction[]
        //                {
        //                    new(OpCodes.Dup),
        //                    new(OpCodes.Brtrue_S, notNullLabel),
        //                    new(OpCodes.Pop),
        //                    new(OpCodes.Br, loopEndLabel),
        //                    new(OpCodes.Nop) { labels = [notNullLabel] }
        //                };

        //                iList.InsertRange(i, toInsert);
        //                i += toInsert.Length;
        //            }
        //        }

        //        return iList;
        //    }

        //    static void TryDeserialize(BlueprintFieldOverrideOperation bpfoo)
        //    {
        //        if (bpfoo.FieldValue is JObject jobj)
        //        {
        //            bpfoo.FieldValue = jobj.ToString();
        //        }

        //        if (bpfoo.FieldValue is string value &&
        //            !bpfoo.fieldType.IsAssignableFrom(value.GetType()))
        //        {
        //            bpfoo.FieldValue = JsonConvert.DeserializeObject(value, bpfoo.fieldType, BlueprintPatcher.Settings);
        //        }
        //    }

        //    [HarmonyPatch(typeof(BlueprintFieldOverrideOperation), nameof(BlueprintFieldOverrideOperation.Apply))]
        //    [HarmonyTranspiler]
        //    static IEnumerable<CodeInstruction> BlueprintFieldOverrideOperation_Apply_Transpiler(IEnumerable<CodeInstruction> instructions)
        //    {
        //        foreach (var i in instructions)
        //        {
        //            if (i.opcode == OpCodes.Ldtoken)
        //            {
        //                yield return new(OpCodes.Ldarg_0);
        //                yield return new(OpCodes.Call, AccessTools.Method(typeof(BlueprintPatchFieldPatch), nameof(TryDeserialize)));
        //            }

        //            yield return i;
        //        }
        //    }
        //}
#endif

        void PrePatchTests()
        {
#if DEBUG
            //var fieldName = nameof(BlueprintFeature.m_Icon).Split(['.'], StringSplitOptions.None);
            //Logger.Log($"FieldName: {fieldName.First()}");
            //var field = typeof(BlueprintFeature).GetField(fieldName.First(), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            //Logger.Log($"Field: {field?.ToString() ?? "NULL"}");

            var sb = new StringBuilder();

            sb.Append("PatchGroups:");
            foreach (var g in Main.PatchGroups.Select(g => g.group))
            {
                sb.AppendLine();
                sb.AppendLine($" Group '{g.DisplayName}'");

                sb.AppendLine($"  Optional {g.IsOptional()}");
                sb.AppendLine($"  Hidden {g.Hidden}");
                sb.AppendLine($"  Experimental {g.IsExperimental()}");
                sb.AppendLine($"  Enabled: {g.IsEnabled()}");
                sb.AppendLine($"  Applied: {g.IsApplied()}");

                sb.Append("  Patches:");
                foreach (var p in g.GetPatches())
                {
                    sb.AppendLine();
                    sb.Append($"   Patch '{p.PatchClass.Name}");
                }
            }

            Logger.Log(sb.ToString());
#endif
        }

        void PostPatchTests()
        {
#if DEBUG

#endif
        }
    }
}