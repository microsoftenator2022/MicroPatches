using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MicroPatches
{
    [AttributeUsage(AttributeTargets.Class)]
    internal class MicroPatchAttribute(string name) : Attribute
    {
        public readonly string Name = name;
        public string Description = "";
    }
}
