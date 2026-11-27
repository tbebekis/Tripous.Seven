/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

using System.Globalization;

namespace Tripous.Data;

/// <summary>
/// Provides driver-specific database operations and prepares SQL commands.
/// <para><strong>NOTE: </strong>Every public method or property is virtual or abstract.
/// Callers use the same SqlProvider API for every database. Derived providers
/// override operations that need driver-specific behavior, so callers can work
/// with common ADO.NET types without casting to a concrete provider.</para>
/// </summary>
public abstract class SqlProvider
{
    /// <summary>
    /// Maps a native column declaration when an item field has no explicit type.
    /// </summary>
    internal virtual DataFieldType GetMetadataFieldType(DbMetaColumn Column)
        => throw new NotSupportedException($"Cannot infer a field type for column '{Column.Name}' using {GetType().Name}.");

    // ● transaction creation
    /// <summary>
    /// Opens an owning context using the requested isolation level.
    /// </summary>
    SqlTransactionContext BeginTransactionContext(string ConnectionString, IsolationLevel Level)
    {
        SqlTransactionContext Context = new(CreateConnection(ConnectionString));
        try
        {
            Context.BeginTransaction(Connection => BeginDbTransaction(Connection, Level));
            return Context;
        }
        catch
        {
            Context.Dispose();
            throw;
        }
    }
    /// <summary>
    /// Starts a transaction on an open connection using the requested isolation level.
    /// An unspecified level uses the driver's default.
    /// </summary>
    protected virtual DbTransaction BeginDbTransaction(DbConnection Connection, IsolationLevel Level)
        => Level == IsolationLevel.Unspecified ? Connection.BeginTransaction() : Connection.BeginTransaction(Level);

