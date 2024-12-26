using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints.JsonSystem.Helpers;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.ElementsSystem;
using Kingmaker.Globalmap.Blueprints.SectorMap;

using Microsoft.CodeAnalysis;

using MicroUtils.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Owlcat.Runtime.Core.Logging;
using Owlcat.Runtime.Core.Utility;

using UnityEngine;

namespace MicroPatches;

public static class JsonPatch
{
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
        
        //PFLog.Mods.DebugLog("value.DeepClone();");
        return value.DeepClone();
    }

    static bool IgnoreProperty(JProperty property)
    {
        return property.Name == "PrototypeLink";
    }

    private static IEnumerable<(string Name, string PropertyName)> GetPropertyMap(Type type)
    {
        foreach (var m in type.GetMembers(AccessTools.all).Where(m =>
            m.Name is not null &&
            m.MemberType is MemberTypes.Field or MemberTypes.Property))
        {
            var attr = m.GetAttribute<JsonPropertyAttribute>();

            yield return (m.Name, attr?.PropertyName ?? m.Name);
        }
    }

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
            IEnumerable<(string Name, string PropertyName)> propertyNameMap = [];

            if (GetObjectTypeFromAttribute(value) is Type type)
            {
                propertyNameMap = GetPropertyMap(type);

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

                    if (nameMap.HasValue && nameMap.Value.Name != nameMap.Value.PropertyName)
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
                {
                    //PFLog.Mods.DebugLog("objectPatch.Add(prop.Name, propPatch.Value.DeepClone());");
                    objectPatch.Add(prop.Name, propPatch.Value.DeepClone());
                }
            }
            else
            {
                //PFLog.Mods.DebugLog("objectPatch.Add(prop.Name, prop.Value.DeepClone());");
                objectPatch.Add(prop.Name, prop.Value.DeepClone());
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

    static Type? GetObjectTypeFromAttribute(JObject o) =>
        GetType(o["$type"]?.ToString());

    public static Type? GetObjectType(JObject o)
    {
        Type? objectType = null;
        if ((objectType = GetObjectTypeFromAttribute(o)) is null)
        {
            if (o.Parent is JProperty prop && prop.Parent is JObject parentObject)
            {
                //var parentType = GetObjectType(parentObject);

                objectType = GetFieldType(parentObject, prop.Name);
            }
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

    static Type? GetArrayElementType(JArray a)
    {
        Type? arrayType = null;

        if (a.Parent is JProperty prop && prop.Parent is JObject parentObject)
        {
            arrayType = GetFieldType(parentObject, prop.Name);
        }
        else if (a.Parent is JArray parentArray)
        {
            arrayType = GetArrayElementType(parentArray);
        }

        if (arrayType is null || Util.GetListTypeElementType(arrayType) is not { } elementType)
        {
            PFLog.Mods.Error($"Could not get type for array:\n{a}", LogSeverity.Warning);
            return null;
        }

        PFLog.Mods.DebugLog($"Array element type = {elementType}");

        return elementType;
    }

    public static Type? GetFieldType(JObject o, string propertyName, Type? objectType = null)
    {
        if ((objectType ??= GetObjectType(o)) is null)
        {
            PFLog.Mods.Error($"Could not get object type for object:\n{o}");
            return null;
        }

        var propertyMap = GetPropertyMap(objectType!);

        var fieldName = propertyMap.FirstOrDefault(n => n.PropertyName == propertyName).Name;

        if (fieldName is null ||
            objectType!.GetFields(AccessTools.all).FirstOrDefault(f => f.Name == fieldName)
            is not FieldInfo field)
            return null;

        return field.FieldType;
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

    static JToken ElementIdentity(JToken element, Type? elementType)
    {
        if (element is not JObject o)
            return element;
        
        elementType ??= GetObjectType(o);

        if (elementType is not null &&
            //(elementType = GetType(o["$type"]?.ToString())) is not null &&
            ElementIdentities.Keys.FirstOrDefault(t => t.IsAssignableFrom(elementType)) is { } type &&
            ElementIdentities.TryGetValue(type, out var identify))
        {
            return identify(element);
        }

        if (o["name"]?.ToString() is { } name)
        {
            //var t = GetType(o["$type"]?.ToString());

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
    static int GetElementCount(JArray array, JToken element, Func<JToken, JToken> identity) =>
        array
            .Select(identity)
            .Where(e => JToken.DeepEquals(identity(element), identity(e)))
            .Count();

    static Optional<JArray> GetArrayPatch(JArray targetArray, JArray originalArray)
    {
        var elementType = GetArrayElementType(targetArray);

        JToken id(JToken e) => ElementIdentity(e, elementType);
        bool equals(JToken a, JToken b) => JToken.DeepEquals(id(a), id(b));

        var patches = new List<ArrayElementPatch>();

        //PFLog.Mods.DebugLog("var currentArray = (JArray)originalArray.DeepClone();");
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
                    var op = new ArrayElementPatch.PatchElement(id(currentArray[i]), elementPatch.Value);
                    patches.Add(op);

                    currentArray = op.Apply(currentArray, elementType, PFLog.Mods) ?? currentArray;
                }

                continue;
            }

            var elementCountDelta = GetElementCount(targetArray, targetArray[i], id) - GetElementCount(currentArray, targetArray[i], id);
            
            if (elementCountDelta > 0)
            {
                if (i is 0)
                    patches.Add(new ArrayElementPatch.Prepend(targetArray[i]));
                else
                    patches.Add(new ArrayElementPatch.Insert(targetArray[i], id(targetArray[i - 1])));
            }
            else if (i < currentArray.Count && !equals(currentArray[i], targetArray[i]))
            {
                if (elementCountDelta == 0)
                {
                    patches.Add(new ArrayElementPatch.Relocate(id(targetArray[i]), i > 0 ? id(currentArray[i - 1]) : JValue.CreateNull()));
                }
                else if (elementCountDelta < 0)
                {
                    patches.Add(new ArrayElementPatch.Remove(id(currentArray[i])));
                }
                else
                    throw new InvalidOperationException();

                i--;
            }
            else
                throw new IndexOutOfRangeException();
                
            currentArray = patches.LastOrDefault()?.Apply(currentArray, elementType, PFLog.Mods) ?? currentArray;
        }

        // Remove any excess items
        if (currentArray.Count > targetArray.Count)
        {
            for (var i = targetArray.Count; i < currentArray.Count; i++)
            {
                patches.Add(new ArrayElementPatch.RemoveFromEnd(id(currentArray[i])));
            }
        }

        if (!JToken.DeepEquals(currentArray, targetArray))
        {
            PFLog.Mods.Warning("Arrays do not match:\n" + currentArray + "\n" + targetArray);
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

    public static JToken ApplyPatch(JToken value, JToken patch, LogChannel? logger = null)
    {
        return PatchValue(value, patch, logger ?? PFLog.Mods, null);
    }

    internal static JToken PatchValue(JToken value, JToken patch, LogChannel logger, Type? type)
    {
        PFLog.Mods.DebugLog($"Applying patch:\n{patch}\nto{value}");

        if (value is JArray valueArray &&
            patch is JArray patchArray)
        {
            var arrayElementType = type != null ? Util.GetListTypeElementType(type) : null;
            var arrayPatchResult = PatchArray(valueArray, patchArray, logger, arrayElementType);

            PFLog.Mods.DebugLog($"Array patch result:\n{arrayPatchResult}");

            return arrayPatchResult;
        }

        if (value is JObject objectValue &&
            patch is JObject patchObject)
        {
            var objectPatchResult = PatchObject(objectValue, patchObject, logger, type ?? GetObjectType(objectValue));

            PFLog.Mods.DebugLog($"Object patch result:\n{objectPatchResult}");

            return objectPatchResult;
        }

        return patch;
    }

    internal static JObject PatchObject(JObject original, JObject patch, LogChannel logger, Type? objectType)
    {
        //PFLog.Mods.DebugLog("var value = (JObject)original.DeepClone();");
        var value = (JObject)original.DeepClone();

        objectType ??= GetObjectType(original);

        foreach (var patchProp in patch.Properties())
        {
            if (original[patchProp.Name] is { } originalValue)
            {
                value[patchProp.Name] = PatchValue(originalValue, patchProp.Value, logger, GetFieldType(original, patchProp.Name, objectType));
            }
            else
            {
                value[patchProp.Name] = patchProp.Value;
            }
        }

        return value;
    }

    internal static JArray PatchArray(JArray original, JArray patch, LogChannel logger, Type? elementType)
    {
        elementType ??= GetArrayElementType(original);

        logger.DebugLog(() => elementType is null, $"Could not get array element type for array:\n{original}", LogSeverity.Error);

        //PFLog.Mods.DebugLog("var value = (JArray)original.DeepClone();");
        var value = (JArray)original.DeepClone();

        foreach (var elementPatch in patch.OfType<JObject>().Select(ArrayElementPatch.FromJObject))
        {
            //logger.DebugLog($"Applying element patch:\n{elementPatch}");
            //logger.DebugLog($"Before:\n{value}");

            value = elementPatch.Apply(value, elementType, logger);

            //logger.DebugLog($"After:\n{value}");
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

        public JArray Apply(JArray array, Type? arrayElementType, LogChannel logger)
        {
            arrayElementType ??= GetArrayElementType(array);

            JToken id(JToken e) => ElementIdentity(e, arrayElementType);

            var targetIndex = -1;
            var insertAfterIndex = -1;
            
            //PFLog.Mods.DebugLog("array = (JArray)array.DeepClone();");
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
                    insertAfterIndex = IndexOf(array, insert.InsertAfterTarget, id);
                    if (insertAfterIndex < 0)
                        throw new KeyNotFoundException($"Could not find {nameof(Insert.InsertAfterTarget)} for patch operation:\n{insert}\nin array:\narray");

                    array.Insert(insertAfterIndex + 1, insert.NewElement);
                    break;
                case Remove remove:
                    targetIndex = IndexOf(array, remove.Target, id);
                    if (targetIndex < 0)
                    {
                        //throw new KeyNotFoundException();
                        logger.Warning($"Target element not found for array patch operation:\n{remove}");
                        break;
                    }

                    array.RemoveAt(targetIndex);

                    break;
                case RemoveFromEnd removeFromEnd:
                    targetIndex = LastIndexOf(array, removeFromEnd.Target, id);
                    if (targetIndex < 0)
                    {
                        //throw new KeyNotFoundException();
                        logger.Warning($"Target element not found for array patch operation:\n{removeFromEnd}");
                        break;
                    }

                    array.RemoveAt(targetIndex);
                    break;
                case PatchElement patchElement:
                    targetIndex = IndexOf(array, patchElement.Target, id);
                    if (targetIndex < 0)
                        throw new KeyNotFoundException($"Could not find {nameof(PatchElement.Target)} for patch operation:\n{patchElement}\nin array:\n{array}");

                    array[targetIndex] = PatchValue(array[targetIndex], patchElement.ElementPatch, logger, arrayElementType);
                    break;
                case Relocate relocate:
                    targetIndex = IndexOf(array, relocate.Target, id);
                    if (targetIndex < 0)
                        throw new KeyNotFoundException($"Could not find {nameof(Relocate.Target)} for patch operation:\n{relocate}\nin array:\n{array}");

                    if (relocate.InsertAfterTarget.Type is not JTokenType.Null)
                    {
                        insertAfterIndex = IndexOf(array, relocate.InsertAfterTarget, id);
                        if (insertAfterIndex < 0)
                            throw new KeyNotFoundException($"Could not find {nameof(Relocate.InsertAfterTarget)} for patch operation:\n{relocate}\nin array:\n{array}");
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
