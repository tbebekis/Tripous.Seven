/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests SQLite SELECT execution through a SQL store.
/// </summary>
public class SqlStoreTests
{
    // ● public
    /// <summary>
    /// The store uses copied settings and supports named parameter inputs.
    /// </summary>
    [Fact]
    public void SelectUsesCopiedSettingsAndParameters()
    {
        string PathName = Path.Combine(Path.GetTempPath(), $"tripous-{Guid.NewGuid():N}.db");
        try
        {
            var Info = new DbConnectionInfo { ConnectionString = $"Data Source={PathName};Pooling=False" };
            var Store = new SqlStore(Info);
            Info.DbServerType = DbServerType.Oracle;
            Info.ConnectionString = "invalid";
            DataTable Result = Store.Select("SELECT :Value AS Result", new Dictionary<string, object> { ["Value"] = 42 });
            Assert.Equal(42L, Result.Rows[0]["Result"]);
            Assert.Equal(DataRowState.Unchanged, Result.Rows[0].RowState);

            var Source = new DataTable();
            Source.Columns.Add("Value", typeof(long));
            Source.Rows.Add  (99L);
            Result = Store.Select("SELECT :Value AS Result", Source.Rows[0]);
            Assert.Equal(99L, Result.Rows[0]["Result"]);
            Assert.Equal(1L, Store.Select("SELECT 1 AS Result").Rows[0]["Result"]);
        }
        finally
        {
            if (File.Exists(PathName))
                File.Delete(PathName);
        }
    }
    /// <summary>
    /// SelectTo replaces rows and observes changes in the caller's transaction.
    /// </summary>
    [Fact]
    public void SelectToUsesCallerTransaction()
    {
        string PathName = Path.Combine(Path.GetTempPath(), $"tripous-{Guid.NewGuid():N}.db");
        try
        {
            var Store = new SqlStore(new DbConnectionInfo { ConnectionString = $"Data Source={PathName};Pooling=False" });
            using (var Context = Store.BeginTransactionContext())
            {
                using (DbCommand Command = Context.Connection.CreateCommand())
                {
                    Command.Transaction = Context.Transaction;
                    Command.CommandText = "CREATE TABLE Items (Id INTEGER); INSERT INTO Items VALUES (3)";
                    Command.ExecuteNonQuery();
                }
                DataTable Table = new();
                Table.Columns.Add("Id", typeof(long));
                Table.Rows.Add(99L);
                int Count = Store.SelectTo(Context.Transaction, Table, "SELECT Id FROM Items WHERE Id = :Id", 30, new SqlParams().Add("Id", 3));
                Assert.Equal(1, Count);
                Assert.Equal(3L, Table.Rows[0]["Id"]);
                Assert.Equal(DataRowState.Unchanged, Table.Rows[0].RowState);
                Assert.True(Context.IsActive);
                Context.Rollback();
            }
            Assert.Equal(0L, Store.Select("SELECT COUNT(*) AS Total FROM sqlite_master WHERE name = 'Items'").Rows[0]["Total"]);
        }
        finally
        {
            if (File.Exists(PathName))
                File.Delete(PathName);
        }
    }
    /// <summary>
    /// Store-owned statements commit and caller-owned transactions can roll back.
    /// </summary>
    [Fact]
    public void ExecSqlCommitsAndSupportsRollback()
    {
        string PathName = Path.Combine(Path.GetTempPath(), $"tripous-{Guid.NewGuid():N}.db");
        try
        {
            var Store = new SqlStore(new DbConnectionInfo { ConnectionString = $"Data Source={PathName};Pooling=False" });
            Store.ExecSql("CREATE TABLE Items (Id INTEGER PRIMARY KEY)");
            Assert.Equal(1, Store.ExecSql("INSERT INTO Items VALUES (:Id)", new SqlParams().Add("Id", 1)));
            using (var Context = Store.BeginTransactionContext())
            {
                Assert.Equal(1, Store.ExecSql(Context.Transaction, "INSERT INTO Items VALUES (:Id)", 30, new Dictionary<string, object> { ["Id"] = 2 }));
                Context.Rollback();
            }
            Assert.Equal(1L, Store.Select("SELECT COUNT(*) AS Total FROM Items").Rows[0]["Total"]);
        }
        finally
        {
            if (File.Exists(PathName))
                File.Delete(PathName);
        }
    }
    /// <summary>
    /// Executes a portable CREATE TABLE through the SQLite provider.
    /// </summary>
    [Fact]
    public void CreatesTableWithTypeTokens()
    {
        string PathName = Path.Combine(Path.GetTempPath(), $"tripous-types-{Guid.NewGuid():N}.db");
        try
        {
            var Store = new SqlStore(new DbConnectionInfo { ConnectionString = $"Data Source={PathName};Pooling=False" });
            Store.ExecSql("CREATE TABLE Items (Id @NVARCHAR(40) @NOT_NULL PRIMARY KEY, Active @BOOL @NOT_NULL, Notes @NBLOB_TEXT @NULL)");
            Store.ExecSql("INSERT INTO Items (Id, Active, Notes) VALUES (:Id, :Active, :Notes)", new SqlParams().Add("Id", "A").Add("Active", true).Add("Notes", null));
            Assert.Equal("A", Store.Select("SELECT Id FROM Items").Rows[0]["Id"]);
        }
        finally
        {
            if (File.Exists(PathName))
                File.Delete(PathName);
        }
    }
    /// <summary>
    /// LastId reads the generated SQLite row ID on the same transaction connection.
    /// </summary>
    [Fact]
    public void LastIdUsesTransactionConnection()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-id-{Guid.NewGuid():N}.db");
        try
        {
            SqlStore Store = new(new DbConnectionInfo { ConnectionString = $"Data Source={FilePath};Pooling=False" });
            Store.ExecSql("CREATE TABLE Items (Id INTEGER PRIMARY KEY, Name TEXT)");
            using (SqlTransactionContext Context = Store.BeginTransactionContext())
            {
                Store.ExecSql(Context.Transaction, "INSERT INTO Items (Name) VALUES (:Name)", new SqlParams().Add("Name", "first"));
                Assert.Equal(1, Store.LastId(Context.Transaction, "Items"));
                Store.ExecSql(Context.Transaction, "INSERT INTO Items (Name) VALUES (:Name)", new SqlParams().Add("Name", "second"));
                Assert.Equal(2, Store.LastId(Context.Transaction, "Items"));
                Assert.Throws<NotSupportedException>(() => Store.NextId(Context.Transaction, "Items"));
                Context.Rollback();
            }
            Assert.Equal(0L, Store.Select("SELECT COUNT(*) AS Total FROM Items").Rows[0]["Total"]);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
}
