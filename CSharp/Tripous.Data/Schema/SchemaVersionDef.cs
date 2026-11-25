/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Defines and registers one version of a database schema.
/// Registration describes changes; it does not execute them.
/// </summary>
public abstract class SchemaVersionDef
{
    // ● protected
    /// <summary>
    /// Populates a new version before it is added to the schema.
    /// </summary>
    protected abstract void RegisterInternal(SchemaVersion Version);

    // ● public
    /// <summary>
    /// Checks the definition independently of schema registration.
    /// </summary>
    public virtual void CheckDescriptor()
    {
        if (VersionNumber <= 0)
            throw new InvalidOperationException("A schema version number must be positive.");
    }
    /// <summary>
    /// Registers this definition once per schema. A different definition or a manually
    /// registered version with the same number is rejected.
    /// </summary>
    public void Register(Schema Schema)
    {
        ArgumentNullException.ThrowIfNull(Schema);
        CheckDescriptor();
        lock (Schema.Versions)
        {
            SchemaVersion Existing = Schema.Find(VersionNumber);
            if (Existing != null)
            {
                if (Existing.DefinitionType == GetType())
                    return;
                throw new InvalidOperationException($"Schema version {VersionNumber} is already registered by another definition.");
            }
            SchemaVersion Version = new() { Version = VersionNumber, DefinitionType = GetType() };
            RegisterInternal(Version);
            Version.CheckDescriptor();
            Schema.Versions.Add(Version);
        }
    }

    // ● properties
    /// <summary>
    /// Gets the positive version number defined by this class.
    /// </summary>
    public abstract int VersionNumber { get; }
}
