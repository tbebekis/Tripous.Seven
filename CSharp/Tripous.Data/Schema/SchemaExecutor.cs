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
/// <para>
/// DDL may commit implicitly on some database servers. If execution fails, earlier
/// structural changes may remain even though the version was not recorded. Statements
/// that may be retried must account for changes already applied.
/// </para>
/// <para>
/// Test each schema migration in a staging environment representative of production
/// before deployment, including recovery from partial failures where applicable.
/// </para>
/// </summary>
static internal class SchemaExecutor
{
    // ● internal
    /// <summary>
    /// Creates tables and views, then runs subsequent structural statements.
    /// The callback runs in the second transaction before it is committed.
    /// </summary>
    /// <remarks>
    /// The two phases commit separately. If the second phase fails, changes committed in
    /// the first phase remain even though the schema version may not have been recorded.
    /// Some database engines also commit DDL implicitly, so changes from a failed phase
    /// may have persisted.
    /// <para>
    /// Rehearse upgrades, partial failures and recovery in staging with representative data
    /// and the same database engine and version as production. Before retrying a failed
    /// upgrade, inspect the actual schema and version record. A retry skips existing tables
    /// and views by name without comparing their definitions, and runs the before and after
    /// statements again. Make those statements safe to rerun where practical, or prepare
    /// explicit recovery steps for changes that can only be applied once.
    /// </para>
    /// </remarks>
    static internal void Execute(SchemaVersion Version, DbConnectionInfo ConnectionInfo, string Domain, Action<DbTransaction> AfterExecute)
    {
        ArgumentNullException.ThrowIfNull(Version);
        ArgumentNullException.ThrowIfNull(ConnectionInfo);
        SqlStore Store = new(ConnectionInfo);
        string Phase = "Create objects";
        string Operation = "Begin transaction";
        string SqlText = null;
        try
        {
            using (SqlTransactionContext Context = Store.BeginTransactionContext())
            {
                foreach (string Statement in Version.StatementsBefore)
                {
                    if (!string.IsNullOrWhiteSpace(Statement))
                    {
                        Operation = "Statement before";
                        SqlText = Statement;
                        Store.ExecSql(Context.Transaction, Statement);
                    }
                }
                foreach (SchemaItem Table in Version.Tables)
                {
                    Operation = $"Check table {Table.Name}";
                    SqlText = null;
                    if (!Store.TableExists(Context.Transaction, Table.Name))
                    {
                        Operation = $"Create table {Table.Name}";
                        SqlText = Table.SqlText;
                        Store.ExecSql(Context.Transaction, Table.SqlText);
                    }
                }
                foreach (SchemaItem View in Version.Views)
                {
                    Operation = $"Check view {View.Name}";
                    SqlText = null;
                    if (!Store.ViewExists(Context.Transaction, View.Name))
                    {
                        Operation = $"Create view {View.Name}";
                        SqlText = View.SqlText;
                        Store.ExecSql(Context.Transaction, View.SqlText);
                    }
                }
                Operation = "Commit transaction";
                SqlText = null;
                Context.Commit();
            }

            Phase = "Subsequent structural statements";
            Operation = "Begin transaction";
            SqlText = null;
            using (SqlTransactionContext Context = Store.BeginTransactionContext())
            {
                foreach (string Statement in Version.StatementsAfter)
                {
                    if (!string.IsNullOrWhiteSpace(Statement))
                    {
                        Operation = "Statement after";
                        SqlText = Statement;
                        Store.ExecSql(Context.Transaction, Statement);
                    }
                }
                Operation = "Record schema version";
                SqlText = null;
                AfterExecute?.Invoke(Context.Transaction);
                Operation = "Commit transaction";
                Context.Commit();
            }
        }
        catch (Exception Error)
        {
            Error.Data["Schema.Domain"] = Domain;
            Error.Data["Schema.Connection"] = ConnectionInfo.Name;
            Error.Data["Schema.Version"] = Version.Version;
            Error.Data["Schema.Phase"] = Phase;
            Error.Data["Schema.Operation"] = Operation;
            if (SqlText != null)
                Error.Data["Schema.SqlText"] = SqlText;
            Error.Data["Schema.Recovery"] = "Earlier structural changes may have been committed; inspect the database before retrying.";
            throw;
        }
    }
}
