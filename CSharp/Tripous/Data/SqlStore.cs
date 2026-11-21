/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Executes SQL statements using a copy of a database connection's settings.
/// </summary>
public class SqlStore
{
    // ● private fields
    readonly DbConnectionInfo fConnectionInfo;
    readonly SqlProvider fProvider;

    // ● private
    SqlParams CreateParams(IDictionary<string, object> Values)
    {
        ArgumentNullException.ThrowIfNull(Values);
        SqlParams Result = new();
        foreach (var Item in Values)
            Result.Add(Item.Key, Item.Value);
        return Result;
    }
    SqlParams CreateParams(DataRow Row)
    {
        ArgumentNullException.ThrowIfNull(Row);
        SqlParams Result = new();
        foreach (DataColumn Column in Row.Table.Columns)
            Result.Add(Column.ColumnName, Row[Column]);
        return Result;
    }

    // ● private logging
    /// <summary>
    /// Logs a prepared SQL command.
    /// </summary>
    void LogSql(DbCommand Command)
    {
        // TODO: Log the SQL command.
    }
    /// <summary>
    /// Logs a failed SQL operation.
    /// </summary>
    void Log(Exception Error, string SqlText, SqlParams Params)
    {
        // TODO: Log the exception and operation details.
    }

    // ● constructor
    /// <summary>
    /// Creates a store using a private copy of the connection settings.
    /// </summary>
    public SqlStore(DbConnectionInfo ConnectionInfo)
    {
        ArgumentNullException.ThrowIfNull(ConnectionInfo);
        fConnectionInfo = new DbConnectionInfo
        {
            Name = ConnectionInfo.Name,
            DbServerType = ConnectionInfo.DbServerType,
            ConnectionString = ConnectionInfo.ConnectionString,
            CommandTimeoutSeconds = ConnectionInfo.CommandTimeoutSeconds
        };
        fProvider = SqlProviders.GetSqlProvider(fConnectionInfo.DbServerType);
    }

