/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Identifies a database constraint type.
/// </summary>
public enum ConstraintType
{
    /// <summary>
    /// Unknown constraint.
    /// </summary>
    Unknown,
    /// <summary>
    /// PrimaryKey constraint.
    /// </summary>
    PrimaryKey,
    /// <summary>
    /// ForeignKey constraint.
    /// </summary>
    ForeignKey,
    /// <summary>
    /// Unique constraint.
    /// </summary>
    Unique,
    /// <summary>
    /// Check constraint.
    /// </summary>
    Check,
    /// <summary>
    /// NotNull constraint.
    /// </summary>
    NotNull,
}
