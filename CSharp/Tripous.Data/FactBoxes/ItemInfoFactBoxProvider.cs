/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// A simple FactBox provider that returns diagnostic item information.
/// </summary>
public class ItemInfoFactBoxProvider : ItemFactBoxProvider
{
    // ● public
    /// <summary>
    /// Creates serializable data for a FactBox.
    /// </summary>
    /// <param name="Context">The FactBox context.</param>
    /// <returns>The serializable FactBox data.</returns>
    public override object GetData(ItemFactBoxContext Context)
    {
        Dictionary<string, object> Result = new()
        {
            ["Module"] = Context?.ModuleDef?.Name ?? string.Empty,
            ["Table"] = Context?.TableDef?.Name ?? string.Empty,
            ["Key"] = Context?.KeyValue ?? string.Empty,
            ["RowState"] = Context?.RowState ?? string.Empty
        };
        return Result;
    }
}
