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
public class DataFieldDef : BaseDef
{
    // ● private fields
    DataFieldType? fDataType;

    // ● public
    /// <summary>
    /// Validates the field before generating its SQL definition.
    /// </summary>
    public void Check() => CheckDescriptor();
    /// <summary>
    /// Checks this field's definition independently of SQL generation.
    /// </summary>
    public override void CheckDescriptor()
    {
        DataTableDef.ValidateIdentifier(Name);
        base.CheckDescriptor();
        if (DataType == DataFieldType.None)
            throw new InvalidOperationException($"Field '{Name}' must have a data type.");
        if (!Enum.IsDefined(DataType))
            throw new InvalidOperationException($"Field '{Name}' must have a valid data type.");
        if (DataType == DataFieldType.String && Length < 1)
            throw new InvalidOperationException($"String field '{Name}' must have a positive length.");
        if (IsPrimaryKey && DataType != DataFieldType.String && DataType != DataFieldType.Integer && DataType != DataFieldType.Int64)
            throw new InvalidOperationException($"Primary key '{Name}' must use a string or integer field.");
        if (Unique && !string.IsNullOrWhiteSpace(UniqueConstraintName))
            DataTableDef.ValidateIdentifier(UniqueConstraintName);
        if (!string.IsNullOrWhiteSpace(ForeignKey))
        {
            string[] Parts = ForeignKey.Split('.', StringSplitOptions.TrimEntries);
            if (Parts.Length != 2)
                throw new InvalidOperationException("A foreign key must specify Table.Field.");
            DataTableDef.ValidateIdentifier(Parts[0]);
            DataTableDef.ValidateIdentifier(Parts[1]);
            if (!string.IsNullOrWhiteSpace(ForeignKeyConstraintName))
                DataTableDef.ValidateIdentifier(ForeignKeyConstraintName);
        }
    }
    /// <summary>
    /// Copies the field definition, including whether its type was explicitly set.
    /// </summary>
    public override void Assign(IDef Source)
    {
        ArgumentNullException.ThrowIfNull(Source);
        if (ReferenceEquals(this, Source))
            return;
        if (Source is not DataFieldDef Other)
            throw new ArgumentException("The source must be a DataFieldDef.", nameof(Source));
        Name = Other.fName;
        TitleKey = Other.fTitleKey;
        fDataType = Other.fDataType;
        Length = Other.Length;
        DecimalPart = Other.DecimalPart;
        Required = Other.Required;
        IsPrimaryKey = Other.IsPrimaryKey;
        DefaultExpression = Other.DefaultExpression;
        Unique = Other.Unique;
        UniqueConstraintName = Other.UniqueConstraintName;
        ForeignKey = Other.ForeignKey;
        ForeignKeyConstraintName = Other.ForeignKeyConstraintName;
        JsonLoaded();
    }
    /// <summary>
    /// Returns the portable SQL definition for the field.
    /// </summary>
    public string GetDefText(bool IncludeName = true)
    {
        Check();
        string TypeText = DataType switch
        {
            DataFieldType.String when Length > 0 => $"{SqlTypeTokens.CNVARCHAR}({Length})",
            DataFieldType.Integer => "integer",
            DataFieldType.Int64 => SqlTypeTokens.CINT64,
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
        string Default = string.IsNullOrWhiteSpace(DefaultExpression) ? "" : $" DEFAULT {DefaultExpression}";
        string Nullability = Required || IsPrimaryKey ? $" {SqlTypeTokens.CNOT_NULL}" : $" {SqlTypeTokens.CNULL}";
        string PrimaryKey = IsPrimaryKey ? " PRIMARY KEY" : "";
        return $"{(IncludeName ? Name + " " : "")}{TypeText}{Default}{Nullability}{PrimaryKey}";
    }

    /// <summary>
    /// Sets the foreign key reference and optional constraint name.
    /// </summary>
    public DataFieldDef SetForeignKey(string Value, string ConstraintName = "")
    {
        ForeignKey = Value;
        if (!string.IsNullOrWhiteSpace(ConstraintName))
            ForeignKeyConstraintName = ConstraintName;
        return this;
    }
    /// <summary>
    /// Sets whether the field is unique and optionally names the constraint.
    /// </summary>
    public DataFieldDef SetUnique(bool Value = true, string ConstraintName = "")
    {
        Unique = Value;
        if (!string.IsNullOrWhiteSpace(ConstraintName))
            UniqueConstraintName = ConstraintName;
        return this;
    }
    /// <summary>
    /// Sets whether the field is required.
    /// </summary>
    public DataFieldDef SetRequired(bool Value = true)
    {
        Required = Value;
        return this;
    }
    /// <summary>
    /// Sets the SQL default expression.
    /// </summary>
    public DataFieldDef SetDefaultExpression(string Value = null)
    {
        DefaultExpression = Value;
        return this;
    }
    /// <summary>
    /// Sets the maximum length of a string field.
    /// </summary>
    public DataFieldDef SetLength(int Value)
    {
        Length = Value;
        return this;
    }

    // ● properties
    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    public override string Name { get => fName ?? ""; set => base.Name = value; }
    /// <summary>
    /// Gets or sets the field type. An unspecified primary key uses the configured default.
    /// </summary>
    [JsonIgnore]
    public DataFieldType DataType
    {
        get => fDataType ?? (IsPrimaryKey ? DbConfig.DefaultPrimaryKeyType : DataFieldType.None);
        set => fDataType = value;
    }
    /// <summary>
    /// Gets or sets the explicitly configured type for JSON; null means no explicit type.
    /// </summary>
    [JsonPropertyName("DataType")]
    public DataFieldType? JsonDataType
    {
        get => fDataType;
        set => fDataType = value;
    }
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
