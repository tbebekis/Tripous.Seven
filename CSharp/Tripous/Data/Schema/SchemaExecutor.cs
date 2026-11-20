/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Executes one schema version in two transaction phases.
/// </summary>
static internal class SchemaExecutor
{
    // ● internal
    /// <summary>
    /// Creates tables and views, then runs subsequent structural statements.
    /// The callback runs in the second transaction before it is committed.
    /// </summary>
    static internal void Execute(SchemaVersion Version, DbConnectionInfo ConnectionInfo, Action<DbTransaction> AfterExecute)
    {
        ArgumentNullException.ThrowIfNull(Version);
        SqlStore Store = new(ConnectionInfo);
        using (SqlTransactionContext Context = Store.BeginTransactionContext())
        {
            foreach (string SqlText in Version.StatementsBefore)
            {
                if (!string.IsNullOrWhiteSpace(SqlText))
                    Store.ExecSql(Context.Transaction, SqlText);
            }
            foreach (SchemaItem Table in Version.Tables)
            {
                if (!Store.TableExists(Context.Transaction, Table.Name))
                    Store.ExecSql(Context.Transaction, Table.SqlText);
            }
            foreach (SchemaItem View in Version.Views)
            {
                if (!Store.ViewExists(Context.Transaction, View.Name))
                    Store.ExecSql(Context.Transaction, View.SqlText);
            }
            Context.Commit();
        }
        using (SqlTransactionContext Context = Store.BeginTransactionContext())
        {
            foreach (string SqlText in Version.StatementsAfter)
            {
                if (!string.IsNullOrWhiteSpace(SqlText))
                    Store.ExecSql(Context.Transaction, SqlText);
            }
            AfterExecute?.Invoke(Context.Transaction);
            Context.Commit();
        }
    }
}
