/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Provides Oracle connectivity and Oracle SQL syntax.
/// </summary>
public class SqlProviderOracle : SqlProvider
{
    // ● constructor
    internal SqlProviderOracle()
    {
    }

    // ● metadata
    /// <summary>
    /// Maps Oracle column metadata to the portable item field types.
    /// NUMBER(19,0) follows the Tripous Int64 declaration and values must fit Int64.
    /// Oracle DATE includes a time of day.
    /// </summary>
    internal override DataFieldType GetMetadataFieldType(DbMetaColumn Column)
    {
        string Type = Column.DataType.Trim().ToUpperInvariant();
        if (Type.StartsWith("TIMESTAMP(", StringComparison.Ordinal) && Type.EndsWith(')'))
            Type = "TIMESTAMP";

        return Type switch
        {
            "NUMBER" when Column.Scale == 0 && Column.Precision is >= 1 and <= 9 => DataFieldType.Integer,
            "NUMBER" when Column.Scale == 0 && Column.Precision is >= 10 and <= 19 => DataFieldType.Int64,
            "NUMBER" when Column.Precision is >= 1 and <= 28 && Column.Scale is >= 0 and <= 28 => DataFieldType.Decimal,
            "BINARY_FLOAT" or "BINARY_DOUBLE" => DataFieldType.Double,
            "CHAR" or "NCHAR" or "VARCHAR2" or "NVARCHAR2" => DataFieldType.String,
            "CLOB" or "NCLOB" => DataFieldType.TextBlob,
            "RAW" or "BLOB" => DataFieldType.Blob,
            "DATE" or "TIMESTAMP" => DataFieldType.DateTime,
            _ => throw new NotSupportedException($"Column '{Column.Name}' uses Oracle type '{Column.DataType}' (precision {Column.Precision}, scale {Column.Scale}), which has no inferred field type."),
        };
    }

    // ● connection
    /// <summary>
    /// Builds a connection string from an Oracle data source or a host and service name.
    /// </summary>
    public override string CreateConnectionString(string Server, string Database, string UserName, string Password)
    {
        OracleConnectionStringBuilder Builder = new()
        {
            DataSource = string.IsNullOrWhiteSpace(Database) ? Server : $"//{Server}/{Database}",
            UserID = UserName,
            Password = Password,
        };
        return Builder.ConnectionString;
    }
    /// <summary>
    /// Prepares commands to bind repeated SQL parameters by name.
    /// </summary>
    public override void PrepareCommand(DbCommand Command, string SqlText, SqlParams Params)
    {
        base.PrepareCommand(Command, SqlText, Params);
        if (Command is OracleCommand OracleCommand)
        {
            OracleCommand.BindByName = true;
            // Dictionary views expose some definitions as LONG without a row identifier.
            OracleCommand.InitialLONGFetchSize = -1;
        }
    }

    // ● locking
    /// <summary>
    /// Builds a SELECT that locks an existing row until the transaction ends.
    /// Tripous schema names are created as unquoted Oracle identifiers.
    /// </summary>
    public override string SelectForUpdateSql(string TableName, string FieldName)
    {
        DataTableDef.ValidateIdentifier(TableName);
        DataTableDef.ValidateIdentifier(FieldName);
        return $"SELECT * FROM {QuoteName(TableName)} WHERE {QuoteName(FieldName)} = {GlobalPrefix}{FieldName} FOR UPDATE";
    }

