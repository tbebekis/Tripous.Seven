/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests named list SELECT definitions.
/// </summary>
public class SelectDefTests
{
    // ● public
    /// <summary>
    /// SELECT definitions require a name and a non-empty SQL statement.
    /// </summary>
    [Fact]
    public void CheckDescriptorRequiresNameAndSql()
    {
        SelectDef Select = new();
        Assert.Throws<InvalidOperationException>(() => Select.CheckDescriptor());
        Select.Name = "Default";
        Assert.Throws<InvalidOperationException>(() => Select.CheckDescriptor());
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
        Assert.Throws<InvalidOperationException>(() => Module.SelectList.Add("active", "select Id from Customer"));
        Module.UpdateReferences();
        Assert.Same(Module, Module.SelectList.Get("ACTIVE").Owner);

        string JsonText = Json.Serialize(Module);
        Assert.DoesNotContain("\"Owner\":", JsonText);
        ModuleDef Loaded = Json.Deserialize<ModuleDef>(JsonText);
        Assert.Equal(2, Loaded.SelectList.Count);
        Assert.Equal("select Id, Name from Customer where Active = 1", Loaded.SelectList.Get("active").SqlText);
        Assert.Same(Loaded, Loaded.SelectList.Get("active").Owner);
    }
}
