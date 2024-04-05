using System;
using System.Collections;
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
        static readonly FieldInfo containerType =
            typeof(PatchClassProcessor).GetField(nameof(containerType), BindingFlags.Instance | BindingFlags.NonPublic);

        static readonly FieldInfo containerAttributes =
            typeof(PatchClassProcessor).GetField(nameof(containerAttributes), BindingFlags.Instance | BindingFlags.NonPublic);

        public static bool HasPatchAttribute(this PatchClassProcessor patchClass) => containerAttributes.GetValue(patchClass) is not null;

        public static bool IsEnabled(this MicroPatch mp) => Main.Instance.GetPatchEnabled(mp);
        public static bool IsApplied(this MicroPatch mp) => Main.Instance.GetPatchApplied(mp);
        public static bool Failed(this MicroPatch mp) => mp.IsEnabled() && !mp.IsApplied();

        public static IEnumerable<MicroPatch> GetPatches(this MicroPatch.IPatchGroup group) =>
            Main.PatchGroups.Single(g => g.group.Equals(group)).patches;

        public static bool IsApplied(this MicroPatch.IPatchGroup group) =>
            group.GetPatches().All(p => p.IsApplied());

        public static bool IsEnabled(this MicroPatch.IPatchGroup group) =>
            group.GetPatches().All(p => p.IsEnabled());

        public static bool Failed(this MicroPatch.IPatchGroup group) =>
            group.GetPatches().Any(p => p.Failed());

        public static bool IsOptional(this MicroPatch.IPatchGroup group) => group.Optional || group.Experimental;
        public static bool IsExperimental(this MicroPatch.IPatchGroup group) => group.Experimental;
    }

    internal abstract class MicroPatchGroup() : MicroPatch.IPatchGroup
    {
        public abstract string DisplayName { get; }
        public virtual string Description { get; } = "";
        public virtual bool Experimental { get; } = false;

        public virtual bool Optional { get; } = false;
        public virtual bool Hidden { get; } = false;
        public virtual Version? MaxGameVersion => null;

        public virtual bool Equals(MicroPatch.IPatchGroup other)
        {
            //Main.PatchLog(nameof(MicroPatchGroup), $"this: {this.GetType()}, {this.DisplayName}\nother: {other.GetType()}, {other.DisplayName}");

            return this.GetType() == other.GetType() &&
            this.DisplayName == other.DisplayName;
        }

        public override bool Equals(object other) => other is MicroPatchGroup g && this.Equals(g);
        public override int GetHashCode() => (this.GetType(), this.DisplayName).GetHashCode();
    }

    internal class MicroPatch
    {
        public interface IPatchGroup : IEquatable<IPatchGroup>
        {
            string DisplayName { get; }
            string Description { get; }
            bool Optional { get; }
            bool Experimental { get; }
            bool Hidden { get; }
            Version? MaxGameVersion { get; }
        }

        private class PatchGroup(
            string displayName,
            string? description,
            bool optional,
            bool experimental,
            bool hidden,
            Version? maxGameVersion) : MicroPatchGroup
        {
            public override string DisplayName => displayName;
            public override string Description => description ?? base.Description;
            public override bool Optional => optional;
            public override bool Experimental => experimental;
            public override bool Hidden => hidden;
            public override Version? MaxGameVersion => maxGameVersion;
        }

        internal static UnityModManager.ModEntry.ModLogger Logger = null!;

        //internal static class Category
        //{
        //    internal const string Experimental = "Experimental";
        //    //internal const string Hidden = "Hidden";
        //    internal const string Optional = "Optional";
        //}

        private MicroPatch(PatchClassProcessor patch, Type patchClass, IPatchGroup group)
        {
            Patch = patch;
            PatchClass = patchClass;
            Group = group;
        }

        //public static MicroPatch FromGroupType<TGroup>(PatchClassProcessor patch, Type patchClass)
        //    where TGroup : IPatchGroup, new() =>
        //    new(patch, patchClass, new TGroup());

        public static MicroPatch FromType(PatchClassProcessor patch, Type patchClass)
        {
            if (patchClass.GetCustomAttribute<MicroPatchGroupAttribute>() is { } groupAttr)
            {
                return new(patch, patchClass, groupAttr.GroupInstance);
            }

            if (patchClass.GetCustomAttribute<MicroPatchAttribute>() is not { } attr)
            {
                Logger.Warning($"Missing {nameof(MicroPatchAttribute)} on patch type {patchClass.Name}");
                attr = new(patchClass.Name) { Hidden = true };
            }

            return new(patch, patchClass, new PatchGroup(
                displayName: attr.Name,
                description: attr.Description,
                optional: attr.Optional,
                experimental: attr.Experimental,
                hidden: attr.Hidden,
                maxGameVersion: attr.MaxGameVersion));
        }

        public PatchClassProcessor Patch { get; }
        public Type PatchClass { get; }

        public IPatchGroup Group { get; }

        public string DisplayName => Group.DisplayName;
        public string? Description => Group.Description;
        public bool IsHidden => Group.Hidden;

        public bool IsExperimental => Group.IsExperimental();
            //Patch.GetCategory() is Category.Experimental;
        public bool IsOptional => Group.IsOptional();
            //Patch.GetCategory() is Category.Optional or Category.Experimental;
        public Version? MaxGameVersion => Group.MaxGameVersion;
    }
}
