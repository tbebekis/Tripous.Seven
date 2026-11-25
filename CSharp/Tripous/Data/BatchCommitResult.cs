/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Holds a batch outcome and progress confirmed by committed transactions.
/// These values alone do not guarantee that a batch can safely be retried.
/// </summary>
public class BatchCommitResult
{
    // ● constructor
    /// <summary>
    /// Creates a batch result with its status, confirmed progress and any exception.
    /// </summary>
    public BatchCommitResult(BatchCommitStatus Status, int CommittedCount, int LastCommittedIteration, Exception Exception)
    {
        this.Status = Status;
        this.CommittedCount = CommittedCount;
        this.LastCommittedIteration = LastCommittedIteration;
        this.Exception = Exception;
    }

    // ● properties
    /// <summary>
    /// Gets the outcome of the batch operation.
    /// </summary>
    public BatchCommitStatus Status { get; }
    /// <summary>
    /// Gets the number of posts in confirmed committed transactions.
    /// </summary>
    public int CommittedCount { get; }
    /// <summary>
    /// Gets the zero-based iteration of the last post in a confirmed committed transaction,
    /// or -1 when no post has been confirmed committed.
    /// </summary>
    public int LastCommittedIteration { get; }
    /// <summary>
    /// Gets the exception that caused the operation to fail, when available.
    /// </summary>
    public Exception Exception { get; }
}
