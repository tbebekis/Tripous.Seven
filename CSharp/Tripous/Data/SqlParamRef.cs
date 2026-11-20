/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Identifies a parameter reference and its position in SQL text.
/// </summary>
public class SqlParamRef
{
    // ● constructor
    /// <summary>
    /// Creates a parameter reference.
    /// </summary>
    public SqlParamRef(string Name, int Index)
    {
        this.Name = Name;
        this.Index = Index;
    }

    // ● properties
    /// <summary>
    /// Gets the parameter name without its prefix.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the index of the prefix in the SQL text.
    /// </summary>
    public int Index { get; }
}
