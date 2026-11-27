/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

using System.Globalization;
using FirebirdSql.Data.FirebirdClient;

namespace Tripous.Data;

/// <summary>
/// Provides Firebird connectivity, identity keys and named generators.
/// </summary>
/// <remarks>
/// Targets Firebird 4 or later. Use regular, unquoted identifiers in schema definitions
/// and application SQL. Firebird stores these identifiers in uppercase.
/// Integer keys posted by TableSet require a database-generated value.
/// </remarks>
public class SqlProviderFirebird : SqlProvider
{
    // ● private fields
    const int fOpenFileError = 335544734;
    const int fCreateFileError = 335544733;
    const int fDatabaseOrFileExists = 335544646;
    const string fGeneratorExistsSql = @"
        SELECT 1 FROM RDB$GENERATORS WHERE RDB$GENERATOR_NAME = :GeneratorName";

    // ● private
    /// <summary>
    /// Creates a store for Firebird generator operations.
    /// </summary>
    SqlStore CreateStore(string ConnectionString)
        => new(new DbConnectionInfo { ConnectionString = ConnectionString, DbServerType = ServerType });

    // ● protected
    /// <summary>
    /// Uses read consistency and waits for a conflicting lock when ReadCommitted is requested.
    /// </summary>
    protected override DbTransaction BeginDbTransaction(DbConnection Connection, IsolationLevel Level)
    {
        if (Level != IsolationLevel.ReadCommitted)
            return base.BeginDbTransaction(Connection, Level);

        return ((FbConnection)Connection).BeginTransaction(new FbTransactionOptions
        {
            TransactionBehavior = FbTransactionBehavior.ReadCommitted | FbTransactionBehavior.ReadConsistency
                | FbTransactionBehavior.Wait | FbTransactionBehavior.Write,
        });
    }
    /// <summary>
    /// Adds UTF-8 explicitly so the column uses Unicode regardless of the database default character set.
    /// </summary>
    protected override string NVarcharWithLengthSql(string LengthClause)
        => base.NVarcharWithLengthSql(LengthClause) + " CHARACTER SET UTF8";

    // ● constructor
    internal SqlProviderFirebird()
    {
    }

    // ● metadata
    /// <summary>
    /// Maps Firebird column types to field types supported by the item tree.
    /// Tripous represents boolean fields with database integers.
    /// </summary>
    internal override DataFieldType GetMetadataFieldType(DbMetaColumn Column)
        => Column.DataType switch
        {
            "SMALLINT" or "INTEGER" => DataFieldType.Integer,
            "INT64" or "BIGINT" => DataFieldType.Int64,
            "CHAR" or "VARCHAR" => DataFieldType.String,
            "FLOAT" or "DOUBLE" or "D_FLOAT" => DataFieldType.Double,
            "NUMERIC" or "DECIMAL" when Column.Precision <= 28 => DataFieldType.Decimal,
            "DATE" => DataFieldType.Date,
            "TIMESTAMP" => DataFieldType.DateTime,
            "BLOB" when Column.DataSubType == "TEXT" => DataFieldType.TextBlob,
            "BLOB" when Column.DataSubType == "BINARY" => DataFieldType.Blob,
            _ => throw new NotSupportedException($"Column '{Column.Name}' uses Firebird type '{Column.DataType}', which has no inferred field type."),
        };

