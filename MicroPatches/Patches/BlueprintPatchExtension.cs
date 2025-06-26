using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints.JsonSystem.Helpers;
using Kingmaker.ElementsSystem;
using Kingmaker.Modding;
using Kingmaker.Utility.DotNetExtensions;

using Microsoft.CodeAnalysis;

using MicroUtils.Linq;
using MicroUtils.Transpiler;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MicroPatches.Patches;

[MicroPatch("Micro's Enhanced Blueprint Patches", Optional = false)]
[HarmonyPatch]
public static class BlueprintPatchExtension
{
    static readonly Dictionary<OwlcatModificationSettings, Dictionary<string, string>> MicroBlueprintPatches = new();

    static bool AddPatch(OwlcatModificationSettings modSettings, OwlcatModificationSettings.BlueprintChangeData bcd)
    {
        if ((int)bcd.PatchType != 2)
            return false;

        PFLog.Mods.Log($"Add patch for {bcd.Guid}");

        if (!MicroBlueprintPatches.ContainsKey(modSettings))
            MicroBlueprintPatches[modSettings] = [];

        MicroBlueprintPatches[modSettings][bcd.Guid] = bcd.Filename;

        return true;
    }

    [HarmonyPatch(typeof(OwlcatModificationSettings), nameof(OwlcatModificationSettings.OnAfterDeserialize))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> OwlcatModificationSettings_OnAfterDeserialize_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var match = instructions.FindInstructionsIndexed(
        [
            ci => ci.opcode == OpCodes.Br_S,
            ci => ci.opcode == OpCodes.Newobj,
            ci => ci.opcode == OpCodes.Throw
        ])
        .ToArray();

        if (match.Length != 3)
            throw new Exception("Could not find target instructions");

        var continueTarget = (Label)match[0].instruction.operand;
        
        var labels = match[1].instruction.labels;
        match[1].instruction.labels = [];

        var iList = instructions.ToList();

        CodeInstruction[] toInsert =
        [
            (new CodeInstruction(OpCodes.Ldarg_0)).WithLabels(labels),
            new(OpCodes.Ldloc_1),
            CodeInstruction.Call(
                (OwlcatModificationSettings modSettings, OwlcatModificationSettings.BlueprintChangeData bcd) =>
                    AddPatch(modSettings, bcd)),
            new(OpCodes.Brtrue_S, continueTarget)
        ];

        iList.InsertRange(match[1].index, toInsert);

        return iList;
    }

    [HarmonyPatch(typeof(OwlcatModification), nameof(OwlcatModification.GetResourceReplacement))]
    [HarmonyPostfix]
    static object? OwlcatModification_GetResourceReplacement_Postfix(object? __result, object resource, OwlcatModification __instance, string guid)
    {
        if (!MicroBlueprintPatches.TryGetValue(__instance.Settings, out var patches) ||
            !patches.TryGetValue(guid, out var patchFile))
            return __result;
        
        var obj = __result ?? resource;

        if (obj is not SimpleBlueprint bp)
            return __result;

        var patchFilePath = __instance.GetBlueprintPatchPath(patchFile);

        if (!File.Exists(patchFilePath) && Path.GetExtension(patchFilePath) is not ".patch")
        {
            var pathWithPatchExtension = $"{patchFilePath}.patch";

            if (!File.Exists(pathWithPatchExtension))
            {
                __instance.Logger.Error($"Patch file {patchFilePath} does not exist");
                return __result;
            }

            __instance.Logger.Warning($"Patch filename for {guid} does not have .patch extension. Using {pathWithPatchExtension}");

            patchFilePath = pathWithPatchExtension;
        }

        __instance.Logger.Log($"Applying patch {patchFilePath} to {bp}");

        try
        {
            var patch = JToken.Parse(File.ReadAllText(patchFilePath));

            var blueprintJson = OwlcatModificationBlueprintPatcher.GetJObject(bp);
            var patchedData = JsonPatch.ApplyPatch(blueprintJson["Data"]!, patch, __instance.Logger);

            blueprintJson["Data"] = patchedData;
            
            using var sReader = new StringReader(blueprintJson.ToString());
            using var jReader = new JsonTextReader(sReader);

            var blueprintWrapper = Json.Serializer.Deserialize<BlueprintJsonWrapper>(jReader)!;

            blueprintWrapper.Data.name = bp.name;
            blueprintWrapper.Data.AssetGuid = bp.AssetGuid;
        
            return blueprintWrapper.Data;
        }
        catch (Exception e)
        {
            __instance.Logger.Exception(e);

            return __result;
        }
    }
}
