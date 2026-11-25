/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests data module creation and initialization.
/// </summary>
[Collection("Database registry")]
public class DataModuleTests
{
    // ● public types
    /// <summary>
    /// A custom data module used to verify class selection.
    /// </summary>
    public class CustomModule : DataModule { }

    /// <summary>
    /// Records insert hook order and initializes a default value.
    /// </summary>
    public class InsertModule : DataModule
    {
        /// <summary>
        /// Gets the recorded hook calls and states.
        /// </summary>
        public List<string> Calls { get; } = new();
        /// <summary>
        /// Records the state before insertion.
        /// </summary>
        protected override void Inserting() => Calls.Add($"Inserting:{State}");
        /// <summary>
        /// Applies a default value to the new item.
        /// </summary>
        protected override void SetDefaultValues()
        {
            Calls.Add($"Defaults:{State}");
            ItemTable.Rows[0]["Name"] = "Default";
        }
        /// <summary>
        /// Records the state after insertion.
        /// </summary>
        protected override void Inserted() => Calls.Add($"Inserted:{State}");
        /// <summary>
        /// Applies a default to each new address row.
        /// </summary>
        protected override void SetDefaultValues(MemTable Table, DataRow Row)
        {
            Calls.Add($"DetailDefault:{Table.TableName}");
            if (Table.TableName == "Address")
                Row["City"] = "Default city";
        }
        /// <summary>
        /// Records the state before editing.
        /// </summary>
        protected override void Editing(object RowId) => Calls.Add($"Editing:{State}");
        /// <summary>
        /// Records the state after editing.
        /// </summary>
        protected override void Edited(object RowId) => Calls.Add($"Edited:{State}");
        /// <summary>
        /// Records the state before deletion.
        /// </summary>
        protected override void Deleting(object RowId) => Calls.Add($"Deleting:{State}");
        /// <summary>
        /// Records the state after deletion.
        /// </summary>
        protected override void Deleted(object RowId) => Calls.Add($"Deleted:{State}");
        /// <summary>
        /// Records the state before saving.
        /// </summary>
        protected override void Committing(bool Reselect) => Calls.Add($"Committing:{State}:{Reselect}");
        /// <summary>
        /// Records the state after saving.
        /// </summary>
        protected override void Committed(bool Reselect, object RowId) => Calls.Add($"Committed:{State}:{Reselect}");
        /// <summary>
        /// Raises an error after the save commits.
        /// </summary>
        public void FailAfterCommit() => TableSet.TransactionStageCommit += (Sender, Args) =>
        {
            if (Args.Stage == TransactionStage.Commit && Args.ExecTime == ExecTime.After)
                throw new InvalidOperationException("After commit failed.");
        };
        /// <summary>
        /// Raises an error after the deletion commits.
        /// </summary>
        public void FailAfterDelete() => TableSet.TransactionStageDelete += (Sender, Args) =>
        {
            if (Args.Stage == TransactionStage.Commit && Args.ExecTime == ExecTime.After)
                throw new InvalidOperationException("After delete failed.");
        };
    }

    /// <summary>
    /// A module that rejects operations through its overridable checks.
    /// </summary>
    public class CheckedModule : DataModule
    {
        /// <summary>
        /// Skips table editing for this check-only test module.
        /// </summary>
        protected override void EndEdit() { }
        /// <summary>
        /// Rejects insertion.
        /// </summary>
        public override void CheckCanInsert() => throw new InvalidOperationException("Insert check");
        /// <summary>
        /// Rejects editing.
        /// </summary>
        public override void CheckCanEdit(object RowId) => throw new InvalidOperationException("Edit check");
        /// <summary>
        /// Rejects deletion.
        /// </summary>
        public override void CheckCanDelete(object RowId) => throw new InvalidOperationException("Delete check");
        /// <summary>
        /// Rejects saving.
        /// </summary>
        public override void CheckCanCommit(bool Reselect) => throw new InvalidOperationException("Commit check");
    }

    /// <summary>
    /// Records transaction stages and writes audit entries inside the transaction.
    /// </summary>
    public class TransactionModule : DataModule
    {
        /// <summary>
        /// Gets the recorded transaction notifications.
        /// </summary>
        public List<string> Stages { get; } = new();
        /// <summary>
        /// Writes an audit entry after item changes have been posted.
        /// </summary>
        protected override void TableSet_TransactionStageCommit(object Sender, TransactionEventArgs Args)
        {
            Stages.Add($"Commit:{Args.Stage}:{Args.ExecTime}");
            if (Args.Stage == TransactionStage.Post && Args.ExecTime == ExecTime.After)
                Args.Store.ExecSql(Args.Transaction, "INSERT INTO Audit (Message) VALUES ('save')");
        }
        /// <summary>
        /// Records delete transaction notifications.
        /// </summary>
        protected override void TableSet_TransactionStageDelete(object Sender, TransactionEventArgs Args)
        {
            Stages.Add($"Delete:{Args.Stage}:{Args.ExecTime}");
            if (Args.Stage == TransactionStage.Post && Args.ExecTime == ExecTime.After)
                Args.Store.ExecSql(Args.Transaction, "INSERT INTO Audit (Message) VALUES ('delete')");
        }
    }

