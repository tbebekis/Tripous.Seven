/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests remote item editing and key conversion.
/// </summary>
[Collection("Database registry")]
public class DataModuleEditTests
{
    // ● public
    /// <summary>
    /// Direct and JSON requests load string, Int32 and Int64 keys and report missing items.
    /// </summary>
    [Fact]
    public void RemoteEditConvertsKeysAndReturnsModulePacket()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-remote-edit-{Guid.NewGuid():N}.db");
        List<ModuleDef> Definitions = new();
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" });
            SqlStore Store = SqlStores.CreateSqlStore(ConnectionName);
            RemoteOperations.Register(typeof(DataModuleEdit));
            foreach ((string TableName, DataFieldType KeyType, string Id, string SqlId) in new[]
            {
                ("StringItem", DataFieldType.String, "C1", "'C1'"),
                ("IntegerItem", DataFieldType.Integer, "42", "42"),
                ("LongItem", DataFieldType.Int64, "9007199254740993", "9007199254740993"),
            })
            {
                string ModuleName = $"{TableName}_{Guid.NewGuid():N}";
                ModuleDef Definition = new() { Name = ModuleName, ConnectionName = ConnectionName };
                Definition.Table.Name = TableName;
                Definition.Table.Fields.Add(new FieldDef { Name = "Id", DataType = KeyType });
                Definition.Table.Fields.Add(new FieldDef { Name = "Name" });
                DataRegistry.Modules.Add(Definition);
                Definitions.Add(Definition);
                string SqlType = KeyType == DataFieldType.String ? "TEXT" : "INTEGER";
                Store.ExecSql($"CREATE TABLE {TableName} (Id {SqlType} PRIMARY KEY, Name TEXT)");
                Store.ExecSql($"INSERT INTO {TableName} VALUES ({SqlId}, 'Theo')");
                RemoteRequest Request = new() { Id = Guid.NewGuid().ToString(), OperationName = "DataModule.Edit" };
                Request.Params["ModuleName"] = ModuleName;
                Request.Params["Id"] = Id;
                RemoteResponse Direct = RemoteOperations.Execute(Request);
                Assert.True(Direct.IsSuccess, Direct.ErrorText);
                Assert.Equal("Success", Direct["TableSetResultStatus"]);
                JsonDataModule DirectPacket = Assert.IsType<JsonDataModule>(Direct["DataModule"]);
                Assert.Equal((int)DataMode.Edit, DirectPacket.State);
                Assert.Equal(Id, Convert.ToString(Assert.Single(Assert.Single(DirectPacket.Tables).Rows).Data[0], System.Globalization.CultureInfo.InvariantCulture));
                RemoteRequest Parsed = Json.Deserialize<RemoteRequest>(Json.Serialize(Request));
                RemoteResponse FromJson = RemoteOperations.Execute(Parsed);
                Assert.True(FromJson.IsSuccess, FromJson.ErrorText);
                Assert.Equal(Id, Convert.ToString(Assert.Single(Assert.Single(Assert.IsType<JsonDataModule>(FromJson["DataModule"]).Tables).Rows).Data[0], System.Globalization.CultureInfo.InvariantCulture));
                if (KeyType == DataFieldType.Integer)
                {
                    Request.Params["Id"] = 42;
                    RemoteRequest NumericRequest = Json.Deserialize<RemoteRequest>(Json.Serialize(Request));
                    Assert.True(RemoteOperations.Execute(NumericRequest).IsSuccess);
                }
                Request.Params["Id"] = KeyType == DataFieldType.String ? "Missing" : "999";
                RemoteResponse Missing = RemoteOperations.Execute(Request);
                Assert.False(Missing.IsSuccess);
                Assert.Equal("NotFound", Missing["TableSetResultStatus"]);
                Assert.Contains("not found", Missing.ErrorText);
            }
        }
        finally
        {
            foreach (ModuleDef Definition in Definitions)
                DataRegistry.Modules.Remove(Definition);
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
}
