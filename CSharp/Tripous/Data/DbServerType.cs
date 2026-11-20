/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Indicates the type of the database server.
/// </summary>
public enum DbServerType
{
    /// <summary>
    /// SQLite.
    /// </summary>
    Sqlite,
    /// <summary>
    /// Microsoft SQL Server.
    /// </summary>
    MsSql,
    /// <summary>
    /// MySQL.
    /// </summary>
    MySql,
    /// <summary>
    /// PostgreSQL.
    /// </summary>
    PostgreSql,
    /// <summary>
    /// Firebird.
    /// </summary>
    Firebird,
    /// <summary>
    /// Oracle.
    /// </summary>
    Oracle,
}
