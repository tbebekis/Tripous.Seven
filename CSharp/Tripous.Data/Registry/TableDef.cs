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

    // ● internal
    /// <summary>
    /// Adds missing fields and updates physical column metadata while preserving declared field types.
    /// Database-generated columns remain available for reads and are excluded from writes.
    /// </summary>
    internal void UpdateFrom(DbMetaTable Source, SqlStore Store, HashSet<FieldDef> InferredFields)
    {
        foreach (DbMetaColumn Column in Source.Columns.OrderBy(Item => Item.OrdinalPosition))
        {
            FieldDef Field = Fields.Find(Column.Name);
            if (Field == null)
            {
                Field = AddField(Column.Name, Store.GetMetadataFieldType(Column));
                InferredFields.Add(Field);
            }
            if (!Field.IsNativeField)
                continue;
            Field.IsNullable = Column.IsNullable;
            if (Field.DataType == DataFieldType.String && Column.SizeInChars > 0)
                Field.MaxLength = Column.SizeInChars;
            if (Column.IsIdentity || Column.IsComputed)
                Field.Flags |= FieldFlags.NoInsertUpdate;
        }
    }

    /// <summary>
    /// Finds the fields that link this table to one of its direct details.
    /// </summary>
    internal (FieldDef Master, FieldDef Detail) GetRelationFields(TableDef Detail)
    {
        return (FindField(GetMasterRelationFieldName(Detail)), Detail.FindField(GetDetailRelationFieldName(Detail)));
    }

    // ● public
    /// <summary>
    /// Finds a join by source table name or SQL alias anywhere in this table's join tree.
    /// Direct joins are checked before nested joins.
    /// </summary>
    public TableDef FindAnyJoinTable(string NameOrAlias)
        => FindJoin(Join => string.Equals(Join.Name, NameOrAlias, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Join.Alias, NameOrAlias, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Finds a direct join that uses the specified field of this table in its join condition.
    /// </summary>
    public TableDef FindJoinTableByMasterKeyField(string MasterKeyField)
        => string.IsNullOrWhiteSpace(MasterKeyField) ? null
            : Joins.FirstOrDefault(Join => string.Equals(Join.MasterField, MasterKeyField, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Finds the first join using the specified parent field anywhere in this table's join tree.
    /// Use the direct lookup when the field name is meaningful only within one table.
    /// </summary>
    public TableDef FindAnyJoinTableByMasterKeyField(string MasterKeyField)
        => string.IsNullOrWhiteSpace(MasterKeyField) ? null
            : FindJoin(Join => string.Equals(Join.MasterField, MasterKeyField, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns whether this table declares a field with the specified name.
    /// </summary>
    public bool FieldExists(string FieldName) => Fields.Contains(FieldName);

    /// <summary>
    /// Finds a field declared directly on this table by name, or returns null.
    /// </summary>
    public FieldDef FindField(string FieldName) => Fields.Find(FieldName);

    /// <summary>
    /// Finds a field declared directly on this table by name, or throws if absent.
    /// </summary>
    public FieldDef GetField(string FieldName) => Fields.Get(FieldName);

    /// <summary>
    /// Returns this table's bindable fields in declaration order.
    /// </summary>
    public List<FieldDef> GetBindableFields() => Fields.Where(Field => Field.IsBindable).ToList();

    /// <summary>
    /// Groups bindable fields by name without distinguishing case.
    /// General comes first, followed by configured groups and then groups found in the fields.
    /// Empty groups are omitted and fields keep their declaration order.
    /// </summary>
    public Dictionary<string, List<FieldDef>> GetBindableGroups()
    {
        Dictionary<string, List<FieldDef>> FieldsByGroup = GetBindableFields()
            .GroupBy(Field => Field.Group.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(Group => Group.Key, Group => Group.ToList(), StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> GroupNames = new[] { "General" }
            .Concat((FieldGroups ?? new List<string>()).Select(Group => Group?.Trim()))
            .Concat(FieldsByGroup.Keys);
        Dictionary<string, List<FieldDef>> Result = new(StringComparer.OrdinalIgnoreCase);
        foreach (string Group in GroupNames)
        {
            if (!string.IsNullOrWhiteSpace(Group) && FieldsByGroup.TryGetValue(Group, out List<FieldDef> GroupFields))
                Result.TryAdd(Group, GroupFields);
        }
        return Result;
    }

    /// <summary>
    /// Finds a declared field by name or alias in this table and its nested joins.
    /// Returns the declaring table together with the field, even for fields omitted from the SELECT.
    /// For example, a lookup field can still be found when a stored snapshot replaces its projection.
    /// </summary>
    public Tuple<TableDef, FieldDef> FindAnyField(string NameOrAlias)
    {
        if (string.IsNullOrWhiteSpace(NameOrAlias))
            return null;
        return FindIn(this, IsJoin);

        Tuple<TableDef, FieldDef> FindIn(TableDef Table, bool IsJoined)
        {
            foreach (FieldDef Field in Table.Fields)
            {
                string Alias = Field.Alias;
                if (IsJoined && string.IsNullOrWhiteSpace(Alias))
                    Alias = $"{Table.Alias}__{Field.Name}";
                if (string.Equals(Field.Name, NameOrAlias, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Alias, NameOrAlias, StringComparison.OrdinalIgnoreCase))
                    return Tuple.Create(Table, Field);
            }
            foreach (TableDef Join in Table.Joins)
            {
                Tuple<TableDef, FieldDef> Result = FindIn(Join, IsJoined: true);
                if (Result != null)
                    return Result;
            }
            return null;
        }
    }

    /// <summary>
    /// Returns the localized title of a declared field found by name or alias.
    /// If the field cannot be found, returns the supplied name or alias.
    /// </summary>
    public string FindAnyFieldTitle(string NameOrAlias)
    {
        Tuple<TableDef, FieldDef> Match = FindAnyField(NameOrAlias);
        if (Match == null)
            return NameOrAlias;
        (TableDef Table, FieldDef Field) = Match;
        return Field.IsTitleKeyEmpty && (Table.IsJoin || !ReferenceEquals(Table, this))
            ? Texts.L($"{Table.Alias}__{Field.Name}")
            : Field.Title;
    }

    /// <summary>
    /// Supplies captions for columns whose caption is empty or still matches their name.
    /// An explicit display label takes precedence over the title of a declared field, including joins.
    /// Existing custom captions are preserved.
    /// </summary>
    public void SetupFieldsDisplayLabelsFor(DataTable Table, Dictionary<string, string> DisplayLabels)
    {
        ArgumentNullException.ThrowIfNull(Table);
        foreach (DataColumn Column in Table.Columns)
        {
            if (!string.IsNullOrWhiteSpace(Column.Caption)
                && !string.Equals(Column.Caption, Column.ColumnName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (DisplayLabels?.TryGetValue(Column.ColumnName, out string Label) == true && !string.IsNullOrWhiteSpace(Label))
                Column.Caption = Label;
            else
                Column.Caption = FindAnyFieldTitle(Column.ColumnName);
        }
    }

    /// <summary>
    /// Finds the descriptor for a memory column by its name, including projected join aliases.
    /// Use the owning table descriptor to determine which fields belong to the database table.
    /// </summary>
    public FieldDef FindColumnField(string ColumnName)
    {
        if (string.IsNullOrWhiteSpace(ColumnName))
            return null;
        return FindField(ColumnName) ?? GetJoinProjections(new())
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
        UpdateReferences();
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
            Owner = this,
            ModuleDef = ModuleDef,
            Alias = ForeignAlias,
            KeyField = ForeignPrimaryKey,
            MasterField = OwnKeyField
        };
        Joins.Add(Join);
        return Join;
    }
    /// <summary>
    /// Adds a field with the specified data type and optional display and lookup metadata.
    /// For example, a CountryId field can name its lookup source and the group where it appears.
    /// </summary>
    public FieldDef AddField(string Name, DataFieldType DataType, int MaxLength = -1, FieldFlags Flags = FieldFlags.None,
        string Group = null, string TitleKey = null, string Alias = null, int Decimals = -1,
        string LookupSource = null, string Locator = null)
    {
        FieldDef Field = new()
        {
            Name = Name,
            DataType = DataType,
            MaxLength = MaxLength,
            Flags = Flags,
            Group = Group,
            TitleKey = TitleKey,
            Alias = Alias,
            Decimals = Decimals,
            LookupSource = LookupSource,
            Locator = Locator,
            TableDef = this
        };
        Fields.Add(Field);
        return Field;
    }
    /// <summary>
    /// Adds the primary key field using the application's default key type.
    /// When no name is supplied, uses the configured KeyField.
    /// </summary>
    public FieldDef AddId(string FieldName = null, FieldFlags Flags = FieldFlags.Required | FieldFlags.Hidden)
        => AddId(FieldName, DbConfig.DefaultPrimaryKeyType, Flags);

    /// <summary>
    /// Adds the primary key field with an explicit string or integer type.
    /// A supplied name becomes the table's KeyField.
    /// </summary>
    public FieldDef AddId(string FieldName, DataFieldType DataType, FieldFlags Flags = FieldFlags.Required | FieldFlags.Hidden, int MaxLength = 40)
    {
        if (DataType is not (DataFieldType.String or DataFieldType.Integer or DataFieldType.Int64))
            throw new ArgumentException("The key field must be a string or integer.", nameof(DataType));

        FieldDef Field = AddField(FieldName ?? KeyField, DataType, DataType == DataFieldType.String ? MaxLength : -1,
            Flags | FieldFlags.ReadOnlyUI);
        KeyField = Field.Name;
        return Field;
    }

    /// <summary>
    /// Adds a string primary key with the specified maximum length.
    /// </summary>
    public FieldDef AddStringId(string FieldName = null, FieldFlags Flags = FieldFlags.Required | FieldFlags.Hidden, int MaxLength = 40)
        => AddId(FieldName, DataFieldType.String, Flags, MaxLength);

    /// <summary>
    /// Adds a 32-bit integer primary key.
    /// </summary>
    public FieldDef AddIntegerId(string FieldName = null, FieldFlags Flags = FieldFlags.Required | FieldFlags.Hidden)
        => AddId(FieldName, DataFieldType.Integer, Flags);

    /// <summary>
    /// Adds a 64-bit integer primary key.
    /// </summary>
    public FieldDef AddInt64Id(string FieldName = null, FieldFlags Flags = FieldFlags.Required | FieldFlags.Hidden)
        => AddId(FieldName, DataFieldType.Int64, Flags);

    /// <summary>
    /// Adds a direct detail and defines the columns linking it to its master.
    /// </summary>
    public TableDef AddDetail(string DetailTableName, string MasterField, string DetailField)
    {
        TableDef Detail = new()
        {
            Name = DetailTableName,
            Master = this,
            ModuleDef = ModuleDef,
            MasterField = MasterField,
            DetailField = DetailField
        };
        Details.Add(Detail);
        return Detail;
    }
    /// <summary>
    /// Adds a string field to this table definition.
    /// </summary>
    public FieldDef AddString(string FieldName, int MaxLength = 96, string Group = null, string TitleKey = null, FieldFlags Flags = FieldFlags.None)
        => AddField(FieldName, DataFieldType.String, MaxLength, Flags: Flags, Group: Group, TitleKey: TitleKey);
    /// <summary>
    /// Adds a 32-bit integer field to this table definition.
    /// </summary>
    public FieldDef AddInteger(string FieldName, string Group = null, string TitleKey = null, FieldFlags Flags = FieldFlags.None)
        => AddField(FieldName, DataFieldType.Integer, Flags: Flags, Group: Group, TitleKey: TitleKey);
    /// <summary>
    /// Adds a 64-bit integer field to this table definition.
    /// </summary>
    public FieldDef AddInt64(string FieldName, string Group = null, string TitleKey = null, FieldFlags Flags = FieldFlags.None)
        => AddField(FieldName, DataFieldType.Int64, Flags: Flags, Group: Group, TitleKey: TitleKey);
    /// <summary>
    /// Adds a double-precision field to this table definition.
    /// </summary>
    public FieldDef AddDouble(string FieldName, string Group = null, int Decimals = -1, string TitleKey = null, FieldFlags Flags = FieldFlags.None)
        => AddField(FieldName, DataFieldType.Double, Flags: Flags, Group: Group, TitleKey: TitleKey, Decimals: Decimals);
    /// <summary>
    /// Adds a decimal field to this table definition.
    /// </summary>
    public FieldDef AddDecimal(string FieldName, string Group = null, int Decimals = -1, string TitleKey = null, FieldFlags Flags = FieldFlags.None)
        => AddField(FieldName, DataFieldType.Decimal, Flags: Flags, Group: Group, TitleKey: TitleKey, Decimals: Decimals);
    /// <summary>
    /// Adds a date field to this table definition.
    /// </summary>
    public FieldDef AddDate(string FieldName, string Group = null, string TitleKey = null, FieldFlags Flags = FieldFlags.None)
        => AddField(FieldName, DataFieldType.Date, Flags: Flags, Group: Group, TitleKey: TitleKey);
    /// <summary>
    /// Adds a date and time field to this table definition.
    /// </summary>
    public FieldDef AddDateTime(string FieldName, string Group = null, string TitleKey = null, FieldFlags Flags = FieldFlags.None)
        => AddField(FieldName, DataFieldType.DateTime, Flags: Flags, Group: Group, TitleKey: TitleKey);
    /// <summary>
    /// Adds a Boolean field to this table definition.
    /// </summary>
    public FieldDef AddBoolean(string FieldName, string Group = null, string TitleKey = null, FieldFlags Flags = FieldFlags.None)
        => AddField(FieldName, DataFieldType.Boolean, Flags: Flags | FieldFlags.Boolean, Group: Group, TitleKey: TitleKey);
    /// <summary>
    /// Adds a binary field to this table definition.
    /// </summary>
    public FieldDef AddBlob(string FieldName, string Group = null, string TitleKey = null, FieldFlags Flags = FieldFlags.None)
        => AddField(FieldName, DataFieldType.Blob, Flags: Flags, Group: Group, TitleKey: TitleKey);
    /// <summary>
    /// Adds a large text field to this table definition.
    /// </summary>
    public FieldDef AddTextBlob(string FieldName, string Group = null, string TitleKey = null, FieldFlags Flags = FieldFlags.None)
        => AddField(FieldName, DataFieldType.TextBlob, Flags: Flags | FieldFlags.Memo, Group: Group, TitleKey: TitleKey);
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
            (FieldDef MasterFieldDef, FieldDef DetailFieldDef) = GetRelationFields(Detail);
            if (MasterFieldDef == null)
                throw new InvalidOperationException($"Detail table '{Detail.Name}' refers to missing master field '{GetMasterRelationFieldName(Detail)}' on '{Name}'.");
            if (DetailFieldDef == null)
                throw new InvalidOperationException($"Detail table '{Detail.Name}' has no relation field '{GetDetailRelationFieldName(Detail)}'.");
            if (!string.IsNullOrWhiteSpace(MasterFieldDef.Expression) || !string.IsNullOrWhiteSpace(DetailFieldDef.Expression))
                throw new InvalidOperationException($"Master-detail fields for '{Detail.Name}' cannot be computed.");
            Type MasterType = GetRelationFieldType(MasterFieldDef.DataType);
            Type DetailType = GetRelationFieldType(DetailFieldDef.DataType);
            if (MasterType == null || DetailType == null)
                throw new NotSupportedException($"Unsupported master-detail field type for '{Detail.Name}'.");
            if (MasterType != DetailType)
                throw new InvalidOperationException(
                    $"Master field '{Name}.{MasterFieldDef.Name}' ({MasterType.Name}) and detail field '{Detail.Name}.{DetailFieldDef.Name}' ({DetailType.Name}) must have the same type.");
        }
    }
    // ● private
    /// <summary>
    /// Searches each level of the join tree in declaration order.
    /// </summary>
    TableDef FindJoin(Func<TableDef, bool> Match)
    {
        TableDef Result = Joins.FirstOrDefault(Match);
        if (Result != null)
            return Result;
        foreach (TableDef Join in Joins)
        {
            Result = Join.FindJoin(Match);
            if (Result != null)
                return Result;
        }
        return null;
    }

    /// <summary>
    /// Resolves the parent column name, defaulting to Id.
    /// </summary>
    static string GetMasterRelationFieldName(TableDef Detail)
        => string.IsNullOrWhiteSpace(Detail.MasterField) ? "Id" : Detail.MasterField;

    /// <summary>
    /// Resolves the detail column name, defaulting to the detail's key.
    /// </summary>
    static string GetDetailRelationFieldName(TableDef Detail)
        => string.IsNullOrWhiteSpace(Detail.DetailField) ? Detail.KeyField : Detail.DetailField;

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
    /// Reconnects fields, details and joins to their owners recursively.
    /// </summary>
    public override void UpdateReferences()
    {
        foreach (FieldDef Field in Fields)
            Field.TableDef = this;
        foreach (TableDef Detail in Details)
        {
            Detail.Master = this;
            Detail.Owner = null;
            Detail.ModuleDef = ModuleDef;
            Detail.UpdateReferences();
        }
        foreach (TableDef Join in Joins)
        {
            Join.Master = null;
            Join.Owner = this;
            Join.ModuleDef = ModuleDef;
            Join.UpdateReferences();
        }
    }

    // ● properties
    /// <summary>
    /// Gets or sets the module definition that owns this table tree.
    /// </summary>
    [JsonIgnore]
    public ModuleDef ModuleDef { get; set; }

    /// <summary>
    /// Gets or sets the table that declares this join.
    /// </summary>
    [JsonIgnore]
    public TableDef Owner { get; set; }

    /// <summary>
    /// Gets whether this table is a detail of another table.
    /// </summary>
    [JsonIgnore]
    public bool IsDetail => Master != null;

    /// <summary>
    /// Gets whether another table declares this table as a join.
    /// </summary>
    [JsonIgnore]
    public bool IsJoin => Owner != null;

    /// <summary>
    /// Gets whether this table has direct detail definitions.
    /// </summary>
    [JsonIgnore]
    public bool HasDetails => fDetails?.Count > 0;

    /// <summary>
    /// Gets whether this table has direct join definitions.
    /// </summary>
    [JsonIgnore]
    public bool HasJoins => Joins.Count > 0;

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
    /// Gets or sets whether a detail represents a single row per master for presentation.
    /// For example, Trade can be the top table, with StoreTrade and FinTrade each
    /// holding one detail row for the same trade.
    /// A primary key or unique constraint on the detail link enforces this in the database.
    /// </summary>
    public bool IsOneToOne { get; set; }

    /// <summary>
    /// Gets or sets whether clients should display this table.
    /// For example, a form can hide a one-to-one detail or omit the grid for a
    /// multi-row detail. Hidden tables remain available in the item tree.
    /// </summary>
    public bool IsUiVisible { get; set; } = true;

    /// <summary>
    /// Gets or sets the preferred display order of the table's field groups.
    /// </summary>
    public List<string> FieldGroups { get; set; } = new();

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
