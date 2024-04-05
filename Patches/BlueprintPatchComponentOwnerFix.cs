using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

using Code.GameCore.Blueprints.BlueprintPatcher;

using HarmonyLib;

using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;

namespace MicroPatches.Patches
{
    [MicroPatch("OwlMod fixes: Component/Element null OwnerBlueprint")]
    [HarmonyPatch]
    static class BlueprintPatchComponentOwnerFix
    {
        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> TargetMethods() => typeof(BlueprintPatcher).GetMethods().Where(mi => mi.Name == nameof(BlueprintPatcher.TryPatchBlueprint));

        [HarmonyPrefix]
        static void Prefix(SimpleBlueprint bp)
        {
            if (Json.BlueprintBeingRead == null)
            {
                //Main.PatchWarning(nameof(BlueprintPatchComponentOwnerFix), $"Json.BlueprintBeingRead is null. Blueprint is {bp}");

                return;
            }

            if (Json.BlueprintBeingRead.Data != bp)
                Json.BlueprintBeingRead = new(bp);
        }
    }
}
