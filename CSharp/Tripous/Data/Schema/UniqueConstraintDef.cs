/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes a named unique constraint across one or more fields.
/// </summary>
public class UniqueConstraintDef
{
    // ● public
    /// <summary>
    /// Returns the SQL definition of the constraint.
    /// </summary>
    public string GetDefText()
    {
        DataTableDef.ValidateIdentifier(Name);
        string[] Names = FieldNames.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (Names.Length == 0)
            throw new InvalidOperationException("A unique constraint must specify at least one field.");
        foreach (string Field in Names)
            DataTableDef.ValidateIdentifier(Field);
        return $"CONSTRAINT {Name} UNIQUE ({string.Join(", ", Names)})";
    }

    // ● properties
    /// <summary>
    /// Gets or sets the constraint name.
    /// </summary>
    public string Name { get; set; } = "";
    /// <summary>
    /// Gets or sets the comma-separated field names.
    /// </summary>
    public string FieldNames { get; set; } = "";
}
