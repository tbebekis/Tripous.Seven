/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes the outcome of a batch commit operation.
/// </summary>
public enum BatchCommitStatus
{
    /// <summary>
    /// The batch completed and every required transaction committed.
    /// </summary>
    Success,
    /// <summary>
    /// The batch failed; any active transaction was confirmed rolled back.
    /// Earlier transactions may already have committed.
    /// </summary>
    Failed,
    /// <summary>
    /// The outcome of the current transaction could not be confirmed.
    /// </summary>
    TransactionOutcomeUnknown,
    /// <summary>
    /// A transaction committed, but immediately subsequent work failed.
    /// </summary>
    AfterCommitFailed,
}
