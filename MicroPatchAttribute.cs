using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using HarmonyLib;

namespace MicroPatches
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class MicroPatchAttribute(string name) : Attribute
    {
        public readonly string Name = name;
        public string Description = "";
        public bool Hidden = false;
    }
}
