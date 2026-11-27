/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Provides Microsoft SQL Server connectivity and identity key retrieval.
/// </summary>
public class SqlProviderMsSql : SqlProvider
{
    // ● constructor
    internal SqlProviderMsSql()
    {
    }

    // ● metadata
    /// <summary>
    /// Maps SQL Server columns to item field types that can hold their values.
    /// </summary>
    internal override DataFieldType GetMetadataFieldType(DbMetaColumn Column)
    {
        string Type = Column.DataType.Trim().ToLowerInvariant();
        return Type switch
        {
            "tinyint" or "smallint" or "int" or "integer" => DataFieldType.Integer,
            "bigint" => DataFieldType.Int64,
            "bit" => DataFieldType.Boolean,
            "char" or "nchar" => DataFieldType.String,
            "varchar" or "nvarchar" => Column.SizeInChars < 0 ? DataFieldType.TextBlob : DataFieldType.String,
            "text" or "ntext" => DataFieldType.TextBlob,
            "float" or "real" => DataFieldType.Double,
            "decimal" or "numeric" when Column.Precision <= 28 => DataFieldType.Decimal,
            "money" or "smallmoney" => DataFieldType.Decimal,
            "date" => DataFieldType.Date,
            "datetime" or "datetime2" or "smalldatetime" => DataFieldType.DateTime,
            "binary" or "varbinary" or "image" or "timestamp" or "rowversion" => DataFieldType.Blob,
            _ => throw new NotSupportedException($"Column '{Column.Name}' uses SQL Server type '{Column.DataType}', which has no inferred field type."),
        };
    }

    // ● connection
    /// <summary>
    /// Builds a SQL Server connection string from the supplied connection settings.
    /// </summary>
    public override string CreateConnectionString(string Server, string Database, string UserName, string Password)
    {
        SqlConnectionStringBuilder Builder = new()
        {
            DataSource = Server,
            InitialCatalog = Database,
            UserID = UserName,
            Password = Password,
        };
        return Builder.ConnectionString;
    }
    /// <summary>
    /// Gets the database name declared in the connection string.
    /// </summary>
    public override string GetDatabaseName(string ConnectionString)
    {
        string Database = ((SqlConnectionStringBuilder)CreateConnectionStringBuilder(ConnectionString)).InitialCatalog;
        return string.IsNullOrWhiteSpace(Database) ? string.Empty : Database;
    }

    // ● database creation
    /// <summary>
    /// Creates the database named in the connection string and waits until it accepts connections.
    /// Returns false if the database already exists.
    /// </summary>
    public override bool CreateDatabase(string ConnectionString)
    {
        var Settings = (SqlConnectionStringBuilder)CreateConnectionStringBuilder(ConnectionString);
        string Database = Settings.InitialCatalog;
        ArgumentException.ThrowIfNullOrWhiteSpace(Database, nameof(ConnectionString));
        if (!string.IsNullOrWhiteSpace(Settings.AttachDBFilename))
            throw new ArgumentException("Database creation requires a catalog name without AttachDBFilename.", nameof(ConnectionString));

        // SQL Server requires CREATE DATABASE to run in autocommit mode.
        Settings.Enlist = false;
        try
        {
            using (DbConnection Existing = OpenConnection(Settings.ConnectionString))
            {
                return false;
            }
        }
        catch (SqlException Error) when (Error.Number == 4060) // Cannot open the requested database.
        {
            Settings.InitialCatalog = "master";
        }

        using (DbConnection Connection = OpenConnection(Settings.ConnectionString))
        using (DbCommand Command = CreateCommand(Connection, $"CREATE DATABASE {QuoteName(Database)}", new SqlParams()))
        {
            try
            {
                Command.ExecuteNonQuery();
            }
            catch (SqlException Error) when (Error.Number == 1801) // The database already exists.
            {
                return false;
            }
        }

        Settings.InitialCatalog = Database;
        if (!WaitUntilDatabaseReady(Settings.ConnectionString))
            throw new InvalidOperationException($"SQL Server database '{Database}' was created, but it did not accept a connection.");
        return true;
    }

