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
        Module.Stocks.Add("Country", null);
        Assert.Throws<InvalidOperationException>(() => Module.SelectList.Add("active", "select Id from Customer"));
        Module.UpdateReferences();
        Assert.Same(Module, Module.SelectList.Get("ACTIVE").Owner);

        string JsonText = Json.Serialize(Module);
        Assert.DoesNotContain("\"Owner\":", JsonText);
        ModuleDef Loaded = Json.Deserialize<ModuleDef>(JsonText);
        Assert.Equal(2, Loaded.SelectList.Count);
        Assert.Equal("select Id, Name from Customer where Active = 1", Loaded.SelectList.Get("active").SqlText);
        Assert.Same(Loaded, Loaded.SelectList.Get("active").Owner);
        Assert.Same(Loaded, Loaded.Stocks.Get("country").Owner);
        Assert.Null(Loaded.Stocks.Get("country").SqlText);
    }
}
