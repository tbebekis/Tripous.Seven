/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Tests SQL command preparation for named and positional parameters.
/// </summary>
public class SqlProviderTests
{
    // ● private types
    /// <summary>
    /// Provides positional SQL tokens for command preparation tests.
    /// </summary>
    class PositionalProvider : SqlProvider
    {
        // ● public
        /// <summary>
        /// Creates a SQLite connection for the test command.
        /// </summary>
        public override DbConnection CreateConnection(string ConnectionString) => new SqliteConnection(ConnectionString);

        // ● properties
        /// <summary>
        /// Gets the database type used by this test provider.
        /// </summary>
        public override DbServerType ServerType => DbServerType.Sqlite;

        /// <summary>
        /// Gets the SQLite factory used by this test provider.
        /// </summary>
        public override DbProviderFactory Factory => SqliteFactory.Instance;

        /// <summary>
        /// Gets the parameter name prefix.
        /// </summary>
        public override string NativePrefix => "@";
        /// <summary>
        /// Gets whether parameters use positional tokens.
        /// </summary>
        public override bool PositionalParameters => true;
    }

    // ● public
    /// <summary>
    /// Firebird limits the outer result while preserving nested limits and trailing comments.
    /// </summary>
    [Fact]
    public void FirebirdRowLimitAppliesToCompleteSelect()
    {
        SqlProvider Provider = SqlProviders.Firebird;
        const string Query = "WITH Sample AS (SELECT FIRST 2 Id FROM Item) SELECT Id FROM Sample UNION ALL SELECT Id FROM Item ORDER BY 1 NULLS FIRST";
        Assert.Equal(Query + " FETCH FIRST 5 ROWS ONLY; -- list", Provider.ApplyRowLimit(Query + "; -- list", 5));
        Assert.Equal("SELECT 'ROWS', :First FROM Item FETCH FIRST 5 ROWS ONLY", Provider.ApplyRowLimit("SELECT 'ROWS', :First FROM Item", 5));
    }
    /// <summary>
    /// Firebird list SELECTs leave outer row limits to the framework.
    /// </summary>
    [Theory]
    [InlineData("SELECT /* comment */ FIRST 2 Id FROM Item")]
    [InlineData("SELECT SKIP 2 Id FROM Item")]
    [InlineData("SELECT Id FROM Item ROWS 2")]
    [InlineData("SELECT Id FROM Item OFFSET 2 ROWS")]
    [InlineData("SELECT Id FROM Item FETCH FIRST 2 ROWS ONLY")]
    public void FirebirdRowLimitRejectsExistingOuterLimit(string SqlText)
    {
        Assert.Throws<InvalidOperationException>(() => SqlProviders.Firebird.ApplyRowLimit(SqlText, 5));
    }
    /// <summary>
    /// Firebird declares UTF-8 after the length of each Unicode field.
    /// </summary>
    [Fact]
    public void FirebirdUnicodeVarcharIncludesExplicitCharacterSet()
    {
        const string Sql = "CREATE TABLE Item (Name @NVARCHAR(40), Code @VARCHAR(20), Note @NVARCHAR (80) DEFAULT '@NVARCHAR(1)')";
        Assert.Equal("CREATE TABLE Item (Name varchar(40) CHARACTER SET UTF8, Code varchar(20), Note varchar (80) CHARACTER SET UTF8 DEFAULT '@NVARCHAR(1)')",
            SqlProviders.Firebird.ReplaceDataTypePlaceholders(Sql));
    }

    /// <summary>
    /// Provider builders expose configured database names and preserve SQLite path placeholders.
    /// </summary>
    [Fact]
    public void ConnectionStringBuildersReadDeclaredDatabaseNames()
    {
        SqlProvider Sqlite = SqlProviders.Sqlite;
        const string SqliteSettings = "Data Source=[Data]/app.db";
        Assert.IsType<SqliteConnectionStringBuilder>(Sqlite.CreateConnectionStringBuilder(SqliteSettings));
        Assert.Equal("[Data]/app.db", Sqlite.GetDatabaseName(SqliteSettings));
        Assert.Equal(string.Empty, Sqlite.GetDatabaseName("Pooling=False"));

        SqlProvider PostgreSql = SqlProviders.PostgreSql;
        const string PostgreSqlSettings = "Host=db.test;Database=Catalog;Username=tester";
        Assert.IsType<Npgsql.NpgsqlConnectionStringBuilder>(PostgreSql.CreateConnectionStringBuilder(PostgreSqlSettings));
        Assert.Equal("Catalog", PostgreSql.GetDatabaseName(PostgreSqlSettings));
        Assert.Equal(string.Empty, PostgreSql.GetDatabaseName("Host=db.test;Username=tester"));
    }

