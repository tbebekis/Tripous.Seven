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

    // ● constructor
    /// <summary>
    /// Creates an empty column packet.
    /// </summary>
    public JsonDataColumn() { }
    /// <summary>
    /// Creates a column packet, preferring the logical type declared by the field definition.
    /// </summary>
    public JsonDataColumn(DataColumn Column, FieldDef Field = null)
    {
        ArgumentNullException.ThrowIfNull(Column);
        Name = Column.ColumnName;
        Title = Field != null ? Field.Title : Column.Caption;
        DataType = (int)(Field != null ? Field.DataType : GetDataType(Column));
        DefaultValue = Field != null ? Field.DefaultValue : Column.DefaultValue == DBNull.Value ? null : Column.DefaultValue;
        MaxLength = Column.DataType == typeof(string) ? Column.MaxLength : -1;
        Unique = Column.Unique;
    }

    // ● properties
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
    /// Gets or sets whether the column values must be unique.
    /// </summary>
    public bool Unique { get; set; }
}
