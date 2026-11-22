/*
 * Tripous.Seven
 * Copyright (c) 2026 Theodoros Bebekis
 *
 * SPDX-License-Identifier: MIT
 * See LICENSE for details.
 */

namespace Tripous.Data;

/// <summary>
/// Specifies optional behavior for a table set.
/// </summary>
[Flags]
public enum TableSetFlags
{
    /// <summary>
    /// No optional behavior is selected.
    /// </summary>
    None = 0,
    /// <summary>
    /// Allows missing SQL statements to be generated from database metadata.
    /// This is a quick alternative to statement generation from definitions.
    /// </summary>
    GenerateSql = 1,
}
