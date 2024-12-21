using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints.JsonSystem.Helpers;
using Kingmaker.ElementsSystem;
using Kingmaker.Globalmap.Blueprints.SectorMap;

using Microsoft.CodeAnalysis;

using MicroUtils.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Owlcat.Runtime.Core.Utility;

namespace MicroPatches;

public static class JsonPatch
{
    public static Action<string>? Logger;

    static void Log(string msg) => Logger?.Invoke(msg);

    public static Optional<JToken> GetPatch(JToken value, JToken original, string[][]? overridePaths = null)
    {
        if (value is JArray arrayValue &&
            original is JArray originalArrayValue)
            return GetArrayPatch(arrayValue, originalArrayValue)
                .Upcast<JArray, JToken>();

        if (value is JObject objectValue &&
            original is JObject originalObjectValue)
            return GetObjectPatch(objectValue, originalObjectValue, overridePaths)
                .Upcast<JObject, JToken>();
        
        //return !JToken.DeepEquals(value, original) ? Optional.Some(value.DeepClone()) : default;
        return value.DeepClone();
    }

    static bool IgnoreProperty(JProperty property)
    {
        return property.Name == "PrototypeLink";
    }

    static Dictionary<Type, string[]> MandatoryOverrides = [];

    internal static Optional<JObject> GetObjectPatch(JObject value, JObject original, string[][]? overridePaths = null)
    {
        var objectPatch = new JObject();

        var props = value.Properties();

        if (overridePaths is not null)
        {
            overridePaths = overridePaths.Where(p => p.Length > 0).ToArray();
            if (!overridePaths.Any())
                overridePaths = null;
        }

        if (value["m_Overrides"] is JArray overrideProps)
        {
            IEnumerable<(string Name, string? PropertyName)> propertyNameMap = [];

            if (GetType(value["$type"]?.ToString()) is Type type)
            {
                propertyNameMap = type.GetMembers()
                    .Choose(m => m.GetAttribute<JsonPropertyAttribute>() is { } attribute ? Optional.Some((m.Name, attribute.PropertyName)) : default);

                if (typeof(BlueprintScriptableObject).IsAssignableFrom(type))
                {
                    overrideProps = (JArray)overrideProps.DeepClone();
                    overrideProps.Add("Components");
                }
            }

            overridePaths = overrideProps.Select(n => n.ToString().Split(['.']))
                .Where(p => p.Length > 0)
                .Select(path =>
                {
                    var nameMap = propertyNameMap.TryFind(m => m.Name == path[0]);

                    if (nameMap.HasValue && nameMap.Value.PropertyName is not null)
                        return [nameMap.Value.PropertyName, .. path.Skip(1)];

                    return path;
                })
                .ToArray();

        }

        foreach (var prop in props)
        {
            if (IgnoreProperty(prop))
                continue;

            string[][]? propertyOverrides = null;
            if (overridePaths is not null)
            {
                propertyOverrides = overridePaths.Where(p => p[0] == prop.Name).Select(p => p.Skip(1).ToArray()).ToArray();

                if (propertyOverrides.Length < 1)
                    continue;
            }

            if (original[prop.Name] is JToken originalValue)
            {
                if (JToken.DeepEquals(originalValue, prop.Value))
                    continue;

                var propPatch = GetPatch(prop.Value, originalValue, propertyOverrides);

                if (propPatch.HasValue)
                    objectPatch.Add(prop.Name, propPatch.Value);
            }
            else
            {
                objectPatch.Add(prop.Name, prop.Value);
            }
        }

        if (objectPatch.Properties().Any())
            return objectPatch;

        return default;
    }

    static Type? GetType(string? typeString)
    {
        if (typeString is null)
            return null;

        if (typeString.Split([',']).FirstOrDefault() is not string guid)
            return null;

        if (GuidClassBinder.GuidToTypeCache.TryGetValue(guid, out var t))
            return t;

        return null;
    }

