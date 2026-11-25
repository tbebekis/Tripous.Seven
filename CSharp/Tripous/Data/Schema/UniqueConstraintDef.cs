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
public class UniqueConstraintDef : BaseDef
{
    // ● public
    /// <summary>
    /// Checks the optional name and the required field names.
    /// </summary>
    public override void CheckDescriptor()
    {
        if (!string.IsNullOrWhiteSpace(Name))
            DataTableDef.ValidateIdentifier(Name);
        string[] Names = (FieldNames ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (Names.Length == 0)
            throw new InvalidOperationException("A unique constraint must specify at least one field.");
        foreach (string Field in Names)
            DataTableDef.ValidateIdentifier(Field);
    }
    /// <summary>
    /// Returns the SQL definition of the constraint.
    /// </summary>
    public string GetDefText() => GetDefText(Name);
    /// <summary>
    /// Returns the SQL definition using the specified constraint name.
    /// </summary>
    public string GetDefText(string ConstraintName)
    {
        CheckDescriptor();
        DataTableDef.ValidateIdentifier(ConstraintName);
        string[] Names = FieldNames.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return $"CONSTRAINT {ConstraintName} UNIQUE ({string.Join(", ", Names)})";
    }

    // ● properties
    /// <summary>
    /// Gets or sets the constraint name.
    /// </summary>
    public override string Name { get => fName ?? ""; set => base.Name = value; }
    /// <summary>
    /// Gets or sets the comma-separated field names.
    /// </summary>
    public string FieldNames { get; set; } = "";
}
