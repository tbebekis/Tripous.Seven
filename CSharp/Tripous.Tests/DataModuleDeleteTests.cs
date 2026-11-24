/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests remote data module deletion.
/// </summary>
[Collection("Database registry")]
public class DataModuleDeleteTests
{
    // ● public
    /// <summary>
    /// Deletion succeeds for existing and already absent items but rejects invalid IDs.
    /// </summary>
    [Fact]
    public void DeleteHandlesExistingMissingAndInvalidIds()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string ModuleName = $"Module_{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-remote-delete-{Guid.NewGuid():N}.db");
        ModuleDef Definition = new() { Name = ModuleName, ConnectionName = ConnectionName };
        Definition.Table.Name = "Customer";
        Definition.Table.Fields.Add(new FieldDef { Name = "Id", DataType = DataFieldType.Int64 });
        Definition.Table.Fields.Add(new FieldDef { Name = "Name" });
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" });
            DataRegistry.Modules.Add(Definition);
            SqlStore Store = SqlStores.CreateSqlStore(ConnectionName);
            Store.ExecSql("CREATE TABLE Customer (Id INTEGER PRIMARY KEY, Name TEXT)");
            Store.ExecSql("INSERT INTO Customer VALUES (100, 'Theo')");
            RemoteOperations.Register(typeof(DataModuleDelete));
            RemoteRequest Request = new() { Id = Guid.NewGuid().ToString(), OperationName = "DataModule.Delete" };
            Request.Params["ModuleName"] = ModuleName;
            Request.Params["Id"] = 100;
            Request = Json.Deserialize<RemoteRequest>(Json.Serialize(Request));
            RemoteResponse Deleted = RemoteOperations.Execute(Request);
            Assert.True(Deleted.IsSuccess, Deleted.ErrorText);
            Assert.Equal("Success", Deleted["TableSetResultStatus"]);
            Assert.Equal(true, Deleted["Success"]);
            Assert.Equal(0L, Store.Select("SELECT COUNT(*) FROM Customer").Rows[0][0]);
            RemoteResponse Missing = RemoteOperations.Execute(Request);
            Assert.True(Missing.IsSuccess, Missing.ErrorText);
            Assert.Equal("NotFound", Missing["TableSetResultStatus"]);
            Assert.Equal(true, Missing["Success"]);
            Request.Params["Id"] = "not-a-number";
            Assert.False(RemoteOperations.Execute(Request).IsSuccess);
            Request.Params["Id"] = "";
            Assert.False(RemoteOperations.Execute(Request).IsSuccess);
        }
        finally
        {
            DataRegistry.Modules.Remove(Definition);
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
}