    /// <summary>
    /// Records item table events and checks default values before row insertion.
    /// </summary>
    public class TableEventsModule : DataModule
    {
        /// <summary>
        /// Gets recorded event calls.
        /// </summary>
        public List<string> Calls { get; } = new();
        /// <summary>
        /// Sets a default on a newly created detail row.
        /// </summary>
        protected override void SetDefaultValues(MemTable Table, DataRow Row)
        {
            Row["City"] = "Athens";
            Calls.Add("Default");
        }
        /// <summary>
        /// Records the row before it is added.
        /// </summary>
        protected override void NewRowAdding(MemTable Table, DataTableNewRowEventArgs Args)
        {
            if (Table.TableName == "Address")
                Calls.Add($"Adding:{Args.Row["City"]}:{Args.Row.RowState}");
        }
        /// <summary>
        /// Records the row after it is added.
        /// </summary>
        protected override void NewRowAdded(MemTable Table, DataTableNewRowEventArgs Args)
        {
            if (Table.TableName == "Address")
                Calls.Add($"Added:{Args.Row.RowState}");
        }
        /// <summary>
        /// Records changes before a value is assigned.
        /// </summary>
        protected override void ColumnChanging(MemTable Table, DataColumnChangeEventArgs Args)
        {
            if (Table.TableName == "Address" && Args.Column.ColumnName == "City")
                Calls.Add("Changing");
        }
        /// <summary>
        /// Records changes after a value is assigned.
        /// </summary>
        protected override void ColumnChanged(MemTable Table, DataColumnChangeEventArgs Args)
        {
            if (Table.TableName == "Address" && Args.Column.ColumnName == "City")
                Calls.Add("Changed");
        }
    }

    /// <summary>
    /// Checks operation flags in hooks, including nested insert calls.
    /// </summary>
    public class OperationFlagsModule : DataModule
    {
        int Depth;
        /// <summary>
        /// Skips table editing for this hook-only test module.
        /// </summary>
        protected override void EndEdit() { }
        /// <summary>
        /// Gets whether the outer insert remained active after the nested call.
        /// </summary>
        public bool OuterInsertStillActive { get; private set; }
        /// <summary>
        /// Allows testing hooks without creating tables.
        /// </summary>
        public override void CheckCanInsert() { }
        /// <summary>
        /// Allows testing hooks without creating tables.
        /// </summary>
        public override void CheckCanEdit(object RowId) { }
        /// <summary>
        /// Allows testing hooks without creating tables.
        /// </summary>
        public override void CheckCanDelete(object RowId) { }
        /// <summary>
        /// Tests the nested insert counter and exception cleanup.
        /// </summary>
        protected override void Inserting()
        {
            if (!IsInserting) throw new Exception("Insert flag is not set.");
            if (Depth++ == 0)
            {
                try { Insert(); }
                catch (InvalidOperationException) { OuterInsertStillActive = IsInserting; }
            }
            throw new InvalidOperationException("Insert hook");
        }
        /// <summary>
        /// Checks the edit flag.
        /// </summary>
        protected override void Editing(object RowId) => throw new InvalidOperationException(IsEditing ? "Edit active" : "Edit inactive");
        /// <summary>
        /// Checks the delete flag.
        /// </summary>
        protected override void Deleting(object RowId) => throw new InvalidOperationException(IsDeleting ? "Delete active" : "Delete inactive");
        /// <summary>
        /// Checks the commit flag.
        /// </summary>
        protected override void Committing(bool Reselect) => throw new InvalidOperationException(IsCommitting ? "Commit active" : "Commit inactive");
    }

    /// <summary>
    /// Observes the committed row values during commit validation.
    /// </summary>
    public class CommitValidationModule : DataModule
    {
        /// <summary>
        /// Gets whether validation saw completed edits in item and detail rows.
        /// </summary>
        public bool SawFinishedEdits { get; private set; }
        /// <summary>
        /// Checks both the item and detail rows during validation.
        /// </summary>
        public override void CheckCanCommit(bool Reselect)
        {
            DataRow Item = ItemTable.Rows[0];
            DataRow Detail = Tables.Get("Address").Rows[0];
            SawFinishedEdits = !Item.HasVersion(DataRowVersion.Proposed)
                && !Detail.HasVersion(DataRowVersion.Proposed)
                && (string)Item["Name"] == "Final name"
                && (string)Detail["City"] == "Final city";
        }
    }

