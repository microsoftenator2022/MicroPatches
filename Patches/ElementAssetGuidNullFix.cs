using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker.ElementsSystem;

namespace MicroPatches.Patches
{
    [MicroPatch("Element AssetGuid null fix")]
    [HarmonyPatch(typeof(Element))]
    static class ElementAssetGuidNullFix
    {
        static bool handleNullName(Element __instance, ref string __result)
        {
            __result = "anonymous";

            return !string.IsNullOrEmpty(__instance.name);
        }

        [HarmonyPatch(nameof(Element.AssetGuidShort), MethodType.Getter)]
        [HarmonyPrefix]
        static bool AssetGuidShort_Prefix(Element __instance, ref string __result) =>
            handleNullName(__instance, ref __result);

        [HarmonyPatch(nameof(Element.AssetGuid), MethodType.Getter)]
        [HarmonyPrefix]
        static bool AssetGuid_Prefix(Element __instance, ref string __result) =>
            handleNullName(__instance, ref __result);
    }
}