    // ● connection
    /// <summary>
    /// Builds a Firebird connection string using UTF-8.
    /// </summary>
    public override string CreateConnectionString(string Server, string Database, string UserName, string Password)
    {
        FbConnectionStringBuilder Builder = new()
        {
            DataSource = Server,
            Database = Database,
            UserID = UserName,
            Password = Password,
            Charset = "UTF8",
        };
        return Builder.ConnectionString;
    }
    /// <summary>
    /// Gets the database name or alias declared in a Firebird connection string.
    /// </summary>
    public override string GetDatabaseName(string ConnectionString)
    {
        string Database = ((FbConnectionStringBuilder)CreateConnectionStringBuilder(ConnectionString)).Database;
        return string.IsNullOrWhiteSpace(Database) ? string.Empty : Database;
    }
    /// <summary>
    /// Creates an empty Firebird database and waits for it to accept connections.
    /// Returns false when the supplied settings already open a database.
    /// The database path or alias is interpreted by Firebird.
    /// </summary>
    public override bool CreateDatabase(string ConnectionString)
    {
        string Database = GetDatabaseName(ConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(Database, nameof(ConnectionString));
        try
        {
            using (DbConnection Existing = OpenConnection(ConnectionString))
            {
                return false;
            }
        }
        catch (FbException Error) when (Error.Errors.Cast<FbError>().Any(Item => Item.Number == fOpenFileError))
        {
            // A file-open error can also mean access denied. Creation must never overwrite a file.
        }

        try
        {
            FbConnection.CreateDatabase(ConnectionString, pageSize: 32768, forcedWrites: true, overwrite: false);
        }
        catch (FbException Error) when (
            Error.Errors.Cast<FbError>().Any(Item => Item.Number is fCreateFileError or fDatabaseOrFileExists)
            && CanConnect(ConnectionString))
        {
            return false;
        }
        if (!WaitUntilDatabaseReady(ConnectionString))
            throw new InvalidOperationException($"Firebird database '{Database}' was created, but it did not accept a connection.");
        return true;
    }

    // ● locking
    /// <summary>
    /// Returns a SELECT statement that locks an existing row for update in the caller's transaction.
    /// </summary>
    public override string SelectForUpdateSql(string TableName, string FieldName)
    {
        DataTableDef.ValidateIdentifier(TableName);
        DataTableDef.ValidateIdentifier(FieldName);
        return $"SELECT * FROM {TableName} WHERE {FieldName} = {GlobalPrefix}{FieldName} WITH LOCK";
    }

    // ● list queries
    /// <summary>
    /// Applies the framework's row limit to the complete list SELECT.
    /// </summary>
    public override string ApplyRowLimit(string SqlText, int RowLimit)
        => SqlTextScanner.ApplyLimit(SqlText, RowLimit, ServerType);

    // ● ID generation
    /// <summary>
    /// Returns the integer key of the inserted row using INSERT RETURNING.
    /// </summary>
    public override long InsertAndGetId(SqlStore Store, DbTransaction Transaction, string SqlText, SqlParams Params, string TableName, string KeyField)
    {
        ArgumentNullException.ThrowIfNull(Store);
        ArgumentException.ThrowIfNullOrWhiteSpace(TableName);
        DataTableDef.ValidateIdentifier(KeyField);
        string ReturningSql = $"{SqlTextScanner.PrepareForAppend(SqlText)}RETURNING {KeyField}";
        object Id = Store.ExecuteScalar(Transaction, ReturningSql, Params);
        if (Id is null or DBNull)
            throw new InvalidOperationException($"Insert did not return a generated key for table '{TableName}'.");
        return Convert.ToInt64(Id, CultureInfo.InvariantCulture);
    }
    /// <summary>
    /// Returns whether the named generator exists in the database.
    /// </summary>
    public override bool GeneratorExists(string ConnectionString, string GeneratorName)
    {
        DataTableDef.ValidateIdentifier(GeneratorName);
        SqlParams Params = new SqlParams().Add("GeneratorName", GeneratorName.ToUpperInvariant());
        return CreateStore(ConnectionString).SelectResult(fGeneratorExistsSql, null, Params) != null;
    }
    /// <summary>
    /// Creates the named generator if it does not exist.
    /// </summary>
    public override void CreateGenerator(string ConnectionString, string GeneratorName)
    {
        DataTableDef.ValidateIdentifier(GeneratorName);
        SqlStore Store = CreateStore(ConnectionString);
        SqlParams Params = new SqlParams().Add("GeneratorName", GeneratorName.ToUpperInvariant());
        Store.ExecSql(Transaction =>
        {
            if (Store.SelectResult(Transaction, fGeneratorExistsSql, null, Params) == null)
                Store.ExecSql(Transaction, $"CREATE SEQUENCE {GeneratorName}");
        });
    }
    /// <summary>
    /// Sets the stored value of a named generator. This change survives a rollback.
    /// </summary>
    public override void SetGeneratorTo(string ConnectionString, string GeneratorName, long Value)
    {
        DataTableDef.ValidateIdentifier(GeneratorName);
        CreateStore(ConnectionString).ExecSql(FormattableString.Invariant($"SET GENERATOR {GeneratorName} TO {Value}"));
    }
    /// <summary>
    /// Returns SQL that increments the named generator and reads its new value.
    /// </summary>
    public override string NextIdByGeneratorSql(string GeneratorName)
    {
        DataTableDef.ValidateIdentifier(GeneratorName);
        return $"SELECT GEN_ID({GeneratorName}, 1) FROM RDB$DATABASE";
    }
    /// <summary>
    /// Sets the generator for a single-column identity primary key or the conventional G_TableName generator.
    /// </summary>
    public override void SetTableGeneratorTo(string ConnectionString, string TableName, long Value)
    {
        DataTableDef.ValidateIdentifier(TableName);
        SqlStore Store = CreateStore(ConnectionString);
        const string IdentitySql = @"
            SELECT TRIM(F.RDB$FIELD_NAME)
            FROM RDB$RELATION_CONSTRAINTS C
            JOIN RDB$INDEX_SEGMENTS S ON S.RDB$INDEX_NAME = C.RDB$INDEX_NAME
            JOIN RDB$RELATION_FIELDS F ON F.RDB$RELATION_NAME = C.RDB$RELATION_NAME
                AND F.RDB$FIELD_NAME = S.RDB$FIELD_NAME
            WHERE C.RDB$RELATION_NAME = :TableName AND C.RDB$CONSTRAINT_TYPE = 'PRIMARY KEY'
                AND F.RDB$IDENTITY_TYPE IS NOT NULL
                AND (SELECT COUNT(*) FROM RDB$INDEX_SEGMENTS K WHERE K.RDB$INDEX_NAME = C.RDB$INDEX_NAME) = 1";
        SqlParams Params = new SqlParams().Add("TableName", TableName.ToUpperInvariant());
        Store.ExecSql(Transaction =>
        {
            if (Store.SelectResult(Transaction, IdentitySql, null, Params) is string FieldName)
            {
                long NextValue = checked(Value + 1);
                Store.ExecSql(Transaction, FormattableString.Invariant($"ALTER TABLE {TableName} ALTER COLUMN {QuoteName(FieldName)} RESTART WITH {NextValue}"));
            }
            else
            {
                Store.ExecSql(Transaction, FormattableString.Invariant($"SET GENERATOR G_{TableName} TO {Value}"));
            }
        });
    }

    // ● table schema changes
    /// <summary>
    /// Quotes a regular Firebird identifier using its uppercase catalog spelling.
    /// </summary>
    public override string QuoteName(string Name)
    {
        DataTableDef.ValidateIdentifier(Name);
        return base.QuoteName(Name.ToUpperInvariant());
    }
    /// <summary>
    /// Builds an ALTER TABLE statement that renames an existing column.
    /// </summary>
    public override string RenameColumnSql(string TableName, string ColumnName, string NewColumnName)
        => $"ALTER TABLE {QuoteName(TableName)} ALTER COLUMN {QuoteName(ColumnName)} TO {QuoteName(NewColumnName)}";
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
        return base.AddColumnSql(TableName, ReplaceDataTypePlaceholders(ColumnDef).TrimEnd());
    }
    /// <summary>
    /// Builds SQL to change a column's declared type and length using the Firebird TYPE clause.
    /// </summary>
    public override string SetColumnLengthSql(string TableName, string ColumnName, string DataType, string Required, string DefaultExpression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DataType);
        return $"ALTER TABLE {QuoteName(TableName)} ALTER COLUMN {QuoteName(ColumnName)} TYPE {ReplaceDataTypePlaceholders(DataType)}";
    }
    /// <summary>
    /// Builds SQL to add NOT NULL. Fill existing NULL values first.
    /// </summary>
    public override string SetNotNullSql(string TableName, string ColumnName, string DataType)
        => $"ALTER TABLE {QuoteName(TableName)} ALTER COLUMN {QuoteName(ColumnName)} SET NOT NULL";
    /// <summary>
    /// Builds SQL to remove a column's NOT NULL requirement.
    /// </summary>
    public override string DropNotNullSql(string TableName, string ColumnName, string DataType)
        => $"ALTER TABLE {QuoteName(TableName)} ALTER COLUMN {QuoteName(ColumnName)} DROP NOT NULL";
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
    /// Builds DROP INDEX SQL using the index name.
    /// </summary>
    public override string DropIndexSql(string TableName, string IndexName) => $"DROP INDEX {QuoteName(IndexName)}";

    // ● properties
    /// <summary>
    /// Gets the database server type implemented by this provider.
    /// </summary>
    public override DbServerType ServerType => DbServerType.Firebird;
    /// <summary>
    /// Gets the Firebird ADO.NET factory.
    /// </summary>
    public override DbProviderFactory Factory => FirebirdClientFactory.Instance;
    /// <summary>
    /// Gets the template in server, database, user name and password order.
    /// </summary>
    public override string ConnectionStringTemplate => "DataSource={0}; Database={1}; User={2}; Password={3}; Charset=UTF8;";
    /// <summary>
    /// Gets the customary Firebird administrative user name.
    /// </summary>
    public override string SuperUser => "SYSDBA";
    /// <summary>
    /// Gets the driver prefix for named parameters.
    /// </summary>
    public override string NativePrefix => "@";
    /// <summary>
    /// Gets the ID mode used for integer keys in the item tree.
    /// </summary>
    public override OidMode OidMode => OidMode.AutoInc;
    /// <summary>
    /// Gets the direct column and constraint alterations supported by Firebird.
    /// </summary>
    public override AlterTableType SupportedAlterTableTypes => AlterTableType.All;
    /// <summary>
    /// Gets whether Firebird supports named generators.
    /// </summary>
    public override bool SupportsGenerators => true;
    /// <summary>
    /// Gets whether Firebird can create databases using the supplied connection settings.
    /// </summary>
    public override bool CanCreateDatabases => true;
    /// <summary>
    /// Gets the Firebird query for the current timestamp with an explicit UTC offset.
    /// </summary>
    public override string ServerDateTimeSql
        => "SELECT CAST(CURRENT_TIMESTAMP AT TIME ZONE '+00:00' AS VARCHAR(40)) FROM RDB$DATABASE";
    /// <summary>
    /// Gets SQL for finding an unquoted Firebird table name.
    /// </summary>
    public override string TableExistsSql => @"
        SELECT RDB$RELATION_NAME FROM RDB$RELATIONS
        WHERE RDB$RELATION_NAME = UPPER(:TableName) AND RDB$VIEW_BLR IS NULL";
    /// <summary>
    /// Gets SQL for finding an unquoted Firebird view name.
    /// </summary>
    public override string ViewExistsSql => @"
        SELECT RDB$RELATION_NAME FROM RDB$RELATIONS
        WHERE RDB$RELATION_NAME = UPPER(:ViewName) AND RDB$VIEW_BLR IS NOT NULL";

    // ● SQL type mappings
    /// <summary>
    /// Gets the declaration for a generated integer identity column.
    /// </summary>
    public override string AutoIncSql => "integer generated by default as identity";
    /// <summary>
    /// Gets the Firebird type name used for variable-length Unicode text.
    /// </summary>
    public override string NVarcharSql => "varchar";
    /// <summary>
    /// Gets the double-precision floating-point type.
    /// </summary>
    public override string FloatSql => "double precision";
    /// <summary>
    /// Gets the integer-backed Boolean type.
    /// </summary>
    public override string BoolSql => "smallint";
    /// <summary>
    /// Gets the large text type.
    /// </summary>
    public override string BlobTextSql => "blob sub_type text";
    /// <summary>
    /// Gets the Unicode large text type for a UTF-8 connection.
    /// </summary>
    public override string NBlobTextSql => BlobTextSql;
    /// <summary>
    /// Gets the nullable column clause. Firebird columns are nullable by default.
    /// </summary>
    public override string NullSql => "";
}
