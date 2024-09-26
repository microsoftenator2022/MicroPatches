using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker.Achievements;
using Kingmaker.EOSSDK;
using Kingmaker.Stores;

using MicroUtils.Linq;
using MicroUtils.Transpiler;

using UnityEngine;

namespace MicroPatches.Patches
{
    //[MicroPatch("Achievement fixes: Null Achievement SteamId", Experimental = true)]
    //[HarmonyPatch(typeof(SteamAchievementsManager), nameof(SteamAchievementsManager.OnUserStatsReceived))]
    static class NullAchievmentSteamIdFix
    {
        static void LogSteamId(AchievementData achievementData)
        {
            var steamId = achievementData.SteamId;

            Main.PatchLog(nameof(SteamAchievementsManager), $"Achievement {achievementData.name} SteamId is {(String.IsNullOrEmpty(steamId) ? "NULL" : steamId)}");
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
        {
            var toMatch = new Func<CodeInstruction, bool>[]
            {
                ci => ci.opcode == OpCodes.Br_S,
                ci => ci.opcode == OpCodes.Ldloc_0,
                ci => ci.Calls(AccessTools.PropertyGetter(typeof(IEnumerator<AchievementEntity>), nameof(IEnumerator<AchievementEntity>.Current))),
                ci => ci.opcode == OpCodes.Stloc_1,
                ci => ci.opcode == OpCodes.Ldloc_1,
                ci => ci.opcode == OpCodes.Ldfld,
                ci => ci.opcode == OpCodes.Ldfld &&
                    ((FieldInfo)ci.operand == AccessTools.Field(typeof(AchievementData), nameof(AchievementData.SteamId)))
            };

            var match = instructions.FindInstructionsIndexed(toMatch).ToArray();

            if (match.Length != toMatch.Length)
            {
                throw new Exception("Could not find matching instructions");
            }

            var iList = instructions.ToList();

            var loopStart = (Label)match[0].instruction.operand;

            var notNullTarget = ilGen.DefineLabel();

            iList.InsertRange(match.Last().index + 1,
            [
                new CodeInstruction(OpCodes.Dup),
                CodeInstruction.Call((string s) => string.IsNullOrEmpty(s)),
                new CodeInstruction(OpCodes.Brfalse_S, notNullTarget),
                new CodeInstruction(OpCodes.Pop),
                new CodeInstruction(OpCodes.Br_S, loopStart),
                new CodeInstruction(OpCodes.Nop) { labels = [notNullTarget] },
            ]);

//#if DEBUG
            iList.InsertRange(match.Last().index, [
                new CodeInstruction(OpCodes.Dup),
                CodeInstruction.Call((AchievementData ad) => LogSteamId(ad))
            ]);
//#endif

            return iList;
        }
    }

    [MicroPatch("Achievement fixes: EGS AchievementsManager NRE", Experimental = true)]
    [HarmonyPatch]
    //[HarmonyPatchCategory(MicroPatch.Category.Experimental)]

    static class EGSAchievementsHelperNullFix
    {
        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> TargetMethods() =>
        [
            AccessTools.Method(typeof(EGSAchievementsManager), nameof(EGSAchievementsManager.SyncAchievements)),
            AccessTools.Method(typeof(EGSAchievementsManager), nameof(EGSAchievementsManager.OnAchievementProgressUpdated)),
            AccessTools.Method(typeof(EGSAchievementsManager), nameof(EGSAchievementsManager.OnAchievementUnlocked))
        ];

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
        {
            var label = ilGen.DefineLabel();

            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(EGSAchievementsManager), nameof(EGSAchievementsManager.m_AchievementsHelper)));
            yield return new CodeInstruction(OpCodes.Brtrue_S, label);
            yield return new CodeInstruction(OpCodes.Ret);
            yield return new CodeInstruction(OpCodes.Nop) { labels = [label] };

            foreach (var ci in instructions)
                yield return ci;
        }
    }
}
