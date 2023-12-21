using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using UnityModManagerNet;

namespace MicroPatches.Patches
{
    internal static class MicroPatchExtensions
    {
        [Obsolete]
        public static Type GetPatchType(this PatchClassProcessor pc) => Main.Patches.First(p => p.Patch == pc).PatchType;

        public static bool IsEnabled(this MicroPatch mp) => Main.Instance.GetPatchEnabled(mp);
        public static bool IsApplied(this MicroPatch mp) => Main.Instance.GetPatchApplied(mp);

        public static bool Failed(this MicroPatch mp) => mp.IsEnabled() && !mp.IsApplied();
    }

    internal class MicroPatch
    {
        internal static UnityModManager.ModEntry.ModLogger Logger = null!;

        internal static class Category
        {
            internal const string Experimental = "Experimental";
            //internal const string Hidden = "Hidden";
            internal const string Optional = "Optional";
        }

        public MicroPatch(PatchClassProcessor patch, Type patchType)
        {
            Patch = patch;
            PatchType = patchType;

            if (PatchType.GetCustomAttribute<MicroPatchAttribute>() is not { } attr)
            {
                Logger.Warning($"Missing {nameof(MicroPatchAttribute)} on patch type {PatchType.Name}");
                attr = new(PatchType.Name) { Hidden = true };
            }    

            PatchAttribute = attr;
        }

        public PatchClassProcessor Patch { get; }
        public Type PatchType { get; }

        public MicroPatchAttribute PatchAttribute { get; }

        public string DisplayName => PatchAttribute.Name;
        public string Description => PatchAttribute.Description;
        public bool IsHidden => PatchAttribute.Hidden;

        public bool IsExperimental => Patch.GetCategory() is Category.Experimental;
        public bool IsOptional => Patch.GetCategory() is Category.Optional or Category.Experimental;

        [Obsolete]
        public static PatchClassProcessor GetPatchProcessor(Type t) => Main.PatchClasses.First(p => p.t == t).pc;

        [Obsolete]
        public static Type GetPatchType(PatchClassProcessor pc) => Main.PatchClasses.First(p => p.pc == pc).t;

        [Obsolete]
        public static MicroPatchAttribute? GetPatchAttribute(PatchClassProcessor pc) => pc.GetPatchType().GetCustomAttribute<MicroPatchAttribute>();

        [Obsolete]
        public static bool IsExperimental_Obsolete(PatchClassProcessor pc) => pc.GetCategory() is Category.Experimental;

        [Obsolete]
        public static bool IsHidden_Obsolete(PatchClassProcessor pc) => false; /*pc.GetCategory() is Category.Hidden;*/

        [Obsolete]
        public static bool IsOptional_Obsolete(PatchClassProcessor pc) =>
#if DEBUG
            false;
#else
            pc.GetCategory() is MicroPatch.Category.Optional or MicroPatch.Category.Experimental;
#endif
    }
}
