/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests code reservation and recovery during data module commits.
/// </summary>
[Collection("Database registry")]
public class CodeProviderCommitTests
{
    // ● public
    /// <summary>
    /// Sequence resets and updates remain inside the caller's transaction.
    /// </summary>
    [Fact]
    public void NumberSeriesResetsAndRollsBack()
    {
        string ConnectionName = $"codes-{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-code-reset-{Guid.NewGuid():N}.db");
        DbConnectionInfo Info = new() { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" };
        try
        {
            Db.Connections.Add(Info);
            SystemSchema.Register(ConnectionName).Execute();
            SqlStore Store = new(Info);
            Store.ExecSql("INSERT INTO SYS_NUMBER_SERIES (Id, Code, Name, Pattern, ResetPeriodId, NextNumber, LastResetValue) VALUES ('1', 'ORDERS', 'Orders', 'SO-{YYYY}-{MM}-{XXXX}', 4, 9, '2026-05')");
            CodeProviderStore Provider = new();
            using (SqlTransactionContext Transaction = Store.BeginTransactionContext())
            {
                Assert.Equal("SO-2026-05-0009", Provider.GetNextCode(Store, Transaction.Transaction, "ORDERS", new DateTime(2026, 5, 31)));
                Transaction.Commit();
            }
            using (SqlTransactionContext Transaction = Store.BeginTransactionContext())
            {
                Assert.Equal("SO-2026-06-0001", Provider.GetNextCode(Store, Transaction.Transaction, "ORDERS", new DateTime(2026, 6, 1)));
                Transaction.Rollback();
            }
            DataRow Row = Store.Select("SELECT NextNumber, LastResetValue FROM SYS_NUMBER_SERIES").Rows[0];
            Assert.Equal(10L, Convert.ToInt64(Row["NextNumber"]));
            Assert.Equal("2026-05", Row["LastResetValue"]);
            using (SqlTransactionContext Transaction = Store.BeginTransactionContext())
            {
                Assert.Equal("SO-2026-06-0001", Provider.GetNextCode(Store, Transaction.Transaction, "ORDERS", new DateTime(2026, 6, 1)));
                Transaction.Commit();
            }
            Row = Store.Select("SELECT NextNumber, LastResetValue FROM SYS_NUMBER_SERIES").Rows[0];
            Assert.Equal(2L, Convert.ToInt64(Row["NextNumber"]));
            Assert.Equal("2026-06", Row["LastResetValue"]);
        }
        finally
        {
            Schema Schema = Schemas.Find("System", ConnectionName);
            if (Schema != null)
                Schemas.List.Remove(Schema);
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A remote commit returns the generated code and can be retried after rollback.
    /// </summary>
    [Fact]
    public void RemoteCommitReturnsGeneratedCode()
    {
        string ConnectionName = $"codes-{Guid.NewGuid():N}";
        string ModuleName = $"module-{Guid.NewGuid():N}";
        string ProviderName = $"provider-{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-remote-codes-{Guid.NewGuid():N}.db");
        DbConnectionInfo Info = new() { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" };
        CodeProviderDef Provider = new() { Name = ProviderName };
        ModuleDef Definition = new() { Name = ModuleName, ConnectionName = ConnectionName, CodeProvider = ProviderName, CodeFieldName = "OrderNumber" };
        Definition.Table.Name = "Customer";
        Definition.Table.Fields.Add(new FieldDef { Name = "Id" });
        Definition.Table.Fields.Add(new FieldDef { Name = "OrderNumber" });
        Definition.Table.Fields.Add(new FieldDef { Name = "Name" });
        try
        {
            Db.Connections.Add(Info);
            DataRegistry.CodeProviders.Add(Provider);
            DataRegistry.Modules.Add(Definition);
            SystemSchema.Register(ConnectionName).Execute();
            SqlStore Store = new(Info);
            Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, OrderNumber TEXT NOT NULL, Name TEXT NOT NULL)");
            Store.ExecSql("INSERT INTO SYS_NUMBER_SERIES (Id, Code, Name, Pattern) VALUES (:Id, :Code, :Name, :Pattern)", new SqlParams().Add("Id", Guid.NewGuid().ToString()).Add("Code", ProviderName).Add("Name", ProviderName).Add("Pattern", "C-{XXXX}"));
            RemOp.Initialize();
            DataModule Input = Definition.Create();
            Input.Insert();
            RemoteRequest Request = new() { Id = Guid.NewGuid().ToString(), OperationName = "DataModule.Commit" };
            Request.Params["ModuleName"] = ModuleName;
            Request.Params["DataModule"] = new JsonDataModule(Input);
            Request = Json.Deserialize<RemoteRequest>(Json.Serialize(Request));
            RemoteResponse Failed = RemoteOperations.Execute(Request);
            Assert.False(Failed.IsSuccess);
            Assert.Equal("CommitFailed", Failed["TableSetResultStatus"]);
            Assert.Equal(1L, Convert.ToInt64(Store.Select("SELECT NextNumber FROM SYS_NUMBER_SERIES").Rows[0][0]));
            Input.CurrentRow["Name"] = "Alice";
            Request.Params["DataModule"] = new JsonDataModule(Input);
            Request = Json.Deserialize<RemoteRequest>(Json.Serialize(Request));
            RemoteResponse Success = RemoteOperations.Execute(Request);
            Assert.True(Success.IsSuccess, Success.ErrorText);
            Assert.Equal("Success", Success["TableSetResultStatus"]);
            JsonDataTable Table = Assert.Single(Assert.IsType<JsonDataModule>(Success["DataModule"]).Tables);
            int Index = Table.Columns.FindIndex(Column => Column.Name == "OrderNumber");
            Assert.True(Index >= 0);
            Assert.Equal("C-0001", Assert.Single(Table.Rows).Data[Index]);
            Assert.Equal("C-0001", Store.Select("SELECT OrderNumber FROM Customer").Rows[0][0]);
            Assert.Equal(2L, Convert.ToInt64(Store.Select("SELECT NextNumber FROM SYS_NUMBER_SERIES").Rows[0][0]));
        }
        finally
        {
            DataRegistry.Modules.Remove(Definition);
            DataRegistry.CodeProviders.Remove(Provider);
            Schema Schema = Schemas.Find("System", ConnectionName);
            if (Schema != null)
                Schemas.List.Remove(Schema);
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A failed insert rolls back the series and clears the generated code before retry.
    /// Manual codes and updates do not consume sequence numbers.
    /// </summary>
    [Theory]
    [InlineData("Code", false, false)]
    [InlineData("OrderNumber", true, false)]
    [InlineData("OrderNumber", true, true)]
    public void CommitReservesCodeAndRestoresItOnRollback(string FieldName, bool UseCustomName, bool UseRowVersion)
    {
        string ConnectionName = $"codes-{Guid.NewGuid():N}";
        string ProviderName = $"provider-{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-codes-{Guid.NewGuid():N}.db");
        DbConnectionInfo Info = new() { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" };
        CodeProviderDef Provider = new() { Name = ProviderName };
        try
        {
            Db.Connections.Add(Info);
            DataRegistry.CodeProviders.Add(Provider);
            SystemSchema.Register(ConnectionName).Execute();
            SqlStore Store = new(Info);
            string VersionColumn = UseRowVersion ? ", Version TEXT NOT NULL" : "";
            Store.ExecSql($"CREATE TABLE Customer (Id TEXT PRIMARY KEY, {FieldName} TEXT NOT NULL, Name TEXT NOT NULL{VersionColumn})");
            Store.ExecSql("INSERT INTO SYS_NUMBER_SERIES (Id, Code, Name, Pattern) VALUES (:Id, :Code, :Name, :Pattern)", new SqlParams().Add("Id", Guid.NewGuid().ToString()).Add("Code", ProviderName).Add("Name", ProviderName).Add("Pattern", "C-{XXXX}"));
            ModuleDef Definition = new() { Name = "Customer", ConnectionName = ConnectionName, CodeProvider = ProviderName };
            if (UseCustomName)
                Definition.CodeFieldName = FieldName;
            Definition.Table.Name = "Customer";
            Definition.Table.Fields.Add(new FieldDef { Name = "Id" });
            Definition.Table.Fields.Add(new FieldDef { Name = FieldName });
            Definition.Table.Fields.Add(new FieldDef { Name = "Name" });
            if (UseRowVersion)
            {
                Definition.RowVersionFieldName = "Version";
                Definition.Table.Fields.Add(new FieldDef { Name = "Version", MaxLength = 36 });
            }
            Definition.CodeFieldName = "Missing";
            Assert.Throws<InvalidOperationException>(() => Definition.Create());
            Definition.CodeFieldName = FieldName;
            Definition.Table.Fields.Get(FieldName).DataType = DataFieldType.Integer;
            Assert.Throws<InvalidOperationException>(() => Definition.Create());
            Definition.Table.Fields.Get(FieldName).DataType = DataFieldType.String;
            Definition.Table.Fields.Get(FieldName).Flags = FieldFlags.Extra;
            Assert.Contains("INSERT", Assert.Throws<InvalidOperationException>(() => Definition.Create()).Message);
            Definition.Table.Fields.Get(FieldName).Flags = FieldFlags.NoInsertUpdate;
            Assert.Contains("INSERT", Assert.Throws<InvalidOperationException>(() => Definition.Create()).Message);
            Definition.Table.Fields.Get(FieldName).Flags = FieldFlags.None;
            Definition.CodeFieldName = "ID";
            Assert.Contains("primary key", Assert.Throws<InvalidOperationException>(() => Definition.Create()).Message);
            Definition.CodeFieldName = FieldName;
            Definition.CodeProvider = "MissingProvider";
            Assert.Throws<KeyNotFoundException>(() => Definition.Create());
            Definition.CodeProvider = ProviderName;
            if (!UseCustomName)
                Definition.CodeFieldName = null;
            DataModule Module = Definition.Create();
            Module.Insert();
            Assert.True(Module.CurrentRow.IsNull(FieldName));
            Assert.Throws<SqliteException>(() => Module.Commit(false));
            Assert.True(Module.CurrentRow.IsNull(FieldName));
            if (UseRowVersion)
                Assert.True(Module.CurrentRow.IsNull("Version"));
            Assert.Equal(1L, Convert.ToInt64(Store.Select("SELECT NextNumber FROM SYS_NUMBER_SERIES").Rows[0][0]));
            Module.CurrentRow["Name"] = "Alice";
            Module.Commit(false);
            Assert.Equal("C-0001", Module.CurrentRow[FieldName]);
            if (UseRowVersion)
                Assert.True(Guid.TryParse((string)Module.CurrentRow["Version"], out _));
            Assert.Equal(2L, Convert.ToInt64(Store.Select("SELECT NextNumber FROM SYS_NUMBER_SERIES").Rows[0][0]));
            Module.CurrentRow["Name"] = "Alice updated";
            Module.Commit(false);
            Module.Insert();
            Module.CurrentRow[FieldName] = "MANUAL";
            Module.CurrentRow["Name"] = "Bob";
            Module.Commit(false);
            Assert.Equal(2L, Convert.ToInt64(Store.Select("SELECT NextNumber FROM SYS_NUMBER_SERIES").Rows[0][0]));
            Store.ExecSql("UPDATE SYS_NUMBER_SERIES SET IsActive = 0");
            Module.Insert();
            Module.CurrentRow["Name"] = "Charlie";
            InvalidOperationException Error = Assert.Throws<InvalidOperationException>(() => Module.Commit(false));
            Assert.Contains("inactive", Error.Message);
            Assert.True(Module.CurrentRow.IsNull(FieldName));
            Assert.Equal(2L, Convert.ToInt64(Store.Select("SELECT NextNumber FROM SYS_NUMBER_SERIES").Rows[0][0]));
        }
        finally
        {
            DataRegistry.CodeProviders.Remove(Provider);
            Schema Schema = Schemas.Find("System", ConnectionName);
            if (Schema != null)
                Schemas.List.Remove(Schema);
            Db.Connections.Remove(ConnectionName);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
}