    static JToken IdentifyByName(JToken t)
    {
        if (t is not JObject o)
            return t;

        if (o["name"] is not { } name)
            return t;

        return JValue.CreateString(name.ToString());
    }

    static readonly Dictionary<Type, Func<JToken, JToken>> ElementIdentities = new()
    {
        { typeof(Element), IdentifyByName },
        { typeof(BlueprintComponent), IdentifyByName },
        { typeof(BlueprintWarpRoutesSettings.DifficultySettings), static t => t is JObject o ? o["Difficulty"] ?? t : t }
    };

    static JToken ElementIdentity(JToken element)
    {
        if (element is not JObject o)
            return element;

        if (GetType(o["$type"]?.ToString()) is { } elementType &&
            ElementIdentities.Keys.FirstOrDefault(t => t.IsAssignableFrom(elementType)) is { } type &&
            ElementIdentities.TryGetValue(type, out var identify))
        {
            return identify(element);
        }

        if (o["name"]?.ToString() is { } name)
        {
            PFLog.Mods.Warning("Patcher fallback");

            var t = GetType(o["$type"]?.ToString());

            if (typeof(BlueprintComponent).IsAssignableFrom(t) ||
                typeof(Element).IsAssignableFrom(t))
                return JValue.CreateString(name);
        }

        return element;
    }

    /// <summary>
    /// Count elements by identity
    /// </summary>
    static int GetElementCount(JArray array, JToken element, Func<JToken, JToken> identity) =>
        array
            .Select(identity)
            .Where(e => JToken.DeepEquals(ElementIdentity(element), ElementIdentity(e)))
            .Count();

    static Optional<JArray> GetArrayPatch(JArray targetArray, JArray originalArray)
    {
        var identity = ElementIdentity;
        
        static bool equals(JToken a, JToken b) => JToken.DeepEquals(ElementIdentity(a), ElementIdentity(b));

        var patches = new List<ArrayElementPatch>();

        var currentArray = (JArray)originalArray.DeepClone();

        for (var i = 0; i < targetArray.Count; i++)
        {
            if (i < currentArray.Count && equals(currentArray[i], targetArray[i]))
            {
                if (JToken.DeepEquals(currentArray[i], targetArray[i]))
                    continue;

                var elementPatch = GetPatch(targetArray[i], currentArray[i]);

                if (elementPatch.HasValue)
                {
                    var op = new ArrayElementPatch.PatchElement(identity(currentArray[i]), elementPatch.Value);
                    patches.Add(op);

                    currentArray = op.Apply(currentArray) ?? currentArray;
                }

                continue;
            }

            var elementCountDelta = GetElementCount(targetArray, targetArray[i], identity) - GetElementCount(currentArray, targetArray[i], identity);
            
            if (elementCountDelta > 0)
            {
                if (i is 0)
                    patches.Add(new ArrayElementPatch.Prepend(targetArray[i]));
                else
                    patches.Add(new ArrayElementPatch.Insert(targetArray[i], identity(targetArray[i - 1])));
            }
            else if (i < currentArray.Count && !equals(currentArray[i], targetArray[i]))
            {
                if (elementCountDelta == 0)
                {
                    patches.Add(new ArrayElementPatch.Relocate(identity(targetArray[i]), i > 0 ? identity(currentArray[i - 1]) : JValue.CreateNull()));
                }
                if (elementCountDelta < 0)
                {
                    var remove = new ArrayElementPatch.Remove(identity(currentArray[i]));
                    patches.Add(remove);
                    currentArray = remove.Apply(currentArray) ?? currentArray;
                }

                i--;
            }
            else
                throw new IndexOutOfRangeException();
                
            currentArray = patches.LastOrDefault()?.Apply(currentArray) ?? currentArray;
        }

        // Remove any excess items
        if (currentArray.Count > targetArray.Count)
        {
            for (var i = targetArray.Count; i < currentArray.Count; i++)
            {
                patches.Add(new ArrayElementPatch.RemoveFromEnd(identity(currentArray[i])));
            }
        }

        if (patches.Count > 0)
        {
            var arrayPatches = new JArray();

            foreach (var patch in patches.Select(p => p.ToJson()).ToArray())
                arrayPatches.Add(patch);

            return arrayPatches;
        }

        return default;
    }

