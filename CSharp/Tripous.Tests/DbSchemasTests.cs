/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests the registry of shared connection metadata schemas.
/// </summary>
public class DbSchemasTests
{
    // ● public
    /// <summary>
    /// Connections with the same name share one schema regardless of case.
    /// The first connection instance remains associated with that schema.
    /// </summary>
    [Fact]
    public void ConnectionSchemasAreSharedByName()
    {
        string Name = $"schema-{Guid.NewGuid():N}";
        try
        {
            DbConnectionInfo First = new() { Name = Name };
            DbConnectionInfo Second = new() { Name = Name.ToUpperInvariant() };
            DbSchema Schema = First.Schema;
            Assert.Same(Schema, Second.Schema);
            Assert.Same(Schema, DbSchemas.Get(Name));
            Assert.Same(First, Schema.ConnectionInfo);
        }
        finally
        {
            DbSchemas.Remove(Name);
        }
    }
    /// <summary>
    /// An unnamed connection cannot obtain a schema from the registry.
    /// </summary>
    [Fact]
    public void SchemaRequiresConnectionName()
    {
        DbConnectionInfo Info = new();
        Assert.Throws<ArgumentException>(() => { _ = Info.Schema; });
        Assert.Null(DbSchemas.FindOrAdd(Info));
        Assert.Null(DbSchemas.FindOrAdd(null));
        Assert.Null(DbSchemas.Find("missing"));
        Assert.Throws<KeyNotFoundException>(() => DbSchemas.Get("missing"));
    }
    /// <summary>
    /// Removing a schema permits the next request to create a new one.
    /// </summary>
    [Fact]
    public void RemovedSchemaIsCreatedAgainOnNextRequest()
    {
        string Name = $"schema-{Guid.NewGuid():N}";
        try
        {
            DbConnectionInfo Info = new() { Name = Name };
            DbSchema First = Info.Schema;
            Assert.Same(First, DbSchemas.FindOrAdd(Info));
            Assert.Same(First, DbSchemas.GetOrAdd(Info));
            Assert.True(DbSchemas.Remove(Name));
            Assert.Null(DbSchemas.Find(Name));
            Assert.NotSame(First, Info.Schema);
        }
        finally
        {
            DbSchemas.Remove(Name);
        }
    }
}
