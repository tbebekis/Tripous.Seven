/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Serializes tests that temporarily configure the global logging callbacks.
/// </summary>
[CollectionDefinition("Sys logging", DisableParallelization = true)]
public class SysLoggingCollection
{
}

/// <summary>
/// Tests logging of failed SQL operations.
/// </summary>
[Collection("Sys logging")]
public class SqlStoreLoggingTests
{
    // ● private types
    sealed class FailingComparison
    {
        public override bool Equals(object Value) => throw new InvalidOperationException("restore comparison failed");
        public override int GetHashCode() => 0;
    }
    sealed class RollbackFailureConnection : DbConnection
    {
        ConnectionState fState = ConnectionState.Closed;
        public bool IsDisposed { get; private set; }
        public Exception DisposeError { get; set; }
        public RollbackFailureTransaction CreatedTransaction { get; private set; }
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "test";
        public override string DataSource => "test";
        public override string ServerVersion => "1";
        public override ConnectionState State => fState;
        public override void Open() => fState = ConnectionState.Open;
        public override void Close() => fState = ConnectionState.Closed;
        public override void ChangeDatabase(string DatabaseName) => throw new NotSupportedException();
        protected override DbTransaction BeginDbTransaction(IsolationLevel IsolationLevel)
            => CreatedTransaction = new RollbackFailureTransaction(this);
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
        protected override void Dispose(bool Disposing)
        {
            if (Disposing)
            {
                IsDisposed = true;
                if (DisposeError != null)
                    throw DisposeError;
            }
            base.Dispose(Disposing);
        }
    }
    sealed class RollbackFailureTransaction : DbTransaction
    {
        readonly DbConnection fConnection;
        public RollbackFailureTransaction(DbConnection Connection) => fConnection = Connection;
        public Exception Error { get; } = new InvalidOperationException("rollback failed");
        public bool IsDisposed { get; private set; }
        public Exception DisposeError { get; set; }
        public override IsolationLevel IsolationLevel => IsolationLevel.Unspecified;
        protected override DbConnection DbConnection => fConnection;
        public override void Commit() { }
        public override void Rollback() => throw Error;
        protected override void Dispose(bool Disposing)
        {
            if (Disposing)
            {
                IsDisposed = true;
                if (DisposeError != null)
                    throw DisposeError;
            }
            base.Dispose(Disposing);
        }
    }

