/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous;

/// <summary>
/// Populates the public properties of an existing object from a JSON object.
/// </summary>
static internal class JsonPopulator
{
    // ● private types
    /// <summary>
    /// Holds a prepared property update until all values have been read.
    /// </summary>
    sealed class PropertyUpdate
    {
        // ● properties
        /// <summary>
        /// Gets or sets the serializer metadata for the property.
        /// </summary>
        public JsonPropertyInfo Property { get; set; }
        /// <summary>
        /// Gets or sets the value to assign or copy into the existing collection.
        /// </summary>
        public object Value { get; set; }
        /// <summary>
        /// Gets or sets the existing read-only collection when one is reused.
        /// </summary>
        public object Target { get; set; }
    }

    // ● private
    static object DefaultValue(Type Type) => Type.IsValueType ? Activator.CreateInstance(Type) : null;
    static object ReadValue(JsonElement Element, JsonPropertyInfo Property, JsonSerializerOptions Options)
    {
        JsonSerializerOptions LocalOptions = new(Options);
        if (Property.NumberHandling.HasValue)
            LocalOptions.NumberHandling = Property.NumberHandling.Value;
        if (Property.CustomConverter != null)
            LocalOptions.Converters.Insert(0, Property.CustomConverter);
        return JsonSerializer.Deserialize(Element.GetRawText(), Property.PropertyType, LocalOptions);
    }
    static bool IsMutableCollection(object Target)
    {
        if (Target is Array)
            return true;
        if (Target is IDictionary Dictionary)
            return !Dictionary.IsReadOnly && !Dictionary.IsFixedSize;
        if (Target is IList List)
            return !List.IsReadOnly && !List.IsFixedSize;
        return false;
    }
    static void ApplyCollection(object Target, object Value)
    {
        if (Target is Array TargetArray)
        {
            Array.Clear(TargetArray);
            if (Value is Array SourceArray)
                Array.Copy(SourceArray, TargetArray, SourceArray.Length);
        }
        else if (Target is IDictionary Dictionary)
        {
            Dictionary.Clear();
            if (Value is IDictionary Items)
            {
                foreach (DictionaryEntry Item in Items)
                    Dictionary.Add(Item.Key, Item.Value);
            }
        }
        else if (Target is IList List)
        {
            List.Clear();
            if (Value is IEnumerable Items)
            {
                foreach (object Item in Items)
                    List.Add(Item);
            }
        }
    }

    // ● internal
    /// <summary>
    /// Prepares property values before applying them to the existing object.
    /// A property setter or collection operation may still fail during application.
    /// </summary>
    static internal void PopulateObject(object Instance, JsonElement Element, JsonSerializerOptions Options)
    {
        if (Element.ValueKind != JsonValueKind.Object)
            throw new JsonException("Expected a JSON object.");

        JsonSerializerOptions MetadataOptions = new(Options);
        MetadataOptions.TypeInfoResolver ??= new DefaultJsonTypeInfoResolver();
        MetadataOptions.MakeReadOnly();
        JsonTypeInfo TypeInfo = MetadataOptions.GetTypeInfo(Instance.GetType());
        if (TypeInfo.Kind != JsonTypeInfoKind.Object)
            throw new NotSupportedException("PopulateObject requires object metadata for the root type.");

        StringComparer Comparer = MetadataOptions.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        Dictionary<string, JsonElement> Values = new(Comparer);
        foreach (JsonProperty Item in Element.EnumerateObject())
            Values[Item.Name] = Item.Value;

        List<PropertyUpdate> Updates = new();
        foreach (JsonPropertyInfo Property in TypeInfo.Properties)
        {
            if (Property.Set == null && Property.Get == null)
                continue;
            bool Found = Values.TryGetValue(Property.Name, out JsonElement JsonValue);
            if (Property.Set != null)
            {
                object Value = Found ? ReadValue(JsonValue, Property, MetadataOptions) : DefaultValue(Property.PropertyType);
                Updates.Add(new PropertyUpdate { Property = Property, Value = Value });
            }
            else
            {
                object Target = Property.Get(Instance);
                if (!IsMutableCollection(Target))
                    continue;
                object Value = Found && JsonValue.ValueKind != JsonValueKind.Null
                    ? ReadValue(JsonValue, Property, MetadataOptions) : null;
                if (Target is Array TargetArray && Value is Array SourceArray && SourceArray.Length > TargetArray.Length)
                    throw new JsonException($"JSON array is too long for read-only property '{Property.Name}'.");
                Updates.Add(new PropertyUpdate { Property = Property, Target = Target, Value = Value });
            }
        }

        foreach (PropertyUpdate Update in Updates)
        {
            if (Update.Property.Set != null)
                Update.Property.Set(Instance, Update.Value);
            else
                ApplyCollection(Update.Target, Update.Value);
        }
    }
}