    private static bool Contains(JArray array, JToken value, Func<JToken, JToken> identity) =>
        IndexOf(array, value, identity) >= 0;

    private static int IndexOf(JArray array, JToken value, Func<JToken, JToken> identity, int startFrom = 0)
    {
        var id = identity(value);

        for (var i = startFrom; i < array.Count; i++)
        {
            if (JToken.DeepEquals(identity(array[i]), id))
                return i;
        }

        return -1;
    }

    private static int LastIndexOf(JArray array, JToken value, Func<JToken, JToken> identity)
    {
        var id = identity(value);
        
        for (var i = array.Count - 1; i >= 0; i--)
        {
            if (JToken.DeepEquals(identity(array[i]), id))
                return i;
        }

        return -1;
    }

    public static JToken ApplyPatch(JToken value, JToken patch)
    {
        return PatchValue(value, patch);
    }

    internal static JToken PatchValue(JToken value, JToken patch)
    {
        if (value is JArray valueArray &&
            patch is JArray patchArray)
        {
            return PatchArray(valueArray, patchArray);
        }

        if (value is JObject objectValue &&
            patch is JObject patchObject)
            return PatchObject(objectValue, patchObject);

        return patch;
    }

    internal static JObject PatchObject(JObject value, JObject patch)
    {
        value = (JObject)value.DeepClone();

        foreach (var patchProp in patch.Properties())
        {
            if (value[patchProp.Name] is { } propValue)
            {
                value[patchProp.Name] = PatchValue(propValue, patchProp.Value);
            }
            else
            {
                value[patchProp.Name] = patchProp.Value;
            }
        }

        return value;
    }

    internal static JArray PatchArray(JArray value, JArray patch)
    {
        value = (JArray)value.DeepClone();

        foreach (var elementPatch in patch.OfType<JObject>().Select(ArrayElementPatch.FromJObject))
        {
#if DEBUG

            PFLog.Mods.Log($"Applying element patch:\n{elementPatch}");
            PFLog.Mods.Log($"Before:\n{value}");
#endif

            value = elementPatch.Apply(value);

#if DEBUG
            PFLog.Mods.Log($"After:\n{value}");
#endif
        }

        return value;
    }

    internal abstract record class ArrayElementPatch(string patchType)
    {
        public string PatchType => patchType;

        public JObject ToJson()
        {
            var obj = new JObject();
            obj.Add(nameof(this.PatchType), this.PatchType);

            switch (this)
            {
                case Prepend prepend:
                    obj.Add(nameof(Prepend.NewElement), prepend.NewElement);
                    break;
                case Append append:
                    obj.Add(nameof(Append.NewElement), append.NewElement);
                    break;
                case Insert insert:
                    obj.Add(nameof(Insert.NewElement), insert.NewElement);
                    obj.Add(nameof(Insert.InsertAfterTarget), insert.InsertAfterTarget);
                    break;
                case Remove remove:
                    obj.Add(nameof(Remove.Target), remove.Target);
                    break;
                case RemoveFromEnd removeFromEnd:
                    obj.Add(nameof(Remove.Target), removeFromEnd.Target);
                    break;
                case PatchElement patchElement:
                    obj.Add(nameof(PatchElement.Target), patchElement.Target);
                    obj.Add(nameof(PatchElement.ElementPatch), patchElement.ElementPatch);
                    break;
                case Relocate relocate:
                    obj.Add(nameof(Relocate.Target), relocate.Target);
                    obj.Add(nameof(Relocate.InsertAfterTarget), relocate.InsertAfterTarget ?? JValue.CreateNull());
                    break;
                default:
                    throw new NotSupportedException();
            }

            return obj;
        }

