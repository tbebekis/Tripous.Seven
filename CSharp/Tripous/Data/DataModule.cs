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
        TableSet NewTableSet = new(NewStore, NewListTable, NewItemTable);
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
        ArgumentNullException.ThrowIfNull(Select);
        return ListSelect(Select.SqlText);
    }
    /// <summary>
    /// Selects the list using the supplied SQL statement. The framework applies
    /// the row limit; the caller must not include a limit in the list SELECT.
    /// </summary>
    public virtual int ListSelect(string SqlText)
    {
        if (string.IsNullOrWhiteSpace(SqlText))
            return 0;
        int RowLimit = DbConfig.DefaultRowLimit;
        if (RowLimit <= 0)
            throw new InvalidOperationException("The data module list row limit must be positive.");
        TableSetResult Result = TableSet.ListSelect(Store.ApplyRowLimit(SqlText, RowLimit));
        if (Result.Exception != null)
            ExceptionDispatchInfo.Capture(Result.Exception).Throw();
        return Result.RowCount;
    }
    /// <summary>
    /// Saves changes made to the list table.
    /// </summary>
    public virtual void ListSave()
    {
        TableSetResult Result = TableSet.ListSave();
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
        CheckCanEdit(RowId);
        fEditing++;
        try
        {
            Editing(RowId);
            State = DataMode.None;
            TableSetResult Result = TableSet.Load(RowId);
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
        CheckCanDelete(RowId);
        fDeleting++;
        try
        {
            Deleting(RowId);
            State = DataMode.None;
            TableSetResult Result = TableSet.Delete(RowId);
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
        EndEdit();
        CheckCanCommit(Reselect);
        fCommitting++;
        try
        {
            Committing(Reselect);
            TableSetResult Result = TableSet.Commit(Reselect);
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
