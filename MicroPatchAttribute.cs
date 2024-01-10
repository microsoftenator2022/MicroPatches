using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using HarmonyLib;

using MicroPatches.Patches;

namespace MicroPatches
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class MicroPatchAttribute(string name) : Attribute
    {
        public readonly string Name = name;
        public string Description { get; init; } = "";
        
        public bool Experimental { get; init; } = false;
        public bool Optional { get; init; } = false;
        public bool Hidden { get; init; } = false;
        public Version? MaxGameVersion { get; init; } = null;
    }

    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class MicroPatchGroupAttribute(Type patchGroup) : Attribute
    {
        public Type PatchGroup = patchGroup;
        public MicroPatch.IPatchGroup GroupInstance => (MicroPatch.IPatchGroup)Activator.CreateInstance(PatchGroup);
    }
}
