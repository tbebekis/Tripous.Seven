/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests remote selection of data module lists.
/// </summary>
[Collection("Database registry")]
public class DataModuleSelectListTests
{
    /// <summary>
    /// Supplies custom SQL and a bound parameter for one registered SELECT.
    /// </summary>
    public class ListSqlModule : DataModule
    {
        /// <summary>
        /// Restricts the custom SELECT to the names after Alice.
        /// </summary>
        protected override string BuildListSelectSql(SelectDef Select, JsonSelectFilters Filters, out SqlParams Params)
        {
            if (Select.Name != "Custom")
                return base.BuildListSelectSql(Select, Filters, out Params);
            Params = new SqlParams().Add("AfterName", "Alice");
            return $"select * from ({Select.SqlText}) X where X.Name > {SqlProvider.GlobalPrefix}AfterName order by X.Name";
        }
        /// <summary>
        /// Accepts a named value for the application's active filter.
        /// </summary>
        protected override int ConvertSelectFilterBoolean(object Value)
            => Equals(Value, "yes") ? 1 : base.ConvertSelectFilterBoolean(Value);
    }

    // ● public
    /// <summary>
    /// Selects registered statements with validated filters and the row limit.
    /// </summary>
    [Fact]
    public void SelectListUsesRegisteredSelectsAndFilters()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string ModuleName = $"Module_{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-remote-list-{Guid.NewGuid():N}.db");
        int OriginalLimit = DbConfig.DefaultRowLimit;
        ModuleDef Definition = new() { Name = ModuleName, ConnectionName = ConnectionName, ClassName = typeof(ListSqlModule).AssemblyQualifiedName };
        Definition.Table.Name = "Customer";
        Definition.Table.Fields.Add(new FieldDef { Name = "Id" });
        Definition.Table.Fields.Add(new FieldDef { Name = "Name" });
        Definition.SelectList.Add("First", "SELECT Id, Name, IsActive, Amount FROM Customer ORDER BY Name");
        Definition.SelectList.Add("Second", "SELECT Id, Name FROM Customer ORDER BY Name DESC");
        Definition.SelectList.Add("Terminated", "SELECT Id, Name FROM Customer ORDER BY Name; -- trailing comment");
        Definition.SelectList.Add("Custom", "SELECT Id, Name FROM Customer");
        Definition.SelectList.Add("Limited", "SELECT Id, Name FROM Customer LIMIT 1");
        Definition.SelectList.Get("First").FilterDefs.Add("Name");
        Definition.SelectList.Get("First").AddFilter("IsActive", FilterDataType: DataFieldType.Boolean);
        Definition.SelectList.Get("First").AddFilter("Amount", FilterDataType: DataFieldType.Integer);
        Definition.SelectList.Get("Terminated").FilterDefs.Add("Name");
        Definition.SelectList.Get("Limited").FilterDefs.Add("Name");
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" });
            DataRegistry.Modules.Add(Definition);
            SqlStore Store = SqlStores.CreateSqlStore(ConnectionName);
            Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT, IsActive INTEGER, Amount INTEGER)");
            Store.ExecSql("INSERT INTO Customer VALUES ('C1', 'Alice', 0, 5), ('C2', 'Zoe', 1, 15), ('C3', 'Bob', 0, 5)");
            DbConfig.DefaultRowLimit = 1;
            RemOp.Initialize();
            RemoteRequest Request = new() { Id = Guid.NewGuid().ToString(), OperationName = "DataModule.SelectList" };
            Request.Params["ModuleName"] = ModuleName;
            Request.Params["Filters"] = Array.Empty<object>();
            Request = Json.Deserialize<RemoteRequest>(Json.Serialize(Request));
            RemoteResponse First = RemoteOperations.Execute(Request);
            Assert.True(First.IsSuccess, First.ErrorText);
            Assert.Equal("Success", First["TableSetResultStatus"]);
            Assert.Equal("First", First["SelectName"]);
            Assert.Equal("Alice", Assert.Single(Assert.IsType<JsonDataTable>(First["Table"]).Rows).Data[1]);
            Request.Params["SelectName"] = "second";
            RemoteResponse Second = RemoteOperations.Execute(Request);
            Assert.True(Second.IsSuccess, Second.ErrorText);
            Assert.Equal("Second", Second["SelectName"]);
            Assert.Equal("Zoe", Assert.Single(Assert.IsType<JsonDataTable>(Second["Table"]).Rows).Data[1]);
            Request.Params["SelectName"] = "Custom";
            RemoteResponse Custom = RemoteOperations.Execute(Request);
            Assert.True(Custom.IsSuccess, Custom.ErrorText);
            Assert.Equal("Custom", Custom["SelectName"]);
            Assert.Equal("Bob", Assert.Single(Assert.IsType<JsonDataTable>(Custom["Table"]).Rows).Data[1]);
            Request.Params["SelectName"] = "missing";
            Assert.False(RemoteOperations.Execute(Request).IsSuccess);
            Request.Params["SelectName"] = "First";
            Request.Params["Filters"] = new[] { new JsonSelectFilter { Name = "Name", ConditionOp = "Contains", Value = "oe" } };
            RemoteResponse Filtered = RemoteOperations.Execute(Request);
            Assert.True(Filtered.IsSuccess, Filtered.ErrorText);
            Assert.Equal("Zoe", Assert.Single(Assert.IsType<JsonDataTable>(Filtered["Table"]).Rows).Data[1]);
            Request = Json.Deserialize<RemoteRequest>(Json.Serialize(Request));
            Filtered = RemoteOperations.Execute(Request);
            Assert.True(Filtered.IsSuccess, Filtered.ErrorText);
            Assert.Equal("Zoe", Assert.Single(Assert.IsType<JsonDataTable>(Filtered["Table"]).Rows).Data[1]);
            Request.Params["Filters"] = new[]
            {
                new JsonSelectFilter { Name = "IsActive", Value = true },
                new JsonSelectFilter { Name = "Amount", ConditionOp = "Between", Value = 10, Value2 = 20 },
            };
            Request = Json.Deserialize<RemoteRequest>(Json.Serialize(Request));
            Filtered = RemoteOperations.Execute(Request);
            Assert.True(Filtered.IsSuccess, Filtered.ErrorText);
            Assert.Equal("Zoe", Assert.Single(Assert.IsType<JsonDataTable>(Filtered["Table"]).Rows).Data[1]);
            Request.Params["Filters"] = new[] { new JsonSelectFilter { Name = "IsActive", Value = "yes" } };
            Request = Json.Deserialize<RemoteRequest>(Json.Serialize(Request));
            Filtered = RemoteOperations.Execute(Request);
            Assert.True(Filtered.IsSuccess, Filtered.ErrorText);
            Assert.Equal("Zoe", Assert.Single(Assert.IsType<JsonDataTable>(Filtered["Table"]).Rows).Data[1]);
            Request.Params["Filters"] = new[] { new JsonSelectFilter { Name = "Name", ConditionOp = "Contains", Value = "oe" } };
            Request.Params["SelectName"] = "Terminated";
            Filtered = RemoteOperations.Execute(Request);
            Assert.True(Filtered.IsSuccess, Filtered.ErrorText);
            Assert.Equal("Zoe", Assert.Single(Assert.IsType<JsonDataTable>(Filtered["Table"]).Rows).Data[1]);
            Request.Params["SelectName"] = "Limited";
            RemoteResponse Limited = RemoteOperations.Execute(Request);
            Assert.False(Limited.IsSuccess);
            Assert.Contains("LIMIT", Limited.ErrorText);
            Request.Params["SelectName"] = "First";
            Request.Params["Filters"] = new[] { new JsonSelectFilter { Name = "Unknown", Value = "Zoe" } };
            Assert.False(RemoteOperations.Execute(Request).IsSuccess);
            Request.Params["Filters"] = new[] { new JsonSelectFilter { Name = "Name", ConditionOp = "Greater", Value = "Zoe" } };
            Assert.False(RemoteOperations.Execute(Request).IsSuccess);
        }
        finally
        {
            DbConfig.DefaultRowLimit = OriginalLimit;
            DataRegistry.Modules.Remove(Definition);
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
}
