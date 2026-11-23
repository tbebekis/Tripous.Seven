/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous;

/// <summary>
/// Indicates when an operation occurs relative to another operation.
/// </summary>
[Flags]
public enum ExecTime
{
    /// <summary>
    /// No execution time is specified.
    /// </summary>
    None = 0,
    /// <summary>
    /// Before the operation.
    /// </summary>
    Before = 1,
    /// <summary>
    /// During the operation.
    /// </summary>
    During = 2,
    /// <summary>
    /// After the operation.
    /// </summary>
    After = 4,
}
