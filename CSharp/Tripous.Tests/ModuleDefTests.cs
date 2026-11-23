/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests data module definitions and their table trees.
/// </summary>
public class ModuleDefTests
{
    // ● public
    /// <summary>
    /// A module uses the configured default connection unless one is specified.
    /// </summary>
    [Fact]
    public void ConnectionNameFallsBackToConfiguredDefault()
    {
        ModuleDef Module = new();
        Assert.Equal(DbConfig.DefaultConnectionName, Module.ConnectionName);
        Module.ConnectionName = "Reporting";
        Assert.Equal("Reporting", Module.ConnectionName);
        Module.ConnectionName = " ";
        Assert.Equal(DbConfig.DefaultConnectionName, Module.ConnectionName);
    }
    /// <summary>
    /// Table definitions are returned master first and references are connected.
    /// </summary>
    [Fact]
    public void GetTablesReturnsTreeAndUpdatesReferences()
    {
        ModuleDef Module = new() { Name = "Orders" };
        Module.Table.Name = "OrderHeader";
        FieldDef Field = new() { Name = "Id" };
        Module.Table.Fields.Add(Field);
        TableDef Detail = new() { Name = "OrderLine" };
        TableDef SubDetail = new() { Name = "OrderPart" };
        Detail.Details.Add(SubDetail);
        Module.Table.Details.Add(Detail);

        List<TableDef> Tables = Module.GetTables();

        Assert.Equal(new[] { Module.Table, Detail, SubDetail }, Tables);
        Assert.Same(Module.Table, Field.TableDef);
        Assert.Same(Module.Table, Detail.Master);
        Assert.Same(Detail, SubDetail.Master);
    }
    /// <summary>
    /// JSON loading preserves field types and reconnects nested table references.
    /// </summary>
    [Fact]
    public void DeserializeReconnectsTableTree()
    {
        ModuleDef Module = new() { Name = "Orders" };
        Module.Table.Name = "OrderHeader";
        Module.Table.Fields.Add(new FieldDef { Name = "Id", DataType = DataFieldType.Int64 });
        Module.Table.Details.Add(new TableDef { Name = "OrderLine" });

        ModuleDef Loaded = Json.Deserialize<ModuleDef>(Json.Serialize(Module));

        Assert.Equal("OrderHeader", Loaded.Table.Name);
        Assert.Equal(DataFieldType.Int64, Loaded.Table.Fields.Get("Id").DataType);
        Assert.Same(Loaded.Table, Loaded.Table.Fields.Get("Id").TableDef);
        Assert.Same(Loaded.Table, Loaded.Table.Details.Get("OrderLine").Master);
    }
}
