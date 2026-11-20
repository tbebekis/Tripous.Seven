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
/// </summary>
public class DbConnectionInfo
{
    // ● properties
    /// <summary>
    /// Gets or sets the optional name of the connection.
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
}
