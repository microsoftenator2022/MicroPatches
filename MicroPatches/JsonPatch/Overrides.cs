using System;
using System.Collections.Generic;
using System.Linq;

using Kingmaker;
using Kingmaker.Blueprints;

using MicroUtils.Linq;

using Newtonsoft.Json.Linq;

namespace MicroPatches;

public static partial class JsonPatch
{
    //static JToken? MaybeProperty(this JToken token, string name)
    //{
    //    if (token is not JObject o)
    //        return null;

    //    return o[name];
    //}

    public static partial class Overrides
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

        public static bool IdentifiedByIndex(Type t) =>
            IndexIdentifiedTypes.Any(type => type.IsAssignableFrom(t));

        static JToken IdentifyByProperties(JToken obj, params string[] propertyNames)
        {
            if (obj is not JObject o)
                return obj;

            var identityObject = new JObject();

            foreach (var n in propertyNames)
            {
                if (o[n] is not { } t)
                    continue;

                identityObject[n] = t.DeepClone();
            }

            if (identityObject.Properties().Count() < 1)
            {
                // Maybe should throw here
                PFLog.Mods.Warning($"Identity has no values for JObject:\n{obj}");
                return obj;
            }

            if (identityObject.Properties().Count() < 2)
                return identityObject.Properties().First().Value!;

            return identityObject;
        }
    }
}