    // ● sequences
    /// <summary>
    /// Checks whether a sequence exists in the current Oracle schema.
    /// </summary>
    public override bool GeneratorExists(string ConnectionString, string GeneratorName)
    {
        DataTableDef.ValidateIdentifier(GeneratorName);
        const string Sql = "SELECT 1 FROM ALL_SEQUENCES WHERE SEQUENCE_OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND SEQUENCE_NAME = UPPER(:Name)";
        SqlStore Store = new(new DbConnectionInfo { ConnectionString = ConnectionString, DbServerType = ServerType });
        return Store.SelectResult(Sql, null, new SqlParams().Add("Name", GeneratorName)) != null;
    }
    /// <summary>
    /// Creates an ascending sequence if it does not already exist.
    /// </summary>
    public override void CreateGenerator(string ConnectionString, string GeneratorName)
    {
        if (GeneratorExists(ConnectionString, GeneratorName))
            return;

        try
        {
            ExecuteSequenceDdl(ConnectionString, $"CREATE SEQUENCE {QuoteName(GeneratorName)}");
        }
        catch (OracleException Error) when (Error.Number == 955 && GeneratorExists(ConnectionString, GeneratorName))
        {
            // Another connection created the sequence.
        }
    }
    /// <summary>
    /// Restarts a sequence so its next value is Value plus one.
    /// Oracle commits sequence DDL implicitly. Restarting can reuse existing keys.
    /// </summary>
    public override void SetGeneratorTo(string ConnectionString, string GeneratorName, long Value)
    {
        DataTableDef.ValidateIdentifier(GeneratorName);
        long NextValue = checked(Value + 1);
        string Sql = FormattableString.Invariant($"ALTER SEQUENCE {QuoteName(GeneratorName)} RESTART START WITH {NextValue}");
        ExecuteSequenceDdl(ConnectionString, Sql);
    }
    /// <summary>
    /// Resets the existing G_TableName sequence used to generate integer keys.
    /// </summary>
    public override void SetTableGeneratorTo(string ConnectionString, string TableName, long Value)
    {
        DataTableDef.ValidateIdentifier(TableName);
        string GeneratorName = "G_" + TableName;
        if (GeneratorExists(ConnectionString, GeneratorName))
            SetGeneratorTo(ConnectionString, GeneratorName, Value);
    }
    /// <summary>
    /// Gets SQL that allocates the next sequence value in the caller's session.
    /// </summary>
    public override string NextIdByGeneratorSql(string GeneratorName)
    {
        DataTableDef.ValidateIdentifier(GeneratorName);
        return $"SELECT {QuoteName(GeneratorName)}.NEXTVAL FROM DUAL";
    }
    /// <summary>
    /// Runs sequence DDL without enlisting in an ambient transaction.
    /// </summary>
    void ExecuteSequenceDdl(string ConnectionString, string Sql)
    {
        var Builder = (OracleConnectionStringBuilder)CreateConnectionStringBuilder(ConnectionString);
        Builder.Enlist = "false";
        using (DbConnection Connection = OpenConnection(Builder.ConnectionString))
        using (DbCommand Command = CreateCommand(Connection, Sql, new SqlParams()))
        {
            Command.ExecuteNonQuery();
        }
    }

    // ● list queries
    /// <summary>
    /// Limits the complete result of an Oracle list query.
    /// </summary>
    public override string ApplyRowLimit(string SqlText, int RowLimit)
        => SqlTextScanner.ApplyLimit(SqlText, RowLimit, ServerType);

    // ● identifiers
    /// <summary>
    /// Quotes an identifier using the uppercase form of unquoted Oracle schema names.
    /// </summary>
    public override string QuoteName(string Name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        return base.QuoteName(Name.ToUpperInvariant());
    }

