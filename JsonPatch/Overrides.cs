using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Kingmaker.Blueprints;
using Kingmaker.ElementsSystem;
using Kingmaker.Globalmap.Blueprints.SectorMap;
using Kingmaker.UnitLogic.Progression.Paths;

using Microsoft.CodeAnalysis;

using MicroUtils.Linq;

using Newtonsoft.Json.Linq;

namespace MicroPatches;

public static partial class JsonPatch
{
    public static class Overrides
    {
        public static readonly List<Func<JProperty, bool>> IgnoreProperties =
        [
            p => p.Name == "PrototypeLink"
        ];

        public static bool IgnoreProperty(JProperty property) => IgnoreProperties.Apply(property).Any(Util.Id);

        static JToken IdentifyByName(JToken t)
        {
            if (t is not JObject o)
                return t;

            if (o["name"] is not { } name)
                return t;

            return JValue.CreateString(name.ToString());
        }

        public static bool IdentifyByIndex(Type t) => t switch
        {
            _ when t == typeof(BlueprintPath.RankEntry) => true,
            _ => false
        };

        public static readonly Dictionary<Type, Func<JToken, JToken>> ElementIdentities = new()
        {
            { typeof(Element), IdentifyByName },
            { typeof(BlueprintComponent), IdentifyByName },
            { typeof(BlueprintWarpRoutesSettings.DifficultySettings), static t => t is JObject o ? o["Difficulty"] ?? t : t },
        };

    }
}
