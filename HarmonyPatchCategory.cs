using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

using HarmonyLib;

namespace HarmonyLib
{
    internal static class HarmonyAttributeExtensions
    {
        static readonly FieldInfo containerType =
            typeof(PatchClassProcessor).GetField(nameof(containerType), BindingFlags.Instance | BindingFlags.NonPublic);

        static readonly FieldInfo containerAttributes =
            typeof(PatchClassProcessor).GetField(nameof(containerAttributes), BindingFlags.Instance | BindingFlags.NonPublic);

        public static bool HasPatchAttribute(this PatchClassProcessor patchClass) => containerAttributes.GetValue(patchClass) is not null;

        public static string? GetCategory(this PatchClassProcessor patchClass) =>
            (containerType.GetValue(patchClass) as Type)?.GetCustomAttribute<HarmonyPatchCategory>()?.Category;

        /// <summary>Searches an assembly for Harmony-annotated classes without category annotations and uses them to create patches</summary>
        /// <param name="harmony">The harmony instance</param>
        /// 
        public static void PatchAllUncategorized(this Harmony harmony)
        {
            var method = new StackTrace().GetFrame(1).GetMethod();
            var assembly = method.ReflectedType.Assembly;
            harmony.PatchAllUncategorized(assembly);
        }

        /// <summary>Searches an assembly for Harmony-annotated classes without category annotations and uses them to create patches</summary>
        /// <param name="harmony">The harmony instance</param>
        /// <param name="assembly">The assembly</param>
        /// 
        public static void PatchAllUncategorized(this Harmony harmony, Assembly assembly)
        {
            var patchClasses = AccessTools.GetTypesFromAssembly(assembly).Select(harmony.CreateClassProcessor).ToArray();
            patchClasses.DoIf((patchClass => string.IsNullOrEmpty(patchClass.GetCategory())), (patchClass => patchClass.Patch()));
        }

        /// <summary>Searches an assembly for Harmony annotations with a specific category and uses them to create patches</summary>
        /// <param name="harmony">The harmony instance</param>
        /// <param name="category">Name of patch category</param>
        /// 
        public static void PatchCategory(this Harmony harmony, string category)
        {
            var method = new StackTrace().GetFrame(1).GetMethod();
            var assembly = method.ReflectedType.Assembly;
            harmony.PatchCategory(assembly, category);
        }

        /// <summary>Searches an assembly for Harmony annotations with a specific category and uses them to create patches</summary>
        /// <param name="harmony">The harmony instance</param>
        /// <param name="assembly">The assembly</param>
        /// <param name="category">Name of patch category</param>
        /// 
        public static void PatchCategory(this Harmony harmony, Assembly assembly, string category)
        {
            var patchClasses = AccessTools.GetTypesFromAssembly(assembly).Select(harmony.CreateClassProcessor).ToArray();
            patchClasses.DoIf((patchClass => patchClass.GetCategory() == category), (patchClass => patchClass.Patch()));
        }
    }

    /// <summary>Annotation to define a category for use with PatchCategory</summary>
    ///
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal class HarmonyPatchCategory : Attribute
    {
        public readonly string Category;

        /// <summary>Annotation specifying the category</summary>
        /// <param name="category">Name of patch category</param>
        ///
        public HarmonyPatchCategory(string category)
        {
            this.Category = category;
        }
    }
}
