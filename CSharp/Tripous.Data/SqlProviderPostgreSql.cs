/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Provides PostgreSQL connectivity and SQL dialect support through Npgsql.
/// </summary>
public class SqlProviderPostgreSql : SqlProvider
{
    // ● private fields
    static readonly string fGeneratorExistsSql = $"""
        SELECT 1 FROM pg_catalog.pg_class
        WHERE oid = pg_catalog.to_regclass({GlobalPrefix}GeneratorName) AND relkind = 'S'
        """;

    // ● construction
    internal SqlProviderPostgreSql()
    {
    }

    // ● metadata
    /// <summary>
    /// Maps PostgreSQL column types to field types supported by the item tree.
    /// Tripous represents boolean fields with database integers.
    /// </summary>
    internal override DataFieldType GetMetadataFieldType(DbMetaColumn Column)
        => Column.DataType switch
        {
            "int2" or "int4" => DataFieldType.Integer,
            "int8" => DataFieldType.Int64,
            "varchar" or "bpchar" or "name" => DataFieldType.String,
            "text" => DataFieldType.TextBlob,
            "numeric" => DataFieldType.Decimal,
            "float4" or "float8" => DataFieldType.Double,
            "date" => DataFieldType.Date,
            "timestamp" => DataFieldType.DateTime,
            "bytea" => DataFieldType.Blob,
            _ => throw new NotSupportedException($"Column '{Column.Name}' uses PostgreSQL type '{Column.DataType}', which has no inferred field type."),
        };

    // ● connection
    /// <summary>
    /// Builds a PostgreSQL connection string with the supplied connection settings.
    /// </summary>
    public override string CreateConnectionString(string Server, string Database, string UserName, string Password)
    {
        var Builder = new NpgsqlConnectionStringBuilder
        {
            Host = Server,
            Database = Database,
            Username = UserName,
            Password = Password
        };
        return Builder.ConnectionString;
    }

    // ● database administration
    /// <summary>
    /// Creates a UTF-8 database through the PostgreSQL maintenance database.
    /// Returns false when the named database already exists.
    /// </summary>
    public override bool CreateDatabase(string ConnectionString)
    {
        var Settings = new NpgsqlConnectionStringBuilder(ConnectionString);
        string Database = Settings.Database;
        ArgumentException.ThrowIfNullOrWhiteSpace(Database);
        Settings.Enlist = false;

        try
        {
            using (DbConnection Existing = OpenConnection(Settings.ConnectionString))
            {
                return false;
            }
        }
        catch (PostgresException Error) when (Error.SqlState == PostgresErrorCodes.InvalidCatalogName)
        {
            Settings.Database = "postgres";
        }

        using (DbConnection Maintenance = OpenConnection(Settings.ConnectionString))
        {
            using (DbCommand CheckName = CreateCommand(Maintenance,
                $"SELECT octet_length({GlobalPrefix}Name) <= current_setting('max_identifier_length')::integer",
                new SqlParams().Add("Name", Database)))
            {
                if (CheckName.ExecuteScalar() is not true)
                    throw new ArgumentException("The database name exceeds the PostgreSQL identifier limit.", nameof(ConnectionString));
            }

            using (DbCommand Create = CreateCommand(Maintenance, $"CREATE DATABASE {QuoteName(Database)} ENCODING 'UTF8'", new SqlParams()))
            {
                try
                {
                    Create.ExecuteNonQuery();
                }
                catch (PostgresException Error) when (Error.SqlState == PostgresErrorCodes.DuplicateDatabase
                    || (Error.SqlState == PostgresErrorCodes.UniqueViolation && Error.ConstraintName == "pg_database_datname_index"))
                {
                    return false;
                }
            }
        }

        Settings.Database = Database;
        if (!WaitUntilDatabaseReady(Settings.ConnectionString))
            throw new InvalidOperationException($"PostgreSQL database '{Database}' was created, but it did not accept a connection.");
        return true;
    }

