/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests data module creation and initialization.
/// </summary>
public class DataModuleTests
{
    // ● public types
    /// <summary>
    /// A custom data module used to verify class selection.
    /// </summary>
    public class CustomModule : DataModule { }

    // ● private
    static ModuleDef CreateDefinition(string Name, string ConnectionName)
    {
        ModuleDef Definition = new() { Name = Name, ConnectionName = ConnectionName };
        Definition.Table.Name = "Customer";
        Definition.Table.Fields.Add(new FieldDef { Name = "Id" });
        Definition.Table.Fields.Add(new FieldDef { Name = "Name" });
        TableDef Detail = new() { Name = "Address", MasterField = "Id", DetailField = "CustomerId" };
        Detail.Fields.Add(new FieldDef { Name = "Id" });
        Detail.Fields.Add(new FieldDef { Name = "CustomerId" });
        Detail.Fields.Add(new FieldDef { Name = "City" });
        Definition.Table.Details.Add(Detail);
        return Definition;
    }

    // ● public
    /// <summary>
    /// List cancellation rejects edits without changing the item table.
    /// </summary>
    [Fact]
    public void ListCancelRejectsListChanges()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        ModuleDef Definition = CreateDefinition("CustomerModule", ConnectionName);
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = "Data Source=:memory:" });
            DataModule Module = Definition.Create();
            Module.ListTable.Columns.Add("Id", typeof(string));
            Module.ListTable.Columns.Add("Name", typeof(string));
            DataRow Row = Module.ListTable.Rows.Add("1", "Original");
            Module.ListTable.AcceptChanges();
            Row["Name"] = "Changed";
            DataRow Item = Module.ItemTable.AddNewRow();
            Item["Name"] = "Unsaved item";

            Module.ListCancel();

            Assert.Equal("Original", Row["Name"]);
            Assert.Equal(DataRowState.Unchanged, Row.RowState);
            Assert.Equal(DataRowState.Added, Item.RowState);
            Assert.Equal("Unsaved item", Item["Name"]);
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
        }
    }
    /// <summary>
    /// Both list SELECT overloads apply the configured default row limit.
    /// </summary>
    [Fact]
    public void ListSelectAppliesRowLimit()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-list-limit-{Guid.NewGuid():N}.db");
        ModuleDef Definition = CreateDefinition("CustomerModule", ConnectionName);
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" });
            DataModule Module = Definition.Create();
            Module.Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)");
            int Limit = DbConfig.DefaultRowLimit;
            Module.Store.ExecSql($"WITH RECURSIVE N(Value) AS (VALUES (1) UNION ALL SELECT Value + 1 FROM N WHERE Value < {Limit + 5}) INSERT INTO Customer SELECT CAST(Value AS TEXT), 'Name' FROM N");
            Assert.Equal(Limit, Module.ListSelect("select Id, Name from Customer order by CAST(Id AS INTEGER); -- list"));
            Assert.Equal(Limit, Module.ListTable.Rows.Count);
            Assert.Equal("1", Module.ListTable.Rows[0]["Id"]);
            Assert.Equal(Limit, Module.ListSelect(Definition.SelectList.Get("DEFAULT")));
            Assert.Throws<InvalidOperationException>(() => Module.ListSelect("select Id, Name from Customer limit 1"));
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A registered name creates a fresh initialized custom module and table tree.
    /// </summary>
    [Fact]
    public void CreateModuleByNameInitializesCustomModule()
    {
        string Name = $"Module_{Guid.NewGuid():N}";
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-module-{Guid.NewGuid():N}.db");
        ModuleDef Definition = CreateDefinition(Name, ConnectionName);
        Definition.ClassName = typeof(CustomModule).FullName;
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" });
            TypeStore.Register(typeof(CustomModule));
            DataRegistry.Modules.Add(Definition);
            DataModule Module = DataRegistry.CreateModule(Name.ToUpperInvariant());
            DataModule Other = DataRegistry.CreateModule(Name);

            Assert.IsType<CustomModule>(Module);
            Assert.NotSame(Module, Other);
            Assert.Same(Definition, Module.ModuleDef);
            Assert.NotSame(Module.ItemTable, Other.ItemTable);
            Assert.Same(Module.ItemTable, Module.Tables.Get("customer"));
            Assert.Same(Module.ItemTable, Module.Tables.Get("Address").Master);
            Assert.True(Module.ItemTable.DetailsActive);
            Assert.NotNull(Module.TableSet);
            Assert.NotNull(Module.ListTable);
            SelectDef DefaultSelect = Definition.SelectList.Get("DEFAULT");
            Assert.Equal("select * from Customer", DefaultSelect.SqlText);
            Assert.Same(Definition, DefaultSelect.Owner);
            Assert.Single(Definition.SelectList);
            Assert.Throws<InvalidOperationException>(() => Module.Initialize(Definition));

            Module.Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)");
            Module.Store.ExecSql("CREATE TABLE Address (Id TEXT PRIMARY KEY, CustomerId TEXT NOT NULL, City TEXT NOT NULL)");
            DataRow Customer = Module.ItemTable.AddNewRow();
            Customer["Name"] = "Theo";
            DataRow Address = Module.Tables.Get("Address").AddNewRow();
            Address["City"] = "Athens";
            string Id = (string)Customer["Id"];
            Assert.Equal(Id, Module.TableSet.Commit());
            Module.TableSet.ProcessEmpty();
            Assert.True(Module.TableSet.Load(Id));
            Assert.Equal("Athens", Assert.Single(Module.Tables.Get("Address").Rows.Cast<DataRow>())["City"]);
            Assert.Equal(1, Module.ListSelect(DefaultSelect));
            Assert.Equal(Id, Assert.Single(Module.ListTable.Rows.Cast<DataRow>())["Id"]);
            Assert.Equal(1, Module.ListSelect("SELECT Id, Name FROM Customer WHERE Name = 'Theo'"));
            Assert.Equal("Theo", Assert.Single(Module.ListTable.Rows.Cast<DataRow>())["Name"]);
        }
        finally
        {
            DataRegistry.Modules.Remove(Definition);
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// An explicitly supplied list SELECT is retained during initialization.
    /// </summary>
    [Fact]
    public void InitializationPreservesExplicitListSelect()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        ModuleDef Definition = CreateDefinition("CustomerModule", ConnectionName);
        SelectDef Select = Definition.SelectList.Add("Active", "select Id, Name from Customer");
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = "Data Source=:memory:" });
            DataModule Module = Definition.Create();
            Assert.Single(Definition.SelectList);
            Assert.Same(Select, Definition.SelectList[0]);
            Assert.Same(Definition, Select.Owner);
            Assert.Throws<ArgumentNullException>(() => Module.ListSelect((SelectDef)null));
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
        }
    }
    /// <summary>
    /// Failed initialization leaves the instance available for a later retry.
    /// </summary>
    [Fact]
    public void FailedInitializationCanBeRetried()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        ModuleDef Definition = CreateDefinition("CustomerModule", ConnectionName);
        DataModule Module = new();
        try
        {
            Assert.Throws<KeyNotFoundException>(() => Module.Initialize(Definition));
            Assert.Null(Module.ModuleDef);
            Assert.Null(Module.Tables);
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = "Data Source=:memory:" });
            Module.Initialize(Definition);
            Assert.Same(Definition, Module.ModuleDef);
            Assert.IsType<DataModule>(Definition.Create());
            Assert.Throws<InvalidOperationException>(() => Module.Initialize(Definition));
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
        }
    }
}
