/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Represents a database view and its columns.
/// </summary>
public class DbMetaView : DbMetaObject
{
    // ● public
    /// <summary>
    /// Returns the view column names as a comma-separated list.
    /// </summary>
    public string GetFieldNameList() => string.Join(", " + Environment.NewLine, Columns.Select(Item => Item.Name));

    // ● properties
    /// <summary>
    /// Gets the Columns metadata.
    /// </summary>
    public List<DbMetaColumn> Columns { get; } = new();
}
