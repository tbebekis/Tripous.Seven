/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes a database table and produces a portable CREATE TABLE statement.
/// </summary>
public class DataTableDef
{
    // ● static public
    /// <summary>
    /// Validates a simple unquoted database identifier.
    /// </summary>
    static public void ValidateIdentifier(string Name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        if (!(char.IsLetter(Name[0]) || Name[0] == '_') || Name.Any(Ch => !(char.IsLetterOrDigit(Ch) || Ch == '_' || Ch == '$')))
            throw new ArgumentException($"Invalid database identifier: {Name}", nameof(Name));
    }

    // ● public
    /// <summary>
    /// Returns a CREATE TABLE statement containing Tripous SQL type placeholders.
    /// </summary>
    public string GetDefText()
    {
        ValidateIdentifier(Name);
        if (Fields.Count == 0)
            throw new InvalidOperationException("A table must contain at least one field.");
        List<string> Definitions = new();
        foreach (DataFieldDef Field in Fields)
            Definitions.Add(Field.GetDefText());
        foreach (DataFieldDef Field in Fields)
        {
            if (Field.Unique)
            {
                ValidateIdentifier(Field.UniqueConstraintName);
                Definitions.Add($"CONSTRAINT {Field.UniqueConstraintName} UNIQUE ({Field.Name})");
            }
        }
        foreach (UniqueConstraintDef Constraint in UniqueConstraints)
            Definitions.Add(Constraint.GetDefText());
        foreach (DataFieldDef Field in Fields)
        {
            if (!string.IsNullOrWhiteSpace(Field.ForeignKey))
            {
                ValidateIdentifier(Field.ForeignKeyConstraintName);
                string[] Parts = Field.ForeignKey.Split('.', StringSplitOptions.TrimEntries);
                if (Parts.Length != 2)
                    throw new InvalidOperationException("A foreign key must specify Table.Field.");
                ValidateIdentifier(Parts[0]);
                ValidateIdentifier(Parts[1]);
                Definitions.Add($"CONSTRAINT {Field.ForeignKeyConstraintName} FOREIGN KEY ({Field.Name}) REFERENCES {Parts[0]} ({Parts[1]})");
            }
        }
        return $"CREATE TABLE {Name} (\n    {string.Join(",\n    ", Definitions)}\n)";
    }

    // ● properties
    /// <summary>
    /// Gets or sets the table name.
    /// </summary>
    public string Name { get; set; } = "";
    /// <summary>
    /// Gets the table fields.
    /// </summary>
    public List<DataFieldDef> Fields { get; } = new();
    /// <summary>
    /// Gets the table-level unique constraints.
    /// </summary>
    public List<UniqueConstraintDef> UniqueConstraints { get; } = new();
}
