using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker.Modding;

using UnityModManagerNet;

namespace MicroPatches.Patches;

[MicroPatch("Owlmod dependency", Optional = false)]
[HarmonyPatch(typeof(OwlcatModificationsManager))]
internal static class EnhancedOMMDependencies
{
    static IEnumerable<OwlcatModificationManifest> UMMModManifests
    {
        get
        {
            foreach (var modInfo in UnityModManager.ModEntries.Select(me => me.Info))
            {
                var manifest = new OwlcatModificationManifest()
                {
                    UniqueName = modInfo.Id ?? "",
                    DisplayName = modInfo.DisplayName ?? "",
                    Version = modInfo.Version ?? "",
                    Description = "",
                    Author = modInfo.Author ?? "",
                    Repository = modInfo.Repository ?? "",
                    HomePage = modInfo.HomePage ?? "",
                    Dependencies = []
                };

                yield return manifest;
            }
        }
    }

    static IEnumerable<OwlcatModification> FakeOwlmods
    {
        get
        {
            foreach (var manifest in UMMModManifests)
            {
                var fakeMod = FormatterServices.GetUninitializedObject(typeof(OwlcatModification));

                typeof(OwlcatModification).GetField(nameof(OwlcatModification.Manifest)).SetValue(fakeMod, manifest);

                yield return (OwlcatModification)fakeMod;
            }
        }
    }

    [HarmonyPatch(nameof(OwlcatModificationsManager.CheckDependencies))]
    [HarmonyPrefix]
    static void InjectUMMMods(ref List<OwlcatModification> appliedModifications)
    {
        appliedModifications = appliedModifications.Concat(FakeOwlmods).ToList();
    }

    // Return true if the version is too low
    static bool VersionCheck(string? thisVersionString, string? otherVersionString)
    {
        if (thisVersionString is null || otherVersionString is null) return true;

        var thisVersion = UnityModManager.ParseVersion(thisVersionString);
        var otherVersion = UnityModManager.ParseVersion(otherVersionString);

        return otherVersion < thisVersion;
    }

    [HarmonyPatch(nameof(OwlcatModificationsManager.CheckDependencies))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> CheckDependencies_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
#if DEBUG
        var patched = false;
#endif

        foreach (var i in instructions)
        {
            if (i.Calls(AccessTools.Method(typeof(string), "op_Inequality", [typeof(string), typeof(string)])))
            {
#if DEBUG
                patched = true;
#endif

                //yield return CodeInstruction.Call((string a, string b) => VersionCheck(a, b)).WithLabels(i.labels);
                //continue;

                i.operand = AccessTools.Method(typeof(EnhancedOMMDependencies), nameof(VersionCheck));
            }

            yield return i;
        }

#if DEBUG
        if (!patched)
            throw new Exception("Could not find instructions to patch");
#endif
    }
}

