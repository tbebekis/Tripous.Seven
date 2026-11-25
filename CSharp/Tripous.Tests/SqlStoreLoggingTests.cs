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
    // ● public
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
}
