/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Verifies lookup, locator mapping, commit and FactBox data in one remote order flow.
/// </summary>
[Collection("Database registry")]
public class OrderReferenceFlowTests
{
    // ● public
    /// <summary>
    /// Updates an order and its detail from remote reference selections.
    /// </summary>
    [Fact]
    public void SavesSelectionsAndShowsUpdatedOrder()
    {
        RemOp.Initialize();
        string Name = $"OrderFlow-{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"{Name}.db");
        var Connection = new DbConnectionInfo { Name = Name, ConnectionString = $"Data Source={FilePath};Pooling=False" };
        var Module = new ModuleDef { Name = Name, ConnectionName = Name };
        Module.Table.Name = "Orders";
        Module.Table.AddInt64Id("Id");
        Module.Table.AddInt64("PaymentMethodId").LookupSource = Name;
        TableDef Line = Module.Table.AddDetail("OrderLine", "Id", "OrderId");
        Line.AddInt64Id("Id");
        Line.AddInt64("OrderId");
        Line.AddInt64("ProductId").Locator = Name;
        Line.AddString("ProductName").SnapshotOf = "Product.Name";
        Line.AddJoin("ProductId", "Product", "Product").AddString("Name");
        Module.FactBoxes.Add(new ItemFactBoxDef { Name = "OrderSummary", ProviderClassName = typeof(OrderSummaryProvider).AssemblyQualifiedName });

        var Lookup = new LookupDef { Name = Name, TableName = "PaymentMethod", ConnectionName = Connection.Name };
        var Locator = new LocatorDef { Name = Name, Source = "Product" };
        Locator.Add("Id", DataFieldType.Int64);
        Locator.Add("Code");
        Locator.Add("Name");
        Locator.AddSearchFields("Code");
        Locator.AddResultFields("Id", "Name");
        Locator.AddListVisibleFields("Name");

        Db.Connections.Add(Connection);
        DataRegistry.Modules.Add(Module);
        DataRegistry.Lookups.Add(Lookup);
        DataRegistry.Locators.Add(Locator);
        try
        {
            var Store = new SqlStore(Connection);
            Store.ExecSql(@"CREATE TABLE PaymentMethod (Id INTEGER PRIMARY KEY, Name TEXT);
CREATE TABLE Product (Id INTEGER PRIMARY KEY, Code TEXT, Name TEXT);
CREATE TABLE Orders (Id INTEGER PRIMARY KEY, PaymentMethodId INTEGER REFERENCES PaymentMethod(Id));
CREATE TABLE OrderLine (Id INTEGER PRIMARY KEY, OrderId INTEGER REFERENCES Orders(Id), ProductId INTEGER REFERENCES Product(Id), ProductName TEXT);
INSERT INTO PaymentMethod VALUES (5000000000, 'Bank Transfer');
INSERT INTO Product VALUES (6000000000, 'COF-01', 'Coffee');
INSERT INTO Orders VALUES (1, NULL);
INSERT INTO OrderLine VALUES (2, 1, NULL, NULL);");

            JsonDataModule Packet = Assert.IsType<JsonDataModule>(Execute("DataModule.Edit", ("Id", "1"))["DataModule"]);
            JsonDataTable Order = Packet.Tables.Single(Table => Table.Name == "Orders");
            JsonDataTable OrderLine = Packet.Tables.Single(Table => Table.Name == "OrderLine");

            var PaymentMethods = Assert.IsType<JsonDataTable>(Execute("Lookup.GetList", ("ReferenceField", "PaymentMethodId"))["Table"]);
            object PaymentId = Assert.Single(PaymentMethods.Rows).Data[0];
            Assert.Equal(5000000000L, PaymentId);
            SetValue(Order, "PaymentMethodId", PaymentId);

            RemoteResponse Found = Execute("Locator.Execute", ("TableName", "OrderLine"), ("ReferenceField", "ProductId"), ("SearchTerm", "COF-01"));
            Assert.Equal("SingleResult", Found["Status"]);
            var Products = Assert.IsType<JsonDataTable>(Found["Table"]);
            var Mapping = Assert.IsType<JsonLocatorMapPlan>(Found["MapPlan"]);
            Assert.Equal("ProductId", Assert.Single(OrderLine.Locators.Items).MapPlan.ReferenceField);
            JsonDataRow Product = Assert.Single(Products.Rows);
            foreach (JsonLocatorMapItem Item in Mapping.Items)
                SetValue(OrderLine, Item.TargetField, Product.Data[Products.Columns.FindIndex(Column => Column.Name == Item.SourceField)]);
            Assert.Equal(6000000000L, OrderLine.Rows[0].Data[OrderLine.Columns.FindIndex(Column => Column.Name == "ProductId")]);

            Execute("DataModule.Commit", ("DataModule", Packet));
            Assert.Equal(5000000000L, Store.Int64Result("SELECT PaymentMethodId FROM Orders WHERE Id = 1", -1));
            Assert.Equal(6000000000L, Store.Int64Result("SELECT ProductId FROM OrderLine WHERE Id = 2", -1));
            Assert.Equal("Coffee", Store.SelectResult("SELECT ProductName FROM OrderLine WHERE Id = 2"));

            RemoteResponse Summary = Execute("DataModule.GetFactBoxes", ("KeyValue", "1"));
            var Box = Assert.Single(Assert.IsType<List<JsonItemFactBox>>(Summary["FactBoxes"]));
            using (var JsonData = System.Text.Json.JsonDocument.Parse(Json.Serialize(Box.Data)))
            {
                Assert.Equal(5000000000L, JsonData.RootElement.GetProperty("PaymentMethodId").GetInt64());
                Assert.Equal("Coffee", JsonData.RootElement.GetProperty("ProductName").GetString());
            }
        }
        finally
        {
            DataRegistry.Locators.Remove(Locator);
            DataRegistry.Lookups.Remove(Lookup);
            DataRegistry.Modules.Remove(Module);
            Db.Connections.Remove(Connection.Name);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }

        RemoteResponse Execute(string Operation, params (string Key, object Value)[] Values)
        {
            RemoteRequest Request = new() { Id = Guid.NewGuid().ToString(), OperationName = Operation };
            Request.Params["ModuleName"] = Module.Name;
            foreach (var (Key, Value) in Values)
                Request.Params[Key] = Value;
            RemoteResponse Response = RemoteOperations.Execute(Json.Deserialize<RemoteRequest>(Json.Serialize(Request)));
            Assert.True(Response.IsSuccess, Response.ErrorText);
            return Response;
        }
    }

    // ● private
    /// <summary>
    /// Applies a client-side edit to the row packet.
    /// </summary>
    static void SetValue(JsonDataTable Table, string Name, object Value)
    {
        int Index = Table.Columns.FindIndex(Column => Column.Name == Name);
        Assert.True(Index >= 0, $"Expected column {Name} in {Table.Name}.");
        JsonDataRow Row = Assert.Single(Table.Rows);
        Row.Data[Index] = Value;
        Row.State = (int)DataRowState.Modified;
    }

    // ● FactBox provider
    /// <summary>
    /// Shows the order's payment method and its selected product.
    /// </summary>
    public class OrderSummaryProvider : ItemFactBoxProvider
    {
        /// <summary>
        /// Builds the summary from the saved order and its line.
        /// </summary>
        public override object GetData(ItemFactBoxContext Context)
            => new
            {
                PaymentMethodId = Context.Row["PaymentMethodId"],
                ProductName = Context.Module.FindTable("OrderLine").Rows[0]["ProductName"],
            };
    }
}
