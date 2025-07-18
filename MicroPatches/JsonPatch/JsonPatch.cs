﻿//#define ARRAY_PATCH_VERSION_2

using System;
using System.Collections.Generic;
using System.Linq;

using HarmonyLib;

using Kingmaker;
using Kingmaker.Blueprints;

using Microsoft.CodeAnalysis;

using MicroUtils.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Owlcat.Runtime.Core.Logging;

namespace MicroPatches;

public static partial class JsonPatch
{
    public static Optional<JToken> GetPatch(JToken value, JToken original, Type? type = null, string[][]? overridePaths = null)
    {
        if (value is JArray arrayValue &&
            original is JArray originalArrayValue)
        {
#if ARRAY_PATCH_VERSION_2
            return GetArrayMapPatch(arrayValue, originalArrayValue, type is not null ? Util.GetListTypeElementType(type) : null)
                .Upcast<JArray, JToken>();
#else
            return GetArrayPatch(arrayValue, originalArrayValue, type is not null ? Util.GetListTypeElementType(type) : null)
                .Upcast<JArray, JToken>();
#endif
        }

        if (value is JObject objectValue &&
            original is JObject originalObjectValue)
            return GetObjectPatch(objectValue, originalObjectValue, type, overridePaths)
                .Upcast<JObject, JToken>();

        //PFLog.Mods.DebugLog("value.DeepClone();");
        return value.DeepClone();
    }