        public static ArrayElementPatch FromJObject(JObject jObject)
        {
            return jObject[nameof(PatchType)]?.ToString() switch
            {
                nameof(Prepend) => new Prepend(jObject[nameof(Prepend.NewElement)]!),
                nameof(Append) => new Append(jObject[nameof(Append.NewElement)]!),
                nameof(Insert) => new Insert(jObject[nameof(Insert.NewElement)]!, jObject[nameof(Insert.InsertAfterTarget)]!),
                nameof(Remove) => new Remove(jObject[nameof(Remove.Target)]!),
                nameof(RemoveFromEnd) => new RemoveFromEnd(jObject[nameof(RemoveFromEnd.Target)]!),
                nameof(PatchElement) => new PatchElement(jObject[nameof(PatchElement.Target)]!, jObject[nameof(PatchElement.ElementPatch)]!),
                nameof(Relocate) => new Relocate(jObject[nameof(Relocate.Target)]!, jObject[nameof(Relocate.InsertAfterTarget)]!),
                _ => throw new InvalidOperationException()
            };
        }

        public record class Prepend(JToken NewElement) : ArrayElementPatch(nameof(Prepend));
        public record class Append(JToken NewElement) : ArrayElementPatch(nameof(Append));
        public record class Insert(JToken NewElement, JToken InsertAfterTarget) : ArrayElementPatch(nameof(Insert));
        public record class Remove(JToken Target) : ArrayElementPatch(nameof(Remove));
        public record class PatchElement(JToken Target, JToken ElementPatch) : ArrayElementPatch(nameof(PatchElement));
        public record class Relocate(JToken Target, JToken InsertAfterTarget) : ArrayElementPatch(nameof(Relocate));
        public record class RemoveFromEnd(JToken Target) : ArrayElementPatch(nameof(RemoveFromEnd));

        public JArray Apply(JArray array)
        {
            int targetIndex = -1;
            var insertAfterIndex = -1;

            array = (JArray)array.DeepClone();

            switch (this)
            {
                case Prepend prepend:
                    array.Insert(0, prepend.NewElement);
                    break;
                case Append append:
                    array.Add(append.NewElement);
                    break;
                case Insert insert:
                    insertAfterIndex = IndexOf(array, insert.InsertAfterTarget, ElementIdentity);
                    if (insertAfterIndex < 0)
                        throw new KeyNotFoundException();

                    array.Insert(insertAfterIndex + 1, insert.NewElement);
                    break;
                case Remove remove:
                    targetIndex = IndexOf(array, remove.Target, ElementIdentity);
                    if (targetIndex < 0)
                        throw new KeyNotFoundException();

                    array.RemoveAt(targetIndex);

                    break;
                case RemoveFromEnd removeFromEnd:
                    targetIndex = LastIndexOf(array, removeFromEnd.Target, ElementIdentity);
                    if (targetIndex < 0)
                        throw new KeyNotFoundException();

                    array.RemoveAt(targetIndex);
                    break;
                case PatchElement patchElement:
                    targetIndex = IndexOf(array, patchElement.Target, ElementIdentity);
                    if (targetIndex < 0)
                        throw new KeyNotFoundException();

                    array[targetIndex] = PatchValue(array[targetIndex], patchElement.ElementPatch);
                    break;
                case Relocate relocate:
                    targetIndex = IndexOf(array, relocate.Target, ElementIdentity);
                    if (targetIndex < 0)
                        throw new KeyNotFoundException();

                    if (relocate.InsertAfterTarget.Type is not JTokenType.Null)
                    {
                        insertAfterIndex = IndexOf(array, relocate.InsertAfterTarget, ElementIdentity);
                        if (insertAfterIndex < 0)
                            throw new KeyNotFoundException();
                    }

                    var value = array[targetIndex];
                    array.RemoveAt(targetIndex);
                    array.Insert(insertAfterIndex + 1, value);
                    break;
                default:
                    throw new InvalidOperationException();
            }

            return array;
        }
    }
}
