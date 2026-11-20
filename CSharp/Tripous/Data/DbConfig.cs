/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Provides application-configurable conventions for the database layer.
/// </summary>
static public class DbConfig
{
    // ● properties
    /// <summary>
    /// Gets or sets the name used to find the default database connection.
    /// </summary>
    static public string DefaultConnectionName { get; set; } = "DEFAULT";
    /// <summary>
    /// Gets or sets the file name used to persist database connections.
    /// </summary>
    static public string ConnectionsFileName { get; set; } = "DbConnections.json";
    /// <summary>
    /// Gets or sets the name of the system database settings table.
    /// </summary>
    static public string SysDbIniTableName { get; set; } = "SYS_INI";
    /// <summary>
    /// Gets or sets the key field name of the system settings table.
    /// </summary>
    static public string SysDbIniEntryField { get; set; } = "EntryKey";
    /// <summary>
    /// Gets or sets the value field name of the system settings table.
    /// </summary>
    static public string SysDbIniValueField { get; set; } = "EntryValue";
    /// <summary>
    /// Gets or sets the binary value field name of the system settings table.
    /// </summary>
    static public string SysDbIniBlobField { get; set; } = "EntryData";
}
