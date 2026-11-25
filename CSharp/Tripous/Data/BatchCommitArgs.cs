/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Defines the callbacks and transaction size used by a batch commit.
/// </summary>
public class BatchCommitArgs : EventArgs
{
    // ● constructor
    /// <summary>
    /// Creates batch arguments. AfterFunc is required to provide a termination condition;
    /// when BeforeFunc is null, every iteration requests a post.
    /// </summary>
    public BatchCommitArgs(Func<bool> BeforeFunc, Func<object, bool> AfterFunc, int TransLimit = 300)
    {
        ArgumentNullException.ThrowIfNull(AfterFunc);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(TransLimit);
        this.BeforeFunc = BeforeFunc ?? (() => true);
        this.AfterFunc = AfterFunc;
        this.TransLimit = TransLimit;
    }

    // ● properties
    /// <summary>
    /// Gets the zero-based index of the current iteration, including skipped iterations.
    /// </summary>
    public int Counter { get; internal set; }
    /// <summary>
    /// Gets the number of posts, including posts in a transaction not yet committed.
    /// </summary>
    public int PostCounter { get; internal set; }
    /// <summary>
    /// Gets the maximum number of posts in each transaction.
    /// </summary>
    public int TransLimit { get; }
    /// <summary>
    /// Gets the callback that prepares an iteration and returns whether to post it.
    /// </summary>
    public Func<bool> BeforeFunc { get; }
    /// <summary>
    /// Gets the callback that decides whether to continue after an iteration.
    /// Its argument is the most recently posted item ID, or null if none was posted;
    /// a posted ID does not imply that its transaction has committed.
    /// </summary>
    public Func<object, bool> AfterFunc { get; }
    /// <summary>
    /// Gets or sets an optional value supplied by the caller.
    /// </summary>
    public object Tag { get; set; }
}
