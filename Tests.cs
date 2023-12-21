using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem.Helpers;
using Kingmaker.Blueprints.Root;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Entities.Base;
using Kingmaker.Modding;
using Kingmaker.UI.Canvases;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Parts;

using MicroPatches.Patches;
using MicroPatches.UGUI;

using Owlcat.Runtime.Core.Logging;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace MicroPatches
{
    internal partial class Main
    {
#if DEBUG
        [MicroPatch("Hidden Failure Test Patch", Description = "Test\nTest\nTest\nTest\nTest\nTest\nTest", Hidden = true)]
        [HarmonyPatch(typeof(Main), nameof(Main.Load))]
        [HarmonyPatchCategory(MicroPatch.Category.Optional)]
        static class TestHiddenFailPatch
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                throw new Exception($"{nameof(TestHiddenFailPatch)}");
            }
        }
#endif

        void PrePatchTests()
        {
#if DEBUG

#endif
        }

        void PostPatchTests()
        {
#if DEBUG

#endif
        }
    }
}