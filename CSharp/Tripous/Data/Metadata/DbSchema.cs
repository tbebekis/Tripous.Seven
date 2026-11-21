/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Holds metadata read from one database connection.
/// </summary>
public class DbSchema
{
    // ● constructor
    /// <summary>
    /// Creates a schema metadata container for a connection.
    /// </summary>
    public DbSchema(DbConnectionInfo ConnectionInfo)
    {
        ArgumentNullException.ThrowIfNull(ConnectionInfo);
        this.ConnectionInfo = ConnectionInfo;
    }

    // ● public
    /// <summary>
    /// Loads the metadata if it is not already loaded.
    /// </summary>
    public void Load()
    {
        if (!IsLoaded)
            ReLoad();
    }
    /// <summary>
    /// Reloads the metadata, retaining existing metadata if loading fails.
    /// </summary>
    public void ReLoad()
    {
        DbSchema Loaded = DbSchemaLoader.Load(ConnectionInfo);
        Tables.Clear();
        Tables.AddRange(Loaded.Tables);
        Views.Clear();
        Views.AddRange(Loaded.Views);
        Procedures.Clear();
        Procedures.AddRange(Loaded.Procedures);
        Sequences.Clear();
        Sequences.AddRange(Loaded.Sequences);
        IsLoaded = true;
    }
    /// <summary>
    /// Clears the metadata and marks it as unloaded.
    /// </summary>
    public void UnLoad()
    {
        Tables.Clear();
        Views.Clear();
        Procedures.Clear();
        Sequences.Clear();
        IsLoaded = false;
    }

    // ● properties
    /// <summary>
    /// Gets the connection settings associated with this schema.
    /// </summary>
    public DbConnectionInfo ConnectionInfo { get; }
    /// <summary>
    /// Gets the connection name.
    /// </summary>
    public string Name => ConnectionInfo.Name;
    /// <summary>
    /// Gets the database server type.
    /// </summary>
    public DbServerType DbServerType => ConnectionInfo.DbServerType;
    /// <summary>
    /// Gets whether metadata has been loaded.
    /// </summary>
    public bool IsLoaded { get; internal set; }
    /// <summary>
    /// Gets metadata for tables.
    /// </summary>
    public List<DbMetaTable> Tables { get; } = new();
    /// <summary>
    /// Gets metadata for views.
    /// </summary>
    public List<DbMetaView> Views { get; } = new();
    /// <summary>
    /// Gets metadata for procedures.
    /// </summary>
    public List<DbMetaProcedure> Procedures { get; } = new();
    /// <summary>
    /// Gets metadata for sequences.
    /// </summary>
    public List<DbMetaSequence> Sequences { get; } = new();
}
