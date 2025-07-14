using System;
using System.Linq;
using System.Reflection;

using HarmonyLib;

using Kingmaker;

static class PatchRunner
{
    static readonly FieldInfo containerAttributes =
        typeof(PatchClassProcessor).GetField(nameof(containerAttributes), BindingFlags.Instance | BindingFlags.NonPublic);

    public static bool HasPatchAttribute(this PatchClassProcessor patchClass) =>
        containerAttributes.GetValue(patchClass) is not null;

    static readonly string[] patchTypeNames = new string[]
    {
        "MicroPatches.Patches.BlueprintPatchComponentOwnerFix"
    };

    static (Type t, PatchClassProcessor pc)[] GetPatchClasses(Harmony harmonyInstance, Assembly assembly) =>
        AccessTools.GetTypesFromAssembly(assembly)
            .Where(t => patchTypeNames.Contains(t.FullName))
            .Select(t => (t, pc: harmonyInstance.CreateClassProcessor(t)))
            .Where(tuple => tuple.pc.HasPatchAttribute())
            .ToArray();
    
    public static void RunPatches(Harmony harmonyInstance)
    {
        foreach (var patchClass in GetPatchClasses(harmonyInstance, Assembly.GetAssembly(typeof(MicroPatches.Util))))
        {
            PFLog.Mods.Log($"Running patches from class {patchClass.t}");
            patchClass.pc.Patch();
        }
    }
}