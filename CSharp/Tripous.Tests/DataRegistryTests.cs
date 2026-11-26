/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests registration and retrieval of data module definitions.
/// </summary>
[Collection("Database registry")]
public class DataRegistryTests
{
    /// <summary>
    /// Records whether the module factory reached construction.
    /// </summary>
    public class AccessCheckedModule : DataModule
    {
        /// <summary>
        /// Records a construction attempt for the access test.
        /// </summary>
        public AccessCheckedModule() => ConstructionCount++;

        /// <summary>
        /// Gets or sets the construction count recorded by the test.
        /// </summary>
        public static int ConstructionCount { get; set; }
    }

    /// <summary>
    /// Rejects unauthorized local and remote requests before constructing the module.
    /// </summary>
    [Fact]
    public void AccessCheckRunsBeforeModuleConstruction()
    {
        string Name = $"Restricted_{Guid.NewGuid():N}";
        ModuleDef Definition = new()
        {
            Name = Name,
            ClassName = typeof(AccessCheckedModule).AssemblyQualifiedName,
            SecurityLevel = UserLevel.User
        };
        AccessCheckedModule.ConstructionCount = 0;
        DataRegistry.Modules.Add(Definition);
        try
        {
            UserContext Guest = new(new AppUser { UserLevel = UserLevel.Guest });
            UnauthorizedAccessException Error = Assert.Throws<UnauthorizedAccessException>(() => DataRegistry.CreateModule(Name, Guest));
            Assert.Throws<UnauthorizedAccessException>(() => new DataModule().Initialize(Definition));
            RemOp.Initialize();
            RemoteRequest Request = new() { Id = Guid.NewGuid().ToString(), OperationName = "DataModule.Initialize" };
            Request.Params["ModuleName"] = Name;
            RemoteResponse Response = RemoteOperations.Execute(Request, Guest);
            Assert.False(Response.IsSuccess);
            Assert.Equal(Error.Message, Response.ErrorText);
            Assert.Equal(0, AccessCheckedModule.ConstructionCount);
            Assert.True(Definition.CanAccess(new AppUser { UserLevel = UserLevel.Admin }));
            Assert.False(Definition.CanAccess(new AppUser { UserLevel = UserLevel.Service }));
            Assert.False(Definition.CanAccess(null));
        }
        finally
        {
            DataRegistry.Modules.Remove(Definition);
        }
    }

    // ● public
    /// <summary>
    /// Module definitions are found by name without distinguishing case.
    /// </summary>
    [Fact]
    public void ModulesAreRegisteredByUniqueName()
    {
        string Name = $"Module_{Guid.NewGuid():N}";
        ModuleDef Module = new() { Name = Name };
        try
        {
            DataRegistry.Modules.Add(Module);
            Assert.Same(Module, DataRegistry.Modules.Find(Name.ToUpperInvariant()));
            Assert.Same(Module, DataRegistry.Modules.Get(Name));
            Assert.Throws<InvalidOperationException>(() => DataRegistry.Modules.Add(new ModuleDef { Name = Name.ToLowerInvariant() }));
        }
        finally
        {
            DataRegistry.Modules.Remove(Module);
        }
    }

    /// <summary>
    /// Updating a registered module preserves its table and filter definitions.
    /// </summary>
    [Fact]
    public void AddOrUpdateModulePreservesRegisteredDescriptors()
    {
        string Name = $"Module_{Guid.NewGuid():N}";
        ModuleDef Module = DataRegistry.AddModule(Name, SecurityLevel: UserLevel.User);
        try
        {
            TableDef Table = Module.Table;
            Table.AddId();
            SelectDef Select = Module.SelectList.Get("DEFAULT");
            SqlFilterDef Filter = Select.AddFilter("Name");
            Assert.Equal(Name, Table.Name);
            Assert.Equal($"select * from {Name}", Select.SqlText);
            Assert.Same(Module, Select.Owner);
            Assert.Same(Module, DataRegistry.AddOrUpdateModule(Name.ToUpperInvariant(), ListSelectSql: $"select Id, Name from {Name}", IsSingleSelect: true));
            Assert.Same(Table, Module.Table);
            Assert.Same(Filter, Module.SelectList.Get("DEFAULT").FilterDefs.Get("Name"));
            Assert.Equal($"select Id, Name from {Name}", Select.SqlText);
            Assert.True(Module.IsSingleSelect);
            Assert.Equal(UserLevel.User, Module.SecurityLevel);
            DataRegistry.AddOrUpdateModule(Name, SecurityLevel: UserLevel.None);
            Assert.Equal(UserLevel.None, Module.SecurityLevel);
        }
        finally
        {
            DataRegistry.Modules.Remove(Module);
        }
    }

