/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Represents a database foreign key.
/// </summary>
public class DbMetaForeignKey : DbMetaConstraint
{
    // ● properties
    /// <summary>
    /// Gets or sets the ForeignTable metadata.
    /// </summary>
    public string ForeignTable { get; set; } = "";
    /// <summary>
    /// Gets or sets the ForeignFields metadata.
    /// </summary>
    public string ForeignFields { get; set; } = "";
    /// <summary>
    /// Gets or sets the UpdateRule metadata.
    /// </summary>
    public string UpdateRule { get; set; } = "";
    /// <summary>
    /// Gets or sets the DeleteRule metadata.
    /// </summary>
    public string DeleteRule { get; set; } = "";
    /// <summary>
    /// Gets a descriptive representation of the foreign key.
    /// </summary>
    public override string DisplayText => $"{Name} ({Columns}) REFERENCES {ForeignTable} ({ForeignFields})";
}
