using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

using HarmonyLib;

using Kingmaker.Blueprints.JsonSystem.Converters;
using Kingmaker.BundlesLoading;
using Kingmaker.Modding;

using RogueTrader.SharedTypes;

using UnityEngine;

namespace MicroPatches.Patches;

[MicroPatch("Load OwlMod BlueprintDirectReferences dependencies", Optional = false)]
[HarmonyPatch]
public static class OwlModDirectReferenceBundleDependenciesFix
{
    [ThreadStatic]
    public static OwlcatModification? ModificationBeingApplied;

    [HarmonyPatch(typeof(OwlcatModification), nameof(OwlcatModification.ApplyContent))]
    [HarmonyPrefix]
    static void OwlcatModification_ApplyInternal_Prefix(OwlcatModification __instance) => ModificationBeingApplied = __instance;

    [HarmonyPatch(typeof(OwlcatModification), nameof(OwlcatModification.ApplyContent))]
    [HarmonyFinalizer]
    static void OwlcatModification_ApplyInternal_Finalizer() => ModificationBeingApplied = null;

    [HarmonyPatch(typeof(OwlcatModification), nameof(OwlcatModification.TryLoadBundle))]
    [HarmonyPostfix]
    static AssetBundle? OwlcatModification_TryLoadBundle_Postfix(AssetBundle? __result, OwlcatModification __instance, string bundleName)
    {
        if (__result != null)
            return __result;

        if (__instance.Bundles.Contains(bundleName))
        {
            __instance.Logger.Log($"Loading {bundleName} from {__instance.Manifest.UniqueName}/Bundles");
            return __instance.LoadBundle(bundleName);
        }

        return null;
    }

    [HarmonyPatch(typeof(OwlcatModificationsManager), nameof(OwlcatModificationsManager.TryLoadBundle))]
    [HarmonyPrefix]
    static bool OwlcatModificationsManager_TryLoadBundle_Prefix(string bundleName, ref AssetBundle? __result)
    {
        __result = ModificationBeingApplied?.TryLoadBundle(bundleName);

        if (__result != null)
            ModificationBeingApplied!.Logger.Log($"Loaded {bundleName} while applying modification");

        return __result == null;
    }

    [HarmonyPatch(typeof(OwlcatModificationsManager), nameof(OwlcatModificationsManager.GetDependenciesForBundle))]
    [HarmonyPrefix]
    static bool OwlcatModificationsManager_GetDependenciesForBundle_Prefix(string bundleName, ref DependencyData? __result)
    {
        __result = ModificationBeingApplied?.GetDependenciesForBundle(bundleName);

        if (__result != null)
            ModificationBeingApplied!.Logger.Log($"Got dependencies [{
                string.Join(", ", __result.BundleToDependencies[bundleName])}] for {bundleName} while applying modification");

