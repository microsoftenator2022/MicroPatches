using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Controllers.TurnBased;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Conditions;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.UnitLogic.Progression.Features;

namespace MicroPatches.Patches
{
    [MicroPatch(" Ignore TurnBasedEventsTrigger for Remote Companions")]
    [HarmonyPatch]
    [HarmonyPatchCategory(Main.ExperimentalCategory)]
    internal static class TurnBasedEventsTriggerRemoteCompanionFix
    {
        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> TargetMethods()
        {
            //return [typeof(TurnBasedModeEventsTrigger).GetMethod(nameof(TurnBasedModeEventsTrigger.HandleUnitStartTurn))];

            Type[] interfaces =
            [
                typeof(ITurnBasedModeHandler),
                typeof(IRoundStartHandler),
                typeof(IRoundEndHandler),
                typeof(ITurnStartHandler),
                typeof(ITurnEndHandler),
                typeof(IInterruptTurnStartHandler),
                typeof(IInterruptTurnEndHandler)
            ];

            var methods = interfaces.SelectMany(i => typeof(TurnBasedModeEventsTrigger).GetInterfaceMap(i).TargetMethods);

//#if DEBUG
            var sb = new StringBuilder();
            sb.Append("Target methods:");
            foreach (var mi in methods)
            {
                sb.AppendLine();
                sb.Append($"  {mi}");
            }

            Main.PatchLog(nameof(TurnBasedEventsTriggerRemoteCompanionFix), sb.ToString());
            sb.Clear();
//#endif

            return methods;
        }

        static bool Prefix(TurnBasedModeEventsTrigger __instance, MethodBase __originalMethod)
        {
            //if (__instance.Fact.Blueprint.AssetGuid != "58073019ae10412e9509b6ac97006bf1")
            //    return true;

            var maybeCompanion = __instance.Fact.Owner.GetOptional<UnitPartCompanion>();

            if (maybeCompanion is null)
                return true;

//#if DEBUG
            Main.PatchLog(nameof(TurnBasedEventsTriggerRemoteCompanionFix), $"{__originalMethod}\n" +
                $"  Owner = {__instance.Fact.Owner.Name}\n" +
                $"  Companion state = {maybeCompanion.State}\n" +
                $"  In combat? {__instance.Fact.Owner.IsInCombat}\n" +
                $"  In party? {__instance.Fact.Owner.IsInPlayerParty}");
//#endif

            return maybeCompanion.State is CompanionState.InParty;
        }
    }
}
