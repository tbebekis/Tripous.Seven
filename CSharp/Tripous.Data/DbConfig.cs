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
    /// Gets or sets whether SQL statements are sent to the information log.
    /// Parameter values are included only when Sys.DebugMode is enabled.
    /// </summary>
    static public bool LogSqlStatements { get; set; }
    /// <summary>
    /// Gets or sets the global maximum number of rows in a data module list.
    /// </summary>
    static public int DefaultRowLimit { get; set; } = 400;
    /// <summary>
    /// Gets or sets the default type of a primary key when no type is specified.
    /// </summary>
    static public DataFieldType DefaultPrimaryKeyType { get; set; } = DataFieldType.String;
    /// <summary>
    /// Gets whether the default primary key type is a GUID-like string.
    /// </summary>
    static public bool GuidOids => DefaultPrimaryKeyType == DataFieldType.String;
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
    /// Gets or sets the system number series table name. Configure it before registering the system schema.
    /// </summary>
    static public string SysNumberSeriesTableName { get; set; } = "SYS_NUMBER_SERIES";
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

    /// <summary>
    /// Gets or sets the maximum length of automatically generated ASCII identifiers.
    /// </summary>
    static public int GeneratedIdentifierMaxLength { get; set; } = 63;
    /// <summary>
    /// Gets or sets the target identifier limit in bytes for PostgreSQL.
    /// The standard server limit is 63 bytes and may vary by server build.
    /// </summary>
    static public int PostgreSqlIdentifierMaxBytes { get; set; } = 63;
    /// <summary>
    /// Gets or sets the target identifier limit in characters for MySQL tables,
    /// columns, indexes and constraints.
    /// </summary>
    static public int MySqlIdentifierMaxCharacters { get; set; } = 64;
    /// <summary>
    /// Gets or sets the target identifier limit in bytes for most Oracle schema objects.
    /// This assumes COMPATIBLE is set to 12.2 or later.
    /// </summary>
    static public int OracleIdentifierMaxBytes { get; set; } = 128;
    /// <summary>
    /// Gets or sets the target identifier limit in characters for Firebird 4 or later.
    /// The server can also impose a separate byte limit or a lower configured limit.
    /// </summary>
    static public int FirebirdIdentifierMaxCharacters { get; set; } = 63;
    /// <summary>
    /// Gets or sets the target identifier limit in characters for SQL Server schema objects.
    /// </summary>
    static public int MsSqlIdentifierMaxCharacters { get; set; } = 128;
}
