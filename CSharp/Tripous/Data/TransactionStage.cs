/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Indicates a stage of a transaction operation.
/// </summary>
public enum TransactionStage
{
    /// <summary>
    /// The transaction is starting.
    /// </summary>
    Start,
    /// <summary>
    /// Changes are being posted.
    /// </summary>
    Post,
    /// <summary>
    /// The transaction is being committed.
    /// </summary>
    Commit,
    /// <summary>
    /// The transaction is being rolled back.
    /// </summary>
    Rollback,
}
