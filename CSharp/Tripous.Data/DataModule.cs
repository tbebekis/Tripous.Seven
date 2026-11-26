/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Holds the tables and operations of a data module.
/// </summary>
[TypeStore]
public class DataModule
{
    // ● private fields
    int fInserting;
    int fEditing;
    int fDeleting;
    int fCommitting;
    DataRow fGeneratedCodeRow;
    DataColumn fGeneratedCodeColumn;
    object fGeneratedCodePreviousValue;

    // ● private
    void Table_RowDeleting(object Sender, DataRowChangeEventArgs Args)
    {
        MemTable Table = (MemTable)Sender;
        if (ModuleDef.CascadeDeletes)
            Table.DeleteDetailRows(Args.Row);
        RowDeleting(Table, Args);
    }
    void HandleTransactionStageCommit(object Sender, TransactionEventArgs Args)
    {
        if (Args.Stage == TransactionStage.Start && Args.ExecTime == ExecTime.Before && fGeneratedCodeRow != null)
        {
            if (fGeneratedCodeRow.RowState == DataRowState.Detached || ItemTable.Rows.IndexOf(fGeneratedCodeRow) < 0)
            {
                fGeneratedCodeRow = null;
                fGeneratedCodeColumn = null;
                fGeneratedCodePreviousValue = null;
            }
            else
                throw new InvalidOperationException("The outcome of the previous generated code reservation is unknown; reload the item before retrying.");
        }
        if (Args.Stage == TransactionStage.Post && Args.ExecTime == ExecTime.Before)
            AssignCodeValue(Args.Transaction);
        if (Args.Stage == TransactionStage.Rollback && Args.ExecTime == ExecTime.After && fGeneratedCodeRow != null)
        {
            fGeneratedCodeRow[fGeneratedCodeColumn] = fGeneratedCodePreviousValue;
            fGeneratedCodeRow = null;
            fGeneratedCodeColumn = null;
            fGeneratedCodePreviousValue = null;
        }
        if (Args.Stage == TransactionStage.Commit && Args.ExecTime == ExecTime.After)
        {
            fGeneratedCodeRow = null;
            fGeneratedCodeColumn = null;
            fGeneratedCodePreviousValue = null;
        }
        TableSet_TransactionStageCommit(Sender, Args);
    }

    // ● protected fields
    /// <summary>
    /// The table operations used by this module.
    /// </summary>
    protected TableSet TableSet;

    // ● audit
    /// <summary>
    /// Sets declared audit fields on the main item row.
    /// A declared user audit field requires an application user ID.
    /// </summary>
    protected virtual void SetAuditValues()
    {
        if (ItemTable == null || ItemTable.Rows.Count != 1)
            return;

        DataRow Row = ItemTable.Rows[0];
        if (Row.RowState == DataRowState.Deleted)
            return;

        string UserId = CurrentUserId;
        DataColumnCollection Columns = ItemTable.Columns;
        if ((Columns.Contains("CreatedBy") || Columns.Contains("ModifiedBy")) && string.IsNullOrWhiteSpace(UserId))
            throw new InvalidOperationException("A current user ID is required for the item audit fields.");

        DateTime Now = DateTime.UtcNow;
        if (Row.RowState == DataRowState.Added)
        {
            if (Columns.Contains("CreatedBy"))
                Row["CreatedBy"] = UserId;
            if (Columns.Contains("CreatedAt"))
                Row["CreatedAt"] = Now;
        }
        if (Columns.Contains("ModifiedBy"))
            Row["ModifiedBy"] = UserId;
        if (Columns.Contains("ModifiedAt"))
            Row["ModifiedAt"] = Now;
    }

    // ● code provider
    /// <summary>
    /// Selects the code provider for a new item with an empty configured code field.
    /// A derived module can select a different registered series, for example for drafts.
    /// Returning null skips code generation for this insert.
    /// </summary>
    protected virtual CodeProviderDef GetCodeProviderDef() => DataRegistry.CodeProviders.Get(ModuleDef.CodeProvider);

    /// <summary>
    /// Reserves and formats the next code for the selected provider inside the commit transaction.
    /// Override this to customize the generated code while keeping the standard assignment and rollback handling.
    /// </summary>
    protected virtual string GetNextCodeLocked(DbTransaction Transaction, CodeProviderDef Provider)
        => new CodeProviderStore().GetNextCode(Store, Transaction, Provider.Name, DateTime.Today);

    /// <summary>
    /// Assigns a registered code provider's value to a new item with an empty code field.
    /// The series update uses the commit transaction.
    /// </summary>
    protected virtual void AssignCodeValue(DbTransaction Transaction)
    {
        if (string.IsNullOrWhiteSpace(ModuleDef.CodeProvider) || ItemTable.Rows.Count != 1)
            return;
        DataRow Row = ItemTable.Rows[0];
        DataColumn Column = ItemTable.Columns[ModuleDef.CodeFieldName] ?? throw new InvalidOperationException($"Code field '{ModuleDef.CodeFieldName}' was not found.");
        if (Row.RowState != DataRowState.Added || !string.IsNullOrWhiteSpace(Convert.ToString(Row[Column])))
            return;
        CodeProviderDef Provider = GetCodeProviderDef();
        if (Provider == null)
            return;
        string Code = GetNextCodeLocked(Transaction, Provider);
        fGeneratedCodeRow = Row;
        fGeneratedCodeColumn = Column;
        fGeneratedCodePreviousValue = Row[Column];
        Row[Column] = Code;
    }

