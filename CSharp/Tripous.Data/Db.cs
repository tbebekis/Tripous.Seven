/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Provides central access to database connections.
/// </summary>
static public class Db
{
    // ● static public
    /// <summary>
    /// Ensures the data library is loaded for application type discovery.
    /// </summary>
    static public void Initialize() { }
    /// <summary>
    /// Returns a named connection or throws if it is not registered.
    /// </summary>
    static public DbConnectionInfo GetConnectionInfo(string Name) => Connections.Get(Name);
    /// <summary>
    /// Returns the configured default connection or throws if it is not registered.
    /// </summary>
    static public DbConnectionInfo GetDefaultConnectionInfo() => Connections.Get(DbConfig.DefaultConnectionName);

    // ● properties
    /// <summary>
    /// Gets the collection of database connections.
    /// </summary>
    static public DbConnections Connections { get; } = new();
}
