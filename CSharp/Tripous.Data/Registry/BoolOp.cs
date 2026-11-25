/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Specifies a logical boolean operator.
/// </summary>
public enum BoolOp
{
    /// <summary>
    /// No operator.
    /// </summary>
    None = 0,
    /// <summary>
    /// Logical AND.
    /// </summary>
    And = 1,
    /// <summary>
    /// Logical OR.
    /// </summary>
    Or = 2,
    /// <summary>
    /// Logical AND NOT.
    /// </summary>
    AndNot = 4,
    /// <summary>
    /// Logical OR NOT.
    /// </summary>
    OrNot = 8
}