    // ● public
    /// <summary>
    /// Returns whether a database table with the given name exists.
    /// </summary>
    public bool TableExists(string TableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(TableName);
        return Select(fProvider.TableExistsSql, new SqlParams().Add("TableName", TableName)).Rows.Count > 0;
    }
    /// <summary>
    /// Returns whether a table exists within the caller's transaction.
    /// </summary>
    public bool TableExists(DbTransaction Transaction, string TableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(TableName);
        return Select(Transaction, fProvider.TableExistsSql, new SqlParams().Add("TableName", TableName)).Rows.Count > 0;
    }
    /// <summary>
    /// Returns whether a view exists within the caller's transaction.
    /// </summary>
    public bool ViewExists(DbTransaction Transaction, string ViewName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ViewName);
        return Select(Transaction, fProvider.ViewExistsSql, new SqlParams().Add("ViewName", ViewName)).Rows.Count > 0;
    }
    /// <summary>
    /// Creates and begins a context that owns a connection and transaction.
    /// </summary>
    public SqlTransactionContext BeginTransactionContext()
    {
        SqlTransactionContext Context = new(fProvider.CreateConnection(fConnectionInfo.ConnectionString));
        try
        {
            Context.BeginTransaction();
            return Context;
        }
        catch
        {
            Context.Dispose();
            throw;
        }
    }
    /// <summary>
    /// Loads an existing table with the results of a SELECT.
    /// </summary>
    public int SelectTo(DbTransaction Transaction, DataTable Table, string SqlText) => SelectTo(Transaction, Table, SqlText, fConnectionInfo.CommandTimeoutSeconds, new SqlParams());
    /// <summary>
    /// Loads an existing table with the results of a SELECT.
    /// </summary>
    public int SelectTo(DbTransaction Transaction, DataTable Table, string SqlText, SqlParams Params) => SelectTo(Transaction, Table, SqlText, fConnectionInfo.CommandTimeoutSeconds, Params);
    /// <summary>
    /// Loads an existing table with the results of a SELECT.
    /// </summary>
    public int SelectTo(DbTransaction Transaction, DataTable Table, string SqlText, IDictionary<string, object> Params) => SelectTo(Transaction, Table, SqlText, fConnectionInfo.CommandTimeoutSeconds, CreateParams(Params));
    /// <summary>
    /// Loads an existing table with the results of a SELECT.
    /// </summary>
    public int SelectTo(DbTransaction Transaction, DataTable Table, string SqlText, DataRow Params) => SelectTo(Transaction, Table, SqlText, fConnectionInfo.CommandTimeoutSeconds, CreateParams(Params));
    /// <summary>
    /// Loads an existing table with the results of a SELECT.
    /// </summary>
    public int SelectTo(DbTransaction Transaction, DataTable Table, string SqlText, int CommandTimeout) => SelectTo(Transaction, Table, SqlText, CommandTimeout, new SqlParams());
    /// <summary>
    /// Clears and loads a table inside the supplied transaction.
    /// The caller owns the transaction and its connection.
    /// </summary>
    public int SelectTo(DbTransaction Transaction, DataTable Table, string SqlText, int CommandTimeout, SqlParams Params)
    {
        ArgumentNullException.ThrowIfNull(Transaction);
        ArgumentNullException.ThrowIfNull(Table);
        ArgumentNullException.ThrowIfNull(Params);
        try
        {
            DbConnection Connection = Transaction.Connection
                ?? throw new InvalidOperationException("The transaction has no active connection.");
            using (DbCommand Command = Connection.CreateCommand())
            {
                Command.Transaction = Transaction;
                Command.CommandTimeout = CommandTimeout;
                fProvider.PrepareCommand(Command, SqlText, Params);
                LogSql(Command);
                using (DbDataReader Reader = Command.ExecuteReader())
                {
                    Table.BeginLoadData();
                    try
                    {
                        Table.Clear();
                        Table.Load(Reader);
                    }
                    finally
                    {
                        Table.EndLoadData();
                    }
                    Table.AcceptChanges();
                    return Table.Rows.Count;
                }
            }
        }
        catch (Exception Error)
        {
            Log(Error, SqlText, Params);
            throw;
        }
    }
    /// <summary>
    /// Loads an existing table with the results of a SELECT.
    /// </summary>
    public int SelectTo(DbTransaction Transaction, DataTable Table, string SqlText, int CommandTimeout, IDictionary<string, object> Params) => SelectTo(Transaction, Table, SqlText, CommandTimeout, CreateParams(Params));
    /// <summary>
    /// Loads an existing table with the results of a SELECT.
    /// </summary>
    public int SelectTo(DbTransaction Transaction, DataTable Table, string SqlText, int CommandTimeout, DataRow Params) => SelectTo(Transaction, Table, SqlText, CommandTimeout, CreateParams(Params));

    /// <summary>
    /// Executes a SELECT inside a supplied transaction and returns a new table.
    /// </summary>
    public DataTable Select(DbTransaction Transaction, string SqlText) => Select(Transaction, SqlText, fConnectionInfo.CommandTimeoutSeconds, new SqlParams());
    /// <summary>
    /// Executes a SELECT inside a supplied transaction and returns a new table.
    /// </summary>
    public DataTable Select(DbTransaction Transaction, string SqlText, SqlParams Params) => Select(Transaction, SqlText, fConnectionInfo.CommandTimeoutSeconds, Params);
    /// <summary>
    /// Executes a SELECT inside a supplied transaction and returns a new table.
    /// </summary>
    public DataTable Select(DbTransaction Transaction, string SqlText, IDictionary<string, object> Params) => Select(Transaction, SqlText, fConnectionInfo.CommandTimeoutSeconds, CreateParams(Params));
    /// <summary>
    /// Executes a SELECT inside a supplied transaction and returns a new table.
    /// </summary>
    public DataTable Select(DbTransaction Transaction, string SqlText, DataRow Params) => Select(Transaction, SqlText, fConnectionInfo.CommandTimeoutSeconds, CreateParams(Params));
    /// <summary>
    /// Executes a SELECT inside a supplied transaction and returns a new table.
    /// </summary>
    public DataTable Select(DbTransaction Transaction, string SqlText, int CommandTimeout) => Select(Transaction, SqlText, CommandTimeout, new SqlParams());
    /// <summary>
    /// Executes a SELECT inside a supplied transaction and returns a new table.
    /// </summary>
    public DataTable Select(DbTransaction Transaction, string SqlText, int CommandTimeout, SqlParams Params)
    {
        DataTable Result = new();
        SelectTo(Transaction, Result, SqlText, CommandTimeout, Params);
        return Result;
    }
    /// <summary>
    /// Executes a SELECT inside a supplied transaction and returns a new table.
    /// </summary>
    public DataTable Select(DbTransaction Transaction, string SqlText, int CommandTimeout, IDictionary<string, object> Params) => Select(Transaction, SqlText, CommandTimeout, CreateParams(Params));
    /// <summary>
    /// Executes a SELECT inside a supplied transaction and returns a new table.
    /// </summary>
    public DataTable Select(DbTransaction Transaction, string SqlText, int CommandTimeout, DataRow Params) => Select(Transaction, SqlText, CommandTimeout, CreateParams(Params));

    /// <summary>
    /// Loads an existing table with the results of a SELECT.
    /// </summary>
    public int SelectTo(DataTable Table, string SqlText) => SelectTo(Table, SqlText, fConnectionInfo.CommandTimeoutSeconds, new SqlParams());
    /// <summary>
    /// Loads an existing table with the results of a SELECT.
    /// </summary>
    public int SelectTo(DataTable Table, string SqlText, SqlParams Params) => SelectTo(Table, SqlText, fConnectionInfo.CommandTimeoutSeconds, Params);
    /// <summary>
    /// Loads an existing table with the results of a SELECT.
    /// </summary>
    public int SelectTo(DataTable Table, string SqlText, IDictionary<string, object> Params) => SelectTo(Table, SqlText, fConnectionInfo.CommandTimeoutSeconds, CreateParams(Params));
    /// <summary>
    /// Loads an existing table with the results of a SELECT.
    /// </summary>
    public int SelectTo(DataTable Table, string SqlText, DataRow Params) => SelectTo(Table, SqlText, fConnectionInfo.CommandTimeoutSeconds, CreateParams(Params));
    /// <summary>
    /// Loads an existing table with the results of a SELECT.
    /// </summary>
    public int SelectTo(DataTable Table, string SqlText, int CommandTimeout) => SelectTo(Table, SqlText, CommandTimeout, new SqlParams());
    /// <summary>
    /// Loads a table using a transaction owned by the store.
    /// </summary>
    public int SelectTo(DataTable Table, string SqlText, int CommandTimeout, SqlParams Params)
    {
        using (SqlTransactionContext Context = BeginTransactionContext())
        {
            int Result = SelectTo(Context.Transaction, Table, SqlText, CommandTimeout, Params);
            Context.Commit();
            return Result;
        }
    }
    /// <summary>
    /// Loads an existing table with the results of a SELECT.
    /// </summary>
    public int SelectTo(DataTable Table, string SqlText, int CommandTimeout, IDictionary<string, object> Params) => SelectTo(Table, SqlText, CommandTimeout, CreateParams(Params));
    /// <summary>
    /// Loads an existing table with the results of a SELECT.
    /// </summary>
    public int SelectTo(DataTable Table, string SqlText, int CommandTimeout, DataRow Params) => SelectTo(Table, SqlText, CommandTimeout, CreateParams(Params));

    /// <summary>
    /// Executes a SELECT with its own transaction and returns a new table.
    /// </summary>
    public DataTable Select(string SqlText) => Select(SqlText, fConnectionInfo.CommandTimeoutSeconds, new SqlParams());
    /// <summary>
    /// Executes a SELECT with its own transaction and returns a new table.
    /// </summary>
    public DataTable Select(string SqlText, SqlParams Params) => Select(SqlText, fConnectionInfo.CommandTimeoutSeconds, Params);
    /// <summary>
    /// Executes a SELECT with its own transaction and returns a new table.
    /// </summary>
    public DataTable Select(string SqlText, IDictionary<string, object> Params) => Select(SqlText, fConnectionInfo.CommandTimeoutSeconds, CreateParams(Params));
    /// <summary>
    /// Executes a SELECT with its own transaction and returns a new table.
    /// </summary>
    public DataTable Select(string SqlText, DataRow Params) => Select(SqlText, fConnectionInfo.CommandTimeoutSeconds, CreateParams(Params));
    /// <summary>
    /// Executes a SELECT with its own transaction and returns a new table.
    /// </summary>
    public DataTable Select(string SqlText, int CommandTimeout) => Select(SqlText, CommandTimeout, new SqlParams());
    /// <summary>
    /// Executes a SELECT with its own transaction and returns a new table.
    /// </summary>
    public DataTable Select(string SqlText, int CommandTimeout, SqlParams Params)
    {
        DataTable Result = new();
        SelectTo(Result, SqlText, CommandTimeout, Params);
        return Result;
    }
    /// <summary>
    /// Executes a SELECT with its own transaction and returns a new table.
    /// </summary>
    public DataTable Select(string SqlText, int CommandTimeout, IDictionary<string, object> Params) => Select(SqlText, CommandTimeout, CreateParams(Params));
    /// <summary>
    /// Executes a SELECT with its own transaction and returns a new table.
    /// </summary>
    public DataTable Select(string SqlText, int CommandTimeout, DataRow Params) => Select(SqlText, CommandTimeout, CreateParams(Params));

    /// <summary>
    /// Executes a SQL statement and returns the affected row count.
    /// </summary>
    public int ExecSql(DbTransaction Transaction, string SqlText) => ExecSql(Transaction, SqlText, fConnectionInfo.CommandTimeoutSeconds, new SqlParams());
    /// <summary>
    /// Executes a SQL statement and returns the affected row count.
    /// </summary>
    public int ExecSql(DbTransaction Transaction, string SqlText, SqlParams Params) => ExecSql(Transaction, SqlText, fConnectionInfo.CommandTimeoutSeconds, Params);
    /// <summary>
    /// Executes a SQL statement and returns the affected row count.
    /// </summary>
    public int ExecSql(DbTransaction Transaction, string SqlText, IDictionary<string, object> Params) => ExecSql(Transaction, SqlText, fConnectionInfo.CommandTimeoutSeconds, CreateParams(Params));
    /// <summary>
    /// Executes a SQL statement and returns the affected row count.
    /// </summary>
    public int ExecSql(DbTransaction Transaction, string SqlText, DataRow Params) => ExecSql(Transaction, SqlText, fConnectionInfo.CommandTimeoutSeconds, CreateParams(Params));
    /// <summary>
    /// Executes a SQL statement and returns the affected row count.
    /// </summary>
    public int ExecSql(DbTransaction Transaction, string SqlText, int CommandTimeout) => ExecSql(Transaction, SqlText, CommandTimeout, new SqlParams());
    /// <summary>
    /// Executes a statement in the caller's transaction and returns the affected row count.
    /// The caller owns the transaction and its connection.
    /// </summary>
    public int ExecSql(DbTransaction Transaction, string SqlText, int CommandTimeout, SqlParams Params)
    {
        ArgumentNullException.ThrowIfNull(Transaction);
        ArgumentNullException.ThrowIfNull(Params);
        try
        {
            DbConnection Connection = Transaction.Connection
                ?? throw new InvalidOperationException("The transaction has no active connection.");
            using (DbCommand Command = Connection.CreateCommand())
            {
                Command.Transaction = Transaction;
                Command.CommandTimeout = CommandTimeout;
                fProvider.PrepareCommand(Command, SqlText, Params);
                LogSql(Command);
                return Command.ExecuteNonQuery();
            }
        }
        catch (Exception Error)
        {
            Log(Error, SqlText, Params);
            throw;
        }
    }
    /// <summary>
    /// Executes a SQL statement and returns the affected row count.
    /// </summary>
    public int ExecSql(DbTransaction Transaction, string SqlText, int CommandTimeout, IDictionary<string, object> Params) => ExecSql(Transaction, SqlText, CommandTimeout, CreateParams(Params));
    /// <summary>
    /// Executes a SQL statement and returns the affected row count.
    /// </summary>
    public int ExecSql(DbTransaction Transaction, string SqlText, int CommandTimeout, DataRow Params) => ExecSql(Transaction, SqlText, CommandTimeout, CreateParams(Params));

    /// <summary>
    /// Executes a SQL statement and returns the affected row count.
    /// </summary>
    public int ExecSql(string SqlText) => ExecSql(SqlText, fConnectionInfo.CommandTimeoutSeconds, new SqlParams());
    /// <summary>
    /// Executes a SQL statement and returns the affected row count.
    /// </summary>
    public int ExecSql(string SqlText, SqlParams Params) => ExecSql(SqlText, fConnectionInfo.CommandTimeoutSeconds, Params);
    /// <summary>
    /// Executes a SQL statement and returns the affected row count.
    /// </summary>
    public int ExecSql(string SqlText, IDictionary<string, object> Params) => ExecSql(SqlText, fConnectionInfo.CommandTimeoutSeconds, CreateParams(Params));
    /// <summary>
    /// Executes a SQL statement and returns the affected row count.
    /// </summary>
    public int ExecSql(string SqlText, DataRow Params) => ExecSql(SqlText, fConnectionInfo.CommandTimeoutSeconds, CreateParams(Params));
    /// <summary>
    /// Executes a SQL statement and returns the affected row count.
    /// </summary>
    public int ExecSql(string SqlText, int CommandTimeout) => ExecSql(SqlText, CommandTimeout, new SqlParams());
    /// <summary>
    /// Executes a statement in a store-owned transaction and returns the affected row count.
    /// </summary>
    public int ExecSql(string SqlText, int CommandTimeout, SqlParams Params)
    {
        using (SqlTransactionContext Context = BeginTransactionContext())
        {
            int Result = ExecSql(Context.Transaction, SqlText, CommandTimeout, Params);
            Context.Commit();
            return Result;
        }
    }
    /// <summary>
    /// Executes a SQL statement and returns the affected row count.
    /// </summary>
    public int ExecSql(string SqlText, int CommandTimeout, IDictionary<string, object> Params) => ExecSql(SqlText, CommandTimeout, CreateParams(Params));
    /// <summary>
    /// Executes a SQL statement and returns the affected row count.
    /// </summary>
    public int ExecSql(string SqlText, int CommandTimeout, DataRow Params) => ExecSql(SqlText, CommandTimeout, CreateParams(Params));

    // ● id generation
    /// <summary>
    /// Returns the next ID from the generator associated with a table.
    /// </summary>
    public int NextId(DbTransaction Transaction, string TableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(TableName);
        return NextIdByGenerator(Transaction, "G_" + TableName);
    }
    /// <summary>
    /// Returns the next ID from a named generator in the supplied transaction.
    /// </summary>
    public int NextIdByGenerator(DbTransaction Transaction, string GeneratorName)
    {
        ArgumentNullException.ThrowIfNull(Transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(GeneratorName);
        DataTable Table = Select(Transaction, fProvider.NextIdByGeneratorSql(GeneratorName));
        if (Table.Rows.Count == 0 || Table.Rows[0].IsNull(0))
            throw new InvalidOperationException("The generator did not return an ID.");
        return Convert.ToInt32(Table.Rows[0][0], System.Globalization.CultureInfo.InvariantCulture);
    }
    /// <summary>
    /// Returns the last generated ID from the connection of the supplied transaction.
    /// </summary>
    public int LastId(DbTransaction Transaction, string TableName)
    {
        ArgumentNullException.ThrowIfNull(Transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(TableName);
        DataTable Table = Select(Transaction, fProvider.LastIdSql(TableName));
        if (Table.Rows.Count == 0 || Table.Rows[0].IsNull(0))
            throw new InvalidOperationException("The provider did not return a generated ID.");
        return Convert.ToInt32(Table.Rows[0][0], System.Globalization.CultureInfo.InvariantCulture);
    }
}
