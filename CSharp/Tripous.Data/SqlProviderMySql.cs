/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Provides MySQL connectivity and SQL dialect support.
/// </summary>
/// <remarks>
/// Tested with MySQL Server 8.0.46 and MySql.Data 9.1.0.
/// To use a different connector version, update the MySql.Data package reference in Tripous.Data.csproj.
/// Use InnoDB for item tables that participate in transactions.
/// Integer keys posted by TableSet require an AUTO_INCREMENT column.
/// </remarks>
public class SqlProviderMySql : SqlProvider
{
    // ● private
    /// <summary>
    /// Builds a MODIFY statement from a complete replacement column definition.
    /// </summary>
    string ModifyColumnSql(string TableName, string ColumnName, string ColumnDef)
        => $"ALTER TABLE {QuoteName(TableName)} MODIFY COLUMN {QuoteName(ColumnName)} {ReplaceDataTypePlaceholders(ColumnDef).TrimEnd()}";

    // ● protected
    /// <summary>
    /// Declares the Unicode character set after the column length.
    /// </summary>
    protected override string NVarcharWithLengthSql(string LengthClause)
        => base.NVarcharWithLengthSql(LengthClause) + " CHARACTER SET utf8mb4";

    // ● constructor
    internal SqlProviderMySql()
    {
    }

    // ● metadata
    /// <summary>
    /// Maps MySQL column types to field types supported by the item tree.
    /// Unsigned columns must fit the full range of the inferred signed type.
    /// </summary>
    internal override DataFieldType GetMetadataFieldType(DbMetaColumn Column)
    {
        string Declaration = Column.DataType.Trim().ToLowerInvariant();
        string Type = Declaration.Split(['(', ' '], 2)[0];
        bool Unsigned = Declaration.Contains(" unsigned", StringComparison.Ordinal);
        return Type switch
        {
            "tinyint" or "smallint" or "mediumint" => DataFieldType.Integer,
            "int" or "integer" => Unsigned ? DataFieldType.Int64 : DataFieldType.Integer,
            "bigint" when !Unsigned => DataFieldType.Int64,
            "char" or "varchar" => DataFieldType.String,
            "float" or "double" or "real" => DataFieldType.Double,
            "decimal" or "numeric" when Column.Precision <= 28 => DataFieldType.Decimal,
            "date" => DataFieldType.Date,
            "datetime" or "timestamp" => DataFieldType.DateTime,
            "binary" or "varbinary" or "tinyblob" or "blob" or "mediumblob" or "longblob" => DataFieldType.Blob,
            "tinytext" or "text" or "mediumtext" or "longtext" => DataFieldType.TextBlob,
            _ => throw new NotSupportedException($"Column '{Column.Name}' uses MySQL type '{Column.DataType}', which has no inferred field type."),
        };
    }

    // ● connection
    /// <summary>
    /// Builds a MySQL connection string using the supplied connection settings.
    /// </summary>
    public override string CreateConnectionString(string Server, string Database, string UserName, string Password)
    {
        MySqlConnectionStringBuilder Builder = new()
        {
            Server = Server,
            Database = Database,
            UserID = UserName,
            Password = Password,
            CharacterSet = "utf8mb4",
        };
        return Builder.ConnectionString;
    }
    /// <summary>
    /// Gets the database name declared in the MySQL connection string.
    /// </summary>
    public override string GetDatabaseName(string ConnectionString)
    {
        string Database = ((MySqlConnectionStringBuilder)CreateConnectionStringBuilder(ConnectionString)).Database;
        return string.IsNullOrWhiteSpace(Database) ? string.Empty : Database;
    }
    /// <summary>
    /// Creates a UTF-8 MySQL database and waits for it to accept connections.
    /// Returns false when the database already exists.
    /// </summary>
    public override bool CreateDatabase(string ConnectionString)
    {
        var Settings = (MySqlConnectionStringBuilder)CreateConnectionStringBuilder(ConnectionString);
        string Database = Settings.Database;
        ArgumentException.ThrowIfNullOrWhiteSpace(Database, nameof(ConnectionString));
        Settings.AutoEnlist = false;

        try
        {
            using (DbConnection Existing = OpenConnection(Settings.ConnectionString))
            {
                return false;
            }
        }
        catch (MySqlException Error) when (Error.Number == (int)MySqlErrorCode.UnknownDatabase)
        {
            Settings.Database = string.Empty;
        }

        using (DbConnection Connection = OpenConnection(Settings.ConnectionString))
        using (DbCommand Command = CreateCommand(Connection, $"CREATE DATABASE {QuoteName(Database)} CHARACTER SET utf8mb4", new SqlParams()))
        {
            try
            {
                Command.ExecuteNonQuery();
            }
            catch (MySqlException Error) when (Error.Number == (int)MySqlErrorCode.DatabaseCreateExists)
            {
                return false;
            }
        }

        Settings.Database = Database;
        if (!WaitUntilDatabaseReady(Settings.ConnectionString))
            throw new InvalidOperationException($"MySQL database '{Database}' was created, but it did not accept a connection.");
        return true;
    }

