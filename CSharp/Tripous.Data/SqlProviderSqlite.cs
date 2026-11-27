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

    // ● locking
    /// <summary>
    /// Returns a SELECT statement for reading a row in the caller's write transaction.
    /// SQLite serializes writers at the database level.
    /// </summary>
    public override string SelectForUpdateSql(string TableName, string FieldName)
    {
        DataTableDef.ValidateIdentifier(TableName);
        DataTableDef.ValidateIdentifier(FieldName);
        return $"SELECT * FROM {TableName} WHERE {FieldName} = {GlobalPrefix}{FieldName}";
    }

    // ● constructor
    internal SqlProviderSqlite()
    {
    }

    // ● public
    /// <summary>
    /// Tests a SQLite connection without enabling creation of a missing database file.
    /// Memory mode and the existing access mode remain available.
    /// </summary>
    protected override string GetConnectionStringForCheck(string ConnectionString)
    {
        SqliteConnectionStringBuilder Builder = new(ConnectionString);
        if (Builder.Mode == SqliteOpenMode.ReadWriteCreate)
            Builder.Mode = SqliteOpenMode.ReadWrite;
        return Builder.ConnectionString;
    }

    /// <summary>
    /// Builds a SQLite connection string from a file path or another SQLite data source.
    /// The optional password is passed to the SQLite driver.
    /// </summary>
    public override string CreateConnectionString(string Server, string Database, string UserName, string Password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Database);
        SqliteConnectionStringBuilder Builder = new() { DataSource = Database };
        if (Password != null)
            Builder.Password = Password;
        return Builder.ConnectionString;
    }

    /// <summary>
    /// Resolves a SQLite file path while retaining connection options such as Pooling and Mode.
    /// AppPath and Data resolve to the Seven application folder. BackUp is its BackUp subfolder.
    /// Relative paths follow the driver's DataDirectory setting when configured.
    /// In-memory, temporary and URI data sources are passed through to the driver.
    /// </summary>
    public override string NormalizeConnectionString(string ConnectionString)
    {
        SqliteConnectionStringBuilder Builder = new(ConnectionString);
        if (HasFilePath(Builder))
        {
            string AppFolder = SysConfig.AppFolderPath;
            string FilePath = Builder.DataSource;
            FilePath = FilePath.Replace("[BackUp]", Path.Combine(AppFolder, "BackUp"), StringComparison.Ordinal)
                .Replace("[AppPath]", AppFolder, StringComparison.Ordinal)
                .Replace("[Data]", AppFolder, StringComparison.Ordinal);
            string DataDirectory = AppDomain.CurrentDomain.GetData("DataDirectory") as string;
            if (!string.IsNullOrEmpty(DataDirectory))
            {
                const string DataDirectoryPrefix = "|DataDirectory|";
                if (FilePath.StartsWith(DataDirectoryPrefix, StringComparison.OrdinalIgnoreCase))
                    FilePath = Path.Combine(DataDirectory, FilePath[DataDirectoryPrefix.Length..]);
                else if (!Path.IsPathRooted(FilePath))
                    FilePath = Path.Combine(DataDirectory, FilePath);
            }
            Builder.DataSource = Path.GetFullPath(FilePath);
        }
        return Builder.ConnectionString;
    }

    /// <summary>
    /// Gets the SQLite data source declared in the connection string.
    /// Path placeholders are preserved.
    /// </summary>
    public override string GetDatabaseName(string ConnectionString)
    {
        string DataSource = ((SqliteConnectionStringBuilder)CreateConnectionStringBuilder(ConnectionString)).DataSource;
        return string.IsNullOrWhiteSpace(DataSource) ? string.Empty : DataSource;
    }

    /// <summary>
    /// Checks whether a SQLite file data source exists without opening the connection.
    /// </summary>
    public override bool DatabaseExists(string ConnectionString)
    {
        SqliteConnectionStringBuilder Builder = new(NormalizeConnectionString(ConnectionString));
        return HasFilePath(Builder) && File.Exists(Builder.DataSource);
    }

    /// <summary>
    /// Returns whether the data source is a file path supported by file operations.
    /// </summary>
    static bool HasFilePath(SqliteConnectionStringBuilder Builder)
        => !string.IsNullOrWhiteSpace(Builder.DataSource) && Builder.Mode != SqliteOpenMode.Memory
            && !string.Equals(Builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase)
            && !Builder.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Joins SQL expressions with SQLite's string concatenation operator.
    /// </summary>
    public override string Concat(params string[] Parts) => string.Join(" || ", Parts);

    /// <summary>
    /// Gets the SQLite expression for the current UTC timestamp.
    /// </summary>
    public override string ServerDateTimeSql => "CURRENT_TIMESTAMP";

    /// <summary>
    /// Applies the framework's limit to a SQLite list SELECT.
    /// List SELECTs must not contain their own top-level LIMIT clause.
    /// </summary>
    public override string ApplyRowLimit(string SqlText, int RowLimit)
        => SqlTextScanner.ApplyLimit(SqlText, RowLimit);

    /// <summary>
    /// Creates an empty SQLite database file without changing an existing file.
    /// </summary>
    public override bool CreateDatabase(string ConnectionString)
    {
        SqliteConnectionStringBuilder Builder = new(NormalizeConnectionString(ConnectionString));
        string FilePath = Builder.DataSource;
        if (!HasFilePath(Builder))
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
    /// Sets the counter of an AUTOINCREMENT table in the main database.
    /// The table must already have a row in sqlite_sequence, which SQLite creates on the first insert.
    /// The next generated ID also depends on the IDs still present in the table.
    /// </summary>
    public override void SetTableGeneratorTo(string ConnectionString, string TableName, long Value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(TableName);
        ArgumentOutOfRangeException.ThrowIfNegative(Value);
        SqlStore Store = new(new DbConnectionInfo { ConnectionString = ConnectionString, DbServerType = ServerType });
        using (SqlTransactionContext Context = Store.BeginTransactionContext())
        {
            SqlParams Params = new SqlParams().Add("Value", Value).Add("TableName", TableName);
            if (!Store.TableExists(Context.Transaction, "sqlite_sequence")
                || Store.ExecSql(Context.Transaction, $"UPDATE main.sqlite_sequence SET seq = {GlobalPrefix}Value WHERE name = {GlobalPrefix}TableName COLLATE NOCASE", Params) != 1)
                throw new InvalidOperationException($"Expected one AUTOINCREMENT counter for table '{TableName}'.");

            Context.Commit();
        }
    }

    /// <summary>
    /// Returns SQL for the last row ID inserted on the current SQLite connection.
    /// </summary>
    public override string LastIdSql(string TableName) => "SELECT last_insert_rowid()";

    // ● table schema changes
    /// <summary>
    /// Builds an ALTER TABLE statement that renames an existing column.
    /// Each argument is a single identifier, quoted so names can contain spaces or quotes.
    /// </summary>
    public override string RenameColumnSql(string TableName, string ColumnName, string NewColumnName)
        => $"ALTER TABLE {QuoteName(TableName)} RENAME COLUMN {QuoteName(ColumnName)} TO {QuoteName(NewColumnName)}";

    /// <summary>
    /// Builds SQL to add a SQLite column using a portable field definition.
    /// Primary keys and unique constraints require a different table change.
    /// </summary>
    public override string AddColumnSql(string TableName, DataFieldDef Field)
    {
        ArgumentNullException.ThrowIfNull(Field);
        if (Field.IsPrimaryKey || Field.Unique || !string.IsNullOrWhiteSpace(Field.ForeignKey))
            throw new NotSupportedException("This SQLite helper adds columns without primary, unique, or foreign key constraints.");

        return AddColumnSql(TableName, $"{QuoteName(Field.Name)} {Field.GetDefText(IncludeName: false)}");
    }

    /// <summary>
    /// Builds SQLite ADD COLUMN SQL from an application-authored column definition.
    /// SQLite validates the definition when the statement executes.
    /// </summary>
    public override string AddColumnSql(string TableName, string ColumnDef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ColumnDef);
        return $"ALTER TABLE {QuoteName(TableName)} ADD COLUMN {ReplaceDataTypePlaceholders(ColumnDef)}";
    }

    /// <summary>
    /// Builds SQL to remove a SQLite column. SQLite checks schema dependencies when the statement runs.
    /// </summary>
    public override string DropColumnSql(string TableName, string ColumnName)
        => $"ALTER TABLE {QuoteName(TableName)} DROP COLUMN {QuoteName(ColumnName)}";

    /// <summary>
    /// Builds SQL to add NOT NULL to an existing column on SQLite 3.53 or later.
    /// SQLite validates the rows when the statement executes.
    /// </summary>
    public override string SetNotNullSql(string TableName, string ColumnName, string DataType)
        => AlterNotNullSql(TableName, ColumnName, "SET");

    /// <summary>
    /// Builds SQL to remove a column's NOT NULL requirement on SQLite 3.53 or later.
    /// </summary>
    public override string DropNotNullSql(string TableName, string ColumnName, string DataType)
        => AlterNotNullSql(TableName, ColumnName, "DROP");

    /// <summary>
    /// Builds a nullability change for SQLite versions that support it directly.
    /// </summary>
    string AlterNotNullSql(string TableName, string ColumnName, string Operation)
    {
        if (!SupportsAlterTableType(AlterTableType.ColumnSetOrDropNotNull))
            throw new NotSupportedException("Direct NOT NULL changes require SQLite 3.53 or later.");
        return $"ALTER TABLE {QuoteName(TableName)} ALTER COLUMN {QuoteName(ColumnName)} {Operation} NOT NULL";
    }

    /// <summary>
    /// Reports that adding a table-level unique constraint requires rebuilding the SQLite table.
    /// Use CreateIndexSql to create a standalone unique index.
    /// </summary>
    public override string AddUniqueConstraintSql(string TableName, string ColumnName, string ConstraintName)
        => throw new NotSupportedException("Adding a SQLite unique constraint requires rebuilding the table.");

    /// <summary>
    /// Reports that dropping a table-level unique constraint requires rebuilding the SQLite table.
    /// Use DropIndexSql for an explicitly created index.
    /// </summary>
    public override string DropUniqueConstraintSql(string TableName, string ConstraintName)
        => throw new NotSupportedException("Dropping a SQLite unique constraint requires rebuilding the table.");

    /// <summary>
    /// Reports that adding a table-level foreign key requires rebuilding the SQLite table.
    /// </summary>
    public override string AddForeignKeySql(string TableName, string ColumnName, string ForeignTableName, string ForeignColumnName, string ConstraintName)
        => throw new NotSupportedException("Adding a SQLite foreign key requires rebuilding the table.");

    /// <summary>
    /// Reports that dropping a table-level foreign key requires rebuilding the SQLite table.
    /// </summary>
    public override string DropForeignKeySql(string TableName, string ConstraintName)
        => throw new NotSupportedException("Dropping a SQLite foreign key requires rebuilding the table.");

    /// <summary>
    /// Builds CREATE INDEX SQL for the columns in the supplied order.
    /// A unique index enforces uniqueness on an existing table.
    /// </summary>
    public override string CreateIndexSql(string TableName, string IndexName, IReadOnlyList<string> ColumnNames, bool Unique = false)
    {
        ArgumentNullException.ThrowIfNull(ColumnNames);
        if (ColumnNames.Count == 0)
            throw new ArgumentException("An index requires at least one column.", nameof(ColumnNames));
        string Columns = string.Join(", ", ColumnNames.Select(QuoteName));
        return $"CREATE {(Unique ? "UNIQUE " : "")}INDEX {QuoteName(IndexName)} ON {QuoteName(TableName)} ({Columns})";
    }

    /// <summary>
    /// Builds DROP INDEX SQL for an explicitly created SQLite index.
    /// The index name identifies the object within its schema.
    /// </summary>
    public override string DropIndexSql(string TableName, string IndexName)
        => $"DROP INDEX {QuoteName(IndexName)}";

    // ● capabilities
    /// <summary>
    /// Gets the direct table alterations implemented for SQLite.
    /// Standalone index operations are available through CreateIndexSql and DropIndexSql.
    /// </summary>
    public override AlterTableType SupportedAlterTableTypes
    {
        get
        {
            using (SqliteConnection Connection = new())
            {
                AlterTableType Result = AlterTableType.ColumnAddOrDrop | AlterTableType.ColumnRename;
                if (Version.TryParse(Connection.ServerVersion, out Version EngineVersion) && EngineVersion >= new Version(3, 53, 0))
                    Result |= AlterTableType.ColumnSetOrDropNotNull;
                return Result;
            }
        }
    }

    /// <summary>
    /// SQLite generates integer identifiers during insertion.
    /// </summary>
    public override OidMode OidMode => OidMode.AutoInc;

    // ● SQL type mappings
    /// <summary>
    /// Gets SQLite's SQL for the primary key declaration.
    /// </summary>
    public override string PrimaryKeySql => "integer not null primary key autoincrement";

    /// <summary>
    /// Gets SQLite's SQL for the auto-increment field declaration.
    /// </summary>
    public override string AutoIncSql => "integer autoincrement";

    /// <summary>
    /// Gets SQLite's SQL for the signed 64-bit integer type.
    /// </summary>
    public override string Int64Sql => "integer";

    /// <summary>
    /// Gets SQLite's SQL for the floating-point type.
    /// </summary>
    public override string FloatSql => "real";

    /// <summary>
    /// Gets SQLite's SQL for the default decimal type.
    /// </summary>
    public override string DecimalSql => "real";

    /// <summary>
    /// Gets SQLite's SQL for the date and time type.
    /// </summary>
    public override string DateTimeSql => "datetime";

    /// <summary>
    /// Gets SQLite's SQL for the date-only type.
    /// </summary>
    public override string DateSql => "datetime";

    /// <summary>
    /// Gets SQLite's SQL for the nullable column clause.
    /// </summary>
    public override string NullSql => " ";

    // ● capabilities
    /// <summary>
    /// Gets whether SQLite database files can be created.
    /// </summary>
    public override bool CanCreateDatabases => true;

    // ● properties
    /// <summary>
    /// Gets the database server type implemented by this provider.
    /// </summary>
    public override DbServerType ServerType => DbServerType.Sqlite;

    /// <summary>
    /// Gets the display description for the SQLite provider.
    /// </summary>
    public override string Description => "SQLite3";

    /// <summary>
    /// Gets the ADO.NET factory for Microsoft.Data.Sqlite.
    /// </summary>
    public override DbProviderFactory Factory => SqliteFactory.Instance;

    /// <summary>
    /// Gets the SQLite template with the database file path as its sole argument.
    /// </summary>
    public override string ConnectionStringTemplate => "Data Source=\"{0}\"";
    /// <summary>
    /// SQLite has no administrative user name.
    /// </summary>
    public override string SuperUser => "";
    /// <summary>
    /// SQLite has no administrative password.
    /// </summary>
    public override string SuperUserPassword => "";
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
