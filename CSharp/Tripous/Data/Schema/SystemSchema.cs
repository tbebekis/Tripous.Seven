/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Defines the versioned Tripous system schema for a database connection.
/// The SYS_INI bootstrap table is created separately by DbIni.
/// </summary>
static public class SystemSchema
{
    // ● static public
    /// <summary>
    /// Registers the system schema versions for a connection. Call Schema.Execute to apply them.
    /// </summary>
    static public Schema Register(string ConnectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ConnectionName);
        Schema Result = Schemas.FindOrAdd("System", ConnectionName);
        new SystemSchemaVersion1().Register(Result);
        return Result;
    }
}
