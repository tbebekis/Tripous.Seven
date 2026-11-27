/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes a database connection and its default command timeout.
/// After changing loaded settings, the programmer is responsible for updating
/// dependent objects, including existing SQL stores and loaded schemas.
/// In particular, create new stores when changing the database server type
/// and unload or reload metadata when connection settings change.
/// </summary>
public class DbConnectionInfo
{
    // ● public
    /// <summary>
    /// Returns the adapter for editing this connection's string.
    /// </summary>
    public DbConAdapter GetConnectionStringAdapter() => DbConAdapters.Get(DbServerType);
    /// <summary>
    /// Returns the SQL provider for this connection's server type.
    /// </summary>
    public SqlProvider GetSqlProvider() => SqlProviders.GetSqlProvider(DbServerType);

    // ● properties
    /// <summary>
    /// Gets or sets the connection name, which must be supplied after deserialization.
    /// </summary>
    public string Name { get; set; } = "";
    /// <summary>
    /// Gets or sets the type of database server.
    /// </summary>
    public DbServerType DbServerType { get; set; } = DbServerType.Sqlite;
    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public string ConnectionString { get; set; } = "";
    /// <summary>
    /// Gets or sets the default command timeout in seconds.
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 30;
    /// <summary>
    /// Gets or sets the isolation level for transactions opened by a store on this connection.
    /// Unspecified inherits the application default from DbConfig.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<IsolationLevel>))]
    public IsolationLevel TransactionIsolationLevel { get; set; } = IsolationLevel.Unspecified;
    /// <summary>
    /// Gets the shared metadata schema registered under this connection's name.
    /// Unload and reload it when connection settings change.
    /// </summary>
    [JsonIgnore]
    public DbSchema Schema => DbSchemas.GetOrAdd(this);
}
