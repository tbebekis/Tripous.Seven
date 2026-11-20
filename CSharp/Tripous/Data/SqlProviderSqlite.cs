/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Provides SQLite-specific database operations.
/// </summary>
public class SqlProviderSqlite : SqlProvider
{
    // ● constructor
    internal SqlProviderSqlite()
    {
    }

    // ● public
    /// <summary>
    /// Creates a SQLite connection for the specified connection string.
    /// </summary>
    public override DbConnection CreateConnection(string ConnectionString) => new SqliteConnection(ConnectionString);

    /// <summary>
    /// Creates an empty SQLite database file without changing an existing file.
    /// </summary>
    public override bool CreateDatabase(string ConnectionString)
    {
        SqliteConnectionStringBuilder Builder = new(ConnectionString);
        string FilePath = Builder.DataSource;
        if (string.IsNullOrWhiteSpace(FilePath) || FilePath == ":memory:" || Builder.Mode == SqliteOpenMode.Memory || FilePath.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A SQLite database file path is required.", nameof(ConnectionString));
        if (Builder.Mode == SqliteOpenMode.ReadOnly || Builder.Mode == SqliteOpenMode.ReadWrite)
            throw new ArgumentException("The SQLite connection mode does not allow database creation.", nameof(ConnectionString));
        string Folder = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(Folder))
            Directory.CreateDirectory(Folder);
        try
        {
            using (FileStream File = new(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                return true;
            }
        }
        catch (IOException) when (File.Exists(FilePath))
        {
            return false;
        }
    }

    // ● protected
    /// <summary>
    /// Gets SQLite mappings for the shared SQL type placeholders.
    /// </summary>
    protected override IReadOnlyList<KeyValuePair<string, string>> TypeTokens => new KeyValuePair<string, string>[]
    {
        new(SqlTypeTokens.CPRIMARY_KEY, "integer not null primary key autoincrement"),
        new(SqlTypeTokens.CAUTO_INC, "integer autoincrement"),
        new(SqlTypeTokens.CNVARCHAR, "nvarchar"),
        new(SqlTypeTokens.CVARCHAR, "varchar"),
        new(SqlTypeTokens.CFLOAT, "real"),
        new(SqlTypeTokens.CDECIMAL, "real"),
        new(SqlTypeTokens.CDATE_TIME, "datetime"),
        new(SqlTypeTokens.CDATE, "datetime"),
        new(SqlTypeTokens.CBOOL, "integer"),
        new(SqlTypeTokens.CNBLOB_TEXT, "text"),
        new(SqlTypeTokens.CBLOB_TEXT, "text"),
        new(SqlTypeTokens.CBLOB, "blob"),
        new(SqlTypeTokens.CNOT_NULL, "not null"),
        new(SqlTypeTokens.CNULL, " ")
    };

    // ● capabilities
    /// <summary>
    /// Gets whether SQLite database files can be created.
    /// </summary>
    public override bool CanCreateDatabases => true;

    // ● properties
    /// <summary>
    /// Gets SQL that finds a table by name in the SQLite schema.
    /// </summary>
    public override string TableExistsSql => "SELECT name FROM sqlite_master WHERE type = 'table' AND name = :TableName COLLATE NOCASE";
    /// <summary>
    /// Gets SQL that finds a view by name in the SQLite schema.
    /// </summary>
    public override string ViewExistsSql => "SELECT name FROM sqlite_master WHERE type = 'view' AND name = :ViewName COLLATE NOCASE";
    /// <summary>
    /// Gets the SQLite parameter prefix.
    /// </summary>
    public override string NativePrefix => ":";
}