    internal static Optional<JObject> GetObjectPatch(JObject value, JObject original, Type? objectType, string[][]? overridePaths = null)
    {
        var objectPatch = new JObject();

        objectType ??= Parser.GetObjectType(original) ?? Parser.GetObjectType(value);

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

            if (Parser.GetObjectTypeFromAttribute(value) is Type type)
            {
                propertyNameMap = Parser.GetPropertyMap(type);

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
            if (Overrides.IgnoreProperty(prop))
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

                var fieldType = Parser.GetFieldType(original, prop.Name, objectType) ??
                    Parser.GetFieldType(value, prop.Name, objectType);

                var propPatch = GetPatch(prop.Value, originalValue, fieldType, propertyOverrides);

                if (propPatch.HasValue)
                    //PFLog.Mods.DebugLog("objectPatch.Add(prop.Name, propPatch.Value.DeepClone());");
                    objectPatch.Add(prop.Name, propPatch.Value.DeepClone());
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

    static Optional<JArray> GetArrayPatch(JArray targetArray, JArray originalArray, Type? elementType)
    {
        elementType ??= Parser.GetArrayElementType(targetArray);

        JToken id(JToken e, int index)
        {
            if (elementType is not null && Overrides.IdentifiedByIndex(elementType))
                return index;

            return Parser.ElementIdentity(e, elementType);
        }

        bool equals(JToken a, JToken b, int index) => JToken.DeepEquals(id(a, index), id(b, index));

        var patches = new List<ArrayElementPatch>();

        //PFLog.Mods.DebugLog("var currentArray = (JArray)originalArray.DeepClone();");
        var currentArray = (JArray)originalArray.DeepClone();

        for (var i = 0; i < targetArray.Count; i++)
        {
            if (i < currentArray.Count && equals(currentArray[i], targetArray[i], i))
            {
                if (JToken.DeepEquals(currentArray[i], targetArray[i]))
                    continue;

                var elementPatch = GetPatch(targetArray[i], currentArray[i], elementType);

                if (elementPatch.HasValue)
                {
                    var op = new ArrayElementPatch.PatchElement(id(currentArray[i], i), elementPatch.Value);
                    patches.Add(op);

                    currentArray = op.Apply(currentArray, elementType, PFLog.Mods) ?? currentArray;
                }

                continue;
            }

            var elementCountDelta = Parser.GetElementCount(targetArray, targetArray[i], t => id(t, i)) - Parser.GetElementCount(currentArray, targetArray[i], t => id(t, i));

            if (elementCountDelta > 0)
            {
                if (i is 0)
                    patches.Add(new ArrayElementPatch.Prepend(targetArray[i]));
                else
                    patches.Add(new ArrayElementPatch.Insert(targetArray[i], id(targetArray[i - 1], i - 1)));
            }
            else if (i < currentArray.Count && !equals(currentArray[i], targetArray[i], i))
            {
                if (elementCountDelta == 0)
                    patches.Add(new ArrayElementPatch.Relocate(id(targetArray[i], i), i > 0 ? id(currentArray[i - 1], i - 1) : JValue.CreateNull()));
                else if (elementCountDelta < 0)
                {
                    patches.Add(new ArrayElementPatch.Remove(id(currentArray[i], i)));
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
        while (currentArray.Count > targetArray.Count)
        {
            var i = currentArray.Count - 1;

            var remove = new ArrayElementPatch.RemoveFromEnd(id(currentArray[i], i));
            patches.Add(remove);
            currentArray = remove.Apply(currentArray, elementType, PFLog.Mods);
        }

        if (!JToken.DeepEquals(currentArray, targetArray))
            PFLog.Mods.Warning("Arrays do not match:\n" + currentArray + "\n" + targetArray);

        if (patches.Count > 0)
        {
            var arrayPatches = new JArray();

            foreach (var patch in patches.Select(p => p.ToJson()).ToArray())
                arrayPatches.Add(patch);

            return arrayPatches;
        }

        return default;
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
            var objectPatchResult = PatchObject(objectValue, patchObject, logger, type ?? Parser.GetObjectType(objectValue));

            PFLog.Mods.DebugLog($"Object patch result:\n{objectPatchResult}");

            return objectPatchResult;
        }

        return patch;
    }

    internal static JObject PatchObject(JObject original, JObject patch, LogChannel logger, Type? objectType)
    {
        //PFLog.Mods.DebugLog("var value = (JObject)original.DeepClone();");
        var value = (JObject)original.DeepClone();

        objectType ??= Parser.GetObjectType(original);

        foreach (var patchProp in patch.Properties())
        {
            if (original[patchProp.Name] is { } originalValue)
                value[patchProp.Name] = PatchValue(originalValue, patchProp.Value, logger, Parser.GetFieldType(original, patchProp.Name, objectType));
            else
            {
                value[patchProp.Name] = patchProp.Value;
            }
        }

        return value;
    }

    internal static JArray PatchArray(JArray original, JArray patch, LogChannel logger, Type? elementType)
    {
        elementType ??= Parser.GetArrayElementType(original);

        logger.DebugLog(() => elementType is null, $"Could not get array element type for array:\n{original}", LogSeverity.Error);

        if (patch.Any(element => element["Version"] is JValue j && j.Value<int>() == 2))
        {
            return PatchMapArray(original, patch, logger, elementType);
        }

        var value = (JArray)original.DeepClone();

        foreach (var elementPatch in patch.OfType<JObject>().Select(ArrayElementPatch.FromJObject))
        {
            value = elementPatch.Apply(value, elementType, logger);
        }

        return value;
    }
    #region Old ArrayElementsPatch
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
            arrayElementType ??= Parser.GetArrayElementType(array);

            JToken id(JToken e) => Parser.ElementIdentity(e, arrayElementType);

            int indexOf(JToken t)
            {
                if (arrayElementType is not null &&
                    Overrides.IdentifiedByIndex(arrayElementType) &&
                    t.Type is JTokenType.Integer)
                    return (int)t;

                return Parser.IndexOf(array, t, id);
            }

            int lastIndexOf(JToken t)
            {
                if (arrayElementType is not null &&
                    Overrides.IdentifiedByIndex(arrayElementType) &&
                    t.Type is JTokenType.Integer)
                    return (int)t;

                return Parser.LastIndexOf(array, t, id);
            }

            var targetIndex = -1;
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
                    insertAfterIndex = indexOf(insert.InsertAfterTarget);

                    if (insertAfterIndex < 0)
                        throw new KeyNotFoundException($"Could not find {nameof(Insert.InsertAfterTarget)} for patch operation:\n{insert}\nin array:\n{array}");

                    array.Insert(insertAfterIndex + 1, insert.NewElement);
                    break;
                case Remove remove:
                    targetIndex = indexOf(remove.Target);
                    if (targetIndex < 0)
                    {
                        //throw new KeyNotFoundException();
                        logger.Warning($"Target element not found for array patch operation:\n{remove}");
                        break;
                    }

                    array.RemoveAt(targetIndex);

                    break;
                case RemoveFromEnd removeFromEnd:
                    targetIndex = lastIndexOf(removeFromEnd.Target);
                    if (targetIndex < 0)
                    {
                        //throw new KeyNotFoundException();
                        logger.Warning($"Target element not found for array patch operation:\n{removeFromEnd}");
                        break;
                    }

                    array.RemoveAt(targetIndex);
                    break;
                case PatchElement patchElement:
                    targetIndex = indexOf(patchElement.Target);
                    if (targetIndex < 0)
                        throw new KeyNotFoundException($"Could not find {nameof(PatchElement.Target)} for patch operation:\n{patchElement}\nin array:\n{array}");

                    array[targetIndex] = PatchValue(array[targetIndex], patchElement.ElementPatch, logger, arrayElementType);
                    break;
                case Relocate relocate:
                    targetIndex = indexOf(relocate.Target);
                    if (targetIndex < 0)
                        throw new KeyNotFoundException($"Could not find {nameof(Relocate.Target)} for patch operation:\n{relocate}\nin array:\n{array}");

                    if (relocate.InsertAfterTarget.Type is not JTokenType.Null)
                    {
                        insertAfterIndex = indexOf(relocate.InsertAfterTarget);
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
        #endregion
    }

    public class ArrayPatch
    {
        public enum ElementPatchType
        {
            Error = 0,
            None,
            NewElement,
            ElementPatch
        }

        public record class Element()
        {
            public const int Version = 2;
            public int TargetIndex { get; init; } = -1;
            public ElementPatchType PatchType { get; init; } = ElementPatchType.Error;
            public Optional<int> OriginalIndex { get; init; }
            public Optional<JToken> Value { get; init; }

            public static Element FromJson(JObject json)
            {
                var patchType = json[nameof(PatchType)]?.ToString() switch
                {
                    nameof(ElementPatchType.None) => ElementPatchType.None,
                    nameof(ElementPatchType.NewElement) => ElementPatchType.NewElement,
                    nameof(ElementPatchType.ElementPatch) => ElementPatchType.ElementPatch,
                    _ => ElementPatchType.Error
                };

                var targetIndex = Optional.OfNullable(json[nameof(TargetIndex)] as JValue).Map(jv => jv.Value<int>()).DefaultValue(-1);
                var originalIndex = Optional.OfNullable((json[nameof(OriginalIndex)] as JValue)).Map(jv => jv.Value<int>());
                var value = json[nameof(Value)];
                
                if (patchType is ElementPatchType.Error ||
                    patchType is not ElementPatchType.NewElement && value is null ||
                    patchType is ElementPatchType.None && value is not null)
                    return new();

                return new() { PatchType = patchType, OriginalIndex = originalIndex, Value = Optional.OfNullable(value) };
            }

            public JObject ToJson()
            {
                var obj = new JObject();

                obj["Version"] = 2;
                obj["TargetIndex"] = this.TargetIndex;
                obj[nameof(this.PatchType)] = Enum.GetName(typeof(ElementPatchType), this.PatchType);
                
                if (this.OriginalIndex.HasValue)
                    obj[nameof(this.OriginalIndex)] = this.OriginalIndex.Value;
                
                if (this.Value.HasValue)
                    obj[nameof(this.Value)] = this.Value.Value;

                return obj;

            }
        }
    }

    public static (Optional<int> originalIndex, JToken element)[] GetArrayElementsMap(JArray originalArray, JArray targetArray, Func<JToken, int, JToken> id)
    {
        var originalElementsGroups = originalArray.Indexed().GroupBy(element => id(element.item, element.index));
        var targetElementsGroups = targetArray.Indexed().GroupBy(element => id(element.item, element.index));

        var elementMap = new Optional<(Optional<int> originalIndex, JToken element)>[targetArray.Count];

        foreach (var group in targetElementsGroups)
        {
            var targetElements = group.ToArray();
            var originalElements = originalElementsGroups.FirstOrDefault(g => JToken.DeepEquals(g.Key, group.Key))?.ToArray() ?? [];

            if (originalElements.Length == 0)
                PFLog.Mods.DebugLog("No original elements?");

            for (var i = 0; i < targetElements.Length; i++)
            {
                var (targetIndex, targetElement) = targetElements[i];
                var originalIndex = default(Optional<int>);

                if (i < originalElements.Length)
                    originalIndex = originalElements[i].index;

                elementMap[targetIndex] = (originalIndex, targetElement);
            }
        }

        return elementMap.Select(x => x.Value).ToArray();
    }

    public static Optional<JArray> GetArrayMapPatch(JArray targetArray, JArray originalArray, Type? elementType)
    {
        elementType ??= Parser.GetArrayElementType(originalArray);

        JToken id(JToken e, int index)
        {
            if (elementType is not null && Overrides.IdentifiedByIndex(elementType))
                return index;

            return Parser.ElementIdentity(e, elementType);
        }

        var elementsMap = GetArrayElementsMap(originalArray, targetArray, id);

        var patchArray = new ArrayPatch.Element[elementsMap.Length];

        for (var i = 0; i < elementsMap.Length; i++)
        {
            var (originalIndex, element) = elementsMap[i];

            var patchElement = new ArrayPatch.Element() { TargetIndex = i, OriginalIndex = originalIndex };

            if (originalIndex.HasValue)
            {
                patchElement = patchElement with { PatchType = ArrayPatch.ElementPatchType.None };

                if (!JToken.DeepEquals(originalArray[originalIndex.Value], element))
                {
                    var elementPatch = GetPatch(targetArray[i], originalArray[originalIndex.Value]);

                    if (elementPatch.HasValue)
                    {
                        patchElement = patchElement with
                        { 
                            PatchType = ArrayPatch.ElementPatchType.ElementPatch,
                            Value = elementPatch
                        };
                    }
                }
            }
            else
            {
                patchElement = patchElement with { PatchType = ArrayPatch.ElementPatchType.NewElement, Value = element };
            }

            if (patchElement.PatchType is ArrayPatch.ElementPatchType.Error)
            {
                PFLog.Mods.Error($"Error creating patch for array element.\nOriginal:\n{originalArray[i]}\nTarget:{targetArray[i]}");
                return default;
            }

            patchArray[i] = patchElement;
        }

        if (patchArray.Indexed().All(p => p.item.PatchType is ArrayPatch.ElementPatchType.None && p.index == p.item.OriginalIndex.DefaultValue(-1)))
            return default;

        return new JArray(patchArray
            //.Where(p =>
            //    p is { PatchType: not ArrayPatch.ElementPatchType.None } ||
            //    p.TargetIndex != p.OriginalIndex.DefaultValue(-1))
            .Select(p => p.ToJson()).ToArray());
    }

    internal static JArray PatchMapArray(JArray original, JArray patch, LogChannel logger, Type? elementType)
    {


        var array = new JArray();

        for (var i = 0; i < patch.Count; i++)
        {
            void LogError() => logger.Error($"Unexpected value in patch array:\n{patch[i]}");

            if (patch[i] is not JObject o)
            {
                LogError();
                return original;
            }    

            var elementPatch = ArrayPatch.Element.FromJson(o);

            var element = elementPatch.PatchType switch
            {
                ArrayPatch.ElementPatchType.None => elementPatch.OriginalIndex.Map(originalIndex => original[originalIndex]),
                ArrayPatch.ElementPatchType.NewElement => elementPatch.Value,
                ArrayPatch.ElementPatchType.ElementPatch => elementPatch.Value.Map(value => ApplyPatch(original[i], value)),
                _ => default
            };

            if (!element.HasValue)
            {
                LogError();
                return original;
            }

            array[elementPatch.TargetIndex] = element.Value;
        }

        return array;
    }
}
