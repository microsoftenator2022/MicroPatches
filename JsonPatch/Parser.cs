using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem.Helpers;
using Kingmaker.ElementsSystem;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Owlcat.Runtime.Core.Logging;
using Owlcat.Runtime.Core.Utility;

namespace MicroPatches;

public static partial class JsonPatch
{
    public static class Parser
    {
        internal static IEnumerable<(string Name, string PropertyName)> GetPropertyMap(Type type)
        {
            foreach (var m in type.GetAllMembers().Where(m =>
                m.Name is not null &&
                m.MemberType is MemberTypes.Field or MemberTypes.Property))
            {
                var attr = m.GetAttribute<JsonPropertyAttribute>();

                yield return (m.Name, attr?.PropertyName ?? m.Name);
            }
        }

        public static Type? GetType(string? typeString)
        {
            if (typeString is null)
                return null;

            if (typeString.Split([',']).FirstOrDefault() is not string guid)
                return null;

            if (GuidClassBinder.GuidToTypeCache.TryGetValue(guid, out var t))
                return t;

            return null;
        }

        public static Type? GetObjectTypeFromAttribute(JObject o) =>
            GetType(o["$type"]?.ToString());

        public static Type? GetObjectType(JObject o)
        {
            Type? objectType = null;
            if ((objectType = GetObjectTypeFromAttribute(o)) is null)
            {
                if (o.Parent is JProperty prop && prop.Parent is JObject parentObject)
                    objectType = GetFieldType(parentObject, prop.Name);

                else if (o.Parent is JArray parentArray)
                {
                    objectType = GetArrayElementType(parentArray);
                }

                if (objectType is null)
                {
                    PFLog.Mods.DebugLog($"Could not get parent for JObject:\n{o}", LogSeverity.Warning);
                    return null;
                }
            }

            PFLog.Mods.DebugLog($"Object type = {objectType}");

            return objectType;
        }

        public static Type? GetArrayElementType(JArray a)
        {
            Type? arrayType = null;

            if (a.Parent is JProperty prop && prop.Parent is JObject parentObject)
                arrayType = GetFieldType(parentObject, prop.Name);
            else if (a.Parent is JArray parentArray)
            {
                arrayType = GetArrayElementType(parentArray);
            }

            if (arrayType is null || Util.GetListTypeElementType(arrayType) is not { } elementType)
            {
                PFLog.Mods.DebugLog($"Could not get type for array:\n{a}", LogSeverity.Warning);
                return null;
            }

            PFLog.Mods.DebugLog($"Array element type = {elementType}");

            return elementType;
        }

        public static Type? GetFieldType(JObject o, string propertyName, Type? objectType = null)
        {
            if ((GetObjectType(o) ?? objectType) is null)
            {
                PFLog.Mods.Error($"Could not get object type for object:\n{o}");
                return null;
            }

            var propertyMap = Parser.GetPropertyMap(objectType!);

            var fieldName = propertyMap.FirstOrDefault(n => n.PropertyName == propertyName).Name;

            PFLog.Mods.DebugLog(() =>
                fieldName is null, $"fieldName is null. propertyName is '{propertyName}'.\nType is {objectType}\nPropertyMap:\n{string.Join("\n", propertyMap)}",
                severity: LogSeverity.Error);

            if (fieldName is null ||
                objectType!.GetAllFields().FirstOrDefault(f => f.Name == fieldName)
                is not FieldInfo field)
                return null;

            return field.FieldType;
        }

        public static JToken ElementIdentity(JToken element, Type? elementType)
        {
            if (element is not JObject o)
                return element;

            elementType ??= Parser.GetObjectType(o);

            if (elementType is not null &&
                //(elementType = GetType(o["$type"]?.ToString())) is not null &&
                Overrides.ElementIdentities.Keys.FirstOrDefault(t => t.IsAssignableFrom(elementType)) is { } type &&
                Overrides.ElementIdentities.TryGetValue(type, out var identify))
            {
                return identify(element);
            }

            if (o["name"]?.ToString() is { } name)
            {
                if (typeof(BlueprintComponent).IsAssignableFrom(elementType) ||
                    typeof(Element).IsAssignableFrom(elementType))
                {
                    PFLog.Mods.DebugLog($"Identifier for {elementType} fallback to old method. Check this.", LogSeverity.Warning);
                    return JValue.CreateString(name);
                }
            }

            return element;
        }

        /// <summary>
        /// Count elements by identity
        /// </summary>
        public static int GetElementCount(JArray array, JToken element, Func<JToken, JToken> identity) =>
            array
                .Select(identity)
                .Where(e => JToken.DeepEquals(identity(element), identity(e)))
                .Count();

        public static bool Contains(JArray array, JToken value, Func<JToken, JToken> identity) =>
        IndexOf(array, value, identity) >= 0;

        public static int IndexOf(JArray array, JToken value, Func<JToken, JToken> identity, int startFrom = 0)
        {
            var id = identity(value);

            for (var i = startFrom; i < array.Count; i++)
            {
                if (JToken.DeepEquals(identity(array[i]), id))
                    return i;
            }

            return -1;
        }

        public static int LastIndexOf(JArray array, JToken value, Func<JToken, JToken> identity)
        {
            var id = identity(value);

            for (var i = array.Count - 1; i >= 0; i--)
            {
                if (JToken.DeepEquals(identity(array[i]), id))
                    return i;
            }

            return -1;
        }

    }
}