    /// <summary>
    /// SQL date literals use a Gregorian date and whole seconds regardless of the current culture.
    /// </summary>
    [Fact]
    public void SqlDateLiteralsUseInvariantFormatting()
    {
        var PreviousCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("th-TH");
            DateTime Value = new(2026, 11, 27, 14, 5, 6, 987, DateTimeKind.Local);

            Assert.Equal("'2026-11-27'", SqlProviders.Sqlite.QSDate(Value));
            Assert.Equal("'2026-11-27 14:05:06'", SqlProviders.Sqlite.QSDateTime(Value));
            Assert.Equal("'2026-11-27'", SqlProviders.PostgreSql.QSDate(Value));
            Assert.Equal("'2026-11-27 14:05:06'", SqlProviders.PostgreSql.QSDateTime(Value));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = PreviousCulture;
        }
    }

    /// <summary>
    /// The locked increment returns the original 64-bit value and participates in rollback.
    /// </summary>
    [Fact]
    public void SelectIncrementLockedReturnsOriginalValueAndRollsBack()
    {
        DbConnectionInfo Info = new() { ConnectionString = "Data Source=:memory:" };
        SqlStore Store = new(Info);
        using (SqlTransactionContext Context = Store.BeginTransactionContext())
        {
            Store.ExecSql(Context.Transaction, "CREATE TABLE Counter (Id INTEGER PRIMARY KEY, NextNumber INTEGER NOT NULL)");
            Store.ExecSql(Context.Transaction, "INSERT INTO Counter VALUES (1, 2147483648)");
            Context.Commit();

            using (DbTransaction Transaction = Context.Connection.BeginTransaction())
            {
                DataRow Original = SqlProviders.Sqlite.SelectIncrementLocked(Store, Transaction, "Counter", "Id", 1, "NextNumber");
                Assert.Equal(2147483648L, Original.Field<long>("NextNumber"));
                using (DataTable Updated = Store.Select(Transaction, "SELECT NextNumber FROM Counter"))
                {
                    Assert.Equal(2147483649L, Updated.Rows[0].Field<long>("NextNumber"));
                }
                Transaction.Rollback();
            }

            using (DbTransaction Transaction = Context.Connection.BeginTransaction())
            using (DataTable RolledBack = Store.Select(Transaction, "SELECT NextNumber FROM Counter"))
            {
                Assert.Equal(2147483648L, RolledBack.Rows[0].Field<long>("NextNumber"));
                Transaction.Commit();
            }
        }
    }

    /// <summary>
    /// SQLite applies the limit before a terminator and trailing comments.
    /// </summary>
    [Fact]
    public void SqliteRowLimitHandlesCommentsAndStatementTerminator()
    {
        SqlProvider Provider = SqlProviders.Sqlite;
        Assert.Equal("select Id from Customer LIMIT 5; -- list", Provider.ApplyRowLimit("select Id from Customer; -- list", 5));
        Assert.Equal("select 'limit' as Value LIMIT 5 -- limit is a comment", Provider.ApplyRowLimit("select 'limit' as Value -- limit is a comment", 5));
        Assert.Equal("select * from (select Id from Customer limit 10) X LIMIT 5", Provider.ApplyRowLimit("select * from (select Id from Customer limit 10) X", 5));
        Assert.Equal("select :limit LIMIT 5", Provider.ApplyRowLimit("select :limit", 5));
        Assert.Throws<InvalidOperationException>(() => Provider.ApplyRowLimit("select Id from Customer LIMIT 10", 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => Provider.ApplyRowLimit("select 1", 0));
    }
    /// <summary>
    /// PostgreSQL command preparation recognizes casts, array parameters, and quoted SQL.
    /// </summary>
    [Fact]
    public void PostgreSqlCommandPreservesDialectSyntax()
    {
        SqlProvider Provider = SqlProviders.GetSqlProvider(DbServerType.PostgreSql);
        string ConnectionString = Provider.CreateConnectionString("db.test", "Catalog", "tester", "a;b=c");
        using DbConnection Connection = Provider.CreateConnection(ConnectionString);
        Assert.Equal("Catalog", Connection.Database);
        Assert.Same(SqlProviders.PostgreSql, Provider);

        const string Sql = @"select array[:Value]::int[], $tag$:ignored @BLOB$tag$, E'it\'s :ignored', 1 /* /* nested */ :ignored */, cast(null as @BLOB)";
        using DbCommand Command = Provider.CreateCommand(Connection, Sql, new SqlParams().Add("Value", 7));
        Assert.Equal(Sql.Replace(":Value", "@Value").Replace("as @BLOB", "as bytea"), Command.CommandText);
        Assert.Equal(7, Assert.IsAssignableFrom<DbParameter>(Assert.Single(Command.Parameters)).Value);
    }
    /// <summary>
    /// PostgreSQL row limits preserve quoted text and reject existing top-level row limits.
    /// </summary>
    [Fact]
    public void PostgreSqlRowLimitRecognizesLimitAndFetch()
    {
        SqlProvider Provider = SqlProviders.PostgreSql;
        const string Sql = "select $$FETCH LIMIT$$ from (select 1 limit 1) x";
        Assert.Equal(Sql + " LIMIT 5; -- list", Provider.ApplyRowLimit(Sql + "; -- list", 5));
        Assert.Throws<InvalidOperationException>(() => Provider.ApplyRowLimit("select 1 LIMIT 2", 5));
        Assert.Throws<InvalidOperationException>(() => Provider.ApplyRowLimit("select 1 FETCH FIRST ROW ONLY", 5));
    }
    /// <summary>
    /// SQLite uses auto-increment while the base provider specifies no mode.
    /// </summary>
    [Fact]
    public void ProvidersReportIntegerIdentifierMode()
    {
        Assert.Equal(OidMode.None, new PositionalProvider().OidMode);
        Assert.Equal(OidMode.AutoInc, SqlProviders.Sqlite.OidMode);
        SqlProvider Provider = SqlProviders.GetSqlProvider(DbServerType.Sqlite);
        Assert.Equal(DbServerType.Sqlite, Provider.ServerType);
        Assert.Equal("Sqlite", Provider.Name);
        Assert.Same(DbConAdapters.Get(DbServerType.Sqlite), Provider.ConnectionStringAdapter);
        Assert.Same(SqliteFactory.Instance, Provider.Factory);
        using (DbConnection VersionConnection = Provider.OpenConnection("Data Source=:memory:"))
        using (DbCommand VersionQuery = Provider.CreateCommand(VersionConnection, "SELECT sqlite_version()", new SqlParams()))
            Assert.Equal(VersionQuery.ExecuteScalar(), Provider.GetServerVersion(new DbConnectionInfo { ConnectionString = "Data Source=:memory:" }));
        using DbConnection Connection = Provider.OpenConnection("Data Source=:memory:");
        using DbCommand Command = Provider.CreateCommand(Connection, "SELECT :Id", new SqlParams().Add("Id", 7));
        Assert.Equal(7L, Command.ExecuteScalar());
        using SqlTransactionContext Context = Provider.BeginTransactionContext(new DbConnectionInfo { ConnectionString = "Data Source=:memory:" });
        using DbCommand TransactionCommand = Provider.CreateCommand(Context.Transaction, "SELECT 1", new SqlParams(), 5);
        Assert.Same(Context.Transaction, TransactionCommand.Transaction);
        Assert.Equal(5, TransactionCommand.CommandTimeout);
        Assert.Equal(1L, TransactionCommand.ExecuteScalar());
        Context.Commit();
    }
    /// <summary>
    /// Repeated named references share one native parameter.
    /// </summary>
    [Fact]
    public void NamedReferencesShareParameter()
    {
        using (var Command = new SqliteCommand())
        {
            SqlProviders.Sqlite.PrepareCommand(Command, "SELECT :Value, :Value", new SqlParams().Add("Value", 7));
            Assert.Equal("SELECT :Value, :Value", Command.CommandText);
            Assert.Single(Command.Parameters);
            Assert.Equal(7, Command.Parameters[0].Value);
        }
    }
    /// <summary>
    /// Positional parameters follow each SQL reference, including repetitions.
    /// </summary>
    [Fact]
    public void PositionalReferencesPreserveOrder()
    {
        using (var Command = new SqliteCommand())
        {
            new PositionalProvider().PrepareCommand(Command, "SELECT :X, :X, :Y", new SqlParams().Add("X", 10).Add("Y", 20));
            Assert.Equal("SELECT ?, ?, ?", Command.CommandText);
            Assert.Equal(new object[] { 10, 10, 20 }, Command.Parameters.Cast<DbParameter>().Select(Item => Item.Value));
        }
    }
    /// <summary>
    /// Missing parameter values are reported before the command is changed.
    /// </summary>
    [Fact]
    public void MissingParameterThrows()
    {
        using (var Command = new SqliteCommand { CommandText = "original" })
        {
            Assert.Throws<ArgumentException>(() => SqlProviders.Sqlite.PrepareCommand(Command, "SELECT :Missing", new SqlParams()));
            Assert.Equal("original", Command.CommandText);
        }
    }
    /// <summary>
    /// SQLite SQL types are replaced without confusing overlapping token names.
    /// </summary>
    [Fact]
    public void ReplacesSqliteTypeTokens()
    {
        string Sql = "CREATE TABLE Items (Name @NVARCHAR(40) @NOT_NULL, Created @DATE_TIME @NULL, Amount @DECIMAL_(18, 4), Notes @NBLOB_TEXT)";
        string Result = SqlProviders.Sqlite.ReplaceDataTypePlaceholders(Sql);
        Assert.Contains("nvarchar(40) not null", Result);
        Assert.Contains("Created datetime", Result);
        Assert.Contains("decimal(18, 4)", Result);
        Assert.Contains("Notes text", Result);
        Assert.DoesNotContain("@", Result);
    }
    /// <summary>
    /// Creates a file database once and leaves an existing database intact.
    /// </summary>
    [Fact]
    public void CreatesSqliteDatabaseWithoutOverwriting()
    {
        string Folder = Path.Combine(Path.GetTempPath(), $"tripous-create-{Guid.NewGuid():N}");
        string FilePath = Path.Combine(Folder, "data;base.db");
        string RelativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), FilePath);
        string ConnectionString = SqlProviders.Sqlite.CreateConnectionString(null, RelativePath, null, null) + ";Pooling=False";
        try
        {
            Assert.True(SqlProviders.Sqlite.CanCreateDatabases);
            DbConnectionInfo ConnectionInfo = new() { ConnectionString = ConnectionString };
            Assert.False(SqlProviders.Sqlite.DatabaseExists(ConnectionInfo));
            Assert.False(SqlProviders.Sqlite.CanConnect(ConnectionInfo));
            Assert.Throws<SqliteException>(() => SqlProviders.Sqlite.EnsureConnection(ConnectionInfo));
            Assert.False(File.Exists(FilePath));
            Assert.True(SqlProviders.Sqlite.CanConnect("Data Source=:memory:"));
            Assert.True(SqlProviders.Sqlite.CanConnect("Data Source=file:shared?mode=memory"));
            Assert.True(SqlProviders.Sqlite.CreateDatabase(ConnectionInfo));
            Assert.True(SqlProviders.Sqlite.DatabaseExists(ConnectionInfo));
            Assert.True(SqlProviders.Sqlite.CanConnect(ConnectionInfo));
            SqlProviders.Sqlite.EnsureConnection(ConnectionInfo);
            Assert.True(SqlProviders.Sqlite.CanConnect(ConnectionString + ";Mode=ReadOnly"));
            var Normalized = new SqliteConnectionStringBuilder(SqlProviders.Sqlite.NormalizeConnectionString(ConnectionString));
            Assert.Equal(FilePath, Normalized.DataSource);
            Assert.False(Normalized.Pooling);
            var AppPath = new SqliteConnectionStringBuilder(SqlProviders.Sqlite.NormalizeConnectionString("Data Source=[AppPath]/data.db"));
            Assert.Equal(Path.Combine(SysConfig.AppFolderPath, "data.db"), AppPath.DataSource);
            var Store = new SqlStore(new DbConnectionInfo { ConnectionString = ConnectionString });
            Store.ExecSql("CREATE TABLE Items (Id INTEGER)");
            Assert.False(SqlProviders.Sqlite.CreateDatabase(ConnectionString));
            Assert.Equal(1L, Store.Select("SELECT COUNT(*) AS Total FROM sqlite_master WHERE name = 'Items'").Rows[0]["Total"]);
            Assert.Throws<ArgumentException>(() => SqlProviders.Sqlite.CreateDatabase("Data Source=:memory:"));
            Assert.False(SqlProviders.Sqlite.DatabaseExists("Data Source=:memory:"));
            using DbConnection UriConnection = SqlProviders.Sqlite.CreateConnection("Data Source=file:shared?mode=memory");
            Assert.Equal("file:shared?mode=memory", new SqliteConnectionStringBuilder(UriConnection.ConnectionString).DataSource);
        }
        finally
        {
            if (Directory.Exists(Folder))
                Directory.Delete(Folder, true);
        }
    }
    /// <summary>
    /// Executes the SQLite helper SQL for renaming a column and reading expressions.
    /// </summary>
    [Fact]
    public void SqliteSchemaAndExpressionHelpersExecute()
    {
        SqlProvider Provider = SqlProviders.Sqlite;
        using DbConnection Connection = Provider.CreateConnection("Data Source=:memory:");
        Connection.Open();
        using DbCommand Command = Connection.CreateCommand();
        Command.CommandText = "CREATE TABLE \"Order Lines\" (\"Old Name\" TEXT)";
        Command.ExecuteNonQuery();
        Command.CommandText = "INSERT INTO \"Order Lines\" VALUES ('one')";
        Command.ExecuteNonQuery();
        Command.CommandText = Provider.RenameColumnSql("Order Lines", "Old Name", "New \"Name");
        Command.ExecuteNonQuery();
        Command.CommandText = $"SELECT {Provider.Concat("\"New \"\"Name\"", "' two'")} FROM \"Order Lines\"";
        using DbDataReader Reader = Command.ExecuteReader();
        Assert.True(Reader.Read());
        Assert.Equal("one two", Reader.GetString(0));

        SqlStore Store = new(new DbConnectionInfo { ConnectionString = "Data Source=:memory:" });
        DateTime Before = DateTime.UtcNow.AddSeconds(-1);
        DateTime Timestamp = Store.GetServerDateTime();
        Assert.Equal(DateTimeKind.Utc, Timestamp.Kind);
        Assert.InRange(Timestamp, Before, DateTime.UtcNow);
    }

    /// <summary>
    /// Creates and drops a composite unique index on an existing SQLite table.
    /// </summary>
    [Fact]
    public void SqliteIndexHelpersEnforceUniquenessUntilDropped()
    {
        SqlProvider Provider = SqlProviders.Sqlite;
        using DbConnection Connection = Provider.CreateConnection("Data Source=:memory:");
        Connection.Open();
        using DbCommand Command = Connection.CreateCommand();
        Command.CommandText = "CREATE TABLE \"Order Lines\" (\"Order Id\" INTEGER, \"Line No\" INTEGER)";
        Command.ExecuteNonQuery();
        Command.CommandText = Provider.CreateIndexSql("Order Lines", "Order Line Unique", new[] { "Order Id", "Line No" }, Unique: true);
        Command.ExecuteNonQuery();
        const string InsertSql = "INSERT INTO \"Order Lines\" VALUES (1, 1)";
        Command.CommandText = InsertSql;
        Assert.Equal(1, Command.ExecuteNonQuery());
        Assert.Throws<SqliteException>(() => Command.ExecuteNonQuery());
        Command.CommandText = Provider.DropIndexSql("Order Lines", "Order Line Unique");
        Command.ExecuteNonQuery();
        Command.CommandText = InsertSql;
        Assert.Equal(1, Command.ExecuteNonQuery());
    }

    /// <summary>
    /// Adds a typed column to an existing SQLite table and drops it again.
    /// </summary>
    [Fact]
    public void SqliteColumnHelpersExecute()
    {
        SqlProvider Provider = SqlProviders.Sqlite;
        Assert.True(Provider.SupportsAlterTableType(AlterTableType.ColumnAddOrDrop | AlterTableType.ColumnRename));
        Assert.False(Provider.SupportsAlterTableType(AlterTableType.TableUniqueConstraint));
        Assert.False(Provider.SupportsAlterTableType(AlterTableType.ColumnLength));
        Assert.False(Provider.SupportsAlterTableType(AlterTableType.ColumnSetOrDropDefault));
        Assert.Throws<NotSupportedException>(() => Provider.SetColumnLengthSql("Items", "Notes", "TEXT", "", ""));
        Assert.Throws<NotSupportedException>(() => Provider.SetColumnDefaultSql("Items", "Notes", "'new'"));
        Assert.Throws<NotSupportedException>(() => Provider.DropColumnDefaultSql("Items", "Notes"));
        Assert.False(Provider.SupportsAlterTableType(AlterTableType.ColumnRename | AlterTableType.TableForeignKeyConstraint));
        using DbConnection Connection = Provider.CreateConnection("Data Source=:memory:");
        Connection.Open();
        using DbCommand Command = Connection.CreateCommand();
        Command.CommandText = "CREATE TABLE Items (Id INTEGER PRIMARY KEY)";
        Command.ExecuteNonQuery();
        Command.CommandText = "INSERT INTO Items VALUES (1)";
        Command.ExecuteNonQuery();
        DataFieldDef Field = new() { Name = "Active", DataType = DataFieldType.Boolean, Required = true, DefaultExpression = "1" };
        Command.CommandText = Provider.AddColumnSql("Items", Field);
        Command.ExecuteNonQuery();
        Command.CommandText = "SELECT Active FROM Items";
        Assert.Equal(1L, Command.ExecuteScalar());
        Command.CommandText = Provider.DropColumnSql("Items", "Active");
        Command.ExecuteNonQuery();
        Command.CommandText = "SELECT Id FROM Items";
        Assert.Equal(1L, Command.ExecuteScalar());
        Command.CommandText = "SELECT Active FROM Items";
        Assert.Throws<SqliteException>(() => Command.ExecuteScalar());
        Field.SetUnique();
        Assert.Throws<NotSupportedException>(() => Provider.AddColumnSql("Items", Field));
        Assert.Throws<NotSupportedException>(() => Provider.AddUniqueConstraintSql("Items", "Id", "UQ_Items_Id"));
        Assert.Throws<NotSupportedException>(() => Provider.DropForeignKeySql("Items", "FK_Items"));
        Command.CommandText = Provider.AddColumnSql("Items", "Notes @NBLOB_TEXT");
        Command.ExecuteNonQuery();
    }

    /// <summary>
    /// Changes nullability through SQLite ALTER COLUMN without recreating the table.
    /// </summary>
    [Fact]
    public void SqliteChangesColumnNullability()
    {
        SqlProvider Provider = SqlProviders.Sqlite;
        using DbConnection Connection = Provider.OpenConnection("Data Source=:memory:");
        using DbCommand Command = Connection.CreateCommand();
        Assert.True(Provider.SupportsAlterTableType(AlterTableType.ColumnSetOrDropNotNull));
        Command.CommandText = "CREATE TABLE Items (Notes TEXT)";
        Command.ExecuteNonQuery();
        Command.CommandText = "INSERT INTO Items VALUES (NULL), ('existing')";
        Command.ExecuteNonQuery();

        Command.CommandText = Provider.SetNotNullSql("Items", "Notes", "TEXT");
        Assert.Throws<SqliteException>(() => Command.ExecuteNonQuery());
        Command.CommandText = Provider.SetDefaultBeforeNotNullUpdateSql("Items", "Notes", "'present'", IsString: true);
        Assert.Equal(1, Command.ExecuteNonQuery());
        Command.CommandText = "SELECT COUNT(*) FROM Items WHERE Notes = 'existing'";
        Assert.Equal(1L, Command.ExecuteScalar());
        Command.CommandText = Provider.SetNotNullSql("Items", "Notes", "TEXT");
        Command.ExecuteNonQuery();

        Command.CommandText = "INSERT INTO Items VALUES (NULL)";
        Assert.Throws<SqliteException>(() => Command.ExecuteNonQuery());
        Command.CommandText = Provider.DropNotNullSql("Items", "Notes", "TEXT");
        Command.ExecuteNonQuery();
        Command.CommandText = "INSERT INTO Items VALUES (NULL)";
        Assert.Equal(1, Command.ExecuteNonQuery());
    }

    /// <summary>
    /// Sets an existing SQLite table counter to a 64-bit value.
    /// </summary>
    [Fact]
    public void SqliteSetsTableGenerator()
    {
        string FilePath = Path.Combine(Path.GetTempPath(), $"tripous-sequence-{Guid.NewGuid():N}.db");
        string ConnectionString = new SqliteConnectionStringBuilder { DataSource = FilePath, Pooling = false }.ConnectionString;
        SqlProvider Provider = SqlProviders.Sqlite;
        try
        {
            SqlStore Store = new(new DbConnectionInfo { ConnectionString = ConnectionString });
            Store.ExecSql("CREATE TABLE \"Order's Lines\" (Id INTEGER PRIMARY KEY AUTOINCREMENT)");
            Assert.Throws<InvalidOperationException>(() => Provider.SetTableGeneratorTo(ConnectionString, "Order's Lines", 7));
            Store.ExecSql("INSERT INTO \"Order's Lines\" DEFAULT VALUES");

            Provider.SetTableGeneratorTo(ConnectionString, "order's lines", 2147483648L);
            Store.ExecSql("INSERT INTO \"Order's Lines\" DEFAULT VALUES");
            using DataTable Result = Store.Select("SELECT MAX(Id) FROM \"Order's Lines\"");
            Assert.Equal(2147483649L, Result.Rows[0][0]);
        }
        finally
        {
            File.Delete(FilePath);
        }
    }

    /// <summary>
    /// Type tokens are replaced only as whole tokens outside quoted text and comments.
    /// </summary>
    [Fact]
    public void ReplacesOnlyUnquotedWholeTypeTokens()
    {
        string Sql = "SELECT '@DATE', \"@BOOL\", `@BLOB`, [@NULL], @DATE_TIME, @DATE, @DATE_EXTRA, @DATE$VALUE, @@DATE -- @DATE\n/* @BOOL @DECIMAL_(9, 2) */ @DECIMAL_ (10, 2), :Value";
        string Result = SqlProviders.Sqlite.ReplaceDataTypePlaceholders(Sql);
        Assert.Contains("'@DATE', \"@BOOL\", `@BLOB`, [@NULL]", Result);
        Assert.Contains("datetime, datetime, @DATE_EXTRA, @DATE$VALUE, @@DATE", Result);
        Assert.Contains("-- @DATE\n/* @BOOL @DECIMAL_(9, 2) */ decimal(10, 2), :Value", Result);
    }
    /// <summary>
    /// Token replacement keeps protected text intact during command preparation.
    /// </summary>
    [Fact]
    public void PreparesParametersAfterReplacingTypes()
    {
        using (var Command = new SqliteCommand())
        {
            SqlProviders.Sqlite.PrepareCommand(Command, "SELECT '@DATE', :Value -- @BOOL\n", new SqlParams().Add("Value", 5));
            Assert.Equal("SELECT '@DATE', :Value -- @BOOL\n", Command.CommandText);
            Assert.Single(Command.Parameters);
        }
    }
}
