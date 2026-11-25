/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests the representative entry points of the separate libraries.
/// </summary>
public class LibraryInitializationTests
{
    // ● public
    /// <summary>
    /// Library initializers are repeatable and their assemblies participate in type discovery.
    /// </summary>
    [Fact]
    public void InitializeLoadsLibrariesForTypeDiscovery()
    {
        Sys.Initialize();
        Db.Initialize();
        RemOp.Initialize();
        Sys.Initialize();
        Db.Initialize();
        RemOp.Initialize();
        Assert.Equal("Tripous", typeof(Sys).Assembly.GetName().Name);
        Assert.Equal("Tripous.Data", typeof(Db).Assembly.GetName().Name);
        Assert.Equal("Tripous.RemoteOps", typeof(RemOp).Assembly.GetName().Name);
        Assert.Contains(typeof(Db).Assembly, AppAssemblies.GetApplicationAssemblies());
        Assert.Contains(typeof(RemOp).Assembly, AppAssemblies.GetApplicationAssemblies());
        Assert.Same(typeof(DataModule), TypeStore.Find(typeof(DataModule).FullName));
        Assert.Same(typeof(Ping), RemoteOperations.Get("Ping"));
    }
}
