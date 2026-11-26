/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Adds common lookup table definitions to a schema version.
/// </summary>
static public class SchemaVersionExtensions
{
    /// <summary>
    /// Registers a lookup table with Id and Name.
    /// </summary>
    static public void AddLookup(this SchemaVersion Version, string TableName, DataFieldType? KeyType = null)
        => AddLookupTable(Version, TableName, WithCode: false, WithIsActive: false, KeyType: KeyType);

    /// <summary>
    /// Registers a lookup table with Id, Code and Name.
    /// </summary>
    static public void AddLookupWithCode(this SchemaVersion Version, string TableName, DataFieldType? KeyType = null)
        => AddLookupTable(Version, TableName, WithCode: true, WithIsActive: false, KeyType: KeyType);

    /// <summary>
    /// Registers a lookup table with Id, Code, Name and IsActive.
    /// </summary>
    static public void AddLookupWithCodeAndIsActive(this SchemaVersion Version, string TableName, DataFieldType? KeyType = null)
        => AddLookupTable(Version, TableName, WithCode: true, WithIsActive: true, KeyType: KeyType);

    /// <summary>
    /// Adds the generated CREATE TABLE statement to the version.
    /// </summary>
    static void AddLookupTable(SchemaVersion Version, string TableName, bool WithCode, bool WithIsActive, DataFieldType? KeyType)
    {
        DataTableDef Table = DataTableDef.CreateLookup(TableName, WithCode, WithIsActive, KeyType);
        Version.Tables.Add(new SchemaItem { Name = Table.Name, SqlText = Table.GetDefText() });
    }
}
