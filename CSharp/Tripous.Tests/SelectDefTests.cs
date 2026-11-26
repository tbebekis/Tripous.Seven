/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests named SELECT definitions.
/// </summary>
public class SelectDefTests
{
    // ● public
    /// <summary>
    /// SELECT definitions require a name; SQL may be supplied by their consumer.
    /// </summary>
    [Fact]
    public void CheckDescriptorRequiresName()
    {
        SelectDef Select = new();
        Assert.Throws<InvalidOperationException>(() => Select.CheckDescriptor());
        Select.Name = "Default";
        Select.CheckDescriptor();
        Select.SqlText = "select Id from Customer";
        Select.CheckDescriptor();
        SqlFilterDef Filter = Select.AddFilter("Active", FilterDataType: DataFieldType.Boolean, ConditionOp: ConditionOp.Between);
        Assert.Contains("requires the Equal", Assert.Throws<InvalidOperationException>(Select.CheckDescriptor).Message);
        Filter.ConditionOp = ConditionOp.Equal;
        Select.CheckDescriptor();
    }
    /// <summary>
    /// Suggested filters use SELECT column metadata and leave registered filters unchanged.
    /// </summary>
    [Fact]
    public void DefineFiltersUsesSelectSchema()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-filter-schema-{Guid.NewGuid():N}.db");
        try
        {
            SqlStore Store = new(new DbConnectionInfo { Name = "FilterSchema", ConnectionString = $"Data Source={FilePath};Pooling=False" });
            Store.ExecSql("CREATE TABLE Customer (Id TEXT, Name TEXT, City TEXT, Amount INTEGER, Remarks TEXT)");
            SelectDef Select = new() { Name = "Customer", SqlText = "SELECT Id, Name, City, Amount, Remarks FROM Customer" };
            Select.AddFilter("Existing");
            SqlFilterDefs Filters = Select.DefineFilters(Store);
            Assert.Equal(new[] { "Name", "Amount", "City" }, Filters.Select(Item => Item.Name));
            Assert.Equal(DataFieldType.String, Filters.Get("Name").FilterDataType);
            Assert.Equal(DataFieldType.Int64, Filters.Get("Amount").FilterDataType);
            Assert.Single(Select.FilterDefs);
            Assert.Equal("Existing", Select.FilterDefs[0].Name);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Boolean filter validation uses the output column name and requires integer storage.
    /// </summary>
    [Fact]
    public void ValidatesBooleanFiltersAgainstSelectOutput()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-boolean-filter-{Guid.NewGuid():N}.db");
        try
        {
            SqlStore Store = new(new DbConnectionInfo { Name = "BooleanFilterSchema", ConnectionString = $"Data Source={FilePath};Pooling=False" });
            Store.ExecSql("CREATE TABLE Customer (Active INTEGER, Name TEXT)");
            SelectDef Select = new() { Name = "Customer", SqlText = "SELECT Active AS IsActive, Name FROM Customer" };
            SqlFilterDef Filter = Select.AddFilter(new FieldDef { Name = "Enabled", DataType = DataFieldType.Integer, Flags = FieldFlags.Boolean });
            Assert.Equal(DataFieldType.Boolean, Filter.FilterDataType);
            Filter.FieldName = "IsActive";
            Select.ValidateBooleanFilterTypes(Store);
            Filter.FieldName = "Name";
            Assert.Contains("requires an integer column", Assert.Throws<InvalidOperationException>(() => Select.ValidateBooleanFilterTypes(Store)).Message);
            Filter.FieldName = "Missing";
            Assert.Contains("absent", Assert.Throws<InvalidOperationException>(() => Select.ValidateBooleanFilterTypes(Store)).Message);
        }
        finally
        {
            File.Delete(FilePath);
        }
    }
    /// <summary>
    /// A module can hold multiple named selects and reconnect their owners after JSON loading.
    /// </summary>
    [Fact]
    public void ModuleSelectsRoundTripAndReconnectOwners()
    {
        ModuleDef Module = new() { Name = "Customer" };
        Module.SelectList.Add("Default", "select * from Customer");
        Module.SelectList.Add("Active", "select Id, Name from Customer where Active = 1");
        SelectDef Active = Module.SelectList.Get("Active");
        Active.AddFilter("Name", FilterDataType: DataFieldType.String, ConditionOp: ConditionOp.Contains);
        Active.AddFilter(new FieldDef { Name = "Enabled", DataType = DataFieldType.Boolean, TitleKey = "Enabled title" });
        Module.Stocks.Add("Country", null);
        Assert.Throws<InvalidOperationException>(() => Module.SelectList.Add("active", "select Id from Customer"));
        Module.UpdateReferences();
        Assert.Same(Module, Module.SelectList.Get("ACTIVE").Owner);

        string JsonText = Json.Serialize(Module);
        Assert.DoesNotContain("\"Owner\":", JsonText);
        ModuleDef Loaded = Json.Deserialize<ModuleDef>(JsonText);
        Assert.Equal(2, Loaded.SelectList.Count);
        Assert.Equal("select Id, Name from Customer where Active = 1", Loaded.SelectList.Get("active").SqlText);
        SqlFilterDef Filter = Loaded.SelectList.Get("active").FilterDefs.Get("name");
        Assert.Equal("Name", Filter.FieldName);
        Assert.Equal(ConditionOp.Contains, Filter.ConditionOp);
        Assert.Equal(BoolOp.And, Filter.BoolOp);
        Assert.Equal(DataFieldType.Boolean, Loaded.SelectList.Get("active").FilterDefs.Get("Enabled").FilterDataType);
        Assert.Equal("Enabled title", Loaded.SelectList.Get("active").FilterDefs.Get("Enabled").TitleKey);
        Assert.Same(Loaded, Loaded.SelectList.Get("active").Owner);
        Assert.Same(Loaded, Loaded.Stocks.Get("country").Owner);
        Assert.Null(Loaded.Stocks.Get("country").SqlText);
    }
}
