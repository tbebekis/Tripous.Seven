/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes a table field and produces its portable SQL definition.
/// </summary>
public class DataFieldDef
{
    // ● public
    /// <summary>
    /// Returns the portable SQL definition for the field.
    /// </summary>
    public string GetDefText(bool IncludeName = true)
    {
        DataTableDef.ValidateIdentifier(Name);
        string TypeText = DataType switch
        {
            DataFieldType.String when Length > 0 => $"{SqlTypeTokens.CNVARCHAR}({Length})",
            DataFieldType.Integer => "integer",
            DataFieldType.Double => SqlTypeTokens.CFLOAT,
            DataFieldType.Decimal => SqlTypeTokens.CDECIMAL,
            DataFieldType.Decimal_ => $"{SqlTypeTokens.CDECIMAL_}({DecimalPart})",
            DataFieldType.Date => SqlTypeTokens.CDATE,
            DataFieldType.DateTime => SqlTypeTokens.CDATE_TIME,
            DataFieldType.Boolean => SqlTypeTokens.CBOOL,
            DataFieldType.Blob => SqlTypeTokens.CBLOB,
            DataFieldType.TextBlob => SqlTypeTokens.CNBLOB_TEXT,
            _ => throw new InvalidOperationException($"Invalid field type or length for {Name}.")
        };
        if (IsPrimaryKey && DataType != DataFieldType.String && DataType != DataFieldType.Integer)
            throw new InvalidOperationException("Primary keys must use a string or integer field.");
        string Default = string.IsNullOrWhiteSpace(DefaultExpression) ? "" : $" DEFAULT {DefaultExpression}";
        string Nullability = Required || IsPrimaryKey ? $" {SqlTypeTokens.CNOT_NULL}" : $" {SqlTypeTokens.CNULL}";
        string PrimaryKey = IsPrimaryKey ? " PRIMARY KEY" : "";
        return $"{(IncludeName ? Name + " " : "")}{TypeText}{Default}{Nullability}{PrimaryKey}";
    }

    // ● properties
    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    public string Name { get; set; } = "";
    /// <summary>
    /// Gets or sets the field type.
    /// </summary>
    public DataFieldType DataType { get; set; }
    /// <summary>
    /// Gets or sets the maximum string length.
    /// </summary>
    public int Length { get; set; }
    /// <summary>
    /// Gets or sets the precision and scale for an explicit decimal type.
    /// </summary>
    public string DecimalPart { get; set; } = "18, 4";
    /// <summary>
    /// Gets or sets whether the field is required.
    /// </summary>
    public bool Required { get; set; }
    /// <summary>
    /// Gets or sets whether the field is the primary key.
    /// </summary>
    public bool IsPrimaryKey { get; set; }
    /// <summary>
    /// Gets or sets the SQL expression used for the default value.
    /// </summary>
    public string DefaultExpression { get; set; } = "";
    /// <summary>
    /// Gets or sets whether the field has a unique constraint.
    /// </summary>
    public bool Unique { get; set; }
    /// <summary>
    /// Gets or sets the unique constraint name.
    /// </summary>
    public string UniqueConstraintName { get; set; } = "";
    /// <summary>
    /// Gets or sets a referenced table and field in Table.Field form.
    /// </summary>
    public string ForeignKey { get; set; } = "";
    /// <summary>
    /// Gets or sets the foreign key constraint name.
    /// </summary>
    public string ForeignKeyConstraintName { get; set; } = "";
}
