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
}
