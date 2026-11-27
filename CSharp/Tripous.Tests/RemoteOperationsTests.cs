/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests registration and dispatch of remote operation types.
/// </summary>
[Collection("Database registry")]
public class RemoteOperationsTests
{
    // ● public types
    /// <summary>
    /// Returns its instance identifier to demonstrate request isolation.
    /// </summary>
    [RemoteOperation("Tests.RemoteOperations.Echo")]
    public class EchoOperation : RemoteOperation
    {
        readonly Guid fInstanceId = Guid.NewGuid();
        /// <summary>
        /// Returns the instance identifier and supplied value.
        /// </summary>
        public override RemoteResponse Execute(RemoteRequest Request)
        {
            RemoteResponse Response = new();
            Response["InstanceId"] = fInstanceId;
            Response["Value"] = Request.Params["Value"];
            return Response;
        }
    }
    /// <summary>
    /// An operation without an attribute.
    /// </summary>
    public class UnnamedOperation : RemoteOperation
    {
        /// <summary>
        /// Executes this operation.
        /// </summary>
        public override RemoteResponse Execute(RemoteRequest Request) => new();
    }

    /// <summary>
    /// Produces a deliberate failure without throwing.
    /// </summary>
    [RemoteOperation("Tests.RemoteOperations.DeclaredFailure")]
    public class DeclaredFailureOperation : RemoteOperation
    {
        /// <summary>
        /// Returns a failed response.
        /// </summary>
        public override RemoteResponse Execute(RemoteRequest Request) => new() { IsSuccess = false, ErrorText = "declined" };
    }
    /// <summary>
    /// Throws during operation execution.
    /// </summary>
    [RemoteOperation("Tests.RemoteOperations.Throws")]
    public class ThrowingOperation : RemoteOperation
    {
        /// <summary>
        /// Throws an operation error.
        /// </summary>
        public override RemoteResponse Execute(RemoteRequest Request) => throw new InvalidOperationException("operation failed");
    }

