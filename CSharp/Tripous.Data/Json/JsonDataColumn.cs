/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes a table column in a JSON data packet.
/// </summary>
public class JsonDataColumn
{
    // ● private
    static DataFieldType GetDataType(DataColumn Column)
    {
        Type Type = Column.DataType;
        if (Type == typeof(string) || Type == typeof(char)) return DataFieldType.String;
        if (Type == typeof(byte) || Type == typeof(sbyte) || Type == typeof(short) || Type == typeof(ushort) || Type == typeof(int)) return DataFieldType.Integer;
        if (Type == typeof(uint) || Type == typeof(long)) return DataFieldType.Int64;
        if (Type == typeof(float) || Type == typeof(double)) return DataFieldType.Double;
        if (Type == typeof(decimal)) return DataFieldType.Decimal;
        if (Type == typeof(DateTime)) return DataFieldType.DateTime;
        if (Type == typeof(bool)) return DataFieldType.Boolean;
        if (Type == typeof(byte[])) return DataFieldType.Blob;
        throw new NotSupportedException($"Unsupported data type '{Type}' for column '{Column.ColumnName}'.");
    }

    /// <summary>
    /// Determines the display type from the logical data type and field flags.
    /// </summary>
    static DataColumnType GetColumnType(DataFieldType DataType, FieldDef Field)
    {
        if (DataType == DataFieldType.Boolean)
            return DataColumnType.Boolean;
        if (Field?.IsImage == true || Field?.IsImagePath == true)
            return DataColumnType.Image;
        if (Field?.IsMemo == true || Field?.IsLargeMemo == true || DataType == DataFieldType.TextBlob)
            return DataColumnType.Memo;
        return DataType switch
        {
            DataFieldType.String => DataColumnType.Text,
            DataFieldType.Integer or DataFieldType.Int64 => DataColumnType.Integer,
            DataFieldType.Double or DataFieldType.Decimal or DataFieldType.Decimal_ => DataColumnType.Decimal,
            DataFieldType.Date => DataColumnType.Date,
            DataFieldType.DateTime => DataColumnType.DateTime,
            _ => DataColumnType.None,
        };
    }

    // ● constructor
    /// <summary>
    /// Creates an empty column packet.
    /// </summary>
    public JsonDataColumn() { }
    /// <summary>
    /// Creates a column packet, preferring the logical type declared by the field definition.
    /// </summary>
    public JsonDataColumn(DataColumn Column, FieldDef Field = null, bool IncludeDefault = true)
    {
        ArgumentNullException.ThrowIfNull(Column);
        Name = Column.ColumnName;
        Title = Field != null ? Field.Title : Column.Caption;
        DataType = (int)(Field?.IsBoolean == true ? DataFieldType.Boolean : Field?.DataType ?? GetDataType(Column));
        Alias = Field == null ? "" : !IncludeDefault ? Name : (string.IsNullOrWhiteSpace(Field.Alias) ? Name : Field.Alias);
        TitleKey = Field?.TitleKey ?? "";
        Decimals = Field?.Decimals ?? -1;
        Flags = (int)(Field?.Flags ?? FieldFlags.None);
        ColumnType = (int)GetColumnType((DataFieldType)DataType, Field);
        DisplayFormat = Field?.DisplayFormat ?? "";
        EditFormat = Field?.EditFormat ?? "";
        DisplayWidth = Field?.DisplayWidth ?? 0;
        LookupSource = Field?.LookupSource ?? "";
        Locator = Field?.Locator ?? "";
        SnapshotOf = Field?.SnapshotOf ?? "";
        Group = Field?.Group ?? "General";
        ToolTip = Field?.ToolTip ?? "";
        if (IncludeDefault)
            DefaultValue = Field != null ? Field.DefaultValue : Column.DefaultValue == DBNull.Value ? null : Column.DefaultValue;
        MaxLength = Column.DataType == typeof(string) ? Column.MaxLength : -1;
        Unique = Column.Unique;
        IsNullable = Field?.IsNullable ?? Column.AllowDBNull;
        Expression = string.IsNullOrEmpty(Column.Expression) ? null : Column.Expression;
    }

    // ● properties
    /// <summary>
    /// Gets or sets the field alias used by the client.
    /// </summary>
    public string Alias { get; set; } = "";
    /// <summary>
    /// Gets or sets the resource key for the column title.
    /// </summary>
    public string TitleKey { get; set; } = "";
    /// <summary>
    /// Gets or sets the display precision, or minus one when unspecified.
    /// </summary>
    public int Decimals { get; set; } = -1;
    /// <summary>
    /// Gets or sets the numeric FieldFlags value.
    /// </summary>
    public int Flags { get; set; }
    /// <summary>
    /// Gets or sets the numeric DataColumnType value used for presentation.
    /// </summary>
    public int ColumnType { get; set; }
    /// <summary>
    /// Gets or sets the format for displaying the value.
    /// </summary>
    public string DisplayFormat { get; set; } = "";
    /// <summary>
    /// Gets or sets the format for editing the value.
    /// </summary>
    public string EditFormat { get; set; } = "";
    /// <summary>
    /// Gets or sets the suggested display width.
    /// </summary>
    public int DisplayWidth { get; set; }
    /// <summary>
    /// Gets or sets the name of the lookup source.
    /// </summary>
    public string LookupSource { get; set; } = "";
    /// <summary>
    /// Gets or sets the name of the locator.
    /// </summary>
    public string Locator { get; set; } = "";
    /// <summary>
    /// Gets or sets the lookup source field whose value is stored in this column.
    /// </summary>
    public string SnapshotOf { get; set; } = "";
    /// <summary>
    /// Gets or sets the display group.
    /// </summary>
    public string Group { get; set; } = "General";
    /// <summary>
    /// Gets or sets the tooltip text.
    /// </summary>
    public string ToolTip { get; set; } = "";
    /// <summary>
    /// Gets or sets the column name.
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// Gets or sets the column title.
    /// </summary>
    public string Title { get; set; }
    /// <summary>
    /// Gets or sets the numeric DataFieldType value.
    /// </summary>
    public int DataType { get; set; }
    /// <summary>
    /// Gets or sets the default value, if defined.
    /// </summary>
    public object DefaultValue { get; set; }
    /// <summary>
    /// Gets or sets the maximum text length, or -1 when unspecified.
    /// </summary>
    public int MaxLength { get; set; } = -1;
    /// <summary>
    /// Gets or sets whether the column can contain a null value.
    /// A projected LEFT JOIN column can be null when the lookup row is absent.
    /// </summary>
    public bool IsNullable { get; set; } = true;
    /// <summary>
    /// Gets or sets whether the column values must be unique.
    /// </summary>
    public bool Unique { get; set; }
    /// <summary>
    /// Gets or sets the in-memory expression associated with this column.
    /// </summary>
    public string Expression { get; set; }
}
