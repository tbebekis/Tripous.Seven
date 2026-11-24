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

    // ● private
    void Table_RowDeleting(object Sender, DataRowChangeEventArgs Args)
    {
        MemTable Table = (MemTable)Sender;
        if (ModuleDef.CascadeDeletes)
            Table.DeleteDetailRows(Args.Row);
        RowDeleting(Table, Args);
    }

    // ● protected fields
    /// <summary>
    /// The table operations used by this module.
    /// </summary>
    protected TableSet TableSet;

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

    // ● initialization
    /// <summary>
    /// Initializes the module from its definition and configured connection.
    /// Throws if this instance was already successfully initialized.
    /// </summary>
    public virtual void Initialize(ModuleDef Definition)
    {
        if (ModuleDef != null)
            throw new InvalidOperationException("The data module is already initialized.");
        ArgumentNullException.ThrowIfNull(Definition);
        if (Definition.Table == null)
            throw new InvalidOperationException("The data module requires a top table definition.");

        SqlStore NewStore = SqlStores.CreateSqlStore(Definition.ConnectionName);
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
            StockDef.CheckDescriptor();
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
        TableSet NewTableSet = new(NewStore, NewListTable, NewItemTable, Flags);
        NewTableSet.TransactionStageCommit += TableSet_TransactionStageCommit;
        NewTableSet.TransactionStageDelete += TableSet_TransactionStageDelete;

        if (Definition.SelectList.Count == 0)
        {
            SelectDef DefaultSelect = Definition.SelectList.Add("DEFAULT", $"select * from {Definition.Table.Name}");
            DefaultSelect.Owner = Definition;
        }

        Store = NewStore;
        Tables = NewTables;
        Stocks = NewStocks;
        ItemTable = NewItemTable;
        ListTable = NewListTable;
        TableSet = NewTableSet;
        ModuleDef = Definition;
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
    public virtual int ListSelect(string SqlText)
    {
        LastTableSetResult = null;
        if (string.IsNullOrWhiteSpace(SqlText))
            return 0;
        int RowLimit = DbConfig.DefaultRowLimit;
        if (RowLimit <= 0)
            throw new InvalidOperationException("The data module list row limit must be positive.");
        TableSetResult Result = TableSet.ListSelect(Store.ApplyRowLimit(SqlText, RowLimit));
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
    {
        LastTableSetResult = null;
        SelectDef Select = GetListSelectDef(SelectName);
        SelectedName = Select.Name;
        if (string.IsNullOrWhiteSpace(Select.SqlText))
            throw new InvalidOperationException($"List SELECT '{SelectedName}' has no SQL statement.");
        ListSelect(Select);
        return new JsonDataTable(ListTable);
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
    /// Gets an item table by name, or throws when it does not exist.
    /// </summary>
    public virtual MemTable GetTable(string TableName) => Tables.Get(TableName);

    // ● properties
    /// <summary>
    /// Gets whether this module has been initialized successfully.
    /// </summary>
    public bool IsInitialized => ModuleDef != null;
    /// <summary>
    /// Gets the name of the initialized module.
    /// </summary>
    public string Name => ModuleDef.Name;
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
    /// Gets the separate list table.
    /// </summary>
    public MemTable ListTable { get; private set; }
}
