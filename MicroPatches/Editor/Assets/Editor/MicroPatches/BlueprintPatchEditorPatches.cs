using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;

using Code.GameCore.Blueprints.BlueprintPatcher;

using Core.Reflection;

using HarmonyLib;

using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints.JsonSystem.EditorDatabase;
using Kingmaker.Blueprints.JsonSystem.Helpers;
using Kingmaker.Editor.Blueprints;

using MicroPatches;

using MicroUtils.Transpiler;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using UnityEditor;

using UnityEngine;

//[HarmonyPatchCategory(MicroPatchesDomainReloadHandler.HarmonyPatchCategoryName)]
[HarmonyPatch]
static class BlueprintPatchEditorPatches
{
    private class Binder : GuidClassBinder, ISerializationBinder
    {
        static Lazy<Dictionary<Type, string>> typeToGuid = new (() =>
        {
            PFLog.Mods.Warning("Rebuilding TypeId cache");

            return AppDomain.CurrentDomain.GetAssembliesSafe()
                .AsParallel()
                .SelectMany(v => v.GetTypesSafe())
                .Where(t => !t.IsAbstract)
                .Select(t => (g: t.CustomAttributes.OfType<TypeIdAttribute>().FirstOrDefault()?.GuidString, t))
                .Where(p => !string.IsNullOrEmpty(p.g))
                .ToDictionary(keySelector: p => p.t, elementSelector: p => p.g);
        });