    // ● transaction event handlers
    /// <summary>
    /// Receives item commit transaction stage notifications.
    /// </summary>
    protected virtual void TableSet_TransactionStageCommit(object Sender, TransactionEventArgs Args) { }
    /// <summary>
    /// Receives item delete transaction stage notifications.
    /// </summary>
    protected virtual void TableSet_TransactionStageDelete(object Sender, TransactionEventArgs Args) { }

    // ● table event handlers
    /// <summary>
    /// Called before a column value changes.
    /// </summary>
    protected virtual void ColumnChanging(MemTable Table, DataColumnChangeEventArgs Args) { }
    /// <summary>
    /// Called after a column value changes.
    /// </summary>
    protected virtual void ColumnChanged(MemTable Table, DataColumnChangeEventArgs Args) { }
    /// <summary>
    /// Called before an item table row is deleted, after any cascade to its details.
    /// </summary>
    protected virtual void RowDeleting(MemTable Table, DataRowChangeEventArgs Args) { }
    /// <summary>
    /// Called after defaults are set and before a new row is added to the table.
    /// </summary>
    protected virtual void NewRowAdding(MemTable Table, DataTableNewRowEventArgs Args) { }
    /// <summary>
    /// Called after a new row is added to the table.
    /// </summary>
    protected virtual void NewRowAdded(MemTable Table, DataTableNewRowEventArgs Args) { }

    // ● item hooks
    /// <summary>
    /// Called before preparing a new item.
    /// </summary>
    protected virtual void Inserting() { }
    /// <summary>
    /// Sets default values on the newly prepared item.
    /// </summary>
    protected virtual void SetDefaultValues() { }
    /// <summary>
    /// Sets default values on a new detail row before it is added to its table.
    /// Not called when table events are disabled.
    /// </summary>
    protected virtual void SetDefaultValues(MemTable Table, DataRow Row) { }
    /// <summary>
    /// Called after a new item has been prepared and the state is Insert.
    /// </summary>
    protected virtual void Inserted() { }
    /// <summary>
    /// Called before loading an item for editing.
    /// </summary>
    protected virtual void Editing(object RowId) { }
    /// <summary>
    /// Called after an item has been loaded and the state is Edit.
    /// </summary>
    protected virtual void Edited(object RowId) { }
    /// <summary>
    /// Called before deleting an item.
    /// </summary>
    protected virtual void Deleting(object RowId) { }
    /// <summary>
    /// Called after an item has been deleted and the state is None.
    /// </summary>
    protected virtual void Deleted(object RowId) { }
    /// <summary>
    /// Called before saving the item tree.
    /// </summary>
    protected virtual void Committing(bool Reselect) { }
    /// <summary>
    /// Called after the item is saved and the state is Edit.
    /// </summary>
    protected virtual void Committed(bool Reselect, object RowId) { }

    // ● JSON
    /// <summary>
    /// Converts a text ID to the type of the item table key.
    /// </summary>
    protected virtual object JsonConvertRowId(string Id)
    {
        DataColumn Column = ItemTable.Columns[ItemTable.KeyField];
        if (Column.DataType == typeof(int))
            return Convert.ToInt32(Id, System.Globalization.CultureInfo.InvariantCulture);
        if (Column.DataType == typeof(long))
            return Convert.ToInt64(Id, System.Globalization.CultureInfo.InvariantCulture);
        return Id;
    }

    /// <summary>
    /// Copies item rows from a module packet and collects rows whose deletion is deferred.
    /// </summary>
    protected virtual void JsonApplyTableRows(MemTable Table, JsonDataModule Source, List<DataRow> DeferredDeletes)
    {
        if (Table == null || Source?.Tables == null)
            return;
        JsonDataTable Packet = Source.Tables.FirstOrDefault(Item => Item != null && string.Equals(Item.Name, Table.TableName, StringComparison.OrdinalIgnoreCase));
        if (Packet == null)
            return;
        Packet.RowsTo(Table, DeferredDeletes);
        foreach (MemTable Detail in Table.Details)
        {
            Detail.UpdateFromMaster();
            JsonApplyTableRows(Detail, Source, DeferredDeletes);
        }
    }

    // ● item editing
    /// <summary>
    /// Completes pending edits throughout the item tree before commit validation.
    /// </summary>
    protected virtual void EndEdit()
    {
        List<MemTable> Tree = ItemTable.GetTreeAsFlatList();
        for (int Index = Tree.Count - 1; Index >= 0; Index--)
        {
            foreach (DataRow Row in Tree[Index].Rows.Cast<DataRow>().ToArray())
            {
                if (Row.RowState != DataRowState.Deleted && Row.HasVersion(DataRowVersion.Proposed))
                    Row.EndEdit();
            }
        }
    }

    /// <summary>
    /// Returns the registered module name for diagnostics.
    /// Before initialization, returns the CLR type name.
    /// </summary>
    public override string ToString() => ModuleDef?.Name ?? base.ToString();

