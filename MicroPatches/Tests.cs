using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;

using HarmonyLib;

using Kingmaker.UI.Selection;

using MicroPatches.Patches;

using MicroUtils.Linq;

using Owlcat.Runtime.Core.Logging;

namespace MicroPatches
{
    internal partial class Main
    {
#if DEBUG
        //[MicroPatch("Hidden Failure Test Patch", Description = "Test\nTest\nTest\nTest\nTest\nTest\nTest", Hidden = true, Optional = true)]
        //[HarmonyPatch(typeof(Main), nameof(Main.Load))]
        //static class TestHiddenFailPatch
        //{
        //    [HarmonyTranspiler]
        //    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        //        throw new Exception($"{nameof(TestHiddenFailPatch)}");
        //}

        //[HarmonyPatch]
        //static class FreeMercs
        //{
        //    [HarmonyTargetMethod]
        //    static MethodBase TargetMethod() =>
        //        typeof(CreateCustomCompanion)
        //        .GetNestedTypes(AccessTools.all)
        //        .First()
        //        .GetMethods(AccessTools.all)
        //        .First(mi => mi.GetParameters()[0].ParameterType == typeof(BaseUnitEntity));

        //    [HarmonyTranspiler]
        //    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        //    {
        //        bool patched = false;

        //        foreach (var i in instructions)
        //        {
        //            if (!patched)
        //            {
        //                if (i.opcode == OpCodes.Brtrue_S)
        //                {
        //                    patched = true;

        //                    yield return new CodeInstruction(OpCodes.Pop);

        //                    i.opcode = OpCodes.Br_S;
        //                }
        //            }
                    
        //            yield return i;
        //        }
        //    }
        //}

        //[HarmonyPatch]
        //static class RemoveRespecPFPenalty
        //{
        //    [HarmonyPatch(typeof(RespecVM), MethodType.Constructor, typeof(List<BaseUnitEntity>), typeof(Action<BaseUnitEntity>), typeof(Action))]
        //    [HarmonyTranspiler]
        //    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        //    {
        //        foreach (var i in instructions)
        //        {
        //            if (i.Calls(AccessTools.Method(typeof(SystemMapSpaceResourcesVM), nameof(SystemMapSpaceResourcesVM.SetAdditionalProfitFactor))))
        //            {
        //                i.opcode = OpCodes.Pop;
        //                i.operand = null;
                        
        //                yield return i;
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

            Logger!.Log(sb.ToString());
#endif
        }

        void PostPatchTests()
        {
#if DEBUG

#endif
        }

#if DEBUG
        [HarmonyPatch(typeof(KingmakerInputModule), nameof(KingmakerInputModule.CheckEventSystem), methodType: MethodType.Enumerator)]
        static class SilenceAwaitXMessages
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var matches = instructions
                    .Pairwise()
                    .Where(pair =>
                        pair.Item1.opcode == OpCodes.Ldstr &&
                        pair.Item2.Calls(AccessTools.Method(typeof(LogChannel), nameof(LogChannel.Log), [typeof(string)])));

                foreach (var (ldstr, callLog) in matches)
                {
                    Main.Logger!.Log($"Silencing '{ldstr.operand}'");

                    ldstr.opcode = OpCodes.Nop;
                    ldstr.operand = null;

                    callLog.opcode = OpCodes.Pop;
                    callLog.operand = null;
                }

                return instructions;
            }
        }
#endif
    }
}
