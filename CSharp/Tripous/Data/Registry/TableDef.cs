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

    // ● private
    object ParseDefaultValue(FieldDef Field)
    {
        string Value = Field.DefaultValue;
        try
        {
            var Culture = System.Globalization.CultureInfo.InvariantCulture;
            var IntegerStyle = System.Globalization.NumberStyles.AllowLeadingSign;
            var NumberStyle = IntegerStyle | System.Globalization.NumberStyles.AllowDecimalPoint;
            switch (Field.DataType)
            {
                case DataFieldType.String:
                case DataFieldType.TextBlob:
                    return Value;
                case DataFieldType.Integer:
                    return int.Parse(Value, IntegerStyle, Culture);
                case DataFieldType.Int64:
                    return long.Parse(Value, IntegerStyle, Culture);
                case DataFieldType.Double:
                    double Number = double.Parse(Value, NumberStyle, Culture);
                    if (!double.IsFinite(Number))
                        throw new FormatException("A finite number is required.");
                    return Number;
                case DataFieldType.Decimal:
                case DataFieldType.Decimal_:
                    return decimal.Parse(Value, NumberStyle, Culture);
                case DataFieldType.Boolean:
                    if (string.Equals(Value, "true", StringComparison.OrdinalIgnoreCase)) return 1;
                    if (string.Equals(Value, "false", StringComparison.OrdinalIgnoreCase)) return 0;
                    throw new FormatException("Expected true or false.");
                case DataFieldType.Date:
                    return DateTime.ParseExact(Value, "yyyy-MM-dd", Culture, System.Globalization.DateTimeStyles.None);
                case DataFieldType.DateTime:
                    return DateTime.ParseExact(Value, new[] { "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF" }, Culture, System.Globalization.DateTimeStyles.None);
                case DataFieldType.Blob:
                    throw new NotSupportedException("Blob defaults are not supported.");
                default:
                    throw new NotSupportedException("The field type does not support defaults.");
            }
        }
        catch (Exception Error) when (Error is FormatException or OverflowException or NotSupportedException)
        {
            throw new InvalidOperationException($"Invalid default for field '{Field.Name}' in table '{Name}'.", Error);
        }
    }

    // ● public
    /// <summary>
    /// Creates a memory table and its columns from this definition.
    /// Integer keys receive temporary negative values for new rows.
    /// Detail tables and SQL statements are configured separately.
    /// </summary>
    public MemTable CreateDescriptorTable()
    {
        CheckDescriptor();
        if (Fields.Find(KeyField) == null)
            throw new InvalidOperationException($"Key field '{KeyField}' was not found in '{Name}'.");

        MemTable Table = new(Name) { KeyField = KeyField };
        if (!string.IsNullOrWhiteSpace(MasterField))
            Table.MasterField = MasterField;
        if (!string.IsNullOrWhiteSpace(DetailField))
            Table.DetailField = DetailField;

        foreach (FieldDef Field in Fields)
        {
            Field.CheckDescriptor();
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
                try { Column.DefaultValue = ParseDefaultValue(Field); }
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
        DataTableDef.ValidateIdentifier(Name);
        FieldDef Key = Fields.Find(KeyField);
        if (Key == null || !Key.IsNativeField)
            throw new InvalidOperationException($"Key field '{KeyField}' must be a native field of '{Name}'.");
        bool IntegerKey = Key.DataType == DataFieldType.Integer || Key.DataType == DataFieldType.Int64;
        if (Key.DataType != DataFieldType.String && !IntegerKey)
            throw new InvalidOperationException($"Key field '{KeyField}' must be a string or integer.");
        if (IntegerKey && Mode != OidMode.Generator && Mode != OidMode.AutoInc)
            throw new InvalidOperationException($"Integer key field '{KeyField}' requires a generator or auto-increment mode.");

        List<FieldDef> NativeFields = Fields.Where(Field => Field.IsNativeField).ToList();
        foreach (FieldDef Field in NativeFields)
        {
            Field.CheckDescriptor();
            DataTableDef.ValidateIdentifier(Field.Name);
            if (Field.Name.Any(Ch => !(char.IsLetterOrDigit(Ch) || Ch == '_')))
                throw new InvalidOperationException($"Field '{Field.Name}' cannot be used as a SQL parameter name.");
        }

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
        base.CheckDescriptor();
        if (string.IsNullOrWhiteSpace(KeyField))
            throw new InvalidOperationException(Texts.GS($"E_{GetType()}_NoKeyField", "Table key field is required."));
        if (Fields.Count == 0)
            throw new InvalidOperationException(Texts.GS($"E_{GetType()}_NoFields", "Table fields are required."));
    }
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
