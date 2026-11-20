/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests ownership and transaction completion in a SQL transaction context.
/// </summary>
public class SqlTransactionContextTests
{
    // ● public
    /// <summary>
    /// Commit completes the transaction and prevents a second completion.
    /// </summary>
    [Fact]
    public void CommitCompletesTransaction()
    {
        using (var Context = new SqlTransactionContext(new SqliteConnection("Data Source=:memory:")))
        {
            Context.BeginTransaction();
            Assert.True(Context.IsActive);
            Context.Commit();
            Assert.True(Context.IsCompleted);
            Assert.False(Context.IsActive);
            Assert.Throws<InvalidOperationException>(() => Context.Rollback());
            Assert.Throws<InvalidOperationException>(() => Context.BeginTransaction());
        }
    }
    /// <summary>
    /// Disposing an active context rolls back its changes and closes the connection.
    /// </summary>
    [Fact]
    public void DisposeRollsBackAndClosesConnection()
    {
        string PathName = Path.Combine(Path.GetTempPath(), $"tripous-{Guid.NewGuid():N}.db");
        string ConnectionString = $"Data Source={PathName};Pooling=False";
        try
        {
            using (var Setup = new SqliteConnection(ConnectionString))
            {
                Setup.Open();
                using (var Command = Setup.CreateCommand())
                {
                    Command.CommandText = "CREATE TABLE Items (Id INTEGER)";
                    Command.ExecuteNonQuery();
                }
            }
            var Connection = new SqliteConnection(ConnectionString);
            using (var Context = new SqlTransactionContext(Connection))
            {
                Context.BeginTransaction();
                using (DbCommand Command = Context.Connection.CreateCommand())
                {
                    Command.Transaction = Context.Transaction;
                    Command.CommandText = "INSERT INTO Items VALUES (1)";
                    Command.ExecuteNonQuery();
                }
            }
            Assert.Equal(ConnectionState.Closed, Connection.State);
            using (var Verify = new SqliteConnection(ConnectionString))
            {
                Verify.Open();
                using (var Command = Verify.CreateCommand())
                {
                    Command.CommandText = "SELECT COUNT(*) FROM Items";
                    Assert.Equal  (0L, Command.ExecuteScalar());
                }
            }
        }
        finally
        {
            if (File.Exists(PathName))
                File.Delete(PathName);
        }
    }
}
