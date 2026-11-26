/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests parsing and editing connection strings without opening a database connection.
/// </summary>
public class DbConAdapterTests
{
    /// <summary>
    /// Keeps quoted file paths and extra options when updating a SQLite connection.
    /// </summary>
    [Fact]
    public void SqlitePreservesQuotedPathsAndExtraOptions()
    {
        DbConnectionInfo Info = new() { ConnectionString = "Data Source=\"old;test.db\";Pooling=False;Mode=ReadOnly" };
        DbConAdapter Adapter = Info.GetConnectionStringAdapter();
        List<DbConProp> Props = Adapter.Parse(Info.ConnectionString);
        Assert.Equal("old;test.db", Props.GetValue(DbConPropType.Database));
        Props.SetValue(DbConPropType.Database, "new;test.db");
        var Result = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(Adapter.Construct(Props));
        Assert.Equal("new;test.db", Result.DataSource);
        Assert.False(Result.Pooling);
        Assert.Equal(Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly, Result.Mode);
    }

    /// <summary>
    /// Round-trips the modeled properties of the server adapters, including quoted values.
    /// </summary>
    [Theory]
    [InlineData(DbServerType.MsSql, "Server=db.test,1434;Initial Catalog=Catalog")]
    [InlineData(DbServerType.MySql, "Server=db.test;Port=3307;Database=Catalog")]
    [InlineData(DbServerType.PostgreSql, "Host=db.test;Port=5433;Database=Catalog")]
    [InlineData(DbServerType.Firebird, "DataSource=db.test;Port=3051;Database=Catalog")]
    [InlineData(DbServerType.Oracle, "Data Source=//db.test:1522/Catalog")]
    public void ServerAdaptersRoundTripProperties(DbServerType ServerType, string ConnectionString)
    {
        DbConAdapter Adapter = DbConAdapters.Get(ServerType);
        List<DbConProp> First = Adapter.Parse(ConnectionString + ";Password=\"x;y=z\"");
        Assert.Equal("db.test", First.GetValue(DbConPropType.Server));
        Assert.Equal("Catalog", First.GetValue(DbConPropType.Database));
        Assert.Equal("x;y=z", First.GetValue(DbConPropType.Password));
        List<DbConProp> Second = Adapter.Parse(Adapter.Construct(First));
        foreach (DbConProp Item in First)
            Assert.Equal(Item.Value, Second.GetValue(Item.PropType));
    }
}
