/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Carries an active list SELECT filter and its values.
/// </summary>
public class JsonSelectFilter
{
    // ● properties
    /// <summary>Gets or sets the SELECT name.</summary>
    public string SelectName { get; set; }
    /// <summary>Gets or sets the registered filter name.</summary>
    public string Name { get; set; }
    /// <summary>Gets or sets the boolean operator name.</summary>
    public string BoolOp { get; set; }
    /// <summary>Gets or sets the condition operator name.</summary>
    public string ConditionOp { get; set; }
    /// <summary>Gets or sets the first value.</summary>
    public object Value { get; set; }
    /// <summary>Gets or sets the second value for a range.</summary>
    public object Value2 { get; set; }
}
