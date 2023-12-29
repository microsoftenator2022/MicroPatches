using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Items.Weapons;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints.JsonSystem.Converters;
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

using MicroUtils.Transpiler;

using Owlcat.Runtime.Core.Logging;

using RogueTrader.SharedTypes;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace MicroPatches
{
    internal partial class Main
    {
#if DEBUG
        [MicroPatch("Hidden Failure Test Patch", Description = "Test\nTest\nTest\nTest\nTest\nTest\nTest", Hidden = true, Optional = true)]
        [HarmonyPatch(typeof(Main), nameof(Main.Load))]
        //[HarmonyPatchCategory(MicroPatch.Category.Optional)]
        static class TestHiddenFailPatch
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
                throw new Exception($"{nameof(TestHiddenFailPatch)}");
        }
#endif

        void PrePatchTests()
        {
#if DEBUG
            var sb = new StringBuilder();

            sb.Append("PatchGroups:");
            foreach (var g in Main.PatchGroups.Select(g => g.group))
            {
                sb.AppendLine();
                sb.AppendLine($" Group '{g.DisplayName}'");

                sb.AppendLine($"  Optional {g.IsOptional()}");
                sb.AppendLine($"  Hidden {g.Hidden}");
                sb.AppendLine($"  Experimental {g.IsExperimental()}");
                sb.AppendLine($"  Enabled: {g.IsEnabled()}");
                sb.AppendLine($"  Applied: {g.IsApplied()}");

                sb.Append("  Patches:");
                foreach (var p in g.GetPatches())
                {
                    sb.AppendLine();
                    sb.Append($"   Patch '{p.PatchClass.Name}");
                }
            }

            Logger.Log(sb.ToString());
#endif
        }

        void PostPatchTests()
        {
#if DEBUG

#endif
        }
    }
}