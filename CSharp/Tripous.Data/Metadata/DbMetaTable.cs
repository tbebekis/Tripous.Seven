/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Represents a database table and its metadata.
/// </summary>
public class DbMetaTable : DbMetaObject
{
    // ● public
    /// <summary>
    /// Returns the table column names as a comma-separated list.
    /// </summary>
    public string GetFieldNameList() => string.Join(", " + Environment.NewLine, Columns.Select(Item => Item.Name));
    /// <summary>
    /// Returns the original CREATE TABLE when available.
    /// Otherwise returns an illustrative, incomplete representation.
    /// </summary>
    public string GetCreateTable()
    {
        if (!string.IsNullOrWhiteSpace(SourceCode))
            return SourceCode;

        return $"-- Incomplete representation; constraints, defaults and other details may be missing.\nCREATE TABLE {Name} (\n    {string.Join(",\n    ", Columns.Select(Item => Item.DisplayText))}\n)";
    }

    // ● properties
    /// <summary>
    /// Gets the Columns metadata.
    /// </summary>
    public List<DbMetaColumn> Columns { get; } = new();
    /// <summary>
    /// Gets the ForeignKeys metadata.
    /// </summary>
    public List<DbMetaForeignKey> ForeignKeys { get; } = new();
    /// <summary>
    /// Gets the Constraints metadata.
    /// </summary>
    public List<DbMetaConstraint> Constraints { get; } = new();
    /// <summary>
    /// Gets the Indexes metadata.
    /// </summary>
    public List<DbMetaIndex> Indexes { get; } = new();
    /// <summary>
    /// Gets the Triggers metadata.
    /// </summary>
    public List<DbMetaTrigger> Triggers { get; } = new();
}