    // ● locking and generated IDs
    /// <summary>
    /// Returns a SELECT statement that locks an existing InnoDB row for update.
    /// </summary>
    public override string SelectForUpdateSql(string TableName, string FieldName)
    {
        DataTableDef.ValidateIdentifier(TableName);
        DataTableDef.ValidateIdentifier(FieldName);
        return $"SELECT * FROM {QuoteName(TableName)} WHERE {QuoteName(FieldName)} = {GlobalPrefix}{FieldName} FOR UPDATE";
    }
    /// <summary>
    /// Gets SQL that reads the last automatically generated ID on the current connection.
    /// </summary>
    public override string LastIdSql(string TableName) => "SELECT LAST_INSERT_ID()";
    /// <summary>
    /// Sets the requested next AUTO_INCREMENT value to one greater than Value.
    /// MySQL keeps it above IDs already present in the table. The ALTER commits independently.
    /// </summary>
    public override void SetTableGeneratorTo(string ConnectionString, string TableName, long Value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(Value);
        long NextValue = checked(Value + 1);
        string Table = QuoteName(TableName);
        var Settings = (MySqlConnectionStringBuilder)CreateConnectionStringBuilder(ConnectionString);
        Settings.AutoEnlist = false;
        const string FindCounterSql = @"
            SELECT 1
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = :TableName
                AND EXTRA LIKE '%auto_increment%'";
        using (DbConnection Connection = OpenConnection(Settings.ConnectionString))
        {
            using (DbCommand FindCounter = CreateCommand(Connection, FindCounterSql, new SqlParams().Add("TableName", TableName)))
            {
                if (FindCounter.ExecuteScalar() == null)
                    throw new InvalidOperationException($"No AUTO_INCREMENT column was found for table '{TableName}'.");
            }
            string SqlText = FormattableString.Invariant($"ALTER TABLE {Table} AUTO_INCREMENT = {NextValue}");
            using (DbCommand Command = CreateCommand(Connection, SqlText, new SqlParams()))
            {
                Command.ExecuteNonQuery();
            }
        }
    }

    // ● list queries
    /// <summary>
    /// Applies an outer LIMIT to a list SELECT using standard MySQL string escaping.
    /// </summary>
    public override string ApplyRowLimit(string SqlText, int RowLimit)
        => SqlTextScanner.ApplyLimit(SqlText, RowLimit, ServerType);

    // ● SQL expressions
    /// <summary>
    /// Joins one or more SQL expressions using MySQL's CONCAT function.
    /// </summary>
    public override string Concat(params string[] Parts) => $"CONCAT({string.Join(", ", Parts)})";

