/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Specifies behavior of a data module field.
/// </summary>
[Flags]
public enum FieldFlags
{
    /// <summary>
    /// No optional field flags are set.
    /// </summary>
    None = 0,
    /// <summary>
    /// Excludes the field from generated INSERT and UPDATE statements.
    /// </summary>
    NoInsertUpdate = 0x400,
    /// <summary>
    /// Identifies a field that does not exist in the database table.
    /// </summary>
    Extra = 0x1000,
}
