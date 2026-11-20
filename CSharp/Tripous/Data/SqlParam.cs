/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Represents a named SQL parameter and its value.
/// </summary>
public class SqlParam
{
    // ● constructor
    /// <summary>
    /// Creates a named parameter.
    /// </summary>
    public SqlParam(string Name, object Value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        this.Name = Name;
        this.Value = Value;
    }

    // ● properties
    /// <summary>
    /// Gets the parameter name without its prefix.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the parameter value.
    /// </summary>
    public object Value { get; }
}
