using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Components;

namespace MicroPatches.Patches
{
    class NamedCasterTargetSwap : MicroPatchGroup
    {
        public override string DisplayName => "Inverted CasterNamedProperty/TargetNamedProperty calculation fix";
        public override bool Optional => true;
        public override bool Experimental => true;
    }

    //[MicroPatchGroup(typeof(NamedCasterTargetSwap))]
    //[HarmonyPatch(typeof(ContextValue), nameof(ContextValue.Calculate))]
    static class NamedCasterTargetSwap_Patch
    {
        //[HarmonyTranspiler]
        //static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        //{
        //    var switchInstruction = instructions.First(ci => ci.opcode == OpCodes.Switch);

        //    if (switchInstruction.operand is not Label[] jumpTable)
        //        throw new Exception();

        //    var casterNamed = (int)ContextValueType.CasterNamedProperty;
        //    var targetNamed = (int)ContextValueType.TargetNamedProperty;

        //    var tTarget = jumpTable[casterNamed];
        //    var cTarget = jumpTable[targetNamed];

        //    jumpTable[casterNamed] = cTarget;
        //    jumpTable[targetNamed] = tTarget;

        //    return instructions;
        //}

        [MicroPatchGroup(typeof(NamedCasterTargetSwap))]
        [HarmonyPatch(typeof(BlueprintsCache), nameof(BlueprintsCache.Init))]
        static class BlueprintUnfixer
        {
            interface IBlueprintFix
            {
                string AssetId { get; }
                void Execute(SimpleBlueprint blueprint);
            }

            record class BlueprintFix<TBlueprint>(string AssetId, Action<TBlueprint> Fix) : IBlueprintFix where TBlueprint : SimpleBlueprint
            {
                public void Execute(SimpleBlueprint blueprint) => Fix((TBlueprint)blueprint);
            }

            static readonly IEnumerable<IBlueprintFix> Fixes =
            [
                new BlueprintFix<BlueprintBuff>("b06f4cae947c4bb2b39522846c0a5ff6", blueprint =>
                {
                    var armorBonus = blueprint.GetComponent<WarhammerArmorBonus>();

                    if (armorBonus.BonusDeflectionValue.ValueType is ContextValueType.CasterNamedProperty)
                        armorBonus.BonusDeflectionValue.ValueType = ContextValueType.TargetNamedProperty;

                    if (armorBonus.BonusAbsorptionValue.ValueType is ContextValueType.CasterNamedProperty)
                        armorBonus.BonusAbsorptionValue.ValueType = ContextValueType.TargetNamedProperty;
                }),
                new BlueprintFix<BlueprintBuff>("ce0ac53faaa94fdea58c1f8b35df6fbe", blueprint =>
                {
                    var armorBonus = blueprint.GetComponent<WarhammerArmorBonus>();

                    if (armorBonus.BonusDeflectionValue.ValueType is ContextValueType.CasterNamedProperty)
                        armorBonus.BonusDeflectionValue.ValueType = ContextValueType.TargetNamedProperty;

                    if (armorBonus.BonusAbsorptionValue.ValueType is ContextValueType.CasterNamedProperty)
                        armorBonus.BonusAbsorptionValue.ValueType = ContextValueType.TargetNamedProperty;
                })
            ];

            [HarmonyPostfix]
            static void Postfix()
            {
                foreach (var fix in Fixes)
                {
                    var blueprint = ResourcesLibrary.TryGetBlueprint(fix.AssetId);

                    try
                    {
#if DEBUG
                        Main.PatchLog($"{nameof(NamedCasterTargetSwap)}.{nameof(BlueprintUnfixer)}", $"Patching blueprint {fix.AssetId} {blueprint.NameSafe() ?? "NULL"}");
#endif
                        fix.Execute(blueprint);
                    }
                    catch (Exception ex)
                    {
                        Main.PatchWarning($"{nameof(NamedCasterTargetSwap)}.{nameof(BlueprintUnfixer)}", $"Blueprint fix failed for blueprint {fix.AssetId} {blueprint.NameSafe()}");
                        Main.PatchLogException(ex);
                    }
                }
            }
        }
    }
}
