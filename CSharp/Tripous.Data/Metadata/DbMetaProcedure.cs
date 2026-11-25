/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Represents a database procedure or function.
/// </summary>
public class DbMetaProcedure : DbMetaObject
{
    // ● properties
    /// <summary>
    /// Gets or sets the ProcedureType metadata.
    /// </summary>
    public string ProcedureType { get; set; } = "";
    /// <summary>
    /// Gets a descriptive representation of the procedure.
    /// </summary>
    public override string DisplayText => string.IsNullOrWhiteSpace(ProcedureType) ? Name : $"{Name} ({ProcedureType})";
}