    // ● SQL
    /// <summary>
    /// Gets the PostgreSQL expression for the current UTC timestamp.
    /// </summary>
    public override string ServerDateTimeSql => "(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')";
    /// <summary>
    /// Applies the framework's row limit to a list SELECT.
    /// List SELECTs must leave top-level LIMIT and FETCH to Tripous.
    /// </summary>
    public override string ApplyRowLimit(string SqlText, int RowLimit)
        => SqlTextScanner.ApplyLimit(SqlText, RowLimit, PostgreSql: true);

    // ● id generation
    /// <summary>
    /// Returns the inserted row's integer key directly from INSERT RETURNING.
    /// Sequence changes made by triggers do not affect the result.
    /// </summary>
    public override long InsertAndGetId(SqlStore Store, DbTransaction Transaction, string SqlText, SqlParams Params, string TableName, string KeyField)
    {
        ArgumentNullException.ThrowIfNull(Store);
        ArgumentException.ThrowIfNullOrWhiteSpace(TableName);
        DataTableDef.ValidateIdentifier(KeyField);
        string ReturningSql = $"{SqlTextScanner.PrepareForAppend(SqlText, PostgreSql: true)}RETURNING {KeyField}";
        object Id = Store.ExecuteScalar(Transaction, ReturningSql, Params);
        if (Id == null || Id == DBNull.Value)
            throw new InvalidOperationException($"Insert did not return a generated key for table '{TableName}'.");
        return Convert.ToInt64(Id, System.Globalization.CultureInfo.InvariantCulture);
    }
    /// <summary>
    /// Checks whether a portable unquoted name resolves to a PostgreSQL sequence.
    /// </summary>
    public override bool GeneratorExists(string ConnectionString, string GeneratorName)
    {
        DataTableDef.ValidateIdentifier(GeneratorName);
        SqlStore Store = new(new DbConnectionInfo { ConnectionString = ConnectionString, DbServerType = ServerType });
        SqlParams Params = new SqlParams().Add("GeneratorName", GeneratorName);
        return Store.Select(fGeneratorExistsSql, Params).Rows.Count > 0;
    }
    /// <summary>
    /// Creates a PostgreSQL sequence with a portable unquoted name.
    /// Repeated calls for the same sequence leave its counter intact.
    /// </summary>
    public override void CreateGenerator(string ConnectionString, string GeneratorName)
    {
        DataTableDef.ValidateIdentifier(GeneratorName);
        SqlStore Store = new(new DbConnectionInfo { ConnectionString = ConnectionString, DbServerType = ServerType });
        using (SqlTransactionContext Context = Store.BeginTransactionContext())
        {
            SqlParams Params = new SqlParams().Add("GeneratorName", GeneratorName);
            if (Store.ExecuteScalar(Context.Transaction,
                $"SELECT octet_length({GlobalPrefix}GeneratorName) <= current_setting('max_identifier_length')::integer", Params) is not true)
                throw new ArgumentException("The sequence name exceeds the PostgreSQL identifier limit.", nameof(GeneratorName));

            Store.ExecSql(Context.Transaction, $"CREATE SEQUENCE IF NOT EXISTS {GeneratorName}");
            if (Store.Select(Context.Transaction, fGeneratorExistsSql, Params).Rows.Count == 0)
                throw new InvalidOperationException($"'{GeneratorName}' does not resolve to a PostgreSQL sequence.");

            Context.Commit();
        }

    }
    /// <summary>
    /// Sets the last issued value of a PostgreSQL sequence.
    /// The next value advances by the configured increment, even after a rollback.
    /// </summary>
    public override void SetGeneratorTo(string ConnectionString, string GeneratorName, long Value)
    {
        DataTableDef.ValidateIdentifier(GeneratorName);
        SqlStore Store = new(new DbConnectionInfo { ConnectionString = ConnectionString, DbServerType = ServerType });
        SqlParams Params = new SqlParams().Add("GeneratorName", GeneratorName).Add("Value", Value);
        Store.Select($"SELECT pg_catalog.setval({GlobalPrefix}GeneratorName::regclass, {GlobalPrefix}Value)", Params);
    }
    /// <summary>
    /// Builds SQL that advances a named sequence and returns its new value.
    /// The name is a portable unquoted identifier.
    /// </summary>
    public override string NextIdByGeneratorSql(string GeneratorName)
    {
        DataTableDef.ValidateIdentifier(GeneratorName);
        return $"SELECT pg_catalog.nextval('{GeneratorName}')";
    }
    /// <summary>
    /// Sets the sequence owned by a single-column primary key, or the conventional G_TableName sequence.
    /// PostgreSQL sequence changes take effect immediately and survive transaction rollback.
    /// </summary>
    public override void SetTableGeneratorTo(string ConnectionString, string TableName, long Value)
    {
        DataTableDef.ValidateIdentifier(TableName);
        SqlStore Store = new(new DbConnectionInfo { ConnectionString = ConnectionString, DbServerType = ServerType });
        using (SqlTransactionContext Context = Store.BeginTransactionContext())
        {
            string Sql = $"""
                SELECT pg_catalog.setval(
                    COALESCE(
                        (SELECT pg_catalog.pg_get_serial_sequence({GlobalPrefix}TableName, a.attname)::regclass
                         FROM pg_catalog.pg_index i
                         JOIN pg_catalog.pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = i.indkey[0]
                         WHERE i.indrelid = pg_catalog.to_regclass({GlobalPrefix}TableName)
                             AND i.indisprimary AND i.indnkeyatts = 1),
                        (SELECT c.oid::regclass FROM pg_catalog.pg_class c
                         WHERE c.oid = pg_catalog.to_regclass({GlobalPrefix}GeneratorName) AND c.relkind = 'S')
                    ),
                    {GlobalPrefix}Value
                )
                """;
            SqlParams Params = new SqlParams().Add("TableName", TableName).Add("GeneratorName", "G_" + TableName).Add("Value", Value);
            object Result = Store.ExecuteScalar(Context.Transaction, Sql, Params);
            if (Result == null || Result == DBNull.Value)
                throw new InvalidOperationException($"No PostgreSQL sequence was found for table '{TableName}'.");

            Context.Commit();
        }

    }
    /// <summary>
    /// Gets the mode for integer keys generated by PostgreSQL during insertion.
    /// </summary>
    public override OidMode OidMode => OidMode.AutoInc;

