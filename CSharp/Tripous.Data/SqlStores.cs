/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Creates SQL stores for configured database connections.
/// </summary>
static public class SqlStores
{
    // ● static public
    /// <summary>
    /// Creates a store for the configured default connection.
    /// </summary>
    static public SqlStore CreateDefaultSqlStore() => CreateSqlStore(Db.GetDefaultConnectionInfo());
    /// <summary>
    /// Creates a store for the specified connection settings.
    /// </summary>
    static public SqlStore CreateSqlStore(DbConnectionInfo ConnectionInfo) => new SqlStore(ConnectionInfo);
    /// <summary>
    /// Creates a store for a named connection.
    /// </summary>
    static public SqlStore CreateSqlStore(string ConnectionName) => CreateSqlStore(Db.GetConnectionInfo(ConnectionName));
}
