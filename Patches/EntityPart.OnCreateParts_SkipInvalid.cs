using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Kingmaker.EntitySystem.Entities.Base;

using Kingmaker.EntitySystem.Entities;

using Owlcat.Runtime.Core.Logging;

namespace MicroPatches.Patches
{
    [MicroPatch("Skip invalid EntityParts in BaseUnitEntity.OnCreateParts")]
    [HarmonyPatch]
    [HarmonyPatchCategory(MicroPatch.Category.Experimental)]
    static class EntityPart_OnCreateParts_SkipInvalid
    {
        //static bool IsUnitEntity(BaseUnitEntity __instance) => __instance is UnitEntity;

        static bool CanAddPart<T>(BaseUnitEntity __instance) where T : EntityPart
        {
            var canAdd = Activator.CreateInstance<T>().RequiredEntityType.IsAssignableFrom(__instance.GetType());

#if DEBUG
            Main.PatchLog(nameof(EntityPart_OnCreateParts_SkipInvalid), $"[DEBUG] {__instance.GetType()} can add {typeof(T)}? {canAdd}");
#endif

            return canAdd;
        }

        [HarmonyPatch(typeof(BaseUnitEntity), nameof(BaseUnitEntity.OnCreateParts))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> BaseUnitEntity_OnCreateParts_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
        {
            var getOrCreate = typeof(Entity).GetMethod(nameof(Entity.GetOrCreate));
            var canAddPart = typeof(EntityPart_OnCreateParts_SkipInvalid).GetMethod(nameof(CanAddPart), BindingFlags.NonPublic | BindingFlags.Static);

            //if (ci.Calls(AccessTools.Method(typeof(Entity), nameof(Entity.GetOrCreate), [], [typeof(PartProvidesFullCover)])))
            //{
            //    var label = ilGen.DefineLabel();
            //    yield return new(OpCodes.Ldarg_0);
            //    yield return CodeInstruction.Call((BaseUnitEntity entity) => IsUnitEntity(entity));
            //    yield return new(OpCodes.Brfalse_S,  label);
            //    yield return ci;
            //    yield return new(OpCodes.Nop) { labels = [label] };
            //}
            foreach (var ci in instructions)
            {
                if (ci.opcode == OpCodes.Call &&
                    ci.operand is MethodInfo mi &&
                    mi.IsGenericMethod &&
                    mi.GetGenericMethodDefinition() == getOrCreate)
                {
#if DEBUG
                    Main.PatchLog(nameof(EntityPart_OnCreateParts_SkipInvalid), $"[DEBUG] {mi}");
#endif

                    var label = ilGen.DefineLabel();

                    yield return new(OpCodes.Ldarg_0);
                    yield return new(OpCodes.Call, canAddPart.MakeGenericMethod(mi.GetGenericArguments()[0]));
                    yield return new(OpCodes.Brfalse_S, label);
                    yield return ci;
                    yield return new(OpCodes.Nop) { labels = [label] };
                }
                else
                    yield return ci;
            }
        }

        static void Log(EntityPart __instance, Entity owner)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"[DEBUG] EntityPart.Attach");
            sb.AppendLine($"  Part: {__instance}");
            sb.AppendLine($"    RequiredEntityType: {__instance.RequiredEntityType}");
            sb.AppendLine($"  Owner: {owner}");

            Main.PatchLog(nameof(EntityPart_OnCreateParts_SkipInvalid), sb.ToString());
        }

        [HarmonyPatch(typeof(EntityPart), nameof(EntityPart.Attach))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> EntityPart_Attach_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var methods = typeof(LogChannel).GetMethods().Where(mi => mi.Name == nameof(LogChannel.Error));

            foreach (var ci in instructions)
            {
                yield return ci;

                if (methods.Any(ci.Calls))
                {
                    yield return new(OpCodes.Ldarg_0);
                    yield return new(OpCodes.Ldarg_1);
                    yield return CodeInstruction.Call((EntityPart part, Entity entity) => Log(part, entity));

                }
            }
        }
    }
}
