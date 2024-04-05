using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

using Kingmaker.Blueprints.JsonSystem;

using Kingmaker.Modding;

using Kingmaker;
using MicroUtils.Transpiler;

namespace MicroPatches.Patches
{
    //internal class OwlcatModificationBlueprintAssetReferencesFix : MicroPatchGroup
    //{
    //    public override string DisplayName => "OwlMod fixes: asset references";
    //    public override bool Optional => false;
    //    public override bool Experimental => false;

    //    [MicroPatchGroup(typeof(OwlcatModificationBlueprintAssetReferencesFix))]
    //    [HarmonyPatch]
    //    static class OwlcatModificationLoadPatch
    //    {
    //        [HarmonyPatch(typeof(OwlcatModification), nameof(OwlcatModification.ApplyInternal))]
    //        [HarmonyTranspiler]
    //        static IEnumerable<CodeInstruction> OwlcatModification_ApplyInternal_Transpiler(
    //            IEnumerable<CodeInstruction> instructions)
    //        {
    //            var match = instructions.FindInstructionsIndexed(new Func<CodeInstruction, bool>[]
    //            {
    //                ci => ci.opcode == OpCodes.Ldarg_0,
    //                ci => ci.Calls(AccessTools.Method(typeof(OwlcatModification), nameof(OwlcatModification.LoadBundles))),
    //                ci => ci.opcode == OpCodes.Ldarg_0,
    //                ci => ci.Calls(AccessTools.Method(typeof(OwlcatModification), nameof(OwlcatModification.LoadBlueprints))),
    //            }).ToArray();

    //            if (match.Length != 4)
    //                throw new Exception("Could not find patch target instructions");

    //            var iList = instructions.ToList();

    //            iList.RemoveRange(match.First().index, match.Length);

    //            return iList;
    //        }
    //    }

    //    [MicroPatchGroup(typeof(OwlcatModificationBlueprintAssetReferencesFix))]
    //    [HarmonyPatch]
    //    static class GameStarter_StartGame_Patch
    //    {
    //        static void LoadOwlModBundlesAndBlueprints()
    //        {
    //            Main.PatchLog(nameof(OwlcatModificationBlueprintAssetReferencesFix), "Loading mod resources");

    //            foreach (var mod in OwlcatModificationsManager.Instance.AppliedModifications)
    //            {
    //                Main.PatchLog(nameof(OwlcatModificationBlueprintAssetReferencesFix), $"Loading resources for mod '{mod.Manifest.UniqueName}'");
    //                try
    //                {
    //                    mod.LoadBundles();
    //                }
    //                catch (Exception ex)
    //                {
    //                    mod.Logger.Exception(ex);
    //                }

    //                try
    //                {
    //                    mod.LoadBlueprints();
    //                }
    //                catch (Exception ex)
    //                {
    //                    mod.Logger.Exception(ex);
    //                }
    //            }
    //        }

    //        [HarmonyTargetMethod]
    //        static MethodBase TargetMethod() =>
    //            AccessTools.Method(
    //                typeof(GameStarter).GetNestedTypes(AccessTools.all)
    //                    .Single(t => t.Name.Contains(nameof(GameStarter.StartGameCoroutine))),
    //                "MoveNext");

    //        [HarmonyTranspiler]
    //        static IEnumerable<CodeInstruction> GameStarter_StartGameCoroutine_Transpiler(IEnumerable<CodeInstruction> instructions)
    //        {
    //            bool addedCall = false;

    //            foreach (var i in instructions)
    //            {
    //                yield return i;

    //                if (!addedCall && i.Calls(AccessTools.Method(typeof(StartGameLoader), nameof(StartGameLoader.LoadDirectReferencesList))))
    //                {
    //                    var ni = CodeInstruction.Call(() => LoadOwlModBundlesAndBlueprints());

    //                    ni.blocks = i.blocks.ToList();

    //                    yield return ni;

    //                    addedCall = true;
    //                }
    //            }

    //            if (!addedCall)
    //                throw new Exception("Could not find patch target instruction");
    //        }
    //    }
    //}
}
