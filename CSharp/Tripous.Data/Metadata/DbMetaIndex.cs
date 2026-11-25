/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Represents a database index.
/// </summary>
public class DbMetaIndex : DbMetaObject
{
    // ● properties
    /// <summary>
    /// Gets or sets the IsUnique metadata.
    /// </summary>
    public bool IsUnique { get; set; }
    /// <summary>
    /// Gets or sets the IndexType metadata.
    /// </summary>
    public string IndexType { get; set; } = "";
    /// <summary>
    /// Gets or sets the Columns metadata.
    /// </summary>
    public string Columns { get; set; } = "";
    /// <summary>
    /// Gets a descriptive representation of the index.
    /// </summary>
    public override string DisplayText => $"{Name} ({Columns}){(IsUnique ? " UNIQUE" : "")}";
}
