using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

using Kingmaker.Modding;

using UnityEngine;

namespace MicroPatches.Patches
{
    [MicroPatch("OwlMod fixes: Fix Owlcat's mod material shaders 'fix'")]
    [HarmonyPatch(typeof(OwlcatModification), nameof(OwlcatModification.PatchMaterialShaders))]
    static class OwlcatModShadersFixFix
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> _)
        {
            yield return new(OpCodes.Ret);
        }

        [HarmonyPostfix]
        static void Postfix(IEnumerable<Material> materials)
        {
            foreach (var material in materials)
            {
                if (material == null || material.shader == null)
                    continue;
                var shader = Shader.Find(material.shader.name);

                if (shader != null)
                    material.shader = shader;
            }
        }
    }
}