    // ● private
    static ModuleDef CreateDefinition(string Name, string ConnectionName)
    {
        ModuleDef Definition = new() { Name = Name, ConnectionName = ConnectionName };
        Definition.Table.Name = "Customer";
        Definition.Table.Fields.Add(new FieldDef { Name = "Id" });
        Definition.Table.Fields.Add(new FieldDef { Name = "Name" });
        TableDef Detail = new() { Name = "Address", MasterField = "Id", DetailField = "CustomerId" };
        Detail.Fields.Add(new FieldDef { Name = "Id" });
        Detail.Fields.Add(new FieldDef { Name = "CustomerId" });
        Detail.Fields.Add(new FieldDef { Name = "City" });
        Definition.Table.Details.Add(Detail);
        return Definition;
    }

    // ● public
    /// <summary>
    /// Each operation invokes its overridable check before proceeding.
    /// </summary>
    [Fact]
    public void ItemOperationsInvokeOverridableChecks()
    {
        CheckedModule Module = new();
        Assert.Equal("Insert check", Assert.Throws<InvalidOperationException>(() => Module.Insert()).Message);
        Assert.Equal("Edit check", Assert.Throws<InvalidOperationException>(() => Module.Edit("I1")).Message);
        Assert.Equal("Delete check", Assert.Throws<InvalidOperationException>(() => Module.Delete("I1")).Message);
        Assert.Equal("Commit check", Assert.Throws<InvalidOperationException>(() => Module.Commit(false)).Message);
    }
    /// <summary>
    /// Operation flags remain active during hooks and reset after exceptions and nested calls.
    /// </summary>
    [Fact]
    public void OperationFlagsTrackExecutionAndNestedCalls()
    {
        OperationFlagsModule Module = new();
        Assert.Throws<InvalidOperationException>(() => Module.Insert());
        Assert.True(Module.OuterInsertStillActive);
        Assert.False(Module.IsInserting);
        Assert.Equal("Edit active", Assert.Throws<InvalidOperationException>(() => Module.Edit("I1")).Message);
        Assert.False(Module.IsEditing);
        Assert.Equal("Delete active", Assert.Throws<InvalidOperationException>(() => Module.Delete("I1")).Message);
        Assert.False(Module.IsDeleting);
        Assert.Equal("Commit active", Assert.Throws<InvalidOperationException>(() => Module.Commit(false)).Message);
        Assert.False(Module.IsCommitting);
    }
    /// <summary>
    /// Data mode values remain compatible with the remote client contract.
    /// </summary>
    [Fact]
    public void DataModeUsesStableRemoteValues()
    {
        Assert.Equal(0, (int)DataMode.None);
        Assert.Equal(2, (int)DataMode.Insert);
        Assert.Equal(4, (int)DataMode.Edit);
    }
    /// <summary>
    /// Insert prepares defaults, exposes the new state to the after hook and protects changes.
    /// </summary>
    [Fact]
    public void InsertPreparesItemAndProtectsChanges()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = "Data Source=:memory:" });
            InsertModule Module = new();
            Module.Initialize(CreateDefinition("CustomerModule", ConnectionName));
            Module.Insert();
            Assert.Equal(DataMode.Insert, Module.State);
            Assert.Equal("Default", Assert.Single(Module.ItemTable.Rows.Cast<DataRow>())["Name"]);
            Assert.Equal(new[] { "Inserting:None", "Defaults:None", "Inserted:Insert" }, Module.Calls);
            Assert.True(Module.HasChanges());
            Assert.Throws<InvalidOperationException>(() => Module.Insert());
            Assert.Single(Module.ItemTable.Rows.Cast<DataRow>());
            Assert.Equal(3, Module.Calls.Count);
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
        }
    }
    /// <summary>
    /// Editing loads an item, protects changes and resets state on missing items or load failures.
    /// </summary>
    [Fact]
    public void EditLoadsItemAndReportsFailures()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-edit-{Guid.NewGuid():N}.db");
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" });
            InsertModule Module = new();
            Module.Initialize(CreateDefinition("CustomerModule", ConnectionName));
            Module.Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT)");
            Module.Store.ExecSql("CREATE TABLE Address (Id TEXT PRIMARY KEY, CustomerId TEXT, City TEXT)");
            Module.Store.ExecSql("INSERT INTO Customer VALUES ('C1', 'Theo')");
            Assert.True(Module.Edit("C1"));
            Assert.Equal(DataMode.Edit, Module.State);
            Assert.Equal("C1", Module.LastEditedId);
            Assert.Equal(new[] { "Editing:None", "Edited:Edit" }, Module.Calls);
            Module.ItemTable.Rows[0]["Name"] = "Changed";
            Assert.Throws<InvalidOperationException>(() => Module.Edit("C1"));
            Assert.Equal("Changed", Module.ItemTable.Rows[0]["Name"]);
            Module.ItemTable.RejectChangesAll();
            Assert.False(Module.Edit("Missing"));
            Assert.Equal(DataMode.None, Module.State);
            Assert.Equal("C1", Module.LastEditedId);
            Assert.Empty(Module.ItemTable.Rows.Cast<DataRow>());
            Module.Store.ExecSql("DROP TABLE Address");
            Assert.Throws<SqliteException>(() => Module.Edit("C1"));
            Assert.Equal(DataMode.None, Module.State);
            Assert.Empty(Module.ItemTable.Rows.Cast<DataRow>());
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Delete protects changes and records deletions even if an after-commit event fails.
    /// </summary>
    [Fact]
    public void DeleteHandlesResultsAndHooks()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-delete-module-{Guid.NewGuid():N}.db");
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" });
            InsertModule Module = new();
            Module.Initialize(CreateDefinition("CustomerModule", ConnectionName));
            Module.Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT)");
            Module.Store.ExecSql("CREATE TABLE Address (Id TEXT PRIMARY KEY, CustomerId TEXT, City TEXT)");
            Module.Store.ExecSql("INSERT INTO Customer VALUES ('C1', 'One'), ('C2', 'Two')");
            Assert.True(Module.Edit("C1"));
            Module.ItemTable.Rows[0]["Name"] = "Changed";
            Assert.Throws<InvalidOperationException>(() => Module.Delete("C1"));
            Module.ItemTable.RejectChangesAll();
            Module.Delete("C1");
            Assert.Equal(DataMode.None, Module.State);
            Assert.Equal("C1", Module.LastDeletedId);
            Assert.Equal(new[] { "Editing:None", "Edited:Edit", "Deleting:Edit", "Deleted:None" }, Module.Calls);
            Module.Delete("Missing");
            Assert.Equal("C1", Module.LastDeletedId);
            Assert.Equal("Deleting:None", Module.Calls[^1]);
            Module.FailAfterDelete();
            Assert.Throws<InvalidOperationException>(() => Module.Delete("C2"));
            Assert.Equal(DataMode.None, Module.State);
            Assert.Equal("C2", Module.LastDeletedId);
            Assert.Equal(0L, Module.Store.Select("SELECT COUNT(*) FROM Customer").Rows[0][0]);
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Commit records successful saves and distinguishes failures before and after commit.
    /// </summary>
    [Fact]
    public void CommitHandlesResultsAndHooks()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-commit-module-{Guid.NewGuid():N}.db");
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" });
            InsertModule Module = new();
            Module.Initialize(CreateDefinition("CustomerModule", ConnectionName));
            Module.Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)");
            Module.Store.ExecSql("CREATE TABLE Address (Id TEXT PRIMARY KEY, CustomerId TEXT, City TEXT)");
            Module.Insert();
            object Id = Module.ItemTable.Rows[0]["Id"];
            Assert.Equal(Id, Module.Commit(false));
            Assert.Equal(DataMode.Edit, Module.State);
            Assert.Equal(Id, Module.LastCommittedId);
            Assert.Contains("Committing:Insert:False", Module.Calls);
            Assert.Contains("Committed:Edit:False", Module.Calls);
            Module.ItemTable.Rows[0]["Name"] = DBNull.Value;
            Assert.Throws<SqliteException>(() => Module.Commit(false));
            Assert.Equal(DataMode.Edit, Module.State);
            Assert.True(Module.HasChanges());
            Assert.Equal(Id, Module.LastCommittedId);
            Module.ItemTable.Rows[0]["Name"] = "Saved again";
            Module.FailAfterCommit();
            Assert.Throws<InvalidOperationException>(() => Module.Commit(false));
            Assert.Equal(DataMode.None, Module.State);
            Assert.Equal(Id, Module.LastCommittedId);
            Assert.Equal("Saved again", Module.Store.Select("SELECT Name FROM Customer").Rows[0][0]);
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Cancel restores changes without selecting a different existing detail row.
    /// </summary>
    [Fact]
    public void CancelPreservesCurrentItemAndDetailSelection()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-cancel-module-{Guid.NewGuid():N}.db");
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" });
            DataModule Module = new();
            Module.Initialize(CreateDefinition("CustomerModule", ConnectionName));
            Module.Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT)");
            Module.Store.ExecSql("CREATE TABLE Address (Id TEXT PRIMARY KEY, CustomerId TEXT, City TEXT)");
            Module.Store.ExecSql("INSERT INTO Customer VALUES ('C1', 'Theo')");
            Module.Store.ExecSql("INSERT INTO Address VALUES ('A1', 'C1', 'Athens'), ('A2', 'C1', 'Patras')");
            Assert.True(Module.Edit("C1"));
            MemTable Detail = Module.Tables.Get("Address");
            DataRow ItemRow = Module.ItemTable.CurrentRow;
            DataRow DetailRow = Detail.Rows.Cast<DataRow>().Single(Row => (string)Row["Id"] == "A2");
            Detail.CurrentRow = DetailRow;
            ItemRow["Name"] = "Changed";
            DetailRow["City"] = "Changed";
            Module.Cancel();
            Assert.Equal(DataMode.Edit, Module.State);
            Assert.Same(ItemRow, Module.ItemTable.CurrentRow);
            Assert.Same(DetailRow, Detail.CurrentRow);
            Assert.Equal("Theo", ItemRow["Name"]);
            Assert.Equal("Patras", DetailRow["City"]);
            Assert.False(Module.HasChanges());
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Cancel removes a new item and returns to None.
    /// </summary>
    [Fact]
    public void CancelInsertRemovesNewItem()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = "Data Source=:memory:" });
            DataModule Module = new();
            Module.Initialize(CreateDefinition("CustomerModule", ConnectionName));
            Module.Insert();
            Module.Cancel();
            Assert.Equal(DataMode.None, Module.State);
            Assert.Empty(Module.ItemTable.Rows.Cast<DataRow>());
            Assert.False(Module.HasChanges());
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
        }
    }
    /// <summary>
    /// Detail defaults apply when new rows are created, but not during loading or disabled events.
    /// </summary>
    [Fact]
    public void DetailDefaultsApplyOnlyToNewRowsWithEventsEnabled()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-defaults-{Guid.NewGuid():N}.db");
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" });
            InsertModule Module = new();
            Module.Initialize(CreateDefinition("CustomerModule", ConnectionName));
            Module.Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT)");
            Module.Store.ExecSql("CREATE TABLE Address (Id TEXT PRIMARY KEY, CustomerId TEXT, City TEXT)");
            Module.Store.ExecSql("INSERT INTO Customer VALUES ('C1', 'Theo')");
            Module.Store.ExecSql("INSERT INTO Address VALUES ('A1', 'C1', 'Athens')");
            Assert.True(Module.Edit("C1"));
            MemTable Detail = Module.Tables.Get("Address");
            Assert.Equal("Athens", Detail.Rows[0]["City"]);
            Assert.DoesNotContain("DetailDefault:Address", Module.Calls);
            DataRow NewRow = Detail.AddNewRow();
            Assert.Equal("Default city", NewRow["City"]);
            Assert.Equal("C1", NewRow["CustomerId"]);
            Assert.Single(Module.Calls, Call => Call == "DetailDefault:Address");
            Detail.EventsDisabled = true;
            try
            {
                DataRow DisabledRow = Detail.AddNewRow();
                Assert.True(DisabledRow.IsNull("City"));
            }
            finally
            {
                Detail.EventsDisabled = false;
            }
            Assert.Single(Module.Calls, Call => Call == "DetailDefault:Address");
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Derived modules receive commit and delete stages and can use the active transaction.
    /// </summary>
    [Fact]
    public void TransactionHandlersReceiveStagesAndUseTransaction()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-module-events-{Guid.NewGuid():N}.db");
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" });
            TransactionModule Module = new();
            Module.Initialize(CreateDefinition("CustomerModule", ConnectionName));
            Module.Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT)");
            Module.Store.ExecSql("CREATE TABLE Address (Id TEXT PRIMARY KEY, CustomerId TEXT, City TEXT)");
            Module.Store.ExecSql("CREATE TABLE Audit (Message TEXT)");
            Module.Insert();
            Module.ItemTable.Rows[0]["Name"] = "Theo";
            object Id = Module.Commit(false);
            Module.Delete(Id);
            Assert.Contains("Commit:Post:After", Module.Stages);
            Assert.Contains("Delete:Post:After", Module.Stages);
            Assert.Equal(2L, Module.Store.Select("SELECT COUNT(*) FROM Audit").Rows[0][0]);
            Assert.Equal(0L, Module.Store.Select("SELECT COUNT(*) FROM Customer").Rows[0][0]);
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Table events call virtual hooks, with defaults applied before NewRowAdding.
    /// </summary>
    [Fact]
    public void ItemTableEventsInvokeVirtualHooks()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = "Data Source=:memory:" });
            TableEventsModule Module = new();
            Module.Initialize(CreateDefinition("CustomerModule", ConnectionName));
            Module.Insert();
            MemTable Detail = Module.Tables.Get("Address");
            DataRow Row = Detail.AddNewRow();
            Assert.True(Module.Calls.IndexOf("Default") < Module.Calls.IndexOf("Adding:Athens:Detached"));
            Assert.True(Module.Calls.IndexOf("Adding:Athens:Detached") < Module.Calls.IndexOf("Added:Added"));
            Module.Calls.Clear();
            Row["City"] = "Patras";
            Assert.Equal(new[] { "Changing", "Changed" }, Module.Calls);
            Module.Calls.Clear();
            Detail.EventsDisabled = true;
            try { Row["City"] = "Heraklion"; }
            finally { Detail.EventsDisabled = false; }
            Assert.Empty(Module.Calls);
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
        }
    }
    /// <summary>
    /// Commit finishes item and detail edits before validation and saves their final values.
    /// </summary>
    [Fact]
    public void CommitEndsPendingEditsBeforeValidation()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-end-edit-{Guid.NewGuid():N}.db");
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" });
            CommitValidationModule Module = new();
            Module.Initialize(CreateDefinition("CustomerModule", ConnectionName));
            Module.Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT)");
            Module.Store.ExecSql("CREATE TABLE Address (Id TEXT PRIMARY KEY, CustomerId TEXT, City TEXT)");
            Module.Store.ExecSql("INSERT INTO Customer VALUES ('C1', 'Old name')");
            Module.Store.ExecSql("INSERT INTO Address VALUES ('A1', 'C1', 'Old city')");
            Assert.True(Module.Edit("C1"));
            DataRow Item = Module.ItemTable.Rows[0];
            DataRow Detail = Module.Tables.Get("Address").Rows[0];
            Item.BeginEdit();
            Item["Name"] = "Final name";
            Detail.BeginEdit();
            Detail["City"] = "Final city";
            Module.Commit(false);
            Assert.True(Module.SawFinishedEdits);
            Assert.Equal("Final name", Module.Store.Select("SELECT Name FROM Customer").Rows[0][0]);
            Assert.Equal("Final city", Module.Store.Select("SELECT City FROM Address").Rows[0][0]);
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Stocks load on initialization and remain outside the item tree.
    /// </summary>
    [Fact]
    public void StocksLoadIndependently()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-stocks-{Guid.NewGuid():N}.db");
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" });
            ModuleDef Definition = CreateDefinition("CustomerModule", ConnectionName);
            SelectDef Country = Definition.Stocks.Add("Country", null);
            Definition.Stocks.Add("ActiveCurrencies", "SELECT Id, Name FROM Currency WHERE Active = 1");
            SqlStore Store = SqlStores.CreateSqlStore(ConnectionName);
            Store.ExecSql("CREATE TABLE Country (Id TEXT, Name TEXT)");
            Store.ExecSql("CREATE TABLE Currency (Id TEXT, Name TEXT, Active INTEGER)");
            Store.ExecSql("INSERT INTO Country VALUES ('GR', 'Greece')");
            Store.ExecSql("INSERT INTO Currency VALUES ('EUR', 'Euro', 1), ('OLD', 'Old', 0)");
            DataModule Module = Definition.Create();
            Assert.Same(Definition, Country.Owner);
            Assert.Equal(2, Module.Stocks.Count);
            Assert.Null(Module.Tables.Find("Country"));
            Assert.Null(Module.Stocks.Get("Country").Master);
            Assert.Equal("Greece", Assert.Single(Module.Stocks.Get("Country").Rows.Cast<DataRow>())["Name"]);
            Assert.Equal("Euro", Assert.Single(Module.Stocks.Get("ActiveCurrencies").Rows.Cast<DataRow>())["Name"]);
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A failed stock query leaves the module available for initialization retry.
    /// </summary>
    [Fact]
    public void StockLoadFailureAllowsInitializationRetry()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-stocks-{Guid.NewGuid():N}.db");
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" });
            ModuleDef Definition = CreateDefinition("CustomerModule", ConnectionName);
            Definition.Stocks.Add("Country", null);
            DataModule Module = new();
            Assert.Throws<SqliteException>(() => Module.Initialize(Definition));
            Assert.False(Module.IsInitialized);
            SqlStores.CreateSqlStore(ConnectionName).ExecSql("CREATE TABLE Country (Id TEXT)");
            Module.Initialize(Definition);
            Assert.True(Module.IsInitialized);
            Assert.Empty(Module.Stocks.Get("Country").Rows.Cast<DataRow>());
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A module with cascade deletes disabled lets foreign keys reject top-down deletion.
    /// </summary>
    [Fact]
    public void CascadeDeletesSettingControlsItemDeletion()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-module-cascade-{Guid.NewGuid():N}.db");
        ModuleDef Definition = CreateDefinition("CustomerModule", ConnectionName);
        Assert.True(Definition.CascadeDeletes);
        Definition.CascadeDeletes = false;
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False;Foreign Keys=True" });
            DataModule Module = Definition.Create();
            Module.Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT)");
            Module.Store.ExecSql("CREATE TABLE Address (Id TEXT PRIMARY KEY, CustomerId TEXT REFERENCES Customer(Id), City TEXT)");
            Module.Store.ExecSql("INSERT INTO Customer VALUES ('C1', 'Theo')");
            Module.Store.ExecSql("INSERT INTO Address VALUES ('A1', 'C1', 'Athens')");
            Assert.False(new JsonDataModule(Module).CascadeDeletes);
            Assert.Throws<SqliteException>(() => Module.Delete("C1"));
            Assert.Equal(1L, Module.Store.Select("SELECT COUNT(*) FROM Customer").Rows[0][0]);
            Assert.Equal(1L, Module.Store.Select("SELECT COUNT(*) FROM Address").Rows[0][0]);
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// List cancellation rejects edits without changing the item table.
    /// </summary>
    [Fact]
    public void ListCancelRejectsListChanges()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        ModuleDef Definition = CreateDefinition("CustomerModule", ConnectionName);
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = "Data Source=:memory:" });
            DataModule Module = Definition.Create();
            Module.ListTable.Columns.Add("Id", typeof(string));
            Module.ListTable.Columns.Add("Name", typeof(string));
            DataRow Row = Module.ListTable.Rows.Add("1", "Original");
            Module.ListTable.AcceptChanges();
            Row["Name"] = "Changed";
            DataRow Item = Module.ItemTable.AddNewRow();
            Item["Name"] = "Unsaved item";

            Module.ListCancel();

            Assert.Equal("Original", Row["Name"]);
            Assert.Equal(DataRowState.Unchanged, Row.RowState);
            Assert.Equal(DataRowState.Added, Item.RowState);
            Assert.Equal("Unsaved item", Item["Name"]);
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
        }
    }
    /// <summary>
    /// Both list SELECT overloads apply the configured default row limit.
    /// </summary>
    [Fact]
    public void ListSelectAppliesRowLimit()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-list-limit-{Guid.NewGuid():N}.db");
        ModuleDef Definition = CreateDefinition("CustomerModule", ConnectionName);
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" });
            DataModule Module = Definition.Create();
            Module.Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)");
            int Limit = DbConfig.DefaultRowLimit;
            Module.Store.ExecSql($"WITH RECURSIVE N(Value) AS (VALUES (1) UNION ALL SELECT Value + 1 FROM N WHERE Value < {Limit + 5}) INSERT INTO Customer SELECT CAST(Value AS TEXT), 'Name' FROM N");
            Assert.Equal(Limit, Module.ListSelect("select Id, Name from Customer order by CAST(Id AS INTEGER); -- list"));
            Assert.Equal(Limit, Module.ListTable.Rows.Count);
            Assert.Equal("1", Module.ListTable.Rows[0]["Id"]);
            Assert.Equal(Limit, Module.ListSelect(Definition.SelectList.Get("DEFAULT")));
            Assert.Equal(1, Module.ListSelect($"select Id, Name from Customer where Id = {SqlProvider.GlobalPrefix}Id", new SqlParams().Add("Id", "2")));
            Assert.Equal("2", Module.ListTable.Rows[0]["Id"]);
            Assert.Throws<InvalidOperationException>(() => Module.ListSelect("select Id, Name from Customer limit 1"));
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A registered name creates a fresh initialized custom module and table tree.
    /// </summary>
    [Fact]
    public void CreateModuleByNameInitializesCustomModule()
    {
        string Name = $"Module_{Guid.NewGuid():N}";
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-module-{Guid.NewGuid():N}.db");
        ModuleDef Definition = CreateDefinition(Name, ConnectionName);
        Definition.ClassName = typeof(CustomModule).FullName;
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" });
            TypeStore.Register(typeof(CustomModule));
            DataRegistry.Modules.Add(Definition);
            DataModule Module = DataRegistry.CreateModule(Name.ToUpperInvariant());
            DataModule Other = DataRegistry.CreateModule(Name);

            Assert.IsType<CustomModule>(Module);
            Assert.True(Module.IsInitialized);
            Assert.Equal(Name, Module.Name);
            Assert.Null(Module.CurrentRow);
            Assert.Equal(DBNull.Value, Module.Id);
            Assert.Same(Module.ItemTable, Module.FindTable("CUSTOMER"));
            Assert.Same(Module.Tables.Get("Address"), Module.GetTable("address"));
            Assert.Null(Module.FindTable("missing"));
            Assert.Throws<KeyNotFoundException>(() => Module.GetTable("missing"));
            Assert.NotSame(Module, Other);
            Assert.Same(Definition, Module.ModuleDef);
            Assert.NotSame(Module.ItemTable, Other.ItemTable);
            Assert.Same(Module.ItemTable, Module.Tables.Get("customer"));
            Assert.Same(Module.ItemTable, Module.Tables.Get("Address").Master);
            Assert.True(Module.ItemTable.DetailsActive);
            Assert.NotNull(Module.ListTable);
            SelectDef DefaultSelect = Definition.SelectList.Get("DEFAULT");
            Assert.Equal("select * from Customer", DefaultSelect.SqlText);
            Assert.Same(Definition, DefaultSelect.Owner);
            Assert.Single(Definition.SelectList);
            Assert.Throws<InvalidOperationException>(() => Module.Initialize(Definition));

            Module.Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)");
            Module.Store.ExecSql("CREATE TABLE Address (Id TEXT PRIMARY KEY, CustomerId TEXT NOT NULL, City TEXT NOT NULL)");
            DataRow Customer = Module.ItemTable.AddNewRow();
            Customer["Name"] = "Theo";
            DataRow Address = Module.Tables.Get("Address").AddNewRow();
            Address["City"] = "Athens";
            string Id = (string)Customer["Id"];
            TableSet Tables = new(Module.Store, Module.ListTable, Module.ItemTable);
            Assert.Equal(Id, Tables.Commit(false).Id);
            Tables.ProcessEmpty();
            Assert.Equal(TableSetResultStatus.Success, Tables.Load(Id).Status);
            Assert.Equal("Athens", Assert.Single(Module.Tables.Get("Address").Rows.Cast<DataRow>())["City"]);
            MemTable OriginalListTable = Module.ListTable;
            Assert.Equal(1, Module.ListSelect(DefaultSelect));
            Assert.Same(OriginalListTable, Module.ListTable);
            Assert.Equal(Id, Assert.Single(Module.ListTable.Rows.Cast<DataRow>())["Id"]);
            Assert.Equal(1, Module.ListSelect("SELECT Id, Name FROM Customer WHERE Name = 'Theo'"));
            Assert.Same(OriginalListTable, Module.ListTable);
            Assert.Equal("Theo", Assert.Single(Module.ListTable.Rows.Cast<DataRow>())["Name"]);
        }
        finally
        {
            DataRegistry.Modules.Remove(Definition);
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// An explicitly supplied list SELECT is retained during initialization.
    /// </summary>
    [Fact]
    public void InitializationPreservesExplicitListSelect()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        ModuleDef Definition = CreateDefinition("CustomerModule", ConnectionName);
        SelectDef Select = Definition.SelectList.Add("Active", "select Id, Name from Customer");
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = "Data Source=:memory:" });
            DataModule Module = Definition.Create();
            Assert.Single(Definition.SelectList);
            Assert.Same(Select, Definition.SelectList[0]);
            Assert.Same(Definition, Select.Owner);
            Assert.Throws<ArgumentNullException>(() => Module.ListSelect((SelectDef)null));
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
        }
    }
    /// <summary>
    /// Failed initialization leaves the instance available for a later retry.
    /// </summary>
    [Fact]
    public void FailedInitializationCanBeRetried()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        ModuleDef Definition = CreateDefinition("CustomerModule", ConnectionName);
        DataModule Module = new();
        try
        {
            Assert.Throws<KeyNotFoundException>(() => Module.Initialize(Definition));
            Assert.Null(Module.ModuleDef);
            Assert.Null(Module.Tables);
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = "Data Source=:memory:" });
            Module.Initialize(Definition);
            Assert.Same(Definition, Module.ModuleDef);
            Assert.IsType<DataModule>(Definition.Create());
            Assert.Throws<InvalidOperationException>(() => Module.Initialize(Definition));
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
        }
    }
}
