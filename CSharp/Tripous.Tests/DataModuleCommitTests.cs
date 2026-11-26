/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests remote data module commit operations.
/// </summary>
[Collection("Database registry")]
public class DataModuleCommitTests
{
    /// <summary>
    /// Parallel remote commits use the audit identity supplied by the host for each request.
    /// </summary>
    [Fact]
    public async Task RemoteCommitsKeepUserContextsSeparate()
    {
        string Name = $"audit-{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"{Name}.db");
        ModuleDef Definition = new() { Name = Name, ConnectionName = Name };
        Definition.Table.Name = "Customer";
        foreach (string Field in new[] { "Id", "CreatedBy", "ModifiedBy" })
            Definition.Table.Fields.Add(new FieldDef { Name = Field });

        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = Name, ConnectionString = $"Data Source={FilePath};Pooling=False" });
            DataRegistry.Modules.Add(Definition);
            SqlStore Store = SqlStores.CreateSqlStore(Name);
            Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, CreatedBy TEXT, ModifiedBy TEXT)");
            RemOp.Initialize();

            RemoteRequest CreateRequest(string Key)
            {
                DataModule Input = Definition.Create(new UserContext(new AppUser { Id = "client" }));
                Input.Insert();
                Input.ItemTable.Rows[0]["Id"] = Key;
                RemoteRequest Request = new() { Id = Guid.NewGuid().ToString(), OperationName = "DataModule.Commit" };
                Request.Params["ModuleName"] = Name;
                Request.Params["CurrentUserId"] = "client";
                Request.Params["DataModule"] = new JsonDataModule(Input);
                return Json.Deserialize<RemoteRequest>(Json.Serialize(Request));
            }

            RemoteRequest First = CreateRequest("A");
            RemoteRequest Second = CreateRequest("B");
            RemoteResponse Anonymous = RemoteOperations.Execute(CreateRequest("C"));
            Assert.False(Anonymous.IsSuccess);
            Assert.Contains("current user ID", Anonymous.ErrorText);

            RemoteResponse[] Responses = await Task.WhenAll(
                Task.Run(() => RemoteOperations.Execute(First, new UserContext(new AppUser { Id = "alice" }))),
                Task.Run(() => RemoteOperations.Execute(Second, new UserContext(new AppUser { Id = "bob" }))));
            Assert.All(Responses, Response => Assert.True(Response.IsSuccess, Response.ErrorText));
            DataTable Rows = Store.Select("SELECT CreatedBy, ModifiedBy FROM Customer ORDER BY Id");
            Assert.Equal(2, Rows.Rows.Count);
            Assert.Equal(new object[] { "alice", "alice" }, Rows.Rows[0].ItemArray);
            Assert.Equal(new object[] { "bob", "bob" }, Rows.Rows[1].ItemArray);
        }
        finally
        {
            DataRegistry.Modules.Remove(Definition);
            Db.Connections.Remove(Name);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }

    // ● public types
    /// <summary>
    /// Fails in the TableSet after the database commit.
    /// </summary>
    public class AfterCommitFailureModule : DataModule
    {
        /// <summary>
        /// Raises a test error after transaction commit.
        /// </summary>
        protected override void TableSet_TransactionStageCommit(object Sender, TransactionEventArgs Args)
        {
            if (Args.Stage == TransactionStage.Commit && Args.ExecTime == ExecTime.After)
                throw new InvalidOperationException("after transaction");
        }
    }
    /// <summary>
    /// Fails in the module hook after TableSet completes.
    /// </summary>
    public class CommittedHookFailureModule : DataModule
    {
        /// <summary>
        /// Raises a test error after successful TableSet work.
        /// </summary>
        protected override void Committed(bool Reselect, object RowId)
            => throw new InvalidOperationException("after module commit");
    }

    // ● public
    /// <summary>
    /// A JSON request inserts, updates and deletes item tree rows in one transaction.
    /// </summary>
    [Fact]
    public void RemoteCommitSavesAndReloadsItemTree()
    {
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string ModuleName = $"Module_{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-remote-commit-{Guid.NewGuid():N}.db");
        ModuleDef Definition = new() { Name = ModuleName, ConnectionName = ConnectionName };
        Definition.Table.Name = "Customer";
        Definition.Table.Fields.Add(new FieldDef { Name = "Id" });
        Definition.Table.Fields.Add(new FieldDef { Name = "Name" });
        Definition.Table.Fields.Add(new FieldDef { Name = "NameLength", DataType = DataFieldType.Integer, Expression = "LEN(Name)" });
        TableDef Address = new() { Name = "Address", MasterField = "Id", DetailField = "CustomerId" };
        Address.Fields.Add(new FieldDef { Name = "Id" });
        Address.Fields.Add(new FieldDef { Name = "CustomerId" });
        Address.Fields.Add(new FieldDef { Name = "City" });
        TableDef Note = new() { Name = "AddressNote", MasterField = "Id", DetailField = "AddressId" };
        Note.Fields.Add(new FieldDef { Name = "Id" });
        Note.Fields.Add(new FieldDef { Name = "AddressId" });
        Note.Fields.Add(new FieldDef { Name = "Text" });
        Address.Details.Add(Note);
        Definition.Table.Details.Add(Address);
        try
        {
            Db.Connections.Add(new DbConnectionInfo { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False;Foreign Keys=True" });
            DataRegistry.Modules.Add(Definition);
            SqlStore Store = SqlStores.CreateSqlStore(ConnectionName);
            Store.ExecSql("CREATE TABLE Customer (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)");
            Store.ExecSql("CREATE TABLE Address (Id TEXT PRIMARY KEY, CustomerId TEXT NOT NULL REFERENCES Customer(Id), City TEXT NOT NULL)");
            Store.ExecSql("CREATE TABLE AddressNote (Id TEXT PRIMARY KEY, AddressId TEXT NOT NULL REFERENCES Address(Id), Text TEXT NOT NULL)");
            RemOp.Initialize();

            DataModule Input = Definition.Create();
            Input.Insert();
            Input.ItemTable.Rows[0]["Name"] = "Theo";
            string CustomerId = (string)Input.ItemTable.Rows[0]["Id"];
            MemTable Addresses = Input.Tables.Get("Address");
            MemTable Notes = Input.Tables.Get("AddressNote");
            DataRow FirstAddress = Addresses.AddNewRow();
            FirstAddress["City"] = "Athens";
            string FirstAddressId = (string)FirstAddress["Id"];
            DataRow FirstNote = Notes.AddNewRow();
            FirstNote["Text"] = "First note";
            DataRow SecondAddress = Addresses.AddNewRow();
            SecondAddress["City"] = "Patras";
            DataRow SecondNote = Notes.AddNewRow();
            SecondNote["Text"] = "Second note";
            RemoteRequest Request = new() { Id = Guid.NewGuid().ToString(), OperationName = "DataModule.Commit" };
            Request.Params["ModuleName"] = ModuleName;
            JsonDataModule InputPacket = new(Input);
            JsonDataTable InputItem = InputPacket.Tables.Single(Table => Table.Name == "Customer");
            int ExpressionIndex = InputItem.Columns.FindIndex(Column => Column.Name == "NameLength");
            Assert.Equal(4, InputItem.Rows[0].Data[ExpressionIndex]);
            InputItem.Rows[0].Data[ExpressionIndex] = "ignored client value";
            Request.Params["DataModule"] = InputPacket;
            Request = Json.Deserialize<RemoteRequest>(Json.Serialize(Request));
            RemoteResponse InsertResult = RemoteOperations.Execute(Request);
            Assert.True(InsertResult.IsSuccess, InsertResult.ErrorText);
            Assert.Equal("Success", InsertResult["TableSetResultStatus"]);
            Assert.Equal(CustomerId, InsertResult["Id"]);
            JsonDataModule Inserted = Assert.IsType<JsonDataModule>(InsertResult["DataModule"]);
            Assert.Equal((int)DataMode.Edit, Inserted.State);
            JsonDataTable InsertedItem = Inserted.Tables.Single(Table => Table.Name == "Customer");
            Assert.Equal("LEN(Name)", InsertedItem.Columns[ExpressionIndex].Expression);
            Assert.Equal(4, InsertedItem.Rows[0].Data[ExpressionIndex]);
            Assert.Equal(2L, Store.Select("SELECT COUNT(*) FROM AddressNote").Rows[0][0]);

            DataModule Edited = Definition.Create();
            Assert.True(Edited.Edit(CustomerId));
            Edited.ItemTable.Rows[0]["Name"] = "Theo updated";
            MemTable EditedAddresses = Edited.Tables.Get("Address");
            MemTable EditedNotes = Edited.Tables.Get("AddressNote");
            DataRow AddressToDelete = EditedAddresses.Rows.Cast<DataRow>().Single(Row => (string)Row["Id"] == FirstAddressId);
            DataRow NoteToDelete = EditedNotes.Rows.Cast<DataRow>().Single(Row => (string)Row["AddressId"] == FirstAddressId);
            NoteToDelete.Delete();
            AddressToDelete.Delete();
            Request.Params["DataModule"] = new JsonDataModule(Edited);
            Request = Json.Deserialize<RemoteRequest>(Json.Serialize(Request));
            RemoteResponse EditResult = RemoteOperations.Execute(Request);
            Assert.True(EditResult.IsSuccess, EditResult.ErrorText);
            Assert.Equal("Theo updated", Store.Select("SELECT Name FROM Customer").Rows[0][0]);
            Assert.Equal(1L, Store.Select("SELECT COUNT(*) FROM Address").Rows[0][0]);
            Assert.Equal(1L, Store.Select("SELECT COUNT(*) FROM AddressNote").Rows[0][0]);

            DataModule Invalid = Definition.Create();
            Assert.True(Invalid.Edit(CustomerId));
            Invalid.ItemTable.Rows[0]["Name"] = "Must roll back";
            Invalid.Tables.Get("Address").AddNewRow();
            Request.Params["DataModule"] = new JsonDataModule(Invalid);
            Request = Json.Deserialize<RemoteRequest>(Json.Serialize(Request));
            RemoteResponse Failed = RemoteOperations.Execute(Request);
            Assert.False(Failed.IsSuccess);
            Assert.Equal("CommitFailed", Failed["TableSetResultStatus"]);
            Assert.Null(Failed["Id"]);
            Assert.Equal("Theo updated", Store.Select("SELECT Name FROM Customer").Rows[0][0]);
            Assert.Equal(1L, Store.Select("SELECT COUNT(*) FROM Address").Rows[0][0]);

            DataModule AllDeleted = Definition.Create();
            Assert.True(AllDeleted.Edit(CustomerId));
            AllDeleted.Tables.Get("AddressNote").Rows[0].Delete();
            AllDeleted.Tables.Get("Address").Rows[0].Delete();
            Request.Params["DataModule"] = new JsonDataModule(AllDeleted);
            Request = Json.Deserialize<RemoteRequest>(Json.Serialize(Request));
            RemoteResponse DeletedTree = RemoteOperations.Execute(Request);
            Assert.True(DeletedTree.IsSuccess, DeletedTree.ErrorText);
            Assert.Equal(0L, Store.Select("SELECT COUNT(*) FROM Address").Rows[0][0]);
            Assert.Equal(0L, Store.Select("SELECT COUNT(*) FROM AddressNote").Rows[0][0]);

            Request.Params.Remove("DataModule");
            RemoteResponse MissingPacket = RemoteOperations.Execute(Request);
            Assert.False(MissingPacket.IsSuccess);
            Assert.True(MissingPacket.ContainsKey("TableSetResultStatus"));
            Assert.Null(MissingPacket["TableSetResultStatus"]);
            Assert.True(MissingPacket.ToJsonObject().ContainsKey("TableSetResultStatus"));
            Assert.Null(MissingPacket.ToJsonObject()["TableSetResultStatus"]);

            TypeStore.Register(typeof(AfterCommitFailureModule));
            Definition.ClassName = typeof(AfterCommitFailureModule).FullName;
            DataModule AfterCommit = Definition.Create();
            AfterCommit.Insert();
            AfterCommit.ItemTable.Rows[0]["Name"] = "Saved before error";
            object AfterCommitId = AfterCommit.ItemTable.Rows[0]["Id"];
            Request.Params["DataModule"] = new JsonDataModule(AfterCommit);
            RemoteResponse AfterError = RemoteOperations.Execute(Request);
            Assert.False(AfterError.IsSuccess);
            Assert.Equal("AfterCommitFailed", AfterError["TableSetResultStatus"]);
            Assert.Equal("AfterCommitFailed", AfterError.ToJsonObject()["TableSetResultStatus"].GetValue<string>());
            Assert.Equal(AfterCommitId, AfterError["Id"]);
            Assert.Equal("after transaction", AfterError.ErrorText);

            TypeStore.Register(typeof(CommittedHookFailureModule));
            Definition.ClassName = typeof(CommittedHookFailureModule).FullName;
            DataModule HookFailure = Definition.Create();
            HookFailure.Insert();
            HookFailure.ItemTable.Rows[0]["Name"] = "Saved before hook error";
            object HookId = HookFailure.ItemTable.Rows[0]["Id"];
            Request.Params["DataModule"] = new JsonDataModule(HookFailure);
            RemoteResponse HookError = RemoteOperations.Execute(Request);
            Assert.False(HookError.IsSuccess);
            Assert.Equal("Success", HookError["TableSetResultStatus"]);
            Assert.Equal(HookId, HookError["Id"]);
            Assert.Equal("after module commit", HookError.ErrorText);
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