    /// <summary>
    /// Creates and edits lookup records with matching schema and module definitions.
    /// </summary>
    [Theory]
    [InlineData(DataFieldType.String, false, false, false)]
    [InlineData(DataFieldType.Integer, true, false, false)]
    [InlineData(DataFieldType.Int64, true, true, false)]
    [InlineData(DataFieldType.Int64, true, true, true)]
    public void LookupModuleUsesTheMatchingSchema(DataFieldType KeyType, bool WithCode, bool WithIsActive, bool ExplicitKeyType)
    {
        string Name = $"Lookup_{Guid.NewGuid():N}";
        string ConnectionName = $"Connection_{Guid.NewGuid():N}";
        string FilePath = Path.Combine(Path.GetTempPath(), $"{Name}.db");
        DataFieldType Previous = DbConfig.DefaultPrimaryKeyType;
        try
        {
            DbConfig.DefaultPrimaryKeyType = ExplicitKeyType ? DataFieldType.String : KeyType;
            DataFieldType? Override = ExplicitKeyType ? KeyType : null;
            DbConnectionInfo Connection = new() { Name = ConnectionName, ConnectionString = $"Data Source={FilePath};Pooling=False" };
            Db.Connections.Add(Connection);
            SqlStore Store = new(Connection);
            SchemaVersion Version = new() { Version = 1 };
            ModuleDef Definition;
            if (WithIsActive)
            {
                Version.AddLookupWithCodeAndIsActive("Country", KeyType: Override);
                Definition = DataRegistry.AddLookupListModuleWithCodeAndIsActive("Country", Name, KeyType: Override);
            }
            else if (WithCode)
            {
                Version.AddLookupWithCode("Country", KeyType: Override);
                Definition = DataRegistry.AddLookupListModuleWithCode("Country", Name, KeyType: Override);
            }
            else
            {
                Version.AddLookup("Country", KeyType: Override);
                Definition = DataRegistry.AddLookupListModule("Country", Name, KeyType: Override);
            }

            if (ExplicitKeyType)
            {
                Assert.Throws<InvalidOperationException>(() =>
                    DataRegistry.AddOrUpdateLookupListModule("OtherCountry", Name, KeyType: DataFieldType.String));
                Assert.Equal("Country", Definition.Table.Name);
                Assert.Same(Definition, DataRegistry.AddOrUpdateLookupListModule("Country", Name));
            }

            Store.ExecSql(Assert.Single(Version.Tables).SqlText);
            Definition.ConnectionName = ConnectionName;
            Assert.Equal(KeyType, Definition.Table.Fields.Get("Id").DataType);
            Assert.True(Definition.IsSingleSelect);
            Assert.False(Definition.UseFilters);
            DataModule Module = Definition.Create();
            bool UsesStringKey = KeyType == DataFieldType.String;
            Assert.Equal(UsesStringKey, Definition.GuidOids);
            Assert.Equal(UsesStringKey, Json.Deserialize<JsonDataModule>(Json.Serialize(Module.JsonInitialize())).GuidOids);
            Module.Insert();
            Module.CurrentRow["Name"] = "Greece";
            if (WithCode)
                Module.CurrentRow["Code"] = "GR";
            if (WithIsActive)
                Assert.Equal(1, Module.CurrentRow["IsActive"]);
            Assert.NotNull(Module.Commit(Reselect: false));
            Assert.Equal(1, Module.ListSelect(Definition.SelectList.Get("DEFAULT")));
            Assert.Equal("Greece", Module.ListTable.Rows[0]["Name"]);
        }
        finally
        {
            DbConfig.DefaultPrimaryKeyType = Previous;
            ModuleDef Registered = DataRegistry.Modules.Find(Name);
            if (Registered != null)
                DataRegistry.Modules.Remove(Registered);
            Db.Connections.Remove(ConnectionName);
            File.Delete(FilePath);
        }
    }
}