    // ● public
    /// <summary>
    /// The common prefix used for named parameters in SQL text.
    /// </summary>
    public const char GlobalPrefix = ':';
    /// <summary>
    /// Creates a connection for the specified connection string.
    /// The caller owns the returned connection.
    /// </summary>
    public virtual DbConnection CreateConnection(string ConnectionString)
    {
        string Normalized = NormalizeConnectionString(ConnectionString);
        DbConnection Connection = Factory.CreateConnection()
            ?? throw new NotSupportedException($"The {Name} factory does not create connections.");
        try
        {
            Connection.ConnectionString = Normalized;
            return Connection;
        }
        catch
        {
            Connection.Dispose();
            throw;
        }
    }
    /// <summary>
    /// Opens and closes a connection to verify that the database can be reached.
    /// Set ThrowIfNot to propagate the connection error.
    /// </summary>
    public virtual bool CanConnect(string ConnectionString, bool ThrowIfNot = false)
    {
        try
        {
            using (DbConnection Connection = OpenConnection(GetConnectionStringForCheck(ConnectionString)))
            {
                return true;
            }
        }
        catch (Exception) when (!ThrowIfNot)
        {
            return false;
        }
    }
    /// <summary>
    /// Tests the connection described by the supplied settings.
    /// Set ThrowIfNot to propagate the connection error.
    /// </summary>
    public virtual bool CanConnect(DbConnectionInfo ConnectionInfo, bool ThrowIfNot = false)
    {
        ArgumentNullException.ThrowIfNull(ConnectionInfo);
        return CanConnect(ConnectionInfo.ConnectionString, ThrowIfNot);
    }
    /// <summary>
    /// Checks whether the database accepts connections, retrying up to the supplied attempt count.
    /// </summary>
    public virtual bool WaitUntilDatabaseReady(string ConnectionString, int RetryCount = 10, int DelayMilliseconds = 1000)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(RetryCount);
        ArgumentOutOfRangeException.ThrowIfNegative(DelayMilliseconds);
        for (int Attempt = 0; Attempt < RetryCount; Attempt++)
        {
            if (CanConnect(ConnectionString))
                return true;
            if (Attempt + 1 < RetryCount)
                Thread.Sleep(DelayMilliseconds);
        }
        return false;
    }
    /// <summary>
    /// Verifies the connection and propagates any error.
    /// </summary>
    public virtual void EnsureConnection(string ConnectionString) => CanConnect(ConnectionString, ThrowIfNot: true);
    /// <summary>
    /// Verifies the supplied connection settings and propagates any connection error.
    /// </summary>
    public virtual void EnsureConnection(DbConnectionInfo ConnectionInfo) => CanConnect(ConnectionInfo, ThrowIfNot: true);
    /// <summary>
    /// Opens a connection using the supplied settings. The caller owns the returned connection.
    /// </summary>
    public virtual DbConnection OpenConnection(DbConnectionInfo ConnectionInfo)
    {
        ArgumentNullException.ThrowIfNull(ConnectionInfo);
        return OpenConnection(ConnectionInfo.ConnectionString);
    }
    /// <summary>
    /// Opens a connection and disposes it if opening fails.
    /// The caller owns the returned connection.
    /// </summary>
    public virtual DbConnection OpenConnection(string ConnectionString)
    {
        DbConnection Connection = CreateConnection(ConnectionString);
        try
        {
            Connection.Open();
            return Connection;
        }
        catch
        {
            Connection.Dispose();
            throw;
        }
    }
   
    /// <summary>
    /// Returns the database engine version using the supplied connection settings.
    /// </summary>
    public virtual string GetServerVersion(DbConnectionInfo ConnectionInfo)
    {
        ArgumentNullException.ThrowIfNull(ConnectionInfo);
        return GetServerVersion(ConnectionInfo.ConnectionString);
    }
    /// <summary>
    /// Reads the database engine version reported by an open connection.
    /// Uses the same connection settings as a connection check.
    /// </summary>
    public virtual string GetServerVersion(string ConnectionString)
    {
        using (DbConnection Connection = OpenConnection(GetConnectionStringForCheck(ConnectionString)))
        {
            return Connection.ServerVersion;
        }
    }

    /// <summary>
    /// Starts a transaction for the supplied connection settings.
    /// The caller owns the returned context, which owns both resources.
    /// </summary>
    public virtual SqlTransactionContext BeginTransactionContext(DbConnectionInfo ConnectionInfo)
    {
        ArgumentNullException.ThrowIfNull(ConnectionInfo);
        IsolationLevel Level = ConnectionInfo.TransactionIsolationLevel;
        return Level == IsolationLevel.Unspecified
            ? BeginTransactionContext(ConnectionInfo.ConnectionString)
            : BeginTransactionContext(ConnectionInfo.ConnectionString, Level);
    }
    /// <summary>
    /// Starts a transaction and returns its owning context.
    /// If opening the connection or starting the transaction fails, disposes the context.
    /// </summary>
    public virtual SqlTransactionContext BeginTransactionContext(string ConnectionString)
        => BeginTransactionContext(ConnectionString, DbConfig.DefaultTransactionIsolationLevel);

    /// <summary>
    /// Returns the connection string to use when testing a connection.
    /// </summary>
    protected virtual string GetConnectionStringForCheck(string ConnectionString) => ConnectionString;
    /// <summary>
    /// Builds a connection string from the provider's database and connection settings.
    /// </summary>
    public virtual string CreateConnectionString(string Server, string Database, string UserName, string Password)
        => throw new NotSupportedException("This provider does not build connection strings.");

    /// <summary>
    /// Creates a provider-specific builder from the supplied connection string.
    /// </summary>
    public virtual DbConnectionStringBuilder CreateConnectionStringBuilder(string ConnectionString)
    {
        DbConnectionStringBuilder Builder = Factory.CreateConnectionStringBuilder()
            ?? throw new NotSupportedException("This provider does not supply a connection string builder.");
        Builder.ConnectionString = ConnectionString;
        return Builder;
    }
    /// <summary>
    /// Gets the database name declared in a connection string without opening a connection.
    /// Returns an empty string when no database name is specified.
    /// </summary>
    public virtual string GetDatabaseName(string ConnectionString) => string.Empty;
    /// <summary>
    /// Resolves provider-specific settings before using a connection string.
    /// </summary>
    public virtual string NormalizeConnectionString(string ConnectionString) => ConnectionString;
   
    /// <summary>
    /// Checks whether the database identified by the connection string exists.
    /// The default implementation tests whether a connection can be opened.
    /// </summary>
    public virtual bool DatabaseExists(string ConnectionString) => CanConnect(ConnectionString);
    /// <summary>
    /// Checks database existence using the supplied connection settings.
    /// </summary>
    public virtual bool DatabaseExists(DbConnectionInfo ConnectionInfo)
    {
        ArgumentNullException.ThrowIfNull(ConnectionInfo);
        return DatabaseExists(ConnectionInfo.ConnectionString);
    }

    /// <summary>
    /// Builds an expression that concatenates the supplied SQL expressions.
    /// </summary>
    public virtual string Concat(params string[] Parts)
        => string.Join(" || ", Parts);
    /// <summary>
    /// Gets a complete SELECT statement that returns the database server's current UTC timestamp.
    /// </summary>
    public virtual string ServerDateTimeSql
        => throw new NotSupportedException("This provider does not define a server date-time query.");
    /// <summary>
    /// Gets the opening delimiter for a quoted database object name.
    /// </summary>
    public virtual string ObjectStartDelimiter => "\"";
    /// <summary>
    /// Gets the closing delimiter for a quoted database object name.
    /// </summary>
    public virtual string ObjectEndDelimiter => "\"";
    /// <summary>
    /// Quotes a single database object name and escapes embedded closing delimiters.
    /// </summary>
    public virtual string QuoteName(string Name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        return ObjectStartDelimiter + Name.Replace(ObjectEndDelimiter, ObjectEndDelimiter + ObjectEndDelimiter) + ObjectEndDelimiter;
    }
    /// <summary>
    /// Quotes and formats a date value for use in a SQL statement.
    /// </summary>
    public virtual string QSDate(DateTime Value)
        => "'" + Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "'";
    /// <summary>
    /// Quotes and formats a date and time value for use in a SQL statement.
    /// Uses whole seconds and preserves the supplied clock time.
    /// </summary>
    public virtual string QSDateTime(DateTime Value)
        => "'" + Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "'";

    // ● locking
    /// <summary>
    /// Returns a SELECT statement for reading an existing row by a unique key
    /// and locking it for update within the caller's transaction.
    /// The key value is bound to a parameter named after the key field.
    /// </summary>
    public virtual string SelectForUpdateSql(string TableName, string FieldName)
        => throw new NotSupportedException("This provider does not support selecting a row for update.");
    /// <summary>
    /// Selects a row with an update lock, increments a non-null integer field,
    /// and returns the row as it was before the increment.
    /// The key field must be unique, and the caller owns the supplied transaction.
    /// </summary>
    public virtual DataRow SelectIncrementLocked(SqlStore Store, DbTransaction Transaction, string TableName, string KeyFieldName, object KeyValue, string ValueFieldName)
    {
        ArgumentNullException.ThrowIfNull(Store);
        ArgumentNullException.ThrowIfNull(Transaction);
        ArgumentNullException.ThrowIfNull(KeyValue);
        DataTableDef.ValidateIdentifier(TableName);
        DataTableDef.ValidateIdentifier(KeyFieldName);
        DataTableDef.ValidateIdentifier(ValueFieldName);
        SqlParams Params = new SqlParams().Add(KeyFieldName, KeyValue);

        using (DataTable Rows = Store.Select(Transaction, SelectForUpdateSql(TableName, KeyFieldName), Params))
        {
            if (Rows.Rows.Count != 1)
                throw new InvalidOperationException($"Expected one row in '{TableName}' for key field '{KeyFieldName}'.");
            DataRow Result = Rows.Rows[0];
            if (Result.IsNull(ValueFieldName))
                throw new InvalidOperationException($"Cannot increment NULL in '{TableName}.{ValueFieldName}'.");

            string SqlText = $"UPDATE {TableName} SET {ValueFieldName} = {ValueFieldName} + 1 WHERE {KeyFieldName} = {GlobalPrefix}{KeyFieldName}";
            if (Store.ExecSql(Transaction, SqlText, Params) != 1)
                throw new InvalidOperationException($"Expected to increment one row in '{TableName}'.");

            return Result;
        }
    }

    /// <summary>
    /// Applies a positive row limit to a list SELECT statement.
    /// The Tripous framework supplies the limit; list SQL must not contain one.
    /// </summary>
    public virtual string ApplyRowLimit(string SqlText, int RowLimit)
    {
        throw new NotSupportedException("This provider does not support limiting list SELECTs.");
    }
    /// <summary>
    /// Gets SQL that returns a row when the table named by :TableName exists.
    /// </summary>
    public virtual string TableExistsSql => throw new NotSupportedException("Table lookup is not supported by this provider.");
    /// <summary>
    /// Gets SQL that returns a row when the view named by :ViewName exists.
    /// </summary>
    public virtual string ViewExistsSql => throw new NotSupportedException("View lookup is not supported by this provider.");
    /// <summary>
    /// Creates an empty database, or returns false if it already exists.
    /// Providers that cannot create databases report that the operation is unsupported.
    /// </summary>
    public virtual bool CreateDatabase(string ConnectionString)
    {
        throw new NotSupportedException("This provider does not support database creation.");
    }
    /// <summary>
    /// Creates the database described by the supplied settings, or returns false if it exists.
    /// </summary>
    public virtual bool CreateDatabase(DbConnectionInfo ConnectionInfo)
    {
        ArgumentNullException.ThrowIfNull(ConnectionInfo);
        return CreateDatabase(ConnectionInfo.ConnectionString);
    }

    /// <summary>
    /// Checks whether the database contains a generator with the supplied name.
    /// </summary>
    public virtual bool GeneratorExists(string ConnectionString, string GeneratorName)
        => throw new NotSupportedException("This provider does not support named generators.");
    /// <summary>
    /// Creates a named generator if it does not already exist.
    /// </summary>
    public virtual void CreateGenerator(string ConnectionString, string GeneratorName)
        => throw new NotSupportedException("This provider does not support named generators.");
    /// <summary>
    /// Sets the stored counter for a named generator.
    /// This operation can affect which IDs are generated later.
    /// </summary>
    public virtual void SetGeneratorTo(string ConnectionString, string GeneratorName, long Value)
        => throw new NotSupportedException("This provider does not support named generators.");
    /// <summary>
    /// Sets a named generator using a 32-bit value.
    /// </summary>
    public virtual void SetGeneratorTo(string ConnectionString, string GeneratorName, int Value)
        => SetGeneratorTo(ConnectionString, GeneratorName, (long)Value);
    /// <summary>
    /// Returns SQL for obtaining the next value of a named generator.
    /// </summary>
    public virtual string NextIdByGeneratorSql(string GeneratorName)
    {
        throw new NotSupportedException("This provider does not support generators.");
    }
    /// <summary>
    /// Sets the stored counter for a table's database-generated keys.
    /// This is an administrative operation that can affect which IDs are generated later.
    /// </summary>
    public virtual void SetTableGeneratorTo(string ConnectionString, string TableName, long Value)
        => throw new NotSupportedException("This provider does not support setting a table generator.");

    /// <summary>
    /// Sets the table generator using a 32-bit value.
    /// </summary>
    public virtual void SetTableGeneratorTo(string ConnectionString, string TableName, int Value)
        => SetTableGeneratorTo(ConnectionString, TableName, (long)Value);

    /// <summary>
    /// Inserts one row and returns its database-generated integer key in the supplied transaction.
    /// The default implementation reads the last generated ID after a successful insert.
    /// </summary>
    public virtual long InsertAndGetId(SqlStore Store, DbTransaction Transaction, string SqlText, SqlParams Params, string TableName, string KeyField)
    {
        ArgumentNullException.ThrowIfNull(Store);
        ArgumentException.ThrowIfNullOrWhiteSpace(TableName);
        if (Store.ExecSql(Transaction, SqlText, Params) == 0)
            throw new InvalidOperationException($"Insert did not affect a row in table '{TableName}'.");
        return Store.LastId(Transaction, TableName);
    }
    /// <summary>
    /// Returns SQL for obtaining the last generated ID in the current connection.
    /// </summary>
    public virtual string LastIdSql(string TableName)
    {
        throw new NotSupportedException("This provider does not support retrieving generated IDs.");
    }
    /// <summary>
    /// Replaces Tripous SQL type placeholders with provider-specific SQL.
    /// </summary>
    public virtual string ReplaceDataTypePlaceholders(string SqlText)
    {
        ArgumentNullException.ThrowIfNull(SqlText);
        Dictionary<string, string> Tokens = new(StringComparer.Ordinal);
        foreach (var Item in TypeTokens)
            Tokens.Add(Item.Key, Item.Value);

        StringBuilder Result = new();
        int Position = 0;
        int Index = 0;
        while (Index < SqlText.Length)
        {
            if (SqlTextScanner.SkipProtectedRegion(SqlText, ref Index, ServerType))
                continue;
            if (SqlText[Index] != '@' || (Index > 0 && (char.IsLetterOrDigit(SqlText[Index - 1]) || SqlText[Index - 1] == '_' || SqlText[Index - 1] == '$' || SqlText[Index - 1] == '@')))
            {
                Index++;
                continue;
            }
            int End = Index + 1;
            while (End < SqlText.Length && (char.IsLetterOrDigit(SqlText[End]) || SqlText[End] == '_' || SqlText[End] == '$'))
                End++;
            string Token = SqlText.Substring(Index, End - Index);
            string Replacement = null;
            bool DecimalWithArgs = Token.Equals(SqlTypeTokens.CDECIMAL_, StringComparison.OrdinalIgnoreCase);
            bool NVarcharWithLength = Token == SqlTypeTokens.CNVARCHAR;
            if (DecimalWithArgs || NVarcharWithLength)
            {
                if (NVarcharWithLength)
                    Replacement = NVarcharSql;
                int Open = End;
                while (Open < SqlText.Length && char.IsWhiteSpace(SqlText[Open]))
                    Open++;
                if (Open < SqlText.Length && SqlText[Open] == '(')
                {
                    int Close = SqlText.IndexOf(')', Open + 1);
                    if (Close >= 0)
                    {
                        Replacement = DecimalWithArgs
                            ? DecimalWithArgsSql(SqlText.Substring(Open + 1, Close - Open - 1))
                            : NVarcharWithLengthSql(SqlText.Substring(End, Close - End + 1));
                        End = Close + 1;
                    }
                }
            }
            else
            {
                Tokens.TryGetValue(Token, out Replacement);
            }
            if (Replacement != null)
            {
                Result.Append(SqlText, Position, Index - Position);
                Result.Append(Replacement);
                Position = End;
            }
            Index = End;
        }
        Result.Append(SqlText, Position, SqlText.Length - Position);
        return Result.ToString();
    }
    /// <summary>
    /// Creates a command and binds the supplied Tripous parameters.
    /// The caller owns the returned command.
    /// </summary>
    public virtual DbCommand CreateCommand(DbConnection Connection, string SqlText, SqlParams Params)
        => CreateCommandCore(Connection, SqlText, Params, null, null);

    /// <summary>
    /// Creates a prepared command on the transaction's connection with the requested timeout.
    /// The caller owns the returned command.
    /// </summary>
    public virtual DbCommand CreateCommand(DbTransaction Transaction, string SqlText, SqlParams Params, int CommandTimeout)
    {
        ArgumentNullException.ThrowIfNull(Transaction);
        DbConnection Connection = Transaction.Connection
            ?? throw new InvalidOperationException("The transaction has no active connection.");
        return CreateCommandCore(Connection, SqlText, Params, Transaction, CommandTimeout);
    }
    /// <summary>
    /// Sets the command's execution settings before applying provider-specific preparation.
    /// </summary>
    DbCommand CreateCommandCore(DbConnection Connection, string SqlText, SqlParams Params, DbTransaction Transaction, int? CommandTimeout)
    {
        ArgumentNullException.ThrowIfNull(Connection);
        DbCommand Command = Connection.CreateCommand();
        try
        {
            if (Transaction != null)
                Command.Transaction = Transaction;
            if (CommandTimeout.HasValue)
                Command.CommandTimeout = CommandTimeout.Value;
            PrepareCommand(Command, SqlText, Params);
            return Command;
        }
        catch
        {
            Command.Dispose();
            throw;
        }
    }
    /// <summary>
    /// Prepares SQL text and parameters for the driver.
    /// </summary>
    public virtual void PrepareCommand(DbCommand Command, string SqlText, SqlParams Params)
    {
        ArgumentNullException.ThrowIfNull(Command);
        ArgumentNullException.ThrowIfNull(SqlText);
        ArgumentNullException.ThrowIfNull(Params);

        var Map = new Dictionary<string, SqlParam>(StringComparer.OrdinalIgnoreCase);
        foreach (SqlParam Param in Params.Items)
            Map.Add(Param.Name, Param);

        SqlText = ReplaceDataTypePlaceholders(SqlText);
        var Refs = SqlParamScanner.Scan(SqlText, GlobalPrefix, ServerType);
        var Added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var Text = new StringBuilder();
        var Parameters = new List<DbParameter>();
        int Position = 0;
        foreach (SqlParamRef Ref in Refs)
        {
            if (!Map.TryGetValue(Ref.Name, out SqlParam Param))
                throw new ArgumentException($"SQL parameter not found: {Ref.Name}", nameof(Params));

            Text.Append(SqlText, Position, Ref.Index - Position);
            string NativeName = NativePrefix + Param.Name;
            Text.Append(PositionalParameters ? "?" : NativeName);
            Position = Ref.Index + Ref.Name.Length + 1;

            if (PositionalParameters || Added.Add(Param.Name))
            {
                DbParameter Parameter = Command.CreateParameter();
                Parameter.ParameterName = NativeName;
                Parameter.Value = Param.Value ?? DBNull.Value;
                Parameters.Add(Parameter);
            }
        }
        Text.Append(SqlText, Position, SqlText.Length - Position);
        Command.CommandText = Text.ToString();
        Command.Parameters.Clear();
        foreach (DbParameter Parameter in Parameters)
            Command.Parameters.Add(Parameter);
    }

    // ● protected
    /// <summary>
    /// Formats a decimal type with explicit precision and scale.
    /// </summary>
    protected virtual string DecimalWithArgsSql(string Args) => $"decimal({Args})";
    /// <summary>
    /// Formats a Unicode variable-length character type with its parenthesized length clause.
    /// </summary>
    protected virtual string NVarcharWithLengthSql(string LengthClause) => NVarcharSql + LengthClause;
    /// <summary>
    /// Gets placeholder mappings in an order that handles longer names first.
    /// </summary>
    protected virtual IReadOnlyList<KeyValuePair<string, string>> TypeTokens => new KeyValuePair<string, string>[]
    {
        new(SqlTypeTokens.CPRIMARY_KEY, PrimaryKeySql),
        new(SqlTypeTokens.CAUTO_INC, AutoIncSql),
        new(SqlTypeTokens.CNVARCHAR, NVarcharSql),
        new(SqlTypeTokens.CVARCHAR, VarcharSql),
        new(SqlTypeTokens.CINT64, Int64Sql),
        new(SqlTypeTokens.CFLOAT, FloatSql),
        new(SqlTypeTokens.CDECIMAL, DecimalSql),
        new(SqlTypeTokens.CDATE_TIME, DateTimeSql),
        new(SqlTypeTokens.CDATE, DateSql),
        new(SqlTypeTokens.CBOOL, BoolSql),
        new(SqlTypeTokens.CNBLOB_TEXT, NBlobTextSql),
        new(SqlTypeTokens.CBLOB_TEXT, BlobTextSql),
        new(SqlTypeTokens.CBLOB, BlobSql),
        new(SqlTypeTokens.CNOT_NULL, NotNullSql),
        new(SqlTypeTokens.CNULL, NullSql)
    };

    // ● table schema changes
    /// <summary>
    /// Builds SQL to rename a column of an existing table.
    /// </summary>
    public virtual string RenameColumnSql(string TableName, string ColumnName, string NewColumnName)
        => throw new NotSupportedException("This provider does not support renaming columns.");
    /// <summary>
    /// Builds SQL to add a column described by a schema field definition.
    /// </summary>
    public virtual string AddColumnSql(string TableName, DataFieldDef Field)
        => throw new NotSupportedException("This provider does not support adding columns.");
    /// <summary>
    /// Builds SQL to add a column from an application-authored SQL definition.
    /// </summary>
    public virtual string AddColumnSql(string TableName, string ColumnDef)
        => $"ALTER TABLE {QuoteName(TableName)} ADD {ColumnDef}";
    /// <summary>
    /// Builds SQL to remove a column from an existing table.
    /// </summary>
    public virtual string DropColumnSql(string TableName, string ColumnName)
        => $"ALTER TABLE {QuoteName(TableName)} DROP {QuoteName(ColumnName)}";
    /// <summary>
    /// Builds an UPDATE that fills existing NULL values before adding a NOT NULL constraint.
    /// DefaultExpression is application-authored SQL, including any required string literal quotes.
    /// IsString is available to provider overrides that need to distinguish text columns.
    /// </summary>
    public virtual string SetDefaultBeforeNotNullUpdateSql(string TableName, string ColumnName, string DefaultExpression, bool IsString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DefaultExpression);
        string Column = QuoteName(ColumnName);
        return $"UPDATE {QuoteName(TableName)} SET {Column} = {DefaultExpression} WHERE {Column} IS NULL";
    }
    /// <summary>
    /// Builds SQL to require a value in an existing column.
    /// The column type is available for providers that require it in the ALTER statement.
    /// </summary>
    public virtual string SetNotNullSql(string TableName, string ColumnName, string DataType)
        => throw new NotSupportedException("This provider does not support setting NOT NULL.");
    /// <summary>
    /// Builds SQL to remove an existing column's NOT NULL requirement.
    /// The column type is available for providers that require it in the ALTER statement.
    /// </summary>
    public virtual string DropNotNullSql(string TableName, string ColumnName, string DataType)
        => throw new NotSupportedException("This provider does not support dropping NOT NULL.");
    /// <summary>
    /// Builds SQL to change an existing column's declared length.
    /// The supplied type, nullability and default let providers restate the column definition.
    /// </summary>
    public virtual string SetColumnLengthSql(string TableName, string ColumnName, string DataType, string Required, string DefaultExpression)
        => throw new NotSupportedException("This provider does not support changing a column's length directly.");
    /// <summary>
    /// Builds SQL to set the default expression for future inserts into an existing column.
    /// </summary>
    public virtual string SetColumnDefaultSql(string TableName, string ColumnName, string DefaultExpression)
        => throw new NotSupportedException("This provider does not support setting a column default directly.");
    /// <summary>
    /// Builds SQL to remove an existing column's default expression.
    /// </summary>
    public virtual string DropColumnDefaultSql(string TableName, string ColumnName)
        => throw new NotSupportedException("This provider does not support dropping a column default directly.");
    /// <summary>
    /// Builds SQL to add a named unique constraint to an existing table.
    /// For example, ALTER TABLE Customer ADD CONSTRAINT UQ_Customer_Code UNIQUE (Code).
    /// </summary>
    public virtual string AddUniqueConstraintSql(string TableName, string ColumnName, string ConstraintName)
        => $"ALTER TABLE {QuoteName(TableName)} ADD CONSTRAINT {QuoteName(ConstraintName)} UNIQUE ({QuoteName(ColumnName)})";
    /// <summary>
    /// Builds SQL to drop a named unique constraint from an existing table.
    /// </summary>
    public virtual string DropUniqueConstraintSql(string TableName, string ConstraintName)
        => $"ALTER TABLE {QuoteName(TableName)} DROP CONSTRAINT {QuoteName(ConstraintName)}";
    /// <summary>
    /// Builds SQL to add a named foreign key constraint to an existing table.
    /// For example, ALTER TABLE City ADD CONSTRAINT FK_City_Country
    /// FOREIGN KEY (CountryId) REFERENCES Country (Id).
    /// </summary>
    public virtual string AddForeignKeySql(string TableName, string ColumnName, string ForeignTableName, string ForeignColumnName, string ConstraintName)
        => $"ALTER TABLE {QuoteName(TableName)} ADD CONSTRAINT {QuoteName(ConstraintName)} FOREIGN KEY ({QuoteName(ColumnName)}) REFERENCES {QuoteName(ForeignTableName)} ({QuoteName(ForeignColumnName)})";
    /// <summary>
    /// Builds SQL to drop a named foreign key constraint from an existing table.
    /// </summary>
    public virtual string DropForeignKeySql(string TableName, string ConstraintName)
        => $"ALTER TABLE {QuoteName(TableName)} DROP CONSTRAINT {QuoteName(ConstraintName)}";
    /// <summary>
    /// Builds SQL to create a named index over one or more columns.
    /// </summary>
    public virtual string CreateIndexSql(string TableName, string IndexName, IReadOnlyList<string> ColumnNames, bool Unique = false)
        => throw new NotSupportedException("This provider does not support creating indexes.");
    /// <summary>
    /// Builds SQL to drop a named index on the specified table.
    /// </summary>
    public virtual string DropIndexSql(string TableName, string IndexName)
        => throw new NotSupportedException("This provider does not support dropping indexes.");

    // ● SQL type mappings
    /// <summary>
    /// Gets the provider's SQL for the primary key declaration.
    /// </summary>
    public virtual string PrimaryKeySql => "primary key";
    /// <summary>
    /// Gets the provider's SQL for the auto-increment field declaration.
    /// </summary>
    public virtual string AutoIncSql => "integer generated by default as identity";
    /// <summary>
    /// Gets the provider's SQL for the Unicode variable-length string type.
    /// </summary>
    public virtual string NVarcharSql => "nvarchar";
    /// <summary>
    /// Gets the provider's SQL for the variable-length string type.
    /// </summary>
    public virtual string VarcharSql => "varchar";
    /// <summary>
    /// Gets the provider's SQL for the signed 64-bit integer type.
    /// </summary>
    public virtual string Int64Sql => "bigint";
    /// <summary>
    /// Gets the provider's SQL for the floating-point type.
    /// </summary>
    public virtual string FloatSql => "float";
    /// <summary>
    /// Gets the provider's SQL for the default decimal type.
    /// </summary>
    public virtual string DecimalSql => "decimal(18, 4)";
    /// <summary>
    /// Gets the provider's SQL for the date and time type.
    /// </summary>
    public virtual string DateTimeSql => "timestamp";
    /// <summary>
    /// Gets the provider's SQL for the date-only type.
    /// </summary>
    public virtual string DateSql => "date";
    /// <summary>
    /// Gets the provider's SQL for the integer-backed Boolean type.
    /// </summary>
    public virtual string BoolSql => "integer";
    /// <summary>
    /// Gets the provider's SQL for the Unicode large text type.
    /// </summary>
    public virtual string NBlobTextSql => "text";
    /// <summary>
    /// Gets the provider's SQL for the large text type.
    /// </summary>
    public virtual string BlobTextSql => "text";
    /// <summary>
    /// Gets the provider's SQL for the binary large object type.
    /// </summary>
    public virtual string BlobSql => "blob";
    /// <summary>
    /// Gets the provider's SQL for the non-null column clause.
    /// </summary>
    public virtual string NotNullSql => "not null";
    /// <summary>
    /// Gets the provider's SQL for the nullable column clause.
    /// </summary>
    public virtual string NullSql => "null";

    // ● capabilities
    /// <summary>
    /// Gets the direct table alterations implemented by this provider.
    /// Providers explicitly advertise the operations that callers can offer.
    /// </summary>
    public virtual AlterTableType SupportedAlterTableTypes => AlterTableType.None;
    /// <summary>
    /// Returns whether the provider supports every requested table alteration.
    /// </summary>
    public virtual bool SupportsAlterTableType(AlterTableType AlterType)
        => (SupportedAlterTableTypes & AlterType) == AlterType;

    /// <summary>
    /// Gets whether the provider supports creating databases.
    /// </summary>
    public virtual bool CanCreateDatabases => false;
    /// <summary>
    /// Gets whether the database supports generators or sequences.
    /// </summary>
    public virtual bool SupportsGenerators => false;
    /// <summary>
    /// Gets whether the database supports auto-increment fields.
    /// </summary>
    public virtual bool SupportsAutoIncFields => true;
    /// <summary>
    /// Gets the provider's mode for generating integer row identifiers.
    /// </summary>
    public virtual OidMode OidMode => OidMode.None;

    // ● properties
    /// <summary>
    /// Gets the database server type implemented by this provider.
    /// </summary>
    public abstract DbServerType ServerType { get; }
    /// <summary>
    /// Gets the driver's ADO.NET factory through the common provider API.
    /// </summary>
    public abstract DbProviderFactory Factory { get; }
    /// <summary>
    /// Gets the provider name derived from its database server type.
    /// </summary>
    public virtual string Name => ServerType.ToString();
    /// <summary>
    /// Gets a display description for this database provider.
    /// </summary>
    public virtual string Description => Name;
    /// <summary>
    /// Gets a format template showing the provider's connection string syntax.
    /// Use CreateConnectionString to escape values supplied by callers.
    /// </summary>
    public virtual string ConnectionStringTemplate => "";
    /// <summary>
    /// Gets the customary administrative user name, when the provider has one.
    /// </summary>
    public virtual string SuperUser => "";
    /// <summary>
    /// Gets the documented default administrative password, when one is available.
    /// </summary>
    public virtual string SuperUserPassword => "";
    /// <summary>
    /// Gets the adapter used to parse and build connection strings for this provider.
    /// </summary>
    public virtual DbConAdapter ConnectionStringAdapter => DbConAdapters.Get(ServerType);

    /// <summary>
    /// Gets the native prefix used by the driver for named parameters.
    /// </summary>
    public abstract string NativePrefix { get; }
    /// <summary>
    /// Gets whether parameters are bound by position.
    /// </summary>
    public virtual bool PositionalParameters => false;
}
