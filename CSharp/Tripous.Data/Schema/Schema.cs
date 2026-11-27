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
    /// Checks the schema and every registered version without executing SQL.
    /// </summary>
    public virtual void CheckDescriptor()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(ConnectionName);
        HashSet<int> Numbers = new();
        foreach (SchemaVersion Version in Versions)
        {
            if (Version == null)
                throw new InvalidOperationException("A schema version cannot be null.");
            Version.CheckDescriptor();
            if (!Numbers.Add(Version.Version))
                throw new InvalidOperationException($"Schema version {Version.Version} is registered more than once.");
        }
    }
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
    /// <remarks>
    /// Each version runs in two transactions. Some database engines also commit DDL
    /// implicitly. A failure can leave changes in the database before the version
    /// is recorded in SYS_INI.
    /// <para>
    /// Rehearse the upgrade and its recovery in staging with representative production
    /// data and the same database engine and version. If you cannot verify the upgrade
    /// and recovery there, apply the migration manually.
    /// </para>
    /// </remarks>
    public void Execute()
    {
        CheckDescriptor();
        DbConnectionInfo ConnectionInfo = Db.GetConnectionInfo(ConnectionName);
        DbIni Ini = new(ConnectionInfo);
        string Entry = $"Database.Version.{ConnectionInfo.Name}.{Domain}";
        int CurrentVersion = Ini.ReadInteger(Entry, -1);
        foreach (SchemaVersion Version in Versions.OrderBy(Item => Item.Version))
        {
            if (Version.Version > CurrentVersion)
            {
                SchemaExecutor.Execute(Version, ConnectionInfo, Domain,
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
