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
    // ● public
    /// <summary>
    /// Selects registered statements with the row limit and rejects unknown selects and filters.
    /// </summary>
    [Fact]
    public void SelectListUsesRegisteredSelectsAndRejectsFilters()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string ModuleName = $"Module_{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-remote-list-{Guid.NewGuid():N}.db");
        int OriginalLimit = DbConfig.DefaultRowLimit;
        ModuleDef Definition = new() { Name = ModuleName, ConnectionName = ConnectionName };
        Definition.Table.Name = "Customer";
        Definition.Table.Fields.Add(new FieldDef { Name = "Id" });
        Definition.Table.Fields.Add(new FieldDef { Name = "Name" });
        Definition.SelectList.Add("First", "SELECT Id, Name FROM Customer ORDER BY Name");
        Definition.SelectList.Add("Second", "SELECT Id, Name FROM Customer ORDER BY Name DESC");
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" });
            DataRegistry.Modules.Add(Definition);
            SqlStore Store = SqlStores.CreateSqlStore(ConnectionName);
            Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT)");
            Store.ExecSql("INSERT INTO Customer VALUES ('C1', 'Alice'), ('C2', 'Zoe')");
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
            Request.Params["SelectName"] = "missing";
            Assert.False(RemoteOperations.Execute(Request).IsSuccess);
            Request.Params["SelectName"] = "First";
            Request.Params["Filters"] = new[] { new { Field = "Name" } };
            RemoteResponse Filtered = RemoteOperations.Execute(Request);
            Assert.False(Filtered.IsSuccess);
            Assert.Contains("filters", Filtered.ErrorText);
            Request = Json.Deserialize<RemoteRequest>(Json.Serialize(Request));
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