    // ● locking and generated IDs
    /// <summary>
    /// Returns a SELECT statement that acquires an update lock for the supplied key.
    /// </summary>
    public override string SelectForUpdateSql(string TableName, string FieldName)
    {
        DataTableDef.ValidateIdentifier(TableName);
        DataTableDef.ValidateIdentifier(FieldName);
        return $"SELECT * FROM {QuoteName(TableName)} WITH (UPDLOCK, ROWLOCK) WHERE {QuoteName(FieldName)} = {GlobalPrefix}{FieldName}";
    }
    /// <summary>
    /// Inserts one row and reads its identity in the same batch and transaction.
    /// SCOPE_IDENTITY reads the identity from the INSERT's scope even when a trigger generates another identity.
    /// </summary>
    public override long InsertAndGetId(SqlStore Store, DbTransaction Transaction, string SqlText, SqlParams Params, string TableName, string KeyField)
    {
        ArgumentNullException.ThrowIfNull(Store);
        ArgumentException.ThrowIfNullOrWhiteSpace(TableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(SqlText);
        string BatchSql = SqlTextScanner.PrepareForAppend(SqlText, ServerType) + ";SELECT CONVERT(bigint, SCOPE_IDENTITY())";
        object Id = Store.ExecuteScalar(Transaction, BatchSql, Params);
        if (Id is not long Result)
            throw new InvalidOperationException($"Insert did not return a generated key for table '{TableName}'.");
        return Result;
    }

    // ● identity reseeding
    /// <summary>
    /// Reseeds the table identity to Value using DBCC CHECKIDENT.
    /// The next insert uses Value for a new or truncated table, or Value plus the identity increment otherwise.
    /// Reseeding below existing values can cause duplicate keys.
    /// </summary>
    public override void SetTableGeneratorTo(string ConnectionString, string TableName, long Value)
    {
        string Table = QuoteUnicodeLiteral(QuoteName(TableName));
        string SqlText = FormattableString.Invariant($"DBCC CHECKIDENT ({Table}, RESEED, {Value}) WITH NO_INFOMSGS");
        SqlStore Store = new(new DbConnectionInfo { ConnectionString = ConnectionString, DbServerType = ServerType });
        Store.ExecSql(SqlText);
    }

    // ● list queries
    /// <summary>
    /// Applies a row limit to the complete result of a SQL Server list query.
    /// </summary>
    public override string ApplyRowLimit(string SqlText, int RowLimit)
        => SqlTextScanner.ApplyLimit(SqlText, RowLimit, ServerType);
    /// <summary>
    /// Concatenates string expressions with the SQL Server + operator.
    /// For example, SELECT FirstName + N' ' + LastName AS FullName FROM Customers.
    /// </summary>
    public override string Concat(params string[] Parts) => string.Join(" + ", Parts);

    // ● SQL literals
    /// <summary>
    /// Quotes a Unicode SQL string literal, including any embedded apostrophes.
    /// </summary>
    static string QuoteUnicodeLiteral(string Value)
        => "N'" + Value.Replace("'", "''") + "'";

    // ● table schema changes
    /// <summary>
    /// Builds a call to sp_rename for one column of an existing table.
    /// </summary>
    public override string RenameColumnSql(string TableName, string ColumnName, string NewColumnName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(NewColumnName);
        string Column = $"{QuoteName(TableName)}.{QuoteName(ColumnName)}";
        return $"EXEC sys.sp_rename {QuoteUnicodeLiteral(Column)}, {QuoteUnicodeLiteral(NewColumnName)}, N'COLUMN'";
    }
    /// <summary>
    /// Builds SQL to add a column from a portable field definition.
    /// Use the separate constraint methods to add unique or foreign key constraints.
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
        return base.AddColumnSql(TableName, ReplaceDataTypePlaceholders(ColumnDef));
    }
    /// <summary>
    /// Builds SQL to drop a column. Remove dependent constraints and indexes first.
    /// </summary>
    public override string DropColumnSql(string TableName, string ColumnName)
        => $"ALTER TABLE {QuoteName(TableName)} DROP COLUMN {QuoteName(ColumnName)}";
    /// <summary>
    /// Builds SQL to change a column's length, restating its nullability.
    /// SQL Server stores the default in a separate constraint.
    /// </summary>
    public override string SetColumnLengthSql(string TableName, string ColumnName, string DataType, string Required, string DefaultExpression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Required);
        return AlterColumnSql(TableName, ColumnName, DataType, Required);
    }
    /// <summary>
    /// Builds SQL to require a value. Fill existing NULL values first.
    /// </summary>
    public override string SetNotNullSql(string TableName, string ColumnName, string DataType)
        => AlterColumnSql(TableName, ColumnName, DataType, "NOT NULL");
    /// <summary>
    /// Builds SQL to make an existing column nullable.
    /// </summary>
    public override string DropNotNullSql(string TableName, string ColumnName, string DataType)
        => AlterColumnSql(TableName, ColumnName, DataType, "NULL");
    /// <summary>
    /// Builds an ALTER COLUMN statement with the full type declaration and nullability.
    /// </summary>
    string AlterColumnSql(string TableName, string ColumnName, string DataType, string Required)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DataType);
        return $"ALTER TABLE {QuoteName(TableName)} ALTER COLUMN {QuoteName(ColumnName)} {ReplaceDataTypePlaceholders(DataType)} {ReplaceDataTypePlaceholders(Required)}";
    }
    /// <summary>
    /// Builds SQL to add a default constraint for future inserts.
    /// To replace an existing default, drop its constraint first.
    /// </summary>
    public override string SetColumnDefaultSql(string TableName, string ColumnName, string DefaultExpression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DefaultExpression);
        return $"ALTER TABLE {QuoteName(TableName)} ADD DEFAULT {DefaultExpression} FOR {QuoteName(ColumnName)}";
    }
    /// <summary>
    /// Builds SQL to look up and drop a column's default constraint by its actual name.
    /// Handles both explicitly named and system-named constraints.
    /// </summary>
    public override string DropColumnDefaultSql(string TableName, string ColumnName)
    {
        string Table = QuoteName(TableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(ColumnName);
        return $@"DECLARE @ConstraintName sysname;
SELECT @ConstraintName = D.name
FROM sys.default_constraints AS D
JOIN sys.columns AS C ON C.object_id = D.parent_object_id AND C.column_id = D.parent_column_id
WHERE C.object_id = OBJECT_ID({QuoteUnicodeLiteral(Table)}, N'U') AND C.name = {QuoteUnicodeLiteral(ColumnName)};
IF @ConstraintName IS NOT NULL
BEGIN
    DECLARE @Sql nvarchar(max) = {QuoteUnicodeLiteral($"ALTER TABLE {Table} DROP CONSTRAINT ")} + QUOTENAME(@ConstraintName);
    EXEC sys.sp_executesql @Sql;
END";
    }
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
    /// Builds DROP INDEX SQL qualified by the table on which the index is defined.
    /// </summary>
    public override string DropIndexSql(string TableName, string IndexName)
        => $"DROP INDEX {QuoteName(IndexName)} ON {QuoteName(TableName)}";

    // ● properties
    /// <summary>
    /// Gets the database server type implemented by this provider.
    /// </summary>
    public override DbServerType ServerType => DbServerType.MsSql;
    /// <summary>
    /// Gets the provider's display name.
    /// </summary>
    public override string Description => "Microsoft SQL Server";
    /// <summary>
    /// Gets the Microsoft.Data.SqlClient factory.
    /// </summary>
    public override DbProviderFactory Factory => SqlClientFactory.Instance;
    /// <summary>
    /// Gets the template in server, database, user name and password order.
    /// </summary>
    public override string ConnectionStringTemplate => "Data Source={0}; Initial Catalog={1}; User Id={2}; Password={3};";
    /// <summary>
    /// Gets the built-in SQL Server administrative login name.
    /// </summary>
    public override string SuperUser => "sa";
    /// <summary>
    /// Gets the prefix used for named parameters.
    /// </summary>
    public override string NativePrefix => "@";
    /// <summary>
    /// Gets the opening delimiter for a quoted SQL Server identifier.
    /// </summary>
    public override string ObjectStartDelimiter => "[";
    /// <summary>
    /// Gets the closing delimiter for a quoted SQL Server identifier.
    /// </summary>
    public override string ObjectEndDelimiter => "]";
    /// <summary>
    /// Gets the ID mode used for integer keys generated on insert.
    /// </summary>
    public override OidMode OidMode => OidMode.AutoInc;
    /// <summary>
    /// Gets whether the provider can create databases.
    /// </summary>
    public override bool CanCreateDatabases => true;
    /// <summary>
    /// Gets SQL that reads the server's current UTC timestamp.
    /// </summary>
    public override string ServerDateTimeSql => "SELECT SYSUTCDATETIME()";
    /// <summary>
    /// Gets SQL that resolves a table in the current database using the caller's default schema.
    /// </summary>
    public override string TableExistsSql => "SELECT 1 WHERE OBJECT_ID(QUOTENAME(:TableName), N'U') IS NOT NULL";
    /// <summary>
    /// Gets SQL that resolves a view in the current database using the caller's default schema.
    /// </summary>
    public override string ViewExistsSql => "SELECT 1 WHERE OBJECT_ID(QUOTENAME(:ViewName), N'V') IS NOT NULL";
    /// <summary>
    /// Gets the direct column and constraint alterations supported by SQL Server.
    /// </summary>
    public override AlterTableType SupportedAlterTableTypes => AlterTableType.All;

    // ● SQL type mappings
    /// <summary>
    /// Gets the declaration for an integer identity column.
    /// </summary>
    public override string AutoIncSql => "int identity(1,1)";
    /// <summary>
    /// Gets the type used for date and time fields.
    /// </summary>
    public override string DateTimeSql => "datetime";
    /// <summary>
    /// Gets the SQL Server integer type used for portable Boolean fields.
    /// </summary>
    public override string BoolSql => "int";
    /// <summary>
    /// Gets the variable-length binary large object type.
    /// </summary>
    public override string BlobSql => "varbinary(max)";
    /// <summary>
    /// Gets the variable-length text large object type.
    /// </summary>
    public override string BlobTextSql => "varchar(max)";
    /// <summary>
    /// Gets the variable-length Unicode text large object type.
    /// </summary>
    public override string NBlobTextSql => "nvarchar(max)";
}
