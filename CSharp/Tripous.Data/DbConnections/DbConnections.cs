/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Holds named database connection settings and persists them as JSON.
/// </summary>
public class DbConnections
{
    // ● private
    void CheckNames()
    {
        HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase);
        foreach (DbConnectionInfo Item in List)
        {
            if (Item == null || string.IsNullOrWhiteSpace(Item.Name))
                throw new InvalidOperationException("Every database connection must have a name.");
            if (!Names.Add(Item.Name))
                throw new InvalidOperationException($"Duplicate database connection name: {Item.Name}");
        }
    }

    // ● public
    /// <summary>
    /// Finds a connection by name, ignoring case, or returns null when absent.
    /// </summary>
    public DbConnectionInfo Find(string Name)
    {
        return List.FirstOrDefault(Item => string.Equals(Item.Name, Name, StringComparison.OrdinalIgnoreCase));
    }
    /// <summary>
    /// Returns a named connection or throws when it cannot be found.
    /// </summary>
    public DbConnectionInfo Get(string Name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        return Find(Name) ?? throw new KeyNotFoundException($"Database connection not found: {Name}");
    }
    /// <summary>
    /// Returns whether a connection with the specified name exists.
    /// </summary>
    public bool Contains(string Name) => Find(Name) != null;
    /// <summary>
    /// Adds a named connection and rejects duplicate names regardless of case.
    /// </summary>
    public void Add(DbConnectionInfo Item)
    {
        ArgumentNullException.ThrowIfNull(Item);
        ArgumentException.ThrowIfNullOrWhiteSpace(Item.Name);
        if (Contains(Item.Name))
            throw new ArgumentException($"A database connection named '{Item.Name}' already exists.", nameof(Item));
        List.Add(Item);
    }
    /// <summary>
    /// Removes a connection by name and returns whether it was found.
    /// </summary>
    public bool Remove(string Name)
    {
        DbConnectionInfo Item = Find(Name);
        return Item != null && List.Remove(Item);
    }
    /// <summary>
    /// Saves the list to the configured file in the application folder.
    /// </summary>
    public void Save() => Save(Sys.GetAppFilePath(DbConfig.ConnectionsFileName));
    /// <summary>
    /// Loads the configured file from the application folder if it exists.
    /// </summary>
    public void Load() => Load(Sys.GetAppFilePath(DbConfig.ConnectionsFileName));
    /// <summary>
    /// Saves the current list to a JSON file.
    /// </summary>
    public void Save(string FilePath) => Json.SaveToFile(this, FilePath);
    /// <summary>
    /// Loads named, case-insensitively unique connections from a JSON file.
    /// Keeps the existing list unchanged when validation fails.
    /// </summary>
    public void Load(string FilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(FilePath);
        if (!File.Exists(FilePath))
            return;
        DbConnections Loaded = new();
        Json.LoadFromFile(Loaded, FilePath);
        Loaded.CheckNames();
        List.Clear();
        List.AddRange(Loaded.List);
    }

    // ● properties
    /// <summary>
    /// Gets the connection settings. Changes are saved explicitly.
    /// </summary>
    public List<DbConnectionInfo> List { get; } = new();
}
