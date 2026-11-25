/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Holds the outcome of a TableSet operation.
/// </summary>
public class TableSetResult
{
    // ● constructor
    /// <summary>
    /// Creates an operation result with its status, item ID and error, when available.
    /// </summary>
    public TableSetResult(TableSetResultStatus Status, object Id, Exception Exception, int RowCount = 0)
    {
        this.Status = Status;
        this.Id = Id;
        this.Exception = Exception;
        this.RowCount = RowCount;
    }

    // ● properties
    /// <summary>
    /// Gets the outcome of the operation.
    /// </summary>
    public TableSetResultStatus Status { get; }
    /// <summary>
    /// Gets the item ID, when available.
    /// </summary>
    public object Id { get; }
    /// <summary>
    /// Gets the number of rows loaded by a successful ListSelect.
    /// Its value is not meaningful for other operations or results.
    /// </summary>
    public int RowCount { get; }
    /// <summary>
    /// Gets the error that caused the operation to fail, when available.
    /// </summary>
    public Exception Exception { get; }
}
