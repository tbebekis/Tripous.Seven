/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Holds registered schemas by domain and database connection name.
/// </summary>
static public class Schemas
{
    // ● static public
    /// <summary>
    /// Finds a schema by domain and connection name, ignoring case.
    /// </summary>
    static public Schema Find(string Domain, string ConnectionName)
    {
        if (string.IsNullOrWhiteSpace(Domain) || string.IsNullOrWhiteSpace(ConnectionName))
            return null;
        return List.FirstOrDefault(Item => string.Equals(Item.Domain, Domain, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Item.ConnectionName, ConnectionName, StringComparison.OrdinalIgnoreCase));
    }
    /// <summary>
    /// Returns the matching schema or registers a new one.
    /// </summary>
    static public Schema FindOrAdd(string Domain, string ConnectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(ConnectionName);
        Schema Result = Find(Domain, ConnectionName);
        if (Result == null)
        {
            Result = new Schema { Domain = Domain, ConnectionName = ConnectionName };
            List.Add(Result);
        }
        return Result;
    }
    /// <summary>
    /// Returns whether the matching schema is registered.
    /// </summary>
    static public bool Exists(string Domain, string ConnectionName) => Find(Domain, ConnectionName) != null;

    // ● properties
    /// <summary>
    /// Gets the registered schemas.
    /// </summary>
    static public List<Schema> List { get; } = new();
}
