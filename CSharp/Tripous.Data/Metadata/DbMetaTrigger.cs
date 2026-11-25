/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Represents a database trigger.
/// </summary>
public class DbMetaTrigger : DbMetaObject
{
    // ● properties
    /// <summary>
    /// Gets or sets the TableName metadata.
    /// </summary>
    public string TableName { get; set; } = "";
    /// <summary>
    /// Gets or sets the TriggerType metadata.
    /// </summary>
    public string TriggerType { get; set; } = "";
    /// <summary>
    /// Gets a descriptive representation of the trigger.
    /// </summary>
    public override string DisplayText => string.IsNullOrWhiteSpace(TriggerType) ? Name : $"{Name} {TriggerType}";
}
