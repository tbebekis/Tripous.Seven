/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Holds shared database metadata schemas by connection name.
/// </summary>
static public class DbSchemas
{
    // ● private fields
    static readonly Dictionary<string, DbSchema> fItems = new(StringComparer.OrdinalIgnoreCase);

    // ● static public
    /// <summary>
    /// Finds the registered schema for a connection name, or returns null.
    /// </summary>
    static public DbSchema Find(string Name)
    {
        if (string.IsNullOrWhiteSpace(Name))
            return null;
        return fItems.TryGetValue(Name, out DbSchema Result) ? Result : null;
    }
    /// <summary>
    /// Finds or creates a schema for a named connection.
    /// Returns null if the connection or its name is missing.
    /// </summary>
    static public DbSchema FindOrAdd(DbConnectionInfo ConnectionInfo)
    {
        if (ConnectionInfo == null || string.IsNullOrWhiteSpace(ConnectionInfo.Name))
            return null;
        if (!fItems.TryGetValue(ConnectionInfo.Name, out DbSchema Result))
        {
            Result = new DbSchema(ConnectionInfo);
            fItems.Add(ConnectionInfo.Name, Result);
        }
        return Result;
    }
    /// <summary>
    /// Returns the registered schema for a connection name or throws.
    /// </summary>
    static public DbSchema Get(string Name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        return Find(Name) ?? throw new KeyNotFoundException($"Database schema not found for connection: {Name}");
    }
    /// <summary>
    /// Returns the shared schema for a named connection, creating it if necessary.
    /// The first connection instance registered for the name belongs to the schema.
    /// </summary>
    static public DbSchema GetOrAdd(DbConnectionInfo ConnectionInfo)
    {
        ArgumentNullException.ThrowIfNull(ConnectionInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(ConnectionInfo.Name);
        return FindOrAdd(ConnectionInfo);
    }
    /// <summary>
    /// Removes a registered schema by connection name.
    /// </summary>
    static public bool Remove(string Name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        return fItems.Remove(Name);
    }
}
