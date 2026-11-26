/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes a table, its columns and its rows in a JSON data packet.
/// </summary>
public class JsonDataTable
{
    // ● private
    static object ConvertValue(object Value, Type DataType)
    {
        if (Value == null || Value == DBNull.Value)
            return DBNull.Value;
        if (Value is JsonElement Element)
        {
            if (Element.ValueKind == JsonValueKind.Null || Element.ValueKind == JsonValueKind.Undefined)
                return DBNull.Value;
            if (DataType == typeof(string))
                return Element.ValueKind == JsonValueKind.String ? Element.GetString() : Element.ToString();
            if (DataType == typeof(byte[]))
                return Element.GetBytesFromBase64();
            if (DataType == typeof(DateTime))
                return Element.GetDateTime();
            if (Element.ValueKind == JsonValueKind.True || Element.ValueKind == JsonValueKind.False)
            {
                bool Boolean = Element.GetBoolean();
                if (DataType == typeof(int)) return Boolean ? 1 : 0;
                if (DataType == typeof(long)) return Boolean ? 1L : 0L;
                if (DataType == typeof(bool)) return Boolean;
            }
            if (Element.ValueKind == JsonValueKind.String)
                Value = Element.GetString();
            else if (DataType == typeof(int)) return Element.GetInt32();
            else if (DataType == typeof(long)) return Element.GetInt64();
            else if (DataType == typeof(double)) return Element.GetDouble();
            else if (DataType == typeof(decimal)) return Element.GetDecimal();
            else if (DataType == typeof(bool)) return Element.GetInt32() != 0;
            else Value = Element.ToString();
        }
        if (DataType == typeof(byte[]) && Value is byte[] Bytes)
            return Bytes;
        if (DataType == typeof(byte[]) && Value is string Text)
            return Convert.FromBase64String(Text);
        if (DataType == typeof(int) && Value is bool Flag)
            return Flag ? 1 : 0;
        if (DataType == typeof(long) && Value is bool LongFlag)
            return LongFlag ? 1L : 0L;
        if (DataType == typeof(bool) && Value is not string && Value is IConvertible)
            return Convert.ToInt64(Value, System.Globalization.CultureInfo.InvariantCulture) != 0;
        return Convert.ChangeType(Value, DataType, System.Globalization.CultureInfo.InvariantCulture);
    }
    static object[] ConvertRow(JsonDataRow Row, DataTable Table)
    {
        if (Row == null || Row.Data == null || Row.Data.Length != Table.Columns.Count)
            throw new InvalidOperationException($"A row in table '{Table.TableName}' must have exactly {Table.Columns.Count} values.");
        object[] Values = new object[Table.Columns.Count];
        for (int Index = 0; Index < Values.Length; Index++)
        {
            DataColumn Column = Table.Columns[Index];
            if (string.IsNullOrEmpty(Column.Expression))
                Values[Index] = ConvertValue(Row.Data[Index], Column.DataType);
        }
        return Values;
    }
    static DataRow AddRow(DataTable Table, object[] Values, DataRowState State)
    {
        DataRow Row = Table.NewRow();
        Row.ItemArray = Values;
        Table.Rows.Add(Row);
        Row.AcceptChanges();
        if (State == DataRowState.Added)
            Row.SetAdded();
        else if (State == DataRowState.Modified)
            Row.SetModified();
        else if (State == DataRowState.Deleted)
            Row.Delete();
        return Row;
    }

    // ● constructor
    /// <summary>
    /// Creates an empty table packet.
    /// </summary>
    public JsonDataTable() { }
    /// <summary>
    /// Creates a table packet, using field definitions for logical column types when available.
    /// </summary>
    public JsonDataTable(DataTable Source, TableDef Definition = null, SelectDef Select = null)
    {
        ArgumentNullException.ThrowIfNull(Source);
        Name = Source.TableName;
        IsOneToOne = Definition?.IsOneToOne ?? false;
        IsUiVisible = Definition?.IsUiVisible ?? true;
        if (Definition?.FieldGroups != null)
            FieldGroups.AddRange(Definition.FieldGroups);
        if (Source is MemTable Table)
        {
            KeyField = Table.KeyField;
            MasterField = Table.MasterField;
            DetailField = Table.DetailField;
            MasterTableName = Table.Master != null ? Table.Master.TableName : string.Empty;
            AutoGenerateGuidKeys = Table.AutoGenerateGuidKeys;
            Details.AddRange(Table.Details.Select(Detail => Detail.TableName));
        }
        else if (Definition != null)
        {
            KeyField = Definition.KeyField;
            MasterField = Definition.MasterField;
            DetailField = Definition.DetailField;
            MasterTableName = Definition.Master != null ? Definition.Master.Name : string.Empty;
            Details.AddRange(Definition.Details.Select(Detail => Detail.Name));
        }
        foreach (DataColumn Column in Source.Columns)
        {
            FieldDef Field = Definition?.FindColumnField(Column.ColumnName);
            bool IsJoinColumn = Field != null && !ReferenceEquals(Definition.FindField(Column.ColumnName), Field);
            JsonDataColumn Packet = new(Column, Field, IncludeDefault: !IsJoinColumn);
            if (IsJoinColumn)
                Packet.IsNullable = true;
            if (Select?.DisplayLabels?.TryGetValue(Column.ColumnName, out string TitleKey) == true)
            {
                Packet.TitleKey = TitleKey;
                Packet.Title = Texts.L(TitleKey);
            }
            if (Select?.ColumnTypes?.TryGetValue(Column.ColumnName, out DataColumnType ColumnType) == true)
                Packet.ColumnType = (int)ColumnType;
            Columns.Add(Packet);
        }
        foreach (DataRow Row in Source.Rows)
        {
            if (Row.RowState == DataRowState.Deleted)
                Deleted.Add(new JsonDataRow(Row));
            else
                Rows.Add(new JsonDataRow(Row));
        }
    }

