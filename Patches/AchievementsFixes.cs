using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker.Achievements;
using Kingmaker.Stores;

using MicroUtils.Linq;
using MicroUtils.Transpiler;

using UnityEngine;

namespace MicroPatches.Patches
{
    [MicroPatch("Achievement fixes: AchievementsManager")]
    [HarmonyPatch(typeof(AchievementsManager), nameof(AchievementsManager.Activate))]
    [HarmonyPatchCategory(Main.ExperimentalCategory)]
    static class AchievementsManagerFixes
    {
        static void InitAchievementsManager(AchievementsManager __instance)
        {
            switch (StoreManager.Store)
            {
                case StoreType.Steam:
#if DEBUG
                    Main.PatchLog(nameof(AchievementsManagerFixes), $"Init {nameof(SteamAchievementsManager)}");
#endif
                    var steamAchievementsManager = SteamAchievementsManager.Instance;
                    if (!steamAchievementsManager)
                    {
                        steamAchievementsManager = new GameObject().AddComponent<SteamAchievementsManager>();
                        UnityEngine.Object.DontDestroyOnLoad(steamAchievementsManager);
                    }
                    steamAchievementsManager.Achievements = __instance;
                    __instance.m_AchievementHandler = steamAchievementsManager;

                    break;

                case StoreType.GoG:
#if DEBUG
                    Main.PatchLog(nameof(AchievementsManagerFixes), $"Init {nameof(GogAchievementsManager)}");
#endif
                    var gogAchievementsManager = GogAchievementsManager.Instance;
                    if (!gogAchievementsManager)
                    {
                        gogAchievementsManager = new GameObject().AddComponent<GogAchievementsManager>();
                        UnityEngine.Object.DontDestroyOnLoad(gogAchievementsManager);
                    }
                    gogAchievementsManager.Achievements = __instance;
                    break;

                case StoreType.EpicGames:
#if DEBUG
                    Main.PatchLog(nameof(AchievementsManagerFixes), $"Init {nameof(EGSAchievementsManager)}");
#endif
                    var egsachievementsManager = new EGSAchievementsManager(__instance);
                    egsachievementsManager.SyncAchievements();
                    __instance.m_AchievementHandler = egsachievementsManager;
                    break;
            }
        }

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
        {
            (var head, instructions) = instructions.Pop();

            while (!head.Calls(AccessTools.PropertyGetter(typeof(Application), nameof(Application.isPlaying))))
            {
                yield return head;

                (head, instructions) = instructions.Pop();
            }

            yield return head;
            
            (var brfalse_s, instructions) = instructions.Pop();

            if (brfalse_s.opcode != OpCodes.Brfalse_S)
                throw new Exception($"Unexpected instruction {brfalse_s}");

            yield return brfalse_s;

            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return CodeInstruction.Call((AchievementsManager instance) => InitAchievementsManager(instance));

            yield return instructions.Last();
        }
    }

    [MicroPatch("Achievement fixes: Null Achievement SteamId")]
    [HarmonyPatch(typeof(SteamAchievementsManager), nameof(SteamAchievementsManager.OnUserStatsReceived))]
    [HarmonyPatchCategory(Main.ExperimentalCategory)]
    static class NullAchievmentSteamIdFix
    {
        static void LogSteamId(string steamId)
        {
            if (steamId == null)
                Main.PatchLog(nameof(AchievementsManagerFixes), $"Achievement NULL");
            else
                Main.PatchLog(nameof(AchievementsManagerFixes), $"Achievement '{steamId}'");
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
#if DEBUG
                new CodeInstruction(OpCodes.Dup),
                CodeInstruction.Call((string s) => LogSteamId(s))
#endif
            ]);

            return iList;
        }
    }
}
