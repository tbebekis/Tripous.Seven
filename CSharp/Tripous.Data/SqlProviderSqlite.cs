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
    /// <summary>
    /// Maps a SQLite column declaration to an item field type.
    /// SQLite integers can hold signed 64-bit values.
    /// </summary>
    internal override DataFieldType GetMetadataFieldType(DbMetaColumn Column)
    {
        string Declaration = Column.DataType.Split('(', 2)[0].Trim().ToUpperInvariant();
        if (Declaration.Contains("INT"))
            return DataFieldType.Int64;
        if (Declaration.Contains("CHAR") || Declaration.Contains("CLOB") || Declaration.Contains("TEXT"))
            return DataFieldType.String;
        if (Declaration.Length == 0)
            throw new NotSupportedException($"Column '{Column.Name}' has no declared SQLite type. Specify its FieldDef explicitly.");
        if (Declaration.Contains("BLOB"))
            return DataFieldType.Blob;
        if (Declaration.Contains("REAL") || Declaration.Contains("FLOA") || Declaration.Contains("DOUB"))
            return DataFieldType.Double;
        return Declaration switch
        {
            "DATE" => DataFieldType.Date,
            "DATETIME" or "TIMESTAMP" => DataFieldType.DateTime,
            "BOOL" or "BOOLEAN" => DataFieldType.Boolean,
            _ => DataFieldType.Decimal,
        };
    }

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
    /// Applies the framework's limit to a SQLite list SELECT.
    /// List SELECTs must not contain their own top-level LIMIT clause.
    /// </summary>
    public override string ApplyRowLimit(string SqlText, int RowLimit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(SqlText);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(RowLimit);
        int Index = 0;
        int Depth = 0;
        int LastCodeEnd = 0;
        while (Index < SqlText.Length)
        {
            int Start = Index;
            bool Comment = SqlText[Index] == '-' && Index + 1 < SqlText.Length && SqlText[Index + 1] == '-'
                || SqlText[Index] == '/' && Index + 1 < SqlText.Length && SqlText[Index + 1] == '*';
            if (SqlTextScanner.SkipProtectedRegion(SqlText, ref Index))
            {
                if (!Comment)
                    LastCodeEnd = Index;
                continue;
            }
            char Ch = SqlText[Index];
            if (Ch == '(')
                Depth++;
            else if (Ch == ')')
                Depth--;
            if (char.IsLetter(Ch) || Ch == '_')
            {
                Index++;
                while (Index < SqlText.Length && (char.IsLetterOrDigit(SqlText[Index]) || SqlText[Index] == '_' || SqlText[Index] == '$'))
                    Index++;
                bool Parameter = Start > 0 && (SqlText[Start - 1] == ':' || SqlText[Start - 1] == '@' || SqlText[Start - 1] == '$');
                if (Depth == 0 && !Parameter && SqlText.AsSpan(Start, Index - Start).Equals("limit", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("List SELECTs must not specify a top-level LIMIT; Tripous applies the row limit.");
                LastCodeEnd = Index;
                continue;
            }
            Index++;
            if (!char.IsWhiteSpace(Ch))
                LastCodeEnd = Index;
        }
        if (LastCodeEnd > 0 && SqlText[LastCodeEnd - 1] == ';')
            LastCodeEnd--;
        return SqlText.Insert(LastCodeEnd, $" LIMIT {RowLimit}");
    }

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

    /// <summary>
    /// Returns SQL for the last row ID inserted on the current SQLite connection.
    /// </summary>
    public override string LastIdSql(string TableName) => "SELECT last_insert_rowid()";

    // ● capabilities
    /// <summary>
    /// SQLite generates integer identifiers during insertion.
    /// </summary>
    public override OidMode OidMode => OidMode.AutoInc;

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
        new(SqlTypeTokens.CINT64, "integer"),
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
