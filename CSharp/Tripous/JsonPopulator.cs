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
    // ● private
    static object DefaultValue(Type Type) => Type.IsValueType ? Activator.CreateInstance(Type) : null;
    static bool IsIgnored(PropertyInfo Property)
    {
        JsonIgnoreAttribute Attribute = Property.GetCustomAttribute<JsonIgnoreAttribute>();
        return Attribute != null && Attribute.Condition == JsonIgnoreCondition.Always;
    }
    static void ResetProperty(object Instance, PropertyInfo Property)
    {
        if (Property.SetMethod != null && Property.SetMethod.IsPublic)
        {
            Property.SetValue(Instance, DefaultValue(Property.PropertyType));
        }
        else if (Property.GetMethod != null && Property.GetMethod.IsPublic)
        {
            object Value = Property.GetValue(Instance);
            if (Value is Array Array)
                System.Array.Clear(Array);
            else if (Value is IDictionary Dictionary)
                Dictionary.Clear();
            else if (Value is IList List && !List.IsReadOnly && !List.IsFixedSize)
                List.Clear();
        }
    }
    static object ReadValue(JsonElement Element, PropertyInfo Property, JsonSerializerOptions Options)
    {
        JsonSerializerOptions LocalOptions = new(Options);
        JsonNumberHandlingAttribute NumberHandling = Property.GetCustomAttribute<JsonNumberHandlingAttribute>();
        if (NumberHandling != null)
            LocalOptions.NumberHandling = NumberHandling.Handling;
        JsonConverterAttribute Converter = Property.GetCustomAttribute<JsonConverterAttribute>();
        if (Converter != null)
        {
            JsonConverter Instance = Converter.CreateConverter(Property.PropertyType);
            if (Instance == null && Converter.ConverterType != null)
                Instance = (JsonConverter)Activator.CreateInstance(Converter.ConverterType);
            if (Instance != null)
                LocalOptions.Converters.Insert(0, Instance);
        }
        return JsonSerializer.Deserialize(Element.GetRawText(), Property.PropertyType, LocalOptions);
    }
    static void PopulateReadOnlyCollection(object Target, JsonElement Element, PropertyInfo Property, JsonSerializerOptions Options)
    {
        if (Target is IList List && !List.IsReadOnly && !List.IsFixedSize)
        {
            if (ReadValue(Element, Property, Options) is IEnumerable Items)
            {
                foreach (object Item in Items)
                    List.Add(Item);
            }
        }
        else if (Target is IDictionary Dictionary)
        {
            if (ReadValue(Element, Property, Options) is IDictionary Items)
            {
                foreach (DictionaryEntry Item in Items)
                    Dictionary.Add(Item.Key, Item.Value);
            }
        }
    }

    // ● internal
    /// <summary>
    /// Resets eligible properties and fills those present in the JSON object.
    /// </summary>
    static internal void PopulateObject(object Instance, JsonElement Element, JsonSerializerOptions Options)
    {
        if (Element.ValueKind != JsonValueKind.Object)
            throw new JsonException("Expected a JSON object.");
        StringComparer Comparer = Options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        Dictionary<string, JsonElement> Values = new(Comparer);
        foreach (JsonProperty Item in Element.EnumerateObject())
            Values[Item.Name] = Item.Value;

        PropertyInfo[] Properties = Instance.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        foreach (PropertyInfo Property in Properties)
        {
            if (Property.GetIndexParameters().Length == 0 && !IsIgnored(Property))
                ResetProperty(Instance, Property);
        }
        foreach (PropertyInfo Property in Properties)
        {
            if (Property.GetIndexParameters().Length != 0 || IsIgnored(Property))
                continue;
            string Name = Property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? Options.PropertyNamingPolicy?.ConvertName(Property.Name) ?? Property.Name;
            if (!Values.TryGetValue(Name, out JsonElement Value))
                continue;
            if (Property.SetMethod != null && Property.SetMethod.IsPublic)
                Property.SetValue(Instance, ReadValue(Value, Property, Options));
            else if (Property.GetMethod != null && Property.GetMethod.IsPublic && Value.ValueKind != JsonValueKind.Null)
                PopulateReadOnlyCollection(Property.GetValue(Instance), Value, Property, Options);
        }
    }
}
