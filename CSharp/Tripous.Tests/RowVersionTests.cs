/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests row version configuration and item commits.
/// </summary>
[Collection("Database registry")]
public class RowVersionTests
{
    // ● public types
    /// <summary>
    /// Raises a conflict exception after the database transaction has committed.
    /// </summary>
    public class AfterCommitConflictModule : DataModule
    {
        /// <summary>Raises the test error after commit.</summary>
        protected override void TableSet_TransactionStageCommit(object Sender, TransactionEventArgs Args)
        {
            if (Args.Stage == TransactionStage.Commit && Args.ExecTime == ExecTime.After)
                throw new RowVersionConflictException("Customer");
        }
    }

    // ● private types
    sealed class Fixture : IDisposable
    {
        public readonly string ConnectionName = $"version-{Guid.NewGuid():N}";
        public readonly string ModuleName = $"module-{Guid.NewGuid():N}";
        public readonly string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-version-{Guid.NewGuid():N}.db");
        public readonly ModuleDef Definition;
        public readonly SqlStore Store;
        public readonly string InitialVersion = Guid.NewGuid().ToString();
        public Fixture()
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False;Foreign Keys=True" });
            Store = SqlStores.CreateSqlStore(ConnectionName);
            Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT NOT NULL, Version TEXT NOT NULL)");
            Store.ExecSql("CREATE TABLE Address (Id TEXT PRIMARY KEY, CustomerId TEXT NOT NULL REFERENCES Customer(Id), City TEXT NOT NULL)");
            Store.ExecSql("INSERT INTO Customer (Id, Name, Version) VALUES (:Id, :Name, :Version)", new SqlParams().Add("Id", "C1").Add("Name", "Original").Add("Version", InitialVersion));
            Definition = new ModuleDef { Name = ModuleName, ConnectionName = ConnectionName, RowVersionFieldName = "Version" };
            Definition.Table.Name = "Customer";
            Definition.Table.Fields.Add(new FieldDef { Name = "Id" });
            Definition.Table.Fields.Add(new FieldDef { Name = "Name" });
            Definition.Table.Fields.Add(new FieldDef { Name = "Version", MaxLength = 36 });
            TableDef Detail = new() { Name = "Address", MasterField = "Id", DetailField = "CustomerId" };
            Detail.Fields.Add(new FieldDef { Name = "Id" });
            Detail.Fields.Add(new FieldDef { Name = "CustomerId" });
            Detail.Fields.Add(new FieldDef { Name = "City" });
            Definition.Table.Details.Add(Detail);
            DataRegistry.Modules.Add(Definition);
        }
        public void Dispose()
        {
            DataRegistry.Modules.Remove(Definition);
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }

    // ● public
    /// <summary>
    /// Invalid fields are rejected and valid configuration can be checked independently.
    /// </summary>
    [Fact]
    public void ValidatesRowVersionField()
    {
        ModuleDef Definition = new() { Name = "Customer", RowVersionFieldName = "Version" };
        Definition.Table.Name = "Customer";
        Definition.Table.Fields.Add(new FieldDef { Name = "Id" });
        Assert.Throws<InvalidOperationException>(() => new DataModule().Initialize(Definition));
        FieldDef Field = new() { Name = "Version", DataType = DataFieldType.Integer };
        Definition.Table.Fields.Add(Field);
        Assert.Throws<InvalidOperationException>(() => new DataModule().Initialize(Definition));
        Field.DataType = DataFieldType.String;
        Field.MaxLength = 35;
        Assert.Throws<InvalidOperationException>(() => new DataModule().Initialize(Definition));
        Field.MaxLength = 36;
        Field.Flags = FieldFlags.Extra;
        Assert.Throws<InvalidOperationException>(() => new DataModule().Initialize(Definition));
        Field.Flags = FieldFlags.NoInsertUpdate;
        Assert.Throws<InvalidOperationException>(() => new DataModule().Initialize(Definition));
        Field.Flags = FieldFlags.None;
        Definition.RowVersionFieldName = "ID";
        Assert.Throws<InvalidOperationException>(() => new DataModule().Initialize(Definition));
        Definition.RowVersionFieldName = "Version";
        Definition.CodeProvider = "Provider";
        Definition.CodeFieldName = "version";
        Assert.Throws<InvalidOperationException>(() => new DataModule().Initialize(Definition));
        Definition.CodeProvider = null;
        Definition.CheckDescriptor();
    }

    /// <summary>
    /// Each successful commit changes the version; a stale user retains their edits.
    /// </summary>
    [Fact]
    public void ExistingItemDetectsConflictAndNoChangeCommitAdvancesVersion()
    {
        using Fixture F = new();
        DataModule First = F.Definition.Create();
        DataModule Second = F.Definition.Create();
        Assert.True(First.Edit("C1"));
        Assert.True(Second.Edit("C1"));
        First.CurrentRow["Name"] = "First";
        First.Commit(false);
        string Version = (string)First.CurrentRow["Version"];
        Assert.True(Guid.TryParse(Version, out _));
        Assert.NotEqual(F.InitialVersion, Version);
        Assert.Throws<RowVersionConflictException>(() => Second.Commit(false));
        Assert.Equal(TableSetResultStatus.RowVersionConflict, Second.LastTableSetResult.Status);
        Assert.Equal(DataRowState.Unchanged, Second.CurrentRow.RowState);
        Second.CurrentRow["Name"] = "Second";
        Assert.Throws<RowVersionConflictException>(() => Second.Commit(false));
        Assert.Equal("Second", Second.CurrentRow["Name"]);
        Assert.Equal(F.InitialVersion, Second.CurrentRow["Version"]);
        Assert.Equal(DataRowState.Modified, Second.CurrentRow.RowState);
        Assert.Equal("First", F.Store.Select("SELECT Name FROM Customer WHERE Id = 'C1'").Rows[0][0]);
        First.Commit(false);
        string Next = (string)First.CurrentRow["Version"];
        Assert.NotEqual(Version, Next);
        Assert.Equal(Next, F.Store.Select("SELECT Version FROM Customer WHERE Id = 'C1'").Rows[0][0]);
    }

    /// <summary>
    /// A stale detail-only change is rejected without removing the new detail row.
    /// </summary>
    [Fact]
    public void StaleDetailOnlyChangeIsRejected()
    {
        using Fixture F = new();
        DataModule First = F.Definition.Create();
        DataModule Second = F.Definition.Create();
        Assert.True(First.Edit("C1"));
        Assert.True(Second.Edit("C1"));
        First.Commit(false);
        DataRow Detail = Second.Tables.Get("Address").AddNewRow();
        Detail["City"] = "Athens";
        Assert.Throws<RowVersionConflictException>(() => Second.Commit(false));
        Assert.Equal(F.InitialVersion, Second.CurrentRow["Version"]);
        Assert.Equal(DataRowState.Unchanged, Second.CurrentRow.RowState);
        Assert.Equal(DataRowState.Added, Detail.RowState);
        Assert.Equal(0L, Convert.ToInt64(F.Store.Select("SELECT COUNT(*) FROM Address").Rows[0][0]));
    }

    /// <summary>
    /// A detail-only failure restores the item version and preserves its unchanged state.
    /// </summary>
    [Fact]
    public void DetailOnlyRollbackRestoresVersionAndRetrySucceeds()
    {
        using Fixture F = new();
        DataModule Module = F.Definition.Create();
        Assert.True(Module.Edit("C1"));
        DataRow Detail = Module.Tables.Get("Address").AddNewRow();
        Assert.Throws<SqliteException>(() => Module.Commit(false));
        Assert.Equal(F.InitialVersion, Module.CurrentRow["Version"]);
        Assert.Equal(DataRowState.Unchanged, Module.CurrentRow.RowState);
        Assert.Equal(DataRowState.Added, Detail.RowState);
        Assert.Equal(F.InitialVersion, F.Store.Select("SELECT Version FROM Customer WHERE Id = 'C1'").Rows[0][0]);
        Detail["City"] = "Athens";
        Module.Commit(false);
        string Version = (string)Module.CurrentRow["Version"];
        Assert.NotEqual(F.InitialVersion, Version);
        Assert.Equal(Version, F.Store.Select("SELECT Version FROM Customer WHERE Id = 'C1'").Rows[0][0]);
        Assert.Equal("Athens", F.Store.Select("SELECT City FROM Address").Rows[0][0]);
    }

    /// <summary>
    /// A failed top-row update retains user edits and restores the original version.
    /// </summary>
    [Fact]
    public void FailedTopUpdateRestoresVersionAndRetainsEdits()
    {
        using Fixture F = new();
        DataModule Module = F.Definition.Create();
        Assert.True(Module.Edit("C1"));
        Module.CurrentRow["Name"] = DBNull.Value;
        Assert.Throws<SqliteException>(() => Module.Commit(false));
        Assert.True(Module.CurrentRow.IsNull("Name"));
        Assert.Equal(F.InitialVersion, Module.CurrentRow["Version"]);
        Assert.Equal(DataRowState.Modified, Module.CurrentRow.RowState);
        Assert.Equal("Original", F.Store.Select("SELECT Name FROM Customer WHERE Id = 'C1'").Rows[0][0]);
        Assert.Equal(F.InitialVersion, F.Store.Select("SELECT Version FROM Customer WHERE Id = 'C1'").Rows[0][0]);
    }

    /// <summary>
    /// A deleted item is treated as a concurrency conflict.
    /// </summary>
    [Fact]
    public void DeletedItemIsAConflict()
    {
        using Fixture F = new();
        DataModule Module = F.Definition.Create();
        Assert.True(Module.Edit("C1"));
        F.Store.ExecSql("DELETE FROM Customer WHERE Id = 'C1'");
        Assert.Throws<RowVersionConflictException>(() => Module.Commit(false));
        Assert.Equal(F.InitialVersion, Module.CurrentRow["Version"]);
        Assert.Equal(DataRowState.Unchanged, Module.CurrentRow.RowState);
    }

    /// <summary>
    /// An exception after commit remains an after-commit failure, even if its type is a conflict.
    /// </summary>
    [Fact]
    public void ConflictExceptionAfterCommitDoesNotHideCommittedTransaction()
    {
        using Fixture F = new();
        AfterCommitConflictModule Module = new();
        Module.Initialize(F.Definition);
        Assert.True(Module.Edit("C1"));
        Assert.Throws<RowVersionConflictException>(() => Module.Commit(false));
        Assert.Equal(TableSetResultStatus.AfterCommitFailed, Module.LastTableSetResult.Status);
        Assert.NotEqual(F.InitialVersion, F.Store.Select("SELECT Version FROM Customer WHERE Id = 'C1'").Rows[0][0]);
    }

    /// <summary>
    /// A failed insert restores the previous empty version; a retry gets a GUID.
    /// </summary>
    [Fact]
    public void InsertRollbackRestoresVersion()
    {
        using Fixture F = new();
        DataModule Module = F.Definition.Create();
        Module.Insert();
        Assert.True(Module.CurrentRow.IsNull("Version"));
        Assert.Throws<SqliteException>(() => Module.Commit(false));
        Assert.True(Module.CurrentRow.IsNull("Version"));
        Module.CurrentRow["Name"] = "New";
        Module.Commit(false);
        Assert.True(Guid.TryParse((string)Module.CurrentRow["Version"], out _));
    }

    /// <summary>
    /// A remote request uses the version already carried by the client tree.
    /// </summary>
    [Fact]
    public void RemoteCommitRejectsStaleTree()
    {
        using Fixture F = new();
        RemOp.Initialize();
        DataModule First = F.Definition.Create();
        DataModule Second = F.Definition.Create();
        Assert.True(First.Edit("C1"));
        Assert.True(Second.Edit("C1"));
        First.CurrentRow["Name"] = "First";
        Second.CurrentRow["Name"] = "Second";
        RemoteRequest Request = new() { Id = Guid.NewGuid().ToString(), OperationName = "DataModule.Commit" };
        Request.Params["ModuleName"] = F.ModuleName;
        Request.Params["DataModule"] = new JsonDataModule(First);
        Request = Json.Deserialize<RemoteRequest>(Json.Serialize(Request));
        RemoteResponse Success = RemoteOperations.Execute(Request);
        Assert.True(Success.IsSuccess, Success.ErrorText);
        Request.Params["DataModule"] = new JsonDataModule(Second);
        Request = Json.Deserialize<RemoteRequest>(Json.Serialize(Request));
        RemoteResponse Conflict = RemoteOperations.Execute(Request);
        Assert.False(Conflict.IsSuccess);
        Assert.Equal("RowVersionConflict", Conflict["TableSetResultStatus"]);
        Assert.Equal("First", F.Store.Select("SELECT Name FROM Customer WHERE Id = 'C1'").Rows[0][0]);
    }
}
