/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Contains named SQL parameter values.
/// </summary>
public class SqlParams
{
    // ● private fields
    readonly List<SqlParam> fItems = new();

    // ● public
    /// <summary>
    /// Adds a parameter; names must be unique regardless of case.
    /// </summary>
    public SqlParams Add(string Name, object Value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        if (fItems.Any(Item => string.Equals(Item.Name, Name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"A parameter named '{Name}' already exists.", nameof(Name));
        fItems.Add(new SqlParam(Name, Value));
        return this;
    }

    // ● properties
    /// <summary>
    /// Gets the number of parameters.
    /// </summary>
    public int Count => fItems.Count;
    /// <summary>
    /// Gets a parameter by its position.
    /// </summary>
    public SqlParam this[int Index] => fItems[Index];
    /// <summary>
    /// Gets the parameters in their insertion order.
    /// </summary>
    public IReadOnlyList<SqlParam> Items => fItems.AsReadOnly();
}
