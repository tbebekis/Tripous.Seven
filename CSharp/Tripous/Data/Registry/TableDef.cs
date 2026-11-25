/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes a data module table and its detail definitions.
/// </summary>
public class TableDef : BaseDef
{
    // ● private fields
    string fKeyField = "Id";
    string fMasterField;
    string fDetailField;
    DefList<FieldDef> fFields;
    DefList<TableDef> fDetails;

    // ● public
    /// <summary>
    /// Creates a memory table and its columns from this definition.
    /// Integer keys receive temporary negative values for new rows.
    /// Detail tables and SQL statements are configured separately.
    /// </summary>
    public MemTable CreateDescriptorTable()
    {
        CheckDescriptor();
        MemTable Table = new(Name) { KeyField = KeyField };
        if (!string.IsNullOrWhiteSpace(MasterField))
            Table.MasterField = MasterField;
        if (!string.IsNullOrWhiteSpace(DetailField))
            Table.DetailField = DetailField;

        foreach (FieldDef Field in Fields)
        {
            Type ColumnType = Field.DataType switch
            {
                DataFieldType.String => typeof(string),
                DataFieldType.Integer => typeof(int),
                DataFieldType.Int64 => typeof(long),
                DataFieldType.Double => typeof(double),
                DataFieldType.Decimal or DataFieldType.Decimal_ => typeof(decimal),
                DataFieldType.Date or DataFieldType.DateTime => typeof(DateTime),
                DataFieldType.Boolean => typeof(int),
                DataFieldType.Blob => typeof(byte[]),
                DataFieldType.TextBlob => typeof(string),
                _ => throw new InvalidOperationException($"Unsupported data type for field '{Field.Name}'.")
            };
            DataColumn Column = new(Field.Name, ColumnType) { Caption = Field.Title };
            if (ColumnType == typeof(string))
                Column.MaxLength = Field.MaxLength;
            if (Field.DefaultValue != null)
            {
                try { Column.DefaultValue = Field.ParseDefaultValue(); }
                catch (Exception Error) when (Error is not InvalidOperationException)
                {
                    throw new InvalidOperationException($"Invalid default for field '{Field.Name}' in table '{Name}'.", Error);
                }
            }
            Table.Columns.Add(Column);
        }
        Table.InitializeAutoInc();
        return Table;
    }
    /// <summary>
    /// Builds statements for this table from its native field definitions.
    /// Integer auto-increment keys are omitted from the insert; other keys
    /// are supplied by the caller or generated before insertion.
    /// Integer keys require a provider generation mode.
    /// </summary>
    public TableSqls BuildSql(OidMode Mode)
    {
        CheckDescriptor();
        FieldDef Key = Fields.Find(KeyField);
        bool IntegerKey = Key.DataType == DataFieldType.Integer || Key.DataType == DataFieldType.Int64;
        if (IntegerKey && Mode != OidMode.Generator && Mode != OidMode.AutoInc)
            throw new InvalidOperationException($"Integer key field '{KeyField}' requires a generator or auto-increment mode.");

        List<FieldDef> NativeFields = Fields.Where(Field => Field.IsNativeField).ToList();
        string Prefix = SqlProvider.GlobalPrefix.ToString();
        List<FieldDef> InsertFields = NativeFields.Where(Field => !Field.IsNoInsertOrUpdate
            && !(ReferenceEquals(Field, Key) && IntegerKey && Mode == OidMode.AutoInc)).ToList();
        List<FieldDef> UpdateFields = NativeFields.Where(Field => !Field.IsNoInsertOrUpdate && !ReferenceEquals(Field, Key)).ToList();
        string Select = $"select {string.Join(", ", NativeFields.Select(Field => Field.Name))} from {Name}";
        string Where = $"{Key.Name} = {Prefix}{Key.Name}";

        return new TableSqls
        {
            SelectSql = Select,
            SelectRowSql = $"{Select} where {Where}",
            InsertRowSql = InsertFields.Count == 0 ? "" : $"insert into {Name} ({string.Join(", ", InsertFields.Select(Field => Field.Name))}) values ({string.Join(", ", InsertFields.Select(Field => Prefix + Field.Name))})",
            UpdateRowSql = UpdateFields.Count == 0 ? "" : $"update {Name} set {string.Join(", ", UpdateFields.Select(Field => Field.Name + " = " + Prefix + Field.Name))} where {Where}",
            DeleteRowSql = $"delete from {Name} where {Where}",
        };
    }
    /// <summary>
    /// Adds a signed 64-bit integer field to this table definition.
    /// </summary>
    public FieldDef AddInt64(string FieldName)
    {
        FieldDef Field = new() { Name = FieldName, DataType = DataFieldType.Int64, TableDef = this };
        Fields.Add(Field);
        return Field;
    }
    /// <summary>
    /// Checks the table name, key field name and presence of fields.
    /// </summary>
    public override void CheckDescriptor()
    {
        CheckTreeTopology();
        base.CheckDescriptor();
        DataTableDef.ValidateIdentifier(Name);
        if (string.IsNullOrWhiteSpace(KeyField))
            throw new InvalidOperationException(Texts.GS($"E_{GetType()}_NoKeyField", "Table key field is required."));
        if (Fields.Count == 0)
            throw new InvalidOperationException(Texts.GS($"E_{GetType()}_NoFields", "Table fields are required."));
        try
        {
            Fields.CheckDescriptors();
        }
        catch (InvalidOperationException Error)
        {
            throw new InvalidOperationException($"Invalid field definition in table '{Name}': {Error.Message}", Error);
        }
        FieldDef Key = Fields.Find(KeyField);
        if (Key == null || !Key.IsNativeField)
            throw new InvalidOperationException($"Key field '{KeyField}' must be a native field of '{Name}'.");
        if (Key.DataType != DataFieldType.String && Key.DataType != DataFieldType.Integer && Key.DataType != DataFieldType.Int64)
            throw new InvalidOperationException($"Key field '{KeyField}' must be a string or integer.");
        Details.CheckDescriptors();
        foreach (TableDef Detail in Details)
        {
            string MasterName = string.IsNullOrWhiteSpace(Detail.MasterField) ? "Id" : Detail.MasterField;
            string DetailName = string.IsNullOrWhiteSpace(Detail.DetailField) ? Detail.KeyField : Detail.DetailField;
            FieldDef MasterFieldDef = Fields.Find(MasterName);
            FieldDef DetailFieldDef = Detail.Fields.Find(DetailName);
            if (MasterFieldDef == null || DetailFieldDef == null)
                throw new InvalidOperationException($"Invalid master-detail fields for '{Detail.Name}'.");
            Type MasterType = GetRelationFieldType(MasterFieldDef.DataType);
            Type DetailType = GetRelationFieldType(DetailFieldDef.DataType);
            if (MasterType == null || DetailType == null)
                throw new NotSupportedException($"Unsupported master-detail field type for '{Detail.Name}'.");
            if (MasterType != DetailType)
                throw new InvalidOperationException($"Invalid master-detail fields for '{Detail.Name}'.");
        }
    }
    // ● private
    void CheckTreeTopology()
    {
        HashSet<TableDef> Tables = new(ReferenceEqualityComparer.Instance);
        HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase);
        Stack<TableDef> Pending = new();
        Pending.Push(this);
        while (Pending.Count > 0)
        {
            TableDef Table = Pending.Pop();
            if (!Tables.Add(Table))
                throw new InvalidOperationException($"Table '{Table.Name}' occurs more than once in the table tree.");
            if (!string.IsNullOrWhiteSpace(Table.Name) && !Names.Add(Table.Name))
                throw new InvalidOperationException($"Table name '{Table.Name}' occurs more than once in the table tree.");
            foreach (TableDef Detail in Table.Details)
                Pending.Push(Detail);
        }
    }
    static Type GetRelationFieldType(DataFieldType DataType) => DataType switch
    {
        DataFieldType.String or DataFieldType.TextBlob => typeof(string),
        DataFieldType.Integer or DataFieldType.Boolean => typeof(int),
        DataFieldType.Int64 => typeof(long),
        _ => null,
    };
    /// <summary>
    /// Reconnects fields and detail definitions to this table recursively.
    /// </summary>
    public override void UpdateReferences()
    {
        foreach (FieldDef Field in Fields)
            Field.TableDef = this;
        foreach (TableDef Detail in Details)
        {
            Detail.Master = this;
            Detail.UpdateReferences();
        }
    }

    // ● properties
    /// <summary>
    /// Gets or sets the master table definition, when this is a detail.
    /// </summary>
    [JsonIgnore]
    public TableDef Master { get; set; }
    /// <summary>
    /// Gets or sets the primary key field name.
    /// </summary>
    public string KeyField
    {
        get => fKeyField;
        set
        {
            if (fKeyField == value)
                return;
            fKeyField = value;
            NotifyPropertyChanged(nameof(KeyField));
        }
    }
    /// <summary>
    /// Gets or sets the master field name used to link this detail.
    /// </summary>
    public string MasterField
    {
        get => fMasterField;
        set
        {
            if (fMasterField == value)
                return;
            fMasterField = value;
            NotifyPropertyChanged(nameof(MasterField));
        }
    }
    /// <summary>
    /// Gets or sets the detail field name, falling back to the key field.
    /// </summary>
    public string DetailField
    {
        get => fDetailField ?? KeyField;
        set
        {
            if (fDetailField == value)
                return;
            fDetailField = value;
            NotifyPropertyChanged(nameof(DetailField));
        }
    }
    /// <summary>
    /// Gets or sets the fields described by this table.
    /// </summary>
    public DefList<FieldDef> Fields
    {
        get => fFields ??= new();
        set
        {
            if (ReferenceEquals(fFields, value))
                return;
            fFields = value;
            NotifyPropertyChanged(nameof(Fields));
        }
    }
    /// <summary>
    /// Gets or sets the direct detail table definitions.
    /// </summary>
    public DefList<TableDef> Details
    {
        get => fDetails ??= new();
        set
        {
            if (ReferenceEquals(fDetails, value))
                return;
            fDetails = value;
            NotifyPropertyChanged(nameof(Details));
        }
    }
}
