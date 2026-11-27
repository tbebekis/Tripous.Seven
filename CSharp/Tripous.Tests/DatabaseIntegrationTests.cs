/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Tests;

/// <summary>
/// Skips a live database test unless its connection string is configured.
/// </summary>
internal sealed class DatabaseFactAttribute : FactAttribute
{
    /// <summary>
    /// Uses the presence of the indicated environment variable to enable the test.
    /// </summary>
    public DatabaseFactAttribute(string Variable)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
            Skip = $"Set {Variable} to run this integration test.";
    }
}

/// <summary>
/// Verifies that each configured provider can execute a query against its database.
/// </summary>
[Trait("Category", "Integration")]
public class DatabaseIntegrationTests
{
    const string MsSqlConnection = "TRIPOUS_TEST_MSSQL_CONNECTION_STRING";
    const string OracleConnection = "TRIPOUS_TEST_ORACLE_CONNECTION_STRING";
    const string PostgreSqlConnection = "TRIPOUS_TEST_POSTGRESQL_CONNECTION_STRING";
    const string MySqlConnection = "TRIPOUS_TEST_MYSQL_CONNECTION_STRING";
    const string FirebirdConnection = "TRIPOUS_TEST_FIREBIRD_CONNECTION_STRING";

    // ● private
    /// <summary>
    /// Queries the configured database without reporting connection details in failures.
    /// </summary>
    static void CheckConnection(DbServerType ServerType, string Variable, string Sql)
    {
        long Result;
        try
        {
            var Info = new DbConnectionInfo
            {
                DbServerType = ServerType,
                ConnectionString = Environment.GetEnvironmentVariable(Variable),
                CommandTimeoutSeconds = 10,
            };
            Result = new SqlStore(Info).Int64Result(Sql, -1);
        }
        catch (Exception Error)
        {
            string ErrorCode = Error is DbException DatabaseError ? $" ({DatabaseError.ErrorCode})" : "";
            throw new Xunit.Sdk.XunitException($"{ServerType} query failed using {Variable}: {Error.GetType().Name}{ErrorCode}.");
        }
        Assert.Equal(1L, Result);
    }

    // ● connection tests
    /// <summary>
    /// Executes a scalar query against SQL Server.
    /// </summary>
    [DatabaseFact(MsSqlConnection)]
    public void MsSql() => CheckConnection(DbServerType.MsSql, MsSqlConnection, "SELECT 1");
    /// <summary>
    /// Executes a scalar query against Oracle.
    /// </summary>
    [DatabaseFact(OracleConnection)]
    public void Oracle() => CheckConnection(DbServerType.Oracle, OracleConnection, "SELECT 1 FROM DUAL");
    /// <summary>
    /// Executes a scalar query against PostgreSQL.
    /// </summary>
    [DatabaseFact(PostgreSqlConnection)]
    public void PostgreSql() => CheckConnection(DbServerType.PostgreSql, PostgreSqlConnection, "SELECT 1");
    /// <summary>
    /// Executes a scalar query against MySQL.
    /// </summary>
    [DatabaseFact(MySqlConnection)]
    public void MySql() => CheckConnection(DbServerType.MySql, MySqlConnection, "SELECT 1");
    /// <summary>
    /// Executes a scalar query against Firebird.
    /// </summary>
    [DatabaseFact(FirebirdConnection)]
    public void Firebird() => CheckConnection(DbServerType.Firebird, FirebirdConnection, "SELECT 1 FROM RDB$DATABASE");
}
