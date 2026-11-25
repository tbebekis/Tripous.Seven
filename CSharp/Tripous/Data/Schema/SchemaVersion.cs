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
    // ● internal
    internal Type DefinitionType { get; set; }

    // ● public
    /// <summary>
    /// Checks the version number and each registered table and view descriptor.
    /// </summary>
    public virtual void CheckDescriptor()
    {
        if (Version <= 0)
            throw new InvalidOperationException("A schema version number must be positive.");
        foreach (SchemaItem Item in Tables.Concat(Views))
        {
            if (Item == null)
                throw new InvalidOperationException("A schema item cannot be null.");
            Item.CheckDescriptor();
        }
    }

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
