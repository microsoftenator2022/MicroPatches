using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using HarmonyLib;

using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints.JsonSystem.Helpers;
using Kingmaker.Modding;

namespace MicroPatches.Patches
{
    [MicroPatch("Add components from mod assemblies to binder cache")]
    [HarmonyPatch(typeof(OwlcatModification), "LoadAssemblies")]
    [HarmonyPatchCategory(Main.ExperimentalCategory)]
    internal static class OwlcatModification_LoadAssemblies_Patch
    {
        internal static readonly Lazy<Dictionary<string, Type>?> GuidToTypeCache = new(() =>
        {
            var maybeGuidToType = AccessTools.PropertyGetter(typeof(GuidClassBinder), "GuidToTypeCache");

            if (maybeGuidToType?.Invoke(null, []) is not Dictionary<string, Type> guidToTypeCache)
            {
                Main.PatchError(nameof(OwlcatModification_LoadAssemblies_Patch), $"GuidToTypeCache = {maybeGuidToType?.ToString() ?? "NULL"}");
                return null;
            }

            return guidToTypeCache;
        });

        internal static readonly Lazy<Dictionary<Type, string>?> TypeToGuidCache = new(() =>
        {
            var maybeTypeToGuid = AccessTools.PropertyGetter(typeof(GuidClassBinder), "TypeToGuidCache");

            if (maybeTypeToGuid?.Invoke(null, []) is not Dictionary<Type, string> typeToGuidCache)
            {
                Main.PatchError(nameof(OwlcatModification_LoadAssemblies_Patch), $"TypeToGuidCache = {maybeTypeToGuid?.ToString() ?? "NULL"}");
                return null;
            }

            return typeToGuidCache;
        });

        [HarmonyPostfix]
        static void Postfix(OwlcatModification __instance)
        {
            if (GuidToTypeCache.Value is not { } guidToTypeCache || TypeToGuidCache.Value is not { } typeToGuidCache)
                return;

            foreach (var assembly in __instance.LoadedAssemblies)
            {
                //var binder = (GuidClassBinder)Json.Serializer.SerializationBinder;

                foreach (var (type, guid) in assembly.GetTypes()
                    .Select(type => (type, type.GetCustomAttribute<TypeIdAttribute>()?.GuidString)))
                {
                    if (guid is null)
                        continue;

                    Main.PatchLog(nameof(OwlcatModification_LoadAssemblies_Patch), $"Adding {type} with TypeId {guid} to binder cache");

                    //binder.AddToCache(type, guid);
                    
                    guidToTypeCache![guid] = type;
                    typeToGuidCache![type] = guid;
                }
            }
        }
    }
}
