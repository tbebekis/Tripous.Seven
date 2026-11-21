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
    // ● private
    static string CreateConstraintName(string Name)
    {
        int MaxLength = DbConfig.GeneratedIdentifierMaxLength;
        const int HashLength = 12;
        if (MaxLength < HashLength + 2)
            throw new InvalidOperationException("Generated identifier length must be at least 14 characters.");

        bool IsAscii = Name.All(Ch => Ch <= 127);
        if (IsAscii && Name.Length <= MaxLength)
            return Name;

        string Digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Name.ToUpperInvariant()))).Substring(0, HashLength);
        string Prefix = new string(Name.Select(Ch => Ch <= 127 ? Ch : '_').ToArray());
        int PrefixLength = Math.Min(Prefix.Length, MaxLength - HashLength - 1);
        return Prefix.Substring(0, PrefixLength) + "_" + Digest;
    }
    static void CheckConstraintName(HashSet<string> Names, string Name)
    {
        ValidateIdentifier(Name);
        if (!Names.Add(Name))
            throw new InvalidOperationException($"Duplicate constraint name: {Name}");
    }

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
        HashSet<string> ConstraintNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (DataFieldDef Field in Fields)
            Definitions.Add(Field.GetDefText());
        foreach (DataFieldDef Field in Fields)
        {
            if (Field.Unique)
            {
                string ConstraintName = string.IsNullOrWhiteSpace(Field.UniqueConstraintName)
                    ? CreateConstraintName($"UQ_{Name}_{Field.Name}") : Field.UniqueConstraintName;
                CheckConstraintName(ConstraintNames, ConstraintName);
                Definitions.Add($"CONSTRAINT {ConstraintName} UNIQUE ({Field.Name})");
            }
        }
        foreach (UniqueConstraintDef Constraint in UniqueConstraints)
        {
            string[] FieldNames = Constraint.FieldNames.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            string ConstraintName = string.IsNullOrWhiteSpace(Constraint.Name)
                ? CreateConstraintName($"UQ_{Name}_{string.Join("_", FieldNames)}") : Constraint.Name;
            CheckConstraintName(ConstraintNames, ConstraintName);
            Definitions.Add(Constraint.GetDefText(ConstraintName));
        }
        foreach (DataFieldDef Field in Fields)
        {
            if (!string.IsNullOrWhiteSpace(Field.ForeignKey))
            {
                string ConstraintName = string.IsNullOrWhiteSpace(Field.ForeignKeyConstraintName)
                    ? CreateConstraintName($"FK_{Name}_{Field.Name}") : Field.ForeignKeyConstraintName;
                CheckConstraintName(ConstraintNames, ConstraintName);
                string[] Parts = Field.ForeignKey.Split('.', StringSplitOptions.TrimEntries);
                if (Parts.Length != 2)
                    throw new InvalidOperationException("A foreign key must specify Table.Field.");
                ValidateIdentifier(Parts[0]);
                ValidateIdentifier(Parts[1]);
                Definitions.Add($"CONSTRAINT {ConstraintName} FOREIGN KEY ({Field.Name}) REFERENCES {Parts[0]} ({Parts[1]})");
            }
        }
        return $"CREATE TABLE {Name} (\n    {string.Join(",\n    ", Definitions)}\n)";
    }

    /// <summary>
    /// Adds a field of the specified type and returns its definition.
    /// </summary>
    public DataFieldDef AddField(string FieldName, DataFieldType DataType, bool Required = false, string DefaultExpression = null)
    {
        DataFieldDef Result = new() { Name = FieldName, DataType = DataType, Required = Required, DefaultExpression = DefaultExpression };
        Fields.Add(Result);
        return Result;
    }
    /// <summary>
    /// Adds a primary key that uses the configured type unless set explicitly.
    /// </summary>
    public DataFieldDef AddId(string FieldName = "Id", int Length = 40)
    {
        DataFieldDef Result = new() { Name = FieldName, IsPrimaryKey = true, Length = Length };
        Fields.Add(Result);
        return Result;
    }
    /// <summary>
    /// Adds a string field.
    /// </summary>
    public DataFieldDef AddString(string FieldName, int Length = 96, bool Required = false, string DefaultExpression = null)
    {
        DataFieldDef Result = AddField(FieldName, DataFieldType.String, Required, DefaultExpression);
        Result.Length = Length;
        return Result;
    }
    /// <summary>
    /// Adds an integer field.
    /// </summary>
    public DataFieldDef AddInteger(string FieldName, bool Required = false, string DefaultExpression = null)
        => AddField(FieldName, DataFieldType.Integer, Required, DefaultExpression);
    /// <summary>
    /// Adds a floating-point field.
    /// </summary>
    public DataFieldDef AddFloat(string FieldName, bool Required = false, string DefaultExpression = null)
        => AddField(FieldName, DataFieldType.Double, Required, DefaultExpression);
    /// <summary>
    /// Adds a decimal field with explicit precision and scale.
    /// </summary>
    public DataFieldDef AddDecimal(string FieldName, string DecimalPart = "18, 4", bool Required = false, string DefaultExpression = null)
    {
        DataFieldDef Result = AddField(FieldName, DataFieldType.Decimal_, Required, DefaultExpression);
        Result.DecimalPart = DecimalPart;
        return Result;
    }
    /// <summary>
    /// Adds a date and time field.
    /// </summary>
    public DataFieldDef AddDateTime(string FieldName, bool Required = false, string DefaultExpression = null)
        => AddField(FieldName, DataFieldType.DateTime, Required, DefaultExpression);
    /// <summary>
    /// Adds a date field.
    /// </summary>
    public DataFieldDef AddDate(string FieldName, bool Required = false, string DefaultExpression = null)
        => AddField(FieldName, DataFieldType.Date, Required, DefaultExpression);
    /// <summary>
    /// Adds a Boolean field.
    /// </summary>
    public DataFieldDef AddBoolean(string FieldName, bool Required = false, string DefaultExpression = null)
        => AddField(FieldName, DataFieldType.Boolean, Required, DefaultExpression);
    /// <summary>
    /// Adds a binary field.
    /// </summary>
    public DataFieldDef AddBlob(string FieldName, bool Required = false)
        => AddField(FieldName, DataFieldType.Blob, Required);
    /// <summary>
    /// Adds a large text field.
    /// </summary>
    public DataFieldDef AddTextBlob(string FieldName, bool Required = false)
        => AddField(FieldName, DataFieldType.TextBlob, Required);
    /// <summary>
    /// Adds a unique constraint, generating its name when no name is specified.
    /// </summary>
    public UniqueConstraintDef AddUniqueConstraint(string FieldNames, string ConstraintName = "")
    {
        UniqueConstraintDef Result = new() { Name = ConstraintName, FieldNames = FieldNames };
        UniqueConstraints.Add(Result);
        return Result;
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
