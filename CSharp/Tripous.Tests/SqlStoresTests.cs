/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests creation of SQL stores from connection settings.
/// </summary>
public class SqlStoresTests
{
    // ● public
    /// <summary>
    /// Each call creates a separate store and selects its provider at construction.
    /// </summary>
    [Fact]
    public void CreatesNewStoresWithCurrentSettings()
    {
        DbConnectionInfo Info = new() { ConnectionString = "Data Source=:memory:" };
        SqlStore First = SqlStores.CreateSqlStore(Info);
        Info.DbServerType = DbServerType.Oracle;
        Assert.Equal(1L, First.Select("SELECT 1 AS Result").Rows[0]["Result"]);
        Assert.NotSame(First, SqlStores.CreateSqlStore(Info));
        Info.DbServerType = DbServerType.Sqlite;
        SqlStore Second = SqlStores.CreateSqlStore(Info);
        Assert.NotSame(First, Second);
    }
}
