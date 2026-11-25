/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests batch transactions and confirmed progress.
/// </summary>
[Collection("Database registry")]
public class BatchCommitTests
{
    // ● public types
    /// <summary>
    /// Fails immediately after a batch transaction commits.
    /// </summary>
    public class AfterCommitFailureModule : DataModule
    {
        /// <summary>
        /// Throws after a database commit.
        /// </summary>
        protected override void TableSet_TransactionStageCommit(object Sender, TransactionEventArgs Args)
        {
            if (Args.Stage == TransactionStage.Commit && Args.ExecTime == ExecTime.After)
                throw new InvalidOperationException("after commit");
        }
    }
    /// <summary>
    /// Records the per-item checks performed by a batch commit.
    /// </summary>
    public class CheckedBatchModule : DataModule
    {
        /// <summary>
        /// Gets the number of checks.
        /// </summary>
        public int CheckCount { get; private set; }
        /// <summary>
        /// Checks that the row edit was completed before validation.
        /// </summary>
        public override void CheckCanCommit(bool Reselect)
        {
            Assert.True(IsCommitting);
            Assert.False(CurrentRow.HasVersion(DataRowVersion.Proposed));
            CheckCount++;
        }
    }

    // ● private types
    sealed class Fixture : IDisposable
    {
        public readonly string Name = $"batch-{Guid.NewGuid():N}";
        public readonly string PathName = Path.Combine(Path.GetTempPath(), $"tripous-batch-{Guid.NewGuid():N}.db");
        public readonly ModuleDef Definition;
        public readonly SqlStore Store;
        public Fixture()
        {
            DbConnectionInfo Info = new() { Name = Name, ConnectionString = $"Data Source={PathName};Pooling=False;Foreign Keys=True" };
            Db.Connections.Add(Info);
            Store = new SqlStore(Info);
            Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)");
            Definition = new ModuleDef { Name = "Customer", ConnectionName = Name };
            Definition.Table.Name = "Customer";
            Definition.Table.Fields.Add(new FieldDef { Name = "Id" });
            Definition.Table.Fields.Add(new FieldDef { Name = "Name" });
        }
        public void Dispose()
        {
            Db.Connections.Remove(Name);
            if (File.Exists(PathName))
                File.Delete(PathName);
        }
    }

    // ● public
    /// <summary>
    /// Skipped iterations do not count as posts; confirmed progress tracks the last posted iteration.
    /// </summary>
    [Fact]
    public void CommitsGroupsAndSkipsIterations()
    {
        using Fixture F = new();
        DataModule Module = F.Definition.Create();
        BatchCommitArgs Args = null;
        List<object> Ids = new();
        Args = new BatchCommitArgs(() =>
        {
            if (Args.Counter == 1)
                return false;
            Module.Insert();
            Module.CurrentRow["Name"] = $"Name{Args.Counter}";
            return true;
        }, Id =>
        {
            Ids.Add(Id);
            return Args.Counter < 3;
        }, 2);
        BatchCommitResult Result = Module.CommitBatch(Args);
        Assert.Equal(BatchCommitStatus.Success, Result.Status);
        Assert.Null(Result.Exception);
        Assert.Equal(3, Result.CommittedCount);
        Assert.Equal(3, Result.LastCommittedIteration);
        Assert.Equal(3, Args.PostCounter);
        Assert.Equal(4, Ids.Count);
        Assert.Equal(Ids[0], Ids[1]);
        Assert.Equal(3L, Convert.ToInt64(F.Store.Select("SELECT COUNT(*) FROM Customer").Rows[0][0]));
    }
    /// <summary>
    /// Each posted item may contain detail rows linked to its top row.
    /// </summary>
    [Fact]
    public void CommitsItemTreesWithDetails()
    {
        using Fixture F = new();
        F.Store.ExecSql("CREATE TABLE Address (Id TEXT PRIMARY KEY, CustomerId TEXT NOT NULL REFERENCES Customer(Id), City TEXT NOT NULL)");
        TableDef Detail = new() { Name = "Address", MasterField = "Id", DetailField = "CustomerId" };
        Detail.Fields.Add(new FieldDef { Name = "Id" });
        Detail.Fields.Add(new FieldDef { Name = "CustomerId" });
        Detail.Fields.Add(new FieldDef { Name = "City" });
        F.Definition.Table.Details.Add(Detail);
        DataModule Module = F.Definition.Create();
        BatchCommitArgs Args = null;
        Args = new BatchCommitArgs(() =>
        {
            Module.Insert();
            Module.CurrentRow["Name"] = $"Name{Args.Counter}";
            Module.Tables.Get("Address").AddNewRow()["City"] = $"City{Args.Counter}";
            return true;
        }, _ => Args.Counter < 1, 2);
        BatchCommitResult Result = Module.CommitBatch(Args);
        Assert.Equal(BatchCommitStatus.Success, Result.Status);
        Assert.Equal(2, Result.CommittedCount);
        DataTable Rows = F.Store.Select("SELECT Customer.Name, Address.City FROM Customer JOIN Address ON Customer.Id = Address.CustomerId ORDER BY Customer.Name");
        Assert.Equal(2, Rows.Rows.Count);
        Assert.Equal("City0", Rows.Rows[0]["City"]);
        Assert.Equal("City1", Rows.Rows[1]["City"]);
    }
    /// <summary>
    /// A later failed group rolls back without losing the confirmed progress of earlier groups.
    /// </summary>
    [Fact]
    public void FailedGroupReportsPreviouslyCommittedPosts()
    {
        using Fixture F = new();
        DataModule Module = F.Definition.Create();
        BatchCommitArgs Args = null;
        Args = new BatchCommitArgs(() =>
        {
            Module.Insert();
            if (Args.Counter != 3)
                Module.CurrentRow["Name"] = $"Name{Args.Counter}";
            return true;
        }, _ => Args.Counter < 4, 2);
        BatchCommitResult Result = Module.CommitBatch(Args);
        Assert.Equal(BatchCommitStatus.Failed, Result.Status);
        Assert.IsType<SqliteException>(Result.Exception);
        Assert.Equal(2, Result.CommittedCount);
        Assert.Equal(1, Result.LastCommittedIteration);
        Assert.Equal(3, Args.PostCounter);
        Assert.Equal(2L, Convert.ToInt64(F.Store.Select("SELECT COUNT(*) FROM Customer").Rows[0][0]));
        Assert.Equal(DataRowState.Added, Module.CurrentRow.RowState);
    }
    /// <summary>
    /// A failing post-commit hook reports the confirmed group rather than a rollback.
    /// </summary>
    [Fact]
    public void PostCommitFailurePreservesConfirmedProgress()
    {
        using Fixture F = new();
        AfterCommitFailureModule Module = new();
        Module.Initialize(F.Definition);
        BatchCommitArgs Args = null;
        Args = new BatchCommitArgs(() =>
        {
            Module.Insert();
            Module.CurrentRow["Name"] = $"Name{Args.Counter}";
            return true;
        }, _ => true, 2);
        BatchCommitResult Result = Module.CommitBatch(Args);
        Assert.Equal(BatchCommitStatus.AfterCommitFailed, Result.Status);
        Assert.Equal(2, Result.CommittedCount);
        Assert.Equal(1, Result.LastCommittedIteration);
        Assert.Equal(2L, Convert.ToInt64(F.Store.Select("SELECT COUNT(*) FROM Customer").Rows[0][0]));
    }
    /// <summary>
    /// An AfterFunc failure immediately after a group commit keeps the confirmed progress.
    /// </summary>
    [Fact]
    public void AfterFuncFailureImmediatelyAfterCommitReportsCommittedGroup()
    {
        using Fixture F = new();
        DataModule Module = F.Definition.Create();
        BatchCommitArgs Args = new(() =>
        {
            Module.Insert();
            Module.CurrentRow["Name"] = "Alice";
            return true;
        }, _ => throw new InvalidOperationException("after callback"), 1);
        BatchCommitResult Result = Module.CommitBatch(Args);
        Assert.Equal(BatchCommitStatus.AfterCommitFailed, Result.Status);
        Assert.Equal(1, Result.CommittedCount);
        Assert.Equal(0, Result.LastCommittedIteration);
        Assert.Equal(1L, Convert.ToInt64(F.Store.Select("SELECT COUNT(*) FROM Customer").Rows[0][0]));
    }
    /// <summary>
    /// A batch that skips its only iteration succeeds without opening a transaction.
    /// </summary>
    [Fact]
    public void BatchCanCompleteWithoutPosts()
    {
        using Fixture F = new();
        DataModule Module = F.Definition.Create();
        BatchCommitArgs Args = new(() => false, Id =>
        {
            Assert.Null(Id);
            return false;
        });
        BatchCommitResult Result = Module.CommitBatch(Args);
        Assert.Equal(BatchCommitStatus.Success, Result.Status);
        Assert.Equal(0, Result.CommittedCount);
        Assert.Equal(-1, Result.LastCommittedIteration);
        Assert.Equal(0, Args.PostCounter);
    }
    /// <summary>
    /// A database-side rollback leaves no confirmed posts.
    /// </summary>
    [Fact]
    public void DatabaseRollbackLeavesNoConfirmedPosts()
    {
        using Fixture F = new();
        F.Store.ExecSql("CREATE TRIGGER FailCustomer BEFORE INSERT ON Customer BEGIN SELECT RAISE(ROLLBACK, 'forced rollback'); END");
        DataModule Module = F.Definition.Create();
        BatchCommitArgs Args = new(() =>
        {
            Module.Insert();
            Module.CurrentRow["Name"] = "Alice";
            return true;
        }, _ => false);
        BatchCommitResult Result = Module.CommitBatch(Args);
        Assert.Equal(BatchCommitStatus.Failed, Result.Status);
        Assert.Equal(0, Result.CommittedCount);
        Assert.Equal(-1, Result.LastCommittedIteration);
        Assert.Equal(0L, Convert.ToInt64(F.Store.Select("SELECT COUNT(*) FROM Customer").Rows[0][0]));
    }
    /// <summary>
    /// A deferred foreign key causes a real transaction commit failure.
    /// </summary>
    [Fact]
    public void CommitFailureReportsUnknownTransactionOutcome()
    {
        using Fixture F = new();
        F.Store.ExecSql("CREATE TABLE PendingReference (CustomerId TEXT, FOREIGN KEY (CustomerId) REFERENCES Customer(Id) DEFERRABLE INITIALLY DEFERRED)");
        F.Store.ExecSql("CREATE TRIGGER AddInvalidReference AFTER INSERT ON Customer BEGIN INSERT INTO PendingReference (CustomerId) VALUES ('missing'); END");
        DataModule Module = F.Definition.Create();
        BatchCommitArgs Args = new(() =>
        {
            Module.Insert();
            Module.CurrentRow["Name"] = "Alice";
            return true;
        }, _ => false, 1);
        BatchCommitResult Result = Module.CommitBatch(Args);
        Assert.Equal(BatchCommitStatus.TransactionOutcomeUnknown, Result.Status);
        Assert.IsType<SqliteException>(Result.Exception);
        Assert.Equal(0, Result.CommittedCount);
        Assert.Equal(-1, Result.LastCommittedIteration);
        Assert.Equal(0L, Convert.ToInt64(F.Store.Select("SELECT COUNT(*) FROM Customer").Rows[0][0]));
        Assert.Equal(DataRowState.Added, Module.CurrentRow.RowState);
    }
    /// <summary>
    /// A failed commit of a later group preserves earlier confirmed progress.
    /// </summary>
    [Fact]
    public void LaterCommitFailureReportsEarlierConfirmedGroup()
    {
        using Fixture F = new();
        F.Store.ExecSql("CREATE TABLE PendingReference (CustomerId TEXT, FOREIGN KEY (CustomerId) REFERENCES Customer(Id) DEFERRABLE INITIALLY DEFERRED)");
        F.Store.ExecSql("CREATE TRIGGER AddInvalidReference AFTER INSERT ON Customer WHEN NEW.Name = 'Fail' BEGIN INSERT INTO PendingReference (CustomerId) VALUES ('missing'); END");
        DataModule Module = F.Definition.Create();
        BatchCommitArgs Args = null;
        Args = new BatchCommitArgs(() =>
        {
            Module.Insert();
            Module.CurrentRow["Name"] = Args.Counter == 2 ? "Fail" : $"Name{Args.Counter}";
            return true;
        }, _ => Args.Counter < 2, 2);
        BatchCommitResult Result = Module.CommitBatch(Args);
        Assert.Equal(BatchCommitStatus.TransactionOutcomeUnknown, Result.Status);
        Assert.IsType<SqliteException>(Result.Exception);
        Assert.Equal(2, Result.CommittedCount);
        Assert.Equal(1, Result.LastCommittedIteration);
        Assert.Equal(3, Args.PostCounter);
        Assert.Equal(2L, Convert.ToInt64(F.Store.Select("SELECT COUNT(*) FROM Customer").Rows[0][0]));
        Assert.Equal(DataRowState.Added, Module.CurrentRow.RowState);
    }
    /// <summary>
    /// DataModule ends edits and invokes its commit check for every posted item.
    /// </summary>
    [Fact]
    public void ModuleChecksEachItemAndEndsPendingEdits()
    {
        using Fixture F = new();
        CheckedBatchModule Module = new();
        Module.Initialize(F.Definition);
        BatchCommitArgs Args = new(() =>
        {
            Module.Insert();
            Module.CurrentRow.BeginEdit();
            Module.CurrentRow["Name"] = "Alice";
            return true;
        }, _ => false);
        BatchCommitResult Result = Module.CommitBatch(Args);
        Assert.Equal(BatchCommitStatus.Success, Result.Status);
        Assert.Equal(1, Module.CheckCount);
        Assert.Equal(DataMode.Edit, Module.State);
        Assert.False(Module.IsCommitting);
        Assert.Equal("Alice", F.Store.Select("SELECT Name FROM Customer").Rows[0][0]);
    }
    /// <summary>
    /// Batch rollback restores the current generated code and version, and rolls back the series.
    /// </summary>
    [Fact]
    public void FailedGroupRollsBackCodeProviderAndRowVersion()
    {
        using Fixture F = new();
        string ProviderName = $"provider-{Guid.NewGuid():N}";
        CodeProviderDef Provider = new() { Name = ProviderName };
        try
        {
            DataRegistry.CodeProviders.Add(Provider);
            SystemSchema.Register(F.Name).Execute();
            F.Store.ExecSql("ALTER TABLE Customer ADD COLUMN Code TEXT");
            F.Store.ExecSql("ALTER TABLE Customer ADD COLUMN Version TEXT");
            F.Store.ExecSql("INSERT INTO SYS_NUMBER_SERIES (Id, Code, Name, Pattern) VALUES (:Id, :Code, :Name, :Pattern)", new SqlParams().Add("Id", Guid.NewGuid().ToString()).Add("Code", ProviderName).Add("Name", ProviderName).Add("Pattern", "C-{XXXX}"));
            F.Definition.CodeProvider = ProviderName;
            F.Definition.RowVersionFieldName = "Version";
            F.Definition.Table.Fields.Add(new FieldDef { Name = "Code" });
            F.Definition.Table.Fields.Add(new FieldDef { Name = "Version", MaxLength = 36 });
            DataModule Module = F.Definition.Create();
            BatchCommitArgs Args = null;
            Args = new BatchCommitArgs(() =>
            {
                Module.Insert();
                if (Args.Counter != 3)
                    Module.CurrentRow["Name"] = $"Name{Args.Counter}";
                return true;
            }, _ => Args.Counter < 4, 2);
            BatchCommitResult Result = Module.CommitBatch(Args);
            Assert.Equal(BatchCommitStatus.Failed, Result.Status);
            Assert.Equal(2, Result.CommittedCount);
            Assert.True(Module.CurrentRow.IsNull("Code"));
            Assert.True(Module.CurrentRow.IsNull("Version"));
            Assert.Equal(3L, Convert.ToInt64(F.Store.Select("SELECT NextNumber FROM SYS_NUMBER_SERIES").Rows[0][0]));
            DataTable Saved = F.Store.Select("SELECT Code, Version FROM Customer ORDER BY Code");
            Assert.Equal(2, Saved.Rows.Count);
            Assert.Equal("C-0001", Saved.Rows[0]["Code"]);
            Assert.Equal("C-0002", Saved.Rows[1]["Code"]);
            Assert.All(Saved.Rows.Cast<DataRow>(), Row => Assert.True(Guid.TryParse((string)Row["Version"], out _)));
        }
        finally
        {
            DataRegistry.CodeProviders.Remove(Provider);
            Schema Schema = Schemas.Find("System", F.Name);
            if (Schema != null)
                Schemas.List.Remove(Schema);
        }
    }
    /// <summary>
    /// BatchInsert copies matching fields and leaves the source table untouched.
    /// </summary>
    [Fact]
    public void BatchInsertCopiesSourceRows()
    {
        using Fixture F = new();
        DataModule Module = F.Definition.Create();
        DataTable Source = new();
        Source.Columns.Add("Id", typeof(string));
        Source.Columns.Add("Name", typeof(string));
        Source.Columns.Add("Ignored", typeof(string));
        Source.Rows.Add("C1", "Alice", "one");
        Source.Rows.Add("C2", "Bob", "two");
        BatchCommitResult Result = Module.BatchInsert(Source);
        Assert.Equal(BatchCommitStatus.Success, Result.Status);
        Assert.Equal(2, Result.CommittedCount);
        Assert.Equal(1, Result.LastCommittedIteration);
        Assert.Equal(2, Source.Rows.Count);
        Assert.All(Source.Rows.Cast<DataRow>(), Row => Assert.Equal(DataRowState.Added, Row.RowState));
        DataTable Saved = F.Store.Select("SELECT Id, Name FROM Customer ORDER BY Id");
        Assert.Equal(2, Saved.Rows.Count);
        Assert.Equal("Alice", Saved.Rows[0]["Name"]);
        Assert.Equal("Bob", Saved.Rows[1]["Name"]);
    }
    /// <summary>
    /// Empty sources do nothing and mismatched column types fail before any insert.
    /// </summary>
    [Fact]
    public void BatchInsertHandlesEmptyAndInvalidSource()
    {
        using Fixture F = new();
        DataModule Module = F.Definition.Create();
        DataTable Source = new();
        Source.Columns.Add("Name", typeof(int));
        BatchCommitResult Empty = Module.BatchInsert(Source);
        Assert.Equal(BatchCommitStatus.Success, Empty.Status);
        Assert.Equal(0, Empty.CommittedCount);
        Assert.Equal(-1, Empty.LastCommittedIteration);
        Source.Rows.Add(42);
        Assert.Throws<ArgumentException>(() => Module.BatchInsert(Source));
        Assert.Throws<ArgumentException>(() => Module.BatchInsert(Module.ItemTable));
        Assert.Equal(0L, Convert.ToInt64(F.Store.Select("SELECT COUNT(*) FROM Customer").Rows[0][0]));
    }
    /// <summary>
    /// BatchInsert does not discard an item that already has unsaved changes.
    /// </summary>
    [Fact]
    public void BatchInsertPreservesExistingUnsavedItem()
    {
        using Fixture F = new();
        DataModule Module = F.Definition.Create();
        Module.Insert();
        DataRow Existing = Module.CurrentRow;
        Existing["Name"] = "Unsaved";
        DataTable Source = new();
        Source.Columns.Add("Name", typeof(string));
        Source.Rows.Add("Batch item");
        BatchCommitResult Result = Module.BatchInsert(Source);
        Assert.Equal(BatchCommitStatus.Failed, Result.Status);
        Assert.IsType<InvalidOperationException>(Result.Exception);
        Assert.Same(Existing, Module.CurrentRow);
        Assert.Equal("Unsaved", Existing["Name"]);
        Assert.Equal(DataRowState.Added, Existing.RowState);
        Assert.Equal(0, Result.CommittedCount);
        Assert.Equal(0L, Convert.ToInt64(F.Store.Select("SELECT COUNT(*) FROM Customer").Rows[0][0]));
    }
    /// <summary>
    /// A later failed source row does not undo the first committed group.
    /// </summary>
    [Fact]
    public void BatchInsertReportsPreviouslyCommittedGroup()
    {
        using Fixture F = new();
        DataModule Module = F.Definition.Create();
        DataTable Source = new();
        Source.Columns.Add("Name", typeof(string));
        for (int Index = 0; Index < 300; Index++)
            Source.Rows.Add($"Name{Index}");
        Source.Rows.Add(DBNull.Value);
        BatchCommitResult Result = Module.BatchInsert(Source);
        Assert.Equal(BatchCommitStatus.Failed, Result.Status);
        Assert.Equal(300, Result.CommittedCount);
        Assert.Equal(299, Result.LastCommittedIteration);
        Assert.Equal(300L, Convert.ToInt64(F.Store.Select("SELECT COUNT(*) FROM Customer").Rows[0][0]));
        Assert.Equal(301, Source.Rows.Count);
    }
    /// <summary>
    /// Manual codes are retained, while empty codes and row versions are generated.
    /// </summary>
    [Fact]
    public void BatchInsertRetainsManualCodesAndGeneratesCodeAndRowVersion()
    {
        using Fixture F = new();
        string ProviderName = $"provider-{Guid.NewGuid():N}";
        CodeProviderDef Provider = new() { Name = ProviderName };
        try
        {
            DataRegistry.CodeProviders.Add(Provider);
            SystemSchema.Register(F.Name).Execute();
            F.Store.ExecSql("ALTER TABLE Customer ADD COLUMN Code TEXT");
            F.Store.ExecSql("ALTER TABLE Customer ADD COLUMN Version TEXT");
            F.Store.ExecSql("INSERT INTO SYS_NUMBER_SERIES (Id, Code, Name, Pattern) VALUES (:Id, :Code, :Name, :Pattern)", new SqlParams().Add("Id", Guid.NewGuid().ToString()).Add("Code", ProviderName).Add("Name", ProviderName).Add("Pattern", "C-{XXXX}"));
            F.Definition.CodeProvider = ProviderName;
            F.Definition.RowVersionFieldName = "Version";
            F.Definition.Table.Fields.Add(new FieldDef { Name = "Code" });
            F.Definition.Table.Fields.Add(new FieldDef { Name = "Version", MaxLength = 36 });
            DataModule Module = F.Definition.Create();
            DataTable Source = new();
            Source.Columns.Add("Name", typeof(string));
            Source.Columns.Add("Code", typeof(string));
            Source.Columns.Add("Version", typeof(string));
            Source.Rows.Add("Alice", "MANUAL", "not-a-version");
            Source.Rows.Add("Bob", DBNull.Value, "not-a-version");
            BatchCommitResult Result = Module.BatchInsert(Source);
            Assert.Equal(BatchCommitStatus.Success, Result.Status);
            DataTable Saved = F.Store.Select("SELECT Name, Code, Version FROM Customer ORDER BY Name");
            Assert.Equal(2, Saved.Rows.Count);
            Assert.Equal("MANUAL", Saved.Rows[0]["Code"]);
            Assert.Equal("C-0001", Saved.Rows[1]["Code"]);
            Assert.All(Saved.Rows.Cast<DataRow>(), Row => Assert.True(Guid.TryParse((string)Row["Version"], out _)));
            Assert.Equal(2L, Convert.ToInt64(F.Store.Select("SELECT NextNumber FROM SYS_NUMBER_SERIES").Rows[0][0]));
            Assert.Equal("MANUAL", Source.Rows[0]["Code"]);
            Assert.Equal("not-a-version", Source.Rows[0]["Version"]);
        }
        finally
        {
            DataRegistry.CodeProviders.Remove(Provider);
            Schema Schema = Schemas.Find("System", F.Name);
            if (Schema != null)
                Schemas.List.Remove(Schema);
        }
    }
}
