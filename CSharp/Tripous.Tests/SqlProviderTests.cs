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
    /// SQLite uses auto-increment while the base provider specifies no mode.
    /// </summary>
    [Fact]
    public void ProvidersReportIntegerIdentifierMode()
    {
        Assert.Equal(OidMode.None, new PositionalProvider().OidMode);
        Assert.Equal(OidMode.AutoInc, SqlProviders.Sqlite.OidMode);
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
        string FilePath = Path.Combine(Folder, "database.db");
        string ConnectionString = new SqliteConnectionStringBuilder { DataSource = FilePath, Pooling = false }.ToString();
        try
        {
            Assert.True(SqlProviders.Sqlite.CanCreateDatabases);
            Assert.True(SqlProviders.Sqlite.CreateDatabase(ConnectionString));
            var Store = new SqlStore(new DbConnectionInfo { ConnectionString = ConnectionString });
            Store.ExecSql("CREATE TABLE Items (Id INTEGER)");
            Assert.False(SqlProviders.Sqlite.CreateDatabase(ConnectionString));
            Assert.Equal(1L, Store.Select("SELECT COUNT(*) AS Total FROM sqlite_master WHERE name = 'Items'").Rows[0]["Total"]);
            Assert.Throws<ArgumentException>(() => SqlProviders.Sqlite.CreateDatabase("Data Source=:memory:"));
        }
        finally
        {
            if (Directory.Exists(Folder))
                Directory.Delete(Folder, true);
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
