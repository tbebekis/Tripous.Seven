/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests remote data module initialization and insertion.
/// </summary>
[Collection("Database registry")]
public class DataModuleInitializeInsertTests
{
    // ● public
    /// <summary>
    /// Initialization returns an empty item and insertion returns a new item with defaults.
    /// </summary>
    [Fact]
    public void InitializeAndInsertReturnModulePackets()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string ModuleName = $"Module_{Guid.NewGuid():N}";
        ModuleDef Definition = new() { Name = ModuleName, ConnectionName = ConnectionName };
        Definition.Table.Name = "Customer";
        Definition.Table.Fields.Add(new FieldDef { Name = "Id" });
        Definition.Table.Fields.Add(new FieldDef { Name = "Name", DefaultValue = "New customer" });
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = "Data Source=:memory:" });
            DataRegistry.Modules.Add(Definition);
            RemOp.Initialize();
            RemoteRequest Request = new() { Id = Guid.NewGuid().ToString(), OperationName = "DataModule.Initialize" };
            Request.Params["ModuleName"] = ModuleName;
            RemoteResponse InitialResponse = RemoteOperations.Execute(Request);
            Assert.True(InitialResponse.IsSuccess, InitialResponse.ErrorText);
            Assert.True(InitialResponse.ContainsKey("TableSetResultStatus"));
            Assert.Null(InitialResponse["TableSetResultStatus"]);
            JsonDataModule Initial = Assert.IsType<JsonDataModule>(InitialResponse["DataModule"]);
            Assert.Equal((int)DataMode.None, Initial.State);
            Assert.Equal(new[] { "DEFAULT" }, Initial.QueryNames);
            Assert.Empty(Assert.Single(Initial.Tables).Rows);
            Request.OperationName = "DataModule.Insert";
            Request = Json.Deserialize<RemoteRequest>(Json.Serialize(Request));
            RemoteResponse InsertResponse = RemoteOperations.Execute(Request);
            Assert.True(InsertResponse.IsSuccess, InsertResponse.ErrorText);
            Assert.True(InsertResponse.ContainsKey("TableSetResultStatus"));
            Assert.Null(InsertResponse["TableSetResultStatus"]);
            JsonDataModule Inserted = Assert.IsType<JsonDataModule>(InsertResponse["DataModule"]);
            Assert.Equal((int)DataMode.Insert, Inserted.State);
            JsonDataRow Row = Assert.Single(Assert.Single(Inserted.Tables).Rows);
            Assert.Equal((int)DataRowState.Added, Row.State);
            Assert.True(Guid.TryParse((string)Row.Data[0], out _));
            Assert.Equal("New customer", Row.Data[1]);
        }
        finally
        {
            DataRegistry.Modules.Remove(Definition);
            Db.Connections.Remove(ConnectionName);
        }
    }
}
