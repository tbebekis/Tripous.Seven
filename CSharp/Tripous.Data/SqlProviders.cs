/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Provides shared instances of available SQL providers.
/// </summary>
static public class SqlProviders
{
    // ● private fields
    static readonly Lazy<SqlProviderSqlite> fSqlite = new(() => new SqlProviderSqlite());

    static readonly Lazy<SqlProviderPostgreSql> fPostgreSql = new(() => new SqlProviderPostgreSql());
    static readonly Lazy<SqlProviderFirebird> fFirebird = new(() => new SqlProviderFirebird());
    static readonly Lazy<SqlProviderMySql> fMySql = new(() => new SqlProviderMySql());

    // ● static public
    /// <summary>
    /// Returns the provider for a supported database server type.
    /// </summary>
    static public SqlProvider GetSqlProvider(DbServerType ServerType)
    {
        return ServerType switch
        {
            DbServerType.Sqlite => Sqlite,
            DbServerType.PostgreSql => PostgreSql,
            DbServerType.Firebird => Firebird,
            DbServerType.MySql => MySql,
            _ => throw new NotSupportedException($"No SQL provider is available for {ServerType}.")
        };
    }

    // ● properties
    /// <summary>
    /// Gets the shared SQLite provider.
    /// </summary>
    static public SqlProviderSqlite Sqlite => fSqlite.Value;

    /// <summary>
    /// Gets the shared PostgreSQL provider.
    /// </summary>
    static public SqlProviderPostgreSql PostgreSql => fPostgreSql.Value;
    /// <summary>
    /// Gets the shared Firebird provider.
    /// </summary>
    static public SqlProviderFirebird Firebird => fFirebird.Value;
    /// <summary>
    /// Gets the shared MySQL provider.
    /// </summary>
    static public SqlProviderMySql MySql => fMySql.Value;
}
