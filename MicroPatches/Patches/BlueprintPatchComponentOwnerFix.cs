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
using Kingmaker.ElementsSystem;

namespace MicroPatches.Patches
{
    class ElementOwnerFixesGroup : MicroPatchGroup
    {
        public override string DisplayName => "OwlMod fixes: Element owner blueprint null fix";
        public override bool Optional => false;
        public override bool Experimental => false;
    }

    [MicroPatch("OwlMod fixes: Component/Element null OwnerBlueprint")]
    [MicroPatchGroup(typeof(ElementOwnerFixesGroup))]
    [HarmonyPatch]
    static class BlueprintPatchComponentOwnerFix
    {
        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> TargetMethods() => typeof(BlueprintPatcher).GetMethods().Where(mi => mi.Name == nameof(BlueprintPatcher.TryPatchBlueprint));

        internal static void FixBlueprintBeingRead(SimpleBlueprint bp, Type patchType)
        {

            if (Json.BlueprintBeingRead == null)
            {
#if DEBUG
                Main.PatchWarning(nameof(BlueprintPatchComponentOwnerFix), $"Json.BlueprintBeingRead is null. Blueprint is {bp}");
//#else
//                return;
#endif
            }

            if (bp != null && Json.BlueprintBeingRead?.Data != bp)
            {
#if DEBUG
                Main.PatchLog(patchType.Name, $"fixing owner: {bp} (was {Json.BlueprintBeingRead?.Data?.ToString() ?? "NULL"})");
#endif

                Json.BlueprintBeingRead = new(bp);
            }
        }

        [HarmonyPrefix]
        static void Prefix(SimpleBlueprint bp) =>
            FixBlueprintBeingRead(bp, typeof(BlueprintPatchComponentOwnerFix));
    }
}