    // ● public
    /// <summary>
    /// Initialization registers the built-in operations once and enables Ping.
    /// </summary>
    [Fact]
    public void InitializeAndPing()
    {
        RemOp.Initialize();
        RemOp.Initialize();
        Assert.Same(typeof(Ping), RemoteOperations.Get("Ping"));
        Assert.Same(typeof(DataModuleInitialize), RemoteOperations.Get("DataModule.Initialize"));
        Assert.Same(typeof(DataModuleInsert), RemoteOperations.Get("DataModule.Insert"));
        Assert.Same(typeof(DataModuleEdit), RemoteOperations.Get("DataModule.Edit"));
        Assert.Same(typeof(DataModuleDelete), RemoteOperations.Get("DataModule.Delete"));
        Assert.Same(typeof(DataModuleSelectList), RemoteOperations.Get("DataModule.SelectList"));
        Assert.Same(typeof(DataModuleCommit), RemoteOperations.Get("DataModule.Commit"));
        RemoteResponse Response = RemoteOperations.Execute(new RemoteRequest { Id = Guid.NewGuid().ToString(), OperationName = "Ping" });
        Assert.True(Response.IsSuccess, Response.ErrorText);
        Assert.False(RemoteOperations.Execute(new RemoteRequest { OperationName = "Ping" }).IsSuccess);
    }
    /// <summary>
    /// Registration uses case-insensitive names and execution creates a fresh operation.
    /// </summary>
    [Fact]
    public void RegisterAndExecuteOperationTypes()
    {
        RemoteOperations.Register(typeof(EchoOperation));
        Assert.Same(typeof(EchoOperation), RemoteOperations.Find("tests.remoteoperations.echo"));
        Assert.Null(RemoteOperations.Find("Tests.RemoteOperations.Missing"));
        Assert.Throws<KeyNotFoundException>(() => RemoteOperations.Get("Tests.RemoteOperations.Missing"));
        Assert.Throws<InvalidOperationException>(() => RemoteOperations.Register(typeof(EchoOperation)));
        Assert.Throws<ArgumentException>(() => RemoteOperations.Register(typeof(UnnamedOperation)));
        Assert.Throws<ArgumentException>(() => RemoteOperations.Register(typeof(string)));
        RemoteOperations.Register("Tests.RemoteOperations.Alias", typeof(EchoOperation));
        RemoteOperations.Register("Tests.RemoteOperations.Unnamed", typeof(UnnamedOperation));
        Assert.Same(typeof(EchoOperation), RemoteOperations.Get("tests.remoteoperations.alias"));
        Assert.Same(typeof(UnnamedOperation), RemoteOperations.Get("tests.remoteoperations.unnamed"));
        Assert.Throws<InvalidOperationException>(() => RemoteOperations.Register("TESTS.REMOTEOPERATIONS.ALIAS", typeof(UnnamedOperation)));
        Assert.Throws<ArgumentException>(() => RemoteOperations.Register("Tests.RemoteOperations.Invalid", typeof(string)));
        RemoteRequest Request = new() { Id = Guid.NewGuid().ToString(), OperationName = "TESTS.REMOTEOPERATIONS.ECHO" };
        Request.Params["Value"] = "hello";
        RemoteResponse First = RemoteOperations.Execute(Request);
        Request.OperationName = "Tests.RemoteOperations.Alias";
        RemoteResponse Second = RemoteOperations.Execute(Request);
        Assert.Equal("hello", First["Value"]);
        Assert.NotEqual(First["InstanceId"], Second["InstanceId"]);
        Assert.True(First.IsSuccess);
        RemoteResponse MissingId = RemoteOperations.Execute(new RemoteRequest { OperationName = "Tests.RemoteOperations.Echo" });
        Assert.False(MissingId.IsSuccess);
        Assert.Contains("GUID", MissingId.ErrorText);
        RemoteResponse InvalidId = RemoteOperations.Execute(new RemoteRequest { Id = "invalid", OperationName = "Tests.RemoteOperations.Echo" });
        Assert.False(InvalidId.IsSuccess);
        Assert.Contains("GUID", InvalidId.ErrorText);
        RemoteOperations.Register(typeof(DeclaredFailureOperation));
        RemoteOperations.Register(typeof(ThrowingOperation));
        RemoteResponse Declined = RemoteOperations.Execute(new RemoteRequest { Id = Guid.NewGuid().ToString(), OperationName = "Tests.RemoteOperations.DeclaredFailure" });
        Assert.False(Declined.IsSuccess);
        Assert.Equal("declined", Declined.ErrorText);
        RemoteResponse Failed = RemoteOperations.Execute(new RemoteRequest { Id = Guid.NewGuid().ToString(), OperationName = "Tests.RemoteOperations.Throws" });
        Assert.False(Failed.IsSuccess);
        Assert.Equal("operation failed", Failed.ErrorText);
        Assert.False(RemoteOperations.Execute(new RemoteRequest { Id = Guid.NewGuid().ToString(), OperationName = "Tests.RemoteOperations.Missing" }).IsSuccess);
    }
    /// <summary>
    /// Lookup and locator operations require access to the field's module and preserve Int64 keys.
    /// </summary>
    [Fact]
    public void ResolvesLookupAndLocatorThroughAccessibleModule()
    {
        RemOp.Initialize();
        string Name = $"Reference-{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"{Name}.db");
        var Info = new DbConnectionInfo { Name = Name, ConnectionString = $"Data Source={FilePath};Pooling=False" };
        var Module = new ModuleDef { Name = Name, ConnectionName = Name, SecurityLevel = UserLevel.Admin };
        Module.Table.Name = "Orders";
        var Field = Module.Table.AddInt64("ContactId");
        Field.LookupSource = Name;
        Field.Locator = Name;
        Db.Connections.Add(Info);
        DataRegistry.Modules.Add(Module);
        LookupDef Lookup = DataRegistry.AddLookup(Name);
        LocatorDef Locator = DataRegistry.AddLocator(Name);
        try
        {
            new SqlStore(Info).ExecSql("CREATE TABLE Contacts (Id INTEGER PRIMARY KEY, Name TEXT); INSERT INTO Contacts VALUES (5000000000, 'Greece')");
            Lookup.ConnectionName = Name;
            Lookup.TableName = "Contacts";
            Locator.Source = "Contacts";
            Locator.Add("Id", DataFieldType.Int64);
            Locator.Add("Name");
            Locator.AddSearchFields("Name");
            Locator.AddResultFields("Id", "Name");

            var Request = new RemoteRequest { Id = Guid.NewGuid().ToString(), OperationName = "Locator.Execute" };
            Request.Params["ModuleName"] = Name;
            Request.Params["ReferenceField"] = Field.Name;
            Request.Params["KeyValue"] = "5000000000";
            Request.Params["Context"] = new { LocatorName = "Unrelated", Params = new { ConnectionName = "Unrelated" } };
            Assert.Contains("Access denied", RemoteOperations.Execute(Request).ErrorText);
            UserContext Context = new(new AppUser { UserLevel = UserLevel.Admin });
            Request = Json.Deserialize<RemoteRequest>(Json.Serialize(Request));
            RemoteResponse Resolved = RemoteOperations.Execute(Request, Context);
            Assert.True(Resolved.IsSuccess, Resolved.ErrorText);
            Assert.Equal("SingleResult", Resolved["Status"]);
            Assert.Equal(5000000000L, Assert.IsType<JsonDataTable>(Resolved["Table"]).Rows[0].Data[0]);
            Assert.Equal("ContactId", Assert.IsType<JsonLocatorMapPlan>(Resolved["MapPlan"]).ReferenceField);

            Request.OperationName = "Locator.GetInfo";
            Assert.Equal(Name, Assert.IsType<JsonLocatorDef>(RemoteOperations.Execute(Request, Context)["Locator"]).Name);
            Request.OperationName = "Lookup.GetList";
            RemoteResponse List = RemoteOperations.Execute(Request, Context);
            Assert.True(List.IsSuccess, List.ErrorText);
            Assert.Equal(5000000000L, Assert.IsType<JsonDataTable>(List["Table"]).Rows[0].Data[0]);
        }
        finally
        {
            DataRegistry.Modules.Remove(Module);
            DataRegistry.Lookups.Remove(Lookup);
            DataRegistry.Locators.Remove(Locator);
            Db.Connections.Remove(Name);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Fetches visible FactBox data for an authorized item and handles missing items.
    /// </summary>
    [Fact]
    public void GetsFactBoxesThroughModuleAccessCheck()
    {
        RemOp.Initialize();
        string Name = $"FactBoxes-{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"{Name}.db");
        var Info = new DbConnectionInfo { Name = Name, ConnectionString = $"Data Source={FilePath};Pooling=False" };
        var Module = new ModuleDef { Name = Name, ConnectionName = Name, SecurityLevel = UserLevel.Admin };
        Module.Table.Name = "Companies";
        Module.Table.AddInt64Id("Id");
        Module.Table.AddString("Name");
        string Provider = typeof(CompanyFactBoxProvider).AssemblyQualifiedName;
        Module.FactBoxes.Add(new ItemFactBoxDef { Name = "Summary", ProviderClassName = Provider });
        Module.FactBoxes.Add(new ItemFactBoxDef { Name = "Hidden", ProviderClassName = Provider, IsVisible = false });
        Module.FactBoxes.Add(new ItemFactBoxDef { Name = "ItemInfo", ProviderClassName = typeof(ItemInfoFactBoxProvider).FullName });
        Module.FactBoxes.Add(new ItemFactBoxDef { Name = "Structure", ProviderClassName = typeof(DataModuleStructureFactBoxProvider).FullName });
        Db.Connections.Add(Info);
        DataRegistry.Modules.Add(Module);
        try
        {
            new SqlStore(Info).ExecSql("CREATE TABLE Companies (Id INTEGER PRIMARY KEY, Name TEXT); INSERT INTO Companies VALUES (5000000000, 'Coffee')");
            var Request = new RemoteRequest { Id = Guid.NewGuid().ToString(), OperationName = "DataModule.GetFactBoxes" };
            Request.Params["ModuleName"] = Name;
            Request.Params["KeyValue"] = "5000000000";
            Assert.Contains("Access denied", RemoteOperations.Execute(Request).ErrorText);

            UserContext Context = new(new AppUser { UserLevel = UserLevel.Admin });
            Request = Json.Deserialize<RemoteRequest>(Json.Serialize(Request));
            RemoteResponse Response = RemoteOperations.Execute(Request, Context);
            Assert.True(Response.IsSuccess, Response.ErrorText);
            Assert.Equal(3, Response["FactBoxCount"]);
            var Boxes = Assert.IsType<List<JsonItemFactBox>>(Response["FactBoxes"]);
            JsonItemFactBox Summary = Assert.Single(Boxes, Box => Box.Name == "Summary");
            var ItemInfo = Assert.IsType<Dictionary<string, object>>(Assert.Single(Boxes, Box => Box.Name == "ItemInfo").Data);
            Assert.Equal(5000000000L, ItemInfo["Key"]);
            var Structure = Assert.IsType<DataModuleStructureFactBoxData>(Assert.Single(Boxes, Box => Box.Name == "Structure").Data);
            Assert.Equal(1, Structure.TableCount);
            Assert.Equal("Id", Structure.Table.KeyField);
            Assert.Equal("Int64", Assert.Single(Structure.Table.Fields, Field => Field.Name == "Id").DataType);
            var Data = Assert.IsType<Dictionary<string, object>>(Summary.Data);
            Assert.Equal(5000000000L, Data["Id"]);
            Assert.Equal("Coffee", Data["Company"]);

            Request.Params["KeyValue"] = "1";
            Assert.Contains("was not found", RemoteOperations.Execute(Request, Context).ErrorText);
            Request.Params.Remove("KeyValue");
            Response = RemoteOperations.Execute(Request, Context);
            Assert.True(Response.IsSuccess, Response.ErrorText);
            Summary = Assert.Single(Assert.IsType<List<JsonItemFactBox>>(Response["FactBoxes"]), Box => Box.Name == "Summary");
            Assert.Null(Assert.IsType<Dictionary<string, object>>(Summary.Data)["Id"]);
        }
        finally
        {
            DataRegistry.Modules.Remove(Module);
            Db.Connections.Remove(Name);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }

    // ● FactBox provider
    /// <summary>
    /// Returns the current row's company and item key.
    /// </summary>
    public class CompanyFactBoxProvider : ItemFactBoxProvider
    {
        /// <summary>
        /// Returns the key and company name, when an item is loaded.
        /// </summary>
        public override object GetData(ItemFactBoxContext Context)
            => new Dictionary<string, object> { ["Id"] = Context.KeyValue, ["Company"] = Context.Row?["Name"] };
    }
}