    // ● public
    /// <summary>
    /// Adds packet rows to an existing table and restores their states.
    /// Each row must contain exactly one value for every target column.
    /// </summary>
    public void RowsTo(DataTable Table) => RowsTo(Table, null);

    // ● internal
    /// <summary>
    /// Restores rows, optionally deferring deleted states until a tree is reconstructed.
    /// </summary>
    internal void RowsTo(DataTable Table, List<DataRow> DeferredDeletes)
    {
        ArgumentNullException.ThrowIfNull(Table);
        List<(object[] Values, DataRowState State)> Prepared = new();
        if (Rows != null)
        {
            foreach (JsonDataRow Row in Rows)
            {
                if (Row == null)
                    throw new InvalidOperationException($"Table '{Table.TableName}' contains a null row.");
                DataRowState State = (DataRowState)Row.State;
                if (State != DataRowState.Unchanged && State != DataRowState.Added && State != DataRowState.Modified)
                    throw new InvalidOperationException($"Table '{Table.TableName}' has an invalid active row state: {Row.State}.");
                Prepared.Add((ConvertRow(Row, Table), State));
            }
        }
        if (Deleted != null)
        {
            foreach (JsonDataRow Row in Deleted)
            {
                if (Row == null || Row.State != (int)DataRowState.Deleted)
                    throw new InvalidOperationException($"Table '{Table.TableName}' has an invalid deleted row state.");
                Prepared.Add((ConvertRow(Row, Table), DataRowState.Deleted));
            }
        }
        foreach ((object[] Values, DataRowState State) in Prepared)
        {
            if (State == DataRowState.Deleted && DeferredDeletes != null)
                DeferredDeletes.Add(AddRow(Table, Values, DataRowState.Unchanged));
            else
                AddRow(Table, Values, State);
        }
    }

    // ● properties
    /// <summary>
    /// Gets or sets the table name.
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// Gets or sets the key field name.
    /// </summary>
    public string KeyField { get; set; } = "Id";
    /// <summary>
    /// Gets or sets the master field name.
    /// </summary>
    public string MasterField { get; set; } = "Id";
    /// <summary>
    /// Gets or sets the detail field name.
    /// </summary>
    public string DetailField { get; set; } = "Id";
    /// <summary>
    /// Gets or sets the master table name.
    /// </summary>
    public string MasterTableName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets whether new string keys are generated as GUIDs.
    /// </summary>
    public bool AutoGenerateGuidKeys { get; set; } = true;
    /// <summary>
    /// Gets or sets whether a client should present this detail as a single row per master.
    /// For example, StoreTrade and FinTrade can each be shown as one row of a Trade.
    /// The value comes from <see cref="TableDef.IsOneToOne"/>.
    /// </summary>
    public bool IsOneToOne { get; set; }
    /// <summary>
    /// Gets or sets whether a client should display this table.
    /// </summary>
    public bool IsUiVisible { get; set; } = true;
    /// <summary>
    /// Gets or sets the names of direct detail tables.
    /// </summary>
    public List<string> Details { get; set; } = new();
    /// <summary>
    /// Gets or sets the preferred display order of field groups from the table definition.
    /// Column packets identify their group through <see cref="JsonDataColumn.Group"/>.
    /// </summary>
    public List<string> FieldGroups { get; set; } = new();
    /// <summary>
    /// Gets or sets the column descriptions in row value order.
    /// </summary>
    public List<JsonDataColumn> Columns { get; set; } = new();
    /// <summary>
    /// Gets or sets the active rows.
    /// </summary>
    public List<JsonDataRow> Rows { get; set; } = new();
    /// <summary>
    /// Gets or sets the deleted rows and their original values.
    /// </summary>
    public List<JsonDataRow> Deleted { get; set; } = new();
}
