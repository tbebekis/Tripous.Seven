/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Holds the registered definitions of the data layer.
/// </summary>
static public class DataRegistry
{
    // ● static public
    /// <summary>
    /// Creates an initialized data module from its registered name.
    /// </summary>
    static public DataModule CreateModule(string Name) => Modules.Get(Name).Create();
    /// <summary>
    /// Creates a module for the user context supplied by the host.
    /// </summary>
    static public DataModule CreateModule(string Name, UserContext Context) => Modules.Get(Name).Create(Context);

    // ● properties
    /// <summary>
    /// Gets the named data module definitions.
    /// </summary>
    static public DefList<ModuleDef> Modules { get; } = new();
    /// <summary>
    /// Gets the named code provider definitions.
    /// </summary>
    static public DefList<CodeProviderDef> CodeProviders { get; } = new();
}
