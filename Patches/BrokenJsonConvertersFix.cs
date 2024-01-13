using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Kingmaker.Blueprints.JsonSystem.Converters;
using Kingmaker.Localization;

using Newtonsoft.Json;

using RogueTrader.SharedTypes;

using UnityModManagerNet;

namespace MicroPatches.Patches
{
    [MicroPatch("OwlMod fixes: Broken Json Converters", Hidden = false, Optional = false)]
    [HarmonyPatch]
    internal static class BrokenJsonConvertersFix
    {
        [HarmonyPatch(typeof(UnityObjectConverter), nameof(UnityObjectConverter.WriteJson))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> UnityObjectConverter_WriteJson_Transpiler(IEnumerable<CodeInstruction> _)
        {
            yield return new(OpCodes.Ldarg_0);
            yield return new(OpCodes.Ldarg_1);
            yield return new(OpCodes.Ldarg_2);
            yield return new(OpCodes.Ldarg_3);
            yield return CodeInstruction.Call(
                (UnityObjectConverter instance,
                JsonWriter writer,
                object value,
                JsonSerializer serializer) =>
                UnityObjectConverter_WriteJson(instance, writer, value, serializer));
            yield return new(OpCodes.Pop);
            yield return new(OpCodes.Ret);
        }

        static (string AssetId, long FileId)? GetAssetId(
            BlueprintReferencedAssets assets,
            UnityEngine.Object? obj)
        {
            if (obj == null)
                return null;

            var index = assets.IndexOf(obj);

            if (index < 0)
            {
                Main.PatchWarning($"{nameof(BrokenJsonConvertersFix)}.{nameof(GetAssetId)}", $"Asset {obj.name ?? "NULL"} not found");
                return null;
            }

            var entry = assets.m_Entries[index];

            return (entry.AssetId, entry.FileId);
        }

        //[HarmonyPatch(typeof(UnityObjectConverter), nameof(UnityObjectConverter.WriteJson))]
        //[HarmonyPrefix]
        static bool UnityObjectConverter_WriteJson(
            UnityObjectConverter __instance,
            JsonWriter writer,
            object value,
            JsonSerializer serializer)
        {

            var obj = value as UnityEngine.Object;
            if (obj == null)
            {
                writer.WriteNull();
                return false;
            }

            BlueprintReferencedAssets? assets = UnityObjectConverter.AssetList;

            if (assets == null)
            {
                writer.WriteNull();
                return false;
            }

            var assetData = GetAssetId(assets, obj);

            if (assetData is null)
            {
                writer.WriteNull();
                return false;
            }

            var (text, num) = assetData.Value;

            writer.WriteStartObject();
            writer.WritePropertyName("guid");
            writer.WriteValue(text);
            writer.WritePropertyName("fileid");
            writer.WriteValue(num);
            writer.WriteEndObject();

            return false;
        }

        [HarmonyPatch(typeof(SharedStringConverter), nameof(SharedStringConverter.WriteJson))]
        [HarmonyPostfix]
        static void SharedStringConverter_WriteJson(
            SharedStringConverter __instance,
            JsonWriter writer,
            object value,
            JsonSerializer serializer)
        {
            if (UnityModManager.ModEntries.Any(me => me.Info.Id == "BlueprintExpoRT" && me.Active))
            {
                Main.PatchLog($"{nameof(BrokenJsonConvertersFix)}.{nameof(SharedStringConverter_WriteJson)}", $"Blueprint ExpoRT active, skipping");
                return;
            }

            var asset = value as SharedStringAsset;
            if (asset == null)
                return;

            writer.WriteStartObject();
            writer.WritePropertyName("stringkey");
            writer.WriteValue(asset.String.Key);
            writer.WriteEndObject();
        }
    }
}
