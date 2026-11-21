/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Represents a database constraint.
/// </summary>
public class DbMetaConstraint : DbMetaObject
{
    // ● properties
    /// <summary>
    /// Gets or sets the ConstraintTypeText metadata.
    /// </summary>
    public string ConstraintTypeText { get; set; } = "";
    /// <summary>
    /// Gets or sets the ConstraintType metadata.
    /// </summary>
    public ConstraintType ConstraintType { get; set; }
    /// <summary>
    /// Gets or sets the Columns metadata.
    /// </summary>
    public string Columns { get; set; } = "";
    /// <summary>
    /// Gets a descriptive representation of the constraint.
    /// </summary>
    public override string DisplayText => $"{Name}{(string.IsNullOrWhiteSpace(Columns) ? "" : " (" + Columns + ")")}{(string.IsNullOrWhiteSpace(ConstraintTypeText) ? "" : " " + ConstraintTypeText)}";
}
