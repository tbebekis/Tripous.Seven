/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes a database schema object and the SQL that creates it.
/// </summary>
public class SchemaItem
{
    // ● properties
    /// <summary>
    /// Gets or sets the name of the schema object.
    /// </summary>
    public string Name { get; set; } = "";
    /// <summary>
    /// Gets or sets the SQL statement that creates the schema object.
    /// </summary>
    public string SqlText { get; set; } = "";
}
