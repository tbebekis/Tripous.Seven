/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Describes the outcome of a TableSet operation.
/// </summary>
public enum TableSetResultStatus
{
    /// <summary>
    /// The operation completed successfully.
    /// </summary>
    Success,
    /// <summary>
    /// No list SELECT SQL was supplied or configured.
    /// </summary>
    ListSelectSqlEmpty,
    /// <summary>
    /// The list SELECT failed.
    /// </summary>
    ListSelectFailed,
    /// <summary>
    /// The list save was not confirmed.
    /// </summary>
    ListSaveFailed,
    /// <summary>
    /// The supplied item ID is empty or invalid.
    /// </summary>
    InvalidId,
    /// <summary>
    /// No item was found for the supplied ID.
    /// </summary>
    NotFound,
    /// <summary>
    /// The item could not be loaded.
    /// </summary>
    LoadFailed,
    /// <summary>
    /// The database commit was not confirmed.
    /// </summary>
    CommitFailed,
    /// <summary>
    /// The database deletion was not confirmed.
    /// </summary>
    DeleteFailed,
    /// <summary>
    /// The database commit succeeded, but subsequent work failed.
    /// </summary>
    AfterCommitFailed,
}