    // ● initialization
    /// <summary>
    /// Binds a user context before running module initialization.
    /// </summary>
    public void Initialize(ModuleDef Definition, UserContext Context)
    {
        if (ModuleDef != null)
            throw new InvalidOperationException("The data module is already initialized.");
        ArgumentNullException.ThrowIfNull(Definition);
        Definition.CheckAccess(Context);
        UserContext = Context;
        try
        {
            Initialize(Definition);
        }
        catch
        {
            if (!IsInitialized)
                UserContext = null;
            throw;
        }
    }
    /// <summary>
    /// Completes the module's item table definitions from a snapshot of database table metadata.
    /// The supplied definition belongs to the module being initialized.
    /// </summary>
    protected virtual void UpdateTableSchema(ModuleDef Definition, SqlStore Store)
    {
        DbSchema Schema = DbSchemaLoader.Load(Store.Schema.ConnectionInfo);
        List<TableDef> Tables = Definition.GetTables();
        HashSet<FieldDef> InferredFields = new(ReferenceEqualityComparer.Instance);
        foreach (TableDef Table in Tables)
        {
            DbMetaTable Source = Schema.Tables.SingleOrDefault(Item =>
                string.Equals(Item.Name, Table.Name, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Table '{Table.Name}' was not found in database metadata.");
            Table.UpdateFrom(Source, Store, InferredFields);
        }

        // An inferred field can link more than one table. Propagate the width through the tree
        // before validating it, leaving any conflicting explicit declarations for validation.
        bool Changed;
        do
        {
            Changed = false;
            foreach (TableDef Table in Tables)
            {
                foreach (TableDef Detail in Table.Details)
                {
                    (FieldDef Master, FieldDef Child) = Table.GetRelationFields(Detail);
                    Changed |= MatchIntegerWidth(Master, Child);
                    Changed |= MatchIntegerWidth(Child, Master);
                }
            }
        } while (Changed);

        bool MatchIntegerWidth(FieldDef Source, FieldDef Target)
        {
            if (Source?.DataType != DataFieldType.Integer || Target?.DataType != DataFieldType.Int64
                || !InferredFields.Contains(Target))
                return false;

            Target.DataType = DataFieldType.Integer;
            return true;
        }
    }
    /// <summary>
    /// Loads application defaults, such as the default company or currency, after the module is set up.
    /// Derived modules can cache these values for use when assigning defaults to new rows.
    /// </summary>
    protected virtual void LoadDefaultValues() { }

    /// <summary>
    /// Initializes the module from its definition and configured connection.
    /// Throws if this instance was already successfully initialized.
    /// </summary>
    public virtual void Initialize(ModuleDef Definition)
    {
        if (ModuleDef != null)
            throw new InvalidOperationException("The data module is already initialized.");
        ArgumentNullException.ThrowIfNull(Definition);
        Definition.CheckAccess(UserContext);
        SqlStore NewStore;
        if (DbConfig.AllowUpdatingTableSchema)
        {
            Definition = (ModuleDef)Json.Deserialize(Json.Serialize(Definition), Definition.GetType());
            NewStore = SqlStores.CreateSqlStore(Definition.ConnectionName);
            UpdateTableSchema(Definition, NewStore);
            Definition.CheckDescriptor();
        }
        else
        {
            Definition.CheckDescriptor();
            NewStore = SqlStores.CreateSqlStore(Definition.ConnectionName);
        }
        TableList NewTables = new();
        foreach (TableDef TableDef in Definition.GetTables())
        {
            MemTable Table = TableDef.CreateDescriptorTable();
            Table.Sqls.AssignFrom(TableDef.BuildSql(NewStore.OidMode));
            NewTables.Add(Table);
        }

        void ConnectDetails(TableDef MasterDef)
        {
            MemTable Master = NewTables.Get(MasterDef.Name);
            foreach (TableDef DetailDef in MasterDef.Details)
            {
                Master.AddDetail(NewTables.Get(DetailDef.Name));
                ConnectDetails(DetailDef);
            }
        }
        ConnectDetails(Definition.Table);
        foreach (TableDef DetailDef in Definition.GetTables())
        {
            MemTable Table = NewTables.Get(DetailDef.Name);
            Table.RowDeleting += Table_RowDeleting;
            Table.ColumnChanging += (Sender, Args) => ColumnChanging((MemTable)Sender, Args);
            Table.ColumnChanged += (Sender, Args) => ColumnChanged((MemTable)Sender, Args);
            Table.NewRowAdding += (Sender, Args) =>
            {
                MemTable Source = (MemTable)Sender;
                if (Source.Master != null)
                    SetDefaultValues(Source, Args.Row);
                NewRowAdding(Source, Args);
            };
            Table.NewRowAdded += (Sender, Args) => NewRowAdded((MemTable)Sender, Args);
        }
        MemTable NewItemTable = NewTables.Get(Definition.Table.Name);
        NewItemTable.DetailsActive = true;
        MemTable NewListTable = new("List");
        TableList NewStocks = new();
        foreach (SelectDef StockDef in Definition.Stocks)
        {
            string SqlText = StockDef.SqlText;
            if (string.IsNullOrWhiteSpace(SqlText))
            {
                DataTableDef.ValidateIdentifier(StockDef.Name);
                SqlText = $"select * from {StockDef.Name}";
            }
            MemTable Stock = new(StockDef.Name);
            Stock.Sqls.SelectSql = SqlText;
            NewStocks.Add(Stock);
            NewStore.SelectTo(Stock, SqlText);
        }
        TableSetFlags Flags = Definition.CascadeDeletes ? TableSetFlags.None : TableSetFlags.NoCascadeDeletes;
        TableSet NewTableSet = new(NewStore, NewListTable, NewItemTable, Flags, Definition.RowVersionFieldName);
        NewTableSet.TransactionStageCommit += HandleTransactionStageCommit;
        NewTableSet.TransactionStageDelete += TableSet_TransactionStageDelete;

        if (Definition.SelectList.Count == 0)
        {
            SelectDef DefaultSelect = new()
            {
                Name = "DEFAULT",
                SqlText = $"select * from {Definition.Table.Name}",
                Owner = Definition
            };
            DefaultSelect.CheckDescriptor();
            Definition.SelectList.Add(DefaultSelect);
        }

        Store = NewStore;
        Tables = NewTables;
        Stocks = NewStocks;
        ItemTable = NewItemTable;
        ListTable = NewListTable;
        TableSet = NewTableSet;
        ModuleDef = Definition;
        try
        {
            LoadDefaultValues();
        }
        catch
        {
            ModuleDef = null;
            TableSet = null;
            Store = null;
            Tables = null;
            Stocks = null;
            ItemTable = null;
            ListTable = null;
            throw;
        }
    }

    // ● list
    /// <summary>
    /// Selects the list using a SELECT definition.
    /// </summary>
    public virtual int ListSelect(SelectDef Select)
    {
        LastTableSetResult = null;
        ArgumentNullException.ThrowIfNull(Select);
        return ListSelect(Select.SqlText);
    }
    /// <summary>
    /// Selects the list using the supplied SQL statement. The framework applies
    /// the row limit; the caller must not include a limit in the list SELECT.
    /// </summary>
    public virtual int ListSelect(string SqlText) => ListSelect(SqlText, new SqlParams());
    /// <summary>
    /// Selects the list using the supplied SQL statement and parameters.
    /// The framework applies the row limit.
    /// </summary>
    public virtual int ListSelect(string SqlText, SqlParams Params)
    {
        LastTableSetResult = null;
        if (string.IsNullOrWhiteSpace(SqlText))
            return 0;
        int RowLimit = DbConfig.DefaultRowLimit;
        if (RowLimit <= 0)
            throw new InvalidOperationException("The data module list row limit must be positive.");
        TableSetResult Result = TableSet.ListSelect(Store.ApplyRowLimit(SqlText, RowLimit), Params);
        LastTableSetResult = Result;
        if (Result.Exception != null)
            ExceptionDispatchInfo.Capture(Result.Exception).Throw();
        return Result.RowCount;
    }
    /// <summary>
    /// Saves changes made to the list table.
    /// </summary>
    public virtual void ListSave()
    {
        LastTableSetResult = null;
        TableSetResult Result = TableSet.ListSave();
        LastTableSetResult = Result;
        if (Result.Exception != null)
            ExceptionDispatchInfo.Capture(Result.Exception).Throw();
    }
    /// <summary>
    /// Rejects changes made to the list table.
    /// </summary>
    public virtual void ListCancel() => TableSet.ListCancel();

    // ● item
    /// <summary>
    /// Prepares a new item, rejecting the operation if the current item has changes.
    /// </summary>
    public virtual void Insert()
    {
        LastTableSetResult = null;
        CheckCanInsert();
        fInserting++;
        try
        {
            Inserting();
            State = DataMode.None;
            try
            {
                TableSet.ProcessInsert();
                SetDefaultValues();
                SetAuditValues();
            }
            catch
            {
                TableSet.ProcessEmpty();
                throw;
            }
            State = DataMode.Insert;
            Inserted();
        }
        finally
        {
            fInserting--;
        }
    }
    /// <summary>
    /// Loads an item for editing and returns false when its ID is invalid or not found.
    /// Rejects the operation if the current item has unsaved changes.
    /// </summary>
    public virtual bool Edit(object RowId)
    {
        LastTableSetResult = null;
        CheckCanEdit(RowId);
        fEditing++;
        try
        {
            Editing(RowId);
            State = DataMode.None;
            TableSetResult Result = TableSet.Load(RowId);
            LastTableSetResult = Result;
            if (Result.Status == TableSetResultStatus.LoadFailed)
                ExceptionDispatchInfo.Capture(Result.Exception).Throw();
            if (Result.Status != TableSetResultStatus.Success)
                return false;
            LastEditedId = RowId;
            State = DataMode.Edit;
            Edited(RowId);
            return true;
        }
        finally
        {
            fEditing--;
        }
    }
    /// <summary>
    /// Deletes an item, rejecting the operation if the current item has changes.
    /// </summary>
    public virtual void Delete(object RowId)
    {
        LastTableSetResult = null;
        CheckCanDelete(RowId);
        fDeleting++;
        try
        {
            Deleting(RowId);
            State = DataMode.None;
            TableSetResult Result = TableSet.Delete(RowId);
            LastTableSetResult = Result;
            if (Result.Status == TableSetResultStatus.Success || Result.Status == TableSetResultStatus.AfterCommitFailed)
                LastDeletedId = RowId;
            if (Result.Exception != null)
                ExceptionDispatchInfo.Capture(Result.Exception).Throw();
            if (Result.Status == TableSetResultStatus.Success)
                Deleted(RowId);
        }
        finally
        {
            fDeleting--;
        }
    }
    /// <summary>
    /// Saves the item tree and returns the committed item ID.
    /// Reselect explicitly specifies whether to reload the item after saving.
    /// </summary>
    public virtual object Commit(bool Reselect)
    {
        LastTableSetResult = null;
        EndEdit();
        SetAuditValues();
        CheckCanCommit(Reselect);
        fCommitting++;
        try
        {
            Committing(Reselect);
            TableSetResult Result = TableSet.Commit(Reselect);
            LastTableSetResult = Result;
            if (Result.Status == TableSetResultStatus.Success || Result.Status == TableSetResultStatus.AfterCommitFailed)
            {
                LastCommittedId = Result.Id;
                State = Result.Status == TableSetResultStatus.Success ? DataMode.Edit : DataMode.None;
            }
            if (Result.Exception != null)
                ExceptionDispatchInfo.Capture(Result.Exception).Throw();
            Committed(Reselect, Result.Id);
            return Result.Id;
        }
        finally
        {
            fCommitting--;
        }
    }
    /// <summary>
    /// Rejects changes in the item tree without selecting a different row.
    /// </summary>
    public virtual void Cancel()
    {
        if (State == DataMode.None)
            return;
        DataMode PreviousState = State;
        TableSet.RejectChanges();
        if (PreviousState == DataMode.Insert)
            State = DataMode.None;
    }
    /// <summary>
    /// Returns whether the item tree has unsaved changes.
    /// </summary>
    public virtual bool HasChanges() => TableSet.HasChanges();

    // ● item checks
    /// <summary>
    /// Checks whether insertion can begin; throws if the item tree has unsaved changes.
    /// </summary>
    public virtual void CheckCanInsert()
    {
        if (HasChanges())
            throw new InvalidOperationException("Cannot insert while the current item has unsaved changes.");
    }
    /// <summary>
    /// Checks whether the item can be loaded; throws if the item tree has unsaved changes.
    /// </summary>
    public virtual void CheckCanEdit(object RowId)
    {
        if (HasChanges())
            throw new InvalidOperationException("Cannot edit while the current item has unsaved changes.");
    }
    /// <summary>
    /// Checks whether the item can be deleted; throws if the item tree has unsaved changes.
    /// </summary>
    public virtual void CheckCanDelete(object RowId)
    {
        if (HasChanges())
            throw new InvalidOperationException("Cannot delete while the current item has unsaved changes.");
    }
    /// <summary>
    /// Checks whether the item tree can be saved.
    /// </summary>
    public virtual void CheckCanCommit(bool Reselect) { }

    // ● list selection
    /// <summary>
    /// Returns a named list SELECT, or the first SELECT when no name is supplied.
    /// </summary>
    protected virtual SelectDef GetListSelectDef(string SelectName)
    {
        if (!string.IsNullOrWhiteSpace(SelectName))
            return ModuleDef.SelectList.Get(SelectName);
        if (ModuleDef.SelectList.Count == 0)
            throw new InvalidOperationException($"Data module '{Name}' has no list SELECT.");
        return ModuleDef.SelectList[0];
    }

    // ● protected JSON filter operations
    /// <summary>
    /// Converts an active filter value using its registered data type.
    /// Boolean values use integer database representation.
    /// </summary>
    protected virtual object ConvertSelectFilterValue(object Value, DataFieldType DataType)
    {
        if (Value == null || Value == DBNull.Value)
            return null;
        if (Value is JsonElement Element)
        {
            if (Element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return null;
            Value = Element.ValueKind == JsonValueKind.String ? Element.GetString() : Element.ToString();
        }
        var Culture = System.Globalization.CultureInfo.InvariantCulture;
        return DataType switch
        {
            DataFieldType.String => Convert.ToString(Value, Culture),
            DataFieldType.Integer => Convert.ToInt32(Value, Culture),
            DataFieldType.Int64 => Convert.ToInt64(Value, Culture),
            DataFieldType.Decimal or DataFieldType.Decimal_ => Convert.ToDecimal(Value, Culture),
            DataFieldType.Double => Convert.ToDouble(Value, Culture),
            DataFieldType.Boolean => ConvertSelectFilterBoolean(Value),
            DataFieldType.Date or DataFieldType.DateTime => Convert.ToDateTime(Value, Culture),
            _ => throw new InvalidOperationException($"Unsupported filter data type: {DataType}"),
        };
    }
    /// <summary>
    /// Converts a Boolean list filter value to the database representation, 0 or 1.
    /// Derived modules can accept additional input formats by overriding this method.
    /// </summary>
    protected virtual int ConvertSelectFilterBoolean(object Value)
    {
        var Culture = System.Globalization.CultureInfo.InvariantCulture;
        if (bool.TryParse(Convert.ToString(Value, Culture), out bool Flag))
            return Flag ? 1 : 0;
        return Convert.ToInt32(Value, Culture) != 0 ? 1 : 0;
    }
    /// <summary>
    /// Returns the condition operators allowed for a filter data type.
    /// </summary>
    protected virtual ConditionOp[] GetFilterConditionOps(DataFieldType DataType)
    {
        return DataType switch
        {
            DataFieldType.Boolean => new[] { ConditionOp.Equal },
            DataFieldType.String => new[] { ConditionOp.Equal, ConditionOp.Contains, ConditionOp.StartsWith, ConditionOp.EndsWith },
            DataFieldType.Integer or DataFieldType.Int64 or DataFieldType.Decimal or DataFieldType.Decimal_ or DataFieldType.Double or DataFieldType.Date or DataFieldType.DateTime => new[] { ConditionOp.Equal, ConditionOp.GreaterOrEqual, ConditionOp.LessOrEqual, ConditionOp.Between },
            _ => Array.Empty<ConditionOp>(),
        };
    }
    /// <summary>
    /// Validates and creates one active filter, or returns null when its values are incomplete.
    /// </summary>
    protected virtual SqlFilterDef CreateSelectFilter(SelectDef Select, JsonSelectFilter Filter)
    {
        if (Filter == null || string.IsNullOrWhiteSpace(Filter.Name))
            throw new InvalidOperationException("A filter name is required.");
        if (!string.IsNullOrWhiteSpace(Filter.SelectName) && !string.Equals(Filter.SelectName, Select.Name, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Filter SELECT does not match '{Select.Name}'.");
        SqlFilterDef Source = Select.FilterDefs.Find(Filter.Name) ?? throw new InvalidOperationException($"Filter '{Filter.Name}' is not defined for SELECT '{Select.Name}'.");
        BoolOp BoolOp = BoolOp.And;
        if (!string.IsNullOrWhiteSpace(Filter.BoolOp) && !Enum.TryParse(Filter.BoolOp, true, out BoolOp))
            throw new InvalidOperationException($"Invalid filter boolean operator: {Filter.BoolOp}");
        if (BoolOp != BoolOp.And && BoolOp != BoolOp.Or)
            throw new InvalidOperationException($"Unsupported filter boolean operator: {BoolOp}");
        ConditionOp ConditionOp = Source.ConditionOp;
        if (!string.IsNullOrWhiteSpace(Filter.ConditionOp) && !Enum.TryParse(Filter.ConditionOp, true, out ConditionOp))
            throw new InvalidOperationException($"Invalid filter condition operator: {Filter.ConditionOp}");
        if (!GetFilterConditionOps(Source.FilterDataType).Contains(ConditionOp))
            throw new InvalidOperationException($"Unsupported condition operator {ConditionOp} for filter '{Source.Name}'.");
        object Value = ConvertSelectFilterValue(Filter.Value, Source.FilterDataType);
        object Value2 = ConditionOp == ConditionOp.Between ? ConvertSelectFilterValue(Filter.Value2, Source.FilterDataType) : null;
        if (Value == null || (ConditionOp == ConditionOp.Between && Value2 == null))
            return null;
        return new SqlFilterDef
        {
            Name = Source.Name,
            FieldName = Source.FieldName,
            FilterDataType = Source.FilterDataType,
            BoolOp = BoolOp,
            ConditionOp = ConditionOp,
            TitleKey = Source.TitleKey,
            Value = Value,
            Value2 = Value2,
        };
    }
    /// <summary>
    /// Validates active filters against a SELECT's registered filter definitions.
    /// </summary>
    protected virtual SqlFilterDefs CreateSelectFilters(SelectDef Select, JsonSelectFilters Filters)
    {
        SqlFilterDefs Result = new() { AllowDuplicateNames = true };
        if (Filters != null)
        {
            foreach (JsonSelectFilter Filter in Filters)
            {
                SqlFilterDef Active = CreateSelectFilter(Select, Filter);
                if (Active != null)
                    Result.Add(Active);
            }
        }
        return Result;
    }
   
    /// <summary>
    /// Prepares a registered list SELECT for execution with the requested filters.
    /// With no active filters, returns the registered SQL as written. Otherwise,
    /// wraps it in an outer SELECT and applies the filters to its result columns.
    /// Filter values go in Params and are bound when the query runs.
    /// ListSelect executes the returned SQL and applies the global row limit.
    /// </summary>
    protected virtual string BuildListSelectSql(SelectDef Select, JsonSelectFilters Filters, out SqlParams Params)
    {
        if (string.IsNullOrWhiteSpace(Select.SqlText))
            throw new InvalidOperationException($"List SELECT '{Select.Name}' has no SQL statement.");
        SqlFilterDefs ActiveFilters = CreateSelectFilters(Select, Filters);
        Params = new();
        if (ActiveFilters.Count == 0)
            return Select.SqlText;

        // Validate the original statement before it becomes a nested query.
        Store.ApplyRowLimit(Select.SqlText, DbConfig.DefaultRowLimit);
        string Where = SqlWhereFilterFormatter.FormatParameterized(ActiveFilters, Params, Select.SqlText);
        string InnerSql = SqlTextScanner.PrepareForAppend(Select.SqlText);
        return $"select * from ({InnerSql}) X where {Where}";
    }
    
    // ● batch operations
    /// <summary>
    /// Posts items prepared by the callbacks in groups of transactions. Each post
    /// completes pending edits and invokes CheckCanCommit before entering the table set.
    /// </summary>
    public virtual BatchCommitResult CommitBatch(BatchCommitArgs Args)
    {
        ArgumentNullException.ThrowIfNull(Args);
        BatchCommitArgs Inner = null;
        Inner = new BatchCommitArgs(() =>
        {
            Args.Counter = Inner.Counter;
            Args.PostCounter = Inner.PostCounter;
            bool ShouldPost = Args.BeforeFunc();
            if (ShouldPost)
            {
                EndEdit();
                SetAuditValues();
                CheckCanCommit(false);
            }
            return ShouldPost;
        }, Id =>
        {
            Args.Counter = Inner.Counter;
            Args.PostCounter = Inner.PostCounter;
            return Args.AfterFunc(Id);
        }, Args.TransLimit) { Tag = Args.Tag };
        fCommitting++;
        try
        {
            BatchCommitResult Result = TableSet.CommitBatch(Inner);
            if (ItemTable.Rows.Count == 0)
                State = DataMode.None;
            else if (ItemTable.Rows[0].RowState == DataRowState.Unchanged)
                State = DataMode.Edit;
            return Result;
        }
        finally
        {
            Args.Counter = Inner.Counter;
            Args.PostCounter = Inner.PostCounter;
            fCommitting--;
        }
    }
    /// <summary>
    /// Inserts source rows through CommitBatch using matching top-table fields.
    /// Non-database fields, fields excluded from INSERT and the row version field
    /// are not copied from the source. A non-empty source code is retained; an empty
    /// code is generated when a code provider is configured. Manually supplied codes
    /// do not advance SYS_NUMBER_SERIES. The caller must ensure that the series cannot
    /// produce conflicting codes, including during the batch and in subsequent use.
    /// The source table is not modified. An empty source succeeds without a post.
    /// </summary>
    public virtual BatchCommitResult BatchInsert(DataTable Source)
    {
        ArgumentNullException.ThrowIfNull(Source);
        if (!IsInitialized)
            throw new InvalidOperationException("The data module must be initialized.");
        if (ReferenceEquals(Source, ItemTable))
            throw new ArgumentException("The source table must be separate from the item table.", nameof(Source));
        DataRow[] Rows = Source.Rows.Cast<DataRow>().ToArray();
        if (Rows.Length == 0)
            return new BatchCommitResult(BatchCommitStatus.Success, 0, -1, null);

        List<(DataColumn Source, DataColumn Target)> Columns = new();
        HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase);
        foreach (DataColumn SourceColumn in Source.Columns)
        {
            FieldDef Field = ModuleDef.Table.Fields.Find(SourceColumn.ColumnName);
            if (Field == null || !Field.IsNativeField || Field.IsNoInsertOrUpdate)
                continue;
            if (!string.IsNullOrWhiteSpace(ModuleDef.RowVersionFieldName) && string.Equals(Field.Name, ModuleDef.RowVersionFieldName, StringComparison.OrdinalIgnoreCase))
                continue;
            DataColumn TargetColumn = ItemTable.Columns[Field.Name]
                ?? throw new InvalidOperationException($"Target column '{Field.Name}' was not found.");
            if (!Names.Add(TargetColumn.ColumnName))
                throw new ArgumentException($"Multiple source columns match '{TargetColumn.ColumnName}'.", nameof(Source));
            if (SourceColumn.DataType != TargetColumn.DataType)
                throw new ArgumentException($"Column type mismatch for '{SourceColumn.ColumnName}'.", nameof(Source));
            Columns.Add((SourceColumn, TargetColumn));
        }

        BatchCommitArgs Args = null;
        Args = new BatchCommitArgs(() =>
        {
            DataRow SourceRow = Rows[Args.Counter];
            Insert();
            DataRow TargetRow = ItemTable.Rows[0];
            foreach ((DataColumn SourceColumn, DataColumn TargetColumn) in Columns)
                TargetRow[TargetColumn] = SourceRow[SourceColumn];
            return true;
        }, _ => Args.Counter < Rows.Length - 1);
        return CommitBatch(Args);
    }

    // ● JSON operations
    /// <summary>
    /// Returns the initialized module as a JSON packet.
    /// </summary>
    public virtual JsonDataModule JsonInitialize() => new(this);
    /// <summary>
    /// Prepares a new item and returns the module as a JSON packet.
    /// </summary>
    public virtual JsonDataModule JsonInsert()
    {
        Insert();
        return new JsonDataModule(this);
    }
    /// <summary>
    /// Executes a named list SELECT and returns the table packet and selected name.
    /// An empty name selects the first registered SELECT.
    /// </summary>
    public virtual JsonDataTable JsonSelectList(string SelectName, out string SelectedName)
        => JsonSelectList(SelectName, null, out SelectedName);
    /// <summary>
    /// Executes a named list SELECT with validated active filters.
    /// </summary>
    public virtual JsonDataTable JsonSelectList(string SelectName, JsonSelectFilters Filters, out string SelectedName)
    {
        LastTableSetResult = null;
        SelectDef Select = GetListSelectDef(SelectName);
        SelectedName = Select.Name;
        string SqlText = BuildListSelectSql(Select, Filters, out SqlParams Params);
        if (Params.Count == 0 && SqlText == Select.SqlText)
            ListSelect(Select);
        else
            ListSelect(SqlText, Params);
        return new JsonDataTable(ListTable, Select: Select);
    }
    /// <summary>
    /// Loads an item by text ID and returns its module packet.
    /// Throws when the item does not exist or the ID is invalid.
    /// </summary>
    public virtual JsonDataModule JsonEdit(string Id)
    {
        LastTableSetResult = null;
        if (string.IsNullOrWhiteSpace(Id))
            throw new ArgumentException("An item ID is required.", nameof(Id));
        object RowId = JsonConvertRowId(Id);
        if (!Edit(RowId))
            throw new KeyNotFoundException($"Item '{Id}' was not found in data module '{Name}'.");
        return new JsonDataModule(this);
    }

    /// <summary>
    /// Applies the item rows from a JSON packet, saves them and returns the reloaded module.
    /// </summary>
    public virtual JsonDataModule JsonCommit(JsonDataModule Source)
    {
        LastTableSetResult = null;
        ArgumentNullException.ThrowIfNull(Source);
        State = (DataMode)Source.State;
        ItemTable.EventsDisabled = true;
        try
        {
            List<DataRow> DeferredDeletes = new();
            JsonApplyTableRows(ItemTable, Source, DeferredDeletes);
            foreach (DataRow Row in DeferredDeletes.OrderByDescending(Row => ((MemTable)Row.Table).Level))
                Row.Delete();
        }
        finally
        {
            ItemTable.EventsDisabled = false;
        }
        Commit(true);
        return new JsonDataModule(this);
    }

    /// <summary>
    /// Deletes an item by text ID. An item that is already absent is considered deleted.
    /// </summary>
    public virtual void JsonDelete(string Id)
    {
        LastTableSetResult = null;
        if (string.IsNullOrWhiteSpace(Id))
            throw new ArgumentException("An item ID is required.", nameof(Id));
        Delete(JsonConvertRowId(Id));
    }

    // ● table lookup
    /// <summary>
    /// Finds an item table by name, or returns null when it does not exist.
    /// </summary>
    public virtual MemTable FindTable(string TableName) => Tables.Find(TableName);
    /// <summary>
    /// Returns whether the item tree contains a table with the specified name.
    /// </summary>
    public bool TableExists(string TableName) => FindTable(TableName) != null;

    /// <summary>
    /// Gets an item table by name, or throws when it does not exist.
    /// </summary>
    public virtual MemTable GetTable(string TableName) => Tables.Get(TableName);

    // ● properties
    /// <summary>
    /// Gets an item tree table by name, including the top table and its details.
    /// Use <see cref="ListTable"/> and <see cref="Stocks"/> to access the other tables.
    /// </summary>
    public MemTable this[string TableName] => GetTable(TableName);

    /// <summary>
    /// Gets the session or request context supplied during initialization.
    /// </summary>
    public UserContext UserContext { get; private set; }
    /// <summary>
    /// Gets the application user ID used for item audit fields.
    /// </summary>
    public string CurrentUserId => UserContext?.CurrentUser?.Id;
    /// <summary>
    /// Gets whether this module has been initialized successfully.
    /// </summary>
    public bool IsInitialized => ModuleDef != null;
    /// <summary>
    /// Gets the name of the initialized module.
    /// </summary>
    public string Name => ModuleDef.Name;
    /// <summary>
    /// Gets the item table as a row provider host for the initialized module.
    /// Consumers can use this host to obtain a row provider for the item and its details.
    /// </summary>
    public IRowProviderHost RowProviderHost => ItemTable;

    /// <summary>
    /// Gets the current item row without changing the selection.
    /// </summary>
    public virtual DataRow CurrentRow => ItemTable.CurrentRow;
    /// <summary>
    /// Gets the current item key, or DBNull.Value when there is no current row.
    /// </summary>
    public virtual object Id => CurrentRow != null ? CurrentRow[ItemTable.KeyField] : DBNull.Value;
    /// <summary>
    /// Gets whether an insert operation is executing.
    /// </summary>
    public bool IsInserting => fInserting > 0;
    /// <summary>
    /// Gets whether an edit operation is executing.
    /// </summary>
    public bool IsEditing => fEditing > 0;
    /// <summary>
    /// Gets whether a delete operation is executing.
    /// </summary>
    public bool IsDeleting => fDeleting > 0;
    /// <summary>
    /// Gets whether a commit operation is executing.
    /// </summary>
    public bool IsCommitting => fCommitting > 0;
    /// <summary>
    /// Gets the current item state.
    /// </summary>
    public DataMode State { get; protected set; } = DataMode.None;
    /// <summary>
    /// Gets the latest TableSet result for the current operation, or null when no
    /// TableSet result was produced. Reset before starting a new operation.
    /// </summary>
    public TableSetResult LastTableSetResult { get; private set; }
    /// <summary>
    /// Gets the ID of the last item successfully loaded for editing.
    /// </summary>
    public object LastEditedId { get; protected set; }
    /// <summary>
    /// Gets the ID of the last item whose deletion was committed.
    /// </summary>
    public object LastDeletedId { get; protected set; }
    /// <summary>
    /// Gets the ID of the last item whose save was committed.
    /// </summary>
    public object LastCommittedId { get; protected set; }
    /// <summary>
    /// Gets the definition used to initialize this module.
    /// </summary>
    public ModuleDef ModuleDef { get; private set; }
    /// <summary>
    /// Gets the SQL store associated with this module.
    /// </summary>
    public SqlStore Store { get; private set; }
    /// <summary>
    /// Gets the item tree tables.
    /// </summary>
    public TableList Tables { get; private set; }
    /// <summary>
    /// Gets the independent stock tables used for lookups and other reference data.
    /// </summary>
    public TableList Stocks { get; private set; }
    /// <summary>
    /// Gets the top table of the item tree.
    /// </summary>
    public MemTable ItemTable { get; private set; }
    /// <summary>
    /// Gets or sets the item tree's master-detail synchronization state after initialization.
    /// This forwards to ItemTable.DetailsActive, which counts activations.
    /// Pair each additional activation with a corresponding deactivation.
    /// </summary>
    public bool DetailsActive
    {
        get => ItemTable.DetailsActive;
        set => ItemTable.DetailsActive = value;
    }
    /// <summary>
    /// Gets the separate list table.
    /// </summary>
    public MemTable ListTable { get; private set; }
}
