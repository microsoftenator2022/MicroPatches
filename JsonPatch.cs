﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

using Newtonsoft.Json.Linq;

namespace MicroPatches;

public class JsonPatch
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

        return value.DeepClone();
    }

    static bool IgnoreProperty(JProperty property)
    {
        return property.Name == "PrototypeLink";
    }

    internal static Optional<JObject> GetObjectPatch(JObject value, JObject original, string[][]? overridePaths = null)
    {
        var objectPatch = new JObject();

        var props = value.Properties();

        if (value["m_Overrides"] is JArray overrideProps)
        {
            overridePaths = overrideProps.Select(n => n.ToString().Split(['.'])).Where(p => p.Length > 0).ToArray();
        }

        if (overridePaths is not null && (overridePaths.Length < 1 ||
            // FIXME: Sometimes the property names don't match the serialized field name.
            !overridePaths.Any(n => props.Any(p => p.Name == n[0]))))
            overridePaths = null;

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

    static JToken ElementIdentity(JToken element)
    {
        if (element is JObject o && o["name"]?.ToString() is { } name)
            return JValue.CreateString(name);

        return element;
    }

    static Optional<JArray> GetArrayPatch(JArray value, JArray originalArray)
    {
        var identity = ElementIdentity;

        var arrayPatch = new JArray();

        // Removed elements
        for (var i = 0; i < originalArray.Count; i++)
        {
            if (!Contains(value, originalArray[i], identity))
            {
                arrayPatch.Add(new ArrayElementPatch.Remove(identity(originalArray[i])).ToJson());
            }
        }

        //var insertions = new List<ArrayElementPatch.Insert>();
        //var relocations = new List<ArrayElementPatch.Relocate>();

        for (var i = 0; i < value.Count; i++)
        {
            var element = value[i];
            var originalElement = originalArray.FirstOrDefault(e => JToken.DeepEquals(identity(element), identity(e)));

            if (originalElement is null)
            {
                if (i is 0)
                    arrayPatch.Add(new ArrayElementPatch.Prepend(element).ToJson());
                else if (i > originalArray.Count)
                    arrayPatch.Add(new ArrayElementPatch.Append(element).ToJson());
                else
                {
                    //insertions.Add(new ArrayElementPatch.Insert(element, identity(value[i - 1])));
                    arrayPatch.Add(new ArrayElementPatch.Insert(element, identity(value[i - 1])).ToJson());
                }
            }
            else if (!JToken.DeepEquals(element, originalElement))
            {
                var elementPatch = GetPatch(element, originalElement);

                if (elementPatch.HasValue)
                    arrayPatch.Add(new ArrayElementPatch.PatchElement(identity(originalElement), elementPatch.Value).ToJson());
            }
        }

        if (arrayPatch.Count > 0)
            return arrayPatch;

        return default;
    }

    private static bool Contains(JArray array, JToken value, Func<JToken, JToken> identity) =>
        IndexOf(array, value, identity) >= 0;

    private static int IndexOf(JArray array, JToken value, Func<JToken, JToken> identity)
    {
        var id = identity(value);

        for (var i = 0; i < array.Count; i++)
        {
            if (JToken.DeepEquals(identity(array[i]), id))
                return i;
        }

        return -1;
    }

    internal static JToken PatchValue(JToken value, JToken patch)
    {
        value = (JValue)value.DeepClone();

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
            value = elementPatch.Apply(value);
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
                case PatchElement patchElement:
                    obj.Add(nameof(PatchElement.Target), patchElement.Target);
                    obj.Add(nameof(PatchElement.ElementPatch), patchElement.ElementPatch);
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
                nameof(PatchElement) => new PatchElement(jObject[nameof(PatchElement.Target)]!, jObject[nameof(PatchElement.ElementPatch)]!),
                //nameof(NestedArray) => throw new NotSupportedException(),
                nameof(Relocate) => throw new NotSupportedException(),
                _ => throw new InvalidOperationException()
            };
        }

        public record class Prepend(JToken NewElement) : ArrayElementPatch(nameof(Prepend));
        public record class Append(JToken NewElement) : ArrayElementPatch(nameof(Append));
        public record class Insert(JToken NewElement, JToken InsertAfterTarget) : ArrayElementPatch(nameof(Insert));
        public record class Remove(JToken Target) : ArrayElementPatch(nameof(Remove));
        public record class PatchElement(JToken Target, JToken ElementPatch) : ArrayElementPatch(nameof(PatchElement));
        //public record class NestedArray(ArrayElementPatch[] ArrayElementPatches, JToken Target) : ArrayElementPatch(nameof(NestedArray));
        public record class Relocate(JToken Target, JToken InsertAfterTarget) : ArrayElementPatch(nameof(Relocate));

        public JArray Apply(JArray array)
        {
            int targetIndex = -1;

            switch (this)
            {
                case Prepend prepend:
                    array.Insert(0, prepend.NewElement);
                    break;
                case Append append:
                    array.Add(append.NewElement);
                    break;
                case Insert insert:
                    targetIndex = IndexOf(array, insert.InsertAfterTarget, ElementIdentity);
                    if (targetIndex < 0)
                        throw new KeyNotFoundException();

                    array.Insert(targetIndex + 1, insert.NewElement);
                    break;
                case Remove remove:
                    targetIndex = IndexOf(array, remove.Target, ElementIdentity);
                    if (targetIndex < 0)
                        throw new KeyNotFoundException();

                    array.Remove(targetIndex);

                    break;
                case PatchElement patchElement:
                    targetIndex = IndexOf(array, patchElement.Target, ElementIdentity);
                    if (targetIndex < 0)
                        throw new KeyNotFoundException();

                    array[targetIndex] = PatchValue(array[targetIndex], patchElement.ElementPatch);
                    break;
                //case NestedArray nestedArray:
                //    throw new NotSupportedException();
                case Relocate relocate:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }

            return array;
        }
    }
}
