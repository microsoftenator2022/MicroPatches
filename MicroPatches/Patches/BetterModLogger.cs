using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Code.GameCore.Blueprints.BlueprintPatcher;

using Core.Reflection;

using HarmonyLib;

using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Localization.Shared;
using Kingmaker.Modding;
using Kingmaker.UnitLogic.FactLogic;

using Newtonsoft.Json;

using Owlcat.Runtime.Core.Logging;

namespace MicroPatches.Patches;

class BetterModLoggerPatchesGroup : MicroPatchGroup
{
    public override string DisplayName => "Better OwlMod logging";
    public override bool Experimental => true;
}

[MicroPatchGroup(typeof(BetterModLoggerPatchesGroup))]
[HarmonyPatch]
static class BetterModLogger
{
    [HarmonyPatch(typeof(OwlcatModification), MethodType.Constructor, [typeof(string), typeof(string), typeof(OwlcatModificationManifest), typeof(Exception), typeof(ILocalizationProvider)])]
    [HarmonyPostfix]
    static void AddModLogSink(OwlcatModification __instance, string dataFolderPath)
    {
        Main.PatchLog(nameof(BetterModLogger), $"{__instance.UniqueName}.Logger.Name = {__instance.Logger?.Name}");

        if (__instance.Logger is null || __instance.Logger.Name != __instance.Manifest.UniqueName)
            return;

        var path = Path.GetDirectoryName(__instance.DataFilePath);

        var fileName = $"{OwlcatModification.InvalidPathCharsRegex.Replace(__instance.Manifest.UniqueName, "")}_Log.txt";

        Main.PatchLog(nameof(BetterModLogger), $"Log file path: {Path.Combine(path, fileName)}");

        var sink = new UberLoggerFilter(new UberLoggerFile(fileName, path), LogSeverity.Disabled, [__instance.Manifest.UniqueName], []);

        Owlcat.Runtime.Core.Logging.Logger.Instance.AddLogger(sink, false);
    }

    [HarmonyPatch(typeof(OwlcatModification), nameof(OwlcatModification.GetBlueprintPatch), [typeof(object), typeof(string)])]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> GetBlueprintPatch_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var applied = false;

        var deserializeMethod = typeof(JsonSerializer).GetMethods().Single(m => m.Name == nameof(JsonSerializer.Deserialize) && m.IsGenericMethodDefinition);

        foreach (var i in instructions)
        {
            yield return i;

            if (i.opcode == OpCodes.Callvirt)
            {
                var m = (MethodInfo)i.operand;

                if (!m.IsGenericMethod || m.GetGenericMethodDefinition() != deserializeMethod)
                    continue;

                yield return new(OpCodes.Ldarg_0);
                yield return CodeInstruction.Call((BlueprintPatch patch, OwlcatModification mod) => BlueprintPatchWithMod.CopyWithMod(patch, mod));
                
                applied = true;
            }
        }

        if (!applied)
            throw new Exception("Target instruction not found");
    }

    [HarmonyPatch(typeof(OwlcatModification), nameof(OwlcatModification.TryPatchBlueprint))]
    static Exception? Finalizer(Exception __exception, OwlcatModification __instance)
    {
        if (__exception is not null)
            __instance.Logger.Exception(__exception);

        return null;
    }
}

[MicroPatchGroup(typeof(BetterModLoggerPatchesGroup))]
[HarmonyPatch]
static class PatchModLogInvocations
{
    static IEnumerable<MethodInfo> TargetMethods() =>
        AccessTools.GetDeclaredMethods(typeof(OwlcatModification)).Where(m => !m.IsStatic && !m.IsGenericMethod);

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var i in instructions)
        {
            if (i.Calls(AccessTools.PropertyGetter(typeof(PFLog), nameof(PFLog.Mods))))
            {
                yield return new(OpCodes.Ldarg_0);

                i.opcode = OpCodes.Ldfld;
                i.operand = AccessTools.Field(typeof(OwlcatModification), nameof(OwlcatModification.Logger));
            }

            yield return i;
        }
    }
}

[MicroPatchGroup(typeof(BetterModLoggerPatchesGroup))]
[HarmonyPatch]
class BlueprintPatchWithMod : BlueprintPatch
{
    public OwlcatModification? Mod = null;


    static readonly object comparatorLoggerLock = new();
    static LogChannel ModLogger = PFLog.Mods;

    static void SetPatchComparatorLogger(LogChannel? logger)
    {
        ModLogger = logger ?? PFLog.Mods;
    }

    public static BlueprintPatchWithMod CopyWithMod(BlueprintPatch original, OwlcatModification mod)
    {
        var patch = new BlueprintPatchWithMod();

        patch.TargetGuid = original.TargetGuid;
        patch.Mod = mod;

        patch.ArrayPatches = original.ArrayPatches.Select(p => PatchOperations.CopyPatchOperation<BlueprintSimpleArrayPatchOperation, PatchOperations.ArrayPatchWithMod>(p, mod)).ToArray();
        patch.ComponentsPatches = original.ComponentsPatches.Select(p => PatchOperations.CopyPatchOperation<BlueprintComponentsPatchOperation, PatchOperations.ComponentsPatchWithMod>(p, mod)).ToArray();
        patch.FieldOverrides = original.FieldOverrides.Select(p => PatchOperations.CopyPatchOperation<BlueprintFieldOverrideOperation, PatchOperations.FieldOverrideWithMod>(p, mod)).ToArray();

        return patch;
    }

