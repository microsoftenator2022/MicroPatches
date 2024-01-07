using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker.Modding;

using MicroUtils.Transpiler;

namespace MicroPatches
{
    [HarmonyPatch]
    internal static class FakeOwlmodDependency
    {
        internal static OwlcatModificationManifest FakeManifest => new() { UniqueName = Main.Instance.ModEntry.Info.Id, Version = Main.Instance.ModEntry.Info.Version };

        static readonly Regex VersionRegex = new(@"(\d+)(?:\.(\d+))?(?:\.(\d+))?.*");

        static OwlcatModification? InjectFakeManifest(OwlcatModificationManifest.Dependency dep)
        {
            //Main.Instance.ModEntry.Logger.Log($"dependency {dep.Name} {dep.Version}");
            //Main.Instance.ModEntry.Logger.Log($"compare {FakeManifest.UniqueName} {FakeManifest.Version}");

            // TODO: Should installed mod version >= dependency version be applied globally?
            int? Major(Match match)
            {
                if (int.TryParse(match.Groups[1].Value, out var value))
                    return value;

                return null;
            }

            int? Minor(Match match)
            {
                if (match.Groups[2].Success && int.TryParse(match.Groups[3].Value, out var value))
                {
                    return value;
                }

                return null;
            }

            int? Rev(Match match)
            {
                if (match.Groups[3].Success && int.TryParse(match.Groups[3].Value, out var value))
                {
                    return value;
                }

                return null;
            }

            if (dep.Name != Main.Instance.ModEntry.Info.Id)
                return null;

            var depVersion = VersionRegex.Match(dep.Version);
            var thisVersion = VersionRegex.Match(FakeManifest.Version);
#if DEBUG
            Main.Instance.ModEntry.Logger.Log($"Dependency version {depVersion}");
            Main.Instance.ModEntry.Logger.Log($"This version {thisVersion}");
#endif
            var fakeManifest = FakeManifest;
            fakeManifest.Version = dep.Version;

            var fakeMod = new OwlcatModification("", "", fakeManifest, new Exception(), null);

            if (!depVersion.Success)
                return fakeMod;

            if (!thisVersion.Success)
                return null;

            if ((Major(depVersion) ?? 0) > (Major(thisVersion) ?? 0))
                return null;

            if ((Minor(depVersion) ?? 0) > (Minor(thisVersion) ?? 0))
                return null;

            if ((Rev(depVersion) ?? 0) > (Rev(thisVersion) ?? 0))
                return null;

            return fakeMod;
        }

        [HarmonyPatch(typeof(OwlcatModificationsManager), nameof(OwlcatModificationsManager.CheckDependencies))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
        {
            
            var getElement = instructions.FindInstructionsIndexed(
            [
                ci => ci.opcode == OpCodes.Ldelem_Ref, // dependencies[i]
                ci => ci.opcode == OpCodes.Stfld,      // OwlcatModificationManifest.Dependency dependency
                ci => ci.opcode == OpCodes.Ldarg_1,
                ci => ci.opcode == OpCodes.Ldloc_S
            ]).ToArray();

            var check = instructions.FindInstructionsIndexed(
            [
                ci => ci.opcode == OpCodes.Stloc_S,
                ci => ci.opcode == OpCodes.Ldloc_S,
                ci => ci.opcode == OpCodes.Brtrue_S // if (owlcatModification == null)
            ]).ToArray();

            if (getElement.Length != 4 || check.Length != 3)
                throw new Exception("Cannot find instructions to patch");

            Label ifFalse = ilGen.DefineLabel();

            Label ifTrue = (Label)(check[2].instruction.operand);

            var iList = instructions.ToList();

            var toInsert = new CodeInstruction[]
            {
                getElement[3].instruction,
                new(OpCodes.Ldfld, getElement[1].instruction.operand),
                CodeInstruction.Call((OwlcatModificationManifest.Dependency dependency) => InjectFakeManifest(dependency)),
                new(OpCodes.Dup),
                new(OpCodes.Brfalse_S, ifFalse),
                check[0].instruction,
                new(OpCodes.Br_S, ifTrue),
                new(OpCodes.Pop) { labels = [ifFalse] }
            };

            iList.InsertRange(getElement[1].index + 1, toInsert);

            //var sb = new StringBuilder();

            //foreach (var i in iList)
            //{
            //    sb.AppendLine(i.ToString());
            //}

            //Main.Instance.ModEntry.Logger.Log(sb.ToString());

            return iList;
        }
    }
}