    // ● public
    /// <summary>
    /// A rollback failure during context disposal is logged and both resources are disposed.
    /// </summary>
    [Fact]
    public void ContextDisposalLogsRollbackFailureAndDisposesResources()
    {
        Action<Exception> PreviousCallback = Sys.LogExceptionProc;
        List<Exception> Logged = new();
        RollbackFailureConnection Connection = new();
        try
        {
            Sys.LogExceptionProc = Logged.Add;
            SqlTransactionContext Context = new(Connection);
            Context.BeginTransaction();
            Context.Dispose();
            Assert.Same(Connection.CreatedTransaction.Error, Assert.Single(Logged));
            Assert.True(Connection.CreatedTransaction.IsDisposed);
            Assert.True(Connection.IsDisposed);
        }
        finally
        {
            Sys.LogExceptionProc = PreviousCallback;
        }
    }
    /// <summary>
    /// Both disposal failures are logged without escaping or preventing the other cleanup.
    /// </summary>
    [Fact]
    public void ContextDisposalLogsBothDisposeFailures()
    {
        Action<Exception> PreviousCallback = Sys.LogExceptionProc;
        List<Exception> Logged = new();
        Exception ConnectionError = new InvalidOperationException("connection dispose failed");
        Exception TransactionError = new InvalidOperationException("transaction dispose failed");
        RollbackFailureConnection Connection = new() { DisposeError = ConnectionError };
        try
        {
            Sys.LogExceptionProc = Logged.Add;
            SqlTransactionContext Context = new(Connection);
            Context.BeginTransaction();
            Connection.CreatedTransaction.DisposeError = TransactionError;
            Context.Dispose();
            Assert.Equal(3, Logged.Count);
            Assert.Same(Connection.CreatedTransaction.Error, Logged[0]);
            Assert.Same(TransactionError, Logged[1]);
            Assert.Same(ConnectionError, Logged[2]);
            Assert.True(Connection.CreatedTransaction.IsDisposed);
            Assert.True(Connection.IsDisposed);
        }
        finally
        {
            Sys.LogExceptionProc = PreviousCallback;
        }
    }
    /// <summary>
    /// Disposal failures after a confirmed commit are logged without changing its outcome.
    /// </summary>
    [Fact]
    public void CommittedContextLogsDisposeFailures()
    {
        Action<Exception> PreviousCallback = Sys.LogExceptionProc;
        List<Exception> Logged = new();
        Exception ConnectionError = new InvalidOperationException("connection dispose failed");
        Exception TransactionError = new InvalidOperationException("transaction dispose failed");
        RollbackFailureConnection Connection = new() { DisposeError = ConnectionError };
        try
        {
            Sys.LogExceptionProc = Logged.Add;
            SqlTransactionContext Context = new(Connection);
            Context.BeginTransaction();
            Connection.CreatedTransaction.DisposeError = TransactionError;
            Context.Commit();
            Assert.True(Context.IsCompleted);
            Context.Dispose();
            Assert.Equal(2, Logged.Count);
            Assert.Same(TransactionError, Logged[0]);
            Assert.Same(ConnectionError, Logged[1]);
            Assert.True(Connection.IsDisposed);
        }
        finally
        {
            Sys.LogExceptionProc = PreviousCallback;
        }
    }
    /// <summary>
    /// A failed unchanged-row restoration is logged without replacing the original commit error.
    /// </summary>
    [Fact]
    public void RestoreUnchangedFailurePreservesOriginalError()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-restore-{Guid.NewGuid():N}.db");
        Action<Exception> PreviousCallback = Sys.LogExceptionProc;
        List<Exception> Logged = new();
        Exception Original = new InvalidOperationException("post failed");
        bool RollbackAfter = false;
        string Version = Guid.NewGuid().ToString();
        try
        {
            SqlStore Store = new(new DbConnectionInfo { ConnectionString = $"Data Source={FilePath};Pooling=False" });
            Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Version TEXT NOT NULL)");
            Store.ExecSql("INSERT INTO Customer (Id, Version) VALUES (:Id, :Version)", new SqlParams().Add("Id", "C1").Add("Version", Version));
            TableDef Definition = new() { Name = "Customer" };
            Definition.Fields.Add(new FieldDef { Name = "Id" });
            Definition.Fields.Add(new FieldDef { Name = "Version", MaxLength = 36 });
            MemTable Item = Definition.CreateDescriptorTable();
            Item.Sqls.AssignFrom(Definition.BuildSql(SqlProviders.Sqlite.OidMode));
            Item.Columns.Add("Probe", typeof(object));
            DataRow Row = Item.AddNewRow();
            Row["Id"] = "C1";
            Row["Version"] = Version;
            Row["Probe"] = new FailingComparison();
            Row.AcceptChanges();
            TableSet Set = new(Store, new MemTable("List"), Item, TableSetFlags.None, "Version");
            Set.TransactionStageCommit += (Sender, Args) =>
            {
                if (Args.Stage == TransactionStage.Post && Args.ExecTime == ExecTime.After)
                {
                    Row["Probe"] = new FailingComparison();
                    throw Original;
                }
                if (Args.Stage == TransactionStage.Rollback && Args.ExecTime == ExecTime.After)
                    RollbackAfter = true;
            };
            Sys.LogExceptionProc = Logged.Add;
            TableSetResult Result = Set.Commit(false);
            Assert.Equal(TableSetResultStatus.CommitFailed, Result.Status);
            Assert.Same(Original, Result.Exception);
            Assert.Contains(Logged, Error => Error.Message == "restore comparison failed");
            Assert.True(RollbackAfter);
            Assert.Equal(Version, Store.Select("SELECT Version FROM Customer WHERE Id = 'C1'").Rows[0][0]);
        }
        finally
        {
            Sys.LogExceptionProc = PreviousCallback;
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A transaction disposed by a hook causes rollback to fail without replacing the original error.
    /// </summary>
    [Fact]
    public void CommitRollbackFailurePreservesOriginalError()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-failed-rollback-{Guid.NewGuid():N}.db");
        Action<Exception> PreviousCallback = Sys.LogExceptionProc;
        List<Exception> Logged = new();
        Exception Original = new InvalidOperationException("post failed");
        List<string> Stages = new();
        try
        {
            SqlStore Store = new(new DbConnectionInfo { ConnectionString = $"Data Source={FilePath};Pooling=False" });
            TableDef Definition = new() { Name = "Customer" };
            Definition.Fields.Add(new FieldDef { Name = "Id" });
            MemTable Item = Definition.CreateDescriptorTable();
            Item.AddNewRow();
            TableSet Set = new(Store, new MemTable("List"), Item);
            Set.TransactionStageCommit += (Sender, Args) =>
            {
                Stages.Add($"{Args.Stage}/{Args.ExecTime}");
                if (Args.Stage == TransactionStage.Post && Args.ExecTime == ExecTime.Before)
                {
                    Args.Transaction.Dispose();
                    throw Original;
                }
            };
            Sys.LogExceptionProc = Logged.Add;
            TableSetResult Result = Set.Commit(false);
            Assert.Equal(TableSetResultStatus.CommitFailed, Result.Status);
            Assert.Same(Original, Result.Exception);
            Assert.NotEmpty(Logged);
            Assert.Contains("Rollback/Before", Stages);
            Assert.DoesNotContain("Rollback/After", Stages);
            Assert.False(Item.EventsDisabled);
        }
        finally
        {
            Sys.LogExceptionProc = PreviousCallback;
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A configured number series table is used by both system schema and code reservation.
    /// </summary>
    [Fact]
    public void NumberSeriesUsesConfiguredTableName()
    {
        string ConnectionName = $"series-{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-series-{Guid.NewGuid():N}.db");
        string PreviousName = DbConfig.SysNumberSeriesTableName;
        DbConnectionInfo Info = new() { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" };
        try
        {
            DbConfig.SysNumberSeriesTableName = "CUSTOM_SERIES";
            Db.Connections.Add(Info);
            SystemSchema.Register(ConnectionName).Execute();
            SqlStore Store = new(Info);
            var Module = new CodeProviderCommitTests.NumberSeriesModule(ConnectionName);
            Assert.True(Store.TableExists("CUSTOM_SERIES"));
            Store.ExecSql("INSERT INTO CUSTOM_SERIES (Id, Code, Name, Pattern) VALUES ('1', 'ORDER', 'Orders', '{XXXX}')");
            using (SqlTransactionContext Context = Store.BeginTransactionContext())
            {
                Assert.Equal("0001", Module.Reserve(Context.Transaction, "ORDER"));
                Context.Commit();
            }
            DbConfig.SysNumberSeriesTableName = "INVALID;DROP";
            using (SqlTransactionContext Context = Store.BeginTransactionContext())
            {
                Assert.Throws<ArgumentException>(() => Module.Reserve(Context.Transaction, "ORDER"));
            }
        }
        finally
        {
            DbConfig.SysNumberSeriesTableName = PreviousName;
            Schema Schema = Schemas.Find("System", ConnectionName);
            if (Schema != null)
                Schemas.List.Remove(Schema);
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// SQL logging is optional, formats parameters and excludes the configured settings table.
    /// </summary>
    [Fact]
    public void SqlLoggingUsesConfigurationAndFormatsParameters()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-sql-log-{Guid.NewGuid():N}.db");
        bool PreviousEnabled = Db.Settings.LogSqlStatements;
        bool PreviousDebug = Sys.DebugMode;
        string PreviousIni = DbConfig.SysDbIniTableName;
        List<string> Messages = new();
        try
        {
            SqlStore Store = new(new DbConnectionInfo { ConnectionString = $"Data Source={FilePath};Pooling=False" });
            Sys.LogInfoProc = Messages.Add;
            Db.Settings.LogSqlStatements = false;
            Store.Select("SELECT 1");
            Assert.Empty(Messages);
            Db.Settings.LogSqlStatements = true;
            Sys.DebugMode = false;
            Store.Select("SELECT :Value", new SqlParams().Add("Value", "sensitive-value"));
            Assert.Equal("SELECT :Value", Assert.Single(Messages).TrimEnd());
            Messages.Clear();
            Sys.DebugMode = true;
            Store.Select("SELECT :Value, :Bytes, :Empty", new SqlParams().Add("Value", 42).Add("Bytes", new byte[] { 1, 2 }).Add("Empty", DBNull.Value));
            string Message = Assert.Single(Messages);
            Assert.Contains("SELECT", Message);
            Assert.Contains("Value = 42", Message);
            Assert.Contains("Bytes = byte[2]", Message);
            Assert.Contains("Empty = NULL", Message);
            DbConfig.SysDbIniTableName = "TEST_SETTINGS";
            Store.Select("SELECT 'TEST_SETTINGS'");
            Assert.Single(Messages);
        }
        finally
        {
            Db.Settings.LogSqlStatements = PreviousEnabled;
            Sys.DebugMode = PreviousDebug;
            DbConfig.SysDbIniTableName = PreviousIni;
            Sys.LogInfoProc = null;
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A failed statement logs its original exception and SQL details, even when a callback fails.
    /// </summary>
    [Fact]
    public void FailedStatementLogsOriginalException()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-log-{Guid.NewGuid():N}.db");
        Exception Logged = null;
        try
        {
            SqlStore Store = new(new DbConnectionInfo { ConnectionString = $"Data Source={FilePath};Pooling=False" });
            Sys.UiLogProc = _ => throw new InvalidOperationException("UI failed");
            Sys.LogExceptionProc = Error => Logged = Error;
            string Sql = "INSERT INTO MissingTable (Name) VALUES (@Name)";
            Exception Error = Assert.Throws<SqliteException>(() => Store.ExecSql(Sql, new SqlParams().Add("Name", "example")));
            Assert.Same(Error, Logged);
            Assert.Equal(Sql, Logged.Data["SQL Statement"]);
            Assert.Equal("example", Logged.Data["Parameter: Name"]);
            Sys.LogExceptionProc = _ => throw new InvalidOperationException("logger failed");
            Assert.Throws<SqliteException>(() => Store.ExecSql(Sql, new SqlParams().Add("Name", "example")));
        }
        finally
        {
            Sys.UiLogProc = null;
            Sys.LogExceptionProc = null;
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A database error reaches a file listener without exposing parameter values outside debug mode.
    /// </summary>
    [Fact]
    public void DatabaseErrorIsWrittenToFile()
    {
        string Folder = Path.Combine(Path.GetTempPath(), $"tripous-db-file-log-{Guid.NewGuid():N}");
        string FilePath = Path.Combine(Folder, "data.db");
        Action<Exception> PreviousCallback = Sys.LogExceptionProc;
        bool PreviousDebug = Sys.DebugMode;
        Tripous.Logging.LogLevel PreviousLevel = Tripous.Logging.Logger.MinLevel;
        Tripous.Logging.FileLogListener Listener = null;
        try
        {
            Directory.CreateDirectory(Folder);
            Listener = new Tripous.Logging.FileLogListener(Path.Combine(Folder, "logs"), "db.log", "header\n");
            Sys.LogExceptionProc = Tripous.Logging.Logger.Error;
            Sys.DebugMode = false;
            Tripous.Logging.Logger.MinLevel = Tripous.Logging.LogLevel.Info;
            SqlStore Store = new(new DbConnectionInfo { ConnectionString = $"Data Source={FilePath};Pooling=False" });
            Assert.Throws<SqliteException>(() => Store.ExecSql("INSERT INTO MissingTable (Name) VALUES (:Name)", new SqlParams().Add("Name", "sensitive-value")));
            Tripous.Logging.Logger.Flush();
            string LogFile = Assert.Single(Directory.GetFiles(Listener.Folder));
            string Text = File.ReadAllText(LogFile);
            Assert.Contains("MissingTable", Text);
            Assert.DoesNotContain("sensitive-value", Text);
        }
        finally
        {
            Tripous.Logging.Logger.Flush();
            Listener?.Unregister();
            Sys.LogExceptionProc = PreviousCallback;
            Sys.DebugMode = PreviousDebug;
            Tripous.Logging.Logger.MinLevel = PreviousLevel;
            if (Directory.Exists(Folder))
                Directory.Delete(Folder, true);
        }
    }
    /// <summary>
    /// A rollback notification failure is logged without replacing the original commit error.
    /// </summary>
    [Fact]
    public void RollbackNotificationFailurePreservesOriginalError()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-rollback-log-{Guid.NewGuid():N}.db");
        List<Exception> Logged = new();
        Exception Original = new InvalidOperationException("post failed");
        Exception Cleanup = new InvalidOperationException("rollback notification failed");
        try
        {
            SqlStore Store = new(new DbConnectionInfo { ConnectionString = $"Data Source={FilePath};Pooling=False" });
            TableDef Definition = new() { Name = "Customer" };
            Definition.Fields.Add(new FieldDef { Name = "Id" });
            MemTable Item = Definition.CreateDescriptorTable();
            Item.AddNewRow();
            TableSet Set = new(Store, new MemTable("List"), Item);
            Set.TransactionStageCommit += (Sender, Args) =>
            {
                if (Args.Stage == TransactionStage.Post && Args.ExecTime == ExecTime.Before)
                    throw Original;
                if (Args.Stage == TransactionStage.Rollback && Args.ExecTime == ExecTime.Before)
                    throw Cleanup;
            };
            Sys.LogExceptionProc = Logged.Add;
            TableSetResult Result = Set.Commit(false);
            Assert.Equal(TableSetResultStatus.CommitFailed, Result.Status);
            Assert.Same(Original, Result.Exception);
            Assert.Contains(Cleanup, Logged);
        }
        finally
        {
            Sys.LogExceptionProc = null;
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
}