    static IEnumerable<MethodInfo> TargetMethods() =>
        AccessTools.GetDeclaredMethods(typeof(BlueprintPatchObjectComparator));

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var i in instructions)
        {
            if (i.LoadsField(AccessTools.Field(typeof(BlueprintPatchObjectComparator), nameof(BlueprintPatchObjectComparator.Logger))))
            {
                i.operand = AccessTools.Field(typeof(BlueprintPatchWithMod), nameof(BlueprintPatchWithMod.ModLogger));
            }

            yield return i;
        }
    }

    [MicroPatchGroup(typeof(BetterModLoggerPatchesGroup))]
    [HarmonyPatch]
    static class PatchOperations
    {
        public interface IPatchOperationWithMod
        {
            OwlcatModification? Mod { get; set; }
        }

        public class FieldOverrideWithMod : BlueprintFieldOverrideOperation, IPatchOperationWithMod
        {
            public OwlcatModification? Mod { get; set; }

            public override void Apply(SimpleBlueprint bp)
            {
                lock(comparatorLoggerLock)
                {
                    SetPatchComparatorLogger(Mod?.Logger);

                    base.Apply(bp);

                    SetPatchComparatorLogger(null);
                }
            }
        }

        public class ComponentsPatchWithMod : BlueprintComponentsPatchOperation, IPatchOperationWithMod
        {
            public OwlcatModification? Mod { get; set; }

            public override void Apply(SimpleBlueprint bp)
            {
                lock (comparatorLoggerLock)
                {
                    SetPatchComparatorLogger(Mod?.Logger);

                    base.Apply(bp);

                    SetPatchComparatorLogger(null);
                }
            }
        }

        public class ArrayPatchWithMod : BlueprintSimpleArrayPatchOperation, IPatchOperationWithMod
        {
            public OwlcatModification? Mod { get; set; }

            public override void Apply(SimpleBlueprint bp)
            {
                lock (comparatorLoggerLock)
                {
                    SetPatchComparatorLogger(Mod?.Logger);

                    base.Apply(bp);

                    SetPatchComparatorLogger(null);
                }
            }
        }

        public static TOut CopyPatchOperation<TIn, TOut>(TIn original, OwlcatModification mod)
            where TIn : BlueprintPatchOperation
            where TOut : TIn, IPatchOperationWithMod, new()
        {
            var result = new TOut();

            result.Mod = mod;

            result.concreteBlueprintType = original.concreteBlueprintType;
            result.field = original.field;
            result.fieldHolder = original.fieldHolder;
            result.FieldName = original.FieldName;
            result.fieldType = original.fieldType;
            result.TargetGuid = original.TargetGuid;

            if (result is FieldOverrideWithMod fo && original is BlueprintFieldOverrideOperation ofo)
            {
                fo.FieldValue = ofo.FieldValue;
            }
            else if (result is ComponentsPatchWithMod cp && original is BlueprintComponentsPatchOperation ocp)
            {
                cp.FieldValue = ocp.FieldValue;
                cp.OperationType = ocp.OperationType;
            }
            else if (result is ArrayPatchWithMod ap && original is BlueprintSimpleArrayPatchOperation oap)
            {
                ap.OperationType = oap.OperationType;
                ap.TargetValue = oap.TargetValue;
                ap.Value = oap.Value;
            }
            else
            {
                Main.PatchError(nameof(CopyPatchOperation), $"Unsupported blueprint patch type {typeof(TIn)}");
            }

            return result;
        }

        static IEnumerable<MethodInfo> TargetMethods()
        {
            foreach (var m in new[]
            {
                typeof(BlueprintPatchOperation),
                typeof(BlueprintFieldOverrideOperation),
                typeof(BlueprintComponentsPatchOperation),
                typeof(BlueprintSimpleArrayPatchOperation),
            }
                .SelectMany(AccessTools.GetDeclaredMethods)
                .Where(m => !m.IsStatic && !m.IsGenericMethod))
                yield return m;
        }

        static LogChannel TryGetLogger(BlueprintPatchOperation operation) =>
            (operation as IPatchOperationWithMod)?.Mod?.Logger ?? PFLog.Mods;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var i in instructions)
            {
                yield return i;

                if (i.Calls(AccessTools.PropertyGetter(typeof(PFLog), nameof(PFLog.Mods))))
                {
                    yield return new(OpCodes.Pop);
                    yield return new(OpCodes.Ldarg_0);
                    yield return CodeInstruction.Call((BlueprintPatchOperation operation) => TryGetLogger(operation));
                }
            }
        }
    }
}
