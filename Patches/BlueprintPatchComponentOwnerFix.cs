using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

using Code.GameCore.Blueprints.BlueprintPatcher;

using HarmonyLib;

using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;

namespace MicroPatches.Patches
{
    [MicroPatch("OwlMod fixes: BlueprintComponentsPatchOperation")]
    [HarmonyPatch]
    static class BlueprintPatchComponentOwnerFix
    {
        [HarmonyPatch(typeof(BlueprintComponent), nameof(BlueprintComponent.OnDeserialized))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> BlueprintComponent_OnDeserialized_Transpiler(
            IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
        {
            var label = ilGen.DefineLabel();

            yield return new(OpCodes.Call,
                AccessTools.PropertyGetter(typeof(Json), nameof(Json.BlueprintBeingRead)));

            yield return new(OpCodes.Brtrue_S, label);

            yield return new(OpCodes.Ret);
            yield return new(OpCodes.Nop) { labels = [label] };

            foreach (var i in instructions)
                yield return i;
        }

        [HarmonyPatch(typeof(BlueprintComponentsPatchOperation), nameof(BlueprintComponentsPatchOperation.Apply))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ApplyPatchOperation_Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            foreach (var i in instructions)
            {
                yield return i;

                if (i.opcode != OpCodes.Ldloc_0)
                    continue;

                yield return new(OpCodes.Dup);
                yield return new(OpCodes.Ldarg_1);
                yield return new(OpCodes.Castclass, typeof(BlueprintScriptableObject));
                yield return new(OpCodes.Call, AccessTools.PropertySetter(
                    typeof(BlueprintComponent),
                    nameof(BlueprintComponent.OwnerBlueprint)));
            }
        }
    }
}