    // ● table schema changes
    /// <summary>
    /// Builds an ALTER TABLE statement that renames an existing column.
    /// Pass the actual names stored in the database catalog.
    /// </summary>
    public override string RenameColumnSql(string TableName, string ColumnName, string NewColumnName)
        => $"ALTER TABLE {QuoteName(TableName)} RENAME COLUMN {QuoteName(ColumnName)} TO {QuoteName(NewColumnName)}";
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
        return $"ALTER TABLE {QuoteName(TableName)} ADD COLUMN {ReplaceDataTypePlaceholders(ColumnDef)}";
    }
    /// <summary>
    /// Builds SQL to change the type declaration, including a new column length.
    /// PostgreSQL retains the column's nullability and default expression.
    /// </summary>
    public override string SetColumnLengthSql(string TableName, string ColumnName, string DataType, string Required, string DefaultExpression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DataType);
        return $"ALTER TABLE {QuoteName(TableName)} ALTER COLUMN {QuoteName(ColumnName)} TYPE {ReplaceDataTypePlaceholders(DataType)}";
    }
    /// <summary>
    /// Builds SQL to add NOT NULL. Existing NULL values must be filled first.
    /// </summary>
    public override string SetNotNullSql(string TableName, string ColumnName, string DataType)
        => $"ALTER TABLE {QuoteName(TableName)} ALTER COLUMN {QuoteName(ColumnName)} SET NOT NULL";
    /// <summary>
    /// Builds SQL to remove the NOT NULL requirement from a column.
    /// </summary>
    public override string DropNotNullSql(string TableName, string ColumnName, string DataType)
        => $"ALTER TABLE {QuoteName(TableName)} ALTER COLUMN {QuoteName(ColumnName)} DROP NOT NULL";
    /// <summary>
    /// Builds SQL to set a default expression for future inserts.
    /// </summary>
    public override string SetColumnDefaultSql(string TableName, string ColumnName, string DefaultExpression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DefaultExpression);
        return $"ALTER TABLE {QuoteName(TableName)} ALTER COLUMN {QuoteName(ColumnName)} SET DEFAULT {DefaultExpression}";
    }
    /// <summary>
    /// Builds SQL to remove an existing column default.
    /// </summary>
    public override string DropColumnDefaultSql(string TableName, string ColumnName)
        => $"ALTER TABLE {QuoteName(TableName)} ALTER COLUMN {QuoteName(ColumnName)} DROP DEFAULT";
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
    /// Builds DROP INDEX SQL. PostgreSQL identifies an index by its own name.
    /// </summary>
    public override string DropIndexSql(string TableName, string IndexName)
        => $"DROP INDEX {QuoteName(IndexName)}";

    // ● capabilities
    /// <summary>
    /// Gets whether PostgreSQL can create databases.
    /// </summary>
    public override bool CanCreateDatabases => true;
    /// <summary>
    /// Gets whether PostgreSQL supports named sequences.
    /// </summary>
    public override bool SupportsGenerators => true;
    /// <summary>
    /// Gets the direct column and constraint alterations supported by PostgreSQL.
    /// </summary>
    public override AlterTableType SupportedAlterTableTypes => AlterTableType.All;

    // ● SQL type mappings
    /// <summary>
    /// Gets the PostgreSQL declaration for an auto-incrementing integer primary key.
    /// </summary>
    public override string PrimaryKeySql => "serial not null primary key";
    /// <summary>
    /// Gets the PostgreSQL declaration for an auto-incrementing integer field.
    /// </summary>
    public override string AutoIncSql => "serial";
    /// <summary>
    /// Gets the PostgreSQL variable-length character type for Unicode text.
    /// </summary>
    public override string NVarcharSql => "varchar";
    /// <summary>
    /// Gets the PostgreSQL double-precision floating-point type.
    /// </summary>
    public override string FloatSql => "double precision";
    /// <summary>
    /// Gets the PostgreSQL variable-length binary type.
    /// </summary>
    public override string BlobSql => "bytea";
    /// <summary>
    /// Gets the PostgreSQL nullable column clause.
    /// </summary>
    public override string NullSql => " ";

    // ● properties
    /// <summary>
    /// Gets the database server type implemented by this provider.
    /// </summary>
    public override DbServerType ServerType => DbServerType.PostgreSql;
    /// <summary>
    /// Gets the display description for the PostgreSQL provider.
    /// </summary>
    public override string Description => "PostgreSQL";
    /// <summary>
    /// Gets the Npgsql ADO.NET factory.
    /// </summary>
    public override DbProviderFactory Factory => NpgsqlFactory.Instance;
    /// <summary>
    /// Gets the PostgreSQL template in host, database, user name and password order.
    /// </summary>
    public override string ConnectionStringTemplate => "Server={0}; Database={1}; User Id={2}; Password={3};";
    /// <summary>
    /// Gets the customary PostgreSQL administrative user name.
    /// </summary>
    public override string SuperUser => "postgres";
    /// <summary>
    /// PostgreSQL does not supply a default password for its administrative user.
    /// </summary>
    public override string SuperUserPassword => "";
    /// <summary>
    /// Gets the Npgsql prefix used for named parameters.
    /// </summary>
    public override string NativePrefix => "@";
    /// <summary>
    /// Gets SQL that resolves a table name using the PostgreSQL search path.
    /// </summary>
    public override string TableExistsSql =>
        "SELECT relname FROM pg_catalog.pg_class WHERE oid = pg_catalog.to_regclass(:TableName) AND relkind IN ('r', 'p', 'f')";
    /// <summary>
    /// Gets SQL that resolves a view name using the PostgreSQL search path.
    /// </summary>
    public override string ViewExistsSql =>
        "SELECT relname FROM pg_catalog.pg_class WHERE oid = pg_catalog.to_regclass(:ViewName) AND relkind IN ('v', 'm')";
}
