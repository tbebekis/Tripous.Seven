/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Holds schema versions for a domain and database connection.
/// </summary>
public class Schema
{
    // ● public
    /// <summary>
    /// Finds a version by number, or returns null when it is absent.
    /// </summary>
    public SchemaVersion Find(int Version) => Versions.Find(Item => Item.Version == Version);
    /// <summary>
    /// Returns an existing version or adds a new one.
    /// </summary>
    public SchemaVersion FindOrAdd(int Version)
    {
        SchemaVersion Result = Find(Version);
        if (Result == null)
        {
            Result = new SchemaVersion { Version = Version };
            Versions.Add(Result);
        }
        return Result;
    }
    /// <summary>
    /// Returns whether the specified version is registered.
    /// </summary>
    public bool Exists(int Version) => Find(Version) != null;

    /// <summary>
    /// Executes registered versions newer than the version stored in SYS_INI.
    /// </summary>
    public void Execute()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(ConnectionName);
        DbConnectionInfo ConnectionInfo = Db.GetConnectionInfo(ConnectionName);
        DbIni Ini = new(ConnectionInfo);
        string Entry = $"Database.Version.{ConnectionInfo.Name}.{Domain}";
        int CurrentVersion = Ini.ReadInteger(Entry, -1);
        foreach (SchemaVersion Version in Versions.OrderBy(Item => Item.Version))
        {
            if (Version.Version > CurrentVersion)
            {
                SchemaExecutor.Execute(Version, ConnectionInfo,
                    Transaction => Ini.WriteInteger(Transaction, Entry, Version.Version));
                CurrentVersion = Version.Version;
            }
        }
    }

    // ● properties
    /// <summary>
    /// Gets or sets the schema domain, such as System or Application.
    /// </summary>
    public string Domain { get; set; } = "";
    /// <summary>
    /// Gets or sets the name of the database connection.
    /// </summary>
    public string ConnectionName { get; set; } = "";
    /// <summary>
    /// Gets the registered schema versions.
    /// </summary>
    public List<SchemaVersion> Versions { get; } = new();
}