    // ● table schema changes
    /// <summary>
    /// Builds SQL to rename a column in an existing table.
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
        return base.AddColumnSql(TableName, ReplaceDataTypePlaceholders(ColumnDef));
    }
    /// <summary>
    /// Builds SQL to drop a column from an existing table.
    /// </summary>
    public override string DropColumnSql(string TableName, string ColumnName)
        => $"ALTER TABLE {QuoteName(TableName)} DROP COLUMN {QuoteName(ColumnName)}";
    /// <summary>
    /// Builds SQL to change the declared length while retaining nullability and defaults.
    /// </summary>
    public override string SetColumnLengthSql(string TableName, string ColumnName, string DataType, string Required, string DefaultExpression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DataType);
        return ModifyColumnSql(TableName, ColumnName, ReplaceDataTypePlaceholders(DataType));
    }
    /// <summary>
    /// Builds SQL to require a value after any existing NULL values have been filled.
    /// </summary>
    public override string SetNotNullSql(string TableName, string ColumnName, string DataType)
        => ModifyColumnSql(TableName, ColumnName, "NOT NULL");
    /// <summary>
    /// Builds SQL to make a column nullable.
    /// </summary>
    public override string DropNotNullSql(string TableName, string ColumnName, string DataType)
        => ModifyColumnSql(TableName, ColumnName, "NULL");
    /// <summary>
    /// Builds SQL to set the default for future inserts.
    /// </summary>
    public override string SetColumnDefaultSql(string TableName, string ColumnName, string DefaultExpression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DefaultExpression);
        return ModifyColumnSql(TableName, ColumnName, $"DEFAULT {DefaultExpression}");
    }
    /// <summary>
    /// Builds SQL to clear a column default.
    /// </summary>
    public override string DropColumnDefaultSql(string TableName, string ColumnName)
        => ModifyColumnSql(TableName, ColumnName, "DEFAULT NULL");
    /// <summary>
    /// Builds a MODIFY clause for one column.
    /// </summary>
    string ModifyColumnSql(string TableName, string ColumnName, string Definition)
        => $"ALTER TABLE {QuoteName(TableName)} MODIFY ({QuoteName(ColumnName)} {Definition})";
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
    /// Builds DROP INDEX SQL. Oracle identifies an index by its own name.
    /// </summary>
    public override string DropIndexSql(string TableName, string IndexName)
        => $"DROP INDEX {QuoteName(IndexName)}";

    // ● SQL literals
    /// <summary>
    /// Formats an Oracle date literal using an explicit format mask.
    /// </summary>
    public override string QSDate(DateTime Value)
        => FormattableString.Invariant($"TO_DATE('{Value:yyyy-MM-dd}', 'YYYY-MM-DD')");
    /// <summary>
    /// Formats an Oracle date and time literal using an explicit format mask.
    /// </summary>
    public override string QSDateTime(DateTime Value)
        => FormattableString.Invariant($"TO_DATE('{Value:yyyy-MM-dd HH:mm:ss}', 'YYYY-MM-DD HH24:MI:SS')");

    // ● properties
    /// <summary>
    /// Gets the database server type implemented by this provider.
    /// </summary>
    public override DbServerType ServerType => DbServerType.Oracle;
    /// <summary>
    /// Gets the provider's display name.
    /// </summary>
    public override string Description => "Oracle";
    /// <summary>
    /// Gets the managed ODP.NET factory.
    /// </summary>
    public override DbProviderFactory Factory => OracleClientFactory.Instance;
    /// <summary>
    /// Gets the template in host, service name, user name and password order.
    /// </summary>
    public override string ConnectionStringTemplate => "Data Source=//{0}/{1}; User Id={2}; Password={3};";
    /// <summary>
    /// Gets the prefix used for named parameters.
    /// </summary>
    public override string NativePrefix => ":";
    /// <summary>
    /// Gets SQL that reads the server's current UTC timestamp.
    /// </summary>
    public override string ServerDateTimeSql => "SELECT SYS_EXTRACT_UTC(SYSTIMESTAMP) FROM DUAL";
    /// <summary>
    /// Gets SQL that finds an unquoted table name in the current Oracle schema.
    /// </summary>
    public override string TableExistsSql => "SELECT 1 FROM ALL_TABLES WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND TABLE_NAME = UPPER(:TableName)";
    /// <summary>
    /// Gets SQL that finds an unquoted view name in the current Oracle schema.
    /// </summary>
    public override string ViewExistsSql => "SELECT 1 FROM ALL_VIEWS WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND VIEW_NAME = UPPER(:ViewName)";
    /// <summary>
    /// Gets the key mode used for integer keys.
    /// </summary>
    public override OidMode OidMode => OidMode.Generator;
    /// <summary>
    /// Gets whether this provider uses identity columns for generated keys.
    /// </summary>
    public override bool SupportsAutoIncFields => false;
    /// <summary>
    /// Gets whether this provider supports named Oracle sequences.
    /// </summary>
    public override bool SupportsGenerators => true;
    /// <summary>
    /// Gets the directly supported table alteration operations.
    /// </summary>
    public override AlterTableType SupportedAlterTableTypes => AlterTableType.All;

    // ● SQL type mappings
    /// <summary>
    /// Gets the Oracle variable-length text type.
    /// </summary>
    public override string VarcharSql => "varchar2";
    /// <summary>
    /// Gets the Oracle variable-length national character type.
    /// </summary>
    public override string NVarcharSql => "nvarchar2";
    /// <summary>
    /// Gets the Oracle type used for signed 64-bit integer values.
    /// </summary>
    public override string Int64Sql => "number(19,0)";
    /// <summary>
    /// Gets the Oracle large binary type.
    /// </summary>
    public override string BlobSql => "blob";
    /// <summary>
    /// Gets the Oracle large character type.
    /// </summary>
    public override string BlobTextSql => "clob";
    /// <summary>
    /// Gets the Oracle large national character type.
    /// </summary>
    public override string NBlobTextSql => "nclob";
    /// <summary>
    /// Gets the Oracle identity declaration for explicit schema definitions.
    /// </summary>
    public override string AutoIncSql => "number(19,0) generated by default as identity";
}