    // ● table schema changes
    /// <summary>
    /// Builds an ALTER TABLE statement that renames an existing column.
    /// </summary>
    public override string RenameColumnSql(string TableName, string ColumnName, string NewColumnName)
        => $"ALTER TABLE {QuoteName(TableName)} RENAME COLUMN {QuoteName(ColumnName)} TO {QuoteName(NewColumnName)}";
    /// <summary>
    /// Builds SQL to add a column from a portable field definition.
    /// Add unique and foreign key constraints separately.
    /// </summary>
    public override string AddColumnSql(string TableName, DataFieldDef Field)
    {
        ArgumentNullException.ThrowIfNull(Field);
        if (Field.Unique || !string.IsNullOrWhiteSpace(Field.ForeignKey))
            throw new NotSupportedException("Add unique and foreign key constraints separately from the column definition.");

        return AddColumnSql(TableName, $"{QuoteName(Field.Name)} {Field.GetDefText(IncludeName: false)}");
    }
    /// <summary>
    /// Builds SQL to add a column from an application-authored definition.
    /// </summary>
    public override string AddColumnSql(string TableName, string ColumnDef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ColumnDef);
        return $"ALTER TABLE {QuoteName(TableName)} ADD COLUMN {ReplaceDataTypePlaceholders(ColumnDef)}";
    }
    /// <summary>
    /// Builds SQL to change a column's declared length and restate its nullability and default.
    /// Include any other attributes that must be preserved in DataType.
    /// </summary>
    public override string SetColumnLengthSql(string TableName, string ColumnName, string DataType, string Required, string DefaultExpression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DataType);
        string Default = string.IsNullOrWhiteSpace(DefaultExpression) ? "" : $" DEFAULT {DefaultExpression}";
        return ModifyColumnSql(TableName, ColumnName, $"{DataType} {Required}{Default}");
    }
    /// <summary>
    /// Builds SQL to require a value. Fill existing NULL values first.
    /// Include all attributes to retain, including any DEFAULT clause, in DataType.
    /// </summary>
    public override string SetNotNullSql(string TableName, string ColumnName, string DataType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DataType);
        return ModifyColumnSql(TableName, ColumnName, $"{DataType} NOT NULL");
    }
    /// <summary>
    /// Builds SQL to make a column nullable.
    /// Include all attributes to retain, including any DEFAULT clause, in DataType.
    /// </summary>
    public override string DropNotNullSql(string TableName, string ColumnName, string DataType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DataType);
        return ModifyColumnSql(TableName, ColumnName, $"{DataType} NULL");
    }
    /// <summary>
    /// Builds SQL to set the default for future inserts.
    /// </summary>
    public override string SetColumnDefaultSql(string TableName, string ColumnName, string DefaultExpression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DefaultExpression);
        return $"ALTER TABLE {QuoteName(TableName)} ALTER COLUMN {QuoteName(ColumnName)} SET DEFAULT {DefaultExpression}";
    }
    /// <summary>
    /// Builds SQL to remove a column default.
    /// </summary>
    public override string DropColumnDefaultSql(string TableName, string ColumnName)
        => $"ALTER TABLE {QuoteName(TableName)} ALTER COLUMN {QuoteName(ColumnName)} DROP DEFAULT";
    /// <summary>
    /// Builds SQL to remove the index that enforces the named unique constraint.
    /// </summary>
    public override string DropUniqueConstraintSql(string TableName, string ConstraintName)
        => DropIndexSql(TableName, ConstraintName);
    /// <summary>
    /// Builds SQL to drop the named foreign key constraint.
    /// </summary>
    public override string DropForeignKeySql(string TableName, string ConstraintName)
        => $"ALTER TABLE {QuoteName(TableName)} DROP FOREIGN KEY {QuoteName(ConstraintName)}";
    /// <summary>
    /// Builds CREATE INDEX SQL using the columns in the supplied order.
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
    /// Builds SQL to drop an index from its owning table.
    /// </summary>
    public override string DropIndexSql(string TableName, string IndexName)
        => $"ALTER TABLE {QuoteName(TableName)} DROP INDEX {QuoteName(IndexName)}";

    // ● properties
    /// <summary>
    /// Gets the database server type implemented by this provider.
    /// </summary>
    public override DbServerType ServerType => DbServerType.MySql;
    /// <summary>
    /// Gets the MySQL Connector/NET ADO.NET factory.
    /// </summary>
    public override DbProviderFactory Factory => MySqlClientFactory.Instance;
    /// <summary>
    /// Gets the template in server, database, user name and password order.
    /// </summary>
    public override string ConnectionStringTemplate => "Server={0}; Database={1}; User Id={2}; Password={3}; Charset=utf8mb4;";
    /// <summary>
    /// Gets the customary MySQL administrative user name.
    /// </summary>
    public override string SuperUser => "root";
    /// <summary>
    /// Gets the prefix used for named parameters.
    /// </summary>
    public override string NativePrefix => "@";
    /// <summary>
    /// Gets the opening delimiter for a quoted MySQL identifier.
    /// </summary>
    public override string ObjectStartDelimiter => "`";
    /// <summary>
    /// Gets the closing delimiter for a quoted MySQL identifier.
    /// </summary>
    public override string ObjectEndDelimiter => "`";
    /// <summary>
    /// Gets the mode for integer keys generated during insertion.
    /// </summary>
    public override OidMode OidMode => OidMode.AutoInc;
    /// <summary>
    /// Gets the direct column and constraint alterations supported by MySQL.
    /// </summary>
    public override AlterTableType SupportedAlterTableTypes => AlterTableType.All;
    /// <summary>
    /// Gets whether MySQL can create databases using the supplied connection settings.
    /// </summary>
    public override bool CanCreateDatabases => true;
    /// <summary>
    /// Gets SQL for the current UTC timestamp.
    /// </summary>
    public override string ServerDateTimeSql => "SELECT UTC_TIMESTAMP(6)";
    /// <summary>
    /// Gets SQL that looks up a table in the current database.
    /// </summary>
    public override string TableExistsSql
        => "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = :TableName AND TABLE_TYPE = 'BASE TABLE'";
    /// <summary>
    /// Gets SQL that looks up a view in the current database.
    /// </summary>
    public override string ViewExistsSql
        => "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.VIEWS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = :ViewName";

    // ● SQL type mappings
    /// <summary>
    /// Gets the declaration for an auto-incrementing integer primary key.
    /// </summary>
    public override string PrimaryKeySql => "integer auto_increment not null primary key";
    /// <summary>
    /// Gets the declaration for an auto-incrementing integer column.
    /// </summary>
    public override string AutoIncSql => "integer auto_increment";
    /// <summary>
    /// Gets the MySQL type name used for variable-length Unicode text.
    /// </summary>
    public override string NVarcharSql => "varchar";
    /// <summary>
    /// Gets the double-precision floating-point type.
    /// </summary>
    public override string FloatSql => "double";
    /// <summary>
    /// Gets the date and time type.
    /// </summary>
    public override string DateTimeSql => "datetime";
    /// <summary>
    /// Gets the integer-backed Boolean type.
    /// </summary>
    public override string BoolSql => "tinyint";
    /// <summary>
    /// Gets the binary large object type.
    /// </summary>
    public override string BlobSql => "longblob";
    /// <summary>
    /// Gets the large text type.
    /// </summary>
    public override string BlobTextSql => "longtext";
    /// <summary>
    /// Gets the Unicode large text type.
    /// </summary>
    public override string NBlobTextSql => "longtext CHARACTER SET utf8mb4";
}
