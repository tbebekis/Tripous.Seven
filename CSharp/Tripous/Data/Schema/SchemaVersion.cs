/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes one schema version, including structural statements to run after tables
/// are created, such as foreign keys and self-references.
/// </summary>
public class SchemaVersion
{
    // ● properties
    /// <summary>
    /// Gets or sets the schema version number.
    /// </summary>
    public int Version { get; set; }
    /// <summary>
    /// Gets the tables to create in this version.
    /// </summary>
    public List<SchemaItem> Tables { get; } = new();
    /// <summary>
    /// Gets the views to create in this version.
    /// </summary>
    public List<SchemaItem> Views { get; } = new();
    /// <summary>
    /// Gets statements to execute before creating tables and views.
    /// </summary>
    public List<string> StatementsBefore { get; } = new();
    /// <summary>
    /// Gets structural statements to execute after creating tables and views,
    /// such as foreign keys and self-references.
    /// </summary>
    public List<string> StatementsAfter { get; } = new();
}
