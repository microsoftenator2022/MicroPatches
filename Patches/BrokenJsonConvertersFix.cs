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
    //[MicroPatch("OwlMod fixes: Broken Json Converters", Hidden = false, Optional = false)]
    //[HarmonyPatch]
    //internal static class BrokenJsonConvertersFix
    //{
    //    [HarmonyPatch(typeof(SharedStringConverter), nameof(SharedStringConverter.WriteJson))]
    //    [HarmonyPostfix]
    //    static void SharedStringConverter_WriteJson(
    //        SharedStringConverter __instance,
    //        JsonWriter writer,
    //        object value,
    //        JsonSerializer serializer)
    //    {
    //        if (UnityModManager.ModEntries.Any(me => me.Info.Id == "BlueprintExpoRT" && me.Active))
    //        {
    //            Main.PatchLog($"{nameof(BrokenJsonConvertersFix)}.{nameof(SharedStringConverter_WriteJson)}", $"Blueprint ExpoRT active, skipping");
    //            return;
    //        }

    //        var asset = value as SharedStringAsset;
    //        if (asset == null)
    //            return;

    //        writer.WriteStartObject();
    //        writer.WritePropertyName("stringkey");
    //        writer.WriteValue(asset.String.Key);
    //        writer.WriteEndObject();
    //    }
    //}
}
