/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Holds active list SELECT filters.
/// </summary>
public class JsonSelectFilters : List<JsonSelectFilter>
{
    // ● static public
    /// <summary>
    /// Converts a direct or JSON request value to a filter list.
    /// </summary>
    static public JsonSelectFilters From(object Value)
    {
        if (Value == null || Value is JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
            return new();
        if (Value is JsonSelectFilters Filters)
            return Filters;
        return Value is JsonElement Element
            ? Json.Deserialize<JsonSelectFilters>(Element.GetRawText()) ?? new()
            : Json.Deserialize<JsonSelectFilters>(Json.Serialize(Value)) ?? new();
    }
}
