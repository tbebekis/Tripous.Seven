/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests the data module tables packet.
/// </summary>
[Collection("Database registry")]
public class JsonDataModuleTests
{
    // ● public
    /// <summary>
    /// The module packet separates list, item and stock tables while retaining logical field types.
    /// </summary>
    [Fact]
    public void DataModuleSeparatesTablesByPurpose()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-json-tables-{Guid.NewGuid():N}.db");
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" });
            ModuleDef Definition = new() { Name = "CustomerModule", TitleKey = "Customers", ConnectionName = ConnectionName };
            Definition.SelectList.Add("All", "SELECT Id, Active FROM Customer");
            Definition.SelectList.Add("Active", "SELECT Id, Active FROM Customer WHERE Active = 1");
            Definition.SecurityLevel = UserLevel.User | UserLevel.Service;
            Definition.Table.Name = "Customer";
            Definition.Table.FieldGroups.Add("Status");
            Definition.DetailOrder["Customer"] = new() { "Contacts", "Addresses" };
            Definition.Table.Fields.Add(new FieldDef { Name = "Id" });
            Definition.Table.Fields.Add(new FieldDef { Name = "Active", DataType = DataFieldType.Boolean, IsNullable = false });
            Definition.Stocks.Add("Country", null);
            SqlStore Store = SqlStores.CreateSqlStore(ConnectionName);
            Store.ExecSql("CREATE TABLE Country (Id TEXT, Name TEXT)");
            Store.ExecSql("INSERT INTO Country VALUES ('GR', 'Greece')");
            DataModule Module = Definition.Create(new UserContext(new AppUser { UserLevel = UserLevel.Admin | UserLevel.Service }));
            Module.Insert();
            Module.ItemTable.Rows[0]["Active"] = 1;
            Module.ListTable.Columns.Add("Name", typeof(string));
            Module.ListTable.Rows.Add("List row");

            JsonDataModule Packet = new(Module);

            Assert.Equal("CustomerModule", Packet.Name);
            Assert.Equal(Definition.Title, Packet.Title);
            Assert.Equal("Customers", Packet.TitleKey);
            Assert.Equal(new[] { "All", "Active" }, Packet.QueryNames);
            Assert.Equal((int)DataMode.Insert, Packet.State);
            Assert.Equal("List", Packet.ListTable.Name);
            Assert.Equal("List row", Assert.Single(Packet.ListTable.Rows).Data[0]);
            Assert.Equal("Customer", Packet.ItemTableName);
            JsonDataTable Item = Assert.Single(Packet.Tables);
            Assert.Equal("Customer", Item.Name);
            Assert.Equal((int)DataFieldType.Boolean, Item.Columns[1].DataType);
            Assert.False(Item.Columns[1].IsNullable);
            Assert.Equal("Country", Assert.Single(Packet.Stocks).Name);
            Assert.Equal("Greece", Packet.Stocks[0].Rows[0].Data[1]);
            JsonDataModule Copy = Json.Deserialize<JsonDataModule>(Json.Serialize(Packet));
            Assert.Equal("CustomerModule", Copy.Name);
            Assert.Equal(new[] { "User", "Service" }, Copy.SecurityLevel);
            Assert.Equal(Definition.Title, Copy.Title);
            Assert.Equal("Customers", Copy.TitleKey);
            Assert.Equal(new[] { "All", "Active" }, Copy.QueryNames);
            Assert.Equal((int)DataMode.Insert, Copy.State);
            Assert.Equal("Customer", Copy.ItemTableName);
            Assert.Equal(new[] { "Status" }, Assert.Single(Copy.Tables).FieldGroups);
            Assert.Equal(new[] { "Contacts", "Addresses" }, Copy.DetailOrder["Customer"]);
            Assert.NotSame(Definition.Table.FieldGroups, Item.FieldGroups);
            Assert.NotSame(Definition.DetailOrder["Customer"], Packet.DetailOrder["Customer"]);
            Assert.Single(Copy.Stocks);
            Definition.SecurityLevel = UserLevel.Admin;
            Assert.Equal(new[] { "Admin" }, new JsonDataModule(Module).SecurityLevel);
            Definition.SecurityLevel = UserLevel.None;
            Assert.Empty(new JsonDataModule(Module).SecurityLevel);
        }
        finally
        {
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A module must be initialized before its tables can be captured.
    /// </summary>
    [Fact]
    public void DataModuleRequiresInitializedModule()
    {
        Assert.Throws<InvalidOperationException>(() => new JsonDataModule(new DataModule()));
    }
}
