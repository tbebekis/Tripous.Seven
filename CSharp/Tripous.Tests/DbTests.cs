/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests central access to database connections.
/// </summary>
[Collection("Database registry")]
public class DbTests
{
    // ● public
    /// <summary>
    /// Finds the configured default connection without case sensitivity.
    /// </summary>
    [Fact]
    public void GetsConfiguredDefaultConnection()
    {
        string OriginalName = DbConfig.DefaultConnectionName;
        string Name = $"test-{Guid.NewGuid():N}";
        DbConnectionInfo Info = new() { Name = Name };
        try
        {
            Db.Connections.Add(Info);
            DbConfig.DefaultConnectionName = Name.ToUpperInvariant();
            Assert.Same(Info, Db.GetDefaultConnectionInfo());
            Assert.Same(Info, Db.GetConnectionInfo(Name));
            DbConfig.DefaultConnectionName = Name + "-missing";
            Assert.Throws<KeyNotFoundException>(() => Db.GetDefaultConnectionInfo());
        }
        finally
        {
            DbConfig.DefaultConnectionName = OriginalName;
            Db.Connections.Remove(Name);
        }
    }
    /// <summary>
    /// Default and named store requests create fresh instances from current settings.
    /// </summary>
    [Fact]
    public void CreatesFreshStoresForConfiguredConnections()
    {
        string OriginalName = DbConfig.DefaultConnectionName;
        string Name = $"test-{Guid.NewGuid():N}";
        DbConnectionInfo Info = new() { Name = Name, ConnectionString = "Data Source=:memory:" };
        try
        {
            Db.Connections.Add(Info);
            DbConfig.DefaultConnectionName = Name;
            SqlStore First = SqlStores.CreateDefaultSqlStore();
            SqlStore Second = SqlStores.CreateDefaultSqlStore();
            Assert.NotSame(First, Second);
            Assert.NotSame(First, SqlStores.CreateSqlStore(Name));
            Info.DbServerType = DbServerType.Oracle;
            Assert.NotSame(First, SqlStores.CreateDefaultSqlStore());
            Assert.Equal(1L, First.Select("SELECT 1 AS Result").Rows[0]["Result"]);
        }
        finally
        {
            DbConfig.DefaultConnectionName = OriginalName;
            Db.Connections.Remove(Name);
        }
    }
}
