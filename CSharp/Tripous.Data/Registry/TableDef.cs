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
    string fAlias;

    // ● public
    /// <summary>
    /// Finds the descriptor for a memory column by its name, including projected join aliases.
    /// Use the owning table descriptor to determine which fields belong to the database table.
    /// </summary>
    public FieldDef FindColumnField(string ColumnName)
    {
        if (string.IsNullOrWhiteSpace(ColumnName))
            return null;
        return Fields.Find(ColumnName) ?? GetJoinProjections(new())
            .FirstOrDefault(Projection => string.Equals(Projection.ColumnName, ColumnName, StringComparison.OrdinalIgnoreCase)).Field;
    }

    /// <summary>
    /// Copies values from a selected lookup row into the matching stored snapshot fields.
    /// Pass a null lookup row to clear those fields when the selection is cleared.
    /// </summary>
    public void AssignLookupSnapshots(DataRow TargetRow, string SourceAlias, DataRow LookupRow)
    {
        ArgumentNullException.ThrowIfNull(TargetRow);
        ArgumentException.ThrowIfNullOrWhiteSpace(SourceAlias);
        List<(DataColumn Column, object Value)> Values = new();
        foreach (FieldDef Field in Fields)
        {
            if (string.IsNullOrWhiteSpace(Field.SnapshotOf))
                continue;
            (string Alias, string SourceColumn) = Field.GetSnapshotSource();
            if (!string.Equals(Alias, SourceAlias, StringComparison.OrdinalIgnoreCase))
                continue;
            DataColumn Target = TargetRow.Table.Columns[Field.Name];
            if (Target == null || Target.ReadOnly)
                throw new InvalidOperationException($"Snapshot target '{Field.Name}' must exist and be writable.");
            if (LookupRow != null && !LookupRow.Table.Columns.Contains(SourceColumn))
                throw new InvalidOperationException($"Lookup source '{SourceAlias}' does not return column '{SourceColumn}'.");
            Values.Add((Target, LookupRow == null ? DBNull.Value : LookupRow[SourceColumn]));
        }
        foreach ((DataColumn Column, object Value) in Values)
            TargetRow[Column] = Value;
    }

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
            AddColumn(Table, Field, Field.Name, UseDefault: true);
        foreach (var Projection in GetJoinProjections(new()))
            AddColumn(Table, Projection.Field, Projection.ColumnName, UseDefault: false);
        foreach (FieldDef Field in Fields)
        {
            if (!string.IsNullOrWhiteSpace(Field.Expression))
                Table.Columns[Field.Name].Expression = Field.Expression;
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

        if (Joins.Count > 0)
        {
            List<string> JoinClauses = new();
            var Projections = GetJoinProjections(JoinClauses);
            if (JoinClauses.Count > 0)
            {
                IEnumerable<string> Columns = NativeFields.Select(Field => $"{Name}.{Field.Name}")
                    .Concat(Projections.Select(Item => Item.Sql));
                Select = $"select {string.Join(", ", Columns)} from {Name} {string.Join(" ", JoinClauses)}";
            }
        }
        string SelectWhere = Joins.Count > 0 ? $"{Name}.{Where}" : Where;

        return new TableSqls
        {
            SelectSql = Select,
            SelectRowSql = $"{Select} where {SelectWhere}",
            InsertRowSql = InsertFields.Count == 0 ? "" : $"insert into {Name} ({string.Join(", ", InsertFields.Select(Field => Field.Name))}) values ({string.Join(", ", InsertFields.Select(Field => Prefix + Field.Name))})",
            UpdateRowSql = UpdateFields.Count == 0 ? "" : $"update {Name} set {string.Join(", ", UpdateFields.Select(Field => Field.Name + " = " + Prefix + Field.Name))} where {Where}",
            DeleteRowSql = $"delete from {Name} where {Where}",
        };
    }
    /// <summary>
    /// Adds a lookup table joined on its key and a field of this table.
    /// The returned definition accepts projected fields and nested joins.
    /// </summary>
    public TableDef AddJoin(string OwnKeyField, string ForeignTable, string ForeignAlias = "", string ForeignPrimaryKey = "Id")
    {
        TableDef Join = new()
        {
            Name = ForeignTable,
            Alias = ForeignAlias,
            KeyField = ForeignPrimaryKey,
            MasterField = OwnKeyField
        };
        Joins.Add(Join);
        return Join;
    }
    /// <summary>
    /// Adds a field with the specified data type to this table definition.
    /// </summary>
    public FieldDef AddField(string Name, DataFieldType DataType, int MaxLength = -1, FieldFlags Flags = FieldFlags.None)
    {
        FieldDef Field = new() { Name = Name, DataType = DataType, MaxLength = MaxLength, Flags = Flags, TableDef = this };
        Fields.Add(Field);
        return Field;
    }
    /// <summary>
    /// Adds a direct detail and defines the columns linking it to its master.
    /// </summary>
    public TableDef AddDetail(string DetailTableName, string MasterField, string DetailField)
    {
        TableDef Detail = new()
        {
            Name = DetailTableName,
            Master = this,
            MasterField = MasterField,
            DetailField = DetailField
        };
        Details.Add(Detail);
        return Detail;
    }
    /// <summary>
    /// Adds a string field to this table definition.
    /// </summary>
    public FieldDef AddString(string FieldName, int MaxLength = 96) => AddField(FieldName, DataFieldType.String, MaxLength);
    /// <summary>
    /// Adds a 32-bit integer field to this table definition.
    /// </summary>
    public FieldDef AddInteger(string FieldName) => AddField(FieldName, DataFieldType.Integer);
    /// <summary>
    /// Adds a 64-bit integer field to this table definition.
    /// </summary>
    public FieldDef AddInt64(string FieldName) => AddField(FieldName, DataFieldType.Int64);
    /// <summary>
    /// Adds a double-precision field to this table definition.
    /// </summary>
    public FieldDef AddDouble(string FieldName) => AddField(FieldName, DataFieldType.Double);
    /// <summary>
    /// Adds a decimal field to this table definition.
    /// </summary>
    public FieldDef AddDecimal(string FieldName) => AddField(FieldName, DataFieldType.Decimal);
    /// <summary>
    /// Adds a date field to this table definition.
    /// </summary>
    public FieldDef AddDate(string FieldName) => AddField(FieldName, DataFieldType.Date);
    /// <summary>
    /// Adds a date and time field to this table definition.
    /// </summary>
    public FieldDef AddDateTime(string FieldName) => AddField(FieldName, DataFieldType.DateTime);
    /// <summary>
    /// Adds a Boolean field to this table definition.
    /// </summary>
    public FieldDef AddBoolean(string FieldName) => AddField(FieldName, DataFieldType.Boolean);
    /// <summary>
    /// Adds a binary field to this table definition.
    /// </summary>
    public FieldDef AddBlob(string FieldName) => AddField(FieldName, DataFieldType.Blob);
    /// <summary>
    /// Adds a large text field to this table definition.
    /// </summary>
    public FieldDef AddTextBlob(string FieldName) => AddField(FieldName, DataFieldType.TextBlob);
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
        if (!string.IsNullOrWhiteSpace(Key.SnapshotOf))
            throw new InvalidOperationException($"Key field '{KeyField}' cannot be a lookup snapshot.");
        if (Key.DataType != DataFieldType.String && Key.DataType != DataFieldType.Integer && Key.DataType != DataFieldType.Int64)
            throw new InvalidOperationException($"Key field '{KeyField}' must be a string or integer.");
        GetJoinProjections(new());
        Details.CheckDescriptors();
        foreach (TableDef Detail in Details)
        {
            string MasterName = string.IsNullOrWhiteSpace(Detail.MasterField) ? "Id" : Detail.MasterField;
            string DetailName = string.IsNullOrWhiteSpace(Detail.DetailField) ? Detail.KeyField : Detail.DetailField;
            FieldDef MasterFieldDef = Fields.Find(MasterName);
            FieldDef DetailFieldDef = Detail.Fields.Find(DetailName);
            if (MasterFieldDef == null || DetailFieldDef == null)
                throw new InvalidOperationException($"Invalid master-detail fields for '{Detail.Name}'.");
            if (!string.IsNullOrWhiteSpace(MasterFieldDef.Expression) || !string.IsNullOrWhiteSpace(DetailFieldDef.Expression))
                throw new InvalidOperationException($"Master-detail fields for '{Detail.Name}' cannot be computed.");
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
    /// <summary>
    /// Collects projected columns and their required joins in SQL order.
    /// </summary>
    List<(FieldDef Field, string ColumnName, string Sql)> GetJoinProjections(List<string> JoinClauses)
    {
        List<(FieldDef Field, string ColumnName, string Sql)> Projections = new();
        HashSet<string> Aliases = new(StringComparer.OrdinalIgnoreCase) { Name };
        HashSet<string> Columns = new(Fields.Select(Field => Field.Name), StringComparer.OrdinalIgnoreCase);
        HashSet<string> SnapshotSources = new(StringComparer.OrdinalIgnoreCase);
        foreach (FieldDef Field in Fields)
        {
            if (!string.IsNullOrWhiteSpace(Field.SnapshotOf))
            {
                (string Alias, string Column) = Field.GetSnapshotSource();
                SnapshotSources.Add($"{Alias}.{Column}");
            }
        }
        foreach (TableDef Join in Joins)
            Visit(this, Name, Join);
        return Projections;

        void Visit(TableDef Parent, string ParentAlias, TableDef Join)
        {
            DataTableDef.ValidateIdentifier(Join.Name);
            DataTableDef.ValidateIdentifier(Join.Alias);
            DataTableDef.ValidateIdentifier(Join.KeyField);
            DataTableDef.ValidateIdentifier(Join.MasterField);
            if (!Aliases.Add(Join.Alias))
                throw new InvalidOperationException($"Duplicate join alias '{Join.Alias}' in table '{Name}'.");
            if (Join.Details.Count > 0)
                throw new InvalidOperationException($"Lookup join '{Join.Alias}' cannot own detail tables.");
            FieldDef ParentField = Parent.Fields.Find(Join.MasterField);
            if (ParentField?.IsNativeField != true
                && !(!ReferenceEquals(Parent, this) && string.Equals(Parent.KeyField, Join.MasterField, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Join '{Join.Alias}' requires a database field '{Join.MasterField}' on '{ParentAlias}'.");

            int FirstProjection = Projections.Count;
            int ClauseIndex = JoinClauses.Count;
            Join.Fields.CheckDescriptors();
            foreach (FieldDef Field in Join.Fields)
            {
                if (!Field.IsNativeField || !string.IsNullOrWhiteSpace(Field.SnapshotOf))
                    throw new InvalidOperationException($"Join field '{Join.Alias}.{Field.Name}' must describe a database column.");
                if (string.Equals(Field.Name, Join.KeyField, StringComparison.OrdinalIgnoreCase)
                    || SnapshotSources.Contains($"{Join.Alias}.{Field.Name}"))
                    continue;
                string ColumnName = string.IsNullOrWhiteSpace(Field.Alias) ? $"{Join.Alias}__{Field.Name}" : Field.Alias;
                DataTableDef.ValidateIdentifier(ColumnName);
                if (!Columns.Add(ColumnName))
                    throw new InvalidOperationException($"Duplicate projected column '{ColumnName}' in table '{Name}'.");
                Projections.Add((Field, ColumnName, $"{Join.Alias}.{Field.Name} as {ColumnName}"));
            }
            foreach (TableDef Child in Join.Joins)
                Visit(Join, Join.Alias, Child);
            if (Projections.Count > FirstProjection)
            {
                string Source = string.Equals(Join.Alias, Join.Name, StringComparison.Ordinal)
                    ? Join.Name : $"{Join.Name} {Join.Alias}";
                JoinClauses.Insert(ClauseIndex, $"left join {Source} on {Join.Alias}.{Join.KeyField} = {ParentAlias}.{Join.MasterField}");
            }
        }
    }
    /// <summary>
    /// Adds a typed column to the memory table.
    /// </summary>
    void AddColumn(MemTable Table, FieldDef Field, string ColumnName, bool UseDefault)
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
        DataColumn Column = new(ColumnName, ColumnType) { Caption = Field.Title };
        if (ColumnType == typeof(string))
            Column.MaxLength = Field.MaxLength;
        if (UseDefault && Field.DefaultValue != null)
        {
            try { Column.DefaultValue = Field.ParseDefaultValue(); }
            catch (Exception Error) when (Error is not InvalidOperationException)
            {
                throw new InvalidOperationException($"Invalid default for field '{Field.Name}' in table '{Name}'.", Error);
            }
        }
        Table.Columns.Add(Column);
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
        foreach (TableDef Join in Joins)
            Join.UpdateReferences();
    }

    // ● properties
    /// <summary>
    /// Gets or sets the master table definition, when this is a detail.
    /// </summary>
    [JsonIgnore]
    public TableDef Master { get; set; }
    /// <summary>
    /// Gets or sets the SQL alias of a lookup join.
    /// When empty, the join uses its table name.
    /// </summary>
    public string Alias
    {
        get => string.IsNullOrWhiteSpace(fAlias) ? Name : fAlias;
        set
        {
            if (fAlias == value)
                return;
            fAlias = value;
            NotifyPropertyChanged(nameof(Alias));
        }
    }
    /// <summary>
    /// Gets the lookup joins projected into this table's memory rows.
    /// Each join can in turn describe nested lookup joins.
    /// </summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public DefList<TableDef> Joins { get; } = new() { AllowDuplicateNames = true };
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
