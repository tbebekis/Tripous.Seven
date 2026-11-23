/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Provides information about a transaction stage notification.
/// </summary>
public class TransactionEventArgs : EventArgs
{
    // ● constructor
    /// <summary>
    /// Creates information about a stage of a database operation.
    /// </summary>
    public TransactionEventArgs(SqlStore Store, DbTransaction Transaction, TransactionStage Stage, ExecTime ExecTime, object RowId)
    {
        this.Store = Store;
        this.Transaction = Transaction;
        this.Stage = Stage;
        this.ExecTime = ExecTime;
        this.RowId = RowId;
    }

    // ● properties
    /// <summary>
    /// Gets the store used by the operation.
    /// </summary>
    public SqlStore Store { get; }
    /// <summary>
    /// Gets the transaction, when one is available at this stage.
    /// </summary>
    public DbTransaction Transaction { get; }
    /// <summary>
    /// Gets the transaction stage.
    /// </summary>
    public TransactionStage Stage { get; }
    /// <summary>
    /// Gets the notification's time relative to the stage.
    /// </summary>
    public ExecTime ExecTime { get; }
    /// <summary>
    /// Gets the row identifier associated with the operation, when available.
    /// </summary>
    public object RowId { get; }
}