        return __result == null;
    }

    [HarmonyPatch(typeof(OwlcatModificationsManager), nameof(OwlcatModificationsManager.GetBundleNameForAsset))]
    [HarmonyPrefix]
    static bool OwlcatModificationsManager_GetBundleNameForAsset_Prefix(string guid, ref string? __result)
    {
        __result = ModificationBeingApplied?.GetBundleNameForAsset(guid);

        if (__result != null)
            ModificationBeingApplied!.Logger.Log($"Got bundle name {__result} for asset {guid} while applying modification");

        return __result == null;
    }

    [HarmonyPatch(typeof(OwlcatModification), nameof(OwlcatModification.LoadBundle))]
    [HarmonyPostfix]
    static void OwlcatModification_LoadBundle_Postfix(string bundleName, OwlcatModification __instance, AssetBundle __result)
    {
        __instance.Logger.Log($"Load bundle {bundleName}. Is null? {__result == null}");
    }

    [HarmonyPatch(typeof(OwlcatModificationsManager), nameof(OwlcatModificationsManager.AppliedModifications), MethodType.Getter)]
    [HarmonyPostfix]
    static OwlcatModification[] OwlcatModificationsManager_AppliedModifications_Postfix(OwlcatModification[] __result) =>
        ModificationBeingApplied is null ? __result : ([ModificationBeingApplied, .. __result]);

    const string DirectReferenceBundleName = "BlueprintDirectReferences";

    [HarmonyPatch(typeof(OwlcatModification), nameof(OwlcatModification.LoadBundles))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> OwlcatModification_LoadBundles_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var matcher = new CodeMatcher(instructions)
            .MatchStartForward(
                new CodeMatch(OpCodes.Ldloc_2),
                new CodeMatch(OpCodes.Ldstr, DirectReferenceBundleName));

        var start = matcher.Pos;

        //Main.PatchLog(nameof(OwlcatModification_LoadBundles_Transpiler), $"Start: [{start:X}] = {matcher.Instruction}");

        matcher = matcher
            .MatchStartForward(
                new CodeMatch(OpCodes.Ldloc_0),
                new CodeMatch(ci => ci.Calls(AccessTools.Method(typeof(IEnumerator), nameof(IEnumerator.MoveNext)))),
                new CodeMatch(OpCodes.Brtrue));

        var end = matcher.Pos;

        //Main.PatchLog(nameof(OwlcatModification_LoadBundles_Transpiler), $"End: {end:X} = {matcher.Instruction}");

        var iList = matcher
            .RemoveInstructionsInRange(start, end - 1)
            .InstructionEnumeration()
            .ToList();

        //Main.PatchLog(nameof(OwlcatModification_LoadBundles_Transpiler), string.Join("\n", iList));

        return iList;

        //var iList = instructions.ToList();

        //var skipSectionStart = instructions.FindInstructionsIndexed(
        //[
        //    ci => ci.opcode == OpCodes.Ldloc_2,
        //    ci => ci.opcode == OpCodes.Ldstr && ci.operand is DirectReferenceBundleName
        //]).ToArray();

        //var skipSectionEnd = instructions.FindInstructionsIndexed(
        //[
        //    ci => ci.opcode == OpCodes.Ldloc_0,
        //    ci => ci.Calls(AccessTools.Method(typeof(IEnumerator), nameof(IEnumerator.MoveNext))),
        //    ci => ci.opcode == OpCodes.Brtrue
        //]).ToArray();

        //if (skipSectionStart.Length != 2 || skipSectionEnd.Length != 3)
        //    throw new Exception("Could not find target instructions");

        //var startIndex = skipSectionStart[0].index;
        //var endIndex = skipSectionEnd[0].index;

        //iList.RemoveRange(start, end - start);

        //Main.Logger.Log(string.Join("\n", iList));

        //return iList;
    }

    [HarmonyPatch(typeof(OwlcatModification), nameof(OwlcatModification.LoadBundles))]
    [HarmonyPostfix]
    static void OwlcatModification_LoadBundles_Postfix(OwlcatModification __instance)
    {
        var bundleName = __instance.Bundles.SingleOrDefault(b => b.EndsWith(DirectReferenceBundleName));

        if (bundleName is null)
            return;

#if DEBUG
        __instance.Logger.Log($"Try load {DirectReferenceBundleName} ({bundleName})");
#endif

        BundlesLoadService.Instance.LoadDependencies(bundleName);
        __instance.m_ReferencedAssetsBundle = BundlesLoadService.Instance.RequestBundle(bundleName);

#if DEBUG
        __instance.Logger.Log($"Bundle {bundleName} is not null? {__instance.m_ReferencedAssetsBundle != null}");
#endif

        if (__instance.m_ReferencedAssetsBundle != null)
        {
#if DEBUG
            __instance.Logger.Log("Load BlueprintReferencedAssets");
#endif

            __instance.m_ReferencedAssets = __instance.m_ReferencedAssetsBundle.LoadAllAssets<BlueprintReferencedAssets>().Single();

            if (__instance.m_ReferencedAssets != null)
            {
#if DEBUG
                __instance.Logger.Log($"{__instance.m_ReferencedAssets.m_Entries.Count} entries");
                foreach (var e in __instance.m_ReferencedAssets.m_Entries)
                {
                    __instance.Logger.Log($"  ({e.AssetId}, {e.FileId}) {e.Asset?.GetType().ToString() ?? "NULL"}");
                }
#endif

                UnityObjectConverter.ModificationAssetLists.Add(__instance.m_ReferencedAssets);

            }
            
            BundlesLoadService.Instance.UnloadBundle(bundleName);
        }

        BundlesLoadService.Instance.UnloadDependencies(bundleName);
    }
}