        void ISerializationBinder.BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            try
            {
                base.BindToName(serializedType, out assemblyName, out typeName);
                return;
            }
            catch (ArgumentException)
            {
                assemblyName = serializedType.Name;
                if (typeToGuid.Value.TryGetValue(serializedType, out typeName))
                    return;

                assemblyName = null;
                typeName = null;
            }
        }
    }

    public static readonly JsonSerializerSettings SerializerSettings =
        new((JsonSerializerSettings)typeof(Json).GetField("Settings", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null))
        {
            SerializationBinder = new Binder()
        };


    private static readonly Lazy<JsonSerializer> serializer = new(() =>
    {
        return JsonSerializer.Create(SerializerSettings);
    });

    private static JsonSerializer Serializer => serializer.Value;

    static void ReplacePatchSerializer()
    {
        var serializerField = AccessTools.DeclaredField(typeof(BlueprintPatchEditorUtility), "Serializer");

        var previousSerializer = serializerField.GetValue(null);

        if (previousSerializer == Serializer)
            return;

        PFLog.Mods.Log("Replacing patch serializer");

        serializerField.SetValue(null, Serializer);

        var newValue = serializerField.GetValue(null);

        if (newValue == previousSerializer || newValue != Serializer)
            PFLog.Mods.Error("Serializer was not replaced");
    }

    static void Prepare()
    {
        ReplacePatchSerializer();
    }

    static string GetGuidFromReferenceOrString(object obj)
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
        Debug.Log($"Patching {nameof(BlueprintPatchObjectComparator)}.{nameof(BlueprintPatchObjectComparator.ObjectsAreEqual)}");

        //const string transpilerApplied = "Transpiler applied";

        //var firstTwo = instructions.Take(2).ToArray();

        //if (firstTwo[0].opcode == OpCodes.Ldstr && (firstTwo[1].operand as string) == transpilerApplied)
        //{
        //    Debug.LogWarning("Transpiler already applied");
        //    foreach (var i in instructions)
        //        yield return i;

        //    yield break;
        //}

        //yield return new CodeInstruction(OpCodes.Ldstr, transpilerApplied);
        //yield return new CodeInstruction(OpCodes.Pop);

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

    static void FixMissingTypes(JsonSerializer _, TextWriter sw, BlueprintPatch patch)
    {
        var jobject = JObject.Parse(JsonConvert.SerializeObject(patch, SerializerSettings));

        foreach (var t in jobject.SelectTokens("..$type").Where(t => string.IsNullOrEmpty(t.ToString())).ToArray())
        {
            t.Parent.Remove();
        }

        //var p2 = JsonConvert.DeserializeObject<BlueprintPatch>(jobject.ToString());

        sw.Write(jobject.ToString());
    }

    [HarmonyPatch(typeof(BlueprintPatchEditorUtility), "SavePatchInternal")]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> SavePatchInternal_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        Debug.Log($"Patching {nameof(BlueprintPatchEditorUtility)}.SavePatchInternal");

        var match = instructions.FindInstructionsIndexed(new Func<CodeInstruction, bool>[]
            {
                ci => ci.LoadsField(AccessTools.DeclaredField(typeof(BlueprintPatchEditorUtility), "Serializer")),
                ci => ci.opcode == OpCodes.Ldloc_S,
                ci => ci.opcode == OpCodes.Ldloc_0,
                ci => ci.Calls(AccessTools.Method(
                    typeof(JsonSerializer),
                    nameof(JsonSerializer.Serialize),
                    new [] { typeof(TextWriter), typeof(object) })),

                ci => ci.opcode == OpCodes.Ldloc_S,
                ci => ci.Calls(AccessTools.Method(typeof(TextWriter), nameof(TextWriter.Flush)))
            })
            .ToArray();
        
        if (match.Length != 6)
        {
            PFLog.Mods.Error($"{nameof(SavePatchInternal_Transpiler)} failed to find target instructions");
            return instructions;
        }
        
        match[3].instruction.opcode = OpCodes.Call;
        match[3].instruction.operand = AccessTools.Method(typeof(BlueprintPatchEditorPatches), nameof(FixMissingTypes));

        return instructions;
    }

    [HarmonyPatch(typeof(BlueprintPatchEditorUtility), "SavePatchInternal")]
    [HarmonyPrefix]
    static bool SavePatchInternal_Prefix()
    {
        if (!MicroPatchesEditorPreferences.Instance.UseMicroPatchMode)
            return true;

        var protoId = AccessTools.Field(typeof(BlueprintPatchEditorUtility), "s_ProtoId").GetValue(null) as string;
        
        var targetBlueprint = AccessTools.Field(typeof(BlueprintPatchEditorUtility), "s_Target").GetValue(null) as BlueprintScriptableObject;
        var targetId = targetBlueprint?.AssetGuid;
        var targetPath = targetId is not null ? BlueprintsDatabase.IdToPath(targetId) : null;

        if (protoId is null)
        {
            PFLog.Mods.Error("Prototype id is null");

            return false;
        }

        if (targetPath is null)
        {
            PFLog.Mods.Error("Target blueprint is null");

            return false;
        }

        var protoPath = BlueprintsDatabase.IdToPath(protoId);
        var protoJson = JObject.Parse(File.ReadAllText(protoPath))["Data"];

        //var s = new StringWriter();

        //Json.Serializer.Serialize(s, new BlueprintJsonWrapper(targetBlueprint));

        //s.Flush();

        //var targetJson = JObject.Parse(s.ToString())["Data"];

        var targetJson = JObject.Parse(File.ReadAllText(targetPath))["Data"];

        var maybePatchJson = JsonPatch.GetPatch(targetJson, protoJson);

        if (!maybePatchJson.HasValue)
        {
            PFLog.Mods.Warning($"{targetBlueprint.name} has no patchable changes to {protoPath}");
            return false;
        }

        var patchJson = maybePatchJson.Value;
        
        PFLog.Mods.Log("Patch:\n" + patchJson.ToString());

        PFLog.Mods.Log($"Testing patch\nBefore:\n{protoJson}\nAfter:\n{JsonPatch.ApplyPatch(protoJson, patchJson)}");

        var defaultDir = (new FileInfo(protoPath)).Directory.ToString();

        var selectedPath = EditorUtility.
            OpenFolderPanel("Select Directory To Save",
            defaultDir, "");
        Debug.Log($"Selected path to save patch: {selectedPath}");
        var finalFilePath = $"{Path.Combine(selectedPath, Path.GetFileNameWithoutExtension(protoPath))}.patch";

        File.WriteAllText(finalFilePath, patchJson.ToString());

        EditorUtility.RevealInFinder(finalFilePath);

        return false;
    }
}
