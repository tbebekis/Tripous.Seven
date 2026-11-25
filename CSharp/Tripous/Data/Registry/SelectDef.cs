/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes a named SELECT statement.
/// </summary>
public class SelectDef : BaseDef
{
    // ● private fields
    string fSqlText;
    SqlFilterDefs fFilterDefs;

    // ● private
    static DataFieldType GetFilterDataType(Type Type)
    {
        if (Type == typeof(string) || Type == typeof(char)) return DataFieldType.String;
        if (Type == typeof(bool)) return DataFieldType.Boolean;
        if (Type == typeof(byte) || Type == typeof(sbyte) || Type == typeof(short) || Type == typeof(ushort) || Type == typeof(int)) return DataFieldType.Integer;
        if (Type == typeof(uint) || Type == typeof(long)) return DataFieldType.Int64;
        if (Type == typeof(float) || Type == typeof(double)) return DataFieldType.Double;
        if (Type == typeof(decimal)) return DataFieldType.Decimal;
        if (Type == typeof(DateTime)) return DataFieldType.DateTime;
        return DataFieldType.None;
    }

    // ● public
    /// <summary>
    /// Checks this SELECT descriptor and its filter definitions.
    /// SQL may be supplied by the consumer or inferred for a stock table.
    /// </summary>
    public override void CheckDescriptor()
    {
        base.CheckDescriptor();
        FilterDefs.CheckDescriptors();
    }
    /// <summary>
    /// Adds a filter definition for this SELECT.
    /// </summary>
    public SqlFilterDef AddFilter(string Name, string FieldName = null, DataFieldType FilterDataType = DataFieldType.String, BoolOp BoolOp = BoolOp.And, ConditionOp ConditionOp = ConditionOp.Equal, string TitleKey = null)
        => FilterDefs.Add(Name, FieldName, FilterDataType, BoolOp, ConditionOp, TitleKey);
    /// <summary>
    /// Adds a filter using a field definition's name, data type and title key.
    /// </summary>
    public SqlFilterDef AddFilter(FieldDef FieldDef)
    {
        ArgumentNullException.ThrowIfNull(FieldDef);
        return AddFilter(FieldDef.Name, FieldDef.Name, FieldDef.DataType, TitleKey: FieldDef.TitleKey);
    }
    /// <summary>
    /// Creates suggested filters from SELECT result metadata using Avalon's common column names.
    /// Returns a new collection and does not change FilterDefs. Types are determined by the driver;
    /// an integer database column cannot by itself identify a logical Boolean field.
    /// </summary>
    public SqlFilterDefs DefineFilters(SqlStore Store, SqlParams Params = null)
    {
        ArgumentNullException.ThrowIfNull(Store);
        string[] Names = { "Code", "Name", "Description", "LastName", "FirstName", "Product", "Customer", "Country", "City", "Date", "Amount", "Price" };
        ReadOnlyCollection<DbColumn> Schema = Store.GetSelectSchema(SqlText, Params ?? new SqlParams());
        var Columns = Schema.Where(Column => Names.Contains(Column.ColumnName, StringComparer.OrdinalIgnoreCase)
            && GetFilterDataType(Column.DataType) != DataFieldType.None
            && !(Column.DataType == typeof(string) && Column.ColumnSize > 256))
            .OrderBy(Column => !string.Equals(Column.ColumnName, "Name", StringComparison.OrdinalIgnoreCase))
            .ThenBy(Column => Column.ColumnName, StringComparer.OrdinalIgnoreCase);
        SqlFilterDefs Result = new();
        foreach (DbColumn Column in Columns)
            Result.Add(Column.ColumnName, Column.ColumnName, GetFilterDataType(Column.DataType), TitleKey: Column.ColumnName);
        return Result;
    }

    // ● properties
    /// <summary>
    /// Gets or sets the filters defined for this SELECT.
    /// </summary>
    public SqlFilterDefs FilterDefs
    {
        get => fFilterDefs ??= new();
        set
        {
            if (ReferenceEquals(fFilterDefs, value))
                return;
            fFilterDefs = value;
            NotifyPropertyChanged(nameof(FilterDefs));
        }
    }
    /// <summary>
    /// Gets or sets the module definition that owns this SELECT.
    /// </summary>
    [JsonIgnore]
    public ModuleDef Owner { get; set; }
    /// <summary>
    /// Gets or sets the SELECT SQL statement.
    /// </summary>
    public string SqlText
    {
        get => fSqlText;
        set
        {
            if (fSqlText == value)
                return;
            fSqlText = value;
            NotifyPropertyChanged(nameof(SqlText));
        }
    }
}
